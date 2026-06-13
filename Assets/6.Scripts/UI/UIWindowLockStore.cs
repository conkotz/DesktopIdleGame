using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-save-slot UI window lock state (<see cref="SaveData.uiWindowLockKeys"/> / <see cref="SaveData.uiWindowLockLocked"/>).
/// Keys match <see cref="UIWindowCloseButton.PersistenceWindowId"/> (usually the target window GameObject name).
/// Locked windows reopen after scene changes (session layout) and game loads (pivot layout).
/// </summary>
public static class UIWindowLockStore
{
    private const string ActivityWindowId = "GameActivityWindow";

    private static readonly Dictionary<string, bool> s_lockedByWindowId = new();

    public static bool IsLocked(string windowId)
    {
        return TryGetLocked(windowId, out bool locked) && locked;
    }

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

    internal static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

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

    /// <summary>Reopens locked windows and refreshes lock icons after layout restore.</summary>
    public static void RestoreAfterSceneLayout()
    {
        RestoreOpenLockedWindows();
        ApplyToAllCloseButtonsInScene();
    }

    /// <summary>
    /// Opens every locked window. Call after layout restore on scene change or game load.
    /// </summary>
    public static void RestoreOpenLockedWindows()
    {
        if (MovePivotsModeController.IsPivotModeActive)
            return;

        foreach (KeyValuePair<string, bool> kv in s_lockedByWindowId)
        {
            if (!kv.Value)
                continue;

            TryOpenWindow(kv.Key);
        }
    }

    public static void ApplyToCloseButton(UIWindowCloseButton closer)
    {
        if (closer == null)
            return;

        string id = closer.PersistenceWindowId;
        if (string.IsNullOrWhiteSpace(id))
            return;

        closer.SetLockedFromPersistence(TryGetLocked(id, out bool locked) && locked);
    }

    public static void ApplyToAllCloseButtonsInScene()
    {
        UIWindowCloseButton[] closers = Object.FindObjectsByType<UIWindowCloseButton>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < closers.Length; i++)
            ApplyToCloseButton(closers[i]);
    }

    private static void TryOpenWindow(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            return;

        GameObject window = FindWindowRoot(windowId.Trim());
        if (!window)
            return;

        if (!window.activeSelf)
            window.SetActive(true);

        window.transform.SetAsLastSibling();

        if (windowId == ActivityWindowId)
        {
            GameLogWindowUI logUi = window.GetComponent<GameLogWindowUI>() ??
                                    window.GetComponentInChildren<GameLogWindowUI>(true);
            logUi?.FlushNow();
        }
    }

    private static GameObject FindWindowRoot(string windowId)
    {
        Transform windowsArea = ResolveWindowsArea();
        if (windowsArea)
        {
            Transform found = FindChildRecursive(windowsArea, windowId);
            if (found)
                return found.gameObject;
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;

            if (t.name == windowId)
                return t.gameObject;
        }

        return null;
    }

    private static Transform ResolveWindowsArea()
    {
        Transform windowsArea = GameObject.Find("WindowsArea")?.transform;
        if (windowsArea)
            return windowsArea;

        GameObject canvas = GameObject.Find("FullWindowCanvas");
        return canvas != null ? canvas.transform.Find("WindowsArea") : null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (!parent || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!child)
                continue;

            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested)
                return nested;
        }

        return null;
    }
}
