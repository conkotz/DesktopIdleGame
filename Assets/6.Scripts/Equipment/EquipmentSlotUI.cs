using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentSlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler,
    IDropHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IItemTooltipHoverSource
{
    // Shared equipment slot colors (not per-slot serialized overrides).
    private static readonly Color SlotIdleColor = new Color32(37, 40, 47, 255);      // #25282F
    private static readonly Color SlotHoverColor = new Color32(155, 131, 85, 255);   // #9B8355
    private static readonly Color SlotPressedColor = new Color32(224, 220, 211, 255); // #E0DCD3

    [Header("Slot Type")]
    [SerializeField] private EquipmentUISlotType slotType;

    [Header("UI")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Outline rarityOutline;

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


    [Header("Refs (auto-find if empty)")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private ToolbeltManager toolbelt;
    [SerializeField] private Inventory inventory;
    [SerializeField] private SharedTooltipUI tooltip;

    [SerializeField] private RectTransform equipmentWindowRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    private static EquipmentManager s_sharedEquipment;
    private static ToolbeltManager s_sharedToolbelt;
    private static Inventory s_sharedInventory;
    private static SharedTooltipUI s_sharedTooltip;
    private static bool s_sharedRefsResolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedReferences()
    {
        s_sharedEquipment = null;
        s_sharedToolbelt = null;
        s_sharedInventory = null;
        s_sharedTooltip = null;
        s_sharedRefsResolved = false;
        EquipDragState.End();
    }
    private RectTransform _tooltipHeightRect;


    [Header("Double Click")]
    [SerializeField] private float doubleClickSeconds = 0.30f;

    private const float IdentifyGlowSeconds = 1.1f;

    [Header("Debug")]
    [SerializeField] private bool logDrops = false;

    private float _lastClickTime;
    private bool _isPointerOver;
    private Coroutine _identifyRoutine;

    private bool _bound;
    private bool _subscribed;

    private string _itemId;
    private ItemDefinition _def;

    private Action<string> _mainCb;
    private Action<string> _offCb;
    private Action<int> _offStackCb;
    private Action<int, string> _toolCb;
    private Action<EquipmentUISlotType, string> _uiSlotCb;
    private Action<int> _activeSetCb;

    private bool _labelLayoutCaptured;
    private Vector2 _defaultLabelAnchorMin;
    private Vector2 _defaultLabelAnchorMax;
    private Vector2 _defaultLabelPivot;
    private Vector2 _defaultLabelAnchoredPosition;
    private Vector2 _defaultLabelSizeDelta;
    private TextAlignmentOptions _defaultLabelAlignment;

    private Canvas _rootCanvas;
    private GameObject _dragIconGO;
    private RectTransform _dragIconRT;
    private Image _dragIconImage;

    // -------------------------------
    // Drag state (INSIDE this script)
    // -------------------------------
    private static class EquipDragState
    {
        public static bool HasDrag { get; private set; }
        public static EquipmentUISlotType FromSlotType { get; private set; }
        public static string ItemId { get; private set; }
        public static int Amount { get; private set; }

        public static void Begin(EquipmentUISlotType fromSlotType, string itemId, int amount)
        {
            FromSlotType = fromSlotType;
            ItemId = itemId;
            Amount = Mathf.Max(1, amount);
            HasDrag = !string.IsNullOrWhiteSpace(itemId);
        }

        public static void End()
        {
            HasDrag = false;
            FromSlotType = default;
            ItemId = null;
            Amount = 0;
        }
    }

    /// <summary>
    /// InventorySlotUI uses this to consume a drag coming from equipment/toolbelt.
    /// </summary>
    public static bool TryConsumeEquipDrag(out EquipmentUISlotType fromSlot, out string itemId, out int amount)
    {
        fromSlot = default;
        itemId = null;
        amount = 0;

        if (!EquipDragState.HasDrag) return false;

        fromSlot = EquipDragState.FromSlotType;
        itemId = EquipDragState.ItemId;
        amount = EquipDragState.Amount;
        EquipDragState.End();
        return true;
    }

    // Backward-safe overload if any older code still calls it
    public static bool TryConsumeEquipDrag(out EquipmentUISlotType fromSlot, out string itemId)
    {
        return TryConsumeEquipDrag(out fromSlot, out itemId, out _);
    }

    private void Awake()
    {
        _preferredSide = preferredSide;

        if (!background) background = GetComponent<Image>();
        if (background)
        {
            background.raycastTarget = true;
            background.color = SlotIdleColor;
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
        if (label) label.raycastTarget = false;

        CaptureDefaultLabelLayout();

        if (!_rootCanvas)
            _rootCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        _bound = false;
        _subscribed = false;

        if (MainMenuUIPrewarm.UseBatchedInstantiation)
            return;

        TryBind();
        TrySubscribe();
        RefreshFromState();
    }

    private void OnDisable()
    {
        Unsubscribe();
        tooltip?.Hide();
        DestroyDragIcon();
        EquipDragState.End();
    }

    private void Update()
    {
        if (!_bound)
        {
            TryBind();
            TrySubscribe();
            RefreshFromState();
        }
    }

    public static void PrewarmSharedReferences()
    {
        if (s_sharedRefsResolved)
            return;

        s_sharedEquipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        s_sharedToolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);
        s_sharedInventory = s_sharedEquipment != null
            ? s_sharedEquipment.Inventory
            : FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        s_sharedTooltip = ResolveSharedEquipmentTooltip();
        s_sharedRefsResolved = s_sharedEquipment != null && s_sharedInventory != null;
    }

    public void PrewarmBinding()
    {
        PrewarmSharedReferences();
        TryBind();
        TrySubscribe();
        RefreshFromState();
    }

    private void TryBind()
    {
        if (!equipment && s_sharedEquipment)
            equipment = s_sharedEquipment;

        if (!toolbelt && s_sharedToolbelt)
            toolbelt = s_sharedToolbelt;

        if (!inventory && s_sharedInventory)
            inventory = s_sharedInventory;

        if (!tooltip && s_sharedTooltip)
            tooltip = s_sharedTooltip;

        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);

        if (!toolbelt)
            toolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);

        if (!inventory)
            inventory = (equipment != null) ? equipment.Inventory : FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (!tooltip)
            tooltip = FindEquipmentTooltip();

        if (!_rootCanvas)
            _rootCanvas = GetComponentInParent<Canvas>();

        _bound = (equipment != null && inventory != null);
    }

    private SharedTooltipUI FindEquipmentTooltip() => ResolveSharedEquipmentTooltip();

    private static SharedTooltipUI ResolveSharedEquipmentTooltip()
    {
        SharedTooltipUI[] allTooltips =
            FindObjectsByType<SharedTooltipUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (SharedTooltipUI t in allTooltips)
        {
            if (t != null && t.name == "SharedToolTipInfoPanel")
                return t;
        }

        foreach (SharedTooltipUI t in allTooltips)
        {
            if (t != null && t.name != "HUDToolInfoPanel")
                return t;
        }

        return null;
    }

    private void TrySubscribe()
    {
        if (!_bound) return;
        if (_subscribed) return;

        _mainCb ??= _ => QueueRefreshFromState();
        _offCb ??= _ => QueueRefreshFromState();
        _offStackCb ??= stack =>
        {
            if (slotType == EquipmentUISlotType.OffHand)
                RefreshOffHandStackLabel(stack);
        };
        _toolCb ??= (_, __) => QueueRefreshFromState();
        _uiSlotCb ??= (uiSlot, _) =>
        {
            if (uiSlot == slotType)
                QueueRefreshFromState();
        };
        _activeSetCb ??= _ => QueueRefreshFromState();

        Unsubscribe();

        equipment.OnMainHandChanged += _mainCb;
        equipment.OnOffHandChanged += _offCb;
        equipment.OnOffHandStackChanged += _offStackCb;
        equipment.OnUISlotChanged += _uiSlotCb;
        equipment.OnActiveSetChanged += _activeSetCb;

        if (toolbelt != null)
            toolbelt.OnToolSlotChanged += _toolCb;

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (equipment != null)
        {
            if (_mainCb != null) equipment.OnMainHandChanged -= _mainCb;
            if (_offCb != null) equipment.OnOffHandChanged -= _offCb;
            if (_offStackCb != null) equipment.OnOffHandStackChanged -= _offStackCb;
            if (_uiSlotCb != null) equipment.OnUISlotChanged -= _uiSlotCb;
            if (_activeSetCb != null) equipment.OnActiveSetChanged -= _activeSetCb;
        }

        if (toolbelt != null)
        {
            if (_toolCb != null) toolbelt.OnToolSlotChanged -= _toolCb;
        }

        _subscribed = false;
    }

    private FlipInsideBounds.PreferredSide _preferredSide;
    private bool _refreshFromStateQueued;

    private void QueueRefreshFromState()
    {
        _refreshFromStateQueued = true;
    }

    private void LateUpdate()
    {
        if (!_refreshFromStateQueued)
            return;

        _refreshFromStateQueued = false;
        RefreshFromState();
    }

    public void SetTooltipDocking(
        RectTransform tooltipHeightRect,
        FlipInsideBounds.PreferredSide preferredSide)
    {
        _tooltipHeightRect = tooltipHeightRect;
        _preferredSide = preferredSide;
    }
    private void RefreshFromState()
    {
        if (!_bound)
        {
            SetEmptyVisual();
            return;
        }

        _itemId = GetItemIdForThisSlot();
        _def = (!string.IsNullOrWhiteSpace(_itemId) && inventory != null) ? inventory.GetItemDef(_itemId) : null;

        if (icon)
        {
            bool hasIcon = _def != null && _def.icon != null;
            icon.enabled = hasIcon;
            icon.sprite = hasIcon ? _def.icon : null;
            icon.preserveAspect = true;
        }

        if (label)
        {
            label.text = GetDisplayLabel();
            ApplyLabelLayoutForCurrentState();
        }

        RefreshRarityBorder(_def);
    }

    private void CaptureDefaultLabelLayout()
    {
        if (_labelLayoutCaptured || !label)
            return;

        RectTransform rt = label.rectTransform;
        _defaultLabelAnchorMin = rt.anchorMin;
        _defaultLabelAnchorMax = rt.anchorMax;
        _defaultLabelPivot = rt.pivot;
        _defaultLabelAnchoredPosition = rt.anchoredPosition;
        _defaultLabelSizeDelta = rt.sizeDelta;
        _defaultLabelAlignment = label.alignment;
        _labelLayoutCaptured = true;
    }

    private bool ShouldUseOffHandStockLabelLayout() =>
        slotType == EquipmentUISlotType.OffHand && _def != null && _def.ShowsOffHandStackCount;

    private void ApplyLabelLayoutForCurrentState()
    {
        if (!label)
            return;

        CaptureDefaultLabelLayout();
        RectTransform rt = label.rectTransform;

        if (ShouldUseOffHandStockLabelLayout())
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -12f);
            rt.sizeDelta = new Vector2(72f, 22f);
            label.alignment = TextAlignmentOptions.Top | TextAlignmentOptions.Center;
            return;
        }

        rt.anchorMin = _defaultLabelAnchorMin;
        rt.anchorMax = _defaultLabelAnchorMax;
        rt.pivot = _defaultLabelPivot;
        rt.anchoredPosition = _defaultLabelAnchoredPosition;
        rt.sizeDelta = _defaultLabelSizeDelta;
        label.alignment = _defaultLabelAlignment;
    }

    private void RefreshOffHandStackLabel(int stackAmount)
    {
        if (!_bound || slotType != EquipmentUISlotType.OffHand || label == null)
            return;

        if (_def != null && _def.ShowsOffHandStackCount)
        {
            label.text = $"x{Mathf.Max(1, stackAmount)}";
            ApplyLabelLayoutForCurrentState();
        }
        else if (label != null)
        {
            label.text = GetTitle();
            ApplyLabelLayoutForCurrentState();
        }
    }

    private void SetEmptyVisual()
    {
        _itemId = null;
        _def = null;

        if (icon)
        {
            icon.enabled = false;
            icon.sprite = null;
        }

        if (label)
        {
            label.text = GetTitle();
            ApplyLabelLayoutForCurrentState();
        }

        RefreshRarityBorder(null);
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
        rarityOutline.useGraphicAlpha = false;
        rarityOutline.effectDistance = rarityBorderThickness;
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

    private string GetItemIdForThisSlot() =>
        GetItemIdForSlot(slotType, equipment, toolbelt);

    public static string GetItemIdForSlot(
        EquipmentUISlotType slotType,
        EquipmentManager equipment,
        ToolbeltManager toolbelt)
    {
        switch (slotType)
        {
            case EquipmentUISlotType.MainHand:
                return equipment ? equipment.MainHandItemId : null;

            case EquipmentUISlotType.OffHand:
                return equipment ? equipment.OffHandItemId : null;

            case EquipmentUISlotType.Toolbelt0:
                return toolbelt ? toolbelt.GetToolItemId(0) : null;
            case EquipmentUISlotType.Toolbelt1:
                return toolbelt ? toolbelt.GetToolItemId(1) : null;
            case EquipmentUISlotType.Toolbelt2:
                return toolbelt ? toolbelt.GetToolItemId(2) : null;
            case EquipmentUISlotType.Toolbelt3:
                return toolbelt ? toolbelt.GetToolItemId(3) : null;

            case EquipmentUISlotType.Helmet:
                return equipment ? equipment.GetEquippedItemId(EquipSlot.Helmet) : null;
            case EquipmentUISlotType.Body:
                return equipment ? equipment.GetEquippedItemId(EquipSlot.Body) : null;
            case EquipmentUISlotType.Boots:
                return equipment ? equipment.GetEquippedItemId(EquipSlot.Boots) : null;
            case EquipmentUISlotType.Trinket:
                return equipment ? equipment.GetEquippedItemId(EquipSlot.Trinket) : null;
            case EquipmentUISlotType.Pendant:
                return equipment ? equipment.GetEquippedItemId(EquipSlot.Pendant) : null;

            case EquipmentUISlotType.Ring1:
                return equipment ? equipment.GetEquippedItemId(EquipSlot.Ring, 0) : null;
            case EquipmentUISlotType.Ring2:
                return equipment ? equipment.GetEquippedItemId(EquipSlot.Ring, 1) : null;
        }

        return null;
    }

    public static void ReplaceItemIdForSlot(
        EquipmentUISlotType slotType,
        EquipmentManager equipment,
        ToolbeltManager toolbelt,
        string newItemId)
    {
        int toolIndex = slotType switch
        {
            EquipmentUISlotType.Toolbelt0 => 0,
            EquipmentUISlotType.Toolbelt1 => 1,
            EquipmentUISlotType.Toolbelt2 => 2,
            EquipmentUISlotType.Toolbelt3 => 3,
            _ => -1
        };

        if (toolIndex >= 0)
        {
            toolbelt?.SetToolItemId(toolIndex, newItemId);
            return;
        }

        equipment?.ReplaceEquippedItemIdForUiSlot(slotType, newItemId);
    }

    public static void ClearSlotOnEnhancementDestroy(
        EquipmentUISlotType slotType,
        EquipmentManager equipment,
        ToolbeltManager toolbelt) =>
        UnequipDragSource(slotType, equipment, toolbelt);

    private int GetToolbeltIndex()
    {
        return slotType switch
        {
            EquipmentUISlotType.Toolbelt0 => 0,
            EquipmentUISlotType.Toolbelt1 => 1,
            EquipmentUISlotType.Toolbelt2 => 2,
            EquipmentUISlotType.Toolbelt3 => 3,
            _ => -1
        };
    }

    private string GetTitle() => EquipSlotDisplayNames.GetDisplayName(slotType);

    private string GetDisplayLabel()
    {
        if (_def == null)
            return GetTitle();

        if (slotType == EquipmentUISlotType.OffHand && _def.ShowsOffHandStackCount && equipment != null)
        {
            int amount = Mathf.Max(1, equipment.OffHandStackAmount);
            return $"x{amount}";
        }

        return "";
    }

    private int GetEquippedAmountForThisSlot()
    {
        if (_def == null)
            return 1;

        if (slotType == EquipmentUISlotType.OffHand && _def.ShowsOffHandStackCount && equipment != null)
            return Mathf.Max(1, equipment.OffHandStackAmount);

        return 1;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        ItemTooltipHoverRegistry.SetHovered(this, true);
        if (background) background.color = SlotHoverColor;
        ShowTooltip();
    }

    private bool CanShowTooltip()
    {
        return tooltip != null &&
               _def != null &&
               isActiveAndEnabled;
    }

    private void ShowTooltip()
    {
        if (!CanShowTooltip())
        {
            tooltip?.Hide();
            return;
        }

        var flipper = tooltip.GetComponent<FlipInsideBounds>();
        if (flipper)
        {
            flipper.SetPreferredSide(_preferredSide);

            if (_tooltipHeightRect)
            {
                flipper.SetMeasureRect(_tooltipHeightRect);
                flipper.SetHeightRect(_tooltipHeightRect);
            }
        }

        bool compact = !_def.IsCombatSupport;
        tooltip.ShowAt(transform, _def, GetEquippedAmountForThisSlot(), compact, itemId: _itemId);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        ItemTooltipHoverRegistry.SetHovered(this, false);
        if (background) background.color = SlotIdleColor;
        tooltip?.Hide();
    }

    public void RefreshTooltipIfHovered()
    {
        if (!_isPointerOver)
            return;

        ShowTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InventoryDragState.HasDrag || EquipDragState.HasDrag)
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

        if (!doubleClick) return;

        DoubleClickReturnToInventory();
        eventData.Use();
    }

    public bool HasItemContext => _bound && _def != null && !string.IsNullOrWhiteSpace(_itemId);
    public ItemDefinition ContextDefinition => _def;
    public string ContextItemId => _itemId;
    public EquipmentUISlotType UISlotType => slotType;

    public void PerformUnequipAction() => DoubleClickReturnToInventory();

    public bool CanIdentifyStats()
    {
        if (!HasItemContext || inventory == null)
            return false;

        return ItemRandomStatIdentification.HasUnidentifiedRandomAffixes(inventory.GetItemDatabase(), _itemId);
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

        ItemDatabase db = inventory.GetItemDatabase();
        if (!ItemRandomStatIdentification.TryIdentify(db, _itemId, out string activityMessage))
            yield break;

        GameLog.Add(activityMessage, ItemRandomStatIdentification.ActivityLogColor);
        RefreshFromState();
        RefreshTooltipIfHovered();
    }

    public void PerformUpgradeAction()
    {
        if (!HasItemContext)
            return;

        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        if (menu != null)
            menu.OpenUpgrade();

        UpgradePageUI upgradePage = FindFirstObjectByType<UpgradePageUI>(FindObjectsInactive.Include);
        if (upgradePage != null)
            upgradePage.SelectGearFromEquipmentSlot(slotType);

        tooltip?.Hide();
    }

    public void PerformDropAction()
    {
        if (!_bound || string.IsNullOrWhiteSpace(_itemId))
            return;

        string itemId = _itemId;
        int amount = GetEquippedAmountForThisSlot();
        if (amount <= 0)
            return;

        Sprite iconSprite = _def != null ? _def.icon : null;
        if (iconSprite == null && inventory != null)
        {
            ItemDefinition def = inventory.GetItemDef(itemId);
            if (def != null)
                iconSprite = def.icon;
        }

        ClearThisSlot();
        RefreshFromState();

        if (DropManager.Instance != null)
            DropManager.Instance.Spawn(itemId, amount, iconSprite);
        ItemGainPopupNotifier.NotifyLost(itemId, amount);
        tooltip?.Hide();
    }

    public void ToggleAdditionalStatsHighlight()
    {
        if (string.IsNullOrWhiteSpace(_itemId))
            return;

        ItemTooltipHighlightState.Toggle(_itemId);
        if (_isPointerOver)
            ShowTooltip();
        else
            tooltip?.Hide();
    }

    private void OpenContextMenu(PointerEventData eventData)
    {
        if (!HasItemContext)
            return;

        tooltip?.Hide();
        ContextMenuUI.EnsureInstance().Show(
            transform as RectTransform,
            InventoryContextMenuBuilder.BuildForEquipmentSlot(this),
            _rootCanvas,
            equipmentWindowRect,
            eventData != null ? eventData.position : (Vector2?)null,
            ItemGainPopupNotifier.ResolveDisplayLabel(_itemId, 1));
    }

    private void DoubleClickReturnToInventory()
    {
        if (!_bound) return;
        if (string.IsNullOrWhiteSpace(_itemId)) return;

        if (slotType == EquipmentUISlotType.OffHand &&
            _def != null &&
            _def.IsCombatSupport &&
            equipment != null)
        {
            equipment.ReturnOffHandStackToInventoryOrDrop();
            RefreshFromState();
            return;
        }

        int amountToReturn = GetEquippedAmountForThisSlot();

        // Inventory.Add can partially succeed and still return false. Only unequip when the
        // full equipped stack was deposited (otherwise stacks would duplicate).
        int added = inventory.AddPartial(_itemId, amountToReturn, notifyItemGainPopup: false);
        if (added < amountToReturn)
        {
            if (added > 0)
                inventory.Remove(_itemId, added);
            return;
        }

        ClearThisSlot();
        RefreshFromState();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!_bound) return;
        if (!InventoryDragState.HasDrag) return;
        if (inventory == null || equipment == null) return;

        if (MerchantClick.MerchantModeOpen &&
            InventoryDragState.Source == InventoryDragState.SourceKind.Inventory)
            return;

        string draggedId = InventoryDragState.ItemId;
        if (string.IsNullOrWhiteSpace(draggedId)) return;

        var draggedDef = inventory.GetItemDef(draggedId);
        if (!draggedDef) return;

        int fromSlot = InventoryDragState.FromSlotIndex;
        if (fromSlot < 0) return;

        if (draggedDef.itemKind == ItemKind.EnhancementScroll)
        {
            if (!IsDropReleaseConfirmed(eventData))
                return;

            InventoryDragState.EndDrag();
            eventData.Use();
            return;
        }

        bool fromStorage = InventoryDragState.Source == InventoryDragState.SourceKind.Storage;
        PlayerStorage playerStorage = fromStorage
            ? (InventoryDragState.StorageSource != null
                ? InventoryDragState.StorageSource
                : FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include))
            : null;
        if (fromStorage && playerStorage == null) return;

        int draggedAmount = InventoryDragState.IsSplit
            ? Mathf.Max(1, InventoryDragState.CarriedAmount)
            : Mathf.Max(1,
                fromStorage
                    ? playerStorage.GetSlot(fromSlot).amount
                    : inventory.GetSlot(fromSlot).amount);

        bool accept = false;

        if (slotType == EquipmentUISlotType.MainHand)
            accept = equipment.CanEquip(draggedId, EquipSlot.MainHand);
        else if (slotType == EquipmentUISlotType.OffHand)
            accept = equipment.CanEquip(draggedId, EquipSlot.OffHand);
        else if (slotType == EquipmentUISlotType.Ring1 || slotType == EquipmentUISlotType.Ring2)
        {
            int ringIndex = slotType == EquipmentUISlotType.Ring1 ? 0 : 1;
            accept = equipment.CanEquipRingToSlot(draggedId, ringIndex);
        }
        else
        {
            int toolIndex = GetToolbeltIndex();
            if (toolIndex >= 0 && toolbelt != null)
            {
                bool isTool =
                    draggedDef.itemKind == ItemKind.Tool &&
                    draggedDef.handVisualKey != ToolKey.None &&
                    draggedDef.handVisualKey != ToolKey.Weapon;

                accept = isTool
                    && !toolbelt.Contains(draggedId)
                    && draggedDef.MeetsEquipmentTierRequirement(SkillsManager.Instance);
            }
            else
            {
                EquipSlot gearSlot = slotType switch
                {
                    EquipmentUISlotType.Helmet => EquipSlot.Helmet,
                    EquipmentUISlotType.Body => EquipSlot.Body,
                    EquipmentUISlotType.Boots => EquipSlot.Boots,
                    EquipmentUISlotType.Trinket => EquipSlot.Trinket,
                    EquipmentUISlotType.Pendant => EquipSlot.Pendant,
                    _ => EquipSlot.None
                };

                accept = (gearSlot != EquipSlot.None) && equipment.CanEquip(draggedId, gearSlot);
            }
        }

        if (logDrops)
            Debug.Log($"[EquipSlotUI] OnDrop slotType={slotType} dragged='{draggedId}' amount={draggedAmount} accept={accept}", this);

        if (!accept)
        {
            if (draggedDef.IsUniquelyEquippedRing &&
                (slotType == EquipmentUISlotType.Ring1 || slotType == EquipmentUISlotType.Ring2))
            {
                GameLog.Add(ItemDefinition.UniquelyEquippedRingActivityLogMessage, GameLog.CannotMessageColor);
            }
            else
            {
                LogRequirementBlockedDrop(draggedDef);
            }

            return;
        }

        // MAIN HAND
        if (slotType == EquipmentUISlotType.MainHand)
        {
            if (!TryRemoveDraggedFromSource(1, fromStorage, playerStorage, fromSlot))
                return;

            string prev = equipment.MainHandItemId;
            equipment.EquipMainHand(draggedId);

            if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                ReturnOrDrop(prev, 1);

            InventoryDragState.EndDrag();
            eventData.Use();
            return;
        }

        // OFF HAND
        if (slotType == EquipmentUISlotType.OffHand)
        {
            bool isSupport = draggedDef.IsCombatSupport;

            // Merge onto same equipped support stack
            if (isSupport &&
                !string.IsNullOrWhiteSpace(equipment.OffHandItemId) &&
                equipment.OffHandItemId == draggedId)
            {
                if (!TryRemoveDraggedFromSource(draggedAmount, fromStorage, playerStorage, fromSlot))
                    return;

                equipment.EquipOffHand(draggedId, draggedAmount);

                InventoryDragState.EndDrag();
                eventData.Use();
                return;
            }

            int equipAmount = isSupport ? draggedAmount : 1;

            string prev = equipment.OffHandItemId;
            int prevAmount = 1;

            if (!string.IsNullOrWhiteSpace(prev))
            {
                var prevDef = inventory.GetItemDef(prev);
                if (prevDef && prevDef.IsCombatSupport)
                    prevAmount = Mathf.Max(1, equipment.OffHandStackAmount);
            }

            if (!TryRemoveDraggedFromSource(equipAmount, fromStorage, playerStorage, fromSlot))
                return;

            equipment.EquipOffHand(draggedId, equipAmount);

            if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                ReturnOrDrop(prev, prevAmount);

            InventoryDragState.EndDrag();
            eventData.Use();
            return;
        }

        // TOOLBELT
        int idx = GetToolbeltIndex();
        if (idx >= 0 && toolbelt != null)
        {
            if (!TryRemoveDraggedFromSource(1, fromStorage, playerStorage, fromSlot))
                return;

            string prev = toolbelt.GetToolItemId(idx);
            toolbelt.SetToolItemId(idx, draggedId);

            if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                ReturnOrDrop(prev, 1);

            InventoryDragState.EndDrag();
            eventData.Use();
            return;
        }

        // RING1 / RING2
        if (slotType == EquipmentUISlotType.Ring1 || slotType == EquipmentUISlotType.Ring2)
        {
            if (!TryRemoveDraggedFromSource(1, fromStorage, playerStorage, fromSlot))
                return;

            int ringIndex = (slotType == EquipmentUISlotType.Ring1) ? 0 : 1;

            string prev = equipment.GetEquippedItemId(EquipSlot.Ring, ringIndex);
            equipment.EquipGear(EquipSlot.Ring, draggedId, ringIndex);

            if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                ReturnOrDrop(prev, 1);

            InventoryDragState.EndDrag();
            eventData.Use();
            return;
        }

        // OTHER GEAR
        {
            EquipSlot gearSlot = slotType switch
            {
                EquipmentUISlotType.Helmet => EquipSlot.Helmet,
                EquipmentUISlotType.Body => EquipSlot.Body,
                EquipmentUISlotType.Boots => EquipSlot.Boots,
                EquipmentUISlotType.Trinket => EquipSlot.Trinket,
                EquipmentUISlotType.Pendant => EquipSlot.Pendant,
                _ => EquipSlot.None
            };

            if (gearSlot != EquipSlot.None)
            {
                if (!TryRemoveDraggedFromSource(1, fromStorage, playerStorage, fromSlot))
                    return;

                string prev = equipment.GetEquippedItemId(gearSlot);
                equipment.EquipGear(gearSlot, draggedId);

                if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                    ReturnOrDrop(prev, 1);

                InventoryDragState.EndDrag();
                eventData.Use();
                return;
            }
        }
    }

    private bool TryRemoveDraggedFromSource(int amount, bool fromStorage, PlayerStorage playerStorage, int fromSlot)
    {
        if (amount <= 0) return false;
        if (fromStorage)
        {
            if (playerStorage == null) return false;
            return playerStorage.RemoveAmountAtSlot(fromSlot, amount) == amount;
        }

        return inventory.RemoveAmountAtSlot(fromSlot, amount) == amount;
    }

    private void ReplaceThisEquippedItemId(string itemId)
    {
        switch (slotType)
        {
            case EquipmentUISlotType.MainHand:
                equipment?.EquipMainHand(itemId);
                break;
            case EquipmentUISlotType.OffHand:
                equipment?.EquipOffHand(itemId, GetEquippedAmountForThisSlot());
                break;
            case EquipmentUISlotType.Helmet:
                equipment?.EquipGear(EquipSlot.Helmet, itemId);
                break;
            case EquipmentUISlotType.Body:
                equipment?.EquipGear(EquipSlot.Body, itemId);
                break;
            case EquipmentUISlotType.Boots:
                equipment?.EquipGear(EquipSlot.Boots, itemId);
                break;
            case EquipmentUISlotType.Trinket:
                equipment?.EquipGear(EquipSlot.Trinket, itemId);
                break;
            case EquipmentUISlotType.Pendant:
                equipment?.EquipGear(EquipSlot.Pendant, itemId);
                break;
            case EquipmentUISlotType.Ring1:
                equipment?.EquipGear(EquipSlot.Ring, itemId, 0);
                break;
            case EquipmentUISlotType.Ring2:
                equipment?.EquipGear(EquipSlot.Ring, itemId, 1);
                break;
            case EquipmentUISlotType.Toolbelt0:
            case EquipmentUISlotType.Toolbelt1:
            case EquipmentUISlotType.Toolbelt2:
            case EquipmentUISlotType.Toolbelt3:
                int toolIndex = GetToolbeltIndex();
                if (toolIndex >= 0)
                    toolbelt?.SetToolItemId(toolIndex, itemId);
                break;
        }
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

    private void LogRequirementBlockedDrop(ItemDefinition draggedDef)
    {
        if (draggedDef == null)
            return;

        if (!draggedDef.UsesEquipmentTierGating || draggedDef.MeetsEquipmentTierRequirement(SkillsManager.Instance))
            return;

        if (!IsRequirementRelevantDropTarget(draggedDef))
            return;

        GameLog.Add(draggedDef.BuildEquipmentTierBlockedMessage());
    }

    private bool IsRequirementRelevantDropTarget(ItemDefinition draggedDef)
    {
        if (draggedDef == null)
            return false;

        if (slotType == EquipmentUISlotType.MainHand)
            return draggedDef.itemKind == ItemKind.Weapon;

        if (slotType == EquipmentUISlotType.OffHand)
        {
            return draggedDef.itemKind == ItemKind.Weapon &&
                   draggedDef.weaponStats.handedness == Handedness.OneHanded &&
                   draggedDef.weaponStats.canEquipInOffHand;
        }

        int toolIndex = GetToolbeltIndex();
        if (toolIndex >= 0)
        {
            return draggedDef.itemKind == ItemKind.Tool &&
                   draggedDef.handVisualKey != ToolKey.None &&
                   draggedDef.handVisualKey != ToolKey.Weapon;
        }

        return false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_bound) return;

        string id = GetItemIdForThisSlot();
        if (string.IsNullOrWhiteSpace(id)) return;

        var def = inventory.GetItemDef(id);
        if (!def) return;

        int amount = GetEquippedAmountForThisSlot();
        EquipDragState.Begin(slotType, id, amount);

        if (background) background.color = SlotPressedColor;

        CreateDragIcon(def.icon);
        UpdateDragIconPosition(eventData);

        if (background) background.color = _isPointerOver ? SlotHoverColor : SlotIdleColor;
        tooltip?.Hide();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIconRT == null) return;
        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyDragIcon();

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            StartCoroutine(EndEquipDragNextFrameIfStillActive());
            return;
        }

        EquipDragState.End();
    }

    private IEnumerator EndEquipDragNextFrameIfStillActive()
    {
        yield return null;
        if (EquipDragState.HasDrag)
            EquipDragState.End();
    }

    private void ClearThisSlot()
    {
        UnequipDragSource(slotType, equipment, toolbelt);
    }

    /// <summary>Used when moving equipped items to inventory or storage (e.g. <see cref="StorageSlotUI"/>).</summary>
    public static void UnequipDragSource(EquipmentUISlotType slotType, EquipmentManager equipment, ToolbeltManager toolbelt)
    {
        switch (slotType)
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
    }

    private void ReturnOrDrop(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return;

        // Inventory.Add can partially succeed and still return false — only drop the remainder.
        int added = inventory.AddPartial(itemId, amount, notifyItemGainPopup: false);
        int left = amount - added;
        if (left <= 0)
            return;

        var def = inventory.GetItemDef(itemId);
        if (DropManager.Instance != null)
            DropManager.Instance.Spawn(itemId, left, def ? def.icon : null);
        else
            PendingLootRecoveryStore.Enqueue(itemId, left);
    }

    private void CreateDragIcon(Sprite sprite)
    {
        if (_rootCanvas == null || sprite == null) return;

        DestroyDragIcon();

        _dragIconGO = new GameObject("EquipDragIcon");
        _dragIconGO.transform.SetParent(_rootCanvas.transform, false);

        _dragIconRT = _dragIconGO.AddComponent<RectTransform>();
        _dragIconImage = _dragIconGO.AddComponent<Image>();
        _dragIconImage.raycastTarget = false;
        _dragIconImage.sprite = sprite;
        _dragIconImage.preserveAspect = true;

        _dragIconRT.sizeDelta = new Vector2(48f, 48f);
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (_dragIconRT == null || _rootCanvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        _dragIconRT.anchoredPosition = localPoint;
    }

    private void DestroyDragIcon()
    {
        if (_dragIconGO) Destroy(_dragIconGO);
        _dragIconGO = null;
        _dragIconRT = null;
        _dragIconImage = null;
    }
}