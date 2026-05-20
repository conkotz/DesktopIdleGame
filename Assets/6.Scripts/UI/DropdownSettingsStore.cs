using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persists dropdown settings (int index) in <see cref="PlayerPrefs"/> (global keys, not tied to save slots).
/// </summary>
public static class DropdownSettingsStore
{
    private const string PrefsPrefix = "Settings.Dropdown.";

    private static readonly string[] CapFramerateLabels = { "60", "100", "144", "180", "Uncapped" };

    public static event Action<DropdownSettingId, int> Changed;

    public static bool HasOptions(DropdownSettingId setting) => GetOptionLabels(setting).Count > 0;

    public static IReadOnlyList<string> GetOptionLabels(DropdownSettingId setting)
    {
        return setting switch
        {
            DropdownSettingId.CapFramerate => CapFramerateLabels,
            _ => Array.Empty<string>()
        };
    }

    public static int GetDefault(DropdownSettingId setting)
    {
        return setting switch
        {
            DropdownSettingId.CapFramerate => CapFramerateLabels.Length - 1,
            _ => 0
        };
    }

    public static int Get(DropdownSettingId setting)
    {
        if (!HasOptions(setting))
            return 0;

        int defaultIndex = GetDefault(setting);
        int max = GetOptionLabels(setting).Count - 1;
        defaultIndex = Mathf.Clamp(defaultIndex, 0, max);
        return Mathf.Clamp(PlayerPrefs.GetInt(GetPrefsKey(setting), defaultIndex), 0, max);
    }

    public static void Set(DropdownSettingId setting, int index)
    {
        if (!HasOptions(setting))
            return;

        int clamped = Mathf.Clamp(index, 0, GetOptionLabels(setting).Count - 1);
        if (Get(setting) == clamped)
            return;

        PlayerPrefs.SetInt(GetPrefsKey(setting), clamped);
        PlayerPrefs.Save();
        Changed?.Invoke(setting, clamped);
    }

    internal static void ClearAllStoredKeysAndReload()
    {
        foreach (DropdownSettingId id in Enum.GetValues(typeof(DropdownSettingId)))
        {
            if (id == DropdownSettingId.Unassigned)
                continue;

            PlayerPrefs.DeleteKey(GetPrefsKey(id));
        }

        PlayerPrefs.Save();

        FramerateCapController.ApplyFromSettings();

        foreach (DropdownSettingId id in Enum.GetValues(typeof(DropdownSettingId)))
            Changed?.Invoke(id, Get(id));
    }

    public static string GetDisplayName(DropdownSettingId setting)
    {
        return setting switch
        {
            DropdownSettingId.Unassigned => "Dropdown setting",
            DropdownSettingId.CapFramerate => "Cap framerate",
            _ => setting.ToString()
        };
    }

    public static string GetPrefsKey(DropdownSettingId setting) => PrefsPrefix + setting;

    /// <summary>Standalone rows (custom labels + prefs key) without a store entry.</summary>
    public static int GetStandalone(string prefsKey, int defaultIndex, int optionCount)
    {
        if (string.IsNullOrWhiteSpace(prefsKey) || optionCount <= 0)
            return 0;

        int max = optionCount - 1;
        defaultIndex = Mathf.Clamp(defaultIndex, 0, max);
        return Mathf.Clamp(PlayerPrefs.GetInt(prefsKey, defaultIndex), 0, max);
    }

    public static void SetStandalone(string prefsKey, int index, int optionCount, int defaultIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(prefsKey) || optionCount <= 0)
            return;

        int clamped = Mathf.Clamp(index, 0, optionCount - 1);
        if (GetStandalone(prefsKey, defaultIndex, optionCount) == clamped)
            return;

        PlayerPrefs.SetInt(prefsKey, clamped);
        PlayerPrefs.Save();
    }
}
