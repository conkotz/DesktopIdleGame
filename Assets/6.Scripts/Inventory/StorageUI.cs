using UnityEngine;

/// <summary>
/// Town chest storage window (grid). <see cref="StorageClick"/> opens this; <see cref="InventorySlotUI"/> checks <see cref="IsOpen"/> for double-click deposit.
/// </summary>
public class StorageUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private StorageGridUI grid;

    private static StorageUI _instance;

    /// <summary>
    /// True when the storage panel is actually shown in the hierarchy (not just a stale flag).
    /// Parent pages or the main menu can hide this without calling <see cref="Close"/>; <c>activeInHierarchy</c> stays in sync.
    /// </summary>
    public static bool IsOpen
    {
        get
        {
            var ui = _instance != null ? _instance : FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);
            if (ui == null) return false;
            return ui.panelRoot != null && ui.panelRoot.activeInHierarchy;
        }
    }

    /// <summary>Open() moves this panel to the top sibling, which blocks drops onto the inventory underneath.
    /// While dragging <i>from</i> storage, we temporarily send the panel to the back so inventory slots receive the drop.</summary>
    private int _savedSiblingIndexForDrag = -1;

    private void Awake()
    {
        _instance = this;

        if (panelRoot)
            panelRoot.SetActive(false);

        if (!grid)
            grid = GetComponentInChildren<StorageGridUI>(true);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public void Open()
    {
        if (panelRoot)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        grid?.RefreshNow();
        // Bind immediately from the player's PlayerStorage; inspector reference can point at a wrong instance.
        grid?.SyncRefreshDisplay();
    }

    public void Close()
    {
        if (panelRoot)
            panelRoot.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void BeginDragFromStoragePanel()
    {
        Transform t = panelRoot ? panelRoot.transform : transform;
        if (_savedSiblingIndexForDrag >= 0) return;

        _savedSiblingIndexForDrag = t.GetSiblingIndex();
        t.SetAsFirstSibling();
    }

    public void EndDragFromStoragePanel()
    {
        if (_savedSiblingIndexForDrag < 0) return;

        Transform t = panelRoot ? panelRoot.transform : transform;
        int idx = _savedSiblingIndexForDrag;
        _savedSiblingIndexForDrag = -1;

        var parent = t.parent;
        if (parent == null) return;

        idx = Mathf.Clamp(idx, 0, parent.childCount - 1);
        t.SetSiblingIndex(idx);
    }
}
