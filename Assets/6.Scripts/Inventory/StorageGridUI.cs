using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds <see cref="PlayerStorage"/> to a fixed grid (default 7×4).
/// </summary>
public class StorageGridUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerStorage storage;
    [SerializeField] private ItemDatabase itemDb;
    [SerializeField] private RectTransform slotsGrid;
    [SerializeField] private StorageSlotUI slotPrefab;
    [SerializeField] private SharedTooltipUI tooltip;

    [Header("Tooltip Docking")]
    [SerializeField] private RectTransform tooltipAnchor;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    [SerializeField] private RectTransform storagePanelRect;

    [Header("Hard Grid Size")]
    [SerializeField] private int columns = 7;
    [SerializeField] private int rows = 4;

    [Header("Layout Fit")]
    [SerializeField] private bool squareCells = true;
    [SerializeField] private float minCellSize = 32f;
    [SerializeField] private int layoutRetryFrames = 3;

    private readonly List<StorageSlotUI> _slotPool = new List<StorageSlotUI>(64);
    private GridLayoutGroup _grid;
    private bool _dirty;
    private Canvas _rootCanvas;

    private int TotalSlots => Mathf.Max(1, columns) * Mathf.Max(1, rows);

    /// <summary>Same <see cref="PlayerStorage"/> bound to this grid (use instead of <c>FindFirstObjectByType</c>).</summary>
    public PlayerStorage PlayerStorage => storage;

    private void Awake()
    {
        ResolveStorageRef();

        if (slotsGrid) _grid = slotsGrid.GetComponent<GridLayoutGroup>();
        _rootCanvas = GetComponentInParent<Canvas>();

        if (!storagePanelRect)
            storagePanelRect = transform as RectTransform;

        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        if (!tooltip)
            tooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        ResolveStorageRef();

        if (storage != null)
            storage.OnStorageChanged += MarkDirty;

        StartCoroutine(DeferredRefresh());
    }

    private void OnDisable()
    {
        if (storage != null)
            storage.OnStorageChanged -= MarkDirty;
    }

    private void MarkDirty() => _dirty = true;

    public void RefreshNow()
    {
        StopAllCoroutines();
        StartCoroutine(DeferredRefresh());
    }

    /// <summary>
    /// Immediate bind pass after the panel becomes active (pairs with <see cref="RefreshNow"/> layout coroutine).
    /// Ensures we read from the same <see cref="PlayerStorage"/> as the player, not a stale inspector reference.
    /// </summary>
    public void SyncRefreshDisplay()
    {
        ResolveStorageRef();
        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        Canvas.ForceUpdateCanvases();
        EnsurePoolSize();
        ApplyGridFit();
        Rebuild();
    }

    private void ResolveStorageRef()
    {
        var pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc != null)
        {
            var ps = pc.GetComponent<PlayerStorage>();
            if (ps != null)
            {
                storage = ps;
                return;
            }
        }

        if (!storage)
            storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (!_dirty) return;
        _dirty = false;
        Rebuild();
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

        yield return null;
        Canvas.ForceUpdateCanvases();
        if (slotsGrid)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotsGrid);
    }

    private void EnsurePoolSize()
    {
        if (!slotsGrid || !slotPrefab) return;

        int needed = TotalSlots;
        if (storage) storage.EnsureSlotCount(needed);

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

        float cell = Mathf.Min(cellW, cellH);
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
        ResolveStorageRef();
        if (!storage || !slotsGrid || !slotPrefab) return;
        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        EnsurePoolSize();

        int totalSlots = TotalSlots;
        storage.EnsureSlotCount(totalSlots);

        for (int i = 0; i < totalSlots; i++)
        {
            var slotUI = _slotPool[i];
            if (!slotUI) continue;

            // GridLayoutGroup order follows transform sibling order; keep it aligned with storage slot index.
            if (slotsGrid && slotUI.transform.parent == slotsGrid)
                slotUI.transform.SetSiblingIndex(i);

            var s = storage.GetSlot(i);

            if (!s.IsEmpty)
            {
                ItemDefinition def = storage.GetItemDef(s.itemId);
                if (!def && itemDb) def = itemDb.Get(s.itemId);

                slotUI.Bind(def, s.amount, s.itemId, tooltip, storage, i, storagePanelRect, _rootCanvas);
                slotUI.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
            }
            else
            {
                slotUI.Bind(null, 0, null, tooltip, storage, i, storagePanelRect, _rootCanvas);
                slotUI.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
            }
        }
    }
}
