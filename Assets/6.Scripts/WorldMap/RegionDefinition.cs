using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DesktopIdleGame/World Map/Region Definition", fileName = "Region_")]
public class RegionDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable id (e.g. greenlands).")]
    public string regionId;

    [Tooltip("Shown in the region list and headers.")]
    public string displayName = "New Region";

    [TextArea(2, 6)]
    public string description;

    [Header("Content")]
    [Tooltip("Nodes belonging to this region (order is list order).")]
    public List<MapNodeDefinition> nodes = new();

    [Header("Visuals")]
    [Tooltip("Optional backdrop when this region is selected (future polish).")]
    public Sprite backgroundSprite;

    public MapNodeDefinition FindNodeById(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || nodes == null) return null;
        for (int i = 0; i < nodes.Count; i++)
        {
            MapNodeDefinition n = nodes[i];
            if (n && n.nodeId == nodeId) return n;
        }

        return null;
    }
}
