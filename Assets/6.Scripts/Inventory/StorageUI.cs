using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

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

    /// <summary>While dragging from storage, the item grid stops blocking raycasts so inventory slots underneath can receive drops.</summary>
    private bool _dragPassThroughActive;

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

    public bool IsGridPrewarmedForLoad() => grid != null && grid.IsDisplayPrewarmed;

    public IEnumerator CoPrewarmForLoad()
    {
        if (!grid)
            yield break;

        bool wasOpen = IsOpen;
        if (panelRoot)
            panelRoot.SetActive(true);

        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();

        MainMenuUIPrewarm.UseBatchedInstantiation = true;
        try
        {
            yield return grid.CoPrewarmPool();
        }
        finally
        {
            MainMenuUIPrewarm.UseBatchedInstantiation = false;
        }

        if (!wasOpen && panelRoot)
            panelRoot.SetActive(false);
    }

    public void Open()
    {
        if (grid != null)
            grid.SelectFirstDisplayedTab();

        if (panelRoot)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        if (grid != null && !grid.IsDisplayPrewarmed)
            grid.RefreshNow();
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
        if (_dragPassThroughActive)
            return;

        _dragPassThroughActive = true;
        grid?.SetDragPassThrough(false);
    }

    /// <summary>While dragging from storage, only pass raycasts through the grid when the pointer leaves it (inventory drops).</summary>
    public void UpdateStorageDragPassThrough(PointerEventData eventData)
    {
        if (!_dragPassThroughActive || grid == null || eventData == null)
            return;

        bool overSlotGrid = grid.IsPointerOverSlotGrid(eventData);
        grid.SetDragPassThrough(!overSlotGrid);
    }

    public void EndDragFromStoragePanel()
    {
        if (!_dragPassThroughActive)
            return;

        _dragPassThroughActive = false;
        grid?.SetDragPassThrough(false);
    }
}
