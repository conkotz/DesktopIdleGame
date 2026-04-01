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
    [SerializeField] private Image background;
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
        if (!background) background = GetComponent<Image>();

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

        RefreshRarityBorder(def);

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
            {
                int qty = _merchant != null ? _merchant.GetQuantity(_entry) : _entry.quantity;
                stockText.text = qty < 0 ? "∞" : (qty == 0 ? "Sold Out" : $"x{qty}");
            }
        }

        if (button)
        {
            int qty = _entry != null ? (_merchant != null ? _merchant.GetQuantity(_entry) : _entry.quantity) : 0;
            bool inStock = _entry != null && qty != 0;
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