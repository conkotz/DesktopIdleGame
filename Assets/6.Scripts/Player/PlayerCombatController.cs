using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public partial class PlayerCombatController : MonoBehaviour, ISaveable
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

    public readonly struct IncomingHealingSourceEntry
    {
        public readonly string sourceName;
        public readonly float totalHealing;

        public IncomingHealingSourceEntry(string sourceName, float totalHealing)
        {
            this.sourceName = sourceName;
            this.totalHealing = totalHealing;
        }
    }

    public readonly struct OutgoingDamageSourceEntry
    {
        public readonly string sourceName;
        public readonly float totalDamage;
        public readonly int hitCount;
        public readonly int useCount;
        public readonly bool tracksUses;

        public OutgoingDamageSourceEntry(
            string sourceName,
            float totalDamage,
            int hitCount,
            int useCount,
            bool tracksUses)
        {
            this.sourceName = sourceName;
            this.totalDamage = totalDamage;
            this.hitCount = hitCount;
            this.useCount = useCount;
            this.tracksUses = tracksUses;
        }
    }

    private struct OutgoingSourceCounter
    {
        public float totalDamage;
        public int hitCount;
        public int useCount;
    }

    /// <summary>How a basic-attack swing's dealt damage is split across outgoing DPS sources.</summary>
    public readonly struct SwingOutgoingAttribution
    {
        public readonly string primarySource;
        public readonly string bonusSource;
        public readonly float bonusFraction;
        public readonly DpsDamageBucket? bonusBucket;

        public SwingOutgoingAttribution(string primarySource, string bonusSource, float bonusFraction, DpsDamageBucket? bonusBucket = null)
        {
            this.primarySource = string.IsNullOrWhiteSpace(primarySource) ? "Auto Attack" : primarySource.Trim();
            this.bonusSource = string.IsNullOrWhiteSpace(bonusSource) ? null : bonusSource.Trim();
            this.bonusFraction = Mathf.Clamp01(bonusFraction);
            this.bonusBucket = bonusBucket;
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
    private int _autoBattleAbilityRoundRobinIndex = -1;
    private bool _autoBattleDeferExtraFlameCharge;
    private int _autoBattleNonFlameSlotsBeforeNextFlameCharge;

    [Header("Combat XP")]
    [SerializeField, Range(0f, 5f)]
    private float xpPerDamage = 0.1f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Fraction of dealt-damage combat XP also awarded to Endurance (same xp/damage rate as the active combat skill).")]
    private float enduranceXpFromDamageDealtFraction = 1f / 3f;

    [SerializeField]
    private string combatXpSource = "Combat";

    [SerializeField]
    private string enduranceOffenceXpSource = "Offence";

    /// <summary>Combat XP awarded per point of damage dealt (Melee / Ranged / Magic).</summary>
    public float XpPerDamage => Mathf.Max(0f, xpPerDamage);

    /// <summary>Endurance XP per damage dealt when offence sharing is enabled (combat rate × fraction).</summary>
    public float EnduranceXpPerDamageDealt =>
        XpPerDamage * Mathf.Clamp01(enduranceXpFromDamageDealtFraction);

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
        if (_target == null || _target.IsDead || !_target.gameObject.activeInHierarchy)
            return null;

        if (!IsValidCombatTarget(_target))
        {
            ClearTargetInternal();
            return null;
        }

        return _target;
    }

    /// <summary>Closest living enemy within current weapon attack range (edge-to-edge).</summary>
    public EnemyBaseController FindClosestEnemyInAttackRange() =>
        FindClosestEnemyInAttackRange(preferCurrentTarget: true);

    public EnemyBaseController FindClosestLivingEnemyForEngage() =>
        FindClosestLivingEnemy();

    /// <summary>
    /// Living enemy in attack range. When <paramref name="preferCurrentTarget"/> is true and
    /// <see cref="CurrentTarget"/> is in range, returns that enemy instead of a slightly closer one.
    /// </summary>
    public EnemyBaseController FindClosestEnemyInAttackRange(bool preferCurrentTarget)
    {
        if (stats == null)
            return null;

        float myRange = GetEffectiveMeleeReach();
        float closeEnoughToSwing = myRange + stopSlack;
        float myX = transform.position.x;
        float myHalf = HalfWidthX(playerCol);

        if (preferCurrentTarget &&
            _target != null && !_target.IsDead && _target.gameObject.activeInHierarchy &&
            IsValidCombatTarget(_target))
        {
            Collider2D currentCol = _target.GetComponent<Collider2D>();
            if (!currentCol)
                currentCol = _target.GetComponentInChildren<Collider2D>();

            float currentHalf = HalfWidthX(currentCol);
            float currentGap = EdgeGapX(myX, _target.transform.position.x, myHalf, currentHalf);
            if (currentGap <= closeEnoughToSwing)
                return _target;
        }

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            if (!IsValidCombatTarget(enemy))
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

    /// <summary>Farthest living enemy within current weapon attack range (edge-to-edge).</summary>
    public EnemyBaseController FindFurthestEnemyInAttackRange()
    {
        if (stats == null)
            return null;

        float closeEnoughToSwing = GetEffectiveMeleeReach() + stopSlack;
        float myX = transform.position.x;
        float myHalf = HalfWidthX(playerCol);

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDist = -1f;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            if (!IsValidCombatTarget(enemy))
                continue;

            Collider2D enemyCol = enemy.GetComponent<Collider2D>();
            if (!enemyCol)
                enemyCol = enemy.GetComponentInChildren<Collider2D>();

            float enemyHalf = HalfWidthX(enemyCol);
            float gap = EdgeGapX(myX, enemy.transform.position.x, myHalf, enemyHalf);
            if (gap > closeEnoughToSwing)
                continue;

            float dist = Mathf.Abs(enemy.transform.position.x - myX);
            if (dist > bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    /// <summary>
    /// Manual Attack hotkey: closest in-range by default; Longbow prefers a recent attacker then furthest in-range.
    /// </summary>
    public EnemyBaseController ResolveManualStarterAttackTarget()
    {
        if (ShouldIdlePickFurthestEnemyFirst())
        {
            EnemyBaseController recentAttacker = TryPickLongbowIdleRecentAttackerInRange();
            if (recentAttacker != null)
                return recentAttacker;

            return FindFurthestEnemyInAttackRange();
        }

        return FindClosestEnemyInAttackRange(preferCurrentTarget: false);
    }

    /// <summary>Max edge gap for melee abilities that may walk into range before firing (Power Slash, Crusader Strike).</summary>
    public const float MeleeAbilityApproachMaxGap = 5f;

    /// <summary>True when edge-to-edge gap to <paramref name="enemy"/> is within the approach band.</summary>
    public bool IsEnemyWithinApproachRange(EnemyBaseController enemy, float maxGap = MeleeAbilityApproachMaxGap)
    {
        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy || stats == null)
            return false;

        float myX = transform.position.x;
        float myHalf = HalfWidthX(playerCol);

        Collider2D enemyCol = enemy.GetComponent<Collider2D>();
        if (!enemyCol)
            enemyCol = enemy.GetComponentInChildren<Collider2D>();

        float enemyHalf = HalfWidthX(enemyCol);
        float gap = EdgeGapX(myX, enemy.transform.position.x, myHalf, enemyHalf);
        return gap <= maxGap;
    }

    /// <summary>Closest living enemy within the melee-ability approach band (may still be outside auto-attack range).</summary>
    public EnemyBaseController FindClosestEnemyWithinApproachRange(float maxGap = MeleeAbilityApproachMaxGap)
    {
        if (stats == null)
            return null;

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

            if (!IsValidCombatTarget(enemy))
                continue;

            Collider2D enemyCol = enemy.GetComponent<Collider2D>();
            if (!enemyCol)
                enemyCol = enemy.GetComponentInChildren<Collider2D>();

            float enemyHalf = HalfWidthX(enemyCol);
            float gap = EdgeGapX(myX, enemy.transform.position.x, myHalf, enemyHalf);
            if (gap > maxGap)
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

        float closeEnoughToSwing = GetEffectiveMeleeReach() + stopSlack;
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
    private float _stableCombatSideSign;
    private bool _attackBufferedFromRange;
    private bool _wasInAttackRangeWithTarget;
    /// <summary>When retaliation is off, leaving attack range suspends auto-attack/chase until Attack or a combat ability is used again.</summary>
    private bool _suspendAutoAttackUntilReengage;
    private float _combatSessionStartTime = -1f;
    private float _combatSessionDamageSum;
    private DpsDamageBreakdown _outgoingDamageSum;
    private DpsDamageBreakdown _incomingDamageSum;
    private DpsMitigationBreakdown _incomingMitigationSum;
    private float _incomingHealingSum;
    private bool _dpsTrackerPaused;
    private float _pausedDpsSessionDuration;
    private float _lastCombatActivityTime = -999f;
    private float _lastHpForCombatEngageTrack = -1f;
    private readonly Dictionary<string, float> _incomingDamageByDealer = new Dictionary<string, float>();
    private readonly List<string> _incomingDealerOrder = new List<string>();
    private readonly Dictionary<string, float> _incomingHealingBySource =
        new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _incomingHealingSourceOrder = new List<string>();
    private readonly Dictionary<string, OutgoingSourceCounter> _outgoingSourceCounters =
        new Dictionary<string, OutgoingSourceCounter>(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _outgoingSourceOrder = new List<string>();

    private readonly List<EnemyBaseController> _ailmentSpreadScratch = new List<EnemyBaseController>(16);
    private readonly List<(EnemyBaseController enemy, float gap)> _cleaveCandidateScratch = new(16);

    private EnemyBaseController _lastEnemyThatDamagedPlayer;
    private float _lastEnemyThatDamagedPlayerTime = -999f;
    private float _nextParryRiposteAt;


    public float GetAttackCooldownSeconds()
    {
        if (stats == null) return 0f;
        return 1f / Mathf.Max(0.01f, stats.AttacksPerSecond);
    }

    /// <summary>Extra reach padding on melee strike / cleave checks (serialized on this component).</summary>
    public float GetMeleeRangePadding() => rangePadding;

    private float GetEffectiveMeleeReach()
    {
        if (stats == null)
            return rangePadding;

        float weaponRange = Mathf.Max(0f, stats.Range);
        if (!abilityController)
            abilityController = GetComponent<PlayerAbilityController>();

        // Cleaving Strikes needs extended reach for cleave anchors; do not apply to normal weapon range.
        if (abilityController != null && abilityController.IsCleavingStrikesActive)
            weaponRange = Mathf.Max(weaponRange, AbilityCombatPower.CleavingStrikesMinMeleeReach);

        return weaponRange + rangePadding;
    }

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

        if (_combatSessionDamageSum <= 0f &&
            _incomingDamageSum.Total <= 0f &&
            _incomingMitigationSum.Total <= 0f &&
            _incomingHealingSum <= 0f)
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

    public DpsMitigationBreakdown GetIncomingMitigationBreakdown()
    {
        return _incomingMitigationSum;
    }

    public DpsMitigationBreakdown GetIncomingMitigationDpsBreakdown()
    {
        if (!TryGetDpsSessionDuration(out float duration))
            return default;

        return _incomingMitigationSum.PerSecond(duration);
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
            if (!_outgoingSourceCounters.TryGetValue(key, out OutgoingSourceCounter counter))
                continue;
            if (counter.totalDamage <= 0f && counter.hitCount <= 0 && counter.useCount <= 0)
                continue;

            entries.Add(new OutgoingDamageSourceEntry(
                key,
                counter.totalDamage,
                counter.hitCount,
                counter.useCount,
                OutgoingSourceTracksUses(key)));
        }

        entries.Sort((a, b) => b.totalDamage.CompareTo(a.totalDamage));
        return entries;
    }

    public List<IncomingHealingSourceEntry> GetIncomingHealingBySource()
    {
        var entries = new List<IncomingHealingSourceEntry>(_incomingHealingSourceOrder.Count);
        for (int i = 0; i < _incomingHealingSourceOrder.Count; i++)
        {
            string key = _incomingHealingSourceOrder[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!_incomingHealingBySource.TryGetValue(key, out float total))
                continue;
            if (total <= 0f)
                continue;

            entries.Add(new IncomingHealingSourceEntry(key, total));
        }

        entries.Sort((a, b) => b.totalHealing.CompareTo(a.totalHealing));
        return entries;
    }

    /// <summary>
    /// Records one activation of an ability or proc source (Bladestorm channel, Power Slash queue, etc.).
    /// </summary>
    public void RecordOutgoingSourceUse(string sourceName)
    {
        if (_dpsTrackerPaused || string.IsNullOrWhiteSpace(sourceName))
            return;
        if (!OutgoingSourceTracksUses(sourceName))
            return;

        MarkRecentCombatActivity();
        EnsureDpsSessionStarted();
        AddOutgoingSourceUse(sourceName.Trim());
    }

    /// <summary>Auto attacks, ailments, and minions only show hit counts on the damage meter.</summary>
    public static bool OutgoingSourceTracksUses(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return false;

        string label = sourceName.Trim();
        if (string.Equals(label, "Auto Attack", System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(label, OutgoingBleedingSourceLabel, System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(label, OutgoingPoisonSourceLabel, System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(label, OutgoingShockSourceLabel, System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(label, OutgoingBurningSourceLabel, System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(label, DefaultMinionOutgoingSourceLabel, System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (IsMinionOutgoingSource(label))
            return false;

        return true;
    }

    /// <summary>Hard reset of current DPS/damage tracker values (Damage Meter Reset button only).</summary>
    public void ResetDpsTrackerNow()
    {
        _dpsTrackerPaused = false;
        _pausedDpsSessionDuration = 0f;
        ResetDpsSession();
        _lastCombatActivityTime = Time.time;
    }

    public bool IsDpsTrackerRunning() =>
        !_dpsTrackerPaused && _combatSessionStartTime >= 0f;

    /// <summary>
    /// Starts or resumes the damage-meter session. Used by the meter play button when elapsed is 0
    /// or after the player unpauses a frozen session.
    /// </summary>
    public void StartDpsTrackerSession()
    {
        if (_dpsTrackerPaused)
        {
            UnpauseDpsTracker();
            return;
        }

        if (_combatSessionStartTime < 0f)
        {
            _combatSessionStartTime = Time.time;
            _pausedDpsSessionDuration = 0f;
        }
    }

    public void RecordIncomingHealingForDps(float healingAmount, string sourceName)
    {
        if (_dpsTrackerPaused || healingAmount <= 0f)
            return;

        string label = string.IsNullOrWhiteSpace(sourceName) ? GenericHealingSourceLabel : sourceName.Trim();
        EnsureDpsSessionStarted();
        _incomingHealingSum += healingAmount;
        AddIncomingHealingSource(label, healingAmount);
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

        duration = Mathf.Max(0.001f, Time.time - _combatSessionStartTime);
        return duration > 0f;
    }

    private EquipmentManager _equipment;
    private Inventory _inventory;

    private const float CombatChaseRetargetEpsilon = 0.04f;

    private void Awake()
    {
        if (!playerCol) playerCol = GetComponent<Collider2D>();
        _equipment = GetComponent<EquipmentManager>();
        _inventory = GetComponent<Inventory>();
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
        TickWayOfTheBerserkerCapstone();
        TickWayOfTheCrusaderCapstone();
        TickWayOfTheBladeDancerCapstone();
        TickBloodbathStacks();
        if (_dpsTrackerPaused) return;

        if (IsProximityCombatEngaged())
            _lastCombatActivityTime = Time.time;

        if (IsCombatEngaged() && _combatSessionStartTime < 0f)
            _combatSessionStartTime = Time.time;

        if (idleCombatEnabled)
        {
            TickAutoConsumables();
            TickAutoAbilities();
            TickIdleCombatTargeting();
            TickIdleAutoPickup();
        }

        if (PlayerAbilityController.BlocksCombatActions)
            return;

        if (_target == null)
            return;

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

        float myRange = GetEffectiveMeleeReach();

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
        bool inAttackRange = gap <= closeEnoughToSwing;
        if (inAttackRange)
            _attackBufferedFromRange = true;
        else
            _attackBufferedFromRange = false;

        if (!retaliationEnabled && _target != null)
        {
            // Only suspend after the player manually disengages chase — not when combat pathing
            // briefly leaves range while closing on a clicked target.
            if (_wasInAttackRangeWithTarget && !inAttackRange && !_combatChaseMovementEnabled)
                _suspendAutoAttackUntilReengage = true;

            if (_suspendAutoAttackUntilReengage)
            {
                _isClosingDistanceForAttack = false;
                _wasInAttackRangeWithTarget = inAttackRange;
                player.ClearActionOverride();
                if (player.IsManualKeyboardSteering)
                    player.StopMoveOnly();
                return;
            }
        }

        _wasInAttackRangeWithTarget = inAttackRange;

        float desiredCenterDist = myRange + myHalf + enemyHalf;
        float dxToEnemy = enemyX - myX;
        if (Mathf.Abs(dxToEnemy) > 0.08f)
            _stableCombatSideSign = Mathf.Sign(dxToEnemy);
        else if (Mathf.Approximately(_stableCombatSideSign, 0f))
            _stableCombatSideSign = player.FacingDirectionX < 0f ? -1f : 1f;

        float desiredX = enemyX - _stableCombatSideSign * desiredCenterDist;
        bool shouldStartClosing = gap > closeEnoughToSwing;
        bool shouldKeepClosing = _isClosingDistanceForAttack && gap > closeEnoughToSwing;
        bool shouldCloseDistance = !_attackBufferedFromRange && (shouldStartClosing || shouldKeepClosing);

        if (shouldCloseDistance && _combatChaseMovementEnabled)
        {
            _isClosingDistanceForAttack = true;

            player.ClearActionOverride();
            bool bladeDancerDashed = !inAttackRange
                && TryBladeDancerDashWhileClosingToTarget(_target);

            if (!player.IsPlayerSteeringMovement && !bladeDancerDashed)
            {
                if (!player.IsCombatMoveTargetNear(desiredX, CombatChaseRetargetEpsilon))
                    player.MoveToPointX_Combat(desiredX);
            }
            else if (player.IsManualKeyboardSteering)
                player.StopMoveOnly();
            return;
        }

        _isClosingDistanceForAttack = false;
        if (player.IsManualKeyboardSteering)
            player.StopMoveOnly();
        else if (player.IsPlayerSteeringMovement)
        {
            // Click-to-move repositioning — keep MoveToPoint without combat overriding it.
        }
        else if (kiteAtRangeEdge && _combatChaseMovementEnabled)
        {
            if (!player.IsCombatMoveTargetNear(desiredX, CombatChaseRetargetEpsilon))
                player.MoveToPointX_Combat(desiredX);
        }
        else
            player.StopMoveOnly();

        if (faceTargetWhenAttacking && inAttackRange)
            player.FaceTargetX(enemyX);

        if (!inAttackRange)
        {
            player.ClearActionOverride();
            return;
        }

        if (player.IsPlayerMovingAwayFromCombatTarget())
        {
            player.ClearActionOverride();
            return;
        }

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
        {
            abilityController.TryConsumeQueuedAttackModifier(ref rolled);
            abilityController.ApplyActiveDamageConversions(ref rolled);
        }

        SwingOutgoingAttribution swingAttribution = abilityController != null
            ? abilityController.BuildSwingOutgoingAttribution(preQueuedModifier, rolled)
            : SwingOutgoingAttribution.AutoAttackOnly;

        TryPrepareWayOfTheCrusaderExtraFireOnAutoAttack(rolled, swingAttribution);

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
            .OrderBy(slot => slot.SlotIndex)
            .ToList();

        if (orderedSlots.Count <= 0)
            return;

        int nonFlameSlots = 0;
        for (int i = 0; i < orderedSlots.Count; i++)
        {
            if (!IsFlameChargeActionBarSlot(orderedSlots[i]))
                nonFlameSlots++;
        }

        if (abilityController.TryGetForcedAutoBattleAbilityId(out string forcedAbilityId) &&
            !string.IsNullOrWhiteSpace(forcedAbilityId))
        {
            for (int i = 0; i < orderedSlots.Count; i++)
            {
                var forcedSlot = orderedSlots[i];
                var forcedAction = forcedSlot.AssignedAction;
                if (forcedAction == null || !forcedAction.IsAssigned || !forcedAction.IsAbility)
                    continue;
                if (CombatStarterAttackAbility.IsCombatStarterAttackId(forcedAction.id))
                    continue;
                if (!string.Equals(forcedAction.id, forcedAbilityId, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!forcedSlot.CanAccept(forcedAction))
                    return;

                bool forcedUsed = abilityController.TryUseAbility(
                    forcedAction.id,
                    showLockedFeedback: false,
                    allowSoulforgedRecastWhileActive: false,
                    requireCrescentSlashTargetInFacingLane: true,
                    requireWhirlwindTargetInRadius: true,
                    requireGuardiansHammerTargetInFacingZone: true);

                if (forcedUsed)
                    _autoBattleAbilityRoundRobinIndex = i;
                return;
            }
        }

        int start = (_autoBattleAbilityRoundRobinIndex + 1 + orderedSlots.Count) % orderedSlots.Count;
        for (int attempt = 0; attempt < orderedSlots.Count; attempt++)
        {
            int idx = (start + attempt) % orderedSlots.Count;
            var slot = orderedSlots[idx];
            var action = slot.AssignedAction;
            if (action == null || !action.IsAssigned || !action.IsAbility)
                continue;
            if (CombatStarterAttackAbility.IsCombatStarterAttackId(action.id))
                continue;

            if (!slot.CanAccept(action))
                continue;

            if (_autoBattleDeferExtraFlameCharge && IsFlameChargeActionBarSlot(slot))
                continue;

            bool used = abilityController.TryUseAbility(
                action.id,
                showLockedFeedback: false,
                allowSoulforgedRecastWhileActive: false,
                requireCrescentSlashTargetInFacingLane: true,
                requireWhirlwindTargetInRadius: true,
                requireGuardiansHammerTargetInFacingZone: true);

            _autoBattleAbilityRoundRobinIndex = idx;

            if (_autoBattleDeferExtraFlameCharge && !IsFlameChargeActionBarSlot(slot))
            {
                _autoBattleNonFlameSlotsBeforeNextFlameCharge--;
                if (_autoBattleNonFlameSlotsBeforeNextFlameCharge <= 0)
                    _autoBattleDeferExtraFlameCharge = false;
            }

            if (!used)
                continue;

            if (IsFlameChargeActionBarSlot(slot))
            {
                int readyCharges = abilityController.GetAbilityStackCountDisplay(AbilityCombatPower.FlameChargeAbilityId);
                if (readyCharges > 0 && nonFlameSlots > 0)
                {
                    _autoBattleDeferExtraFlameCharge = true;
                    _autoBattleNonFlameSlotsBeforeNextFlameCharge = nonFlameSlots;
                }
                else
                    _autoBattleDeferExtraFlameCharge = false;
            }

            break;
        }
    }

    private static bool IsFlameChargeActionBarSlot(ActionBarSlotUI slot)
    {
        if (slot?.AssignedAction == null || !slot.AssignedAction.IsAbility)
            return false;

        return string.Equals(
            slot.AssignedAction.id,
            AbilityCombatPower.FlameChargeAbilityId,
            System.StringComparison.OrdinalIgnoreCase);
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

        EquipmentManager equipment = _equipment != null ? _equipment : GetComponent<EquipmentManager>();
        Inventory inventory = _inventory != null ? _inventory : GetComponent<Inventory>();

        if (equipment == null || inventory == null) return null;
        if (string.IsNullOrWhiteSpace(equipment.MainHandItemId)) return null;

        return inventory.GetItemDef(equipment.MainHandItemId);
    }

    private bool IsSupportEquippedWithoutCompatibleMainWeapon(out string message)
    {
        message = null;
        EquipmentManager equipment = _equipment != null ? _equipment : GetComponent<EquipmentManager>();
        Inventory inv = _inventory != null ? _inventory : GetComponent<Inventory>();
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
        HandleRangedAttackInternal(targetAtFireTime, rolled, wasCrit, swingAttribution, consumeAmmo: true);
    }

    /// <summary>
    /// Bonus ranged shot (animation + projectile). Triple Shot phantom arrows skip off-hand ammo consumption.
    /// </summary>
    public void FireBonusRangedAttackShot(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution,
        bool consumeAmmo)
    {
        if (targetAtFireTime == null || targetAtFireTime.IsDead || rolled.IsEmpty || player == null)
            return;

        player.TriggerAttackAnim();
        HandleRangedAttackInternal(targetAtFireTime, rolled, wasCrit, swingAttribution, consumeAmmo);
    }

    private void HandleRangedAttackInternal(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution,
        bool consumeAmmo)
    {
        float fireDelay = Mathf.Max(0f, rangedProjectileFireDelay);
        if (fireDelay <= 0f)
        {
            ResolveRangedAttackAtRelease(targetAtFireTime, rolled, wasCrit, swingAttribution, consumeAmmo);
            return;
        }

        StartCoroutine(ResolveRangedAttackAfterFireDelay(targetAtFireTime, rolled, wasCrit, fireDelay, swingAttribution, consumeAmmo));
    }

    private System.Collections.IEnumerator ResolveRangedAttackAfterFireDelay(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        float fireDelay,
        SwingOutgoingAttribution swingAttribution,
        bool consumeAmmo = true)
    {
        yield return new WaitForSeconds(fireDelay);
        ResolveRangedAttackAtRelease(targetAtFireTime, rolled, wasCrit, swingAttribution, consumeAmmo);
    }

    private void ResolveRangedAttackAtRelease(
        EnemyBaseController targetAtFireTime,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution,
        bool consumeAmmo = true)
    {
        if (targetAtFireTime == null || targetAtFireTime.IsDead)
            return;

        float delay = Mathf.Max(0f, rangedDamageDelayOffset);
        bool spawnedProjectile = TrySpawnRangedProjectile(targetAtFireTime, out float travelTime);
        if (spawnedProjectile)
            delay += travelTime;

        if (delay <= 0f)
        {
            ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit, swingAttribution, consumeAmmo: consumeAmmo);
            return;
        }

        StartCoroutine(ResolveAttackHitAfterDelay(targetAtFireTime, rolled, wasCrit, delay, swingAttribution, consumeAmmo: consumeAmmo));
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
        SwingOutgoingAttribution swingAttribution,
        bool suppressOnHitAilments = false,
        bool consumeAmmo = true)
    {
        yield return new WaitForSeconds(delay);
        ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit, swingAttribution, suppressOnHitAilments, consumeAmmo);
    }

    /// <summary>
    /// Melee Lv10 Parry — roll on incoming enemy hit; default reduces damage and reflects a portion;
    /// Riposte enhancement performs a free auto attack without advancing swing cadence.
    /// </summary>
    public bool TryProcessParryOnEnemyHit(EnemyBaseController attacker, ref SplitDamage incomingHit, bool incomingWasCrit)
    {
        if (attacker == null || stats == null || player == null || incomingHit.IsEmpty)
            return false;

        if (!stats.IsParryMajorPassiveActive())
            return false;

        if (!IsAttackerWithinParryRange(attacker))
            return false;

        if (UnityEngine.Random.value >= stats.GetParryChanceFraction())
            return false;

        if (stats.GetParryEnhancementPick() == 0 && Time.time < _nextParryRiposteAt)
            return false;

        SpawnParrySlashVfx(attacker);
        ShowParryDamagePopup(attacker != null ? attacker.transform : null);

        if (stats.GetParryEnhancementPick() == 0)
        {
            TryPerformParryRiposteAttack(attacker);
            _nextParryRiposteAt = Time.time + AbilityCombatPower.ParryRiposteCooldownSeconds;
            return true;
        }

        SplitDamage original = incomingHit;
        float reflectFrac = stats.GetParryMitigationFraction();
        SplitDamage reflected = BuildParryReflectDamage(original, reflectFrac);
        incomingHit = original * (1f - reflectFrac);
        float parryMitigated = Mathf.Max(0f, original.Total - incomingHit.Total);
        if (parryMitigated > 0f)
            RecordIncomingMitigationForDps(new DpsMitigationBreakdown { Parry = parryMitigated });
        ApplyParryReflectDamage(attacker, reflected, incomingWasCrit);
        return true;
    }

    private void ShowParryDamagePopup(Transform attacker)
    {
        if (player == null || DamagePopupSystem.Instance == null)
            return;

        var anchor = player.GetComponentInChildren<DamagePopupAnchor>(true);
        Vector3 anchorPos = anchor ? anchor.WorldPos : player.transform.position;
        Vector3 pos;
        Vector3 dir;
        if (attacker != null && DamagePopupSystem.Instance != null)
        {
            pos = DamagePopupSystem.Instance.ResolveLingeringStatusWorldPos(
                player.transform,
                anchorPos,
                attacker.position,
                true,
                player.FacingDirectionX);
            dir = Vector3.up;
        }
        else
        {
            player.GetIncomingDamagePopupPlacement(anchorPos, attacker, 0.35f, out pos, out dir);
        }

        bool riposteLabel = stats != null && stats.GetParryEnhancementPick() == 0;
        DamagePopupSystem.Instance.SpawnParry(pos, dir, riposteLabel, transform);
    }

    private bool TryPerformParryRiposteAttack(EnemyBaseController attacker)
    {
        if (attacker == null || attacker.IsDead || stats == null || player == null)
            return false;

        if (IsRangedAttack() || IsMagicAttack())
            return false;

        SplitDamage rolled = stats.RollSplitAttackDamage(out bool wasCrit);
        SplitDamage preQueuedModifier = rolled;

        if (abilityController != null)
        {
            abilityController.TryConsumeQueuedAttackModifier(ref rolled);
            abilityController.ApplyActiveDamageConversions(ref rolled);
        }

        SwingOutgoingAttribution swingAttribution = abilityController != null
            ? abilityController.BuildSwingOutgoingAttribution(preQueuedModifier, rolled)
            : SwingOutgoingAttribution.AutoAttackOnly;

        if (rolled.IsEmpty)
            return false;

        player.TriggerAttackAnim();

        float meleeDelay = Mathf.Max(0f, meleeHitImpactDelay);
        if (meleeDelay <= 0f)
            ResolveAttackHitNow(attacker, rolled, wasCrit, swingAttribution);
        else
            StartCoroutine(ResolveAttackHitAfterDelay(attacker, rolled, wasCrit, meleeDelay, swingAttribution));

        return true;
    }

    private static SplitDamage BuildParryReflectDamage(SplitDamage original, float reflectFrac)
    {
        float Lane(float amount) =>
            amount > 0f ? Mathf.Max(1f, Mathf.Ceil(amount * reflectFrac - 1e-6f)) : 0f;

        return new SplitDamage(
            Lane(original.physical),
            Lane(original.magic),
            Lane(original.corruptionDamage));
    }

    private void ApplyParryReflectDamage(EnemyBaseController attacker, SplitDamage reflected, bool wasCrit)
    {
        if (attacker == null || attacker.IsDead || reflected.IsEmpty)
            return;

        var swingAttribution = new SwingOutgoingAttribution(AbilityCombatPower.ParryReflectOutgoingSourceLabel, null, 0f);
        ApplySplitDamageToTarget(attacker, reflected, wasCrit, AbilityCombatPower.ParryReflectOutgoingSourceLabel, swingAttribution);
    }

    private bool IsAttackerWithinParryRange(EnemyBaseController enemy)
    {
        if (enemy == null || player == null)
            return false;

        Vector3 origin = player.transform.position;
        float radius = AbilityCombatPower.ParryMeleeRange;
        float dx = Mathf.Abs(enemy.transform.position.x - origin.x);
        float dy = Mathf.Abs(enemy.transform.position.y - origin.y);
        return dx <= radius && dy <= radius;
    }

    private void SpawnParrySlashVfx(EnemyBaseController attacker)
    {
        if (attacker == null || player == null)
            return;

        PlayerAbilityVfxController vfx = ResolveAbilityVfx();
        if (vfx == null)
            return;

        vfx.SpawnParrySlashLine(player.transform.position, attacker.transform.position);
    }

    private PlayerAbilityVfxController _abilityVfxCached;

    private PlayerAbilityVfxController ResolveAbilityVfx()
    {
        if (_abilityVfxCached != null)
            return _abilityVfxCached;

        if (abilityController != null)
            _abilityVfxCached = abilityController.GetComponent<PlayerAbilityVfxController>();

        if (_abilityVfxCached == null && player != null)
            _abilityVfxCached = player.GetComponent<PlayerAbilityVfxController>();

        return _abilityVfxCached;
    }

    private void ResolveAttackHitNow(
        EnemyBaseController targetToHit,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution = default,
        bool suppressOnHitAilments = false,
        bool consumeAmmo = true)
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
            TryAddWayOfTheBerserkerStackOnAutoAttack(swingAttribution);

        TryApplyPendingWayOfTheCrusaderExtraFireDamage(targetToHit, wasCrit);

        if (totalDealt > 0f && consumeAmmo)
            TryConsumeOffHandSupportAmmo();

        if (abilityController != null && totalDealt > 0f &&
            swingAttribution.AttributesEntireSwingToBonusSource)
        {
            abilityController.TryGrantBattleEngineEnergyForPendingAbilityHit();
        }

        bool suppressBleed = false;
        bool suppressPoison = false;
        bool suppressElementalMagicAilment = false;
        bool triggerCrescentSlash = false;
        bool crescentAppliesElemental = false;
        bool crescentPenetrating = false;
        if (abilityController != null)
        {
            var queued = abilityController.ConsumeQueuedHitEffects(
                targetToHit,
                dealt.physical,
                dealt.corruptionDamage,
                dealt.magic);
            suppressBleed = queued.suppressDefaultBleed;
            suppressPoison = queued.suppressDefaultPoison;
            suppressElementalMagicAilment = queued.suppressDefaultElementalMagicAilment;
            triggerCrescentSlash = queued.triggerCrescentSlash;
            crescentAppliesElemental = queued.crescentAppliesElemental;
            crescentPenetrating = queued.crescentPenetrating;
        }

        if (!suppressOnHitAilments && stats != null && stats.CanApplyOutgoingAilmentsOnHit())
        {
            if (!suppressBleed)
                TryApplyBleed(targetToHit, dealt);
            if (!suppressPoison)
                TryApplyPoison(targetToHit, dealt);
            if (!suppressElementalMagicAilment)
                TryApplyElementalMagicAilment(targetToHit, dealt);
            TryApplyMeleeShock(targetToHit, dealt);
        }

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

        if (primaryHitSucceeded && stats != null)
        {
            TryApplySecondarySpecialistDualWieldFollowUp(
                targetToHit,
                rolled,
                wasCrit,
                swingAttribution,
                suppressOnHitAilments,
                suppressBleed,
                suppressPoison,
                suppressElementalMagicAilment);
            TryApplyBladeDancerTripleHitFollowUp(
                targetToHit,
                rolled,
                wasCrit,
                swingAttribution,
                suppressOnHitAilments,
                suppressBleed,
                suppressPoison,
                suppressElementalMagicAilment);
        }
    }

    /// <summary>
    /// Secondary Specialist (dual wield): every Nth successful melee hit applies a follow-up strike.
    /// </summary>
    public void TryApplySecondarySpecialistDualWieldFollowUp(
        EnemyBaseController targetToHit,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution = default,
        bool suppressOnHitAilments = false,
        bool suppressBleed = false,
        bool suppressPoison = false,
        bool suppressElementalMagicAilment = false)
    {
        if (targetToHit == null || stats == null || rolled.IsEmpty)
            return;

        if (!stats.TryConsumeSecondarySpecialistDualWieldDoubleHit())
            return;

        float followUpFraction = AbilityCombatPower.TacticianSecondarySpecialistDualWieldFollowUpDamageFraction;
        SplitDamage followUpHit = stats.RollSplitAttackDamage(out bool followUpWasCrit) * followUpFraction;
        if (followUpHit.IsEmpty)
            return;

        var doubleHitAttribution = new SwingOutgoingAttribution(
            AbilityCombatPower.TacticianSecondarySpecialistDoubleHitSourceLabel,
            null,
            0f);
        DamageResult doubleDealt = ApplySplitDamageToTarget(
            targetToHit,
            followUpHit,
            followUpWasCrit,
            AbilityCombatPower.TacticianSecondarySpecialistDoubleHitSourceLabel,
            doubleHitAttribution);

        if (doubleDealt.Total > 0f)
        {
            RecordOutgoingSourceUse(AbilityCombatPower.TacticianSecondarySpecialistDoubleHitSourceLabel);
            player.ApplyLifeSteal(doubleDealt.Total);
        }

        if (!suppressOnHitAilments && stats != null && stats.CanApplyOutgoingAilmentsOnHit())
        {
            if (!suppressBleed)
                TryApplyBleed(targetToHit, doubleDealt);
            if (!suppressPoison)
                TryApplyPoison(targetToHit, doubleDealt);
            if (!suppressElementalMagicAilment)
                TryApplyElementalMagicAilment(targetToHit, doubleDealt);
            TryApplyMeleeShock(targetToHit, doubleDealt);
        }

        stats.TryApplyTacticianStunOnEnemyHit(targetToHit);
    }

    private void ApplyCleaveSecondaryHits(EnemyBaseController primaryTarget, int extraTargets, HashSet<EnemyBaseController> alreadyHit)
    {
        if (extraTargets <= 0 || stats == null || primaryTarget == null)
            return;

        float cleaveRadius = AbilityCombatPower.CleavingStrikesCleaveRadiusFromAnchor + rangePadding;

        float myX = transform.position.x;
        float myY = transform.position.y;
        float myHalf = HalfWidthX(playerCol);
        float primaryX = primaryTarget.transform.position.x;
        Collider2D primaryCol = primaryTarget.GetComponent<Collider2D>();
        if (primaryCol == null)
            primaryCol = primaryTarget.GetComponentInChildren<Collider2D>();
        float primaryHalf = HalfWidthX(primaryCol);
        float yTol = Mathf.Max(0.85f, cleaveRadius * 0.4f);

        IReadOnlyList<EnemyBaseController> candidates = CombatEnemyRegistry.GetLiveEnemies();
        _cleaveCandidateScratch.Clear();

        for (int i = 0; i < candidates.Count; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!IsValidSecondaryTarget(e, alreadyHit))
                continue;

            if (Mathf.Abs(e.transform.position.y - myY) > yTol)
                continue;

            Collider2D enemyCol = e.GetComponent<Collider2D>();

            float enemyHalf = HalfWidthX(enemyCol);
            float gapFromPlayer = EdgeGapX(myX, e.transform.position.x, myHalf, enemyHalf);
            float gapFromPrimary = EdgeGapX(primaryX, e.transform.position.x, primaryHalf, enemyHalf);
            float gap = Mathf.Min(gapFromPlayer, gapFromPrimary);
            if (gapFromPlayer > cleaveRadius && gapFromPrimary > cleaveRadius)
                continue;

            _cleaveCandidateScratch.Add((e, gap));
        }

        int count = Mathf.Min(extraTargets, _cleaveCandidateScratch.Count);
        float critMult = Mathf.Max(1f, stats.CritMultiplier);
        for (int i = 0; i < count; i++)
        {
            int bestIdx = 0;
            float bestGap = _cleaveCandidateScratch[0].gap;
            for (int j = 1; j < _cleaveCandidateScratch.Count; j++)
            {
                float gap = _cleaveCandidateScratch[j].gap;
                if (gap < bestGap)
                {
                    bestGap = gap;
                    bestIdx = j;
                }
            }

            EnemyBaseController e = _cleaveCandidateScratch[bestIdx].enemy;
            _cleaveCandidateScratch.RemoveAt(bestIdx);

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

        IReadOnlyList<EnemyBaseController> candidates = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < candidates.Count; i++)
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
        int stackCount = Mathf.Max(1, payload.maxStacks);
        PoisonPayload spreadPayload = BuildFullDurationPoisonSpreadPayload(payload);

        CollectEnemiesInAilmentSpreadRadius(originEnemy, _ailmentSpreadScratch);
        for (int i = 0; i < _ailmentSpreadScratch.Count; i++)
        {
            AilmentController ac = _ailmentSpreadScratch[i].GetComponent<AilmentController>();
            if (ac == null)
                continue;

            ac.ApplyPoisonStacksFromHit(spreadPayload, stackCount);
        }
    }

    private PoisonPayload BuildFullDurationPoisonSpreadPayload(PoisonPayload source)
    {
        if (stats == null)
            return source;

        float duration = Mathf.Max(0.1f, stats.PoisonDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        int sourceTicks = Mathf.Max(1, source.ticks);
        int tickDamagePerStack = Mathf.Max(1, Mathf.CeilToInt(source.totalDamage / sourceTicks));
        float totalPerStack = tickDamagePerStack * ticks;

        return new PoisonPayload(
            totalPerStack,
            duration,
            ticks,
            source.maxStacks,
            source.source,
            source.outgoingDpsSourceLabel,
            source.outgoingAttributeToMinion,
            source.poisonMasteryOwner);
    }

    private void ApplyCrescentSlashSecondaryHits(EnemyBaseController primaryTarget, bool penetrating, bool applyElemental, HashSet<EnemyBaseController> alreadyHit)
    {
        if (stats == null)
            return;

        float reach = AbilityCombatPower.CrescentSlashReach;
        float forward = player != null ? Mathf.Sign(player.transform.localScale.x >= 0f ? 1f : -1f) : 1f;
        Vector3 origin = transform.position;
        IReadOnlyList<EnemyBaseController> candidates = CombatEnemyRegistry.GetLiveEnemies();
        var forwardHits = new List<(EnemyBaseController enemy, float dist)>(candidates.Count);

        for (int i = 0; i < candidates.Count; i++)
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
        if (stats != null && stats.CanApplyOutgoingAilmentsOnHit())
        {
            TryApplyBleed(target, dealt);
            TryApplyPoison(target, dealt);
            TryApplyElementalMagicAilment(target, dealt, forceElementalAilment);
            TryApplyMeleeShock(target, dealt);
        }
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
        if (!enabled)
        {
            _autoBattleDeferExtraFlameCharge = false;
            _autoBattleNonFlameSlotsBeforeNextFlameCharge = 0;
            _autoBattleAbilityRoundRobinIndex = -1;
            PlayerAbilityController.EndAutoBattleWhirlwindChannelIfActive();
        }

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
        if (PlayerAbilityController.SuppressesIdleCombatTargeting)
            return;

        // Manual disengage: do not let idle retargeting re-acquire/repath into melee
        // until the player explicitly re-engages (attack click/ability).
        if (!retaliationEnabled && (!_combatChaseMovementEnabled || _suspendAutoAttackUntilReengage))
            return;

        if (Time.time < _nextIdleScanTime) return;
        _nextIdleScanTime = Time.time + Mathf.Max(0.05f, idleRescanInterval);

        EnemyBaseController current = (_target != null && !_target.IsDead && _target.gameObject.activeInHierarchy &&
                                       IsValidCombatTarget(_target))
            ? _target
            : null;

        if (_target != null && current == null)
            ClearTargetInternal();

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

    /// <summary>Recent enemy that damaged the player (for ailment status popup placement).</summary>
    public bool TryGetRecentIncomingDamageDealerWorld(float maxAgeSeconds, out Vector3 dealerWorld)
    {
        dealerWorld = default;
        if (_lastEnemyThatDamagedPlayer == null)
            return false;

        if (Time.time - _lastEnemyThatDamagedPlayerTime > Mathf.Max(0.1f, maxAgeSeconds))
            return false;

        if (_lastEnemyThatDamagedPlayer.IsDead || !_lastEnemyThatDamagedPlayer.gameObject.activeInHierarchy)
            return false;

        dealerWorld = _lastEnemyThatDamagedPlayer.transform.position;
        return true;
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

        if (!IsEnemyWithinAttackRange(e))
            return null;

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

        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        if (enemies == null || enemies.Count == 0) return null;

        float bestDist = -1f;
        EnemyBaseController best = null;

        float myX = transform.position.x;

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!e) continue;
            if (e.IsDead) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (!IsValidCombatTarget(e)) continue;

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
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        if (enemies == null || enemies.Count == 0) return null;

        float bestDist = float.MaxValue;
        EnemyBaseController best = null;

        float myX = transform.position.x;

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!e) continue;
            if (e.IsDead) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (!IsValidCombatTarget(e)) continue;

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
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        if (enemies == null || enemies.Count == 0) return null;

        float bestDist = -1f;
        EnemyBaseController best = null;

        float myX = transform.position.x;

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!e) continue;
            if (e.IsDead) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (!IsValidCombatTarget(e)) continue;

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
        if (!IsValidCombatTarget(enemy))
        {
            ClearTarget();
            return;
        }

        if (_target == enemy)
            return;

        SetTargetInternal(enemy);
    }

    /// <summary>
    /// Mouse or interact hotkey on an enemy: set/refresh target and resume auto-chase into attack range
    /// (re-engages the same target after manual repositioning).
    /// </summary>
    public void EngageTargetFromPlayerInput(EnemyBaseController enemy)
    {
        if (!IsValidCombatTarget(enemy))
        {
            ClearTarget();
            return;
        }

        NotifyExplicitCombatEngage();

        if (!player)
            player = GetComponent<PlayerController>();

        player?.PrepareForCombatEngageInput();

        if (_target == enemy)
        {
            _isClosingDistanceForAttack = false;
            _attackBufferedFromRange = false;
            _combatChaseMovementEnabled = true;
            OnTargetChanged?.Invoke();
            return;
        }

        SetTargetInternal(enemy);
    }

    /// <summary>
    /// Sets combat target only when none is active (abilities with Sets Target On Hit should not retarget mid-fight).
    /// </summary>
    public void SetTargetIfNone(EnemyBaseController enemy)
    {
        if (!IsValidCombatTarget(enemy))
            return;

        if (_target != null && !_target.IsDead && _target.gameObject.activeInHierarchy)
            return;

        SetTarget(enemy);
    }

    private bool _combatChaseMovementEnabled = true;

    /// <summary>
    /// Player moved manually (keyboard, click-to-move, etc.) while a combat target exists — keep target but stop auto-chase and pause auto-attack until re-engage.
    /// </summary>
    public void NotifyPlayerInitiatedMovement()
    {
        _combatChaseMovementEnabled = false;
        if (!retaliationEnabled && _target != null)
            _suspendAutoAttackUntilReengage = true;
    }

    /// <summary>Resume auto-attack/chase after the player explicitly uses Attack or a combat ability.</summary>
    public void NotifyExplicitCombatEngage()
    {
        _suspendAutoAttackUntilReengage = false;
        _attackBufferedFromRange = false;
        _combatChaseMovementEnabled = true;
    }

    /// <summary>Large position snaps (map travel, ability teleports, scene spawn) clear the current combat target.</summary>
    public void NotifyPlayerTeleported()
    {
        ClearTarget();
    }

    public static bool IsValidCombatTargetForPlayer(EnemyBaseController enemy, PlayerController owner)
    {
        if (!enemy || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            return false;

        if (!owner)
            return true;

        Transform enemyTransform = enemy.transform;
        Transform ownerTransform = owner.transform;

        if (enemyTransform == ownerTransform)
            return false;

        if (ownerTransform.IsChildOf(enemyTransform) || enemyTransform.IsChildOf(ownerTransform))
            return false;

        if (enemy.GetComponent<PlayerController>() != null)
            return false;

        if (enemy.GetComponentInParent<PlayerController>() != null)
            return false;

        return true;
    }

    private static bool IsValidCombatTarget(EnemyBaseController enemy, PlayerController owner)
    {
        return IsValidCombatTargetForPlayer(enemy, owner);
    }

    private bool IsValidCombatTarget(EnemyBaseController enemy)
    {
        if (!player)
            player = GetComponent<PlayerController>();
        return IsValidCombatTarget(enemy, player);
    }

    private void SetTargetInternal(EnemyBaseController enemy)
    {
        if (!IsValidCombatTarget(enemy))
        {
            ClearTargetInternal();
            return;
        }

        EnemyBaseController previous = _target;
        _target = enemy;
        _isClosingDistanceForAttack = false;
        _attackBufferedFromRange = false;
        _suspendAutoAttackUntilReengage = false;
        _wasInAttackRangeWithTarget = false;
        _targetColCached = null;
        _combatChaseMovementEnabled = true;
        TryConsumeBladeDancerDashOnNewTarget(previous, enemy);
        OnTargetChanged?.Invoke();
    }

    public void ClearTarget()
    {
        _isClosingDistanceForAttack = false;
        _attackBufferedFromRange = false;
        _suspendAutoAttackUntilReengage = false;
        _wasInAttackRangeWithTarget = false;
        ClearTargetInternal();
    }

    private void ClearTargetInternal()
    {
        _target = null;
        _targetColCached = null;
        _combatChaseMovementEnabled = true;
        _stableCombatSideSign = 0f;
        _suspendAutoAttackUntilReengage = false;
        _wasInAttackRangeWithTarget = false;

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

        if (TryWayOfTheSlayerExecute(target, out DamageResult executeResult))
        {
            if (target.IsDead)
                NotifyBladeDancerKillCritBuff();
            return executeResult;
        }

        float conditionalDamageMult = GetConditionalMeleeDamageMultiplier(target);
        if (wasCrit && stats != null)
        {
            conditionalDamageMult *= stats.GetPredatorsInstinctExecutionerCritDamageFactor(target, true);
            stats.OnPlayerCritLanded();
        }

        bool isProcFollowUp = IsOutgoingProcFollowUpLabel(outgoingDamageSourceLabel);
        bool isPlayerWeaponSwing = string.IsNullOrWhiteSpace(outgoingDamageSourceLabel);
        bool deferSwingOutgoing = (isPlayerWeaponSwing &&
            (swingAttribution.HasBonus || HasAilmentConditionalDamageBonusOnTarget(target)))
            || isProcFollowUp;
        string sourceLabel = deferSwingOutgoing
            ? DeferredSwingOutgoingDpsLabel
            : ResolveOutgoingDamageSourceLabel(outgoingDamageSourceLabel, swingAttribution);

        float armorRatingMultiplier = stats != null ? stats.GetTacticianOutgoingArmorRatingMultiplier() : 1f;

        if (rolled.physical > 0f)
        {
            int dealt = target.TakeDamage(
                Mathf.RoundToInt(rolled.physical * conditionalDamageMult),
                DamageType.Physical,
                wasCrit,
                player.transform,
                stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null,
                dpsBucketOverride: null,
                armorRatingMultiplier: armorRatingMultiplier,
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
        {
            if (isProcFollowUp)
                RecordProcFollowUpOutgoingDamage(result.Total, outgoingDamageSourceLabel);
            else
                RecordWeaponSwingOutgoingDamage(result.Total, target, swingAttribution);
        }

        if (target.IsDead)
            NotifyBladeDancerKillCritBuff();

        if (stats != null && result.physical > 0f)
            stats.TryApplyTacticianStunOnEnemyHit(target);

        return result;
    }

    private bool HasAilmentConditionalDamageBonusOnTarget(EnemyBaseController target)
    {
        if (stats == null || target == null)
            return false;

        GetConditionalMeleeDamageMultiplierBreakdown(
            target,
            out _,
            out float bleedBonus,
            out float poisonBonus,
            out float shockBonus,
            out float burnBonus,
            out float ailmentedBonus);
        return bleedBonus > 0f || poisonBonus > 0f || shockBonus > 0f || burnBonus > 0f || ailmentedBonus > 0f;
    }

    private float GetConditionalMeleeDamageMultiplier(EnemyBaseController target)
    {
        GetConditionalMeleeDamageMultiplierBreakdown(
            target,
            out float lowHpBonus,
            out float bleedBonus,
            out float poisonBonus,
            out float shockBonus,
            out float burnBonus,
            out float ailmentedBonus);
        return 1f + Mathf.Max(0f, lowHpBonus + bleedBonus + poisonBonus + shockBonus + burnBonus + ailmentedBonus);
    }

    private void GetConditionalMeleeDamageMultiplierBreakdown(
        EnemyBaseController target,
        out float lowHpBonus,
        out float bleedBonus,
        out float poisonBonus,
        out float shockBonus,
        out float burnBonus,
        out float ailmentedBonus)
    {
        lowHpBonus = 0f;
        bleedBonus = 0f;
        poisonBonus = 0f;
        shockBonus = 0f;
        burnBonus = 0f;
        ailmentedBonus = 0f;

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
            if (ailments.HasBurn)
                burnBonus = Mathf.Max(0f, stats.MeleeDamageVsBurning);
            if (ailments.HasBleed || ailments.HasPoison || ailments.HasBurn)
                ailmentedBonus = Mathf.Max(0f, stats.MeleeDamageVsAilmented);
        }

        CharacterStats targetStats = target.GetComponent<CharacterStats>();
        if (targetStats != null && targetStats.MaxHP > 0f)
        {
            float hp01 = targetStats.HP / Mathf.Max(1f, targetStats.MaxHP);
            if (hp01 < stats.MeleeLowHpThreshold01)
                lowHpBonus = Mathf.Max(0f, stats.MeleeDamageVsLowHp);
        }
    }

    private void TryApplyBleed(EnemyBaseController target, DamageResult dealt)
    {
        if (target == null) return;
        if (dealt.physical <= 0f) return;
        if (stats.GetEffectiveBleedChanceForProcs() <= 0f) return;

        if (Random.value > stats.GetEffectiveBleedChanceForProcs())
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
            transform,
            maxStacks: stats.BleedMaxStacks);

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

        CharacterStats victimStats = target.Stats;
        float poisonMult = stats.GetPoisonMultiplierAgainst(victimStats);
        float totalPoisonDamage =
            poisonSourceDamage * stats.PoisonPoolFractionOfCorruptionDamage * (1f + poisonMult);
        if (totalPoisonDamage <= 0f) return;

        float duration = Mathf.Max(0.1f, stats.PoisonDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        int maxStacks = Mathf.Max(1, stats.PoisonMaxStacks);

        var payload = new PoisonPayload(
            totalPoisonDamage,
            duration,
            ticks,
            maxStacks,
            transform,
            poisonMasteryOwner: transform);

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

        float fireDealt = stats.ResolveFireDamageFromDealt(dealt.magic, dealt.physical);
        if (fireDealt > 0f)
        {
            stats.TryApplyBurnFromDealtHit(ailments, dealt.magic, dealt.physical, transform);
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
        AwardCombatXp(damageDealt, bucket, grantXp, outgoingDamageSourceLabel, 1f);
    }

    public void AwardCombatXp(
        float damageDealt,
        DpsDamageBucket? bucket,
        bool grantXp,
        string outgoingDamageSourceLabel,
        float mapScalingXpRateMultiplier)
    {
        if (damageDealt <= 0f)
            return;

        RecordDamageForDps(damageDealt, bucket, outgoingDamageSourceLabel);

        if (!grantXp || xpPerDamage <= 0f)
            return;

        var sm = SkillsManager.Instance;
        if (sm == null)
            return;

        float xpRate = xpPerDamage * Mathf.Max(1f, mapScalingXpRateMultiplier);
        float xpAmount = damageDealt * xpRate;
        SkillType skill = sm.GetCombatSkillFromCurrentWeapon(player, stats);
        sm.AddXpFloat(skill, xpAmount, combatXpSource);

        float enduranceShare = Mathf.Clamp01(enduranceXpFromDamageDealtFraction);
        if (enduranceShare > 0f)
            sm.AddXpFloat(SkillType.Endurance, xpAmount * enduranceShare, enduranceOffenceXpSource);
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

    public void RecordIncomingMitigationForDps(in DpsMitigationBreakdown mitigation)
    {
        if (mitigation.Total <= 0f || _dpsTrackerPaused)
            return;

        MarkRecentCombatActivity();
        EnsureDpsSessionStarted();
        _incomingMitigationSum.Add(mitigation);
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

    /// <summary>Melee proc follow-ups (Blade Dancer bonus strike, Secondary Specialist double hit) — never mixed into Auto Attack.</summary>
    private static bool IsOutgoingProcFollowUpLabel(string outgoingDamageSourceLabel)
    {
        if (string.IsNullOrWhiteSpace(outgoingDamageSourceLabel))
            return false;

        string label = outgoingDamageSourceLabel.Trim();
        return string.Equals(label, AbilityCombatPower.WayOfTheBladeDancerTripleHitSourceLabel, System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(label, AbilityCombatPower.TacticianSecondarySpecialistDoubleHitSourceLabel, System.StringComparison.OrdinalIgnoreCase);
    }

    private void RecordProcFollowUpOutgoingDamage(float totalDealt, string procSourceLabel)
    {
        if (totalDealt <= 0f || _dpsTrackerPaused || string.IsNullOrWhiteSpace(procSourceLabel))
            return;

        RecordDamageForDps(totalDealt, DpsDamageBucket.Physical, procSourceLabel.Trim());
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
            RecordDamageForDps(totalDealt, swingAttribution.bonusBucket ?? DpsDamageBucket.Physical, swingAttribution.bonusSource);
            return;
        }

        GetConditionalMeleeDamageMultiplierBreakdown(
            target,
            out float lowHpBonus,
            out float bleedBonus,
            out float poisonBonus,
            out float shockBonus,
            out float burnBonus,
            out float ailmentedBonus);

        float totalMult = 1f + lowHpBonus + bleedBonus + poisonBonus + shockBonus + burnBonus + ailmentedBonus;
        if (totalMult <= 1e-6f)
            totalMult = 1f;

        float powerSlashAmount = swingAttribution.HasBonus ? totalDealt * swingAttribution.bonusFraction : 0f;
        float afterPowerSlash = totalDealt - powerSlashAmount;

        float bleedAmount = bleedBonus > 0f ? afterPowerSlash * (bleedBonus / totalMult) : 0f;
        float poisonAmount = poisonBonus > 0f ? afterPowerSlash * (poisonBonus / totalMult) : 0f;
        float shockAmount = shockBonus > 0f ? afterPowerSlash * (shockBonus / totalMult) : 0f;
        float burnAmount = burnBonus > 0f ? afterPowerSlash * (burnBonus / totalMult) : 0f;
        float autoAmount = afterPowerSlash - bleedAmount - poisonAmount - shockAmount - burnAmount;

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
        if (burnAmount > 0f)
            RecordDamageForDps(burnAmount, DpsDamageBucket.Burn, OutgoingBurningSourceLabel);
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
        if (string.Equals(outgoingDamageSourceLabel, OutgoingBurningSourceLabel, System.StringComparison.OrdinalIgnoreCase))
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
    public const string OutgoingBurningSourceLabel = "Burning";
    public const string OutgoingShockSourceLabel = "Shock";
    public const string DefaultMinionOutgoingSourceLabel = "Soulforged Weapon";
    public const string HpRegenHealingSourceLabel = "HP Regen";
    public const string PotionHealingSourceLabel = "Potion Healing";
    public const string FoodHealingSourceLabel = "Food Healing";
    public const string CrusaderStrikeHealingSourceLabel = "Crusader Strike Heal";
    public const string LeechHealingSourceLabel = "Life Steal";
    public const string PhoenixSoulHealingSourceLabel = "Phoenix Soul";
    public const string AbilityHealthRefundHealingSourceLabel = "Ability Health Refund";
    public const string GenericHealingSourceLabel = "Healing";
    public const string WarBannerTriumphantRallyHealingSourceLabel = "Triumphant Rally";

    /// <summary>
    /// Green +HP floating text for every labeled heal except natural HP regen ticks
    /// (<see cref="HpRegenHealingSourceLabel"/>) and life steal (<see cref="LeechHealingSourceLabel"/>).
    /// Pass a stable label from <see cref="CharacterStats.Heal"/>.
    /// </summary>
    public static bool ShouldShowHealingPopupForSource(string sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(sourceLabel))
            return false;

        return !string.Equals(sourceLabel, HpRegenHealingSourceLabel, System.StringComparison.OrdinalIgnoreCase)
               && !string.Equals(sourceLabel, LeechHealingSourceLabel, System.StringComparison.OrdinalIgnoreCase);
    }

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
            DpsDamageBucket.Burn => OutgoingBurningSourceLabel,
            DpsDamageBucket.Minion => DefaultMinionOutgoingSourceLabel,
            _ => null
        };
    }

    private void AddOutgoingSourceDamage(string sourceName, float amount)
    {
        if (amount <= 0f || string.IsNullOrWhiteSpace(sourceName))
            return;

        string key = sourceName.Trim();
        if (_outgoingSourceCounters.TryGetValue(key, out OutgoingSourceCounter counter))
        {
            counter.totalDamage += amount;
            counter.hitCount++;
            _outgoingSourceCounters[key] = counter;
            return;
        }

        _outgoingSourceCounters[key] = new OutgoingSourceCounter
        {
            totalDamage = amount,
            hitCount = 1,
            useCount = 0
        };
        _outgoingSourceOrder.Add(key);
    }

    private void AddOutgoingSourceUse(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return;

        string key = sourceName.Trim();
        if (_outgoingSourceCounters.TryGetValue(key, out OutgoingSourceCounter counter))
        {
            counter.useCount++;
            _outgoingSourceCounters[key] = counter;
            return;
        }

        _outgoingSourceCounters[key] = new OutgoingSourceCounter
        {
            totalDamage = 0f,
            hitCount = 0,
            useCount = 1
        };
        _outgoingSourceOrder.Add(key);
    }

    private void AddIncomingHealingSource(string sourceName, float amount)
    {
        if (amount <= 0f || string.IsNullOrWhiteSpace(sourceName))
            return;

        string key = sourceName.Trim();
        if (_incomingHealingBySource.TryGetValue(key, out float total))
        {
            _incomingHealingBySource[key] = total + amount;
            return;
        }

        _incomingHealingBySource[key] = amount;
        _incomingHealingSourceOrder.Add(key);
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

    /// <summary>Restores DPS timing after death was prevented (e.g. Phoenix Soul — Ashen Rebirth).</summary>
    public void UnpauseDpsTracker()
    {
        if (!_dpsTrackerPaused)
            return;

        _dpsTrackerPaused = false;
        if (_combatSessionStartTime >= 0f)
            _combatSessionStartTime = Time.time - _pausedDpsSessionDuration;
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
        _incomingMitigationSum = default;
        _incomingHealingSum = 0f;
        _incomingDamageByDealer.Clear();
        _incomingDealerOrder.Clear();
        _incomingHealingBySource.Clear();
        _incomingHealingSourceOrder.Clear();
        _outgoingSourceCounters.Clear();
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