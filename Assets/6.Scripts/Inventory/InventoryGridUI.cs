using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class InventoryGridUI : MonoBehaviour
{
    private enum InventoryViewFilter
    {
        All,
        Resources,
        Equips,
        Consumables,
        Enhance
    }

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemDatabase itemDb;
    [Tooltip("Rect that defines the available area for the grid (usually your SlotsGrid RectTransform).")]
    [SerializeField] private RectTransform slotsGrid;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private ScrollRect inventoryScrollRect;
    [SerializeField] private RectTransform scrollContentRoot;

    [Header("Tooltip Docking")]
    [SerializeField] private RectTransform tooltipAnchor;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    [Header("Drop/Delete Boundary (recommended: your whole inventory window panel RectTransform)")]
    [SerializeField] private RectTransform inventoryPanelRect;

    [Header("Hard Grid Size")]
    [SerializeField] private int columns = 8;
    [SerializeField] private int minVisibleRows = 3;

    [Header("Layout Fit")]
    [Tooltip("When true, runtime auto-fit will not overwrite GridLayoutGroup cell/constraint/axis settings. Use inspector values directly.")]
    [SerializeField] private bool useManualGridLayoutSettings = false;
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

    [Header("Category Filters")]
    [SerializeField] private Button allFilterButton;
    [SerializeField] private Button resourceFilterButton;
    [SerializeField] private Button equipsFilterButton;
    [Tooltip("Enhancement items filter (scrolls + map enhancements).")]
    [FormerlySerializedAs("jewelryFilterButton")]
    [SerializeField] private Button enhanceFilterButton;
    [SerializeField] private Button consumablesFilterButton;

    private InventoryViewFilter _activeFilter = InventoryViewFilter.All;

    /// <summary>Fired whenever the active category filter changes. Listeners can recompute filter-aware UI (e.g. total value).</summary>
    public event Action OnFilterChanged;

    private const int PrewarmPoolBatchSize = 12;

    private readonly List<InventorySlotUI> _slotPool = new List<InventorySlotUI>(64);
    private GridLayoutGroup _grid;
    private bool _dirty;
    private bool _poolPrewarmed;
    private bool _layoutSettled;
    private bool _displayPrewarmed;
    /// <summary>Coalesce <see cref="OnInventoryChanged"/> into one <see cref="Rebuild"/> per frame (same frame as the change, after Update).</summary>
    private readonly List<int> _filteredSourceSlotScratch = new List<int>(128);

    /// <summary>Skip expensive <see cref="ApplyGridFit"/> when viewport + grid settings are unchanged since last rebuild.</summary>
    private int _lastLayoutFitSignature = int.MinValue;

    /// <summary>Skip per-slot <see cref="InventorySlotUI.Bind"/> when mapped inventory data for that grid cell is unchanged.</summary>
    private int _lastInventorySlotCountForRebindCache = -1;
    private int[] _rebindCacheSrcIdx;
    private int[] _rebindCacheAmt;
    private string[] _rebindCacheItemId;
    private int[] _rebindCacheDefId;
    private bool[] _rebindCacheIdentifyPending;

    private Canvas _rootCanvas;
    private RectTransform _resolvedViewport;
    private RectTransform _resolvedContent;
    private Inventory _subscribedInventory;

    private int TotalSlots => GetTargetSlotCount();

    private void Awake()
    {
        TryResolveInventory();

        if (slotsGrid) _grid = slotsGrid.GetComponent<GridLayoutGroup>();
        if (!_grid && slotsGrid) _grid = slotsGrid.GetComponent<GridLayoutGroup>();

        _rootCanvas = GetComponentInParent<Canvas>();

        if (!inventoryScrollRect && slotsGrid)
            inventoryScrollRect = slotsGrid.GetComponentInParent<ScrollRect>(true);

        EnsureScrollViewportClipping();

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
        EnsureScrollViewportClipping();
        TryResolveInventory();

        if (_subscribedInventory != null)
        {
            _subscribedInventory.OnInventoryChanged -= MarkDirty;
            _subscribedInventory.OnInventoryChanged += MarkDirty;
        }

        int totalSlots = TotalSlots;
        if (_poolPrewarmed && _slotPool.Count < totalSlots)
            _poolPrewarmed = false;

        BindFilterButtons();
        SetFilter(InventoryViewFilter.All, rebuildNow: false);

        if (TryShowPrewarmedWithoutRebuild())
            return;

        if (hideGridUntilReady)
            SetGridVisible(false);

        StartCoroutine(DeferredRefresh());
    }

    private void TryResolveInventory()
    {
        Inventory resolved = Inventory.ResolvePlayer();
        if (!resolved)
            return;

        if (inventory == resolved)
            return;

        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryChanged -= MarkDirty;

        inventory = resolved;
        _subscribedInventory = resolved;
        _displayPrewarmed = false;
        _dirty = true;
        ClearSlotRebindCache();
    }

    /// <summary>
    /// After load-time prewarm, pool/layout are warm — re-bind live inventory data on every open.
    /// </summary>
    private bool TryShowPrewarmedWithoutRebuild()
    {
        if (!_displayPrewarmed || !_poolPrewarmed || _dirty)
            return false;

        int totalSlots = TotalSlots;
        if (_slotPool.Count < totalSlots)
            return false;

        if (hideGridUntilReady)
            SetGridVisible(true);

        StartCoroutine(CoReflowAfterPrewarmedOpen());
        return true;
    }

    private IEnumerator CoReflowAfterPrewarmedOpen()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        int totalSlots = TotalSlots;
        int sig = ComputeInventoryGridLayoutSignature(totalSlots);
        if (sig != _lastLayoutFitSignature)
        {
            ApplyGridFit();
            _lastLayoutFitSignature = sig;
        }

        Rebuild();
        _dirty = false;
    }

    private void OnDisable()
    {
        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryChanged -= MarkDirty;

        UnbindFilterButtons();

        if (_dirty && inventory != null)
        {
            _dirty = false;
            Rebuild();
        }
    }

    private void MarkDirty()
    {
        _dirty = true;
        _displayPrewarmed = false;
        InventoryUiRefreshCoordinator.MarkGridDirty(this);
    }

    internal void FlushCoalescedRebuild()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || !_dirty || inventory == null)
            return;

        _dirty = false;
        Rebuild();
    }

    public static void RefreshAllGrids()
    {
        InventoryGridUI[] grids = FindObjectsByType<InventoryGridUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < grids.Length; i++)
        {
            InventoryGridUI grid = grids[i];
            if (grid)
                grid.RefreshNow();
        }

        UpgradeInventoryGridUI[] upgradeGrids = FindObjectsByType<UpgradeInventoryGridUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < upgradeGrids.Length; i++)
        {
            UpgradeInventoryGridUI grid = upgradeGrids[i];
            if (grid)
                grid.RefreshNow();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_displayPrewarmed && !_dirty)
            return;

        _lastLayoutFitSignature = int.MinValue;
        ApplyGridFit();
    }

    public void RefreshNow()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _dirty = true;
            return;
        }

        StopAllCoroutines();
        StartCoroutine(DeferredRefresh());
    }

    /// <summary>Shares serialized grid bindings with <see cref="UpgradeInventoryGridUI"/> on the same GameObject.</summary>
    public bool TryGetUpgradeGridBindings(
        out Inventory boundInventory,
        out ItemDatabase boundItemDb,
        out RectTransform boundSlotsGrid,
        out InventorySlotUI boundSlotPrefab,
        out SharedTooltipUI boundTooltip,
        out RectTransform boundPanelRect,
        out RectTransform boundTooltipAnchor,
        out RectTransform boundTooltipHeightRect,
        out int boundColumns,
        out int boundMinVisibleRows)
    {
        boundInventory = inventory;
        boundItemDb = itemDb;
        boundSlotsGrid = slotsGrid;
        boundSlotPrefab = slotPrefab;
        boundTooltip = tooltip;
        boundPanelRect = inventoryPanelRect;
        boundTooltipAnchor = tooltipAnchor;
        boundTooltipHeightRect = tooltipHeightRect;
        boundColumns = columns;
        boundMinVisibleRows = minVisibleRows;
        return boundSlotsGrid != null && boundSlotPrefab != null;
    }

    public bool IsPoolPrewarmed => _poolPrewarmed;
    public bool IsDisplayPrewarmed => _displayPrewarmed;

    public IEnumerator CoPrewarmPool()
    {
        TryResolveInventory();

        int totalSlots = TotalSlots;
        if (_displayPrewarmed && _poolPrewarmed && _slotPool.Count >= totalSlots)
            yield break;

        if (_poolPrewarmed && _slotPool.Count >= totalSlots)
        {
            FinalizePrewarmDisplayState();
            yield break;
        }

        inventory?.EnsureSlotCount(totalSlots);
        yield return CoEnsurePoolSize(totalSlots, MainMenuUIPrewarm.UseBatchedInstantiation);

        for (int i = 0; i < Mathf.Max(1, layoutRetryFrames); i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (slotsGrid && slotsGrid.rect.width > 1f && slotsGrid.rect.height > 1f)
                break;
        }

        ApplyGridFit();
        Rebuild();
        FinalizePrewarmDisplayState();
    }

    private void FinalizePrewarmDisplayState()
    {
        _poolPrewarmed = true;
        _layoutSettled = slotsGrid && slotsGrid.rect.width > 1f && slotsGrid.rect.height > 1f;
        _displayPrewarmed = true;
        _dirty = false;

        if (hideGridUntilReady)
            SetGridVisible(true);
    }

    private IEnumerator DeferredRefresh()
    {
        int totalSlots = TotalSlots;
        if (_poolPrewarmed && _slotPool.Count >= totalSlots)
            EnsurePoolSize(totalSlots);
        else if (MainMenuUIPrewarm.UseBatchedInstantiation)
            yield return CoEnsurePoolSize(totalSlots, batched: true);
        else
            EnsurePoolSize(totalSlots);

        for (int i = 0; i < Mathf.Max(1, layoutRetryFrames); i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (slotsGrid && slotsGrid.rect.width > 1f && slotsGrid.rect.height > 1f)
                break;
        }

        ApplyGridFit();
        Rebuild();
        _dirty = false;

        // Extra layout settle pass before showing (prevents 1-frame wrong positions).
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (slotsGrid && !_layoutSettled)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotsGrid);
        Canvas.ForceUpdateCanvases();
        _layoutSettled = slotsGrid && slotsGrid.rect.width > 1f && slotsGrid.rect.height > 1f;

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

    private void EnsurePoolSize(int needed)
    {
        if (!slotsGrid || !slotPrefab) return;

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

        if (_slotPool.Count >= needed)
            _poolPrewarmed = true;
    }

    private IEnumerator CoEnsurePoolSize(int needed, bool batched)
    {
        if (!slotsGrid || !slotPrefab)
            yield break;

        if (!batched)
        {
            EnsurePoolSize(needed);
            yield break;
        }

        while (_slotPool.Count < needed)
        {
            int batchEnd = Mathf.Min(_slotPool.Count + PrewarmPoolBatchSize, needed);
            for (int i = _slotPool.Count; i < batchEnd; i++)
            {
                var slot = Instantiate(slotPrefab, slotsGrid, false);
                _slotPool.Add(slot);
            }

            for (int i = 0; i < _slotPool.Count; i++)
            {
                if (_slotPool[i] != null)
                    _slotPool[i].gameObject.SetActive(i < needed);
            }

            yield return null;
        }

        _poolPrewarmed = true;
    }

    private void ApplyGridFit()
    {
        EnsureScrollViewportClipping();

        if (!slotsGrid) return;
        if (!_grid) _grid = slotsGrid.GetComponent<GridLayoutGroup>();
        if (!_grid) return;

        if (useManualGridLayoutSettings)
        {
            ApplyContentHeightForRows(GetTargetRowCountForCurrentGridSettings());
            return;
        }

        RectTransform fitRect = _resolvedViewport ? _resolvedViewport : slotsGrid;
        float w = fitRect.rect.width;
        float h = fitRect.rect.height;
        if (w <= 1f || h <= 1f) return;

        columns = Mathf.Max(1, columns);
        minVisibleRows = Mathf.Max(1, minVisibleRows);
        int rowCount = GetTargetRowCount();
        int fitRows = Mathf.Max(minVisibleRows, 1);

        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.startAxis = GridLayoutGroup.Axis.Horizontal;

        float usableW = w - _grid.padding.left - _grid.padding.right - _grid.spacing.x * (columns - 1);
        float usableH = h - _grid.padding.top - _grid.padding.bottom - _grid.spacing.y * (fitRows - 1);

        float cellW = usableW / columns;
        float cellH = usableH / fitRows;

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

        ApplyContentHeightForRows(rowCount);
    }

    public void Rebuild()
    {
        if (!inventory || !slotsGrid || !slotPrefab) return;
        int totalSlots = TotalSlots;
        inventory.EnsureSlotCount(totalSlots);
        EnsurePoolSize(totalSlots);

        InvalidateSlotRebindCacheIfInventorySlotCountChanged();

        int layoutSig = ComputeInventoryGridLayoutSignature(totalSlots);
        if (layoutSig != _lastLayoutFitSignature)
        {
            ApplyGridFit();
            _lastLayoutFitSignature = layoutSig;
        }

        bool allFilterActive = _activeFilter == InventoryViewFilter.All;
        List<int> visibleSourceSlots = null;
        if (!allFilterActive)
        {
            BuildFilteredSourceSlotListInto(totalSlots, _filteredSourceSlotScratch);
            visibleSourceSlots = _filteredSourceSlotScratch;
        }

        EnsureRebindCacheCapacity(totalSlots);

        for (int i = 0; i < totalSlots; i++)
        {
            var slotUI = _slotPool[i];
            if (!slotUI) continue;

            bool hasMappedSource = allFilterActive || (i < visibleSourceSlots.Count);
            int sourceSlotIndex = allFilterActive ? i : (hasMappedSource ? visibleSourceSlots[i] : -1);
            var s = hasMappedSource ? inventory.GetSlot(sourceSlotIndex) : default;

            int interactiveSlotIndex = hasMappedSource ? sourceSlotIndex : -1;
            int cacheAmt = hasMappedSource ? s.amount : 0;
            string cacheId = hasMappedSource ? s.itemId : null;

            int defIdentity = 0;
            bool identifyPending = false;
            ItemDefinition def = null;
            if (hasMappedSource && !s.IsEmpty)
            {
                def = inventory.GetItemDef(s.itemId);
                if (!def && itemDb) def = itemDb.Get(s.itemId);
                defIdentity = def != null ? def.GetInstanceID() : 0;
                ItemDatabase db = itemDb != null ? itemDb : inventory.GetItemDatabase();
                identifyPending = ItemRandomStatIdentification.HasUnidentifiedRandomAffixes(db, s.itemId);
            }

            if (interactiveSlotIndex == _rebindCacheSrcIdx[i] &&
                cacheAmt == _rebindCacheAmt[i] &&
                GridItemIdEquals(cacheId, _rebindCacheItemId[i]) &&
                defIdentity == _rebindCacheDefId[i] &&
                identifyPending == _rebindCacheIdentifyPending[i])
            {
                continue;
            }

            _rebindCacheSrcIdx[i] = interactiveSlotIndex;
            _rebindCacheAmt[i] = cacheAmt;
            _rebindCacheItemId[i] = cacheId;
            _rebindCacheDefId[i] = defIdentity;
            _rebindCacheIdentifyPending[i] = identifyPending;

            if (hasMappedSource && !s.IsEmpty)
            {
                slotUI.Bind(def, s.amount, s.itemId, tooltip, inventory, interactiveSlotIndex, inventoryPanelRect, _rootCanvas);
                slotUI.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
            }
            else
            {
                slotUI.Bind(null, 0, null, tooltip, inventory, interactiveSlotIndex, inventoryPanelRect, _rootCanvas);
                slotUI.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
            }
        }
    }

    private void InvalidateSlotRebindCacheIfInventorySlotCountChanged()
    {
        if (inventory == null)
            return;

        int n = inventory.SlotCount;
        if (n == _lastInventorySlotCountForRebindCache)
            return;

        _lastInventorySlotCountForRebindCache = n;
        ClearSlotRebindCache();
    }

    private void ClearSlotRebindCache()
    {
        if (_rebindCacheSrcIdx == null)
            return;

        for (int i = 0; i < _rebindCacheSrcIdx.Length; i++)
        {
            _rebindCacheSrcIdx[i] = int.MinValue;
            _rebindCacheAmt[i] = int.MinValue;
            _rebindCacheItemId[i] = null;
            _rebindCacheDefId[i] = 0;
            if (_rebindCacheIdentifyPending != null)
                _rebindCacheIdentifyPending[i] = false;
        }
    }

    private void EnsureRebindCacheCapacity(int needed)
    {
        if (_rebindCacheSrcIdx != null && _rebindCacheSrcIdx.Length >= needed)
            return;

        int newCap = needed <= 0 ? 16 : Mathf.NextPowerOfTwo(Mathf.Max(needed, 16));
        var src = new int[newCap];
        var amt = new int[newCap];
        var ids = new string[newCap];
        var did = new int[newCap];
        var identify = new bool[newCap];
        for (int i = 0; i < newCap; i++)
        {
            src[i] = int.MinValue;
            amt[i] = int.MinValue;
        }

        if (_rebindCacheSrcIdx != null && _rebindCacheSrcIdx.Length > 0)
        {
            int copy = Mathf.Min(_rebindCacheSrcIdx.Length, newCap);
            Array.Copy(_rebindCacheSrcIdx, src, copy);
            Array.Copy(_rebindCacheAmt, amt, copy);
            Array.Copy(_rebindCacheItemId, ids, copy);
            if (_rebindCacheDefId != null && _rebindCacheDefId.Length >= copy)
                Array.Copy(_rebindCacheDefId, did, copy);
            if (_rebindCacheIdentifyPending != null && _rebindCacheIdentifyPending.Length >= copy)
                Array.Copy(_rebindCacheIdentifyPending, identify, copy);
        }

        _rebindCacheSrcIdx = src;
        _rebindCacheAmt = amt;
        _rebindCacheItemId = ids;
        _rebindCacheDefId = did;
        _rebindCacheIdentifyPending = identify;
    }

    private static bool GridItemIdEquals(string a, string b) =>
        string.IsNullOrEmpty(a) ? string.IsNullOrEmpty(b) : string.Equals(a, b, StringComparison.Ordinal);

    private int ComputeInventoryGridLayoutSignature(int totalSlots)
    {
        HashCode hc = new HashCode();
        hc.Add((int)_activeFilter);
        hc.Add(totalSlots);
        hc.Add(columns);
        hc.Add(minVisibleRows);
        hc.Add(useManualGridLayoutSettings);
        hc.Add(squareCells);

        if (slotsGrid != null)
        {
            if (useManualGridLayoutSettings)
            {
                hc.Add(Mathf.RoundToInt(slotsGrid.rect.width * 1000f));
                hc.Add(Mathf.RoundToInt(slotsGrid.rect.height * 1000f));
            }
            else
            {
                RectTransform fitRect = _resolvedViewport != null ? _resolvedViewport : slotsGrid;
                hc.Add(Mathf.RoundToInt(fitRect.rect.width * 1000f));
                hc.Add(Mathf.RoundToInt(fitRect.rect.height * 1000f));
            }
        }

        return hc.ToHashCode();
    }

    public void SetFilterAll() => SetFilter(InventoryViewFilter.All);
    public void SetFilterResources() => SetFilter(InventoryViewFilter.Resources);
    public void SetFilterEquips() => SetFilter(InventoryViewFilter.Equips);
    public void SetFilterEnhance() => SetFilter(InventoryViewFilter.Enhance);
    public void SetFilterConsumables() => SetFilter(InventoryViewFilter.Consumables);

    private void SetFilter(InventoryViewFilter filter, bool rebuildNow = true)
    {
        bool changed = _activeFilter != filter;
        _activeFilter = filter;
        ApplyFilterButtonVisuals();
        if (changed)
        {
            _lastLayoutFitSignature = int.MinValue;
            ClearSlotRebindCache();
        }

        if (rebuildNow)
            Rebuild();
        if (changed)
            OnFilterChanged?.Invoke();
    }

    /// <summary>True when no category filter is active (i.e. every inventory item is visible).</summary>
    public bool IsShowingAllItems => _activeFilter == InventoryViewFilter.All;

    /// <summary>
    /// Sums the value of every inventory slot that currently passes the active category filter.
    /// Falls back to <see cref="Inventory.GetTotalInventoryValue"/> when the All filter is active.
    /// </summary>
    public int GetVisibleInventoryValue()
    {
        if (inventory == null)
            return 0;

        if (_activeFilter == InventoryViewFilter.All)
            return inventory.GetTotalInventoryValue();

        int total = 0;
        int slotCount = inventory.SlotCount;
        for (int i = 0; i < slotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (slot.IsEmpty)
                continue;

            ItemDefinition def = inventory.GetItemDef(slot.itemId);
            if (!def && itemDb)
                def = itemDb.Get(slot.itemId);
            if (def == null || !PassesFilter(def))
                continue;

            total += inventory.GetSlotValue(i);
        }
        return total;
    }

    private void BindFilterButtons()
    {
        if (allFilterButton) allFilterButton.onClick.AddListener(SetFilterAll);
        if (resourceFilterButton) resourceFilterButton.onClick.AddListener(SetFilterResources);
        if (equipsFilterButton) equipsFilterButton.onClick.AddListener(SetFilterEquips);
        if (enhanceFilterButton) enhanceFilterButton.onClick.AddListener(SetFilterEnhance);
        if (consumablesFilterButton) consumablesFilterButton.onClick.AddListener(SetFilterConsumables);
    }

    private void UnbindFilterButtons()
    {
        if (allFilterButton) allFilterButton.onClick.RemoveListener(SetFilterAll);
        if (resourceFilterButton) resourceFilterButton.onClick.RemoveListener(SetFilterResources);
        if (equipsFilterButton) equipsFilterButton.onClick.RemoveListener(SetFilterEquips);
        if (enhanceFilterButton) enhanceFilterButton.onClick.RemoveListener(SetFilterEnhance);
        if (consumablesFilterButton) consumablesFilterButton.onClick.RemoveListener(SetFilterConsumables);
    }

    private void BuildFilteredSourceSlotListInto(int totalSlots, List<int> results)
    {
        results.Clear();
        for (int sourceSlotIndex = 0; sourceSlotIndex < totalSlots; sourceSlotIndex++)
        {
            var sourceSlot = inventory.GetSlot(sourceSlotIndex);
            if (sourceSlot.IsEmpty)
                continue;

            ItemDefinition def = inventory.GetItemDef(sourceSlot.itemId);
            if (!def && itemDb)
                def = itemDb.Get(sourceSlot.itemId);
            if (def == null)
                continue;

            if (PassesFilter(def))
                results.Add(sourceSlotIndex);
        }
    }

    private bool PassesFilter(ItemDefinition def)
    {
        if (def == null)
            return false;

        return _activeFilter switch
        {
            InventoryViewFilter.All => true,
            InventoryViewFilter.Resources => def.itemKind == ItemKind.Resource,
            InventoryViewFilter.Equips => def.itemKind == ItemKind.Weapon ||
                                          def.itemKind == ItemKind.Armour ||
                                          def.itemKind == ItemKind.CombatSupport ||
                                          def.itemKind == ItemKind.Tool ||
                                          def.itemKind == ItemKind.Jewelry,
            InventoryViewFilter.Consumables => def.itemKind == ItemKind.Consumable && !def.IsMapEnhancement,
            InventoryViewFilter.Enhance => def.IsEnhancementScroll || def.IsMapEnhancement,
            _ => true
        };
    }

    private void ApplyFilterButtonVisuals()
    {
        SetFilterButtonVisual(allFilterButton, _activeFilter == InventoryViewFilter.All);
        SetFilterButtonVisual(resourceFilterButton, _activeFilter == InventoryViewFilter.Resources);
        SetFilterButtonVisual(equipsFilterButton, _activeFilter == InventoryViewFilter.Equips);
        SetFilterButtonVisual(enhanceFilterButton, _activeFilter == InventoryViewFilter.Enhance);
        SetFilterButtonVisual(consumablesFilterButton, _activeFilter == InventoryViewFilter.Consumables);
    }

    private void SetFilterButtonVisual(Button button, bool active)
    {
        UITabBarButtonVisuals.Apply(button, active);
    }

    private int GetTargetSlotCount()
    {
        if (inventory != null && inventory.SlotCount > 0)
            return inventory.SlotCount;

        return Mathf.Max(1, columns) * Mathf.Max(1, minVisibleRows);
    }

    private int GetTargetRowCount()
    {
        int totalSlots = Mathf.Max(1, GetTargetSlotCount());
        int safeColumns = Mathf.Max(1, columns);
        return Mathf.Max(1, Mathf.CeilToInt(totalSlots / (float)safeColumns));
    }

    private int GetTargetRowCountForCurrentGridSettings()
    {
        int totalSlots = Mathf.Max(1, GetTargetSlotCount());
        int effectiveColumns = ResolveEffectiveColumnCount(totalSlots);
        return Mathf.Max(1, Mathf.CeilToInt(totalSlots / (float)Mathf.Max(1, effectiveColumns)));
    }

    private int ResolveEffectiveColumnCount(int totalSlots)
    {
        if (_grid == null)
            return Mathf.Max(1, columns);

        switch (_grid.constraint)
        {
            case GridLayoutGroup.Constraint.FixedColumnCount:
                return Mathf.Max(1, _grid.constraintCount);
            case GridLayoutGroup.Constraint.FixedRowCount:
            {
                int rows = Mathf.Max(1, _grid.constraintCount);
                return Mathf.Max(1, Mathf.CeilToInt(totalSlots / (float)rows));
            }
            default:
                // Flexible constraint: use configured fallback so row estimation stays stable.
                return Mathf.Max(1, columns);
        }
    }

    private void ApplyContentHeightForRows(int rows)
    {
        if (_grid == null)
            return;

        rows = Mathf.Max(1, rows);
        float height =
            _grid.padding.top +
            _grid.padding.bottom +
            (_grid.cellSize.y * rows) +
            (_grid.spacing.y * Mathf.Max(0, rows - 1));

        RectTransform content = _resolvedContent ? _resolvedContent : slotsGrid;
        if (!content)
            return;

        Vector2 size = content.sizeDelta;
        size.y = Mathf.Max(0f, height);
        content.sizeDelta = size;
    }

    /// <summary>
    /// Ensures inventory slots are clipped to the visible scroll viewport, even if scene wiring misses the mask component.
    /// </summary>
    private void EnsureScrollViewportClipping()
    {
        if (!inventoryScrollRect && slotsGrid)
            inventoryScrollRect = slotsGrid.GetComponentInParent<ScrollRect>(true);
        if (!inventoryScrollRect)
            return;

        _resolvedViewport = inventoryScrollRect.viewport;
        if (!_resolvedViewport)
            _resolvedViewport = FindViewportChild(inventoryScrollRect.transform);
        if (!_resolvedViewport)
            _resolvedViewport = inventoryScrollRect.transform as RectTransform;
        if (!_resolvedViewport)
            return;

        NormalizeViewportRectTransform();

        if (_resolvedViewport.GetComponent<RectMask2D>() == null)
            _resolvedViewport.gameObject.AddComponent<RectMask2D>();

        NormalizeScrollContentWiring();
    }

    /// <summary>
    /// Some scene setups end up with a viewport anchored as a point (size 0), which breaks clipping.
    /// Force a sane stretch-to-parent viewport only when clearly invalid.
    /// </summary>
    private void NormalizeViewportRectTransform()
    {
        if (_resolvedViewport == null)
            return;

        bool invalidSize = _resolvedViewport.rect.width <= 1f || _resolvedViewport.rect.height <= 1f;
        bool pointAnchored = Mathf.Approximately(_resolvedViewport.anchorMin.x, _resolvedViewport.anchorMax.x) &&
                             Mathf.Approximately(_resolvedViewport.anchorMin.y, _resolvedViewport.anchorMax.y);
        if (!invalidSize && !pointAnchored)
            return;

        _resolvedViewport.anchorMin = Vector2.zero;
        _resolvedViewport.anchorMax = Vector2.one;
        _resolvedViewport.pivot = new Vector2(0.5f, 0.5f);
        _resolvedViewport.anchoredPosition = Vector2.zero;
        _resolvedViewport.sizeDelta = Vector2.zero;
        _resolvedViewport.offsetMin = Vector2.zero;
        _resolvedViewport.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Keeps ScrollRect content wiring deterministic so clipping/scrolling target the same rect.
    /// </summary>
    private void NormalizeScrollContentWiring()
    {
        if (inventoryScrollRect == null || _resolvedViewport == null || slotsGrid == null)
            return;

        _resolvedContent = scrollContentRoot;
        if (!_resolvedContent)
            _resolvedContent = inventoryScrollRect.content;
        if (!_resolvedContent)
            _resolvedContent = slotsGrid.parent as RectTransform;
        if (!_resolvedContent)
            _resolvedContent = slotsGrid;

        if (_resolvedContent.parent != _resolvedViewport)
            _resolvedContent.SetParent(_resolvedViewport, false);

        if (slotsGrid.parent != _resolvedContent)
            slotsGrid.SetParent(_resolvedContent, false);

        if (inventoryScrollRect.content != _resolvedContent)
            inventoryScrollRect.content = _resolvedContent;

        // Content: top-pinned, stretch width to viewport.
        _resolvedContent.anchorMin = new Vector2(0f, 1f);
        _resolvedContent.anchorMax = new Vector2(1f, 1f);
        _resolvedContent.pivot = new Vector2(0.5f, 1f);
        _resolvedContent.anchoredPosition = new Vector2(0f, 0f);
        Vector2 contentSize = _resolvedContent.sizeDelta;
        contentSize.x = 0f;
        _resolvedContent.sizeDelta = contentSize;

        // Slots grid: child of content, also stretch-width top pinned.
        slotsGrid.anchorMin = new Vector2(0f, 1f);
        slotsGrid.anchorMax = new Vector2(1f, 1f);
        slotsGrid.pivot = new Vector2(0.5f, 1f);
        slotsGrid.anchoredPosition = new Vector2(0f, 0f);
        Vector2 size = slotsGrid.sizeDelta;
        size.x = 0f;
        slotsGrid.sizeDelta = size;
    }

    private static RectTransform FindViewportChild(Transform root)
    {
        if (root == null)
            return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (string.Equals(c.name, "Viewport", System.StringComparison.OrdinalIgnoreCase))
                return c as RectTransform;
        }
        return null;
    }
}