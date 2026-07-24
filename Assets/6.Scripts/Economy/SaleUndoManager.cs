using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Recent player sales for the undo window.</summary>
public readonly struct SaleUndoSnapshot
{
    public readonly int Id;
    public readonly string ItemId;
    public readonly int Amount;
    public readonly int Gold;

    public SaleUndoSnapshot(int id, string itemId, int amount, int gold)
    {
        Id = id;
        ItemId = itemId;
        Amount = amount;
        Gold = gold;
    }
}

public class SaleUndoManager : MonoBehaviour
{
    public static SaleUndoManager Instance { get; private set; }

    public event Action EntriesChanged;

    [Serializable]
    private class SaleEntry
    {
        public int id;
        public string itemId;
        public int amount;
        public int gold;
        public string merchantId;
        public int stockAddedAmount;
    }

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    [Header("Behaviour")]
    [Tooltip("Cap on stored undo rows (all merchants combined). 0 = unlimited. No time-based expiry.")]
    [SerializeField] private int maxEntriesKept = 0;
    [Tooltip("Max undo rows kept per merchant. Oldest entries for that merchant are removed when exceeded.")]
    [SerializeField] private int maxEntriesPerMerchant = 30;

    public int MaxEntriesPerMerchant => Mathf.Max(1, maxEntriesPerMerchant);

    private readonly List<SaleEntry> _entries = new();
    private int _nextId = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebindRefs();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        RebindRefs();
    }

    private void RebindRefs()
    {
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!wallet) wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
    }

    /// <summary>Records a sale for undo for the active merchant.</summary>
    public void RecordSale(string itemId, int amount, int gold, Merchant merchant, int stockAddedAmount = 0)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || gold <= 0)
            return;

        if (merchant == null)
            return;

        if (!inventory || !wallet)
            RebindRefs();

        if (!inventory || !wallet)
            return;

        var entry = new SaleEntry
        {
            id = _nextId++,
            itemId = itemId,
            amount = amount,
            gold = gold,
            merchantId = merchant.MerchantId,
            stockAddedAmount = Mathf.Max(0, stockAddedAmount)
        };

        _entries.Insert(0, entry);

        TrimOldestEntriesForMerchant(merchant.MerchantId, MaxEntriesPerMerchant);

        if (maxEntriesKept > 0 && _entries.Count > maxEntriesKept)
            _entries.RemoveRange(maxEntriesKept, _entries.Count - maxEntriesKept);

        NotifyChanged();
    }

    public int GetRemainingCapacityForMerchant(string merchantId)
    {
        return Mathf.Max(0, MaxEntriesPerMerchant - GetUndoCountForMerchant(merchantId));
    }

    public int GetUndoCountForMerchant(string merchantId)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            return 0;

        string mid = merchantId.Trim();
        int n = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].merchantId, mid, StringComparison.Ordinal))
                n++;
        }

        return n;
    }

    public void CopyEntriesForMerchant(string merchantId, List<SaleUndoSnapshot> dst)
    {
        dst.Clear();
        if (string.IsNullOrWhiteSpace(merchantId))
            return;

        string mid = merchantId.Trim();
        for (int i = 0; i < _entries.Count; i++)
        {
            SaleEntry e = _entries[i];
            if (string.Equals(e.merchantId, mid, StringComparison.Ordinal))
                dst.Add(new SaleUndoSnapshot(e.id, e.itemId, e.amount, e.gold));
        }
    }

    public bool HasEntry(int entryId)
    {
        return _entries.FindIndex(e => e.id == entryId) >= 0;
    }

    /// <summary>
    /// Drops all undo rows. Required on full wipe / New Game / Load Game because this manager is
    /// DontDestroyOnLoad and is not an <see cref="ISaveable"/> — otherwise sales from a previous
    /// slot can be undone into a fresh character.
    /// </summary>
    public void ClearAllEntries()
    {
        if (_entries.Count == 0)
            return;

        _entries.Clear();
        NotifyChanged();
    }

    public bool TryUndoEntry(int entryId)
    {
        int idx = _entries.FindIndex(e => e.id == entryId);
        if (idx < 0)
            return false;

        return UndoAtIndex(idx);
    }

    private bool UndoAtIndex(int idx)
    {
        var e = _entries[idx];

        if (!wallet || !inventory)
            RebindRefs();
        if (!wallet || !inventory)
        {
            Debug.LogWarning("[SaleUndoManager] Undo aborted: wallet or inventory missing.");
            return false;
        }

        Merchant merchant = null;
        if (e.stockAddedAmount > 0)
        {
            merchant = FindMerchantById(e.merchantId);
            if (merchant == null || !merchant.TryRemoveReplenishedStock(e.itemId, e.stockAddedAmount))
                return false;
        }

        if (!wallet.SpendGold(e.gold))
        {
            // Keep the undo row — the player may earn gold later. Only roll back the
            // tentative stock removal; never destroy the recovery opportunity.
            if (merchant != null)
                merchant.TryReplenishStockFromPlayerSale(e.itemId, e.stockAddedAmount, out _);
            return false;
        }

        var touchedInv = new List<int>(4);
        int toInv = inventory.AddPartial(e.itemId, e.amount, notifyItemGainPopup: false, touchedSlotIndices: touchedInv);
        int left = e.amount - toInv;
        int toStorage = 0;
        var touchedStorage = new List<int>(4);
        PlayerStorage storage = null;
        if (left > 0)
        {
            storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
            if (storage != null)
            {
                toStorage = storage.TryDepositAmountFromExternal(e.itemId, left, touchedStorage);
                left -= toStorage;
            }
        }

        if (left > 0)
        {
            if (toInv > 0)
                inventory.RemoveAmountFromTouchedSlots(toInv, touchedInv);
            if (toStorage > 0 && storage != null)
                storage.RemoveAmountFromTouchedSlots(toStorage, touchedStorage);
            wallet.AddGold(e.gold);
            if (merchant != null)
                merchant.TryReplenishStockFromPlayerSale(e.itemId, e.stockAddedAmount, out _);
            return false;
        }

        if (toStorage > 0)
        {
            GameLog.Add(
                "Inventory was full — restored sold items to storage.",
                GameLog.CannotMessageColor);
        }

        string undoItemName = ItemGainPopupNotifier.ResolveDisplayLabel(e.itemId, e.amount);
        GameLog.SaleUndoRestored(undoItemName, e.amount, e.gold);

        _entries.RemoveAt(idx);
        NotifyChanged();
        return true;
    }

    private void TrimOldestEntriesForMerchant(string merchantId, int maxPerMerchant)
    {
        if (string.IsNullOrWhiteSpace(merchantId) || maxPerMerchant <= 0)
            return;

        string mid = merchantId.Trim();
        int count = GetUndoCountForMerchant(mid);
        while (count > maxPerMerchant)
        {
            int removeIdx = -1;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_entries[i].merchantId, mid, StringComparison.Ordinal))
                {
                    removeIdx = i;
                    break;
                }
            }

            if (removeIdx < 0)
                break;

            _entries.RemoveAt(removeIdx);
            count--;
        }
    }

    private void NotifyChanged()
    {
        EntriesChanged?.Invoke();
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
