#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EditorBootstrapRedirect
{
    private const string BootstrapSceneName = "Bootstrap";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Redirect()
    {
        var active = SceneManager.GetActiveScene();

        if (active.name == BootstrapSceneName)
            return;

        SceneManager.LoadScene(BootstrapSceneName);
    }
}
#endif