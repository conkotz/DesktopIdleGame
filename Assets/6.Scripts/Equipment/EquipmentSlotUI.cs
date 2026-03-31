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
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Slot Type")]
    [SerializeField] private EquipmentUISlotType slotType;

    [Header("UI")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Outline rarityOutline;

    [Header("Slot Colours")]
    [SerializeField] private Color idleColor = new Color32(30, 34, 42, 255);
    [SerializeField] private Color hoverColor = new Color32(42, 48, 58, 255);
    [SerializeField] private Color pressedColor = new Color32(58, 66, 80, 255);

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
    private RectTransform _tooltipHeightRect;


    [Header("Double Click")]
    [SerializeField] private float doubleClickSeconds = 0.30f;

    [Header("Debug")]
    [SerializeField] private bool logDrops = false;

    private float _lastClickTime;
    private bool _isPointerOver;

    private bool _bound;
    private bool _subscribed;

    private string _itemId;
    private ItemDefinition _def;

    private Action<string> _mainCb;
    private Action<string> _offCb;
    private Action<int, string> _toolCb;
    private Action<EquipmentUISlotType, string> _uiSlotCb;

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
        if (label) label.raycastTarget = false;

        if (!_rootCanvas)
            _rootCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        _bound = false;
        _subscribed = false;

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

    private void TryBind()
    {
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

    private SharedTooltipUI FindEquipmentTooltip()
    {
        SharedTooltipUI[] allTooltips =
            FindObjectsByType<SharedTooltipUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var t in allTooltips)
        {
            if (t != null && t.name == "SharedToolTipInfoPanel")
                return t;
        }

        foreach (var t in allTooltips)
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

        _mainCb ??= _ => RefreshFromState();
        _offCb ??= _ => RefreshFromState();
        _toolCb ??= (_, __) => RefreshFromState();
        _uiSlotCb ??= (uiSlot, _) =>
        {
            if (uiSlot == slotType)
                RefreshFromState();
        };

        Unsubscribe();

        equipment.OnMainHandChanged += _mainCb;
        equipment.OnOffHandChanged += _offCb;
        equipment.OnUISlotChanged += _uiSlotCb;

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
            if (_uiSlotCb != null) equipment.OnUISlotChanged -= _uiSlotCb;
        }

        if (toolbelt != null)
        {
            if (_toolCb != null) toolbelt.OnToolSlotChanged -= _toolCb;
        }

        _subscribed = false;
    }

    private FlipInsideBounds.PreferredSide _preferredSide;

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
            label.text = GetDisplayLabel();

        RefreshRarityBorder(_def);
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
            label.text = GetTitle();

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
            ItemRarity.Common => commonBorder,
            ItemRarity.Uncommon => uncommonBorder,
            ItemRarity.Rare => rareBorder,
            ItemRarity.Epic => epicBorder,
            ItemRarity.Legendary => legendaryBorder,
            _ => commonBorder
        };
    }

    private string GetItemIdForThisSlot()
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

    private string GetTitle()
    {
        return slotType switch
        {
            EquipmentUISlotType.MainHand => "Main Hand",
            EquipmentUISlotType.OffHand => "Off Hand",
            EquipmentUISlotType.Helmet => "Helm",
            EquipmentUISlotType.Body => "Body",
            EquipmentUISlotType.Boots => "Boots",
            EquipmentUISlotType.Trinket => "Trinket",
            EquipmentUISlotType.Pendant => "Pendant",
            EquipmentUISlotType.Ring1 => "Ring 1",
            EquipmentUISlotType.Ring2 => "Ring 2",
            EquipmentUISlotType.Toolbelt0 => "Tool 1",
            EquipmentUISlotType.Toolbelt1 => "Tool 2",
            EquipmentUISlotType.Toolbelt2 => "Tool 3",
            EquipmentUISlotType.Toolbelt3 => "Tool 4",
            _ => "Slot"
        };
    }

    private string GetDisplayLabel()
    {
        if (_def == null)
            return GetTitle();

        if (slotType == EquipmentUISlotType.OffHand && _def.IsCombatSupport && equipment != null)
        {
            int amount = Mathf.Max(1, equipment.OffHandStackAmount);
            return amount > 1 ? $"x{amount}" : "";
        }

        return "";
    }

    private int GetEquippedAmountForThisSlot()
    {
        if (_def == null)
            return 1;

        if (slotType == EquipmentUISlotType.OffHand && _def.IsCombatSupport && equipment != null)
            return Mathf.Max(1, equipment.OffHandStackAmount);

        return 1;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        if (background) background.color = hoverColor;
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
        tooltip.ShowAt(transform, _def, GetEquippedAmountForThisSlot(), compact);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        if (background) background.color = idleColor;
        tooltip?.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (InventoryDragState.HasDrag || EquipDragState.HasDrag)
            return;

        float t = Time.unscaledTime;
        bool doubleClick = (t - _lastClickTime) <= doubleClickSeconds;
        _lastClickTime = t;

        if (!doubleClick) return;

        DoubleClickReturnToInventory();
        eventData.Use();
    }

    private void DoubleClickReturnToInventory()
    {
        if (!_bound) return;
        if (string.IsNullOrWhiteSpace(_itemId)) return;

        int amountToReturn = GetEquippedAmountForThisSlot();

        bool ok = inventory.Add(_itemId, amountToReturn);
        if (!ok) return;

        ClearThisSlot();
        RefreshFromState();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!_bound) return;
        if (!InventoryDragState.HasDrag) return;
        if (inventory == null || equipment == null) return;

        string draggedId = InventoryDragState.ItemId;
        if (string.IsNullOrWhiteSpace(draggedId)) return;

        var draggedDef = inventory.GetItemDef(draggedId);
        if (!draggedDef) return;

        int fromSlot = InventoryDragState.FromSlotIndex;
        if (fromSlot < 0) return;

        int draggedAmount = InventoryDragState.IsSplit
            ? Mathf.Max(1, InventoryDragState.CarriedAmount)
            : Mathf.Max(1, inventory.GetSlot(fromSlot).amount);

        bool accept = false;

        if (slotType == EquipmentUISlotType.MainHand)
            accept = equipment.CanEquip(draggedId, EquipSlot.MainHand);
        else if (slotType == EquipmentUISlotType.OffHand)
            accept = equipment.CanEquip(draggedId, EquipSlot.OffHand);
        else if (slotType == EquipmentUISlotType.Ring1 || slotType == EquipmentUISlotType.Ring2)
            accept = equipment.CanEquip(draggedId, EquipSlot.Ring);
        else
        {
            int toolIndex = GetToolbeltIndex();
            if (toolIndex >= 0 && toolbelt != null)
            {
                bool isTool =
                    draggedDef.itemKind == ItemKind.Tool &&
                    draggedDef.handVisualKey != ToolKey.None &&
                    draggedDef.handVisualKey != ToolKey.Weapon;

                accept = isTool && !toolbelt.Contains(draggedId);
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

        if (!accept) return;

        // MAIN HAND
        if (slotType == EquipmentUISlotType.MainHand)
        {
            if (inventory.RemoveAmountAtSlot(fromSlot, 1) != 1)
                return;

            string prev = equipment.MainHandItemId;
            equipment.EquipMainHand(draggedId);

            if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                ReturnOrDrop(prev, 1);

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
                if (inventory.RemoveAmountAtSlot(fromSlot, draggedAmount) != draggedAmount)
                    return;

                equipment.EquipOffHand(draggedId, draggedAmount);

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

            if (inventory.RemoveAmountAtSlot(fromSlot, equipAmount) != equipAmount)
                return;

            equipment.EquipOffHand(draggedId, equipAmount);

            if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                ReturnOrDrop(prev, prevAmount);

            eventData.Use();
            return;
        }

        // TOOLBELT
        int idx = GetToolbeltIndex();
        if (idx >= 0 && toolbelt != null)
        {
            if (inventory.RemoveAmountAtSlot(fromSlot, 1) != 1)
                return;

            string prev = toolbelt.GetToolItemId(idx);
            toolbelt.SetToolItemId(idx, draggedId);

            if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                ReturnOrDrop(prev, 1);

            eventData.Use();
            return;
        }

        // RING1 / RING2
        if (slotType == EquipmentUISlotType.Ring1 || slotType == EquipmentUISlotType.Ring2)
        {
            if (inventory.RemoveAmountAtSlot(fromSlot, 1) != 1)
                return;

            int ringIndex = (slotType == EquipmentUISlotType.Ring1) ? 0 : 1;

            string prev = equipment.GetEquippedItemId(EquipSlot.Ring, ringIndex);
            equipment.EquipGear(EquipSlot.Ring, draggedId, ringIndex);

            if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                ReturnOrDrop(prev, 1);

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
                if (inventory.RemoveAmountAtSlot(fromSlot, 1) != 1)
                    return;

                string prev = equipment.GetEquippedItemId(gearSlot);
                equipment.EquipGear(gearSlot, draggedId);

                if (!string.IsNullOrWhiteSpace(prev) && prev != draggedId)
                    ReturnOrDrop(prev, 1);

                eventData.Use();
                return;
            }
        }
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

        if (background) background.color = pressedColor;

        CreateDragIcon(def.icon);
        UpdateDragIconPosition(eventData);

        if (background) background.color = _isPointerOver ? hoverColor : idleColor;
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

        bool ok = inventory.Add(itemId, amount);
        if (ok) return;

        var def = inventory.GetItemDef(itemId);
        if (DropManager.Instance != null)
            DropManager.Instance.Spawn(itemId, amount, def ? def.icon : null);
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