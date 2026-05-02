using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Activity-log lines when the player selects a locked region (Level Select, Quest journal, etc.).
/// Builds text from <see cref="RegionDefinition"/> prerequisites instead of hardcoded region ids.
/// </summary>
public static class LockedRegionClickFeedback
{
    public static void LogLockedRegionNotice(RegionDefinition region)
    {
        if (!region)
            return;

        string msg = BuildRegionLockActivityLogMessage(region);
        if (string.IsNullOrWhiteSpace(msg))
        {
            string fallback = string.IsNullOrWhiteSpace(region.displayName)
                ? "This region is locked."
                : $"{region.displayName.Trim()} is locked.";
            GameLog.Add(fallback, GameLog.CannotMessageColor);
            return;
        }

        GameLog.Add(msg, GameLog.CannotMessageColor);
    }

    /// <summary>
    /// Player-facing explanation of what still blocks <see cref="RegionDefinition.IsRegionUnlocked"/>.
    /// Empty when the region should not show a detailed hint (caller may use a generic line).
    /// </summary>
    public static string BuildRegionLockActivityLogMessage(RegionDefinition region)
    {
        if (!region)
            return "";

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            UnityEngine.Object.FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        WorldMapDefinition map = progress && progress.WorldMap
            ? progress.WorldMap
            : Resources.Load<WorldMapDefinition>("Databases/WorldMap_Main");
        QuestProgressManager qpm = QuestProgressManager.Instance ??
            UnityEngine.Object.FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);

        string activeNodeId = ActiveLevelContext.Current != null ? ActiveLevelContext.Current.nodeId : "";
        string regionTitle = string.IsNullOrWhiteSpace(region.displayName)
            ? region.regionId
            : region.displayName.Trim();

        if (region.lockWhenAllNodesCompleted != null &&
            region.lockWhenAllNodesCompleted.Count > 0 &&
            progress != null)
        {
            bool lockListHasIds = false;
            bool lockListAllComplete = true;
            for (int li = 0; li < region.lockWhenAllNodesCompleted.Count; li++)
            {
                string lockNodeId = region.lockWhenAllNodesCompleted[li];
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
                if (string.IsNullOrEmpty(activeNodeId) || !region.OwnsMapNodeId(activeNodeId.Trim()))
                    return $"{regionTitle} is no longer available.";
            }
        }

        if (!region.useRegionLock)
            return "";

        bool nodesConfigured = HasAnyConfiguredIds(region.prerequisiteCompletedNodeIds);
        bool questsConfigured = HasAnyConfiguredIds(region.prerequisiteRewardClaimedQuestIds);
        if (!nodesConfigured && !questsConfigured)
            return $"{regionTitle} is locked — add map or quest prerequisites on the region asset.";

        var sentences = new List<string>();

        if (nodesConfigured && progress != null)
        {
            AppendUnmetMapPrerequisites(
                sentences,
                region.prerequisiteCompletedNodeIds,
                region.requireAllPrerequisites,
                progress,
                map);
        }

        if (questsConfigured && qpm != null)
        {
            AppendUnmetQuestRewardPrerequisites(
                sentences,
                region.prerequisiteRewardClaimedQuestIds,
                region.requireAllPrerequisiteQuestRewards,
                qpm);
        }

        if (!string.IsNullOrWhiteSpace(region.unlockAfterLeavingWorldRegionId) &&
            progress != null &&
            map != null &&
            !progress.HasEnteredOutsideWorldRegion(region.unlockAfterLeavingWorldRegionId.Trim(), map))
        {
            string leaveRegion = region.unlockAfterLeavingWorldRegionId.Trim();
            RegionDefinition leaveDef = map.FindRegionById(leaveRegion);
            string leaveName = leaveDef && !string.IsNullOrWhiteSpace(leaveDef.displayName)
                ? leaveDef.displayName.Trim()
                : FormatIdFallback(leaveRegion);
            sentences.Add($"Enter a map outside {leaveName} to unlock this region.");
        }

        if (sentences.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.Append(regionTitle);
        sb.Append(" is locked. ");
        for (int i = 0; i < sentences.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(sentences[i]);
        }

        return sb.ToString();
    }

    private static void AppendUnmetMapPrerequisites(
        List<string> sentences,
        List<string> nodeIds,
        bool requireAll,
        WorldMapProgressManager progress,
        WorldMapDefinition map)
    {
        if (nodeIds == null || progress == null)
            return;

        var unmetNames = new List<string>();
        var metAny = false;
        for (int i = 0; i < nodeIds.Count; i++)
        {
            string raw = nodeIds[i];
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            string id = raw.Trim();
            if (progress.IsNodeCompleted(id))
            {
                metAny = true;
                continue;
            }

            unmetNames.Add(ResolveMapNodeDisplayName(map, id));
        }

        if (unmetNames.Count == 0)
            return;

        if (requireAll)
        {
            sentences.Add(
                unmetNames.Count == 1
                    ? $"Complete map: {unmetNames[0]}."
                    : "Complete these maps: " + string.Join(", ", unmetNames) + ".");
            return;
        }

        if (!metAny && unmetNames.Count > 0)
        {
            sentences.Add(
                unmetNames.Count == 1
                    ? $"Complete map: {unmetNames[0]}."
                    : "Complete any of these maps: " + string.Join(", ", unmetNames) + ".");
        }
    }

    private static void AppendUnmetQuestRewardPrerequisites(
        List<string> sentences,
        List<string> questIds,
        bool requireAll,
        QuestProgressManager qpm)
    {
        if (questIds == null || qpm == null)
            return;

        var unmetNames = new List<string>();
        var metAny = false;
        for (int i = 0; i < questIds.Count; i++)
        {
            string raw = questIds[i];
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            string id = raw.Trim();
            if (qpm.IsRewardClaimed(id))
            {
                metAny = true;
                continue;
            }

            QuestDefinition q = qpm.GetQuestDefinition(id);
            string nm = q && !string.IsNullOrWhiteSpace(q.displayName) ? q.displayName.Trim() : FormatIdFallback(id);
            unmetNames.Add(nm);
        }

        if (unmetNames.Count == 0)
            return;

        if (requireAll)
        {
            sentences.Add(
                unmetNames.Count == 1
                    ? $"Complete and claim quest reward: {unmetNames[0]}."
                    : "Complete and claim quest rewards for: " + string.Join(", ", unmetNames) + ".");
            return;
        }

        if (!metAny && unmetNames.Count > 0)
        {
            sentences.Add(
                unmetNames.Count == 1
                    ? $"Complete and claim quest reward: {unmetNames[0]}."
                    : "Complete and claim any of these quests: " + string.Join(", ", unmetNames) + ".");
        }
    }

    private static string ResolveMapNodeDisplayName(WorldMapDefinition map, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return "";
        MapNodeDefinition node = map ? map.FindNodeById(nodeId.Trim()) : null;
        if (node && !string.IsNullOrWhiteSpace(node.displayName))
            return node.displayName.Trim();
        return FormatIdFallback(nodeId.Trim());
    }

    private static string FormatIdFallback(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "";
        return char.ToUpperInvariant(id[0]) + id.Substring(1).Replace('_', ' ');
    }

    private static bool HasAnyConfiguredIds(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
            return false;
        for (int i = 0; i < ids.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]))
                return true;
        }

        return false;
    }
}
