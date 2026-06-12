using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-session window layout (drag position + corner scale). Survives GamePlay scene reloads; cleared on bootstrap / game load pivot reset.
/// </summary>
public static class UIWindowSessionLayoutMemory
{
    private static readonly Dictionary<string, UIWindowLayoutPrefs.Snapshot> SessionLayouts = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SessionLayouts.Clear();
    }

    public static void Capture(RectTransform rect, string memoryKey)
    {
        if (!rect || string.IsNullOrWhiteSpace(memoryKey))
            return;

        string key = memoryKey.Trim();
        SessionLayouts[key] = UIWindowLayoutPrefs.Capture(rect);
        UIWindowPositionMemory.Save(key, rect.anchoredPosition);
    }

    public static bool TryGet(string memoryKey, out UIWindowLayoutPrefs.Snapshot snapshot)
    {
        snapshot = default;
        if (string.IsNullOrWhiteSpace(memoryKey))
            return false;

        return SessionLayouts.TryGetValue(memoryKey.Trim(), out snapshot);
    }

    public static void ForgetAll()
    {
        SessionLayouts.Clear();
    }

    public static void ForgetKey(string memoryKey)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return;

        SessionLayouts.Remove(memoryKey.Trim());
    }
}
