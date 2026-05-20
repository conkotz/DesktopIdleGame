using UnityEngine;

/// <summary>
/// Applies <see cref="DropdownSettingId.CapFramerate"/> via <see cref="Application.targetFrameRate"/>.
/// </summary>
public static class FramerateCapController
{
    private static readonly int[] s_capValues = { 60, 100, 144, 180, -1 };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        DropdownSettingsStore.Changed += OnDropdownSettingChanged;
        ApplyFromSettings();
    }

    private static void OnDropdownSettingChanged(DropdownSettingId id, int _)
    {
        if (id == DropdownSettingId.CapFramerate)
            ApplyFromSettings();
    }

    public static void ApplyFromSettings()
    {
        if (!DropdownSettingsStore.HasOptions(DropdownSettingId.CapFramerate))
            return;

        ApplyCapIndex(DropdownSettingsStore.Get(DropdownSettingId.CapFramerate));
    }

    public static void ApplyCapIndex(int index)
    {
        int maxIndex = s_capValues.Length - 1;
        index = Mathf.Clamp(index, 0, maxIndex);
        int fps = s_capValues[index];

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;
    }

    public static int GetCapValueForIndex(int index)
    {
        index = Mathf.Clamp(index, 0, s_capValues.Length - 1);
        return s_capValues[index];
    }
}
