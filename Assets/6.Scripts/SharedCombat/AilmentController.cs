using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class AilmentController : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool showDotPopups = true;
    [SerializeField] private bool debugLogs = false;

    [Header("Burn")]
    public const float DefaultBurnTickIntervalSeconds = 3f;
    public const float DefaultBurnWallClockDurationSeconds = 15f;

    [Tooltip("Wall-clock seconds burn keeps ticking each refresh. Faster tick intervals add ticks within this window; combust is unchanged.")]
    [SerializeField, Min(1f)] private float burnWallClockDurationSeconds = DefaultBurnWallClockDurationSeconds;
    [Tooltip("Legacy prefab field (× 3s default interval). Ignored when Burn Wall Clock Duration Seconds is set.")]
    [SerializeField, Min(1)] private int burnDurationTicks = 5;
    [Tooltip("Each burn tick deals this fraction of the applying fire hit (min 1/tick), before Burn Damage mult.")]
    [SerializeField, Range(0.01f, 1f)]
    [FormerlySerializedAs("burnTotalDamageFractionOfHit")]
    private float burnDamageFractionOfHitPerTick = 0.15f;
    [Tooltip("Combust burst = current tick damage × this count (10× strongest tick; unchanged by tick interval).")]
    [SerializeField, Min(1)] private int burnCombustTicksWorth = 10;
    private const int BurnMaxStacks = 3;

    private CharacterStats characterStats;
    private EnemyBaseController enemy;
    private PlayerBuffController playerBuffs;

    private Coroutine bleedRoutine;
    private Coroutine exclusiveBleedRoutine;
    private Coroutine poisonRoutine;
    private Coroutine chillRoutine;
    private Coroutine shockRoutine;

    private readonly List<int> bleedTickSchedule = new();
    private readonly List<int> exclusiveBleedTickSchedule = new();
    private readonly List<PoisonStack> poisonStacks = new();
    // Learned from the latest poison payload source (typically player stats PoisonMaxStacks).
    private int poisonBaseMaxStacks = 1;
    private int temporaryPoisonMaxStackBonus;
    private float temporaryPoisonMaxStackBonusExpiresAt = -1f;
    private readonly List<float> chillExpireTimes = new();
    private float chillSlowPerStack = 0.15f;
    private int burnStackCount;
    private int burnDamagePerTick;
    private float burnExpiresAt = -1f;
    private float burnLastApplicationAt = -1f;
    private float _burnTickWaitSeconds = DefaultBurnTickIntervalSeconds;
    private Transform burnDotSource;
    private string _bleedDotDealerLabel = "";
    private string _exclusiveBleedDotDealerLabel = "";
    private CharacterStats _poisonOwnerPlayerStats;
    private bool _poisonNeurotoxinActive;
    private string _poisonDotDealerLabel = "";
    private string _burnDotDealerLabel = "";
    private string _bleedOutgoingDpsSourceLabel;
    private string _exclusiveBleedOutgoingDpsSourceLabel;
    private string _poisonOutgoingDpsSourceLabel;
    private string _burnOutgoingDpsSourceLabel;
    private bool _bleedOutgoingAttributeToMinion;
    private bool _exclusiveBleedOutgoingAttributeToMinion;
    private bool _poisonOutgoingAttributeToMinion;
    private bool _burnOutgoingAttributeToMinion;
    private Coroutine burnTickRoutine;
    private float shockExpireTime = -1f;
    private float shockDamageTakenMultiplier = 0f;

    /// <summary>Last known dealer world position for floating DoT when the dealer Transform is destroyed (e.g. enemy died).</summary>
    private Vector3 _poisonDotDealerWorldPos;
    private bool _hasPoisonDotDealerWorldPos;
    private Vector3 _bleedDotDealerWorldPos;
    private bool _hasBleedDotDealerWorldPos;
    private Vector3 _exclusiveBleedDotDealerWorldPos;
    private bool _hasExclusiveBleedDotDealerWorldPos;
    private Vector3 _burnDotDealerWorldPos;
    private bool _hasBurnDotDealerWorldPos;
    private Vector3 _shockDotDealerWorldPos;
    private bool _hasShockDotDealerWorldPos;
    private Vector3 _chillDotDealerWorldPos;
    private bool _hasChillDotDealerWorldPos;

    public event System.Action OnAilmentsChanged;

    public bool HasBleed => bleedTickSchedule.Count > 0 || bleedRoutine != null || exclusiveBleedTickSchedule.Count > 0 || exclusiveBleedRoutine != null;
    public bool HasPoison => poisonStacks.Count > 0 || poisonRoutine != null;

    public int BleedStacks => HasBleed ? 1 : 0;
    public int PoisonStacks => poisonStacks.Count;

    public bool HasBurn => burnStackCount > 0;
    public bool HasChill => chillExpireTimes.Count > 0 || chillRoutine != null;
    public bool HasShock => shockDamageTakenMultiplier > 0f && Time.time < shockExpireTime;
    public int BurnStacks => burnStackCount;
    public int BurnHitsToExplode => BurnMaxStacks;
    public int ChillStacks => chillExpireTimes.Count;
    public float ChillSlowPercent => Mathf.Clamp01(GetChillSlowMultiplier()) * 100f;

    /// <summary>Damage the next bleed tick will deal (sum of normal + exclusive bleed). Bleed ticks every 1s, so this is also damage/second.</summary>
    public int BleedDamagePerSecond
    {
        get
        {
            int dps = 0;
            if (bleedTickSchedule.Count > 0) dps += Mathf.Max(0, bleedTickSchedule[0]);
            if (exclusiveBleedTickSchedule.Count > 0) dps += Mathf.Max(0, exclusiveBleedTickSchedule[0]);
            return dps;
        }
    }

    /// <summary>Total damage all active poison stacks will deal on the next tick (poison ticks every 1s).</summary>
    public int PoisonDamagePerSecond
    {
        get
        {
            int dps = 0;
            for (int i = 0; i < poisonStacks.Count; i++)
            {
                PoisonStack s = poisonStacks[i];
                if (s != null && s.ticksRemaining > 0)
                    dps += Mathf.Max(1, s.tickDamage);
            }
            return dps;
        }
    }

    /// <summary>Damage burn will deal each tick (burn ticks every 1s).</summary>
    public int BurnDamagePerSecond => HasBurn ? Mathf.Max(0, burnDamagePerTick) : 0;

    /// <summary>Extra incoming damage % currently applied by shock (e.g. 25 means +25% damage taken).</summary>
    public float ShockDamageTakenBonusPercent => HasShock ? Mathf.Max(0f, shockDamageTakenMultiplier) * 100f : 0f;

    [System.Serializable]
    private class PoisonStack
    {
        public int tickDamage;
        public int ticksRemaining;

        public PoisonStack(int tickDamage, int ticksRemaining)
        {
            this.tickDamage = tickDamage;
            this.ticksRemaining = ticksRemaining;
        }
    }

    private void Awake()
    {
        characterStats = GetComponent<CharacterStats>();
        if (characterStats == null)
            characterStats = GetComponentInParent<CharacterStats>();

        enemy = GetComponent<EnemyBaseController>();
        if (enemy == null)
            enemy = GetComponentInParent<EnemyBaseController>();

        playerBuffs = GetComponent<PlayerBuffController>();
        if (playerBuffs == null)
            playerBuffs = GetComponentInParent<PlayerBuffController>();
    }

    private bool IsPlayerVictim => playerBuffs != null;

    private void TrySpawnEnemyAilmentActivationPopup(string message, Color color, Transform source)
    {
        if (IsPlayerVictim || DamagePopupSystem.Instance == null || string.IsNullOrWhiteSpace(message))
            return;

        DamagePopupAnchor anchor = GetComponentInChildren<DamagePopupAnchor>(true);
        Vector3 anchorPos = anchor != null ? anchor.WorldPos : transform.position;
        Vector3 dealerPos = source != null ? source.position : transform.position;
        Vector3 pos = DamagePopupSystem.GetWorldPosBehindVictim(anchorPos, dealerPos);
        DamagePopupSystem.Instance.SpawnLingeringStatus(pos, message, color);
    }

    private static Color ResolveAilmentStatusColor(FloatingDamageTextUI prefab, System.Func<FloatingDamageTextUI, Color> pick, Color fallback)
    {
        if (prefab == null)
            return fallback;
        return pick(prefab);
    }

    /// <summary>World position of whoever applied this ailment (for player overhead status popups).</summary>
    public bool TryGetStatusPopupDealerWorld(string statusMessage, out Vector3 dealerWorld)
    {
        dealerWorld = default;
        if (string.IsNullOrWhiteSpace(statusMessage))
            return false;

        if (string.Equals(statusMessage, "Poisoned", System.StringComparison.OrdinalIgnoreCase) &&
            _hasPoisonDotDealerWorldPos)
        {
            dealerWorld = _poisonDotDealerWorldPos;
            return true;
        }

        if (string.Equals(statusMessage, "bleeding", System.StringComparison.OrdinalIgnoreCase))
        {
            if (_hasBleedDotDealerWorldPos)
            {
                dealerWorld = _bleedDotDealerWorldPos;
                return true;
            }

            if (_hasExclusiveBleedDotDealerWorldPos)
            {
                dealerWorld = _exclusiveBleedDotDealerWorldPos;
                return true;
            }
        }

        if (string.Equals(statusMessage, "Burnt", System.StringComparison.OrdinalIgnoreCase) &&
            _hasBurnDotDealerWorldPos)
        {
            dealerWorld = _burnDotDealerWorldPos;
            return true;
        }

        if (string.Equals(statusMessage, "Shocked", System.StringComparison.OrdinalIgnoreCase) &&
            _hasShockDotDealerWorldPos)
        {
            dealerWorld = _shockDotDealerWorldPos;
            return true;
        }

        if (string.Equals(statusMessage, "Chilled", System.StringComparison.OrdinalIgnoreCase) &&
            _hasChillDotDealerWorldPos)
        {
            dealerWorld = _chillDotDealerWorldPos;
            return true;
        }

        return false;
    }

    private void ClearDotDealerWorldCaches()
    {
        _hasPoisonDotDealerWorldPos = false;
        _hasBleedDotDealerWorldPos = false;
        _hasExclusiveBleedDotDealerWorldPos = false;
        _hasBurnDotDealerWorldPos = false;
        _hasShockDotDealerWorldPos = false;
        _hasChillDotDealerWorldPos = false;
    }

    private static void CacheAilmentStatusDealerWorld(Transform source, ref Vector3 worldPos, ref bool hasWorldPos)
    {
        if (source == null)
            return;

        worldPos = source.position;
        hasWorldPos = true;
    }

    private void OnDisable()
    {
        ClearAllAilments();
    }

    public void ClearAllAilments()
    {
        if (bleedRoutine != null) StopCoroutine(bleedRoutine);
        if (poisonRoutine != null) StopCoroutine(poisonRoutine);
        if (chillRoutine != null) StopCoroutine(chillRoutine);
        if (shockRoutine != null) StopCoroutine(shockRoutine);
        if (burnTickRoutine != null) StopCoroutine(burnTickRoutine);

        bleedRoutine = null;
        poisonRoutine = null;
        chillRoutine = null;
        shockRoutine = null;
        burnTickRoutine = null;

        bleedTickSchedule.Clear();
        poisonStacks.Clear();
        poisonBaseMaxStacks = 1;
        _poisonOwnerPlayerStats = null;
        _poisonNeurotoxinActive = false;
        chillExpireTimes.Clear();
        burnStackCount = 0;
        burnDamagePerTick = 0;
        burnExpiresAt = -1f;
        burnLastApplicationAt = -1f;
        burnTickRoutine = null;
        burnDotSource = null;
        _bleedDotDealerLabel = "";
        _exclusiveBleedDotDealerLabel = "";
        _poisonDotDealerLabel = "";
        _burnDotDealerLabel = "";
        shockExpireTime = -1f;
        shockDamageTakenMultiplier = 0f;
        ClearDotDealerWorldCaches();

        OnAilmentsChanged?.Invoke();
    }

    public bool ClearBleed()
    {
        bool hadBleed = HasBleed;

        if (bleedRoutine != null)
            StopCoroutine(bleedRoutine);
        if (exclusiveBleedRoutine != null)
            StopCoroutine(exclusiveBleedRoutine);

        bleedRoutine = null;
        exclusiveBleedRoutine = null;
        bleedTickSchedule.Clear();
        exclusiveBleedTickSchedule.Clear();
        _bleedDotDealerLabel = "";
        _exclusiveBleedDotDealerLabel = "";
        _hasBleedDotDealerWorldPos = false;
        _hasExclusiveBleedDotDealerWorldPos = false;

        if (hadBleed)
            OnAilmentsChanged?.Invoke();

        return hadBleed;
    }

    public bool ClearPoison()
    {
        bool hadPoison = HasPoison;

        if (poisonRoutine != null)
            StopCoroutine(poisonRoutine);

        poisonRoutine = null;
        poisonStacks.Clear();
        poisonBaseMaxStacks = 1;
        _poisonOwnerPlayerStats = null;
        _poisonNeurotoxinActive = false;
        _poisonDotDealerLabel = "";
        _hasPoisonDotDealerWorldPos = false;

        if (hadPoison)
            OnAilmentsChanged?.Invoke();

        return hadPoison;
    }

    /// <summary>
    /// Temporarily increases the effective poison stack cap on this target.
    /// Reapplying refreshes/extends the duration if it would last longer.
    /// </summary>
    public void GrantTemporaryPoisonMaxStacksBonus(int bonusStacks, float durationSeconds)
    {
        if (bonusStacks <= 0 || durationSeconds <= 0f)
            return;

        temporaryPoisonMaxStackBonus = Mathf.Max(temporaryPoisonMaxStackBonus, bonusStacks);
        temporaryPoisonMaxStackBonusExpiresAt = Mathf.Max(temporaryPoisonMaxStackBonusExpiresAt, Time.time + durationSeconds);
    }

    /// <summary>
    /// Returns base cap plus any active temporary stack-cap bonus.
    /// </summary>
    public int GetEffectivePoisonMaxStacks(int baseMaxStacks)
    {
        int activeBonus = IsTemporaryPoisonCapBonusActive() ? temporaryPoisonMaxStackBonus : 0;
        return Mathf.Max(1, baseMaxStacks + Mathf.Max(0, activeBonus));
    }

    public void ApplyBleedFromHit(BleedPayload payload)
    {
        if (IsDead()) return;
        if (payload.totalDamage <= 0f) return;

        if (playerBuffs != null && playerBuffs.IsBleedImmune)
        {
            if (debugLogs)
                Debug.Log("[Ailments] Bleed prevented by immunity.", this);
            return;
        }

        // While exclusive bleed is active, block normal bleed applications.
        if (exclusiveBleedTickSchedule.Count > 0 || exclusiveBleedRoutine != null)
            return;

        int tickCount = Mathf.Max(1, payload.ticks);
        int newBleedTick = Mathf.Max(1, Mathf.CeilToInt(payload.totalDamage / tickCount));

        _bleedDotDealerLabel = ResolveDotDealerLabelForDps(payload.source);
        _bleedOutgoingDpsSourceLabel = payload.outgoingDpsSourceLabel;
        _bleedOutgoingAttributeToMinion = payload.outgoingAttributeToMinion;
        if (payload.source != null)
        {
            _bleedDotDealerWorldPos = payload.source.position;
            _hasBleedDotDealerWorldPos = true;
        }

        bool firstBleed = bleedTickSchedule.Count == 0 && exclusiveBleedTickSchedule.Count == 0;

        RefreshBleedSchedule(newBleedTick, tickCount);

        if (bleedTickSchedule.Count > 0 && bleedRoutine == null)
            bleedRoutine = StartCoroutine(BleedRoutine(payload.source));

        if (firstBleed && bleedTickSchedule.Count > 0)
        {
            FloatingDamageTextUI fx = DamagePopupSystem.Instance != null ? DamagePopupSystem.Instance.PopupPrefab : null;
            Color c = ResolveAilmentStatusColor(fx, f => f.BleedDamageColor, new Color32(170, 35, 35, 255));
            TrySpawnEnemyAilmentActivationPopup("Bleeding", c, payload.source);
        }

        OnAilmentsChanged?.Invoke();
    }

    /// <summary>
    /// Applies a separate "exclusive" bleed channel.
    /// While any exclusive bleed ticks remain, normal bleeds are blocked from being applied.
    /// Exclusive bleed does not override/refresh the normal bleed schedule.
    /// </summary>
    public void ApplyExclusiveBleedFromHit(BleedPayload payload)
    {
        if (IsDead()) return;
        if (payload.totalDamage <= 0f) return;

        if (playerBuffs != null && playerBuffs.IsBleedImmune)
            return;

        int tickCount = Mathf.Max(1, payload.ticks);
        int newTick = Mathf.Max(1, Mathf.CeilToInt(payload.totalDamage / tickCount));

        _exclusiveBleedDotDealerLabel = ResolveDotDealerLabelForDps(payload.source);
        _exclusiveBleedOutgoingDpsSourceLabel = payload.outgoingDpsSourceLabel;
        _exclusiveBleedOutgoingAttributeToMinion = payload.outgoingAttributeToMinion;
        if (payload.source != null)
        {
            _exclusiveBleedDotDealerWorldPos = payload.source.position;
            _hasExclusiveBleedDotDealerWorldPos = true;
        }

        RefreshExclusiveBleedSchedule(newTick, tickCount);

        if (exclusiveBleedTickSchedule.Count > 0 && exclusiveBleedRoutine == null)
            exclusiveBleedRoutine = StartCoroutine(ExclusiveBleedRoutine(payload.source));

        OnAilmentsChanged?.Invoke();
    }

    private void RefreshBleedSchedule(int newTickDamage, int tickCount)
    {
        while (bleedTickSchedule.Count > tickCount)
            bleedTickSchedule.RemoveAt(bleedTickSchedule.Count - 1);

        while (bleedTickSchedule.Count < tickCount)
            bleedTickSchedule.Add(0);

        for (int i = 0; i < tickCount; i++)
            bleedTickSchedule[i] = Mathf.Max(bleedTickSchedule[i], newTickDamage);
    }

    private void RefreshExclusiveBleedSchedule(int newTickDamage, int tickCount)
    {
        while (exclusiveBleedTickSchedule.Count > tickCount)
            exclusiveBleedTickSchedule.RemoveAt(exclusiveBleedTickSchedule.Count - 1);

        while (exclusiveBleedTickSchedule.Count < tickCount)
            exclusiveBleedTickSchedule.Add(0);

        for (int i = 0; i < tickCount; i++)
            exclusiveBleedTickSchedule[i] = Mathf.Max(exclusiveBleedTickSchedule[i], newTickDamage);
    }

    private static string ResolveDotDealerLabelForDps(Transform source)
    {
        string s = PlayerCombatController.ResolveIncomingDamageDealerDisplayName(source);
        return string.IsNullOrWhiteSpace(s)
            ? PlayerCombatController.IncomingDotDamageDealerFallback
            : s.Trim();
    }

    private IEnumerator BleedRoutine(Transform source)
    {
        while (!IsDead())
        {
            if (bleedTickSchedule.Count == 0)
                break;

            yield return new WaitForSeconds(1f);

            if (IsDead())
                yield break;

            if (bleedTickSchedule.Count == 0)
                continue;

            int tickDamage = bleedTickSchedule[0];
            bleedTickSchedule.RemoveAt(0);

            if (source != null)
            {
                _bleedDotDealerWorldPos = source.position;
                _hasBleedDotDealerWorldPos = true;
            }

            ApplyBleedTick(tickDamage, source, _bleedDotDealerLabel, exclusiveChannel: false, _bleedOutgoingDpsSourceLabel, _bleedOutgoingAttributeToMinion);
            OnAilmentsChanged?.Invoke();
        }

        bleedRoutine = null;
        OnAilmentsChanged?.Invoke();
    }

    private IEnumerator ExclusiveBleedRoutine(Transform source)
    {
        while (!IsDead())
        {
            if (exclusiveBleedTickSchedule.Count == 0)
                break;

            yield return new WaitForSeconds(1f);

            if (IsDead())
                yield break;

            if (exclusiveBleedTickSchedule.Count == 0)
                continue;

            int tickDamage = exclusiveBleedTickSchedule[0];
            exclusiveBleedTickSchedule.RemoveAt(0);

            if (source != null)
            {
                _exclusiveBleedDotDealerWorldPos = source.position;
                _hasExclusiveBleedDotDealerWorldPos = true;
            }

            ApplyBleedTick(tickDamage, source, _exclusiveBleedDotDealerLabel, exclusiveChannel: true, _exclusiveBleedOutgoingDpsSourceLabel, _exclusiveBleedOutgoingAttributeToMinion);
            OnAilmentsChanged?.Invoke();
        }

        exclusiveBleedRoutine = null;
        OnAilmentsChanged?.Invoke();
    }

    private void ApplyBleedTick(
        int damage,
        Transform source,
        string dealerLabelForDps,
        bool exclusiveChannel,
        string outgoingDpsSourceLabel,
        bool outgoingAttributeToMinion)
    {
        Vector3? dealerWorldFallback = exclusiveChannel
            ? (_hasExclusiveBleedDotDealerWorldPos ? (Vector3?)_exclusiveBleedDotDealerWorldPos : null)
            : (_hasBleedDotDealerWorldPos ? (Vector3?)_bleedDotDealerWorldPos : null);

        ApplyDotDamage(
            damage,
            FloatingDamageTextUI.PopupDamageKind.Bleed,
            source,
            dealerLabelForDps,
            dealerWorldFallback,
            outgoingDpsSourceLabel,
            outgoingAttributeToMinion);

        if (debugLogs)
            Debug.Log($"[Ailments] Bleed tick: {damage}", this);
    }

    public void ApplyPoisonFromHit(PoisonPayload payload)
    {
        if (IsDead()) return;
        if (payload.totalDamage <= 0f) return;

        if (playerBuffs != null && playerBuffs.IsPoisonImmune)
        {
            if (debugLogs)
                Debug.Log("[Ailments] Poison prevented by immunity.", this);
            return;
        }

        int ticks = Mathf.Max(1, payload.ticks);
        int tickDamage = Mathf.Max(1, Mathf.CeilToInt(payload.totalDamage / ticks));
        poisonBaseMaxStacks = Mathf.Max(1, payload.maxStacks);
        int maxStacks = GetEffectivePoisonMaxStacks(poisonBaseMaxStacks);

        _poisonOwnerPlayerStats = CharacterStats.ResolvePoisonOwnerPlayerStats(payload.poisonMasteryOwner);
        _poisonNeurotoxinActive = _poisonOwnerPlayerStats != null &&
                                  _poisonOwnerPlayerStats.GetMasterOfVenomsEnhancementPick() == 0;

        int stacksAfterApply = Mathf.Min(maxStacks, poisonStacks.Count + 1);
        if (_poisonOwnerPlayerStats != null)
        {
            int tickReduction = _poisonOwnerPlayerStats.GetMasterOfVenomsLethalCompoundTickReduction(stacksAfterApply);
            ticks = Mathf.Max(1, ticks - tickReduction);
            tickDamage = Mathf.Max(1, Mathf.CeilToInt(payload.totalDamage / ticks));
        }

        _poisonDotDealerLabel = ResolveDotDealerLabelForDps(payload.source);
        _poisonOutgoingDpsSourceLabel = payload.outgoingDpsSourceLabel;
        _poisonOutgoingAttributeToMinion = payload.outgoingAttributeToMinion;

        if (payload.source != null)
        {
            _poisonDotDealerWorldPos = payload.source.position;
            _hasPoisonDotDealerWorldPos = true;
        }

        while (poisonStacks.Count >= maxStacks)
            poisonStacks.RemoveAt(0);

        bool firstPoison = poisonStacks.Count == 0;
        poisonStacks.Add(new PoisonStack(tickDamage, ticks));

        if (poisonRoutine == null)
            poisonRoutine = StartCoroutine(PoisonRoutine(payload.source));

        if (firstPoison)
        {
            FloatingDamageTextUI fx = DamagePopupSystem.Instance != null ? DamagePopupSystem.Instance.PopupPrefab : null;
            Color c = ResolveAilmentStatusColor(fx, f => f.PoisonDamageColor, new Color32(85, 200, 90, 255));
            TrySpawnEnemyAilmentActivationPopup("Poisoned", c, payload.source);
        }

        OnAilmentsChanged?.Invoke();
    }

    private IEnumerator PoisonRoutine(Transform source)
    {
        while (!IsDead())
        {
            CleanupExpiredTemporaryPoisonCapBonus();
            TrimPoisonStacksToCurrentCap();

            if (poisonStacks.Count == 0)
                break;

            yield return new WaitForSeconds(1f);

            if (IsDead())
                yield break;

            int totalDamage = 0;

            for (int i = poisonStacks.Count - 1; i >= 0; i--)
            {
                PoisonStack stack = poisonStacks[i];
                if (stack == null)
                {
                    poisonStacks.RemoveAt(i);
                    continue;
                }

                totalDamage += Mathf.Max(1, stack.tickDamage);
                stack.ticksRemaining--;

                if (stack.ticksRemaining <= 0)
                    poisonStacks.RemoveAt(i);
            }

            if (totalDamage > 0)
            {
                if (source != null)
                {
                    _poisonDotDealerWorldPos = source.position;
                    _hasPoisonDotDealerWorldPos = true;
                }

                ApplyPoisonTick(totalDamage, source);
            }

            OnAilmentsChanged?.Invoke();
        }

        poisonRoutine = null;
        OnAilmentsChanged?.Invoke();
    }

    private bool IsTemporaryPoisonCapBonusActive()
    {
        return temporaryPoisonMaxStackBonus > 0 && Time.time < temporaryPoisonMaxStackBonusExpiresAt;
    }

    private void CleanupExpiredTemporaryPoisonCapBonus()
    {
        if (temporaryPoisonMaxStackBonus <= 0)
            return;
        if (Time.time < temporaryPoisonMaxStackBonusExpiresAt)
            return;

        temporaryPoisonMaxStackBonus = 0;
        temporaryPoisonMaxStackBonusExpiresAt = -1f;
    }

    private void TrimPoisonStacksToCurrentCap()
    {
        // The "base" system cap on this project comes from payload.maxStacks from normal applications (typically stats.PoisonMaxStacks).
        // When temporary cap expires, trim oldest stacks back down naturally.
        int cap = GetEffectivePoisonMaxStacks(poisonBaseMaxStacks);
        bool trimmed = false;
        while (poisonStacks.Count > cap)
        {
            poisonStacks.RemoveAt(0);
            trimmed = true;
        }

        if (trimmed)
            OnAilmentsChanged?.Invoke();
    }

    private void ApplyPoisonTick(int damage, Transform source)
    {
        Vector3? dealerWorldFallback = _hasPoisonDotDealerWorldPos ? (Vector3?)_poisonDotDealerWorldPos : null;

        int finalDamage = damage;
        bool wasCrit = false;
        FloatingDamageTextUI.PopupDamageKind popupKind = FloatingDamageTextUI.PopupDamageKind.Poison;

        if (_poisonOwnerPlayerStats != null && _poisonOwnerPlayerStats.MasterOfVenomsPoisonCanCriticallyStrike())
        {
            if (Random.value < Mathf.Clamp01(_poisonOwnerPlayerStats.CritChance))
            {
                wasCrit = true;
                finalDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(damage * _poisonOwnerPlayerStats.GetMasterOfVenomsPoisonCritDamageMultiplier()));
                popupKind = FloatingDamageTextUI.PopupDamageKind.PoisonCrit;
            }
        }

        ApplyDotDamage(
            finalDamage,
            popupKind,
            source,
            _poisonDotDealerLabel,
            dealerWorldFallback,
            _poisonOutgoingDpsSourceLabel,
            _poisonOutgoingAttributeToMinion,
            wasCrit);

        if (debugLogs)
            Debug.Log($"[Ailments] Poison tick: {finalDamage} crit={wasCrit}", this);
    }

    public void ApplyChillFromHit(ChillPayload payload)
    {
        if (IsDead()) return;

        CacheAilmentStatusDealerWorld(payload.source, ref _chillDotDealerWorldPos, ref _hasChillDotDealerWorldPos);

        float duration = Mathf.Max(0.1f, payload.duration);
        int maxStacks = Mathf.Max(1, payload.maxStacks);
        chillSlowPerStack = Mathf.Clamp01(payload.slowPerStack);

        float now = Time.time;
        PruneExpiredChillStacks(now);

        bool firstChill = chillExpireTimes.Count == 0;

        if (chillExpireTimes.Count >= maxStacks)
            chillExpireTimes.RemoveAt(0);

        chillExpireTimes.Add(now + duration);

        // Refresh behavior: when a new chill stack is applied, all active chill stacks
        // get a fresh full duration window.
        float refreshedExpire = now + duration;
        for (int i = 0; i < chillExpireTimes.Count; i++)
            chillExpireTimes[i] = refreshedExpire;

        if (chillRoutine == null)
            chillRoutine = StartCoroutine(ChillRoutine());

        if (firstChill)
        {
            FloatingDamageTextUI fx = DamagePopupSystem.Instance != null ? DamagePopupSystem.Instance.PopupPrefab : null;
            Color c = ResolveAilmentStatusColor(fx, f => f.ChillPresentationColor, new Color32(90, 160, 255, 255));
            TrySpawnEnemyAilmentActivationPopup("Chilled", c, payload.source);
        }

        OnAilmentsChanged?.Invoke();
    }

    private IEnumerator ChillRoutine()
    {
        while (!IsDead())
        {
            PruneExpiredChillStacks(Time.time);
            if (chillExpireTimes.Count == 0)
                break;

            yield return new WaitForSeconds(0.1f);
        }

        chillRoutine = null;
        OnAilmentsChanged?.Invoke();
    }

    public void ApplyBurnFromHit(BurnPayload payload)
    {
        TryApplyBurnFromFireHit(
            payload.sourceDamage,
            1f,
            payload.burnDamageMultiplier,
            payload.source);
    }

    /// <summary>
    /// Fire hits: successful burn rolls add a stack (max 3) and refresh the 15s sustain window.
    /// Burn falls off if no new burn application lands within that window. Tick damage uses the strongest hit (bleed-style).
    /// At 3 stacks, combust for tick damage × 10, then clear.
    /// </summary>
    public bool TryApplyBurnFromFireHit(
        float fireDamageDealt,
        float applyChance,
        float burnDamageMultiplier,
        Transform source,
        string outgoingDpsSourceLabel = null,
        bool outgoingAttributeToMinion = false,
        float burnTickIntervalSeconds = DefaultBurnTickIntervalSeconds)
    {
        if (IsDead()) return false;
        if (fireDamageDealt <= 0f) return false;

        _burnDotDealerLabel = ResolveDotDealerLabelForDps(source);
        _burnOutgoingDpsSourceLabel = outgoingDpsSourceLabel;
        _burnOutgoingAttributeToMinion = outgoingAttributeToMinion;
        burnDotSource = source != null ? source : transform;

        if (source != null)
        {
            _burnDotDealerWorldPos = source.position;
            _hasBurnDotDealerWorldPos = true;
        }
        else
        {
            _burnDotDealerWorldPos = transform.position;
            _hasBurnDotDealerWorldPos = true;
        }

        bool hadBurn = burnStackCount > 0;

        if (UnityEngine.Random.value > Mathf.Clamp01(applyChance))
            return false;

        float mult = Mathf.Max(0f, burnDamageMultiplier);
        float fraction = Mathf.Clamp01(burnDamageFractionOfHitPerTick);
        int candidateTick = Mathf.Max(1, Mathf.CeilToInt(fireDamageDealt * fraction * mult));
        burnDamagePerTick = Mathf.Max(burnDamagePerTick, candidateTick);
        burnStackCount = Mathf.Min(BurnMaxStacks, burnStackCount + 1);
        RefreshBurnExpiryTimer();
        _burnTickWaitSeconds = Mathf.Max(0.05f, burnTickIntervalSeconds);

        if (burnTickRoutine == null)
            burnTickRoutine = StartCoroutine(BurnTickRoutine());

        if (!hadBurn)
        {
            FloatingDamageTextUI fx = DamagePopupSystem.Instance != null ? DamagePopupSystem.Instance.PopupPrefab : null;
            Color c = ResolveAilmentStatusColor(fx, f => f.BurnPresentationColor, new Color32(255, 140, 40, 255));
            TrySpawnEnemyAilmentActivationPopup("Burnt", c, source);
        }

        OnAilmentsChanged?.Invoke();

        if (burnStackCount >= BurnMaxStacks)
            CombustBurn();

        return true;
    }

    public bool ClearBurn()
    {
        if (burnTickRoutine != null)
        {
            StopCoroutine(burnTickRoutine);
            burnTickRoutine = null;
        }

        bool had = burnStackCount > 0;
        burnStackCount = 0;
        burnDamagePerTick = 0;
        burnExpiresAt = -1f;
        burnLastApplicationAt = -1f;
        burnDotSource = null;
        _burnDotDealerLabel = "";
        _hasBurnDotDealerWorldPos = false;

        if (had)
            OnAilmentsChanged?.Invoke();

        return had;
    }

    private void CombustBurn()
    {
        if (burnStackCount <= 0 && burnDamagePerTick <= 0)
            return;

        int ticksWorth = Mathf.Max(1, burnCombustTicksWorth);
        int combustDamage = Mathf.Max(1, burnDamagePerTick * ticksWorth);

        burnStackCount = 0;
        burnDamagePerTick = 0;
        burnExpiresAt = -1f;
        burnLastApplicationAt = -1f;

        if (burnTickRoutine != null)
        {
            StopCoroutine(burnTickRoutine);
            burnTickRoutine = null;
        }

        if (burnDotSource != null)
        {
            _burnDotDealerWorldPos = burnDotSource.position;
            _hasBurnDotDealerWorldPos = true;
        }

        Vector3? burnDealerWorld = _hasBurnDotDealerWorldPos ? (Vector3?)_burnDotDealerWorldPos : null;
        ApplyDotDamage(
            combustDamage,
            FloatingDamageTextUI.PopupDamageKind.Magic,
            burnDotSource,
            _burnDotDealerLabel,
            burnDealerWorld,
            _burnOutgoingDpsSourceLabel,
            _burnOutgoingAttributeToMinion);
        OnAilmentsChanged?.Invoke();
    }

    private void RecordBurnApplicationSustain()
    {
        burnLastApplicationAt = Time.time;
        burnExpiresAt = burnLastApplicationAt + GetBurnWallClockDurationSeconds();
    }

    private void RefreshBurnExpiryTimer() => RecordBurnApplicationSustain();

    private bool IsBurnExpiredFromLackOfApplications()
    {
        if (burnLastApplicationAt < 0f)
            return burnExpiresAt > 0f && Time.time >= burnExpiresAt;

        return Time.time >= burnLastApplicationAt + GetBurnWallClockDurationSeconds();
    }

    private float GetBurnWallClockDurationSeconds()
    {
        if (burnWallClockDurationSeconds > 0f)
            return burnWallClockDurationSeconds;

        return Mathf.Max(1f, burnDurationTicks * DefaultBurnTickIntervalSeconds);
    }

    private IEnumerator BurnTickRoutine()
    {
        while (!IsDead())
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, _burnTickWaitSeconds));

            if (burnStackCount <= 0)
            {
                burnDamagePerTick = 0;
                burnExpiresAt = -1f;
                burnLastApplicationAt = -1f;
                break;
            }

            if (IsBurnExpiredFromLackOfApplications())
            {
                burnStackCount = 0;
                burnDamagePerTick = 0;
                burnExpiresAt = -1f;
                burnLastApplicationAt = -1f;
                break;
            }

            if (burnDamagePerTick > 0)
            {
                if (burnDotSource != null)
                {
                    _burnDotDealerWorldPos = burnDotSource.position;
                    _hasBurnDotDealerWorldPos = true;
                }

                Vector3? burnDealerWorld = _hasBurnDotDealerWorldPos ? (Vector3?)_burnDotDealerWorldPos : null;
                ApplyDotDamage(
                    burnDamagePerTick,
                    FloatingDamageTextUI.PopupDamageKind.Magic,
                    burnDotSource,
                    _burnDotDealerLabel,
                    burnDealerWorld,
                    _burnOutgoingDpsSourceLabel,
                    _burnOutgoingAttributeToMinion);
            }

            OnAilmentsChanged?.Invoke();
        }

        burnTickRoutine = null;
        OnAilmentsChanged?.Invoke();
    }

    public void ApplyShockFromHit(ShockPayload payload)
    {
        if (IsDead()) return;

        CacheAilmentStatusDealerWorld(payload.source, ref _shockDotDealerWorldPos, ref _hasShockDotDealerWorldPos);

        float duration = Mathf.Max(0.1f, payload.duration);
        float incomingDamageBonus = Mathf.Max(0f, payload.damageTakenMultiplier);

        bool firstShock = !HasShock;

        shockDamageTakenMultiplier = incomingDamageBonus;
        shockExpireTime = Time.time + duration;

        if (shockRoutine == null)
            shockRoutine = StartCoroutine(ShockRoutine());

        if (firstShock)
        {
            FloatingDamageTextUI fx = DamagePopupSystem.Instance != null ? DamagePopupSystem.Instance.PopupPrefab : null;
            Color c = ResolveAilmentStatusColor(fx, f => f.ShockPresentationColor, new Color32(255, 190, 70, 255));
            TrySpawnEnemyAilmentActivationPopup("Shocked", c, payload.source);
        }

        OnAilmentsChanged?.Invoke();
    }

    private IEnumerator ShockRoutine()
    {
        while (!IsDead() && Time.time < shockExpireTime)
            yield return null;

        shockDamageTakenMultiplier = 0f;
        shockExpireTime = -1f;
        shockRoutine = null;
        OnAilmentsChanged?.Invoke();
    }

    public float GetIncomingDamageMultiplier()
    {
        if (!HasShock)
            return 1f;

        return 1f + shockDamageTakenMultiplier;
    }

    public float GetMoveSpeedMultiplier()
    {
        float slow = GetChillSlowMultiplier();
        if (_poisonNeurotoxinActive && HasPoison && _poisonOwnerPlayerStats != null)
        {
            slow += Mathf.Clamp01(
                _poisonOwnerPlayerStats.GetMasterOfVenomsNeurotoxinMoveSlowPerPoisonStack() * PoisonStacks);
        }

        return Mathf.Max(0.1f, 1f - slow);
    }

    /// <summary>Outgoing damage multiplier while Neurotoxin poison is active (afflicted enemy deals less damage).</summary>
    public float GetOutgoingDamageMultiplier()
    {
        if (_poisonNeurotoxinActive && HasPoison && _poisonOwnerPlayerStats != null)
            return _poisonOwnerPlayerStats.GetMasterOfVenomsNeurotoxinOutgoingDamageMultiplier();

        return 1f;
    }

    private void ApplyDotDamage(
        int damage,
        FloatingDamageTextUI.PopupDamageKind type,
        Transform source,
        string dealerLabelForDps,
        Vector3? dotDealerWorldPositionFallback = null,
        string outgoingDpsSourceLabel = null,
        bool outgoingAttributeToMinion = false,
        bool wasCrit = false)
    {
        damage = Mathf.Max(1, damage);

        if (enemy != null)
        {
            enemy.ApplyDirectDotDamage(
                damage,
                source,
                type,
                showDotPopups,
                dotDealerWorldPositionFallback,
                outgoingDpsSourceLabel,
                outgoingAttributeToMinion,
                wasCrit);
            return;
        }

        if (characterStats != null)
        {
            PlayerController pcTeleport = GetComponent<PlayerController>();
            if (pcTeleport != null && pcTeleport.TeleportDamageImmune)
                return;

            float applied = characterStats.TakeDamageFromResolvedDot(damage, out _);
            int finalDamage = Mathf.RoundToInt(applied);
            PlayerCombatController combat = GetComponent<PlayerCombatController>();
            if (combat == null)
                combat = GetComponentInParent<PlayerCombatController>();
            if (combat != null && finalDamage > 0)
                combat.RecordIncomingDamageForDps(finalDamage, ToDpsBucket(type), source, dealerLabelForDps);

            if (showDotPopups && finalDamage > 0 && DamagePopupSystem.Instance != null)
            {
                PlayerController pc = GetComponent<PlayerController>();
                if (pc == null)
                    pc = GetComponentInParent<PlayerController>();

                Transform popupOwner = pc != null ? pc.transform : transform;
                var anchor = popupOwner.GetComponentInChildren<DamagePopupAnchor>(true);
                Vector3 anchorPos = anchor ? anchor.WorldPos : popupOwner.position;

                Vector3? dealerWorld = source != null ? source.position : dotDealerWorldPositionFallback;

                Vector3 pos;
                Vector3 dir;
                if (pc != null && dealerWorld.HasValue)
                    pc.GetIncomingDamagePopupPlacementFromDealerWorld(anchorPos, dealerWorld.Value, 0.35f, out pos, out dir);
                else if (pc != null)
                    pc.GetIncomingDamagePopupPlacement(anchorPos, source, 0.35f, out pos, out dir);
                else if (dealerWorld.HasValue)
                {
                    Vector3 dw = dealerWorld.Value;
                    float towardAttackerX = Mathf.Sign(dw.x - popupOwner.position.x);
                    if (towardAttackerX == 0f)
                        towardAttackerX = 1f;
                    pos = anchorPos + new Vector3(-towardAttackerX * 0.35f, 0f, 0f);
                    dir = DamagePopupSystem.GetDriftDirectionForVictim(popupOwner, dw);
                }
                else
                {
                    float towardAttackerX = source ? Mathf.Sign(source.position.x - popupOwner.position.x) : 0f;
                    if (towardAttackerX == 0f)
                        towardAttackerX = 1f;
                    pos = anchorPos + new Vector3(-towardAttackerX * 0.35f, 0f, 0f);
                    dir = source ? DamagePopupSystem.GetDriftDirectionForVictim(popupOwner, source) : Vector3.up;
                }

                DamagePopupSystem.Instance.Spawn(
                    pos,
                    finalDamage,
                    type,
                    wasCrit,
                    true,
                    dir,
                    false
                );
            }
        }
    }

    private static DpsDamageBucket ToDpsBucket(FloatingDamageTextUI.PopupDamageKind kind)
    {
        return kind switch
        {
            FloatingDamageTextUI.PopupDamageKind.Bleed => DpsDamageBucket.Bleed,
            FloatingDamageTextUI.PopupDamageKind.Poison => DpsDamageBucket.Poison,
            FloatingDamageTextUI.PopupDamageKind.PoisonCrit => DpsDamageBucket.Poison,
            FloatingDamageTextUI.PopupDamageKind.Magic => DpsDamageBucket.Burn,
            FloatingDamageTextUI.PopupDamageKind.Corruption => DpsDamageBucket.Corruption,
            _ => DpsDamageBucket.Physical
        };
    }

    private bool IsDead()
    {
        if (enemy != null) return enemy.IsDead;
        if (characterStats != null) return characterStats.IsDead;
        return false;
    }

    private void PruneExpiredChillStacks(float now)
    {
        bool changed = false;
        for (int i = chillExpireTimes.Count - 1; i >= 0; i--)
        {
            if (now >= chillExpireTimes[i])
            {
                chillExpireTimes.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            OnAilmentsChanged?.Invoke();
    }

    private float GetChillSlowMultiplier()
    {
        if (chillExpireTimes.Count <= 0)
            return 0f;

        // 15% slow per stack, capped by stack count constraints from payload.
        return Mathf.Clamp01(chillExpireTimes.Count * Mathf.Max(0f, chillSlowPerStack));
    }
}


// =========================================================
// FUTURE AILMENTS OVERVIEW
// =========================================================
/*
    🔴 BLEED (IMPLEMENTED)
    - Source: Physical damage
    - Behaviour: strongest tick per slot
    - Extends duration
    - No mitigation, no crit, no block

    🟢 POISON (IMPLEMENTED)
    - Source: Corruption damage (poison application); ticks use resolved DoT damage.
    - Behaviour: stacks
    - Each stack ticks independently
    - No mitigation, no crit, no block

    🔥 BURN (TODO)
    - Source: Magic damage
    - Behaviour: refresh duration (NOT stack)
    - Optional: ramp damage over time
    - Good for mage builds

    ❄️ CHILL (TODO - NON-DOT)
    - Effect: reduces move speed + attack speed
    - No damage
    - Duration-based debuff

    ⚡ SHOCK (TODO - NON-DOT)
    - Effect: increases damage taken
    - Could reduce armor/resistance
    - Great for burst synergy

    ☠️ FUTURE IDEAS
    - Curse: reduces stats
    - Stun: handled on <see cref="EnemyBaseController"/> (action lockout + status popup)
    - Weaken: reduces outgoing damage
    - Armor break: reduces armor only
*/

// =========================================================
// FUTURE AILMENT TEMPLATES
// =========================================================
/*
    BURN
    - magic DOT
    - usually refresh duration instead of stacking

    CHILL
    - movement / attack speed slow
    - no DOT required

    SHOCK
    - increased damage taken
    - could reduce armor / resist
*/