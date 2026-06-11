using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Resolves where items can be obtained for database rows.</summary>
public static class DatabaseItemSourceCatalog
{
    private const string EnemyDatabaseResourcePath = "Databases/EnemyDatabase";
    private const string WorldMapResourcePath = "Databases/WorldMap_Main";
    private const string QuestDatabaseResourcePath = "Databases/QuestDatabase_Main";
    private const string ItemDatabaseResourcePath = "Databases/ItemDatabase";

    private static Dictionary<string, HashSet<string>> _sourcesByItemId;
    private static bool _built;

    public static string FormatObtainLocations(ItemDefinition item)
    {
        if (!item || string.IsNullOrWhiteSpace(item.itemId))
            return string.Empty;

        EnsureBuilt();

        string key = NormalizeItemId(item.itemId);
        if (string.IsNullOrEmpty(key) ||
            !_sourcesByItemId.TryGetValue(key, out HashSet<string> sources) ||
            sources == null ||
            sources.Count == 0)
        {
            return string.Empty;
        }

        var list = new List<string>(sources);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", list);
    }

    public static void Invalidate() => _built = false;

    private static void EnsureBuilt()
    {
        if (_built)
            return;

        _sourcesByItemId = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        ItemDatabase itemDb = Resources.Load<ItemDatabase>(ItemDatabaseResourcePath);
        EnemyDatabase enemyDb = Resources.Load<EnemyDatabase>(EnemyDatabaseResourcePath);
        WorldMapDefinition worldMap = Resources.Load<WorldMapDefinition>(WorldMapResourcePath);
        QuestDatabase questDb = Resources.Load<QuestDatabase>(QuestDatabaseResourcePath);

        if (itemDb != null)
            ScanCookingAndOpenables(itemDb.GetAll());

        if (enemyDb != null)
            ScanEnemyLoot(enemyDb.GetAllSortedByDisplayName(), worldMap);

        if (worldMap != null)
            ScanWorldMap(worldMap);

        if (questDb != null)
            ScanQuestRewards(questDb.All);

        _built = true;
    }

