using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Inventory))]
public class PlayerController : MonoBehaviour
{
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

    [Header("Stats")]
    [SerializeField] private CharacterStats characterStats;


    [Header("Pickup")]
    [SerializeField] private float pickupRange = 0.25f;
    private ItemDrop _pickupTarget;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    // Delay between gather animation “swings” while gathering
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

    [Header("Popup (World Tooltip)")]
    [SerializeField] private GameObject actionPopup;          // one popup object
    [SerializeField] private float actionPopupSeconds = 1.5f; // default duration
    private Coroutine _actionPopupRoutine;

    [Header("Tools Required (Gather)")]
    [SerializeField] private bool requireToolForGathering = true;

    private bool _hideHandsForUnarmedGather;

    [Header("Gather Without Tool (Debuff)")]
    [SerializeField] private bool allowGatherWithoutTool = true;

    [Tooltip("Multiplier applied when missing the required tool. 0.3 = 70% slower.")]
    [SerializeField, Range(0.05f, 1f)] private float missingToolSpeedMultiplier = 0.3f;

    [SerializeField] private PlayerCombatState combatState;
    public bool InCombat => combatState && combatState.InCombat;

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

    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private ToolbeltManager toolbelt;

    private bool _pausedForInvFull;
    private ResourceNode _pausedNode;

    private LaneBounds laneBounds;
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

