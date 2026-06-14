using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Transforms that must track <see cref="WorldFloorToUIEdge"/> vertical corrections but are not parented under its world root.
/// </summary>
public static class WorldFloorFollowerRegistry
{
    [Flags]
    public enum Category
    {
        Actor = 1 << 0,
        ItemDrop = 1 << 1,
        Resource = 1 << 2,
        Cave = 1 << 3,
    }

    private struct Entry
    {
        public Transform Transform;
        public Category Category;
    }

    private static readonly List<Entry> Followers = new(64);
    private static bool s_legacyCaveBootstrapDone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Followers.Clear();
        s_legacyCaveBootstrapDone = false;
    }

    static WorldFloorFollowerRegistry()
    {
        SceneManager.sceneUnloaded += _ => Followers.RemoveAll(e => !e.Transform);
    }

    public static void Register(Transform t, Category category = Category.Actor)
    {
        if (!t)
            return;

        for (int i = 0; i < Followers.Count; i++)
        {
            if (Followers[i].Transform == t)
            {
                Entry e = Followers[i];
                e.Category = category;
                Followers[i] = e;
                return;
            }
        }

        Followers.Add(new Entry { Transform = t, Category = category });
    }

    public static void Unregister(Transform t)
    {
        if (!t)
            return;

        for (int i = Followers.Count - 1; i >= 0; i--)
        {
            if (Followers[i].Transform == t)
                Followers.RemoveAt(i);
        }
    }

    public static void BootstrapLegacyCaveFollowersOnce()
    {
        if (s_legacyCaveBootstrapDone)
            return;

        s_legacyCaveBootstrapDone = true;

        try
        {
            GameObject[] tagged = GameObject.FindGameObjectsWithTag("Cave");
            for (int i = 0; i < tagged.Length; i++)
            {
                GameObject go = tagged[i];
                if (go)
                    Register(go.transform, Category.Cave);
            }
        }
        catch (UnityException)
        {
            // Cave tag may not exist in this project.
        }
    }

    public static void MoveAllOutsideRoot(
        Transform worldRoot,
        float deltaY,
        HashSet<Transform> movedScratch,
        Action<Transform, float> moveY,
        Category mask)
    {
        movedScratch.Clear();

        for (int i = Followers.Count - 1; i >= 0; i--)
        {
            Entry entry = Followers[i];
            Transform t = entry.Transform;
            if (!t)
            {
                Followers.RemoveAt(i);
                continue;
            }

            if ((entry.Category & mask) == 0)
                continue;

            if (t == worldRoot || t.IsChildOf(worldRoot))
                continue;

            if (!movedScratch.Add(t))
                continue;

            moveY(t, deltaY);
        }
    }
}
