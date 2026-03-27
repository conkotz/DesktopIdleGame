using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AilmentController : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool showDotPopups = true;
    [SerializeField] private bool debugLogs = false;

    private CharacterStats characterStats;
    private EnemyBaseController enemy;
    private PlayerBuffController playerBuffs;

    private Coroutine bleedRoutine;
    private Coroutine poisonRoutine;
    private Coroutine chillRoutine;
    private Coroutine shockRoutine;

    private readonly List<int> bleedTickSchedule = new();
    private readonly List<PoisonStack> poisonStacks = new();
    private readonly List<float> chillExpireTimes = new();
    private float chillSlowPerStack = 0.15f;
    private bool burnActive;
    private int burnRemainingFireHits;
    private int burnAdditionalHitsRequired = 4;
    private float burnAccumulatedDamage;
    private int burnHitsToExplode = 4;
    private float shockExpireTime = -1f;
    private float shockDamageTakenMultiplier = 0f;

    public event System.Action OnAilmentsChanged;

    public bool HasBleed => bleedTickSchedule.Count > 0 || bleedRoutine != null;
    public bool HasPoison => poisonStacks.Count > 0 || poisonRoutine != null;

    public int BleedStacks => HasBleed ? 1 : 0;
    public int PoisonStacks => poisonStacks.Count;

    public bool HasBurn => burnActive;
    public bool HasChill => chillExpireTimes.Count > 0 || chillRoutine != null;
    public bool HasShock => shockDamageTakenMultiplier > 0f && Time.time < shockExpireTime;
    // Burn is a "charged" debuff: once applied, it needs N additional FIRE hits to explode.
    // Stacks shown are progress toward the explosion (0..N).
    public int BurnStacks => burnActive ? Mathf.Clamp(burnAdditionalHitsRequired - burnRemainingFireHits, 0, burnAdditionalHitsRequired) : 0;
    public int BurnHitsToExplode => Mathf.Max(1, burnAdditionalHitsRequired);
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

        bleedRoutine = null;
        poisonRoutine = null;
        chillRoutine = null;
        shockRoutine = null;

        bleedTickSchedule.Clear();
        poisonStacks.Clear();
        chillExpireTimes.Clear();
        burnActive = false;
        burnRemainingFireHits = 0;
        burnAdditionalHitsRequired = 4;
        burnAccumulatedDamage = 0f;
        burnHitsToExplode = 4;
        shockExpireTime = -1f;
        shockDamageTakenMultiplier = 0f;

        OnAilmentsChanged?.Invoke();
    }

    public bool ClearBleed()
    {
        bool hadBleed = HasBleed;

        if (bleedRoutine != null)
            StopCoroutine(bleedRoutine);

        bleedRoutine = null;
        bleedTickSchedule.Clear();

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

        if (hadPoison)
            OnAilmentsChanged?.Invoke();

        return hadPoison;
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

    private void RefreshBleedSchedule(int newTickDamage, int tickCount)
    {
        while (bleedTickSchedule.Count > tickCount)
            bleedTickSchedule.RemoveAt(bleedTickSchedule.Count - 1);

        while (bleedTickSchedule.Count < tickCount)
            bleedTickSchedule.Add(0);

        for (int i = 0; i < tickCount; i++)
            bleedTickSchedule[i] = Mathf.Max(bleedTickSchedule[i], newTickDamage);
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
        int maxStacks = Mathf.Max(1, payload.maxStacks);

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
        if (IsDead()) return;
        if (payload.sourceDamage <= 0f) return;

        // Legacy entry point (older logic). Treat as "start burn" using the payload config.
        StartBurn(payload.sourceDamage, payload.hitsToExplode, payload.explosionMultiplier);
        OnAilmentsChanged?.Invoke();
    }

    public void StartBurnFromFireHit(float fireHitDamage, int additionalFireHitsToExplode, float explosionMultiplier)
    {
        if (IsDead()) return;
        if (fireHitDamage <= 0f) return;
        StartBurn(fireHitDamage, additionalFireHitsToExplode, explosionMultiplier);
        OnAilmentsChanged?.Invoke();
    }

    public void ApplyBurnFollowUpFireHit(float fireHitDamage, float explosionMultiplier, Transform source)
    {
        if (IsDead()) return;
        if (!burnActive) return;
        if (fireHitDamage <= 0f) return;

        burnAccumulatedDamage += fireHitDamage;
        burnRemainingFireHits = Mathf.Max(0, burnRemainingFireHits - 1);

        if (burnRemainingFireHits > 0)
        {
            OnAilmentsChanged?.Invoke();
            return;
        }

        ExplodeBurn(explosionMultiplier, source);
    }

    public bool ClearBurn()
    {
        bool had = burnActive;
        burnActive = false;
        burnRemainingFireHits = 0;
        burnAccumulatedDamage = 0f;

        if (had)
            OnAilmentsChanged?.Invoke();

        return had;
    }

    private void StartBurn(float firstFireHitDamage, int additionalFireHitsToExplode, float explosionMultiplier)
    {
        burnActive = true;
        burnAdditionalHitsRequired = Mathf.Max(1, additionalFireHitsToExplode);
        burnRemainingFireHits = burnAdditionalHitsRequired;
        burnAccumulatedDamage = firstFireHitDamage;
        burnHitsToExplode = burnAdditionalHitsRequired;
    }

    private void ExplodeBurn(float explosionMultiplier, Transform source)
    {
        int explosionDamage = Mathf.Max(1, Mathf.RoundToInt(burnAccumulatedDamage * Mathf.Max(0f, explosionMultiplier)));

        burnActive = false;
        burnRemainingFireHits = 0;
        burnAccumulatedDamage = 0f;

        ApplyDotDamage(explosionDamage, FloatingDamageTextUI.PopupDamageKind.Magical, source);
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