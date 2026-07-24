using System.Text;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;

public class Merchant : MonoBehaviour
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

    private MerchantStockRuntime StockRuntime => MerchantStockRuntime.EnsureInstance();

    private void Awake()
    {
        EnsureEconomyRefs();
        ApplyIdentityToLabel();
        MerchantStockRuntime.EnsureInstance();
    }

    /// <summary>
    /// Always re-resolve canonical player inventory — a cached first-found shell/scene copy
    /// can charge gold while placing bought items into the wrong bag (and miss save dirty).
    /// </summary>
    private void EnsureEconomyRefs()
    {
        inventory = Inventory.ResolvePlayer();
        if (!wallet)
            wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
    }

    /// <summary>Refreshes shop UI after persistent stock was loaded or changed off this instance.</summary>
    public void NotifyStockChanged() => StockChanged?.Invoke(this);

    private void OnEnable()
    {
        ApplyIdentityToLabel();
        OffscreenMarkerTargetRegistry.Register(OffscreenMarkerTargetRegistry.Kind.Npc, transform);
        WorldFloorFollowerRegistry.Register(transform, WorldFloorFollowerRegistry.Category.Actor);
    }

    private void OnDisable()
    {
        OffscreenMarkerTargetRegistry.Unregister(transform);
        WorldFloorFollowerRegistry.Unregister(transform);
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
        EnsureEconomyRefs();
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
        EnsureEconomyRefs();
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

        if (!TrySpendCosts(entry, amount))
        {
            GameLog.PurchaseFailed("Purchase failed", ResolveItemDisplayName(entry.itemId));
            Debug.LogWarning($"[Merchant] Could not spend costs for {amount}x {itemId}.");
            return false;
        }

        // CanAdd passed, but still use AddPartial so a race/partial fill never charges the player
        // while leaving a false "full fail" after items already landed in the bag.
        var touched = new List<int>(4);
        int added = inventory.AddPartial(entry.itemId, amount, notifyItemGainPopup: false, touchedSlotIndices: touched);
        if (added < amount)
        {
            if (added > 0)
                inventory.RemoveAmountFromTouchedSlots(added, touched);
            RefundCosts(entry, amount);
            GameLog.PurchaseFailed("Purchase failed", ResolveItemDisplayName(entry.itemId));
            Debug.LogError($"[Merchant] Failed to add {amount}x {itemId} after spending costs — refunded.");
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
        EnsureEconomyRefs();
        if (entry == null) return false;
        if (amount <= 0) return true;
        if (entry.costs == null) return true;

        for (int i = 0; i < entry.costs.Count; i++)
        {
            var cost = entry.costs[i];
            if (cost == null || cost.amount <= 0) continue;
            // Bulk (50x) multiplies can wrap int and look "free" — reject overflow as unaffordable.
            if (!TryComputeTotalCost(cost.amount, amount, out int totalCostAmount))
                return false;

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

    /// <summary>
    /// Spends all cost rows atomically. On any failure, already-spent rows are refunded so a
    /// multi-cost purchase cannot charge gold then skip an item cost (or the reverse) and still grant stock.
    /// </summary>
    private bool TrySpendCosts(MerchantStock.Entry entry, int amountMultiplier)
    {
        if (entry == null || amountMultiplier <= 0)
            return false;
        if (entry.costs == null || entry.costs.Count == 0)
            return true;

        var spentGoldTotals = new List<int>(entry.costs.Count);
        var spentItemIds = new List<string>(entry.costs.Count);
        var spentItemAmounts = new List<int>(entry.costs.Count);

        for (int i = 0; i < entry.costs.Count; i++)
        {
            var cost = entry.costs[i];
            if (cost == null || cost.amount <= 0)
                continue;

            if (!TryComputeTotalCost(cost.amount, amountMultiplier, out int totalCostAmount))
            {
                RefundPartialSpend(spentGoldTotals, spentItemIds, spentItemAmounts);
                Debug.LogWarning("[Merchant] Purchase cost overflow; aborting spend.");
                return false;
            }

            switch (cost.type)
            {
                case MerchantStock.CostType.Gold:
                    if (!wallet || !wallet.SpendGold(totalCostAmount))
                    {
                        RefundPartialSpend(spentGoldTotals, spentItemIds, spentItemAmounts);
                        Debug.LogWarning($"[Merchant] Failed to spend {totalCostAmount} gold.");
                        return false;
                    }

                    spentGoldTotals.Add(totalCostAmount);
                    break;

                case MerchantStock.CostType.Item:
                    if (!inventory || string.IsNullOrWhiteSpace(cost.itemId) ||
                        !inventory.Remove(cost.itemId, totalCostAmount))
                    {
                        RefundPartialSpend(spentGoldTotals, spentItemIds, spentItemAmounts);
                        Debug.LogWarning($"[Merchant] Failed to remove {totalCostAmount}x {cost.itemId}.");
                        return false;
                    }

                    spentItemIds.Add(cost.itemId);
                    spentItemAmounts.Add(totalCostAmount);
                    break;
            }
        }

        return true;
    }

    private void RefundPartialSpend(
        List<int> spentGoldTotals,
        List<string> spentItemIds,
        List<int> spentItemAmounts)
    {
        if (spentGoldTotals != null && wallet)
        {
            for (int i = 0; i < spentGoldTotals.Count; i++)
                wallet.AddGold(spentGoldTotals[i]);
        }

        if (spentItemIds == null || spentItemAmounts == null)
            return;

        int n = Mathf.Min(spentItemIds.Count, spentItemAmounts.Count);
        for (int i = 0; i < n; i++)
            RefundItemCostWithStorageOverflow(spentItemIds[i], spentItemAmounts[i]);
    }

    private void RefundCosts(MerchantStock.Entry entry, int amountMultiplier)
    {
        if (entry == null || entry.costs == null || amountMultiplier <= 0)
            return;

        for (int i = 0; i < entry.costs.Count; i++)
        {
            var cost = entry.costs[i];
            if (cost == null || cost.amount <= 0)
                continue;

            if (!TryComputeTotalCost(cost.amount, amountMultiplier, out int totalCostAmount))
            {
                Debug.LogWarning("[Merchant] Refund cost overflow; skipping this cost row.");
                continue;
            }

            switch (cost.type)
            {
                case MerchantStock.CostType.Gold:
                    if (wallet)
                        wallet.AddGold(totalCostAmount);
                    break;

                case MerchantStock.CostType.Item:
                    if (!string.IsNullOrWhiteSpace(cost.itemId))
                        RefundItemCostWithStorageOverflow(cost.itemId, totalCostAmount);
                    break;
            }
        }
    }

    /// <summary>
    /// Multiplies unit cost by purchase quantity without int wrap.
    /// Overflow returns false so bulk buys cannot charge a wrapped (negative/tiny) total.
    /// </summary>
    private static bool TryComputeTotalCost(int unitCost, int quantity, out int total)
    {
        total = 0;
        if (unitCost <= 0 || quantity <= 0)
            return false;

        long product = (long)unitCost * quantity;
        if (product > int.MaxValue)
            return false;

        total = (int)product;
        return true;
    }

    private void RefundItemCostWithStorageOverflow(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        int left = amount;
        if (inventory)
            left -= inventory.AddPartial(itemId, left, notifyItemGainPopup: false);

        if (left <= 0)
            return;

        PlayerStorage storage = PlayerStorage.ResolvePlayer();
        if (storage != null)
            left -= storage.TryDepositAmountFromExternal(itemId, left);

        if (left > 0)
            PendingLootRecoveryStore.Enqueue(itemId, left);
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
            var inv = Inventory.ResolvePlayer();
            int currentGold = wallet ? wallet.Gold : 0;
            int stockQty = GetQuantity(entry);
            bool showBulkCost = ShopCostFormatter.ShouldShowBulkCost(stockQty);

            foreach (var cost in entry.costs)
            {
                if (cost == null) continue;

                switch (cost.type)
                {
                    case MerchantStock.CostType.Gold:
                    {
                        bool canPay = wallet != null && currentGold >= cost.amount;
                        string line = ShopCostFormatter.FormatTooltipGold(cost.amount, showBulkCost);
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
                        string line = ShopCostFormatter.FormatTooltipItemCost(cost.amount, itemName, showBulkCost);
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

        return ItemGainPopupNotifier.ResolveDisplayLabel(itemId, 1);
    }

    private string ResolveStockItemId(string itemId)
    {
        if (inventory != null)
            return inventory.ResolveStockItemId(itemId);

        if (string.IsNullOrWhiteSpace(itemId))
            return itemId;

        const string separator = "__enh_";
        int markerIndex = itemId.IndexOf(separator, StringComparison.Ordinal);
        return markerIndex > 0 ? itemId.Substring(0, markerIndex) : itemId;
    }

    public bool TryReplenishStockFromPlayerSale(string itemId, int amount, out int addedToStock)
    {
        addedToStock = 0;

        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || stock == null)
            return false;

        string stockItemId = ResolveStockItemId(itemId);
        var entry = stock.GetEntry(stockItemId);
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

        string stockItemId = ResolveStockItemId(itemId);
        return stock.GetEntry(stockItemId) != null;
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

        string stockItemId = ResolveStockItemId(itemId);
        var entry = stock.GetEntry(stockItemId);
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
        EnsureEconomyRefs();
        if (!inventory || !wallet)
            return;

        int goldGained = 0;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (slot.IsEmpty) continue;
            if (!CanBuyItemFromPlayer(slot.itemId)) continue;

            int valuePerItem = inventory.GetItemValue(slot.itemId);
            if (valuePerItem <= 0) continue;

            int amount = slot.amount;
            string itemId = slot.itemId;
            int desiredGold = CurrencyWallet.ComputeClampedSaleGold(valuePerItem, amount);
            if (desiredGold <= 0)
                continue;

            // Soft-ceiling may apply less than desired — never leave items sold for 0 gold,
            // and never record undo gold higher than what was actually granted.
            inventory.RemoveStackAtSlot(i);
            int appliedGold = wallet.AddGoldReturningApplied(desiredGold);
            if (appliedGold <= 0)
            {
                int restored = inventory.AddPartial(itemId, amount, notifyItemGainPopup: false);
                int left = amount - restored;
                if (left > 0)
                {
                    PlayerStorage storage = PlayerStorage.ResolvePlayer();
                    if (storage != null)
                        left -= storage.TryDepositAmountFromExternal(itemId, left);
                    if (left > 0)
                        PendingLootRecoveryStore.Enqueue(itemId, left);
                }

                break;
            }

            goldGained += appliedGold;
            if (goldGained < 0)
                goldGained = int.MaxValue;
            SaleUndoManager.Instance?.RecordSale(itemId, amount, appliedGold, this, stockAddedAmount: 0);
        }

        if (goldGained > 0)
            Debug.Log($"[Merchant] Sold items for {goldGained} gold.");
        else
            Debug.Log("[Merchant] Nothing sellable to sell.");
    }

    private void SellFirstNonEmptyStack()
    {
        EnsureEconomyRefs();
        if (!inventory || !wallet)
            return;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (slot.IsEmpty) continue;
            if (!CanBuyItemFromPlayer(slot.itemId)) continue;

            int valuePerItem = inventory.GetItemValue(slot.itemId);
            if (valuePerItem <= 0) continue;

            int amount = slot.amount;
            string itemId = slot.itemId;
            int desiredGold = CurrencyWallet.ComputeClampedSaleGold(valuePerItem, amount);
            if (desiredGold <= 0)
                continue;

            inventory.RemoveStackAtSlot(i);
            int goldGained = wallet.AddGoldReturningApplied(desiredGold);
            if (goldGained <= 0)
            {
                int restored = inventory.AddPartial(itemId, amount, notifyItemGainPopup: false);
                int left = amount - restored;
                if (left > 0)
                {
                    PlayerStorage storage = PlayerStorage.ResolvePlayer();
                    if (storage != null)
                        left -= storage.TryDepositAmountFromExternal(itemId, left);
                    if (left > 0)
                        PendingLootRecoveryStore.Enqueue(itemId, left);
                }

                Debug.Log("[Merchant] Gold at soft ceiling; nothing sold.");
                return;
            }

            SaleUndoManager.Instance?.RecordSale(itemId, amount, goldGained, this, stockAddedAmount: 0);

            Debug.Log($"[Merchant] Sold {amount}x {itemId} for {goldGained} gold.");
            return;
        }

        Debug.Log("[Merchant] Nothing sellable to sell.");
    }

    public int GetQuantity(MerchantStock.Entry entry)
    {
        int index = GetEntryIndex(entry);
        if (index < 0)
            return entry != null ? Mathf.Max(-1, entry.defaultQuantity) : 0;

        return StockRuntime.GetQuantity(GetMerchantId(), stock, index, transform);
    }

    public void SetQuantity(MerchantStock.Entry entry, int quantity, bool persistToDisk = true)
    {
        int index = GetEntryIndex(entry);
        if (index < 0) return;

        int before = StockRuntime.GetQuantity(GetMerchantId(), stock, index, transform);
        StockRuntime.SetQuantity(GetMerchantId(), stock, index, quantity, transform);
        int after = StockRuntime.GetQuantity(GetMerchantId(), stock, index, transform);
        if (before == after)
            return;

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

    /// <summary>Resets this merchant's runtime stock quantities to the MerchantStock default values.</summary>
    public void ResetStockToDefaults(bool persistToDisk = true)
    {
        if (stock == null)
            return;

        StockRuntime.ResetToDefaults(GetMerchantId(), stock);
        StockChanged?.Invoke(this);
        if (persistToDisk && SaveManager.Instance != null)
            SaveManager.Instance.NotifyShopStockChanged();
    }

    private string GetMerchantId()
    {
        if (!string.IsNullOrWhiteSpace(merchantId))
            return merchantId.Trim();

        if (stock != null)
            return $"merchantStock:{stock.StockSaveKey}";

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
}