    private static void ScanCookingAndOpenables(IReadOnlyList<ItemDefinition> items)
    {
        if (items == null)
            return;

        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition item = items[i];
            if (!item)
                continue;

            CookableStats cookable = item.cookableStats;
            if (cookable.isCookable && !string.IsNullOrWhiteSpace(cookable.cookedResultItemId))
                Register(cookable.cookedResultItemId.Trim(), "Cooking");

            OpenableLootEntry[] openableLoot = item.consumableStats.openableLoot;
            if (openableLoot == null || openableLoot.Length == 0)
                continue;

            string sourceLabel = string.IsNullOrWhiteSpace(item.displayName)
                ? "Openable item"
                : $"Open: {item.displayName.Trim()}";

            for (int j = 0; j < openableLoot.Length; j++)
            {
                OpenableLootEntry entry = openableLoot[j];
                if (string.IsNullOrWhiteSpace(entry.itemId))
                    continue;

                Register(entry.itemId.Trim(), sourceLabel);
            }
        }
    }

    private static void ScanEnemyLoot(IReadOnlyList<EnemyDefinition> enemies, WorldMapDefinition worldMap)
    {
        if (enemies == null)
            return;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyDefinition enemy = enemies[i];
            if (!enemy || string.IsNullOrWhiteSpace(enemy.enemyId))
                continue;

            string enemyLabel = string.IsNullOrWhiteSpace(enemy.displayName)
                ? enemy.enemyId.Trim()
                : enemy.displayName.Trim();

            List<string> mapLocations = worldMap != null
                ? DatabaseRegionEnemyCatalog.CollectMapLocationDisplayNames(enemy.enemyId.Trim(), worldMap)
                : new List<string>();

            string dropLabel = mapLocations.Count > 0
                ? $"{enemyLabel} ({string.Join(", ", mapLocations)})"
                : $"{enemyLabel} drops";

            RegisterLootTable(enemy.loot, dropLabel);
            RegisterLootTable(enemy.eliteLoot, dropLabel);
        }
    }

    private static void RegisterLootTable(List<EnemyLootEntry> lootTable, string sourceLabel)
    {
        if (lootTable == null)
            return;

        for (int i = 0; i < lootTable.Count; i++)
        {
            EnemyLootEntry entry = lootTable[i];
            ItemDefinition item = entry?.item;
            if (!item || string.IsNullOrWhiteSpace(item.itemId))
                continue;

            Register(item.itemId, sourceLabel);
        }
    }

    private static void ScanWorldMap(WorldMapDefinition worldMap)
    {
        if (worldMap?.regions == null)
            return;

        for (int r = 0; r < worldMap.regions.Count; r++)
        {
            RegionDefinition region = worldMap.regions[r];
            if (!DatabaseRegionEnemyCatalog.IsDatabaseListableRegion(region) || region?.nodes == null)
                continue;

            for (int n = 0; n < region.nodes.Count; n++)
                ScanMapNode(region.nodes[n]);
        }
    }

    private static void ScanMapNode(MapNodeDefinition node)
    {
        if (!node)
            return;

        string mapLabel = ResolveMapNodeLabel(node);

        ScanSpawnRows(node.spawnGroupPlans, mapLabel, node);
        ScanWaveList(node.enduranceWaves, mapLabel, node);
        ScanWaveList(node.simpleCombatWaves, mapLabel, node);
        ScanEnduranceCompletionLoot(node, mapLabel);
        ScanScalingDrops(node, mapLabel);
    }

    private static void ScanSpawnRows(List<LevelSpawnGroupPlan> plans, string mapLabel, MapNodeDefinition node)
    {
        if (plans == null)
            return;

        for (int p = 0; p < plans.Count; p++)
            ScanSpawnList(plans[p]?.spawns, mapLabel, node);
    }

    private static void ScanWaveList(List<EnduranceWavePlan> waves, string mapLabel, MapNodeDefinition node)
    {
        if (waves == null)
            return;

        for (int w = 0; w < waves.Count; w++)
        {
            EnduranceWavePlan wave = waves[w];
            wave?.EnsureReady();
            ScanSpawnList(wave?.spawns, mapLabel, node);
        }
    }

    private static void ScanSpawnList(List<SpawnPrefabCount> spawns, string mapLabel, MapNodeDefinition node)
    {
        if (spawns == null)
            return;

        for (int i = 0; i < spawns.Count; i++)
        {
            SpawnPrefabCount row = spawns[i];
            if (row == null)
                continue;

            if (row.itemDefinition != null && !string.IsNullOrWhiteSpace(row.itemDefinition.itemId))
                Register(row.itemDefinition.itemId, mapLabel);

            if (!row.TryResolveSpawnPrefab(out GameObject prefab, out _, node, logWarnings: false) || !prefab)
                continue;

            ScanPrefabForSources(prefab, mapLabel);
        }
    }

    private static void ScanPrefabForSources(GameObject prefab, string mapLabel)
    {
        if (!prefab)
            return;

        ResourceNode[] resourceNodes = prefab.GetComponentsInChildren<ResourceNode>(true);
        for (int i = 0; i < resourceNodes.Length; i++)
            RegisterNodeDefinitionYields(resourceNodes[i]?.Definition, mapLabel);

        Merchant[] merchants = prefab.GetComponentsInChildren<Merchant>(true);
        for (int i = 0; i < merchants.Length; i++)
            RegisterMerchantStock(merchants[i], mapLabel);
    }

    private static void RegisterNodeDefinitionYields(NodeDefinition nodeDef, string mapLabel)
    {
        if (!nodeDef)
            return;

        string gatherLabel = string.IsNullOrWhiteSpace(nodeDef.displayName)
            ? mapLabel
            : $"{mapLabel} ({nodeDef.displayName.Trim()})";

        if (nodeDef.mainYieldEntries != null)
        {
            for (int i = 0; i < nodeDef.mainYieldEntries.Length; i++)
            {
                ItemDefinition item = nodeDef.mainYieldEntries[i].item;
                if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
                    Register(item.itemId, gatherLabel);
            }
        }

        if (nodeDef.bonusDrops != null)
        {
            for (int i = 0; i < nodeDef.bonusDrops.Length; i++)
            {
                ItemDefinition item = nodeDef.bonusDrops[i].item;
                if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
                    Register(item.itemId, gatherLabel);
            }
        }

        if (nodeDef.hiddenDrops != null)
        {
            for (int i = 0; i < nodeDef.hiddenDrops.Length; i++)
            {
                ItemDefinition item = nodeDef.hiddenDrops[i].item;
                if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
                    Register(item.itemId, gatherLabel);
            }
        }
    }

    private static void RegisterMerchantStock(Merchant merchant, string mapLabel)
    {
        if (!merchant || merchant.Stock == null)
            return;

        string merchantLabel = string.IsNullOrWhiteSpace(merchant.MerchantName)
            ? "Merchant"
            : merchant.MerchantName.Trim();

        string sourceLabel = string.IsNullOrWhiteSpace(mapLabel)
            ? $"Shop: {merchantLabel}"
            : $"Shop: {merchantLabel} ({mapLabel})";

        IReadOnlyList<MerchantStock.Entry> entries = merchant.Stock.Items;
        for (int i = 0; i < entries.Count; i++)
        {
            MerchantStock.Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId))
                continue;

            Register(entry.itemId.Trim(), sourceLabel);
        }
    }

    private static void ScanEnduranceCompletionLoot(MapNodeDefinition node, string mapLabel)
    {
        if (node.enduranceCompletionLoot != null)
            RegisterTrialLootRows(node.enduranceCompletionLoot, mapLabel);

        if (node.enduranceCompletionLootByTier == null)
            return;

        for (int i = 0; i < node.enduranceCompletionLootByTier.Count; i++)
        {
            EnduranceTrialLootByTier tier = node.enduranceCompletionLootByTier[i];
            if (tier?.entries == null)
                continue;

            RegisterTrialLootRows(tier.entries, mapLabel);
        }
    }

    private static void RegisterTrialLootRows(List<EnduranceTrialLootEntry> entries, string mapLabel)
    {
        if (entries == null)
            return;

        string label = string.IsNullOrWhiteSpace(mapLabel)
            ? "Endurance trial reward"
            : $"{mapLabel} (endurance reward)";

        for (int i = 0; i < entries.Count; i++)
        {
            EnduranceTrialLootEntry entry = entries[i];
            ItemDefinition item = entry?.item;
            if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
                Register(item.itemId, label);
        }
    }

    private static void ScanScalingDrops(MapNodeDefinition node, string mapLabel)
    {
        if (!node.mapCombatScalingEnabled)
            return;

        var drops = new List<MapScalingSpecialLootEntry>();
        node.CollectCombatScalingSpecialDropsUpToSlider(MapCombatScaling.SliderMax, drops);

        string label = string.IsNullOrWhiteSpace(mapLabel)
            ? "Combat maps (scaling)"
            : $"{mapLabel} (scaling)";

        for (int i = 0; i < drops.Count; i++)
        {
            MapScalingSpecialLootEntry entry = drops[i];
            ItemDefinition item = entry?.item;
            if (item == null || string.IsNullOrWhiteSpace(item.itemId))
                continue;

            Register(item.itemId, label);
        }

        Register(MapCombatScalingSpecialDropDefaults.MapEnhancementTier1ItemId, label);
        Register(MapCombatScalingSpecialDropDefaults.MapEnhancementTier2ItemId, label);
        Register(MapCombatScalingSpecialDropDefaults.SlotReductionScrollItemId, label);
        Register("basic_weapon_physical_scroll", label);
        Register("intermediate_weapon_physical_scroll", label);
        Register("advanced_weapon_physical_scroll", label);
    }

    private static void ScanQuestRewards(IReadOnlyList<QuestDefinition> quests)
    {
        if (quests == null)
            return;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestDefinition quest = quests[i];
            if (!quest)
                continue;

            string sourceLabel = FormatQuestSourceLabel(quest);
            if (string.IsNullOrWhiteSpace(sourceLabel))
                sourceLabel = "Quest reward";

            if (quest.rewardItem != null && !string.IsNullOrWhiteSpace(quest.rewardItem.itemId))
                Register(quest.rewardItem.itemId, sourceLabel);
            else if (!string.IsNullOrWhiteSpace(quest.rewardItemId))
                Register(quest.rewardItemId.Trim(), sourceLabel);
        }
    }

    private static string FormatQuestSourceLabel(QuestDefinition quest)
    {
        if (!quest)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(quest.obtainLocationDisplayName))
            return quest.obtainLocationDisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(quest.obtainLocationId))
            return HumanizeId(quest.obtainLocationId.Trim());

        if (!string.IsNullOrWhiteSpace(quest.displayName))
            return quest.displayName.Trim();

        return string.Empty;
    }

    private static void Register(string itemId, string sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(sourceLabel))
            return;

        string key = NormalizeItemId(itemId);
        if (string.IsNullOrEmpty(key))
            return;

        if (!_sourcesByItemId.TryGetValue(key, out HashSet<string> sources))
        {
            sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _sourcesByItemId[key] = sources;
        }

        sources.Add(sourceLabel.Trim());
    }

    private static string ResolveMapNodeLabel(MapNodeDefinition node)
    {
        if (!node)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(node.displayName))
            return node.displayName.Trim();

        if (!string.IsNullOrWhiteSpace(node.nodeId))
            return HumanizeId(node.nodeId.Trim());

        return string.Empty;
    }

    private static string NormalizeItemId(string itemId) =>
        string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim().ToLowerInvariant().Replace(" ", "_");

    private static string HumanizeId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string[] parts = raw.Replace('_', ' ').Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                continue;

            parts[i] = char.ToUpperInvariant(parts[i][0]) +
                       (parts[i].Length > 1 ? parts[i].Substring(1) : string.Empty);
        }

        return string.Join(' ', parts);
    }
}
