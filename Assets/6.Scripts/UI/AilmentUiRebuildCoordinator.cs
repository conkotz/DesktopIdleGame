using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// Coalesces multiple <see cref="AilmentController.OnAilmentsChanged"/> UI rebuilds into one pass per frame.
/// Static API only — uses <see cref="Canvas.willRenderCanvases"/> (no runtime GameObject spawn).
/// Any legacy scene/runtime object with this component is destroyed in <see cref="Awake"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class AilmentUiRebuildCoordinator : MonoBehaviour
{
    private static readonly HashSet<UnitOverheadUI> PendingOverheads = new();
    private static readonly HashSet<BuffsDebuffsPanel> PendingPanels = new();
    private static readonly HashSet<BuffsDebuffsPanel> PendingBuffPanels = new();
    private static bool _hookSubscribed;
    private static int _lastFlushFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        UnsubscribeHook();
        PendingOverheads.Clear();
        PendingPanels.Clear();
        PendingBuffPanels.Clear();
        _lastFlushFrame = -1;
    }

    static AilmentUiRebuildCoordinator()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        PendingOverheads.Clear();
        PendingPanels.Clear();
        PendingBuffPanels.Clear();
        _lastFlushFrame = -1;
    }

    /// <summary>
    /// Destroys legacy scene/runtime orphans from the old dynamic-spawn implementation.
    /// </summary>
    private void Awake()
    {
        Destroy(gameObject);
    }

    public static void MarkUnitOverheadDirty(UnitOverheadUI overhead)
    {
        if (!overhead || !Application.isPlaying)
            return;

        PendingOverheads.Add(overhead);
        EnsureHook();
    }

    public static void MarkBuffsPanelDirty(BuffsDebuffsPanel panel)
    {
        if (!panel || !Application.isPlaying)
            return;

        PendingPanels.Add(panel);
        EnsureHook();
    }

    public static void MarkBuffsPanelDirtyForBuffs(BuffsDebuffsPanel panel)
    {
        if (!panel || !Application.isPlaying)
            return;

        PendingBuffPanels.Add(panel);
        EnsureHook();
    }

    private static void EnsureHook()
    {
        if (_hookSubscribed)
            return;

        _hookSubscribed = true;
        Canvas.willRenderCanvases += OnWillRenderCanvasesFlush;
    }

    private static void UnsubscribeHook()
    {
        if (!_hookSubscribed)
            return;

        Canvas.willRenderCanvases -= OnWillRenderCanvasesFlush;
        _hookSubscribed = false;
    }

    private static void OnWillRenderCanvasesFlush()
    {
        if (PendingOverheads.Count == 0 && PendingPanels.Count == 0 && PendingBuffPanels.Count == 0)
            return;

        int frame = Time.frameCount;
        if (frame == _lastFlushFrame)
            return;

        _lastFlushFrame = frame;

        Profiler.BeginSample("AilmentUI.RebuildCoalesced");
        try
        {
            foreach (UnitOverheadUI overhead in PendingOverheads)
            {
                if (overhead)
                    overhead.FlushCoalescedAilmentUiRebuild();
            }

            PendingOverheads.Clear();

            foreach (BuffsDebuffsPanel panel in PendingPanels)
            {
                if (panel)
                    panel.FlushCoalescedAilmentUiRebuild();
            }

            PendingPanels.Clear();

            foreach (BuffsDebuffsPanel panel in PendingBuffPanels)
            {
                if (panel)
                    panel.FlushCoalescedBuffUiRebuild();
            }

            PendingBuffPanels.Clear();
        }
        finally
        {
            Profiler.EndSample();
        }
    }
}
