using System.Text;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;

public class Merchant : MonoBehaviour, ISaveable
{
    public event Action<Merchant> StockChanged;
    [Header("Identity")]
    [SerializeField, InspectorName("Name")] private string characterName = "";
    [SerializeField] private string merchantName = "Merchant";
    [SerializeField] private MerchantStock stock;

    [Header("Behaviour")]
    [SerializeField] private bool ctrlClickSellsAll = true;
    [Tooltip("If enabled, this merchant only buys items that exist in its stock list.")]
    [SerializeField] private bool onlyBuysStockedItems = false;
    [SerializeField] private string cannotBuyItemPopupText = "Cannot sell that item to {merchant}.";

    [Header("Save Identity")]
    [Tooltip(
        "Optional override for save JSON merchantId. If empty, uses merchantStock:<Stock Save Key> (or asset file name when Stock Save Key is empty). Empty is normal — you only need a custom id if two NPCs share one MerchantStock asset.")]
    [SerializeField] private string merchantId;

    [Header("Refs (optional)")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;
    [SerializeField] private TMP_Text nameLabel;

    public string MerchantName => merchantName;
    /// <summary>When true, this merchant only buys items it stocks.</summary>
    public bool OnlyBuysStockedItems => onlyBuysStockedItems;
    /// <summary>Inspector "Name" (e.g. person shown in bold above role on the world label).</summary>
    public string CharacterDisplayName =>
        string.IsNullOrWhiteSpace(characterName) ? "" : characterName.Trim();

    public MerchantStock Stock => stock;
    public string MerchantId => GetMerchantId();

    // Runtime quantities by stock entry index. We never mutate the ScriptableObject asset directly.
    private readonly List<int> _runtimeQuantities = new();

    private void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!wallet) wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
        ApplyIdentityToLabel();
        InitializeRuntimeStockFromDefaults();
    }

    private void OnEnable()
    {
        ApplyIdentityToLabel();
    }

    private void OnValidate()
    {
        ApplyIdentityToLabel();
    }

    public void RegisterNameLabel(TMP_Text label)
    {
        if (label)
            nameLabel = label;
    }

    public void RefreshNameLabel() => ApplyIdentityToLabel();

    private void ApplyIdentityToLabel()
    {
        if (!nameLabel)
            nameLabel = FindNameLabel();

        if (!nameLabel)
            return;

        string role = string.IsNullOrWhiteSpace(merchantName) ? "Merchant" : merchantName.Trim();
        NpcNameLabelFormatting.Apply(nameLabel, CharacterDisplayName, role);
    }

    private TMP_Text FindNameLabel()
    {
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label && label.name == "NameLabel")
                return label;
        }

        return null;
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
            GameLog.PurchaseFailed("Not enough stock", ResolveItemDisplayName(entry.itemId));
            Debug.Log($"[Merchant] Not enough stock for {itemId}.");
            return false;
        }

        if (!CanAfford(entry, amount))
        {
            GameLog.PurchaseFailed("Cannot afford", ResolveItemDisplayName(entry.itemId));
            Debug.Log($"[Merchant] Cannot afford {amount}x {itemId}.");
            return false;
        }

        if (!inventory.CanAdd(entry.itemId, amount))
        {
            GameLog.InventoryFull(ResolveItemDisplayName(entry.itemId));
            Debug.Log($"[Merchant] Inventory full, could not add {amount}x {itemId}.");
            return false;
        }

        SpendCosts(entry, amount);

        bool added = inventory.Add(entry.itemId, amount, null, notifyItemGainPopup: false);
        if (!added)
        {
            GameLog.PurchaseFailed("Purchase failed", ResolveItemDisplayName(entry.itemId));
            Debug.LogError($"[Merchant] Failed to add {amount}x {itemId} after spending costs.");
            return false;
        }

        if (available > 0)
            SetQuantity(entry, available - amount, persistToDisk: false);

        ItemGainPopupNotifier.Notify(entry.itemId, amount, purchased: true);
        SaveManager.Instance?.NotifyShopStockChanged();

        Debug.Log($"[Merchant] Bought {amount}x {itemId}.");
        return true;
    }

    public bool CanAfford(MerchantStock.Entry entry)
    {
        return CanAfford(entry, 1);
    }

    public bool CanAfford(MerchantStock.Entry entry, int amount)
    {
        if (entry == null) return false;
        if (amount <= 0) return true;
        if (entry.costs == null) return true;

        for (int i = 0; i < entry.costs.Count; i++)
        {
            var cost = entry.costs[i];
            if (cost == null || cost.amount <= 0) continue;
            int totalCostAmount = cost.amount * amount;

            switch (cost.type)
            {
                case MerchantStock.CostType.Gold:
                    if (!wallet || wallet.Gold < totalCostAmount)
                        return false;
                    break;

                case MerchantStock.CostType.Item:
                    if (!inventory || string.IsNullOrWhiteSpace(cost.itemId))
                        return false;

                    if (inventory.GetTotalAmount(cost.itemId) < totalCostAmount)
                        return false;
                    break;
            }
        }

        return true;
    }

    private void SpendCosts(MerchantStock.Entry entry)
    {
        SpendCosts(entry, 1);
    }

    private void SpendCosts(MerchantStock.Entry entry, int amountMultiplier)
    {
        if (entry == null || entry.costs == null || amountMultiplier <= 0) return;

        for (int i = 0; i < entry.costs.Count; i++)
        {
            var cost = entry.costs[i];
            if (cost == null || cost.amount <= 0) continue;
            int totalCostAmount = cost.amount * amountMultiplier;

            switch (cost.type)
            {
                case MerchantStock.CostType.Gold:
                    if (!wallet.SpendGold(totalCostAmount))
                        Debug.LogWarning($"[Merchant] Failed to spend {totalCostAmount} gold.");
                    break;

                case MerchantStock.CostType.Item:
                    if (!inventory.Remove(cost.itemId, totalCostAmount))
                        Debug.LogWarning($"[Merchant] Failed to remove {totalCostAmount}x {cost.itemId}.");
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
            int currentGold = wallet ? wallet.Gold : 0;

            foreach (var cost in entry.costs)
            {
                if (cost == null) continue;

                switch (cost.type)
                {
                    case MerchantStock.CostType.Gold:
                    {
                        bool canPay = wallet != null && currentGold >= cost.amount;
                        string line = $"• {cost.amount} Gold";
                        if (!canPay)
                            line = $"<color=#FF6B6B>{line}</color>";
                        sb.AppendLine(line);
                        break;
                    }

                    case MerchantStock.CostType.Item:
                    {
                        string itemName = cost.itemId;
                        bool canPay = inv != null && !string.IsNullOrWhiteSpace(cost.itemId) &&
                                      inv.GetTotalAmount(cost.itemId) >= cost.amount;
                        if (inv != null)
                        {
                            var def = inv.GetItemDef(cost.itemId);
                            if (def != null)
                                itemName = def.displayName;
                        }
                        string line = $"• {cost.amount} {itemName}";
                        if (!canPay)
                            line = $"<color=#FF6B6B>{line}</color>";
                        sb.AppendLine(line);
                        break;
                    }

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

    private string ResolveItemDisplayName(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "";

        if (inventory != null)
        {
            ItemDefinition def = inventory.GetItemDef(itemId);
            if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName;
        }

        return itemId;
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
            return entry != null ? Mathf.Max(-1, entry.defaultQuantity) : 0;

        if (index >= _runtimeQuantities.Count)
            return entry != null ? Mathf.Max(-1, entry.defaultQuantity) : 0;

        return _runtimeQuantities[index];
    }

    public void SetQuantity(MerchantStock.Entry entry, int quantity, bool persistToDisk = true)
    {
        int index = GetEntryIndex(entry);
        if (index < 0) return;

        EnsureRuntimeStockCapacity();
        int clamped = Mathf.Max(-1, quantity);
        if (_runtimeQuantities[index] == clamped)
            return;

        _runtimeQuantities[index] = clamped;
        StockChanged?.Invoke(this);
        if (persistToDisk && SaveManager.Instance != null)
            SaveManager.Instance.NotifyShopStockChanged();
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
            _runtimeQuantities.Add(e != null ? Mathf.Max(-1, e.defaultQuantity) : 0);
        }
    }

    /// <summary>Resets this merchant's runtime stock quantities to the MerchantStock default values.</summary>
    public void ResetStockToDefaults(bool persistToDisk = true)
    {
        InitializeRuntimeStockFromDefaults();
        StockChanged?.Invoke(this);
        if (persistToDisk && SaveManager.Instance != null)
            SaveManager.Instance.NotifyShopStockChanged();
    }

    private void EnsureRuntimeStockCapacity()
    {
        if (stock == null || stock.Items == null) return;
        while (_runtimeQuantities.Count < stock.Items.Count)
        {
            int i = _runtimeQuantities.Count;
            var e = stock.Items[i];
            _runtimeQuantities.Add(e != null ? Mathf.Max(-1, e.defaultQuantity) : 0);
        }
    }

    private string GetMerchantId()
    {
        if (!string.IsNullOrWhiteSpace(merchantId))
            return merchantId.Trim();

        if (stock != null)
            return $"merchantStock:{stock.StockSaveKey}";

        return $"{gameObject.scene.name}:{BuildPath(transform)}";
    }

    private static SaveData.MerchantStockSave FindMerchantStockSave(SaveData data, string id)
    {
        if (data?.merchantStocks == null || string.IsNullOrWhiteSpace(id))
            return null;

        for (int i = 0; i < data.merchantStocks.Count; i++)
        {
            SaveData.MerchantStockSave s = data.merchantStocks[i];
            if (s != null && s.merchantId == id)
                return s;
        }

        return null;
    }

    private List<string> BuildMerchantSaveLookupKeys()
    {
        var keys = new List<string>();

        if (!string.IsNullOrWhiteSpace(merchantId))
            keys.Add(merchantId.Trim());

        if (stock != null)
            keys.Add($"merchantStock:{stock.StockSaveKey}");

        keys.Add($"{gameObject.scene.name}:{BuildPath(transform)}");
        return keys;
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

        string id = GetMerchantId();
        List<string> legacyIds = BuildMerchantSaveLookupKeys();

        // Drop older save buckets for this vendor (e.g. scene path before we switched to merchantStock:<asset> keys).
        for (int i = data.merchantStocks.Count - 1; i >= 0; i--)
        {
            SaveData.MerchantStockSave s = data.merchantStocks[i];
            if (s == null || string.IsNullOrWhiteSpace(s.merchantId))
                continue;
            if (s.merchantId == id)
                continue;
            if (legacyIds.Contains(s.merchantId))
                data.merchantStocks.RemoveAt(i);
        }

        SaveData.MerchantStockSave save = FindMerchantStockSave(data, id);
        if (save == null)
        {
            save = new SaveData.MerchantStockSave { merchantId = id };
            data.merchantStocks.Add(save);
        }

        save.merchantId = id;

        EnsureRuntimeStockCapacity();
        int n = stock.Items.Count;
        save.quantities = new int[n];
        for (int i = 0; i < n; i++)
            save.quantities[i] = i < _runtimeQuantities.Count ? _runtimeQuantities[i] : 0;
    }

    public void LoadFrom(SaveData data)
    {
        // New Game path (empty save data) => reset to defaults.
        InitializeRuntimeStockFromDefaults();

        if (data == null || data.merchantStocks == null || data.merchantStocks.Count == 0 || stock == null || stock.Items == null)
            return;

        SaveData.MerchantStockSave matched = null;
        List<string> lookupKeys = BuildMerchantSaveLookupKeys();
        for (int k = 0; k < lookupKeys.Count; k++)
        {
            matched = FindMerchantStockSave(data, lookupKeys[k]);
            if (matched != null)
                break;
        }

        if (matched == null || matched.quantities == null || matched.quantities.Length == 0)
            return;

        EnsureRuntimeStockCapacity();
        int n = Mathf.Min(_runtimeQuantities.Count, matched.quantities.Length);
        for (int i = 0; i < n; i++)
            _runtimeQuantities[i] = Mathf.Max(-1, matched.quantities[i]);
    }
}