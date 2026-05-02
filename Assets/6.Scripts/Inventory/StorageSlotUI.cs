using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StorageSlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler
{
    private const float BorderThicknessMul = 0.65f;

    [Header("UI")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
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

        if (icon) icon.raycastTarget = false;
        if (countText) countText.raycastTarget = false;

        foreach (var mg in GetComponentsInChildren<MaskableGraphic>(true))
            mg.maskable = true;

        if (!_inventory)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
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

        ApplySlotBackground();
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
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (InventoryDragState.HasDrag)
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

        _storage.TryWithdrawAllToInventory(_inventory, _slotIndex);
        _tooltip?.Hide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;

        AutoBattleLootHighlight.ClearStorageSlot(_slotIndex);

        if (background)
            background.color = hoverColor;

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

        _tooltip.ShowAt(transform, _def, _amount, compact: false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;

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
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragIconGO) Destroy(_dragIconGO);

        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
        {
            TryDropStorageDragToGround();
            return;
        }

        // Drop handlers on other UI (e.g. inventory slots) may run after EndDrag on the source in some
        // Unity versions. Clearing drag state immediately breaks those drops — defer cleanup one frame.
        StartCoroutine(EndStorageDragDeferred());
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

        int removed = _storage.RemoveAmountAtSlot(fromSlot, dropAmount);
        if (removed > 0)
        {
            Sprite iconSprite = _def ? _def.icon : null;
            if (DropManager.Instance != null)
                DropManager.Instance.Spawn(itemId, removed, iconSprite);
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

            int dep = _storage.TryDepositAmountFromExternal(equipItemId, equipAmount);
            int remainder = equipAmount - dep;
            if (remainder > 0)
            {
                if (!_inventory.Add(equipItemId, remainder, null, notifyItemGainPopup: false))
                {
                    if (dep > 0)
                        _storage.RemoveItemAmountAcrossSlots(equipItemId, dep);
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

        int fromStorage = InventoryDragState.FromSlotIndex;
        int toStorage = _slotIndex;

        if (fromStorage < 0 || toStorage < 0) return;
        if (fromStorage == toStorage)
        {
            InventoryDragState.EndDrag();
            return;
        }

        if (InventoryDragState.IsSplit)
        {
            _storage.MoveAmount(fromStorage, toStorage, InventoryDragState.CarriedAmount);
            InventoryDragState.EndDrag();
            return;
        }

        var from = _storage.GetSlot(fromStorage);
        var to = _storage.GetSlot(toStorage);

        if (!from.IsEmpty && !to.IsEmpty && from.itemId == to.itemId)
        {
            int moved = _storage.MoveAmount(fromStorage, toStorage, from.amount);
            if (moved > 0)
            {
                InventoryDragState.EndDrag();
                return;
            }
        }

        _storage.SwapSlots(fromStorage, toStorage);
        InventoryDragState.EndDrag();
    }

    private void CreateDragIcon()
    {
        _dragIconGO = new GameObject("StorageDragIcon");
        _dragIconGO.transform.SetParent(_rootCanvas.transform, false);

        _dragIconRT = _dragIconGO.AddComponent<RectTransform>();
        _dragIconImage = _dragIconGO.AddComponent<Image>();
        _dragIconImage.raycastTarget = false;
        _dragIconImage.sprite = _def.icon;
        _dragIconImage.preserveAspect = true;
        _dragIconRT.sizeDelta = new Vector2(48f, 48f);
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
        ApplySlotBackground();
    }
}
