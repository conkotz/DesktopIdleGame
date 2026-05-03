using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Hook a UI <see cref="Button"/> (e.g. Bootstrap <c>ExitGameButton</c>): quits the player build, or stops Play Mode in the Editor.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class ExitGameButton : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(QuitApplication);
    }

    private void OnDestroy()
    {
        if (_button)
            _button.onClick.RemoveListener(QuitApplication);
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
