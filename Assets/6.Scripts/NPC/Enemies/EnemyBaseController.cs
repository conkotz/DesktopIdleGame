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

    [Header("Gold drop (legacy)")]
    [Tooltip("Used only when Enemy Definition is not assigned on this prefab. Otherwise gold comes from EnemyDefinition.")]
    [SerializeField] private bool dropGold = true;
    [SerializeField] private int goldMin = 1;
    [SerializeField] private int goldMax = 5;
    [SerializeField, Range(0f, 1f)] private float goldDropChance = 1f;
    [SerializeField] private Vector3 goldPopupWorldOffset = new Vector3(0f, 1.2f, 0f);

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

    private bool _countedAlive;
    private bool _provoked;
    private bool _mapAggroTriggeredForSession;
    private bool _engaged;
    private bool _isChasingForRange;
    private bool _isElite;
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

    public bool IsDead => state == EnemyState.Dead;
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

        if (!stats)
            stats = GetComponent<CharacterStats>();

        if (!stats)
        {
            Debug.LogError($"[Enemy] {name} is missing CharacterStats.", this);
            return;
        }

        stats.ApplyEnemyDefinition(def);
        if (spawnAsElite)
            stats.ApplyEliteEnemyScaling();

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
    }

    private void OnDisable()
    {
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

        // Same vitals regen as the player (life/energy/mana); base stats apply to enemies too.
        if (stats)
            stats.TickRegen(Time.deltaTime);

        if (!IsPlayerValidAlive())
        {
            _hitQueued = false;
            _provoked = false;
            ClearEngagement();
            state = EnemyState.Idle;
            SetMoving(false);
            return;
        }

        float dist = DistanceToPlayerX();
        bool shouldAggro = ResolveShouldAggro(dist);

        if (!shouldAggro)
        {
            _hitQueued = false;
            _queuedHitCommitted = false;
            ClearEngagement();
            state = EnemyState.Idle;
            TickIdleWanderPhaseIfNeeded();
            SetMoving(ShouldShowIdleWanderMoving());
            return;
        }

        // Special close-range pulses should only happen after the enemy is actively aggroed.
        TryApplyDeadlyCloseRangeEffect();

        UpdateEngagement(dist);

        if (state != EnemyState.Dead)
            FaceTargetX(player.position.x);

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

            if (!IsPlayerValidAlive()) return;

            // If the attack windup already started, treat the hit as committed.
            if (committedHit || !requireRangeOnHit || DistanceToPlayerX() <= AttackRange)
                ApplyEnemyHitToPlayer();
        }
    }

    private void FixedUpdate()
    {
        if (state == EnemyState.Dead || !_rb)
            return;

        if (!IsPlayerValidAlive())
        {
            StopHorizontal();
            EnforceWorldBoundsX();
            return;
        }

        float dist = DistanceToPlayerX();
        bool shouldAggro = ResolveShouldAggro(dist);

        bool shouldChaseForRange = ResolveShouldChaseForRange(dist);
        if (shouldAggro && shouldChaseForRange)
        {
            float dx = player.position.x - transform.position.x;
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

    private bool ResolveShouldAggro(float distanceToPlayerX)
    {
        MapNodeDefinition def = GetActiveMapNodeDefinition();
        LevelEnemyAggroMode mode = def != null ? def.enemyAggroMode : LevelEnemyAggroMode.Aggressive;
        bool playerTriggeredWaveAggro = def != null && LevelAggroState.IsWaveAggroLatched(def);
        bool playerTriggeredMapAggro = playerTriggeredWaveAggro || _mapAggroTriggeredForSession;
        bool inEnemyAggroRange = distanceToPlayerX <= aggroRange;
        bool ignoreRange = LevelIgnoresAggroRange(def);

        if (ignoreRange)
        {
            return mode switch
            {
                LevelEnemyAggroMode.Aggressive => true,
                // Same latch/provoke rules as ranged aggro; distance gate removed once triggered.
                LevelEnemyAggroMode.CalmUntilPlayerAggressive => _provoked || playerTriggeredMapAggro,
                _ => _provoked
            };
        }

        return mode switch
        {
            LevelEnemyAggroMode.Aggressive => inEnemyAggroRange,

            // Calm until player aggression: always retaliates when personally provoked (damaged),
            // and after map trigger also aggroes by this enemy's own aggroRange.
            LevelEnemyAggroMode.CalmUntilPlayerAggressive => _provoked || (playerTriggeredMapAggro && inEnemyAggroRange),

            // Calm mode remains retaliation-only.
            _ => _provoked
        };
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

    private void TryStartEnemyAttack()
    {
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

    private void ApplyEnemyHitToPlayer()
    {
        if (_playerController == null || stats == null)
            return;

        SplitDamage hit = stats.RollSplitAttackDamage(out bool wasCrit);

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
            ApplyAilmentsToPlayer(hit);
    }

    private void ApplyAilmentsToPlayer(SplitDamage hit)
    {
        if (player == null || stats == null)
            return;

        AilmentController targetAilments = player.GetComponent<AilmentController>();
        if (targetAilments == null)
            targetAilments = player.GetComponentInChildren<AilmentController>();

        if (targetAilments == null)
            return;

        // Bleed
        if (stats.BleedChance > 0f && stats.BleedBaseTotalDamage > 0f)
        {
            if (UnityEngine.Random.value < stats.BleedChance)
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
        if (stats.PoisonChance > 0f && stats.PoisonPerStackTotalDamage > 0f)
        {
            if (UnityEngine.Random.value < stats.PoisonChance)
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

    public int TakeDamage(int amount, DamageType type, bool wasCrit, Transform attacker, AttackSkill? attackSkillSource = null, DpsDamageBucket? dpsBucketOverride = null)
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

        float shockMult = _ailments != null ? _ailments.GetIncomingDamageMultiplier() : 1f;
        float scaledAmount = Mathf.Max(0f, amount * shockMult);
        float applied = stats.TakeDamage(scaledAmount, type, out bool blocked, out float hpDamage);
        int finalDamage = Mathf.RoundToInt(applied);

        if (blocked)
            wasCrit = false;

        AwardCombatXpToSource(attacker, finalDamage, dpsBucketOverride ?? ToDpsBucket(type));

        OnDamaged?.Invoke(finalDamage, wasCrit);

        if (DamagePopupSystem.Instance != null)
        {
            Vector3 pos = damagePopupAnchor ? damagePopupAnchor.WorldPos : transform.position;

            // Bias popups toward the impact side (attacker side) so they don't feel "behind" when facing flips.
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

    public void ApplyDirectDotDamage(int finalDamage, Transform source, FloatingDamageTextUI.PopupDamageKind popupKind, bool showPopup)
    {
        if (state == EnemyState.Dead || stats == null)
            return;

        finalDamage = Mathf.Max(1, finalDamage);

        TryTriggerMapWideAggroFromAttacker(source);
        _provoked = true;

        // DOT tick amount is already final; do not re-apply armor/MR/corruption resist.
        float applied = stats.TakeDamageFromResolvedDot(finalDamage, out _);
        int dealt = Mathf.RoundToInt(applied);

        AwardCombatXpToSource(source, dealt, ToDpsBucket(popupKind));

        OnDamaged?.Invoke(dealt, false);

        if (showPopup && DamagePopupSystem.Instance != null)
        {
            Vector3 pos = damagePopupAnchor ? damagePopupAnchor.WorldPos : transform.position;

            if (source)
            {
                float dirX = Mathf.Sign(source.position.x - transform.position.x); // toward source
                if (dirX == 0f) dirX = 1f;
                pos.x += dirX * 0.25f;
            }

            Vector3 dir = source
                ? (transform.position - source.position).normalized
                : Vector3.up;

            DamagePopupSystem.Instance.Spawn(
                pos,
                dealt,
                popupKind,
                false,
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

        _provoked = false;
        state = EnemyState.Dead;
        _hitQueued = false;
        _queuedHitCommitted = false;

        ClearEngagement();
        StopHorizontal();
        SetMoving(false);

        if (_countedAlive)
        {
            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);
            _countedAlive = false;
        }

        OnDeath?.Invoke();

        string eid = EnemyId;
        if (QuestProgressManager.Instance != null)
            QuestProgressManager.Instance.NotifyEnemyKilledForActiveMap(eid);
        else
        {
            QuestProgressManager mgr = FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
            if (mgr)
                mgr.NotifyEnemyKilledForActiveMap(eid);
        }

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (wmp != null)
        {
            string activeNodeId = ActiveLevelContext.Current != null ? ActiveLevelContext.Current.nodeId : "";
            if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
                activeNodeId = GameplayLevelBootstrapper.Instance.ActiveDefinition.nodeId ?? activeNodeId;
            wmp.NotifyEnemyKilledOnNode(activeNodeId, 1);
        }

        TryDropGold();
        TryDropLoot();

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
        bool useDef = definition != null;
        bool shouldDrop = useDef ? definition.dropGold : dropGold;
        if (!shouldDrop) return;

        float chance = useDef ? definition.goldDropChance : goldDropChance;
        if (chance < 1f && UnityEngine.Random.value > chance)
            return;

        int min = useDef ? definition.goldMin : goldMin;
        int max = useDef ? definition.goldMax : goldMax;
        Vector3 offset = useDef ? definition.goldPopupWorldOffset : goldPopupWorldOffset;

        int gmin = Mathf.Max(0, min);
        int gmax = Mathf.Max(gmin, max);
        int amount = UnityEngine.Random.Range(gmin, gmax + 1);

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

        float eliteChanceMul = _isElite ? Mathf.Max(0f, definition.eliteLootChanceMultiplier) : 1f;

        bool singlePick = definition.lootAtMostOneDropPerTable;

        switch (definition.eliteLootHandling)
        {
            case EnemyEliteLootHandling.ScaleBaseLootChances:
                RollEnemyLootTable(definition.loot, eliteChanceMul, singlePick);
                if (_isElite)
                    RollEnemyLootTable(definition.eliteLoot, 1f, singlePick);
                break;
            case EnemyEliteLootHandling.EliteLootTableOnly:
                if (_isElite)
                    RollEnemyLootTable(definition.eliteLoot, 1f, singlePick);
                else
                    RollEnemyLootTable(definition.loot, 1f, singlePick);
                break;
            case EnemyEliteLootHandling.ScaledBasePlusExtraEliteEntries:
                RollEnemyLootTable(definition.loot, eliteChanceMul, singlePick);
                if (_isElite)
                    RollEnemyLootTable(definition.eliteLoot, 1f, singlePick);
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

                float p = Mathf.Clamp01(e.dropChance * dropChanceMultiplier);
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
            dm.SpawnAtWorldPosition(won.item.itemId.Trim(), stack, won.item.icon, spawnBase, alignToGround: true);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            EnemyLootEntry e = entries[i];
            if (e?.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;

            float p = Mathf.Clamp01(e.dropChance * dropChanceMultiplier);
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
            dm.SpawnAtWorldPosition(e.item.itemId.Trim(), stack, e.item.icon, spawnBase, alignToGround: true);
        }
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

    private void AwardCombatXpToSource(Transform source, float damageDealt, DpsDamageBucket bucket)
    {
        if (source == null || damageDealt <= 0f)
            return;

        var combat = source.GetComponent<PlayerCombatController>();
        if (combat == null)
            combat = source.GetComponentInParent<PlayerCombatController>();

        bool grantXp = definition == null || definition.grantCombatXp;
        if (combat != null)
            combat.AwardCombatXp(damageDealt * (_isElite ? 2f : 1f), bucket, grantXp);
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

        LevelAggroState.TriggerPlayerAggression(def);
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

        Vector3 pos = damagePopupAnchor ? damagePopupAnchor.WorldPos : transform.position;
        if (attacker)
        {
            float dirX = Mathf.Sign(attacker.position.x - transform.position.x);
            if (dirX == 0f) dirX = 1f;
            pos.x += dirX * 0.25f;
        }

        Vector3 dir = attacker ? (transform.position - attacker.position).normalized : Vector3.up;
        DamagePopupSystem.Instance.Spawn(
            pos,
            0,
            FloatingDamageTextUI.PopupDamageKind.Immune,
            false,
            false,
            dir,
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