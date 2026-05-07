using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-memory ordered list of tracked quests. Order reflects track click sequence.
/// </summary>
public static class QuestTrackerState
{
    public const int MaxTrackedQuestCount = 5;

    private static readonly List<string> TrackedQuestIds = new();

    public static event Action Changed;

    public static IReadOnlyList<string> OrderedTrackedQuestIds => TrackedQuestIds;
    public static int TrackedCount => TrackedQuestIds.Count;
    public static bool CanTrackMore => TrackedQuestIds.Count < MaxTrackedQuestCount;

    public static bool IsTracked(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return false;
        return TrackedQuestIds.Contains(questId.Trim());
    }

    public static bool TrackQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return false;

        string id = questId.Trim();
        if (TrackedQuestIds.Contains(id))
            return true;
        if (!CanTrackMore)
            return false;

        TrackedQuestIds.Add(id);
        Changed?.Invoke();
        return true;
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

    public static void PruneToMaxCount()
    {
        bool changed = false;
        while (TrackedQuestIds.Count > MaxTrackedQuestCount)
        {
            TrackedQuestIds.RemoveAt(TrackedQuestIds.Count - 1);
            changed = true;
        }

        if (changed)
            Changed?.Invoke();
    }

    public static void ReplaceTrackedQuestIds(IEnumerable<string> orderedQuestIds)
    {
        TrackedQuestIds.Clear();

        if (orderedQuestIds != null)
        {
            foreach (string raw in orderedQuestIds)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                string id = raw.Trim();
                if (TrackedQuestIds.Contains(id))
                    continue;

                TrackedQuestIds.Add(id);
                if (TrackedQuestIds.Count >= MaxTrackedQuestCount)
                    break;
            }
        }

        Changed?.Invoke();
    }
}
