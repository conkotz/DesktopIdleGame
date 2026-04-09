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
    [Tooltip("Seconds of burn DoT before expiring if not refreshed by another fire hit.")]
    [SerializeField, Min(1)] private int burnDurationTicks = 15;
    [Tooltip("Each burn tick deals this fraction of the applying fire hit (min 1/tick), before Burn Damage mult.")]
    [SerializeField, Range(0.01f, 1f)]
    [FormerlySerializedAs("burnTotalDamageFractionOfHit")]
    private float burnDamageFractionOfHitPerTick = 0.15f;
    [Tooltip("Combust burst = current tick damage × this count (10s worth at 1 tick/s).")]
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
    private int burnTicksRemaining;
    private Transform burnDotSource;
    private Coroutine burnTickRoutine;
    private float shockExpireTime = -1f;
    private float shockDamageTakenMultiplier = 0f;

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
        chillExpireTimes.Clear();
        burnStackCount = 0;
        burnDamagePerTick = 0;
        burnTicksRemaining = 0;
        burnTickRoutine = null;
        burnDotSource = null;
        shockExpireTime = -1f;
        shockDamageTakenMultiplier = 0f;

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

        bool wasInactive = !HasBleed;

        RefreshBleedSchedule(newBleedTick, tickCount);

        if (wasInactive && bleedTickSchedule.Count > 0)
        {
            int instantTick = Mathf.Max(1, bleedTickSchedule[0]);
            bleedTickSchedule.RemoveAt(0);
            ApplyBleedTick(instantTick, payload.source);
        }

        if (bleedTickSchedule.Count > 0 && bleedRoutine == null)
            bleedRoutine = StartCoroutine(BleedRoutine(payload.source));

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

        bool wasInactive = exclusiveBleedTickSchedule.Count == 0 && exclusiveBleedRoutine == null;
        RefreshExclusiveBleedSchedule(newTick, tickCount);

        if (wasInactive && exclusiveBleedTickSchedule.Count > 0)
        {
            int instantTick = Mathf.Max(1, exclusiveBleedTickSchedule[0]);
            exclusiveBleedTickSchedule.RemoveAt(0);
            ApplyBleedTick(instantTick, payload.source);
        }

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

            ApplyBleedTick(tickDamage, source);
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

            ApplyBleedTick(tickDamage, source);
            OnAilmentsChanged?.Invoke();
        }

        exclusiveBleedRoutine = null;
        OnAilmentsChanged?.Invoke();
    }

    private void ApplyBleedTick(int damage, Transform source)
    {
        ApplyDotDamage(damage, FloatingDamageTextUI.PopupDamageKind.Bleed, source);

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

        while (poisonStacks.Count >= maxStacks)
            poisonStacks.RemoveAt(0);

        poisonStacks.Add(new PoisonStack(tickDamage, ticks));

        if (poisonRoutine == null)
            poisonRoutine = StartCoroutine(PoisonRoutine(payload.source));

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
                ApplyPoisonTick(totalDamage, source);

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
        ApplyDotDamage(damage, FloatingDamageTextUI.PopupDamageKind.Poison, source);

        if (debugLogs)
            Debug.Log($"[Ailments] Poison tick: {damage}", this);
    }

    public void ApplyChillFromHit(ChillPayload payload)
    {
        if (IsDead()) return;

        float duration = Mathf.Max(0.1f, payload.duration);
        int maxStacks = Mathf.Max(1, payload.maxStacks);
        chillSlowPerStack = Mathf.Clamp01(payload.slowPerStack);

        float now = Time.time;
        PruneExpiredChillStacks(now);

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
    /// Fire hits: refresh 15s timer while burning. Successful rolls add a stack (max 3); tick damage uses the strongest hit (bleed-style).
    /// At 3 stacks, combust for tick damage × 10, then clear.
    /// </summary>
    public bool TryApplyBurnFromFireHit(
        float fireDamageDealt,
        float applyChance,
        float burnDamageMultiplier,
        Transform source)
    {
        if (IsDead()) return false;
        if (fireDamageDealt <= 0f) return false;

        burnDotSource = source != null ? source : transform;

        bool hadBurn = burnStackCount > 0;
        if (hadBurn)
        {
            burnTicksRemaining = Mathf.Max(1, burnDurationTicks);
            OnAilmentsChanged?.Invoke();
        }

        if (UnityEngine.Random.value > Mathf.Clamp01(applyChance))
            return false;

        float mult = Mathf.Max(0f, burnDamageMultiplier);
        float fraction = Mathf.Clamp01(burnDamageFractionOfHitPerTick);
        int candidateTick = Mathf.Max(1, Mathf.CeilToInt(fireDamageDealt * fraction * mult));
        burnDamagePerTick = Mathf.Max(burnDamagePerTick, candidateTick);
        burnStackCount = Mathf.Min(BurnMaxStacks, burnStackCount + 1);
        burnTicksRemaining = Mathf.Max(1, burnDurationTicks);

        if (burnTickRoutine == null)
            burnTickRoutine = StartCoroutine(BurnTickRoutine());

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
        burnTicksRemaining = 0;

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
        burnTicksRemaining = 0;

        if (burnTickRoutine != null)
        {
            StopCoroutine(burnTickRoutine);
            burnTickRoutine = null;
        }

        ApplyDotDamage(combustDamage, FloatingDamageTextUI.PopupDamageKind.Magical, burnDotSource);
        OnAilmentsChanged?.Invoke();
    }

    private IEnumerator BurnTickRoutine()
    {
        var wait = new WaitForSeconds(1f);

        while (!IsDead())
        {
            yield return wait;

            if (burnStackCount <= 0 || burnTicksRemaining <= 0)
            {
                burnStackCount = 0;
                burnDamagePerTick = 0;
                burnTicksRemaining = 0;
                break;
            }

            burnTicksRemaining--;
            if (burnDamagePerTick > 0)
                ApplyDotDamage(burnDamagePerTick, FloatingDamageTextUI.PopupDamageKind.Magical, burnDotSource);

            OnAilmentsChanged?.Invoke();
        }

        burnTickRoutine = null;
        OnAilmentsChanged?.Invoke();
    }

    public void ApplyShockFromHit(ShockPayload payload)
    {
        if (IsDead()) return;

        float duration = Mathf.Max(0.1f, payload.duration);
        float incomingDamageBonus = Mathf.Max(0f, payload.damageTakenMultiplier);

        shockDamageTakenMultiplier = incomingDamageBonus;
        shockExpireTime = Time.time + duration;

        if (shockRoutine == null)
            shockRoutine = StartCoroutine(ShockRoutine());

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
        return Mathf.Max(0.1f, 1f - slow);
    }

    private void ApplyDotDamage(int damage, FloatingDamageTextUI.PopupDamageKind type, Transform source)
    {
        damage = Mathf.Max(1, damage);

        if (enemy != null)
        {
            enemy.ApplyDirectDotDamage(damage, source, type, showDotPopups);
            return;
        }

        if (characterStats != null)
        {
            float applied = characterStats.TakeDamage(damage, DamageType.True, out _);
            int finalDamage = Mathf.RoundToInt(applied);

            if (showDotPopups && finalDamage > 0 && DamagePopupSystem.Instance != null)
            {
                // Prefer child anchor so it follows visual flip/offset correctly.
                var anchor = GetComponentInChildren<DamagePopupAnchor>(true);
                Vector3 pos = anchor ? anchor.WorldPos : transform.position;

                // DOT source side feels like "impact" side as well.
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
                    finalDamage,
                    type,
                    false,
                    true,
                    dir,
                    false
                );
            }
        }
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
    - Source: True damage
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
    - Stun: disables actions
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