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
    public static readonly int[] KillThresholdsForLevel = { 200, 500, 1000, 2000, 5000, 10000, 20000 };

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

    /// <summary>Max slider index allowed from kill-unlocked play level and total kills on this map.</summary>
    public static int GetMaxSelectableSliderValue(int unlockedLevel, int totalKillsOnMap = 0)
    {
        if (unlockedLevel <= MinLevel)
            return SliderMin;

        int kills = Math.Max(0, totalKillsOnMap);
        int maxFromLevel = unlockedLevel >= MaxLevel
            ? SliderMax - 1
            : Mathf.Clamp(unlockedLevel - 1, SliderMin, SliderMax);

        if (KillThresholdsForLevel.Length > 0 &&
            kills >= KillThresholdsForLevel[KillThresholdsForLevel.Length - 1])
            return SliderMax;

        return maxFromLevel;
    }

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
        var sb = new StringBuilder();

        sb.AppendLine($"<b><size=14>Total enemies killed in this area: {kills}</size></b>");
        sb.AppendLine();

        for (int scaling = 1; scaling <= KillThresholdsForLevel.Length; scaling++)
        {
            int required = KillThresholdsForLevel[scaling - 1];
            bool tierUnlocked = kills >= required;
            int displayKills = tierUnlocked ? required : kills;
            string status = tierUnlocked ? "unlocked" : "locked";
            string color = tierUnlocked ? "#55CC55" : "#DD4444";
            sb.AppendLine(
                $"<color={color}>Scaling {scaling}: Kills on this map {displayKills}/{required} - {status}</color>");
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
        sb.AppendLine("Special loot in this scale tier (these values do not get scaled):");

        if (node == null)
        {
            sb.AppendLine("None.");
            return sb.ToString().TrimEnd();
        }

        var entries = new List<MapScalingSpecialLootEntry>();
        node.CollectMapSpecificSpecialDrops(sliderValue, entries);

        var customLines = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            MapScalingSpecialLootEntry entry = entries[i];
            if (entry?.item == null)
                continue;

            string name = !string.IsNullOrWhiteSpace(entry.item.displayName)
                ? entry.item.displayName.Trim()
                : entry.item.name;
            customLines.Add($"• {name} — {FormatSpecialDropChancePercent(entry.dropChance)}");
        }

        if (node.IsMapCombatScalingEnabled() && sliderValue >= 2)
        {
            IReadOnlyList<string> defaultLines = MapCombatScalingSpecialDropDefaults.BuildSummaryLinesForScalingLevel(sliderValue);
            for (int i = 0; i < defaultLines.Count; i++)
                customLines.Add($"• {defaultLines[i]}");
        }

        if (customLines.Count == 0)
        {
            sb.AppendLine("None.");
            return sb.ToString().TrimEnd();
        }

        for (int i = 0; i < customLines.Count; i++)
            sb.AppendLine(customLines[i]);

        return sb.ToString().TrimEnd();
    }

    public static string FormatSpecialDropChancePercent(float dropChance)
    {
        float pct = Mathf.Max(0f, dropChance) * 100f;
        if (pct >= 1f)
            return $"{Mathf.RoundToInt(pct)}%";
        if (pct >= 0.1f)
            return $"{pct:0.#}%";
        return $"{pct:0.##}%";
    }

    public static MapNodeDefinition ResolveActiveCombatMapNode()
    {
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition;

        return ActiveLevelContext.Current;
    }

    public static string BuildLocationDisplayName(MapNodeDefinition node, WorldMapProgressManager progress = null)
    {
        if (node == null)
            return string.Empty;

        string name = !string.IsNullOrWhiteSpace(node.displayName) ? node.displayName.Trim() : node.nodeId?.Trim() ?? string.Empty;
        if (!node.IsMapCombatScalingEnabled())
            return name;

        progress ??= WorldMapProgressManager.Instance;
        int slider = progress != null ? progress.GetCombatMapScalingSelectedTier(node.nodeId) : SliderMin;
        if (slider <= SliderMin)
            return name;

        return $"{name} - Scale {slider}";
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

/// <summary>
/// One drop chance per category; on success a random item from <see cref="itemPool"/> is granted.
/// </summary>
[Serializable]
public class MapScalingSpecialLootGroupRoll
{
    [UnityEngine.Range(0f, 1f)]
    public float dropChance;

    public List<ItemDefinition> itemPool = new();

    public ItemDefinition RollRandomItem()
    {
        if (itemPool == null || itemPool.Count == 0)
            return null;

        return itemPool[UnityEngine.Random.Range(0, itemPool.Count)];
    }
}

/// <summary>Special zone drops that unlock when the map reaches this scaling level (2–7).</summary>
[Serializable]
public class MapScalingLevelSpecialDrops
{
    [UnityEngine.Range(1, MapCombatScaling.SliderMax)]
    [UnityEngine.Tooltip("Map scaling slider value when these drops apply. Only this tier's entries roll when that tier is selected.")]
    public int scalingLevel = 3;

    public List<MapScalingSpecialLootEntry> drops = new();
}
