using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent]
public class PlayerCombatController : MonoBehaviour, ISaveable
{
    public readonly struct IncomingDealerDamageEntry
    {
        public readonly string dealerName;
        public readonly float totalDamage;

        public IncomingDealerDamageEntry(string dealerName, float totalDamage)
        {
            this.dealerName = dealerName;
            this.totalDamage = totalDamage;
        }
    }

    public readonly struct OutgoingDamageSourceEntry
    {
        public readonly string sourceName;
        public readonly float totalDamage;

        public OutgoingDamageSourceEntry(string sourceName, float totalDamage)
        {
            this.sourceName = sourceName;
            this.totalDamage = totalDamage;
        }
    }

    /// <summary>How a basic-attack swing's dealt damage is split across outgoing DPS sources.</summary>
    public readonly struct SwingOutgoingAttribution
    {
        public readonly string primarySource;
        public readonly string bonusSource;
        public readonly float bonusFraction;

        public SwingOutgoingAttribution(string primarySource, string bonusSource, float bonusFraction)
        {
            this.primarySource = string.IsNullOrWhiteSpace(primarySource) ? "Auto Attack" : primarySource.Trim();
            this.bonusSource = string.IsNullOrWhiteSpace(bonusSource) ? null : bonusSource.Trim();
            this.bonusFraction = Mathf.Clamp01(bonusFraction);
        }

        public bool HasBonus =>
            bonusFraction > 1e-6f && !string.IsNullOrWhiteSpace(bonusSource);

        /// <summary>Entire swing damage (e.g. Power Slash) is credited to <see cref="bonusSource"/> only.</summary>
        public bool AttributesEntireSwingToBonusSource =>
            HasBonus && bonusFraction >= 0.999f;

        public static SwingOutgoingAttribution AutoAttackOnly =>
            new SwingOutgoingAttribution("Auto Attack", null, 0f);
    }

    /// <summary>TakeDamage records XP only; swing outgoing DPS is applied after the full hit resolves.</summary>
    public const string DeferredSwingOutgoingDpsLabel = "__deferred_swing_outgoing__";

    private struct DamageSample
    {
        public float time;
        public float amount;
    }
    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CharacterStats stats;

    [Header("Optional Colliders (for edge-to-edge range)")]
    [SerializeField] private Collider2D playerCol;
    private Collider2D _targetColCached;

    [Header("Targeting")]
    [SerializeField] private bool clearTargetIfDead = true;

    [Header("Idle combat targeting")]
    [Tooltip("When idle combat is enabled and a closer enemy appears, allow switching targets mid-route to reduce walking past new spawns.")]
    [SerializeField] private bool idleAllowRetargetToCloserEnemy = true;
    [Tooltip("Minimum X-distance improvement (world units) required before switching targets to avoid thrashing.")]
    [SerializeField, Min(0f)] private float idleRetargetCloserByAtLeastUnits = 0.35f;

    [Header("Attack")]
    [Tooltip("Extra padding so range doesn't feel pixel-perfect.")]
    [SerializeField] private float rangePadding = 0.05f;

    [Tooltip("Stops micro-corrections at range edge (prevents tiny walk jitter).")]
    [SerializeField] private float stopSlack = 0.04f;

    [Tooltip("If false, player will not attack while gathering.")]
    [SerializeField] private bool allowAttackingWhileGathering = false;

    [Tooltip("If true, we keep moving to stay exactly at range edge. If false, we just move into range and stop.")]
    [SerializeField] private bool kiteAtRangeEdge = false;

    [Tooltip("If true, face the current target when in range (before swinging).")]
    [SerializeField] private bool faceTargetWhenAttacking = true;

