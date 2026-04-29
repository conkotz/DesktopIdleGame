using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Kirurobo;

public class MonitorSwitcher : MonoBehaviour
{
    [Header("UniWin")]
    [SerializeField] private UniWindowController uniWin;

#pragma warning disable 0414
    [Tooltip("When enabled, cycles displays using the hotkey (Windows standalone builds only). Prefer the Settings ► Swap button.")]
    [SerializeField] private bool enableHotkey;
    [SerializeField] private KeyCode hotkey = KeyCode.F10;
    [SerializeField] private bool snapOnStart = true;
    [SerializeField] private int delayFrames = 6;
    [SerializeField] private bool syncUnityResolutionToMonitor = true;
    [SerializeField] private bool useNativeWorkAreaPlacement = true;
    [SerializeField] private bool refreshTransparencyAfterMove = true;
    private int _index = 0;
#pragma warning restore 0414

    private void Awake()
    {
        if (!uniWin) uniWin = FindFirstObjectByType<UniWindowController>();
    }

    private void OnEnable()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (snapOnStart)
            StartCoroutine(SnapStart());
#endif
    }



    private void Update()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!enableHotkey || !uniWin) return;

        if (Input.GetKeyDown(hotkey))
            SwapToNextMonitor();
#endif
    }

    /// <summary>Called from Settings UI (and optionally <see cref="enableHotkey"/>).</summary>
    public void SwapToNextMonitor()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!uniWin) uniWin = FindFirstObjectByType<UniWindowController>();
        if (!uniWin) return;

        var mons = GetMonitors();
        if (mons.Count == 0) return;

        _index = (_index + 1) % mons.Count;
        MoveToMonitor(mons[_index]);
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private IEnumerator SnapStart()
    {
        for (int i = 0; i < delayFrames; i++)
            yield return null;

        var mons = GetMonitors();
        if (mons.Count == 0 || !uniWin) yield break;

        // Start on PRIMARY monitor (index 0 after sorting)
        _index = 0;
        MoveToMonitor(mons[_index]);
    }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;    // work area (excludes taskbar)
        public uint dwFlags;
    }

    private const uint MONITORINFOF_PRIMARY = 0x00000001;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private struct MonitorRect
    {
        public RECT monitor;
        public RECT work;
        public bool primary;
    }

    private static List<MonitorRect> GetMonitors()
    {
        var list = new List<MonitorRect>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, hdc, rc, data) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(hMon, ref mi))
            {
                list.Add(new MonitorRect
                {
                    monitor = mi.rcMonitor,
                    work = mi.rcWork,
                    primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0
                });
            }
            return true;
        }, IntPtr.Zero);

        // Primary first for predictable cycling
        list.Sort((a, b) => b.primary.CompareTo(a.primary));
        return list;
    }

    private void MoveToMonitor(MonitorRect target)
    {
        RECT w = target.work;

        int workLeft = w.left;
        int workTop = w.top;
        int workRight = w.right;
        int workBottom = w.bottom;

        int workWidth = workRight - workLeft;
        int workHeight = workBottom - workTop;

        // Fill the monitor work area
        if (syncUnityResolutionToMonitor)
            Screen.SetResolution(workWidth, workHeight, FullScreenMode.Windowed);

        uniWin.windowSize = new Vector2(workWidth, workHeight);
        uniWin.windowPosition = new Vector2(workLeft, workTop);

        if (useNativeWorkAreaPlacement)
            ApplyNativeWindowRect(workLeft, workTop, workWidth, workHeight);

        RefreshTransparency();
        StartCoroutine(ReapplyUniWindowRectNextFrame(workLeft, workTop, workWidth, workHeight));
    }

    private IEnumerator ReapplyUniWindowRectNextFrame(int x, int y, int width, int height)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (uniWin)
        {
            uniWin.windowSize = new Vector2(width, height);
            uniWin.windowPosition = new Vector2(x, y);
            if (useNativeWorkAreaPlacement)
                ApplyNativeWindowRect(x, y, width, height);

            RefreshTransparency();
        }
    }

    private void RefreshTransparency()
    {
        if (!refreshTransparencyAfterMove || !uniWin)
            return;

        var type = uniWin.transparentType;
        uniWin.SetTransparentType(type);
        uniWin.isTransparent = true;
    }

    private static void ApplyNativeWindowRect(int x, int y, int width, int height)
    {
        IntPtr hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
            return;

        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            x,
            y,
            width,
            height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

#endif
}