using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime + save snapshot for town services unlocked via outpost quest rewards.
/// </summary>
public static class TownServiceUnlockStore
{
    private static readonly HashSet<string> UnlockedIds = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsUnlocked(string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return false;

        return UnlockedIds.Contains(serviceId.Trim());
    }

    /// <returns>True when the service is now unlocked (including if it was already unlocked).</returns>
    public static bool Unlock(string serviceId, out bool wasNew)
    {
        wasNew = false;
        if (string.IsNullOrWhiteSpace(serviceId))
            return false;

        string id = serviceId.Trim();
        if (UnlockedIds.Contains(id))
            return true;

        UnlockedIds.Add(id);
        wasNew = true;
        return true;
    }

    public static void EnsureLists(SaveData data)
    {
        if (data == null)
            return;

        data.unlockedTownServiceIds ??= new List<string>();
    }

    public static void CopyFromSnapshot(SaveData target, SaveData source)
    {
        if (target == null)
            return;

        EnsureLists(target);
        target.unlockedTownServiceIds.Clear();

        if (source?.unlockedTownServiceIds == null)
            return;

        for (int i = 0; i < source.unlockedTownServiceIds.Count; i++)
        {
            string id = source.unlockedTownServiceIds[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            target.unlockedTownServiceIds.Add(id.Trim());
        }
    }

    public static void ApplyFromSaveData(SaveData data)
    {
        UnlockedIds.Clear();
        if (data == null)
            return;

        EnsureLists(data);
        for (int i = 0; i < data.unlockedTownServiceIds.Count; i++)
        {
            string id = data.unlockedTownServiceIds[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            UnlockedIds.Add(id.Trim());
        }
    }

    public static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

        EnsureLists(data);
        data.unlockedTownServiceIds.Clear();

        if (UnlockedIds.Count == 0)
            return;

        var ids = new List<string>(UnlockedIds);
        ids.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < ids.Count; i++)
            data.unlockedTownServiceIds.Add(ids[i]);
    }
}
