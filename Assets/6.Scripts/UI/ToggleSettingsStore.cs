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
    private const string ExpandStripBackgroundKey = "Settings.ExpandStripBackground";
    private const string ShowFpsKey = "Settings.ShowFps";
    private const string MinimiseHudKey = "Settings.MinimiseHud";
    private const string LegacyMinimiseHudDisplayInTownKey = "Settings.MinimiseHudDisplayInTown";
    private const string ShowOffscreenMarkersKey = "Settings.ShowOffscreenMarkers";
    private const string ShowIncomingDamageNumbersKey = "Settings.ShowIncomingDamageNumbers";
    private const string ShowOutgoingDamageNumbersKey = "Settings.ShowOutgoingDamageNumbers";
    private const string MoveWindowPivotsKey = "Settings.MoveWindowPivots";
    private const string ShowDevPanelKey = "Settings.ShowDevPanel";
    private const string CompactDamageNumbersKey = "Settings.CompactDamageNumbers";
    private const string HidePlayerOverheadBarsKey = "Settings.HidePlayerOverheadBars";
    private const string DimHudWhenOverlappedKey = "Settings.DimHudWhenOverlapped";

    private static bool _moveWindowPivotsSessionActive;

    public static event Action<ToggleSettingId, bool> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionOnlyToggles()
    {
        _moveWindowPivotsSessionActive = false;
        if (PlayerPrefs.HasKey(MoveWindowPivotsKey))
        {
            PlayerPrefs.DeleteKey(MoveWindowPivotsKey);
            PlayerPrefs.Save();
        }
    }

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
            // Default off (0) — strip layout unlocked until the player opts in.
            ToggleSettingId.ExpandStripBackground =>
                PlayerPrefs.GetInt(ExpandStripBackgroundKey, 0) != 0,
            ToggleSettingId.ShowFps =>
                PlayerPrefs.GetInt(ShowFpsKey, 0) != 0,
            ToggleSettingId.MinimiseHud =>
                GetMinimiseHud(),
            ToggleSettingId.ShowOffscreenMarkers =>
                PlayerPrefs.GetInt(ShowOffscreenMarkersKey, 1) != 0,
            ToggleSettingId.ShowIncomingDamageNumbers =>
                PlayerPrefs.GetInt(ShowIncomingDamageNumbersKey, 1) != 0,
            ToggleSettingId.ShowOutgoingDamageNumbers =>
                PlayerPrefs.GetInt(ShowOutgoingDamageNumbersKey, 1) != 0,
            ToggleSettingId.MoveWindowPivots => _moveWindowPivotsSessionActive,
            ToggleSettingId.ShowDevPanel =>
                PlayerPrefs.GetInt(ShowDevPanelKey, 1) != 0,
            ToggleSettingId.CompactDamageNumbers =>
                PlayerPrefs.GetInt(CompactDamageNumbersKey, 0) != 0,
            ToggleSettingId.HidePlayerOverheadBars =>
                PlayerPrefs.GetInt(HidePlayerOverheadBarsKey, 0) != 0,
            ToggleSettingId.DimHudWhenOverlapped =>
                PlayerPrefs.GetInt(DimHudWhenOverlappedKey, 1) != 0,
            _ => false
        };
    }

    /// <summary>
    /// Show health bar out of combat. Default OFF for new installs. Migrates legacy &quot;hide&quot; prefs by inverting once.
    /// </summary>
    private static bool GetShowPlayerHealthBarOutOfCombat()
    {
        if (PlayerPrefs.HasKey(ShowPlayerHealthBarOutOfCombatKey))
            return PlayerPrefs.GetInt(ShowPlayerHealthBarOutOfCombatKey, 0) != 0;

        if (PlayerPrefs.HasKey(LegacyHidePlayerHealthBarOutOfCombatKey))
        {
            bool legacyHidePrimaryMeaningWasOn = PlayerPrefs.GetInt(LegacyHidePlayerHealthBarOutOfCombatKey, 1) != 0;
            return !legacyHidePrimaryMeaningWasOn;
        }

        return false;
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

    private static bool GetMinimiseHud()
    {
        if (PlayerPrefs.HasKey(MinimiseHudKey))
            return PlayerPrefs.GetInt(MinimiseHudKey, 0) != 0;

        if (PlayerPrefs.HasKey(LegacyMinimiseHudDisplayInTownKey))
        {
            bool enabled = PlayerPrefs.GetInt(LegacyMinimiseHudDisplayInTownKey, 0) != 0;
            PlayerPrefs.SetInt(MinimiseHudKey, enabled ? 1 : 0);
            PlayerPrefs.DeleteKey(LegacyMinimiseHudDisplayInTownKey);
            PlayerPrefs.Save();
            return enabled;
        }

        return false;
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
            case ToggleSettingId.ExpandStripBackground:
                PlayerPrefs.SetInt(ExpandStripBackgroundKey, value ? 1 : 0);
                break;
            case ToggleSettingId.ShowFps:
                PlayerPrefs.SetInt(ShowFpsKey, value ? 1 : 0);
                break;
            case ToggleSettingId.MinimiseHud:
                PlayerPrefs.SetInt(MinimiseHudKey, value ? 1 : 0);
                PlayerPrefs.DeleteKey(LegacyMinimiseHudDisplayInTownKey);
                break;
            case ToggleSettingId.ShowOffscreenMarkers:
                PlayerPrefs.SetInt(ShowOffscreenMarkersKey, value ? 1 : 0);
                break;
            case ToggleSettingId.ShowIncomingDamageNumbers:
                PlayerPrefs.SetInt(ShowIncomingDamageNumbersKey, value ? 1 : 0);
                break;
            case ToggleSettingId.ShowOutgoingDamageNumbers:
                PlayerPrefs.SetInt(ShowOutgoingDamageNumbersKey, value ? 1 : 0);
                break;
            case ToggleSettingId.MoveWindowPivots:
                _moveWindowPivotsSessionActive = value;
                if (PlayerPrefs.HasKey(MoveWindowPivotsKey))
                    PlayerPrefs.DeleteKey(MoveWindowPivotsKey);
                break;
            case ToggleSettingId.ShowDevPanel:
                PlayerPrefs.SetInt(ShowDevPanelKey, value ? 1 : 0);
                break;
            case ToggleSettingId.CompactDamageNumbers:
                PlayerPrefs.SetInt(CompactDamageNumbersKey, value ? 1 : 0);
                break;
            case ToggleSettingId.HidePlayerOverheadBars:
                PlayerPrefs.SetInt(HidePlayerOverheadBarsKey, value ? 1 : 0);
                break;
            case ToggleSettingId.DimHudWhenOverlapped:
                PlayerPrefs.SetInt(DimHudWhenOverlappedKey, value ? 1 : 0);
                break;
        }

        PlayerPrefs.Save();
        Changed?.Invoke(setting, value);

        if (setting == ToggleSettingId.ShowWindowResizeHandles)
            UIWindowCornerResize.RefreshAllHandlesVisibility();

        if (setting == ToggleSettingId.ExpandStripBackground)
            FullWindowBackgroundPresenter.RefreshAllFromSettings();

        if (setting == ToggleSettingId.ShowFps)
            FpsDisplayText.RefreshAllFromSettings();

        if (setting == ToggleSettingId.MinimiseHud)
            HUDToggle.RefreshAllFromMinimiseHudSetting();

        if (setting == ToggleSettingId.DimHudWhenOverlapped)
            HUDView.RefreshAllOverlapFadeFromSettings();

        if (setting == ToggleSettingId.ShowOffscreenMarkers)
            OffscreenMarkersController.RefreshAllFromSettings();

        if (setting == ToggleSettingId.MoveWindowPivots)
            MovePivotsModeController.RefreshAllFromSettings();

        if (setting == ToggleSettingId.ShowDevPanel)
            DevTestingPanelUI.RefreshAllFromSettings();
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
        PlayerPrefs.DeleteKey(ExpandStripBackgroundKey);
        PlayerPrefs.DeleteKey(ShowFpsKey);
        PlayerPrefs.DeleteKey(MinimiseHudKey);
        PlayerPrefs.DeleteKey(LegacyMinimiseHudDisplayInTownKey);
        PlayerPrefs.DeleteKey(ShowOffscreenMarkersKey);
        PlayerPrefs.DeleteKey(ShowIncomingDamageNumbersKey);
        PlayerPrefs.DeleteKey(ShowOutgoingDamageNumbersKey);
        PlayerPrefs.DeleteKey(MoveWindowPivotsKey);
        PlayerPrefs.DeleteKey(ShowDevPanelKey);
        PlayerPrefs.DeleteKey(CompactDamageNumbersKey);
        PlayerPrefs.DeleteKey(HidePlayerOverheadBarsKey);
        PlayerPrefs.DeleteKey(DimHudWhenOverlappedKey);
        PlayerPrefs.Save();

        _moveWindowPivotsSessionActive = false;

        UIWindowCornerResize.RefreshAllHandlesVisibility();
        FullWindowBackgroundPresenter.RefreshAllFromSettings();
        FpsDisplayText.RefreshAllFromSettings();
        HUDToggle.RefreshAllFromMinimiseHudSetting();
        HUDView.RefreshAllOverlapFadeFromSettings();
        OffscreenMarkersController.RefreshAllFromSettings();

        MovePivotsModeController.RefreshAllFromSettings();
        DevTestingPanelUI.RefreshAllFromSettings();

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
            ToggleSettingId.ExpandStripBackground =>
                "Expand background",
            ToggleSettingId.ShowFps =>
                "Show FPS",
            ToggleSettingId.MinimiseHud =>
                "Always minimise HUD when out of combat",
            ToggleSettingId.ShowOffscreenMarkers =>
                "Show Offscreen Markers",
            ToggleSettingId.ShowIncomingDamageNumbers =>
                "Show incoming damage numbers",
            ToggleSettingId.ShowOutgoingDamageNumbers =>
                "Show outgoing damage numbers",
            ToggleSettingId.MoveWindowPivots =>
                "Move pivots",
            ToggleSettingId.ShowDevPanel =>
                "Show dev panel (TESTING ONLY SETTING)",
            ToggleSettingId.CompactDamageNumbers =>
                "Compact damage numbers",
            ToggleSettingId.HidePlayerOverheadBars =>
                "Hide player overhead bars",
            ToggleSettingId.DimHudWhenOverlapped =>
                "Dim HUD when enemy or player is underneath",
            _ => setting.ToString()
        };
    }

    public static bool TryGetTooltip(ToggleSettingId setting, out string title, out string description)
    {
        title = null;
        description = null;

        switch (setting)
        {
            case ToggleSettingId.DisableScreenOverlayVisuals:
                title = GetDisplayName(setting);
                description =
                    "Optional fullscreen overlay effects.\n\n" +
                    "When enabled, shows woodcutting ability range indicators (e.g. Cleaving Chop), " +
                    "Spectral Axe area indicators, nearby tree hitbox range highlights, and the cave biome screen flicker.";
                return true;
            case ToggleSettingId.MoveWindowPivots:
                title = GetDisplayName(setting);
                description =
                    "Reposition HUD windows using colored placeholders instead of the live UI.\n\n" +
                    "Saved pivot positions apply the next time you load the game. " +
                    "Dragging a window during normal play keeps its position for the rest of the session only — " +
                    "it returns to the saved pivot on reload unless you update pivots here.";
                return true;
            default:
                return false;
        }
    }
}
