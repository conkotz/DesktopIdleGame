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

    [Header("Unlock (optional)")]
    [Tooltip("If false, this region is always available. If true, completion prerequisites below are evaluated.")]
    public bool useRegionLock;

    [Tooltip("Node ids that must be completed before this region unlocks. These can be nodes from any region (e.g. dungeon/boss in another region).")]
    public List<string> prerequisiteCompletedNodeIds = new();

    [Tooltip("If true, ALL prerequisite node ids must be completed. If false, ANY one completed node unlocks the region.")]
    public bool requireAllPrerequisites = true;

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

    /// <summary>
    /// Region availability gate (separate from per-node skill/map unlock checks).
    /// </summary>
    public bool IsRegionUnlocked(WorldMapProgressManager progress)
    {
        if (!useRegionLock)
            return true;

        if (prerequisiteCompletedNodeIds == null || prerequisiteCompletedNodeIds.Count == 0)
            return false;

        if (progress == null)
            return false;

        bool anyConfigured = false;
        bool anyMet = false;

        for (int i = 0; i < prerequisiteCompletedNodeIds.Count; i++)
        {
            string id = prerequisiteCompletedNodeIds[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            anyConfigured = true;
            bool met = progress.IsNodeCompleted(id.Trim());

            if (requireAllPrerequisites && !met)
                return false;
            if (!requireAllPrerequisites && met)
                anyMet = true;
        }

        if (!anyConfigured)
            return false;

        return requireAllPrerequisites || anyMet;
    }
}
