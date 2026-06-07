using UnityEngine;

public static class UpgradeScrollDisplay
{
    public static string FormatOptionName(ItemDefinition scroll)
    {
        if (scroll == null || !scroll.IsEnhancementScroll)
            return string.Empty;

        EnhancementScrollStats stats = scroll.enhancementScrollStats;
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
        {
            float pct = magnitude * 100f;
            string pctText = pct >= 1f ? $"{Mathf.RoundToInt(pct)}%" : $"{pct:0.#}%";
            return $"{pctText} {statLabel}";
        }

        if (Mathf.Approximately(magnitude, Mathf.Round(magnitude)))
            return $"{Mathf.RoundToInt(magnitude)} flat {statLabel}";

        return $"{magnitude:0.##} flat {statLabel}";
    }

    public static string FormatOptionValueDescription(ItemDefinition scroll)
    {
        if (scroll == null || !scroll.IsEnhancementScroll)
            return string.Empty;

        EnhancementScrollStats stats = scroll.enhancementScrollStats;
        return FormatStatsDescription(stats);
    }

    private static string GetStatShortLabel(EnhancementScrollTargetStat stat)
    {
        return stat switch
        {
            EnhancementScrollTargetStat.PhysicalDamage => "physical damage",
            EnhancementScrollTargetStat.MagicDamage => "magic damage",
            EnhancementScrollTargetStat.CorruptionDamage => "corruption damage",
            EnhancementScrollTargetStat.Health => "health",
            EnhancementScrollTargetStat.Energy => "energy",
            EnhancementScrollTargetStat.Mana => "mana",
            EnhancementScrollTargetStat.Armor => "armour",
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
            or EnhancementScrollTargetStat.StaminaEfficiency;
    }
}