        if (actionPopup)
            actionPopup.SetActive(false);
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
            bool shouldLookWalking =
                (state == State.MoveToPoint && Mathf.Abs(transform.position.x - moveTargetX) > clickArriveThreshold) ||
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
            return;

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
        ShowPopup("Inventory Full!", 1.5f);

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
    }

    // -------------------------
    // Click / Movement
    // -------------------------


    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if (locked)
        {
            // Cancel manual intentions immediately
            // (combat will drive movement)
            // Don't clear combat target here.
            targetNode = null;
            _pickupTarget = null;
            state = State.Idle;
        }
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

            sm.SetActiveXpDisplay(sk, src);
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

            ShowPopup(string.IsNullOrWhiteSpace(node.MissingToolMessage)
                ? "Missing tool in toolbelt."
                : node.MissingToolMessage);
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

    // -------------------------
    // Popup (World Tooltip)
    // -------------------------

    private void ShowPopupInternal(string msg, float? seconds = null)
    {
        if (!actionPopup) return;

        if (string.IsNullOrWhiteSpace(msg))
            msg = "Action not allowed.";

        var tmp = actionPopup.GetComponentInChildren<TMP_Text>(true);
        if (tmp) tmp.text = msg;

        if (_actionPopupRoutine != null)
            StopCoroutine(_actionPopupRoutine);

        _actionPopupRoutine = StartCoroutine(PopupRoutine(seconds ?? actionPopupSeconds));
    }

    public void ShowPopup(string msg)
    {
        ShowPopupInternal(msg, null);
    }

    public void ShowPopup(string msg, float seconds)
    {
        ShowPopupInternal(msg, seconds);
    }

    private IEnumerator PopupRoutine(float secs)
    {
        actionPopup.SetActive(true);
        yield return new WaitForSeconds(secs);
        actionPopup.SetActive(false);
        _actionPopupRoutine = null;
    }

    public void CancelAction()
    {
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

        float min = laneBounds ? laneBounds.MinX : -999f;
        float max = laneBounds ? laneBounds.MaxX : 999f;

        moveTargetX = Mathf.Clamp(x, min, max);
        state = State.MoveToPoint;

        equipment?.ClearMainHandVisualOverride();
        SetAction(PlayerAction.Walking, true);
    }

    public void MoveToPointX_Combat(float x)
    {
        if (_isDead) return;

        float min = laneBounds ? laneBounds.MinX : -999f;
        float max = laneBounds ? laneBounds.MaxX : 999f;

        float clamped = Mathf.Clamp(x, min, max);

        // ✅ already basically there? don't enter MoveToPoint state
        if (Mathf.Abs(transform.position.x - clamped) <= clickArriveThreshold)
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
        if (!targetNode) { ReturnToIdle(); return; }

        float targetX = targetNode.workSpot.position.x;
        MoveToX(targetX, GetMoveSpeed());

        float dist = Mathf.Abs(transform.position.x - targetX);
        if (dist <= targetNode.interactRange)
        {
            state = State.Gather;

            // ✅ face before snapping makes currentX != targetX still meaningful
            FaceTargetX(targetNode.workSpot.position.x);

            _nextGatherAnimTime = 0f;

            _accumItems = 0f;
            _gatherTimer = 0f;
            _nextGatherInterval = targetNode.GetNextInterval();

            ApplyGatherHandVisuals();

            SetAction(GetGatherAction(), true);
        }
    }

    private void TickMoveToPoint()
    {
        MoveToX(moveTargetX, GetMoveSpeed());

        float dist = Mathf.Abs(transform.position.x - moveTargetX);
        if (dist <= clickArriveThreshold)
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
                    ShowPopup("Inventory Full!", 1.5f);
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
                {
                    ShowPopup(string.IsNullOrWhiteSpace(targetNode.MissingToolMessage)
                        ? "Missing tool in toolbelt."
                        : targetNode.MissingToolMessage);
                }
            }
            else
            {
                CancelAction();
            }
        }
    }

    // -------------------------
    // Gather
    // -------------------------

    private void TickGather()
    {

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

            _nextGatherAnimTime = Time.time + Mathf.Max(0.05f, gatherAnimDelaySeconds);
        }

        Vector3 pos = transform.position;
        float minX = laneBounds ? laneBounds.MinX : -999f;
        float maxX = laneBounds ? laneBounds.MaxX : 999f;
        pos.x = Mathf.Clamp(targetNode.workSpot.position.x, minX, maxX);
        transform.position = pos;

        if (targetNode.UseRandomInterval)
        {
            if (_nextGatherInterval <= 0f)
                _nextGatherInterval = targetNode.GetNextInterval();

            _gatherTimer += Time.deltaTime * _gatherSpeedMultiplier;

            if (_gatherTimer >= _nextGatherInterval)
            {
                DoOneGatherTick();
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
            int mainAmt = def.RollMainYieldAmount();
            // Gathering Grit: doubles BASE yield only. Never duplicates bonus drops.
            if (mainAmt > 0 && UnityEngine.Random.value <= Mathf.Clamp01(_gatherGritChance))
                mainAmt *= 2;
            if (mainAmt > 0)
            {
                // Add main item
                int added = inventory.AddPartial(def.YieldItemId, mainAmt);
                int overflow = mainAmt - added;

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

                    ShowPopup("Inventory Full!", 1.5f);
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

                ShowPopup("Inventory Full!", 1.5f);
            }
        }
    }

    private void ReturnToIdle()
    {
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

    private void PauseGatherForLowEnergy()
    {
        ResourceNode resumeNode = targetNode;
        ShowPopup(string.IsNullOrWhiteSpace(lowEnergyPopupText) ? "* Fatigued *" : lowEnergyPopupText);
        // Stop gather immediately and return to full idle state, then retry after delay.
        _fatiguedUntilTime = Time.time + Mathf.Max(0.1f, fatiguedResumeDelaySeconds);
        ReturnToIdle();
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

        // Gathering
        if (_action == PlayerAction.Mining ||
            _action == PlayerAction.Woodcutting ||
            _action == PlayerAction.Fishing)
        {
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

        // Force a refresh so we immediately go back to walk/idle as appropriate
        SetAction(state == State.MoveToPoint || state == State.MoveToTarget || state == State.MoveToPickup
            ? PlayerAction.Walking
            : PlayerAction.Idle, true);
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
        float min = laneBounds ? laneBounds.MinX : -999f;
        float max = laneBounds ? laneBounds.MaxX : 999f;

        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, x, speed * Time.deltaTime);
        pos.x = Mathf.Clamp(pos.x, min, max);

        transform.position = pos;
    }

    private float GetMoveSpeed()
    {
        // fallback so you don't brick movement if stats is missing
        if (!characterStats) return 3f;
        return characterStats.FinalMoveSpeed;
    }

    private void UpdateSpriteFlip()
    {
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
            float targetX = targetNode.workSpot.position.x;
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

        bool blocked;
        float finalDamage = characterStats.TakeDamage(amount, type, out blocked);

        if (blocked)
            wasCrit = false;

        if (DamagePopupSystem.Instance != null)
        {
            // Prefer child anchor so it follows visual flip/offset correctly.
            var anchor = GetComponentInChildren<DamagePopupAnchor>(true);
            Vector3 pos = anchor ? anchor.WorldPos : transform.position;

            // If we know the attacker, bias the popup to the impact side (attacker side),
            // so damage appears "in front" even if we're facing away.
            if (attacker)
            {
                float dirX = Mathf.Sign(attacker.position.x - transform.position.x); // toward attacker
                if (dirX == 0f) dirX = 1f;
                pos.x += dirX * 0.25f;
            }

            Vector3 dir = attacker
                ? (transform.position - attacker.position).normalized
                : Vector3.up;

            FloatingDamageTextUI.PopupDamageKind popupKind = type switch
            {
                DamageType.Physical => FloatingDamageTextUI.PopupDamageKind.Physical,
                DamageType.Magical => FloatingDamageTextUI.PopupDamageKind.Magical,
                DamageType.True => FloatingDamageTextUI.PopupDamageKind.True,
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

        if (!blocked && !_isDead)
        {
            TriggerHurtAnim();
        }
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
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        clickToMoveEnabled = false;

        combat?.ClearTarget();
        ClearActionOverride();
        CancelAction();

        TriggerDieAnim();

        if (_deathRoutine != null) StopCoroutine(_deathRoutine);
        _deathRoutine = StartCoroutine(DisableAfterDeath());
    }

    private IEnumerator DisableAfterDeath()
    {
        float wait = GetDieClipLength();
        yield return new WaitForSeconds(Mathf.Max(0.05f, wait));

        // for now: disable player
        gameObject.SetActive(false);
    }

   


    private float ApplyMitigation(float rawDamage, DamageType type, out bool blocked)
    {
        blocked = false;

        rawDamage = Mathf.Max(0f, rawDamage);
        if (rawDamage <= 0f) return 0f;

        switch (type)
        {
            case DamageType.True:
                return rawDamage;

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

            case DamageType.Magical:
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