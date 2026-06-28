using System.Text;
using UnityEngine;

/// <summary>
/// Boss combat map scaling (tiers 0–7). Separate from normal combat map scaling bonuses.
/// </summary>
public static class MapBossCombatScaling
{
    /// <summary>Kills required to unlock boss scaling tier 1 … 7 (index 0 → tier 1, etc.).</summary>
    public static readonly int[] KillThresholdsForLevel = { 3, 10, 25, 50, 100, 250, 500 };

    public const float HpBonusPerSliderTier = 0.50f;
    public const float DamageBonusPerSliderTier = 0.20f;
    public const float LootBonusPerSliderTier = 0.10f;
    public const float GoldBonusPerSliderTier = 0.20f;

    public static float GetHpMultiplier(int sliderValue)
    {
        int tier = Mathf.Clamp(sliderValue, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        return 1f + tier * HpBonusPerSliderTier;
    }

    public static float GetDamageMultiplier(int sliderValue)
    {
        int tier = Mathf.Clamp(sliderValue, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        return 1f + tier * DamageBonusPerSliderTier;
    }

    public static float GetLootChanceMultiplier(int sliderValue)
    {
        int tier = Mathf.Clamp(sliderValue, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        return 1f + tier * LootBonusPerSliderTier;
    }

    public static float GetGoldMultiplier(int sliderValue)
    {
        int tier = Mathf.Clamp(sliderValue, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        return 1f + tier * GoldBonusPerSliderTier;
    }

    public static string BuildBonusesText(int sliderValue)
    {
        sliderValue = Mathf.Clamp(sliderValue, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        int hpPct = Mathf.RoundToInt((GetHpMultiplier(sliderValue) - 1f) * 100f);
        int dmgPct = Mathf.RoundToInt((GetDamageMultiplier(sliderValue) - 1f) * 100f);
        int lootPct = Mathf.RoundToInt((GetLootChanceMultiplier(sliderValue) - 1f) * 100f);
        int goldPct = Mathf.RoundToInt((GetGoldMultiplier(sliderValue) - 1f) * 100f);

        var sb = new StringBuilder();
        sb.AppendLine($"Selected scale bonuses (scaling {sliderValue}):");
        sb.AppendLine($"• Enemy HP +{hpPct}% from base");
        sb.AppendLine($"• Enemy damage +{dmgPct}% from base");
        sb.AppendLine($"• Loot drop chance +{lootPct}% from base");
        sb.AppendLine($"• Gold dropped +{goldPct}% from base");
        return sb.ToString().TrimEnd();
    }

    public static string BuildEditorPreviewText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Kill unlock tiers: 3 / 10 / 25 / 50 / 100 / 250 / 500 on this map.");
        sb.AppendLine("Per selected scaling tier (from base):");
        sb.AppendLine($"• HP +{Mathf.RoundToInt(HpBonusPerSliderTier * 100f)}% per tier");
        sb.AppendLine($"• Damage +{Mathf.RoundToInt(DamageBonusPerSliderTier * 100f)}% per tier");
        sb.AppendLine($"• Loot drop chance +{Mathf.RoundToInt(LootBonusPerSliderTier * 100f)}% per tier");
        sb.AppendLine($"• Gold dropped +{Mathf.RoundToInt(GoldBonusPerSliderTier * 100f)}% per tier");
        sb.AppendLine();
        sb.AppendLine("Example at scaling 7: HP +350%, damage +140%, loot +70%, gold +140%.");
        sb.AppendLine("Loot uses the same multiplier logic as normal maps (0.05 × 2.0 = 0.10).");
        return sb.ToString().TrimEnd();
    }
}
