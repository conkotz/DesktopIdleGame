using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryGridUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemDatabase itemDb;
    [Tooltip("Rect that defines the available area for the grid (usually your SlotsGrid RectTransform).")]
    [SerializeField] private RectTransform slotsGrid;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private SharedTooltipUI tooltip;

    [Header("Tooltip Docking")]
    [SerializeField] private RectTransform tooltipAnchor;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    [Header("Drop/Delete Boundary (recommended: your whole inventory window panel RectTransform)")]
    [SerializeField] private RectTransform inventoryPanelRect;

    [Header("Hard Grid Size")]
    [SerializeField] private int columns = 8;
    [SerializeField] private int rows = 3;

    [Header("Layout Fit")]
    [SerializeField] private bool squareCells = true;
    [SerializeField] private float minCellSize = 32f;
    [Tooltip("How many frames to retry sizing/layout if rect size isn't ready yet (build safety).")]
    [SerializeField] private int layoutRetryFrames = 3;

    [Header("First-Open Snap Fix")]
    [Tooltip("Hide the grid visually until layout is stable (prevents 1-frame snapping).")]
    [SerializeField] private bool hideGridUntilReady = true;

    [Tooltip("Optional visual root to hide/show. Defaults to SlotsGrid GameObject.")]
    [SerializeField] private GameObject gridVisualRoot;

    [Tooltip("If true, hides via CanvasGroup alpha (keeps layout active). Recommended.")]
    [SerializeField] private bool hideViaCanvasGroup = true;

    [Tooltip("Optional CanvasGroup used for hiding. If empty and hideViaCanvasGroup is true, one will be created on gridVisualRoot.")]
    [SerializeField] private CanvasGroup gridCanvasGroup;
    private bool _warnedNoHideSupport;

    private readonly List<InventorySlotUI> _slotPool = new List<InventorySlotUI>(64);
    private GridLayoutGroup _grid;
    private bool _dirty;

    private Canvas _rootCanvas;

    private int TotalSlots => Mathf.Max(1, columns) * Mathf.Max(1, rows);

    private void Awake()
    {
        if (!inventory)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player) inventory = player.GetComponent<Inventory>();
        }

        if (slotsGrid) _grid = slotsGrid.GetComponent<GridLayoutGroup>();
        if (!_grid && slotsGrid) _grid = slotsGrid.GetComponent<GridLayoutGroup>();

        _rootCanvas = GetComponentInParent<Canvas>();

        if (!inventoryPanelRect)
            inventoryPanelRect = transform as RectTransform;

        if (!gridVisualRoot && slotsGrid)
            gridVisualRoot = slotsGrid.gameObject;

        if (hideGridUntilReady && hideViaCanvasGroup && gridVisualRoot)
        {
            if (!gridCanvasGroup)
                gridCanvasGroup = gridVisualRoot.GetComponent<CanvasGroup>();
            if (!gridCanvasGroup)
                gridCanvasGroup = gridVisualRoot.AddComponent<CanvasGroup>();
        }

        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        if (!tooltip)
            tooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.OnInventoryChanged += MarkDirty;

        if (hideGridUntilReady)
            SetGridVisible(false);

        StartCoroutine(DeferredRefresh());
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= MarkDirty;
    }

    private void MarkDirty() => _dirty = true;

    private void Update()
    {
        if (!_dirty) return;
        _dirty = false;
        Rebuild();
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyGridFit();
    }

    public void RefreshNow()
    {
        StopAllCoroutines();
        StartCoroutine(DeferredRefresh());
    }

    private IEnumerator DeferredRefresh()
    {
        EnsurePoolSize();

        for (int i = 0; i < Mathf.Max(1, layoutRetryFrames); i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (slotsGrid && slotsGrid.rect.width > 1f && slotsGrid.rect.height > 1f)
                break;
        }

        ApplyGridFit();
        Rebuild();

        // Extra layout settle pass before showing (prevents 1-frame wrong positions).
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (slotsGrid)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotsGrid);
        Canvas.ForceUpdateCanvases();

        if (hideGridUntilReady)
            SetGridVisible(true);
    }

    private void SetGridVisible(bool visible)
    {
        if (!gridVisualRoot) return;

        // Preferred: hide visually but keep layout active.
        if (hideViaCanvasGroup && gridCanvasGroup)
        {
            gridCanvasGroup.alpha = visible ? 1f : 0f;
            gridCanvasGroup.blocksRaycasts = visible;
            gridCanvasGroup.interactable = visible;
            return;
        }

        // Failsafe: do NOT deactivate the GameObject (can prevent coroutines / layout from running).
        // If no CanvasGroup is available, keep it visible rather than breaking the UI.
        if (!_warnedNoHideSupport)
        {
            _warnedNoHideSupport = true;
            Debug.LogWarning("[InventoryGridUI] hideGridUntilReady is enabled, but no CanvasGroup is available. " +
                             "Grid will remain visible to avoid disabling the object and breaking layout/coroutines.", this);
        }
    }

    private void EnsurePoolSize()
    {
        if (!slotsGrid || !slotPrefab) return;

        int needed = TotalSlots;

        if (inventory) inventory.EnsureSlotCount(needed);

        while (_slotPool.Count < needed)
        {
            var slot = Instantiate(slotPrefab, slotsGrid, false);
            _slotPool.Add(slot);
        }

        for (int i = 0; i < _slotPool.Count; i++)
        {
            if (_slotPool[i] != null)
                _slotPool[i].gameObject.SetActive(i < needed);
        }
    }

    private void ApplyGridFit()
    {
        if (!slotsGrid) return;
        if (!_grid) _grid = slotsGrid.GetComponent<GridLayoutGroup>();
        if (!_grid) return;

        float w = slotsGrid.rect.width;
        float h = slotsGrid.rect.height;
        if (w <= 1f || h <= 1f) return;

        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;

        float usableW = w - _grid.padding.left - _grid.padding.right - _grid.spacing.x * (columns - 1);
        float usableH = h - _grid.padding.top - _grid.padding.bottom - _grid.spacing.y * (rows - 1);

        float cellW = usableW / columns;
        float cellH = usableH / rows;

        float cell = cellW;
        cell = Mathf.Min(cell, cellH);

        cell = Mathf.Floor(cell);
        cell = Mathf.Max(minCellSize, cell);

        if (squareCells)
            _grid.cellSize = new Vector2(cell, cell);
        else
            _grid.cellSize = new Vector2(
                Mathf.Max(minCellSize, Mathf.Floor(cellW)),
                Mathf.Max(minCellSize, Mathf.Floor(cellH))
            );
    }

    public void Rebuild()
    {
        if (!inventory || !slotsGrid || !slotPrefab) return;
        EnsurePoolSize();

        int totalSlots = TotalSlots;
        inventory.EnsureSlotCount(totalSlots);

        for (int i = 0; i < totalSlots; i++)
        {
            var slotUI = _slotPool[i];
            if (!slotUI) continue;

            var s = inventory.GetSlot(i);

            if (!s.IsEmpty)
            {
                ItemDefinition def = inventory.GetItemDef(s.itemId);
                if (!def && itemDb) def = itemDb.Get(s.itemId);

                slotUI.Bind(def, s.amount, s.itemId, tooltip, inventory, i, inventoryPanelRect, _rootCanvas);
                slotUI.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
            }
            else
            {
                slotUI.Bind(null, 0, null, tooltip, inventory, i, inventoryPanelRect, _rootCanvas);
                slotUI.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
            }
        }
    }
}