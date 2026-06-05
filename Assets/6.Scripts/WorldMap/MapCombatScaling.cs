using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Kill-based combat map scaling (levels 1–7). Level 1 is base; thresholds unlock higher tiers.
/// All combat bonuses multiply from the enemy/map base values, not from previously scaled values.
/// </summary>
public static class MapCombatScaling
{
    public const int MinLevel = 1;
    public const int MaxLevel = 7;
    public const int SliderMin = 0;
    public const int SliderMax = 7;

    /// <summary>Kills required to unlock scaling level 2 … 7 (index 0 → level 2, etc.).</summary>
    public static readonly int[] KillThresholdsForLevel = { 200, 500, 1000, 2000, 5000, 10000 };

    public const float HpMultiplierPerLevelAboveBase = 1f;
    public const float XpRateBonusPerLevelAboveBase = 0.10f;
    public const float LootChanceBonusPerLevelAboveBase = 0.25f;
    public const float GoldBonusPerLevelAboveBase = 0.25f;

    public static int GetUnlockedLevel(int totalKillsOnMap)
    {
        int kills = Math.Max(0, totalKillsOnMap);
        int level = MinLevel;
        for (int i = 0; i < KillThresholdsForLevel.Length; i++)
        {
            if (kills >= KillThresholdsForLevel[i])
                level = i + 2;
        }

        return Math.Clamp(level, MinLevel, MaxLevel);
    }

    public static int GetKillsRequiredForLevel(int level)
    {
        if (level <= MinLevel)
            return 0;
        int index = level - 2;
        if (index < 0 || index >= KillThresholdsForLevel.Length)
            return KillThresholdsForLevel[KillThresholdsForLevel.Length - 1];
        return KillThresholdsForLevel[index];
    }

    /// <summary>Level 1 = 1×, level 2 = 2×, level 3 = 3× base HP.</summary>
    public static float GetHpMultiplier(int scalingLevel) =>
        Math.Max(1, Math.Clamp(scalingLevel, MinLevel, MaxLevel));

    /// <summary>Level 1 = 1× XP rate (0.1 dmg→XP default), level 2 = 1.1×, level 3 = 1.2×, …</summary>
    public static float GetXpRateMultiplier(int scalingLevel)
    {
        int level = Math.Clamp(scalingLevel, MinLevel, MaxLevel);
        return 1f + (level - 1) * XpRateBonusPerLevelAboveBase;
    }

    /// <summary>Level 1 = 1× base drop chance, level 2 = 1.25×, level 3 = 1.5×, …</summary>
    public static float GetLootChanceMultiplier(int scalingLevel)
    {
        int level = Math.Clamp(scalingLevel, MinLevel, MaxLevel);
        return 1f + (level - 1) * LootChanceBonusPerLevelAboveBase;
    }

    /// <summary>Level 1 = 1× base gold, level 2 = 1.25×, level 3 = 1.5×, …</summary>
    public static float GetGoldMultiplier(int scalingLevel)
    {
        int level = Math.Clamp(scalingLevel, MinLevel, MaxLevel);
        return 1f + (level - 1) * GoldBonusPerLevelAboveBase;
    }

    /// <summary>Maps world-map slider value (0–7) to gameplay scaling level (1–7). 0 = base; 1+ adds combat bonuses.</summary>
    public static int GetPlayLevelFromSliderValue(int sliderValue)
    {
        int slider = Mathf.Clamp(sliderValue, SliderMin, SliderMax);
        if (slider <= 0)
            return MinLevel;

        return Mathf.Clamp(slider + 1, 2, MaxLevel);
    }

    /// <summary>Max slider index allowed from kill-unlocked play level.</summary>
    public static int GetMaxSelectableSliderValue(int unlockedLevel) =>
        unlockedLevel <= MinLevel ? SliderMin : Mathf.Clamp(unlockedLevel - 1, SliderMin, SliderMax);

    public static int GetEffectivePlayLevel(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node == null || !node.IsMapCombatScalingEnabled())
            return MinLevel;

        string nodeId = node.nodeId;
        if (string.IsNullOrWhiteSpace(nodeId))
            return MinLevel;

