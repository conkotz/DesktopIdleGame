using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaleUndoManager : MonoBehaviour
{
    public static SaleUndoManager Instance { get; private set; }

    [Serializable]
    private class SaleEntry
    {
        public int id;
        public string itemId;
        public int amount;
        public int gold;
        public string merchantId;
        public int stockAddedAmount;
        public float expiresAt;
    }

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    [Header("UI (bound at runtime)")]
    [SerializeField] private GameObject undoPanel;          // UndoSellPanel
    [SerializeField] private RectTransform rowsRoot;        // RowRoot
    [SerializeField] private SaleUndoRowUI undoRowPrefab;   // inactive template

    [Header("Shop dock")]
    [Tooltip(
        "Gap between shop chrome and undo panel. Bottom dock: panel top sits just under the shop bottom edge and rows stack downward; top dock (flip): panel bottom sits just above the shop top.")]
    [SerializeField] private float shopDockEdgePadding = 8f;

    [Header("Behaviour")]
    [SerializeField] private int visibleMax = 4;
    [SerializeField] private int maxEntriesKept = 30;
    [SerializeField] private float undoTimeoutSeconds = 10f;

    private readonly List<SaleEntry> _entries = new();
    private int _nextId = 1;
    private bool _isUndoPanelVisible;
    private float _autoHideAt = -1f;

    private RectTransform _panelRect;
    private RectTransform _shopDockOptional;
    private RectTransform _flipClampOptional;

    private bool _capturedStripLayout;
    private Transform _stripParent;
    private int _stripSiblingIndex;
    private Vector2 _stripAnchorMin;
    private Vector2 _stripAnchorMax;
    private Vector2 _stripPivot;
    private Vector2 _stripAnchoredPosition;
    private Vector2 _stripSizeDelta;

    private readonly Vector3[] _worldCorners = new Vector3[4];

    private void Awake()
    {
        // ✅ singleton + persist
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Refs can be missing in Bootstrap; we rebind on scene load
        RebindRefs();

        // UI is now bound by SaleUndoUIBinder, not discovered here.
        ClearUIRefs();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // New scene may have a new Inventory (or none). Rebind.
        RebindRefs();

        // If UI is currently bound, refresh it (keeps panel correct after scene changes)
        if (undoPanel && rowsRoot && undoRowPrefab && _panelRect)
            RefreshUI();
    }

    private void RebindRefs()
    {
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!wallet) wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
    }

    private void ClearUIRefs()
    {
        undoPanel = null;
        rowsRoot = null;
        undoRowPrefab = null;
        _panelRect = null;
        _shopDockOptional = null;
        _flipClampOptional = null;
        _capturedStripLayout = false;
    }

    /// <summary>
    /// Called by the scene's UI binder when a selling UI exists in that scene.
    /// Safe to call multiple times (eg. different scenes).
    /// </summary>
    public void BindUI(GameObject panel, RectTransform root, SaleUndoRowUI rowPrefab)
    {
        BindUI(panel, root, rowPrefab, null, null);
    }

    public void BindUI(
        GameObject panel,
        RectTransform root,
        SaleUndoRowUI rowPrefab,
        RectTransform shopWindowDock,
        RectTransform verticalClampBounds)
    {
        undoPanel = panel;
        rowsRoot = root;
        undoRowPrefab = rowPrefab;
        _shopDockOptional = shopWindowDock;
        _flipClampOptional = verticalClampBounds;

        _panelRect = undoPanel ? undoPanel.GetComponent<RectTransform>() : null;
        _capturedStripLayout = false;
        if (!rowsRoot && _panelRect)
        {
            var rr = _panelRect.Find("RowRoot");
            rowsRoot = rr ? rr.GetComponent<RectTransform>() : _panelRect;
        }

        // If prefab not provided, try find inactive template
        if (!undoRowPrefab && rowsRoot)
        {
            var t = rowsRoot.Find("UndoRowPrefab");
            if (t) undoRowPrefab = t.GetComponent<SaleUndoRowUI>();
        }

        if (undoRowPrefab && undoRowPrefab.gameObject.activeSelf)
            undoRowPrefab.gameObject.SetActive(false);

        CaptureStripLayoutIfNeeded();

        if (undoPanel) undoPanel.SetActive(_isUndoPanelVisible && _entries.Count > 0);

        RefreshUI();
    }

    private void CaptureStripLayoutIfNeeded()
    {
        if (_capturedStripLayout || !_panelRect)
            return;

        _stripParent = _panelRect.parent;
        _stripSiblingIndex = _panelRect.GetSiblingIndex();
        _stripAnchorMin = _panelRect.anchorMin;
        _stripAnchorMax = _panelRect.anchorMax;
        _stripPivot = _panelRect.pivot;
        _stripAnchoredPosition = _panelRect.anchoredPosition;
        _stripSizeDelta = _panelRect.sizeDelta;
        _capturedStripLayout = true;
    }

    private void RestoreStripDockLayout()
    {
        if (!_capturedStripLayout || !_stripParent || !_panelRect)
            return;

        _panelRect.SetParent(_stripParent, false);
        int idx = Mathf.Clamp(_stripSiblingIndex, 0, _stripParent.childCount - 1);
        _panelRect.SetSiblingIndex(idx);
        _panelRect.anchorMin = _stripAnchorMin;
        _panelRect.anchorMax = _stripAnchorMax;
        _panelRect.pivot = _stripPivot;
        _panelRect.anchoredPosition = _stripAnchoredPosition;
        _panelRect.sizeDelta = _stripSizeDelta;
    }

    private ShopUI FindShopUI() => FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);

    private RectTransform ResolveShopDock(ShopUI shop)
    {
        if (_shopDockOptional)
            return _shopDockOptional;
        return shop ? shop.PanelRectTransform : null;
    }

    private RectTransform ResolveClampBounds(ShopUI shop)
    {
        if (_flipClampOptional)
            return _flipClampOptional;

        return shop ? shop.PanelRectTransform : null;
    }

    private void ApplyUndoPanelPlacement(bool layoutAlreadyRebuilt)
    {
        if (!_panelRect || !undoPanel || !undoPanel.activeInHierarchy)
            return;

        ShopUI shop = FindShopUI();
        RectTransform dock = ResolveShopDock(shop);
        bool useShop = shop && shop.IsOpen && dock;

        if (useShop)
        {
            if (_panelRect.parent != dock)
            {
                _panelRect.SetParent(dock, false);
                _panelRect.SetAsLastSibling();
            }

            if (!layoutAlreadyRebuilt)
                RebuildUndoLayout();

            RectTransform clampRt = ResolveClampBounds(shop);
            ResolveShopVerticalDock(clampRt);
        }
        else
            RestoreStripDockLayout();
    }

    /// <summary>
    /// Pin under the shop bottom (below buy/undo bar): top edge of panel at shop bottom, content stacks downward.
    /// </summary>
    private void DockShopUndoToBottom()
    {
        float pad = shopDockEdgePadding;
        _panelRect.anchorMin = new Vector2(0.5f, 0f);
        _panelRect.anchorMax = new Vector2(0.5f, 0f);
        _panelRect.pivot = new Vector2(0.5f, 1f);
        _panelRect.anchoredPosition = new Vector2(0f, -pad);
    }

    /// <summary>
    /// Flip: bottom edge of panel at shop top, list extends upward above the window when bottom space is tight.
    /// </summary>
    private void DockShopUndoToTop()
    {
        float pad = shopDockEdgePadding;
        _panelRect.anchorMin = new Vector2(0.5f, 1f);
        _panelRect.anchorMax = new Vector2(0.5f, 1f);
        _panelRect.pivot = new Vector2(0.5f, 0f);
        _panelRect.anchoredPosition = new Vector2(0f, pad);
    }

    private void RebuildUndoLayout()
    {
        if (rowsRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowsRoot);
        if (_panelRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
        Canvas.ForceUpdateCanvases();
    }

    private void ResolveShopVerticalDock(RectTransform clampBounds)
    {
        if (!clampBounds)
        {
            DockShopUndoToBottom();
            return;
        }

        const float epsilon = 2f;

        DockShopUndoToBottom();
        RebuildUndoLayout();

        clampBounds.GetWorldCorners(_worldCorners);
        float bMin = _worldCorners[0].y;
        float bMax = _worldCorners[2].y;

        _panelRect.GetWorldCorners(_worldCorners);
        float pMin = _worldCorners[0].y;
        if (pMin >= bMin - epsilon)
            return;

        DockShopUndoToTop();
        RebuildUndoLayout();

        _panelRect.GetWorldCorners(_worldCorners);
        float pMax = _worldCorners[2].y;
        if (pMax <= bMax + epsilon)
            return;

        DockShopUndoToBottom();
        RebuildUndoLayout();

        _panelRect.GetWorldCorners(_worldCorners);
        float bv = Mathf.Max(0f, bMin - _worldCorners[0].y);
        DockShopUndoToTop();
        RebuildUndoLayout();
        _panelRect.GetWorldCorners(_worldCorners);
        float tv = Mathf.Max(0f, _worldCorners[2].y - bMax);

        if (bv <= tv)
        {
            DockShopUndoToBottom();
            RebuildUndoLayout();
        }
    }

    private void MaintainShopVerticalFlipIfDocked()
    {
        if (!_panelRect || !undoPanel || !undoPanel.activeSelf)
            return;

        ShopUI shop = FindShopUI();
        if (shop == null || !shop.IsOpen)
            return;

        RectTransform dock = ResolveShopDock(shop);
        RectTransform clampRt = ResolveClampBounds(shop);
        if (!dock || _panelRect.parent != dock || !clampRt)
            return;

        clampRt.GetWorldCorners(_worldCorners);
        float bMin = _worldCorners[0].y;
        float bMax = _worldCorners[2].y;

        _panelRect.GetWorldCorners(_worldCorners);
        float pMin = _worldCorners[0].y;
        float pMax = _worldCorners[2].y;

        bool dockedBelowShop = Mathf.Approximately(_panelRect.anchorMin.y, 0f) &&
                               Mathf.Approximately(_panelRect.anchorMax.y, 0f);
        bool dockedAboveShop = Mathf.Approximately(_panelRect.anchorMin.y, 1f) &&
                               Mathf.Approximately(_panelRect.anchorMax.y, 1f);

        if (dockedBelowShop && pMin < bMin - 1f)
        {
            DockShopUndoToTop();
            return;
        }

        if (dockedAboveShop && pMax > bMax + 1f)
            DockShopUndoToBottom();
    }

    private void LateUpdate()
    {
        MaintainShopVerticalFlipIfDocked();
    }

    private void Update()
    {
        if (_entries.Count == 0) return;

        float now = Time.unscaledTime;
        if (_isUndoPanelVisible && undoTimeoutSeconds > 0f && _autoHideAt > 0f && now >= _autoHideAt)
        {
            _isUndoPanelVisible = false;
            RefreshUI();
        }
    }

    public void RecordSale(string itemId, int amount, int gold, string merchantId = null, int stockAddedAmount = 0)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || gold <= 0) return;

        // Rebind in case Inventory/Wallet changed with scene
        if (!inventory || !wallet) RebindRefs();

        // UI may not exist in some scenes - that's OK.
        if (!inventory || !wallet || !_panelRect || rowsRoot == null || !undoRowPrefab) return;

        var entry = new SaleEntry
        {
            id = _nextId++,
            itemId = itemId,
            amount = amount,
            gold = gold,
            merchantId = merchantId,
            stockAddedAmount = Mathf.Max(0, stockAddedAmount),
            expiresAt = Time.unscaledTime + undoTimeoutSeconds
        };

        _entries.Insert(0, entry);

        if (_entries.Count > maxEntriesKept)
            _entries.RemoveRange(maxEntriesKept, _entries.Count - maxEntriesKept);

        _isUndoPanelVisible = true;
        _autoHideAt = undoTimeoutSeconds > 0f ? Time.unscaledTime + undoTimeoutSeconds : -1f;
        RefreshUI();
    }

    public void ToggleUndoPanelVisibility()
    {
        if (_entries.Count <= 0)
        {
            _isUndoPanelVisible = false;
            RefreshUI();
            return;
        }

        _isUndoPanelVisible = !_isUndoPanelVisible;
        if (_isUndoPanelVisible && undoTimeoutSeconds > 0f)
            _autoHideAt = Time.unscaledTime + undoTimeoutSeconds;

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (!undoPanel || rowsRoot == null || !undoRowPrefab || !_panelRect) return;

        // Clear clones (keep inactive template)
        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
        {
            var child = rowsRoot.GetChild(i);
            if (child.gameObject == undoRowPrefab.gameObject) continue;
            Destroy(child.gameObject);
        }

        if (_entries.Count == 0)
        {
            undoPanel.SetActive(false);
            RestoreStripDockLayout();
            return;
        }

        if (!_isUndoPanelVisible)
        {
            undoPanel.SetActive(false);
            RestoreStripDockLayout();
            return;
        }

        undoPanel.SetActive(true);

        int showCount = Mathf.Min(visibleMax, _entries.Count);

        for (int i = 0; i < showCount; i++)
        {
            var e = _entries[i];

            var row = Instantiate(undoRowPrefab, rowsRoot);
            row.gameObject.SetActive(true);

            var def = inventory.GetItemDef(e.itemId);
            string niceName = def ? def.displayName : e.itemId;
            Sprite icon = def ? def.icon : null;

            row.Bind(e.id, $"Sold {e.amount}x {niceName} for {e.gold}g", icon, Undo);
        }

        int hidden = _entries.Count - showCount;
        if (hidden > 0)
        {
            var moreRow = Instantiate(undoRowPrefab, rowsRoot);
            moreRow.gameObject.SetActive(true);

            string txt = hidden == 1 ? "+1 more" : $"+{hidden} more";
            moreRow.BindSummary(txt);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowsRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
        ApplyUndoPanelPlacement(layoutAlreadyRebuilt: true);
    }

    private void Undo(int entryId)
    {
        int idx = _entries.FindIndex(e => e.id == entryId);
        if (idx < 0) return;

        var e = _entries[idx];

        Merchant merchant = null;
        if (e.stockAddedAmount > 0)
        {
            merchant = FindMerchantById(e.merchantId);
            if (merchant == null || !merchant.TryRemoveReplenishedStock(e.itemId, e.stockAddedAmount))
                return;
        }

        if (!wallet.SpendGold(e.gold))
        {
            if (merchant != null)
                merchant.TryReplenishStockFromPlayerSale(e.itemId, e.stockAddedAmount, out _);
            _entries.RemoveAt(idx);
            RefreshUI();
            return;
        }

        int added = inventory.AddPartial(e.itemId, e.amount);
        if (added < e.amount)
        {
            if (added > 0) inventory.Remove(e.itemId, added);
            wallet.AddGold(e.gold);
            if (merchant != null)
                merchant.TryReplenishStockFromPlayerSale(e.itemId, e.stockAddedAmount, out _);
            return;
        }

        _entries.RemoveAt(idx);
        if (_entries.Count == 0)
            _isUndoPanelVisible = false;
        RefreshUI();
    }

    private Merchant FindMerchantById(string merchantId)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            return null;

        var merchants = FindObjectsByType<Merchant>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < merchants.Length; i++)
        {
            var m = merchants[i];
            if (m != null && m.MerchantId == merchantId)
                return m;
        }

        return null;
    }
}