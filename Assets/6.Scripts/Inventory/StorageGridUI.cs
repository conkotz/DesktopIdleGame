using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Binds <see cref="PlayerStorage"/> to a fixed per-tab grid (default 8×11 = 88 visible slots).
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

    [Header("Tabs")]
    [SerializeField] private StorageTabBarUI tabBar;

    [Header("Hard Grid Size")]
    [SerializeField] private int columns = 8;
    [SerializeField] private int rows = 11;

    [Header("Layout Fit")]
    [SerializeField] private bool squareCells = true;
    [SerializeField] private float minCellSize = 32f;
    [SerializeField] private int layoutRetryFrames = 3;

    private const int PrewarmPoolBatchSize = 12;

    private readonly List<StorageSlotUI> _slotPool = new List<StorageSlotUI>(96);
    private GridLayoutGroup _grid;
    private bool _dirty;
    private bool _poolPrewarmed;
    private bool _displayPrewarmed;
    private bool _layoutSettled;
    private Canvas _rootCanvas;
    private StorageTabKind _activeTab = StorageTabKind.Main;
    private Coroutine _displayRefreshCo;

    private int TotalSlots
    {
        get
        {
            if (storage != null)
                return Mathf.Max(1, storage.GetSlotsForTab(_activeTab));
            return Mathf.Max(1, columns) * Mathf.Max(1, rows);
        }
    }

    public PlayerStorage PlayerStorage => storage;
    public StorageTabKind ActiveTab => _activeTab;

    public bool IsPointerOverSlotGrid(PointerEventData eventData)
    {
        if (!slotsGrid || eventData == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            slotsGrid,
            eventData.position,
            eventData.pressEventCamera);
    }

    private CanvasGroup _dragPassThroughGroup;
    private bool _dragPassThroughActive;

    /// <summary>Lets pointer events reach UI behind the item grid (inventory) while keeping the storage window on top.</summary>
    public void SetDragPassThrough(bool enabled)
    {
        if (enabled == _dragPassThroughActive)
            return;

        _dragPassThroughActive = enabled;
        if (_dragPassThroughGroup == null)
        {
            Transform target = slotsGrid != null ? slotsGrid : transform;
            _dragPassThroughGroup = target.GetComponent<CanvasGroup>();
            if (_dragPassThroughGroup == null)
                _dragPassThroughGroup = target.gameObject.AddComponent<CanvasGroup>();
        }

        _dragPassThroughGroup.blocksRaycasts = !enabled;
    }

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

        EnsureTabBar();
    }

    private void OnEnable()
    {
        ResolveStorageRef();
        EnsureTabBar();

        if (storage != null)
            storage.OnStorageChanged += MarkDirty;

        if (tabBar != null)
        {
            tabBar.OnTabSelected += HandleTabSelected;
            _activeTab = tabBar.ActiveTab;
        }

        if (MainMenuUIPrewarm.UseBatchedInstantiation)
            return;

        if (TryShowPrewarmedWithoutRebuild())
            return;

        StartCoroutine(DeferredRefresh());
    }

    private IEnumerator CoReflowAfterPrewarmedOpen()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        ApplyGridFit();
        RefreshDisplayInstant();
    }

    public bool IsDisplayPrewarmed => _displayPrewarmed;

    public bool HasPendingRefresh => _dirty;

    public IEnumerator CoPrewarmPool()
    {
        ResolveStorageRef();
        EnsureTabBar();

        if (_displayPrewarmed && _poolPrewarmed)
            yield break;

        if (storage)
            storage.EnsureSlotCount(storage.ComputeTotalSlotCount());

        StorageTabKind[] tabs = (StorageTabKind[])System.Enum.GetValues(typeof(StorageTabKind));
        int maxVisibleSlots = 0;
        for (int t = 0; t < tabs.Length; t++)
        {
            _activeTab = tabs[t];
            maxVisibleSlots = Mathf.Max(maxVisibleSlots, TotalSlots);
        }

        yield return CoEnsurePoolSize(maxVisibleSlots, MainMenuUIPrewarm.UseBatchedInstantiation);

        for (int i = 0; i < Mathf.Max(1, layoutRetryFrames); i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (slotsGrid && slotsGrid.rect.width > 1f && slotsGrid.rect.height > 1f)
                break;
        }

        for (int t = 0; t < tabs.Length; t++)
        {
            _activeTab = tabs[t];
            ApplyGridFit();
            Rebuild();
            yield return null;
        }

        if (tabBar != null)
            _activeTab = tabBar.ActiveTab;
        else
            _activeTab = StorageTabKind.Main;

        _poolPrewarmed = _slotPool.Count >= maxVisibleSlots;
        _layoutSettled = slotsGrid && slotsGrid.rect.width > 1f && slotsGrid.rect.height > 1f;
        _displayPrewarmed = true;
        _dirty = false;
    }

    private bool TryShowPrewarmedWithoutRebuild()
    {
        if (!_displayPrewarmed || !_poolPrewarmed)
            return false;

        if (_slotPool.Count < TotalSlots)
            return false;

        StartCoroutine(CoReflowAfterPrewarmedOpen());
        return true;
    }

    private void OnDisable()
    {
        if (storage != null)
            storage.OnStorageChanged -= MarkDirty;

        if (tabBar != null)
            tabBar.OnTabSelected -= HandleTabSelected;

        if (_dirty && storage != null)
        {
            _dirty = false;
            Rebuild();
        }
    }

    private void EnsureTabBar()
    {
        if (!tabBar)
            tabBar = GetComponentInParent<StorageUI>(true)?.GetComponentInChildren<StorageTabBarUI>(true);
        if (!tabBar)
            tabBar = StorageTabBarUI.EnsureOnStorageWindow(this);
    }

    private void HandleTabSelected(StorageTabKind tab)
    {
        _activeTab = tab;
        ScheduleTabRefresh();

        ScrollRect scroll = slotsGrid != null ? slotsGrid.GetComponentInParent<ScrollRect>(true) : null;
        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f;
    }

    private void MarkDirty()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _dirty = true;
            return;
        }

        RefreshDisplayInstant();
    }

    private void RefreshDisplayInstant()
    {
        if (_displayRefreshCo != null)
        {
            StopCoroutine(_displayRefreshCo);
            _displayRefreshCo = null;
        }

        _dirty = false;
        if (!TryRefreshChangedSlotsOnly())
            Rebuild();
    }

    private bool TryRefreshChangedSlotsOnly()
    {
        if (!storage || !storage.TryConsumeUiRefreshHint(out IReadOnlyList<int> dirtySlots, out bool fullRefresh))
            return false;

        if (fullRefresh || dirtySlots == null || dirtySlots.Count == 0)
            return false;

        if (!PrepareRebuildContext(out int totalSlots, out int globalOffset))
            return true;

        int tabStart = globalOffset;
        int tabEnd = tabStart + totalSlots;

        for (int d = 0; d < dirtySlots.Count; d++)
        {
            int globalIndex = dirtySlots[d];
            if (globalIndex < tabStart || globalIndex >= tabEnd)
                continue;

            BindSlotAtIndex(globalIndex - globalOffset, globalOffset);
        }

        return true;
    }

    private void ScheduleTabRefresh()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _dirty = true;
            return;
        }

        if (_displayRefreshCo != null)
            StopCoroutine(_displayRefreshCo);

        _dirty = false;
        _displayRefreshCo = StartCoroutine(CoTimeSlicedRebuild());
    }

    public void RefreshNow()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _dirty = true;
            return;
        }

        StopAllCoroutines();
        _displayRefreshCo = null;
        StartCoroutine(DeferredRefresh());
    }

    /// <summary>Opens the leftmost storage tab (respects saved tab order).</summary>
    public void SelectFirstDisplayedTab()
    {
        EnsureTabBar();
        if (tabBar != null)
        {
            tabBar.SelectFirstDisplayedTab(notify: false);
            _activeTab = tabBar.ActiveTab;
        }
    }

    public void SyncRefreshDisplay()
    {
        ResolveStorageRef();
        EnsureTabBar();
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

    private IEnumerator CoTimeSlicedRebuild()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        ApplyGridFit();

        if (!PrepareRebuildContext(out int totalSlots, out int globalOffset))
        {
            _displayRefreshCo = null;
            yield break;
        }

        for (int i = 0; i < totalSlots; i++)
        {
            BindSlotAtIndex(i, globalOffset);
            if (i < totalSlots - 1 && (i + 1) % PrewarmPoolBatchSize == 0)
                yield return null;
        }

        _displayRefreshCo = null;
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
        if (slotsGrid && !_layoutSettled)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotsGrid);
        _layoutSettled = slotsGrid && slotsGrid.rect.width > 1f && slotsGrid.rect.height > 1f;
    }

    private IEnumerator CoEnsurePoolSize(int count, bool batched)
    {
        if (!batched)
        {
            EnsurePoolSize();
            yield break;
        }

        while (_slotPool.Count < count)
        {
            int batchEnd = Mathf.Min(_slotPool.Count + PrewarmPoolBatchSize, count);
            for (int i = _slotPool.Count; i < batchEnd; i++)
            {
                StorageSlotUI created = Instantiate(slotPrefab, slotsGrid, false);
                _slotPool.Add(created);
            }

            for (int i = 0; i < _slotPool.Count; i++)
            {
                if (_slotPool[i] != null)
                    _slotPool[i].gameObject.SetActive(i < count);
            }

            yield return null;
        }
    }

    private void EnsurePoolSize()
    {
        if (!slotsGrid || !slotPrefab) return;

        int needed = TotalSlots;
        if (storage)
            storage.EnsureSlotCount(storage.ComputeTotalSlotCount());

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
        if (w <= 1f) return;

        ScrollRect scroll = slotsGrid.GetComponentInParent<ScrollRect>(true);
        if (h <= 1f && scroll == null) return;

        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;

        float usableW = w - _grid.padding.left - _grid.padding.right - _grid.spacing.x * (columns - 1);

        if (squareCells && scroll != null)
        {
            float edge = Mathf.Floor(usableW / columns);
            edge = Mathf.Max(minCellSize, edge);
            _grid.cellSize = new Vector2(edge, edge);
            return;
        }

        float usableH = h - _grid.padding.top - _grid.padding.bottom - _grid.spacing.y * (rows - 1);
        float cellW = usableW / columns;
        float cellH = usableH / rows;

        float cellSide = Mathf.Min(cellW, cellH);
        cellSide = Mathf.Floor(cellSide);
        cellSide = Mathf.Max(minCellSize, cellSide);

        if (squareCells)
            _grid.cellSize = new Vector2(cellSide, cellSide);
        else
            _grid.cellSize = new Vector2(
                Mathf.Max(minCellSize, Mathf.Floor(cellW)),
                Mathf.Max(minCellSize, Mathf.Floor(cellH))
            );
    }

    public void Rebuild()
    {
        if (!PrepareRebuildContext(out int totalSlots, out int globalOffset))
            return;

        for (int i = 0; i < totalSlots; i++)
            BindSlotAtIndex(i, globalOffset);
    }

    private bool PrepareRebuildContext(out int totalSlots, out int globalOffset)
    {
        totalSlots = 0;
        globalOffset = 0;

        ResolveStorageRef();
        EnsureTabBar();
        if (!storage || !slotsGrid || !slotPrefab)
            return false;

        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        if (tabBar != null)
            _activeTab = tabBar.ActiveTab;

        EnsurePoolSize();

        totalSlots = TotalSlots;
        storage.EnsureSlotCount(storage.ComputeTotalSlotCount());
        globalOffset = storage.GetTabStartIndex(_activeTab);
        return true;
    }

    private void BindSlotAtIndex(int i, int globalOffset)
    {
        if (i < 0 || i >= _slotPool.Count)
            return;

        var slotUI = _slotPool[i];
        if (!slotUI)
            return;

        int globalIndex = globalOffset + i;

        if (slotsGrid && slotUI.transform.parent == slotsGrid)
            slotUI.transform.SetSiblingIndex(i);

        var s = storage.GetSlot(globalIndex);

        if (!s.IsEmpty)
        {
            ItemDefinition def = storage.GetItemDef(s.itemId);
            if (!def && itemDb)
                def = itemDb.Get(s.itemId);

            slotUI.Bind(def, s.amount, s.itemId, tooltip, storage, globalIndex, storagePanelRect, _rootCanvas);
            slotUI.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
        }
        else
        {
            slotUI.Bind(null, 0, null, tooltip, storage, globalIndex, storagePanelRect, _rootCanvas);
            slotUI.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
        }
    }
}
