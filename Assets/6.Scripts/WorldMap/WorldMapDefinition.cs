using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/World Map/World Map Definition", fileName = "WorldMap_")]
public class WorldMapDefinition : ScriptableObject
{
    [Header("Regions")]
    [Tooltip("All regions available on this world map.")]
    public List<RegionDefinition> regions = new();

    [Header("Start")]
    [Tooltip("Region highlighted by default when opening the map.")]
    public string startingRegionId;

    [Tooltip("First node the player can access; seeded as unlocked in progress.")]
    public string startingNodeId;

    public RegionDefinition FindRegionById(string regionId)
    {
        if (string.IsNullOrEmpty(regionId) || regions == null) return null;
        for (int i = 0; i < regions.Count; i++)
        {
            RegionDefinition r = regions[i];
            if (r && r.regionId == regionId) return r;
        }

        return null;
    }

    public MapNodeDefinition FindNodeById(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || regions == null) return null;
        for (int i = 0; i < regions.Count; i++)
        {
            RegionDefinition r = regions[i];
            if (!r) continue;
            MapNodeDefinition n = r.FindNodeById(nodeId);
            if (n) return n;
        }

        return null;
    }

    public RegionDefinition FindRegionContainingNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || regions == null) return null;
        for (int i = 0; i < regions.Count; i++)
        {
            RegionDefinition r = regions[i];
            if (!r) continue;
            if (r.FindNodeById(nodeId)) return r;
        }

        return null;
    }
}
