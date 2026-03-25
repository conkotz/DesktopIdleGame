using System.Text;
using UnityEngine;

public class Merchant : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string merchantName = "Merchant";
    [SerializeField] private MerchantStock stock;

    [Header("Behaviour")]
    [SerializeField] private bool ctrlClickSellsAll = true;

    [Header("Refs (optional)")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    public string MerchantName => merchantName;
    public MerchantStock Stock => stock;

    private void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!wallet) wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
    }

    private void OnMouseDown()
    {
        if (!inventory || !wallet) return;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrl) return;

        if (ctrlClickSellsAll)
            SellAll();
        else
            SellFirstNonEmptyStack();
    }

    // =========================
    // BUYING
    // =========================

    public bool TryBuy(string itemId, int amount = 1)
    {
        if (stock == null || inventory == null || wallet == null || amount <= 0)
            return false;

        var entry = stock.GetEntry(itemId);
        if (entry == null)
        {
            Debug.LogWarning($"[Merchant] No stock entry found for '{itemId}'.");
            return false;
        }

        if (entry.quantity >= 0 && entry.quantity < amount)
        {
            Debug.Log($"[Merchant] Not enough stock for {itemId}.");
            return false;
        }

        for (int i = 0; i < amount; i++)
        {
            if (!CanAfford(entry))
            {
                Debug.Log($"[Merchant] Cannot afford {itemId}.");
                return false;
            }

            if (!inventory.CanAdd(entry.itemId, 1))
            {
                Debug.Log($"[Merchant] Inventory full, could not add {itemId}.");
                return false;
            }

            SpendCosts(entry);

            bool added = inventory.Add(entry.itemId, 1);
            if (!added)
            {
                Debug.LogError($"[Merchant] Failed to add {itemId} after spending costs.");
                return false;
            }

            if (entry.quantity > 0)
                entry.quantity--;
        }

        Debug.Log($"[Merchant] Bought {amount}x {itemId}.");
        return true;
    }

    public bool CanAfford(MerchantStock.Entry entry)
    {
        if (entry == null) return false;
        if (entry.costs == null) return true;

        for (int i = 0; i < entry.costs.Count; i++)
        {
            var cost = entry.costs[i];
            if (cost == null || cost.amount <= 0) continue;

            switch (cost.type)
            {
                case MerchantStock.CostType.Gold:
                    if (!wallet || wallet.Gold < cost.amount)
                        return false;
                    break;

                case MerchantStock.CostType.Item:
                    if (!inventory || string.IsNullOrWhiteSpace(cost.itemId))
                        return false;

                    if (inventory.GetTotalAmount(cost.itemId) < cost.amount)
                        return false;
                    break;
            }
        }

        return true;
    }

    private void SpendCosts(MerchantStock.Entry entry)
    {
        if (entry == null || entry.costs == null) return;

        for (int i = 0; i < entry.costs.Count; i++)
        {
            var cost = entry.costs[i];
            if (cost == null || cost.amount <= 0) continue;

            switch (cost.type)
            {
                case MerchantStock.CostType.Gold:
                    if (!wallet.SpendGold(cost.amount))
                        Debug.LogWarning($"[Merchant] Failed to spend {cost.amount} gold.");
                    break;

                case MerchantStock.CostType.Item:
                    if (!inventory.Remove(cost.itemId, cost.amount))
                        Debug.LogWarning($"[Merchant] Failed to remove {cost.amount}x {cost.itemId}.");
                    break;
            }
        }
    }

    public string GetPriceText(MerchantStock.Entry entry)
    {
        if (entry == null || entry.costs == null || entry.costs.Count == 0)
            return "Free";

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < entry.costs.Count; i++)
        {
            var cost = entry.costs[i];
            if (cost == null || cost.amount <= 0) continue;

            if (sb.Length > 0)
                sb.Append(" + ");

            switch (cost.type)
            {
                case MerchantStock.CostType.Gold:
                    sb.Append(cost.amount).Append(" Gold");
                    break;

                case MerchantStock.CostType.Item:
                    sb.Append(cost.amount).Append(" ").Append(cost.itemId);
                    break;
            }
        }

        return sb.Length > 0 ? sb.ToString() : "Free";
    }

    public string GetPriceTooltipText(MerchantStock.Entry entry)
    {
        if (entry == null)
            return "";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("Cost:");

        if (entry.costs == null || entry.costs.Count == 0)
        {
            sb.AppendLine("• Free");
        }
        else
        {
            var inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

            foreach (var cost in entry.costs)
            {
                if (cost == null) continue;

                switch (cost.type)
                {
                    case MerchantStock.CostType.Gold:
                        sb.AppendLine($"• {cost.amount} Gold");
                        break;

                    case MerchantStock.CostType.Item:
                        string itemName = cost.itemId;
                        if (inv != null)
                        {
                            var def = inv.GetItemDef(cost.itemId);
                            if (def != null)
                                itemName = def.displayName;
                        }
                        sb.AppendLine($"• {cost.amount} {itemName}");
                        break;

                    default:
                        sb.AppendLine($"• {cost.amount}");
                        break;
                }
            }
        }

        sb.AppendLine();

        if (entry.quantity == 0)
            sb.Append("Stock: Sold Out");
        else if (entry.quantity < 0)
            sb.Append("Stock: ∞");
        else
            sb.Append($"Stock: {entry.quantity}");

        return sb.ToString().TrimEnd();
    }

    // =========================
    // SELLING
    // =========================

    private void SellAll()
    {
        int goldGained = 0;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (slot.IsEmpty) continue;

            int valuePerItem = inventory.GetItemValue(slot.itemId);
            if (valuePerItem <= 0) continue;

            goldGained += valuePerItem * slot.amount;
            inventory.RemoveStackAtSlot(i);
        }

        if (goldGained > 0)
        {
            wallet.AddGold(goldGained);
            Debug.Log($"[Merchant] Sold items for {goldGained} gold.");
        }
        else
        {
            Debug.Log("[Merchant] Nothing sellable to sell.");
        }
    }

    private void SellFirstNonEmptyStack()
    {
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (slot.IsEmpty) continue;

            int valuePerItem = inventory.GetItemValue(slot.itemId);
            if (valuePerItem <= 0) continue;

            int goldGained = valuePerItem * slot.amount;

            inventory.RemoveStackAtSlot(i);
            wallet.AddGold(goldGained);

            Debug.Log($"[Merchant] Sold {slot.amount}x {slot.itemId} for {goldGained} gold.");
            return;
        }

        Debug.Log("[Merchant] Nothing sellable to sell.");
    }
}