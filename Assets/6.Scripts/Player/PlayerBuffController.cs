using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerBuffController : MonoBehaviour
{
    [Serializable]
    public class ActiveBuff
    {
        public string id;
        public ConsumableEffectType type;
        public float magnitude;
        public float endTime;

        public float duration; // ✅ ADD THIS

        /// <summary>Corner stack count for HUD (e.g. Cleaving Strikes swings). 0 = use potion-style value label only.</summary>
        public int displayStacks;

        public float RemainingSeconds => Mathf.Max(0f, endTime - Time.time);
        public bool IsExpired => Time.time >= endTime;
    }

    private readonly List<ActiveBuff> activeBuffs = new();
    private readonly Dictionary<ConsumableEffectType, float> totals = new();

    private CharacterStats stats;
    private AilmentController ailmentController;

    public event Action OnBuffsChanged;

    public IReadOnlyList<ActiveBuff> ActiveBuffs => activeBuffs;

    public bool IsPoisonImmune => HasBuff(ConsumableEffectType.PoisonImmunity);
    public bool IsBleedImmune => HasBuff(ConsumableEffectType.BleedImmunity);

    public float PhysicalDamageBoostPercent => GetTotalMagnitude(ConsumableEffectType.PhysicalDamageBoost);
    public float MagicDamageBoostPercent => GetTotalMagnitude(ConsumableEffectType.MagicDamageBoost);
    public float AttackSpeedPercent => GetTotalMagnitude(ConsumableEffectType.AttackSpeed);

    private void Awake()
    {
        stats = GetComponent<CharacterStats>();
        ailmentController = GetComponent<AilmentController>();
    }

    private void Update()
    {
        TickBuffs();
    }

    public void ApplyBuff(ConsumableGrantedEffect effect)
    {
        if (effect.effectType == ConsumableEffectType.None)
            return;

        if (effect.duration <= 0f)
        {
            ApplyInstantEffect(effect);
            return;
        }

        float endTime = Time.time + effect.duration;

        activeBuffs.RemoveAll(b => b.type == effect.effectType);

        activeBuffs.Add(new ActiveBuff
        {
            id = string.IsNullOrWhiteSpace(effect.effectId) ? effect.effectType.ToString() : effect.effectId,
            type = effect.effectType,
            magnitude = effect.magnitude,
            endTime = endTime,
            duration = effect.duration, // ✅ ADD THIS
            displayStacks = 0
        });

        RecalculateBuffTotals();
        NotifyChanged();
    }

    /// <summary>Registers or updates a timed ability buff for the HUD bar only (no consumable stat totals).</summary>
    public void SetHudAbilityBuff(string abilityId, int displayStacks, float endTime, float durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return;

        activeBuffs.RemoveAll(b =>
            b.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(b.id, abilityId, StringComparison.OrdinalIgnoreCase));

        // Cleaving Strikes (and similar) can outlast the duration until hit charges are spent — keep HUD while stacks > 0.
        if (displayStacks <= 0)
        {
            NotifyChanged();
            return;
        }

        activeBuffs.Add(new ActiveBuff
        {
            id = abilityId,
            type = ConsumableEffectType.HudAbilityBuff,
            magnitude = 0f,
            endTime = endTime,
            duration = Mathf.Max(0f, durationSeconds),
            displayStacks = displayStacks
        });

        NotifyChanged();
    }

    public void ClearHudAbilityBuff(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return;

        int removed = activeBuffs.RemoveAll(b =>
            b.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(b.id, abilityId, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
            NotifyChanged();
    }

    public bool HasBuff(ConsumableEffectType type)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].type == type && !activeBuffs[i].IsExpired)
                return true;
        }

        return false;
    }

    public float GetTotalMagnitude(ConsumableEffectType type)
    {
        return totals.TryGetValue(type, out float value) ? value : 0f;
    }

    public float GetRemainingTime(ConsumableEffectType type)
    {
        float best = 0f;

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].type != type)
                continue;

            best = Mathf.Max(best, activeBuffs[i].RemainingSeconds);
        }

        return best;
    }

    public bool RemoveBuff(ConsumableEffectType type)
    {
        int removed = activeBuffs.RemoveAll(b => b.type == type);

        if (removed <= 0)
            return false;

        RecalculateBuffTotals();
        NotifyChanged();
        return true;
    }

    public void RemoveAllBuffs()
    {
        if (activeBuffs.Count == 0)
            return;

        activeBuffs.Clear();
        RecalculateBuffTotals();
        NotifyChanged();
    }

    private void ApplyInstantEffect(ConsumableGrantedEffect effect)
    {
        bool changed = false;

        switch (effect.effectType)
        {
            case ConsumableEffectType.CleansePoison:
                changed = TryCleansePoison();
                break;

            case ConsumableEffectType.CleanseBleed:
                changed = TryCleanseBleed();
                break;

            case ConsumableEffectType.CleanseAllAilments:
                changed = TryCleanseAllAilments();
                break;
        }

        if (changed)
        {
            RecalculateBuffTotals();
            NotifyChanged();
        }
    }

    private void TickBuffs()
    {
        bool changed = false;
        float dt = Time.deltaTime;

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = activeBuffs[i];

            if (buff.IsExpired)
            {
                // Ability HUD buffs use the real duration for the icon timer only; expiry is cleared by gameplay (e.g. Cleaving Strikes).
                if (buff.type == ConsumableEffectType.HudAbilityBuff)
                    continue;

                activeBuffs.RemoveAt(i);
                changed = true;
                continue;
            }

            // ✅ APPLY OVER-TIME EFFECTS
            switch (buff.type)
            {
                case ConsumableEffectType.HealOverTime:
                    if (stats != null && buff.magnitude > 0f && buff.duration > 0f)
                    {
                        float healPerSecond = buff.magnitude / buff.duration;
                        stats.Heal(healPerSecond * dt);
                    }
                    break;

                case ConsumableEffectType.ManaRegenOverTime:
                    if (stats != null && buff.magnitude > 0f && buff.duration > 0f)
                    {
                        float manaPerSecond = buff.magnitude / buff.duration;
                        stats.AddMana(manaPerSecond * dt);
                    }
                    break;
            }
        }

        if (changed)
        {
            RecalculateBuffTotals();
            NotifyChanged();
        }
    }

    private void RecalculateBuffTotals()
    {
        totals.Clear();

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            ActiveBuff buff = activeBuffs[i];

            if (buff.IsExpired)
                continue;

            if (buff.type == ConsumableEffectType.HudAbilityBuff)
                continue;

            if (totals.ContainsKey(buff.type))
                totals[buff.type] += buff.magnitude;
            else
                totals[buff.type] = buff.magnitude;
        }

        if (stats != null)
            stats.NotifyStatsChanged();
    }

    private void NotifyChanged()
    {
        OnBuffsChanged?.Invoke();
    }

    private bool TryCleansePoison()
    {
        if (ailmentController == null)
            return false;

        return ailmentController.ClearPoison();
    }

    private bool TryCleanseBleed()
    {
        if (ailmentController == null)
            return false;

        return ailmentController.ClearBleed();
    }

    private bool TryCleanseAllAilments()
    {
        if (ailmentController == null)
            return false;

        bool poisonChanged = ailmentController.ClearPoison();
        bool bleedChanged = ailmentController.ClearBleed();
        return poisonChanged || bleedChanged;
    }
}