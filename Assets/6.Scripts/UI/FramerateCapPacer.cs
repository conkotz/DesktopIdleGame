using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Caps maximum frame rate for <see cref="FramerateCapController"/>.
/// Sleeps at the start of each frame (aligned with the FPS counter) only when ahead of schedule.
/// </summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class FramerateCapPacer : MonoBehaviour
{
    private const double SpinReserveSeconds = 0.0025;
    private const double WaitEpsilonSeconds = 0.00005;

    private static FramerateCapPacer s_instance;
    private static int s_targetFps = -1;
    private static bool s_highResTimerActive;

    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    private double _nextFrameStartDeadline;
    private bool _hasDeadline;
    private SpinWait _spinWait;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint periodMs);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint periodMs);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_instance = null;
        s_targetFps = -1;
        s_highResTimerActive = false;
    }

    public static void SetTargetFps(int fps)
    {
        if (fps == s_targetFps)
        {
            if (s_instance != null)
                s_instance.ResetDeadline();
            return;
        }

        s_targetFps = fps;
        SetHighResolutionTimer(fps > 0);

        EnsureExists();
        if (s_instance != null)
            s_instance.ResetDeadline();
    }

    private static void SetHighResolutionTimer(bool enable)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (enable == s_highResTimerActive)
            return;

        if (enable)
            TimeBeginPeriod(1);
        else
            TimeEndPeriod(1);

        s_highResTimerActive = enable;
#endif
    }

    private static void EnsureExists()
    {
        if (s_instance != null)
            return;

        var go = new GameObject(nameof(FramerateCapPacer));
        s_instance = go.AddComponent<FramerateCapPacer>();
        DontDestroyOnLoad(go);
    }

    private void ResetDeadline()
    {
        _nextFrameStartDeadline = 0;
        _hasDeadline = false;
    }

    private void Update()
    {
        if (s_targetFps <= 0)
            return;

        double frameSeconds = 1.0 / s_targetFps;
        double now = s_clock.Elapsed.TotalSeconds;

        if (!_hasDeadline)
        {
            _nextFrameStartDeadline = now + frameSeconds;
            _hasDeadline = true;
            return;
        }

        double wait = _nextFrameStartDeadline - now;
        while (wait > WaitEpsilonSeconds)
        {
            if (wait > SpinReserveSeconds + WaitEpsilonSeconds)
            {
                int sleepMs = Mathf.Max(1, (int)((wait - SpinReserveSeconds) * 1000));
                Thread.Sleep(sleepMs);
            }
            else
            {
                _spinWait.SpinOnce();
            }

            now = s_clock.Elapsed.TotalSeconds;
            wait = _nextFrameStartDeadline - now;
        }

        _nextFrameStartDeadline += frameSeconds;

        now = s_clock.Elapsed.TotalSeconds;
        if (_nextFrameStartDeadline < now)
            _nextFrameStartDeadline = now;
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
            SetHighResolutionTimer(false);
            s_targetFps = -1;
        }
    }
}
