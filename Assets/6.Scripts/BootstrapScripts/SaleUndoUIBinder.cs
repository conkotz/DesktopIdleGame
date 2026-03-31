using UnityEngine;

public class SaleUndoUIBinder : MonoBehaviour
{
    [Header("UI Refs (in this scene)")]
    [SerializeField] private GameObject undoPanel;        // usually this GameObject
    [SerializeField] private RectTransform rowsRoot;      // RowRoot RectTransform
    [SerializeField] private SaleUndoRowUI undoRowPrefab; // inactive template under RowRoot

    private void Awake()
    {
        if (!undoPanel) undoPanel = gameObject;

        // Ensure manager exists
        var mgr = SaleUndoManager.Instance != null
            ? SaleUndoManager.Instance
            : FindFirstObjectByType<SaleUndoManager>(FindObjectsInactive.Include);

        if (mgr != null)
            mgr.BindUI(undoPanel, rowsRoot, undoRowPrefab);
    }
}