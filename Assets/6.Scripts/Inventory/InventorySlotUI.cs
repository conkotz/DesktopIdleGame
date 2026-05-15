// ===============================
// InventorySlotUI.cs  (FULL)
// - Double click equips:
//     * MainHand items -> try toolbelt first (no duplicates), else main hand
//     * OffHand items -> off hand
//     * Food / potion -> matching action bar slot (replaces prior assignment)
// - Drag inventory -> equipment works (and won't drop to world while over UI)
// - Drag equipment -> inventory works via EquipmentSlotUI.TryConsumeEquipDrag
// - Still supports merchant ctrl-sell logic (kept from your version)
// ===============================

using System.Collections;
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
    private const float InventoryBorderThicknessMul = 0.65f;

    [Header("UI")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Outline rarityOutline;

    [Header("Slot Colours")]
    [SerializeField] private Color idleColor = new Color32(32, 34, 37, 255);      // #202225
    [SerializeField] private Color hoverColor = new Color32(198, 184, 158, 255);  // #C6B89E
    [SerializeField] private Color pressedColor = new Color32(217, 164, 65, 255); // #D9A441

    [Header("Idle auto-battle new loot")]
    [SerializeField] private Color autoBattleNewLootColor = new Color32(217, 190, 100, 255);

    [Header("Rarity Border")]
    [Tooltip("If false, no rarity border is shown.")]
    [SerializeField] private bool showRarityBorder = true;

    [SerializeField] private Color commonBorder = new Color32(140, 140, 140, 255);
    [SerializeField] private Color uncommonBorder = new Color32(80, 200, 120, 255);
    [SerializeField] private Color rareBorder = new Color32(80, 150, 255, 255);
    [SerializeField] private Color epicBorder = new Color32(190, 90, 255, 255);
    [SerializeField] private Color legendaryBorder = new Color32(255, 170, 40, 255);

    [Tooltip("Outline thickness in UI space (bigger = thicker).")]
    [SerializeField] private Vector2 rarityBorderThickness = new Vector2(4f, 4f);


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
        bool sameVisual =
            ReferenceEquals(_inventory, inventory) &&
            _slotIndex == slotIndex &&
            _amount == amount &&
            ReferenceEquals(_def, def) &&
            ItemIdEquals(_itemId, itemId);

        _tooltip = tooltip;
        _inventory = inventory;
        _slotIndex = slotIndex;
        _inventoryPanelRect = inventoryPanelRect;
        _rootCanvas = rootCanvas;

        if (sameVisual)
        {
            ApplySlotBackground();
            return;
        }

        _def = def;
        _amount = amount;
        _itemId = itemId;

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
            AutoBattleLootHighlight.ClearInventorySlot(slotIndex);

        ApplySlotBackground();
    }

    private static bool ItemIdEquals(string a, string b) =>
        string.IsNullOrEmpty(a) ? string.IsNullOrEmpty(b) : string.Equals(a, b, System.StringComparison.Ordinal);

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
        float mul = Mathf.Clamp(InventoryBorderThicknessMul, 0.1f, 1f);
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
        else if (AutoBattleLootHighlight.IsInventorySlotMarked(_slotIndex))
            background.color = autoBattleNewLootColor;
        else
            background.color = idleColor;
    }

    /// <summary>Updates the idle / new-loot tint without re-binding slot data.</summary>
    public void RefreshLootHighlightVisual() => ApplySlotBackground();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_slotIndex < 0)
            return;

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        // don't do click actions while dragging something
        if (InventoryDragState.HasDrag)
            return;

        float t = Time.unscaledTime;
        bool doubleClick = (t - _lastClickTime) <= doubleClickSeconds;
        _lastClickTime = t;

        if (StorageUI.IsOpen && (doubleClick || InputUtil.CtrlHeld()))
        {
            TryDoubleClickDepositToStorage();
            eventData.Use();
            return;
        }

        if (doubleClick)
        {
            TryDoubleClickEquipFromThisSlot();
            eventData.Use();
            return;
        }

        // Merchant mode + CTRL -> sell full stack
        if (!MerchantClick.MerchantModeOpen)
            return;

        if (!InputUtil.CtrlHeld())
            return;

        if (_inventory == null || wallet == null)
            return;

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty) return;
        string soldItemName = ResolveItemDisplayName(slot.itemId);

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

        Merchant saleMerchant = null;
        int stockAdded = 0;
        if (MerchantClick.TryGetActiveMerchant(out var activeMerchant))
        {
            saleMerchant = activeMerchant;
            activeMerchant.TryReplenishStockFromPlayerSale(slot.itemId, removed, out stockAdded);
        }

        var spawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
        if (spawner) spawner.ShowGoldGained(goldGained);

        SaleUndoManager.Instance?.RecordSale(slot.itemId, removed, goldGained, saleMerchant, stockAdded);
        GameLog.SoldItem(soldItemName, removed, goldGained);

        eventData.Use();
        _tooltip?.Hide();
    }

    private bool TryDoubleClickDepositToStorage()
    {
        if (_inventory == null) return false;

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty) return false;

        PlayerStorage storage = ResolvePlayerStorageForChest();
        if (storage == null) return false;

        int moved = storage.TryDepositAllFromInventorySlot(_inventory, _slotIndex);
        return moved > 0;
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

                if (def.UsesEquipmentTierGating && !def.MeetsEquipmentTierRequirement(SkillsManager.Instance))
                {
                    ShowEquipFailPopup(def, EquipSlot.MainHand);
                    return;
                }

                // must have empty tool slot or do nothing
                // remove one item first, then add; if fails, put back
                if (_inventory.RemoveAmountAtSlot(_slotIndex, 1) != 1) return;

                bool ok = toolbelt.TryAddToFirstEmpty(slot.itemId);
                if (!ok)
                {
                    // toolbelt full -> put back, and DO NOT equip mainhand
                    _inventory.Add(slot.itemId, 1, null, notifyItemGainPopup: false);
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
                _inventory.Add(prev, 1, null, notifyItemGainPopup: false);

            return;
        }

        // OFF HAND ITEMS (Armour OR dual-wield weapon)
            if (def.equipSlot == EquipSlot.OffHand ||
       (def.itemKind == ItemKind.Weapon &&
        def.weaponStats.handedness == Handedness.OneHanded &&
        def.weaponStats.canEquipInOffHand))
        {
            if (!equipment.CanEquip(slot.itemId, EquipSlot.OffHand))
            {
                ShowEquipFailPopup(def, EquipSlot.OffHand);
                return;
            }

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
                _inventory.Add(prev, Mathf.Max(1, prevAmount), null, notifyItemGainPopup: false);

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
                    _inventory.Add(beforeR1, 1, null, notifyItemGainPopup: false);

                return;
            }

            // Normal single-slot gear
            if (!TryCanEquipOrPopup(def, def.equipSlot)) return;

            if (_inventory.RemoveAmountAtSlot(_slotIndex, 1) != 1) return;

            string prev = equipment.GetEquippedItemId(def.equipSlot);
            equipment.EquipGear(def.equipSlot, slot.itemId);

            if (!string.IsNullOrWhiteSpace(prev) && prev != slot.itemId)
                _inventory.Add(prev, 1, null, notifyItemGainPopup: false);

            return;
        }

        if (def.IsOpenable && slot.amount > 0)
        {
            TryOpenItemAtSlot(def);
            return;
        }

        if ((def.IsFood || def.IsPotion) && slot.amount > 0)
        {
            ActionBarUI actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
            if (actionBar != null && actionBar.TryMoveConsumableFromInventorySlot(_slotIndex, slot.amount))
                return;
        }
    }

    /// <summary>
    /// Opens one of an <see cref="ConsumableType.Openable"/> item from this slot: rolls the loot table and grants
    /// the rewards into the inventory. Always consumes exactly 1 of the source item.
    ///
    /// Safety: only consumes the source if at least one reward will be added (an empty roll on an empty table is
    /// otherwise pointless and silently destroys the item). Rolls are independent — see
    /// <see cref="ItemDefinition.RollOpenableLoot"/>.
    ///
    /// Tracker: every successful reward is registered with <see cref="SessionTrackerData.RegisterLootGain"/> using
    /// the source item's <see cref="ItemDefinition.displayName"/> as the source label, so the Tracker window's
    /// gold section groups the rewards under the opened item (e.g. "Bird Nest → Feather x10, Leather x1").
    /// </summary>
    private void TryOpenItemAtSlot(ItemDefinition def)
    {
        if (def == null || _inventory == null)
            return;

        if (!def.IsOpenable)
            return;

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty || slot.amount <= 0)
            return;

        int required = Mathf.Max(1, def.OpenRequiredAmount);
        if (slot.amount < required)
        {
            GameLog.Add($"You need {required} {def.displayName} to open one ({slot.amount}/{required}).");
            return;
        }

        // Empty table or a roll that produced nothing: don't silently delete the player's items.
        var rolled = def.RollOpenableLoot();
        if (rolled == null || rolled.Count == 0)
        {
            GameLog.Add($"{def.displayName} contained nothing this time.");
            return;
        }

        if (_inventory.RemoveAmountAtSlot(_slotIndex, required) != required)
            return;

        // Use the opened item's display name as the loot source so the Tracker rolls everything from one open under
        // a single row (e.g. "Bird Nest"). Fall back to itemId only when the displayName field is empty.
        string trackerSource = !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : def.itemId;
        SessionTrackerData tracker = SessionTrackerData.EnsureInstance();

        for (int i = 0; i < rolled.Count; i++)
        {
            (string itemId, int amount) reward = rolled[i];
            if (string.IsNullOrWhiteSpace(reward.itemId) || reward.amount <= 0)
                continue;

            _inventory.Add(reward.itemId, reward.amount);
            tracker?.RegisterLootGain(trackerSource, reward.itemId, reward.amount);
        }

        _tooltip?.Hide();
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
            _inventory.Add(prev, 1, null, notifyItemGainPopup: false);

        return true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;

        AutoBattleLootHighlight.ClearInventorySlot(_slotIndex);

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

        ApplySlotBackground();

        _tooltip?.Hide();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_slotIndex < 0)
            return;

        // Ctrl+click is used for sell (merchant) and stash (storage); don't start a drag.
        if (InputUtil.CtrlHeld() &&
            (MerchantClick.MerchantModeOpen || StorageUI.IsOpen))
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

        InventoryDragState.BeginDrag(_slotIndex, _itemId, carriedAmount, split, InventoryDragState.SourceKind.Inventory);

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
        InventoryDragIconPool.Hide();
        _dragIconGO = null;
        _dragIconRT = null;
        _dragIconImage = null;

        // If we dropped onto ANY UI element, do NOT drop to world.
        // Defer clearing drag state: OnDrop on the target may run after EndDrag on the source; clearing
        // here first breaks cross-panel drops (e.g. inventory -> storage).
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            StartCoroutine(DeferredEndInventoryDragOverUi());
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
                    ApplySlotBackground();
                    return;
                }

                int removed = _inventory.RemoveAmountAtSlot(fromSlot, dropAmount);

                if (removed > 0)
                {
                    var def = _inventory.GetItemDef(itemId);
                    Sprite iconSprite = def ? def.icon : null;

                    if (DropManager.Instance != null)
                        DropManager.Instance.Spawn(itemId, removed, iconSprite);
                    ItemGainPopupNotifier.NotifyLost(itemId, removed);
                }
            }
        }

        InventoryDragState.EndDrag();
        ApplySlotBackground();
    }

    private IEnumerator DeferredEndInventoryDragOverUi()
    {
        yield return null;
        if (InventoryDragState.HasDrag && InventoryDragState.Source == InventoryDragState.SourceKind.Inventory)
            InventoryDragState.EndDrag();
        ApplySlotBackground();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_slotIndex < 0)
            return;

        // Storage chest slot -> inventory slot
        if (InventoryDragState.HasDrag && InventoryDragState.Source == InventoryDragState.SourceKind.Storage)
        {
            PlayerStorage storage = InventoryDragState.StorageSource != null
                ? InventoryDragState.StorageSource
                : FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
            if (storage != null && _inventory != null)
            {
                int fromStorage = InventoryDragState.FromSlotIndex;
                int amount = InventoryDragState.IsSplit
                    ? InventoryDragState.CarriedAmount
                    : storage.GetSlot(fromStorage).amount;

                if (amount > 0)
                {
                    int moved = storage.TryMoveFromStorageToInventory(_inventory, fromStorage, _slotIndex, amount, null);
                    if (moved > 0)
                    {
                        InventoryDragState.EndDrag();
                        _tooltip?.Hide();
                        return;
                    }
                }

                var sFrom = storage.GetSlot(fromStorage);
                var sTo = _inventory.GetSlot(_slotIndex);
                if (!sFrom.IsEmpty && !sTo.IsEmpty && sFrom.itemId != sTo.itemId)
                {
                    storage.SwapInventorySlotWithStorage(_inventory, _slotIndex, fromStorage);
                    InventoryDragState.EndDrag();
                    _tooltip?.Hide();
                    return;
                }
            }

            InventoryDragState.EndDrag();
            return;
        }

        // ✅ Dropping from Equipment/Toolbelt -> Inventory (consumes drag state from EquipmentSlotUI)
        if (EquipmentSlotUI.TryConsumeEquipDrag(out var fromSlotType, out var equipItemId, out var equipAmount))
        {
            if (_inventory == null) return;
            if (string.IsNullOrWhiteSpace(equipItemId)) return;

            bool ok = _inventory.TryPlaceExternalAtSlot(equipItemId, equipAmount, _slotIndex, null, notifyItemGainPopup: false);
            if (!ok)
            {
                // No room / can't place on this slot -> keep equipped (drag state already consumed)
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

        if (IsDraggedEnhancementScroll() && ShouldTreatDropAsEnhancementTarget(toSlot))
        {
            if (!IsDropReleaseConfirmed(eventData))
                return;

            bool attempted = EnhancementUpgradeService.TryUseScrollOnInventorySlot(
                _inventory,
                fromSlot,
                toSlot,
                out bool success);

            if (attempted)
            {
                EnhancementFlashUI.Flash(success);
                InventoryDragState.EndDrag();
                _tooltip?.Hide();
                eventData.Use();
                return;
            }

            // Invalid scroll target: consume the drop event so the scroll snaps back instead of swapping slots.
            InventoryDragState.EndDrag();
            eventData.Use();
            return;
        }

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

    private bool IsDraggedEnhancementScroll()
    {
        if (_inventory == null || !InventoryDragState.HasDrag)
            return false;
        if (InventoryDragState.Source != InventoryDragState.SourceKind.Inventory)
            return false;

        ItemDefinition draggedDef = _inventory.GetItemDef(InventoryDragState.ItemId);
        return draggedDef && draggedDef.itemKind == ItemKind.EnhancementScroll;
    }

    private static bool IsDropReleaseConfirmed(PointerEventData eventData)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
            return mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed;
#endif
        return eventData == null ||
               eventData.button != PointerEventData.InputButton.Left ||
               Input.GetMouseButtonUp(0) ||
               !Input.GetMouseButton(0);
    }

    private bool ShouldTreatDropAsEnhancementTarget(int targetSlotIndex)
    {
        if (_inventory == null)
            return false;

        var target = _inventory.GetSlot(targetSlotIndex);
        if (target.IsEmpty)
            return false;

        ItemDefinition targetDef = _inventory.GetItemDef(target.itemId);
        return targetDef && targetDef.itemKind != ItemKind.EnhancementScroll;
    }

    private string ResolveItemDisplayName(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "";

        ItemDefinition def = _inventory ? _inventory.GetItemDef(itemId) : null;
        return def && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : itemId;
    }

    private void ReturnOrDrop(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || _inventory == null || amount <= 0) return;

        bool ok = _inventory.Add(itemId, amount, null, notifyItemGainPopup: false);
        if (ok) return;

        var def = _inventory.GetItemDef(itemId);
        if (DropManager.Instance != null)
            DropManager.Instance.Spawn(itemId, amount, def ? def.icon : null);
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

    private void ShowEquipFailPopup(ItemDefinition def, EquipSlot slot)
    {
        if (def == null || player == null) return;

        if (def.UsesEquipmentTierGating && !def.MeetsEquipmentTierRequirement(SkillsManager.Instance))
        {
            player.SendMessage(
                "ShowPopup",
                def.BuildEquipmentTierBlockedMessage(),
                SendMessageOptions.DontRequireReceiver
            );
            return;
        }

        if (slot == EquipSlot.MainHand && def.IsWeapon && def.RequiresOffhandSupport)
        {
            player.SendMessage(
                "ShowPopup",
                $"Cannot equip: requires {def.RequiredSupportType} in offhand.",
                SendMessageOptions.DontRequireReceiver
            );
            return;
        }

        if (slot == EquipSlot.OffHand && def.IsCombatSupport)
        {
            player.SendMessage(
                "ShowPopup",
                $"Cannot equip: requires compatible main-hand weapon for {def.SupportType}.",
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

    /// <summary>Use the same PlayerStorage instance as <see cref="StorageGridUI"/> when available so slot indices match the UI.</summary>
    private static PlayerStorage ResolvePlayerStorageForChest()
    {
        var grid = FindFirstObjectByType<StorageGridUI>(FindObjectsInactive.Include);
        if (grid != null && grid.PlayerStorage != null)
            return grid.PlayerStorage;

        return FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
    }
    
}

public static class InventoryDragState
{
    public enum SourceKind { None, Inventory, Storage }

    public static bool HasDrag { get; private set; }

    public static SourceKind Source { get; private set; }

    public static int FromSlotIndex { get; private set; } = -1;
    public static string ItemId { get; private set; }
    public static int CarriedAmount { get; private set; }
    public static bool IsSplit { get; private set; }

    /// <summary>Set when <see cref="Source"/> is <see cref="SourceKind.Storage"/> so drops use the same backing store as the grid.</summary>
    public static PlayerStorage StorageSource { get; private set; }

    public static void BeginDrag(int fromSlotIndex, string itemId, int carriedAmount, bool isSplit, SourceKind source = SourceKind.Inventory, PlayerStorage storageSource = null)
    {
        FromSlotIndex = fromSlotIndex;
        ItemId = itemId;
        CarriedAmount = carriedAmount;
        IsSplit = isSplit;
        Source = source;
        StorageSource = source == SourceKind.Storage ? storageSource : null;
        HasDrag = fromSlotIndex >= 0 && !string.IsNullOrEmpty(itemId) && carriedAmount > 0;
    }

    public static void EndDrag()
    {
        HasDrag = false;
        Source = SourceKind.None;
        FromSlotIndex = -1;
        ItemId = null;
        CarriedAmount = 0;
        IsSplit = false;
        StorageSource = null;
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