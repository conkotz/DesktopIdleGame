using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Default special drops applied to every combat map with scaling enabled.
/// Scroll categories roll once per kill at the listed chance, then pick a random scroll from that pool.
/// Map enhancements and slot reduction scrolls use fixed per-item chances.
/// </summary>
public static class MapCombatScalingSpecialDropDefaults
{
    public const float ChaosDropChance = 0.001f;

    public const string MapEnhancementTier1ItemId = "map_enhancement_tier1";
    public const string MapEnhancementTier2ItemId = "map_enhancement_tier2";
    public const string SlotReductionScrollItemId = "slot_reduction_scroll";

    public static bool TryGetScrollDisplayGroupLabel(ItemDefinition item, out string label)
    {
        label = null;
        if (item == null || !item.IsEnhancementScroll)
            return false;

        EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(item);
        if (option == null || option.track == EnhancementTrack.Special)
            return false;

        if (option.track == EnhancementTrack.Corruption)
        {
            label = "Chaos Enhancement scrolls";
            return true;
        }

        label = option.tier switch
        {
            EnhancementTier.Basic => "Basic Enhancement scrolls",
            EnhancementTier.Intermediate => "Intermediate Enhancement scrolls",
            EnhancementTier.Advanced => "Advanced Enhancement scrolls",
            _ => null,
        };

        return !string.IsNullOrWhiteSpace(label);
    }

