using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CharacterStats))]
public class EnemyBaseController : MonoBehaviour
{
    public enum EnemyState { Idle, Chasing, Attacking, Dead }

    [SerializeField] private string displayName = "Enemy";

    [Header("Data (source of truth)")]
    [Tooltip("When assigned, base stats and display name are applied from this asset at runtime. Leave empty to keep prefab CharacterStats values.")]
    [SerializeField] private EnemyDefinition definition;

    [Header("Shared Stats")]
    [SerializeField] private CharacterStats stats;

    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Aggro")]
    [SerializeField] private float aggroRange = 5f;

    [Header("Movement")]
    [Tooltip("Chase speed. 0 = stationary (e.g. target dummy, fixed boss). Also feeds combat power mobility (via CharacterStats).")]
    [Min(0f)]
    [SerializeField] private float moveSpeed = 2.5f;
    [Tooltip("Move on X only (recommended).")]
    [SerializeField] private bool xOnly = true;

    [Tooltip("Set from EnemyDefinition.idleWander* when a definition is applied.")]
    [SerializeField] private bool idleWanderEnabled;
    [SerializeField] private float idleWanderSpeed = 0.8f;
    [SerializeField] private float idleWanderMoveMinSec = 1f;
    [SerializeField] private float idleWanderMoveMaxSec = 3f;
    [SerializeField] private float idleWanderIdleMinSec = 2f;
    [SerializeField] private float idleWanderIdleMaxSec = 8f;
    [SerializeField] private float idleWanderMaxDistanceFromSpawn = 10f;

    private bool _idleWanderInMovePhase = true;
    private float _idleWanderPhaseEndTime;
    private float _idleWanderDirSign = 1f;
    private int _idleWanderPhaseTickFrame = -1;
    private const float WanderSpawnRangeEpsilon = 0.02f;
    private const float WorldBoundsXPadding = 0f;
    private float _spawnOriginX;

    [Header("Attack (from CharacterStats)")]
    [Tooltip("Delay before damage is applied (for animation timing).")]
    [SerializeField] private float enemyAttackWindup = 0.10f;

    [Tooltip("Require player still in range when hit happens.")]
    [SerializeField] private bool requireRangeOnHit = true;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = true;

    [Tooltip("Optional override if clip lookup fails. Set to die clip length (seconds).")]
    [SerializeField] private float dieClipLengthOverride = 0f;

