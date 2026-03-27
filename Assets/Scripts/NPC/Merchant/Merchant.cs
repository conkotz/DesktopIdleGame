using System.Text;
using System.Collections.Generic;
using System;
using UnityEngine;

public class Merchant : MonoBehaviour, ISaveable
{
    public event Action<Merchant> StockChanged;
    [Header("Identity")]
    [SerializeField] private string merchantName = "Merchant";
    [SerializeField] private MerchantStock stock;

    [Header("Behaviour")]
    [SerializeField] private bool ctrlClickSellsAll = true;
    [Tooltip("If enabled, this merchant only buys items that exist in its stock list.")]
    [SerializeField] private bool onlyBuysStockedItems = false;
    [SerializeField] private string cannotBuyItemPopupText = "Cannot sell that item to {merchant}.";

    [Header("Save Identity")]
    [Tooltip("Unique id for this merchant used in save data. Leave empty to auto-generate from scene path.")]
    [SerializeField] private string merchantId;

    [Header("Refs (optional)")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    public string MerchantName => merchantName;
    public MerchantStock Stock => stock;
    public string MerchantId => GetMerchantId();

    // Runtime quantities by stock entry index. We never mutate the ScriptableObject asset directly.
    private readonly List<int> _runtimeQuantities = new();

    private void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!wallet) wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
        InitializeRuntimeStockFromDefaults();
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

        int available = GetQuantity(entry);
        if (available >= 0 && available < amount)
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

            int current = GetQuantity(entry);
            if (current > 0)
                SetQuantity(entry, current - 1);
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

        int qty = GetQuantity(entry);
        if (qty == 0)
            sb.Append("Stock: Sold Out");
        else if (qty < 0)
            sb.Append("Stock: ∞");
        else
            sb.Append($"Stock: {qty}");

        return sb.ToString().TrimEnd();
    }

    public bool TryReplenishStockFromPlayerSale(string itemId, int amount, out int addedToStock)
    {
        addedToStock = 0;

        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || stock == null)
            return false;

        var entry = stock.GetEntry(itemId);
        if (entry == null)
            return false;

        int current = GetQuantity(entry);
        if (current < 0)
        {
            // Infinite stock doesn't need quantity mutation.
            return true;
        }

        int add = Mathf.Max(1, amount);
        SetQuantity(entry, current + add);
        addedToStock = add;
        return true;
    }

    public bool CanBuyItemFromPlayer(string itemId)
    {
        if (!onlyBuysStockedItems)
            return true;

        if (string.IsNullOrWhiteSpace(itemId) || stock == null)
            return false;

        return stock.GetEntry(itemId) != null;
    }

    public bool TryRejectUnsellableItemWithPopup(string itemId)
    {
        if (CanBuyItemFromPlayer(itemId))
            return false;

        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            string merchantLabel = string.IsNullOrWhiteSpace(merchantName) ? "this merchant" : merchantName;
            string template = string.IsNullOrWhiteSpace(cannotBuyItemPopupText)
                ? "Cannot sell that item to {merchant}."
                : cannotBuyItemPopupText;
            string msg = template.Replace("{merchant}", merchantLabel);
            player.ShowPopup(msg);
        }

        return true;
    }

    public bool TryRemoveReplenishedStock(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || stock == null)
            return false;

        var entry = stock.GetEntry(itemId);
        if (entry == null)
            return false;

        int current = GetQuantity(entry);
        if (current < 0)
        {
            // Infinite stock: nothing to remove.
            return true;
        }

        if (current < amount)
            return false;

        SetQuantity(entry, current - amount);
        return true;
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
            if (!CanBuyItemFromPlayer(slot.itemId)) continue;

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
            if (!CanBuyItemFromPlayer(slot.itemId)) continue;

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

    public int GetQuantity(MerchantStock.Entry entry)
    {
        int index = GetEntryIndex(entry);
        if (index < 0)
            return entry != null ? entry.quantity : 0;

        if (index >= _runtimeQuantities.Count)
            return entry.quantity;

        return _runtimeQuantities[index];
    }

    public void SetQuantity(MerchantStock.Entry entry, int quantity)
    {
        int index = GetEntryIndex(entry);
        if (index < 0) return;

        EnsureRuntimeStockCapacity();
        int clamped = Mathf.Max(-1, quantity);
        if (_runtimeQuantities[index] == clamped)
            return;

        _runtimeQuantities[index] = clamped;
        StockChanged?.Invoke(this);
    }

    private int GetEntryIndex(MerchantStock.Entry entry)
    {
        if (stock == null || entry == null || stock.Items == null) return -1;

        for (int i = 0; i < stock.Items.Count; i++)
        {
            if (ReferenceEquals(stock.Items[i], entry))
                return i;
        }
        return -1;
    }

    private void InitializeRuntimeStockFromDefaults()
    {
        _runtimeQuantities.Clear();
        if (stock == null || stock.Items == null) return;

        for (int i = 0; i < stock.Items.Count; i++)
        {
            var e = stock.Items[i];
            _runtimeQuantities.Add(e != null ? Mathf.Max(-1, e.quantity) : 0);
        }
    }

    private void EnsureRuntimeStockCapacity()
    {
        if (stock == null || stock.Items == null) return;
        while (_runtimeQuantities.Count < stock.Items.Count)
            _runtimeQuantities.Add(0);
    }

    private string GetMerchantId()
    {
        if (!string.IsNullOrWhiteSpace(merchantId))
            return merchantId;

        // Stable fallback id per scene object path.
        return $"{gameObject.scene.name}:{BuildPath(transform)}";
    }

    private static string BuildPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        var p = t.parent;
        while (p != null)
        {
            sb.Insert(0, '/');
            sb.Insert(0, p.name);
            p = p.parent;
        }
        return sb.ToString();
    }

    public void SaveInto(SaveData data)
    {
        if (data == null || stock == null || stock.Items == null) return;

        data.merchantStocks ??= new List<SaveData.MerchantStockSave>();

        var save = new SaveData.MerchantStockSave
        {
            merchantId = GetMerchantId(),
            quantities = new List<int>()
        };

        EnsureRuntimeStockCapacity();
        for (int i = 0; i < stock.Items.Count; i++)
            save.quantities.Add(i < _runtimeQuantities.Count ? _runtimeQuantities[i] : 0);

        data.merchantStocks.Add(save);
    }

    public void LoadFrom(SaveData data)
    {
        // New Game path (empty save data) => reset to defaults.
        InitializeRuntimeStockFromDefaults();

        if (data == null || data.merchantStocks == null || data.merchantStocks.Count == 0 || stock == null || stock.Items == null)
            return;

        string id = GetMerchantId();
        SaveData.MerchantStockSave matched = null;
        for (int i = 0; i < data.merchantStocks.Count; i++)
        {
            var s = data.merchantStocks[i];
            if (s != null && s.merchantId == id)
            {
                matched = s;
                break;
            }
        }

        if (matched == null || matched.quantities == null)
            return;

        EnsureRuntimeStockCapacity();
        int n = Mathf.Min(_runtimeQuantities.Count, matched.quantities.Count);
        for (int i = 0; i < n; i++)
            _runtimeQuantities[i] = Mathf.Max(-1, matched.quantities[i]);
    }
}