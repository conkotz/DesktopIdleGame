using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// Coalesces inventory grid / gold label refreshes into one pass per frame (before canvas render).
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryUiRefreshCoordinator : MonoBehaviour
{
    private static readonly HashSet<InventoryGridUI> PendingGrids = new();
    private static readonly HashSet<UpgradeInventoryGridUI> PendingUpgradeGrids = new();
    private static readonly HashSet<InventoryTotalValueUI> PendingValueLabels = new();
    private static readonly HashSet<GoldUI> PendingGoldLabels = new();
    private static bool _hookSubscribed;
    private static int _lastFlushFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        UnsubscribeHook();
        PendingGrids.Clear();
        PendingUpgradeGrids.Clear();
        PendingValueLabels.Clear();
        PendingGoldLabels.Clear();
        _lastFlushFrame = -1;
    }

    static InventoryUiRefreshCoordinator()
    {
        SceneManager.sceneUnloaded += _ => ResetStatics();
    }

    private void Awake() => Destroy(gameObject);

    public static void MarkGridDirty(InventoryGridUI grid)
    {
        if (!grid || !Application.isPlaying)
            return;

        PendingGrids.Add(grid);
        EnsureHook();
    }

    public static void MarkUpgradeGridDirty(UpgradeInventoryGridUI grid)
    {
        if (!grid || !Application.isPlaying)
            return;

        PendingUpgradeGrids.Add(grid);
        EnsureHook();
    }

    public static void MarkValueLabelDirty(InventoryTotalValueUI label)
    {
        if (!label || !Application.isPlaying)
            return;

        PendingValueLabels.Add(label);
        EnsureHook();
    }

    public static void MarkGoldLabelDirty(GoldUI label)
    {
        if (!label || !Application.isPlaying)
            return;

        PendingGoldLabels.Add(label);
        EnsureHook();
    }

    /// <summary>Immediately rebuilds any grids marked dirty this frame (e.g. after storage/inventory transfer).</summary>
    public static void FlushNow()
    {
        FlushPendingInternal();
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
        if (PendingGrids.Count == 0 &&
            PendingUpgradeGrids.Count == 0 &&
            PendingValueLabels.Count == 0 &&
            PendingGoldLabels.Count == 0)
            return;

        int frame = Time.frameCount;
        if (frame == _lastFlushFrame)
            return;

        _lastFlushFrame = frame;
        FlushPendingInternal();
    }

    private static void FlushPendingInternal()
    {
        if (PendingGrids.Count == 0 &&
            PendingUpgradeGrids.Count == 0 &&
            PendingValueLabels.Count == 0 &&
            PendingGoldLabels.Count == 0)
            return;

        Profiler.BeginSample("InventoryUI.RefreshCoalesced");
        try
        {
            foreach (InventoryGridUI grid in PendingGrids)
            {
                if (grid)
                    grid.FlushCoalescedRebuild();
            }

            PendingGrids.Clear();

            foreach (UpgradeInventoryGridUI grid in PendingUpgradeGrids)
            {
                if (grid)
                    grid.FlushCoalescedRebuild();
            }

            PendingUpgradeGrids.Clear();

            foreach (InventoryTotalValueUI label in PendingValueLabels)
            {
                if (label)
                    label.FlushCoalescedRefresh();
            }

            PendingValueLabels.Clear();

            foreach (GoldUI label in PendingGoldLabels)
            {
                if (label)
                    label.FlushCoalescedRefresh();
            }

            PendingGoldLabels.Clear();
        }
        finally
        {
            Profiler.EndSample();
        }
    }
}
