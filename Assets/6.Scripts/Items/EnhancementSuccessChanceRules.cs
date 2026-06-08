using UnityEngine;

public static class EnhancementSuccessChanceRules
{
    private static readonly float[] WeaponArmourChances = { 0.70f, 0.60f, 0.50f, 0.40f, 0.30f, 0.20f, 0.10f, 0.05f };
    private static readonly float[] ToolChances = { 0.70f, 0.50f, 0.30f };

    public const float ChaosSuccessChance = 0.35f;

    public static float GetSuccessChance(ItemDefinition gear, EnhancementOptionEntry option)
    {
        if (option == null)
            return 0f;

        if (option.track == EnhancementTrack.Corruption)
            return ChaosSuccessChance;

        if (option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return Mathf.Clamp01(option.successChance);

        return GetStandardSuccessChance(gear, gear != null ? gear.SuccessfulEnhancements : 0);
    }

    public static float GetStandardSuccessChance(ItemDefinition gear, int successfulEnhancements)
    {
        int index = Mathf.Max(0, successfulEnhancements);

        if (gear != null && gear.IsTool)
        {
            int clamped = Mathf.Clamp(index, 0, ToolChances.Length - 1);
            return ToolChances[clamped];
        }

        int gearIndex = Mathf.Clamp(index, 0, WeaponArmourChances.Length - 1);
        return WeaponArmourChances[gearIndex];
    }

    public static string FormatSuccessChanceLabel(EnhancementOptionEntry option, ItemDefinition gear)
    {
        if (option == null)
            return string.Empty;

        if (gear != null)
            return FormatPercent(GetSuccessChance(gear, option));

        if (option.track == EnhancementTrack.Corruption)
            return FormatPercent(ChaosSuccessChance);

        if (option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return FormatPercent(Mathf.Clamp01(option.successChance));

        if ((option.allowedGearTypes & EnhancementScrollGearMask.Tool) != 0 &&
            (option.allowedGearTypes & EnhancementScrollGearMask.Weapon) == 0 &&
            (option.allowedGearTypes & EnhancementScrollGearMask.Armor) == 0 &&
            !EnhancementScrollGearRules.MaskTargetsArmorSlots(option.allowedGearTypes))
        {
            return "Varies by item (70%–30%)";
        }

        return "Varies by item (70%–5%)";
    }

    private static string FormatPercent(float chance01)
    {
        float pct = Mathf.Clamp01(chance01) * 100f;
        return pct >= 1f ? $"{Mathf.RoundToInt(pct)}%" : $"{pct:0.#}%";
    }
}
