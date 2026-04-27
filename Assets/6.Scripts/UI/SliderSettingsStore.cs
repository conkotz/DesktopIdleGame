using System;
using UnityEngine;

public static class SliderSettingsStore
{
    public const string HudResizeKey = "ui.scaleMultiplier";
    public const string WindowResizeKey = "ui.windowScaleMultiplier";

    public static event Action<SliderSettingId, float> Changed;

    public static float Get(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => PlayerPrefs.GetFloat(HudResizeKey, GetDefault(setting)),
            SliderSettingId.WindowResize => PlayerPrefs.GetFloat(WindowResizeKey, GetDefault(setting)),
            _ => GetDefault(setting)
        };
    }

    public static void Set(SliderSettingId setting, float value)
    {
        float clamped = Mathf.Clamp(value, GetMin(setting), GetMax(setting));
        if (Mathf.Approximately(Get(setting), clamped))
            return;

        switch (setting)
        {
            case SliderSettingId.HudResize:
                PlayerPrefs.SetFloat(HudResizeKey, clamped);
                break;
            case SliderSettingId.WindowResize:
                PlayerPrefs.SetFloat(WindowResizeKey, clamped);
                break;
        }

        PlayerPrefs.Save();
        Changed?.Invoke(setting, clamped);
    }

    public static string GetDisplayName(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => "HUD resize",
            SliderSettingId.WindowResize => "Window resize",
            _ => setting.ToString()
        };
    }

    public static float GetDefault(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => 1f,
            SliderSettingId.WindowResize => 1f,
            _ => 0f
        };
    }

    public static float GetMin(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => 0.5f,
            SliderSettingId.WindowResize => 0.5f,
            _ => 0f
        };
    }

    public static float GetMax(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => 1.5f,
            SliderSettingId.WindowResize => 1.5f,
            _ => 1f
        };
    }
}