    [Header("Combat Engage")]
    [SerializeField] private float engagePadding = 0.05f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Tooltip("Exact state names in Animator (case-sensitive).")]
    [SerializeField] private string dieStateName = "die";

    [SerializeField] private string movingBool = "Moving";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string dieTrigger = "Die";

    [Header("Sprite Flip")]
    [SerializeField] private Transform visualsRoot;
    [SerializeField] private bool invertFlip = true;

    [Header("World UI (do NOT flip)")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private Transform nameLabel;
    [SerializeField] private Transform hpBar;

    [Header("Damage Popup Anchor")]
    [Tooltip("Optional override. If null, we auto-resolve (prefer under visualsRoot, then under this enemy).")]
    [SerializeField] private DamagePopupAnchor damagePopupAnchor;

    /// <summary>Last dealer world position for DoT popups when the dealer Transform was destroyed.</summary>
    private Vector3 _damagePopupDealerLastWorldPos;
    private bool _damagePopupDealerLastWorldPosValid;

    [Header("Loot drop (world pickup)")]
    [Tooltip("Where item loot from EnemyDefinition spawns. If null, auto-finds a descendant named DropAnchor, else uses this enemy's position.")]
    [SerializeField] private Transform dropLootAnchor;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private EnemyState state = EnemyState.Idle;

    public static int AliveEnemyCount { get; private set; }

    private Rigidbody2D _rb;
    private PlayerController _playerController;
    private PlayerCombatState _playerCombatState;

    private float _nextEnemyAttackTime;
    private bool _hitQueued;
    private float _hitTime;
    private bool _queuedHitCommitted;
    private float _stunnedUntil;

    private bool _countedAlive;
    private bool _provoked;
    private bool _playerDamagedThisEnemy;
    private bool _minionDamagedThisEnemy;
    private Transform _retaliationMinionTarget;
    private bool _mapAggroTriggeredForSession;
    private bool _engaged;
    private bool _isChasingForRange;
    private bool _isElite;
    private float _mapScalingLootChanceMultiplier = 1f;
    private float _mapScalingGoldMultiplier = 1f;
    private float _mapScalingXpRateMultiplier = 1f;
    private float _mapEnhancementLootBonusFraction;
    private float _mapEnhancementGoldBonusFraction;
    private float _nextDeadlyCloseRangePulseTime;
    /// <summary>Display name without the Elite prefix; used for overhead rich text (red "Elite" + name).</summary>
    private string _nameCoreForUi = "";

    private const float EliteVisualScale = 1.3f;
    private const float DeadlyCloseRangePulseIntervalSeconds = 1f;
    private static readonly Color EliteSpriteColorTint = new Color(1f, 0.72f, 0.72f, 1f);
    private const string BossPrefixRichText = "<size=125%><color=#FF2B2B><b>Boss</b></color></size>";

    private CurrencyWallet _wallet;
    private GoldPopupSpawner _goldPopupSpawner;
    private AilmentController _ailments;
    private EnemyAbilityController _abilityController;
    private bool _abilityCombatActive;
    private bool _abilityEnrageApplied;

    public bool IsDead => state == EnemyState.Dead;
    public bool IsStunned => Time.time < _stunnedUntil;
    public CharacterStats Stats => stats;
    public int HP => stats ? Mathf.RoundToInt(stats.HP) : 0;
    public int MaxHP => stats ? stats.MaxHP : 0;

    public int Armor => stats ? stats.Armor : 0;
    public int MagicResist => stats ? stats.MagicResist : 0;
    public float PhysBlockChance => stats ? stats.PhysBlockChance : 0f;
    public float CritChance => stats ? stats.CritChance : 0f;
    public float CritMultiplier => stats ? stats.CritMultiplier : 1f;

    public float AttackRange => stats ? stats.Range : 0f;
    public float AttacksPerSecond => stats ? stats.AttacksPerSecond : 0f;

    /// <summary>Inspector Move Speed — fed into <see cref="CharacterStats"/> CP mobility / combat profile for this enemy.</summary>
    public float MoveSpeed => moveSpeed;

    public float CombatPower => stats ? stats.CombatPower : 0f;
    public int CombatPowerRounded => Mathf.RoundToInt(CombatPower);

    public string DisplayName => displayName;

    /// <summary>Spawned as Elite from level respawn roll (+HP, +damage, 2× XP per hit, 2× gold).</summary>
    public bool IsElite => _isElite;

    /// <summary>Active data asset; set in the inspector or via <see cref="InitializeFromDefinition"/>.</summary>
    public EnemyDefinition Definition => definition;

    public string EnemyId => definition ? definition.enemyId : "";

    public string EnemyDescription => definition ? definition.description : "";

    public Sprite EnemyIcon => definition ? definition.icon : null;

    /// <summary>Combat profile label derived from stats (same as <see cref="CharacterStats.GetCombatProfileLabel"/>).</summary>
    public string CombatProfileLabel => stats ? stats.GetCombatProfileLabel() : "";

    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;
    public event Action<int, bool> OnDamaged;
    public event Action<string> OnNameChanged;

    public void SetDisplayName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        displayName = newName;
        if (!_isElite)
            _nameCoreForUi = "";
        OnNameChanged?.Invoke(displayName);
    }

    /// <summary>TMP rich text prefixes: Boss (bigger red), Elite (red), then core display name.</summary>
    public string GetRichTextDisplayNameForOverhead()
    {
        string core = string.IsNullOrEmpty(_nameCoreForUi) ? displayName : _nameCoreForUi;
        bool isBoss = definition != null && definition.isBossEnemy;

        if (isBoss && _isElite)
            return $"{BossPrefixRichText} <color=#FF5C5C>Elite</color> {core}";
        if (isBoss)
            return $"{BossPrefixRichText} {core}";
        if (_isElite)
            return $"<color=#FF5C5C>Elite</color> {core}";
        return core;
    }

    /// <summary>
    /// Applies identity and combat values from <paramref name="def"/>; behaviour and visuals stay on the prefab.
    /// Call immediately after spawning if you assign the definition from code (after Awake has run without a definition).
    /// </summary>
    public void InitializeFromDefinition(EnemyDefinition def, bool spawnAsElite = false)
    {
        if (def == null)
        {
            Debug.LogWarning($"[Enemy] InitializeFromDefinition(null) on '{name}' — keeping prefab stats.", this);
            return;
        }

        definition = def;
        _isElite = spawnAsElite;
        _abilityEnrageApplied = false;

        if (!stats)
            stats = GetComponent<CharacterStats>();

        if (!stats)
        {
            Debug.LogError($"[Enemy] {name} is missing CharacterStats.", this);
            return;
        }

        stats.ApplyEnemyDefinition(def);
        ApplyActiveMapCombatScaling();
        ApplyActiveMapEnhancementModifiers();
        if (spawnAsElite)
        {
            stats.ApplyEliteEnemyScaling();
            ApplyMapEnhancementEliteHealthReduction();
        }

        string dn = string.IsNullOrWhiteSpace(def.displayName) ? "Enemy" : def.displayName.Trim();
        _nameCoreForUi = dn;
        if (spawnAsElite)
        {
            SetDisplayName("Elite " + dn);
            ApplyEliteVisualPresentation();
        }
        else
        {
            SetDisplayName(dn);
        }

        moveSpeed = Mathf.Max(0f, def.moveSpeed);

        idleWanderEnabled = def.idleWanderEnabled;
        idleWanderSpeed = Mathf.Max(0f, def.idleWanderSpeed);
        idleWanderMoveMinSec = Mathf.Max(0.05f, def.idleWanderMoveMinSec);
        idleWanderMoveMaxSec = Mathf.Max(idleWanderMoveMinSec, def.idleWanderMoveMaxSec);
        idleWanderIdleMinSec = Mathf.Max(0.05f, def.idleWanderIdleMinSec);
        idleWanderIdleMaxSec = Mathf.Max(idleWanderIdleMinSec, def.idleWanderIdleMaxSec);
        idleWanderMaxDistanceFromSpawn = Mathf.Max(0f, def.idleWanderMaxDistanceFromSpawn);
        ResetIdleWanderCycle();

        stats.RefreshVitalsFromStats(fillIfEmpty: true);
        OnHealthChanged?.Invoke(HP, MaxHP);

        EnsureAbilityController();
        _abilityController?.Bind(def, this);
    }

    private void EnsureAbilityController()
    {
        if (_abilityController)
            return;

        _abilityController = GetComponent<EnemyAbilityController>();
        if (!_abilityController)
            _abilityController = gameObject.AddComponent<EnemyAbilityController>();
    }

    private void ApplyActiveMapCombatScaling()
    {
        _mapScalingLootChanceMultiplier = 1f;
        _mapScalingGoldMultiplier = 1f;
        _mapScalingXpRateMultiplier = 1f;

        if (!stats)
            return;

        MapNodeDefinition node = MapCombatScaling.ResolveActiveCombatMapNode();
        if (node == null || !node.IsMapCombatScalingEnabled())
            return;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        int scalingLevel = node.GetCombatScalingLevel(progress);

        float hpMult = MapCombatScaling.GetHpMultiplier(scalingLevel);
        stats.ApplyMapCombatScalingHealth(hpMult);

        _mapScalingLootChanceMultiplier = MapCombatScaling.GetLootChanceMultiplier(scalingLevel);
        _mapScalingGoldMultiplier = MapCombatScaling.GetGoldMultiplier(scalingLevel);
        _mapScalingXpRateMultiplier = MapCombatScaling.GetXpRateMultiplier(scalingLevel);
    }

    private void ApplyActiveMapEnhancementModifiers()
    {
        _mapEnhancementLootBonusFraction = 0f;
        _mapEnhancementGoldBonusFraction = 0f;

        if (!stats)
            return;

        MapNodeDefinition node = MapCombatScaling.ResolveActiveCombatMapNode();
        if (node == null)
            return;

        MapEnhancementAggregate aggregate = MapEnhancementService.BuildAggregate(node);
        if (aggregate == null || !aggregate.HasAnyEffect)
            return;

        _mapEnhancementLootBonusFraction = Mathf.Max(0f, aggregate.lootBonusFraction);
        _mapEnhancementGoldBonusFraction = Mathf.Max(0f, aggregate.goldBonusFraction);

        if (aggregate.enemyDamageReductionFraction > 0.001f)
            stats.ApplyMapEnhancementDamageReduction(aggregate.enemyDamageReductionFraction);
    }

    private void ApplyMapEnhancementEliteHealthReduction()
    {
        if (!stats)
            return;

        MapNodeDefinition node = MapCombatScaling.ResolveActiveCombatMapNode();
        if (node == null)
            return;

        MapEnhancementAggregate aggregate = MapEnhancementService.BuildAggregate(node);
        if (aggregate == null || aggregate.eliteHealthReductionFraction <= 0.001f)
            return;

        stats.ApplyMapEnhancementEliteHealthReduction(aggregate.eliteHealthReductionFraction);
    }

    /// <summary>After <see cref="InitializeFromDefinition"/>, applies Tier II–V scaling from <see cref="EnduranceTrialTier"/>.</summary>
    public void ApplyEnduranceTrialTier(int tier1Based)
    {
        if (!stats)
            stats = GetComponent<CharacterStats>();
        if (!stats)
            return;

        float h = EnduranceTrialTier.GetHealthMultiplier(tier1Based);
        float d = EnduranceTrialTier.GetDamageMultiplier(tier1Based);
        float a = EnduranceTrialTier.GetArmorAndResistMultiplier(tier1Based);
        stats.ApplyEnduranceTrialDifficultyScaling(h, d, a);
        OnHealthChanged?.Invoke(HP, MaxHP);
    }

    private void Awake()
    {
        if (!damagePopupAnchor)
        {
            // Prefer an anchor under the flipped visuals (Body) so it mirrors correctly when facing changes.
            if (visualsRoot)
                damagePopupAnchor = visualsRoot.GetComponentInChildren<DamagePopupAnchor>(true);

            // Fallback: anywhere under this enemy.
            if (!damagePopupAnchor)
                damagePopupAnchor = GetComponentInChildren<DamagePopupAnchor>(true);
        }
        _rb = GetComponent<Rigidbody2D>();
        _ailments = GetComponent<AilmentController>();

        if (!stats)
            stats = GetComponent<CharacterStats>();

        if (!stats)
        {
            Debug.LogError($"[Enemy] {name} is missing CharacterStats.", this);
            enabled = false;
            return;
        }

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!animator)
            Debug.LogError("[Enemy] Animator not found. Assign it on the prefab.", this);

        if (!uiRoot)
        {
            var t = transform.Find("UIRoot");
            if (!t) t = transform.Find("UI");
            if (t) uiRoot = t;
        }

        if (definition != null)
            InitializeFromDefinition(definition);
        else
            EnsureAbilityController();

        _spawnOriginX = transform.position.x;
    }

    private void OnEnable()
    {
        LevelAggroState.AggroPulseTriggered -= HandleAggroPulseTriggered;
        LevelAggroState.AggroPulseTriggered += HandleAggroPulseTriggered;

        if (stats != null)
        {
            stats.OnHPChanged += HandleStatsHPChanged;
            stats.OnDied += HandleStatsDied;
        }

        if (state != EnemyState.Dead && !_countedAlive)
        {
            AliveEnemyCount++;
            _countedAlive = true;
        }

        CombatEnemyRegistry.Register(this);
    }

    private void OnDisable()
    {
        CombatEnemyRegistry.Unregister(this);

        LevelAggroState.AggroPulseTriggered -= HandleAggroPulseTriggered;

        if (stats != null)
        {
            stats.OnHPChanged -= HandleStatsHPChanged;
            stats.OnDied -= HandleStatsDied;
        }

        if (_countedAlive)
        {
            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);
            _countedAlive = false;
        }

        _provoked = false;
        _mapAggroTriggeredForSession = false;
        EndAbilityCombat();
        ClearEngagement();
    }

    private void Start()
    {
        if (!stats) return;

        stats.RefreshVitalsFromStats(fillIfEmpty: true);

        OnHealthChanged?.Invoke(HP, MaxHP);
        SetMoving(false);
        ResetIdleWanderCycle(startInMovePhase: true);
    }

    private void ResetIdleWanderCycle(bool startInMovePhase = false)
    {
        if (!idleWanderEnabled || idleWanderSpeed <= 0f)
            return;

        _idleWanderInMovePhase = startInMovePhase;
        _idleWanderPhaseEndTime = Time.time + (_idleWanderInMovePhase
            ? UnityEngine.Random.Range(idleWanderMoveMinSec, idleWanderMoveMaxSec)
            : UnityEngine.Random.Range(idleWanderIdleMinSec, idleWanderIdleMaxSec));
        _idleWanderDirSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
    }

    private void TickIdleWanderPhaseIfNeeded()
    {
        if (_idleWanderPhaseTickFrame == Time.frameCount)
            return;

        if (!idleWanderEnabled || idleWanderSpeed <= 0f)
            return;
        if (!IsPlayerValidAlive())
            return;

        _idleWanderPhaseTickFrame = Time.frameCount;

        while (Time.time >= _idleWanderPhaseEndTime)
        {
            if (_idleWanderInMovePhase)
            {
                _idleWanderInMovePhase = false;
                _idleWanderPhaseEndTime = Time.time +
                    UnityEngine.Random.Range(idleWanderIdleMinSec, idleWanderIdleMaxSec);
            }
            else
            {
                _idleWanderInMovePhase = true;
                _idleWanderPhaseEndTime = Time.time +
                    UnityEngine.Random.Range(idleWanderMoveMinSec, idleWanderMoveMaxSec);
                _idleWanderDirSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            }
        }
    }

    private bool ShouldShowIdleWanderMoving()
    {
        if (!idleWanderEnabled || idleWanderSpeed <= 0f)
            return false;
        if (!IsPlayerValidAlive())
            return false;
        GetIdleWanderSpawnBounds(out float minX, out float maxX);

        return _idleWanderInMovePhase || IsOutsideWanderSpawnRange(transform.position.x, minX, maxX);
    }

    private void Update()
    {
        ResolvePlayer();

        if (state == EnemyState.Dead)
            return;

        if (HandleStunnedCombatLockout())
            return;

        if (IsAbilityMovementLocked())
        {
            _hitQueued = false;
            _queuedHitCommitted = false;
            SetMoving(false);
            if (_rb)
                StopHorizontal();
            return;
        }

        // Same vitals regen as the player (life/energy/mana); base stats apply to enemies too.
        if (stats)
            stats.TickRegen(Time.deltaTime);

        if (!IsCombatThreatValidAlive())
        {
            _hitQueued = false;
            _provoked = false;
            _playerDamagedThisEnemy = false;
            _minionDamagedThisEnemy = false;
            _retaliationMinionTarget = null;
            EndAbilityCombat();
            ClearEngagement();
            state = EnemyState.Idle;
            SetMoving(false);
            return;
        }

        float dist = GetNearestPlayerTeamThreatDistanceX();
        bool shouldAggro = ResolveShouldAggro(dist);

        if (!shouldAggro)
        {
            _hitQueued = false;
            _queuedHitCommitted = false;
            EndAbilityCombat();
            ClearEngagement();
            state = EnemyState.Idle;
            TickIdleWanderPhaseIfNeeded();
            SetMoving(ShouldShowIdleWanderMoving());
            return;
        }

        BeginAbilityCombatIfNeeded();
        _abilityController?.Tick();

        // Special close-range pulses should only happen after the enemy is actively aggroed.
        TryApplyDeadlyCloseRangeEffect();

        UpdateEngagement(dist);

        if (state != EnemyState.Dead)
        {
            Transform threat = GetCombatThreatTransform();
            if (threat)
                FaceTargetX(threat.position.x);
        }

        bool shouldChaseForRange = ResolveShouldChaseForRange(dist);
        if (!shouldChaseForRange)
        {
            state = EnemyState.Attacking;
            TryStartEnemyAttack();
        }
        else
        {
            state = EnemyState.Chasing;
        }

        SetMoving(state == EnemyState.Chasing);

        if (_hitQueued && Time.time >= _hitTime)
        {
            _hitQueued = false;
            bool committedHit = _queuedHitCommitted;
            _queuedHitCommitted = false;

            if (!IsCombatThreatValidAlive()) return;

            if (IsStunned)
                return;

            // If the attack windup already started, treat the hit as committed.
            if (committedHit || !requireRangeOnHit || DistanceToThreatX() <= AttackRange)
                ApplyEnemyHitToThreat();
        }
    }

    private void FixedUpdate()
    {
        if (state == EnemyState.Dead || !_rb)
            return;

        if (IsStunned)
        {
            StopHorizontal();
            EnforceWorldBoundsX();
            return;
        }

        if (IsAbilityMovementLocked())
        {
            StopHorizontal();
            EnforceWorldBoundsX();
            return;
        }

        if (!IsCombatThreatValidAlive())
        {
            StopHorizontal();
            EnforceWorldBoundsX();
            return;
        }

        float dist = DistanceToThreatX();
        bool shouldAggro = ResolveShouldAggro(GetNearestPlayerTeamThreatDistanceX());
        bool shouldChaseForRange = ResolveShouldChaseForRange(dist);
        if (shouldAggro && shouldChaseForRange)
        {
            Transform threat = GetCombatThreatTransform();
            if (!threat)
            {
                StopHorizontal();
                EnforceWorldBoundsX();
                return;
            }

            float dx = threat.position.x - transform.position.x;
            float dir = Mathf.Sign(dx);
            float ailmentMoveMult = _ailments != null ? _ailments.GetMoveSpeedMultiplier() : 1f;
            float currentMoveSpeed = Mathf.Max(0f, moveSpeed * ailmentMoveMult);

            float yVel = _rb.linearVelocity.y;
            _rb.linearVelocity = new Vector2(dir * currentMoveSpeed, xOnly ? yVel : _rb.linearVelocity.y);
        }
        else if (!shouldAggro && TryApplyIdleWanderMovement())
        {
            // Wander velocity applied in TryApplyIdleWanderMovement.
        }
        else
        {
            StopHorizontal();
        }

        EnforceWorldBoundsX();
    }

    /// <summary>
    /// Keeps the enemy inside <see cref="WorldBounds"/> horizontally (chase, idle wander, knockback, etc.).
    /// </summary>
    private void EnforceWorldBoundsX()
    {
        if (!_rb || !WorldBounds.Instance)
            return;

        float minX = WorldBounds.Instance.Left + WorldBoundsXPadding;
        float maxX = WorldBounds.Instance.Right - WorldBoundsXPadding;
        if (minX > maxX)
            maxX = minX;

        Vector2 p = _rb.position;
        Vector2 v = _rb.linearVelocity;

        p.x = Mathf.Clamp(p.x, minX, maxX);

        if (p.x <= minX)
            v.x = Mathf.Max(0f, v.x);
        else if (p.x >= maxX)
            v.x = Mathf.Min(0f, v.x);

        _rb.position = p;
        _rb.linearVelocity = v;
    }

    private bool TryApplyIdleWanderMovement()
    {
        TickIdleWanderPhaseIfNeeded();

        if (!idleWanderEnabled || idleWanderSpeed <= 0f)
            return false;

        GetIdleWanderSpawnBounds(out float minX, out float maxX);

        bool outsideBounds = IsOutsideWanderSpawnRange(transform.position.x, minX, maxX);
        if (!_idleWanderInMovePhase && !outsideBounds)
            return false;

        float x = transform.position.x;

        if (x <= minX)
            _idleWanderDirSign = 1f;
        else if (x >= maxX)
            _idleWanderDirSign = -1f;

        if (!_idleWanderInMovePhase && outsideBounds)
            _idleWanderPhaseEndTime = Time.time + UnityEngine.Random.Range(idleWanderMoveMinSec, idleWanderMoveMaxSec);

        FaceTargetX(transform.position.x + _idleWanderDirSign * 100f);

        float ailmentMoveMult = _ailments != null ? _ailments.GetMoveSpeedMultiplier() : 1f;
        float spd = Mathf.Max(0f, idleWanderSpeed * ailmentMoveMult);
        float yVel = _rb.linearVelocity.y;
        _rb.linearVelocity = new Vector2(_idleWanderDirSign * spd, xOnly ? yVel : _rb.linearVelocity.y);
        return true;
    }

    private void GetIdleWanderSpawnBounds(out float minX, out float maxX)
    {
        float maxDist = Mathf.Max(0f, idleWanderMaxDistanceFromSpawn);
        minX = _spawnOriginX - maxDist;
        maxX = _spawnOriginX + maxDist;
    }

    private static bool IsOutsideWanderSpawnRange(float x, float minX, float maxX)
    {
        return x < minX - WanderSpawnRangeEpsilon || x > maxX + WanderSpawnRangeEpsilon;
    }

    private void StopHorizontal()
    {
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    private bool ResolveShouldChaseForRange(float dist)
    {
        float attackRange = Mathf.Max(0f, AttackRange);
        float chaseStart = attackRange + Mathf.Max(0f, engagePadding);
        float chaseStop = attackRange;

        if (_isChasingForRange)
            _isChasingForRange = dist > chaseStop;
        else
            _isChasingForRange = dist > chaseStart;

        return _isChasingForRange;
    }

    private bool ResolveShouldAggro(float nearestPlayerTeamDistanceX)
    {
        MapNodeDefinition def = GetActiveMapNodeDefinition();
        LevelEnemyAggroMode mode = def != null ? def.enemyAggroMode : LevelEnemyAggroMode.Aggressive;
        bool playerTriggeredWaveAggro = def != null && LevelAggroState.IsWaveAggroLatched(def);
        bool playerTriggeredMapAggro = playerTriggeredWaveAggro || _mapAggroTriggeredForSession;
        bool ignoreRange = LevelIgnoresAggroRange(def);

        return EnemyAggro.ShouldEngage(
            mode,
            _provoked,
            playerTriggeredMapAggro,
            nearestPlayerTeamDistanceX,
            aggroRange,
            ignoreRange);
    }

    /// <summary>
    /// When the active <see cref="MapNodeDefinition"/> sets <see cref="MapNodeDefinition.ignoreAggroRange"/>,
    /// enemies skip the horizontal distance gate once they would aggro. Aggressive mode commits from anywhere;
    /// Calm remains retaliation-only; CalmUntilPlayerAggressive still requires map/player provocation first.
    /// </summary>
    private static bool LevelIgnoresAggroRange(MapNodeDefinition def)
    {
        if (def == null)
            def = GetActiveMapNodeDefinition();
        return def != null && def.ignoreAggroRange;
    }

    private static MapNodeDefinition GetActiveMapNodeDefinition()
    {
        MapNodeDefinition def = ActiveLevelContext.Current;
        if (def == null && GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        return def;
    }

    private void ResolvePlayer()
    {
        if (player && _playerController && _wallet && _goldPopupSpawner) return;

        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) player = go.transform;
        }

        if (player && !_playerController)
            _playerController = player.GetComponent<PlayerController>();

        if (player && _playerCombatState == null)
            _playerCombatState = player.GetComponent<PlayerCombatState>();

        if (!_wallet)
            _wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

        if (!_goldPopupSpawner)
            _goldPopupSpawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
    }

    private float DistanceToPlayerX()
    {
        if (!player) return float.MaxValue;
        return Mathf.Abs(player.position.x - transform.position.x);
    }

    private float DistanceToThreatX()
    {
        Transform threat = GetCombatThreatTransform();
        if (!threat) return float.MaxValue;
        return Mathf.Abs(threat.position.x - transform.position.x);
    }

    private float GetNearestPlayerTeamThreatDistanceX() =>
        EnemyAggro.GetNearestPlayerTeamDistanceX(transform.position.x, player);

    private Transform GetCombatThreatTransform()
    {
        if (IsRetaliationMinionValidAlive())
            return _retaliationMinionTarget;

        if (!_playerDamagedThisEnemy && TryGetMapAggroMinionThreat(out Transform mapMinion))
            return mapMinion;

        return player;
    }

    private static bool TryGetMapAggroMinionThreat(out Transform minionThreat)
    {
        minionThreat = LevelAggroState.MapAggroInstigatorMinion;
        if (!minionThreat)
            return false;

        MinionCombatTarget mct = minionThreat.GetComponent<MinionCombatTarget>();
        if (!mct)
            mct = minionThreat.GetComponentInParent<MinionCombatTarget>();
        if (mct == null || !mct.IsAlive)
        {
            minionThreat = null;
            return false;
        }

        minionThreat = mct.transform;
        return true;
    }

    private bool IsRetaliationMinionValidAlive()
    {
        if (!_retaliationMinionTarget)
            return false;

        MinionCombatTarget mct = _retaliationMinionTarget.GetComponent<MinionCombatTarget>();
        if (!mct)
            mct = _retaliationMinionTarget.GetComponentInParent<MinionCombatTarget>();
        if (mct == null || !mct.IsAlive)
        {
            _retaliationMinionTarget = null;
            return false;
        }

        return true;
    }

    private bool IsCombatThreatValidAlive()
    {
        if (IsRetaliationMinionValidAlive())
            return true;
        return IsPlayerValidAlive();
    }

    /// <summary>Enemy switches melee retaliation to a living minion that damaged it.</summary>
    public void NotifyRetaliationAgainstMinion(Transform minionTransform)
    {
        if (!minionTransform)
            return;

        MinionCombatTarget mct = minionTransform.GetComponent<MinionCombatTarget>();
        if (!mct)
            mct = minionTransform.GetComponentInParent<MinionCombatTarget>();
        if (mct == null || !mct.IsAlive)
            return;

        _retaliationMinionTarget = mct.transform;
        _provoked = true;
    }

    /// <summary>Center-to-center X distance used for disengage and other melee-threat checks.</summary>
    public float GetPlayerMeleeThreatDistanceX() => DistanceToThreatX();

    public bool TryApplyStun(float durationSeconds, float chance01, Transform source = null)
    {
        if (state == EnemyState.Dead || durationSeconds <= 0f)
            return false;

        if (UnityEngine.Random.value >= Mathf.Clamp01(chance01))
            return false;

        _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + durationSeconds);
        _hitQueued = false;
        _queuedHitCommitted = false;
        if (_rb)
            StopHorizontal();
        SetMoving(false);
        TrySpawnStunStatusPopup(source);
        return true;
    }

    private bool HandleStunnedCombatLockout()
    {
        if (!IsStunned)
            return false;

        _hitQueued = false;
        _queuedHitCommitted = false;
        SetMoving(false);
        if (_rb)
            StopHorizontal();

        return true;
    }

    private void TrySpawnStunStatusPopup(Transform source)
    {
        if (DamagePopupSystem.Instance == null)
            return;

        DamagePopupAnchor anchor = GetComponentInChildren<DamagePopupAnchor>(true);
        Vector3 anchorPos = anchor != null ? anchor.WorldPos : transform.position;
        Vector3 dealerPos = source != null ? source.position : transform.position;
        Vector3 pos = DamagePopupSystem.GetWorldPosBehindVictim(anchorPos, dealerPos);

        Color color = new Color32(38, 22, 12, 255);
        FloatingDamageTextUI prefab = DamagePopupSystem.Instance.PopupPrefab;
        if (prefab != null)
            color = prefab.StunPresentationColor;

        DamagePopupSystem.Instance.SpawnLingeringStatus(pos, "Stunned", color, transform);
    }

    private void TrySpawnExecuteStatusPopup(Transform source)
    {
        if (DamagePopupSystem.Instance == null)
            return;

        DamagePopupAnchor anchor = GetComponentInChildren<DamagePopupAnchor>(true);
        Vector3 anchorPos = anchor != null ? anchor.WorldPos : transform.position;
        Vector3 dealerPos = source != null ? source.position : transform.position;
        Vector3 pos = DamagePopupSystem.GetWorldPosBehindVictim(anchorPos, dealerPos);

        Color color = new Color32(140, 18, 28, 255);
        FloatingDamageTextUI prefab = DamagePopupSystem.Instance.PopupPrefab;
        if (prefab != null)
            color = prefab.ExecutePresentationColor;

        DamagePopupSystem.Instance.SpawnLingeringStatus(
            pos,
            AbilityCombatPower.WayOfTheSlayerExecuteStatusPopupLabel,
            color,
            transform);
    }

    /// <summary>Way of the Slayer — bypasses mitigation and removes all remaining guard/HP.</summary>
    public int TakeExecuteDamage(
        Transform attacker,
        AttackSkill? attackSkillSource = null,
        string outgoingDpsSourceLabel = null)
    {
        if (state == EnemyState.Dead || stats == null)
            return 0;

        TryTriggerMapWideAggroFromAttacker(attacker);
        _provoked = true;

        if (IsImmuneToIncomingHit(attackSkillSource))
        {
            ShowImmunePopup(attacker);
            return 0;
        }

        float applied = stats.ApplyExecuteDamage(out _);
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(applied));
        if (finalDamage <= 0)
            return 0;

        AwardCombatXpToSource(attacker, finalDamage, DpsDamageBucket.Physical, outgoingDpsSourceLabel);
        OnDamaged?.Invoke(finalDamage, false);

        if (DamagePopupSystem.Instance != null && ToggleSettingsStore.Get(ToggleSettingId.ShowOutgoingDamageNumbers))
        {
            GetDamagePopupSpawnForDealer(attacker, null, out Vector3 pos, out Vector3 dir);
            DamagePopupSystem.Instance.Spawn(
                pos,
                finalDamage,
                FloatingDamageTextUI.PopupDamageKind.Physical,
                false,
                false,
                dir,
                false);
            TrySpawnExecuteStatusPopup(attacker);
        }

        if (stats.IsDead && state != EnemyState.Dead)
            Die();

        return finalDamage;
    }

    private void TryStartEnemyAttack()
    {
        if (IsStunned)
            return;

        float aps = Mathf.Max(0f, AttacksPerSecond);
        if (aps <= 0f)
            return;

        if (Time.time < _nextEnemyAttackTime)
            return;

        float cooldown = 1f / Mathf.Max(0.01f, aps);
        _nextEnemyAttackTime = Time.time + cooldown;

        float windup = Mathf.Max(0f, enemyAttackWindup);
        _hitTime = Time.time + windup;
        _hitQueued = true;
        _queuedHitCommitted = true;

        SetTriggerSafe(attackTrigger);

        _provoked = true;
        SetMoving(false);
    }

    private void ApplyEnemyHitToThreat(float damageMultiplier = 1f)
    {
        if (IsRetaliationMinionValidAlive())
        {
            ApplyEnemyHitToMinion(damageMultiplier);
            return;
        }

        ApplyEnemyHitToPlayer(damageMultiplier);
    }

    private void ApplyEnemyHitToMinion(float damageMultiplier = 1f)
    {
        if (stats == null || !_retaliationMinionTarget)
            return;

        MinionCombatTarget mct = _retaliationMinionTarget.GetComponent<MinionCombatTarget>();
        if (!mct || !mct.IsAlive)
        {
            _retaliationMinionTarget = null;
            return;
        }

        SplitDamage hit = stats.RollSplitAttackDamage(out bool wasCrit);
        float totalMult = Mathf.Max(0f, damageMultiplier);
        float neurotoxinMult = _ailments != null ? _ailments.GetOutgoingDamageMultiplier() : 1f;
        if (neurotoxinMult < 0.999f)
            totalMult *= neurotoxinMult;

        if (totalMult < 0.999f || totalMult > 1.001f)
        {
            hit.physical *= totalMult;
            hit.magic *= totalMult;
            hit.corruptionDamage *= totalMult;
        }

        if (hit.IsEmpty)
            return;

        mct.TakeDamageFromEnemy(hit, wasCrit, transform, this);
    }

    private void ApplyEnemyHitToPlayer(float damageMultiplier = 1f)
    {
        if (_playerController == null || stats == null)
            return;

        SplitDamage hit = stats.RollSplitAttackDamage(out bool wasCrit);
        bool forcePoison = false;
        EnsureAbilityController();
        _abilityController?.TryRollToxicFangsOnAttack(ref hit, out forcePoison);

        float totalMult = Mathf.Max(0f, damageMultiplier);
        float neurotoxinMult = _ailments != null ? _ailments.GetOutgoingDamageMultiplier() : 1f;
        if (neurotoxinMult < 0.999f)
            totalMult *= neurotoxinMult;

        if (totalMult < 0.999f || totalMult > 1.001f)
        {
            hit.physical *= totalMult;
            hit.magic *= totalMult;
            hit.corruptionDamage *= totalMult;
        }

        if (_playerController != null)
        {
            PlayerCombatController combat = _playerController.GetComponent<PlayerCombatController>();
            CharacterStats playerStats = _playerController.GetComponent<CharacterStats>();
            bool riposteParry = playerStats != null && playerStats.GetParryEnhancementPick() == 0;
            bool reservedBlock = false;
            if (playerStats != null && hit.physical > 0f)
                reservedBlock = playerStats.TryReserveNextIncomingPhysicalBlock();

            if (combat != null && (!reservedBlock || riposteParry))
                combat.TryProcessParryOnEnemyHit(this, ref hit, wasCrit);
        }

        if (hit.IsEmpty)
            return;

        bool dealtAnyDamage = false;

        if (hit.physical > 0f)
        {
            _playerController.TakeDamage(hit.physical, DamageType.Physical, transform, wasCrit);
            dealtAnyDamage = true;
        }

        if (hit.magic > 0f)
        {
            _playerController.TakeDamage(hit.magic, DamageType.Magic, transform, wasCrit);
            dealtAnyDamage = true;
        }

        if (hit.corruptionDamage > 0f)
        {
            _playerController.TakeDamage(hit.corruptionDamage, DamageType.Corruption, transform, wasCrit);
            dealtAnyDamage = true;
        }

        if (dealtAnyDamage)
            ApplyAilmentsToPlayer(hit, forcePoison);
    }

    private void ApplyAilmentsToPlayer(SplitDamage hit, bool forcePoison = false)
    {
        if (player == null || stats == null)
            return;

        CharacterStats playerStatsEarly = _playerController != null
            ? _playerController.GetComponent<CharacterStats>()
            : null;
        if (playerStatsEarly != null && playerStatsEarly.IsWayOfTheSlayerCrowdControlImmune())
            return;

        AilmentController targetAilments = player.GetComponent<AilmentController>();
        if (targetAilments == null)
            targetAilments = player.GetComponentInChildren<AilmentController>();

        if (targetAilments == null)
            return;

        // Bleed
        CharacterStats playerStats = _playerController != null
            ? _playerController.GetComponent<CharacterStats>()
            : null;
        float bleedChance = playerStats != null
            ? playerStats.GetIncomingBleedChanceFromEnemies(stats.BleedChance)
            : stats.BleedChance;

        if (bleedChance > 0f && stats.BleedBaseTotalDamage > 0f)
        {
            if (UnityEngine.Random.value < bleedChance)
            {
                BleedPayload bleed = new BleedPayload
                {
                    totalDamage = stats.BleedBaseTotalDamage,
                    ticks = stats.BleedTicks,
                    source = transform
                };

                targetAilments.ApplyBleedFromHit(bleed);
            }
        }

        // Poison
        if (stats.PoisonPerStackTotalDamage > 0f && (forcePoison || stats.PoisonChance > 0f))
        {
            if (forcePoison || UnityEngine.Random.value < stats.PoisonChance)
            {
                PoisonPayload poison = new PoisonPayload
                {
                    totalDamage = stats.PoisonPerStackTotalDamage,
                    ticks = stats.PoisonTicks,
                    maxStacks = stats.PoisonMaxStacks,
                    source = transform
                };

                targetAilments.ApplyPoisonFromHit(poison);
            }
        }

        // Fire burn (enemy): any fire damage on the hit; chance from EnemyDefinition burnChance → MagicAilmentApplyChance.
        bool isFireHit =
            stats.CurrentMagicAttackType == MagicAttackType.Fire &&
            hit.Total > 0f;

        if (isFireHit)
        {
            targetAilments.TryApplyBurnFromFireHit(
                hit.Total,
                stats.BurnApplyChance,
                stats.BurnExplosionMultiplier,
                transform);
            return;
        }

        if (hit.magic <= 0f || stats.MagicAilmentApplyChance <= 0f)
            return;

        if (UnityEngine.Random.value > stats.MagicAilmentApplyChance)
            return;

        switch (stats.CurrentMagicAttackType)
        {
            case MagicAttackType.Ice:
                targetAilments.ApplyChillFromHit(new ChillPayload(
                    duration: stats.ChillDuration,
                    maxStacks: stats.ChillMaxStacks,
                    slowPerStack: stats.ChillSlowPerStack,
                    source: transform
                ));
                break;

            case MagicAttackType.Lightning:
            default:
                targetAilments.ApplyShockFromHit(new ShockPayload(
                    duration: stats.ShockDuration,
                    damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
                    source: transform
                ));
                break;
        }
    }

    /// <summary>World spawn position + drift for floating damage on this enemy (direct hits and DoT ticks share this).</summary>
    private void GetDamagePopupSpawnForDealer(Transform dealer, Vector3? dealerWorldIfTransformMissing, out Vector3 worldPos, out Vector3 driftDir)
    {
        worldPos = damagePopupAnchor ? damagePopupAnchor.WorldPos : transform.position;
        driftDir = Vector3.up;

        Vector3 dealerPos = default;
        bool haveDealer = false;

        if (dealer != null)
        {
            dealerPos = dealer.position;
            _damagePopupDealerLastWorldPos = dealerPos;
            _damagePopupDealerLastWorldPosValid = true;
            haveDealer = true;
        }
        else if (dealerWorldIfTransformMissing.HasValue)
        {
            dealerPos = dealerWorldIfTransformMissing.Value;
            haveDealer = true;
        }
        else if (_damagePopupDealerLastWorldPosValid)
        {
            dealerPos = _damagePopupDealerLastWorldPos;
            haveDealer = true;
        }

        if (!haveDealer)
            return;

        float dirX = Mathf.Sign(dealerPos.x - transform.position.x);
        if (dirX == 0f)
            dirX = 1f;
        worldPos.x += dirX * 0.25f;
        driftDir = DamagePopupSystem.GetDriftDirectionForVictim(transform, dealerPos);
    }

    private void GetStatusPopupSpawnBehindDealer(Transform dealer, out Vector3 worldPos)
    {
        Vector3 anchorPos = damagePopupAnchor ? damagePopupAnchor.WorldPos : transform.position;
        Vector3 dealerPos = dealer != null
            ? dealer.position
            : _damagePopupDealerLastWorldPosValid
                ? _damagePopupDealerLastWorldPos
                : anchorPos;

        worldPos = DamagePopupSystem.GetWorldPosBehindVictim(anchorPos, dealerPos);
    }

    public int TakeDamage(
        int amount,
        DamageType type,
        bool wasCrit,
        Transform attacker,
        AttackSkill? attackSkillSource = null,
        DpsDamageBucket? dpsBucketOverride = null,
        float armorRatingMultiplier = 1f,
        float magicResistRatingMultiplier = 1f,
        string outgoingDpsSourceLabel = null)
    {
        if (state == EnemyState.Dead || stats == null)
            return 0;

        TryTriggerMapWideAggroFromAttacker(attacker);
        _provoked = true;
        StampDamageEngagementFromAttacker(attacker);
        ResolveIncomingAggroFromAttacker(attacker);

        if (IsImmuneToIncomingHit(attackSkillSource))
        {
            ShowImmunePopup(attacker);
            return 0;
        }

        float shockMult = _ailments != null ? _ailments.GetIncomingDamageMultiplier() : 1f;
        float scaledAmount = Mathf.Max(0f, amount * shockMult);

        if (wasCrit && scaledAmount > 0f)
        {
            EnemyShadowStrikeMarks shadowMarks = GetComponent<EnemyShadowStrikeMarks>();
            if (shadowMarks != null)
                scaledAmount *= shadowMarks.TryConsumeLethalCritDamageMultiplier();
        }

        float applied = stats.TakeDamage(
            scaledAmount,
            type,
            out bool blocked,
            out float hpDamage,
            out _,
            armorRatingMultiplier,
            magicResistRatingMultiplier);
        int finalDamage = Mathf.RoundToInt(applied);

        if (blocked)
            wasCrit = false;

        AwardCombatXpToSource(attacker, finalDamage, dpsBucketOverride ?? ToDpsBucket(type), outgoingDpsSourceLabel);

        OnDamaged?.Invoke(finalDamage, wasCrit);

        ResolvePlayer();
        BeginAbilityCombatIfNeeded();
        _abilityController?.NotifyDamaged(attackSkillSource, GetPlayerMeleeThreatDistanceX());

        if (DamagePopupSystem.Instance != null && ToggleSettingsStore.Get(ToggleSettingId.ShowOutgoingDamageNumbers))
        {
            Vector3 pos;
            Vector3 dir;
            if (blocked)
            {
                GetStatusPopupSpawnBehindDealer(attacker, out pos);
                dir = Vector3.up;
            }
            else
                GetDamagePopupSpawnForDealer(attacker, null, out pos, out dir);

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
                finalDamage,
                popupKind,
                wasCrit,
                false,
                dir,
                blocked
            );
        }

        if (hpDamage > 0.001f && !blocked && !stats.IsDead)
            SetTriggerSafe(hurtTrigger);

        if (stats.IsDead && state != EnemyState.Dead)
            Die();

        return finalDamage;
    }

    public void ApplyDirectDotDamage(
        int finalDamage,
        Transform source,
        FloatingDamageTextUI.PopupDamageKind popupKind,
        bool showPopup,
        Vector3? dotDealerWorldPositionFallback = null,
        string outgoingDpsSourceLabel = null,
        bool outgoingAttributeToMinion = false,
        bool wasCrit = false)
    {
        if (state == EnemyState.Dead || stats == null)
            return;

        finalDamage = Mathf.Max(1, finalDamage);

        TryTriggerMapWideAggroFromAttacker(source);
        _provoked = true;
        StampDamageEngagementFromAttacker(source);
        ResolveIncomingAggroFromAttacker(source);

        // DOT tick amount is already final; do not re-apply armor/MR/corruption resist.
        float applied = stats.TakeDamageFromResolvedDot(finalDamage, out _);
        int dealt = Mathf.RoundToInt(applied);

        DpsDamageBucket bucket = ToDpsBucket(popupKind);
        if (outgoingAttributeToMinion)
            bucket = DpsDamageBucket.Minion;

        AwardCombatXpToSource(source, dealt, bucket, outgoingDpsSourceLabel);

        OnDamaged?.Invoke(dealt, false);

        if (showPopup && DamagePopupSystem.Instance != null
            && ToggleSettingsStore.Get(ToggleSettingId.ShowOutgoingDamageNumbers))
        {
            GetDamagePopupSpawnForDealer(source, dotDealerWorldPositionFallback, out Vector3 pos, out Vector3 dir);

            DamagePopupSystem.Instance.Spawn(
                pos,
                dealt,
                popupKind,
                wasCrit,
                true,
                dir,
                false
            );
        }

        if (stats.IsDead && state != EnemyState.Dead)
            Die();
    }

    private void HandleStatsHPChanged(float current, float max)
    {
        OnHealthChanged?.Invoke(Mathf.RoundToInt(current), Mathf.RoundToInt(max));
    }

    private void HandleStatsDied()
    {
        if (state != EnemyState.Dead)
            Die();
    }

    private void Die()
    {
        if (state == EnemyState.Dead) return;

        EnemyShadowStrikeMarks shadowMarks = GetComponent<EnemyShadowStrikeMarks>();
        shadowMarks?.NotifyEnemyDied();

        _provoked = false;
        state = EnemyState.Dead;
        _hitQueued = false;
        _queuedHitCommitted = false;

        CombatEnemyRegistry.Unregister(this);

        ClearEngagement();
        StopHorizontal();
        SetMoving(false);

        if (_countedAlive)
        {
            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);
            _countedAlive = false;
        }

        OnDeath?.Invoke();

        string lootSourceName = ResolveLootSourceName();
        if (!string.IsNullOrWhiteSpace(lootSourceName))
            SessionTrackerData.EnsureInstance().RegisterEnemyKill(lootSourceName);

        string eid = EnemyId;
        QuestProgressManager.Instance?.NotifyEnemyKilledForActiveMap(eid);

        PlayerAbilityController.NotifyBattleTranceKillFromEnemyDeath();

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance;
        if (wmp != null)
        {
            string activeNodeId = ActiveLevelContext.Current != null ? ActiveLevelContext.Current.nodeId : "";
            if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
                activeNodeId = GameplayLevelBootstrapper.Instance.ActiveDefinition.nodeId ?? activeNodeId;
            wmp.NotifyEnemyKilledOnNode(activeNodeId, 1);
        }

        TryDropGold();
        TryDropLoot();
        TryDropMapCombatScalingSpecialLoot();

        if (_rb) _rb.simulated = false;

        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        if (animator)
        {
            ResetTriggerSafe(attackTrigger);
            ResetTriggerSafe(hurtTrigger);
            SetTriggerSafe(dieTrigger);

            if (!string.IsNullOrWhiteSpace(dieStateName))
                animator.Play(dieStateName, 0, 0f);

            SetBoolSafe(movingBool, false);
        }
        else
        {
            Debug.LogWarning($"[Enemy] No animator on {name}. Destroying without death anim.", this);
        }

        if (destroyOnDeath)
            StartCoroutine(DestroyAfterDeathAnim());
    }

    private IEnumerator DestroyAfterDeathAnim()
    {
        float wait = GetDieClipLength();
        yield return new WaitForSeconds(Mathf.Max(0.05f, wait));
        Destroy(gameObject);
    }

    private float GetDieClipLength()
    {
        if (dieClipLengthOverride > 0f)
            return dieClipLengthOverride;

        if (animator && animator.runtimeAnimatorController != null)
        {
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    if (!c) continue;

                    if (c.name.Equals(dieStateName, StringComparison.OrdinalIgnoreCase) ||
                        c.name.IndexOf(dieStateName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return c.length;
                    }
                }
            }
        }

        return 0.8f;
    }

    private bool IsPlayerValidAlive()
    {
        if (!player) return false;
        if (!player.gameObject.activeInHierarchy) return false;

        if (!_playerController) return true;
        return !_playerController.IsDead;
    }

    private void SetMoving(bool moving)
    {
        SetBoolSafe(movingBool, moving);
    }

    private void SetTriggerSafe(string triggerName)
    {
        if (!animator || string.IsNullOrWhiteSpace(triggerName))
            return;

        if (!HasAnimatorParameter(animator, triggerName, AnimatorControllerParameterType.Trigger))
            return;

        animator.SetTrigger(triggerName);
    }

    private void ResetTriggerSafe(string triggerName)
    {
        if (!animator || string.IsNullOrWhiteSpace(triggerName))
            return;

        if (!HasAnimatorParameter(animator, triggerName, AnimatorControllerParameterType.Trigger))
            return;

        animator.ResetTrigger(triggerName);
    }

    private void SetBoolSafe(string boolName, bool value)
    {
        if (!animator || string.IsNullOrWhiteSpace(boolName))
            return;

        if (!HasAnimatorParameter(animator, boolName, AnimatorControllerParameterType.Bool))
            return;

        animator.SetBool(boolName, value);
    }

    private static bool HasAnimatorParameter(Animator a, string paramName, AnimatorControllerParameterType expectedType)
    {
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].type == expectedType && ps[i].name == paramName)
                return true;
        }

        return false;
    }

    private void UpdateEngagement(float distX)
    {
        bool nowEngaged = distX <= (AttackRange + engagePadding);
        if (nowEngaged == _engaged) return;

        _engaged = nowEngaged;

        if (_playerCombatState)
            _playerCombatState.SetEngaged(this, _engaged);
    }

    private void ClearEngagement()
    {
        if (!_engaged) return;

        _engaged = false;

        if (_playerCombatState)
            _playerCombatState.SetEngaged(this, false);
    }

    public void FaceTargetWorldX(float targetX) => FaceTargetX(targetX);

    public Transform GetVisualsRootTransform() => visualsRoot != null ? visualsRoot : transform;

    public void ApplyAbilityEnrage(float scaleMultiplier, float attackSpeedMultiplier, float moveSpeedMultiplier)
    {
        if (_abilityEnrageApplied || state == EnemyState.Dead)
            return;

        _abilityEnrageApplied = true;

        float scale = Mathf.Max(1f, scaleMultiplier);
        if (visualsRoot != null)
            visualsRoot.localScale *= scale;
        else
            transform.localScale *= scale;

        moveSpeed = Mathf.Max(0f, moveSpeed * Mathf.Max(1f, moveSpeedMultiplier));
        stats?.MultiplyEnemyUnarmedAttacksPerSecond(Mathf.Max(1f, attackSpeedMultiplier));
        TrySpawnEnrageStatusPopup();
    }

    private void TrySpawnEnrageStatusPopup()
    {
        if (DamagePopupSystem.Instance == null)
            return;

        DamagePopupAnchor anchor = GetComponentInChildren<DamagePopupAnchor>(true);
        Vector3 pos = anchor != null ? anchor.WorldPos : transform.position;
        Color color = new Color32(255, 72, 48, 255);
        FloatingDamageTextUI prefab = DamagePopupSystem.Instance.PopupPrefab;
        if (prefab != null)
            color = prefab.BurnPresentationColor;

        DamagePopupSystem.Instance.SpawnLingeringStatus(pos, "ENRAGED", color, transform);
    }

    public void TryApplyAbilityShockwaveDamageToPlayer(
        Vector3 impactWorld,
        float radius,
        float directHitRadius = 0f,
        float directHitDamageMultiplier = 1f)
    {
        if (_playerController == null || stats == null || state == EnemyState.Dead)
            return;

        float shockwaveRadius = Mathf.Max(0.1f, radius);
        float dx = Mathf.Abs(_playerController.transform.position.x - impactWorld.x);
        if (dx > shockwaveRadius)
            return;

        float damageMultiplier = 1f;
        float directRadius = Mathf.Max(0f, directHitRadius);
        if (directRadius > 0f && dx <= directRadius)
            damageMultiplier = Mathf.Max(1f, directHitDamageMultiplier);

        ApplyEnemyHitToPlayer(damageMultiplier);
    }

    private bool IsAbilityMovementLocked() =>
        _abilityController != null && _abilityController.IsMovementLocked;

    private void BeginAbilityCombatIfNeeded()
    {
        if (_abilityCombatActive)
            return;

        EnsureAbilityController();
        if (!_abilityController)
            return;

        _abilityCombatActive = true;
        _abilityController.NotifyCombatActive(player, _playerController, _rb);
    }

    private void EndAbilityCombat()
    {
        if (!_abilityCombatActive)
            return;

        _abilityCombatActive = false;
        _abilityController?.NotifyCombatEnded();
    }

    private void FaceTargetX(float targetX)
    {
        if (!visualsRoot) return;

        float myX = transform.position.x;
        bool faceLeft = targetX < myX;

        bool flip = faceLeft;
        if (invertFlip) flip = !flip;

        Vector3 s = visualsRoot.localScale;
        float abs = Mathf.Abs(s.x);
        s.x = flip ? -abs : abs;
        visualsRoot.localScale = s;

        ApplyUIUnflip(s.x);
    }

    private void ApplyEliteVisualPresentation()
    {
        if (visualsRoot != null)
            visualsRoot.localScale *= EliteVisualScale;
        else
            transform.localScale *= EliteVisualScale;

        Transform tintRoot = visualsRoot != null ? visualsRoot : transform;
        SpriteRenderer[] srs = tintRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (!sr)
                continue;
            Color c = sr.color;
            c.r *= EliteSpriteColorTint.r;
            c.g *= EliteSpriteColorTint.g;
            c.b *= EliteSpriteColorTint.b;
            sr.color = c;
        }
    }

    private void ApplyUIUnflip(float visualsScaleX)
    {
        if (uiRoot)
        {
            Vector3 us = uiRoot.localScale;
            float abs = Mathf.Abs(us.x);
            // UIRoot should not mirror with the body flip.
            us.x = abs;
            uiRoot.localScale = us;
            return;
        }

        if (nameLabel)
        {
            Vector3 ns = nameLabel.localScale;
            float abs = Mathf.Abs(ns.x);
            ns.x = abs;
            nameLabel.localScale = ns;
        }

        if (hpBar)
        {
            Vector3 hs = hpBar.localScale;
            float abs = Mathf.Abs(hs.x);
            hs.x = abs;
            hpBar.localScale = hs;
        }
    }

    private void TryDropGold()
    {
        const bool fallbackDropGold = true;
        const float fallbackGoldDropChance = 1f;
        const int fallbackGoldMin = 1;
        const int fallbackGoldMax = 5;
        Vector3 fallbackGoldPopupWorldOffset = new Vector3(0f, 1.2f, 0f);

        bool useDef = definition != null;
        bool shouldDrop = useDef ? definition.dropGold : fallbackDropGold;
        if (!shouldDrop) return;

        float chance = useDef ? definition.goldDropChance : fallbackGoldDropChance;
        if (chance < 1f && UnityEngine.Random.value > chance)
            return;

        int min = useDef ? definition.goldMin : fallbackGoldMin;
        int max = useDef ? definition.goldMax : fallbackGoldMax;
        Vector3 offset = useDef ? definition.goldPopupWorldOffset : fallbackGoldPopupWorldOffset;

        int gmin = Mathf.Max(0, min);
        int gmax = Mathf.Max(gmin, max);
        int baseAmount = UnityEngine.Random.Range(gmin, gmax + 1);
        int amount = baseAmount;

        if (_mapScalingGoldMultiplier > 1.0001f)
            amount = Mathf.Max(0, Mathf.RoundToInt(baseAmount * _mapScalingGoldMultiplier));

        if (_mapEnhancementGoldBonusFraction > 0.001f)
            amount = Mathf.Max(0, amount + Mathf.RoundToInt(baseAmount * _mapEnhancementGoldBonusFraction));

        if (_isElite)
        {
            if (useDef)
                amount = Mathf.Max(0, Mathf.RoundToInt(amount * Mathf.Max(1f, definition.eliteGoldMultiplier)));
            else
                amount = Mathf.Max(0, amount * 2);
        }

        if (amount <= 0) return;

        if (_wallet == null || _goldPopupSpawner == null)
            ResolvePlayer();

        if (_wallet != null)
            _wallet.AddGold(amount);

        if (_goldPopupSpawner != null)
            _goldPopupSpawner.ShowGoldGainedAtWorld(transform.position + offset, amount);
    }

    private void TryDropLoot()
    {
        if (definition == null)
            return;

        float mapLootMul = Mathf.Max(1f, _mapScalingLootChanceMultiplier);
        float eliteChanceMul = (_isElite ? Mathf.Max(0f, definition.eliteLootChanceMultiplier) : 1f) * mapLootMul;

        bool singlePick = definition.lootAtMostOneDropPerTable;

        switch (definition.eliteLootHandling)
        {
            case EnemyEliteLootHandling.ScaleBaseLootChances:
                RollEnemyLootTable(definition.loot, eliteChanceMul, singlePick);
                if (_isElite)
                    RollEnemyLootTable(definition.eliteLoot, mapLootMul, singlePick);
                break;
            case EnemyEliteLootHandling.EliteLootTableOnly:
                if (_isElite)
                    RollEnemyLootTable(definition.eliteLoot, mapLootMul, singlePick);
                else
                    RollEnemyLootTable(definition.loot, mapLootMul, singlePick);
                break;
            case EnemyEliteLootHandling.ScaledBasePlusExtraEliteEntries:
                RollEnemyLootTable(definition.loot, eliteChanceMul, singlePick);
                if (_isElite)
                    RollEnemyLootTable(definition.eliteLoot, mapLootMul, singlePick);
                break;
        }
    }

    private void RollEnemyLootTable(List<EnemyLootEntry> entries, float dropChanceMultiplier, bool atMostOneDropFromSuccessfulRolls)
    {
        if (entries == null || entries.Count == 0)
            return;

        DropManager dm = DropManager.Instance != null
            ? DropManager.Instance
            : FindFirstObjectByType<DropManager>(FindObjectsInactive.Include);
        if (dm == null)
        {
            Debug.LogWarning($"[Enemy] No DropManager in scene — item loot from '{name}' was not spawned.", this);
            return;
        }

        Transform anchor = ResolveDropLootAnchor();
        Vector3 spawnBase = anchor ? anchor.position : transform.position;

        if (atMostOneDropFromSuccessfulRolls)
        {
            int winnerIndex = -1;
            int successCount = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                EnemyLootEntry e = entries[i];
                if (e?.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                    continue;

                float p = ComputeEffectiveLootDropChance(e.dropChance, dropChanceMultiplier);
                if (p <= 0f)
                    continue;
                if (p < 1f && UnityEngine.Random.value > p)
                    continue;

                successCount++;
                if (UnityEngine.Random.Range(0, successCount) == 0)
                    winnerIndex = i;
            }

            if (winnerIndex < 0)
                return;

            EnemyLootEntry won = entries[winnerIndex];
            int amtMin = Mathf.Max(1, won.amountMin);
            int amtMax = Mathf.Max(amtMin, won.amountMax);
            int stack = UnityEngine.Random.Range(amtMin, amtMax + 1);
            if (stack <= 0)
                return;

            // Match player-drop behavior: align to ground so loot doesn't hover if the anchor is above the floor.
            dm.SpawnAtWorldPosition(won.item.itemId.Trim(), stack, won.item.icon, spawnBase, alignToGround: true, sourceName: ResolveLootSourceName());
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            EnemyLootEntry e = entries[i];
            if (e?.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;

            float p = ComputeEffectiveLootDropChance(e.dropChance, dropChanceMultiplier);
            if (p <= 0f)
                continue;
            if (p < 1f && UnityEngine.Random.value > p)
                continue;

            int amtMin = Mathf.Max(1, e.amountMin);
            int amtMax = Mathf.Max(amtMin, e.amountMax);
            int stack = UnityEngine.Random.Range(amtMin, amtMax + 1);
            if (stack <= 0)
                continue;

            // Match player-drop behavior: align to ground so loot doesn't hover if the anchor is above the floor.
            dm.SpawnAtWorldPosition(e.item.itemId.Trim(), stack, e.item.icon, spawnBase, alignToGround: true, sourceName: ResolveLootSourceName());
        }
    }

    private float ComputeEffectiveLootDropChance(float baseChance, float dropChanceMultiplier)
    {
        float scaled = baseChance * dropChanceMultiplier;
        float enhanced = !_isElite && _mapEnhancementLootBonusFraction > 0.001f
            ? baseChance * _mapEnhancementLootBonusFraction
            : 0f;
        return Mathf.Clamp01(scaled + enhanced);
    }

    private string ResolveLootSourceName()
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.displayName))
            return null;

        string core = definition.displayName.Trim();
        // Elites get their own row in the session tracker so the player can see how often elite loot rolls land.
        return _isElite ? $"Elite {core}" : core;
    }

    private Transform ResolveDropLootAnchor()
    {
        if (dropLootAnchor != null)
            return dropLootAnchor;

        var all = GetComponentsInChildren<Transform>(true);
        for (int j = 0; j < all.Length; j++)
        {
            Transform t = all[j];
            if (t != null && t.name == "DropAnchor")
            {
                dropLootAnchor = t;
                return dropLootAnchor;
            }
        }

        return null;
    }

    private void AwardCombatXpToSource(
        Transform source,
        float damageDealt,
        DpsDamageBucket bucket,
        string outgoingDpsSourceLabel = null)
    {
        if (source == null || damageDealt <= 0f)
            return;

        PlayerCombatController combat = ResolvePlayerCombatFromDamageSource(source);

        bool grantXp = definition == null || definition.grantCombatXp;
        if (combat != null)
        {
            float xpDamage = damageDealt * (_isElite ? 2f : 1f);
            combat.AwardCombatXp(xpDamage, bucket, grantXp, outgoingDpsSourceLabel, _mapScalingXpRateMultiplier);
        }
    }

    private static PlayerCombatController ResolvePlayerCombatFromDamageSource(Transform source)
    {
        if (!source)
            return null;

        PlayerCombatController combat = source.GetComponent<PlayerCombatController>();
        if (combat != null)
            return combat;

        combat = source.GetComponentInParent<PlayerCombatController>();
        if (combat != null)
            return combat;

        MinionCombatTarget mct = source.GetComponent<MinionCombatTarget>();
        if (!mct)
            mct = source.GetComponentInParent<MinionCombatTarget>();
        return mct != null ? mct.OwnerCombat : null;
    }

    private void StampDamageEngagementFromAttacker(Transform attacker)
    {
        if (!attacker)
            return;

        if (EnemyAggro.IsDirectPlayerAttacker(attacker))
        {
            _playerDamagedThisEnemy = true;
            return;
        }

        MinionCombatTarget minionAttacker = EnemyAggro.GetMinionCombatTargetFrom(attacker);
        if (minionAttacker != null && minionAttacker.IsAlive)
            _minionDamagedThisEnemy = true;
    }

    private void ResolveIncomingAggroFromAttacker(Transform attacker)
    {
        if (!attacker)
            return;

        MinionCombatTarget minionAttacker = EnemyAggro.GetMinionCombatTargetFrom(attacker);

        EnemyAggro.ResolveIncomingHitAggro(
            attacker,
            _playerDamagedThisEnemy,
            _minionDamagedThisEnemy,
            IsRetaliationMinionValidAlive(),
            IsMinionActivelyStrikingThisEnemy(_retaliationMinionTarget),
            minionAttacker,
            ref _retaliationMinionTarget);
    }

    private bool IsMinionActivelyStrikingThisEnemy(Transform minionTransform)
    {
        if (!minionTransform)
            return false;

        SoulforgedWarriorMinion warrior = minionTransform.GetComponent<SoulforgedWarriorMinion>();
        if (!warrior)
            warrior = minionTransform.GetComponentInParent<SoulforgedWarriorMinion>();
        if (warrior)
            return warrior.CurrentTarget == this;

        MinionCombatController combat = minionTransform.GetComponent<MinionCombatController>();
        if (!combat)
            combat = minionTransform.GetComponentInParent<MinionCombatController>();
        return combat != null && combat.CurrentTarget == this;
    }

    private void TryDropMapCombatScalingSpecialLoot()
    {
        // Fixed per-entry chances — never multiplied by map scaling loot bonuses or gear drop-rate modifiers.
        MapNodeDefinition node = MapCombatScaling.ResolveActiveCombatMapNode();
        if (node == null || !node.IsMapCombatScalingEnabled())
            return;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        string nodeId = node.nodeId;
        int sliderTier = !string.IsNullOrWhiteSpace(nodeId) && progress != null
            ? progress.GetCombatMapScalingSelectedTier(nodeId)
            : 0;
        if (sliderTier <= 0)
            return;

        var individualEntries = new List<MapScalingSpecialLootEntry>();
        var groupRolls = new List<MapScalingSpecialLootGroupRoll>();
        node.CollectMapSpecificSpecialDrops(sliderTier, individualEntries);
        if (node.IsMapCombatScalingEnabled())
        {
            MapCombatScalingSpecialDropDefaults.CollectIndividualDropsForScalingLevel(sliderTier, individualEntries);
            MapCombatScalingSpecialDropDefaults.CollectGroupRollsForScalingLevel(sliderTier, groupRolls);
        }

        if (individualEntries.Count == 0 && groupRolls.Count == 0)
            return;

        DropManager dm = DropManager.Instance != null
            ? DropManager.Instance
            : FindFirstObjectByType<DropManager>(FindObjectsInactive.Include);
        if (dm == null)
            return;

        ItemDatabase itemDb = Resources.Load<ItemDatabase>("Databases/ItemDatabase");

        Transform anchor = ResolveDropLootAnchor();
        Vector3 spawnBase = anchor ? anchor.position : transform.position;
        string sourceName = ResolveLootSourceName();

        for (int i = 0; i < individualEntries.Count; i++)
            TrySpawnMapScalingSpecialDrop(individualEntries[i], node, itemDb, dm, spawnBase, sourceName);

        for (int i = 0; i < groupRolls.Count; i++)
        {
            MapScalingSpecialLootGroupRoll groupRoll = groupRolls[i];
            if (groupRoll?.itemPool == null || groupRoll.itemPool.Count == 0)
                continue;

            float p = Mathf.Clamp01(groupRoll.dropChance);
            if (p <= 0f)
                continue;
            if (p < 1f && UnityEngine.Random.value > p)
                continue;

            ItemDefinition picked = groupRoll.RollRandomItem();
            if (picked == null)
                continue;

            var entry = new MapScalingSpecialLootEntry
            {
                item = picked,
                dropChance = 1f,
                amountMin = 1,
                amountMax = 1,
            };
            TrySpawnMapScalingSpecialDrop(entry, node, itemDb, dm, spawnBase, sourceName);
        }
    }

    private static void TrySpawnMapScalingSpecialDrop(
        MapScalingSpecialLootEntry entry,
        MapNodeDefinition node,
        ItemDatabase itemDb,
        DropManager dm,
        Vector3 spawnBase,
        string sourceName)
    {
        if (entry?.item == null || string.IsNullOrWhiteSpace(entry.item.itemId) || dm == null)
            return;

        float p = Mathf.Clamp01(entry.dropChance);
        if (p <= 0f)
            return;
        if (p < 1f && UnityEngine.Random.value > p)
            return;

        int amtMin = Mathf.Max(1, entry.amountMin);
        int amtMax = Mathf.Max(amtMin, entry.amountMax);
        int stack = UnityEngine.Random.Range(amtMin, amtMax + 1);
        if (stack <= 0)
            return;

        string dropItemId = MapEnhancementService.ResolveLootDropItemId(entry.item.itemId.Trim(), node, itemDb);
        ItemDefinition dropDef = itemDb != null ? itemDb.Get(dropItemId) : entry.item;
        Sprite dropIcon = dropDef != null && dropDef.icon != null ? dropDef.icon : entry.item.icon;

        dm.SpawnAtWorldPosition(dropItemId, stack, dropIcon, spawnBase, alignToGround: true, sourceName: sourceName);
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

    private static DpsDamageBucket ToDpsBucket(FloatingDamageTextUI.PopupDamageKind kind)
    {
        return kind switch
        {
            FloatingDamageTextUI.PopupDamageKind.Bleed => DpsDamageBucket.Bleed,
            FloatingDamageTextUI.PopupDamageKind.Poison => DpsDamageBucket.Poison,
            FloatingDamageTextUI.PopupDamageKind.Magic => DpsDamageBucket.Burn,
            FloatingDamageTextUI.PopupDamageKind.Corruption => DpsDamageBucket.Corruption,
            _ => DpsDamageBucket.Physical
        };
    }

    private static void TryTriggerMapWideAggroFromAttacker(Transform attacker)
    {
        if (!IsFromPlayerTeam(attacker))
            return;

        MapNodeDefinition def = ActiveLevelContext.Current;
        if (def == null && GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        if (def == null || def.enemyAggroMode != LevelEnemyAggroMode.CalmUntilPlayerAggressive)
            return;

        LevelAggroState.TriggerAggression(def, attacker);
    }

    private static bool IsFromPlayerTeam(Transform attacker)
    {
        if (attacker == null)
            return false;

        if (attacker.CompareTag("Player"))
            return true;
        if (attacker.GetComponent<PlayerController>() != null || attacker.GetComponentInParent<PlayerController>() != null)
            return true;
        if (attacker.GetComponent<PlayerCombatController>() != null || attacker.GetComponentInParent<PlayerCombatController>() != null)
            return true;
        if (attacker.GetComponent<PlayerAbilityController>() != null || attacker.GetComponentInParent<PlayerAbilityController>() != null)
            return true;
        if (attacker.GetComponent<SoulforgedWeaponMinion>() != null || attacker.GetComponentInParent<SoulforgedWeaponMinion>() != null)
            return true;
        if (attacker.GetComponent<SoulforgedWarriorMinion>() != null || attacker.GetComponentInParent<SoulforgedWarriorMinion>() != null)
            return true;
        if (attacker.GetComponent<MinionCombatTarget>() != null || attacker.GetComponentInParent<MinionCombatTarget>() != null)
            return true;

        return false;
    }

    private bool IsImmuneToIncomingHit(AttackSkill? attackSkillSource)
    {
        if (definition == null || attackSkillSource == null)
            return false;

        return attackSkillSource.Value switch
        {
            AttackSkill.Melee => definition.immuneToMeleeDamage,
            AttackSkill.Ranged => definition.immuneToRangedDamage,
            AttackSkill.Magic => definition.immuneToMagicDamage,
            _ => false
        };
    }

    private void ShowImmunePopup(Transform attacker)
    {
        if (DamagePopupSystem.Instance == null)
            return;

        GetStatusPopupSpawnBehindDealer(attacker, out Vector3 pos);
        DamagePopupSystem.Instance.Spawn(
            pos,
            0,
            FloatingDamageTextUI.PopupDamageKind.Immune,
            false,
            false,
            Vector3.up,
            false
        );
    }

    private void TryApplyDeadlyCloseRangeEffect()
    {
        if (definition == null || !definition.deadlyAtCloseRange || definition.deadlyCloseRangeTyplessDamage <= 0f)
            return;
        if (_playerController == null || Time.time < _nextDeadlyCloseRangePulseTime)
            return;

        float triggerDistance = Mathf.Max(0.1f, definition.deadlyCloseRangeDistance);
        if (DistanceToPlayerX() > triggerDistance)
            return;

        _nextDeadlyCloseRangePulseTime = Time.time + DeadlyCloseRangePulseIntervalSeconds;
        _playerController.TakeDamage(definition.deadlyCloseRangeTyplessDamage, DamageType.Typless, transform, false);
    }

    private void HandleAggroPulseTriggered(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || state == EnemyState.Dead)
            return;

        MapNodeDefinition def = ActiveLevelContext.Current;
        if (def == null && GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        if (def == null || def.enemyAggroMode != LevelEnemyAggroMode.CalmUntilPlayerAggressive)
            return;

        string activeNodeId = string.IsNullOrWhiteSpace(def.nodeId) ? string.Empty : def.nodeId.Trim();
        if (!string.Equals(activeNodeId, nodeId.Trim(), StringComparison.Ordinal))
            return;
        // Mark this enemy as "player aggression triggered on this map" so it can
        // switch to normal range-based aggro without forcing hard aggro at any distance.
        _mapAggroTriggeredForSession = true;

        if (_playerDamagedThisEnemy || _minionDamagedThisEnemy)
            return;

        if (TryGetMapAggroMinionThreat(out Transform instigator))
            _retaliationMinionTarget = instigator;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats ? stats.Range : 0f);
    }
}