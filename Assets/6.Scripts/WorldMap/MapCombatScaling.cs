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

    public static int[] ResolveKillThresholds(MapNodeDefinition node) =>
        node != null && node.UsesBossAreaScaling()
            ? MapBossCombatScaling.KillThresholdsForLevel
            : KillThresholdsForLevel;

    public const float HpMultiplierPerLevelAboveBase = 1f;
    public const float XpRateBonusPerLevelAboveBase = 0.10f;
    public const float LootChanceBonusPerLevelAboveBase = 0.25f;
    public const float GoldBonusPerLevelAboveBase = 0.25f;

    public static int GetUnlockedLevel(int totalKillsOnMap, MapNodeDefinition node = null)
    {
        int[] thresholds = ResolveKillThresholds(node);
        int kills = Math.Max(0, totalKillsOnMap);
        int level = MinLevel;
        for (int i = 0; i < thresholds.Length; i++)
        {
            if (kills >= thresholds[i])
                level = i + 2;
        }

        return Math.Clamp(level, MinLevel, MaxLevel);
    }

    public static int GetKillsRequiredForLevel(int level, MapNodeDefinition node = null)
    {
        if (level <= MinLevel)
            return 0;

        int[] thresholds = ResolveKillThresholds(node);
        int index = level - 2;
        if (index < 0 || index >= thresholds.Length)
            return thresholds[thresholds.Length - 1];
        return thresholds[index];
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

    /// <summary>Total extra enemy spawns from map scaling at this slider tier (+1 per tier above base).</summary>
    public static int GetExtraSpawnCountFromSlider(int sliderValue)
    {
        int slider = Mathf.Clamp(sliderValue, SliderMin, SliderMax);
        return slider <= SliderMin ? 0 : slider;
    }

    /// <summary>Selected slider tier clamped by kill unlocks (same cap used for active combat bonuses).</summary>
    public static int GetEffectiveSliderValue(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node == null || !node.IsMapCombatScalingEnabled())
            return SliderMin;

        string nodeId = node.nodeId;
        if (string.IsNullOrWhiteSpace(nodeId))
            return SliderMin;

        progress ??= WorldMapProgressManager.Instance;
        int kills = progress != null ? progress.GetEnemyKillsOnNode(nodeId) : 0;
        int unlocked = GetUnlockedLevel(kills, node);
        int selected = progress != null ? progress.GetCombatMapScalingSelectedTier(nodeId) : SliderMin;
        int maxSlider = GetMaxSelectableSliderValue(unlocked, kills, node);
        return Mathf.Clamp(selected, SliderMin, maxSlider);
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
    public static int GetMaxSelectableSliderValue(int unlockedLevel, int totalKillsOnMap = 0, MapNodeDefinition node = null)
    {
        if (unlockedLevel <= MinLevel)
            return SliderMin;

        int kills = Math.Max(0, totalKillsOnMap);
        int[] thresholds = ResolveKillThresholds(node);
        int maxFromLevel = unlockedLevel >= MaxLevel
            ? SliderMax - 1
            : Mathf.Clamp(unlockedLevel - 1, SliderMin, SliderMax);

        if (thresholds.Length > 0 && kills >= thresholds[thresholds.Length - 1])
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
        int unlocked = GetUnlockedLevel(kills, node);
        int selectedSlider = progress != null ? progress.GetCombatMapScalingSelectedTier(nodeId) : 0;
        int requested = GetPlayLevelFromSliderValue(selectedSlider);
        return Mathf.Min(unlocked, requested);
    }

    public static int ResolveActiveScalingLevel(MapNodeDefinition node, WorldMapProgressManager progress) =>
        GetEffectivePlayLevel(node, progress);

    public static float ResolveHpMultiplier(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node != null && node.UsesBossAreaScaling())
        {
            int slider = GetEffectiveSliderValue(node, progress);
            return MapBossCombatScaling.GetHpMultiplier(slider);
        }

        return GetHpMultiplier(GetEffectivePlayLevel(node, progress));
    }

    public static float ResolveDamageMultiplier(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node != null && node.UsesBossAreaScaling())
        {
            int slider = GetEffectiveSliderValue(node, progress);
            return MapBossCombatScaling.GetDamageMultiplier(slider);
        }

        return 1f;
    }

    public static float ResolveLootChanceMultiplier(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node != null && node.UsesBossAreaScaling())
        {
            int slider = GetEffectiveSliderValue(node, progress);
            return MapBossCombatScaling.GetLootChanceMultiplier(slider);
        }

        return GetLootChanceMultiplier(GetEffectivePlayLevel(node, progress));
    }

    public static float ResolveGoldMultiplier(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node != null && node.UsesBossAreaScaling())
        {
            int slider = GetEffectiveSliderValue(node, progress);
            return MapBossCombatScaling.GetGoldMultiplier(slider);
        }

        return GetGoldMultiplier(GetEffectivePlayLevel(node, progress));
    }

    public static float ResolveXpRateMultiplier(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node != null && node.UsesBossAreaScaling())
            return 1f;

        return GetXpRateMultiplier(GetEffectivePlayLevel(node, progress));
    }

    public static string BuildUnlockTiersText(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node == null || !node.IsMapCombatScalingEnabled())
            return string.Empty;

        int kills = progress != null ? progress.GetEnemyKillsOnNode(node.nodeId) : 0;
        var sb = new StringBuilder();

        sb.AppendLine($"<b><size=14>Total enemies killed in this area: {kills}</size></b>");
        sb.AppendLine();

        int[] thresholds = ResolveKillThresholds(node);
        for (int scaling = 1; scaling <= thresholds.Length; scaling++)
        {
            int required = thresholds[scaling - 1];
            bool tierUnlocked = kills >= required;
            int displayKills = tierUnlocked ? required : kills;
            string status = tierUnlocked ? "unlocked" : "locked";
            string color = tierUnlocked ? "#55CC55" : "#DD4444";
            sb.AppendLine(
                $"<color={color}>Scaling {scaling}: Kills on this map {displayKills}/{required} - {status}</color>");
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildBonusesText(int sliderValue, MapNodeDefinition node = null)
    {
        if (node != null && node.UsesBossAreaScaling())
            return MapBossCombatScaling.BuildBonusesText(sliderValue);

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

        int extraSpawns = GetExtraSpawnCountFromSlider(sliderValue);
        if (extraSpawns > 0)
        {
            string spawnLabel = extraSpawns == 1 ? "spawn" : "spawns";
            sb.AppendLine($"• +{extraSpawns} additional enemy {spawnLabel}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Extra scaling spawns per enemy id, distributed round-robin across distinct map enemy types.</summary>
    public static Dictionary<string, int> BuildScalingExtraSpawnsByEnemyId(MapNodeDefinition map, int sliderValue)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (map == null)
            return result;

        int totalExtras = GetExtraSpawnCountFromSlider(sliderValue);
        if (totalExtras <= 0)
            return result;

        List<string> orderedEnemyIds = CollectDistinctEnemyIdsInSpawnOrder(map);
        if (orderedEnemyIds.Count == 0)
            return result;

        for (int i = 0; i < totalExtras; i++)
        {
            string enemyId = orderedEnemyIds[i % orderedEnemyIds.Count];
            result.TryGetValue(enemyId, out int current);
            result[enemyId] = current + 1;
        }

        return result;
    }

    public static List<MapEnemySpawnCountEntry> BuildEnemySpawnCounts(
        MapNodeDefinition map,
        int sliderValue,
        MapEnhancementAggregate enhancements = null)
    {
        var entries = new List<MapEnemySpawnCountEntry>();
        if (map?.spawnGroupPlans == null || map.spawnGroupPlans.Count == 0)
            return entries;

        var baseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var spawnOrder = new List<string>();

        for (int p = 0; p < map.spawnGroupPlans.Count; p++)
        {
            LevelSpawnGroupPlan plan = map.spawnGroupPlans[p];
            if (plan?.spawns == null)
                continue;

            for (int s = 0; s < plan.spawns.Count; s++)
            {
                SpawnPrefabCount row = plan.spawns[s];
                if (row?.enemyDefinition == null || row.count <= 0)
                    continue;

                string enemyId = row.enemyDefinition.enemyId?.Trim();
                if (string.IsNullOrEmpty(enemyId))
                    continue;

                baseCounts.TryGetValue(enemyId, out int current);
                baseCounts[enemyId] = current + row.count;

                if (!displayNames.ContainsKey(enemyId))
                {
                    displayNames[enemyId] = ResolveEnemyDisplayName(row.enemyDefinition);
                    spawnOrder.Add(enemyId);
                }
            }
        }

        if (spawnOrder.Count == 0)
            return entries;

        Dictionary<string, int> scalingExtras = map.UsesNormalMapCombatScaling()
            ? BuildScalingExtraSpawnsByEnemyId(map, sliderValue)
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < spawnOrder.Count; i++)
        {
            string enemyId = spawnOrder[i];
            int count = baseCounts.TryGetValue(enemyId, out int baseCount) ? baseCount : 0;

            if (scalingExtras.TryGetValue(enemyId, out int scaleExtra))
                count += scaleExtra;

            if (enhancements?.extraSpawnsByEnemyId != null &&
                enhancements.extraSpawnsByEnemyId.TryGetValue(enemyId, out int enhancementExtra))
                count += enhancementExtra;

            entries.Add(new MapEnemySpawnCountEntry(displayNames[enemyId], count));
        }

        return entries;
    }

    public static string BuildCombatLocationTypeRichText(
        MapNodeDefinition map,
        int sliderValue,
        MapEnhancementAggregate enhancements = null)
    {
        List<MapEnemySpawnCountEntry> counts = BuildEnemySpawnCounts(map, sliderValue, enhancements);
        string typeLabel = map != null && map.isBossMap ? "Boss Combat" : "Combat";
        if (counts.Count == 0)
            return typeLabel;

        var suffix = new StringBuilder();
        suffix.Append(" - ");
        for (int i = 0; i < counts.Count; i++)
        {
            if (i > 0)
                suffix.Append(", ");

            MapEnemySpawnCountEntry entry = counts[i];
            suffix.Append(entry.displayName);
            suffix.Append(" (x");
            suffix.Append(entry.count);
            suffix.Append(')');
        }

        return $"{typeLabel}<size=75%>{suffix}</size>";
    }

    public static List<string> CollectDistinctEnemyIdsInSpawnOrder(MapNodeDefinition map)
    {
        var orderedIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (map?.spawnGroupPlans == null)
            return orderedIds;

        for (int p = 0; p < map.spawnGroupPlans.Count; p++)
        {
            LevelSpawnGroupPlan plan = map.spawnGroupPlans[p];
            if (plan?.spawns == null)
                continue;

            for (int s = 0; s < plan.spawns.Count; s++)
            {
                SpawnPrefabCount row = plan.spawns[s];
                if (row?.enemyDefinition == null || row.count <= 0)
                    continue;

                string enemyId = row.enemyDefinition.enemyId?.Trim();
                if (string.IsNullOrEmpty(enemyId) || seen.Contains(enemyId))
                    continue;

                seen.Add(enemyId);
                orderedIds.Add(enemyId);
            }
        }

        return orderedIds;
    }

    private static string ResolveEnemyDisplayName(EnemyDefinition def)
    {
        if (def == null)
            return "Enemy";

        if (!string.IsNullOrWhiteSpace(def.displayName))
            return def.displayName.Trim();

        if (!string.IsNullOrWhiteSpace(def.enemyId))
            return def.enemyId.Trim();

        return def.name;
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

        if (node.IsMapCombatScalingEnabled() && sliderValue >= 2 && !node.UsesBossAreaScaling())
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

public readonly struct MapEnemySpawnCountEntry
{
    public readonly string displayName;
    public readonly int count;

    public MapEnemySpawnCountEntry(string displayName, int count)
    {
        this.displayName = displayName;
        this.count = count;
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
