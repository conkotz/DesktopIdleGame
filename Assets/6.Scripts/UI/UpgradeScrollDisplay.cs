using UnityEngine;

public static class UpgradeScrollDisplay
{
    public static string FormatOptionName(ItemDefinition scroll)
    {
        if (scroll == null || !scroll.IsEnhancementScroll)
            return string.Empty;

        EnhancementScrollStats stats = scroll.GetEffectiveEnhancementScrollStats();
        string statName = ItemDefinition.GetEnhancementScrollTargetStatDisplayName(stats.targetStat);
        string kindName = stats.modifierKind == EnhancementScrollModifierKind.Percent ? "Percent" : "Flat";
        return $"{statName} {kindName}";
    }

    public static string FormatOptionValue(ItemDefinition scroll) =>
        FormatOptionValueDescription(scroll);

    public static string FormatStatsDescription(EnhancementScrollStats stats)
    {
        if (stats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
        {
            int slots = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(stats.modifierValue)));
            return slots == 1 ? "1 used slot" : $"{slots} used slots";
        }

        string statLabel = GetStatShortLabel(stats.targetStat);
        bool displayAsPercent = stats.modifierKind == EnhancementScrollModifierKind.Percent ||
                                IsPercentDisplayedScrollStat(stats.targetStat);
        float magnitude = Mathf.Abs(stats.modifierValue);

        if (displayAsPercent)
            return $"{FormatPercentMagnitude(magnitude)} {statLabel}";

        if (Mathf.Approximately(magnitude, Mathf.Round(magnitude)))
            return $"{Mathf.RoundToInt(magnitude)} flat {statLabel}";

        return $"{magnitude:0.##} flat {statLabel}";
    }

    public static string FormatWeightScaledRangeDescription(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        if (option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return FormatStatsDescription(option.ToScrollStats());

        EnhancementWeightValues weights = option.ResolveWeightValues();
        string statLabel = GetStatShortLabel(option.targetStat);
        bool displayAsPercent = option.modifierKind == EnhancementScrollModifierKind.Percent ||
                                IsPercentDisplayedScrollStat(option.targetStat);

        float lo = Mathf.Min(weights.light, weights.heavy);
        float hi = Mathf.Max(weights.light, weights.heavy);

        if (displayAsPercent)
            return $"{FormatPercentRange(lo, hi)} {statLabel}";

        int loInt = Mathf.RoundToInt(lo);
        int hiInt = Mathf.RoundToInt(hi);
        return loInt == hiInt
            ? $"{loInt} flat {statLabel}"
            : $"{loInt}-{hiInt} flat {statLabel}";
    }

    public static string FormatGenericScrollEffectLabel(EnhancementScrollTargetStat stat)
    {
        if (stat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return "- used upgrade slot";

        return $"+ {GetStatShortLabel(stat)}";
    }

    public static string FormatOptionValueDescription(ItemDefinition scroll)
    {
        if (scroll == null || !scroll.IsEnhancementScroll)
            return string.Empty;

        EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(scroll);
        if (option != null && option.UsesWeightScalingEffective())
            return FormatWeightScaledRangeDescription(option);

        EnhancementScrollStats stats = scroll.GetEffectiveEnhancementScrollStats();
        if (EnhancementWeightScalingRules.IsWeightScaledStat(stats.targetStat))
            return FormatGenericScrollEffectLabel(stats.targetStat);

        return FormatStatsDescription(stats);
    }

    private static string GetStatShortLabel(EnhancementScrollTargetStat stat)
    {
        return stat switch
        {
            EnhancementScrollTargetStat.PhysicalDamage => "physical damage",
            EnhancementScrollTargetStat.MagicDamage => "magic damage",
            EnhancementScrollTargetStat.FireDamage => "fire damage",
            EnhancementScrollTargetStat.IceDamage => "ice damage",
            EnhancementScrollTargetStat.LightningDamage => "lightning damage",
            EnhancementScrollTargetStat.CorruptionDamage => "corruption damage",
            EnhancementScrollTargetStat.Health => "health",
            EnhancementScrollTargetStat.Energy => "energy",
            EnhancementScrollTargetStat.Mana => "mana",
            EnhancementScrollTargetStat.Armour => "armour",
            EnhancementScrollTargetStat.MagicResist => "magic resist",
            EnhancementScrollTargetStat.CorruptionResist => "corruption resist",
            EnhancementScrollTargetStat.CritChance => "crit chance",
            EnhancementScrollTargetStat.CritMultiplier => "crit multi",
            EnhancementScrollTargetStat.AttackSpeed => "attack speed",
            EnhancementScrollTargetStat.LifeSteal => "life steal",
            EnhancementScrollTargetStat.MoveSpeed => "move speed",
            EnhancementScrollTargetStat.GatherSpeed => "gather speed",
            EnhancementScrollTargetStat.GatheringGrit => "gathering grit",
            EnhancementScrollTargetStat.StaminaEfficiency => "stamina efficiency",
            EnhancementScrollTargetStat.PoisonChance => "poison chance",
            EnhancementScrollTargetStat.PoisonMultiplier => "poison multi",
            EnhancementScrollTargetStat.BurnChance => "burn chance",
            EnhancementScrollTargetStat.ChillChance => "chill chance",
            EnhancementScrollTargetStat.ShockChance => "shock chance",
            EnhancementScrollTargetStat.BurnMultiplier => "burn multi",
            EnhancementScrollTargetStat.EnergyEfficiency => "energy efficiency",
            EnhancementScrollTargetStat.FlatGuard => "flat guard",
            EnhancementScrollTargetStat.ManaRegen => "mana regen",
            EnhancementScrollTargetStat.UpgradeSlotReduction => "used slot",
            _ => stat.ToString().ToLowerInvariant()
        };
    }

    public static string FormatSelectedEnhancementLine(ItemDefinition scroll)
    {
        if (scroll == null || !scroll.IsEnhancementScroll)
            return "Enhancement Selected: None";

        string scrollName = !string.IsNullOrWhiteSpace(scroll.displayName)
            ? scroll.displayName.Trim()
            : scroll.itemId;
        string value = FormatOptionValueDescription(scroll);
        return $"Enhancement Selected: {scrollName}: {value}";
    }

    private static bool IsPercentDisplayedScrollStat(EnhancementScrollTargetStat stat)
    {
        return stat is EnhancementScrollTargetStat.GatheringGrit
            or EnhancementScrollTargetStat.StaminaEfficiency
            or EnhancementScrollTargetStat.EnergyEfficiency;
    }

    private static string FormatPercentMagnitude(float magnitude01)
    {
        float pct = Mathf.Abs(magnitude01) * 100f;
        return pct >= 1f ? $"{Mathf.RoundToInt(pct)}%" : $"{pct:0.#}%";
    }

    private static string FormatPercentRange(float lo01, float hi01)
    {
        float loPct = Mathf.Abs(lo01) * 100f;
        float hiPct = Mathf.Abs(hi01) * 100f;
        string loText = loPct >= 1f ? $"{Mathf.RoundToInt(loPct)}%" : $"{loPct:0.#}%";
        string hiText = hiPct >= 1f ? $"{Mathf.RoundToInt(hiPct)}%" : $"{hiPct:0.#}%";
        return loText == hiText ? loText : $"{loText}-{hiText}";
    }
}
