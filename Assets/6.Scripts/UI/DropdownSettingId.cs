/// <summary>
/// Register dropdown settings here (mirrors <see cref="ToggleSettingId"/> / <see cref="SliderSettingId"/>).
/// </summary>
public enum DropdownSettingId
{
    /// <summary>Prefab default — assign a real id or use <see cref="DropdownSettingsRowUI"/> option overrides.</summary>
    Unassigned = 0,

    /// <summary>Limits frame rate via <see cref="FramerateCapPacer"/> (vSync off).</summary>
    CapFramerate = 1,
}
