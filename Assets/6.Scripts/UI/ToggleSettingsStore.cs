using System;
using UnityEngine;

/// <summary>
/// Persists miscellaneous toggles in <see cref="PlayerPrefs"/> (global keys, not tied to save slots).
/// </summary>
public static class ToggleSettingsStore
{
    private const string LegacyHidePlayerHealthBarOutOfCombatKey = "Settings.HidePlayerHealthBarOutOfCombat";
    private const string ShowPlayerHealthBarOutOfCombatKey = "Settings.ShowPlayerHealthBarOutOfCombat";
    private const string ShowOverheadHealthGuardNumbersKey = "Settings.ShowOverheadHealthGuardNumbers";
    private const string UseTwentyFourHourTimeKey = "Settings.UseTwentyFourHourTime";
    private const string ShowWindowResizeHandlesKey = "Settings.ShowWindowResizeHandles";
    private const string TopMostGameWindowKey = "Settings.TopMostGameWindow";
    private const string AutoTrackNewQuestKey = "Settings.AutoTrackNewQuest";
    private const string AutoLootDuringAutoBattleKey = "Settings.AutoLootDuringAutoBattle";
    private const string ShowHelpPopupsKey = "Settings.ShowHelpPopups";
    /// <summary>Inverted naming from before ShowHelpPopups; migrated once.</summary>
    private const string LegacyHideHelpPopupsKey = "Settings.HideHelpPopups";
    private const string GroupRepeatedActivityLogItemGainsKey = "Settings.GroupRepeatedActivityLogItemGains";
    private const string DisableScreenOverlayVisualsKey = "Settings.DisableScreenOverlayVisuals";

    public static event Action<ToggleSettingId, bool> Changed;

    public static bool Get(ToggleSettingId setting)
    {
        return setting switch
        {
            ToggleSettingId.ShowPlayerHealthBarOutOfCombat => GetShowPlayerHealthBarOutOfCombat(),
            ToggleSettingId.ShowOverheadHealthGuardNumbers =>
                PlayerPrefs.GetInt(ShowOverheadHealthGuardNumbersKey, 1) != 0,
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
            ToggleSettingId.ShowHelpPopups => GetShowHelpPopups(),
            ToggleSettingId.GroupRepeatedActivityLogItemGains =>
                PlayerPrefs.GetInt(GroupRepeatedActivityLogItemGainsKey, 0) != 0,
            ToggleSettingId.DisableScreenOverlayVisuals =>
                PlayerPrefs.GetInt(DisableScreenOverlayVisualsKey, 0) != 0,
            _ => false
        };
    }

    /// <summary>
    /// New key prefers show-out-of-combat (default ON). Migrates legacy &quot;hide&quot; prefs by inverting once.
    /// </summary>
    private static bool GetShowPlayerHealthBarOutOfCombat()
    {
        if (PlayerPrefs.HasKey(ShowPlayerHealthBarOutOfCombatKey))
            return PlayerPrefs.GetInt(ShowPlayerHealthBarOutOfCombatKey, 1) != 0;

        if (PlayerPrefs.HasKey(LegacyHidePlayerHealthBarOutOfCombatKey))
        {
            bool legacyHidePrimaryMeaningWasOn = PlayerPrefs.GetInt(LegacyHidePlayerHealthBarOutOfCombatKey, 1) != 0;
            return !legacyHidePrimaryMeaningWasOn;
        }

        return true;
    }

    private static bool GetShowHelpPopups()
    {
        if (PlayerPrefs.HasKey(ShowHelpPopupsKey))
            return PlayerPrefs.GetInt(ShowHelpPopupsKey, 1) != 0;

        if (PlayerPrefs.HasKey(LegacyHideHelpPopupsKey))
        {
            bool hideLegacyOn = PlayerPrefs.GetInt(LegacyHideHelpPopupsKey, 0) != 0;
            bool show = !hideLegacyOn;
            PlayerPrefs.SetInt(ShowHelpPopupsKey, show ? 1 : 0);
            PlayerPrefs.DeleteKey(LegacyHideHelpPopupsKey);
            PlayerPrefs.Save();
            return show;
        }

        return true;
    }

