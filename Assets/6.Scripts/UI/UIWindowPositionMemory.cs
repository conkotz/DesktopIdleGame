using System.Collections.Generic;
using UnityEngine;

public static class UIWindowPositionMemory
{
    private static readonly Dictionary<string, Vector2> SavedAnchoredPositions = new();

    public static void Save(string key, Vector2 anchoredPosition)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        SavedAnchoredPositions[key.Trim()] = anchoredPosition;
    }

    public static bool TryGet(string key, out Vector2 anchoredPosition)
    {
        anchoredPosition = default;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return SavedAnchoredPositions.TryGetValue(key.Trim(), out anchoredPosition);
    }

    public static void ForgetAll()
    {
        SavedAnchoredPositions.Clear();
    }

    public static void ForgetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        SavedAnchoredPositions.Remove(key.Trim());
    }

    public static void ResetAllWindowsToAnchors()
    {
        ForgetAll();
        UIWindowCornerResize.ResetAllScalesToDefault();
        HelperPopupLayoutPrefs.Clear();

        UIDragWindow[] windows = Object.FindObjectsByType<UIDragWindow>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] != null)
                windows[i].ResetToAnchorPoint();
        }
    }
}
