using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-map exit positions (parallel lists on <see cref="SaveData"/>). Recorded when leaving GamePlay;
/// restored when entering via map teleport or login resume.
/// </summary>
public static class PlayerMapExitPositionStore
{
    public static void EnsureLists(SaveData data)
    {
        if (data == null)
            return;

        data.mapExitPositionNodeIds ??= new List<string>();
        data.mapExitPositionX ??= new List<float>();
        data.mapExitPositionY ??= new List<float>();
        data.mapExitPositionZ ??= new List<float>();
        RepairParallelLists(data);
    }

    public static void CopyFromSnapshot(SaveData target, SaveData source)
    {
        if (target == null)
            return;

        EnsureLists(target);
        target.mapExitPositionNodeIds.Clear();
        target.mapExitPositionX.Clear();
        target.mapExitPositionY.Clear();
        target.mapExitPositionZ.Clear();

        if (source == null)
            return;

        EnsureLists(source);
        int n = source.mapExitPositionNodeIds.Count;
        for (int i = 0; i < n; i++)
        {
            string id = source.mapExitPositionNodeIds[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;
            target.mapExitPositionNodeIds.Add(id.Trim());
            target.mapExitPositionX.Add(source.mapExitPositionX[i]);
            target.mapExitPositionY.Add(source.mapExitPositionY[i]);
            target.mapExitPositionZ.Add(source.mapExitPositionZ[i]);
        }

        RepairParallelLists(target);
    }

    public static void RecordExitPosition(SaveData data, string nodeId, Vector3 worldPosition)
    {
        if (data == null || string.IsNullOrWhiteSpace(nodeId))
            return;

        if (!IsFinitePosition(worldPosition))
            return;

        EnsureLists(data);
        string key = nodeId.Trim();

        int idx = FindNodeIndex(data.mapExitPositionNodeIds, key);
        if (idx >= 0)
        {
            data.mapExitPositionX[idx] = worldPosition.x;
            data.mapExitPositionY[idx] = worldPosition.y;
            data.mapExitPositionZ[idx] = worldPosition.z;
            return;
        }

        data.mapExitPositionNodeIds.Add(key);
        data.mapExitPositionX.Add(worldPosition.x);
        data.mapExitPositionY.Add(worldPosition.y);
        data.mapExitPositionZ.Add(worldPosition.z);
    }

    public static bool TryGetExitPosition(SaveData data, string nodeId, out Vector3 worldPosition)
    {
        worldPosition = default;
        if (data == null || string.IsNullOrWhiteSpace(nodeId))
            return false;

        EnsureLists(data);
        int idx = FindNodeIndex(data.mapExitPositionNodeIds, nodeId.Trim());
        if (idx < 0)
            return false;

        worldPosition = new Vector3(
            data.mapExitPositionX[idx],
            data.mapExitPositionY[idx],
            data.mapExitPositionZ[idx]);
        return IsFinitePosition(worldPosition);
    }

    public static void RepairParallelLists(SaveData data)
    {
        if (data == null)
            return;

        data.mapExitPositionNodeIds ??= new List<string>();
        data.mapExitPositionX ??= new List<float>();
        data.mapExitPositionY ??= new List<float>();
        data.mapExitPositionZ ??= new List<float>();

        int n = data.mapExitPositionNodeIds.Count;
        while (data.mapExitPositionX.Count < n)
            data.mapExitPositionX.Add(0f);
        while (data.mapExitPositionY.Count < n)
            data.mapExitPositionY.Add(0f);
        while (data.mapExitPositionZ.Count < n)
            data.mapExitPositionZ.Add(0f);

        while (data.mapExitPositionX.Count > n)
            data.mapExitPositionX.RemoveAt(data.mapExitPositionX.Count - 1);
        while (data.mapExitPositionY.Count > n)
            data.mapExitPositionY.RemoveAt(data.mapExitPositionY.Count - 1);
        while (data.mapExitPositionZ.Count > n)
            data.mapExitPositionZ.RemoveAt(data.mapExitPositionZ.Count - 1);
    }

    private static int FindNodeIndex(List<string> nodeIds, string nodeId)
    {
        if (nodeIds == null || string.IsNullOrWhiteSpace(nodeId))
            return -1;

        for (int i = 0; i < nodeIds.Count; i++)
        {
            if (string.Equals(nodeIds[i]?.Trim(), nodeId, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static bool IsFinitePosition(Vector3 p) =>
        !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
        !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
}