        int kills = progress != null ? progress.GetEnemyKillsOnNode(nodeId) : 0;
        int unlocked = GetUnlockedLevel(kills);
        int selectedSlider = progress != null ? progress.GetCombatMapScalingSelectedTier(nodeId) : 0;
        int requested = GetPlayLevelFromSliderValue(selectedSlider);
        return Mathf.Min(unlocked, requested);
    }

    public static int ResolveActiveScalingLevel(MapNodeDefinition node, WorldMapProgressManager progress) =>
        GetEffectivePlayLevel(node, progress);

    public static string BuildUnlockTiersText(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node == null || !node.IsMapCombatScalingEnabled())
            return string.Empty;

        int kills = progress != null ? progress.GetEnemyKillsOnNode(node.nodeId) : 0;
        int unlocked = GetUnlockedLevel(kills);
        var sb = new StringBuilder();

        sb.AppendLine($"<b><size=14>Total enemies killed in this area: {kills}</size></b>");
        sb.AppendLine();

        for (int scaling = 1; scaling <= KillThresholdsForLevel.Length; scaling++)
        {
            int playLevel = scaling + 1;
            int required = GetKillsRequiredForLevel(playLevel);
            bool tierUnlocked = unlocked >= playLevel;
            int displayKills = tierUnlocked ? required : kills;
            string status = tierUnlocked ? "unlocked" : "locked";
            sb.AppendLine($"Scaling {scaling}: Kills on this map {displayKills}/{required} - {status}");
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildBonusesText(int sliderValue)
    {
        sliderValue = Mathf.Clamp(sliderValue, SliderMin, SliderMax);
        int playLevel = GetPlayLevelFromSliderValue(sliderValue);
        int hpPct = Mathf.RoundToInt((GetHpMultiplier(playLevel) - 1f) * 100f);
        int xpPct = Mathf.RoundToInt((GetXpRateMultiplier(playLevel) - 1f) * 100f);
        int lootPct = Mathf.RoundToInt((GetLootChanceMultiplier(playLevel) - 1f) * 100f);
        int goldPct = Mathf.RoundToInt((GetGoldMultiplier(playLevel) - 1f) * 100f);

        var sb = new StringBuilder();
        sb.AppendLine($"Selected scale bonuses (scaling {sliderValue}):");
        sb.AppendLine($"• Enemy HP +{hpPct}% from base");
        sb.AppendLine($"• Combat XP rate +{xpPct}% from base (elites grant double this scaled XP value)");
        sb.AppendLine($"• Loot drop chance +{lootPct}% from base");
        sb.AppendLine($"• Gold dropped +{goldPct}% from base");
        return sb.ToString().TrimEnd();
    }

    public static string BuildSpecialLootText(MapNodeDefinition node, int sliderValue)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Special loot in this scale tier:");

        if (node == null)
        {
            sb.AppendLine("None.");
            return sb.ToString().TrimEnd();
        }

        int playLevel = GetPlayLevelFromSliderValue(sliderValue);
        var entries = new List<MapScalingSpecialLootEntry>();
        node.CollectCombatScalingSpecialDropsUpToLevel(playLevel, entries);

        if (entries.Count == 0)
        {
            sb.AppendLine("None.");
            return sb.ToString().TrimEnd();
        }

        for (int i = 0; i < entries.Count; i++)
        {
            MapScalingSpecialLootEntry entry = entries[i];
            if (entry?.item == null)
                continue;

            string name = !string.IsNullOrWhiteSpace(entry.item.displayName)
                ? entry.item.displayName.Trim()
                : entry.item.name;
            int pct = Mathf.RoundToInt(entry.dropChance * 100f);
            sb.AppendLine($"• {name} — {pct}%");
        }

        return sb.ToString().TrimEnd();
    }

    public static MapNodeDefinition ResolveActiveCombatMapNode()
    {
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition;

        return ActiveLevelContext.Current;
    }
}

/// <summary>Fixed-chance loot unlocked at a combat map scaling tier (not scaled by tier bonuses).</summary>
[Serializable]
public class MapScalingSpecialLootEntry
{
    public ItemDefinition item;

    [UnityEngine.Range(0f, 1f)]
    [UnityEngine.Tooltip("Independent roll chance. Not affected by map scaling loot multipliers.")]
    public float dropChance = 0.01f;

    [UnityEngine.Min(1)]
    public int amountMin = 1;

    [UnityEngine.Min(1)]
    public int amountMax = 1;
}

/// <summary>Special zone drops that unlock when the map reaches this scaling level (2–7).</summary>
[Serializable]
public class MapScalingLevelSpecialDrops
{
    [UnityEngine.Range(2, MapCombatScaling.MaxLevel)]
    [UnityEngine.Tooltip("Scaling level when these drops become available (cumulative with lower tiers).")]
    public int scalingLevel = 2;

    public List<MapScalingSpecialLootEntry> drops = new();
}
