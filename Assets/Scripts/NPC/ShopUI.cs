using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private ShopSlotUI slotPrefab;

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI shopTooltip;
    [SerializeField] private RectTransform shopWindowRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    private readonly List<ShopSlotUI> _spawned = new();
    private Merchant _currentMerchant;

    private void Awake()
    {
        TryResolveRefs();

        if (panelRoot)
            panelRoot.SetActive(false);

        if (!shopWindowRect && panelRoot)
            shopWindowRect = panelRoot.transform as RectTransform;

        if (!shopTooltip)
            shopTooltip = FindShopTooltip();
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

        _currentMerchant = merchant;

        if (titleText)
            titleText.text = merchant.MerchantName;

        if (panelRoot)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        Rebuild(merchant);
    }

    public void Close()
    {
        _currentMerchant = null;
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

        bool success = merchant.TryBuy(entry.itemId, 1);

        if (!success)
        {
            Debug.Log("[ShopUI] Purchase failed.");
            return;
        }

        Rebuild(merchant);
    }
}