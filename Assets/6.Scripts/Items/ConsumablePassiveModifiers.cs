using UnityEngine;

/// <summary>
/// Effective consumable stats from Endurance major passive Alchemist's Boon (tooltips + runtime).
/// </summary>
public static class ConsumablePassiveModifiers
{
    public static CharacterStats ResolveLocalPlayerStats() =>
        AbilityTooltipDamagePreview.FindLocalPlayerStats();

    public static bool IsAlchemistsBoonActive(CharacterStats stats) =>
        stats != null && stats.IsAlchemistsBoonMajorPassiveActive();

    public static float GetEffectiveGrantedEffectDuration(ItemDefinition def, CharacterStats stats)
    {
        if (def == null || !def.HasGrantedEffect)
            return 0f;

        float duration = def.GrantedEffect.duration;
        if (IsAlchemistsBoonActive(stats))
            duration += AbilityCombatPower.AlchemistsBoonConsumableDurationBonusSeconds;

        return Mathf.Max(0f, duration);
    }

    public static float GetEffectiveFoodEffectDuration(ItemDefinition def, CharacterStats stats)
    {
        if (def == null || !def.IsFood)
            return 0f;

        return Mathf.Max(0f, def.consumableStats.foodEffectDurationSeconds);
    }

    /// <summary>Overheal buff window for instant-only food when Alchemist's Boon grants overheal without timed food buffs.</summary>
    public static float GetAlchemistsBoonOverhealBuffDurationSeconds(ItemDefinition def, CharacterStats stats)
    {
        if (def == null || !def.IsFood || !IsAlchemistsBoonActive(stats))
            return 0f;

        float duration = def.consumableStats.foodEffectDurationSeconds;
        if (duration <= 0.001f)
            duration = AbilityCombatPower.AlchemistsBoonDefaultFoodBuffDurationSeconds;

        return Mathf.Max(0f, duration);
    }

    public static int GetEffectiveHealAmount(ItemDefinition def, CharacterStats stats)
    {
        if (def == null)
            return 0;

        int amount = def.HealAmount;
        if (amount <= 0 || !def.IsFood || !IsAlchemistsBoonActive(stats))
            return amount;

        return Mathf.Max(0, Mathf.RoundToInt(amount * (1f + AbilityCombatPower.AlchemistsBoonFoodHealBonusFraction)));
    }

    public static int ScaleFoodHealAmount(int baseAmount, CharacterStats stats)
    {
        if (baseAmount <= 0 || !IsAlchemistsBoonActive(stats))
            return baseAmount;

        return Mathf.Max(0, Mathf.RoundToInt(baseAmount * (1f + AbilityCombatPower.AlchemistsBoonFoodHealBonusFraction)));
    }

    public static int GetEffectiveFoodRegenTotal(ItemDefinition def, CharacterStats stats)
    {
        if (def == null || !def.IsFood)
            return 0;

        return def.consumableStats.foodRegenTotalHeal;
    }

    public static int GetEffectiveFoodOverhealCapFlat(ItemDefinition def, CharacterStats stats)
    {
        if (def == null || !def.IsFood || !IsAlchemistsBoonActive(stats))
            return def != null && def.consumableStats.foodEnableOverheal
                ? def.consumableStats.foodOverhealMaxAboveMaxHp
                : 0;

        int passiveCap = stats != null
            ? Mathf.Max(1, Mathf.RoundToInt(stats.MaxHP * AbilityCombatPower.AlchemistsBoonFoodOverhealMaxHpFraction))
            : 0;

        int configured = def.consumableStats.foodEnableOverheal
            ? def.consumableStats.foodOverhealMaxAboveMaxHp
            : 0;

        return Mathf.Max(configured, passiveCap);
    }

    public static float GetEffectiveUseCooldown(ItemDefinition def, CharacterStats stats)
    {
        if (def == null || !def.IsConsumable)
            return 0f;

        float cooldown = def.UseCooldown;
        if (cooldown <= 0f || !IsAlchemistsBoonActive(stats))
            return cooldown;

        int pick = stats.GetAlchemistsBoonEnhancementPick();
        return AbilityCombatPower.GetAlchemistsBoonEffectiveCooldownSeconds(
            cooldown, pick, def.IsFood, def.IsPotion);
    }

    public static ConsumableGrantedEffect GetEffectiveGrantedEffect(ItemDefinition def, CharacterStats stats)
    {
        if (def == null || !def.HasGrantedEffect)
            return default;

        ConsumableGrantedEffect effect = def.GrantedEffect;
        effect.duration = GetEffectiveGrantedEffectDuration(def, stats);
        return effect;
    }
}
