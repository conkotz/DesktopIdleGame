using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// Coalesces multiple <see cref="AilmentController.OnAilmentsChanged"/> UI rebuilds into one pass per frame.
/// Flushes from a hidden runner's <see cref="LateUpdate"/> so icons stay in sync even when canvases do not repaint.
/// </summary>
[DisallowMultipleComponent]
public sealed class AilmentUiRebuildCoordinator : MonoBehaviour
{
    private static readonly HashSet<UnitOverheadUI> PendingOverheads = new();
    private static readonly HashSet<BuffsDebuffsPanel> PendingPanels = new();
    private static readonly HashSet<BuffsDebuffsPanel> PendingBuffPanels = new();
    private static AilmentUiRebuildCoordinator _runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PendingOverheads.Clear();
        PendingPanels.Clear();
        PendingBuffPanels.Clear();
        _runner = null;
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
    }

    /// <summary>Destroys legacy scene/runtime orphans from the old dynamic-spawn implementation.</summary>
    private void Awake()
    {
        if (_runner != null && _runner != this)
        {
            Destroy(gameObject);
            return;
        }

        _runner = this;
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(gameObject);
    }

    private void LateUpdate()
    {
        FlushPending();
    }

    public static void MarkUnitOverheadDirty(UnitOverheadUI overhead)
    {
        if (!overhead || !Application.isPlaying)
            return;

        PendingOverheads.Add(overhead);
        EnsureRunner();
    }

    public static void MarkBuffsPanelDirty(BuffsDebuffsPanel panel)
    {
        if (!panel || !Application.isPlaying)
            return;

        PendingPanels.Add(panel);
        EnsureRunner();
    }

    public static void MarkBuffsPanelDirtyForBuffs(BuffsDebuffsPanel panel)
    {
        if (!panel || !Application.isPlaying)
            return;

        PendingBuffPanels.Add(panel);
        EnsureRunner();
    }

    private static void EnsureRunner()
    {
        if (_runner != null)
            return;

        var go = new GameObject(nameof(AilmentUiRebuildCoordinator));
        _runner = go.AddComponent<AilmentUiRebuildCoordinator>();
    }

    private static void FlushPending()
    {
        if (PendingOverheads.Count == 0 && PendingPanels.Count == 0 && PendingBuffPanels.Count == 0)
            return;

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
