using System;
using UnityEngine;

/// <summary>
/// User preferences stored in <see cref="PlayerPrefs"/> (machine-wide keys, independent of save slots).
/// New games do not erase these entries — settings persist across new games unless the OS clears prefs.
/// </summary>
public static class GlobalUserSettings
{
    public static event Action RestoredDefaults;

    public static void RestoreAllToDefaults()
    {
        ToggleSettingsStore.ClearAllStoredKeysAndReload();
        SliderSettingsStore.ClearAllStoredKeysAndReload();
        DropdownSettingsStore.ClearAllStoredKeysAndReload();
        PlayerMovementSettingsStore.ClearStoredKeyAndReload();
        HotkeyBindingManager.ResetPersistedBindingsToDefaults();
        StripCameraController.FactoryResetStoredStripLayoutAcrossApp();

        UIWindowPositionMemory.ResetAllWindowsToAnchors();

        PlayerPrefs.Save();
        RestoredDefaults?.Invoke();
        GameLog.Add("All settings restored to factory defaults.");
    }
}
