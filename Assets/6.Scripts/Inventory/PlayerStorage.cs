using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Town chest / bank storage: same slot shape as <see cref="Inventory"/>, separate grid (default 7×4).
/// </summary>
public class PlayerStorage : MonoBehaviour, ISaveable
{
    public const int DefaultSlotCount = 28;

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

    public void ReplaceSlot(int slotIndex, Slot newSlot)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return;
        _slots[slotIndex] = newSlot;
        OnStorageChanged?.Invoke();
    }

    public bool SwapSlots(int slotA, int slotB)
    {
        if (slotA == slotB) return false;
        if (slotA < 0 || slotB < 0) return false;
        if (slotA >= _slots.Count || slotB >= _slots.Count) return false;

        (_slots[slotA], _slots[slotB]) = (_slots[slotB], _slots[slotA]);
        OnStorageChanged?.Invoke();
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

        OnStorageChanged?.Invoke();
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
        OnStorageChanged?.Invoke();
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
            OnStorageChanged?.Invoke();
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
            OnStorageChanged?.Invoke();
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
            OnStorageChanged?.Invoke();
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
            OnStorageChanged?.Invoke();
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
            inv.ReplaceSlot(toInvSlot, new Inventory.Slot { itemId = from.itemId, amount = amount });
            RemoveAmountAtSlot(fromStorageSlot, amount);
            return amount;
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
        OnStorageChanged?.Invoke();
        return true;
    }

    /// <summary>Deposit as much as possible from one inventory slot (double-click from inventory).</summary>
    public int TryDepositAllFromInventorySlot(Inventory inv, int invSlot)
    {
        if (inv == null) return 0;

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

    /// <summary>Withdraw everything from a storage slot into inventory (double-click from storage).</summary>
    public int TryWithdrawAllToInventory(Inventory inv, int storageSlot)
    {
        if (inv == null) return 0;

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

        int count = data.storageSlotCount > 0 ? data.storageSlotCount : DefaultSlotCount;
        count = Mathf.Max(1, count);
        EnsureSlotCount(count);

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            s.Clear();
            _slots[i] = s;
        }

        if (data.storageSlots != null)
        {
            int n = Mathf.Min(_slots.Count, data.storageSlots.Count);
            for (int i = 0; i < n; i++)
            {
                var d = data.storageSlots[i];
                if (string.IsNullOrWhiteSpace(d.itemId) || d.amount <= 0) continue;

                string id = Inventory.RemapLegacyItemId(d.itemId);
                if (GetItemDef(id) == null)
                {
                    Debug.LogWarning($"[PlayerStorage] Unknown itemId '{d.itemId}' remapped to '{id}' but still not found. Clearing storage slot {i}.");
                    continue;
                }

                _slots[i] = new Slot { itemId = id, amount = d.amount };
            }
        }

        OnStorageChanged?.Invoke();
    }
}
