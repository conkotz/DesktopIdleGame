// ===============================
// InventorySlotUI.cs  (FULL)
// - Double click equips:
//     * MainHand items -> try toolbelt first (no duplicates), else main hand
//     * OffHand items -> off hand
// - Drag inventory -> equipment works (and won't drop to world while over UI)
// - Drag equipment -> inventory works via EquipmentSlotUI.TryConsumeEquipDrag
// - Still supports merchant ctrl-sell logic (kept from your version)
// ===============================

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler
{
    [Header("UI")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;

    [Header("Slot Colours")]
    [SerializeField] private Color idleColor = new Color32(30, 34, 42, 255);
    [SerializeField] private Color hoverColor = new Color32(42, 48, 58, 255);
    [SerializeField] private Color pressedColor = new Color32(58, 66, 80, 255);

    [Header("Selling")]
    [Tooltip("If empty, it will auto-find at runtime.")]
    [SerializeField] private CurrencyWallet wallet;

    [Header("Equipment")]
    [Tooltip("If empty, it will auto-find at runtime.")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private ToolbeltManager toolbelt;
    [SerializeField] private PlayerController player;

    [Header("Double Click Equip")]
    [SerializeField] private float doubleClickSeconds = 0.30f;
    private float _lastClickTime;

    private bool _isPointerOver;

    private ItemDefinition _def;
    private SharedTooltipUI _tooltip;

    private RectTransform _tooltipAnchor;
    private RectTransform _tooltipHeightRect;
    private FlipInsideBounds.PreferredSide _preferredSide = FlipInsideBounds.PreferredSide.Left;


    private Inventory _inventory;
    private RectTransform _inventoryPanelRect;
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

        if (icon) icon.raycastTarget = false;
        if (countText) countText.raycastTarget = false;

        if (!wallet)
            wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);

        if (!toolbelt)
            toolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);

        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    }

    public static class InputUtil
    {
        public static bool ShiftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

        public static bool CtrlHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
        }
    }

    public void Bind(
    ItemDefinition def,
    int amount,
    string itemId,
    SharedTooltipUI tooltip,
    Inventory inventory,
    int slotIndex,
    RectTransform inventoryPanelRect,
    Canvas rootCanvas)
    {
        _def = def;
        _amount = amount;
        _itemId = itemId;
        _tooltip = tooltip;

        _inventory = inventory;
        _slotIndex = slotIndex;
        _inventoryPanelRect = inventoryPanelRect;
        _rootCanvas = rootCanvas;

        if (icon)
        {
            bool hasIcon = def != null && def.icon != null;
            icon.enabled = hasIcon;
            icon.sprite = hasIcon ? def.icon : null;
            icon.preserveAspect = true;
        }

        if (countText)
            countText.text = (def != null && amount > 1) ? amount.ToString() : "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        // don't do click actions while dragging something
        if (InventoryDragState.HasDrag)
            return;

        // Double click to Equip/Toolbelt
        float t = Time.unscaledTime;
        bool doubleClick = (t - _lastClickTime) <= doubleClickSeconds;
        _lastClickTime = t;

        if (doubleClick)
        {
            TryDoubleClickEquipFromThisSlot();
            eventData.Use();
            return;
        }

        // Existing: Merchant mode + CTRL -> sell full stack
        if (!MerchantClick.MerchantModeOpen)
            return;

        if (!InputUtil.CtrlHeld())
            return;

        if (_inventory == null || wallet == null)
            return;

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty) return;

        if (MerchantClick.TryGetActiveMerchant(out var activeMerchantRef) &&
            activeMerchantRef != null &&
            activeMerchantRef.TryRejectUnsellableItemWithPopup(slot.itemId))
        {
            eventData.Use();
            _tooltip?.Hide();
            return;
        }

        int valuePerItem = _inventory.GetItemValue(slot.itemId);
        if (valuePerItem <= 0) return;

        int removed = _inventory.RemoveAmountAtSlot(_slotIndex, slot.amount);
        if (removed <= 0) return;

        int goldGained = valuePerItem * removed;
        wallet.AddGold(goldGained);

        string merchantId = null;
        int stockAdded = 0;
        if (MerchantClick.TryGetActiveMerchant(out var activeMerchant))
        {
            activeMerchant.TryReplenishStockFromPlayerSale(slot.itemId, removed, out stockAdded);
            merchantId = activeMerchant.MerchantId;
        }

        var spawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
        if (spawner) spawner.ShowGoldGained(goldGained);

        SaleUndoManager.Instance?.RecordSale(slot.itemId, removed, goldGained, merchantId, stockAdded);

        eventData.Use();
        _tooltip?.Hide();
    }

    private void TryDoubleClickEquipFromThisSlot()
    {
        if (_inventory == null || equipment == null) return;

        if (!toolbelt)
            toolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty) return;

        var def = _inventory.GetItemDef(slot.itemId);
        if (!def) return;

        // MAIN HAND ITEMS (weapon or tool)
        if (def.equipSlot == EquipSlot.MainHand)
        {
            // TOOL = Pickaxe/Axe/Rod ONLY -> toolbelt ONLY
            bool isTool =
                def.handVisualKey != ToolKey.None &&
                def.handVisualKey != ToolKey.Weapon;

            if (isTool)
            {
                if (toolbelt == null) return;
                if (toolbelt.Contains(slot.itemId)) return; // no duplicates

                // must have empty tool slot or do nothing
                // remove one item first, then add; if fails, put back
                if (_inventory.RemoveAmountAtSlot(_slotIndex, 1) != 1) return;

                bool ok = toolbelt.TryAddToFirstEmpty(slot.itemId);
                if (!ok)
                {
                    // toolbelt full -> put back, and DO NOT equip mainhand
                    _inventory.Add(slot.itemId, 1);
                }
                return;
            }

            // WEAPON ONLY -> main hand ONLY
            if (def.handVisualKey != ToolKey.Weapon)
                return;

            // Toggle weapon equip
            // If the same weapon is already equipped, do nothing (no toggle-off)
            if (equipment.MainHandItemId == slot.itemId)
                return;

            if (!TryCanEquipOrPopup(def, EquipSlot.MainHand))
                return;

            if (_inventory.RemoveAmountAtSlot(_slotIndex, 1) != 1) return;

            string prev = equipment.MainHandItemId;
            equipment.EquipMainHand(slot.itemId);

            if (!string.IsNullOrWhiteSpace(prev) && prev != slot.itemId)
                _inventory.Add(prev, 1);

            return;
        }

        // OFF HAND ITEMS (Armour OR dual-wield weapon)
            if (def.equipSlot == EquipSlot.OffHand ||
       (def.itemKind == ItemKind.Weapon &&
        def.weaponStats.handedness == Handedness.OneHanded &&
        def.weaponStats.canEquipInOffHand))
        {
            if (!equipment.CanEquip(slot.itemId, EquipSlot.OffHand))
                return;

            // Same support item already equipped -> add to stack
            if (def.IsCombatSupport &&
                equipment.OffHandItemId == slot.itemId)
            {
                int addAmount = slot.amount;

                if (_inventory.RemoveAmountAtSlot(_slotIndex, addAmount) != addAmount)
                    return;

                int newAmount = equipment.OffHandStackAmount + addAmount;
                equipment.EquipOffHand(slot.itemId, newAmount);
                return;
            }

            // Same non-support item already equipped -> do nothing
            if (!def.IsCombatSupport && equipment.OffHandItemId == slot.itemId)
                return;

            int equipAmount = def.IsCombatSupport ? slot.amount : 1;

            if (_inventory.RemoveAmountAtSlot(_slotIndex, equipAmount) != equipAmount)
                return;

            string prev = equipment.OffHandItemId;
            int prevAmount = equipment.OffHandStackAmount;

            equipment.EquipOffHand(slot.itemId, equipAmount);

            if (!string.IsNullOrWhiteSpace(prev) && prev != slot.itemId)
                _inventory.Add(prev, Mathf.Max(1, prevAmount));

            return;
        }

        if (def.equipSlot != EquipSlot.None)
        {
            // Rings are special (2 slots)
            if (def.equipSlot == EquipSlot.Ring)
            {
                if (!equipment.CanEquip(slot.itemId, EquipSlot.Ring)) return;

                if (_inventory.RemoveAmountAtSlot(_slotIndex, 1) != 1) return;

                // if we replaced an existing ring, return it
                // (we handle it by checking which slot got replaced)
                string beforeR1 = equipment.GetEquippedItemId(EquipSlot.Ring, 0);
                string beforeR2 = equipment.GetEquippedItemId(EquipSlot.Ring, 1);

                TryAutoEquipRing(slot.itemId);

                string afterR1 = equipment.GetEquippedItemId(EquipSlot.Ring, 0);
                string afterR2 = equipment.GetEquippedItemId(EquipSlot.Ring, 1);

                // If something got replaced, add it back
                // (simple approach: if both were full, we replaced ring1)
                if (!string.IsNullOrWhiteSpace(beforeR1) && beforeR1 != afterR1 && beforeR1 != slot.itemId)
                    _inventory.Add(beforeR1, 1);

                return;
            }

            // Normal single-slot gear
            if (!TryCanEquipOrPopup(def, def.equipSlot)) return;

            if (_inventory.RemoveAmountAtSlot(_slotIndex, 1) != 1) return;

            string prev = equipment.GetEquippedItemId(def.equipSlot);
            equipment.EquipGear(def.equipSlot, slot.itemId);

            if (!string.IsNullOrWhiteSpace(prev) && prev != slot.itemId)
                _inventory.Add(prev, 1);

            return;
        }
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

    private bool TryAutoEquipRing(string itemId)
    {
        // Prefer empty ring slots
        string r1 = equipment.GetEquippedItemId(EquipSlot.Ring, 0);
        string r2 = equipment.GetEquippedItemId(EquipSlot.Ring, 1);

        if (string.IsNullOrWhiteSpace(r1))
        {
            equipment.EquipGear(EquipSlot.Ring, itemId, 0);
            return true;
        }

        if (string.IsNullOrWhiteSpace(r2))
        {
            equipment.EquipGear(EquipSlot.Ring, itemId, 1);
            return true;
        }

        // both full -> replace ring1 (your choice)
        equipment.EquipGear(EquipSlot.Ring, itemId, 0);
        return true;
    }

    private bool TryEquipExactGearSlot(EquipSlot slot, string itemId)
    {
        if (!equipment.CanEquip(itemId, slot)) return false;

        string prev = equipment.GetEquippedItemId(slot);

        equipment.EquipGear(slot, itemId);

        if (!string.IsNullOrWhiteSpace(prev) && prev != itemId)
            _inventory.Add(prev, 1);

        return true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;

        if (background)
            background.color = hoverColor;

        if (_tooltip == null || _def == null)
            return;

        var flipper = _tooltip.GetComponent<FlipInsideBounds>();
        if (flipper)
        {
            flipper.SetPreferredSide(_preferredSide);

            // Use the inventory window/panel for flip decision and vertical clamping
            if (_tooltipHeightRect)
            {
                flipper.SetMeasureRect(_tooltipHeightRect);
                flipper.SetHeightRect(_tooltipHeightRect);
            }
        }

        // Use THIS slot as the anchor, so tooltip appears beside hovered slot
        _tooltip.ShowAt(transform, _def, _amount, compact: false);
    }



    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;

        if (background)
            background.color = idleColor;

        _tooltip?.Hide();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // IMPORTANT: if merchant mode is open and ctrl held, don't allow drag.
        if (MerchantClick.MerchantModeOpen && InputUtil.CtrlHeld())
            return;

        if (_def == null || string.IsNullOrEmpty(_itemId) || _rootCanvas == null || _inventory == null) return;

        if (background)
            background.color = pressedColor;

        bool split = InputUtil.ShiftHeld();

        int carriedAmount = _amount;
        if (split)
        {
            carriedAmount = _amount / 2;
            if (carriedAmount <= 0) split = false;
        }

        InventoryDragState.BeginDrag(_slotIndex, _itemId, carriedAmount, split);

        CreateDragIcon();
        UpdateDragIconPosition(eventData);

        if (background)
            background.color = _isPointerOver ? hoverColor : idleColor;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIconRT == null) return;
        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragIconGO) Destroy(_dragIconGO);

        // If we dropped onto ANY UI element, do NOT drop to world.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            InventoryDragState.EndDrag();
            return;
        }

        // Otherwise, if outside inventory panel rect -> drop to world
        if (_inventory != null && _inventoryPanelRect != null && InventoryDragState.HasDrag)
        {
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(
                _inventoryPanelRect,
                eventData.position,
                eventData.pressEventCamera
            );

            if (!inside)
            {
                int fromSlot = InventoryDragState.FromSlotIndex;
                string itemId = InventoryDragState.ItemId;
                int dropAmount = InventoryDragState.IsSplit ? InventoryDragState.CarriedAmount : _inventory.GetSlot(fromSlot).amount;

                if (string.IsNullOrWhiteSpace(itemId) || dropAmount <= 0)
                {
                    InventoryDragState.EndDrag();
                    return;
                }

                int removed = _inventory.RemoveAmountAtSlot(fromSlot, dropAmount);

                if (removed > 0)
                {
                    var def = _inventory.GetItemDef(itemId);
                    Sprite iconSprite = def ? def.icon : null;

                    if (DropManager.Instance != null)
                        DropManager.Instance.Spawn(itemId, removed, iconSprite);
                }
            }
        }

        InventoryDragState.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        // ✅ Dropping from Equipment/Toolbelt -> Inventory (consumes drag state from EquipmentSlotUI)
        if (EquipmentSlotUI.TryConsumeEquipDrag(out var fromSlotType, out var equipItemId, out var equipAmount))
        {
            if (_inventory == null) return;
            if (string.IsNullOrWhiteSpace(equipItemId)) return;

            bool ok = _inventory.Add(equipItemId, equipAmount);
            if (!ok)
            {
                // Inventory full -> keep equipped (we consumed the drag state, so just do nothing)
                return;
            }

            // Clear the source slot (FIX: include ALL gear slots)
            switch (fromSlotType)
            {
                case EquipmentUISlotType.MainHand:
                    equipment?.UnequipMainHand();
                    break;

                case EquipmentUISlotType.OffHand:
                    equipment?.UnequipOffHand();
                    break;

                case EquipmentUISlotType.Helmet:
                    equipment?.Unequip(EquipSlot.Helmet);
                    break;

                case EquipmentUISlotType.Body:
                    equipment?.Unequip(EquipSlot.Body);
                    break;

                case EquipmentUISlotType.Boots:
                    equipment?.Unequip(EquipSlot.Boots);
                    break;

                case EquipmentUISlotType.Trinket:
                    equipment?.Unequip(EquipSlot.Trinket);
                    break;

                case EquipmentUISlotType.Pendant:
                    equipment?.Unequip(EquipSlot.Pendant);
                    break;

                case EquipmentUISlotType.Ring1:
                    equipment?.Unequip(EquipSlot.Ring, 0);
                    break;

                case EquipmentUISlotType.Ring2:
                    equipment?.Unequip(EquipSlot.Ring, 1);
                    break;

                case EquipmentUISlotType.Toolbelt0:
                    toolbelt?.Clear(0);
                    break;
                case EquipmentUISlotType.Toolbelt1:
                    toolbelt?.Clear(1);
                    break;
                case EquipmentUISlotType.Toolbelt2:
                    toolbelt?.Clear(2);
                    break;
                case EquipmentUISlotType.Toolbelt3:
                    toolbelt?.Clear(3);
                    break;
            }

            _tooltip?.Hide();
            return;
        }

        // Normal inventory drop behavior (swap/stack)
        if (_inventory == null) return;
        if (!InventoryDragState.HasDrag) return;

        int fromSlot = InventoryDragState.FromSlotIndex;
        int toSlot = _slotIndex;

        if (fromSlot < 0 || toSlot < 0) return;
        if (fromSlot == toSlot) return;

        if (InventoryDragState.IsSplit)
        {
            _inventory.MoveAmount(fromSlot, toSlot, InventoryDragState.CarriedAmount);
            return;
        }

        var from = _inventory.GetSlot(fromSlot);
        var to = _inventory.GetSlot(toSlot);

        if (!from.IsEmpty && !to.IsEmpty && from.itemId == to.itemId)
        {
            int moved = _inventory.MoveAmount(fromSlot, toSlot, from.amount);
            if (moved > 0) return;
        }

        _inventory.SwapSlots(fromSlot, toSlot);
    }

    private void ReturnOrDrop(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || _inventory == null || amount <= 0) return;

        bool ok = _inventory.Add(itemId, amount);
        if (ok) return;

        var def = _inventory.GetItemDef(itemId);
        if (DropManager.Instance != null)
            DropManager.Instance.Spawn(itemId, amount, def ? def.icon : null);
    }

    private void CreateDragIcon()
    {
        _dragIconGO = new GameObject("DragIcon");
        _dragIconGO.transform.SetParent(_rootCanvas.transform, false);

        _dragIconRT = _dragIconGO.AddComponent<RectTransform>();
        _dragIconImage = _dragIconGO.AddComponent<Image>();
        _dragIconImage.raycastTarget = false;
        _dragIconImage.sprite = _def.icon;
        _dragIconImage.preserveAspect = true;
        _dragIconRT.sizeDelta = new Vector2(48f, 48f);
    }

    private void ShowEquipFailPopup(ItemDefinition def, EquipSlot slot)
    {
        if (def == null || player == null) return;

        if (slot == EquipSlot.MainHand && def.IsWeapon && def.RequiresOffhandSupport)
        {
            player.SendMessage(
                "ShowPopup",
                $"Requires {def.RequiredSupportType} in offhand.",
                SendMessageOptions.DontRequireReceiver
            );
            return;
        }

        if (slot == EquipSlot.OffHand && def.IsCombatSupport)
        {
            player.SendMessage(
                "ShowPopup",
                $"Requires compatible main-hand weapon for {def.SupportType}.",
                SendMessageOptions.DontRequireReceiver
            );
            return;
        }

        player.SendMessage(
            "ShowPopup",
            "Cannot equip that item there.",
            SendMessageOptions.DontRequireReceiver
        );
    }

    private bool TryCanEquipOrPopup(ItemDefinition def, EquipSlot slot)
    {
        if (def == null || equipment == null) return false;

        bool canEquip = equipment.CanEquip(def.itemId, slot);
        if (!canEquip)
            ShowEquipFailPopup(def, slot);

        return canEquip;
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
    
}

