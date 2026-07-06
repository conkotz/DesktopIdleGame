using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime + save snapshot for player-moved world interactable X positions (keyed by mapNodeId:spawnPointName).
/// </summary>
public static class WorldObjectPositionStore
{
    public const string TownMapNodeId = "duskwood";

    private static readonly Dictionary<string, float> SavedWorldXByKey =
        new(StringComparer.OrdinalIgnoreCase);

    public static string BuildKey(string mapNodeId, string spawnPointName)
    {
        if (string.IsNullOrWhiteSpace(mapNodeId) || string.IsNullOrWhiteSpace(spawnPointName))
            return string.Empty;

        return $"{mapNodeId.Trim()}:{spawnPointName.Trim()}";
    }

    public static void EnsureLists(SaveData data)
    {
        if (data == null)
            return;

        data.worldObjectPositionKeys ??= new List<string>();
        data.worldObjectPositionX ??= new List<float>();
        RepairParallelLists(data);
    }

    public static void CopyFromSnapshot(SaveData target, SaveData source)
    {
        if (target == null)
            return;

        EnsureLists(target);
        target.worldObjectPositionKeys.Clear();
        target.worldObjectPositionX.Clear();

        if (source == null)
            return;

        EnsureLists(source);
        int n = source.worldObjectPositionKeys.Count;
        for (int i = 0; i < n; i++)
        {
            string key = source.worldObjectPositionKeys[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;

            target.worldObjectPositionKeys.Add(key.Trim());
            target.worldObjectPositionX.Add(source.worldObjectPositionX[i]);
        }

        RepairParallelLists(target);
    }

    public static void ApplyFromSaveData(SaveData data)
    {
        SavedWorldXByKey.Clear();
        if (data == null)
            return;

        EnsureLists(data);
        int n = data.worldObjectPositionKeys.Count;
        for (int i = 0; i < n; i++)
        {
            string key = data.worldObjectPositionKeys[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;

            float x = data.worldObjectPositionX[i];
            if (float.IsNaN(x) || float.IsInfinity(x))
                continue;

            SavedWorldXByKey[key.Trim()] = x;
        }
    }

    public static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

        EnsureLists(data);
        data.worldObjectPositionKeys.Clear();
        data.worldObjectPositionX.Clear();

        if (SavedWorldXByKey.Count == 0)
            return;

        var keys = new List<string>(SavedWorldXByKey.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            data.worldObjectPositionKeys.Add(key);
            data.worldObjectPositionX.Add(SavedWorldXByKey[key]);
        }
    }

    public static bool TryGetSavedWorldX(string positionKey, out float worldX)
    {
        worldX = 0f;
        if (string.IsNullOrWhiteSpace(positionKey))
            return false;

        return SavedWorldXByKey.TryGetValue(positionKey.Trim(), out worldX);
    }

    public static void RecordWorldX(string positionKey, float worldX)
    {
        if (string.IsNullOrWhiteSpace(positionKey))
            return;

        if (float.IsNaN(worldX) || float.IsInfinity(worldX))
            return;

        SavedWorldXByKey[positionKey.Trim()] = worldX;
    }

    public static void ClearSavedPositionsForMap(string mapNodeId)
    {
        if (string.IsNullOrWhiteSpace(mapNodeId))
        {
            SavedWorldXByKey.Clear();
            return;
        }

        string prefix = $"{mapNodeId.Trim()}:";
        var keysToRemove = new List<string>(SavedWorldXByKey.Count);
        foreach (string key in SavedWorldXByKey.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                keysToRemove.Add(key);
        }

        for (int i = 0; i < keysToRemove.Count; i++)
            SavedWorldXByKey.Remove(keysToRemove[i]);
    }

    public static void ResetTownObjectsToOriginalPositions()
    {
        WorldObjectMoveModeController.CancelIfActive();
        ClearSavedPositionsForMap(TownMapNodeId);
        WorldObjectMovable.ResetSpawnPositionsForMap(TownMapNodeId);
        SaveManager.Instance?.SaveImmediate();
        GameLog.Add("Town objects reset to original positions.");
    }

    public static void RepairParallelLists(SaveData data)
    {
        if (data == null)
            return;

        data.worldObjectPositionKeys ??= new List<string>();
        data.worldObjectPositionX ??= new List<float>();

        int n = data.worldObjectPositionKeys.Count;
        while (data.worldObjectPositionX.Count < n)
            data.worldObjectPositionX.Add(0f);
        while (data.worldObjectPositionX.Count > n)
            data.worldObjectPositionX.RemoveAt(data.worldObjectPositionX.Count - 1);
    }
}
