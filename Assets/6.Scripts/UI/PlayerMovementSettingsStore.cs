using System;
using UnityEngine;

/// <summary>
/// Persists player movement input mode in <see cref="PlayerPrefs"/> (global, not per save slot).
/// </summary>
public static class PlayerMovementSettingsStore
{
    private const string MovementModeKey = "Settings.PlayerMovementMode";

    public static event Action<PlayerMovementMode> Changed;

    public static PlayerMovementMode GetMode()
    {
        int raw = PlayerPrefs.GetInt(MovementModeKey, (int)PlayerMovementMode.Mouse);
        return Enum.IsDefined(typeof(PlayerMovementMode), raw)
            ? (PlayerMovementMode)raw
            : PlayerMovementMode.Mouse;
    }

    public static bool UsesKeyboardMovement() => GetMode() == PlayerMovementMode.Keyboard;

    public static void SetMode(PlayerMovementMode mode)
    {
        if (!Enum.IsDefined(typeof(PlayerMovementMode), mode))
            mode = PlayerMovementMode.Mouse;

        if (GetMode() == mode)
            return;

        PlayerPrefs.SetInt(MovementModeKey, (int)mode);
        PlayerPrefs.Save();
        Changed?.Invoke(mode);
    }

    public static void ToggleMode()
    {
        SetMode(GetMode() == PlayerMovementMode.Mouse
            ? PlayerMovementMode.Keyboard
            : PlayerMovementMode.Mouse);
    }

    internal static void ClearStoredKeyAndReload()
    {
        PlayerPrefs.DeleteKey(MovementModeKey);
        PlayerPrefs.Save();
        Changed?.Invoke(GetMode());
    }

    public static string GetButtonLabel(PlayerMovementMode mode)
    {
        return mode == PlayerMovementMode.Keyboard ? "Keyboard" : "Mouse";
    }
}
