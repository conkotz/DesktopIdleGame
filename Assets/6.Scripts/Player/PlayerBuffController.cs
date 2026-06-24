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

        /// <summary>Indefinite HUD ability buff (e.g. Soulforged Weapon until dismissed): no radial overlay or action-bar timer.</summary>
        public bool hudPersistActiveOverlay;

        /// <summary>When true, buff totals and mechanics still run but the buff strip omits this row.</summary>
        public bool hideFromBuffPanel;

        public float RemainingSeconds => Mathf.Max(0f, endTime - Time.time);
        public bool IsExpired => Time.time >= endTime;
    }

    private readonly List<ActiveBuff> activeBuffs = new();
    private readonly Dictionary<ConsumableEffectType, float> totals = new();

    private CharacterStats stats;
    private AilmentController ailmentController;

    private int _buffBatchDepth;
    private bool _buffBatchDirty;

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
            displayStacks = 0,
            hideFromBuffPanel = effect.hideFromBuffPanel
        });

        FlushBuffBatchIfNeeded();
    }

    /// <summary>Defer stat/buff UI refresh until <see cref="EndBuffBatch"/> (e.g. food applies several timed buffs at once).</summary>
    public void BeginBuffBatch()
    {
        _buffBatchDepth++;
    }

    public void EndBuffBatch()
    {
        if (_buffBatchDepth <= 0)
            return;

        _buffBatchDepth--;
        if (_buffBatchDepth > 0)
            return;

        if (!_buffBatchDirty)
            return;

        _buffBatchDirty = false;
        RecalculateBuffTotals();
        NotifyChanged();
    }

    private void FlushBuffBatchIfNeeded()
    {
        if (_buffBatchDepth > 0)
        {
            _buffBatchDirty = true;
            return;
        }

        RecalculateBuffTotals();
        NotifyChanged();
    }

    /// <summary>Registers or updates a timed ability buff for the HUD bar only (no consumable stat totals).</summary>
    private static bool ShouldClearHudAbilityBuffRow(
        int displayStacks,
        float endTime,
        float durationSeconds,
        bool persistActiveOverlay)
    {
        if (displayStacks > 0)
            return false;
        if (persistActiveOverlay)
            return false;
        // Timed ability buffs may use 0 stacks while active (e.g. War Banner ramp).
        if (durationSeconds > 0f && endTime > Time.time)
            return false;
        return true;
    }

    /// <summary>Whether a HUD ability row should occupy a slot on the buff strip (mirrors strip clear rules).</summary>
    public static bool ShouldDisplayHudAbilityBuffInStrip(ActiveBuff buff)
    {
        if (buff == null || buff.type != ConsumableEffectType.HudAbilityBuff)
            return false;

        return !ShouldClearHudAbilityBuffRow(
            buff.displayStacks,
            buff.endTime,
            buff.duration,
            buff.hudPersistActiveOverlay);
    }

    /// <summary>Whether an active buff row should be shown on the HUD buff strip.</summary>
    public static bool ShouldDisplayInBuffStrip(ActiveBuff buff)
    {
        if (buff == null || buff.hideFromBuffPanel)
            return false;

        if (buff.type == ConsumableEffectType.HudAbilityBuff)
            return ShouldDisplayHudAbilityBuffInStrip(buff);

        return !buff.IsExpired;
    }

    public void SetHudAbilityBuff(
        string abilityId,
        int displayStacks,
        float endTime,
        float durationSeconds,
        bool persistActiveOverlay = false)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return;

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            ActiveBuff existing = activeBuffs[i];
            if (existing.type != ConsumableEffectType.HudAbilityBuff ||
                !string.Equals(existing.id, abilityId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ShouldClearHudAbilityBuffRow(displayStacks, endTime, durationSeconds, persistActiveOverlay))
            {
                activeBuffs.RemoveAt(i);
                NotifyChanged();
                return;
            }

            bool wasVisible = ShouldDisplayHudAbilityBuffInStrip(existing);

            bool structuralLayoutChange =
                existing.hudPersistActiveOverlay != persistActiveOverlay ||
                !Mathf.Approximately(existing.duration, Mathf.Max(0f, durationSeconds));

            existing.endTime = endTime;
            existing.duration = Mathf.Max(0f, durationSeconds);
            existing.displayStacks = displayStacks;
            existing.hudPersistActiveOverlay = persistActiveOverlay;

            bool nowVisible = ShouldDisplayHudAbilityBuffInStrip(existing);

            if (structuralLayoutChange || wasVisible != nowVisible)
                NotifyChanged();
            return;
        }

        activeBuffs.RemoveAll(b =>
            b.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(b.id, abilityId, StringComparison.OrdinalIgnoreCase));

        if (ShouldClearHudAbilityBuffRow(displayStacks, endTime, durationSeconds, persistActiveOverlay))
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
            displayStacks = displayStacks,
            hudPersistActiveOverlay = persistActiveOverlay
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

    /// <summary>
    /// True when the buff strip would show this ability (timed window, Cleaving Strikes swings remaining, Soulforged
    /// minion count, etc.). Matches <see cref="SetHudAbilityBuff"/> rows with <see cref="ActiveBuff.displayStacks"/> &gt; 0.
    /// </summary>
    public bool IsHudAbilityBuffActive(string abilityId) => TryGetHudAbilityBuff(abilityId, out _);

    /// <summary>HUD ability buff row for this id, if any (may be expired by clock while stacks remain, e.g. Cleaving Strikes).</summary>
    public bool TryGetHudAbilityBuff(string abilityId, out ActiveBuff buff)
    {
        buff = null;
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            ActiveBuff b = activeBuffs[i];
            if (b.type != ConsumableEffectType.HudAbilityBuff)
                continue;
            if (!string.Equals(b.id, abilityId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (b.displayStacks <= 0)
            {
                if (!HasFiniteHudAbilityBuffDuration(b) || b.endTime <= Time.time)
                    continue;
            }
            buff = b;
            return true;
        }

        return false;
    }

    /// <summary>True for finite-duration HUD ability buffs (not indefinite / until-dismissed rows).</summary>
    public static bool HasFiniteHudAbilityBuffDuration(ActiveBuff b) =>
        b != null &&
        b.type == ConsumableEffectType.HudAbilityBuff &&
        !b.hudPersistActiveOverlay &&
        b.duration > 0f;

    /// <summary>
    /// Whether the radial overlay / action-bar active wedge should show (finite duration with time left only).
    /// Indefinite buffs still appear in the buff strip icon list but without overlay bars or timers.
    /// </summary>
    public bool ShouldDisplayHudAbilityBuffTimedPresentation(string abilityId, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (!TryGetHudAbilityBuff(abilityId, out ActiveBuff b))
            return false;
        if (!HasFiniteHudAbilityBuffDuration(b))
            return false;

        remainingSeconds = b.RemainingSeconds;
        return remainingSeconds > 0f;
    }

    /// <summary>Whether a numeric countdown should be shown on the action bar or buff icon.</summary>
    public bool ShouldDisplayHudAbilityBuffCountdown(string abilityId, out float remainingSeconds) =>
        ShouldDisplayHudAbilityBuffTimedPresentation(abilityId, out remainingSeconds);

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

    /// <summary>Removes a single consumable/food/potion buff row (right-click dismiss on buff strip).</summary>
    public bool TryDismissConsumableBuff(ActiveBuff buff)
    {
        if (buff == null || buff.type == ConsumableEffectType.HudAbilityBuff || buff.type == ConsumableEffectType.None)
            return false;

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff row = activeBuffs[i];
            if (row.type != buff.type)
                continue;
            if (!string.Equals(row.id, buff.id, StringComparison.OrdinalIgnoreCase))
                continue;

            activeBuffs.RemoveAt(i);
            RecalculateBuffTotals();
            NotifyChanged();
            stats?.NotifyStatsChanged(affectsCombatPower: false);
            return true;
        }

        return false;
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
                        stats.Heal(healPerSecond * dt, PlayerCombatController.PotionHealingSourceLabel);
                    }
                    break;

                case ConsumableEffectType.ManaRegenOverTime:
                    if (stats != null && buff.magnitude > 0f && buff.duration > 0f)
                    {
                        float manaPerSecond = buff.magnitude / buff.duration;
                        stats.AddMana(manaPerSecond * dt);
                    }
                    break;

                case ConsumableEffectType.FoodHealOverTime:
                    if (stats != null && buff.magnitude > 0f && buff.duration > 0f)
                    {
                        float healPerSecond = buff.magnitude / buff.duration;
                        stats.Heal(healPerSecond * dt, PlayerCombatController.FoodHealingSourceLabel);
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
        {
            stats.ClampHpToFoodOverhealCap();
            stats.NotifyStatsChanged();
        }
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