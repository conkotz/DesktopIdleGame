using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-memory ordered list of tracked quests. Order reflects track click sequence.
/// </summary>
public static class QuestTrackerState
{
    private static readonly List<string> TrackedQuestIds = new();

    public static event Action Changed;

    public static IReadOnlyList<string> OrderedTrackedQuestIds => TrackedQuestIds;

    public static bool IsTracked(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return false;
        return TrackedQuestIds.Contains(questId.Trim());
    }

    public static void TrackQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        string id = questId.Trim();
        if (TrackedQuestIds.Contains(id))
            return;

        TrackedQuestIds.Add(id);
        Changed?.Invoke();
    }

    public static void UntrackQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        if (!TrackedQuestIds.Remove(questId.Trim()))
            return;

        Changed?.Invoke();
    }

    public static void PruneMissing(Func<string, bool> isValidQuestId)
    {
        if (isValidQuestId == null)
            return;

        bool changed = false;
        for (int i = TrackedQuestIds.Count - 1; i >= 0; i--)
        {
            if (isValidQuestId(TrackedQuestIds[i]))
                continue;
            TrackedQuestIds.RemoveAt(i);
            changed = true;
        }

        if (changed)
            Changed?.Invoke();
    }
}
