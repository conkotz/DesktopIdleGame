using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StorageSlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler,
    IItemTooltipHoverSource
{
    private const float BorderThicknessMul = 0.65f;
    private const float IdentifyGlowSeconds = 1.1f;

    [Header("UI")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Image identifyIcon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Outline rarityOutline;

    [Header("Slot Colours")]
    [SerializeField] private Color idleColor = new Color32(32, 34, 37, 255);
    [SerializeField] private Color hoverColor = new Color32(198, 184, 158, 255);
    [SerializeField] private Color pressedColor = new Color32(217, 164, 65, 255);

    [Header("Idle auto-battle new loot")]
    [SerializeField] private Color autoBattleNewLootColor = new Color32(217, 190, 100, 255);

    [Header("Rarity Border")]
    [SerializeField] private bool showRarityBorder = true;
    [SerializeField] private Color uncommonBorder = new Color32(80, 200, 120, 255);
    [SerializeField] private Color rareBorder = new Color32(80, 150, 255, 255);
    [SerializeField] private Color epicBorder = new Color32(190, 90, 255, 255);
    [SerializeField] private Color legendaryBorder = new Color32(255, 170, 40, 255);
    [SerializeField] private Vector2 rarityBorderThickness = new Vector2(4f, 4f);

    [Header("Double click")]
    [SerializeField] private float doubleClickSeconds = 0.30f;
    private float _lastClickTime;

    private bool _isPointerOver;
    private ItemDefinition _def;
    private SharedTooltipUI _tooltip;

    private RectTransform _tooltipAnchor;
    private RectTransform _tooltipHeightRect;
    private FlipInsideBounds.PreferredSide _preferredSide = FlipInsideBounds.PreferredSide.Left;

    private PlayerStorage _storage;
    private Inventory _inventory;
    private RectTransform _storagePanelRect;
    private Canvas _rootCanvas;

    private int _slotIndex;
    private string _itemId;
    private int _amount;
    private bool _identifyPendingCached;
    private Coroutine _identifyRoutine;

    public int SlotIndex => _slotIndex;

    public bool HasItemContext => _def != null && _amount > 0 && !string.IsNullOrEmpty(_itemId);
    public ItemDefinition ContextDefinition => _def;
    public string ContextItemId => _itemId;

    public bool CanWithdrawToInventory()
    {
        if (_storage == null || _inventory == null || _amount <= 0)
            return false;

        return _inventory.CanAdd(_itemId, 1);
    }

    private GameObject _dragIconGO;
    private RectTransform _dragIconRT;
    private Image _dragIconImage;

    private void Awake()
    {
        if (!background) background = GetComponent<Image>();
        if (background)
        {
            background.enabled = true;
            background.raycastTarget = true;
            background.color = idleColor;
        }

        if (!rarityOutline && background)
            rarityOutline = background.GetComponent<Outline>();
        if (!rarityOutline && background)
            rarityOutline = background.gameObject.AddComponent<Outline>();
        if (rarityOutline)
        {
            rarityOutline.enabled = false;
            rarityOutline.useGraphicAlpha = false;
            rarityOutline.effectDistance = rarityBorderThickness;
        }

        if (!identifyIcon)
            identifyIcon = transform.Find("IdentifyIcon")?.GetComponent<Image>();

        if (identifyIcon)
        {
            identifyIcon.raycastTarget = false;
            identifyIcon.gameObject.SetActive(false);
        }

        if (icon) icon.raycastTarget = false;
        if (countText) countText.raycastTarget = false;

        foreach (var mg in GetComponentsInChildren<MaskableGraphic>(true))
            mg.maskable = true;

        if (!_inventory)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        AutoBattleLootHighlight.RegisterStorageSlotUi(this);
    }

    private void OnDestroy()
    {
        AutoBattleLootHighlight.UnregisterStorageSlotUi(this);
    }

    public void Bind(
        ItemDefinition def,
        int amount,
        string itemId,
        SharedTooltipUI tooltip,
        PlayerStorage storage,
        int slotIndex,
        RectTransform storagePanelRect,
        Canvas rootCanvas)
    {
        bool identifyPending = IsIdentifyPending(def, amount, itemId);

        bool sameVisual =
            ReferenceEquals(_storage, storage) &&
            _slotIndex == slotIndex &&
            _amount == amount &&
            ReferenceEquals(_def, def) &&
            ItemIdEquals(_itemId, itemId) &&
            _identifyPendingCached == identifyPending;

        _def = def;
        _amount = amount;
        _itemId = itemId;
        _tooltip = tooltip;

        _storage = storage;
        _slotIndex = slotIndex;
        _storagePanelRect = storagePanelRect;
        _rootCanvas = rootCanvas;

        if (!_inventory)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (sameVisual)
        {
            RefreshIdentifyIcon(identifyPending);
            ApplySlotBackground();
            return;
        }

        _identifyPendingCached = identifyPending;

        if (icon)
        {
            bool hasIcon = def != null && def.icon != null;
            icon.enabled = hasIcon;
            icon.sprite = hasIcon ? def.icon : null;
            icon.preserveAspect = true;
        }

        if (countText)
            countText.text = (def != null && amount > 0) ? amount.ToString() : "";

        RefreshRarityBorder(def);

        if (def == null || amount <= 0 || string.IsNullOrEmpty(itemId))
            AutoBattleLootHighlight.ClearStorageSlot(slotIndex);

        RefreshIdentifyIcon(identifyPending);
        ApplySlotBackground();
    }

    private static bool IsIdentifyPending(ItemDefinition def, int amount, string itemId)
    {
        if (def == null || amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return false;

        ItemDatabase db = ResolveItemDatabase();
        return ItemRandomStatIdentification.HasUnidentifiedRandomAffixes(db, itemId);
    }

    private static ItemDatabase ResolveItemDatabase()
    {
        Inventory inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inventory != null)
            return inventory.GetItemDatabase();

        ItemDatabase db = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        if (db != null)
            return db;

        return Resources.Load<ItemDatabase>("Databases/ItemDatabase");
    }

    private Sprite ResolveDropIcon(string itemId)
    {
        if (_def != null && _def.icon != null)
            return _def.icon;

        if (icon != null && icon.enabled && icon.sprite != null)
            return icon.sprite;

        if (_storage != null)
        {
            ItemDefinition storageDef = _storage.GetItemDef(itemId);
            if (storageDef != null && storageDef.icon != null)
                return storageDef.icon;
        }

        return ResolveItemDatabase()?.Get(itemId)?.icon;
    }

    private static bool ItemIdEquals(string a, string b) =>
        string.IsNullOrEmpty(a) ? string.IsNullOrEmpty(b) : string.Equals(a, b, System.StringComparison.Ordinal);

    private void RefreshIdentifyIcon(bool show)
    {
        if (!identifyIcon)
            return;

        identifyIcon.gameObject.SetActive(show);
        if (show)
            identifyIcon.enabled = true;
    }

    public void SetTooltipDocking(
        RectTransform tooltipAnchor,
        RectTransform tooltipHeightRect,
        FlipInsideBounds.PreferredSide preferredSide)
    {
        _tooltipAnchor = tooltipAnchor;
        _tooltipHeightRect = tooltipHeightRect;
        _preferredSide = preferredSide;
    }

    private void RefreshRarityBorder(ItemDefinition def)
    {
        if (!showRarityBorder || !rarityOutline)
        {
            if (rarityOutline) rarityOutline.enabled = false;
            return;
        }

        if (def == null)
        {
            rarityOutline.enabled = false;
            return;
        }

        rarityOutline.enabled = true;
        float mul = Mathf.Clamp(BorderThicknessMul, 0.1f, 1f);
        rarityOutline.effectDistance = rarityBorderThickness * mul;
        rarityOutline.effectColor = def.rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => uncommonBorder,
            ItemRarity.Rare => rareBorder,
            ItemRarity.Epic => epicBorder,
            ItemRarity.Legendary => legendaryBorder,
            _ => Color.white
        };
    }

    private void ApplySlotBackground()
    {
        if (!background) return;

        if (_isPointerOver)
            background.color = hoverColor;
        else if (AutoBattleLootHighlight.IsStorageSlotMarked(_slotIndex))
            background.color = autoBattleNewLootColor;
        else
            background.color = idleColor;
    }

    /// <summary>Updates the idle / new-loot tint without re-binding slot data.</summary>
    public void RefreshLootHighlightVisual() => ApplySlotBackground();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InventoryDragState.HasDrag)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            OpenContextMenu(eventData);
            eventData.Use();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        float t = Time.unscaledTime;
        bool doubleClick = (t - _lastClickTime) <= doubleClickSeconds;
        _lastClickTime = t;

        if (doubleClick || InventorySlotUI.InputUtil.CtrlHeld())
        {
            TryDoubleClickWithdrawToInventory();
            eventData.Use();
        }
    }

    private void TryDoubleClickWithdrawToInventory()
    {
        if (_storage == null || _inventory == null) return;
        var slot = _storage.GetSlot(_slotIndex);
        if (slot.IsEmpty) return;

        ItemDefinition defForBar = _def;
        bool assignConsumableAfter = defForBar != null && (defForBar.IsFood || defForBar.IsPotion);

        int moved = _storage.TryWithdrawAllToInventory(_inventory, _slotIndex);
        _tooltip?.Hide();

        if (moved > 0 && assignConsumableAfter && defForBar != null)
        {
            ActionBarUI actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
            actionBar?.TryAssignConsumableFromItemDefinition(defForBar);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        ItemTooltipHoverRegistry.SetHovered(this, true);

        AutoBattleLootHighlight.ClearStorageSlot(_slotIndex);

        if (background)
            background.color = hoverColor;

        if (_tooltip == null || _def == null)
            return;

        ShowItemTooltip();
    }

    private void ShowItemTooltip()
    {
        if (_tooltip == null || _def == null)
            return;

        var flipper = _tooltip.GetComponent<FlipInsideBounds>();
        if (flipper)
        {
            flipper.SetPreferredSide(_preferredSide);
            if (_tooltipHeightRect)
            {
                flipper.SetMeasureRect(_tooltipHeightRect);
                flipper.SetHeightRect(_tooltipHeightRect);
            }
        }

        ItemDatabase db = ResolveItemDatabase();
        ItemRandomStatIdentification.GetTooltipRandomStatFlags(
            db,
            _def,
            _itemId,
            out bool maskUnrolledRandomStats,
            out bool showRandomStatPoolOptions);

        _tooltip.ShowAt(
            transform,
            _def,
            _amount,
            compact: false,
            maskUnrolledRandomStats: maskUnrolledRandomStats,
            showRandomStatPoolOptions: showRandomStatPoolOptions,
            itemId: _itemId);
    }

    public bool CanIdentifyStats()
    {
        if (!HasItemContext)
            return false;

        ItemDatabase db = ResolveItemDatabase();
        return ItemRandomStatIdentification.HasUnidentifiedRandomAffixes(db, _itemId);
    }

    public void PerformIdentifyStatsAction()
    {
        if (!CanIdentifyStats())
            return;

        if (_identifyRoutine != null)
            StopCoroutine(_identifyRoutine);

        _identifyRoutine = StartCoroutine(IdentifyStatsRoutine());
    }

    private IEnumerator IdentifyStatsRoutine()
    {
        UIPulseGlowOverlay glow = UIPulseGlowOverlay.Show(transform as RectTransform);
        yield return new WaitForSecondsRealtime(IdentifyGlowSeconds);
        glow?.Clear();
        _identifyRoutine = null;

        if (!CanIdentifyStats())
            yield break;

        ItemDatabase db = ResolveItemDatabase();
        if (!ItemRandomStatIdentification.TryIdentify(db, _itemId, out string activityMessage))
            yield break;

        GameLog.Add(activityMessage, ItemRandomStatIdentification.ActivityLogColor);

        _identifyPendingCached = false;
        RefreshIdentifyIcon(false);

        if (_isPointerOver)
            ShowItemTooltip();
    }

    public void PerformLookupAction()
    {
        if (string.IsNullOrWhiteSpace(_itemId))
            return;

        MainMenuWindowUI.Resolve()?.OpenDatabaseLookupItem(_itemId);
    }

    public void PerformEquipAction()
    {
        if (!TryWithdrawOneToInventory(out int invSlot))
            return;

        InventorySlotUI invSlotUi = FindInventorySlotUi(invSlot);
        invSlotUi?.PerformEquipAction();
    }

    public void PerformOpenAction()
    {
        if (!TryWithdrawOneToInventory(out int invSlot))
            return;

        InventorySlotUI invSlotUi = FindInventorySlotUi(invSlot);
        invSlotUi?.PerformOpenAction();
    }

    public void PerformEatAction()
    {
        if (!TryWithdrawOneToInventory(out int invSlot))
            return;

        InventorySlotUI invSlotUi = FindInventorySlotUi(invSlot);
        if (invSlotUi != null)
            invSlotUi.PerformEatAction();
    }

    public void PerformDropAction()
    {
        if (_storage == null || _slotIndex < 0)
            return;

        var slot = _storage.GetSlot(_slotIndex);
        if (slot.IsEmpty || string.IsNullOrWhiteSpace(slot.itemId) || slot.amount <= 0)
            return;

        string itemId = slot.itemId;
        int dropAmount = slot.amount;
        Sprite iconSprite = ResolveDropIcon(itemId);

        int removed = _storage.RemoveAmountAtSlot(_slotIndex, dropAmount);
        if (removed <= 0)
            return;

        PendingLootRecoveryStore.TrySpawnWorldDropOrEnqueue(itemId, removed, iconSprite);
        ItemGainPopupNotifier.NotifyLost(itemId, removed);
        _tooltip?.Hide();
    }

    public void ToggleAdditionalStatsHighlight()
    {
        if (string.IsNullOrWhiteSpace(_itemId))
            return;

        ItemTooltipHighlightState.Toggle(_itemId);
        RefreshAdvancedStatsTooltip();
    }

    private void RefreshAdvancedStatsTooltip()
    {
        if (_tooltip == null || _def == null)
            return;

        if (ItemTooltipHighlightState.IsEnabled(_itemId))
            ShowItemTooltip();
        else if (_isPointerOver)
            ShowItemTooltip();
        else
            _tooltip.Hide();
    }

    public void RefreshTooltipIfHovered()
    {
        if (!_isPointerOver || _tooltip == null || _def == null)
            return;

        ShowItemTooltip();
    }

    private void OpenContextMenu(PointerEventData eventData)
    {
        if (!HasItemContext)
            return;

        _tooltip?.Hide();
        ContextMenuUI.EnsureInstance().Show(
            transform as RectTransform,
            InventoryContextMenuBuilder.BuildForStorageSlot(this),
            _rootCanvas,
            _storagePanelRect,
            eventData != null ? eventData.position : (Vector2?)null,
            ItemGainPopupNotifier.ResolveDisplayLabel(_itemId, 1));
    }

    private bool TryWithdrawOneToInventory(out int inventorySlotUsed)
    {
        inventorySlotUsed = -1;
        if (_storage == null || _inventory == null || string.IsNullOrWhiteSpace(_itemId))
            return false;

        int targetSlot = FindBestInventorySlotForSingleWithdraw();
        if (targetSlot < 0)
            return false;

        int moved = _storage.TryMoveFromStorageToInventory(_inventory, _slotIndex, targetSlot, 1);
        if (moved != 1)
            return false;

        inventorySlotUsed = targetSlot;
        return true;
    }

    private int FindBestInventorySlotForSingleWithdraw()
    {
        if (_inventory == null || string.IsNullOrWhiteSpace(_itemId))
            return -1;

        int maxStack = _inventory.GetMaxStackForItem(_itemId);
        for (int i = 0; i < _inventory.SlotCount; i++)
        {
            var slot = _inventory.GetSlot(i);
            if (slot.IsEmpty)
                return i;

            if (slot.itemId == _itemId && slot.amount < maxStack)
                return i;
        }

        return -1;
    }

    private static InventorySlotUI FindInventorySlotUi(int inventorySlotIndex)
    {
        InventorySlotUI[] slots = FindObjectsByType<InventorySlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlotUI slotUi = slots[i];
            if (slotUi && slotUi.SlotIndex == inventorySlotIndex)
                return slotUi;
        }

        return null;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        ItemTooltipHoverRegistry.SetHovered(this, false);

        ApplySlotBackground();
        _tooltip?.Hide();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (MerchantClick.MerchantModeOpen && InventorySlotUI.InputUtil.CtrlHeld())
            return;

        // Ctrl+click withdraws to inventory (same as double-click); don't start a drag.
        if (InventorySlotUI.InputUtil.CtrlHeld())
            return;

        if (_def == null || string.IsNullOrEmpty(_itemId) || _rootCanvas == null || _storage == null) return;

        if (background)
            background.color = pressedColor;

        bool split = InventorySlotUI.InputUtil.ShiftHeld();

        int carriedAmount = _amount;
        if (split)
        {
            carriedAmount = _amount / 2;
            if (carriedAmount <= 0) split = false;
        }

        InventoryDragState.BeginDrag(_slotIndex, _itemId, carriedAmount, split, InventoryDragState.SourceKind.Storage, _storage);

        var storageUi = FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);
        storageUi?.BeginDragFromStoragePanel();

        CreateDragIcon();
        UpdateDragIconPosition(eventData);

        ApplySlotBackground();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIconRT == null) return;
        UpdateDragIconPosition(eventData);

        var storageUi = FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);
        storageUi?.UpdateStorageDragPassThrough(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        InventoryDragIconPool.Hide();
        _dragIconGO = null;
        _dragIconRT = null;
        _dragIconImage = null;

        if (TryDropStorageDragOnSlotUnderPointer(eventData))
            return;

        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
        {
            TryDropStorageDragToGround();
            return;
        }

        // Drop handlers on other UI (e.g. inventory slots) may run after EndDrag on the source in some
        // Unity versions. Clearing drag state immediately breaks those drops — defer cleanup one frame.
        StartCoroutine(EndStorageDragDeferred());
    }

    /// <summary>Raycast fallback when grid pass-through or event order prevented IDropHandler from firing.</summary>
    private bool TryDropStorageDragOnSlotUnderPointer(PointerEventData eventData)
    {
        if (!InventoryDragState.HasDrag || InventoryDragState.Source != InventoryDragState.SourceKind.Storage)
            return false;

        StorageSlotUI target = RaycastStorageSlotUnderPointer(eventData);
        if (target == null || target == this)
            return false;

        if (!target.TryAcceptStorageDragDrop())
            return false;

        EndStorageDragNow();
        return true;
    }

    private static StorageSlotUI RaycastStorageSlotUnderPointer(PointerEventData eventData)
    {
        if (EventSystem.current == null || eventData == null)
            return null;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hit = results[i].gameObject;
            if (!hit)
                continue;

            StorageSlotUI slot = hit.GetComponentInParent<StorageSlotUI>();
            if (slot)
                return slot;
        }

        return null;
    }

    /// <returns>True when a storage drag was applied to this slot.</returns>
    public bool TryAcceptStorageDragDrop()
    {
        if (_storage == null)
            return false;

        if (!InventoryDragState.HasDrag || InventoryDragState.Source != InventoryDragState.SourceKind.Storage)
            return false;

        if (InventoryDragState.StorageSource != null && InventoryDragState.StorageSource != _storage)
            return false;

        int fromStorage = InventoryDragState.FromSlotIndex;
        int toStorage = _slotIndex;

        if (fromStorage < 0 || toStorage < 0)
            return false;

        if (fromStorage == toStorage)
        {
            InventoryDragState.EndDrag();
            return true;
        }

        if (InventoryDragState.IsSplit)
        {
            if (_storage.MoveAmount(fromStorage, toStorage, InventoryDragState.CarriedAmount) > 0)
            {
                InventoryDragState.EndDrag();
                _tooltip?.Hide();
                return true;
            }

            return false;
        }

        var from = _storage.GetSlot(fromStorage);
        var to = _storage.GetSlot(toStorage);

        if (!from.IsEmpty && !to.IsEmpty && from.itemId == to.itemId)
        {
            if (_storage.MoveAmount(fromStorage, toStorage, from.amount) > 0)
            {
                InventoryDragState.EndDrag();
                _tooltip?.Hide();
                return true;
            }
        }

        if (_storage.SwapSlots(fromStorage, toStorage))
        {
            InventoryDragState.EndDrag();
            _tooltip?.Hide();
            return true;
        }

        return false;
    }

    private void TryDropStorageDragToGround()
    {
        if (_storage == null || !InventoryDragState.HasDrag || InventoryDragState.Source != InventoryDragState.SourceKind.Storage)
        {
            EndStorageDragNow();
            return;
        }

        int fromSlot = InventoryDragState.FromSlotIndex;
        string itemId = InventoryDragState.ItemId;
        int dropAmount = InventoryDragState.IsSplit ? InventoryDragState.CarriedAmount : _storage.GetSlot(fromSlot).amount;

        if (string.IsNullOrWhiteSpace(itemId) || dropAmount <= 0)
        {
            EndStorageDragNow();
            return;
        }

        Sprite iconSprite = ResolveDropIcon(itemId);
        int removed = _storage.RemoveAmountAtSlot(fromSlot, dropAmount);
        if (removed > 0)
        {
            PendingLootRecoveryStore.TrySpawnWorldDropOrEnqueue(itemId, removed, iconSprite);
            ItemGainPopupNotifier.NotifyLost(itemId, removed);
        }

        EndStorageDragNow();
    }

    private IEnumerator EndStorageDragDeferred()
    {
        yield return null;
        if (InventoryDragState.HasDrag && InventoryDragState.Source == InventoryDragState.SourceKind.Storage)
            InventoryDragState.EndDrag();

        EndStorageDragNow();
    }

    private void EndStorageDragNow()
    {
        if (InventoryDragState.HasDrag && InventoryDragState.Source == InventoryDragState.SourceKind.Storage)
            InventoryDragState.EndDrag();

        var storageUi = FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);
        storageUi?.EndDragFromStoragePanel();

        ApplySlotBackground();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_storage == null) return;

        if (!_inventory)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (EquipmentSlotUI.TryConsumeEquipDrag(out var fromSlotType, out var equipItemId, out var equipAmount))
        {
            if (_inventory == null) return;
            if (string.IsNullOrWhiteSpace(equipItemId)) return;

            var equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
            var toolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);
            if (equipment == null) return;

            var touchedStorage = new List<int>(4);
            int dep = _storage.TryDepositAmountFromExternal(equipItemId, equipAmount, touchedStorage);
            int remainder = equipAmount - dep;
            if (remainder > 0)
            {
                // Inventory.Add can partially succeed and still return false. Roll back any
                // partial inventory/storage deposits so the still-equipped stack is not duplicated.
                var touchedInv = new List<int>(4);
                int toInv = _inventory.AddPartial(equipItemId, remainder, notifyItemGainPopup: false, touchedSlotIndices: touchedInv);
                if (toInv < remainder)
                {
                    if (toInv > 0)
                        _inventory.RemoveAmountFromTouchedSlots(toInv, touchedInv);
                    if (dep > 0)
                        _storage.RemoveAmountFromTouchedSlots(dep, touchedStorage);
                    return;
                }
            }

            EquipmentSlotUI.UnequipDragSource(fromSlotType, equipment, toolbelt);
            _tooltip?.Hide();
            return;
        }

        if (InventoryDragState.HasDrag && InventoryDragState.Source == InventoryDragState.SourceKind.Inventory)
        {
            if (_inventory == null)
            {
                InventoryDragState.EndDrag();
                return;
            }

            int fromInv = InventoryDragState.FromSlotIndex;
            int amount = InventoryDragState.IsSplit
                ? InventoryDragState.CarriedAmount
                : _inventory.GetSlot(fromInv).amount;

            if (amount > 0)
            {
                int moved = _storage.TryMoveFromInventoryToStorage(_inventory, fromInv, _slotIndex, amount, null);
                if (moved > 0)
                {
                    InventoryDragState.EndDrag();
                    _tooltip?.Hide();
                    return;
                }
            }

            var invFrom = _inventory.GetSlot(fromInv);
            var stTo = _storage.GetSlot(_slotIndex);
            if (!invFrom.IsEmpty && !stTo.IsEmpty && invFrom.itemId != stTo.itemId)
            {
                _storage.SwapInventorySlotWithStorage(_inventory, fromInv, _slotIndex);
                InventoryDragState.EndDrag();
                _tooltip?.Hide();
                return;
            }

            InventoryDragState.EndDrag();
            return;
        }

        if (!InventoryDragState.HasDrag || InventoryDragState.Source != InventoryDragState.SourceKind.Storage)
            return;

        TryAcceptStorageDragDrop();
    }

    private void CreateDragIcon()
    {
        if (_rootCanvas == null)
            return;

        InventoryDragIconPool.Show(_rootCanvas, _def != null ? _def.icon : null);
        _dragIconGO = InventoryDragIconPool.GameObject;
        _dragIconRT = InventoryDragIconPool.RectTransform;
        _dragIconImage = InventoryDragIconPool.Image;
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (_dragIconRT == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        _dragIconRT.anchoredPosition = localPoint;
    }

    private void OnDisable()
    {
        _isPointerOver = false;
        _tooltip?.Hide();
        if (InventoryDragState.HasDrag &&
            InventoryDragState.Source == InventoryDragState.SourceKind.Storage &&
            InventoryDragState.FromSlotIndex == _slotIndex)
        {
            InventoryDragState.EndDrag();
            InventoryDragIconPool.Hide();
        }
        ApplySlotBackground();
    }
}
