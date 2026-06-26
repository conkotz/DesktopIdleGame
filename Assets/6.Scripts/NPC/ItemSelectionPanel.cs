using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop detail bar shown when the player selects a purchasable stock entry.
/// </summary>
public class ItemSelectionPanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root to show/hide. Defaults to this GameObject when empty.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Fields")]
    [SerializeField] private Image itemIconImage;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text goldCostText;
    [SerializeField] private TMP_Text resourceCostText;
    [Tooltip("Optional row/container to hide when there is no item resource cost.")]
    [SerializeField] private GameObject resourceCostRoot;

    [Header("Affordability")]
    [SerializeField] private Color canAffordColor = new Color(0.31f, 0.78f, 0.47f, 1f);
    [SerializeField] private Color cannotAffordColor = new Color(1f, 0.42f, 0.42f, 1f);
    [SerializeField] private Color defaultStockColor = Color.white;
    [SerializeField] private Color soldOutStockColor = new Color(1f, 0.42f, 0.42f, 1f);

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    private void Awake()
    {
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!wallet)
            wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

        if (!panelRoot)
            panelRoot = gameObject;

        if (itemIconImage)
            ConfigureItemIconLayout();

        Hide();
    }

    private void ConfigureItemIconLayout()
    {
        RectTransform iconRect = itemIconImage.rectTransform;

        Vector2 size = iconRect.sizeDelta;
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(120f, 120f);

        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = size;
        iconRect.anchoredPosition = new Vector2(size.x * 0.5f, 0f);

        itemIconImage.preserveAspect = true;
        itemIconImage.raycastTarget = false;
    }

    public void ShowSelection(Merchant merchant, MerchantStock.Entry entry, ItemDefinition def)
    {
        if (entry == null || def == null)
        {
            Hide();
            return;
        }

        if (itemNameText)
            itemNameText.text = def.displayName;

        if (itemIconImage)
        {
            itemIconImage.sprite = def.icon;
            itemIconImage.enabled = def.icon != null;
            itemIconImage.preserveAspect = true;
            ConfigureItemIconLayout();
        }

        if (stockText)
        {
            int qty = merchant != null ? merchant.GetQuantity(entry) : entry.defaultQuantity;
            stockText.text = FormatStock(merchant, entry);
            stockText.color = qty == 0 ? soldOutStockColor : defaultStockColor;
        }

        ResolveCosts(entry, out int goldAmount, out bool hasGoldCost, out string resourceCostLine);

        int stockQty = merchant != null ? merchant.GetQuantity(entry) : entry.defaultQuantity;
        bool showBulkCost = ShopCostFormatter.ShouldShowBulkCost(stockQty);

        if (goldCostText)
        {
            goldCostText.text = hasGoldCost
                ? ShopCostFormatter.FormatCompactGold(goldAmount, showBulkCost)
                : "Free";
            goldCostText.color = CanAffordGold(goldAmount, hasGoldCost) ? canAffordColor : cannotAffordColor;
        }

        bool hasResourceCost = !string.IsNullOrEmpty(resourceCostLine);
        GameObject resourceRow = resourceCostRoot
            ? resourceCostRoot
            : resourceCostText ? resourceCostText.gameObject : null;
        if (resourceRow)
            resourceRow.SetActive(hasResourceCost);
        if (hasResourceCost && resourceCostText)
        {
            resourceCostText.text = FormatResourceCostLine(entry, showBulkCost);
            resourceCostText.color = CanAffordResources(entry) ? canAffordColor : cannotAffordColor;
        }

        if (panelRoot)
            panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (itemIconImage)
        {
            itemIconImage.sprite = null;
            itemIconImage.enabled = false;
        }

        if (panelRoot)
            panelRoot.SetActive(false);
    }

    private static string FormatStock(Merchant merchant, MerchantStock.Entry entry)
    {
        if (entry == null)
            return "";

        int qty = merchant != null ? merchant.GetQuantity(entry) : entry.defaultQuantity;
        if (qty < 0)
            return "Stock: ∞";
        if (qty == 0)
            return "Stock: SOLD OUT";
        return $"Stock: x{qty}";
    }

    private bool CanAffordGold(int goldAmount, bool hasGoldCost)
    {
        if (!hasGoldCost)
            return true;

        return wallet != null && wallet.Gold >= goldAmount;
    }

    private bool CanAffordResources(MerchantStock.Entry entry)
    {
        if (entry?.costs == null)
            return true;

        for (int i = 0; i < entry.costs.Count; i++)
        {
            MerchantStock.Cost cost = entry.costs[i];
            if (cost == null || cost.amount <= 0 || cost.type != MerchantStock.CostType.Item)
                continue;

            if (inventory == null || string.IsNullOrWhiteSpace(cost.itemId))
                return false;

            if (inventory.GetTotalAmount(cost.itemId) < cost.amount)
                return false;
        }

        return true;
    }

    private void ResolveCosts(
        MerchantStock.Entry entry,
        out int goldAmount,
        out bool hasGoldCost,
        out string resourceCostLine)
    {
        goldAmount = 0;
        hasGoldCost = false;
        resourceCostLine = null;

        if (entry?.costs == null || entry.costs.Count == 0)
            return;

        var resourceParts = new StringBuilder();
        for (int i = 0; i < entry.costs.Count; i++)
        {
            MerchantStock.Cost cost = entry.costs[i];
            if (cost == null || cost.amount <= 0)
                continue;

            if (cost.type == MerchantStock.CostType.Gold)
            {
                goldAmount = cost.amount;
                hasGoldCost = true;
                continue;
            }

            if (cost.type != MerchantStock.CostType.Item)
                continue;

            if (resourceParts.Length > 0)
                resourceParts.Append(", ");

            resourceParts.Append(cost.amount);
            resourceParts.Append(' ');
            resourceParts.Append(ResolveItemCostName(cost.itemId));
        }

        if (resourceParts.Length > 0)
            resourceCostLine = resourceParts.ToString();
    }

    private string FormatResourceCostLine(MerchantStock.Entry entry, bool showBulkCost)
    {
        if (entry?.costs == null || entry.costs.Count == 0)
            return "";

        var parts = new StringBuilder();
        for (int i = 0; i < entry.costs.Count; i++)
        {
            MerchantStock.Cost cost = entry.costs[i];
            if (cost == null || cost.amount <= 0 || cost.type != MerchantStock.CostType.Item)
                continue;

            if (parts.Length > 0)
                parts.Append(", ");

            parts.Append(ShopCostFormatter.FormatItemCost(
                cost.amount,
                ResolveItemCostName(cost.itemId),
                showBulkCost));
        }

        return parts.ToString();
    }

    private string ResolveItemCostName(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "Item";

        if (inventory != null)
        {
            ItemDefinition def = inventory.GetItemDef(itemId);
            if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName;
        }

        return itemId;
    }
}
