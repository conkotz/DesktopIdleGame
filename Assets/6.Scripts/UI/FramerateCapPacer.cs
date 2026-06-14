using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Caps maximum frame rate for <see cref="FramerateCapController"/>.
/// Waits after <see cref="WaitForEndOfFrame"/> so render/present time is included in each frame slot.
/// </summary>
[DefaultExecutionOrder(32000)]
[DisallowMultipleComponent]
public sealed class FramerateCapPacer : MonoBehaviour
{
    private const double SpinReserveSeconds = 0.0015;
    private const double WaitEpsilonSeconds = 0.00002;
    private const int SpinOnlyThresholdFps = 90;

    private const double HighCapPipelineBoostPerFps = 0.000284;
    private const int HighCapPipelineBoostStartFps = 100;

    private static FramerateCapPacer s_instance;
    private static int s_targetFps = -1;
    private static bool s_highResTimerActive;

    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    private double _nextFrameDeadline;
    private bool _hasDeadline;
    private SpinWait _spinWait;
    private Coroutine _paceRoutine;

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

    private void OnEnable()
    {
        _paceRoutine = StartCoroutine(CoPaceAfterEndOfFrame());
    }

    private void OnDisable()
    {
        if (_paceRoutine != null)
        {
            StopCoroutine(_paceRoutine);
            _paceRoutine = null;
        }
    }

    private void ResetDeadline()
    {
        _nextFrameDeadline = 0;
        _hasDeadline = false;
    }

    private IEnumerator CoPaceAfterEndOfFrame()
    {
        var endOfFrame = new WaitForEndOfFrame();

        while (true)
        {
            yield return endOfFrame;

            if (s_targetFps <= 0)
                continue;

            double frameSeconds = GetFrameSeconds(s_targetFps);
            double now = s_clock.Elapsed.TotalSeconds;

            if (!_hasDeadline)
            {
                _nextFrameDeadline = now + frameSeconds;
                _hasDeadline = true;
                continue;
            }

            WaitUntil(_nextFrameDeadline);

            _nextFrameDeadline += frameSeconds;

            now = s_clock.Elapsed.TotalSeconds;
            if (_nextFrameDeadline < now)
                _nextFrameDeadline = now;
        }
    }

    private static double GetFrameSeconds(int targetFps)
    {
        if (targetFps <= HighCapPipelineBoostStartFps)
            return 1.0 / targetFps;

        // Unity EOF/coroutine resume adds ~0.1-0.2ms per slot; hurts shorter intervals more.
        // Tuned from 180-cap reading 176 and 144-cap reading 142 on Windows.
        double pipelineBoost = 1.0 + HighCapPipelineBoostPerFps * (targetFps - HighCapPipelineBoostStartFps);
        return 1.0 / (targetFps * pipelineBoost);
    }

    private void WaitUntil(double deadline)
    {
        double wait = deadline - s_clock.Elapsed.TotalSeconds;
        while (wait > WaitEpsilonSeconds)
        {
            if (s_targetFps >= SpinOnlyThresholdFps || wait <= SpinReserveSeconds + WaitEpsilonSeconds)
                _spinWait.SpinOnce();
            else
                Thread.Sleep(Mathf.Max(1, (int)((wait - SpinReserveSeconds) * 1000)));

            wait = deadline - s_clock.Elapsed.TotalSeconds;
        }
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
