using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Town chest / bank storage: same slot shape as <see cref="Inventory"/>, separate grid (UI drives slot count).
/// </summary>
public class PlayerStorage : MonoBehaviour, ISaveable
{
    public const int DefaultSlotCount = 72;

    [SerializeField] private ItemDatabase itemDb;
    [SerializeField] private int defaultMaxStack = 99;

    [Serializable]
    public struct Slot
    {
        public string itemId;
        public int amount;

        public bool IsEmpty => string.IsNullOrEmpty(itemId) || amount <= 0;

        public void Clear()
        {
            itemId = null;
            amount = 0;
        }
    }

    private readonly List<Slot> _slots = new List<Slot>(DefaultSlotCount);

    public event Action OnStorageChanged;

    private int _batchChangeNotifyDepth;
    private bool _batchChangeNotifyPending;

    /// <summary>
    /// Delays <see cref="OnStorageChanged"/> until <see cref="EndBatchChanges"/> so multi-step transfers
    /// (store-all, withdraw-all) do not rebuild listeners once per partial stack.
    /// </summary>
    public void BeginBatchChanges() => _batchChangeNotifyDepth++;

    public void EndBatchChanges()
    {
        if (_batchChangeNotifyDepth <= 0)
        {
            _batchChangeNotifyDepth = 0;
            return;
        }

        _batchChangeNotifyDepth--;
        if (_batchChangeNotifyDepth > 0)
            return;

        if (_batchChangeNotifyPending)
        {
            _batchChangeNotifyPending = false;
            NotifyStorageChanged();
        }
    }

    private void NotifyStorageChanged()
    {
        if (_batchChangeNotifyDepth > 0)
            _batchChangeNotifyPending = true;
        else
            OnStorageChanged?.Invoke();
    }

    private void Awake()
    {
        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        EnsureSlotCount(DefaultSlotCount);
    }

    private int GetMaxStack(string itemId, int? maxStackOverride = null)
    {
        if (maxStackOverride.HasValue)
            return Mathf.Max(1, maxStackOverride.Value);

        if (string.IsNullOrWhiteSpace(itemId))
            return Mathf.Max(1, defaultMaxStack);

        if (!itemDb)
            return Mathf.Max(1, defaultMaxStack);

        var def = itemDb.Get(itemId);
        if (!def)
            return Mathf.Max(1, defaultMaxStack);

        return Mathf.Max(1, def.maxStack);
    }

    public ItemDefinition GetItemDef(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        // ItemDatabase may live in a scene that loads after this component's first Awake (e.g. DDOL player).
        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        if (!itemDb) return null;
        return itemDb.Get(itemId);
    }

    public int SlotCount => _slots.Count;

    public void EnsureSlotCount(int count)
    {
        count = Mathf.Max(1, count);
        while (_slots.Count < count) _slots.Add(new Slot());
        if (_slots.Count > count) _slots.RemoveRange(count, _slots.Count - count);
    }

