public enum ToggleSettingId
{
    /// <summary>When true (default), the player overhead HP strip is visible outside combat.</summary>
    ShowPlayerHealthBarOutOfCombat = 0,

    UseTwentyFourHourTime = 1,
    ShowWindowResizeHandles = 2,
    TopMostGameWindow = 3,
    AutoTrackNewQuest = 4,
    AutoLootDuringAutoBattle = 5,

    /// <summary>When true (default), HP and guard show numeric text on player/enemy overheads; false = bars only.</summary>
    ShowOverheadHealthGuardNumbers = 6,

    /// <summary>When true (default), tutorial/helper tips may appear; false disables all helpers (persisted).</summary>
    ShowHelpPopups = 7,

    /// <summary>
    /// When true (default), repeated item-gain / purchase lines in the activity log merge into one entry with &quot;(Repeat action)&quot;.
    /// When false, each gain logs as its own line.
    /// </summary>
    GroupRepeatedActivityLogItemGains = 8,

    /// <summary>
    /// Stored preference: when true, non-essential strip screen overlay visuals are off (e.g. biome cave overlay).
    /// Settings UI may label this as &quot;Show screen overlay visuals&quot; and invert the checkbox.
    /// </summary>
    DisableScreenOverlayVisuals = 9,
}
