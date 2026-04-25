using System;
using UnityEngine;

public static class ToggleSettingsStore
{
    private const string HidePlayerHealthBarOutOfCombatKey = "Settings.HidePlayerHealthBarOutOfCombat";

    public static event Action<ToggleSettingId, bool> Changed;

    public static bool Get(ToggleSettingId setting)
    {
        return setting switch
        {
            ToggleSettingId.HidePlayerHealthBarOutOfCombat =>
                PlayerPrefs.GetInt(HidePlayerHealthBarOutOfCombatKey, 0) != 0,
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
        }

        PlayerPrefs.Save();
        Changed?.Invoke(setting, value);
    }

    public static string GetDisplayName(ToggleSettingId setting)
    {
        return setting switch
        {
            ToggleSettingId.HidePlayerHealthBarOutOfCombat => "Hide player health bar out of combat",
            _ => setting.ToString()
        };
    }
}
