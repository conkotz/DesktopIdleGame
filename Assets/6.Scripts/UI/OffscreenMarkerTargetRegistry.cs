using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Registered world targets for <see cref="OffscreenMarkersController"/> without tag/type scene scans during gameplay.
/// </summary>
public static class OffscreenMarkerTargetRegistry
{
    public enum Kind
    {
        Npc,
        Resource,
        Storage,
        NoticeBoard,
        Cave
    }

    private struct Entry
    {
        public Transform Transform;
        public int InstanceId;
        public Func<Transform, Vector3> ResolveWorldPosition;
    }

    private static readonly Dictionary<Kind, List<Entry>> Lists = new()
    {
        { Kind.Npc, new List<Entry>(16) },
        { Kind.Resource, new List<Entry>(32) },
        { Kind.Storage, new List<Entry>(8) },
        { Kind.NoticeBoard, new List<Entry>(8) },
        { Kind.Cave, new List<Entry>(8) },
    };

    private static bool _legacyBootstrapDone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        foreach (KeyValuePair<Kind, List<Entry>> pair in Lists)
            pair.Value.Clear();

        _legacyBootstrapDone = false;
    }

    static OffscreenMarkerTargetRegistry()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (KeyValuePair<Kind, List<Entry>> pair in Lists)
            pair.Value.Clear();

        _legacyBootstrapDone = false;
    }

    public static void Register(Kind kind, Transform transform, Func<Transform, Vector3> resolveWorldPosition = null)
    {
        if (!transform)
            return;

        List<Entry> list = Lists[kind];
        int id = transform.GetInstanceID();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].InstanceId == id)
                return;
        }

        list.Add(new Entry
        {
            Transform = transform,
            InstanceId = id,
            ResolveWorldPosition = resolveWorldPosition
        });
    }

    public static void Unregister(Transform transform)
    {
        if (!transform)
            return;

        int id = transform.GetInstanceID();
        foreach (KeyValuePair<Kind, List<Entry>> pair in Lists)
        {
            List<Entry> list = pair.Value;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].InstanceId == id)
                    list.RemoveAt(i);
            }
        }
    }

    public static void ForEachActive(Kind kind, Action<int, Vector3> visitor)
    {
        if (visitor == null)
            return;

        PruneInvalid(kind);
        List<Entry> list = Lists[kind];
        for (int i = 0; i < list.Count; i++)
        {
            Entry entry = list[i];
            Transform t = entry.Transform;
            if (!t || !t.gameObject.activeInHierarchy)
                continue;

            Vector3 worldPos = entry.ResolveWorldPosition != null
                ? entry.ResolveWorldPosition(t)
                : t.position;
            visitor(entry.InstanceId, worldPos);
        }
    }

    /// <summary>
    /// One-time scene bootstrap for legacy tagged objects without explicit registrants.
    /// Not called from gameplay refresh loops.
    /// </summary>
    public static void BootstrapLegacyTaggedObjectsOnce()
    {
        if (_legacyBootstrapDone)
            return;

        _legacyBootstrapDone = true;
        BootstrapTag("NPC", Kind.Npc);
        BootstrapTag("Resource", Kind.Resource);
        BootstrapTag("Storage", Kind.Storage);
        BootstrapTag("NoticeBoard", Kind.NoticeBoard);
        BootstrapTag("Cave", Kind.Cave);
    }

    private static void BootstrapTag(string unityTag, Kind kind)
    {
        GameObject[] tagged = GameObject.FindGameObjectsWithTag(unityTag);
        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (!go)
                continue;

            Transform t = go.transform;
            if (kind == Kind.Resource)
            {
                ResourceNode node = go.GetComponentInParent<ResourceNode>();
                if (node)
                {
                    Register(Kind.Resource, node.transform, ResolveResourceWorldPosition);
                    continue;
                }
            }

            Register(kind, t);
        }
    }

    private static Vector3 ResolveResourceWorldPosition(Transform resourceRoot)
    {
        if (!resourceRoot)
            return Vector3.zero;

        ResourceNode node = resourceRoot.GetComponent<ResourceNode>();
        if (node && node.workSpot)
            return node.workSpot.position;

        return resourceRoot.position;
    }

    private static void PruneInvalid(Kind kind)
    {
        List<Entry> list = Lists[kind];
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Transform t = list[i].Transform;
            if (!t)
                list.RemoveAt(i);
        }
    }
}
