using System;
using UnityEngine;

public static class SliderSettingsStore
{
    public const string HudResizeKey = "ui.scaleMultiplier";
    public const string WindowResizeKey = "ui.windowScaleMultiplier";
    public const string OverheadHpBarResizeKey = "ui.overheadHpBarScaleMultiplier";

    public static event Action<SliderSettingId, float> Changed;

    public static float Get(SliderSettingId setting)
    {
        float value = setting switch
        {
            SliderSettingId.HudResize => PlayerPrefs.GetFloat(HudResizeKey, GetDefault(setting)),
            SliderSettingId.WindowResize => PlayerPrefs.GetFloat(WindowResizeKey, GetDefault(setting)),
            SliderSettingId.OverheadHpBarResize =>
                PlayerPrefs.GetFloat(OverheadHpBarResizeKey, GetDefault(setting)),
            _ => GetDefault(setting)
        };

        return Mathf.Clamp(value, GetMin(setting), GetMax(setting));
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
            case SliderSettingId.OverheadHpBarResize:
                PlayerPrefs.SetFloat(OverheadHpBarResizeKey, clamped);
                break;
        }

        PlayerPrefs.Save();
        Changed?.Invoke(setting, clamped);
    }

    internal static void ClearAllStoredKeysAndReload()
    {
        PlayerPrefs.DeleteKey(HudResizeKey);
        PlayerPrefs.DeleteKey(WindowResizeKey);
        PlayerPrefs.DeleteKey(OverheadHpBarResizeKey);
        PlayerPrefs.Save();

        foreach (SliderSettingId id in Enum.GetValues(typeof(SliderSettingId)))
            Changed?.Invoke(id, Get(id));
    }

    public static string GetDisplayName(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => "HUD resize",
            SliderSettingId.WindowResize => "Window resize",
            SliderSettingId.OverheadHpBarResize => "Overhead HP bar resize",
            _ => setting.ToString()
        };
    }

    public static bool IsVisible(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.WindowResize => false,
            _ => true
        };
    }

    public static float GetDefault(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => 1f,
            SliderSettingId.WindowResize => 1f,
            SliderSettingId.OverheadHpBarResize => 1f,
            _ => 0f
        };
    }

    public static float GetMin(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => 0.75f,
            SliderSettingId.WindowResize => 0.75f,
            SliderSettingId.OverheadHpBarResize => 0.75f,
            _ => 0f
        };
    }

    public static float GetMax(SliderSettingId setting)
    {
        return setting switch
        {
            SliderSettingId.HudResize => 1.25f,
            SliderSettingId.WindowResize => 1.25f,
            SliderSettingId.OverheadHpBarResize => 1.25f,
            _ => 1f
        };
    }
}