    public static int GetScrollDisplayGroupSortOrder(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return 99;

        if (label.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (label.StartsWith("Intermediate", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (label.StartsWith("Advanced", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (label.StartsWith("Chaos", StringComparison.OrdinalIgnoreCase))
            return 3;

        return 99;
    }

    public static void CollectGroupRollsForScalingLevel(int scalingLevel, List<MapScalingSpecialLootGroupRoll> results)
    {
        if (results == null || scalingLevel < 2)
            return;

        ItemDatabase itemDb = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        EnhancementOptionDatabase optionDb = EnhancementOptionResolver.GetDatabase();
        if (itemDb == null || optionDb?.Options == null)
            return;

        int tier = Mathf.Clamp(scalingLevel, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        AppendGroupRollsForScalingLevel(tier, results, itemDb, optionDb);
    }

    public static void CollectIndividualDropsForScalingLevel(int scalingLevel, List<MapScalingSpecialLootEntry> results)
    {
        if (results == null || scalingLevel < 2)
            return;

        ItemDatabase itemDb = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        if (itemDb == null)
            return;

        int tier = Mathf.Clamp(scalingLevel, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        AppendIndividualDropsForScalingLevel(tier, results, itemDb);
    }

    [Obsolete("Use CollectGroupRollsForScalingLevel.")]
    public static void CollectForScalingLevel(int scalingLevel, List<MapScalingSpecialLootEntry> results) =>
        CollectLegacyIndividualScrollEntries(scalingLevel, results);

    [Obsolete("Scroll defaults now use grouped rolls.")]
    public static void CollectLegacyIndividualScrollEntries(int scalingLevel, List<MapScalingSpecialLootEntry> results)
    {
        if (results == null || scalingLevel < 2)
            return;

        ItemDatabase itemDb = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        EnhancementOptionDatabase optionDb = EnhancementOptionResolver.GetDatabase();
        if (itemDb == null || optionDb?.Options == null)
            return;

        int tier = Mathf.Clamp(scalingLevel, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        ResolveChancesForLevel(tier, out float basicChance, out float intermediateChance, out float advancedChance);

        if (basicChance > 0f)
            AppendLegacyIndividualScrollDrops(results, itemDb, optionDb, EnhancementTrack.Standard, EnhancementTier.Basic, basicChance);
        if (intermediateChance > 0f)
            AppendLegacyIndividualScrollDrops(results, itemDb, optionDb, EnhancementTrack.Standard, EnhancementTier.Intermediate, intermediateChance);
        if (advancedChance > 0f)
            AppendLegacyIndividualScrollDrops(results, itemDb, optionDb, EnhancementTrack.Standard, EnhancementTier.Advanced, advancedChance);

        AppendLegacyIndividualScrollDrops(results, itemDb, optionDb, EnhancementTrack.Corruption, tier: null, ChaosDropChance);
    }

    public static void ResolveChancesForLevel(
        int scalingLevel,
        out float basicChance,
        out float intermediateChance,
        out float advancedChance)
    {
        basicChance = 0f;
        intermediateChance = 0f;
        advancedChance = 0f;

        switch (scalingLevel)
        {
            case 2:
                basicChance = 0.002f;
                break;
            case 3:
                basicChance = 0.003f;
                break;
            case 4:
                basicChance = 0.004f;
                break;
            case 5:
                basicChance = 0.004f;
                intermediateChance = 0.001f;
                break;
            case 6:
                basicChance = 0.004f;
                intermediateChance = 0.002f;
                break;
            default:
                if (scalingLevel >= 7)
                {
                    basicChance = 0.004f;
                    intermediateChance = 0.002f;
                    advancedChance = 0.001f;
                }

                break;
        }
    }

    public static float ResolveSlotReductionScrollChanceForLevel(int scalingLevel)
    {
        return scalingLevel switch
        {
            2 => 0.0001f,
            3 => 0.0005f,
            4 => 0.0007f,
            5 => 0.001f,
            6 => 0.0015f,
            >= 7 => 0.002f,
            _ => 0f,
        };
    }

    public static void ResolveMapEnhancementChancesForLevel(
        int scalingLevel,
        out float tier1Chance,
        out float tier2Chance)
    {
        tier1Chance = 0f;
        tier2Chance = 0f;

        switch (scalingLevel)
        {
            case 3:
                tier1Chance = 0.001f;
                break;
            case 4:
                tier1Chance = 0.0015f;
                break;
            case 5:
                tier1Chance = 0.002f;
                break;
            case 6:
                tier1Chance = 0.0025f;
                tier2Chance = 0.0005f;
                break;
            default:
                if (scalingLevel >= 7)
                {
                    tier1Chance = 0.004f;
                    tier2Chance = 0.001f;
                }

                break;
        }
    }

    public static IReadOnlyList<string> BuildSummaryLinesForScalingLevel(int scalingLevel)
    {
        var lines = new List<string>(8);
        if (scalingLevel < 2)
            return lines;

        ResolveChancesForLevel(scalingLevel, out float basicChance, out float intermediateChance, out float advancedChance);

        if (basicChance > 0f)
            lines.Add($"Basic Enhancement scrolls — {MapCombatScaling.FormatSpecialDropChancePercent(basicChance)}");
        if (intermediateChance > 0f)
            lines.Add($"Intermediate Enhancement scrolls — {MapCombatScaling.FormatSpecialDropChancePercent(intermediateChance)}");
        if (advancedChance > 0f)
            lines.Add($"Advanced Enhancement scrolls — {MapCombatScaling.FormatSpecialDropChancePercent(advancedChance)}");

        lines.Add($"Chaos Enhancement scrolls — {MapCombatScaling.FormatSpecialDropChancePercent(ChaosDropChance)}");

        ResolveMapEnhancementChancesForLevel(scalingLevel, out float tier1Chance, out float tier2Chance);
        if (tier1Chance > 0f)
            lines.Add($"Map Enhancement (Tier 1) — {MapCombatScaling.FormatSpecialDropChancePercent(tier1Chance)}");
        if (tier2Chance > 0f)
            lines.Add($"Map Enhancement (Tier 2) — {MapCombatScaling.FormatSpecialDropChancePercent(tier2Chance)}");

        float reductionChance = ResolveSlotReductionScrollChanceForLevel(scalingLevel);
        if (reductionChance > 0f)
            lines.Add($"Slot Reduction Scroll — {MapCombatScaling.FormatSpecialDropChancePercent(reductionChance)}");

        return lines;
    }

    public static string BuildEditorPreviewText()
    {
        var sb = new StringBuilder();
        for (int level = 2; level <= MapCombatScaling.SliderMax; level++)
        {
            sb.AppendLine($"Scaling {level}");
            IReadOnlyList<string> lines = BuildSummaryLinesForScalingLevel(level);
            for (int i = 0; i < lines.Count; i++)
                sb.AppendLine($"  • {lines[i]}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendGroupRollsForScalingLevel(
        int scalingLevel,
        List<MapScalingSpecialLootGroupRoll> results,
        ItemDatabase itemDb,
        EnhancementOptionDatabase optionDb)
    {
        if (results == null || itemDb == null || optionDb?.Options == null || scalingLevel < 2)
            return;

        ResolveChancesForLevel(scalingLevel, out float basicChance, out float intermediateChance, out float advancedChance);

        if (basicChance > 0f)
            AppendScrollGroupRoll(results, itemDb, optionDb, EnhancementTrack.Standard, EnhancementTier.Basic, basicChance);
        if (intermediateChance > 0f)
            AppendScrollGroupRoll(results, itemDb, optionDb, EnhancementTrack.Standard, EnhancementTier.Intermediate, intermediateChance);
        if (advancedChance > 0f)
            AppendScrollGroupRoll(results, itemDb, optionDb, EnhancementTrack.Standard, EnhancementTier.Advanced, advancedChance);

        AppendScrollGroupRoll(results, itemDb, optionDb, EnhancementTrack.Corruption, tier: null, ChaosDropChance);
    }

    private static void AppendIndividualDropsForScalingLevel(
        int scalingLevel,
        List<MapScalingSpecialLootEntry> results,
        ItemDatabase itemDb)
    {
        if (results == null || itemDb == null || scalingLevel < 2)
            return;

        ResolveMapEnhancementChancesForLevel(scalingLevel, out float tier1Chance, out float tier2Chance);
        AppendFixedItemDrop(results, itemDb, MapEnhancementTier1ItemId, tier1Chance);
        AppendFixedItemDrop(results, itemDb, MapEnhancementTier2ItemId, tier2Chance);

        float reductionChance = ResolveSlotReductionScrollChanceForLevel(scalingLevel);
        AppendFixedItemDrop(results, itemDb, SlotReductionScrollItemId, reductionChance);
    }

    private static void AppendFixedItemDrop(
        List<MapScalingSpecialLootEntry> results,
        ItemDatabase itemDb,
        string itemId,
        float dropChance)
    {
        if (dropChance <= 0f || string.IsNullOrWhiteSpace(itemId))
            return;

        ItemDefinition item = itemDb.Get(itemId.Trim());
        if (item == null)
            return;

        results.Add(new MapScalingSpecialLootEntry
        {
            item = item,
            dropChance = dropChance,
            amountMin = 1,
            amountMax = 1,
        });
    }

    private static void AppendScrollGroupRoll(
        List<MapScalingSpecialLootGroupRoll> results,
        ItemDatabase itemDb,
        EnhancementOptionDatabase optionDb,
        EnhancementTrack track,
        EnhancementTier? tier,
        float dropChance)
    {
        if (dropChance <= 0f)
            return;

        var pool = BuildScrollPool(itemDb, optionDb, track, tier);
        if (pool.Count == 0)
            return;

        results.Add(new MapScalingSpecialLootGroupRoll
        {
            dropChance = dropChance,
            itemPool = pool,
        });
    }

    private static List<ItemDefinition> BuildScrollPool(
        ItemDatabase itemDb,
        EnhancementOptionDatabase optionDb,
        EnhancementTrack track,
        EnhancementTier? tier)
    {
        var pool = new List<ItemDefinition>();
        IReadOnlyList<EnhancementOptionEntry> options = optionDb.Options;
        for (int i = 0; i < options.Count; i++)
        {
            EnhancementOptionEntry option = options[i];
            if (option == null || option.track != track)
                continue;

            if (track == EnhancementTrack.Special)
                continue;

            if (tier.HasValue && option.tier != tier.Value)
                continue;

            if (string.IsNullOrWhiteSpace(option.linkedScrollItemId))
                continue;

            ItemDefinition item = itemDb.Get(option.linkedScrollItemId.Trim());
            if (item != null)
                pool.Add(item);
        }

        return pool;
    }

    private static void AppendLegacyIndividualScrollDrops(
        List<MapScalingSpecialLootEntry> results,
        ItemDatabase itemDb,
        EnhancementOptionDatabase optionDb,
        EnhancementTrack track,
        EnhancementTier? tier,
        float dropChance)
    {
        List<ItemDefinition> pool = BuildScrollPool(itemDb, optionDb, track, tier);
        for (int i = 0; i < pool.Count; i++)
        {
            results.Add(new MapScalingSpecialLootEntry
            {
                item = pool[i],
                dropChance = dropChance,
                amountMin = 1,
                amountMax = 1,
            });
        }
    }
}
