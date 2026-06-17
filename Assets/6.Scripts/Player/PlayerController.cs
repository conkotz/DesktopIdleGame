using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(Inventory))]
public class PlayerController : MonoBehaviour
{
    private const string GameplaySceneName = "GamePlay";

    public enum State { Idle, MoveToTarget, MoveToPoint, Gather, MoveToPickup }
    public enum PlayerAction { Idle, Walking, Mining, Woodcutting, Fishing, Fighting, Fatigued }

    [Header("Click To Move")]
    [SerializeField] private bool clickToMoveEnabled = true;

    [SerializeField] private bool movementLocked = false; // runtime lock
    public bool MovementLocked => movementLocked;

    [Header("Playable Strip Gate")]
    [SerializeField] private bool restrictClicksToStrip = true;

    [SerializeField] private string stripCameraName = "StripCamera"; // your scene camera name
    [SerializeField] private string fullCameraName = "FullCamera";
    private Camera _stripCam;
    private Camera _fullWindowCam;

    [Tooltip("Set to Pickup layer")]
    [SerializeField] private LayerMask pickupMask;

    [Tooltip("Set to Enemy layer")]
    [SerializeField] private LayerMask enemyMask;

    [SerializeField] private PlayerCombatController combat;

    private ActionBarUI _actionBarUiCache;

    public bool AnyEnemyOnMap => EnemyBaseController.AliveEnemyCount > 0;

    [Tooltip("Set to Resource nodes / merchant / interactables layer(s)")]
    [SerializeField] private LayerMask interactableMask;

    [SerializeField] private float clickArriveThreshold = 0.05f;
    [Tooltip("Small combat-only arrival epsilon so micro-adjusts still happen to enter attack range.")]
    [SerializeField, Min(0.001f)] private float combatArriveThreshold = 0.005f;

    [Header("Stats")]
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private AilmentController ailments;
    [SerializeField] private PlayerAbilityController abilityController;


    [Header("Pickup")]
    [SerializeField] private float pickupRange = 0.25f;
    private ItemDrop _pickupTarget;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    // Delay between gather animation “swings” while gathering (code-driven; see TickGather).
    [Tooltip("Seconds between gather swings. If your Animator transitions gather→idle on exit time, only this interval should retrigger gather — not Reassert.")]
    [SerializeField] private float gatherAnimDelaySeconds = 3f;
    private float _nextGatherAnimTime;

    [Header("Animator State Names (exact, case-sensitive)")]
    [SerializeField] private string idleStateName = "idle";
    [SerializeField] private string walkStateName = "walk";
    [SerializeField] private string gatherStateName = "gather";


    [Header("Combat Animation")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string rangedAttackTriggerName = "RangedAttack";
    [SerializeField] private string magicAttackTriggerName = "MagicAttack";

    private bool _attackLocked;
    private float _attackUnlockTime;

    [Header("Hurt / Death Animation")]
    [SerializeField] private string hurtTriggerName = "Hurt";
    [SerializeField] private string dieTriggerName = "Die";
    [SerializeField] private string dieStateName = "die";     // state name in animator (case-sensitive)
    [SerializeField] private float dieDisableDelay = 0f;      // 0 = auto detect, otherwise seconds

    private bool _isDead;
    private Coroutine _deathRoutine;
    private Coroutine _deathPoseRoutine;
    private Coroutine _deathRespawnRoutine;
    private MapNodeDefinition _pendingDeathRespawnNode;
    private bool _deathRespawnHere;

    [Header("Death Respawn Popup (optional; auto-resolved by name if empty)")]
    [SerializeField] private GameObject deathPopupWindow;
    [SerializeField] private TMP_Text deathPopupNotificationText;
    [SerializeField] private Button deathPopupButton;
    [SerializeField] private TMP_Text deathPopupButtonLabel;
    [SerializeField, Min(0f)] private float deathPopupDelaySeconds = 1f;
    [SerializeField, Min(0.01f)] private float deathRespawnFadeSeconds = 0.35f;

    [Header("Overflow Drops (World)")]
    [SerializeField] private bool dropOverflowToGround = true;
    [SerializeField] private ItemDrop worldDropPrefab; // legacy fallback (prefer DropManager)
    [SerializeField] private float dropScatterRadius = 0.15f;

    [Header("Debug")]
    [SerializeField] private State state = State.Idle;

    [Header("Sprite Flip")]
    [SerializeField] private Transform visualsRoot;   // drag Player/Visuals here
    [SerializeField] private bool invertFlip = true;
    [SerializeField] private float flipDeadzone = 0.0005f;
    private float _lastX;
    public float FacingDirectionX { get; private set; } = 1f;

    private bool _keyboardManualMoveThisFrame;
    private float _keyboardSteerDir;
    private bool _keyboardSteerNotified;
    private bool _moveToPointFromPlayerInput;
    public bool IsManualKeyboardSteering => _keyboardManualMoveThisFrame;
    public bool IsPerformingAttackAnimation => _attackLocked;
    /// <summary>Keyboard hold or player click-to-move while repositioning in combat.</summary>
    public bool IsPlayerSteeringMovement =>
        _keyboardManualMoveThisFrame ||
        (_moveToPointFromPlayerInput && state == State.MoveToPoint);

    /// <summary>True when recent horizontal motion is away from the current combat target.</summary>
    public bool IsMovingAwayFromCombatTarget() => IsPlayerMovingAwayFromCombatTarget();

    /// <summary>
    /// True while the player is actively retreating from the current combat target via input or motion.
    /// Auto attacks should pause until this returns false (player standing still again).
    /// </summary>
    public bool IsPlayerMovingAwayFromCombatTarget()
    {
        if (combat == null)
            return false;

        EnemyBaseController target = combat.CurrentTarget;
        if (target == null || target.IsDead)
            return false;

        float myX = transform.position.x;
        float targetX = target.transform.position.x;

        if (IsKeyboardMoveLeftHeld() || IsKeyboardMoveRightHeld())
        {
            float dir = 0f;
            if (IsKeyboardMoveLeftHeld())
                dir -= 1f;
            if (IsKeyboardMoveRightHeld())
                dir += 1f;

            if (dir > 0f && targetX < myX || dir < 0f && targetX > myX)
                return true;
        }

        float dx = myX - _lastX;
        if (Mathf.Abs(dx) > flipDeadzone && (dx > 0f && targetX < myX || dx < 0f && targetX > myX))
            return true;

        if (state == State.MoveToPoint && _moveToPointFromPlayerInput)
        {
            float gapNow = Mathf.Abs(myX - targetX);
            float gapAtDest = Mathf.Abs(moveTargetX - targetX);
            if (gapAtDest > gapNow + flipDeadzone && Mathf.Abs(moveTargetX - myX) > flipDeadzone)
                return true;
        }

        return false;
    }

    private Rigidbody2D _rb;
    private Collider2D _groundAlignCollider;

    private bool _suppressSpriteFlipForTeleport;
    private bool _teleportDamageImmune;

    /// <summary>Lets <see cref="PlayerLevelTransition"/> own visuals scale during a level change.</summary>
    public void SetTeleportOutVisualsActive(bool active) => _suppressSpriteFlipForTeleport = active;

    /// <summary>Scene transition / spawn — when true, <see cref="TakeDamage"/> and DoTs are ignored.</summary>
    public bool TeleportDamageImmune => _teleportDamageImmune;

    /// <inheritdoc cref="TeleportDamageImmune"/>
    public void SetTeleportDamageImmune(bool immune) => _teleportDamageImmune = immune;

    [Header("Summons")]
    [Tooltip("Home anchor for Soulforged Weapon (world position + rotation). If unset, Awake resolves SoulforgedWeaponSpawnPoint under Sprite Flip → Visuals Root (same hierarchy as ranged/magic spawn points), then a direct child of this object, else player root.")]
    [SerializeField, FormerlySerializedAs("spectralWeaponHomeAnchor")]
    private Transform spectralWeaponSpawnPoint;

    private bool _warnedSoulforgedWeaponSpawnPointOnce;

    /// <summary>
    /// Home anchor for Soulforged Weapon summons (under visuals root with other weapon spawn points when auto-resolved). Falls back to player root if missing.
    /// </summary>
    public Transform SoulforgedWeaponSpawnPoint => spectralWeaponSpawnPoint ? spectralWeaponSpawnPoint : transform;

    private void ResolveSoulforgedWeaponSpawnPoint()
    {
        if (spectralWeaponSpawnPoint)
            return;

        // Same pattern as PlayerCombatController projectile anchors: child of visuals root (e.g. Soldier) so it moves/flips with the rig.
        if (visualsRoot)
        {
            Transform underVisuals = visualsRoot.Find("SoulforgedWeaponSpawnPoint");
            if (underVisuals)
            {
                spectralWeaponSpawnPoint = underVisuals;
                return;
            }
        }

        Transform t = transform.Find("SoulforgedWeaponSpawnPoint");
        if (t)
        {
            spectralWeaponSpawnPoint = t;
            return;
        }

        if (!_warnedSoulforgedWeaponSpawnPointOnce)
        {
            Debug.LogWarning(
                "[Player] SoulforgedWeaponSpawnPoint: assign in inspector, or add SoulforgedWeaponSpawnPoint under Sprite Flip → Visuals Root, or as a direct child of the player. Using player transform for summon home.",
                this);
            _warnedSoulforgedWeaponSpawnPointOnce = true;
        }
    }

    /// <summary>World position for Soulforged Weapon idle/home (see <see cref="SoulforgedWeaponSpawnPoint"/>).</summary>
    public Vector3 GetSoulforgedWeaponHomeWorldPosition() => SoulforgedWeaponSpawnPoint.position;

    [Header("Popup (reserved; messages go to activity log only)")]
    [SerializeField] private GameObject actionPopup;

    private const float RepeatedPopupActivityLogIntervalSeconds = 5f;
    /// <summary>While true, duplicate "Food on cooldown …" lines are suppressed until latch resets.</summary>
    private bool _foodConsumableUnusableActivityLogged;
    /// <summary>While true, duplicate "Potions on cooldown …" lines are suppressed until latch resets.</summary>
    private bool _potionConsumableUnusableActivityLogged;
    private readonly Dictionary<string, float> _nextActivityLogTimeByPopupMessage = new(StringComparer.OrdinalIgnoreCase);

    [Header("Tools Required (Gather)")]
    [SerializeField] private bool requireToolForGathering = true;

    private bool _hideHandsForUnarmedGather;

    [Header("Gather Without Tool (Debuff)")]
    [SerializeField] private bool allowGatherWithoutTool = true;

    [Tooltip("Multiplier applied when missing the required tool. 0.3 = 70% slower.")]
    [SerializeField, Range(0.05f, 1f)] private float missingToolSpeedMultiplier = 0.2f;

    [SerializeField] private PlayerCombatState combatState;
    public bool InCombat => combatState && combatState.InCombat;

    /// <summary>True when an enemy is in engage range; use for combat logic that must not treat soft combat as proximity.</summary>
    public bool HasEnemyProximityEngagement => combatState && combatState.HasEnemyProximityEngagement;

    /// <summary>Marks the player as in combat for a short window after dealing or taking damage (ranged abilities, etc.).</summary>
    public void NotifySoftCombatInteraction(float keepAliveSeconds)
    {
        if (combatState)
            combatState.NotifySoftCombat(keepAliveSeconds);
    }

    private float _gatherSpeedMultiplier = 1f;
    private float _gatherGritChance;
    private float _gatherBonusFindChance;
    private float _gatherStaminaEfficiency;
    private float _activeFishingBaitSpeedBonusFraction;
    private WoodcuttingRuntimeBonuses _woodcuttingBonuses;
    private float _woodcuttingContinuousGatherSeconds;
    private float _woodcuttingFrenzyUntil;
    private float _woodcuttingFlowLingerUntil;

    private FishingRuntimeBonuses _fishingBonuses;
    private float _fishingContinuousGatherSeconds;
    private float _fishingFrenzyUntil;

    /// <summary>Lv15 Calm Waters (major): stacks built every 4s while fishing; +2% speed / +1% bonus find per stack.</summary>
    private int _fishingCalmWatersMajorStacks;
    private float _fishingCalmWatersMajorStackTimer;
    /// <summary>Lasting Waters: Calm stacks that decay 1 per 2s after fishing stops until the next gather merges them.</summary>
    private int _fishingCalmWatersLingerStacks;
    private float _fishingCalmWatersNextLingerDecayAt;

    private PlayerBuffController _buffControllerCache;
    private bool _woodcuttingFlowStateHudRegistered;
    private bool _fishingCalmWatersMajorHudRegistered;
    private int _fishingCalmWatersMajorHudLastStacks = int.MinValue;

    private const float WoodcuttingForestFlowContinuousSecondsThreshold = 15f;
    private const float FishingCalmWatersContinuousSecondsThreshold = 15f;
    private const float FishingGritFrenzyDurationSeconds = 7f;
    private static float FishingCalmWatersMajorStackIntervalSeconds =>
        GatheringPassiveTooltipText.FishingCalmWatersMajorStackIntervalSeconds;
    private static float FishingCalmWatersLingerDecayIntervalSeconds =>
        GatheringPassiveTooltipText.FishingCalmWatersMajorLingerDecaySeconds;

    /// <summary>HUD buff strip id for Lv15 Flow State (assign icon on the Buffs / Debuffs panel).</summary>
    public const string WoodcuttingFlowStateHudBuffId = "woodcutting_flow_state";
    /// <summary>HUD buff strip id for Lv15 Calm Waters major (stack count = Calm stacks).</summary>
    public const string FishingCalmWatersMajorHudBuffId = "fishing_calm_waters_major";
    public const int WoodcuttingMajorPassiveSourceLevel = 15;
    public const int FishingMajorPassiveSourceLevel = 15;
    public const int WoodcuttingLv35MajorPassiveSourceLevel = 35;
    /// <summary>Woodcutting skill level at which the capstone passive unlocks (see woodcutting skill tree).</summary>
    public const int WoodcuttingCapstonePassiveRequiredLevel = 50;

    /// <summary>Spine id segment for the Lv15 ability row pick (0–2) used with <see cref="SkillsManager.GetSkillChoiceSelection"/> string overload.</summary>
    public static string WoodcuttingLevel15ChoiceSpineId(int abilityRowPick) =>
        $"Lv{WoodcuttingMajorPassiveSourceLevel}_{Mathf.Clamp(abilityRowPick, 0, 2)}";

    /// <summary>Spine id segment for Fishing Lv15 major-passive row pick (0–2).</summary>
    public static string FishingLevel15ChoiceSpineId(int abilityRowPick) =>
        $"Lv{FishingMajorPassiveSourceLevel}_{Mathf.Clamp(abilityRowPick, 0, 2)}";

    /// <summary>Spine id segment for the Lv35 major-passive row pick (0–1) used with <see cref="SkillsManager.GetSkillChoiceSelection"/> string overload.</summary>
    public static string WoodcuttingLevel35ChoiceSpineId(int abilityRowPick) =>
        $"Lv{WoodcuttingLv35MajorPassiveSourceLevel}_{Mathf.Clamp(abilityRowPick, 0, 1)}";
    private const float WoodcuttingFrenzyDurationSeconds = 7f;

    private struct WoodcuttingRuntimeBonuses
    {
        public float extraLogChance;
        public float baseYieldPercent;
        /// <summary>Total fraction of max stamina restored on Woodcutting Grit proc.</summary>
        public float gritProcRestoreStaminaFraction;
        public float bonusXpChance;
        public float noStaminaSwingChance;
        public int forestFlowStacks;
        public int frenzyStacks;
    }

    private struct FishingRuntimeBonuses
    {
        public int calmWatersStacks;
        public int frenzyStacks;
    }

    [Header("Future Stamina Integration")]
    [Tooltip("Base stamina cost per successful gather tick. Hook to stamina system later.")]
    [SerializeField] private float baseGatherStaminaCostPerTick = 10f;

    [Header("Gather Energy Cost")]
    [SerializeField] private string lowEnergyPopupText = "* Fatigued *";
    [SerializeField, Min(0.1f)] private float fatiguedResumeDelaySeconds = 3f;

    [Header("Endurance XP (Defence)")]
    [Tooltip("Pre-mitigation damage per 1 Endurance XP. Example: 10 means 10 damage = 1 XP.")]
    [SerializeField, Min(0.01f)] private float enduranceDamagePerXp = 10f;

    /// <summary>Endurance XP awarded per point of pre-mitigation damage taken (e.g. 1 / damagePerXp).</summary>
    public float EnduranceXpPerDamage => 1f / Mathf.Max(0.01f, enduranceDamagePerXp);

    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private ToolbeltManager toolbelt;

    private bool _pausedForInvFull;
    private ResourceNode _pausedNode;

    private LaneBounds laneBounds;
    private const float WorldBoundsXPadding = 0.5f;
    private Inventory inventory;
    private ResourceNode targetNode;

    private float _accumItems;
    private float _gatherTimer;
    private float _nextGatherInterval;

    private PlayerAction _action = PlayerAction.Idle;
    private float moveTargetX;
    private Camera _cam;

    private string _gatherToolItemId;
    private Coroutine _fatiguedResumeRoutine;
    private float _fatiguedUntilTime;

    /// <summary>When fatigued mid-gather, we keep random-interval / rate progress so resume does not reset the ore timer.</summary>
    private bool _fatigueGatherProgressPending;
    private ResourceNode _fatigueGatherSavedNode;
    private float _fatigueGatherSavedTimer;
    private float _fatigueGatherSavedInterval;
    private float _fatigueGatherSavedAccum;

    private readonly List<Drop> _drops = new List<Drop>(8);
    private readonly Dictionary<string, int> _mainYieldScratch = new();
    private readonly List<int> _fishingXpScratch = new(8);
    private const string MissingFishingBaitPopupText = "Cannot fish due to no bait in inventory";

    private enum GatherSwingSpendFailureReason
    {
        None = 0,
        LowEnergy = 1,
        MissingFishingBait = 2
    }

    private GatherSwingSpendFailureReason _lastGatherSwingSpendFailureReason;

    public ResourceNode CurrentTarget => targetNode;
    public PlayerAction CurrentAction => _action;

    public event Action<PlayerAction> OnActionChanged;
    public event Action<bool, float> OnGatherDebuffChanged;

    [SerializeField] private float reassertCooldown = 0.08f;
    private float _nextReassertTime;
    private float _locomotionSampleStartX;
    private const float LocomotionMotionEpsilon = 0.0008f;


    private bool _hasActionOverride = false;
    private PlayerAction _actionOverride;

    public string displayName => characterStats ? characterStats.UnitDisplayName : "Adventurer";
    public float HP => characterStats ? characterStats.HP : 0f;
    public float MaxHP => characterStats ? characterStats.MaxHP : 0f;
    public float Energy => characterStats ? characterStats.Energy : 0f;
    public float MaxEnergy => characterStats ? characterStats.MaxEnergy : 0f;
    public float Mana => characterStats ? characterStats.Mana : 0f;
    public float MaxMana => characterStats ? characterStats.MaxMana : 0f;

    public event Action<float, float> OnHPChanged;
    public event Action<float, float> OnEnergyChanged;
    public event Action<float, float> OnManaChanged;
    public event Action<string> OnNameChanged;

    public bool IsDead => _isDead;

    // -------------------------
    // Unity lifecycle
    // -------------------------

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        CombatPlayerRefs.Register(this);
        WorldFloorFollowerRegistry.Register(transform, WorldFloorFollowerRegistry.Category.Actor);
        RebindCameras();

        if (!characterStats)
            characterStats = GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            characterStats.OnHPChanged += HandleStatsHpChanged;
            characterStats.OnEnergyChanged += HandleStatsEnergyChanged;
            characterStats.OnManaChanged += HandleStatsManaChanged;
            characterStats.OnNameChanged += HandleStatsNameChanged;
            characterStats.OnDied += HandleStatsDied;
        }

        if (!inventory) inventory = GetComponent<Inventory>();
        if (inventory == null) return;

        if (!equipment)
            equipment = GetComponent<EquipmentManager>();

        if (!characterStats)
            characterStats = GetComponent<CharacterStats>();

        // equipment?.ForceUnequipAll(); // (leave as your choice)

        if (!toolbelt)
            toolbelt = GetComponent<ToolbeltManager>();

        if (toolbelt != null)
            toolbelt.OnToolSlotChanged += HandleToolSlotChanged;

        if (!toolbelt)
            Debug.LogError("[Toolbelt] No ToolbeltManager on Player. Add it to Player to avoid duplicate instance bugs.", this);

        if (actionPopup)
            actionPopup.SetActive(false);

        if (!combat)
            combat = GetComponent<PlayerCombatController>();

        inventory.OnInventoryFull += HandleInventoryFull;
        inventory.OnInventoryChanged += HandleInventoryChanged;

        if(equipment != null)
{
            equipment.OnMainHandChanged += HandleEquipmentChanged;
            equipment.OnOffHandChanged += HandleEquipmentChanged;

            // ✅ NEW: fires for Helmet/Body/Boots/Trinket/Pendant/Ring1/Ring2 etc
            equipment.OnUISlotChanged += HandleAnyUISlotChanged;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        CombatPlayerRefs.Unregister(this);
        WorldFloorFollowerRegistry.Unregister(transform);

        if (characterStats != null)
        {
            characterStats.OnHPChanged -= HandleStatsHpChanged;
            characterStats.OnEnergyChanged -= HandleStatsEnergyChanged;
            characterStats.OnManaChanged -= HandleStatsManaChanged;
            characterStats.OnNameChanged -= HandleStatsNameChanged;
            characterStats.OnDied -= HandleStatsDied;
        }

        if (inventory == null) return;

        if (toolbelt != null)
            toolbelt.OnToolSlotChanged -= HandleToolSlotChanged;

        inventory.OnInventoryFull -= HandleInventoryFull;
        inventory.OnInventoryChanged -= HandleInventoryChanged;

        if (equipment != null)
        {
            equipment.OnMainHandChanged -= HandleEquipmentChanged;
            equipment.OnOffHandChanged -= HandleEquipmentChanged;

            // ✅ NEW
            equipment.OnUISlotChanged -= HandleAnyUISlotChanged;
        }
    }