    public Slot GetSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return default;
        return _slots[slotIndex];
    }

    public int GetTotalAmount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0;
        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].itemId == itemId)
                total += _slots[i].amount;
        }

        return total;
    }

    /// <summary>Removes stacks from highest slot index first (same order as <see cref="Inventory.Remove"/>).</summary>
    public bool Remove(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
        if (GetTotalAmount(itemId) < amount) return false;

        for (int i = _slots.Count - 1; i >= 0 && amount > 0; i--)
        {
            var s = _slots[i];
            if (s.IsEmpty || s.itemId != itemId) continue;

            int take = Mathf.Min(s.amount, amount);
            s.amount -= take;
            amount -= take;
            if (s.amount <= 0) s.Clear();
            _slots[i] = s;
        }

        NotifyStorageChanged();
        return amount == 0;
    }

    public void ReplaceSlot(int slotIndex, Slot newSlot)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return;
        _slots[slotIndex] = newSlot;
        NotifyStorageChanged();
    }

    public bool SwapSlots(int slotA, int slotB)
    {
        if (slotA == slotB) return false;
        if (slotA < 0 || slotB < 0) return false;
        if (slotA >= _slots.Count || slotB >= _slots.Count) return false;

        (_slots[slotA], _slots[slotB]) = (_slots[slotB], _slots[slotA]);
        NotifyStorageChanged();
        return true;
    }

    /// <summary>Same rules as <see cref="Inventory.SortByDatabaseOrder"/>: merge stacks, then order by database index (larger stacks first per item).</summary>
    public void SortByDatabaseOrder()
    {
        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        if (itemDb == null)
        {
            Debug.LogWarning("[PlayerStorage] Cannot sort — ItemDatabase missing.");
            return;
        }

        var filled = new List<Slot>();

        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsEmpty)
                filled.Add(_slots[i]);
        }

        var totals = new Dictionary<string, int>();
        foreach (var s in filled)
        {
            if (!totals.ContainsKey(s.itemId))
                totals[s.itemId] = 0;
            totals[s.itemId] += s.amount;
        }

        var merged = new List<Slot>();
        var itemIds = new List<string>(totals.Keys);
        itemIds.Sort((a, b) => itemDb.GetIndex(a).CompareTo(itemDb.GetIndex(b)));

        foreach (var itemId in itemIds)
        {
            int total = totals[itemId];
            int maxStack = GetMaxStack(itemId);
            int remaining = total;
            while (remaining > 0)
            {
                int chunk = Mathf.Min(maxStack, remaining);
                merged.Add(new Slot { itemId = itemId, amount = chunk });
                remaining -= chunk;
            }
        }

        merged.Sort((a, b) =>
        {
            int indexA = itemDb.GetIndex(a.itemId);
            int indexB = itemDb.GetIndex(b.itemId);

            int result = indexA.CompareTo(indexB);
            if (result != 0) return result;

            return b.amount.CompareTo(a.amount);
        });

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            s.Clear();
            _slots[i] = s;
        }

        int limit = Mathf.Min(merged.Count, _slots.Count);
        for (int i = 0; i < limit; i++)
            _slots[i] = merged[i];

        if (merged.Count > _slots.Count)
            Debug.LogError($"[PlayerStorage] After sort/merge need {merged.Count} slots but only {_slots.Count} exist — overflow.");

        NotifyStorageChanged();
    }

    public int RemoveAmountAtSlot(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return 0;
        if (amount <= 0) return 0;

        var s = _slots[slotIndex];
        if (s.IsEmpty) return 0;

        int removed = Mathf.Min(amount, s.amount);
        s.amount -= removed;
        if (s.amount <= 0) s.Clear();

        _slots[slotIndex] = s;
        NotifyStorageChanged();
        return removed;
    }

    public int MoveAmount(int fromSlot, int toSlot, int amount, int? maxStackOverride = null)
    {
        if (fromSlot < 0 || toSlot < 0) return 0;
        if (fromSlot >= _slots.Count || toSlot >= _slots.Count) return 0;
        if (fromSlot == toSlot) return 0;
        if (amount <= 0) return 0;

        var from = _slots[fromSlot];
        var to = _slots[toSlot];

        if (from.IsEmpty) return 0;

        int move = Mathf.Min(amount, from.amount);
        if (move <= 0) return 0;

        if (to.IsEmpty)
        {
            to.itemId = from.itemId;
            to.amount = move;
            from.amount -= move;
            if (from.amount <= 0) from.Clear();

            _slots[fromSlot] = from;
            _slots[toSlot] = to;
            NotifyStorageChanged();
            return move;
        }

        if (to.itemId == from.itemId)
        {
            int maxStack = GetMaxStack(from.itemId, maxStackOverride);
            int space = maxStack - to.amount;
            if (space <= 0) return 0;

            int add = Mathf.Min(space, move);
            to.amount += add;
            from.amount -= add;

            if (from.amount <= 0) from.Clear();

            _slots[fromSlot] = from;
            _slots[toSlot] = to;
            NotifyStorageChanged();
            return add;
        }

        return 0;
    }

    /// <summary>Moves items from an inventory slot into a storage slot (stack merge rules).</summary>
    public int TryMoveFromInventoryToStorage(Inventory inv, int fromInvSlot, int toStorageSlot, int amount, int? maxStackOverride = null)
    {
        if (inv == null) return 0;
        if (toStorageSlot < 0 || toStorageSlot >= _slots.Count) return 0;

        var from = inv.GetSlot(fromInvSlot);
        if (from.IsEmpty) return 0;

        amount = Mathf.Min(amount, from.amount);
        if (amount <= 0) return 0;

        var to = _slots[toStorageSlot];

        if (to.IsEmpty)
        {
            to.itemId = from.itemId;
            to.amount = amount;
            _slots[toStorageSlot] = to;
            inv.RemoveAmountAtSlot(fromInvSlot, amount);
            NotifyStorageChanged();
            return amount;
        }

        if (to.itemId == from.itemId)
        {
            int maxStack = GetMaxStack(from.itemId, maxStackOverride);
            int space = maxStack - to.amount;
            int add = Mathf.Min(space, amount);
            if (add <= 0) return 0;

            to.amount += add;
            _slots[toStorageSlot] = to;
            inv.RemoveAmountAtSlot(fromInvSlot, add);
            NotifyStorageChanged();
            return add;
        }

        return 0;
    }

    /// <summary>Moves items from a storage slot into an inventory slot (stack merge rules).</summary>
    public int TryMoveFromStorageToInventory(Inventory inv, int fromStorageSlot, int toInvSlot, int amount, int? maxStackOverride = null)
    {
        if (inv == null) return 0;
        if (fromStorageSlot < 0 || fromStorageSlot >= _slots.Count) return 0;

        var from = _slots[fromStorageSlot];
        if (from.IsEmpty) return 0;

        amount = Mathf.Min(amount, from.amount);
        if (amount <= 0) return 0;

        var to = inv.GetSlot(toInvSlot);

        if (to.IsEmpty)
        {
            int maxStack = GetMaxStack(from.itemId, maxStackOverride);
            int move = Mathf.Min(amount, maxStack);
            inv.ReplaceSlot(toInvSlot, new Inventory.Slot { itemId = from.itemId, amount = move });
            RemoveAmountAtSlot(fromStorageSlot, move);
            return move;
        }

        if (to.itemId == from.itemId)
        {
            int maxStack = GetMaxStack(from.itemId, maxStackOverride);
            int space = maxStack - to.amount;
            int add = Mathf.Min(space, amount);
            if (add <= 0) return 0;

            var merged = to;
            merged.amount += add;
            inv.ReplaceSlot(toInvSlot, merged);
            RemoveAmountAtSlot(fromStorageSlot, add);
            return add;
        }

        return 0;
    }

    public bool SwapInventorySlotWithStorage(Inventory inv, int invSlot, int storageSlot)
    {
        if (inv == null) return false;
        if (storageSlot < 0 || storageSlot >= _slots.Count) return false;

        var a = inv.GetSlot(invSlot);
        var b = _slots[storageSlot];

        inv.ReplaceSlot(invSlot, new Inventory.Slot
        {
            itemId = b.IsEmpty ? null : b.itemId,
            amount = b.IsEmpty ? 0 : b.amount
        });
        _slots[storageSlot] = new Slot
        {
            itemId = a.IsEmpty ? null : a.itemId,
            amount = a.IsEmpty ? 0 : a.amount
        };
        NotifyStorageChanged();
        return true;
    }

    /// <summary>
    /// Places items not coming from an inventory slot (e.g. unequipped gear) into storage,
    /// merging into matching stacks then filling empty slots up to max stack.
    /// </summary>
    /// <param name="touchedSlotIndices">If non-null, each storage slot index that received items is appended (merge or new stack).</param>
    public int TryDepositAmountFromExternal(string itemId, int amount, IList<int> touchedSlotIndices = null)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return 0;

        int movedTotal = 0;
        int remaining = amount;

        while (remaining > 0)
        {
            bool progressed = false;

            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty || s.itemId != itemId) continue;

                int maxStack = GetMaxStack(itemId);
                int space = maxStack - s.amount;
                if (space <= 0) continue;

                int add = Mathf.Min(space, remaining);
                s.amount += add;
                remaining -= add;
                movedTotal += add;
                _slots[i] = s;
                touchedSlotIndices?.Add(i);
                progressed = true;
            }

            if (remaining <= 0) break;

            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;

                int maxStack = GetMaxStack(itemId);
                int chunk = Mathf.Min(remaining, maxStack);
                _slots[i] = new Slot { itemId = itemId, amount = chunk };
                remaining -= chunk;
                movedTotal += chunk;
                touchedSlotIndices?.Add(i);
                progressed = true;
                break;
            }

            if (!progressed) break;
        }

        if (movedTotal > 0)
            NotifyStorageChanged();

        return movedTotal;
    }

    /// <summary>How many of <paramref name="amount"/> could be deposited from external source (same rules as <see cref="TryDepositAmountFromExternal"/>).</summary>
    public int GetReceivableAmountFromExternal(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return 0;

        int remaining = amount;
        int total = 0;

        while (remaining > 0)
        {
            bool progressed = false;

            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty || s.itemId != itemId)
                    continue;

                int maxStack = GetMaxStack(itemId);
                int space = maxStack - s.amount;
                if (space <= 0)
                    continue;

                int add = Mathf.Min(space, remaining);
                total += add;
                remaining -= add;
                progressed = true;
            }

            if (remaining <= 0)
                break;

            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty)
                    continue;

                int maxStack = GetMaxStack(itemId);
                int chunk = Mathf.Min(remaining, maxStack);
                total += chunk;
                remaining -= chunk;
                progressed = true;
                break;
            }

            if (!progressed)
                break;
        }

        return total;
    }

    /// <summary>Deposits every non-empty inventory stack into storage, limited by free storage space (same rules as per-slot deposit).</summary>
    public int TryDepositEntireInventory(Inventory inv)
    {
        if (inv == null) return 0;

        inv.BeginBatchChanges();
        BeginBatchChanges();
        try
        {
            int total = 0;
            int n = inv.SlotCount;
            for (int i = 0; i < n; i++)
            {
                if (inv.GetSlot(i).IsEmpty) continue;
                total += TryDepositAllFromInventorySlot(inv, i);
            }

            return total;
        }
        finally
        {
            inv.EndBatchChanges();
            EndBatchChanges();
        }
    }

    /// <summary>Removes up to <paramref name="amount"/> of <paramref name="itemId"/> across slots (for rollback after partial external deposit).</summary>
    public int RemoveItemAmountAcrossSlots(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return 0;

        int removedTotal = 0;
        int toRemove = amount;

        while (toRemove > 0)
        {
            int progressed = 0;
            for (int i = 0; i < _slots.Count && toRemove > 0; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty || s.itemId != itemId) continue;

                int r = RemoveAmountAtSlot(i, Mathf.Min(toRemove, s.amount));
                if (r <= 0) continue;

                toRemove -= r;
                removedTotal += r;
                progressed += r;
            }

            if (progressed == 0) break;
        }

        return removedTotal;
    }

    /// <summary>Deposit as much as possible from one inventory slot (double-click from inventory).</summary>
    public int TryDepositAllFromInventorySlot(Inventory inv, int invSlot)
    {
        if (inv == null) return 0;

        inv.BeginBatchChanges();
        BeginBatchChanges();
        try
        {
            int movedTotal = 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                var from = inv.GetSlot(invSlot);
                if (from.IsEmpty) break;

                int moved = TryMoveFromInventoryToStorage(inv, invSlot, i, from.amount, null);
                movedTotal += moved;
            }

            return movedTotal;
        }
        finally
        {
            inv.EndBatchChanges();
            EndBatchChanges();
        }
    }

    /// <summary>Withdraw everything from a storage slot into inventory (double-click from storage).</summary>
    public int TryWithdrawAllToInventory(Inventory inv, int storageSlot)
    {
        if (inv == null) return 0;

        inv.BeginBatchChanges();
        BeginBatchChanges();
        try
        {
            int movedTotal = 0;
            int slotCount = inv.SlotCount;

            for (int i = 0; i < slotCount; i++)
            {
                var from = _slots[storageSlot];
                if (from.IsEmpty) break;

                int moved = TryMoveFromStorageToInventory(inv, storageSlot, i, from.amount, null);
                movedTotal += moved;
            }

            return movedTotal;
        }
        finally
        {
            inv.EndBatchChanges();
            EndBatchChanges();
        }
    }

    public void SaveInto(SaveData data)
    {
        if (data == null) return;

        data.storageSlotCount = _slots.Count;
        if (data.storageSlots == null)
            data.storageSlots = new List<SaveData.InventorySlotData>();
        else
            data.storageSlots.Clear();

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            data.storageSlots.Add(new SaveData.InventorySlotData
            {
                itemId = s.IsEmpty ? null : s.itemId,
                amount = s.IsEmpty ? 0 : s.amount
            });
        }
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null) return;

        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        if (!itemDb)
            itemDb = Resources.Load<ItemDatabase>("ItemDatabase");
        if (!itemDb)
        {
            ItemDatabase[] loaded = Resources.FindObjectsOfTypeAll<ItemDatabase>();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null)
                {
                    itemDb = loaded[i];
                    break;
                }
            }
        }

        int count = data.storageSlotCount > 0 ? data.storageSlotCount : DefaultSlotCount;
        count = Mathf.Max(1, count);
        EnsureSlotCount(count);

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            s.Clear();
            _slots[i] = s;
        }

        if (itemDb)
        {
            itemDb.LoadRuntimeEnhancedItemsFrom(data);
            MapEnhancementRegistry.LoadFrom(data, itemDb);
        }

        if (data.storageSlots != null)
        {
            int n = Mathf.Min(_slots.Count, data.storageSlots.Count);
            bool canValidateDefs = itemDb != null;
            for (int i = 0; i < n; i++)
            {
                var d = data.storageSlots[i];
                if (string.IsNullOrWhiteSpace(d.itemId) || d.amount <= 0) continue;

                string id = Inventory.RemapLegacyItemId(d.itemId);
                if (canValidateDefs && GetItemDef(id) == null)
                {
                    Debug.LogWarning($"[PlayerStorage] Unknown itemId '{d.itemId}' remapped to '{id}' but still not found. Clearing storage slot {i}.");
                    continue;
                }

                if (!canValidateDefs)
                    Debug.LogWarning($"[PlayerStorage] ItemDatabase unavailable during LoadFrom; preserving storage slot {i} item '{id}' without validation.");

                _slots[i] = new Slot { itemId = id, amount = d.amount };
            }
        }

        NotifyStorageChanged();
    }
}
