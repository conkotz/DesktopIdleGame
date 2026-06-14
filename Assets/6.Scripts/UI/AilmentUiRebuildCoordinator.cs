using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Coalesces multiple <see cref="AilmentController.OnAilmentsChanged"/> UI rebuilds into one pass per frame.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class AilmentUiRebuildCoordinator : MonoBehaviour
{
    private static AilmentUiRebuildCoordinator _instance;

    private readonly HashSet<UnitOverheadUI> _pendingOverheads = new();
    private readonly HashSet<BuffsDebuffsPanel> _pendingPanels = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    public static void MarkUnitOverheadDirty(UnitOverheadUI overhead)
    {
        if (!overhead)
            return;

        EnsureInstance();
        _instance._pendingOverheads.Add(overhead);
    }

    public static void MarkBuffsPanelDirty(BuffsDebuffsPanel panel)
    {
        if (!panel)
            return;

        EnsureInstance();
        _instance._pendingPanels.Add(panel);
    }

    private static void EnsureInstance()
    {
        if (_instance)
            return;

        var go = new GameObject(nameof(AilmentUiRebuildCoordinator));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AilmentUiRebuildCoordinator>();
    }

    private void LateUpdate()
    {
        if (_pendingOverheads.Count == 0 && _pendingPanels.Count == 0)
            return;

        Profiler.BeginSample("AilmentUI.RebuildCoalesced");
        try
        {
            foreach (UnitOverheadUI overhead in _pendingOverheads)
            {
                if (overhead)
                    overhead.FlushCoalescedAilmentUiRebuild();
            }

            _pendingOverheads.Clear();

            foreach (BuffsDebuffsPanel panel in _pendingPanels)
            {
                if (panel)
                    panel.FlushCoalescedAilmentUiRebuild();
            }

            _pendingPanels.Clear();
        }
        finally
        {
            Profiler.EndSample();
        }
    }
}
