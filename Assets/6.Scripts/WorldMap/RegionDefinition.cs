using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/World Map/Region Definition", fileName = "Region_")]
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

    [Tooltip(
        "After map-node prerequisites pass, keep this region locked until the player has entered a map outside this world region id " +
        "(e.g. greenlands uses 'tutorial' so Combat 2 can finish while still on Tutorial 2, then Greenlands opens after leaving for another region).")]
    public string unlockAfterLeavingWorldRegionId = "";

    [Header("Lock after progression (optional)")]
    [Tooltip("When ALL listed node ids are completed, this region is treated as locked (e.g. Tutorial retired after both maps are cleared).")]
    public List<string> lockWhenAllNodesCompleted = new();

    [Tooltip("When true, Level Select / Quests omit this region’s row entirely whenever it would be locked (hide retired tutorial instead of “Tutorial (Locked)”).")]
    public bool hideFromRegionListsWhenLocked;

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
    /// <param name="activeGameplayNodeId">
    /// Optional <see cref="MapNodeDefinition.nodeId"/> for the current GamePlay session (bootstrap / pending level).
    /// When set and this region contains that node, retire locks from <see cref="lockWhenAllNodesCompleted"/> are suppressed
    /// so Quests stay usable on that map (e.g. Tutorial 2).
    /// </param>
    /// <param name="map">World map asset (needed for <see cref="unlockAfterLeavingWorldRegionId"/>).</param>
    public bool IsRegionUnlocked(WorldMapProgressManager progress, string activeGameplayNodeId = null, WorldMapDefinition map = null)
    {
        if (lockWhenAllNodesCompleted != null && lockWhenAllNodesCompleted.Count > 0 && progress != null)
        {
            bool lockListHasIds = false;
            bool lockListAllComplete = true;
            for (int li = 0; li < lockWhenAllNodesCompleted.Count; li++)
            {
                string lockNodeId = lockWhenAllNodesCompleted[li];
                if (string.IsNullOrWhiteSpace(lockNodeId))
                    continue;
                lockListHasIds = true;
                if (!progress.IsNodeCompleted(lockNodeId.Trim()))
                {
                    lockListAllComplete = false;
                    break;
                }
            }

            if (lockListHasIds && lockListAllComplete)
            {
                if (!string.IsNullOrEmpty(activeGameplayNodeId) &&
                    OwnsMapNodeId(activeGameplayNodeId.Trim()))
                    return true;
                return false;
            }
        }

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

        bool prereqsDone = requireAllPrerequisites || anyMet;
        if (!prereqsDone)
            return false;

        if (!string.IsNullOrWhiteSpace(unlockAfterLeavingWorldRegionId) && progress != null && map != null)
        {
            if (!progress.HasEnteredOutsideWorldRegion(unlockAfterLeavingWorldRegionId.Trim(), map))
                return false;
        }

        return true;
    }

    /// <summary>
    /// False when <see cref="hideFromRegionListsWhenLocked"/> is set and <see cref="IsRegionUnlocked"/> is false (row should not appear).
    /// </summary>
    public bool ShouldListInRegionPicker(WorldMapProgressManager progress, string activeGameplayNodeId, WorldMapDefinition map)
    {
        if (!hideFromRegionListsWhenLocked)
            return true;
        return IsRegionUnlocked(progress, activeGameplayNodeId, map);
    }

    private bool OwnsMapNodeId(string nodeId)
    {
        if (nodes == null || string.IsNullOrEmpty(nodeId))
            return false;
        for (int i = 0; i < nodes.Count; i++)
        {
            MapNodeDefinition n = nodes[i];
            if (n && string.Equals(n.nodeId, nodeId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
