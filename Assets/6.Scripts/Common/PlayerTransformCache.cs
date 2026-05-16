using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared player transform lookup for world-facing scripts (avoids per-instance Find every frame).
/// </summary>
public static class PlayerTransformCache
{
    private static Transform s_player;
    private static string s_playerTag = "Player";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_player = null;
        s_playerTag = "Player";
    }

    static PlayerTransformCache()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Invalidate();
    }

    public static void Invalidate() => s_player = null;

    public static Transform Resolve(string playerTag = "Player", PlayerController playerOverride = null)
    {
        if (playerOverride)
        {
            s_player = playerOverride.transform;
            return s_player;
        }

        if (s_player)
            return s_player;

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            s_playerTag = playerTag.Trim();
            GameObject tagged = GameObject.FindGameObjectWithTag(s_playerTag);
            if (tagged)
            {
                s_player = tagged.transform;
                return s_player;
            }
        }

        PlayerController pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        s_player = pc ? pc.transform : null;
        return s_player;
    }
}
