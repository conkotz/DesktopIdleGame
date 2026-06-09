using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Upgrade-page inventory mirror: shows the full player inventory, dims non-gear items,
/// and routes gear selection to the center upgrade slot (click, double-click, or drag).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class UpgradeInventoryGridUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemDatabase itemDb;
    [SerializeField] private RectTransform slotsGrid;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private RectTransform inventoryPanelRect;
    [SerializeField] private RectTransform tooltipAnchor;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    [Header("Layout")]
    [SerializeField] private int columns = 7;
    [SerializeField] private int minVisibleRows = 4;

    public event Action<int, ItemDefinition, string> GearSelected;

    private readonly List<InventorySlotUI> _slotPool = new(64);
    private GridLayoutGroup _grid;
    private Canvas _rootCanvas;
    private RectTransform _upgradeDropTarget;
    private int _selectedSourceSlot = -1;
    private bool _dirty = true;

    private void Awake()
    {
        if (!inventory)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player)
                inventory = player.GetComponent<Inventory>();
        }

        if (slotsGrid)
            _grid = slotsGrid.GetComponent<GridLayoutGroup>();

        _rootCanvas = GetComponentInParent<Canvas>();

        if (!inventoryPanelRect)
            inventoryPanelRect = transform as RectTransform;

        AdoptLegacyGridBindings();
        DisableLegacyInventoryGrid();
    }

    private void OnEnable()
    {
        DisableLegacyInventoryGrid();
        TrySubscribeInventory();
        _dirty = true;
        RebuildIfDirty();
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
    }

    private void LateUpdate()
    {
        RebuildIfDirty();
    }

    public void SetUpgradeDropTarget(RectTransform dropTarget) => _upgradeDropTarget = dropTarget;

    public void MarkDirty() => _dirty = true;

    public void SetSelectedSourceSlot(int sourceSlotIndex)
    {
        _selectedSourceSlot = sourceSlotIndex;
        ApplySelectionVisuals();
    }

    public int SelectedSourceSlot => _selectedSourceSlot;

    public SharedTooltipUI Tooltip => tooltip;

    public RectTransform TooltipHeightRect => tooltipHeightRect;

    public FlipInsideBounds.PreferredSide TooltipPreferredSide => preferredSide;

    private Inventory _subscribedInventory;

    private void TrySubscribeInventory()
    {
        Inventory inv = inventory ?? FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inv == _subscribedInventory)
            return;

        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryChanged -= OnInventoryChanged;

        inventory = inv;
        _subscribedInventory = inv;
        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryChanged += OnInventoryChanged;
    }

    private void UnsubscribeInventory()
    {
        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryChanged -= OnInventoryChanged;
        _subscribedInventory = null;
    }

    private void OnInventoryChanged()
    {
        _dirty = true;
        if (_selectedSourceSlot >= 0 && !IsUpgradableGearSlot(_selectedSourceSlot))
            SetSelectedSourceSlot(-1);
    }

    private void AdoptLegacyGridBindings()
    {
        InventoryGridUI legacyGrid = GetComponent<InventoryGridUI>();
        if (legacyGrid == null || !legacyGrid.TryGetUpgradeGridBindings(
                out Inventory boundInventory,
                out ItemDatabase boundItemDb,
                out RectTransform boundSlotsGrid,
                out InventorySlotUI boundSlotPrefab,
                out SharedTooltipUI boundTooltip,
                out RectTransform boundPanelRect,
                out RectTransform boundTooltipAnchor,
                out RectTransform boundTooltipHeightRect,
                out int boundColumns,
                out int boundMinVisibleRows))
            return;

        inventory ??= boundInventory;
        itemDb ??= boundItemDb;
        slotsGrid ??= boundSlotsGrid;
        slotPrefab ??= boundSlotPrefab;
        tooltip ??= boundTooltip;
        inventoryPanelRect ??= boundPanelRect;
        tooltipAnchor ??= boundTooltipAnchor;
        tooltipHeightRect ??= boundTooltipHeightRect;
        if (columns <= 0)
            columns = boundColumns;
        if (minVisibleRows <= 0)
            minVisibleRows = boundMinVisibleRows;
    }

    private void DisableLegacyInventoryGrid()
    {
        InventoryGridUI legacyGrid = GetComponent<InventoryGridUI>();
        if (legacyGrid != null)
            legacyGrid.enabled = false;
    }

    private void RebuildIfDirty()
    {
        if (!_dirty)
            return;

        _dirty = false;
        Rebuild();
    }

    public void Rebuild()
    {
        if (!inventory || !slotsGrid || !slotPrefab)
            return;

        inventory.EnsureSlotCount(inventory.SlotCount);

        int totalSlots = Mathf.Max(inventory.SlotCount, columns * minVisibleRows);
        EnsurePoolSize(totalSlots);
        ApplyContentHeight(totalSlots);

        for (int i = 0; i < totalSlots; i++)
        {
            InventorySlotUI slotUi = _slotPool[i];
            if (!slotUi)
                continue;

            if (i >= inventory.SlotCount)
            {
                slotUi.Bind(null, 0, null, tooltip, inventory, -1, inventoryPanelRect, _rootCanvas);
                slotUi.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
                slotUi.ConfigureUpgradeView(true, canSelectGear: false, onSelect: null, _upgradeDropTarget);
                slotUi.SetUpgradeSelected(false);
                continue;
            }

            Inventory.Slot slot = inventory.GetSlot(i);
            ItemDefinition def = null;
            if (!slot.IsEmpty)
            {
                def = inventory.GetItemDef(slot.itemId);
                if (!def && itemDb)
                    def = itemDb.Get(slot.itemId);
            }

            bool canSelect = IsUpgradableGear(def);
            slotUi.Bind(def, slot.amount, slot.itemId, tooltip, inventory, i, inventoryPanelRect, _rootCanvas);
            slotUi.SetTooltipDocking(tooltipAnchor, tooltipHeightRect, preferredSide);
            slotUi.ConfigureUpgradeView(true, canSelect, HandleSlotClicked, _upgradeDropTarget);
            slotUi.SetUpgradeSelected(i == _selectedSourceSlot);
        }
    }

    private void HandleSlotClicked(int sourceSlotIndex)
    {
        if (!IsUpgradableGearSlot(sourceSlotIndex))
            return;

        Inventory.Slot slot = inventory.GetSlot(sourceSlotIndex);
        ItemDefinition def = inventory.GetItemDef(slot.itemId);
        if (!def && itemDb)
            def = itemDb.Get(slot.itemId);

        SetSelectedSourceSlot(sourceSlotIndex);
        GearSelected?.Invoke(sourceSlotIndex, def, slot.itemId);
    }

    private void ApplySelectionVisuals()
    {
        for (int i = 0; i < _slotPool.Count; i++)
        {
            InventorySlotUI slotUi = _slotPool[i];
            if (!slotUi)
                continue;

            slotUi.SetUpgradeSelected(slotUi.SlotIndex == _selectedSourceSlot);
        }
    }

    private static bool IsUpgradableGear(ItemDefinition def)
    {
        if (def == null || !def.HasUpgradeSlots)
            return false;

        return def.IsWeapon || def.IsArmor || def.IsTool;
    }

    private bool IsUpgradableGearSlot(int slotIndex)
    {
        if (inventory == null || slotIndex < 0 || slotIndex >= inventory.SlotCount)
            return false;

        Inventory.Slot slot = inventory.GetSlot(slotIndex);
        if (slot.IsEmpty)
            return false;

        ItemDefinition def = inventory.GetItemDef(slot.itemId);
        if (!def && itemDb)
            def = itemDb.Get(slot.itemId);

        return IsUpgradableGear(def);
    }

    private void EnsurePoolSize(int count)
    {
        while (_slotPool.Count < count)
        {
            InventorySlotUI created = Instantiate(slotPrefab, slotsGrid);
            created.gameObject.SetActive(true);
            _slotPool.Add(created);
        }

        for (int i = 0; i < _slotPool.Count; i++)
            _slotPool[i].gameObject.SetActive(i < count);
    }

    private void ApplyContentHeight(int slotCount)
    {
        if (!_grid || !slotsGrid)
            return;

        int rows = Mathf.Max(minVisibleRows, Mathf.CeilToInt((float)slotCount / Mathf.Max(1, columns)));
        float height = _grid.padding.top + _grid.padding.bottom +
                       rows * _grid.cellSize.y +
                       Mathf.Max(0, rows - 1) * _grid.spacing.y;
        slotsGrid.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}
