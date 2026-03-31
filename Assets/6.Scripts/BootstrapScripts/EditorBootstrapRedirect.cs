#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EditorBootstrapRedirect
{
    // Use a non-const so the compiler doesn't treat branches as unreachable in editor builds.
    private static bool ForceBootMenuOnPlay = true;
    private const string BootSceneName = "Bootstrap";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Redirect()
    {
        if (!ForceBootMenuOnPlay)
            return;

        var active = SceneManager.GetActiveScene();

        // If we're already in the boot scene, do nothing.
        if (active.name == BootSceneName)
            return;

        SceneManager.LoadScene(BootSceneName);
    }
}
#endif