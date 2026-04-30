using System;
using UnityEngine;

/// <summary>
/// Persists miscellaneous toggles in <see cref="PlayerPrefs"/> (global keys, not tied to save slots).
/// </summary>
public static class ToggleSettingsStore
{
    private const string HidePlayerHealthBarOutOfCombatKey = "Settings.HidePlayerHealthBarOutOfCombat";
    private const string UseTwentyFourHourTimeKey = "Settings.UseTwentyFourHourTime";
    private const string ShowWindowResizeHandlesKey = "Settings.ShowWindowResizeHandles";
    private const string TopMostGameWindowKey = "Settings.TopMostGameWindow";
    private const string AutoTrackNewQuestKey = "Settings.AutoTrackNewQuest";
    private const string AutoLootDuringAutoBattleKey = "Settings.AutoLootDuringAutoBattle";

    public static event Action<ToggleSettingId, bool> Changed;

    public static bool Get(ToggleSettingId setting)
    {
        return setting switch
        {
            ToggleSettingId.HidePlayerHealthBarOutOfCombat =>
                PlayerPrefs.GetInt(HidePlayerHealthBarOutOfCombatKey, 1) != 0,
            ToggleSettingId.UseTwentyFourHourTime =>
                PlayerPrefs.GetInt(UseTwentyFourHourTimeKey, 1) != 0,
            ToggleSettingId.ShowWindowResizeHandles =>
                PlayerPrefs.GetInt(ShowWindowResizeHandlesKey, 0) != 0,
            ToggleSettingId.TopMostGameWindow =>
                PlayerPrefs.GetInt(TopMostGameWindowKey, 1) != 0,
            ToggleSettingId.AutoTrackNewQuest =>
                PlayerPrefs.GetInt(AutoTrackNewQuestKey, 1) != 0,
            ToggleSettingId.AutoLootDuringAutoBattle =>
                PlayerPrefs.GetInt(AutoLootDuringAutoBattleKey, 1) != 0,
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
            case ToggleSettingId.AutoTrackNewQuest:
                PlayerPrefs.SetInt(AutoTrackNewQuestKey, value ? 1 : 0);
                break;
            case ToggleSettingId.AutoLootDuringAutoBattle:
                PlayerPrefs.SetInt(AutoLootDuringAutoBattleKey, value ? 1 : 0);
                break;
        }

        PlayerPrefs.Save();
        Changed?.Invoke(setting, value);

        if (setting == ToggleSettingId.ShowWindowResizeHandles)
            UIWindowCornerResize.RefreshAllHandlesVisibility();
    }

    internal static void ClearAllStoredKeysAndReload()
    {
        PlayerPrefs.DeleteKey(HidePlayerHealthBarOutOfCombatKey);
        PlayerPrefs.DeleteKey(UseTwentyFourHourTimeKey);
        PlayerPrefs.DeleteKey(ShowWindowResizeHandlesKey);
        PlayerPrefs.DeleteKey(TopMostGameWindowKey);
        PlayerPrefs.DeleteKey(AutoTrackNewQuestKey);
        PlayerPrefs.DeleteKey(AutoLootDuringAutoBattleKey);
        PlayerPrefs.Save();

        UIWindowCornerResize.RefreshAllHandlesVisibility();

        foreach (ToggleSettingId id in Enum.GetValues(typeof(ToggleSettingId)))
            Changed?.Invoke(id, Get(id));
    }

    public static string GetDisplayName(ToggleSettingId setting)
    {
        return setting switch
        {
            ToggleSettingId.HidePlayerHealthBarOutOfCombat => "Hide player health bar out of combat",
            ToggleSettingId.UseTwentyFourHourTime => "Use 24-hour time",
            ToggleSettingId.ShowWindowResizeHandles => "Show window resize handles",
            ToggleSettingId.TopMostGameWindow => "Is topmost game window",
            ToggleSettingId.AutoTrackNewQuest => "Auto track new quest",
            ToggleSettingId.AutoLootDuringAutoBattle => "Auto loot during auto battle",
            _ => setting.ToString()
        };
    }
}
