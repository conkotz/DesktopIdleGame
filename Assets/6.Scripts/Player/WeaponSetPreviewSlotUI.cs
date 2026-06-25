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

    private bool _labelLayoutCaptured;
    private Vector2 _defaultLabelAnchorMin;
    private Vector2 _defaultLabelAnchorMax;
    private Vector2 _defaultLabelPivot;
    private Vector2 _defaultLabelAnchoredPosition;
    private Vector2 _defaultLabelSizeDelta;
    private TextAlignmentOptions _defaultLabelAlignment;

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

        CaptureDefaultLabelLayout();

        ApplyVisuals();
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
        slotType == PreviewSlotType.OffHandInactive && _def != null && _def.ShowsOffHandStackCount;

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
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.sizeDelta = new Vector2(96f, 22f);
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

    private void OnEnable()
    {
        if (MainMenuUIPrewarm.UseBatchedInstantiation)
            return;

        TryBind();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        tooltip?.Hide();
    }

    public void PrewarmBinding()
    {
        TryBind();
        Subscribe();
        Refresh();
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
        {
            label.text = GetDisplayLabel();
            ApplyLabelLayoutForCurrentState();
        }

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
            PreviewSlotType.MainHandInactive => "Main hand 2",
            PreviewSlotType.OffHandInactive => "Offhand 2",
            _ => "Slot"
        };
    }

    private string GetDisplayLabel()
    {
        if (_def == null)
            return GetTitle();

        if (slotType == PreviewSlotType.OffHandInactive && _def.ShowsOffHandStackCount && equipment != null)
        {
            int amount = Mathf.Max(1, equipment.GetInactiveOffHandStackAmount());
            return $"Set 2 · x{amount}";
        }

        return "";
    }

    private int GetPreviewAmount()
    {
        if (_def == null)
            return 1;

        if (slotType == PreviewSlotType.OffHandInactive && _def.ShowsOffHandStackCount && equipment != null)
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
        {
            label.text = GetTitle();
            ApplyLabelLayoutForCurrentState();
        }

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