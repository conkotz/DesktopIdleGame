using UnityEngine;

/// <summary>
/// Death → respawn reloads the GamePlay scene with <see cref="LoadSceneMode.Single"/>, which destroys
/// <see cref="HelperGameplayController"/>. This flag tells the old instance not to tear down the DontDestroyOnLoad
/// helper overlay so the new scene can re-wire and restore modal state.
/// </summary>
public static class GameplayRespawnHelperPersistence
{
    public const string KeepOverlayAcrossNextGameplayLoadKey = "Helper.KeepOverlayAcrossNextGameplayLoad";

    public static void MarkKeepHelperOverlayAcrossNextGameplayLoad()
    {
        PlayerPrefs.SetInt(KeepOverlayAcrossNextGameplayLoadKey, 1);
        PlayerPrefs.Save();
    }

    public static bool IsKeepHelperOverlayAcrossNextGameplayLoadFlagSet() =>
        PlayerPrefs.GetInt(KeepOverlayAcrossNextGameplayLoadKey, 0) != 0;

    public static bool ConsumeKeepHelperOverlayAcrossNextGameplayLoad()
    {
        if (PlayerPrefs.GetInt(KeepOverlayAcrossNextGameplayLoadKey, 0) == 0)
            return false;

        PlayerPrefs.DeleteKey(KeepOverlayAcrossNextGameplayLoadKey);
        PlayerPrefs.Save();
        return true;
    }

    public static void ClearStaleKeepOverlayFlagIfPresent()
    {
        if (PlayerPrefs.GetInt(KeepOverlayAcrossNextGameplayLoadKey, 0) == 0)
            return;

        PlayerPrefs.DeleteKey(KeepOverlayAcrossNextGameplayLoadKey);
        PlayerPrefs.Save();
    }
}
