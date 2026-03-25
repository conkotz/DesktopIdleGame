using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private Button button;

    [Header("Tooltip (shared)")]
    [SerializeField] private SharedTooltipUI tooltip;

    private RectTransform _shopWindowRect;
    private FlipInsideBounds.PreferredSide _preferredSide = FlipInsideBounds.PreferredSide.Left;

    private ShopUI _shop;
    private Merchant _merchant;
    private MerchantStock.Entry _entry;
    private ItemDefinition _def;

    private bool _isPointerOver;

    public ItemDefinition Definition => _def;
    public MerchantStock.Entry Entry => _entry;

    private void Awake()
    {
        if (icon) icon.raycastTarget = false;
        if (priceText) priceText.raycastTarget = false;
        if (stockText) stockText.raycastTarget = false;
    }

    private void OnDisable()
    {
        _isPointerOver = false;
        tooltip?.Hide();
    }

    private void OnDestroy()
    {
        if (_isPointerOver)
            tooltip?.Hide();
    }

    public void Bind(
        ShopUI shop,
        Merchant merchant,
        MerchantStock.Entry entry,
        ItemDefinition def,
        SharedTooltipUI sharedTooltip,
        RectTransform shopWindowRect,
        FlipInsideBounds.PreferredSide preferredSide)
    {
        _shop = shop;
        _merchant = merchant;
        _entry = entry;
        _def = def;

        tooltip = sharedTooltip;
        _shopWindowRect = shopWindowRect;
        _preferredSide = preferredSide;

        if (icon)
        {
            icon.sprite = def ? def.icon : null;
            icon.enabled = def && def.icon != null;
            icon.preserveAspect = true;
        }

        if (priceText)
        {
            priceText.text = GetCompactPriceText();

            bool canAfford = _merchant != null && _entry != null && _merchant.CanAfford(_entry);
            priceText.color = canAfford ? Color.white : Color.red;
        }

        if (stockText)
        {
            stockText.gameObject.SetActive(true);

            if (_entry == null)
                stockText.text = "";
            else
                stockText.text = _entry.quantity < 0 ? "∞" : (_entry.quantity == 0 ? "Sold Out" : $"x{_entry.quantity}");
        }

        if (button)
        {
            bool inStock = _entry != null && _entry.quantity != 0;
            button.interactable = def != null && inStock;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                tooltip?.Hide();

                if (_shop != null && _merchant != null && _entry != null)
                    _shop.TryBuy(_merchant, _entry);
            });
        }

        RefreshHoveredTooltip();
    }

    private string GetCompactPriceText()
    {
        if (_merchant == null || _entry == null || _entry.costs == null || _entry.costs.Count == 0)
            return "Free";

        if (_entry.costs.Count == 1)
        {
            var cost = _entry.costs[0];
            if (cost.type == MerchantStock.CostType.Gold)
                return $"{cost.amount}g";

            return $"{cost.amount}x";
        }

        return "Cost";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        tooltip?.Hide();
    }

    private bool CanShowTooltip()
    {
        return tooltip != null &&
               _def != null &&
               _entry != null &&
               isActiveAndEnabled;
    }

    private void RefreshHoveredTooltip()
    {
        if (!_isPointerOver)
            return;

        if (CanShowTooltip())
            ShowTooltip();
        else
            tooltip?.Hide();
    }

    public void ShowTooltip()
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

            if (_shopWindowRect)
            {
                flipper.SetMeasureRect(_shopWindowRect);
                flipper.SetHeightRect(_shopWindowRect);
            }
        }

        string customPrice = (_merchant != null && _entry != null)
    ? _merchant.GetPriceTooltipText(_entry)
    : null;

        tooltip.ShowAt(
            transform,
            _def,
            1,
            compact: false,
            valueOverride: null,
            valueLabelOverride: "Cost",
            customValueOverride: customPrice
        );
    }
}