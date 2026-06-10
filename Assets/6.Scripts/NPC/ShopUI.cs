using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Outer ShopWindow root (inventory + shop section). When empty, resolved from panelRoot's parent.")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image panelRootImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ShopSlotUI slotPrefab;
    [SerializeField] private Button buy1xButton;
    [SerializeField] private Button buy50xButton;
    [SerializeField] private Button buybackToggleButton;
    [SerializeField] private Button restockButton;
    [SerializeField] private TMP_Text restockButtonLabel;
    [Tooltip("Full-window undo grid; swaps with this shop when the Undo control is used.")]
    [SerializeField] private UndoShopWindowUI undoShopWindow;
    [Header("Undo Window Activation")]
    [Tooltip("Allows using an UndoShopWindow GameObject that starts disabled in hierarchy. Enabled automatically when opening Undo.")]
    [SerializeField] private bool enableDisabledUndoWindowOnUse = true;
    [Header("Buy button visuals")]
    [SerializeField] private Image buy1xButtonImage;
    [SerializeField] private Image buy50xButtonImage;
    [SerializeField] private TMP_Text buy1xButtonText;
    [SerializeField] private TMP_Text buy50xButtonText;
    [SerializeField] private Color unselectedTextColor = new Color32(174, 162, 148, 255);
    [SerializeField] private Color buyButtonsDisabledTextColor = new Color32(174, 162, 148, 115);
    [SerializeField] private Vector3 unselectedScale = Vector3.one;

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI shopTooltip;
    [SerializeField] private RectTransform shopWindowRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    [Header("Pointer blocking")]
    [Tooltip("Top drag strip on ShopWindow. When empty, resolved from windowRoot/HeaderDragWindow.")]
    [SerializeField] private RectTransform shopDragHandle;
    [Tooltip("When set, these graphics always receive raycasts while the shop is open (blocks clicks to the game). Panel Root Image is included automatically. Use for extra backdrop/underlay Images.")]
    [SerializeField] private Graphic[] additionalPointerBlockingGraphics;

    [Header("Selection")]
    [Tooltip("Optional. Full-area graphic behind shop item slots (earlier sibling = underneath). Left-click clears the selected item so Buy buttons grey out. Leave empty to only clear on shop close.")]
    [SerializeField] private Graphic clearSelectionWhenClicked;

    [Header("Selection detail bar")]
    [SerializeField] private ItemSelectionPanel itemSelectionPanel;

    [Header("Shop item grid")]
    [Tooltip("Fixed column count (items wrap to new rows). Cell size is computed from the SlotsGrid rect so the grid fits the panel.")]
    [SerializeField] private int shopGridColumns = 6;
    [Tooltip("Used with column count to pick a square cell size that fits this many rows in the grid area (matches inventory-style sizing).")]
    [SerializeField] private int shopGridRowsForFit = 3;
    [Tooltip(
        "When off (default), gap between slots comes from the Grid Layout Group on contentRoot — change Spacing there and it will stick. " +
        "When on, ShopUI pushes the vector below every layout (overrides the Grid Layout Group).")]
    [SerializeField] private bool useGridSpacingFromShopUI;
    [SerializeField] private Vector2 shopGridSpacing = new Vector2(10f, 10f);
    [SerializeField] private bool shopSquareCells = true;
    [SerializeField] private float shopMinCellSize = 48f;
    [SerializeField] private int shopGridLayoutRetryFrames = 3;

    private readonly List<ShopSlotUI> _spawned = new();
    private GridLayoutGroup _shopGrid;
    private Coroutine _shopGridLayoutRetry;
    private Merchant _currentMerchant;
    private string _selectedShopItemId;
    private QuestProgressManager _questProgressEventsTarget;
    private GameObject _resolvedWindowRoot;
    private HashSet<Graphic> _pointerBlockingGraphics;

    public bool IsOpen
    {
        get
        {
            GameObject root = ResolveWindowRoot();
            return root != null && root.activeInHierarchy;
        }
    }

    /// <summary>Outer combined shop window (inventory + merchant stock).</summary>
    public RectTransform WindowRectTransform => ResolveWindowRoot() != null
        ? ResolveWindowRoot().transform as RectTransform
        : null;

    /// <summary>Root <see cref="RectTransform"/> of the shop item section (ShopSection).</summary>
    public RectTransform PanelRectTransform => panelRoot != null ? panelRoot.transform as RectTransform : null;

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
            buy1xButton.onClick.AddListener(OnBuy1Clicked);
        }

        if (buy50xButton)
        {
            buy50xButton.onClick.RemoveAllListeners();
            buy50xButton.onClick.AddListener(OnBuy50Clicked);
        }

        if (buybackToggleButton)
        {
            buybackToggleButton.onClick.RemoveAllListeners();
            buybackToggleButton.onClick.AddListener(ToggleBuybackPanel);
        }

        if (!restockButton && panelRoot)
        {
            Transform t = panelRoot.transform.Find("HeaderDragWindow/RestockShop");
            if (!t)
                t = panelRoot.transform.Find("RestockShop");
            if (t)
                restockButton = t.GetComponent<Button>();
        }

        if (restockButton)
        {
            restockButton.onClick.RemoveAllListeners();
            restockButton.onClick.AddListener(OnRestockClicked);
        }
        if (!restockButtonLabel && restockButton)
            restockButtonLabel = restockButton.GetComponentInChildren<TMP_Text>(true);

        if (!buy1xButtonImage && buy1xButton)
            buy1xButtonImage = buy1xButton.GetComponent<Image>();

        if (!buy50xButtonImage && buy50xButton)
            buy50xButtonImage = buy50xButton.GetComponent<Image>();

        if (!buy1xButtonText && buy1xButton)
            buy1xButtonText = buy1xButton.GetComponentInChildren<TMP_Text>(true);

        if (!buy50xButtonText && buy50xButton)
            buy50xButtonText = buy50xButton.GetComponentInChildren<TMP_Text>(true);

        WireClearSelectionBackdrop();
        ApplyBuyButtonVisualStyles();
        UpdateBuyButtonsForSelectionState();

        GameObject root = ResolveWindowRoot();
        if (root)
            root.SetActive(false);

        if (!shopWindowRect)
            shopWindowRect = WindowRectTransform ?? PanelRectTransform;

        if (!shopTooltip)
            shopTooltip = FindShopTooltip();

        if (!undoShopWindow)
            undoShopWindow = FindFirstObjectByType<UndoShopWindowUI>(FindObjectsInactive.Include);

        if (!itemSelectionPanel)
            itemSelectionPanel = GetComponentInChildren<ItemSelectionPanel>(true);

        if (contentRoot)
            _shopGrid = contentRoot.GetComponent<GridLayoutGroup>();

        ResolveShopDragHandle();
        CachePointerBlockingGraphics();
        RefreshShopRaycastTargets();
        EnsureShopDragHandleOnTop();
    }

    private void OnEnable()
    {
        if (SaleUndoManager.Instance != null)
            SaleUndoManager.Instance.EntriesChanged += OnSaleUndoEntriesChanged;
        TrySubscribeQuestProgress();
    }

    private void OnDisable()
    {
        if (SaleUndoManager.Instance != null)
            SaleUndoManager.Instance.EntriesChanged -= OnSaleUndoEntriesChanged;
        UnsubscribeQuestProgress();

        if (_shopGridLayoutRetry != null)
        {
            StopCoroutine(_shopGridLayoutRetry);
            _shopGridLayoutRetry = null;
        }

        if (_currentMerchant != null)
            _currentMerchant.StockChanged -= HandleMerchantStockChanged;
    }

    private void OnSaleUndoEntriesChanged()
    {
        if (IsOpen)
            RefreshUndoSaleButtonState();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (IsOpen && contentRoot)
            ApplyShopGridLayout();
    }

    private void WireClearSelectionBackdrop()
    {
        if (!clearSelectionWhenClicked)
            return;

        var receiver = clearSelectionWhenClicked.GetComponent<ShopClearSelectionReceiver>();
        if (!receiver)
            receiver = clearSelectionWhenClicked.gameObject.AddComponent<ShopClearSelectionReceiver>();
        receiver.Initialize(this);
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
        _selectedShopItemId = null;

        if (titleText)
            titleText.text = merchant.MerchantName;

        GameObject root = ResolveWindowRoot();
        if (root)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        if (panelRoot && panelRoot != root)
            panelRoot.SetActive(true);

        Rebuild(merchant);
        RefreshSelectionPanel();
        RefreshShopRaycastTargets();
        EnsureShopDragHandleOnTop();
        RefreshUndoSaleButtonState();
        RefreshRestockButtonState();
    }

    /// <summary>Keep the shop drag strip above panel siblings so raycasts reach <see cref="UIDragWindow"/>.</summary>
    public void EnsureShopDragHandleOnTop()
    {
        ResolveShopDragHandle();
        if (shopDragHandle)
            shopDragHandle.SetAsLastSibling();
    }

    public void Close()
    {
        ClearSlotSelection();
        SetCurrentMerchant(null);
        shopTooltip?.Hide();

        if (undoShopWindow && undoShopWindow.IsOpen)
            undoShopWindow.CloseWindow();

        GameObject root = ResolveWindowRoot();
        if (root)
            root.SetActive(false);

        RefreshRestockButtonState();
    }

    private GameObject ResolveWindowRoot()
    {
        if (_resolvedWindowRoot)
            return _resolvedWindowRoot;

        if (windowRoot)
        {
            _resolvedWindowRoot = windowRoot;
            return _resolvedWindowRoot;
        }

        if (panelRoot && panelRoot.transform.parent != null)
        {
            Transform parent = panelRoot.transform.parent;
            if (parent.name.Contains("ShopWindow", System.StringComparison.OrdinalIgnoreCase))
            {
                _resolvedWindowRoot = parent.gameObject;
                return _resolvedWindowRoot;
            }
        }

        _resolvedWindowRoot = panelRoot;
        return _resolvedWindowRoot;
    }

    /// <summary>Called by <see cref="ShopSlotUI"/> when the player selects a purchasable item.</summary>
    public void NotifySlotSelected(ShopSlotUI slot)
    {
        if (slot?.Entry == null || string.IsNullOrWhiteSpace(slot.Entry.itemId))
            return;

        _selectedShopItemId = slot.Entry.itemId;

        for (int i = 0; i < _spawned.Count; i++)
        {
            ShopSlotUI s = _spawned[i];
            if (!s) continue;
            s.SetSlotSelected(s == slot);
        }

        UpdateBuyButtonsForSelectionState();
        RefreshSelectionPanel();
    }

    /// <summary>Clears the current shop item selection (grey Buy buttons). Tooltip is hidden.</summary>
    public void ClearSlotSelection()
    {
        _selectedShopItemId = null;

        for (int i = 0; i < _spawned.Count; i++)
            _spawned[i]?.SetSlotSelected(false);

        UpdateBuyButtonsForSelectionState();
        RefreshSelectionPanel();
        shopTooltip?.Hide();
    }

    private void RefreshSelectionPanel()
    {
        if (!itemSelectionPanel)
            return;

        MerchantStock.Entry entry = FindSelectedEntry(_currentMerchant);
        if (entry == null || inventory == null)
        {
            itemSelectionPanel.Hide();
            return;
        }

        ItemDefinition def = inventory.GetItemDef(entry.itemId);
        if (!def)
        {
            itemSelectionPanel.Hide();
            return;
        }

        itemSelectionPanel.ShowSelection(_currentMerchant, entry, def);
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

        RestoreSlotSelectionAfterRebuild(merchant);
        ForceLayoutRefresh();
        ScheduleShopGridLayout();
        RefreshShopRaycastTargets();
    }

    public void RefreshShopRaycastTargets()
    {
        GameObject raycastRoot = ResolveWindowRoot();
        if (!raycastRoot)
            return;

        Graphic[] graphics = raycastRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (!g)
                continue;

            // Interactive controls (buttons, slots) + explicit backdrop blockers must receive raycasts.
            bool isInteractive = g.GetComponentInParent<Selectable>(true) != null;
            bool isInventorySlot = g.GetComponent<InventorySlotUI>() != null;
            // UIDragWindow lives on a header Image that is NOT a Selectable; without this, raycastTarget
            // stays false and the bar is click-through — drag never starts (see RefreshShopRaycastTargets).
            bool isDragHandle = g.GetComponentInParent<UIDragWindow>(true) != null;
            bool isResizeHandle = g.GetComponentInParent<UIWindowResizeHandle>(true) != null;
            g.raycastTarget = isInteractive || isInventorySlot || isDragHandle || isResizeHandle || IsPointerBlockingGraphic(g);
        }

        EnsureShopDragHandleOnTop();
    }

    private void ResolveShopDragHandle()
    {
        if (shopDragHandle)
            return;

        GameObject root = ResolveWindowRoot();
        if (!root)
            return;

        Transform handle = root.transform.Find("HeaderDragWindow");
        if (handle)
            shopDragHandle = handle as RectTransform;
    }

    private void CachePointerBlockingGraphics()
    {
        _pointerBlockingGraphics = new HashSet<Graphic>();

        if (panelRootImage)
            _pointerBlockingGraphics.Add(panelRootImage);

        GameObject root = ResolveWindowRoot();
        if (root)
        {
            Transform inventoryRoot = root.transform.Find("InventoryWindowInsideShop");
            if (inventoryRoot && inventoryRoot.TryGetComponent(out Image inventoryPanelImage))
                _pointerBlockingGraphics.Add(inventoryPanelImage);

            if (inventoryRoot)
            {
                Transform inventoryBottomBar = inventoryRoot.Find("BottomBarOfInvent");
                if (inventoryBottomBar && inventoryBottomBar.TryGetComponent(out Image inventoryBottomBarImage))
                    _pointerBlockingGraphics.Add(inventoryBottomBarImage);
            }

            ScrollRect[] scrollRects = root.GetComponentsInChildren<ScrollRect>(true);
            for (int i = 0; i < scrollRects.Length; i++)
            {
                ScrollRect scroll = scrollRects[i];
                if (scroll != null && scroll.viewport != null &&
                    scroll.viewport.TryGetComponent(out Image viewportImage))
                {
                    _pointerBlockingGraphics.Add(viewportImage);
                }
            }

            Transform undoRoot = root.transform.Find("UndoShopWindow");
            if (undoRoot)
            {
                Transform undoHeader = undoRoot.Find("Header");
                if (undoHeader && undoHeader.TryGetComponent(out Image undoHeaderImage))
                    _pointerBlockingGraphics.Add(undoHeaderImage);

                Transform undoBottomBar = undoRoot.Find("BottomBarOfShop");
                if (undoBottomBar && undoBottomBar.TryGetComponent(out Image undoBottomBarImage))
                    _pointerBlockingGraphics.Add(undoBottomBarImage);
            }
        }

        GameObject window = ResolveWindowRoot();
        if (window)
        {
            Transform shopHeader = window.transform.Find("HeaderDragWindow");
            if (shopHeader && shopHeader.TryGetComponent(out Image shopHeaderImage))
                _pointerBlockingGraphics.Add(shopHeaderImage);
        }

        if (panelRoot)
        {
            Transform panelTransform = panelRoot.transform;

            Transform shopBottomBar = panelTransform.Find("BottomBarOfShop");
            if (shopBottomBar && shopBottomBar.TryGetComponent(out Image shopBottomBarImage))
                _pointerBlockingGraphics.Add(shopBottomBarImage);

            if (contentRoot != null && contentRoot.parent != null &&
                contentRoot.parent.TryGetComponent(out Image shopContentBackdropImage))
            {
                _pointerBlockingGraphics.Add(shopContentBackdropImage);
            }
        }

        if (itemSelectionPanel && itemSelectionPanel.TryGetComponent(out Image selectionPanelImage))
            _pointerBlockingGraphics.Add(selectionPanelImage);

        if (additionalPointerBlockingGraphics == null)
            return;

        for (int i = 0; i < additionalPointerBlockingGraphics.Length; i++)
        {
            Graphic graphic = additionalPointerBlockingGraphics[i];
            if (graphic)
                _pointerBlockingGraphics.Add(graphic);
        }
    }

    private bool IsPointerBlockingGraphic(Graphic g)
    {
        return _pointerBlockingGraphics != null && _pointerBlockingGraphics.Contains(g);
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

    private void ScheduleShopGridLayout()
    {
        ApplyShopGridLayout();

        if (ShopGridRectIsReady())
            return;

        if (_shopGridLayoutRetry != null)
            StopCoroutine(_shopGridLayoutRetry);
        _shopGridLayoutRetry = StartCoroutine(CoRetryShopGridLayout());
    }

    private IEnumerator CoRetryShopGridLayout()
    {
        for (int i = 0; i < Mathf.Max(1, shopGridLayoutRetryFrames); i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            ApplyShopGridLayout();
            if (ShopGridRectIsReady())
                break;
        }

        _shopGridLayoutRetry = null;
    }

    private bool ShopGridRectIsReady()
    {
        return contentRoot is RectTransform r && r.rect.width > 1f && r.rect.height > 1f;
    }

    private void ApplyShopGridLayout()
    {
        if (!contentRoot)
            return;

        if (!_shopGrid)
            _shopGrid = contentRoot.GetComponent<GridLayoutGroup>();
        if (!_shopGrid)
            return;

        var rect = contentRoot as RectTransform;
        if (!rect)
            return;

        int cols = Mathf.Max(1, shopGridColumns);
        int rows = Mathf.Max(1, shopGridRowsForFit);

        _shopGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _shopGrid.constraintCount = cols;

        if (useGridSpacingFromShopUI)
            _shopGrid.spacing = shopGridSpacing;

        float w = rect.rect.width;
        float h = rect.rect.height;
        if (w <= 1f || h <= 1f)
            return;

        Vector2 sp = _shopGrid.spacing;
        RectOffset pad = _shopGrid.padding;
        float usableW = w - pad.left - pad.right - sp.x * (cols - 1);
        float usableH = h - pad.top - pad.bottom - sp.y * (rows - 1);

        float cellW = Mathf.Max(1f, usableW) / cols;
        float cellH = Mathf.Max(1f, usableH) / rows;
        float cell = Mathf.Min(cellW, cellH);
        cell = Mathf.Floor(cell);
        cell = Mathf.Max(shopMinCellSize, cell);

        if (shopSquareCells)
            _shopGrid.cellSize = new Vector2(cell, cell);
        else
        {
            _shopGrid.cellSize = new Vector2(
                Mathf.Max(shopMinCellSize, Mathf.Floor(cellW)),
                Mathf.Max(shopMinCellSize, Mathf.Floor(cellH)));
        }
    }

    public void TryBuy(Merchant merchant, MerchantStock.Entry entry)
    {
        TryBuy(merchant, entry, null);
    }

    /// <param name="amountOverride">When set, purchase this many items in one transaction.</param>
    public void TryBuy(Merchant merchant, MerchantStock.Entry entry, int? amountOverride)
    {
        if (merchant == null || entry == null)
            return;

        int amount = amountOverride ?? 1;
        amount = Mathf.Max(1, amount);
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

        if (!IsOpen)
            return;

        Rebuild(merchant);
        RefreshSelectionPanel();
        RefreshRestockButtonState();
    }

    private void OnDestroy()
    {
        if (_currentMerchant != null)
            _currentMerchant.StockChanged -= HandleMerchantStockChanged;
    }

    private void OnBuy1Clicked()
    {
        MerchantStock.Entry entry = FindSelectedEntry(_currentMerchant);
        if (entry != null)
            TryBuy(_currentMerchant, entry, 1);
    }

    private void OnBuy50Clicked()
    {
        MerchantStock.Entry entry = FindSelectedEntry(_currentMerchant);
        if (entry != null)
            TryBuy(_currentMerchant, entry, 50);
    }

    private MerchantStock.Entry FindSelectedEntry(Merchant merchant)
    {
        if (merchant?.Stock == null || string.IsNullOrEmpty(_selectedShopItemId))
            return null;

        return merchant.Stock.GetEntry(_selectedShopItemId);
    }

    private void RestoreSlotSelectionAfterRebuild(Merchant merchant)
    {
        for (int i = 0; i < _spawned.Count; i++)
            _spawned[i]?.SetSlotSelected(false);

        if (string.IsNullOrEmpty(_selectedShopItemId) || merchant == null)
        {
            UpdateBuyButtonsForSelectionState();
            RefreshSelectionPanel();
            return;
        }

        if (FindSelectedEntry(merchant) == null)
        {
            _selectedShopItemId = null;
            UpdateBuyButtonsForSelectionState();
            RefreshSelectionPanel();
            return;
        }

        for (int i = 0; i < _spawned.Count; i++)
        {
            ShopSlotUI s = _spawned[i];
            if (s && s.Entry != null && s.Entry.itemId == _selectedShopItemId)
                s.SetSlotSelected(true);
        }

        UpdateBuyButtonsForSelectionState();
        RefreshSelectionPanel();
    }

    private static bool IsEntryPurchasable(Merchant merchant, string itemId)
    {
        if (merchant?.Stock == null)
            return false;

        MerchantStock.Entry entry = merchant.Stock.GetEntry(itemId);
        if (entry == null)
            return false;

        int qty = merchant.GetQuantity(entry);
        return qty != 0;
    }

    private bool HasSelectedEntry()
    {
        return !string.IsNullOrEmpty(_selectedShopItemId)
               && _currentMerchant != null
               && FindSelectedEntry(_currentMerchant) != null;
    }

    private bool CanPurchaseSelection()
    {
        return HasSelectedEntry()
               && IsEntryPurchasable(_currentMerchant, _selectedShopItemId);
    }

    private void UpdateBuyButtonsForSelectionState()
    {
        bool canBuy = CanPurchaseSelection();

        if (buy1xButton) buy1xButton.interactable = canBuy;
        if (buy50xButton) buy50xButton.interactable = canBuy;

        if (buy1xButton) buy1xButton.transform.localScale = unselectedScale;
        if (buy50xButton) buy50xButton.transform.localScale = unselectedScale;

        ApplyBuyButtonLabelColors();
    }

    private void ApplyBuyButtonLabelColors()
    {
        bool canBuy = CanPurchaseSelection();
        Color textColor = canBuy ? unselectedTextColor : buyButtonsDisabledTextColor;
        if (buy1xButtonText) buy1xButtonText.color = textColor;
        if (buy50xButtonText) buy50xButtonText.color = textColor;
    }

    private void ApplyBuyButtonVisualStyles()
    {
        ApplyBuyButtonVisualStyle(buy1xButton, buy1xButtonImage);
        ApplyBuyButtonVisualStyle(buy50xButton, buy50xButtonImage);
        ApplyBuyButtonLabelColors();
    }

    private void ApplyBuyButtonVisualStyle(Button button, Image image)
    {
        if (!button)
            return;

        if (!image)
            image = button.targetGraphic as Image;

        Color normal = UITabBarButtonVisuals.NormalImageColor;
        Color hover = Color.Lerp(
            UITabBarButtonVisuals.NormalImageColor,
            new Color(0.58f, 0.44f, 0.24f, 1f),
            0.72f);
        Color pressed = UITabBarButtonVisuals.SelectedImageColor;
        Color disabled = new Color(0.12f, 0.10f, 0.09f, 0.55f);

        if (image != null)
            image.color = Color.white;

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = hover;
        colors.pressedColor = pressed;
        colors.selectedColor = hover;
        colors.disabledColor = disabled;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private void ToggleBuybackPanel()
    {
        if (!undoShopWindow)
            undoShopWindow = FindFirstObjectByType<UndoShopWindowUI>(FindObjectsInactive.Include);

        if (!undoShopWindow || !_currentMerchant)
            return;

        if (undoShopWindow.IsOpen)
        {
            undoShopWindow.CloseWindow();
            EnsureShopDragHandleOnTop();
            return;
        }

        if (enableDisabledUndoWindowOnUse)
            undoShopWindow.EnsureWindowEnabledForUse();

        undoShopWindow.OpenForMerchant(_currentMerchant);
        EnsureShopDragHandleOnTop();
    }

    private void OnRestockClicked()
    {
        if (_currentMerchant == null || _currentMerchant.Stock == null)
            return;

        QuestProgressManager qpm = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (qpm == null)
            return;

        if (qpm.TryAcceptShopRestockQuest(_currentMerchant.Stock.StockSaveKey, out string msg))
        {
            if (!string.IsNullOrWhiteSpace(msg))
                GameLog.Add(msg);
            MainMenuWindowUI.Resolve()?.OpenQuestShow();
            RefreshRestockButtonState();
            return;
        }

        if (!string.IsNullOrWhiteSpace(msg))
            GameLog.Add(msg, GameLog.CannotMessageColor);
        RefreshRestockButtonState();
    }

    private void TrySubscribeQuestProgress()
    {
        QuestProgressManager qpm = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (qpm == null || qpm == _questProgressEventsTarget)
            return;

        UnsubscribeQuestProgress();
        _questProgressEventsTarget = qpm;
        _questProgressEventsTarget.ProgressChanged += HandleQuestProgressChanged;
    }

    private void UnsubscribeQuestProgress()
    {
        if (_questProgressEventsTarget == null)
            return;
        _questProgressEventsTarget.ProgressChanged -= HandleQuestProgressChanged;
        _questProgressEventsTarget = null;
    }

    private void HandleQuestProgressChanged()
    {
        if (!isActiveAndEnabled)
            return;
        RefreshRestockButtonState();
    }

    private void RefreshRestockButtonState()
    {
        if (!restockButton)
            return;

        if (_currentMerchant == null || _currentMerchant.Stock == null)
        {
            restockButton.gameObject.SetActive(false);
            return;
        }

        QuestProgressManager qpm = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        bool hasConfiguredQuest = qpm != null && qpm.HasShopRestockQuestConfigured(_currentMerchant.Stock.StockSaveKey);
        restockButton.gameObject.SetActive(hasConfiguredQuest);
        if (!hasConfiguredQuest)
            return;

        bool inProgress = false;
        if (qpm != null)
            inProgress = qpm.HasActiveShopRestockQuest(_currentMerchant.Stock.StockSaveKey);

        restockButton.interactable = !inProgress;
        if (restockButtonLabel != null)
            restockButtonLabel.text = inProgress ? "Restock: In Progress" : "Restock";
    }

    /// <summary>Updates Undo control visibility for the current merchant (always clickable to open/close undo).</summary>
    public void RefreshUndoSaleButtonState()
    {
        if (!buybackToggleButton)
            return;

        bool hasMerchant = _currentMerchant;
        buybackToggleButton.gameObject.SetActive(hasMerchant);
        if (!hasMerchant)
            return;

        buybackToggleButton.interactable = true;
    }

    /// <summary>Left-click target behind item slots to clear selection.</summary>
    private sealed class ShopClearSelectionReceiver : MonoBehaviour, IPointerClickHandler
    {
        private ShopUI _shop;

        public void Initialize(ShopUI shop) => _shop = shop;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_shop || eventData.button != PointerEventData.InputButton.Left)
                return;
            _shop.ClearSlotSelection();
        }
    }
}