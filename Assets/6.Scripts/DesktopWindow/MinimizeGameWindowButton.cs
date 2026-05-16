using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone: minimizes the game OS window (taskbar), including when UniWindow <c>topmost</c> is enabled.
/// Wire on <c>UIButton_MinimiseGame</c> or call <see cref="MinimizeApplicationWindow"/> from a Button On Click list.
/// </summary>
[RequireComponent(typeof(Button))]
public class MinimizeGameWindowButton : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.RemoveListener(MinimizeApplicationWindow);
        _button.onClick.AddListener(MinimizeApplicationWindow);
    }

    private void OnEnable()
    {
        if (!_button)
            _button = GetComponent<Button>();
        _button.onClick.RemoveListener(MinimizeApplicationWindow);
        _button.onClick.AddListener(MinimizeApplicationWindow);
    }

    private void OnDisable()
    {
        if (_button)
            _button.onClick.RemoveListener(MinimizeApplicationWindow);
    }

    public void MinimizeApplicationWindow()
    {
#if UNITY_EDITOR
        Debug.Log($"[{nameof(MinimizeGameWindowButton)}] Minimizes the player window in Windows/macOS standalone builds (not in the Editor).");
#elif UNITY_STANDALONE_WIN
        // Prefer foreground window right after the click; fall back for edge cases.
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            hwnd = GetActiveWindow();
        if (hwnd != IntPtr.Zero)
            ShowWindow(hwnd, SW_MINIMIZE);
        else
            Debug.LogWarning($"[{nameof(MinimizeGameWindowButton)}] No window handle — could not minimize.");
#elif UNITY_STANDALONE_OSX
        Debug.LogWarning(
            $"[{nameof(MinimizeGameWindowButton)}] macOS: use the window's yellow minimize control or add a native bridge. Programmatic minimize is not implemented here.");
#else
        Debug.LogWarning(
            $"[{nameof(MinimizeGameWindowButton)}] OS minimize is only implemented for Windows and macOS standalone (current: {Application.platform}).");
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const int SW_MINIMIZE = 6;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
#endif
}
