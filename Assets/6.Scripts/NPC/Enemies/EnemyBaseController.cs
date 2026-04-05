using System;
using System.Collections;
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

    private bool _idleWanderInMovePhase = true;
    private float _idleWanderPhaseEndTime;
    private float _idleWanderDirSign = 1f;
    private int _idleWanderPhaseTickFrame = -1;

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

    [Header("Gold Drop")]
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

    private bool _countedAlive;
    private bool _provoked;
    private bool _engaged;

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
        OnNameChanged?.Invoke(displayName);
    }

    /// <summary>
    /// Applies identity and combat values from <paramref name="def"/>; behaviour and visuals stay on the prefab.
    /// Call immediately after spawning if you assign the definition from code (after Awake has run without a definition).
    /// </summary>
    public void InitializeFromDefinition(EnemyDefinition def)
    {
        if (def == null)
        {
            Debug.LogWarning($"[Enemy] InitializeFromDefinition(null) on '{name}' — keeping prefab stats.", this);
            return;
        }

        definition = def;

        if (!stats)
            stats = GetComponent<CharacterStats>();

        if (!stats)
        {
            Debug.LogError($"[Enemy] {name} is missing CharacterStats.", this);
            return;
        }

        stats.ApplyEnemyDefinition(def);

        string dn = string.IsNullOrWhiteSpace(def.displayName) ? "Enemy" : def.displayName.Trim();
        SetDisplayName(dn);

        moveSpeed = Mathf.Max(0f, def.moveSpeed);

        idleWanderEnabled = def.idleWanderEnabled;
        idleWanderSpeed = Mathf.Max(0f, def.idleWanderSpeed);
        idleWanderMoveMinSec = Mathf.Max(0.05f, def.idleWanderMoveMinSec);
        idleWanderMoveMaxSec = Mathf.Max(idleWanderMoveMinSec, def.idleWanderMoveMaxSec);
        idleWanderIdleMinSec = Mathf.Max(0.05f, def.idleWanderIdleMinSec);
        idleWanderIdleMaxSec = Mathf.Max(idleWanderIdleMinSec, def.idleWanderIdleMaxSec);
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
    }

    private void OnEnable()
    {
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
        ClearEngagement();
    }

    private void Start()
    {
        if (!stats) return;

        stats.RefreshVitalsFromStats(fillIfEmpty: true);

        OnHealthChanged?.Invoke(HP, MaxHP);
        SetMoving(false);
        ResetIdleWanderCycle();
    }

    private void ResetIdleWanderCycle()
    {
        if (!idleWanderEnabled || idleWanderSpeed <= 0f)
            return;

        // Start in the idle (stand still) phase so the first move burst happens after a pause.
        _idleWanderInMovePhase = false;
        _idleWanderPhaseEndTime = Time.time +
            UnityEngine.Random.Range(idleWanderIdleMinSec, idleWanderIdleMaxSec);
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
        if (EnemyWanderBounds.Instance == null ||
            !EnemyWanderBounds.Instance.TryGetWorldXBounds(out _, out _))
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
        if (EnemyWanderBounds.Instance == null ||
            !EnemyWanderBounds.Instance.TryGetWorldXBounds(out _, out _))
            return false;
        return _idleWanderInMovePhase;
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
        bool useDistanceAggro = LevelUsesDistanceAggro();
        bool shouldAggro = LevelIgnoresAggroRange() || _provoked || (useDistanceAggro && dist <= aggroRange);

        if (!shouldAggro)
        {
            _hitQueued = false;
            ClearEngagement();
            state = EnemyState.Idle;
            TickIdleWanderPhaseIfNeeded();
            SetMoving(ShouldShowIdleWanderMoving());
            return;
        }

        UpdateEngagement(dist);

        if (state != EnemyState.Dead)
            FaceTargetX(player.position.x);

        if (dist <= AttackRange)
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

            if (!IsPlayerValidAlive()) return;

            if (!requireRangeOnHit || DistanceToPlayerX() <= AttackRange)
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
            return;
        }

        float dist = DistanceToPlayerX();
        bool useDistanceAggro = LevelUsesDistanceAggro();
        bool shouldAggro = LevelIgnoresAggroRange() || _provoked || (useDistanceAggro && dist <= aggroRange);

        if (shouldAggro && dist > AttackRange)
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
    }

    private bool TryApplyIdleWanderMovement()
    {
        TickIdleWanderPhaseIfNeeded();

        if (!idleWanderEnabled || idleWanderSpeed <= 0f)
            return false;

        EnemyWanderBounds wb = EnemyWanderBounds.Instance;
        if (wb == null || !wb.TryGetWorldXBounds(out float minX, out float maxX))
            return false;

        if (!_idleWanderInMovePhase)
            return false;

        float x = transform.position.x;
        if (x <= minX)
            _idleWanderDirSign = 1f;
        else if (x >= maxX)
            _idleWanderDirSign = -1f;

        FaceTargetX(transform.position.x + _idleWanderDirSign * 100f);

        float ailmentMoveMult = _ailments != null ? _ailments.GetMoveSpeedMultiplier() : 1f;
        float spd = Mathf.Max(0f, idleWanderSpeed * ailmentMoveMult);
        float yVel = _rb.linearVelocity.y;
        _rb.linearVelocity = new Vector2(_idleWanderDirSign * spd, xOnly ? yVel : _rb.linearVelocity.y);
        return true;
    }

    private void StopHorizontal()
    {
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    /// <summary>
    /// When false (calm level), proximity does not trigger aggro — only <see cref="_provoked"/> (e.g. after taking damage).
    /// </summary>
    private static bool LevelUsesDistanceAggro()
    {
        MapNodeDefinition def = ActiveLevelContext.Current;
        if (def == null && GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        if (def == null)
            return true;
        return def.enemyAggroMode == LevelEnemyAggroMode.Aggressive;
    }

    /// <summary>
    /// When the active <see cref="MapNodeDefinition"/> sets <see cref="MapNodeDefinition.ignoreAggroRange"/>,
    /// enemies always engage the player (skips proximity check). Overrides Calm for that level.
    /// </summary>
    private static bool LevelIgnoresAggroRange()
    {
        MapNodeDefinition def = ActiveLevelContext.Current;
        if (def == null && GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        return def != null && def.ignoreAggroRange;
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

        if (hit.magical > 0f)
        {
            _playerController.TakeDamage(hit.magical, DamageType.Magical, transform, wasCrit);
            dealtAnyDamage = true;
        }

        if (hit.trueDamage > 0f)
        {
            _playerController.TakeDamage(hit.trueDamage, DamageType.True, transform, wasCrit);
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

        // Elemental magic ailments (shared CharacterStats tuning for both enemies and player).
        bool isFireMagicHit =
            stats.CurrentMagicAttackType == MagicAttackType.Fire &&
            hit.magical > 0f;

        if (targetAilments.HasBurn && !isFireMagicHit)
            targetAilments.ClearBurn();

        if (isFireMagicHit)
        {
            if (targetAilments.HasBurn)
            {
                targetAilments.ApplyBurnFollowUpFireHit(hit.magical, stats.BurnExplosionMultiplier, transform);
                return;
            }

            if (stats.MagicAilmentApplyChance > 0f && UnityEngine.Random.value <= stats.MagicAilmentApplyChance)
                targetAilments.StartBurnFromFireHit(hit.magical, stats.BurnHitsToExplode, stats.BurnExplosionMultiplier);

            return;
        }

        if (hit.magical <= 0f || stats.MagicAilmentApplyChance <= 0f)
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

    public int TakeDamage(int amount, DamageType type, bool wasCrit, Transform attacker)
    {
        if (state == EnemyState.Dead || stats == null)
            return 0;

        _provoked = true;

        float shockMult = _ailments != null ? _ailments.GetIncomingDamageMultiplier() : 1f;
        float scaledAmount = Mathf.Max(0f, amount * shockMult);
        float applied = stats.TakeDamage(scaledAmount, type, out bool blocked);
        int finalDamage = Mathf.RoundToInt(applied);      

        if (blocked)
            wasCrit = false;

        AwardCombatXpToSource(attacker, finalDamage);

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
                DamageType.Magical => FloatingDamageTextUI.PopupDamageKind.Magical,
                DamageType.True => FloatingDamageTextUI.PopupDamageKind.True,
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

        if (finalDamage > 0 && !blocked && !stats.IsDead)
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

        _provoked = true;

        // DOT damage passed here is already the final resolved amount,
        // so apply it as True damage to bypass extra mitigation.
        float applied = stats.TakeDamage(finalDamage, DamageType.True, out _);
        int dealt = Mathf.RoundToInt(applied);

        AwardCombatXpToSource(source, dealt);

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

        ClearEngagement();
        StopHorizontal();
        SetMoving(false);

        if (_countedAlive)
        {
            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);
            _countedAlive = false;
        }

        OnDeath?.Invoke();
        TryDropGold();

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
        if (!dropGold) return;

        if (goldDropChance < 1f && UnityEngine.Random.value > goldDropChance)
            return;

        int min = Mathf.Max(0, goldMin);
        int max = Mathf.Max(min, goldMax);
        int amount = UnityEngine.Random.Range(min, max + 1);

        if (amount <= 0) return;

        if (_wallet == null || _goldPopupSpawner == null)
            ResolvePlayer();

        if (_wallet != null)
            _wallet.AddGold(amount);

        if (_goldPopupSpawner != null)
            _goldPopupSpawner.ShowGoldGainedAtWorld(transform.position + goldPopupWorldOffset, amount);
    }

    private void AwardCombatXpToSource(Transform source, float damageDealt)
    {
        if (source == null || damageDealt <= 0f)
            return;

        var combat = source.GetComponent<PlayerCombatController>();
        if (combat == null)
            combat = source.GetComponentInParent<PlayerCombatController>();

        if (combat != null)
            combat.AwardCombatXp(damageDealt);
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