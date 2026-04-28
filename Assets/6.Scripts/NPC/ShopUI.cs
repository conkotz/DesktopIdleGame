using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image panelRootImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ShopSlotUI slotPrefab;
    [SerializeField] private Button buy1xButton;
    [SerializeField] private Button buy50xButton;
    [SerializeField] private Button buybackToggleButton;
    [Header("Buy Toggle Visuals")]
    [SerializeField] private bool enableButtonTint = false;
    [SerializeField] private Image buy1xButtonImage;
    [SerializeField] private Image buy50xButtonImage;
    [SerializeField] private TMP_Text buy1xButtonText;
    [SerializeField] private TMP_Text buy50xButtonText;
    [SerializeField] private Color buySelectedColor = new Color(0.20f, 0.60f, 0.20f, 1f);
    [SerializeField] private Color buyUnselectedColor = Color.white;
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private Color unselectedTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Vector3 selectedScale = new Vector3(1.05f, 1.05f, 1f);
    [SerializeField] private Vector3 unselectedScale = Vector3.one;

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI shopTooltip;
    [SerializeField] private RectTransform shopWindowRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    [Header("Pointer blocking")]
    [Tooltip("When set, these graphics always receive raycasts while the shop is open (blocks clicks to the game). Panel Root Image is included automatically. Use for extra backdrop/underlay Images.")]
    [SerializeField] private Graphic[] additionalPointerBlockingGraphics;

    private readonly List<ShopSlotUI> _spawned = new();
    private Merchant _currentMerchant;
    private int _buyAmount = 1;
    public bool IsOpen => panelRoot != null && panelRoot.activeInHierarchy;

    private void Awake()
    {
        TryResolveRefs();

        if (!panelRootImage && panelRoot)
            panelRootImage = panelRoot.GetComponent<Image>();

        // Root panel image should block clicks from reaching the world (EventSystem). Stripping this was causing click-through.
        if (panelRootImage)
            panelRootImage.raycastTarget = true;

        if (buy1xButton)
        {
            buy1xButton.onClick.RemoveAllListeners();
            buy1xButton.onClick.AddListener(SetBuyAmount1x);
        }

        if (buy50xButton)
        {
            buy50xButton.onClick.RemoveAllListeners();
            buy50xButton.onClick.AddListener(SetBuyAmount50x);
        }

        if (buybackToggleButton)
        {
            buybackToggleButton.onClick.RemoveAllListeners();
            buybackToggleButton.onClick.AddListener(ToggleBuybackPanel);
        }

        if (!buy1xButtonImage && buy1xButton)
            buy1xButtonImage = buy1xButton.GetComponent<Image>();

        if (!buy50xButtonImage && buy50xButton)
            buy50xButtonImage = buy50xButton.GetComponent<Image>();

        if (!buy1xButtonText && buy1xButton)
            buy1xButtonText = buy1xButton.GetComponentInChildren<TMP_Text>(true);

        if (!buy50xButtonText && buy50xButton)
            buy50xButtonText = buy50xButton.GetComponentInChildren<TMP_Text>(true);

        UpdateBuyToggleVisuals();

        if (panelRoot)
            panelRoot.SetActive(false);

        if (!shopWindowRect && panelRoot)
            shopWindowRect = panelRoot.transform as RectTransform;

        if (!shopTooltip)
            shopTooltip = FindShopTooltip();

        RefreshShopRaycastTargets();
    }

    private void TryResolveRefs()
    {
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (!wallet)
            wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
    }

    private SharedTooltipUI FindShopTooltip()
    {
        SharedTooltipUI[] allTooltips = FindObjectsByType<SharedTooltipUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

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

    public void Open(Merchant merchant)
    {
        TryResolveRefs();

        if (!shopTooltip)
            shopTooltip = FindShopTooltip();

        if (!inventory || !wallet)
        {
            Debug.LogError("[ShopUI] Missing Inventory/Wallet.");
            return;
        }

        if (!merchant || merchant.Stock == null)
        {
            Debug.LogWarning("[ShopUI] No merchant/stock provided.");
            return;
        }

        SetCurrentMerchant(merchant);
        // Requirement: each time a shop opens, default back to 1x.
        SetBuyAmountInternal(1);

        if (titleText)
            titleText.text = merchant.MerchantName;

        if (panelRoot)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        Rebuild(merchant);
        RefreshShopRaycastTargets();
    }

    public void Close()
    {
        SetCurrentMerchant(null);
        shopTooltip?.Hide();

        if (panelRoot)
            panelRoot.SetActive(false);
    }

    private void Rebuild(Merchant merchant)
    {
        shopTooltip?.Hide();

        foreach (var s in _spawned)
        {
            if (s) Destroy(s.gameObject);
        }
        _spawned.Clear();

        if (merchant == null || merchant.Stock == null)
            return;

        foreach (var entry in merchant.Stock.Items)
        {
            if (string.IsNullOrWhiteSpace(entry.itemId))
                continue;

            var def = inventory.GetItemDef(entry.itemId);
            if (!def)
                continue;

            var slot = Instantiate(slotPrefab, contentRoot);

            slot.Bind(
                this,
                merchant,
                entry,
                def,
                shopTooltip,
                shopWindowRect,
                preferredSide
            );

            _spawned.Add(slot);
        }

        ForceLayoutRefresh();
        RefreshShopRaycastTargets();
    }

    private void RefreshShopRaycastTargets()
    {
        if (!panelRoot)
            return;

        Graphic[] graphics = panelRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (!g)
                continue;

            // Interactive controls (buttons, slots) + explicit backdrop blockers must receive raycasts.
            bool isInteractive = g.GetComponentInParent<Selectable>(true) != null;
            // UIDragWindow lives on a header Image that is NOT a Selectable; without this, raycastTarget
            // stays false and the bar is click-through — drag never starts (see RefreshShopRaycastTargets).
            bool isDragHandle = g.GetComponentInParent<UIDragWindow>(true) != null;
            bool isResizeHandle = g.GetComponentInParent<UIWindowResizeHandle>(true) != null;
            g.raycastTarget = isInteractive || isDragHandle || isResizeHandle || IsPointerBlockingGraphic(g);
        }
    }

    private bool IsPointerBlockingGraphic(Graphic g)
    {
        if (panelRootImage && g == panelRootImage)
            return true;

        if (additionalPointerBlockingGraphics == null)
            return false;

        for (int i = 0; i < additionalPointerBlockingGraphics.Length; i++)
        {
            if (additionalPointerBlockingGraphics[i] == g)
                return true;
        }

        return false;
    }

    private void ForceLayoutRefresh()
    {
        if (!contentRoot) return;

        var rect = contentRoot as RectTransform;
        if (!rect) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        var parentRect = rect.parent as RectTransform;
        if (parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        Canvas.ForceUpdateCanvases();
    }

    public void TryBuy(Merchant merchant, MerchantStock.Entry entry)
    {
        if (merchant == null || entry == null)
            return;

        int amount = Mathf.Max(1, _buyAmount);
        bool success = merchant.TryBuy(entry.itemId, amount);

        if (!success)
        {
            Debug.Log($"[ShopUI] Purchase failed ({amount}x).");
            return;
        }

        Rebuild(merchant);
    }

    private void SetCurrentMerchant(Merchant merchant)
    {
        if (_currentMerchant != null)
            _currentMerchant.StockChanged -= HandleMerchantStockChanged;

        _currentMerchant = merchant;

        if (_currentMerchant != null)
            _currentMerchant.StockChanged += HandleMerchantStockChanged;
    }

    private void HandleMerchantStockChanged(Merchant merchant)
    {
        if (merchant == null || merchant != _currentMerchant)
            return;

        if (!panelRoot || !panelRoot.activeInHierarchy)
            return;

        Rebuild(merchant);
    }

    private void OnDisable()
    {
        if (_currentMerchant != null)
            _currentMerchant.StockChanged -= HandleMerchantStockChanged;
    }

    private void OnDestroy()
    {
        if (_currentMerchant != null)
            _currentMerchant.StockChanged -= HandleMerchantStockChanged;
    }

    public void SetBuyAmount1x() => SetBuyAmountInternal(1);
    public void SetBuyAmount50x() => SetBuyAmountInternal(50);

    private void SetBuyAmountInternal(int amount)
    {
        _buyAmount = Mathf.Max(1, amount);
        UpdateBuyToggleVisuals();
    }

    private void UpdateBuyToggleVisuals()
    {
        // Make selected option non-interactable to indicate active toggle state.
        bool oneSelected = _buyAmount == 1;
        bool fiftySelected = _buyAmount == 50;

        if (buy1xButton) buy1xButton.interactable = !oneSelected;
        if (buy50xButton) buy50xButton.interactable = !fiftySelected;

        if (enableButtonTint && buy1xButtonImage)
            buy1xButtonImage.color = oneSelected ? buySelectedColor : buyUnselectedColor;

        if (enableButtonTint && buy50xButtonImage)
            buy50xButtonImage.color = fiftySelected ? buySelectedColor : buyUnselectedColor;

        if (buy1xButtonText)
            buy1xButtonText.color = oneSelected ? selectedTextColor : unselectedTextColor;

        if (buy50xButtonText)
            buy50xButtonText.color = fiftySelected ? selectedTextColor : unselectedTextColor;

        if (buy1xButton)
            buy1xButton.transform.localScale = oneSelected ? selectedScale : unselectedScale;

        if (buy50xButton)
            buy50xButton.transform.localScale = fiftySelected ? selectedScale : unselectedScale;
    }

    private void ToggleBuybackPanel()
    {
        if (SaleUndoManager.Instance == null)
            return;

        SaleUndoManager.Instance.ToggleUndoPanelVisibility();
    }
}