using UnityEngine;

/// <summary>
/// Dev-only holder for item references. <see cref="DevTestingPanelUI"/> copies these via <see cref="ExportDevItemRefsForTestingPanel"/> on Awake.
/// Keyboard shortcuts live on <see cref="BugAndSuggestionReportUI"/> (F1–F7 → <see cref="DevTestingPanelUI"/>).
/// </summary>
public class DebugGiveItems : MonoBehaviour
{
    [Header("Item refs (imported by DevTestingPanelUI)")]
    [SerializeField] private ItemDefinition fishDef;
    [SerializeField] private ItemDefinition logsDef;
    [SerializeField] private ItemDefinition stoneChunkDef;
    [SerializeField] private ItemDefinition devDestroyerMaceDef;

    /// <summary>Lets <see cref="DevTestingPanelUI"/> reuse the same serialized item refs from the Player (or any scene object).</summary>
    public void ExportDevItemRefsForTestingPanel(
        out ItemDefinition fish,
        out ItemDefinition logs,
        out ItemDefinition stone,
        out ItemDefinition mace)
    {
        fish = fishDef;
        logs = logsDef;
        stone = stoneChunkDef;
        mace = devDestroyerMaceDef;
    }
}
