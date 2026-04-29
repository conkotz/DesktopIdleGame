using System;
using UnityEngine;

public static class ToggleSettingsStore
{
    private const string HidePlayerHealthBarOutOfCombatKey = "Settings.HidePlayerHealthBarOutOfCombat";
    private const string UseTwentyFourHourTimeKey = "Settings.UseTwentyFourHourTime";
    private const string ShowWindowResizeHandlesKey = "Settings.ShowWindowResizeHandles";
    private const string TopMostGameWindowKey = "Settings.TopMostGameWindow";

    public static event Action<ToggleSettingId, bool> Changed;

    public static bool Get(ToggleSettingId setting)
    {
        return setting switch
        {
            ToggleSettingId.HidePlayerHealthBarOutOfCombat =>
                PlayerPrefs.GetInt(HidePlayerHealthBarOutOfCombatKey, 0) != 0,
            ToggleSettingId.UseTwentyFourHourTime =>
                PlayerPrefs.GetInt(UseTwentyFourHourTimeKey, 1) != 0,
            ToggleSettingId.ShowWindowResizeHandles =>
                PlayerPrefs.GetInt(ShowWindowResizeHandlesKey, 0) != 0,
            ToggleSettingId.TopMostGameWindow =>
                PlayerPrefs.GetInt(TopMostGameWindowKey, 1) != 0,
            _ => false
        };
    }

    public static void Set(ToggleSettingId setting, bool value)
    {
        if (Get(setting) == value)
            return;

        switch (setting)
        {
            case ToggleSettingId.HidePlayerHealthBarOutOfCombat:
                PlayerPrefs.SetInt(HidePlayerHealthBarOutOfCombatKey, value ? 1 : 0);
                break;
            case ToggleSettingId.UseTwentyFourHourTime:
                PlayerPrefs.SetInt(UseTwentyFourHourTimeKey, value ? 1 : 0);
                break;
            case ToggleSettingId.ShowWindowResizeHandles:
                PlayerPrefs.SetInt(ShowWindowResizeHandlesKey, value ? 1 : 0);
                break;
            case ToggleSettingId.TopMostGameWindow:
                PlayerPrefs.SetInt(TopMostGameWindowKey, value ? 1 : 0);
                break;
        }

        PlayerPrefs.Save();
        Changed?.Invoke(setting, value);

        if (setting == ToggleSettingId.ShowWindowResizeHandles)
            UIWindowCornerResize.RefreshAllHandlesVisibility();
    }

    public static string GetDisplayName(ToggleSettingId setting)
    {
        return setting switch
        {
            ToggleSettingId.HidePlayerHealthBarOutOfCombat => "Hide player health bar out of combat",
            ToggleSettingId.UseTwentyFourHourTime => "Use 24-hour time",
            ToggleSettingId.ShowWindowResizeHandles => "Show window resize handles",
            ToggleSettingId.TopMostGameWindow => "Is topmost game window",
            _ => setting.ToString()
        };
    }
}
