using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-save-slot UI window lock state (<see cref="SaveData.uiWindowLockKeys"/> / <see cref="SaveData.uiWindowLockLocked"/>).
/// Keys match <see cref="UIWindowCloseButton.PersistenceWindowId"/> (usually the target window GameObject name).
/// </summary>
public static class UIWindowLockStore
{
    private static readonly Dictionary<string, bool> s_lockedByWindowId = new();

    public static bool TryGetLocked(string windowId, out bool locked)
    {
        locked = false;
        if (string.IsNullOrWhiteSpace(windowId))
            return false;

        return s_lockedByWindowId.TryGetValue(windowId.Trim(), out locked);
    }

    public static void Record(string windowId, bool locked)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            return;

        string key = windowId.Trim();
        if (s_lockedByWindowId.TryGetValue(key, out bool existing) && existing == locked)
            return;

        s_lockedByWindowId[key] = locked;
        SaveManager.Instance?.Save();
    }

    public static void ClearAll()
    {
        s_lockedByWindowId.Clear();
    }

    /// <summary>Refreshes the in-memory map from live <see cref="UIWindowCloseButton"/> instances before writing a save.</summary>
    public static void CollectFromScene()
    {
        UIWindowCloseButton[] closers = Object.FindObjectsByType<UIWindowCloseButton>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < closers.Length; i++)
        {
            UIWindowCloseButton closer = closers[i];
            if (closer == null)
                continue;

            string id = closer.PersistenceWindowId;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            s_lockedByWindowId[id] = closer.IsLocked;
        }
    }

    internal static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

        CollectFromScene();

        if (data.uiWindowLockKeys == null)
            data.uiWindowLockKeys = new List<string>();
        if (data.uiWindowLockLocked == null)
            data.uiWindowLockLocked = new List<int>();

        data.uiWindowLockKeys.Clear();
        data.uiWindowLockLocked.Clear();

        foreach (KeyValuePair<string, bool> kv in s_lockedByWindowId)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            data.uiWindowLockKeys.Add(kv.Key);
            data.uiWindowLockLocked.Add(kv.Value ? 1 : 0);
        }
    }

    internal static void ApplyFromSaveData(SaveData data)
    {
        s_lockedByWindowId.Clear();

        if (data?.uiWindowLockKeys == null || data.uiWindowLockLocked == null)
        {
            ApplyToAllCloseButtonsInScene();
            return;
        }

        int count = Mathf.Min(data.uiWindowLockKeys.Count, data.uiWindowLockLocked.Count);
        for (int i = 0; i < count; i++)
        {
            string key = data.uiWindowLockKeys[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;

            s_lockedByWindowId[key.Trim()] = data.uiWindowLockLocked[i] != 0;
        }

        ApplyToAllCloseButtonsInScene();
    }

    public static void ApplyToCloseButton(UIWindowCloseButton closer)
    {
        if (closer == null)
            return;

        string id = closer.PersistenceWindowId;
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (TryGetLocked(id, out bool locked))
            closer.SetLockedFromPersistence(locked);
    }

    private static void ApplyToAllCloseButtonsInScene()
    {
        UIWindowCloseButton[] closers = Object.FindObjectsByType<UIWindowCloseButton>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < closers.Length; i++)
            ApplyToCloseButton(closers[i]);
    }
}
