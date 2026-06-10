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