    public static void Set(ToggleSettingId setting, bool value)
    {
        if (Get(setting) == value)
            return;

        switch (setting)
        {
            case ToggleSettingId.ShowPlayerHealthBarOutOfCombat:
                PlayerPrefs.SetInt(ShowPlayerHealthBarOutOfCombatKey, value ? 1 : 0);
                PlayerPrefs.DeleteKey(LegacyHidePlayerHealthBarOutOfCombatKey);
                break;
            case ToggleSettingId.ShowOverheadHealthGuardNumbers:
                PlayerPrefs.SetInt(ShowOverheadHealthGuardNumbersKey, value ? 1 : 0);
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
            case ToggleSettingId.ShowHelpPopups:
                PlayerPrefs.SetInt(ShowHelpPopupsKey, value ? 1 : 0);
                PlayerPrefs.DeleteKey(LegacyHideHelpPopupsKey);
                break;
            case ToggleSettingId.GroupRepeatedActivityLogItemGains:
                PlayerPrefs.SetInt(GroupRepeatedActivityLogItemGainsKey, value ? 1 : 0);
                break;
            case ToggleSettingId.DisableScreenOverlayVisuals:
                PlayerPrefs.SetInt(DisableScreenOverlayVisualsKey, value ? 1 : 0);
                break;
        }

        PlayerPrefs.Save();
        Changed?.Invoke(setting, value);

        if (setting == ToggleSettingId.ShowWindowResizeHandles)
            UIWindowCornerResize.RefreshAllHandlesVisibility();
    }

    internal static void ClearAllStoredKeysAndReload()
    {
        PlayerPrefs.DeleteKey(LegacyHidePlayerHealthBarOutOfCombatKey);
        PlayerPrefs.DeleteKey(ShowPlayerHealthBarOutOfCombatKey);
        PlayerPrefs.DeleteKey(ShowOverheadHealthGuardNumbersKey);
        PlayerPrefs.DeleteKey(UseTwentyFourHourTimeKey);
        PlayerPrefs.DeleteKey(ShowWindowResizeHandlesKey);
        PlayerPrefs.DeleteKey(TopMostGameWindowKey);
        PlayerPrefs.DeleteKey(AutoTrackNewQuestKey);
        PlayerPrefs.DeleteKey(AutoLootDuringAutoBattleKey);
        PlayerPrefs.DeleteKey(ShowHelpPopupsKey);
        PlayerPrefs.DeleteKey(LegacyHideHelpPopupsKey);
        PlayerPrefs.DeleteKey(GroupRepeatedActivityLogItemGainsKey);
        PlayerPrefs.DeleteKey(DisableScreenOverlayVisualsKey);
        PlayerPrefs.Save();

        UIWindowCornerResize.RefreshAllHandlesVisibility();

        foreach (ToggleSettingId id in Enum.GetValues(typeof(ToggleSettingId)))
            Changed?.Invoke(id, Get(id));
    }

    public static string GetDisplayName(ToggleSettingId setting)
    {
        return setting switch
        {
            ToggleSettingId.ShowPlayerHealthBarOutOfCombat => "Show player health bar out of combat",
            ToggleSettingId.ShowOverheadHealthGuardNumbers => "Show health and guard number values on hp bars",
            ToggleSettingId.UseTwentyFourHourTime => "Use 24-hour time",
            ToggleSettingId.ShowWindowResizeHandles => "Show window resize handles",
            ToggleSettingId.TopMostGameWindow => "Is topmost game window",
            ToggleSettingId.AutoTrackNewQuest => "Auto track new quest",
            ToggleSettingId.AutoLootDuringAutoBattle => "Auto loot during auto battle",
            ToggleSettingId.ShowHelpPopups => "Enable help feature",
            ToggleSettingId.GroupRepeatedActivityLogItemGains =>
                "Show repeated actions as grouped in activity log",
            ToggleSettingId.DisableScreenOverlayVisuals =>
                "Show screen overlay visuals",
            _ => setting.ToString()
        };
    }
}