    private void Awake()
    {
        if (!combatState) combatState = GetComponent<PlayerCombatState>();
        if (!ailments) ailments = GetComponent<AilmentController>();
        if (!abilityController) abilityController = GetComponent<PlayerAbilityController>();

        inventory = GetComponent<Inventory>();
        laneBounds = FindFirstObjectByType<LaneBounds>();
        _cam = Camera.main;

        if (!visualsRoot)
        {
            var visualOffset = transform.Find("Visuals/VisualOffset");
            visualsRoot = visualOffset ? visualOffset : (transform.Find("Visuals") ? transform.Find("Visuals") : transform);
        }

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        _rb = GetComponent<Rigidbody2D>();
        _groundAlignCollider = ResolveGroundAlignCollider();

        if (actionPopup)
            actionPopup.SetActive(false);

        ResolveSoulforgedWeaponSpawnPoint();
    }

    private void Start()
    {
        StartCoroutine(InitAnimNextFrame());
        if (characterStats != null)
        {
            OnNameChanged?.Invoke(characterStats.UnitDisplayName);
            OnHPChanged?.Invoke(characterStats.HP, characterStats.MaxHP);
            OnEnergyChanged?.Invoke(characterStats.Energy, characterStats.MaxEnergy);
            OnManaChanged?.Invoke(characterStats.Mana, characterStats.MaxMana);
            if (SceneManager.GetActiveScene().name == "GamePlay")
                characterStats.SnapGuardToNaturalCapOnSessionLoad();
        }
    }

    private IEnumerator InitAnimNextFrame()
    {
        yield return null;
        SetAction(PlayerAction.Idle, true);
    }

    private void Update()
    {
        if (_isDead)
            return;

        _locomotionSampleStartX = transform.position.x;

        PlayerSprintInput.PollSprintKey();

        if (_attackLocked && Time.time >= _attackUnlockTime)
        {
            _attackLocked = false;
        }

        if (clickToMoveEnabled)
            HandleClickToMove();

        PollKeyboardSteeringInput();
        TryInteractHotkey();
        TryEnterAreaHotkey();

        TickStateMachine();
        ApplyKeyboardMovementDelta();
        ApplyActionPresentation();

        UpdateSpriteFlip();
        characterStats?.TickRegen(Time.deltaTime);
        SyncWoodcuttingFlowStateHudBuffIfNeeded();
        TickFishingCalmWatersLingerDecay();
        SyncFishingCalmWatersMajorHudBuffIfNeeded();
    }

    private void TickStateMachine()
    {
        switch (state)
        {
            case State.Idle:
                break;
            case State.MoveToTarget:
                TickMoveToTarget();
                break;
            case State.MoveToPoint:
                TickMoveToPoint();
                break;
            case State.Gather:
                {
                    // actually collect resources
                    TickGather();                 
                    // keep the correct gather animation/action
                    var ga = GetGatherAction();
                    if (_action != ga) SetAction(ga, true);

                    break;
                }
            case State.MoveToPickup:
                TickMoveToPickup();
                break;
        }
    }

    private void HandleStatsHpChanged(float current, float max)
    {
        OnHPChanged?.Invoke(current, max);
    }

    private void HandleStatsEnergyChanged(float current, float max)
    {
        OnEnergyChanged?.Invoke(current, max);
    }

    private void HandleStatsManaChanged(float current, float max)
    {
        OnManaChanged?.Invoke(current, max);
    }

    private void HandleStatsNameChanged(string newName)
    {
        OnNameChanged?.Invoke(newName);
    }

    private void HandleStatsDied()
    {
        if (abilityController != null && abilityController.TryTriggerPhoenixAshenRebirth())
            return;

        Die();
    }

    private void HandleEquipmentChanged(string _) 
    { 
        RecalculateMaxVitalsFromGear(); 
    }

    private void HandleAnyUISlotChanged(EquipmentUISlotType slot, string itemId)
    {
        RecalculateMaxVitalsFromGear();
    }

    private void ApplyActionPresentation()
    {
        bool isLocomoting = ShouldPresentWalkingLocomotion();

        bool isGathering =
            state == State.Gather;

        bool hasLiveCombatTarget =
            combat != null &&
            combat.CurrentTarget != null &&
            !combat.CurrentTarget.IsDead;

        // Fighting only after combat has started (swing / soft-combat), not merely because a target is selected.
        bool shouldShowFighting =
            !isLocomoting &&
            !isGathering &&
            (_attackLocked || InCombat);

        // Steady locomotion: skip SetAction spam, but still recover walk if hurt/combat kicked the Animator to idle.
        if (isLocomoting &&
            !shouldShowFighting &&
            _action == PlayerAction.Walking)
        {
            EnsureWalkAnimatorDuringLocomotion();
            return;
        }

        if (shouldShowFighting)
        {
            SetAction(PlayerAction.Fighting);
        }
        else if (_keyboardManualMoveThisFrame)
        {
            SetAction(PlayerAction.Walking);
        }
        else
        {
            switch (state)
            {
                case State.Idle:
                    if (Time.time < _fatiguedUntilTime)
                        SetAction(PlayerAction.Fatigued);
                    else
                        SetAction(PlayerAction.Idle);
                    break;

                case State.MoveToTarget:
                case State.MoveToPoint:
                case State.MoveToPickup:
                    if (isLocomoting)
                        SetAction(PlayerAction.Walking);
                    else if (hasLiveCombatTarget)
                        SetAction(PlayerAction.Fighting);
                    else
                        SetAction(PlayerAction.Idle);
                    break;

                case State.Gather:
                    SetAction(GetGatherAction());
                    break;
            }
        }
    }

    // -------------------------
    // Anim triggers
    // -------------------------

    private string _currentStateName;

    private void PlayState(string stateName, bool restart)
    {
        if (!animator) return;
        if (string.IsNullOrWhiteSpace(stateName)) return;

        // If already in the same state, don’t spam Play() unless restarting
        if (!restart && _currentStateName == stateName)
        {
            // Guard against stale cached state name after trigger-based attacks:
            // only skip if animator is truly in that state right now.
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(stateName))
                return;
        }

        // If you want a safety check:
        // if (!animator.HasState(0, Animator.StringToHash(stateName))) return; // optional

        float t = restart ? 0f : animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        animator.Play(stateName, 0, t);

