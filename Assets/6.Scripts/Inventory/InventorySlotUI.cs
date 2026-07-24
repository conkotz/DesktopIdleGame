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
    IDropHandler,
    IItemTooltipHoverSource
{
    private const float InventoryBorderThicknessMul = 0.65f;
    private const float IdentifyGlowSeconds = 1.1f;

    [Header("UI")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Image identifyIcon;
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

    private System.Action<int> _upgradeViewSelectCallback;
    private bool _upgradeViewActive;
    private bool _upgradeViewCanSelect;
    private bool _upgradeDragging;
    private RectTransform _upgradeDropTarget;
    private CanvasGroup _upgradeCanvasGroup;
    private bool _upgradeSelected;
    private bool _identifyPendingCached;
    private Coroutine _identifyRoutine;

    private static readonly Color UpgradeDimIconColor = new(0.45f, 0.45f, 0.45f, 0.55f);

    public int SlotIndex => _slotIndex;

    public bool HasItemContext => _def != null && _amount > 0 && !string.IsNullOrEmpty(_itemId);
    public ItemDefinition ContextDefinition => _def;
    public string ContextItemId => _itemId;

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

        if (!wallet)
            wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);

        if (!toolbelt)
            toolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);

        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        AutoBattleLootHighlight.RegisterInventorySlotUi(this);
    }

    private void OnDestroy()
    {
        AutoBattleLootHighlight.UnregisterInventorySlotUi(this);
        ClearStuckInventoryDragIfNeeded();
    }

    private void OnDisable()
    {
        _isPointerOver = false;
        _tooltip?.Hide();
        ClearStuckInventoryDragIfNeeded();
        ApplySlotBackground();
    }

    private void ClearStuckInventoryDragIfNeeded()
    {
        if (!InventoryDragState.HasDrag)
            return;
        if (InventoryDragState.Source != InventoryDragState.SourceKind.Inventory)
            return;
        if (InventoryDragState.FromSlotIndex != _slotIndex)
            return;

        InventoryDragState.EndDrag();
        InventoryDragIconPool.Hide();
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
        bool identifyPending = IsIdentifyPending(def, amount, itemId, inventory);

        bool sameVisual =
            ReferenceEquals(_inventory, inventory) &&
            _slotIndex == slotIndex &&
            _amount == amount &&
            ReferenceEquals(_def, def) &&
            ItemIdEquals(_itemId, itemId) &&
            _identifyPendingCached == identifyPending;

        _tooltip = tooltip;
        _inventory = inventory;
        _slotIndex = slotIndex;
        _inventoryPanelRect = inventoryPanelRect;
        _rootCanvas = rootCanvas;

        if (sameVisual)
        {
            RefreshIdentifyIcon(identifyPending);
            ApplySlotBackground();
            return;
        }

        _def = def;
        _amount = amount;
        _itemId = itemId;
        _identifyPendingCached = identifyPending;

        if (icon)
        {
            bool hasIcon = def != null && def.icon != null;
            icon.enabled = hasIcon;
            icon.sprite = hasIcon ? def.icon : null;
            icon.preserveAspect = true;
        }

        ApplyUpgradeDimVisual();

        if (countText)
            countText.text = (def != null && amount > 0) ? amount.ToString() : "";

        RefreshRarityBorder(def);

        if (def == null || amount <= 0 || string.IsNullOrEmpty(itemId))
            AutoBattleLootHighlight.ClearInventorySlot(slotIndex);

        RefreshIdentifyIcon(identifyPending);
        ApplySlotBackground();
    }

    private static bool IsIdentifyPending(ItemDefinition def, int amount, string itemId, Inventory inventory)
    {
        if (def == null || amount <= 0 || string.IsNullOrWhiteSpace(itemId) || inventory == null)
            return false;

        return ItemRandomStatIdentification.HasUnidentifiedRandomAffixes(inventory.GetItemDatabase(), itemId);
    }

    private void RefreshIdentifyIcon(bool show)
    {
        if (!identifyIcon)
            return;

        identifyIcon.gameObject.SetActive(show);
        if (show)
            identifyIcon.enabled = true;
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

        if (_upgradeViewActive && (!HasItemContext || !_upgradeViewCanSelect))
        {
            rarityOutline.enabled = false;
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

        if (_upgradeSelected)
            background.color = pressedColor;
        else if (_isPointerOver)
            background.color = hoverColor;
        else if (AutoBattleLootHighlight.IsInventorySlotMarked(_slotIndex))
            background.color = autoBattleNewLootColor;
        else
            background.color = idleColor;
    }

    /// <summary>Updates the idle / new-loot tint without re-binding slot data.</summary>
    public void RefreshLootHighlightVisual() => ApplySlotBackground();

    public void ConfigureUpgradeView(
        bool active,
        bool canSelectGear,
        System.Action<int> onSelect,
        RectTransform dropTarget = null)
    {
        _upgradeViewActive = active;
        _upgradeViewCanSelect = canSelectGear;
        _upgradeViewSelectCallback = canSelectGear ? onSelect : null;
        _upgradeDropTarget = dropTarget;
        ApplyUpgradeDimVisual();
        ApplySlotBackground();
    }

    public void ClearUpgradeView()
    {
        _upgradeViewActive = false;
        _upgradeViewCanSelect = false;
        _upgradeViewSelectCallback = null;
        _upgradeDropTarget = null;
        _upgradeDragging = false;
        _upgradeSelected = false;
        ApplyUpgradeDimVisual();
        ApplySlotBackground();
    }

    public void SetUpgradeSelected(bool selected)
    {
        _upgradeSelected = selected;
        ApplySlotBackground();
    }

    private void EnsureUpgradeCanvasGroup()
    {
        if (_upgradeCanvasGroup)
            return;

        _upgradeCanvasGroup = GetComponent<CanvasGroup>();
        if (!_upgradeCanvasGroup)
            _upgradeCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void ApplyUpgradeDimVisual()
    {
        EnsureUpgradeCanvasGroup();

        bool dimItem = _upgradeViewActive && (!HasItemContext || !_upgradeViewCanSelect);
        _upgradeCanvasGroup.alpha = dimItem ? 0.42f : 1f;
        _upgradeCanvasGroup.interactable = true;
        _upgradeCanvasGroup.blocksRaycasts = true;

        if (icon)
            icon.color = dimItem ? UpgradeDimIconColor : Color.white;

        RefreshRarityBorder(_def);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_slotIndex < 0)
            return;

        if (InventoryDragState.HasDrag)
            return;

        if (_upgradeViewActive)
        {
            if (eventData.button == PointerEventData.InputButton.Left &&
                _upgradeViewCanSelect &&
                HasItemContext)
            {
                float clickTime = Time.unscaledTime;
                bool isUpgradeDoubleClick = (clickTime - _lastClickTime) <= doubleClickSeconds;
                _lastClickTime = clickTime;
                if (isUpgradeDoubleClick)
                    _upgradeViewSelectCallback?.Invoke(_slotIndex);
            }

            eventData.Use();
            return;
        }

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

        if (StorageUI.IsOpen && (doubleClick || InputUtil.CtrlHeld()))
        {
            TryDoubleClickDepositToStorage();
            eventData.Use();
            return;
        }

        if (FurnaceClick.IsFurnaceOpen && doubleClick)
        {
            TryDoubleClickDepositToFurnace();
            eventData.Use();
            return;
        }

        if (CookingClick.IsCookingOpen && doubleClick)
        {
            TryDoubleClickDepositToCookingRange();
            eventData.Use();
            return;
        }

        if (doubleClick && !MerchantClick.MerchantModeOpen)
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

        PerformSellAction();
        eventData.Use();
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

    private bool TryDoubleClickDepositToFurnace()
    {
        if (_inventory == null)
            return false;

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty)
            return false;

        ItemDefinition def = _inventory.GetItemDef(slot.itemId);
        if (def != null && def.IsProcessingSkillEnhancement &&
            def.ProcessingSkillTarget == ProcessingSkillTarget.Smelting)
        {
            return FurnaceUI.TryDepositEnhancementFromInventorySlot(_inventory, _slotIndex, 0);
        }

        if (!SmeltingRecipes.IsSmeltableOre(slot.itemId))
            return false;

        return FurnaceUI.TryDepositFromInventorySlot(_inventory, _slotIndex, 0);
    }

    private bool TryDoubleClickDepositToCookingRange()
    {
        if (_inventory == null)
            return false;

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty)
            return false;

        ItemDefinition def = _inventory.GetItemDef(slot.itemId);
        if (def != null && def.IsProcessingSkillEnhancement &&
            def.ProcessingSkillTarget == ProcessingSkillTarget.Cooking)
        {
            return CookingUI.TryDepositEnhancementFromInventorySlot(_inventory, _slotIndex, 0);
        }

        if (!CookingRecipes.IsCookableRaw(slot.itemId))
            return false;

        return CookingUI.TryDepositFromInventorySlot(_inventory, _slotIndex, 0);
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

        if (MapEnhancementService.IsRolledMapEnhancement(slot.itemId))
        {
            if (MapEnhancementService.TryEquipFromInventorySlotAuto(_inventory, _slotIndex))
                MapCombatScalingPopupUI.RefreshEnhancementSlotsIfOpen();
            return;
        }

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

                // remove one item first, then add; if fails, put back.
                // Same-type replace displaces the old tool — must return it (do not destroy).
                if (_inventory.RemoveAmountAtSlot(_slotIndex, 1) != 1) return;

                bool ok = toolbelt.TryAddToFirstEmpty(slot.itemId, out string displacedToolId);
                if (!ok)
                {
                    // toolbelt full -> put back, and DO NOT equip mainhand
                    ReturnOrDrop(slot.itemId, 1);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(displacedToolId))
                    ReturnOrDrop(displacedToolId, 1);
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
                ReturnOrDrop(prev, 1);

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

                equipment.EquipOffHand(slot.itemId, addAmount);
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
                ReturnOrDrop(prev, Mathf.Max(1, prevAmount));

            return;
        }

        if (def.equipSlot != EquipSlot.None)
        {
            if (def.equipSlot == EquipSlot.Ring)
            {
                equipment.TryEquipRingFromInventorySlot(_inventory, _slotIndex);
                return;
            }

            // Normal single-slot gear
            if (!TryCanEquipOrPopup(def, def.equipSlot)) return;

            if (_inventory.RemoveAmountAtSlot(_slotIndex, 1) != 1) return;

            string prev = equipment.GetEquippedItemId(def.equipSlot);
            equipment.EquipGear(def.equipSlot, slot.itemId);

            if (!string.IsNullOrWhiteSpace(prev) && prev != slot.itemId)
                ReturnOrDrop(prev, 1);

            return;
        }

        if (def.IsOpenable && slot.amount > 0)
        {
            TryOpenItemAtSlot(_slotIndex, def);
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
    private bool TryOpenItemAtSlot(int slotIndex, ItemDefinition def)
    {
        if (def == null || _inventory == null)
            return false;

        if (!def.IsOpenable)
            return false;

        var slot = _inventory.GetSlot(slotIndex);
        if (slot.IsEmpty || slot.amount <= 0)
            return false;

        int required = Mathf.Max(1, def.OpenRequiredAmount);
        if (slot.amount < required)
        {
            GameLog.Add($"You need {required} {def.displayName} to open one ({slot.amount}/{required}).");
            return false;
        }

        // Empty table or a roll that produced nothing: don't silently delete the player's items.
        var rolled = def.RollOpenableLoot();
        if (rolled == null || rolled.Count == 0)
        {
            GameLog.Add($"{def.displayName} contained nothing this time.");
            return false;
        }

        if (_inventory.RemoveAmountAtSlot(slotIndex, required) != required)
            return false;

        // Use the opened item's display name as the loot source so the Tracker rolls everything from one open under
        // a single row (e.g. "Bird Nest"). Fall back to itemId only when the displayName field is empty.
        string trackerSource = !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : def.itemId;
        SessionTrackerData tracker = SessionTrackerData.EnsureInstance();

        // Source is already consumed — never use Inventory.Add here: partial overflow returns false and
        // silently drops the remainder. Route leftovers through storage / world drop / pending recovery.
        for (int i = 0; i < rolled.Count; i++)
        {
            (string itemId, int amount) reward = rolled[i];
            if (string.IsNullOrWhiteSpace(reward.itemId) || reward.amount <= 0)
                continue;

            GrantOpenedLoot(reward.itemId, reward.amount, trackerSource, tracker);
        }

        return true;
    }

    /// <summary>
    /// Grants openable rewards without losing overflow when the bag is full.
    /// </summary>
    private void GrantOpenedLoot(string itemId, int amount, string trackerSource, SessionTrackerData tracker)
    {
        if (_inventory == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        int left = amount;
        int toInv = _inventory.AddPartial(itemId, left, notifyItemGainPopup: true);
        left -= toInv;
        if (toInv > 0)
            tracker?.RegisterLootGain(trackerSource, itemId, toInv);

        if (left <= 0)
            return;

        PlayerStorage storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (storage != null)
        {
            int toStorage = storage.TryDepositAmountFromExternal(itemId, left);
            if (toStorage > 0)
            {
                left -= toStorage;
                tracker?.RegisterLootGain(trackerSource, itemId, toStorage);
                GameLog.Add(
                    $"Inventory was full — sent {toStorage}x {ResolveItemDisplayName(itemId)} to storage.",
                    GameLog.CannotMessageColor);
            }
        }

        if (left <= 0)
            return;

        ItemDefinition rewardDef = _inventory.GetItemDef(itemId);
        if (DropManager.Instance != null)
        {
            DropManager.Instance.Spawn(itemId, left, rewardDef ? rewardDef.icon : null);
            tracker?.RegisterLootGain(trackerSource, itemId, left);
            GameLog.Add(
                $"Inventory and storage are full — dropped {left}x {ResolveItemDisplayName(itemId)} on the ground.",
                GameLog.CannotMessageColor);
            return;
        }

        PendingLootRecoveryStore.Enqueue(itemId, left);
        tracker?.RegisterLootGain(trackerSource, itemId, left);
        GameLog.Add(
            $"Inventory and storage are full — held {left}x {ResolveItemDisplayName(itemId)} until you free space.",
            GameLog.CannotMessageColor);
    }

    public bool CanShowOpenAllAction()
    {
        if (_def == null || _inventory == null || !_def.IsOpenable)
            return false;

        return _inventory.GetTotalAmount(_def.itemId) > 1;
    }

    private static int FindOpenableSlotIndex(Inventory inventory, string itemId, int requiredAmount)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(itemId))
            return -1;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (slot.IsEmpty || slot.itemId != itemId || slot.amount < requiredAmount)
                continue;

            return i;
        }

        return -1;
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

    private bool TryEquipExactGearSlot(EquipSlot slot, string itemId)
    {
        if (!equipment.CanEquip(itemId, slot)) return false;

        string prev = equipment.GetEquippedItemId(slot);

        equipment.EquipGear(slot, itemId);

        if (!string.IsNullOrWhiteSpace(prev) && prev != itemId)
            ReturnOrDrop(prev, 1);

        return true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        ItemTooltipHoverRegistry.SetHovered(this, true);

        AutoBattleLootHighlight.ClearInventorySlot(_slotIndex);

        if (background)
            background.color = hoverColor;

        if (_upgradeViewActive && (!HasItemContext || !_upgradeViewCanSelect))
        {
            ApplySlotBackground();
            return;
        }

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

            // Use the inventory window/panel for flip decision and vertical clamping
            if (_tooltipHeightRect)
            {
                flipper.SetMeasureRect(_tooltipHeightRect);
                flipper.SetHeightRect(_tooltipHeightRect);
            }
        }

        ItemDatabase db = _inventory != null ? _inventory.GetItemDatabase() : null;
        ItemRandomStatIdentification.GetTooltipRandomStatFlags(
            db,
            _def,
            _itemId,
            out bool maskUnrolledRandomStats,
            out bool showRandomStatPoolOptions);

        // Use THIS slot as the anchor, so tooltip appears beside hovered slot
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
        if (!HasItemContext || _inventory == null)
            return false;

        return ItemRandomStatIdentification.HasUnidentifiedRandomAffixes(_inventory.GetItemDatabase(), _itemId);
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

        ItemDatabase db = _inventory.GetItemDatabase();
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

    public void PerformEquipAction() => TryDoubleClickEquipFromThisSlot();

    public void PerformUpgradeAction()
    {
        if (_slotIndex < 0 || !HasItemContext)
            return;

        int slotIndex = _slotIndex;
        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        if (menu != null)
            menu.OpenUpgrade();

        UpgradePageUI upgradePage = FindFirstObjectByType<UpgradePageUI>(FindObjectsInactive.Include);
        if (upgradePage != null)
            upgradePage.SelectGearFromSlot(slotIndex);

        _tooltip?.Hide();
    }

    public void PerformEquipOnMapAction()
    {
        if (_inventory == null)
            return;

        if (MapEnhancementService.TryEquipFromInventorySlotAuto(_inventory, _slotIndex))
            MapCombatScalingPopupUI.RefreshEnhancementSlotsIfOpen();
    }

    public void PerformStoreAction() => TryDoubleClickDepositToStorage();

    public bool CanSellToActiveMerchant(out string cantSellLabel)
    {
        cantSellLabel = "Can't sell here";

        if (!HasItemContext || _inventory == null)
            return false;

        if (!MerchantClick.TryGetActiveMerchant(out Merchant merchant) || merchant == null)
            return false;

        int valuePerItem = _inventory.GetItemValue(_itemId);
        if (valuePerItem <= 0 || !merchant.CanBuyItemFromPlayer(_itemId))
            return false;

        return true;
    }

    public void PerformSellOneAction() => PerformSellAtAmount(1);

    public void PerformSellAllAction() => PerformSellAtAmount(int.MaxValue);

    public void PerformSellAction() => PerformSellAllAction();

    private void PerformSellAtAmount(int amount)
    {
        if (_inventory == null || wallet == null || _slotIndex < 0)
            return;

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty)
            return;

        string soldItemName = ResolveItemDisplayName(slot.itemId);

        if (MerchantClick.TryGetActiveMerchant(out var activeMerchantRef) &&
            activeMerchantRef != null &&
            activeMerchantRef.TryRejectUnsellableItemWithPopup(slot.itemId))
        {
            _tooltip?.Hide();
            return;
        }

        int valuePerItem = _inventory.GetItemValue(slot.itemId);
        if (valuePerItem <= 0)
            return;

        int toRemove = Mathf.Min(amount, slot.amount);
        int removed = _inventory.RemoveAmountAtSlot(_slotIndex, toRemove);
        if (removed <= 0)
            return;

        int goldGained = valuePerItem * removed;
        wallet.AddGold(goldGained);

        Merchant saleMerchant = null;
        if (MerchantClick.TryGetActiveMerchant(out var activeMerchant))
            saleMerchant = activeMerchant;

        var spawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
        if (spawner)
            spawner.ShowGoldGained(goldGained);

        SaleUndoManager.Instance?.RecordSale(slot.itemId, removed, goldGained, saleMerchant, stockAddedAmount: 0);
        GameLog.SoldItem(soldItemName, removed, goldGained);
        _tooltip?.Hide();
    }

    public void PerformOpenAction()
    {
        if (_def == null || _inventory == null)
            return;

        if (TryOpenItemAtSlot(_slotIndex, _def))
            _tooltip?.Hide();
    }

    public void PerformOpenAllAction()
    {
        if (_def == null || _inventory == null || !_def.IsOpenable)
            return;

        int required = Mathf.Max(1, _def.OpenRequiredAmount);
        string itemId = _def.itemId;
        const int maxOpensPerAction = 10_000;
        int opened = 0;

        while (_inventory.GetTotalAmount(itemId) >= required && opened < maxOpensPerAction)
        {
            int slotIndex = FindOpenableSlotIndex(_inventory, itemId, required);
            if (slotIndex < 0)
                break;

            if (!TryOpenItemAtSlot(slotIndex, _def))
                break;

            opened++;
        }

        if (opened > 0)
            _tooltip?.Hide();
    }

    public void PerformEatAction()
    {
        if (_inventory == null || _slotIndex < 0)
            return;

        PlayerConsumableController consumables = FindFirstObjectByType<PlayerConsumableController>(FindObjectsInactive.Include);
        if (consumables != null)
            consumables.TryUseFromInventorySlot(_slotIndex);
    }

    public void PerformDropAction()
    {
        if (_inventory == null || _slotIndex < 0)
            return;

        var slot = _inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty || string.IsNullOrWhiteSpace(slot.itemId) || slot.amount <= 0)
            return;

        int removed = _inventory.RemoveAmountAtSlot(_slotIndex, slot.amount);
        if (removed <= 0)
            return;

        Sprite iconSprite = _def ? _def.icon : null;
        if (DropManager.Instance != null)
            DropManager.Instance.Spawn(slot.itemId, removed, iconSprite);
        ItemGainPopupNotifier.NotifyLost(slot.itemId, removed);
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
            InventoryContextMenuBuilder.BuildForInventorySlot(this),
            _rootCanvas,
            _inventoryPanelRect,
            eventData != null ? eventData.position : (Vector2?)null,
            ResolveItemDisplayName(_itemId));
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
        if (_slotIndex < 0)
            return;

        if (_upgradeViewActive)
        {
            if (!_upgradeViewCanSelect || !HasItemContext)
                return;

            _upgradeDragging = true;
            if (background)
                background.color = pressedColor;
            CreateDragIcon();
            UpdateDragIconPosition(eventData);
            return;
        }

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
        InventoryDragIconPool.UpdateDragSortOverlay(eventData.position, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_upgradeDragging)
        {
            _upgradeDragging = false;
            InventoryDragIconPool.Hide();
            _dragIconGO = null;
            _dragIconRT = null;
            _dragIconImage = null;

            if (_upgradeDropTarget != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    _upgradeDropTarget,
                    eventData.position,
                    eventData.pressEventCamera))
            {
                _upgradeViewSelectCallback?.Invoke(_slotIndex);
            }

            ApplySlotBackground();
            return;
        }

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

        if (_upgradeViewActive)
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

    private string ResolveItemDisplayName(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "";

        return ItemGainPopupNotifier.ResolveDisplayLabel(itemId, 1);
    }

    private void ReturnOrDrop(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || _inventory == null || amount <= 0) return;

        // Inventory.Add can partially succeed and still return false — only overflow the remainder.
        int added = _inventory.AddPartial(itemId, amount, notifyItemGainPopup: false);
        int left = amount - added;
        if (left <= 0)
            return;

        PlayerStorage storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (storage != null)
        {
            int toStorage = storage.TryDepositAmountFromExternal(itemId, left);
            left -= toStorage;
            if (left <= 0)
                return;
        }

        var def = _inventory.GetItemDef(itemId);
        if (DropManager.Instance != null)
            DropManager.Instance.Spawn(itemId, left, def ? def.icon : null);
        else
            PendingLootRecoveryStore.Enqueue(itemId, left);
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