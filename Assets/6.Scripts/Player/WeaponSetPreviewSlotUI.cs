using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WeaponSetPreviewSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum PreviewSlotType
    {
        MainHandInactive,
        OffHandInactive
    }

    [Header("Type")]
    [SerializeField] private PreviewSlotType slotType;

    [Header("UI")]
    [SerializeField] private Image hitTarget;   // transparent root image
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private RectTransform equipmentWindowRect;
    private Transform _tooltipAnchor;

    [Header("Visuals")]
    [SerializeField, Range(0f, 1f)] private float iconAlpha = 0.40f;
    [SerializeField, Range(0f, 1f)] private float labelAlpha = 0.75f;

    [Header("Refs")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;

    private Action<string> _mainCb;
    private Action<string> _offCb;
    private Action<EquipmentUISlotType, string> _uiSlotCb;

    private bool _subscribed;
    private string _itemId;
    private ItemDefinition _def;

    private void Awake()
    {
        if (!equipment) equipment = FindFirstObjectByType<EquipmentManager>();
        if (!inventory && equipment) inventory = equipment.Inventory;
        if (!tooltip) tooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);

        if (hitTarget)
        {
            var c = hitTarget.color;
            c.a = 0f; // invisible hit area
            hitTarget.color = c;
            hitTarget.raycastTarget = true;
        }

        if (icon) icon.raycastTarget = false;
        if (label) label.raycastTarget = false;

        ApplyVisuals();
    }

    private void OnEnable()
    {
        TryBind();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        tooltip?.Hide();
    }

    private void TryBind()
    {
        if (!equipment) equipment = FindFirstObjectByType<EquipmentManager>();
        if (!inventory && equipment) inventory = equipment.Inventory;
        if (!tooltip) tooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
    }

    private void Subscribe()
    {
        if (_subscribed || equipment == null) return;

        _mainCb ??= _ => Refresh();
        _offCb ??= _ => Refresh();
        _uiSlotCb ??= (_, __) => Refresh();

        equipment.OnMainHandChanged += _mainCb;
        equipment.OnOffHandChanged += _offCb;
        equipment.OnUISlotChanged += _uiSlotCb;

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

        _subscribed = false;
    }

    private void Refresh()
    {
        if (equipment == null || inventory == null)
        {
            SetEmpty();
            return;
        }

        _itemId = GetPreviewItemId();
        _def = string.IsNullOrWhiteSpace(_itemId) ? null : inventory.GetItemDef(_itemId);

        if (icon)
        {
            bool hasIcon = _def != null && _def.icon != null;
            icon.enabled = hasIcon;
            icon.sprite = hasIcon ? _def.icon : null;
            icon.preserveAspect = true;
        }

        if (label)
            label.text = GetDisplayLabel();

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (icon)
        {
            Color c = icon.color;
            c.a = iconAlpha;
            icon.color = c;
        }

        if (label)
        {
            Color c = label.color;
            c.a = labelAlpha;
            label.color = c;
        }
    }

    private string GetPreviewItemId()
    {
        if (equipment == null) return null;

        return slotType switch
        {
            PreviewSlotType.MainHandInactive => equipment.GetInactiveMainHandItemId(),
            PreviewSlotType.OffHandInactive => equipment.GetInactiveOffHandItemId(),
            _ => null
        };
    }

    private string GetTitle()
    {
        return slotType switch
        {
            PreviewSlotType.MainHandInactive => "Main Hand",
            PreviewSlotType.OffHandInactive => "Off Hand",
            _ => "Slot"
        };
    }

    private string GetDisplayLabel()
    {
        if (_def == null)
            return GetTitle();

        if (slotType == PreviewSlotType.OffHandInactive && _def.IsCombatSupport && equipment != null)
        {
            int amount = Mathf.Max(1, equipment.GetInactiveOffHandStackAmount());
            return $"x{amount}";
        }

        return "";
    }

    private int GetPreviewAmount()
    {
        if (_def == null)
            return 1;

        if (slotType == PreviewSlotType.OffHandInactive && _def.IsCombatSupport && equipment != null)
            return Mathf.Max(1, equipment.GetInactiveOffHandStackAmount());

        return 1;
    }

    private void SetEmpty()
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

        ApplyVisuals();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip == null || _def == null)
            return;

        var flipper = tooltip.GetComponent<FlipInsideBounds>();
        if (flipper)
        {
            flipper.SetPreferredSide(FlipInsideBounds.PreferredSide.Left);
            flipper.SetMeasureRect(equipmentWindowRect);
        }

        bool compact = !_def.IsCombatSupport;
        tooltip.ShowAt(GetTooltipAnchor(), _def, GetPreviewAmount(), compact);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltip?.Hide();
    }

    private Transform GetTooltipAnchor()
    {
        if (_tooltipAnchor) return _tooltipAnchor;

        var t = transform;
        while (t != null)
        {
            var a = t.Find("ToolTipAnchor");
            if (a)
            {
                _tooltipAnchor = a;
                return _tooltipAnchor;
            }
            t = t.parent;
        }

        return transform;
    }
}