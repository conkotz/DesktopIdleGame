using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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

    [Header("Selection (shop)")]
    [Tooltip("Recolors the rarity outline while this slot is selected (no second outline).")]
    [SerializeField] private Color selectedRarityBorderColor = new Color32(145, 108, 28, 255);

    [Header("Rarity slot panel (button target graphic)")]
    [Tooltip("How much the slot fill blends from the dark base toward the rarity accent when an item is shown (higher = less grey/muddy undertone).")]
    [Range(0f, 1f)]
    [SerializeField] private float rarityPanelAccentBlend = 0.92f;

    [SerializeField] private Color rarityPanelDarkBase = new Color(0.08f, 0.09f, 0.11f, 1f);

    [Header("Sold out")]
    [Range(0f, 1f)]
    [SerializeField] private float soldOutIconAlpha = 0.75f;

    [Header("Tooltip (shared)")]
    [SerializeField] private SharedTooltipUI tooltip;

    private RectTransform _shopWindowRect;
    private FlipInsideBounds.PreferredSide _preferredSide = FlipInsideBounds.PreferredSide.Left;

    private ShopUI _shop;
    private Merchant _merchant;
    private MerchantStock.Entry _entry;
    private ItemDefinition _def;

    private bool _isPointerOver;
    private bool _slotSelected;

    public ItemDefinition Definition => _def;
    public MerchantStock.Entry Entry => _entry;
    public bool IsInStock =>
        _merchant != null && _entry != null && _merchant.GetQuantity(_entry) != 0;

    public void PerformBuy1Action()
    {
        if (_shop == null || _merchant == null || _entry == null || !IsInStock)
            return;

        _shop.NotifySlotSelected(this);
        _shop.TryBuy(_merchant, _entry, 1);
    }

    public void PerformBuy50Action()
    {
        if (_shop == null || _merchant == null || _entry == null || !IsInStock)
            return;

        _shop.NotifySlotSelected(this);
        _shop.TryBuy(_merchant, _entry, 50);
    }

    private void OpenContextMenu(PointerEventData eventData)
    {
        if (_def == null)
            return;

        tooltip?.Hide();
        ContextMenuUI.EnsureInstance().ShowAtScreen(
            ShopContextMenuBuilder.BuildForShopSlot(this),
            eventData != null ? eventData.position : (Vector2?)null,
            _def.displayName);
    }

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

        int qtyForVisual = _entry != null ? (_merchant != null ? _merchant.GetQuantity(_entry) : _entry.defaultQuantity) : 0;
        bool soldOut = def != null && _entry != null && qtyForVisual >= 0 && qtyForVisual == 0;

        if (icon)
        {
            icon.sprite = def ? def.icon : null;
            icon.enabled = def && def.icon != null;
            icon.preserveAspect = true;

            Color ic = icon.color;
            ic.a = soldOut ? soldOutIconAlpha : 1f;
            icon.color = ic;
        }

        UpdateRarityOutline();

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
                stockText.text = qtyForVisual < 0 ? "∞" : (qtyForVisual == 0 ? "Sold Out" : $"x{qtyForVisual}");

            Color st = stockText.color;
            st.a = 1f;
            stockText.color = st;
        }

        if (button)
        {
            // Sold-out entries stay clickable so the selection panel can show details.
            button.interactable = def != null;

            button.onClick.RemoveAllListeners();

            ApplyRarityPanelColors(def);
        }

        RefreshHoveredTooltip();
    }

    /// <summary>Highlights this slot when the player has selected it for purchasing.</summary>
    public void SetSlotSelected(bool selected)
    {
        _slotSelected = selected;
        UpdateRarityOutline();
    }

    private void UpdateRarityOutline()
    {
        if (!showRarityBorder || !rarityOutline)
        {
            if (rarityOutline) rarityOutline.enabled = false;
            return;
        }

        if (_def == null)
        {
            rarityOutline.enabled = false;
            return;
        }

        rarityOutline.enabled = true;
        rarityOutline.useGraphicAlpha = false;
        rarityOutline.effectDistance = rarityBorderThickness;

        if (_slotSelected)
        {
            rarityOutline.effectColor = selectedRarityBorderColor;
            return;
        }

        rarityOutline.effectColor = _def.rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => uncommonBorder,
            ItemRarity.Rare => rareBorder,
            ItemRarity.Epic => epicBorder,
            ItemRarity.Legendary => legendaryBorder,
            _ => Color.white
        };
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_def == null || _shop == null || _merchant == null || _entry == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            OpenContextMenu(eventData);
            eventData.Use();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!button)
            return;

        int qty = _merchant.GetQuantity(_entry);
        bool inStock = qty != 0;

        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrlHeld)
        {
            _shop.NotifySlotSelected(this);
            if (inStock)
                _shop.TryBuy(_merchant, _entry, 1);
            return;
        }

        _shop.NotifySlotSelected(this);
    }

    private void ApplyRarityPanelColors(ItemDefinition def)
    {
        if (!button)
            return;

        if (button.targetGraphic is Image targetGraphic)
            targetGraphic.color = Color.white;

        Color baseFill = GetRarityPanelBaseColor(def);
        ColorBlock cb = button.colors;
        cb.normalColor = baseFill;
        cb.highlightedColor = Color.Lerp(baseFill, Color.white, 0.14f);
        cb.pressedColor = Color.Lerp(baseFill, Color.black, 0.18f);
        cb.selectedColor = cb.highlightedColor;
        Color dim = Color.Lerp(baseFill, Color.black, 0.22f);
        cb.disabledColor = new Color(dim.r, dim.g, dim.b, 0.92f);
        cb.colorMultiplier = 1f;
        button.colors = cb;
    }

    private Color GetRarityPanelBaseColor(ItemDefinition def)
    {
        if (def == null)
            return new Color(0.118f, 0.133f, 0.165f, 1f);

        Color accent = def.rarity switch
        {
            ItemRarity.Common => new Color(0.14f, 0.15f, 0.17f, 1f),
            ItemRarity.Uncommon => uncommonBorder,
            ItemRarity.Rare => rareBorder,
            ItemRarity.Epic => epicBorder,
            ItemRarity.Legendary => legendaryBorder,
            _ => Color.white
        };

        if (def.rarity == ItemRarity.Common)
            return accent;

        float t = Mathf.Clamp01(rarityPanelAccentBlend);
        return Color.Lerp(rarityPanelDarkBase, accent, t);
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
            customValueOverride: customPrice,
            maskUnrolledRandomStats: _def != null && _def.HasRandomStatPool
        );
    }
}