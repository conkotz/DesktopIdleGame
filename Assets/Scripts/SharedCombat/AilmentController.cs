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

    private readonly List<int> bleedTickSchedule = new();
    private readonly List<PoisonStack> poisonStacks = new();

    public event System.Action OnAilmentsChanged;

    public bool HasBleed => bleedTickSchedule.Count > 0 || bleedRoutine != null;
    public bool HasPoison => poisonStacks.Count > 0 || poisonRoutine != null;

    public int BleedStacks => HasBleed ? 1 : 0;
    public int PoisonStacks => poisonStacks.Count;

    public bool HasBurn => false;
    public bool HasChill => false;
    public bool HasShock => false;

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

        bleedRoutine = null;
        poisonRoutine = null;

        bleedTickSchedule.Clear();
        poisonStacks.Clear();

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