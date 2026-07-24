using UnityEngine;

/// <summary>
/// Applies <see cref="DropdownSettingId.CapFramerate"/> as a maximum FPS cap.
/// Uses <see cref="FramerateCapPacer"/> only (not <see cref="Application.targetFrameRate"/>, which double-limits on Windows).
/// </summary>
public static class FramerateCapController
{
    private static readonly int[] s_capValues = { 60, 100, 144, 180, -1 };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DropdownSettingsStore.Changed -= OnDropdownSettingChanged;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        DropdownSettingsStore.Changed -= OnDropdownSettingChanged;
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
        QualitySettings.maxQueuedFrames = 1;

        if (fps <= 0)
        {
            FramerateCapPacer.SetTargetFps(-1);
            Application.targetFrameRate = -1;
            return;
        }

        Application.targetFrameRate = -1;
        FramerateCapPacer.SetTargetFps(fps);
    }

    public static int GetCapValueForIndex(int index)
    {
        index = Mathf.Clamp(index, 0, s_capValues.Length - 1);
        return s_capValues[index];
    }
}
