using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on an empty GameObject in the GamePlay scene to define a named set of spawn points.
/// Child transforms are treated as spawn locations (or you can provide an explicit list).
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay/Spawn Point Group")]
[DisallowMultipleComponent]
public class SpawnPointGroup : MonoBehaviour
{
    private const string AllSpawnsGroupId = "AllSpawns";
    private const string StartName = "SpawnPointStart";
    private const string BaseName = "SpawnPointBase";
    private const string LastName = "SpawnPointLast";
    private const string PositiveAnchorName = "SpawnPoint39";

    [Tooltip("Must match MapNodeDefinition.spawnGroupPlans[*].groupId.")]
    public string groupId = "Default";

    [Tooltip("Optional explicit list; if empty, child transforms are used.")]
    [SerializeField] private List<Transform> points = new();

    public IReadOnlyList<Transform> Points
    {
        get
        {
            if (TryCollectAllSpawnsLanePoints(out List<Transform> lanePoints))
                return lanePoints;

            if (points != null && points.Count > 0)
                return points;

            var list = new List<Transform>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
                list.Add(transform.GetChild(i));
            return list;
        }
    }

    private void Awake()
    {
        EnsureAllSpawnsLaneChildren();
    }

    private void OnValidate()
    {
        EnsureAllSpawnsLaneChildren();
    }

    private bool TryCollectAllSpawnsLanePoints(out List<Transform> lanePoints)
    {
        lanePoints = null;
        if (!string.Equals(groupId?.Trim(), AllSpawnsGroupId, StringComparison.OrdinalIgnoreCase))
            return false;

        EnsureAllSpawnsLaneChildren();

        lanePoints = new List<Transform>(transform.childCount);
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child)
                continue;

            string name = child.name;
            if (string.Equals(name, StartName, StringComparison.Ordinal) ||
                string.Equals(name, LastName, StringComparison.Ordinal))
                continue;

            lanePoints.Add(child);
        }

        lanePoints.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        return lanePoints.Count > 0;
    }

    private void EnsureAllSpawnsLaneChildren()
    {
        if (!string.Equals(groupId?.Trim(), AllSpawnsGroupId, StringComparison.OrdinalIgnoreCase))
            return;

        Transform start = FindDirectChildByName(StartName);
        Transform spawnBase = FindDirectChildByName(BaseName);
        Transform last = FindDirectChildByName(LastName);
        if (!start || !spawnBase || !last)
            return;

        Transform positiveAnchor = FindDirectChildByName(PositiveAnchorName);
        int positiveAnchorIndex = 39;
        if (!positiveAnchor)
        {
            positiveAnchor = spawnBase;
            positiveAnchorIndex = 1;
        }

        float y = spawnBase.localPosition.y;
        float z = spawnBase.localPosition.z;

        // Fill SpawnPoint-1, SpawnPoint-2, ... between Start and Base at 1-unit steps.
        int negCount = Mathf.Max(0, Mathf.RoundToInt(spawnBase.localPosition.x - start.localPosition.x) - 1);
        for (int i = 1; i <= negCount; i++)
        {
            string name = $"SpawnPoint-{i}";
            EnsureChild(name, new Vector3(spawnBase.localPosition.x - i, y, z));
        }

        // Fill SpawnPoint40, SpawnPoint41, ... between positive anchor and Last at 1-unit steps.
        int posCount = Mathf.Max(0, Mathf.RoundToInt(last.localPosition.x - positiveAnchor.localPosition.x) - 1);
        for (int i = 1; i <= posCount; i++)
        {
            int idx = positiveAnchorIndex + i;
            string name = $"SpawnPoint{idx}";
            EnsureChild(name, new Vector3(positiveAnchor.localPosition.x + i, y, z));
        }
    }

    private Transform EnsureChild(string name, Vector3 localPosition)
    {
        Transform existing = FindDirectChildByName(name);
        if (existing)
            return existing;

        var go = new GameObject(name);
        Transform t = go.transform;
        t.SetParent(transform, false);
        t.localPosition = localPosition;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        return t;
    }

    private Transform FindDirectChildByName(string name)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c && string.Equals(c.name, name, StringComparison.Ordinal))
                return c;
        }
        return null;
    }
}

