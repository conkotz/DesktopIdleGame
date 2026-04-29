using UnityEngine;

public class SaleUndoUIBinder : MonoBehaviour
{
    [Header("UI Refs (in this scene)")]
    [SerializeField] private GameObject undoPanel;        // usually this GameObject
    [SerializeField] private RectTransform rowsRoot;      // RowRoot RectTransform
    [SerializeField] private SaleUndoRowUI undoRowPrefab; // inactive template under RowRoot

    [Header("Shop dock (FullWindow / WindowsArea)")]
    [Tooltip("Shop window root (e.g. Shop GameObject). Undo panel reparents here while the shop is open.")]
    [SerializeField] private RectTransform shopWindowRoot;
    [Tooltip("Area to stay inside vertically (e.g. WindowsArea under FullWindowCanvas). If unset, ShopUI.PanelRectTransform is used.")]
    [SerializeField] private RectTransform verticalClampBounds;

    private void Awake()
    {
        if (!undoPanel) undoPanel = gameObject;

        // Ensure manager exists
        var mgr = SaleUndoManager.Instance != null
            ? SaleUndoManager.Instance
            : FindFirstObjectByType<SaleUndoManager>(FindObjectsInactive.Include);

        if (mgr != null)
            mgr.BindUI(undoPanel, rowsRoot, undoRowPrefab, shopWindowRoot, verticalClampBounds);
    }
}