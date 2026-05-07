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
    private Camera _stripCam;

    [Tooltip("Set to Pickup layer")]
    [SerializeField] private LayerMask pickupMask;

    [Tooltip("Set to Enemy layer")]
    [SerializeField] private LayerMask enemyMask;

    [SerializeField] private PlayerCombatController combat;

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
    private MapNodeDefinition _pendingDeathRespawnNode;

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

    private Rigidbody2D _rb;

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

    private const float ConsumableCooldownActivityLogIntervalSeconds = 5f;
    private const float RepeatedPopupActivityLogIntervalSeconds = 5f;
    private float _nextFoodCooldownActivityLogTime;
    private float _nextPotionCooldownActivityLogTime;
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

    [Header("Future Stamina Integration")]
    [Tooltip("Base stamina cost per successful gather tick. Hook to stamina system later.")]
    [SerializeField] private float baseGatherStaminaCostPerTick = 10f;

    [Header("Gather Energy Cost")]
    [SerializeField] private string lowEnergyPopupText = "* Fatigued *";
    [SerializeField, Min(0.1f)] private float fatiguedResumeDelaySeconds = 3f;

    [Header("Endurance XP (Defence)")]
    [Tooltip("Pre-mitigation damage per 1 Endurance XP. Example: 10 means 10 damage = 1 XP.")]
    [SerializeField, Min(0.01f)] private float enduranceDamagePerXp = 10f;

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

    public ResourceNode CurrentTarget => targetNode;
    public PlayerAction CurrentAction => _action;

    public event Action<PlayerAction> OnActionChanged;
    public event Action<bool, float> OnGatherDebuffChanged;

    [SerializeField] private float reassertCooldown = 0.08f;
    private float _nextReassertTime;


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

        if (_attackLocked && Time.time >= _attackUnlockTime)
        {
            _attackLocked = false;
        }

        if (clickToMoveEnabled)
            HandleClickToMove();

        TickStateMachine();
        ApplyActionPresentation();

        UpdateSpriteFlip();
        characterStats?.TickRegen(Time.deltaTime);
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
        bool isMoving =
            state == State.MoveToPoint ||
            state == State.MoveToTarget ||
            state == State.MoveToPickup;

        bool isGathering =
            state == State.Gather;

        bool hasLiveCombatTarget =
            combat != null &&
            combat.CurrentTarget != null &&
            !combat.CurrentTarget.IsDead;

        // Only show Fighting when we're actually in combat presentation,
        // not while still walking toward the target.
        bool shouldShowFighting =
            !isMoving &&
            !isGathering &&
            (_attackLocked || InCombat || hasLiveCombatTarget);

        if (shouldShowFighting)
        {
            SetAction(PlayerAction.Fighting);
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
                    SetAction(PlayerAction.Walking);
                    break;

                case State.Gather:
                    SetAction(GetGatherAction());
                    break;
            }
        }

        if (!_attackLocked && animator)
        {
            float walkDeadzone = clickArriveThreshold;
            if (combat != null &&
                combat.CurrentTarget != null &&
                !combat.CurrentTarget.IsDead)
            {
                walkDeadzone = Mathf.Max(0.001f, combatArriveThreshold);
            }

            bool shouldLookWalking =
                (state == State.MoveToPoint && Mathf.Abs(transform.position.x - moveTargetX) > walkDeadzone) ||
                (state == State.MoveToTarget) ||
                (state == State.MoveToPickup);

            if (shouldLookWalking)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (!st.IsName(walkStateName))
                    PlayState(walkStateName, restart: false);
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

        bool isMoving =
            state == State.MoveToPoint ||
            state == State.MoveToTarget ||
            state == State.MoveToPickup;

        // Don't interrupt locomotion with hurt while moving,
        // otherwise the character visually slides.
        if (isMoving)
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
        _pausedNode.Definition.PreviewDrops(_drops);
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
            ShowPopup(InCombat ? "Can't gather while in combat!" : "Can't gather while enemies are on the map!");
            return; // IMPORTANT: don't set targetNode/state
        }

        if (!node || !node.workSpot) return;

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

            int xpHint = -1;
            if (node.Definition != null && node.Definition.xpPerTick > 0)
                xpHint = node.Definition.xpPerTick;

            sm.SetActiveXpDisplay(sk, src, xpHint);
        }

        // =========================================================
        // Gather logic continues normally
        // =========================================================

        _gatherSpeedMultiplier = 1f;
        _gatherGritChance = 0f;
        _gatherBonusFindChance = 0f;
        _gatherStaminaEfficiency = 0f;

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

            SetAction(PlayerAction.Walking, false); // or just SetAction(PlayerAction.Walking);
            return;
        }

        if (TryFindToolInToolbelt(node.RequiredTool, out string toolItemId))
        {
            _gatherToolItemId = toolItemId;

            var toolDef = inventory ? inventory.GetItemDef(toolItemId) : null;
            _gatherSpeedMultiplier = toolDef ? toolDef.GatherSpeedMultiplier : 1f;
            _gatherGritChance = toolDef ? toolDef.GatheringGrit : 0f;
            _gatherBonusFindChance = toolDef ? toolDef.BonusResourceFindChance : 0f;
            _gatherStaminaEfficiency = toolDef ? toolDef.StaminaEfficiency : 0f;

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
            return TryPassConsumableCooldownLogGate(ref _nextFoodCooldownActivityLogTime);

        if (IsConsumableCooldownMessage(msg, "Potions") ||
            IsConsumableCooldownMessage(msg, "Potion"))
            return TryPassConsumableCooldownLogGate(ref _nextPotionCooldownActivityLogTime);

        return TryPassRepeatedPopupLogGate(msg);
    }

    private static bool IsConsumableCooldownMessage(string msg, string consumableLabel)
    {
        return !string.IsNullOrWhiteSpace(msg) &&
               msg.StartsWith($"{consumableLabel} on cooldown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryPassConsumableCooldownLogGate(ref float nextAllowedTime)
    {
        if (Time.unscaledTime < nextAllowedTime)
            return false;

        nextAllowedTime = Time.unscaledTime + ConsumableCooldownActivityLogIntervalSeconds;
        return true;
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

        if (restrictClicksToStrip)
        {
            if (!_stripCam) RebindCameras();
            if (!_stripCam) return;

            if (!_stripCam.pixelRect.Contains(Input.mousePosition))
                return;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        // World clicks should always exit shop mode.
        MerchantClick.ForceCloseMerchantMode();
        MerchantClick.CancelPendingOpen();

        if (!_cam)
            _cam = Camera.main;

        if (!_cam)
            return;

        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        Vector2 point = new Vector2(world.x, world.y);

        LayerMask combinedMask = pickupMask | interactableMask | enemyMask;
        Collider2D winner = WorldClickPicker2D.PickTopmostAtPoint(point, combinedMask);

        if (winner != null)
        {
            // Enemy click = always allowed (sets target)
            var enemyClick = winner.GetComponentInParent<EnemyClick>();
            if (enemyClick != null && combat != null)
            {
                combat.SetTarget(enemyClick.GetEnemy());
                return;
            }

            // If we clicked a ResourceNode while enemies exist / in combat:
            // show popup but treat click like ground (move there) and do NOT clear combat target.
            var node = winner.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                if (AnyEnemyOnMap || InCombat)
                {
                    ShowPopup("Can't gather while enemies are on the map!");
                    MoveToPointX(world.x);   // move toward it
                    return;                  // don't consume click further
                }

                // Not in combat: gather normally
                SelectNode(node);
                return;
            }

            // Optional: pickups etc. (leave as-is)
            // If you want pickups also to pass through during combat, handle similarly here.

            return; // other interactables consume click
        }

        InterruptWorkIfNeeded();

        // clicking empty space cancels target combat
        combat?.ClearTarget();

        MoveToPointX(world.x);
    }

    private void InterruptWorkIfNeeded()
    {
        if (state == State.Gather || state == State.MoveToTarget || state == State.MoveToPickup)
        {
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

    public void MoveToPointX(float x)
    {
        if (_isDead) return;

        targetNode = null;
        _pickupTarget = null;

        _accumItems = 0f;
        _gatherTimer = 0f;
        _nextGatherInterval = 0f;

        GetClampXMinMax(out float min, out float max);

        moveTargetX = Mathf.Clamp(x, min, max);
        state = State.MoveToPoint;

        equipment?.ClearMainHandVisualOverride();
        SetAction(PlayerAction.Walking, true);
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

        moveTargetX = clamped;
        state = State.MoveToPoint;
    }

    public void StopMoveOnly()
    {
        if (state == State.MoveToPoint)
            state = State.Idle;
    }

    private void TickMoveToTarget()
    {
        if (movementLocked)
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
                if (targetNode.UseRandomInterval && _fatigueGatherSavedInterval <= 0f)
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
            ReturnToIdle();
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
            _gatherSpeedMultiplier = toolDef ? toolDef.GatherSpeedMultiplier : 1f;
            _gatherGritChance = toolDef ? toolDef.GatheringGrit : 0f;
            _gatherBonusFindChance = toolDef ? toolDef.BonusResourceFindChance : 0f;
            _gatherStaminaEfficiency = toolDef ? toolDef.StaminaEfficiency : 0f;

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

        // Only “swing” the gather animation once every N seconds while gathering
        if (animator && Time.time >= _nextGatherAnimTime)
        {
            // Spend gather energy on the same cadence as gather swings for smoother feel.
            if (!TrySpendGatherEnergyOnSwing(targetNode.Definition))
            {
                PauseGatherForLowEnergy();
                return;
            }

            PlayState(gatherStateName, restart: true);

            _nextGatherAnimTime = Time.time + Mathf.Max(0.25f, gatherAnimDelaySeconds);
        }

        Vector3 pos = transform.position;
        GetClampXMinMax(out float minX, out float maxX);
        pos.x = Mathf.Clamp(targetNode.workSpot.position.x, minX, maxX);
        transform.position = pos;
        SyncPlayerRigidbody2DPosition();

        if (targetNode.UseRandomInterval)
        {
            if (_nextGatherInterval <= 0f)
                _nextGatherInterval = targetNode.GetNextInterval();

            _gatherTimer += Time.deltaTime * _gatherSpeedMultiplier;

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

            return;
        }

        float rate = targetNode.RatePerSecond;
        if (rate <= 0f) return;

        _accumItems += rate * Time.deltaTime * _gatherSpeedMultiplier;
        int gained = Mathf.FloorToInt(_accumItems);

        if (gained > 0)
        {
            for (int i = 0; i < gained; i++)
                DoOneGatherTick();

            _accumItems -= gained;
        }
    }

    private void DoOneGatherTick()
    {
        if (targetNode == null || targetNode.Definition == null) return;

        var def = targetNode.Definition;
        _ = GetEffectiveGatherStaminaCostPerTick(); // Reserved for stamina spend integration.

        // ---- 1) MAIN yield first (this is the ONLY thing that grants XP) ----
        if (def.HasMainYield)
        {
            int baseMainAmt = def.RollMainYieldAmount();
            int mainAmt = baseMainAmt;
            bool gritProc = false;
            // Gathering Grit: doubles BASE yield only. Never duplicates bonus drops.
            if (mainAmt > 0 && UnityEngine.Random.value <= Mathf.Clamp01(_gatherGritChance))
            {
                mainAmt *= 2;
                gritProc = true;
            }
            if (mainAmt > 0)
            {
                // Add main item
                int added = inventory.AddPartial(def.YieldItemId, mainAmt);
                int overflow = mainAmt - added;
                int gritBonusAdded = gritProc ? Mathf.Clamp(added - baseMainAmt, 0, baseMainAmt) : 0;

                if (overflow > 0)
                {
                    if (dropOverflowToGround)
                    {
                        var itemDef = inventory.GetItemDef(def.YieldItemId);
                        Sprite icon = itemDef ? itemDef.icon : null;

                        if (DropManager.Instance != null)
                            DropManager.Instance.Spawn(def.YieldItemId, overflow, icon);
                        else if (worldDropPrefab != null)
                        {
                            float scatterX = UnityEngine.Random.Range(-dropScatterRadius, dropScatterRadius);
                            Vector3 spawnPos = transform.position + new Vector3(scatterX, 0.1f, 0f);

                            var drop = Instantiate(worldDropPrefab, spawnPos, Quaternion.identity);
                            drop.Init(def.YieldItemId, overflow, icon);
                        }
                    }

                    ShowPopup("Inventory Full!");
                }

                // ✅ XP ONLY for main yield tick
                var sm = SkillsManager.Instance;
                if (sm != null && def.xpPerTick > 0)
                {
                    SkillType skill = def.actionType switch
                    {
                        NodeAction.Mining => SkillType.Mining,
                        NodeAction.Woodcutting => SkillType.Woodcutting,
                        NodeAction.Fishing => SkillType.Fishing,
                        _ => SkillType.Woodcutting
                    };

                    sm.AddXp(skill, def.xpPerTick, def.displayName);
                }

                if (gritBonusAdded > 0)
                {
                    ItemDefinition mainItemDef = inventory.GetItemDef(def.YieldItemId);
                    string itemName = (mainItemDef != null && !string.IsNullOrWhiteSpace(mainItemDef.displayName))
                        ? mainItemDef.displayName.Trim()
                        : def.YieldItemId;
                    GameLog.Add($"Grit triggered: +{gritBonusAdded} {itemName}", GameLog.ItemGainColor);
                }
            }
        }

        // ---- 2) BONUS drops (NO XP from these) ----
        _drops.Clear();
        // Bonus Resource Find Chance scales ONLY bonus roll chances:
        // effectiveChance = baseChance * (1 + bonusFindChance)
        // Base yield amount is intentionally unaffected.
        def.PreviewDrops(_drops, _gatherBonusFindChance);

        // PreviewDrops includes main too, so we must ignore index 0 main OR skip matching itemId
        // Easiest: process ONLY entries that are NOT the main yield itemId
        for (int i = 0; i < _drops.Count; i++)
        {
            var d = _drops[i];
            if (string.IsNullOrWhiteSpace(d.itemId) || d.amount <= 0) continue;

            // Skip main yield because we already handled it above
            if (d.itemId == def.YieldItemId) continue;

            int added = inventory.AddPartial(d.itemId, d.amount);
            int overflow = d.amount - added;

            if (overflow > 0)
            {
                if (dropOverflowToGround)
                {
                    var itemDef = inventory.GetItemDef(d.itemId);
                    Sprite icon = itemDef ? itemDef.icon : null;

                    if (DropManager.Instance != null)
                        DropManager.Instance.Spawn(d.itemId, overflow, icon);
                    else if (worldDropPrefab != null)
                    {
                        float scatterX = UnityEngine.Random.Range(-dropScatterRadius, dropScatterRadius);
                        Vector3 spawnPos = transform.position + new Vector3(scatterX, 0.1f, 0f);

                        var drop = Instantiate(worldDropPrefab, spawnPos, Quaternion.identity);
                        drop.Init(d.itemId, overflow, icon);
                    }
                }

                ShowPopup("Inventory Full!");
            }
        }
    }

    private void ReturnToIdle(bool keepFatigueGatherProgress = false)
    {
        if (!keepFatigueGatherProgress)
            ClearFatigueGatherProgress();

        _gatherSpeedMultiplier = 1f;
        _gatherGritChance = 0f;
        _gatherBonusFindChance = 0f;
        _gatherStaminaEfficiency = 0f;
        OnGatherDebuffChanged?.Invoke(false, 1f);

        targetNode = null;
        _pickupTarget = null;
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
        if (def == null || characterStats == null) return true;

        // Energy model:
        // Node defines energy cost directly per gather swing.
        // Tool stamina efficiency reduces that swing cost.
        float baseCostPerSwing = Mathf.Max(0f, def.energyCostPerSwing);
        float spendPerSwing = Mathf.Max(0f, baseCostPerSwing * (1f - Mathf.Clamp01(_gatherStaminaEfficiency)));
        if (spendPerSwing <= 0f) return true;

        return characterStats.SpendEnergy(spendPerSwing);
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

    private void SetAction(PlayerAction newAction, bool forceNotify = false)
    {
        // If action hasn't changed, we usually early-out.
        // But attack triggers/transitions can kick the Animator back to idle,
        // so we must re-assert the correct state by checking the Animator's REAL current state.
        if (!forceNotify && _action == newAction)
        {
            ReassertAnimatorForAction(newAction);
            return;
        }

        _action = newAction;

        OnActionChanged?.Invoke(_action);
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
            (action == PlayerAction.Mining || action == PlayerAction.Woodcutting || action == PlayerAction.Fishing))
            return;

        string expected =
            (action == PlayerAction.Walking) ? walkStateName :
            (action == PlayerAction.Mining || action == PlayerAction.Woodcutting || action == PlayerAction.Fishing) ? gatherStateName :
            idleStateName;

        // If animator got kicked back to something else, reassert without restarting.
        var st = animator.GetCurrentAnimatorStateInfo(0);
        bool isAlreadyExpected = st.IsName(expected);

        if (isAlreadyExpected) return;

        _nextReassertTime = Time.time + reassertCooldown;
        PlayState(expected, restart: false);
    }

    private void UpdateAnimatorFromAction()
    {
        if (!animator) return;

        // During an attack, do not override anything
        if (_attackLocked) return;

        // Moving
        if (state == State.MoveToPoint || state == State.MoveToTarget || state == State.MoveToPickup)
        {
            PlayState(walkStateName, restart: false);
            return;
        }

        // Gathering — swing cadence is owned by TickGather (_nextGatherAnimTime + gatherAnimDelaySeconds).
        // Do not restart the gather clip on every SetAction(..., true) while already in the gather state,
        // or the animation appears to spasm / run at the wrong rate after combat-related refreshes.
        if (_action == PlayerAction.Mining ||
            _action == PlayerAction.Woodcutting ||
            _action == PlayerAction.Fishing)
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
        PlayerAction resume =
            (state == State.MoveToPoint || state == State.MoveToTarget || state == State.MoveToPickup)
                ? PlayerAction.Walking
                : state == State.Gather
                    ? GetGatherAction()
                    : PlayerAction.Idle;
        SetAction(resume, true);
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
    private void GetClampXMinMax(out float minX, out float maxX)
    {
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
        return characterStats.FinalMoveSpeed;
    }

    private void UpdateSpriteFlip()
    {
        if (_suppressSpriteFlipForTeleport)
            return;

        float currentX = transform.position.x;
        // ✅ Combat facing override
        if (combat != null)
        {
            var target = combat.CurrentTarget;

            if (target != null && !target.IsDead)
            {
                float targetX = target.transform.position.x;          

                bool faceLeft = targetX < currentX;

                bool flip = faceLeft;
                if (invertFlip) flip = !flip;

                ApplyVisualFlip(flip);
                _lastX = currentX;
                return; // IMPORTANT: skip normal movement-based flip
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
        bool faceLeft = targetX < myX;

        bool flip = faceLeft;
        if (invertFlip) flip = !flip;

        ApplyVisualFlip(flip);
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
        float finalDamage = characterStats.TakeDamage(amount, type, out blocked, out float hpDamage);
        if (combat != null && finalDamage > 0f)
            combat.RecordIncomingDamageForDps(finalDamage, ToDpsBucket(type), attacker);

        AwardEnduranceXpFromIncomingDamage(preMitigatedDamage);

        if (blocked)
            wasCrit = false;

        if (DamagePopupSystem.Instance != null)
        {
            var anchor = GetComponentInChildren<DamagePopupAnchor>(true);
            Vector3 anchorPos = anchor ? anchor.WorldPos : transform.position;

            GetIncomingDamagePopupPlacement(anchorPos, attacker, 0.35f, out Vector3 pos, out Vector3 dir);

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
                blocked
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
    /// Spawn point and world drift for incoming damage popups. Always offsets horizontally away from the attacker
    /// and drifts in that same world direction (flee the threat). Does not use sprite facing so it stays correct
    /// even if <see cref="visualsRoot"/> scale and combat orientation disagree.
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

        float towardAttackerX = Mathf.Sign(attacker.position.x - transform.position.x);
        if (towardAttackerX == 0f)
            towardAttackerX = 1f;

        spawnWorldPos = anchorWorldPos + new Vector3(-towardAttackerX * sideOffset, 0f, 0f);

        Vector3 awayFromAttacker = transform.position - attacker.position;
        driftWorldDir = awayFromAttacker.sqrMagnitude > 0.0001f ? awayFromAttacker.normalized : Vector3.up;
    }

    public void Heal(float amount)
    {
        if (_isDead || !characterStats) return;
        characterStats.Heal(amount);
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

        characterStats.Heal(healAmount);
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
        MapNodeDefinition current = ActiveLevelContext.Current;
        if (current == null && GameplayLevelBootstrapper.Instance != null)
            current = GameplayLevelBootstrapper.Instance.ActiveDefinition;

        if (current != null && current.respawnHereIfDied)
            return current;

        return ResolveRegionTownRespawnNode();
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

    /// <summary>
    /// Hotkey action: travel to the town node for the region the player is currently in.
    /// Returns false when no valid town destination can be resolved.
    /// </summary>
    public static bool TryReturnToTownViaHotkey()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null && player.IsDead)
            return false;

        MapNodeDefinition destination = ResolveRegionTownRespawnNode();
        if (destination == null)
            return false;

        string townName = !string.IsNullOrWhiteSpace(destination.displayName) ? destination.displayName.Trim() : destination.nodeId;
        if (!string.IsNullOrWhiteSpace(townName))
            GameLog.Add($"Returning to town: {townName}");
        else
            GameLog.Add("Returning to town");

        ActiveLevelContext.SetPendingLevel(destination, logToConsole: false);
        PlayerLevelTransition.LoadSceneWithEffectOrImmediate(GameplaySceneName);
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

        if (deathPopupNotificationText)
            deathPopupNotificationText.text = "You have died";
        if (deathPopupButtonLabel)
            deathPopupButtonLabel.text = "Respawn";

        if (deathPopupButton)
        {
            deathPopupButton.onClick.RemoveAllListeners();
            deathPopupButton.onClick.AddListener(OnDeathPopupRespawnClicked);
            deathPopupButton.interactable = true;
        }

        deathPopupWindow.SetActive(true);
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
        const int topOrder = 32760;

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
        if (deathPopupButton)
            deathPopupButton.interactable = false;
        StartCoroutine(CoFadeAndRespawnToTown());
    }

    private IEnumerator CoFadeAndRespawnToTown()
    {
        CanvasGroup fader = CreateRuntimeSceneFader();
        if (fader != null)
        {
            fader.alpha = 0f;
            float t = 0f;
            float dur = Mathf.Max(0.01f, deathRespawnFadeSeconds);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                fader.alpha = Mathf.Lerp(0f, 1f, k);
                yield return null;
            }
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
        SaveManager.Instance?.SaveBeforeSceneTransition();
        SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
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

    private void ResolveDeathPopupRefs()
    {
        if (!deathPopupWindow)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.gameObject != null && t.gameObject.name == "PopupWindow")
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

    private static CanvasGroup CreateRuntimeSceneFader()
    {
        GameObject go = new GameObject("DeathRespawnFader", typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Image img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        RectTransform rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        return cg;
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
                        return 0f; // ✅ block = 0 damage
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
        // Clamp so negatives don't *increase* damage unless you want that design.
        rating = Mathf.Max(0f, rating);

        // 100/(100+rating) diminishing returns
        float multiplier = 100f / (100f + rating);
        return damage * multiplier;
    }

    private void AwardEnduranceXpFromIncomingDamage(float preMitigatedDamage)
    {
        if (preMitigatedDamage <= 0f)
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