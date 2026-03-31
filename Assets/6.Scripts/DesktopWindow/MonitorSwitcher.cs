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
    [SerializeField] private KeyCode hotkey = KeyCode.F10;
    [SerializeField] private bool snapOnStart = true;
    [SerializeField] private int delayFrames = 6;
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
        if (!uniWin) return;

        if (Input.GetKeyDown(hotkey))
        {
            var mons = GetMonitors();
            if (mons.Count == 0) return;

            _index = (_index + 1) % mons.Count;
            MoveToMonitor(mons[_index]);
        }
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

    private static bool TryGetPrimary(List<MonitorRect> mons, out MonitorRect primary)
    {
        for (int i = 0; i < mons.Count; i++)
        {
            if (mons[i].primary)
            {
                primary = mons[i];
                return true;
            }
        }
        primary = default;
        return false;
    }

    private void MoveToMonitor(MonitorRect target)
    {
        var mons = GetMonitors();
        if (mons.Count == 0) return;

        if (!TryGetPrimary(mons, out var primary))
            primary = mons[0];

        // Use WORK AREA (excludes taskbar)
        RECT p = primary.monitor;
        RECT w = target.work;

        int workLeft = w.left;
        int workTop = w.top;
        int workRight = w.right;
        int workBottom = w.bottom;

        int workWidth = workRight - workLeft;
        int workHeight = workBottom - workTop;

        // Fill the monitor work area
        uniWin.windowSize = new Vector2(workWidth, workHeight);

        // Position at work area's bottom-left
        // UniWin origin: primary monitor bottom-left, Y+ up
        // Windows coords: Y+ down
        float uniX = workLeft - p.left;
        float uniY = p.bottom - workBottom;

        uniWin.windowPosition = new Vector2(uniX, uniY);
    }

#endif
}