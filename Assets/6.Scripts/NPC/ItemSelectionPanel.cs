using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Shop detail bar shown when the player selects a purchasable stock entry.
/// </summary>
public class ItemSelectionPanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root to show/hide. Defaults to this GameObject when empty.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Fields")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text goldCostText;
    [SerializeField] private TMP_Text resourceCostText;
    [Tooltip("Optional row/container to hide when there is no item resource cost.")]
    [SerializeField] private GameObject resourceCostRoot;

    [Header("Affordability")]
    [SerializeField] private Color canAffordColor = new Color(0.31f, 0.78f, 0.47f, 1f);
    [SerializeField] private Color cannotAffordColor = new Color(1f, 0.42f, 0.42f, 1f);

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

        Hide();
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

        if (stockText)
            stockText.text = FormatStock(merchant, entry);

        ResolveCosts(entry, out int goldAmount, out bool hasGoldCost, out string resourceCostLine);

        if (goldCostText)
        {
            goldCostText.text = hasGoldCost ? $"{goldAmount}g" : "Free";
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
            resourceCostText.text = resourceCostLine;
            resourceCostText.color = CanAffordResources(entry) ? canAffordColor : cannotAffordColor;
        }

        if (panelRoot)
            panelRoot.SetActive(true);
    }

    public void Hide()
    {
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
            return "Stock: Sold Out";
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
