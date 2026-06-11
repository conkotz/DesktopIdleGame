using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves which <see cref="EnemyDefinition"/> ids appear on maps in a world region.
/// </summary>
public static class DatabaseRegionEnemyCatalog
{
    public const string DefaultRegionId = "greenlands";
    private const string TutorialRegionId = "tutorial";

    public static bool IsDatabaseListableRegion(RegionDefinition region)
    {
        if (!region || string.IsNullOrWhiteSpace(region.regionId))
            return false;

        return !string.Equals(region.regionId.Trim(), TutorialRegionId, StringComparison.OrdinalIgnoreCase);
    }

    public static List<RegionDefinition> GetListableRegions(
        WorldMapDefinition map,
        WorldMapProgressManager progress,
        string activeGameplayNodeId = null)
    {
        var result = new List<RegionDefinition>();
        if (!map || map.regions == null)
            return result;

        for (int i = 0; i < map.regions.Count; i++)
        {
            RegionDefinition region = map.regions[i];
            if (!IsDatabaseListableRegion(region))
                continue;
            if (!region.ShouldListInRegionPicker(progress, activeGameplayNodeId, map))
                continue;
            if (!IsRegionAvailable(region, progress, activeGameplayNodeId, map))
                continue;
            result.Add(region);
        }

        return result;
    }

    public static RegionDefinition ResolveDefaultRegion(IReadOnlyList<RegionDefinition> regions, WorldMapDefinition map)
    {
        if (regions == null || regions.Count == 0)
            return null;

        if (map != null)
        {
            RegionDefinition greenlands = map.FindRegionById(DefaultRegionId);
            if (greenlands != null && ContainsRegion(regions, greenlands))
                return greenlands;
        }

        return regions[0];
    }

    public static HashSet<string> CollectEnemyIds(RegionDefinition region)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (region?.nodes == null)
            return ids;

        for (int i = 0; i < region.nodes.Count; i++)
            CollectEnemyIdsFromNode(region.nodes[i], ids);

        return ids;
    }

    public static string FormatMapLocationsForEnemy(
        string enemyId,
        WorldMapDefinition map,
        RegionDefinition regionFilter = null)
    {
        List<string> locations = CollectMapLocationDisplayNames(enemyId, map, regionFilter);
        if (locations.Count == 0)
            return string.Empty;

        return string.Join(", ", locations);
    }

    public static List<string> CollectMapLocationDisplayNames(
        string enemyId,
        WorldMapDefinition map,
        RegionDefinition regionFilter = null)
    {
        var locations = new List<string>();
        if (string.IsNullOrWhiteSpace(enemyId))
            return locations;

        string normalizedEnemyId = enemyId.Trim();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (regionFilter != null)
        {
            CollectMapLocationDisplayNamesFromRegion(regionFilter, normalizedEnemyId, locations, seen);
            locations.Sort(StringComparer.OrdinalIgnoreCase);
            return locations;
        }

        if (!map || map.regions == null)
            return locations;

        for (int i = 0; i < map.regions.Count; i++)
        {
            RegionDefinition region = map.regions[i];
            if (!IsDatabaseListableRegion(region))
                continue;

            CollectMapLocationDisplayNamesFromRegion(region, normalizedEnemyId, locations, seen);
        }

        locations.Sort(StringComparer.OrdinalIgnoreCase);
        return locations;
    }

    private static void CollectMapLocationDisplayNamesFromRegion(
        RegionDefinition region,
        string enemyId,
        List<string> locations,
        HashSet<string> seen)
    {
        if (!region || region.nodes == null || locations == null || seen == null)
            return;

        for (int i = 0; i < region.nodes.Count; i++)
        {
            MapNodeDefinition node = region.nodes[i];
            if (!NodeContainsEnemy(node, enemyId))
                continue;

            string displayName = ResolveMapNodeDisplayName(node);
            if (string.IsNullOrWhiteSpace(displayName) || !seen.Add(displayName))
                continue;

            locations.Add(displayName);
        }
    }

    private static bool NodeContainsEnemy(MapNodeDefinition node, string enemyId)
    {
        if (!node || string.IsNullOrWhiteSpace(enemyId))
            return false;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectEnemyIdsFromNode(node, ids);
        return ids.Contains(enemyId);
    }

    private static string ResolveMapNodeDisplayName(MapNodeDefinition node)
    {
        if (!node)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(node.displayName))
            return node.displayName.Trim();

        if (!string.IsNullOrWhiteSpace(node.nodeId))
            return node.nodeId.Trim();

        return string.Empty;
    }

    private static void CollectEnemyIdsFromNode(MapNodeDefinition node, HashSet<string> ids)
    {
        if (!node)
            return;

        if (node.spawnGroupPlans != null)
        {
            for (int p = 0; p < node.spawnGroupPlans.Count; p++)
                CollectEnemyIdsFromSpawnRows(node.spawnGroupPlans[p]?.spawns, ids);
        }

        CollectEnemyIdsFromWaveList(node.enduranceWaves, ids);
        CollectEnemyIdsFromWaveList(node.simpleCombatWaves, ids);
    }

    private static void CollectEnemyIdsFromWaveList(List<EnduranceWavePlan> waves, HashSet<string> ids)
    {
        if (waves == null)
            return;

        for (int w = 0; w < waves.Count; w++)
        {
            EnduranceWavePlan wave = waves[w];
            wave?.EnsureReady();
            CollectEnemyIdsFromSpawnRows(wave?.spawns, ids);
        }
    }

    private static void CollectEnemyIdsFromSpawnRows(List<SpawnPrefabCount> spawns, HashSet<string> ids)
    {
        if (spawns == null || ids == null)
            return;

        for (int i = 0; i < spawns.Count; i++)
        {
            SpawnPrefabCount row = spawns[i];
            EnemyDefinition enemy = row != null ? row.enemyDefinition : null;
            if (!enemy || string.IsNullOrWhiteSpace(enemy.enemyId))
                continue;

            ids.Add(enemy.enemyId.Trim());
        }
    }

    private static bool ContainsRegion(IReadOnlyList<RegionDefinition> regions, RegionDefinition target)
    {
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i] == target)
                return true;
        }

        return false;
    }

    private static bool IsRegionAvailable(
        RegionDefinition region,
        WorldMapProgressManager progress,
        string activeGameplayNodeId,
        WorldMapDefinition map)
    {
        if (!region)
            return false;

        return region.IsRegionUnlocked(progress, activeGameplayNodeId, map);
    }
}