    [Header("Ranged Projectile Visuals")]
    [SerializeField] private ProjectileVisual rangedProjectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField, Min(0.01f)] private float rangedProjectileSpeed = 12f;
    [SerializeField] private float rangedProjectileRotationOffset = 0f;
    [Tooltip("Delay from attack start to projectile release (animation sync).")]
    [SerializeField, Min(0f)] private float rangedProjectileFireDelay = 0f;
    [SerializeField, Min(0f)] private float rangedDamageDelayOffset = 0f;

    [Header("Magic Projectile Visuals")]
    [SerializeField] private MonoBehaviour magicProjectilePrefab;
    [SerializeField] private MonoBehaviour magicLightningProjectilePrefab;
    [SerializeField] private MonoBehaviour magicFireProjectilePrefab;
    [SerializeField] private MonoBehaviour magicIceProjectilePrefab;
    [SerializeField] private Transform magicProjectileSpawnPoint;
    [SerializeField, Min(0.01f)] private float magicProjectileSpeed = 14f;
    [SerializeField] private float magicProjectileRotationOffset = 0f;
    [Tooltip("Delay from attack start to magic bolt release (animation sync).")]
    [SerializeField, Min(0f)] private float magicProjectileFireDelay = 0f;

    [Tooltip("Melee only: seconds after the attack anim starts before damage resolves (syncs hit to the downward swing). Ranged/magic unchanged.")]
    [SerializeField, Min(0f)] private float meleeHitImpactDelay = 0.12f;

    [Header("Idle Combat (Auto Target)")]
    [SerializeField] private bool idleCombatEnabled = false;

    [Tooltip("How often to rescan for a living enemy while idle (seconds). Closest by default; Longbow picks the furthest enemy first.")]
    [SerializeField] private float idleRescanInterval = 0.25f;

    [Tooltip("Longbow idle auto-battle: if the player was damaged by an enemy within this window, prefer that enemy when acquiring a new target (before furthest-in-range).")]
    [SerializeField, Min(0.1f)] private float longbowPrioritizeRecentAttackerSeconds = 2.5f;

    [Tooltip("While idle combat is on, every N seconds all dropped items on the scene begin vacuuming to the player and are picked up on contact.")]
    [SerializeField, Min(0.5f)] private float idleAutoPickupIntervalSeconds = 20f;
    private float _nextIdleAutoPickupTime;
    [Header("Retaliation")]
    [SerializeField] private bool retaliationEnabled = false;

    [Header("Auto Consumables")]
    [SerializeField] private ActionBarUI actionBar;
    [SerializeField] private PlayerConsumableController consumableController;
    [SerializeField] private PlayerAbilityController abilityController;
    [SerializeField] private Inventory inventory;

    [SerializeField] private float autoConsumeInterval = 0.2f;
    [SerializeField] private bool autoUseFood = true;
    [SerializeField] private bool autoUsePotions = true;
    [SerializeField] private float autoAbilityInterval = 0.1f;
    [SerializeField] private bool autoUseAbilities = true;

    private float _nextAutoConsumeTime;
    private float _nextAutoAbilityTime;

    [Header("Combat XP")]
    [SerializeField, Range(0f, 5f)]
    private float xpPerDamage = 0.1f;

    [SerializeField]
    private string combatXpSource = "Combat";

    /// <summary>Combat XP awarded per point of damage dealt (Melee / Ranged / Magic).</summary>
    public float XpPerDamage => Mathf.Max(0f, xpPerDamage);

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    [Header("DPS Tracking")]
    [SerializeField, Min(0.1f)] private float dpsResetOutOfCombatSeconds = 5f;

    public event System.Action<bool> OnIdleCombatChanged;
    public event System.Action<bool> OnRetaliationChanged;
    public event System.Action OnTargetChanged;

    public EnemyBaseController CurrentTarget => _target;
    public EnemyBaseController Target => _target;

    /// <summary>Live combat target for ability aim (not gated on camera visibility).</summary>
    public EnemyBaseController GetPrimaryEngagedEnemy()
    {
        if (_target != null && !_target.IsDead && _target.gameObject.activeInHierarchy)
            return _target;
        return null;
    }

    /// <summary>Closest living enemy within current weapon attack range (edge-to-edge).</summary>
    public EnemyBaseController FindClosestEnemyInAttackRange()
    {
        if (stats == null)
            return null;

        float myRange = Mathf.Max(0f, stats.Range) + rangePadding;
        float closeEnoughToSwing = myRange + stopSlack;
        float myX = transform.position.x;
        float myHalf = HalfWidthX(playerCol);

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            Collider2D enemyCol = enemy.GetComponent<Collider2D>();
            if (!enemyCol)
                enemyCol = enemy.GetComponentInChildren<Collider2D>();

            float enemyHalf = HalfWidthX(enemyCol);
            float gap = EdgeGapX(myX, enemy.transform.position.x, myHalf, enemyHalf);
            if (gap > closeEnoughToSwing)
                continue;

            float dist = Mathf.Abs(enemy.transform.position.x - myX);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    /// <summary>True when edge-to-edge gap to <paramref name="enemy"/> is within current weapon attack reach.</summary>
    public bool IsEnemyWithinAttackRange(EnemyBaseController enemy)
    {
        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy || stats == null)
            return false;

        float closeEnoughToSwing = Mathf.Max(0f, stats.Range) + rangePadding + stopSlack;
        float myX = transform.position.x;
        float myHalf = HalfWidthX(playerCol);

        Collider2D enemyCol = enemy.GetComponent<Collider2D>();
        if (!enemyCol)
            enemyCol = enemy.GetComponentInChildren<Collider2D>();

        float enemyHalf = HalfWidthX(enemyCol);
        float gap = EdgeGapX(myX, enemy.transform.position.x, myHalf, enemyHalf);
        return gap <= closeEnoughToSwing;
    }

    public bool IdleCombatEnabled => idleCombatEnabled;
    public bool RetaliationEnabled => retaliationEnabled;
    public float NextAttackTime => _nextAttackTime;

    private EnemyBaseController _target;
    private float _nextAttackTime;
    private float _nextIdleScanTime;
    private float _nextLowManaPopupTime;
    private float _nextSupportWeaponMismatchPopupTime;
    private bool _isClosingDistanceForAttack;
    private bool _attackBufferedFromRange;
    private float _combatSessionStartTime = -1f;
    private float _combatSessionDamageSum;
    private DpsDamageBreakdown _outgoingDamageSum;
    private DpsDamageBreakdown _incomingDamageSum;
    private bool _dpsTrackerPaused;
    private float _pausedDpsSessionDuration;
    private float _lastCombatActivityTime = -999f;
    private float _lastHpForCombatEngageTrack = -1f;
    private bool _dpsAutoResetEnabled = true;
    private readonly Dictionary<string, float> _incomingDamageByDealer = new Dictionary<string, float>();
    private readonly List<string> _incomingDealerOrder = new List<string>();
    private readonly Dictionary<string, float> _outgoingDamageBySource = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _outgoingSourceOrder = new List<string>();

    private readonly List<EnemyBaseController> _ailmentSpreadScratch = new List<EnemyBaseController>(16);

    private EnemyBaseController _lastEnemyThatDamagedPlayer;
    private float _lastEnemyThatDamagedPlayerTime = -999f;


    public float GetAttackCooldownSeconds()
    {
        if (stats == null) return 0f;
        return 1f / Mathf.Max(0.01f, stats.AttacksPerSecond);
    }

    /// <summary>Extra reach padding on melee strike / cleave checks (serialized on this component).</summary>
    public float GetMeleeRangePadding() => rangePadding;

    public float GetAttackCycleNormalized()
    {
        float cooldown = GetAttackCooldownSeconds();
        if (cooldown <= 0f)
            return 0f;

        float remaining = Mathf.Max(0f, _nextAttackTime - Time.time);
        float readyProgress = 1f - (remaining / cooldown);
        return Mathf.Clamp01(readyProgress);
    }

    /// <summary>Whether an ability may consume the current attack cycle right now.</summary>
    public bool CanConsumeAttackCycleNow()
    {
        return Time.time >= _nextAttackTime;
    }

    /// <summary>
    /// Reserves the next attack cycle for an ability-cast attack timing gate.
    /// Returns false if the normal attack timer is still cooling down.
    /// </summary>
    public bool TryConsumeAttackCycleForAbilityCast()
    {
        if (!CanConsumeAttackCycleNow())
            return false;
        if (stats == null)
            return false;

        float cooldown = 1f / Mathf.Max(0.01f, stats.AttacksPerSecond);
        _nextAttackTime = Time.time + cooldown;
        if (player != null)
            player.SetActionOverride(PlayerController.PlayerAction.Fighting);
        return true;
    }

    public float GetCurrentDps()
    {
        if (_combatSessionDamageSum <= 0f || !TryGetDpsSessionDuration(out float duration))
            return 0f;

        if (duration <= 0f)
            return 0f;

        return _combatSessionDamageSum / duration;
    }

    public DpsDamageBreakdown GetOutgoingDpsBreakdown()
    {
        if (!TryGetDpsSessionDuration(out float duration))
            return default;

        return _outgoingDamageSum.PerSecond(duration);
    }

    public DpsDamageBreakdown GetIncomingDpsBreakdown()
    {
        if (!TryGetDpsSessionDuration(out float duration))
            return default;

        return _incomingDamageSum.PerSecond(duration);
    }

    public float GetCurrentIncomingDps()
    {
        DpsDamageBreakdown incoming = GetIncomingDpsBreakdown();
        return incoming.Total;
    }

    /// <summary>
    /// Elapsed seconds since the current damage-tracking session began.
    /// Returns 0 until any outgoing/incoming damage has been recorded, and resets to 0 when tracker values are cleared.
    /// </summary>
    public float GetDamageSessionElapsedSeconds()
    {
        if (_combatSessionStartTime < 0f)
            return 0f;

        if (_combatSessionDamageSum <= 0f && _incomingDamageSum.Total <= 0f)
            return 0f;

        if (_dpsTrackerPaused)
            return Mathf.Max(0f, _pausedDpsSessionDuration);

        return Mathf.Max(0f, Time.time - _combatSessionStartTime);
    }

    public float GetOutgoingTotalDamage()
    {
        return Mathf.Max(0f, _combatSessionDamageSum);
    }

    public DpsDamageBreakdown GetOutgoingTotalDamageBreakdown()
    {
        return _outgoingDamageSum;
    }

    public float GetIncomingTotalDamage()
    {
        return Mathf.Max(0f, _incomingDamageSum.Total);
    }

    public DpsDamageBreakdown GetIncomingTotalDamageBreakdown()
    {
        return _incomingDamageSum;
    }

    public List<IncomingDealerDamageEntry> GetIncomingDamageByDealer()
    {
        List<IncomingDealerDamageEntry> entries = new List<IncomingDealerDamageEntry>(_incomingDealerOrder.Count);
        for (int i = 0; i < _incomingDealerOrder.Count; i++)
        {
            string key = _incomingDealerOrder[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!_incomingDamageByDealer.TryGetValue(key, out float total))
                continue;
            if (total <= 0f)
                continue;

            entries.Add(new IncomingDealerDamageEntry(key, total));
        }

        return entries;
    }

    /// <summary>Outgoing damage grouped by ability / attack source (Auto Attack, Power Slash, etc.).</summary>
    public List<OutgoingDamageSourceEntry> GetOutgoingDamageBySource()
    {
        var entries = new List<OutgoingDamageSourceEntry>(_outgoingSourceOrder.Count);
        for (int i = 0; i < _outgoingSourceOrder.Count; i++)
        {
            string key = _outgoingSourceOrder[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!_outgoingDamageBySource.TryGetValue(key, out float total))
                continue;
            if (total <= 0f)
                continue;

            entries.Add(new OutgoingDamageSourceEntry(key, total));
        }

        entries.Sort((a, b) => b.totalDamage.CompareTo(a.totalDamage));
        return entries;
    }

    /// <summary>When false, combat DPS session values are never auto-cleared out of combat.</summary>
    public void SetDpsAutoResetEnabled(bool enabled)
    {
        _dpsAutoResetEnabled = enabled;
    }

    /// <summary>Hard reset of current DPS/damage tracker values, even mid-combat.</summary>
    public void ResetDpsTrackerNow()
    {
        _dpsTrackerPaused = false;
        _pausedDpsSessionDuration = 0f;
        ResetDpsSession();
        _lastCombatActivityTime = Time.time;
    }

    private bool TryGetDpsSessionDuration(out float duration)
    {
        duration = 0f;
        if (_combatSessionStartTime < 0f)
            return false;

        if (_dpsTrackerPaused)
        {
            duration = Mathf.Max(0.001f, _pausedDpsSessionDuration);
            return duration > 0f;
        }

        if (!IsCombatEngaged())
            return false;

        duration = Mathf.Max(0.001f, Time.time - _combatSessionStartTime);
        return duration > 0f;
    }

    private void Awake()
    {
        if (!playerCol) playerCol = GetComponent<Collider2D>();
        TryResolveAutoConsumeRefs();
        if (idleCombatEnabled)
            _nextIdleAutoPickupTime = Time.time + idleAutoPickupIntervalSeconds;
    }

    private void OnEnable()
    {
        TryResolveAutoConsumeRefs();
        if (stats)
        {
            stats.OnHPChanged += HandlePlayerHpChangedForCombatEngage;
            stats.OnDied += PauseDpsTracker;
            _lastHpForCombatEngageTrack = stats.HP;
        }
    }

    private void OnDisable()
    {
        if (stats)
        {
            stats.OnHPChanged -= HandlePlayerHpChangedForCombatEngage;
            stats.OnDied -= PauseDpsTracker;
        }
    }

    private void HandlePlayerHpChangedForCombatEngage(float hp, float maxHp)
    {
        if (_lastHpForCombatEngageTrack >= 0f && hp < _lastHpForCombatEngageTrack - 0.0001f)
            MarkRecentCombatActivity();
        _lastHpForCombatEngageTrack = hp;
    }

    /// <summary>Bumps local engagement clock and player <see cref="PlayerCombatState"/> soft combat (dealt or took damage).</summary>
    private void MarkRecentCombatActivity()
    {
        if (_dpsTrackerPaused)
            return;

        float window = Mathf.Max(0.1f, dpsResetOutOfCombatSeconds);
        _lastCombatActivityTime = Time.time;
        if (player)
            player.NotifySoftCombatInteraction(window);
    }

    /// <summary>
    /// Guard absorbs and similar non-HP combat events should still refresh the DPS session / soft-combat clock.
    /// </summary>
    public void NotifyNonHpCombatInteraction()
    {
        MarkRecentCombatActivity();
    }

    private void Update()
    {
        if (!player || !stats) return;
        if (_dpsTrackerPaused) return;

        float window = Mathf.Max(0.1f, dpsResetOutOfCombatSeconds);

        if (IsProximityCombatEngaged())
            _lastCombatActivityTime = Time.time;

        if (IsCombatEngaged())
        {
            if (_combatSessionStartTime < 0f)
                _combatSessionStartTime = Time.time;
        }
        else if (_dpsAutoResetEnabled && Time.time - _lastCombatActivityTime >= window)
        {
            ResetDpsSession();
        }

        if (idleCombatEnabled)
        {
            TickAutoConsumables();
            TickAutoAbilities();
            TickIdleCombatTargeting();
            TickIdleAutoPickup();
        }

        if (_target == null) return;

        if (clearTargetIfDead && _target.IsDead)
        {
            _isClosingDistanceForAttack = false;
            _attackBufferedFromRange = false;
            ClearTargetInternal();

            if (player != null)
                player.ForceIdleAction();

            return;
        }

        if (!allowAttackingWhileGathering)
        {
            var a = player.CurrentAction;
            bool isGathering =
                (a == PlayerController.PlayerAction.Mining ||
                 a == PlayerController.PlayerAction.Woodcutting ||
                 a == PlayerController.PlayerAction.Fishing);

            if (isGathering)
                return;
        }

        if (PlayerAbilityController.BlocksCombatActions)
            return;

        if (IsSupportEquippedWithoutCompatibleMainWeapon(out string supportMismatchMessage))
        {
            player.ClearActionOverride();
            if (Time.time >= _nextSupportWeaponMismatchPopupTime)
            {
                player.ShowPopup(supportMismatchMessage);
                _nextSupportWeaponMismatchPopupTime = Time.time + 0.4f;
            }
            return;
        }

        if (stats.AttacksPerSecond <= 0f || stats.MaxDamage <= 0)
        {
            var mainDef = player != null && stats != null
                ? GetMainWeaponDefForPopup()
                : null;

            if (_target != null && mainDef != null && mainDef.RequiresOffhandSupport)
            {
                player.SendMessage(
                    "ShowPopup",
                    $"Requires {mainDef.RequiredSupportType} in offhand.",
                    SendMessageOptions.DontRequireReceiver
                );
            }

            return;
        }

        float myRange = Mathf.Max(0f, stats.Range) + rangePadding;

        float enemyX = _target.transform.position.x;
        float myX = transform.position.x;

        if (_targetColCached == null || _targetColCached.gameObject != _target.gameObject)
            _targetColCached = _target.GetComponent<Collider2D>();

        float myHalf = HalfWidthX(playerCol);
        float enemyHalf = HalfWidthX(_targetColCached);

        float gap = EdgeGapX(myX, enemyX, myHalf, enemyHalf);
        // Same outer band as shouldStartClosing so we don't get stuck "closing" forever when the gap
        // hovers just outside melee range (moving targets + float noise used to pin gap > myRange every frame).
        float closeEnoughToSwing = myRange + stopSlack;
        if (gap <= closeEnoughToSwing)
            _attackBufferedFromRange = true;

        float desiredCenterDist = myRange + myHalf + enemyHalf;
        float desiredX = (myX < enemyX) ? (enemyX - desiredCenterDist) : (enemyX + desiredCenterDist);
        bool shouldStartClosing = gap > closeEnoughToSwing;
        bool shouldKeepClosing = _isClosingDistanceForAttack && gap > closeEnoughToSwing;
        bool shouldCloseDistance = !_attackBufferedFromRange && (shouldStartClosing || shouldKeepClosing);

        if (shouldCloseDistance)
        {
            _isClosingDistanceForAttack = true;

            player.ClearActionOverride();
            player.MoveToPointX_Combat(desiredX);
            return;
        }

        _isClosingDistanceForAttack = false;
        if (kiteAtRangeEdge)
            player.MoveToPointX_Combat(desiredX);
        else
            player.StopMoveOnly();

        if (faceTargetWhenAttacking)
            player.FaceTargetX(enemyX);

        float cooldown = 1f / Mathf.Max(0.01f, stats.AttacksPerSecond);

        // Prioritize queued Crescent Slash over normal auto attack cadence.
        if (abilityController != null && abilityController.TryAutoReleaseQueuedCrescentSlashFromCadence())
            return;

        if (Time.time < _nextAttackTime)
        {
            return;
        }

        if (IsMagicAttack() && !TrySpendManaForMagicAttack())
        {
            _attackBufferedFromRange = false;
            player.ClearActionOverride();
            if (Time.time >= _nextLowManaPopupTime)
            {
                player.ShowPopup("Not enough mana.");
                _nextLowManaPopupTime = Time.time + 0.4f;
            }
            return;
        }

        _nextAttackTime = Time.time + cooldown;
        _attackBufferedFromRange = false;

        player.SetActionOverride(PlayerController.PlayerAction.Fighting);

        SplitDamage rolled = stats.RollSplitAttackDamage(out bool wasCrit);
        SplitDamage preQueuedModifier = rolled;

        if (abilityController != null)
            abilityController.TryConsumeQueuedAttackModifier(ref rolled);

        SwingOutgoingAttribution swingAttribution = abilityController != null
            ? abilityController.BuildSwingOutgoingAttribution(preQueuedModifier, rolled)
            : SwingOutgoingAttribution.AutoAttackOnly;

        if (rolled.IsEmpty)
        {
            _attackBufferedFromRange = false;
            player.ClearActionOverride();
            return;
        }

        player.TriggerAttackAnim();

        if (IsRangedAttack())
        {
            HandleRangedAttack(_target, rolled, wasCrit, swingAttribution);
        }
        else if (IsMagicAttack())
        {
            HandleMagicAttack(_target, rolled, wasCrit, swingAttribution);
        }
        else
        {
            float meleeDelay = Mathf.Max(0f, meleeHitImpactDelay);
            if (meleeDelay <= 0f)
                ResolveAttackHitNow(_target, rolled, wasCrit, swingAttribution);
            else
                StartCoroutine(ResolveAttackHitAfterDelay(_target, rolled, wasCrit, meleeDelay, swingAttribution));
        }
    }

    /// <summary>Target selected or an enemy is in proximity engage range (not soft combat from recent hits).</summary>
    private bool IsProximityCombatEngaged()
    {
        bool hasLiveTarget = _target != null && !_target.IsDead && _target.gameObject.activeInHierarchy;
        bool enemyNear = player != null && player.HasEnemyProximityEngagement;
        return hasLiveTarget || enemyNear;
    }

    /// <summary>Proximity engagement, or recent damage dealt or taken (within <see cref="dpsResetOutOfCombatSeconds"/>).</summary>
    private bool IsCombatEngaged()
    {
        if (IsProximityCombatEngaged())
            return true;
        return Time.time - _lastCombatActivityTime < Mathf.Max(0.1f, dpsResetOutOfCombatSeconds);
    }

    private void TickAutoConsumables()
    {
        TryResolveAutoConsumeRefs();

        if (Time.time < _nextAutoConsumeTime)
            return;

        _nextAutoConsumeTime = Time.time + Mathf.Max(0.05f, autoConsumeInterval);

        if (!idleCombatEnabled)
            return;

        if (!IsPlayerAlive())
            return;

        if (actionBar == null || consumableController == null || inventory == null)
            return;

        if (autoUseFood && TryAutoUseFood())
            return;

        if (autoUsePotions)
            TryAutoUsePotion();
    }

    private bool TryAutoUseFood()
    {
        float currentHp = GetCurrentHP();
        float maxHp = GetMaxHP();

        if (maxHp <= 0f || currentHp <= 0f)
            return false;

        float missingHp = maxHp - currentHp;

        foreach (var slot in actionBar.GetSlots())
        {
            if (slot == null)
                continue;

            var action = slot.AssignedAction;
            if (action == null || !action.IsAssigned || !action.IsItem)
                continue;

            if (slot.SlotType != ActionBarSlotType.Food &&
                slot.SlotType != ActionBarSlotType.Any)
                continue;

            var def = inventory.GetItemDef(action.id);
            if (!def || !def.IsConsumable || !def.IsFood)
                continue;

            if (def.HealAmount <= 0)
                continue;

            int count = consumableController.CountItem(action.id);
            if (count <= 0)
                continue;

            if (consumableController.IsOnCooldown(action.id, out _))
                continue;

            if (missingHp < (float)def.HealAmount)
                continue;

            return consumableController.TryUseItem(action.id);
        }

        return false;
    }

    private bool TryAutoUsePotion()
    {
        foreach (var slot in actionBar.GetSlots())
        {
            if (slot == null)
                continue;

            var action = slot.AssignedAction;
            if (action == null || !action.IsAssigned || !action.IsItem)
                continue;

            if (slot.SlotType != ActionBarSlotType.Potion &&
                slot.SlotType != ActionBarSlotType.Any)
                continue;

            var def = inventory.GetItemDef(action.id);
            if (!def || !def.IsConsumable || !def.IsPotion)
                continue;

            int count = consumableController.CountItem(action.id);
            if (count <= 0)
                continue;

            if (consumableController.IsOnCooldown(action.id, out _))
                continue;

            bool used = consumableController.TryUseItem(action.id);
            return used;
        }

        return false;
    }

    private bool IsPlayerAlive()
    {

        return player != null && !player.IsDead;
    }

    private void TickAutoAbilities()
    {
        TryResolveAutoConsumeRefs();

        if (Time.time < _nextAutoAbilityTime)
            return;

        _nextAutoAbilityTime = Time.time + Mathf.Max(0.05f, autoAbilityInterval);

        if (!idleCombatEnabled || !autoUseAbilities)
            return;

        if (!IsPlayerAlive())
            return;

        if (actionBar == null || abilityController == null)
            return;

        if (_target == null || _target.IsDead)
            return;

        var orderedSlots = actionBar
            .GetSlots()
            .Where(slot => slot != null)
            .OrderBy(slot => slot.SlotIndex);

        foreach (var slot in orderedSlots)
        {
            var action = slot.AssignedAction;
            if (action == null || !action.IsAssigned || !action.IsAbility)
                continue;

            if (!slot.CanAccept(action))
                continue;

            if (abilityController.TryUseAbility(
                    action.id,
                    showLockedFeedback: false,
                    allowSoulforgedRecastWhileActive: false,
                    requireCrescentSlashTargetInFacingLane: true,
                    requireWhirlwindTargetInRadius: true))
                break;
        }
    }

    private float GetCurrentHP()
    {
        return player != null ? player.HP : 0f;
    }

    private float GetMaxHP()
    {
        return player != null ? player.MaxHP : 0f;
    }


    private void TryConsumeOffHandSupportAmmo()
    {
        var equipment = GetComponent<EquipmentManager>();
        if (!equipment) return;

        var offDef = equipment.GetOffHandDef();
        if (!offDef || !offDef.IsCombatSupport) return;
        if (!offDef.SupportConsumableOnAttack) return;

        int consume = Mathf.Max(1, offDef.SupportConsumeAmountPerAttack);
        equipment.ConsumeOffHandSupport(consume);
    }

    private ItemDefinition GetMainWeaponDefForPopup()
    {
        if (stats == null) return null;

        var equipment = GetComponent<EquipmentManager>();
        var inventory = GetComponent<Inventory>();

        if (equipment == null || inventory == null) return null;
        if (string.IsNullOrWhiteSpace(equipment.MainHandItemId)) return null;

        return inventory.GetItemDef(equipment.MainHandItemId);
    }

    private bool IsSupportEquippedWithoutCompatibleMainWeapon(out string message)
    {
        message = null;
        EquipmentManager equipment = GetComponent<EquipmentManager>();
        Inventory inv = GetComponent<Inventory>();
        if (equipment == null || inv == null)
            return false;

        ItemDefinition support = equipment.GetOffHandDef();
        if (!support || !support.IsCombatSupport)
            return false;

        ItemDefinition main = inv.GetItemDef(equipment.MainHandItemId);
        MainHandWeaponArchetype required = support.SupportRequiredMainHandArchetype;
        if (required == MainHandWeaponArchetype.None)
            return false;

        if (!main || !main.IsWeapon || main.MainHandArchetype != required)
        {
            string need = required.ToString();
            message = $"Cannot attack: {support.SupportType} equipped requires a {need}. Equip a {need} or unequip the support.";
            return true;
        }

        return false;
    }

    private bool IsRangedAttack()
    {
        return stats != null && stats.CurrentAttackSkill == AttackSkill.Ranged;
    }

    private bool IsMagicAttack()
    {
        return stats != null && stats.CurrentAttackSkill == AttackSkill.Magic;
    }

    private bool TrySpendManaForMagicAttack()
    {
        if (player == null)
            return false;

        float manaCost = 0f;
        var weapon = GetMainWeaponDefForPopup();
        if (weapon != null)
            manaCost = weapon.ManaCostPerAttack;

        if (manaCost <= 0f)
            return true;

        return player.SpendMana(manaCost);
    }

    private void HandleRangedAttack(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution)
    {
        float fireDelay = Mathf.Max(0f, rangedProjectileFireDelay);
        if (fireDelay <= 0f)
        {
            ResolveRangedAttackAtRelease(targetAtFireTime, rolled, wasCrit, swingAttribution);
            return;
        }

        StartCoroutine(ResolveRangedAttackAfterFireDelay(targetAtFireTime, rolled, wasCrit, fireDelay, swingAttribution));
    }

    private System.Collections.IEnumerator ResolveRangedAttackAfterFireDelay(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        float fireDelay,
        SwingOutgoingAttribution swingAttribution)
    {
        yield return new WaitForSeconds(fireDelay);
        ResolveRangedAttackAtRelease(targetAtFireTime, rolled, wasCrit, swingAttribution);
    }

    private void ResolveRangedAttackAtRelease(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution)
    {
        if (targetAtFireTime == null || targetAtFireTime.IsDead)
            return;

        float delay = Mathf.Max(0f, rangedDamageDelayOffset);
        bool spawnedProjectile = TrySpawnRangedProjectile(targetAtFireTime, out float travelTime);
        if (spawnedProjectile)
            delay += travelTime;

        if (delay <= 0f)
        {
            ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit, swingAttribution);
            return;
        }

        StartCoroutine(ResolveAttackHitAfterDelay(targetAtFireTime, rolled, wasCrit, delay, swingAttribution));
    }

    private void HandleMagicAttack(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution)
    {
        float fireDelay = Mathf.Max(0f, magicProjectileFireDelay);
        if (fireDelay <= 0f)
        {
            ResolveMagicAttackAtRelease(targetAtFireTime, rolled, wasCrit, swingAttribution);
            return;
        }

        StartCoroutine(ResolveMagicAttackAfterFireDelay(targetAtFireTime, rolled, wasCrit, fireDelay, swingAttribution));
    }

    private System.Collections.IEnumerator ResolveMagicAttackAfterFireDelay(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        float fireDelay,
        SwingOutgoingAttribution swingAttribution)
    {
        yield return new WaitForSeconds(fireDelay);
        ResolveMagicAttackAtRelease(targetAtFireTime, rolled, wasCrit, swingAttribution);
    }

    private void ResolveMagicAttackAtRelease(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution)
    {
        if (targetAtFireTime == null || targetAtFireTime.IsDead)
            return;

        if (!TrySpawnMagicProjectile(targetAtFireTime, out IMagicProjectileVisual bolt))
        {
            ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit, swingAttribution);
            return;
        }

        bolt.OnImpact += () => ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit, swingAttribution);
    }

    private bool TrySpawnRangedProjectile(EnemyBaseController targetAtFireTime, out float travelTime)
    {
        travelTime = 0f;

        if (rangedProjectilePrefab == null || targetAtFireTime == null)
            return false;

        Transform spawn = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        Vector3 start = spawn.position;
        Vector3 targetCenter = GetTargetCenterMass(targetAtFireTime);

        ProjectileVisual proj = Instantiate(rangedProjectilePrefab, start, Quaternion.identity);
        proj.Launch(
            start,
            targetAtFireTime.transform,
            targetCenter,
            rangedProjectileSpeed,
            rangedProjectileRotationOffset
        );

        travelTime = Mathf.Max(0f, proj.EstimatedTravelTime);
        return true;
    }

    private static Vector3 GetTargetCenterMass(EnemyBaseController target)
    {
        if (target == null)
            return Vector3.zero;

        if (target.TryGetComponent<Collider2D>(out var col) && col != null)
            return col.bounds.center;

        var childCol = target.GetComponentInChildren<Collider2D>();
        if (childCol != null)
            return childCol.bounds.center;

        var sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        return target.transform.position;
    }

    private bool TrySpawnMagicProjectile(EnemyBaseController targetAtFireTime, out IMagicProjectileVisual bolt)
    {
        bolt = null;

        MonoBehaviour prefab = ResolveMagicProjectilePrefab();
        if (prefab == null || targetAtFireTime == null)
            return false;

        if (!prefab.TryGetComponent<IMagicProjectileVisual>(out _))
            return false;

        Transform spawn = magicProjectileSpawnPoint != null
            ? magicProjectileSpawnPoint
            : (projectileSpawnPoint != null ? projectileSpawnPoint : transform);

        Vector3 start = spawn.position;
        Vector3 targetCenter = GetTargetCenterMass(targetAtFireTime);

        MonoBehaviour instance = Instantiate(prefab, start, Quaternion.identity);
        if (!instance.TryGetComponent<IMagicProjectileVisual>(out bolt))
        {
            Destroy(instance.gameObject);
            return false;
        }

        bolt.Launch(
            start,
            targetAtFireTime.transform,
            targetCenter,
            magicProjectileSpeed,
            magicProjectileRotationOffset
        );

        return true;
    }

    private MonoBehaviour ResolveMagicProjectilePrefab()
    {
        MagicAttackType type = stats != null ? stats.CurrentMagicAttackType : MagicAttackType.Lightning;
        return type switch
        {
            MagicAttackType.Fire => magicFireProjectilePrefab != null ? magicFireProjectilePrefab : magicProjectilePrefab,
            MagicAttackType.Ice => magicIceProjectilePrefab != null ? magicIceProjectilePrefab : magicProjectilePrefab,
            _ => magicLightningProjectilePrefab != null ? magicLightningProjectilePrefab : magicProjectilePrefab
        };
    }


    private System.Collections.IEnumerator ResolveAttackHitAfterDelay(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        float delay,
        SwingOutgoingAttribution swingAttribution)
    {
        yield return new WaitForSeconds(delay);
        ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit, swingAttribution);
    }

    private void ResolveAttackHitNow(
        EnemyBaseController targetToHit,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution = default)
    {
        if (string.IsNullOrWhiteSpace(swingAttribution.primarySource))
            swingAttribution = SwingOutgoingAttribution.AutoAttackOnly;

        DamageResult dealt = ApplySplitDamageToTarget(targetToHit, rolled, wasCrit, null, swingAttribution);
        float totalDealt = dealt.Total;
        bool primaryHitSucceeded = totalDealt > 0f;
        var alreadyHit = new HashSet<EnemyBaseController>();
        if (targetToHit != null && primaryHitSucceeded)
            alreadyHit.Add(targetToHit);

        if (totalDealt > 0f)
            player.ApplyLifeSteal(totalDealt);

        if (totalDealt > 0f)
            TryConsumeOffHandSupportAmmo();

        bool suppressBleed = false;
        bool suppressPoison = false;
        bool triggerCrescentSlash = false;
        bool crescentAppliesElemental = false;
        bool crescentPenetrating = false;
        if (abilityController != null)
        {
            var queued = abilityController.ConsumeQueuedHitEffects(targetToHit, dealt.physical, dealt.corruptionDamage);
            suppressBleed = queued.suppressDefaultBleed;
            suppressPoison = queued.suppressDefaultPoison;
            triggerCrescentSlash = queued.triggerCrescentSlash;
            crescentAppliesElemental = queued.crescentAppliesElemental;
            crescentPenetrating = queued.crescentPenetrating;
        }

        if (!suppressBleed)
            TryApplyBleed(targetToHit, dealt);
        if (!suppressPoison)
            TryApplyPoison(targetToHit, dealt);
        TryApplyElementalMagicAilment(targetToHit, dealt);
        TryApplyMeleeShock(targetToHit, dealt);

        if (primaryHitSucceeded && abilityController != null &&
            abilityController.TryConsumeCleavingExtraTargetsOnSuccessfulHit(out int cleaveExtraTargets) &&
            cleaveExtraTargets > 0)
        {
            ApplyCleaveSecondaryHits(targetToHit, cleaveExtraTargets, alreadyHit);
        }

        if (primaryHitSucceeded && triggerCrescentSlash && abilityController != null)
        {
            // Weapon damage already applied; add the same ability-scaled Crescent packet as instant-cast / secondary targets.
            abilityController.ApplyMeleeQueuedCrescentSlashExtraHit(targetToHit, crescentAppliesElemental);
            ApplyCrescentSlashSecondaryHits(targetToHit, crescentPenetrating, crescentAppliesElemental, alreadyHit);
        }
    }

    private void ApplyCleaveSecondaryHits(EnemyBaseController primaryTarget, int extraTargets, HashSet<EnemyBaseController> alreadyHit)
    {
        if (extraTargets <= 0 || stats == null)
            return;

        // Match primary melee reach: edge-to-edge X gap vs colliders (not center-to-center), with min 3 only for short weapons.
        const float cleavingMinWeaponRange = 3f;
        float weaponRange = Mathf.Max(0f, stats.Range);
        float effectiveReach = Mathf.Max(cleavingMinWeaponRange, weaponRange) + rangePadding;

        float myX = transform.position.x;
        float myY = transform.position.y;
        float myHalf = HalfWidthX(playerCol);
        float yTol = Mathf.Max(0.85f, effectiveReach * 0.4f);

        var candidates = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var nearest = new List<(EnemyBaseController enemy, float gap)>(candidates.Length);

        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!IsValidSecondaryTarget(e, alreadyHit))
                continue;

            if (Mathf.Abs(e.transform.position.y - myY) > yTol)
                continue;

            Collider2D enemyCol = e.GetComponent<Collider2D>();
            if (enemyCol == null)
                enemyCol = e.GetComponentInChildren<Collider2D>();

            float gap = EdgeGapX(myX, e.transform.position.x, myHalf, HalfWidthX(enemyCol));
            if (gap > effectiveReach)
                continue;

            nearest.Add((e, gap));
        }

        nearest.Sort((a, b) => a.gap.CompareTo(b.gap));
        int count = Mathf.Min(extraTargets, nearest.Count);
        float critMult = Mathf.Max(1f, stats.CritMultiplier);
        for (int i = 0; i < count; i++)
        {
            EnemyBaseController e = nearest[i].enemy;

            // Roll each target independently (min/max + crit).
            SplitDamage secondaryBase = stats.RollSplitAttackDamage(out bool baseWasCrit);
            if (baseWasCrit && critMult > 1f)
            {
                secondaryBase.physical /= critMult;
                secondaryBase.magic /= critMult;
                // corruption damage is not crit-scaled on cleave base rolls.
            }

            SplitDamage secondaryHit = abilityController != null
                ? abilityController.BuildCleavingSecondarySplit(secondaryBase)
                : secondaryBase;
            if (secondaryHit.IsEmpty)
                continue;

            bool secondaryCrit = false;
            if (UnityEngine.Random.value <= Mathf.Clamp01(stats.CritChance))
            {
                secondaryCrit = true;
                secondaryHit.physical *= critMult;
                secondaryHit.magic *= critMult;
            }

            string cleaveLabel = abilityController != null
                ? abilityController.GetAbilityOutgoingDamageSourceLabel(AbilityCombatPower.CleavingStrikesAbilityId)
                : "Cleaving Strikes";
            ApplySecondaryHitPipeline(e, secondaryHit, secondaryCrit, forceElementalAilment: false, cleaveLabel);
            alreadyHit.Add(e);
        }
    }

    /// <summary>
    /// Radial from <paramref name="originEnemy"/> position (not the player). Radius is max(3, weapon range) + padding.
    /// Fills <paramref name="results"/> with other living enemies in range (excludes <paramref name="originEnemy"/>).
    /// </summary>
    private void CollectEnemiesInAilmentSpreadRadius(EnemyBaseController originEnemy, List<EnemyBaseController> results)
    {
        results.Clear();
        if (stats == null || originEnemy == null)
            return;

        const float spreadMinRadius = 3f;
        const float spreadMaxRadius = 5f;
        float weaponRange = Mathf.Max(0f, stats.Range);
        float radius = Mathf.Max(spreadMinRadius, weaponRange) + rangePadding;
        radius = Mathf.Min(spreadMaxRadius, radius);
        float r2 = radius * radius;
        Vector3 origin = originEnemy.transform.position;

        var candidates = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyBaseController e = candidates[i];
            if (e == null || e.IsDead || !e.gameObject.activeInHierarchy || e == originEnemy)
                continue;

            float sqr = (e.transform.position - origin).sqrMagnitude;
            if (sqr > r2)
                continue;

            results.Add(e);
        }
    }

    /// <summary>Rend Crimson Spread: same exclusive bleed payload to every other enemy in radial range of the struck target.</summary>
    public void ApplyBleedToEnemiesInRadialSpread(EnemyBaseController originEnemy, BleedPayload payload)
    {
        CollectEnemiesInAilmentSpreadRadius(originEnemy, _ailmentSpreadScratch);
        for (int i = 0; i < _ailmentSpreadScratch.Count; i++)
        {
            AilmentController ac = _ailmentSpreadScratch[i].GetComponent<AilmentController>();
            if (ac != null)
                ac.ApplyExclusiveBleedFromHit(payload);
        }
    }

    /// <summary>Envenom Contagion Burst: full poison stack packet to every other enemy in radial range of the source (victim may be dead).</summary>
    public void ApplyPoisonContagionSpread(EnemyBaseController originEnemy, PoisonPayload payload)
    {
        CollectEnemiesInAilmentSpreadRadius(originEnemy, _ailmentSpreadScratch);
        for (int i = 0; i < _ailmentSpreadScratch.Count; i++)
        {
            AilmentController ac = _ailmentSpreadScratch[i].GetComponent<AilmentController>();
            if (ac == null)
                continue;

            for (int s = 0; s < payload.maxStacks; s++)
                ac.ApplyPoisonFromHit(payload);
        }
    }

    private void ApplyCrescentSlashSecondaryHits(EnemyBaseController primaryTarget, bool penetrating, bool applyElemental, HashSet<EnemyBaseController> alreadyHit)
    {
        if (stats == null)
            return;

        float reach = Mathf.Max(0.1f, stats.Range + 6f);
        float forward = player != null ? Mathf.Sign(player.transform.localScale.x >= 0f ? 1f : -1f) : 1f;
        Vector3 origin = transform.position;
        var candidates = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var forwardHits = new List<(EnemyBaseController enemy, float dist)>(candidates.Length);

        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!IsValidSecondaryTarget(e, alreadyHit))
                continue;

            Vector3 to = e.transform.position - origin;
            float forwardDist = to.x * forward;
            if (forwardDist <= 0f || forwardDist > reach)
                continue;

            float laneWidth = Mathf.Max(0.6f, reach * 0.35f);
            if (Mathf.Abs(to.y) > laneWidth)
                continue;

            forwardHits.Add((e, forwardDist));
        }

        forwardHits.Sort((a, b) => a.dist.CompareTo(b.dist));
        // Base Crescent Slash is "up to 3 enemies" including the primary hit, so apply to up to 2 additional targets here.
        int cap = penetrating ? forwardHits.Count : Mathf.Min(2, forwardHits.Count);
        for (int i = 0; i < cap; i++)
        {
            EnemyBaseController e = forwardHits[i].enemy;
            if (abilityController != null)
                abilityController.ApplyMeleeQueuedCrescentSlashExtraHit(e, applyElemental);
            else
            {
                SplitDamage secondaryHit = stats.RollSplitAttackDamage(out bool secondaryWasCrit);
                ApplySecondaryHitPipeline(e, secondaryHit, secondaryWasCrit, forceElementalAilment: applyElemental);
            }

            alreadyHit.Add(e);
        }
    }

    private bool IsValidSecondaryTarget(EnemyBaseController enemy, HashSet<EnemyBaseController> alreadyHit)
    {
        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            return false;
        if (alreadyHit != null && alreadyHit.Contains(enemy))
            return false;
        return true;
    }

    private void ApplySecondaryHitPipeline(
        EnemyBaseController target,
        SplitDamage rolled,
        bool wasCrit,
        bool forceElementalAilment,
        string outgoingDamageSourceLabel = null)
    {
        DamageResult dealt = ApplySplitDamageToTarget(target, rolled, wasCrit, outgoingDamageSourceLabel);
        if (dealt.Total <= 0f)
            return;

        player.ApplyLifeSteal(dealt.Total);
        TryApplyBleed(target, dealt);
        TryApplyPoison(target, dealt);
        TryApplyElementalMagicAilment(target, dealt, forceElementalAilment);
        TryApplyMeleeShock(target, dealt);
    }

    public void ToggleIdleCombat()
    {
        bool wantOn = !idleCombatEnabled;
        if (wantOn && IsIdleCombatLockedByQuestProgress())
            return;

        SetIdleCombatEnabled(wantOn);
    }

    private static bool IsIdleCombatLockedByQuestProgress()
    {
        QuestProgressManager qpm = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        return qpm != null && !qpm.IsIdleCombatUnlocked;
    }

    public void ToggleRetaliation()
    {
        SetRetaliationEnabled(!retaliationEnabled);
    }

    public void SetRetaliationEnabled(bool enabled)
    {
        retaliationEnabled = enabled;
        OnRetaliationChanged?.Invoke(retaliationEnabled);
    }

    public void SaveInto(SaveData data)
    {
        if (data == null) return;
        data.retaliationEnabled = retaliationEnabled;
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null) return;
        SetRetaliationEnabled(data.retaliationEnabled);
    }

    public void SetIdleCombatEnabled(bool enabled)
    {
        if (enabled && IsIdleCombatLockedByQuestProgress())
            return;

        idleCombatEnabled = enabled;
        OnIdleCombatChanged?.Invoke(idleCombatEnabled);

        if (enabled)
        {
            player?.SetMovementLocked(true);
            _nextIdleScanTime = 0f;
            _nextAutoConsumeTime = 0f;
            _nextAutoAbilityTime = 0f;
            _nextIdleAutoPickupTime = Time.time + idleAutoPickupIntervalSeconds;
            TickAutoConsumables();
            TickAutoAbilities();
            TickIdleCombatTargeting();
        }
        else
        {
            player?.SetMovementLocked(false);
            ClearTarget();
        }
    }

    private void TickIdleAutoPickup()
    {
        if (!inventory) return;
        if (!ToggleSettingsStore.Get(ToggleSettingId.AutoLootDuringAutoBattle))
            return;
        if (Time.time < _nextIdleAutoPickupTime) return;
        _nextIdleAutoPickupTime = Time.time + idleAutoPickupIntervalSeconds;

        PlayerStorage storage = null;
        if (player != null)
            storage = player.GetComponent<PlayerStorage>();
        if (!storage)
            storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);

        var drops = FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < drops.Length; i++)
        {
            var d = drops[i];
            if (d != null)
                d.BeginAutoBattleVacuum(transform, playerCol, inventory, storage);
        }
    }

    private void TickIdleCombatTargeting()
    {
        if (Time.time < _nextIdleScanTime) return;
        _nextIdleScanTime = Time.time + Mathf.Max(0.05f, idleRescanInterval);

        EnemyBaseController current = (_target != null && !_target.IsDead && _target.gameObject.activeInHierarchy)
            ? _target
            : null;

        EnemyBaseController picked = ResolveIdlePickedEnemy(current);
        if (picked != null)
        {
            bool acquiredNewTarget = current == null;
            if (current != picked)
                SetTargetInternal(picked);

            if (debugLogs && acquiredNewTarget)
                Debug.Log($"[Combat] Idle picked target: {picked.name}", this);
        }
        else
        {
            ClearTargetInternal();
        }
    }

    private void TryRememberEnemyDamageSourceForLongbowRetarget(Transform source)
    {
        if (source == null)
            return;

        EnemyBaseController enemy = source.GetComponentInParent<EnemyBaseController>();
        if (!enemy)
            enemy = source.GetComponentInChildren<EnemyBaseController>(true);

        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            return;

        _lastEnemyThatDamagedPlayer = enemy;
        _lastEnemyThatDamagedPlayerTime = Time.time;
    }

    /// <summary>
    /// Longbow idle acquisition: if the player was damaged by an enemy recently, prefer that enemy when it is still alive and in bow range.
    /// </summary>
    private EnemyBaseController TryPickLongbowIdleRecentAttackerInRange()
    {
        if (_lastEnemyThatDamagedPlayer == null)
            return null;

        if (Time.time - _lastEnemyThatDamagedPlayerTime > longbowPrioritizeRecentAttackerSeconds)
            return null;

        EnemyBaseController e = _lastEnemyThatDamagedPlayer;
        if (e.IsDead || !e.gameObject.activeInHierarchy)
            return null;

        float maxRange = GetCurrentMaxAttackRangeUnits();
        if (maxRange > 0.0001f)
        {
            float d = Mathf.Abs(e.transform.position.x - transform.position.x);
            if (d > maxRange)
                return null;
        }

        return e;
    }

    /// <summary>Longbow + ranged: idle auto-battle targets the enemy farthest along X first; Swiftbow/other uses closest.</summary>
    private bool ShouldIdlePickFurthestEnemyFirst()
    {
        ItemDefinition def = GetMainWeaponDefForPopup();
        if (def == null || !def.IsWeapon)
            return false;
        if (def.weaponStats.attackSkill != AttackSkill.Ranged)
            return false;
        return def.weaponStats.rangedBowType == RangedBowType.Longbow;
    }

    private EnemyBaseController ResolveIdlePickedEnemy(EnemyBaseController current)
    {
        // 1) No valid current target: pick the best new one.
        if (current == null)
        {
            if (ShouldIdlePickFurthestEnemyFirst())
            {
                EnemyBaseController recentAttacker = TryPickLongbowIdleRecentAttackerInRange();
                if (recentAttacker != null)
                    return recentAttacker;

                return FindFurthestLivingEnemyWithinAttackRangeOrClosestFallback();
            }

            return FindClosestLivingEnemy();
        }

        // 2) Longbow smart-pick only applies when acquiring a target from idle.
        // Once already engaged, keep current target stable.
        if (ShouldIdlePickFurthestEnemyFirst())
            return current;

        // 3) Smart retarget: if a closer enemy appears while moving, switch.
        if (!idleAllowRetargetToCloserEnemy)
            return current;

        EnemyBaseController closest = FindClosestLivingEnemy();
        if (closest == null || closest == current)
            return current;

        float myX = transform.position.x;
        float curD = Mathf.Abs(current.transform.position.x - myX);
        float closeD = Mathf.Abs(closest.transform.position.x - myX);

        // Only switch when the new target is materially closer.
        if (closeD + Mathf.Max(0f, idleRetargetCloserByAtLeastUnits) < curD)
            return closest;

        return current;
    }

    private float GetCurrentMaxAttackRangeUnits()
    {
        if (stats == null)
            return 0f;
        // Stats.Range is used elsewhere as the weapon's reach/range.
        return Mathf.Max(0f, stats.Range);
    }

    private EnemyBaseController FindFurthestLivingEnemyWithinAttackRangeOrClosestFallback()
    {
        EnemyBaseController furthestInRange = FindFurthestLivingEnemyWithinAttackRange();
        if (furthestInRange != null)
            return furthestInRange;
        return FindClosestLivingEnemy();
    }

    private EnemyBaseController FindFurthestLivingEnemyWithinAttackRange()
    {
        float maxRange = GetCurrentMaxAttackRangeUnits();
        if (maxRange <= 0.0001f)
            return null;

        var enemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return null;

        float bestDist = -1f;
        EnemyBaseController best = null;

        float myX = transform.position.x;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (!e) continue;
            if (e.IsDead) continue;
            if (!e.gameObject.activeInHierarchy) continue;

            float d = Mathf.Abs(e.transform.position.x - myX);
            if (d > maxRange)
                continue;

            if (d > bestDist)
            {
                bestDist = d;
                best = e;
            }
        }

        return best;
    }

    private EnemyBaseController FindClosestLivingEnemy()
    {
        var enemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return null;

        float bestDist = float.MaxValue;
        EnemyBaseController best = null;

        float myX = transform.position.x;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (!e) continue;
            if (e.IsDead) continue;
            if (!e.gameObject.activeInHierarchy) continue;

            float d = Mathf.Abs(e.transform.position.x - myX);
            if (d < bestDist)
            {
                bestDist = d;
                best = e;
            }
        }

        return best;
    }

    private EnemyBaseController FindFurthestLivingEnemy()
    {
        var enemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return null;

        float bestDist = -1f;
        EnemyBaseController best = null;

        float myX = transform.position.x;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (!e) continue;
            if (e.IsDead) continue;
            if (!e.gameObject.activeInHierarchy) continue;

            float d = Mathf.Abs(e.transform.position.x - myX);
            if (d > bestDist)
            {
                bestDist = d;
                best = e;
            }
        }

        return best;
    }

    public bool TryRetaliateFromAttacker(Transform attackerTransform)
    {
        if (!retaliationEnabled)
            return false;
        if (attackerTransform == null)
            return false;

        // Keep the current engaged target. Never override it.
        if (_target != null && !_target.IsDead && _target.gameObject.activeInHierarchy)
            return false;

        EnemyBaseController attacker = attackerTransform.GetComponentInParent<EnemyBaseController>();
        if (attacker == null || attacker.IsDead || !attacker.gameObject.activeInHierarchy)
            return false;

        SetTargetInternal(attacker);
        return true;
    }

    public void SetTarget(EnemyBaseController enemy)
    {
        if (!enemy || enemy.IsDead)
        {
            ClearTarget();
            return;
        }

        if (_target == enemy)
            return;

        SetTargetInternal(enemy);
    }

    private void SetTargetInternal(EnemyBaseController enemy)
    {
        _target = enemy;
        _isClosingDistanceForAttack = false;
        _attackBufferedFromRange = false;
        _targetColCached = null;
        OnTargetChanged?.Invoke();
    }

    public void ClearTarget()
    {
        _isClosingDistanceForAttack = false;
        _attackBufferedFromRange = false;
        ClearTargetInternal();
    }

    private void ClearTargetInternal()
    {
        _target = null;
        _targetColCached = null;

        if (player)
        {
            player.ClearActionOverride();
            player.StopMoveOnly();
        }

        OnTargetChanged?.Invoke();
    }

    private static float HalfWidthX(Collider2D c) => c ? c.bounds.extents.x : 0f;

    private static float EdgeGapX(float ax, float bx, float aHalf, float bHalf)
    {
        return Mathf.Abs(bx - ax) - (aHalf + bHalf);
    }

    private struct DamageResult
    {
        public float physical;
        public float magic;
        public float corruptionDamage;

        public float Total => physical + magic + corruptionDamage;
    }

    private DamageResult ApplySplitDamageToTarget(
        EnemyBaseController target,
        SplitDamage rolled,
        bool wasCrit,
        string outgoingDamageSourceLabel = null,
        SwingOutgoingAttribution swingAttribution = default)
    {
        DamageResult result = default;
        if (target == null || target.IsDead) return result;
        float conditionalDamageMult = GetConditionalMeleeDamageMultiplier(target);
        bool isPlayerWeaponSwing = string.IsNullOrWhiteSpace(outgoingDamageSourceLabel);
        bool deferSwingOutgoing = isPlayerWeaponSwing &&
            (swingAttribution.HasBonus || HasAilmentConditionalDamageBonusOnTarget(target));
        string sourceLabel = deferSwingOutgoing
            ? DeferredSwingOutgoingDpsLabel
            : ResolveOutgoingDamageSourceLabel(outgoingDamageSourceLabel, swingAttribution);

        if (rolled.physical > 0f)
        {
            int dealt = target.TakeDamage(
                Mathf.RoundToInt(rolled.physical * conditionalDamageMult),
                DamageType.Physical,
                wasCrit,
                player.transform,
                stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null,
                dpsBucketOverride: null,
                armorRatingMultiplier: 1f,
                magicResistRatingMultiplier: 1f,
                outgoingDpsSourceLabel: sourceLabel);

            result.physical = Mathf.Max(0f, dealt);
        }

        if (rolled.magic > 0f)
        {
            int dealt = target.TakeDamage(
                Mathf.RoundToInt(rolled.magic * conditionalDamageMult),
                DamageType.Magic,
                wasCrit,
                player.transform,
                stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null,
                dpsBucketOverride: null,
                armorRatingMultiplier: 1f,
                magicResistRatingMultiplier: 1f,
                outgoingDpsSourceLabel: sourceLabel);

            result.magic = Mathf.Max(0f, dealt);
        }

        if (rolled.corruptionDamage > 0f)
        {
            float potency = Mathf.Max(0f, rolled.corruptionDamage * conditionalDamageMult);
            int dealt = target.TakeDamage(
                Mathf.RoundToInt(potency),
                DamageType.Corruption,
                wasCrit,
                player.transform,
                stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null,
                outgoingDpsSourceLabel: sourceLabel);

            result.corruptionDamage = Mathf.Max(0f, dealt);
        }

        if (deferSwingOutgoing)
            RecordWeaponSwingOutgoingDamage(result.Total, target, swingAttribution);

        return result;
    }

    private bool HasAilmentConditionalDamageBonusOnTarget(EnemyBaseController target)
    {
        if (stats == null || target == null)
            return false;

        GetConditionalMeleeDamageMultiplierBreakdown(target, out _, out float bleedBonus, out float poisonBonus, out float shockBonus);
        return bleedBonus > 0f || poisonBonus > 0f || shockBonus > 0f;
    }

    private float GetConditionalMeleeDamageMultiplier(EnemyBaseController target)
    {
        GetConditionalMeleeDamageMultiplierBreakdown(
            target,
            out float lowHpBonus,
            out float bleedBonus,
            out float poisonBonus,
            out float shockBonus);
        return 1f + Mathf.Max(0f, lowHpBonus + bleedBonus + poisonBonus + shockBonus);
    }

    private void GetConditionalMeleeDamageMultiplierBreakdown(
        EnemyBaseController target,
        out float lowHpBonus,
        out float bleedBonus,
        out float poisonBonus,
        out float shockBonus)
    {
        lowHpBonus = 0f;
        bleedBonus = 0f;
        poisonBonus = 0f;
        shockBonus = 0f;

        if (stats == null || target == null)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments != null)
        {
            if (ailments.HasBleed)
                bleedBonus = Mathf.Max(0f, stats.MeleeDamageVsBleeding);
            if (ailments.HasPoison)
                poisonBonus = Mathf.Max(0f, stats.MeleeDamageVsPoisoned);
            if (ailments.HasShock)
                shockBonus = Mathf.Max(0f, stats.MeleeDamageVsShocked);
        }

        CharacterStats targetStats = target.GetComponent<CharacterStats>();
        if (targetStats != null && targetStats.MaxHP > 0f)
        {
            float hp01 = targetStats.HP / Mathf.Max(1f, targetStats.MaxHP);
            if (hp01 <= stats.MeleeLowHpThreshold01)
                lowHpBonus = Mathf.Max(0f, stats.MeleeDamageVsLowHp);
        }
    }

    private void TryApplyBleed(EnemyBaseController target, DamageResult dealt)
    {
        if (target == null) return;
        if (dealt.physical <= 0f) return;
        if (stats.BleedChance <= 0f) return;

        if (Random.value > stats.BleedChance)
            return;

        float duration = Mathf.Max(1f, stats.BleedDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        float baseDuration = Mathf.Max(1f, stats.BleedBaseDuration);

        float bleedTickDamage = dealt.physical * (1f + stats.BleedMultiplier) / baseDuration;
        if (bleedTickDamage <= 0f) return;

        float totalBleedDamage = bleedTickDamage * ticks;

        var payload = new BleedPayload(
            totalBleedDamage,
            duration,
            ticks,
            transform
        );

        var ailments = target.GetComponent<AilmentController>();
        if (ailments != null)
            ailments.ApplyBleedFromHit(payload);
    }

    private void TryApplyPoison(EnemyBaseController target, DamageResult dealt)
    {
        if (target == null) return;

        float poisonSourceDamage = dealt.corruptionDamage;

        if (poisonSourceDamage <= 0f) return;
        if (stats.PoisonChance <= 0f || stats.PoisonMultiplier < 0f) return;

        if (Random.value > stats.PoisonChance)
            return;

        float totalPoisonDamage =
            poisonSourceDamage * stats.PoisonPoolFractionOfCorruptionDamage * (1f + stats.PoisonMultiplier);
        if (totalPoisonDamage <= 0f) return;

        float duration = Mathf.Max(0.1f, stats.PoisonDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        int maxStacks = Mathf.Max(1, stats.PoisonMaxStacks);

        var payload = new PoisonPayload(
            totalPoisonDamage,
            duration,
            ticks,
            maxStacks,
            transform
        );

        var ailments = target.GetComponent<AilmentController>();
        if (ailments != null)
            ailments.ApplyPoisonFromHit(payload);
    }

    private void TryApplyElementalMagicAilment(EnemyBaseController target, DamageResult dealt, bool forceApply = false)
    {
        if (target == null) return;
        if (stats == null) return;
        var ailments = target.GetComponent<AilmentController>();
        if (ailments == null) return;

        if (stats.CurrentAttackAppliesAsFireForBurn && dealt.Total > 0f)
        {
            ailments.TryApplyBurnFromFireHit(
                dealt.Total,
                stats.BurnApplyChance,
                stats.BurnExplosionMultiplier,
                transform);
            return;
        }

        // Non-fire elemental ailments use normal apply chance per hit.
        if (!forceApply && dealt.magic <= 0f) return;
        if (!forceApply && stats.CurrentAttackSkill != AttackSkill.Magic) return;

        float chance = stats.MagicAilmentApplyChance;
        if (!forceApply)
        {
            if (chance <= 0f) return;
            if (Random.value > chance) return;
        }

        switch (stats.CurrentMagicAttackType)
        {
            case MagicAttackType.Ice:
                ailments.ApplyChillFromHit(new ChillPayload(
                    duration: stats.ChillDuration,
                    maxStacks: stats.ChillMaxStacks,
                    slowPerStack: stats.ChillSlowPerStack,
                    source: transform
                ));
                break;

            case MagicAttackType.Lightning:
            default:
                ailments.ApplyShockFromHit(new ShockPayload(
                    duration: stats.ShockDuration,
                    damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
                    source: transform
                ));
                break;
        }
    }

    private void TryApplyMeleeShock(EnemyBaseController target, DamageResult dealt)
    {
        if (target == null || stats == null)
            return;
        if (dealt.Total <= 0f)
            return;
        if (dealt.magic <= 0f)
            return;
        if (stats.GetMeleeMagicLightningFraction() <= 0f)
            return;

        float chance = stats.MeleeShockChance;
        if (chance <= 0f || Random.value > chance)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        ailments.ApplyShockFromHit(new ShockPayload(
            duration: stats.ShockDuration,
            damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
            source: transform
        ));
    }

    public void AwardCombatXp(float damageDealt)
    {
        AwardCombatXp(damageDealt, null);
    }

    public void AwardCombatXp(float damageDealt, DpsDamageBucket? bucket)
    {
        AwardCombatXp(damageDealt, bucket, true);
    }

    public void AwardCombatXp(float damageDealt, DpsDamageBucket? bucket, bool grantXp)
    {
        AwardCombatXp(damageDealt, bucket, grantXp, null);
    }

    public void AwardCombatXp(float damageDealt, DpsDamageBucket? bucket, bool grantXp, string outgoingDamageSourceLabel)
    {
        if (damageDealt <= 0f)
            return;

        RecordDamageForDps(damageDealt, bucket, outgoingDamageSourceLabel);

        if (!grantXp || xpPerDamage <= 0f)
            return;

        var sm = SkillsManager.Instance;
        if (sm == null)
            return;

        SkillType skill = sm.GetCombatSkillFromCurrentWeapon(player, stats);
        sm.AddXpFloat(skill, damageDealt * xpPerDamage, combatXpSource);
    }

    public const string IncomingDotDamageDealerFallback = "Ailment/World";

    public void RecordIncomingDamageForDps(
        float damageAmount,
        DpsDamageBucket bucket,
        Transform source = null,
        string dealerDisplayNameOverride = null)
    {
        if (damageAmount <= 0f || _dpsTrackerPaused)
            return;

        string dealerName = !string.IsNullOrWhiteSpace(dealerDisplayNameOverride)
            ? dealerDisplayNameOverride.Trim()
            : ResolveIncomingDamageDealerDisplayName(source);

        if (string.IsNullOrWhiteSpace(dealerName))
            dealerName = IncomingDealerLabelForUnresolvedSource(bucket);

        MarkRecentCombatActivity();
        EnsureDpsSessionStarted();
        _incomingDamageSum.Add(bucket, damageAmount);
        AddIncomingDealerDamageByName(dealerName, damageAmount);
        TryRememberEnemyDamageSourceForLongbowRetarget(source);
    }

    private void AddIncomingDealerDamageByName(string dealerName, float amount)
    {
        if (amount <= 0f || string.IsNullOrWhiteSpace(dealerName))
            return;

        if (_incomingDamageByDealer.TryGetValue(dealerName, out float current))
        {
            _incomingDamageByDealer[dealerName] = current + amount;
            return;
        }

        _incomingDamageByDealer[dealerName] = amount;
        _incomingDealerOrder.Add(dealerName);
    }

    /// <summary>
    /// Display name for incoming DPS attribution (enemy display name, unit name, or object name). Null when <paramref name="source"/> is missing/destroyed.
    /// </summary>
    public static string ResolveIncomingDamageDealerDisplayName(Transform source)
    {
        if (source == null)
            return null;

        EnemyBaseController enemy = source.GetComponentInParent<EnemyBaseController>();
        if (!enemy)
            enemy = source.GetComponentInChildren<EnemyBaseController>(true);

        if (enemy)
        {
            string name = NormalizeEnemyTypeName(enemy.DisplayName);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        CharacterStats sourceStats = source.GetComponentInParent<CharacterStats>();
        if (sourceStats != null && !string.IsNullOrWhiteSpace(sourceStats.UnitDisplayName))
            return sourceStats.UnitDisplayName.Trim();

        return source.name.Replace("(Clone)", "").Trim();
    }

    private static string IncomingDealerLabelForUnresolvedSource(DpsDamageBucket bucket)
    {
        return bucket switch
        {
            DpsDamageBucket.Bleed or DpsDamageBucket.Poison or DpsDamageBucket.Burn or DpsDamageBucket.Corruption
                => IncomingDotDamageDealerFallback,
            _ => "Unknown"
        };
    }

    private static string NormalizeEnemyTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string trimmed = value.Trim();
        const string elitePrefix = "Elite ";
        if (trimmed.StartsWith(elitePrefix, System.StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(elitePrefix.Length).Trim();

        return trimmed;
    }

    private void RecordDamageForDps(float damageAmount, DpsDamageBucket? bucket, string outgoingDamageSourceLabel = null)
    {
        if (damageAmount <= 0f || _dpsTrackerPaused)
            return;

        if (string.Equals(outgoingDamageSourceLabel, DeferredSwingOutgoingDpsLabel, System.StringComparison.Ordinal))
            return;

        MarkRecentCombatActivity();
        EnsureDpsSessionStarted();
        _combatSessionDamageSum += damageAmount;

        DpsDamageBucket effectiveBucket = ResolveEffectiveOutgoingBucket(bucket, outgoingDamageSourceLabel);
        _outgoingDamageSum.Add(effectiveBucket, damageAmount);

        string sourceLabel = ResolveOutgoingDamageSourceLabel(outgoingDamageSourceLabel, default, effectiveBucket);
        AddOutgoingSourceDamage(sourceLabel, damageAmount);
    }

    private void RecordWeaponSwingOutgoingDamage(
        float totalDealt,
        EnemyBaseController target,
        SwingOutgoingAttribution swingAttribution)
    {
        if (totalDealt <= 0f || _dpsTrackerPaused)
            return;

        if (swingAttribution.AttributesEntireSwingToBonusSource)
        {
            RecordDamageForDps(totalDealt, DpsDamageBucket.Physical, swingAttribution.bonusSource);
            return;
        }

        GetConditionalMeleeDamageMultiplierBreakdown(
            target,
            out float lowHpBonus,
            out float bleedBonus,
            out float poisonBonus,
            out float shockBonus);

        float totalMult = 1f + lowHpBonus + bleedBonus + poisonBonus + shockBonus;
        if (totalMult <= 1e-6f)
            totalMult = 1f;

        float powerSlashAmount = swingAttribution.HasBonus ? totalDealt * swingAttribution.bonusFraction : 0f;
        float afterPowerSlash = totalDealt - powerSlashAmount;

        float bleedAmount = bleedBonus > 0f ? afterPowerSlash * (bleedBonus / totalMult) : 0f;
        float poisonAmount = poisonBonus > 0f ? afterPowerSlash * (poisonBonus / totalMult) : 0f;
        float shockAmount = shockBonus > 0f ? afterPowerSlash * (shockBonus / totalMult) : 0f;
        float autoAmount = afterPowerSlash - bleedAmount - poisonAmount - shockAmount;

        if (autoAmount > 0f)
            RecordDamageForDps(autoAmount, DpsDamageBucket.Physical, swingAttribution.primarySource);
        if (powerSlashAmount > 0f)
            RecordDamageForDps(powerSlashAmount, DpsDamageBucket.Physical, swingAttribution.bonusSource);
        if (bleedAmount > 0f)
            RecordDamageForDps(bleedAmount, DpsDamageBucket.Bleed, OutgoingBleedingSourceLabel);
        if (poisonAmount > 0f)
            RecordDamageForDps(poisonAmount, DpsDamageBucket.Poison, OutgoingPoisonSourceLabel);
        if (shockAmount > 0f)
            RecordDamageForDps(shockAmount, DpsDamageBucket.Physical, OutgoingShockSourceLabel);
    }

    private static DpsDamageBucket ResolveEffectiveOutgoingBucket(
        DpsDamageBucket? bucket,
        string outgoingDamageSourceLabel)
    {
        if (IsMinionOutgoingSource(outgoingDamageSourceLabel))
            return DpsDamageBucket.Minion;

        if (bucket.HasValue)
            return bucket.Value;

        if (string.IsNullOrWhiteSpace(outgoingDamageSourceLabel))
            return DpsDamageBucket.Physical;

        if (string.Equals(outgoingDamageSourceLabel, OutgoingBleedingSourceLabel, System.StringComparison.OrdinalIgnoreCase))
            return DpsDamageBucket.Bleed;
        if (string.Equals(outgoingDamageSourceLabel, OutgoingPoisonSourceLabel, System.StringComparison.OrdinalIgnoreCase))
            return DpsDamageBucket.Poison;
        if (string.Equals(outgoingDamageSourceLabel, "Burning", System.StringComparison.OrdinalIgnoreCase))
            return DpsDamageBucket.Burn;

        return DpsDamageBucket.Physical;
    }

    public static bool IsMinionOutgoingSource(string outgoingDamageSourceLabel)
    {
        if (string.IsNullOrWhiteSpace(outgoingDamageSourceLabel))
            return false;

        return string.Equals(
            outgoingDamageSourceLabel.Trim(),
            DefaultMinionOutgoingSourceLabel,
            System.StringComparison.OrdinalIgnoreCase);
    }

    public const string OutgoingBleedingSourceLabel = "Bleeding";
    public const string OutgoingPoisonSourceLabel = "Poison";
    public const string OutgoingShockSourceLabel = "Shock";
    public const string DefaultMinionOutgoingSourceLabel = "Soulforged Weapon";

    private string ResolveOutgoingDamageSourceLabel(
        string explicitLabel,
        SwingOutgoingAttribution swingAttribution = default,
        DpsDamageBucket? bucket = null)
    {
        if (string.Equals(explicitLabel, DeferredSwingOutgoingDpsLabel, System.StringComparison.Ordinal))
            return explicitLabel;

        if (!string.IsNullOrWhiteSpace(explicitLabel))
            return explicitLabel.Trim();

        string bucketLabel = OutgoingSourceLabelForBucket(bucket);
        if (!string.IsNullOrWhiteSpace(bucketLabel))
            return bucketLabel;

        if (!string.IsNullOrWhiteSpace(swingAttribution.primarySource))
            return swingAttribution.primarySource;

        return "Auto Attack";
    }

    private static string OutgoingSourceLabelForBucket(DpsDamageBucket? bucket)
    {
        if (!bucket.HasValue)
            return null;

        return bucket.Value switch
        {
            DpsDamageBucket.Bleed => OutgoingBleedingSourceLabel,
            DpsDamageBucket.Poison => OutgoingPoisonSourceLabel,
            DpsDamageBucket.Burn => "Burning",
            DpsDamageBucket.Minion => DefaultMinionOutgoingSourceLabel,
            _ => null
        };
    }

    private void AddOutgoingSourceDamage(string sourceName, float amount)
    {
        if (amount <= 0f || string.IsNullOrWhiteSpace(sourceName))
            return;

        if (_outgoingDamageBySource.TryGetValue(sourceName, out float current))
        {
            _outgoingDamageBySource[sourceName] = current + amount;
            return;
        }

        _outgoingDamageBySource[sourceName] = amount;
        _outgoingSourceOrder.Add(sourceName);
    }

    private void EnsureDpsSessionStarted()
    {
        if (_combatSessionStartTime < 0f)
            _combatSessionStartTime = Time.time;
    }

    public void PauseDpsTracker()
    {
        if (_dpsTrackerPaused)
            return;

        if (_combatSessionStartTime < 0f)
        {
            _pausedDpsSessionDuration = 0f;
        }
        else
        {
            _pausedDpsSessionDuration = Mathf.Max(0.001f, Time.time - _combatSessionStartTime);
        }

        _dpsTrackerPaused = true;
    }

    public void ResetDpsTrackerForRespawn()
    {
        _dpsTrackerPaused = false;
        _pausedDpsSessionDuration = 0f;
        ResetDpsSession();
        _lastCombatActivityTime = -999f;
    }

    private void ResetDpsSession()
    {
        _combatSessionStartTime = -1f;
        _combatSessionDamageSum = 0f;
        _outgoingDamageSum = default;
        _incomingDamageSum = default;
        _incomingDamageByDealer.Clear();
        _incomingDealerOrder.Clear();
        _outgoingDamageBySource.Clear();
        _outgoingSourceOrder.Clear();
        _pausedDpsSessionDuration = 0f;
        _lastEnemyThatDamagedPlayer = null;
        _lastEnemyThatDamagedPlayerTime = -999f;
    }

    private void TryResolveAutoConsumeRefs()
    {
        if (!player) player = GetComponent<PlayerController>();
        if (!stats) stats = GetComponent<CharacterStats>();
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!consumableController) consumableController = GetComponent<PlayerConsumableController>();
        if (!abilityController) abilityController = GetComponent<PlayerAbilityController>();

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
    }
}