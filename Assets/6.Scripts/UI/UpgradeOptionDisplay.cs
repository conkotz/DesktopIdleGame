using UnityEngine;

public static class UpgradeOptionDisplay
{
    public static string FormatOptionType(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(option.displayName))
            return option.displayName.Trim();

        string stat = ItemDefinition.GetEnhancementScrollTargetStatDisplayName(option.targetStat);
        return $"{stat} {EnhancementTierRules.GetTierDisplayName(option.tier)}";
    }

    public static string FormatOptionValue(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        return UpgradeScrollDisplay.FormatStatsDescription(option.ToScrollStats());
    }

    public static string FormatSelectedEnhancementLine(
        EnhancementOptionEntry option,
        EnhancementOptionPayment payment,
        ItemDatabase itemDb)
    {
        if (option == null)
            return "Enhancement Selected: None";

        string type = FormatOptionType(option);
        string value = FormatOptionValue(option);
        string cost = EnhancementOptionPayment.FormatCostLabel(payment, itemDb);
        if (payment.Kind == EnhancementPaymentKind.None)
            return $"Enhancement Selected: {type}: {value}";

        return $"Enhancement Selected: {type}: {value} ({cost})";
    }

    public static string FormatSectionTitle(EnhancementTrack track)
    {
        return track switch
        {
            EnhancementTrack.Corruption => "Corruption Upgrades",
            _ => "Standard Upgrades",
        };
    }
}