public static class InventoryDragState
{
    public static bool HasDrag { get; private set; }

    public static int FromSlotIndex { get; private set; } = -1;
    public static string ItemId { get; private set; }
    public static int CarriedAmount { get; private set; }
    public static bool IsSplit { get; private set; }

    public static void BeginDrag(int fromSlotIndex, string itemId, int carriedAmount, bool isSplit)
    {
        FromSlotIndex = fromSlotIndex;
        ItemId = itemId;
        CarriedAmount = carriedAmount;
        IsSplit = isSplit;
        HasDrag = fromSlotIndex >= 0 && !string.IsNullOrEmpty(itemId) && carriedAmount > 0;
    }

    public static void EndDrag()
    {
        HasDrag = false;
        FromSlotIndex = -1;
        ItemId = null;
        CarriedAmount = 0;
        IsSplit = false;
    }

    private static EquipSlot MapUiToEquipSlot(EquipmentUISlotType uiSlot)
    {
        return uiSlot switch
        {
            EquipmentUISlotType.MainHand => EquipSlot.MainHand,
            EquipmentUISlotType.OffHand => EquipSlot.OffHand,

            EquipmentUISlotType.Helmet => EquipSlot.Helmet,
            EquipmentUISlotType.Body => EquipSlot.Body,
            EquipmentUISlotType.Boots => EquipSlot.Boots,

            EquipmentUISlotType.Trinket => EquipSlot.Trinket,
            EquipmentUISlotType.Pendant => EquipSlot.Pendant,

            // ✅ Rings are now ONE slot type in ItemDefinition
            EquipmentUISlotType.Ring1 => EquipSlot.Ring,
            EquipmentUISlotType.Ring2 => EquipSlot.Ring,

            _ => EquipSlot.None
        };
    }
}