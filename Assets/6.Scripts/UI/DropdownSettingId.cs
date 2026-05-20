/// <summary>
/// Register dropdown settings here (mirrors <see cref="ToggleSettingId"/> / <see cref="SliderSettingId"/>).
/// </summary>
public enum DropdownSettingId
{
    /// <summary>Prefab default — assign a real id or use <see cref="DropdownSettingsRowUI"/> option overrides.</summary>
    Unassigned = 0,

    /// <summary>Limits <see cref="Application.targetFrameRate"/> (vSync off).</summary>
    CapFramerate = 1,
}