        _currentStateName = stateName;
    }
    public void TriggerAttackAnim()
    {
        if (!animator) return;

        string triggerToUse = GetAttackTriggerName();
        float duration = Mathf.Clamp(GetAttackClipLength(), 0.05f, 2.0f);

        _attackLocked = true;
        _attackUnlockTime = Time.time + duration;
        TryFaceCombatTargetDuringAttack();

        SetActionOverride(PlayerAction.Fighting);
        ClearFightingOverrideSoon(duration);

        animator.ResetTrigger(attackTriggerName);
        animator.ResetTrigger(rangedAttackTriggerName);
        animator.ResetTrigger(magicAttackTriggerName);

        animator.SetTrigger(triggerToUse);
    }

    /// <summary>
    /// Replays the attack animation trigger without touching attack locks/action override.
    /// Use for cosmetic follow-up swings that must not affect normal attack cadence.
    /// </summary>
    public void TriggerAttackAnimVisualOnly()
    {
        if (!animator) return;

        string triggerToUse = GetAttackTriggerName();

        animator.ResetTrigger(attackTriggerName);
        animator.ResetTrigger(rangedAttackTriggerName);
        animator.ResetTrigger(magicAttackTriggerName);

        animator.SetTrigger(triggerToUse);

        // Visual-only casts don't participate in attack lock/override flow,
        // so force a small recovery back to locomotion/idle after the clip window.
        float duration = Mathf.Clamp(GetAttackClipLength(), 0.05f, 2.0f);
        if (_visualOnlyAttackRecoverRoutine != null)
            StopCoroutine(_visualOnlyAttackRecoverRoutine);
        _visualOnlyAttackRecoverRoutine = StartCoroutine(RecoverFromVisualOnlyAttack(duration));
    }

    private Coroutine _visualOnlyAttackRecoverRoutine;

    private IEnumerator RecoverFromVisualOnlyAttack(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        // Don't interfere with normal locked attacks.
        if (_attackLocked)
        {
            _visualOnlyAttackRecoverRoutine = null;
            yield break;
        }

        bool isMoving =
            state == State.MoveToPoint ||
            state == State.MoveToTarget ||
            state == State.MoveToPickup;

        if (isMoving)
        {
            SetAction(PlayerAction.Walking, true);
            PlayState(walkStateName, restart: false);
        }
        else if (state == State.Gather)
        {
            SetAction(GetGatherAction(), true);
        }
        else if (state == State.Idle)
        {
            SetAction(PlayerAction.Idle, true);
            PlayState(idleStateName, restart: false);
        }

        _visualOnlyAttackRecoverRoutine = null;
    }

    private string GetAttackTriggerName()
    {
        if (!characterStats)
            return attackTriggerName;

        return characterStats.CurrentAttackSkill switch
        {
            AttackSkill.Ranged => rangedAttackTriggerName,
            AttackSkill.Magic => magicAttackTriggerName,
            _ => attackTriggerName
        };
    }

    private void TriggerHurtAnim()
    {
        if (!animator || _isDead)
            return;

        // Let attack clips finish; hurt must not cut them short.
        if (_attackLocked)
            return;

        // Never interrupt locomotion — hurt→idle while the transform keeps moving causes sliding.
        if (IsLocomotionPresentationActive())
            return;

        animator.ResetTrigger(attackTriggerName);
        animator.ResetTrigger(rangedAttackTriggerName);
        animator.ResetTrigger(magicAttackTriggerName);
        animator.ResetTrigger(hurtTriggerName);
        animator.SetTrigger(hurtTriggerName);
    }
    private void TriggerDieAnim()
    {
        if (!animator) return;

        animator.ResetTrigger(attackTriggerName);
        animator.ResetTrigger(rangedAttackTriggerName);
        animator.ResetTrigger(magicAttackTriggerName);
        animator.ResetTrigger(hurtTriggerName);

        animator.SetTrigger(dieTriggerName);
        animator.Play(dieStateName, 0, 0f);
    }

    private float GetDieClipLength()
    {
        if (dieDisableDelay > 0f) return dieDisableDelay;

        if (!animator || animator.runtimeAnimatorController == null)
            return 0.8f;

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips != null)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                if (!c) continue;

                if (c.name.Equals(dieStateName, StringComparison.OrdinalIgnoreCase) ||
                    c.name.IndexOf(dieStateName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return c.length;
            }
        }

        return 0.8f;
    }

    // -------------------------
    // Inventory full pause
    // -------------------------

    private void HandleInventoryFull()
    {
        ShowPopup("Inventory Full!");

        if (dropOverflowToGround) return;

        if (state == State.Gather || state == State.MoveToTarget)
        {
            _pausedForInvFull = true;
            _pausedNode = targetNode;
            ReturnToIdle();
        }
    }

    private void HandleInventoryChanged()
    {
        if (!_pausedForInvFull || _pausedNode == null || inventory == null) return;
        if (_pausedNode.Definition == null || _pausedNode.workSpot == null) return;

        _drops.Clear();
        _pausedNode.Definition.PreviewDrops(_drops, 0f, default, GetGatherSkillLevel(_pausedNode.ActionType));
        if (_drops.Count == 0) return;

        string mainItemId = _drops[0].itemId;
        if (string.IsNullOrWhiteSpace(mainItemId)) return;

        if (!inventory.CanAdd(mainItemId, 1)) return;

        _pausedForInvFull = false;
        targetNode = _pausedNode;
        _pausedNode = null;

        float targetX = targetNode.workSpot.position.x;
        float dist = Mathf.Abs(transform.position.x - targetX);

        state = dist <= targetNode.interactRange ? State.Gather : State.MoveToTarget;
        SetAction(state == State.Gather ? GetGatherAction() : PlayerAction.Walking, true);
        if (state == State.Gather)
            FaceGatheringPoint();
    }

    // -------------------------
    // Click / Movement
    // -------------------------

    private void PollKeyboardSteeringInput()
    {
        _keyboardManualMoveThisFrame = false;
        _keyboardSteerDir = 0f;

        if (movementLocked || _isDead)
        {
            _keyboardSteerNotified = false;
            return;
        }

        if (!CanPollKeyboardMovementInput())
        {
            _keyboardSteerNotified = false;
            return;
        }

        bool left = IsKeyboardMoveLeftHeld();
        bool right = IsKeyboardMoveRightHeld();
        if (!left && !right)
        {
            _keyboardSteerNotified = false;
            return;
        }

        if (left)
            _keyboardSteerDir -= 1f;
        if (right)
            _keyboardSteerDir += 1f;
        if (Mathf.Abs(_keyboardSteerDir) < 0.01f)
        {
            _keyboardSteerNotified = false;
            return;
        }

        _keyboardManualMoveThisFrame = true;
        if (!_keyboardSteerNotified)
        {
            NotifyPlayerInitiatedMovement();
            _keyboardSteerNotified = true;
        }

        CancelAutoMovementFromKeyboardSteering();
    }

    private void ApplyKeyboardMovementDelta()
    {
        if (PlayerSprintInput.IsSprintDashing)
            return;

        if (!_keyboardManualMoveThisFrame || Mathf.Abs(_keyboardSteerDir) < 0.01f)
            return;

        float dir = _keyboardSteerDir;
        float speed = GetMoveSpeed();
        float targetX = transform.position.x + dir * speed * Time.deltaTime;
        GetClampXMinMax(out float min, out float max);
        targetX = Mathf.Clamp(targetX, min, max);
        MoveToX(targetX, speed);

        float desiredFacing = dir >= 0f ? 1f : -1f;
        if (!Mathf.Approximately(Mathf.Sign(FacingDirectionX), desiredFacing))
            FaceTargetX(targetX + dir);
    }

    private void CancelAutoMovementFromKeyboardSteering()
    {
        NPCInteractionSettings.CancelPendingInteract();
        MapNodePortalTeleporter.CancelPendingApproachForPlayer(this);
        InMapTeleporter.CancelPendingApproachForPlayer(this);

        if (state == State.Gather || state == State.MoveToTarget || state == State.MoveToPickup)
            InterruptWorkIfNeeded();
        else if (state == State.MoveToPoint)
            StopMoveOnly();
    }

    private void TryInteractHotkey()
    {
        if (!WasInteractHotkeyPressedThisFrame())
            return;

        if (movementLocked || _isDead)
            return;

        if (!CanPollWorldInteractHotkey())
            return;

        if (NPCDialogueBoxUI.TryConsumeInteractHotkey())
            return;

        float px = transform.position.x;
        float py = transform.position.y;
        LayerMask combinedMask = pickupMask | interactableMask | enemyMask;

        if (!WorldInteractRouter.TryFindClosestRoutableCollider(
                px,
                py,
                combinedMask,
                WorldInteractRouter.InteractHotkeyHalfRangeX,
                out Collider2D winner))
        {
            WorldInteractRouter.ApplyCombatTargetForInteractHotkeyMiss(this);
            return;
        }

        WorldInteractRouter.RouteInteract(winner, this);
    }

    private void TryEnterAreaHotkey()
    {
        if (!WasEnterAreaHotkeyPressedThisFrame())
            return;

        if (movementLocked || _isDead)
            return;

        if (!CanPollWorldInteractHotkey())
            return;

        float px = transform.position.x;
        float py = transform.position.y;

        if (!WorldInteractRouter.TryFindClosestEnterAreaCollider(
                px,
                py,
                interactableMask,
                WorldInteractRouter.InteractHotkeyHalfRangeX,
                out Collider2D winner))
            return;

        WorldInteractRouter.RouteEnterArea(winner, this);
    }

    private static bool CanPollKeyboardMovementInput()
    {
        if (HotkeySettingsRowUI.IsRebinding)
            return false;
        if (HelperGameplayController.BlocksStripGameplay)
            return false;
        return !IsTypingIntoInputField();
    }

    private static bool CanPollWorldInteractHotkey()
    {
        if (HotkeySettingsRowUI.IsRebinding)
            return false;
        if (HelperGameplayController.BlocksStripGameplay)
            return false;
        if (IsTypingIntoInputField())
            return false;

        // Do not gate on IsPointerOverGameObject — a dialogue/shop panel often covers the strip while
        // the mouse rests on it, which would block the Interact key even though it is a keyboard bind.
        return true;
    }

    private static bool IsTypingIntoInputField()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
            return false;

        GameObject selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null ||
               selected.GetComponent<InputField>() != null;
    }

    private bool IsKeyboardMoveLeftHeld()
    {
        KeyCode key = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(HotkeyBindId.MoveLeft)
            : HotkeyBindingManager.GetDefaultKey(HotkeyBindId.MoveLeft);
        return key != KeyCode.None && Input.GetKey(key);
    }

    private bool IsKeyboardMoveRightHeld()
    {
        KeyCode key = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(HotkeyBindId.MoveRight)
            : HotkeyBindingManager.GetDefaultKey(HotkeyBindId.MoveRight);
        return key != KeyCode.None && Input.GetKey(key);
    }

    private static bool WasInteractHotkeyPressedThisFrame() =>
        WasHotkeyBindPressedThisFrame(HotkeyBindId.Interact);

    private static bool WasEnterAreaHotkeyPressedThisFrame() =>
        WasHotkeyBindPressedThisFrame(HotkeyBindId.EnterArea);

    private static bool WasHotkeyBindPressedThisFrame(HotkeyBindId bindId)
    {
        KeyCode key = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(bindId)
            : HotkeyBindingManager.GetDefaultKey(bindId);
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    /// <param name="preserveGatherStateForUiModal">
    /// When true and the player is gathering a resource (<see cref="State.Gather"/> with a valid
    /// <see cref="targetNode"/>), do not clear targets or Idle the state — <see cref="movementLocked"/> still
    /// freezes gather ticks until unlocked (modal dismiss).
    /// </param>
    public void SetMovementLocked(bool locked, bool preserveGatherStateForUiModal = false)
    {
        movementLocked = locked;

        if (!locked)
            return;

        bool keepGatherFreeze =
            preserveGatherStateForUiModal &&
            state == State.Gather &&
            targetNode &&
            targetNode.Definition != null;

        if (keepGatherFreeze)
            return;

        ClearFatigueGatherProgress();

        targetNode = null;
        _pickupTarget = null;
        state = State.Idle;
    }

    /// <summary>Locks movement and basic attacks for ability channel windows (e.g. Final Severance).</summary>
    public void SetAbilityChannelLock(bool locked, float attackLockSeconds = 2f)
    {
        SetMovementLocked(locked, preserveGatherStateForUiModal: false);
        if (locked)
        {
            _attackLocked = true;
            _attackUnlockTime = Time.time + Mathf.Max(0.05f, attackLockSeconds);
            TryFaceCombatTargetDuringAttack();
            SetActionOverride(PlayerAction.Fighting);
        }
        else
        {
            _attackLocked = false;
            _attackUnlockTime = 0f;
            ClearActionOverride();
        }
    }

    /// <summary>Keeps attack presentation through channeled abilities (extends an existing attack lock).</summary>
    public void ExtendAttackLockUntil(float unlockTime)
    {
        if (_isDead)
            return;

        _attackLocked = true;
        _attackUnlockTime = Mathf.Max(_attackUnlockTime, unlockTime);
        TryFaceCombatTargetDuringAttack();
        SetActionOverride(PlayerAction.Fighting);

        float remaining = Mathf.Max(0.05f, unlockTime - Time.time);
        ClearFightingOverrideSoon(remaining);
    }

    public void SelectNode(ResourceNode node)
    {
        if (movementLocked)
        {
            ShowPopup("Idle combat active.");
            return;
        }

        if (_isDead) return;

        // Soft block: show popup but don't interrupt combat.
        // Let movement click logic decide where to go.
        if (AnyEnemyOnMap || InCombat)
        {
            ShowPopup(GetGatherBlockedMessage());
            return; // IMPORTANT: don't set targetNode/state
        }

        if (!node) return;
        node.ChooseClosestWorkSpot(transform.position);
        if (!node.workSpot) return;

        // Spam-clicking the same resource re-runs arrival logic: TickMoveToTarget zeros
        // _nextGatherAnimTime so the next TickGather spends energy again immediately.
        if (targetNode == node && (state == State.Gather || state == State.MoveToTarget))
            return;

        if (_fatigueGatherProgressPending && node != _fatigueGatherSavedNode)
            ClearFatigueGatherProgress();

        if (!MeetsNodeLevelRequirement(node))
        {
            ShowPopup($"Requires level {node.RequiredLevel}.");
            return;
        }

        if (node.ActionType == NodeAction.Fishing &&
            HasFishingRodInToolbelt() &&
            !HasAnyFishingBaitInInventory())
        {
            ShowPopup(MissingFishingBaitPopupText);
            return;
        }

        // =========================================================
        // ✅ IMMEDIATE XP DISPLAY SWITCH (BEFORE FIRST TICK)
        // =========================================================
        var sm = SkillsManager.Instance;
        if (sm != null)
        {
            SkillType sk = node.ActionType switch
            {
                NodeAction.Mining => SkillType.Mining,
                NodeAction.Woodcutting => SkillType.Woodcutting,
                NodeAction.Fishing => SkillType.Fishing,
                _ => SkillType.Woodcutting
            };

            string src = (!string.IsNullOrWhiteSpace(node.DisplayName))
                ? node.DisplayName
                : node.name;

            int skillLevel = sm.GetLevel(sk);
            int xpHint = node.Definition != null ? node.Definition.GetBestMainYieldXpHint(skillLevel) : -1;
            if (xpHint <= 0)
                xpHint = -1;

            sm.SetActiveXpDisplay(sk, src, xpHint);
        }

        // =========================================================
        // Gather logic continues normally
        // =========================================================

        _gatherSpeedMultiplier = 1f;
        _gatherGritChance = 0f;
        _gatherBonusFindChance = 0f;
        _gatherStaminaEfficiency = 0f;
        _activeFishingBaitSpeedBonusFraction = 0f;
        ResetWoodcuttingRuntimeState(clearBonuses: true);
        ResetFishingRuntimeState(clearBonuses: true);
        if (node.ActionType != NodeAction.Woodcutting)
            _woodcuttingFlowLingerUntil = 0f;

        if (!requireToolForGathering || !node.RequiresTool)
        {
            _gatherToolItemId = null;
            _gatherSpeedMultiplier = 1f;
            _gatherGritChance = 0f;
            _gatherBonusFindChance = 0f;
            _gatherStaminaEfficiency = 0f;

            equipment?.ClearMainHandUnarmedOverride();
            equipment?.ClearMainHandVisualOverride();
            _hideHandsForUnarmedGather = false;
            ApplyGatherHandVisuals();

            OnGatherDebuffChanged?.Invoke(false, 1f);

            targetNode = node;
            _pickupTarget = null;
            state = State.MoveToTarget;

            _accumItems = 0f;
            _gatherTimer = 0f;
            _nextGatherInterval = 0f;

            SetAction(PlayerAction.Walking, false);
            return;
        }

        if (TryFindToolInToolbelt(node.RequiredTool, out string toolItemId))
        {
            _gatherToolItemId = toolItemId;

            var toolDef = inventory ? inventory.GetItemDef(toolItemId) : null;
            ApplyGatherCoreStatsFromCharacterAndTool(node.ActionType, toolDef);

            bool debuffed = _gatherSpeedMultiplier < 0.999f;
            OnGatherDebuffChanged?.Invoke(debuffed, _gatherSpeedMultiplier);

            equipment?.ClearMainHandUnarmedOverride();
            _hideHandsForUnarmedGather = false;
            equipment?.ClearHideBothHandsOverride();
            equipment?.SetMainHandVisualOverride(toolItemId);
        }
        else
        {
            _gatherToolItemId = null;

            ShowPopup(BuildMissingToolActivityMessage(node));
            if (allowGatherWithoutTool)
            {
                _gatherSpeedMultiplier = missingToolSpeedMultiplier;
                _gatherGritChance = 0f;
                _gatherBonusFindChance = 0f;
                _gatherStaminaEfficiency = 0f;
                OnGatherDebuffChanged?.Invoke(true, _gatherSpeedMultiplier);

                _hideHandsForUnarmedGather = true;
                ApplyGatherHandVisuals();
            }
            else
            {
                OnGatherDebuffChanged?.Invoke(false, 1f);
                equipment?.ClearMainHandUnarmedOverride();
                equipment?.ClearMainHandVisualOverride();
                return;
            }
        }

        targetNode = node;
        ApplyWoodcuttingSkillRuntimeBonusesIfNeeded(node);
        ApplyFishingSkillRuntimeBonusesIfNeeded(node);
        // Lasting Focus: if Flow State was active when the previous gather stopped and we're still inside
        // the 5s linger window, seed continuous time on this new tree so Flow stays active through the
        // entire resumed gather instead of dropping in the gap between linger expiry and the next 15s ramp.
        if (node != null && node.ActionType == NodeAction.Woodcutting &&
            IsWoodcuttingMajorFlowLingerSelected() && Time.time < _woodcuttingFlowLingerUntil)
        {
            _woodcuttingContinuousGatherSeconds = WoodcuttingForestFlowContinuousSecondsThreshold;
        }
        _pickupTarget = null;
        state = State.MoveToTarget;

        _accumItems = 0f;
        _gatherTimer = 0f;
        _nextGatherInterval = 0f;

        SetAction(PlayerAction.Walking, true);
    }

    private bool TryFindToolInToolbelt(ToolKey requiredTool, out string toolItemId)
    {
        toolItemId = null;

        if (requiredTool == ToolKey.None)
            return true;

        if (!toolbelt)
        {
            Debug.LogError("[Toolbelt] toolbelt ref is NULL on PlayerController. You likely have two ToolbeltManager instances (UI vs Player).", this);
            return false;
        }

        if (!inventory)
        {
            Debug.LogError("[Toolbelt] inventory ref is NULL on PlayerController.", this);
            return false;
        }

        for (int i = 0; i < ToolbeltManager.SlotCount; i++)
        {
            string id = toolbelt.GetToolItemId(i);
            if (string.IsNullOrWhiteSpace(id)) continue;

            var def = inventory.GetItemDef(id);
            if (!def) continue;

            if (def.handVisualKey == requiredTool)
            {
                toolItemId = id;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when any toolbelt slot holds an item whose <see cref="ItemDefinition.handVisualKey"/> is <see cref="ToolKey.FishingRod"/>.
    /// Used for fishing-only rules (bait, gather swing vs idle presentation).
    /// </summary>
    private bool HasFishingRodInToolbelt() => TryFindToolInToolbelt(ToolKey.FishingRod, out _);

    public void ForceIdleAction()
    {
        ClearActionOverride();
        SetAction(PlayerAction.Idle, true);
    }

    // -------------------------
    // Skills
    // -------------------------


    private bool MeetsNodeLevelRequirement(ResourceNode node)
    {
        if (!node || !node.UseLevelRequirement) return true;

        SkillType skill = node.ActionType switch
        {
            NodeAction.Mining => SkillType.Mining,
            NodeAction.Woodcutting => SkillType.Woodcutting,
            NodeAction.Fishing => SkillType.Fishing,
            _ => SkillType.Woodcutting
        };

        int lvl = SkillsManager.Instance ? SkillsManager.Instance.GetLevel(skill) : 1;
        return lvl >= node.RequiredLevel;
    }

    private int GetGatherSkillLevel(NodeAction actionType)
    {
        SkillType skill = SkillFromNodeAction(actionType);
        return SkillsManager.Instance ? Mathf.Max(1, SkillsManager.Instance.GetLevel(skill)) : 1;
    }

    private static SkillType SkillFromNodeAction(NodeAction a) => a switch
    {
        NodeAction.Mining => SkillType.Mining,
        NodeAction.Woodcutting => SkillType.Woodcutting,
        NodeAction.Fishing => SkillType.Fishing,
        _ => SkillType.Woodcutting
    };

    private const string GatherSpeedGreatlyReducedTail = "Gather speed is greatly reduced.";

    private static string BuildMissingToolActivityMessage(ResourceNode node, bool appendGatherPenalty)
    {
        string baseMsg = string.IsNullOrWhiteSpace(node.MissingToolMessage)
            ? "Missing tool in toolbelt."
            : node.MissingToolMessage.Trim();

        return appendGatherPenalty ? $"{baseMsg} - {GatherSpeedGreatlyReducedTail}" : baseMsg;
    }

    private string BuildMissingToolActivityMessage(ResourceNode node) =>
        BuildMissingToolActivityMessage(node, allowGatherWithoutTool);

    // -------------------------
    // Popup feedback (activity log only; no floating copy above the player)
    // -------------------------

    /// <summary>
    /// Clears the "unusable" activity-log latch so the next blocked attempt (e.g. shared cooldown) can log again.
    /// Call when a food/potion use succeeds, or from <see cref="PlayerConsumableController"/> when that item is off cooldown.
    /// </summary>
    public void ResetConsumableUnusableActivityLogLatchFor(ItemDefinition def)
    {
        if (def == null || !def.IsConsumable)
            return;
        if (def.IsFood)
            _foodConsumableUnusableActivityLogged = false;
        if (def.IsPotion)
            _potionConsumableUnusableActivityLogged = false;
    }

    private void ShowPopupInternal(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg))
            msg = "Action not allowed.";

        if (ShouldLogPopupToActivity(msg))
            GameLog.Add(msg);

        // Activity log only — no floating world copy above the player.
        if (actionPopup)
            actionPopup.SetActive(false);
    }

    public void ShowPopup(string msg)
    {
        ShowPopupInternal(msg);
    }

    private bool ShouldLogPopupToActivity(string msg)
    {
        if (IsConsumableCooldownMessage(msg, "Food"))
        {
            if (_foodConsumableUnusableActivityLogged)
                return false;
            _foodConsumableUnusableActivityLogged = true;
            return true;
        }

        if (IsConsumableCooldownMessage(msg, "Potions") ||
            IsConsumableCooldownMessage(msg, "Potion"))
        {
            if (_potionConsumableUnusableActivityLogged)
                return false;
            _potionConsumableUnusableActivityLogged = true;
            return true;
        }

        return TryPassRepeatedPopupLogGate(msg);
    }

    private static bool IsConsumableCooldownMessage(string msg, string consumableLabel)
    {
        return !string.IsNullOrWhiteSpace(msg) &&
               msg.StartsWith($"{consumableLabel} on cooldown", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryPassRepeatedPopupLogGate(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg))
            return true;

        string key = msg.Trim();
        if (_nextActivityLogTimeByPopupMessage.TryGetValue(key, out float nextAllowedTime) &&
            Time.unscaledTime < nextAllowedTime)
            return false;

        _nextActivityLogTimeByPopupMessage[key] = Time.unscaledTime + RepeatedPopupActivityLogIntervalSeconds;
        return true;
    }

    public void CancelAction()
    {
        ClearFatigueGatherProgress();
        CaptureWoodcuttingFlowLingerOnGatherStop();
        CaptureFishingCalmWatersOnGatherStop();
        ResetFishingRuntimeState(clearBonuses: true);

        _gatherSpeedMultiplier = 1f;
        OnGatherDebuffChanged?.Invoke(false, 1f);

        targetNode = null;
        _pickupTarget = null;
        state = State.Idle;

        _accumItems = 0f;
        _gatherTimer = 0f;
        _nextGatherInterval = 0f;
        _nextGatherAnimTime = 0f;

        ClearGatherHandVisuals();

        SetAction(PlayerAction.Idle, true);
    }

    private void HandleClickToMove()
    {
        if (movementLocked)
            return;

        if (_isDead) return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (restrictClicksToStrip && !IsGameplayClickAllowedAtScreen(Input.mousePosition))
            return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        // World clicks should always exit shop mode.
        MerchantClick.ForceCloseMerchantMode();
        MerchantClick.CancelPendingOpen();

        // Expanded sky / margins: horizontal move only — no strip targeting or interact.
        if (IsExpandBackgroundOutsideStripClick(Input.mousePosition))
        {
            if (!_stripCam)
                RebindCameras();
            if (!_stripCam)
                return;

            Vector3 stripWorld = _stripCam.ScreenToWorldPoint(Input.mousePosition);
            InterruptWorkIfNeeded();
            MoveToPointX(stripWorld.x, fromPlayerInput: true);
            return;
        }

        Camera clickCamera = ResolveWorldClickCamera();
        if (!clickCamera)
            return;

        Vector3 world = clickCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        Vector2 point = new Vector2(world.x, world.y);

        LayerMask combinedMask = pickupMask | interactableMask | enemyMask;
        Collider2D winner = WorldClickPicker2D.PickTopmostAtPoint(point, combinedMask);

        if (winner != null)
        {
            var enemyClick = winner.GetComponentInParent<EnemyClick>();
            if (enemyClick != null && combat != null)
            {
                EnemyBaseController clickedEnemy = enemyClick.GetEnemy();
                if (clickedEnemy != null)
                    combat.EngageTargetFromPlayerInput(clickedEnemy);
                return;
            }

            // If we clicked a ResourceNode while enemies exist / in combat:
            // show popup but treat click like ground (move there) and do NOT clear combat target.
            var node = winner.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                if (AnyEnemyOnMap || InCombat)
                {
                    ShowPopup(GetGatherBlockedMessage());
                    MoveToPointX(world.x, fromPlayerInput: true);
                    return;
                }
            }

            if (WorldInteractRouter.IsRoutableCollider(winner))
            {
                WorldInteractRouter.RouteInteract(winner, this);
                return;
            }

            return;
        }

        RequestWalkToScreenPosition(Input.mousePosition);
    }

    /// <summary>Walk-to-point from a screen position (context menu Walk here on empty ground).</summary>
    public void RequestWalkToScreenPosition(Vector3 screenPosition)
    {
        if (movementLocked || _isDead)
            return;

        if (restrictClicksToStrip && !IsGameplayClickAllowedAtScreen(screenPosition))
            return;

        MerchantClick.ForceCloseMerchantMode();
        MerchantClick.CancelPendingOpen();

        if (IsExpandBackgroundOutsideStripClick(screenPosition))
        {
            if (!_stripCam)
                RebindCameras();
            if (!_stripCam)
                return;

            Vector3 stripWorld = _stripCam.ScreenToWorldPoint(screenPosition);
            InterruptWorkIfNeeded();
            MoveToPointX(stripWorld.x, fromPlayerInput: true);
            return;
        }

        Camera clickCamera = ResolveWorldClickCamera();
        if (!clickCamera)
            return;

        Vector3 world = clickCamera.ScreenToWorldPoint(screenPosition);
        world.z = 0f;
        InterruptWorkIfNeeded();
        MoveToPointX(world.x, fromPlayerInput: true);
    }

    private void InterruptWorkIfNeeded()
    {
        if (state == State.Gather || state == State.MoveToTarget || state == State.MoveToPickup)
        {
            CaptureWoodcuttingFlowLingerOnGatherStop();
            CaptureFishingCalmWatersOnGatherStop();
            ResetFishingRuntimeState(clearBonuses: true);
            targetNode = null;
            _pickupTarget = null;

            _accumItems = 0f;
            _gatherTimer = 0f;
            _nextGatherInterval = 0f;

            _pausedForInvFull = false;
            _pausedNode = null;

            equipment?.ClearMainHandVisualOverride();
        }
    }

    /// <summary>
    /// Player chose to move (click-to-move or keyboard). Stops combat chase, clears interact focus, cancels walk-to NPC/merchant.
    /// </summary>
    public void NotifyPlayerInitiatedMovement()
    {
        combat?.NotifyPlayerInitiatedMovement();
        PlayerWorldInteractFocus.ClearForPlayer(this);
        NPCInteractionSettings.CancelPendingInteract();
        MerchantClick.CancelPendingOpen();
    }

    /// <summary>Interrupts gather/pickup paths when the player starts a sprint dash; click-to-move and combat chase keep their destination.</summary>
    public void InterruptForSprintDash()
    {
        if (state == State.Gather || state == State.MoveToTarget || state == State.MoveToPickup)
        {
            NotifyPlayerInitiatedMovement();
            InterruptWorkIfNeeded();
        }
    }

    /// <summary>Keyboard left/right if held, otherwise walk target or current facing.</summary>
    public float ResolveSprintDashDirectionSign()
    {
        bool left = IsKeyboardMoveLeftHeld();
        bool right = IsKeyboardMoveRightHeld();
        if (left && !right)
            return -1f;
        if (right && !left)
            return 1f;

        if (state == State.MoveToPoint)
        {
            float dx = moveTargetX - transform.position.x;
            if (Mathf.Abs(dx) > 0.01f)
                return dx > 0f ? 1f : -1f;
        }

        if (combat != null && combat.CurrentTarget != null && !combat.CurrentTarget.IsDead)
        {
            float dx = combat.CurrentTarget.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.01f)
                return dx > 0f ? 1f : -1f;
        }

        return FacingDirectionX >= 0f ? 1f : -1f;
    }

    public float ClampWorldX(float x)
    {
        GetClampXMinMax(out float min, out float max);
        return Mathf.Clamp(x, min, max);
    }

    /// <summary>
    /// Clamps X using the lane bounds for <paramref name="laneReferenceWorldX"/> (where the move started),
    /// so dashes cannot travel past the edge of the current lane into a gap or another play area.
    /// </summary>
    public float ClampWorldXForLaneAt(float laneReferenceWorldX, float x)
    {
        GetClampXMinMax(out float min, out float max, laneReferenceWorldX);
        return Mathf.Clamp(x, min, max);
    }

    public void SetHorizontalPositionForScriptedMove(float x, float faceDirectionSign)
    {
        SetHorizontalPositionForScriptedMove(x, faceDirectionSign, transform.position.x);
    }

    public void SetHorizontalPositionForScriptedMove(float x, float faceDirectionSign, float laneReferenceWorldX)
    {
        Vector3 pos = transform.position;
        pos.x = ClampWorldXForLaneAt(laneReferenceWorldX, x);
        transform.position = pos;
        SyncPlayerRigidbody2DPosition();
        if (Mathf.Abs(faceDirectionSign) > 0.01f)
            FaceTargetX(pos.x + faceDirectionSign);
    }

    /// <summary>Instant same-map reposition (e.g. in-map teleporter). Uses destination X for play-area bounds.</summary>
    public void WarpToWorldX(float worldX)
    {
        if (_isDead)
            return;

        InMapTeleporter.CancelPendingApproachForPlayer(this);
        MapNodePortalTeleporter.CancelPendingApproachForPlayer(this);
        NPCInteractionSettings.CancelPendingInteract();
        MerchantClick.CancelPendingOpen();

        float x = PlayAreaBounds.TryGetClampXForWorldX(worldX, WorldBoundsXPadding, out float min, out float max)
            ? Mathf.Clamp(worldX, min, max)
            : ClampWorldX(worldX);

        CaptureWoodcuttingFlowLingerOnGatherStop();
        CaptureFishingCalmWatersOnGatherStop();
        ResetFishingRuntimeState(clearBonuses: true);

        targetNode = null;
        _pickupTarget = null;
        _moveToPointFromPlayerInput = false;
        if (state == State.MoveToPoint)
            StopMoveOnly();
        state = State.Idle;

        Vector3 pos = transform.position;
        pos.x = x;
        transform.position = pos;
        AlignToActiveFloorForCurrentX();
        SyncPlayerRigidbody2DPosition();
        CameraFollow.SnapToTargetHorizontal();

        PlayerCombatController combat = GetComponent<PlayerCombatController>();
        combat?.ClearTarget();
        PlayerWorldInteractFocus.ClearForPlayer(this);
    }

    /// <summary>Re-snap current position to active lane floor (use after visual scale changes).</summary>
    public void SnapToActiveLaneAtCurrentX()
    {
        if (_isDead)
            return;

        AlignToActiveFloorForCurrentX();
        SyncPlayerRigidbody2DPosition();
    }

    private Collider2D ResolveGroundAlignCollider()
    {
        Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
        if (cols == null || cols.Length == 0)
            return null;

        Collider2D fallback = null;
        for (int i = 0; i < cols.Length; i++)
        {
            Collider2D c = cols[i];
            if (!c)
                continue;

            fallback ??= c;

            // Prefer the physical body collider (not trigger), bound to this player's rigidbody.
            if (!c.isTrigger && c.attachedRigidbody == _rb)
                return c;
        }

        return fallback;
    }

    private void AlignToActiveFloorForCurrentX()
    {
        if (!PlayAreaBounds.TryGetFloorTopYForWorldX(transform.position.x, out float floorTop))
            return;

        _groundAlignCollider ??= ResolveGroundAlignCollider();
        if (_groundAlignCollider == null)
            return;

        Physics2D.SyncTransforms();
        float colliderBottomBelowRoot = transform.position.y - _groundAlignCollider.bounds.min.y;
        float targetY = floorTop + colliderBottomBelowRoot;

        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.y - targetY) <= 0.0001f)
            return;

        pos.y = targetY;
        transform.position = pos;
    }

    /// <summary>Clears manual repositioning so combat can chase the clicked enemy into range.</summary>
    public void PrepareForCombatEngageInput()
    {
        InterruptWorkIfNeeded();
        _moveToPointFromPlayerInput = false;
        if (state == State.MoveToPoint)
            StopMoveOnly();
        PlayerWorldInteractFocus.ClearForPlayer(this);
        NPCInteractionSettings.CancelPendingInteract();
        MerchantClick.CancelPendingOpen();
    }

    public void MoveToPointX(float x, bool fromPlayerInput = false)
    {
        if (_isDead) return;

        GetClampXMinMax(out float min, out float max);
        float clamped = Mathf.Clamp(x, min, max);

        if (fromPlayerInput)
        {
            float distFromCurrent = Mathf.Abs(clamped - transform.position.x);
            if (distFromCurrent <= clickArriveThreshold)
                return;

            if (Mathf.Abs(clamped - moveTargetX) <= clickArriveThreshold)
                return;

            bool redirectWhileWalking =
                state == State.MoveToPoint &&
                _moveToPointFromPlayerInput;

            if (redirectWhileWalking)
            {
                moveTargetX = clamped;
                return;
            }

            NotifyPlayerInitiatedMovement();
        }

        CaptureWoodcuttingFlowLingerOnGatherStop();
        CaptureFishingCalmWatersOnGatherStop();
        ResetFishingRuntimeState(clearBonuses: true);

        targetNode = null;
        _pickupTarget = null;

        _accumItems = 0f;
        _gatherTimer = 0f;
        _nextGatherInterval = 0f;

        moveTargetX = clamped;
        _moveToPointFromPlayerInput = fromPlayerInput;
        state = State.MoveToPoint;

        equipment?.ClearMainHandVisualOverride();
        SetAction(PlayerAction.Walking, fromPlayerInput ? false : true);
    }

    public bool IsCombatMoveTargetNear(float worldX, float epsilon)
    {
        return state == State.MoveToPoint && Mathf.Abs(moveTargetX - worldX) <= epsilon;
    }

    public void MoveToPointX_Combat(float x)
    {
        if (_isDead) return;

        GetClampXMinMax(out float min, out float max);

        float clamped = Mathf.Clamp(x, min, max);

        // Use a tighter combat epsilon than click movement so we still micro-step into attack range.
        float combatSnap = Mathf.Max(0.001f, combatArriveThreshold);
        if (Mathf.Abs(transform.position.x - clamped) <= combatSnap)
        {
            // If combat is spamming this every frame, force Idle so we can idle between attacks.
            if (state == State.MoveToPoint)
                state = State.Idle;
            return;
        }

        const float retargetEpsilon = 0.04f;
        if (state == State.MoveToPoint && Mathf.Abs(moveTargetX - clamped) <= retargetEpsilon)
            return;

        moveTargetX = clamped;
        state = State.MoveToPoint;
    }

    public void StopMoveOnly()
    {
        if (state == State.MoveToPoint)
        {
            _moveToPointFromPlayerInput = false;
            state = State.Idle;
        }
    }

    private void TickMoveToTarget()
    {
        if (movementLocked)
            return;

        if (_keyboardManualMoveThisFrame)
            return;

        if (!targetNode) { ReturnToIdle(); return; }

        float targetX = targetNode.workSpot.position.x;
        MoveToX(targetX, GetMoveSpeed());

        float dist = Mathf.Abs(transform.position.x - targetX);
        if (dist <= targetNode.interactRange)
        {
            state = State.Gather;

            FaceGatheringPoint();

            _nextGatherAnimTime = 0f;

            if (_fatigueGatherProgressPending && targetNode == _fatigueGatherSavedNode && _fatigueGatherSavedNode != null)
            {
                _accumItems = _fatigueGatherSavedAccum;
                _gatherTimer = _fatigueGatherSavedTimer;
                if (_fatigueGatherSavedInterval <= 0f)
                    _nextGatherInterval = targetNode.GetNextInterval();
                else
                    _nextGatherInterval = _fatigueGatherSavedInterval;
                ClearFatigueGatherProgress();
            }
            else
            {
                _accumItems = 0f;
                _gatherTimer = 0f;
                _nextGatherInterval = targetNode.GetNextInterval();
            }

            ApplyGatherHandVisuals();

            SetAction(GetGatherAction(), true);
        }
    }

    private void TickMoveToPoint()
    {
        if (PlayerSprintInput.IsSprintDashing)
            return;

        if (_keyboardManualMoveThisFrame)
            return;

        MoveToX(moveTargetX, GetMoveSpeed());

        float dist = Mathf.Abs(transform.position.x - moveTargetX);
        // Combat runs before this script: MoveToPointX_Combat can set MoveToPoint and we move in the same frame.
        // Using the wide click threshold here makes us ReturnToIdle immediately while still chasing a moving
        // enemy (tiny steps), so presentation stays Fighting/idle while the transform keeps moving = sliding.
        float arrive = clickArriveThreshold;
        if (combat != null &&
            combat.CurrentTarget != null &&
            !combat.CurrentTarget.IsDead)
        {
            arrive = Mathf.Max(0.001f, combatArriveThreshold);
        }

        if (dist <= arrive)
        {
            // Combat micro-steps to a moving enemy: do not run full ReturnToIdle() (gather resets + weapon
            // visual clears + forced Idle action) every time we hit the tight arrive threshold — that fights
            // combat presentation and can leave you sliding in Fighting/idle without reaching attack logic.
            if (combat != null &&
                combat.CurrentTarget != null &&
                !combat.CurrentTarget.IsDead)
            {
                state = State.Idle;
                return;
            }
            else if (_moveToPointFromPlayerInput)
            {
                state = State.Idle;
                _moveToPointFromPlayerInput = false;
                SetAction(PlayerAction.Idle, false);
            }
            else
            {
                ReturnToIdle();
            }

            return;
        }
    }

    // -------------------------
    // Pickup move + pickup
    // -------------------------

    public void RequestPickup(ItemDrop drop)
    {
        if (movementLocked)
        {
            ShowPopup("Idle combat active.");
            return;
        }

        if (_isDead) return;

        combat?.ClearTarget();
        if (drop == null) return;

        targetNode = null;

        _accumItems = 0f;
        _gatherTimer = 0f;
        _nextGatherInterval = 0f;

        equipment?.ClearMainHandVisualOverride();

        _pickupTarget = drop;
        state = State.MoveToPickup;
        SetAction(PlayerAction.Walking, true);

        equipment?.ClearMainHandVisualOverride();
    }

    private void TickMoveToPickup()
    {
        if (movementLocked)
            return;

        if (_keyboardManualMoveThisFrame)
            return;

        if (_pickupTarget == null)
        {
            ReturnToIdle();
            return;
        }

        float targetX = _pickupTarget.transform.position.x;
        MoveToX(targetX, GetMoveSpeed());

        float dist = Mathf.Abs(transform.position.x - targetX);
        if (dist <= pickupRange)
        {
            if (inventory != null && _pickupTarget != null)
            {
                bool canFullyPickup = inventory.CanAdd(_pickupTarget.ItemId, _pickupTarget.Amount);

                _pickupTarget.TryPickup(inventory);

                if (!canFullyPickup)
                    ShowPopup("Inventory Full!");
            }

            _pickupTarget = null;
            ReturnToIdle();
        }
    }

    private void HandleToolSlotChanged(int index, string itemId)
    {
        if (targetNode == null) return;
        if (!requireToolForGathering) return;
        if (!targetNode.RequiresTool) return;

        bool hasTool = TryFindToolInToolbelt(targetNode.RequiredTool, out string toolItemId);

        if (hasTool)
        {
            _gatherToolItemId = toolItemId;
            var toolDef = inventory ? inventory.GetItemDef(toolItemId) : null;
            ApplyGatherCoreStatsFromCharacterAndTool(targetNode.ActionType, toolDef);

            bool debuffed = _gatherSpeedMultiplier < 0.999f;
            OnGatherDebuffChanged?.Invoke(debuffed, _gatherSpeedMultiplier);

            _hideHandsForUnarmedGather = false;
            equipment?.ClearHideBothHandsOverride();
            equipment?.ClearMainHandUnarmedOverride();

            equipment?.SetMainHandVisualOverride(toolItemId);
        }
        else
        {
            _gatherToolItemId = null;

            if (allowGatherWithoutTool)
            {
                _gatherSpeedMultiplier = missingToolSpeedMultiplier;
                _gatherGritChance = 0f;
                _gatherBonusFindChance = 0f;
                _gatherStaminaEfficiency = 0f;

                OnGatherDebuffChanged?.Invoke(true, _gatherSpeedMultiplier);

                equipment?.SetMainHandUnarmedOverride();
                equipment?.SetHideBothHandsOverride(true);

                if (state == State.Gather)
                    ShowPopup(BuildMissingToolActivityMessage(targetNode));
            }
            else
            {
                CancelAction();
                return;
            }
        }
    }

    // -------------------------
    // Gather
    // -------------------------

    private void TickGather()
    {
        if (movementLocked)
            return;

        if (AnyEnemyOnMap)
        {
            CancelAction();
            return;
        }

        if (InCombat)
        {
            CancelAction();
            return;
        }

        if (!targetNode) { ReturnToIdle(); return; }
        if (!inventory) { ReturnToIdle(); return; }
        if (targetNode.Definition == null) return;

        // Cleaving Chop fans out to nearby trees on each tree's OWN gather cadence; advance per-tree timers
        // while the buff is up and the player is still in an active gather session. Called before the
        // primary tree's own interval logic so the secondary trees don't lag a frame behind.
        if (targetNode.ActionType == NodeAction.Woodcutting)
            AdvanceCleavingChopSecondaryGathers(Time.deltaTime);

        if (targetNode.ActionType == NodeAction.Woodcutting)
            _woodcuttingContinuousGatherSeconds += Time.deltaTime;

        if (targetNode.ActionType == NodeAction.Fishing)
        {
            _fishingContinuousGatherSeconds += Time.deltaTime;

            if (IsFishingMajorCalmWatersSelected() && IsFishingMajorLastingWatersSelected() && _fishingCalmWatersLingerStacks > 0)
            {
                _fishingCalmWatersMajorStacks = Mathf.Min(GetFishingCalmWatersMaxStacks(), _fishingCalmWatersLingerStacks);
                _fishingCalmWatersLingerStacks = 0;
            }

            if (IsFishingMajorCalmWatersSelected())
            {
                _fishingCalmWatersMajorStackTimer += Time.deltaTime;
                int maxStacks = GetFishingCalmWatersMaxStacks();
                while (_fishingCalmWatersMajorStacks < maxStacks &&
                       _fishingCalmWatersMajorStackTimer >= FishingCalmWatersMajorStackIntervalSeconds)
                {
                    _fishingCalmWatersMajorStackTimer -= FishingCalmWatersMajorStackIntervalSeconds;
                    _fishingCalmWatersMajorStacks++;
                }
            }
        }

        // Only “swing” the gather animation once every N seconds while gathering
        if (animator && Time.time >= _nextGatherAnimTime)
        {
            // Spend gather energy on the same cadence as gather swings for smoother feel.
            if (!TrySpendGatherEnergyOnSwing(targetNode.Definition))
            {
                if (_lastGatherSwingSpendFailureReason == GatherSwingSpendFailureReason.MissingFishingBait)
                    StopFishingForMissingBait();
                else
                    PauseGatherForLowEnergy();
                return;
            }

            // Fishing with a rod in toolbelt: idle during ticks (no chop-style swing). Without a rod, use the gather swing like other skills.
            bool hideFishingGatherSwing =
                targetNode.ActionType == NodeAction.Fishing && HasFishingRodInToolbelt();
            if (!hideFishingGatherSwing)
                PlayState(gatherStateName, restart: true);

            _nextGatherAnimTime = Time.time + Mathf.Max(0.25f, gatherAnimDelaySeconds);
        }

        Vector3 pos = transform.position;
        GetClampXMinMax(out float minX, out float maxX);
        pos.x = Mathf.Clamp(targetNode.workSpot.position.x, minX, maxX);
        transform.position = pos;
        SyncPlayerRigidbody2DPosition();

        if (_nextGatherInterval <= 0f)
            _nextGatherInterval = targetNode.GetNextInterval();

        _gatherTimer += Time.deltaTime * GetEffectiveGatherSpeedMultiplier();

        if (_gatherTimer >= _nextGatherInterval)
        {
            DoOneGatherTick();

            // AddPartial can synchronously invoke OnInventoryChanged (e.g. helper overlay locks movement →
            // SetMovementLocked clears targetNode). Don't touch targetNode afterward.
            if (!targetNode)
                return;

            _gatherTimer = 0f;
            _nextGatherInterval = targetNode.GetNextInterval();
        }
    }

    private void DoOneGatherTick()
    {
        if (targetNode == null || targetNode.Definition == null) return;

        var def = targetNode.Definition;
        _ = GetEffectiveGatherStaminaCostPerTick(); // Reserved for stamina spend integration.
        bool isWoodcutting = targetNode.ActionType == NodeAction.Woodcutting;
        bool isFishing = targetNode.ActionType == NodeAction.Fishing;
        bool gritProc = false;
        int fishingMajorPick = GetFishingLevel15RowPick();
        int fishingMajorEnhancement = GetFishingLevel15EnhancementIndex(fishingMajorPick);

        // ---- 1) MAIN yield first (this is the ONLY thing that grants XP) ----
        if (def.HasMainYield)
        {
            bool countTowardDepletion = true;
            int woodcuttingMajorPick = GetWoodcuttingLevel15RowPick();
            int woodcuttingMajorEnhancement = GetWoodcuttingLevel15EnhancementIndex(woodcuttingMajorPick);
            if (def.UsesDepletion && characterStats != null)
            {
                if (isWoodcutting)
                {
                    float skipP = Mathf.Clamp01(characterStats.AxeWoodcuttingChanceNotToCountTowardTreeDepletion);
                    if (woodcuttingMajorPick == 0)
                    {
                        skipP += 0.15f;
                        if (woodcuttingMajorEnhancement == 1)
                            skipP += 0.10f;
                        skipP = Mathf.Clamp01(skipP);
                    }
                    if (skipP > 0f && UnityEngine.Random.value < skipP)
                        countTowardDepletion = false;
                }
                else if (isFishing)
                {
                    float skipP = 0f;
                    if (fishingMajorPick == 0)
                    {
                        skipP += 0.15f;
                        if (fishingMajorEnhancement == 1)
                            skipP += 0.10f;
                        skipP = Mathf.Clamp01(skipP);
                    }
                    if (skipP > 0f && UnityEngine.Random.value < skipP)
                        countTowardDepletion = false;
                }
            }

            if (isWoodcutting && def.UsesDepletion && abilityController != null && abilityController.IsAvatarOfTheForestActive)
                countTowardDepletion = false;

            targetNode.NotifyGatherTickBeforeBonuses(countTowardDepletion);

            if (isWoodcutting && !countTowardDepletion && woodcuttingMajorPick == 0 &&
                woodcuttingMajorEnhancement == 0 && characterStats != null)
            {
                float restore = 0.10f * Mathf.Max(1f, characterStats.MaxEnergy);
                characterStats.AddEnergy(restore);
            }

            if (isFishing && !countTowardDepletion && fishingMajorPick == 0 &&
                fishingMajorEnhancement == 0 && characterStats != null)
            {
                float restore = 0.10f * Mathf.Max(1f, characterStats.MaxEnergy);
                characterStats.AddEnergy(restore);
            }

            int gatherSkillLevel = GetGatherSkillLevel(def.actionType);
            def.RollMainYieldCounts(_mainYieldScratch, gatherSkillLevel, isFishing ? _fishingXpScratch : null);

            bool woodSingle =
                isWoodcutting &&
                _mainYieldScratch.Count == 1;

            float gritRoll = Mathf.Clamp01(_gatherGritChance);
            if (isWoodcutting && IsWoodcuttingMajorFlowDeepFocusSelected() && IsWoodcuttingMajorFlowBuffActive())
                gritRoll = Mathf.Clamp01(gritRoll + 0.10f);

            bool mainYieldEligibleForXp = false;
            int mainAmt = 0;
            string mainItemId = null;

            if (woodSingle)
            {
                foreach (var kv in _mainYieldScratch)
                {
                    mainItemId = kv.Key;
                    mainAmt = kv.Value;
                    break;
                }

                if (isWoodcutting && _woodcuttingBonuses.baseYieldPercent > 0f)
                    mainAmt = Mathf.Max(1, Mathf.RoundToInt(mainAmt * (1f + _woodcuttingBonuses.baseYieldPercent)));

                if (isWoodcutting && _woodcuttingBonuses.extraLogChance > 0f &&
                    UnityEngine.Random.value <= _woodcuttingBonuses.extraLogChance)
                    mainAmt += 1;

                // Gathering Grit: doubles BASE yield only. Never duplicates bonus drops.
                if (mainAmt > 0 && UnityEngine.Random.value <= gritRoll)
                {
                    mainAmt *= 2;
                    gritProc = true;
                }

                if (isWoodcutting && gritProc && woodcuttingMajorPick == 1)
                {
                    float heavyExtraChance = woodcuttingMajorEnhancement == 1 ? 0.20f : 0.15f;
                    if (UnityEngine.Random.value < heavyExtraChance)
                        mainAmt += 1;
                }

                if (targetNode.ApplyDepletedYieldPenaltyThisTick)
                    mainAmt = RollDepletedGatherYield(mainAmt, def.depletedYieldMultiplier);

                if (isWoodcutting && mainAmt > 0 && IsWoodcuttingCapstoneBonusLogUnlocked())
                    mainAmt += 1;

                mainYieldEligibleForXp = mainAmt > 0;

                if (mainAmt > 0 && !string.IsNullOrWhiteSpace(mainItemId))
                    TryDepositGatherMainLoot(mainItemId, mainAmt, def.displayName);
            }
            else
            {
                int totalBase = NodeDefinition.SumMainYieldCounts(_mainYieldScratch);
                if (totalBase > 0 && UnityEngine.Random.value <= gritRoll)
                {
                    gritProc = true;
                    string[] keys = new string[_mainYieldScratch.Count];
                    int ki = 0;
                    foreach (var k in _mainYieldScratch.Keys)
                        keys[ki++] = k;
                    for (int gi = 0; gi < keys.Length; gi++)
                    {
                        string k = keys[gi];
                        _mainYieldScratch[k] *= 2;
                    }

                    if (isFishing && _fishingXpScratch.Count > 0)
                    {
                        int nXp = _fishingXpScratch.Count;
                        for (int xi = 0; xi < nXp; xi++)
                            _fishingXpScratch.Add(_fishingXpScratch[xi]);
                    }
                }

                if (isFishing && gritProc && fishingMajorPick == 1)
                {
                    float poweredExtraChance = fishingMajorEnhancement == 1 ? 0.20f : 0.15f;
                    if (UnityEngine.Random.value < poweredExtraChance)
                    {
                        foreach (var kv in _mainYieldScratch)
                        {
                            if (kv.Value <= 0 || string.IsNullOrWhiteSpace(kv.Key))
                                continue;
                            _mainYieldScratch[kv.Key] = kv.Value + 1;
                            if (_fishingXpScratch.Count > 0)
                                _fishingXpScratch.Add(_fishingXpScratch[_fishingXpScratch.Count - 1]);
                            break;
                        }
                    }
                }

                if (targetNode.ApplyDepletedYieldPenaltyThisTick)
                {
                    string[] keys = new string[_mainYieldScratch.Count];
                    int ki = 0;
                    foreach (var k in _mainYieldScratch.Keys)
                        keys[ki++] = k;
                    for (int gi = 0; gi < keys.Length; gi++)
                    {
                        string k = keys[gi];
                        int v = _mainYieldScratch[k];
                        _mainYieldScratch[k] = RollDepletedGatherYield(v, def.depletedYieldMultiplier);
                    }
                }

                mainYieldEligibleForXp = NodeDefinition.SumMainYieldCounts(_mainYieldScratch) > 0;

                foreach (var kv in _mainYieldScratch)
                {
                    if (kv.Value <= 0 || string.IsNullOrWhiteSpace(kv.Key))
                        continue;
                    TryDepositGatherMainLoot(kv.Key, kv.Value, def.displayName);
                }
            }

            if (mainYieldEligibleForXp)
            {
                // ✅ XP ONLY for main yield tick
                var sm = SkillsManager.Instance;
                if (sm != null)
                {
                    if (isFishing && _fishingXpScratch.Count > 0)
                    {
                        for (int xi = 0; xi < _fishingXpScratch.Count; xi++)
                        {
                            int xv = _fishingXpScratch[xi];
                            if (xv > 0)
                                sm.AddXp(SkillType.Fishing, xv, def.displayName);
                        }
                    }
                    else if (!isFishing && def.xpPerTick > 0)
                    {
                        SkillType skill = def.actionType switch
                        {
                            NodeAction.Mining => SkillType.Mining,
                            NodeAction.Woodcutting => SkillType.Woodcutting,
                            NodeAction.Fishing => SkillType.Fishing,
                            _ => SkillType.Woodcutting
                        };

                        sm.AddXp(skill, def.xpPerTick, def.displayName);
                        if (isWoodcutting && _woodcuttingBonuses.bonusXpChance > 0f &&
                            UnityEngine.Random.value < _woodcuttingBonuses.bonusXpChance)
                            sm.AddXp(skill, def.xpPerTick, def.displayName);
                    }
                }

                if (isWoodcutting && gritProc && characterStats != null &&
                    _woodcuttingBonuses.gritProcRestoreStaminaFraction > 0f)
                {
                    float restore = _woodcuttingBonuses.gritProcRestoreStaminaFraction * Mathf.Max(1f, characterStats.MaxEnergy);
                    characterStats.AddEnergy(restore);
                }

                if (isWoodcutting && gritProc && _woodcuttingBonuses.frenzyStacks > 0)
                    _woodcuttingFrenzyUntil = Time.time + WoodcuttingFrenzyDurationSeconds;

                if (isFishing && gritProc && characterStats != null && characterStats.RodFishingFrenzyStacks > 0)
                    _fishingFrenzyUntil = Time.time + FishingGritFrenzyDurationSeconds;
            }
        }

        // ---- 2) BONUS drops (NO XP from these) ----
        _drops.Clear();
        // Bonus Resource Find Chance scales ONLY bonus roll chances:
        // effectiveChance = baseChance * (1 + bonusFindChance)
        // Base yield amount is intentionally unaffected.
        float bonusFindForRoll = _gatherBonusFindChance;
        if (isWoodcutting && gritProc && GetWoodcuttingLevel15RowPick() == 1 &&
            GetWoodcuttingLevel15EnhancementIndex(1) == 0)
            bonusFindForRoll += 0.10f;
        if (isFishing && gritProc && fishingMajorPick == 1 && fishingMajorEnhancement == 0)
            bonusFindForRoll += 0.10f;
        if (isFishing && IsFishingMajorCalmWatersSelected() && _fishingCalmWatersMajorStacks > 0)
            bonusFindForRoll += 0.01f * _fishingCalmWatersMajorStacks;
        if (isWoodcutting)
            bonusFindForRoll += GetWoodcuttingLevel35BonusFindAdd();
        if (isWoodcutting && abilityController != null)
            bonusFindForRoll *= abilityController.GetAvatarOfTheForestBonusFindFinalMultiplier();
        var dropCtx = isWoodcutting ? BuildWoodcuttingLevel35DropContext() : default;
        def.PreviewDrops(_drops, bonusFindForRoll, dropCtx, GetGatherSkillLevel(def.actionType));

        // PreviewDrops includes main too, so we must ignore index 0 main OR skip matching itemId
        // Easiest: process ONLY entries that are NOT the main yield itemId
        for (int i = 0; i < _drops.Count; i++)
        {
            var d = _drops[i];
            if (string.IsNullOrWhiteSpace(d.itemId) || d.amount <= 0) continue;

            // Skip main pool items because we already handled them above
            if (def.IsMainYieldPoolItem(d.itemId)) continue;

            int bonusAmt = targetNode.ApplyDepletedYieldPenaltyThisTick
                ? RollDepletedGatherYield(d.amount, def.depletedYieldMultiplier)
                : d.amount;
            if (bonusAmt <= 0) continue;

            int added = inventory.AddPartial(d.itemId, bonusAmt);
            int overflow = bonusAmt - added;

            if (added > 0)
                SessionTrackerData.EnsureInstance().RegisterLootGain(def.displayName, d.itemId, added);

            if (overflow > 0)
            {
                if (dropOverflowToGround)
                {
                    var itemDef = inventory.GetItemDef(d.itemId);
                    Sprite icon = itemDef ? itemDef.icon : null;

                    if (DropManager.Instance != null)
                        DropManager.Instance.Spawn(d.itemId, overflow, icon, def.displayName);
                    else if (worldDropPrefab != null)
                    {
                        float scatterX = UnityEngine.Random.Range(-dropScatterRadius, dropScatterRadius);
                        Vector3 spawnPos = transform.position + new Vector3(scatterX, 0.1f, 0f);

                        var drop = Instantiate(worldDropPrefab, spawnPos, Quaternion.identity);
                        drop.Init(d.itemId, overflow, icon);
                        drop.SetSourceName(def.displayName);
                    }
                }

                ShowPopup("Inventory Full!");
            }
        }

        if (def.HasMainYield)
            targetNode.NotifyGatherTickFinishedDepletionCheck();
    }

    // ---- Cleaving Chop secondary gather timers ---------------------------------------------------
    // Each in-range Woodcutting node accumulates its OWN gather progress while the buff is active, so a
    // splitwood tree ticks on its (fast) interval while you're chopping a hardwood — the cleave isn't
    // gated by the primary tree's pace. Timers are cleared whenever the buff or gather session ends.

    private struct CleavingSecondaryTimer
    {
        public float accum;
        public float nextInterval;
    }

    private readonly Dictionary<ResourceNode, CleavingSecondaryTimer> _cleavingSecondaryTimers = new();
    private readonly List<ResourceNode> _cleavingSecondaryTimersScratch = new();
    private readonly List<ResourceNode> _cleavingSecondaryNodeQueryBuf = new();
    private float _cleavingSecondaryNodeQueryRefreshAt;
    private const float CleavingSecondaryNodeQueryRefreshSeconds = 0.5f;

    /// <summary>
    /// Drives Cleaving Chop's per-tree gather timers. Called from <see cref="TickGather"/> so secondaries
    /// only advance while the player is actively gathering — pausing for combat, fatigue, low energy, etc.
    /// Each tree uses its own <see cref="ResourceNode.GetNextInterval"/> random cadence.
    /// </summary>
    private void AdvanceCleavingChopSecondaryGathers(float deltaSeconds)
    {
        if (abilityController == null || !abilityController.IsCleavingChopActive)
        {
            ClearCleavingSecondaryTimers();
            return;
        }

        if (targetNode == null || targetNode.ActionType != NodeAction.Woodcutting)
            return;
        if (inventory == null)
            return;

        float range = abilityController.GetCleavingChopRange();
        if (range <= 0f)
            return;

        float efficiency = abilityController.GetCleavingChopSecondaryYieldEfficiency();
        if (efficiency <= 0f)
            return;

        Vector3 origin = targetNode.transform.position;
        float rangeSqr = range * range;
        float speedMult = GetEffectiveGatherSpeedMultiplier();

        RefreshCleavingSecondaryCandidatesIfNeeded(origin, rangeSqr);

        // Advance every tracked tree's own timer and fire a secondary gather whenever its interval lapses.
        _cleavingSecondaryTimersScratch.Clear();
        _cleavingSecondaryTimersScratch.AddRange(_cleavingSecondaryTimers.Keys);
        for (int i = 0; i < _cleavingSecondaryTimersScratch.Count; i++)
        {
            ResourceNode node = _cleavingSecondaryTimersScratch[i];
            if (!IsValidCleavingSecondaryTarget(node, origin, rangeSqr))
            {
                _cleavingSecondaryTimers.Remove(node);
                continue;
            }

            var nodeDef = node.Definition;
            var entry = _cleavingSecondaryTimers[node];

            if (entry.nextInterval <= 0f)
                entry.nextInterval = node.GetNextInterval();

            entry.accum += deltaSeconds * speedMult;
            while (entry.accum >= entry.nextInterval && entry.nextInterval > 0f)
            {
                entry.accum -= entry.nextInterval;
                DoOneCleavingSecondaryYield(node, efficiency);
                entry.nextInterval = node.GetNextInterval();
            }

            _cleavingSecondaryTimers[node] = entry;
        }
    }

    /// <summary>Reset timers when the buff drops or the gather session ends.</summary>
    private void ClearCleavingSecondaryTimers()
    {
        if (_cleavingSecondaryTimers.Count > 0)
            _cleavingSecondaryTimers.Clear();
        _cleavingSecondaryNodeQueryRefreshAt = 0f;
    }

    /// <summary>
    /// Re-scan the scene at most every <see cref="CleavingSecondaryNodeQueryRefreshSeconds"/> seconds so newly
    /// spawned or revealed trees join the cleave, and de-pool any that left range / depleted.
    /// </summary>
    private void RefreshCleavingSecondaryCandidatesIfNeeded(Vector3 origin, float rangeSqr)
    {
        if (Time.time < _cleavingSecondaryNodeQueryRefreshAt)
            return;
        _cleavingSecondaryNodeQueryRefreshAt = Time.time + CleavingSecondaryNodeQueryRefreshSeconds;

        _cleavingSecondaryNodeQueryBuf.Clear();
        ResourceNode[] all = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            ResourceNode node = all[i];
            if (!IsValidCleavingSecondaryTarget(node, origin, rangeSqr))
                continue;
            _cleavingSecondaryNodeQueryBuf.Add(node);
            if (!_cleavingSecondaryTimers.ContainsKey(node))
                _cleavingSecondaryTimers.Add(node, default);
        }
    }

    private bool IsValidCleavingSecondaryTarget(ResourceNode node, Vector3 origin, float rangeSqr)
    {
        if (!node || node == targetNode)
            return false;
        if (node.ActionType != NodeAction.Woodcutting)
            return false;
        if (!MeetsNodeLevelRequirement(node))
            return false;
        if (node.IsDepleted)
            return false;
        if (node.Definition == null || !node.Definition.HasMainYield)
            return false;

        float dx = node.transform.position.x - origin.x;
        float dy = node.transform.position.y - origin.y;
        return dx * dx + dy * dy <= rangeSqr;
    }

    /// <summary>
    /// Single secondary "swing" outcome for <paramref name="node"/>: rolls its own yield range, applies the
    /// efficiency multiplier stochastically, then deposits to inventory or drops overflow to the floor.
    /// Depletion DOES advance (same as a normal chop) so cleaved trees eventually run out, but bonus drops
    /// and XP are intentionally skipped — secondaries only ever yield the tree's main log item.
    /// </summary>
    private void DoOneCleavingSecondaryYield(ResourceNode node, float efficiency)
    {
        if (!node || node.Definition == null || !node.Definition.HasMainYield)
            return;
        if (!MeetsNodeLevelRequirement(node))
            return;
        if (inventory == null)
            return;

        var nodeDef = node.Definition;

        bool countTowardDepletion = !(abilityController != null && abilityController.IsAvatarOfTheForestActive);
        node.NotifyGatherTickBeforeBonuses(countTowardDepletion);

        int woodGatherLevel = GetGatherSkillLevel(NodeAction.Woodcutting);
        nodeDef.RollMainYieldCounts(_mainYieldScratch, woodGatherLevel, null);
        int rolled = NodeDefinition.SumMainYieldCounts(_mainYieldScratch);
        if (rolled <= 0)
        {
            node.NotifyGatherTickFinishedDepletionCheck();
            return;
        }

        int secondaryAmt = RollCleavingChopSecondaryYield(rolled, efficiency);

        if (node.ApplyDepletedYieldPenaltyThisTick)
            secondaryAmt = RollDepletedGatherYield(secondaryAmt, nodeDef.depletedYieldMultiplier);

        if (secondaryAmt <= 0)
        {
            node.NotifyGatherTickFinishedDepletionCheck();
            return;
        }

        string yieldId = nodeDef.GetPrimaryYieldItemIdForSkillLevel(woodGatherLevel);
        if (string.IsNullOrWhiteSpace(yieldId))
        {
            node.NotifyGatherTickFinishedDepletionCheck();
            return;
        }

        int added = inventory.AddPartial(yieldId, secondaryAmt);
        int overflow = secondaryAmt - added;

        if (added > 0)
            SessionTrackerData.EnsureInstance().RegisterLootGain(nodeDef.displayName, yieldId, added);

        if (overflow > 0 && dropOverflowToGround)
        {
            var itemDef = inventory.GetItemDef(yieldId);
            Sprite icon = itemDef ? itemDef.icon : null;

            if (DropManager.Instance != null)
                DropManager.Instance.Spawn(yieldId, overflow, icon, nodeDef.displayName);
            else if (worldDropPrefab != null)
            {
                float scatterX = UnityEngine.Random.Range(-dropScatterRadius, dropScatterRadius);
                Vector3 spawnPos = node.transform.position + new Vector3(scatterX, 0.1f, 0f);

                var drop = Instantiate(worldDropPrefab, spawnPos, Quaternion.identity);
                drop.Init(yieldId, overflow, icon);
                drop.SetSourceName(nodeDef.displayName);
            }
        }

        // Final depletion check: matches the primary-tick contract — if this swing was the cap hit, the
        // tree now flips to depleted (visual overlay, regen timer, etc.) and drops out of the cleave set
        // automatically on the next IsValidCleavingSecondaryTarget filter.
        node.NotifyGatherTickFinishedDepletionCheck();
    }

    /// <summary>
    /// Stochastically reduce <paramref name="amount"/> by <paramref name="efficiency"/> (0..1). Same shape
    /// as <see cref="RollDepletedGatherYield"/> so small main rolls (e.g. 1 log) average to the configured
    /// efficiency instead of being clamped to 1.
    /// </summary>
    private static int RollCleavingChopSecondaryYield(int amount, float efficiency)
    {
        if (amount <= 0)
            return 0;
        float m = Mathf.Clamp01(efficiency);
        if (m >= 1f)
            return amount;
        int sum = 0;
        for (int i = 0; i < amount; i++)
        {
            if (UnityEngine.Random.value < m)
                sum++;
        }

        return sum;
    }

    private void TryDepositGatherMainLoot(string itemId, int amount, string sourceDisplayName)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId) || inventory == null)
            return;

        int added = inventory.AddPartial(itemId, amount);
        int overflow = amount - added;

        if (added > 0)
            SessionTrackerData.EnsureInstance().RegisterLootGain(sourceDisplayName, itemId, added);

        if (overflow <= 0)
            return;

        if (dropOverflowToGround)
        {
            var itemDef = inventory.GetItemDef(itemId);
            Sprite icon = itemDef ? itemDef.icon : null;

            if (DropManager.Instance != null)
                DropManager.Instance.Spawn(itemId, overflow, icon, sourceDisplayName);
            else if (worldDropPrefab != null)
            {
                float scatterX = UnityEngine.Random.Range(-dropScatterRadius, dropScatterRadius);
                Vector3 spawnPos = transform.position + new Vector3(scatterX, 0.1f, 0f);

                var drop = Instantiate(worldDropPrefab, spawnPos, Quaternion.identity);
                drop.Init(itemId, overflow, icon);
                drop.SetSourceName(sourceDisplayName);
            }
        }

        ShowPopup("Inventory Full!");
    }

    /// <summary>
    /// Per-item stochastic yield while depleted so small base amounts (e.g. 1 log/tick) average to
    /// <paramref name="depletedMultiplier"/> of the raw roll instead of staying stuck at 1.
    /// </summary>
    private static int RollDepletedGatherYield(int amount, float depletedMultiplier)
    {
        if (amount <= 0)
            return 0;
        float m = Mathf.Clamp01(depletedMultiplier);
        int sum = 0;
        for (int i = 0; i < amount; i++)
        {
            if (UnityEngine.Random.value < m)
                sum++;
        }

        return sum;
    }

    private void ReturnToIdle(bool keepFatigueGatherProgress = false)
    {
        if (!keepFatigueGatherProgress)
            ClearFatigueGatherProgress();

        CaptureWoodcuttingFlowLingerOnGatherStop();
        CaptureFishingCalmWatersOnGatherStop();

        _gatherSpeedMultiplier = 1f;
        _gatherGritChance = 0f;
        _gatherBonusFindChance = 0f;
        _gatherStaminaEfficiency = 0f;
        _activeFishingBaitSpeedBonusFraction = 0f;
        ResetWoodcuttingRuntimeState(clearBonuses: true);
        ResetFishingRuntimeState(clearBonuses: true);
        ClearCleavingSecondaryTimers();
        OnGatherDebuffChanged?.Invoke(false, 1f);

        targetNode = null;
        _pickupTarget = null;
        _moveToPointFromPlayerInput = false;
        state = State.Idle;

        _accumItems = 0f;
        _gatherTimer = 0f;
        _nextGatherInterval = 0f;
        _nextGatherAnimTime = 0f;

        _hideHandsForUnarmedGather = false;
        equipment?.ClearHideBothHandsOverride();
        equipment?.ClearMainHandUnarmedOverride();
        equipment?.ClearMainHandVisualOverride();

        SetAction(PlayerAction.Idle, true);
    }

    private float GetEffectiveGatherStaminaCostPerTick()
    {
        // Future stamina hook:
        // effectiveStaminaCost = baseStaminaCost * (1 - staminaEfficiency)
        // clamped so cost never drops below zero.
        float baseCost = Mathf.Max(0f, baseGatherStaminaCostPerTick);
        return Mathf.Max(0f, baseCost * (1f - Mathf.Clamp01(_gatherStaminaEfficiency)));
    }

    private bool TrySpendGatherEnergyOnSwing(NodeDefinition def)
    {
        _lastGatherSwingSpendFailureReason = GatherSwingSpendFailureReason.None;
        if (def == null || characterStats == null) return true;

        // Energy model: node defines % of max energy per swing (stable swings per full bar as max energy grows).
        // Stamina efficiency reduces that cost.
        float pctOfMax = Mathf.Clamp01(def.energyCostPercentOfMaxPerSwing / 100f);
        if (pctOfMax <= 0f)
            return true;

        if (targetNode && targetNode.ActionType == NodeAction.Woodcutting &&
            abilityController != null && abilityController.IsAvatarOfTheForestActive)
            return true;

        if (targetNode && targetNode.ActionType == NodeAction.Fishing)
        {
            if (HasFishingRodInToolbelt())
            {
                if (!TryConsumeBestFishingBaitForSwing(out float baitSpeedBonusFraction))
                {
                    _lastGatherSwingSpendFailureReason = GatherSwingSpendFailureReason.MissingFishingBait;
                    return false;
                }

                _activeFishingBaitSpeedBonusFraction = Mathf.Max(0f, baitSpeedBonusFraction);
            }
            else
            {
                // No rod in toolbelt: unarmed-style fishing — no bait consumed; gather debuff still applies from missing tool path.
                _activeFishingBaitSpeedBonusFraction = 0f;
            }
        }
        else
        {
            _activeFishingBaitSpeedBonusFraction = 0f;
        }

        float maxEnergy = Mathf.Max(1f, characterStats.MaxEnergy);
        float baseCostPerSwing = maxEnergy * pctOfMax;
        float staminaEfficiency = Mathf.Clamp01(_gatherStaminaEfficiency);
        if (targetNode && targetNode.ActionType == NodeAction.Woodcutting &&
            _woodcuttingBonuses.forestFlowStacks > 0 &&
            _woodcuttingContinuousGatherSeconds >= WoodcuttingForestFlowContinuousSecondsThreshold)
            staminaEfficiency = Mathf.Clamp01(staminaEfficiency + 0.03f * _woodcuttingBonuses.forestFlowStacks);

        if (targetNode && targetNode.ActionType == NodeAction.Woodcutting && IsWoodcuttingMajorFlowBuffActive())
            staminaEfficiency = Mathf.Clamp01(staminaEfficiency + 0.10f);

        if (targetNode && targetNode.ActionType == NodeAction.Fishing &&
            _fishingBonuses.calmWatersStacks > 0 &&
            _fishingContinuousGatherSeconds >= FishingCalmWatersContinuousSecondsThreshold)
            staminaEfficiency = Mathf.Clamp01(staminaEfficiency + 0.03f * _fishingBonuses.calmWatersStacks);

        float spendPerSwing = Mathf.Max(0f, baseCostPerSwing * (1f - staminaEfficiency));

        if (targetNode && targetNode.ActionType == NodeAction.Woodcutting &&
            _woodcuttingBonuses.noStaminaSwingChance > 0f &&
            UnityEngine.Random.value < _woodcuttingBonuses.noStaminaSwingChance)
            spendPerSwing = 0f;

        if (spendPerSwing <= 0f) return true;

        bool spent = characterStats.SpendEnergy(spendPerSwing);
        if (!spent)
            _lastGatherSwingSpendFailureReason = GatherSwingSpendFailureReason.LowEnergy;
        return spent;
    }

    private float GetEffectiveGatherSpeedMultiplier()
    {
        float mult = _gatherSpeedMultiplier;
        if (targetNode == null)
            return Mathf.Max(0.05f, mult);

        if (targetNode.ActionType == NodeAction.Fishing)
        {
            float fishMult = _gatherSpeedMultiplier * (1f + Mathf.Max(0f, _activeFishingBaitSpeedBonusFraction));
            float fishBonus = 0f;
            if (_fishingBonuses.calmWatersStacks > 0 &&
                _fishingContinuousGatherSeconds >= FishingCalmWatersContinuousSecondsThreshold)
                fishBonus += 0.03f * _fishingBonuses.calmWatersStacks;
            if (IsFishingMajorCalmWatersSelected() && _fishingCalmWatersMajorStacks > 0)
                fishBonus += 0.02f * _fishingCalmWatersMajorStacks;
            if (_fishingBonuses.frenzyStacks > 0 && Time.time < _fishingFrenzyUntil)
                fishBonus += 0.05f * _fishingBonuses.frenzyStacks;
            return Mathf.Max(0.05f, fishMult * (1f + fishBonus));
        }

        if (targetNode.ActionType != NodeAction.Woodcutting)
            return Mathf.Max(0.05f, mult);

        float bonus = 0f;
        if (_woodcuttingBonuses.forestFlowStacks > 0 &&
            _woodcuttingContinuousGatherSeconds >= WoodcuttingForestFlowContinuousSecondsThreshold)
            bonus += 0.03f * _woodcuttingBonuses.forestFlowStacks;
        if (_woodcuttingBonuses.frenzyStacks > 0 && Time.time < _woodcuttingFrenzyUntil)
            bonus += 0.05f * _woodcuttingBonuses.frenzyStacks;
        if (IsWoodcuttingMajorFlowBuffActive())
            bonus += 0.10f;

        float speed = mult * (1f + bonus);
        if (abilityController != null)
            speed += abilityController.GetAvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd();

        return Mathf.Max(0.05f, speed);
    }

    private bool HasAnyFishingBaitInInventory()
    {
        return TryGetBestFishingBaitItemId(out _, out _);
    }

    private bool TryConsumeBestFishingBaitForSwing(out float speedBonusFraction)
    {
        speedBonusFraction = 0f;
        if (inventory == null)
            return false;

        if (!TryGetBestFishingBaitItemId(out string baitItemId, out ItemDefinition baitDef))
            return false;

        if (!inventory.Remove(baitItemId, 1))
            return false;

        speedBonusFraction = baitDef != null ? baitDef.FishingBaitSpeedBonusFraction : 0f;
        return true;
    }

    private bool TryGetBestFishingBaitItemId(out string itemId, out ItemDefinition def)
    {
        itemId = null;
        def = null;
        if (inventory == null)
            return false;

        int bestTier = int.MinValue;
        float bestSpeed = float.MinValue;
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            Inventory.Slot slot = inventory.GetSlot(i);
            if (slot.IsEmpty || string.IsNullOrWhiteSpace(slot.itemId) || slot.amount <= 0)
                continue;

            ItemDefinition candidate = inventory.GetItemDef(slot.itemId);
            if (candidate == null || !candidate.IsFishingBait)
                continue;

            int tier = (int)candidate.FishingBaitTier;
            float speed = candidate.FishingBaitSpeedBonusFraction;
            if (tier > bestTier || (tier == bestTier && speed > bestSpeed))
            {
                bestTier = tier;
                bestSpeed = speed;
                itemId = slot.itemId;
                def = candidate;
            }
        }

        return !string.IsNullOrWhiteSpace(itemId);
    }

    private void StopFishingForMissingBait()
    {
        ShowPopup(MissingFishingBaitPopupText);
        ReturnToIdle();
    }

    /// <summary>
    /// For stats UI: Forest Flow / grit frenzy / Major Flow fractions stack additively, then multiply sheet axe speed.
    /// Avatar of the Forest is shown as a flat +0.10 on that result (not inside the <c>(1 + Σ)</c> bracket); see
    /// <see cref="EquipmentStatsPanelUI.PopulateGatheringToolsSection"/>. Major Flow can apply briefly after stopping (Lv15 choice).
    /// </summary>
    public bool TryGetWoodcuttingLiveBuffInfo(
        out float frenzySpeedFraction,
        out float forestFlowSpeedFraction,
        out float forestFlowStaminaEfficiencyAddFraction,
        out float majorFlowSpeedFraction,
        out float majorFlowStaminaEfficiencyAddFraction)
    {
        frenzySpeedFraction = 0f;
        forestFlowSpeedFraction = 0f;
        forestFlowStaminaEfficiencyAddFraction = 0f;
        majorFlowSpeedFraction = 0f;
        majorFlowStaminaEfficiencyAddFraction = 0f;

        bool inWoodGather = state == State.Gather && targetNode != null && targetNode.ActionType == NodeAction.Woodcutting;
        bool any = false;
        if (inWoodGather)
        {
            if (_woodcuttingBonuses.forestFlowStacks > 0 &&
                _woodcuttingContinuousGatherSeconds >= WoodcuttingForestFlowContinuousSecondsThreshold)
            {
                float ff = 0.03f * _woodcuttingBonuses.forestFlowStacks;
                forestFlowSpeedFraction = ff;
                forestFlowStaminaEfficiencyAddFraction = ff;
                any = true;
            }

            if (_woodcuttingBonuses.frenzyStacks > 0 && Time.time < _woodcuttingFrenzyUntil)
            {
                frenzySpeedFraction = 0.05f * _woodcuttingBonuses.frenzyStacks;
                any = true;
            }
        }

        if (IsWoodcuttingMajorFlowBuffActive())
        {
            majorFlowSpeedFraction = 0.10f;
            majorFlowStaminaEfficiencyAddFraction = 0.10f;
            any = true;
        }

        return any;
    }

    /// <summary>Lv15 Flow State — Deep Focus: +10% Grit Chance while Flow is active (UI display).</summary>
    public bool TryGetWoodcuttingMajorFlowDeepFocusGritBonus(out float additiveGritChance)
    {
        additiveGritChance = 0f;
        if (!IsWoodcuttingMajorFlowDeepFocusSelected())
            return false;
        if (!IsWoodcuttingMajorFlowBuffActive())
            return false;
        additiveGritChance = 0.10f;
        return true;
    }

    /// <summary>Changes when woodcutting gather transient buffs change; used to refresh tool stats without full sheet churn.</summary>
    public int GetWoodcuttingStatsPanelStamp()
    {
        _ = TryGetWoodcuttingLiveBuffInfo(out float frenFrac, out float ffSpd, out float ffStam, out float majSpd, out float majStam);

        float avatarSpd = abilityController != null ? abilityController.GetAvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd() : 0f;
        float avatarBfMul = abilityController != null ? abilityController.GetAvatarOfTheForestBonusFindFinalMultiplier() : 1f;

        int h0 = HashCode.Combine(
            Mathf.RoundToInt(frenFrac * 1000f),
            Mathf.RoundToInt(ffSpd * 1000f),
            Mathf.RoundToInt(ffStam * 1000f),
            Mathf.RoundToInt(majSpd * 1000f),
            Mathf.RoundToInt(avatarSpd * 1000f),
            Mathf.RoundToInt(avatarBfMul * 1000f));
        int h1 = HashCode.Combine(
            Mathf.RoundToInt(majStam * 1000f),
            _woodcuttingBonuses.forestFlowStacks,
            _woodcuttingBonuses.frenzyStacks,
            GetWoodcuttingLevel15BuildStamp());
        return HashCode.Combine(h0, h1, Mathf.RoundToInt(_woodcuttingFlowLingerUntil * 100f));
    }

    /// <summary>Fishing Grit Frenzy + Calm Waters (continuous fishing): fractions for stats panel live display.</summary>
    public bool TryGetFishingLiveBuffInfo(out float frenzySpeedFraction, out float calmWatersSpeedFraction, out float calmWatersStaminaEfficiencyAddFraction)
    {
        frenzySpeedFraction = 0f;
        calmWatersSpeedFraction = 0f;
        calmWatersStaminaEfficiencyAddFraction = 0f;

        bool inFishGather = state == State.Gather && targetNode != null && targetNode.ActionType == NodeAction.Fishing;
        bool any = false;
        if (inFishGather)
        {
            if (_fishingBonuses.calmWatersStacks > 0 &&
                _fishingContinuousGatherSeconds >= FishingCalmWatersContinuousSecondsThreshold)
            {
                float cw = 0.03f * _fishingBonuses.calmWatersStacks;
                calmWatersSpeedFraction = cw;
                calmWatersStaminaEfficiencyAddFraction = cw;
                any = true;
            }

            if (IsFishingMajorCalmWatersSelected() && _fishingCalmWatersMajorStacks > 0)
            {
                calmWatersSpeedFraction += 0.02f * _fishingCalmWatersMajorStacks;
                any = true;
            }
        }

        if (_fishingBonuses.frenzyStacks > 0 && Time.time < _fishingFrenzyUntil)
        {
            frenzySpeedFraction = 0.05f * _fishingBonuses.frenzyStacks;
            any = true;
        }

        return any;
    }

    /// <summary>Changes when fishing gather transient buffs change; used to refresh Rod stats without full sheet churn.</summary>
    public int GetFishingStatsPanelStamp()
    {
        _ = TryGetFishingLiveBuffInfo(out float fren, out float calmSpd, out float calmStam);

        int h0 = HashCode.Combine(
            Mathf.RoundToInt(fren * 1000f),
            Mathf.RoundToInt(calmSpd * 1000f),
            Mathf.RoundToInt(calmStam * 1000f));
        int h1 = HashCode.Combine(
            _fishingBonuses.calmWatersStacks,
            _fishingBonuses.frenzyStacks,
            Mathf.RoundToInt(_fishingFrenzyUntil * 100f));
        int h2 = HashCode.Combine(
            _fishingCalmWatersMajorStacks,
            _fishingCalmWatersLingerStacks,
            GetFishingLevel15BuildStamp(),
            Mathf.RoundToInt(_fishingCalmWatersMajorStackTimer * 1000f));
        return HashCode.Combine(h0, h1, h2);
    }

    private int GetWoodcuttingLevel15RowPick()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null) return -1;
        int pick = sm.GetSkillAbilityRowPick(SkillType.Woodcutting, WoodcuttingMajorPassiveSourceLevel, -1);
        if (pick < 0 || pick > 2)
            return -1;
        return pick;
    }

    private int GetWoodcuttingLevel15EnhancementIndex(int rowPick)
    {
        if (rowPick < 0 || rowPick > 2)
            return -1;
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null) return -1;
        int enh = sm.GetSkillChoiceSelection(SkillType.Woodcutting, WoodcuttingLevel15ChoiceSpineId(rowPick), -1);
        if (enh < 0 || enh > 1)
            return -1;
        return enh;
    }

    private int GetWoodcuttingLevel15BuildStamp()
    {
        int pick = GetWoodcuttingLevel15RowPick();
        return HashCode.Combine(pick, GetWoodcuttingLevel15EnhancementIndex(pick));
    }

    private int GetFishingLevel15RowPick()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null) return -1;
        int pick = sm.GetSkillAbilityRowPick(SkillType.Fishing, FishingMajorPassiveSourceLevel, -1);
        if (pick < 0 || pick > 2)
            return -1;
        return pick;
    }

    private int GetFishingLevel15EnhancementIndex(int rowPick)
    {
        if (rowPick < 0 || rowPick > 2)
            return -1;
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null) return -1;
        int enh = sm.GetSkillChoiceSelection(SkillType.Fishing, FishingLevel15ChoiceSpineId(rowPick), -1);
        if (enh < 0 || enh > 1)
            return -1;
        return enh;
    }

    private int GetFishingLevel15BuildStamp()
    {
        int pick = GetFishingLevel15RowPick();
        return HashCode.Combine(pick, GetFishingLevel15EnhancementIndex(pick));
    }

    private bool IsFishingMajorCalmWatersSelected() => GetFishingLevel15RowPick() == 2;

    private bool IsFishingMajorLastingWatersSelected() =>
        GetFishingLevel15RowPick() == 2 && GetFishingLevel15EnhancementIndex(2) == 0;

    private bool IsFishingMajorDeepWatersSelected() =>
        GetFishingLevel15RowPick() == 2 && GetFishingLevel15EnhancementIndex(2) == 1;

    private int GetFishingCalmWatersMaxStacks() =>
        GatheringPassiveTooltipText.GetFishingCalmWatersMaxStacks(IsFishingMajorDeepWatersSelected());

    private int GetFishingCalmWatersHudDisplayStacks()
    {
        if (state == State.Gather && targetNode && targetNode.ActionType == NodeAction.Fishing)
            return _fishingCalmWatersMajorStacks;
        return _fishingCalmWatersLingerStacks;
    }

    private void CaptureFishingCalmWatersOnGatherStop()
    {
        bool wasFishGather = state == State.Gather && targetNode && targetNode.ActionType == NodeAction.Fishing;
        if (!wasFishGather)
            return;

        if (IsFishingMajorCalmWatersSelected() && IsFishingMajorLastingWatersSelected())
        {
            _fishingCalmWatersLingerStacks = _fishingCalmWatersMajorStacks;
            if (_fishingCalmWatersLingerStacks > 0)
                _fishingCalmWatersNextLingerDecayAt = Time.time + FishingCalmWatersLingerDecayIntervalSeconds;
        }
        else
            _fishingCalmWatersLingerStacks = 0;
    }

    private void TickFishingCalmWatersLingerDecay()
    {
        if (_fishingCalmWatersLingerStacks <= 0)
            return;
        if (state == State.Gather && targetNode && targetNode.ActionType == NodeAction.Fishing)
            return;
        if (Time.time < _fishingCalmWatersNextLingerDecayAt)
            return;

        _fishingCalmWatersLingerStacks--;
        _fishingCalmWatersNextLingerDecayAt = Time.time + FishingCalmWatersLingerDecayIntervalSeconds;
    }

    private void SyncFishingCalmWatersMajorHudBuffIfNeeded()
    {
        if (!_buffControllerCache)
            _buffControllerCache = GetComponent<PlayerBuffController>();
        if (!_buffControllerCache)
            return;

        if (!IsFishingMajorCalmWatersSelected())
        {
            if (_fishingCalmWatersMajorHudRegistered)
            {
                _buffControllerCache.ClearHudAbilityBuff(FishingCalmWatersMajorHudBuffId);
                _fishingCalmWatersMajorHudRegistered = false;
                _fishingCalmWatersMajorHudLastStacks = int.MinValue;
            }
            return;
        }

        int stacks = GetFishingCalmWatersHudDisplayStacks();
        if (stacks <= 0)
        {
            if (_fishingCalmWatersMajorHudRegistered)
            {
                _buffControllerCache.ClearHudAbilityBuff(FishingCalmWatersMajorHudBuffId);
                _fishingCalmWatersMajorHudRegistered = false;
                _fishingCalmWatersMajorHudLastStacks = int.MinValue;
            }
            return;
        }

        if (stacks == _fishingCalmWatersMajorHudLastStacks && _fishingCalmWatersMajorHudRegistered)
            return;

        _buffControllerCache.SetHudAbilityBuff(FishingCalmWatersMajorHudBuffId, stacks, 0f, 0f);
        _fishingCalmWatersMajorHudRegistered = true;
        _fishingCalmWatersMajorHudLastStacks = stacks;
    }

    private bool IsWoodcuttingMajorFlowBuffActive()
    {
        if (!IsWoodcuttingMajorFlowStateSelected())
            return false;
        if (state == State.Gather && targetNode && targetNode.ActionType == NodeAction.Woodcutting &&
            _woodcuttingContinuousGatherSeconds >= WoodcuttingForestFlowContinuousSecondsThreshold)
            return true;
        return IsWoodcuttingMajorFlowLingerSelected() && Time.time < _woodcuttingFlowLingerUntil;
    }

    /// <summary>
    /// Called from every code path that ends a woodcutting gather (ReturnToIdle, CancelAction, MoveToPointX, etc.).
    /// Captures the Lasting Focus 5s linger when Flow State was active, so the buff persists across click-to-move
    /// and other interrupts instead of dropping the moment the player leaves the Gather state.
    /// </summary>
    private void CaptureWoodcuttingFlowLingerOnGatherStop()
    {
        bool wasWoodGather = state == State.Gather && targetNode && targetNode.ActionType == NodeAction.Woodcutting;
        if (!wasWoodGather)
            return;

        if (IsWoodcuttingMajorFlowLingerSelected() &&
            _woodcuttingContinuousGatherSeconds >= WoodcuttingForestFlowContinuousSecondsThreshold)
        {
            _woodcuttingFlowLingerUntil = Time.time + 5f;
        }
        else
        {
            _woodcuttingFlowLingerUntil = 0f;
        }
    }

    private bool IsWoodcuttingMajorFlowStateSelected()
    {
        return GetWoodcuttingLevel15RowPick() == 2;
    }

    private bool IsWoodcuttingMajorFlowLingerSelected()
    {
        return GetWoodcuttingLevel15RowPick() == 2 && GetWoodcuttingLevel15EnhancementIndex(2) == 0;
    }

    private bool IsWoodcuttingMajorFlowDeepFocusSelected()
    {
        return GetWoodcuttingLevel15RowPick() == 2 && GetWoodcuttingLevel15EnhancementIndex(2) == 1;
    }

    private void SyncWoodcuttingFlowStateHudBuffIfNeeded()
    {
        if (!_buffControllerCache)
            _buffControllerCache = GetComponent<PlayerBuffController>();
        if (!_buffControllerCache)
            return;

        if (!IsWoodcuttingMajorFlowStateSelected())
        {
            if (_woodcuttingFlowStateHudRegistered)
            {
                _buffControllerCache.ClearHudAbilityBuff(WoodcuttingFlowStateHudBuffId);
                _woodcuttingFlowStateHudRegistered = false;
            }
            return;
        }

        bool show = IsWoodcuttingMajorFlowBuffActive();
        if (show == _woodcuttingFlowStateHudRegistered)
            return;

        if (show)
        {
            _buffControllerCache.SetHudAbilityBuff(WoodcuttingFlowStateHudBuffId, 1, 0f, 0f);
            _woodcuttingFlowStateHudRegistered = true;
        }
        else
        {
            _buffControllerCache.ClearHudAbilityBuff(WoodcuttingFlowStateHudBuffId);
            _woodcuttingFlowStateHudRegistered = false;
        }
    }

    private int GetWoodcuttingLevel35RowPick()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null) return -1;
        // Hard-gate the Lv35 row by the actual unlocked level so legacy/stale commits below 35 cannot
        // leak hidden-drop or bonus-find bonuses into earlier levels.
        if (!sm.IsLevelUnlocked(SkillType.Woodcutting, WoodcuttingLv35MajorPassiveSourceLevel))
            return -1;
        int pick = sm.GetSkillAbilityRowPick(SkillType.Woodcutting, WoodcuttingLv35MajorPassiveSourceLevel, -1);
        if (pick < 0 || pick > 1)
            return -1;
        return pick;
    }

    private int GetWoodcuttingLevel35EnhancementIndex(int rowPick)
    {
        if (rowPick < 0 || rowPick > 1)
            return -1;
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null) return -1;
        int enh = sm.GetSkillChoiceSelection(SkillType.Woodcutting, WoodcuttingLevel35ChoiceSpineId(rowPick), -1);
        if (enh < 0 || enh > 1)
            return -1;
        return enh;
    }

    /// <summary>Builds the woodcutting Lv35 major-passive context applied to bonus and hidden drops (hidden rolls only after a bonus proc on the same tick).</summary>
    private NodeDefinition.GatherDropContext BuildWoodcuttingLevel35DropContext()
    {
        var ctx = default(NodeDefinition.GatherDropContext);
        int pick35 = GetWoodcuttingLevel35RowPick();
        if (pick35 < 0)
            return ctx;

        int enh35 = GetWoodcuttingLevel35EnhancementIndex(pick35);

        if (pick35 == 0)
        {
            ctx.hiddenChanceFlatBonus = 0.10f;
            if (enh35 == 0)
                ctx.hiddenChanceFlatBonus += 0.05f;
            else if (enh35 == 1)
                ctx.hiddenDoubleAmountChance = 0.10f;
        }
        else if (pick35 == 1)
        {
            ctx.bonusDropExtraOneChance = 0.25f;
            if (enh35 == 1)
                ctx.bonusDropExtraOneChance += 0.10f;
        }
        return ctx;
    }

    /// <summary>
    /// Flat 0–1 chance added to each hidden-drop entry after a bonus drop succeeds on the same woodcutting tick
    /// (Ancient Lumbercraft + Experienced Gatherer). Used by stats UI; matches <see cref="BuildWoodcuttingLevel35DropContext"/>.
    /// </summary>
    public float GetWoodcuttingHiddenRevealChanceFlatBonus()
    {
        return BuildWoodcuttingLevel35DropContext().hiddenChanceFlatBonus;
    }

    /// <summary>
    /// Woodcutting Lv50 capstone (Bountiful Chop): +1 main log on the player's primary gather tick only
    /// (see <see cref="DoOneGatherTick"/>). Not used by cleave secondaries or spectral axe gathers.
    /// </summary>
    private static bool IsWoodcuttingCapstoneBonusLogUnlocked()
    {
        SkillsManager sm = SkillsManager.Instance;
        return sm != null && sm.IsLevelUnlocked(SkillType.Woodcutting, WoodcuttingCapstonePassiveRequiredLevel);
    }

    /// <summary>Lv35 Forest's Favor / Rich Harvest adds +10% Bonus Find Chance to gather rolls.</summary>
    private float GetWoodcuttingLevel35BonusFindAdd()
    {
        int pick35 = GetWoodcuttingLevel35RowPick();
        if (pick35 != 1)
            return 0f;
        return GetWoodcuttingLevel35EnhancementIndex(pick35) == 0 ? 0.10f : 0f;
    }

    private void ResetWoodcuttingRuntimeState(bool clearBonuses)
    {
        if (clearBonuses)
            _woodcuttingBonuses = default;
        _woodcuttingContinuousGatherSeconds = 0f;
        _woodcuttingFrenzyUntil = 0f;
    }

    private void ApplyWoodcuttingSkillRuntimeBonusesIfNeeded(ResourceNode node)
    {
        ResetWoodcuttingRuntimeState(clearBonuses: true);
        if (node == null || node.ActionType != NodeAction.Woodcutting)
            return;

        if (characterStats != null)
        {
            _woodcuttingBonuses = new WoodcuttingRuntimeBonuses
            {
                extraLogChance = characterStats.AxeWoodcuttingExtraMainRollChance,
                baseYieldPercent = characterStats.AxeWoodcuttingBaseYieldBonus,
                gritProcRestoreStaminaFraction = characterStats.AxeWoodcuttingGritProcRestoreStaminaFraction,
                bonusXpChance = characterStats.AxeWoodcuttingBonusXpChance,
                noStaminaSwingChance = characterStats.AxeWoodcuttingNoStaminaSwingChance,
                forestFlowStacks = characterStats.AxeWoodcuttingForestFlowStacks,
                frenzyStacks = characterStats.AxeWoodcuttingFrenzyStacks
            };
        }
    }

    private void ResetFishingRuntimeState(bool clearBonuses)
    {
        if (clearBonuses)
            _fishingBonuses = default;
        _fishingContinuousGatherSeconds = 0f;
        _fishingFrenzyUntil = 0f;
        _fishingCalmWatersMajorStacks = 0;
        _fishingCalmWatersMajorStackTimer = 0f;
    }

    private void ApplyFishingSkillRuntimeBonusesIfNeeded(ResourceNode node)
    {
        ResetFishingRuntimeState(clearBonuses: true);
        if (node == null || node.ActionType != NodeAction.Fishing)
            return;

        if (characterStats != null)
        {
            _fishingBonuses = new FishingRuntimeBonuses
            {
                calmWatersStacks = characterStats.RodFishingCalmWatersStacks,
                frenzyStacks = characterStats.RodFishingFrenzyStacks
            };
        }
    }

    private void ApplyGatherCoreStatsFromCharacterAndTool(NodeAction actionType, ItemDefinition toolDef)
    {
        if (characterStats == null)
        {
            _gatherSpeedMultiplier = toolDef ? toolDef.GatherSpeedMultiplier : 1f;
            _gatherGritChance = toolDef ? toolDef.GatheringGrit : 0f;
            _gatherBonusFindChance = toolDef ? toolDef.BonusResourceFindChance : 0f;
            _gatherStaminaEfficiency = toolDef ? toolDef.StaminaEfficiency : 0f;
            return;
        }

        switch (actionType)
        {
            case NodeAction.Woodcutting:
                _gatherSpeedMultiplier = Mathf.Max(0.05f, characterStats.AxeSpeedMult);
                _gatherGritChance = Mathf.Clamp01(characterStats.AxeGrit);
                _gatherBonusFindChance = Mathf.Max(0f, characterStats.AxeBonusFindChance);
                _gatherStaminaEfficiency = Mathf.Clamp01(characterStats.AxeStaminaEfficiency);
                break;
            case NodeAction.Mining:
                _gatherSpeedMultiplier = Mathf.Max(0.05f, characterStats.PickaxeSpeedMult);
                _gatherGritChance = Mathf.Clamp01(characterStats.PickaxeGrit);
                _gatherBonusFindChance = Mathf.Max(0f, characterStats.PickaxeBonusFindChance);
                _gatherStaminaEfficiency = Mathf.Clamp01(characterStats.PickaxeStaminaEfficiency);
                break;
            case NodeAction.Fishing:
                _gatherSpeedMultiplier = Mathf.Max(0.05f, characterStats.RodSpeedMult);
                _gatherGritChance = Mathf.Clamp01(characterStats.RodGrit);
                _gatherBonusFindChance = Mathf.Max(0f, characterStats.RodBonusFindChance);
                _gatherStaminaEfficiency = Mathf.Clamp01(characterStats.RodStaminaEfficiency);
                break;
            default:
                _gatherSpeedMultiplier = toolDef ? toolDef.GatherSpeedMultiplier : 1f;
                _gatherGritChance = toolDef ? toolDef.GatheringGrit : 0f;
                _gatherBonusFindChance = toolDef ? toolDef.BonusResourceFindChance : 0f;
                _gatherStaminaEfficiency = toolDef ? toolDef.StaminaEfficiency : 0f;
                break;
        }
    }

    private void ClearFatigueGatherProgress()
    {
        _fatigueGatherProgressPending = false;
        _fatigueGatherSavedNode = null;
        _fatigueGatherSavedTimer = 0f;
        _fatigueGatherSavedInterval = 0f;
        _fatigueGatherSavedAccum = 0f;
    }

    private void PauseGatherForLowEnergy()
    {
        ResourceNode resumeNode = targetNode;
        ShowPopup(string.IsNullOrWhiteSpace(lowEnergyPopupText) ? "* Fatigued *" : lowEnergyPopupText);
        // Stop gather immediately and return to full idle state, then retry after delay.
        _fatiguedUntilTime = Time.time + Mathf.Max(0.1f, fatiguedResumeDelaySeconds);

        bool savedGatherProgress = resumeNode != null && state == State.Gather;
        if (savedGatherProgress)
        {
            _fatigueGatherProgressPending = true;
            _fatigueGatherSavedNode = resumeNode;
            _fatigueGatherSavedTimer = _gatherTimer;
            _fatigueGatherSavedInterval = _nextGatherInterval;
            _fatigueGatherSavedAccum = _accumItems;
        }

        // ReturnToIdle clears hand visuals; keep pickaxe/tool (or unarmed gather rules) while fatigued.
        bool hideHands = _hideHandsForUnarmedGather;
        ReturnToIdle(savedGatherProgress);
        if (resumeNode != null)
        {
            _hideHandsForUnarmedGather = hideHands;
            ApplyGatherHandVisuals();
        }

        StartFatiguedResume(resumeNode);
    }

    private void StartFatiguedResume(ResourceNode node)
    {
        if (node == null) return;
        if (_fatiguedResumeRoutine != null)
            StopCoroutine(_fatiguedResumeRoutine);
        _fatiguedResumeRoutine = StartCoroutine(FatiguedResumeRoutine(node));
    }

    private IEnumerator FatiguedResumeRoutine(ResourceNode node)
    {
        float wait = Mathf.Max(0.1f, fatiguedResumeDelaySeconds);
        yield return new WaitForSeconds(wait);
        _fatiguedResumeRoutine = null;

        if (_isDead) yield break;
        if (state != State.Idle) yield break;
        if (!node || !node.gameObject.activeInHierarchy) yield break;
        if (AnyEnemyOnMap || InCombat) yield break;
        if (!MeetsNodeLevelRequirement(node)) yield break;

        SelectNode(node);
    }

    private PlayerAction GetGatherAction()
    {
        if (!targetNode || !targetNode.Definition) return PlayerAction.Idle;
        return targetNode.Definition.gatherPlayerAction;
    }

    private void SyncActionBarGatheringStripToAction(PlayerAction action)
    {
        ActionBarUI bar = ResolveActionBarUi();
        if (bar == null)
            return;

        switch (action)
        {
            case PlayerAction.Mining:
                bar.ShowGatheringBarForSkill(SkillType.Mining);
                break;
            case PlayerAction.Woodcutting:
                bar.ShowGatheringBarForSkill(SkillType.Woodcutting);
                break;
            case PlayerAction.Fishing:
                bar.ShowGatheringBarForSkill(SkillType.Fishing);
                break;
            case PlayerAction.Walking:
                // Keep the current gathering strip (or combat bar) while pathing to a node.
                break;
            case PlayerAction.Fighting:
                if (state == State.Gather)
                {
                    // Gather-tree abilities can briefly use combat presentation; keep the active gathering strip visible.
                    break;
                }
                // Combat always returns to weapon sets on the bar; gathering layout is hidden.
                bar.ExitGatheringBarToCombat();
                break;
            default:
                // Idle / Fatigued / etc.: do not clear a gathering strip chosen in town — W/M/F stay until combat or Tab/set swap.
                break;
        }
    }

    private string GetGatherBlockedMessage()
    {
        if (InCombat)
            return "Can't gather while in combat!";
        if (AnyEnemyOnMap)
            return "Can't gather while enemies are on the map!";
        return "Can't gather right now.";
    }

    private ActionBarUI ResolveActionBarUi()
    {
        if (_actionBarUiCache == null)
            _actionBarUiCache = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        return _actionBarUiCache;
    }

    private void SetAction(PlayerAction newAction, bool forceNotify = false)
    {
        // If action hasn't changed, we usually early-out.
        // But attack triggers/transitions can kick the Animator back to idle,
        // so we must re-assert the correct state by checking the Animator's REAL current state.
        if (!forceNotify && _action == newAction)
        {
            if (newAction == PlayerAction.Walking && ShouldPresentWalkingLocomotion())
            {
                EnsureWalkAnimatorDuringLocomotion();
                return;
            }

            ReassertAnimatorForAction(newAction);
            return;
        }

        _action = newAction;

        OnActionChanged?.Invoke(_action);
        SyncActionBarGatheringStripToAction(_action);
        UpdateAnimatorFromAction();
    }

    private void ReassertAnimatorForAction(PlayerAction action)
    {     
        if (!animator) return;
        if (_attackLocked) return;
        if (Time.time < _nextReassertTime) return;

        // If the Animator Controller exits "gather" back to idle via exit time (common setup),
        // we are no longer in the gather state most of the time — but we must NOT call PlayState
        // here on a timer, or we re-enter gather every reassertCooldown and the swing looks
        // dozens of times faster than gatherAnimDelaySeconds. Only TickGather may drive gather replays.
        if (state == State.Gather &&
            (action == PlayerAction.Mining || action == PlayerAction.Woodcutting ||
             (action == PlayerAction.Fishing && !HasFishingRodInToolbelt())))
            return;

        string expected =
            (action == PlayerAction.Walking) ? walkStateName :
            (action == PlayerAction.Mining || action == PlayerAction.Woodcutting) ? gatherStateName :
            (action == PlayerAction.Fishing && state == State.Gather && !HasFishingRodInToolbelt()) ? gatherStateName :
            (action == PlayerAction.Fishing) ? idleStateName :
            idleStateName;

        // If animator got kicked back to something else, reassert without restarting.
        var st = animator.GetCurrentAnimatorStateInfo(0);
        bool isAlreadyExpected = st.IsName(expected);

        if (isAlreadyExpected) return;

        _nextReassertTime = Time.time + reassertCooldown;
        PlayState(expected, restart: false);
    }

    private bool HasHorizontalLocomotionThisFrame()
    {
        return Mathf.Abs(transform.position.x - _locomotionSampleStartX) > LocomotionMotionEpsilon;
    }

    private bool ShouldPresentWalkingLocomotion()
    {
        return _keyboardManualMoveThisFrame || HasHorizontalLocomotionThisFrame();
    }

    private bool IsLocomotionPresentationActive()
    {
        return ShouldPresentWalkingLocomotion();
    }

    private bool _walkAnimatorConfirmedThisSession;
    private int _lastWalkAnimatorVerifyFrame = -1;

    /// <summary>
    /// Lightweight walk recovery during steady locomotion — hurt/combat can kick the Animator to idle without changing <see cref="_action"/>.
    /// </summary>
    private void EnsureWalkAnimatorDuringLocomotion()
    {
        if (!animator || _attackLocked || !ShouldPresentWalkingLocomotion())
            return;

        int frame = Time.frameCount;
        if (_walkAnimatorConfirmedThisSession && frame - _lastWalkAnimatorVerifyFrame < 4)
            return;

        _lastWalkAnimatorVerifyFrame = frame;

        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName(walkStateName))
        {
            _walkAnimatorConfirmedThisSession = true;
            return;
        }

        _walkAnimatorConfirmedThisSession = false;

        if (Time.time < _nextReassertTime)
            return;

        _nextReassertTime = Time.time + reassertCooldown;
        PlayState(walkStateName, restart: false);
    }

    private void UpdateAnimatorFromAction()
    {
        if (!animator) return;

        // During an attack, do not override anything
        if (_attackLocked) return;

        // Moving — walk only while horizontal speed is actually non-zero (or keyboard steer is held).
        if (ShouldPresentWalkingLocomotion())
        {
            PlayState(walkStateName, restart: false);
            return;
        }

        // Gathering — swing cadence is owned by TickGather (_nextGatherAnimTime + gatherAnimDelaySeconds).
        // Do not restart the gather clip on every SetAction(..., true) while already in the gather state,
        // or the animation appears to spasm / run at the wrong rate after combat-related refreshes.
        if (_action == PlayerAction.Fishing)
        {
            if (state == State.Gather)
            {
                if (!HasFishingRodInToolbelt())
                {
                    var gst = animator.GetCurrentAnimatorStateInfo(0);
                    if (gst.IsName(gatherStateName))
                        return;
                    PlayState(gatherStateName, restart: true);
                    return;
                }

                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.IsName(idleStateName))
                    return;
            }

            PlayState(idleStateName, restart: false);
            return;
        }

        if (_action == PlayerAction.Mining || _action == PlayerAction.Woodcutting)
        {
            if (state == State.Gather)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.IsName(gatherStateName))
                    return;
            }

            PlayState(gatherStateName, restart: true);
            return;
        }

        // Fighting but between attack swings = idle visual is okay
        if (_action == PlayerAction.Fighting)
        {
            PlayState(idleStateName, restart: false);
            return;
        }

        // Default
        PlayState(idleStateName, restart: false);
    }

    private Coroutine _clearFightOverrideRoutine;

    private void ClearFightingOverrideSoon(float seconds)
    {
        if (_clearFightOverrideRoutine != null) StopCoroutine(_clearFightOverrideRoutine);
        _clearFightOverrideRoutine = StartCoroutine(ClearFightingOverrideRoutine(seconds));
    }

    private IEnumerator ClearFightingOverrideRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        // Only clear if we're still overriding Fighting
        if (_hasActionOverride && _actionOverride == PlayerAction.Fighting)
            ClearActionOverride();

        _clearFightOverrideRoutine = null;

        // Force a refresh so we immediately go back to walk / gather / idle as appropriate
        PlayerAction resume = PlayerAction.Idle;
        if (state == State.Gather)
            resume = GetGatherAction();
        else if (HasPendingLocomotionTarget())
            resume = PlayerAction.Walking;
        SetAction(resume, true);
    }

    private bool HasPendingLocomotionTarget()
    {
        switch (state)
        {
            case State.MoveToPoint:
                return Mathf.Abs(transform.position.x - moveTargetX) > LocomotionMotionEpsilon;
            case State.MoveToTarget:
                return targetNode != null &&
                       Mathf.Abs(transform.position.x - targetNode.workSpot.position.x) > LocomotionMotionEpsilon;
            case State.MoveToPickup:
                return _pickupTarget != null &&
                       Mathf.Abs(transform.position.x - _pickupTarget.transform.position.x) > LocomotionMotionEpsilon;
            default:
                return false;
        }
    }


    private float GetAttackClipLength()
    {
        if (!animator || animator.runtimeAnimatorController == null)
            return 0.5f;

        string triggerToUse = GetAttackTriggerName();
        string triggerLower = triggerToUse.ToLowerInvariant();

        var clips = animator.runtimeAnimatorController.animationClips;

        foreach (var c in clips)
        {
            if (!c) continue;

            string clipLower = c.name.ToLowerInvariant();

            if (c.name.Equals(triggerToUse, StringComparison.OrdinalIgnoreCase) ||
                clipLower.Contains(triggerLower))
            {
                return c.length;
            }
        }

        return 0.6f;
    }

    private void MoveToX(float x, float speed)
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, x, speed * Time.deltaTime);

        GetClampXMinMax(out float min, out float max);
        pos.x = Mathf.Clamp(pos.x, min, max);

        transform.position = pos;
        SyncPlayerRigidbody2DPosition();
    }

    /// <summary>
    /// Kinematic Rigidbody2D stays in lockstep with scripted transform writes (Update) vs floor alignment(LateUpdate).
    /// </summary>
    private void SyncPlayerRigidbody2DPosition()
    {
        if (_rb)
            _rb.position = transform.position;
    }

    /// <summary>
    /// Horizontal limits for player position: <see cref="WorldBounds"/> with edge padding when present, else <see cref="LaneBounds"/>.
    /// </summary>
    private void GetClampXMinMax(out float minX, out float maxX, float laneReferenceWorldX = float.NaN)
    {
        float refX = float.IsNaN(laneReferenceWorldX) ? transform.position.x : laneReferenceWorldX;
        if (PlayAreaBounds.TryGetClampXForWorldX(refX, WorldBoundsXPadding, out minX, out maxX))
            return;

        if (WorldBounds.Instance != null)
        {
            minX = WorldBounds.Instance.Left + WorldBoundsXPadding;
            maxX = WorldBounds.Instance.Right - WorldBoundsXPadding;
            return;
        }

        minX = laneBounds ? laneBounds.MinX : -999f;
        maxX = laneBounds ? laneBounds.MaxX : 999f;
    }

    private float GetMoveSpeed()
    {
        // fallback so you don't brick movement if stats is missing
        if (!characterStats) return 3f;

        float speed = characterStats.FinalMoveSpeed;
        if (ailments != null)
        {
            float ailmentMult = ailments.GetMoveSpeedMultiplier();
            if (characterStats != null && characterStats.IsWayOfTheSlayerCrowdControlImmune())
                speed = Mathf.Max(speed, speed * ailmentMult);
            else if (combat != null && combat.IsWayOfTheBerserkerSlowImmune())
                speed = Mathf.Max(speed, speed * ailmentMult);
            else
                speed *= ailmentMult;
        }

        return PlayerSprintInput.ApplySprintBonus(speed);
    }

    private void UpdateSpriteFlip()
    {
        if (_suppressSpriteFlipForTeleport)
            return;

        float currentX = transform.position.x;

        if (_attackLocked && TryFaceCombatTargetDuringAttack())
        {
            _lastX = currentX;
            return;
        }

        if (state == State.MoveToPoint && _moveToPointFromPlayerInput)
        {
            bool faceLeft = moveTargetX < currentX;
            bool flip = faceLeft;
            if (invertFlip) flip = !flip;
            ApplyVisualFlip(flip);
            _lastX = currentX;
            return;
        }

        if (combat != null)
        {
            EnemyBaseController target = combat.CurrentTarget;

            if (target != null && !target.IsDead &&
                !IsPlayerSteeringMovement &&
                !IsMovingAwayFromCombatTarget() &&
                !(state == State.MoveToPoint && _moveToPointFromPlayerInput))
            {
                float targetX = target.transform.position.x;
                if (Mathf.Abs(targetX - currentX) <= 0.04f)
                {
                    _lastX = currentX;
                    return;
                }

                bool faceLeft = targetX < currentX;
                bool flip = faceLeft;
                if (invertFlip) flip = !flip;

                ApplyVisualFlip(flip);
                _lastX = currentX;
                return;
            }
        }

        if (!visualsRoot) return;
 

        if (state == State.Gather && targetNode != null && targetNode.workSpot != null)
        {
            float targetX = targetNode.GatherFacingWorldX;
            bool faceLeft = targetX < currentX;

            bool flip = faceLeft;
            if (invertFlip) flip = !flip;

            ApplyVisualFlip(flip);
            _lastX = currentX;
            return;
        }

        float dx = currentX - _lastX;

        if (Mathf.Abs(dx) > flipDeadzone)
        {
            bool movingLeft = dx < 0f;
            bool flip = movingLeft;
            if (invertFlip) flip = !flip;

            ApplyVisualFlip(flip);
        }

        _lastX = currentX;
    }

    private void ApplyVisualFlip(bool flip)
    {
        Vector3 s = visualsRoot.localScale;
        float abs = Mathf.Abs(s.x);
        s.x = flip ? -abs : abs;
        visualsRoot.localScale = s;

        bool facingLeft = invertFlip ? !flip : flip;
        FacingDirectionX = facingLeft ? -1f : 1f;
    }

    public void FaceTargetX(float targetX)
    {
        if (!visualsRoot) return;

        float myX = transform.position.x;
        if (Mathf.Abs(targetX - myX) <= 0.04f)
            return;

        bool faceLeft = targetX < myX;

        bool flip = faceLeft;
        if (invertFlip) flip = !flip;

        ApplyVisualFlip(flip);
    }

    /// <summary>Faces the current combat target while an attack clip is playing (overrides movement-facing).</summary>
    private bool TryFaceCombatTargetDuringAttack()
    {
        if (combat == null)
            return false;

        EnemyBaseController target = combat.CurrentTarget;
        if (target == null || target.IsDead)
            return false;

        FaceTargetX(target.transform.position.x);
        return true;
    }

    /// <summary>
    /// After teleport/dash snaps, prevents <see cref="UpdateSpriteFlip"/> from treating the jump as movement and flipping the sprite away from the target.
    /// </summary>
    public void SyncSpriteFlipTrackingToPosition()
    {
        _lastX = transform.position.x;
    }

    private void FaceGatheringPoint()
    {
        if (!targetNode) return;
        FaceTargetX(targetNode.GatherFacingWorldX);
    }

    private void ApplyGatherHandVisuals()
    {
        if (!equipment) return;

        if (_hideHandsForUnarmedGather)
        {
            equipment.ClearMainHandUnarmedOverride();
            equipment.ClearMainHandVisualOverride();
            equipment.SetHideBothHandsOverride(true);
            return;
        }

        equipment.ClearHideBothHandsOverride();

        if (!string.IsNullOrWhiteSpace(_gatherToolItemId))
        {
            equipment.ClearMainHandUnarmedOverride();
            equipment.SetMainHandVisualOverride(_gatherToolItemId);
            return;
        }

        equipment.ClearMainHandUnarmedOverride();
        equipment.ClearMainHandVisualOverride();
    }

    private void ClearGatherHandVisuals()
    {
        _hideHandsForUnarmedGather = false;

        if (!equipment) return;

        equipment.ClearHideBothHandsOverride();
        equipment.ClearMainHandUnarmedOverride();
        equipment.ClearMainHandVisualOverride();
    }
    // -------------------------
    // HUD / Vitals
    // -------------------------

    public void TakeDamage(float amount, DamageType type, Transform attacker = null, bool wasCrit = false)
    {
        if (_isDead || !characterStats) return;

        if (_teleportDamageImmune)
            return;

        if (combat != null && attacker != null)
            combat.TryRetaliateFromAttacker(attacker);

        float preMitigatedDamage = Mathf.Max(0f, amount);
        bool blocked;
        float finalDamage = characterStats.TakeDamage(
            amount,
            type,
            out blocked,
            out float hpDamage,
            out DpsMitigationBreakdown mitigationReport);
        if (combat != null)
        {
            if (mitigationReport.Total > 0f)
                combat.RecordIncomingMitigationForDps(mitigationReport);
            if (finalDamage > 0f)
                combat.RecordIncomingDamageForDps(finalDamage, ToDpsBucket(type), attacker);
        }

        AwardEnduranceXpFromIncomingDamage(preMitigatedDamage, attacker);

        if (blocked)
            wasCrit = false;

        if (DamagePopupSystem.Instance != null && ToggleSettingsStore.Get(ToggleSettingId.ShowIncomingDamageNumbers))
        {
            var anchor = GetComponentInChildren<DamagePopupAnchor>(true);
            Vector3 anchorPos = anchor ? anchor.WorldPos : transform.position;

            GetIncomingDamagePopupPlacement(anchorPos, attacker, 0.35f, out Vector3 pos, out Vector3 dir);
            if (blocked && DamagePopupSystem.Instance != null)
            {
                Vector3 dealerPos = attacker != null ? attacker.position : transform.position;
                pos = DamagePopupSystem.Instance.ResolveLingeringStatusWorldPos(
                    transform,
                    anchorPos,
                    dealerPos,
                    attacker != null,
                    FacingDirectionX);
                dir = Vector3.up;
            }

            FloatingDamageTextUI.PopupDamageKind popupKind = type switch
            {
                DamageType.Physical => FloatingDamageTextUI.PopupDamageKind.Physical,
                DamageType.Magic => FloatingDamageTextUI.PopupDamageKind.Magic,
                DamageType.Corruption => FloatingDamageTextUI.PopupDamageKind.Corruption,
                DamageType.Typless => FloatingDamageTextUI.PopupDamageKind.Typless,
                _ => FloatingDamageTextUI.PopupDamageKind.Physical
            };

            DamagePopupSystem.Instance.Spawn(
                pos,
                Mathf.RoundToInt(finalDamage),
                popupKind,
                wasCrit,
                false,
                dir,
                blocked,
                transform
            );
        }

        if (!blocked && !_isDead && hpDamage > 0.001f)
        {
            TriggerHurtAnim();
        }
    }

    private static DpsDamageBucket ToDpsBucket(DamageType type)
    {
        return type switch
        {
            DamageType.Magic => DpsDamageBucket.Magic,
            DamageType.Corruption => DpsDamageBucket.Corruption,
            DamageType.Typless => DpsDamageBucket.Physical,
            _ => DpsDamageBucket.Physical
        };
    }

    /// <summary>
    /// Spawn point and drift for incoming damage popups on the player. Matches <see cref="TakeDamage"/>:
    /// anchor from this object’s hierarchy, horizontal offset away from the attacker in world X, drift = normalized (player − attacker).
    /// </summary>
    public void GetIncomingDamagePopupPlacement(
        Vector3 anchorWorldPos,
        Transform attacker,
        float sideOffset,
        out Vector3 spawnWorldPos,
        out Vector3 driftWorldDir)
    {
        spawnWorldPos = anchorWorldPos;
        driftWorldDir = Vector3.up;

        if (!attacker)
            return;

        GetIncomingDamagePopupPlacementFromDealerWorld(anchorWorldPos, attacker.position, sideOffset, out spawnWorldPos, out driftWorldDir);
    }

    /// <summary>
    /// Same horizontal offset and drift as <see cref="GetIncomingDamagePopupPlacement"/> but from a fixed world position
    /// (used for DoT when the dealer <see cref="Transform"/> was destroyed, e.g. enemy died while poison still ticks).
    /// </summary>
    public void GetIncomingDamagePopupPlacementFromDealerWorld(
        Vector3 anchorWorldPos,
        Vector3 dealerWorldPosition,
        float sideOffset,
        out Vector3 spawnWorldPos,
        out Vector3 driftWorldDir)
    {
        float towardAttackerX = Mathf.Sign(dealerWorldPosition.x - transform.position.x);
        if (towardAttackerX == 0f)
            towardAttackerX = 1f;

        spawnWorldPos = anchorWorldPos + new Vector3(-towardAttackerX * sideOffset, 0f, 0f);

        Vector3 away = transform.position - dealerWorldPosition;
        driftWorldDir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.up;
    }

    public void Heal(float amount, string sourceLabel = null)
    {
        if (_isDead || !characterStats) return;
        characterStats.Heal(amount, sourceLabel);
    }

    public bool SpendEnergy(float amount)
    {
        if (_isDead || !characterStats) return false;
        return characterStats.SpendEnergy(amount);
    }

    public void AddEnergy(float amount)
    {
        if (_isDead || !characterStats) return;
        characterStats.AddEnergy(amount);
    }

    public bool SpendMana(float amount)
    {
        if (_isDead || !characterStats) return false;
        return characterStats.SpendMana(amount);
    }

    public void AddMana(float amount)
    {
        if (_isDead || !characterStats) return;
        characterStats.AddMana(amount);
    }

    public void SetDisplayName(string newName)
    {
        if (!characterStats) return;
        characterStats.SetDisplayName(newName);
    }

    public void ApplyLifeSteal(float postMitigationDamage)
    {
        if (_isDead || !characterStats) return;

        float ls = Mathf.Clamp01(characterStats.LifeSteal);
        if (ls <= 0f || postMitigationDamage <= 0f) return;

        float healAmount = postMitigationDamage * ls;
        if (healAmount <= 0f) return;

        characterStats.Heal(healAmount, PlayerCombatController.LeechHealingSourceLabel);
    }

    private void RecalculateMaxVitalsFromGear()
    {
        if (!characterStats) return;
        characterStats.RefreshVitalsFromStats(fillIfEmpty: false);
        characterStats.ClampGuardToNaturalCap();
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        combat?.PauseDpsTracker();
        ailments?.ClearAllAilments();
        abilityController?.EndSoulforgedOnOwnerDeath();

        clickToMoveEnabled = false;

        combat?.ClearTarget();
        if (combat != null)
            combat.enabled = false;
        ClearActionOverride();
        CancelAction();

        TriggerDieAnim();

        _pendingDeathRespawnNode = ResolveDeathRespawnNode();
        _deathRespawnHere = IsRespawnHereDeathTarget(_pendingDeathRespawnNode);

        string diedOnNodeId = NpcPostDeathRespawnDialogueStore.ResolveCurrentGameplayMapNodeId();
        if (string.IsNullOrEmpty(diedOnNodeId) && SaveManager.Instance != null)
            diedOnNodeId = SaveManager.Instance.GetLastWrittenActiveMapNodeId();
        NpcPostDeathRespawnDialogueStore.MarkPendingAndSave(diedOnNodeId);

        if (_deathRoutine != null) StopCoroutine(_deathRoutine);
        if (_deathPoseRoutine != null) StopCoroutine(_deathPoseRoutine);
        _deathPoseRoutine = StartCoroutine(HoldDeathPoseAtFinalFrame());
        _deathRoutine = StartCoroutine(ShowDeathPopupAfterDelay());
    }

    private IEnumerator ShowDeathPopupAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, deathPopupDelaySeconds));
        ShowDeathPopup();
    }

    private IEnumerator HoldDeathPoseAtFinalFrame()
    {
        float wait = GetDieClipLength();
        yield return new WaitForSeconds(Mathf.Max(0.05f, wait));

        if (!_isDead || animator == null)
            yield break;

        // Lock visuals on the end of the death animation so the character cannot blend back to idle.
        animator.Play(dieStateName, 0, 0.999f);
        animator.Update(0f);
        animator.speed = 0f;
    }
    /// <summary>
    /// Respawn target on death: town node of the current region.
    /// Fallbacks: any town on the map, then starting node.
    /// </summary>
    private static MapNodeDefinition ResolveDeathRespawnNode()
    {
        MapNodeDefinition current = ResolveCurrentGameplayMapNode();
        if (current != null && current.respawnHereIfDied)
            return current;

        return ResolveRegionTownRespawnNode();
    }

    private static MapNodeDefinition ResolveCurrentGameplayMapNode()
    {
        MapNodeDefinition current = ActiveLevelContext.Current;
        if (current == null && GameplayLevelBootstrapper.Instance != null)
            current = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        return current;
    }

    private static bool IsRespawnHereDeathTarget(MapNodeDefinition respawnTarget)
    {
        MapNodeDefinition current = ResolveCurrentGameplayMapNode();
        if (current == null || respawnTarget == null || !current.respawnHereIfDied)
            return false;

        return string.Equals(current.nodeId, respawnTarget.nodeId, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Region-town fallback target on death.
    /// Fallbacks: any town on the map, then starting node.
    /// </summary>
    private static MapNodeDefinition ResolveRegionTownRespawnNode()
    {
        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        WorldMapDefinition map = progress ? progress.WorldMap : null;
        if (!map)
            map = Resources.Load<WorldMapDefinition>("Databases/WorldMap_Main");
        if (!map)
            return null;

        MapNodeDefinition current = ActiveLevelContext.Current;
        if (current != null)
        {
            RegionDefinition currentRegion = map.FindRegionContainingNode(current.nodeId);
            MapNodeDefinition townInRegion = FindTownNodeInRegion(currentRegion);
            if (townInRegion != null)
                return townInRegion;
        }

        // Cross-region fallback if the current node has no town configured.
        for (int i = 0; i < map.regions.Count; i++)
        {
            MapNodeDefinition town = FindTownNodeInRegion(map.regions[i]);
            if (town != null)
                return town;
        }

        return map.FindNodeById(map.startingNodeId);
    }

    private const float ReturnToTownInputCooldownSeconds = 5f;
    private const float ReturnToTownAfterMapEntryGraceSeconds = 3f;
    private static float _nextReturnToTownAllowedUnscaledTime;
    private static float _returnToTownAllowedAfterMapEntryUnscaledTime;
    private static bool _returnToTownTravelInProgress;
    private static bool _gameplayMapSpawnSettled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetReturnToTownTravelGate()
    {
        _nextReturnToTownAllowedUnscaledTime = 0f;
        _returnToTownAllowedAfterMapEntryUnscaledTime = 0f;
        _returnToTownTravelInProgress = false;
        _gameplayMapSpawnSettled = false;
    }

    /// <summary>Gameplay spawn / fade is in progress — block map exit position staging and return-to-town.</summary>
    public static void NotifyGameplayMapSpawnStarted() => _gameplayMapSpawnSettled = false;

    /// <summary>Called when <see cref="PlayerSpawnController"/> finishes placing the player on a gameplay map.</summary>
    public static void NotifyGameplayMapSpawnFinished()
    {
        _gameplayMapSpawnSettled = true;
        _returnToTownAllowedAfterMapEntryUnscaledTime =
            Time.unscaledTime + ReturnToTownAfterMapEntryGraceSeconds;
    }

    public static bool IsGameplayMapSpawnSettledForTravel() =>
        _gameplayMapSpawnSettled && IsGameplayTravelEnvironmentReady();

    /// <summary>Called when a return-to-town teleport finishes or is aborted before scene load.</summary>
    public static void NotifyReturnToTownTravelFinished()
    {
        _returnToTownTravelInProgress = false;
    }

    public static bool IsReturnToTownTravelInProgress() => _returnToTownTravelInProgress;

    /// <summary>
    /// Hotkey / UI action: travel to the town node for the region the player is currently in.
    /// Returns false when blocked by cooldown, grace period, an in-progress teleport, or no valid destination.
    /// </summary>
    public static bool TryReturnToTownViaHotkey()
    {
        if (!CanReturnToTownNow(out MapNodeDefinition destination))
            return false;

        _returnToTownTravelInProgress = true;
        _nextReturnToTownAllowedUnscaledTime = Time.unscaledTime + ReturnToTownInputCooldownSeconds;

        string townName = !string.IsNullOrWhiteSpace(destination.displayName)
            ? destination.displayName.Trim()
            : destination.nodeId;
        if (!string.IsNullOrWhiteSpace(townName))
            GameLog.Add($"Returning to town: {townName}");
        else
            GameLog.Add("Returning to town");

        MapNodeDefinition restoreContext = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;

        MapTravelSession.BeginTravel(destination, MapTravelSession.EntryMethod.MapTeleport, logPendingLevel: false);
        if (!PlayerLevelTransition.LoadSceneWithEffectOrImmediate(GameplaySceneName))
        {
            if (restoreContext != null)
                ActiveLevelContext.SetPendingLevel(restoreContext, logToConsole: false);
            MapTravelSession.ClearPendingEntryMethod();
            NotifyReturnToTownTravelFinished();
            return false;
        }

        return true;
    }

    private static bool CanReturnToTownNow(out MapNodeDefinition destination)
    {
        destination = null;

        if (_returnToTownTravelInProgress)
            return false;

        if (Time.unscaledTime < _nextReturnToTownAllowedUnscaledTime)
            return false;

        if (!_gameplayMapSpawnSettled)
            return false;

        if (Time.unscaledTime < _returnToTownAllowedAfterMapEntryUnscaledTime)
            return false;

        if (!IsGameplayTravelEnvironmentReady())
            return false;

        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            if (player.IsDead)
                return false;

            PlayerLevelTransition transition = player.GetComponent<PlayerLevelTransition>();
            if (transition != null && (transition.IsTransitionRunning || transition.PendingScaleRestore))
                return false;
        }

        destination = ResolveRegionTownRespawnNode();
        if (destination == null)
            return false;

        string destinationId = string.IsNullOrWhiteSpace(destination.nodeId) ? "" : destination.nodeId.Trim();
        MapNodeDefinition current = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;
        if (current != null &&
            !string.IsNullOrEmpty(destinationId) &&
            string.Equals(current.nodeId, destinationId, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool IsGameplayTravelEnvironmentReady()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.name.Equals(GameplaySceneName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (SaveManager.Instance != null && !SaveManager.Instance.IsGameFullyLoaded)
            return false;

        if (GameplayLevelBootstrapper.Instance == null ||
            GameplayLevelBootstrapper.Instance.ActiveDefinition == null)
            return false;

        return true;
    }

    private static MapNodeDefinition FindTownNodeInRegion(RegionDefinition region)
    {
        if (region == null || region.nodes == null)
            return null;

        for (int i = 0; i < region.nodes.Count; i++)
        {
            MapNodeDefinition node = region.nodes[i];
            if (node != null && node.nodeType == MapNodeType.Town)
                return node;
        }

        return null;
    }

    private void ShowDeathPopup()
    {
        ResolveDeathPopupRefs();
        if (!deathPopupWindow)
            return;

        DeathRespawnPopupUI styledPopup = deathPopupWindow.GetComponent<DeathRespawnPopupUI>();
        if (styledPopup == null)
            styledPopup = deathPopupWindow.AddComponent<DeathRespawnPopupUI>();

        if (deathPopupNotificationText)
            deathPopupNotificationText.text = "You Have Died";
        if (deathPopupButtonLabel)
            deathPopupButtonLabel.text = _deathRespawnHere ? "Respawn Here" : "Respawn in Town";

        if (deathPopupButton)
        {
            deathPopupButton.onClick.RemoveAllListeners();
            deathPopupButton.onClick.AddListener(OnDeathPopupRespawnClicked);
            deathPopupButton.interactable = true;
        }

        deathPopupWindow.SetActive(true);
        styledPopup.PrepareForShow(_deathRespawnHere);
        ForceDeathPopupOnTop();
    }

    private void ForceDeathPopupOnTop()
    {
        if (!deathPopupWindow)
            return;

        Transform t = deathPopupWindow.transform;
        t.SetAsLastSibling();

        Canvas popupCanvas = deathPopupWindow.GetComponent<Canvas>();
        if (popupCanvas == null)
            popupCanvas = deathPopupWindow.AddComponent<Canvas>();

        int topLayerId = GetHighestSortingLayerId();
        const int topOrder = short.MaxValue;

        popupCanvas.overrideSorting = true;
        popupCanvas.sortingLayerID = topLayerId;
        popupCanvas.sortingOrder = topOrder;

        if (deathPopupWindow.GetComponent<GraphicRaycaster>() == null)
            deathPopupWindow.AddComponent<GraphicRaycaster>();

        Canvas[] nestedCanvases = deathPopupWindow.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < nestedCanvases.Length; i++)
        {
            Canvas c = nestedCanvases[i];
            if (c == null)
                continue;
            c.overrideSorting = true;
            c.sortingLayerID = topLayerId;
            c.sortingOrder = topOrder;
        }
    }

    private static int GetHighestSortingLayerId()
    {
        SortingLayer[] layers = SortingLayer.layers;
        if (layers == null || layers.Length == 0)
            return 0;

        int bestId = layers[0].id;
        int bestValue = layers[0].value;
        for (int i = 1; i < layers.Length; i++)
        {
            if (layers[i].value <= bestValue)
                continue;
            bestValue = layers[i].value;
            bestId = layers[i].id;
        }

        return bestId;
    }

    private void OnDeathPopupRespawnClicked()
    {
        if (_deathRespawnRoutine != null)
            return;

        if (deathPopupButton)
            deathPopupButton.interactable = false;
        _deathRespawnRoutine = StartCoroutine(CoFadeAndRespawnToTown());
    }

    private IEnumerator CoFadeAndRespawnToTown()
    {
        try
        {
            if (deathPopupWindow)
                deathPopupWindow.SetActive(false);

            CanvasGroup fader = PlayerSpawnController.CreateOrResolveGameplayBlackFade();
            if (fader != null)
            {
                PlayerSpawnController.BringGameplayBlackFadeToFront(fader);
                fader.alpha = 0f;
                float t = 0f;
                float dur = Mathf.Max(0.01f, deathRespawnFadeSeconds);
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    if (!fader)
                        fader = PlayerSpawnController.CreateOrResolveGameplayBlackFade();
                    if (fader)
                    {
                        PlayerSpawnController.BringGameplayBlackFadeToFront(fader);
                        fader.alpha = Mathf.Lerp(0f, 1f, k);
                    }

                    yield return null;
                }

                if (!fader)
                    fader = PlayerSpawnController.CreateOrResolveGameplayBlackFade();
                if (fader)
                    fader.alpha = 1f;
            }

            if (characterStats != null)
                characterStats.ReviveFull();

            ResetDeathStateForRespawnLoad();

            MapNodeDefinition respawnNode = _pendingDeathRespawnNode != null
                ? _pendingDeathRespawnNode
                : ResolveDeathRespawnNode();
            if (respawnNode != null)
                ActiveLevelContext.SetPendingLevel(respawnNode, logToConsole: false);
            else
                Debug.LogWarning("[Player] Respawn town node could not be resolved; loading current GamePlay context.", this);

            MainMenuWindowUI.CaptureOpenStateForSceneChange();
            GameplayRespawnHelperPersistence.MarkKeepHelperOverlayAcrossNextGameplayLoad();
            MapTravelSession.ClearPendingEntryMethod();
            SaveSlotManager.SetPendingGameplaySpawnDisposition(SaveSlotManager.GameplaySpawnDisposition.DefaultSpawnPoint);
            SaveSlotManager.MarkSkipApplySavedWorldPositionFromSaveOnce();
            SaveManager.Instance?.SaveBeforeSceneTransition();
            SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
        }
        finally
        {
            _deathRespawnRoutine = null;
        }
    }

    private void ResetDeathStateForRespawnLoad()
    {
        _isDead = false;
        clickToMoveEnabled = true;
        movementLocked = false;

        if (animator != null)
            animator.speed = 1f;

        if (combat != null)
        {
            combat.ResetDpsTrackerForRespawn();
            combat.enabled = true;
        }

        _pendingDeathRespawnNode = null;
    }

    private static bool IsDeathRespawnPopupWindowName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName == "PopupWindow" ||
               objectName.StartsWith("RespawnPopupWindow", System.StringComparison.Ordinal);
    }

    private void ResolveDeathPopupRefs()
    {
        if (deathPopupWindow != null && !IsDeathRespawnPopupWindowName(deathPopupWindow.name))
            deathPopupWindow = null;

        if (!deathPopupWindow)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.gameObject == null)
                    continue;

                if (IsDeathRespawnPopupWindowName(t.gameObject.name))
                {
                    deathPopupWindow = t.gameObject;
                    break;
                }
            }
        }

        if (!deathPopupWindow)
            return;

        if (!deathPopupNotificationText)
            deathPopupNotificationText = FindChildTmpByName(deathPopupWindow.transform, "NotificationText")
                                        ?? FindChildTmpByName(deathPopupWindow.transform, "Notification");

        if (!deathPopupButton)
            deathPopupButton = FindChildComponentByName<Button>(deathPopupWindow.transform, "Button");

        if (!deathPopupButtonLabel)
        {
            if (deathPopupButton)
                deathPopupButtonLabel = deathPopupButton.GetComponentInChildren<TMP_Text>(true);
            if (!deathPopupButtonLabel)
                deathPopupButtonLabel = FindChildTmpByName(deathPopupWindow.transform, "ButtonText");
        }
    }

    private static TMP_Text FindChildTmpByName(Transform root, string name)
    {
        return FindChildComponentByName<TMP_Text>(root, name);
    }

    private static T FindChildComponentByName<T>(Transform root, string name) where T : Component
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.gameObject == null || t.gameObject.name != name)
                continue;
            T comp = t.GetComponent<T>();
            if (comp != null)
                return comp;
        }

        return null;
    }

    private float ApplyMitigation(float rawDamage, DamageType type, out bool blocked)
    {
        blocked = false;

        rawDamage = Mathf.Max(0f, rawDamage);
        if (rawDamage <= 0f) return 0f;

        switch (type)
        {
            case DamageType.Corruption:
                {
                    float cr = characterStats ? characterStats.CorruptionResist : 0f;
                    return MitigateByRating(rawDamage, cr);
                }

            case DamageType.Physical:
                {
                    float armor = characterStats ? characterStats.Armor : 0f;
                    float dmg = MitigateByRating(rawDamage, armor);

                    float blockChance = characterStats ? characterStats.PhysBlockChance : 0f;
                    if (blockChance > 0f && UnityEngine.Random.value < Mathf.Clamp01(blockChance))
                    {
                        blocked = true;
                        float mitigation = characterStats ? characterStats.PhysBlockMitigationFraction : AbilityCombatPower.BasePhysBlockMitigation;
                        dmg *= 1f - mitigation;
                    }

                    return dmg;
                }

            case DamageType.Magic:
                {
                    float mr = characterStats ? characterStats.MagicResist : 0f;
                    return MitigateByRating(rawDamage, mr);
                }

            default:
                return rawDamage;
        }
    }

    private static float MitigateByRating(float damage, float rating)
    {
        rating = CombatResistRules.ClampRating(rating);

        // 100/(100+rating) diminishing returns
        float multiplier = 100f / (100f + rating);
        return damage * multiplier;
    }

    private void AwardEnduranceXpFromIncomingDamage(float preMitigatedDamage, Transform attacker = null)
    {
        if (preMitigatedDamage <= 0f)
            return;

        if (!ShouldAwardEnduranceXpFromAttacker(attacker))
            return;

        SkillsManager sm = SkillsManager.Instance;
        if (!sm)
            return;

        float perXp = Mathf.Max(0.01f, enduranceDamagePerXp);
        float xp = preMitigatedDamage / perXp;
        if (xp <= 0f)
            return;

        sm.AddXpFloat(SkillType.Endurance, xp, "Defence");
    }

    private static bool ShouldAwardEnduranceXpFromAttacker(Transform attacker)
    {
        if (!attacker)
            return true;

        EnemyBaseController enemy = attacker.GetComponentInParent<EnemyBaseController>();
        if (!enemy || enemy.Definition == null)
            return true;

        return enemy.Definition.grantCombatXp;
    }

    public void SetActionOverride(PlayerAction action)
    {
        if (_isDead) return;

        _hasActionOverride = true;
        _actionOverride = action;
        SetAction(action, true);
    }

    public void ClearActionOverride()
    {
        _hasActionOverride = false;
    }



    private bool IsGameplayClickAllowedAtScreen(Vector3 screenPos)
    {
        if (!restrictClicksToStrip)
            return true;

        if (!_stripCam)
            RebindCameras();
        if (!_stripCam)
            return false;

        if (ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
            return true;

        return _stripCam.pixelRect.Contains(screenPos);
    }

    /// <summary>
    /// Expand background: pointer is in sky or side margins (outside strip camera pixel rect).
    /// Clicks here are move-only on X; strip interactions require clicking inside the strip viewport.
    /// </summary>
    private bool IsExpandBackgroundOutsideStripClick(Vector3 screenPos)
    {
        if (!ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
            return false;

        if (!_stripCam)
            RebindCameras();

        return _stripCam != null && !_stripCam.pixelRect.Contains(screenPos);
    }

    private Camera ResolveWorldClickCamera()
    {
        if (!_stripCam)
            RebindCameras();

        if (ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground) &&
            _fullWindowCam &&
            _stripCam &&
            !_stripCam.pixelRect.Contains(Input.mousePosition))
        {
            return _fullWindowCam;
        }

        if (_stripCam)
            return _stripCam;

        if (!_cam)
            _cam = Camera.main;

        return _cam;
    }

    // -------------------------
    // Scene Management
    // -------------------------

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindCameras();
        if (characterStats != null && scene.name == "GamePlay")
            characterStats.SnapGuardToNaturalCapOnSessionLoad();
    }

    private void RebindCameras()
    {
        _cam = Camera.main;

        var go = GameObject.Find(stripCameraName);
        _stripCam = go ? go.GetComponent<Camera>() : null;

        var fullGo = GameObject.Find(fullCameraName);
        _fullWindowCam = fullGo ? fullGo.GetComponent<Camera>() : null;

        if (_stripCam == null)
        {
            var cams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                if (!c) continue;
                if (_cam && c == _cam) continue;

                if (c.rect.width < 0.99f || c.rect.height < 0.99f || c.rect.y > 0.01f)
                {
                    _stripCam = c;
                    break;
                }
            }
        }
    }
}
public static class TransformPathExt
{
    public static string GetHierarchyPath(this Transform t)
    {
        if (!t) return "<null>";
        string path = t.name;
        while (t.parent)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}