using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour, ISaveable
{
    [SerializeField] private ItemDatabase itemDb;
    [SerializeField] private int defaultMaxStack = 99;
    [SerializeField, Min(1)] private int startingSlotCount = 24;

    private void Awake()
    {
        EnsureItemDatabaseRef();
        EnsureSlotCount(startingSlotCount);
        if (!itemDb)
            Debug.LogError("[Inventory] ItemDatabase not found. Item defs/values/tooltips will be NULL.");
    }

    private void EnsureItemDatabaseRef()
    {
        if (itemDb)
            return;

        itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        if (itemDb)
            return;

        itemDb = Resources.Load<ItemDatabase>("ItemDatabase");
        if (itemDb)
            return;

        ItemDatabase[] loaded = Resources.FindObjectsOfTypeAll<ItemDatabase>();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null)
            {
                itemDb = loaded[i];
                return;
            }
        }
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

        EnsureItemDatabaseRef();
        if (!itemDb)
        {
            Debug.LogError("[Inventory] GetItemDef called but itemDb is NULL.");
            return null;
        }

        var def = itemDb.Get(itemId);
        if (!def)
            Debug.LogWarning($"[Inventory] ItemDefinition not found for id '{itemId}' in ItemDatabase '{itemDb.name}'.");

        return def;
    }

    public ItemDefinition CreateRuntimeEnhancedItem(ItemDefinition baseDef, string runtimeItemId = null)
    {
        return itemDb ? itemDb.CreateRuntimeEnhancedItem(baseDef, runtimeItemId) : null;
    }

    public bool IsRuntimeEnhancedItem(string itemId)
    {
        return itemDb && itemDb.IsRuntimeEnhancedItem(itemId);
    }

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

    private readonly List<Slot> _slots = new List<Slot>(32);

    public event Action OnInventoryChanged;
    public event Action OnInventoryFull;

    private int _batchChangeNotifyDepth;
    private bool _batchChangeNotifyPending;

    /// <summary>
    /// Delays <see cref="OnInventoryChanged"/> until the matching <see cref="EndBatchChanges"/> so bulk moves
    /// (e.g. store-all) do not rebuild UI once per partial stack transfer.
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
            NotifyInventoryChanged();
        }
    }

    private void NotifyInventoryChanged()
    {
        if (_batchChangeNotifyDepth > 0)
            _batchChangeNotifyPending = true;
        else
            OnInventoryChanged?.Invoke();
    }

    public int SlotCount => _slots.Count;

    /// <summary>
    /// Increase inventory capacity by <paramref name="amount"/> slots.
    /// Never shrinks capacity.
    /// </summary>
    public void UnlockAdditionalSlots(int amount)
    {
        if (amount <= 0)
            return;

        int before = _slots.Count;
        EnsureSlotCount(before + amount);
        int gained = _slots.Count - before;
        if (gained > 0)
        {
            NotifyInventoryChanged();
            GameLog.Add($"Inventory expanded +{gained}");
        }
    }

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

    /// <summary>Used by storage drag-drop to write an exact slot after validation (e.g. cross-container swap).</summary>
    public void ReplaceSlot(int slotIndex, Slot newSlot)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return;
        _slots[slotIndex] = newSlot;
        NotifyInventoryChanged();
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

    public bool IsFull()
    {
        foreach (var slot in _slots)
        {
            if (slot.IsEmpty)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Adds as much as possible. Returns how many were actually added (0..amount).
    /// Does NOT fire OnInventoryFull (caller decides what to do with overflow).
    /// </summary>
    /// <param name="notifyItemGainPopup">False skips item-gained world popups.</param>
    /// <param name="touchedSlotIndices">If non-null, receives each inventory slot index that was created or had its amount increased.</param>
    public int AddPartial(string itemId, int amount = 1, int? maxStackOverride = null, bool notifyItemGainPopup = true, IList<int> touchedSlotIndices = null)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return 0;

        int maxStack = GetMaxStack(itemId, maxStackOverride);
        int remaining = amount;
        int addedTotal = 0;

        // 1) Fill existing stacks
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty || s.itemId != itemId) continue;

            int space = maxStack - s.amount;
            if (space <= 0) continue;

            int add = Mathf.Min(space, remaining);
            s.amount += add;
            _slots[i] = s;
            touchedSlotIndices?.Add(i);

            remaining -= add;
            addedTotal += add;
        }

        // 2) Create new stacks in empty slots
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty) continue;

            int add = Mathf.Min(maxStack, remaining);
            _slots[i] = new Slot { itemId = itemId, amount = add };
            touchedSlotIndices?.Add(i);

            remaining -= add;
            addedTotal += add;
        }

        if (addedTotal > 0)
        {
            NotifyInventoryChanged();
            if (notifyItemGainPopup)
                ItemGainPopupNotifier.Notify(itemId, addedTotal);
        }

        return addedTotal;
    }

    /// <summary>How many of <paramref name="amount"/> could fit without changing inventory (same rules as <see cref="AddPartial"/>).</summary>
    public int GetReceivableAmount(string itemId, int amount, int? maxStackOverride = null)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return 0;

        int maxStack = GetMaxStack(itemId, maxStackOverride);
        int remaining = amount;
        int total = 0;

        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty || s.itemId != itemId)
                continue;

            int space = maxStack - s.amount;
            if (space <= 0)
                continue;

            int add = Mathf.Min(space, remaining);
            total += add;
            remaining -= add;
        }

        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty)
                continue;

            int add = Mathf.Min(maxStack, remaining);
            total += add;
            remaining -= add;
        }

        return total;
    }

    /// <summary>Add items to the bag. Set <paramref name="notifyItemGainPopup"/> false to skip world "item gained" feedback.</summary>
    public bool Add(string itemId, int amount = 1, int? maxStackOverride = null, bool notifyItemGainPopup = true)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return true;

        int beforeTotal = GetTotalAmount(itemId);

        int itemValue = GetItemValue(itemId);
        int totalValue = itemValue * amount;
        //Debug.Log($"[Inventory] Gained {amount}x {itemId} | Value per item: {itemValue} | Total value: {totalValue}");

        int maxStack = GetMaxStack(itemId, maxStackOverride);

        // 1) Fill existing stacks
        for (int i = 0; i < _slots.Count && amount > 0; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty || s.itemId != itemId) continue;

            int space = maxStack - s.amount;
            if (space <= 0) continue;

            int add = Mathf.Min(space, amount);
            s.amount += add;
            _slots[i] = s;
            amount -= add;
        }

        // 2) Create new stacks in empty slots
        for (int i = 0; i < _slots.Count && amount > 0; i++)
        {
            if (!_slots[i].IsEmpty) continue;

            int add = Mathf.Min(maxStack, amount);
            _slots[i] = new Slot { itemId = itemId, amount = add };
            amount -= add;
        }

        bool overflow = amount > 0;

        NotifyInventoryChanged();     

        if (overflow)
            OnInventoryFull?.Invoke();

        int gained = GetTotalAmount(itemId) - beforeTotal;
        if (gained > 0 && notifyItemGainPopup)
            ItemGainPopupNotifier.Notify(itemId, gained);

        return !overflow;
    }

    public bool Remove(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;

        int available = GetTotalAmount(itemId);
        if (available < amount) return false;

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

        NotifyInventoryChanged();
        return true;
    }

    public void RemoveStackAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return;

        var s = _slots[slotIndex];
        if (s.IsEmpty) return;

        s.Clear();
        _slots[slotIndex] = s;
        NotifyInventoryChanged();
    }

    public bool SwapSlots(int slotA, int slotB)
    {
        if (slotA == slotB) return false;
        if (slotA < 0 || slotB < 0) return false;
        if (slotA >= _slots.Count || slotB >= _slots.Count) return false;

        (_slots[slotA], _slots[slotB]) = (_slots[slotB], _slots[slotA]);
        NotifyInventoryChanged();
        return true;
    }

    public int GetItemValue(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !itemDb) return 0;

        var def = itemDb.Get(itemId);
        if (!def) return 0;

        return Mathf.Max(0, def.value);
    }

    public int GetSlotValue(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return 0;

        var s = _slots[slotIndex];
        if (s.IsEmpty) return 0;

        return GetItemValue(s.itemId) * s.amount;
    }

    public int GetTotalInventoryValue()
    {
        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
            total += GetSlotValue(i);
        return total;
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
        NotifyInventoryChanged();

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

            NotifyInventoryChanged();
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

            NotifyInventoryChanged();
            return add;
        }

        return 0;
    }

    public void SaveInto(SaveData data)
    {
        if (data == null) return;

        if (itemDb)
            itemDb.SaveRuntimeEnhancedItemsInto(data);

        data.inventorySlotCount = _slots.Count;

        if (data.inventorySlots == null)
            data.inventorySlots = new List<SaveData.InventorySlotData>();
        else
            data.inventorySlots.Clear();

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            data.inventorySlots.Add(new SaveData.InventorySlotData
            {
                itemId = s.IsEmpty ? null : s.itemId,
                amount = s.IsEmpty ? 0 : s.amount
            });
        }
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null) return;

        EnsureItemDatabaseRef();
        if (itemDb)
            itemDb.LoadRuntimeEnhancedItemsFrom(data);

        // Make sure we have the right slot count first
        int count = Mathf.Max(1, data.inventorySlotCount > 0 ? data.inventorySlotCount : 32);
        EnsureSlotCount(count);

        // Clear everything
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            s.Clear();
            _slots[i] = s;
        }

        // Restore saved slots (clamp to current size)
        if (data.inventorySlots != null)
        {
            int n = Mathf.Min(_slots.Count, data.inventorySlots.Count);
            bool canValidateDefs = itemDb != null;
            for (int i = 0; i < n; i++)
            {
                var d = data.inventorySlots[i];
                if (string.IsNullOrWhiteSpace(d.itemId) || d.amount <= 0) continue;

                string id = RemapLegacyItemId(d.itemId);

                // If DB is ready and still unknown, skip broken id.
                // If DB is not ready yet (scene init race), preserve raw slot data so items are not lost.
                if (canValidateDefs && GetItemDef(id) == null)
                {
                    Debug.LogWarning($"[Inventory] Unknown itemId '{d.itemId}' remapped to '{id}' but still not found. Clearing slot {i}.");
                    continue;
                }

                if (!canValidateDefs)
                    Debug.LogWarning($"[Inventory] ItemDatabase unavailable during LoadFrom; preserving slot {i} item '{id}' without validation.");

                _slots[i] = new Slot
                {
                    itemId = id,
                    amount = Mathf.Max(0, d.amount)
                };
            }
        }

        NotifyInventoryChanged();
    }

    public bool IsCapacityFull()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];

            if (s.IsEmpty)
                return false;

            int maxStack = GetMaxStack(s.itemId, null);
            if (s.amount < maxStack)
                return false;
        }

        return true;
    }

    public bool CanAdd(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return false;

        int maxStack = GetMaxStack(itemId, null);
        int remaining = amount;

        // 1) Check existing stacks
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty || s.itemId != itemId) continue;

            int space = maxStack - s.amount;
            if (space <= 0) continue;

            remaining -= Mathf.Min(space, remaining);
        }

        // 2) Check empty slots
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                remaining -= Mathf.Min(maxStack, remaining);
            }
        }

        return remaining <= 0;
    }

    /// <summary>
    /// Places items not from an existing inventory slot (e.g. unequipped gear) into <paramref name="slotIndex"/>.
    /// Empty/same-item: fills or merges that slot first; overflow uses normal <see cref="AddPartial"/> rules.
    /// Different item: swaps with that slot and stacks the displaced items elsewhere (fails if they cannot fit).
    /// </summary>
    public bool TryPlaceExternalAtSlot(
        string itemId,
        int amount,
        int slotIndex,
        int? maxStackOverride = null,
        bool notifyItemGainPopup = true)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
        if (slotIndex < 0 || slotIndex >= _slots.Count) return false;

        int maxStack = GetMaxStack(itemId, maxStackOverride);
        var to = _slots[slotIndex];

        if (to.IsEmpty)
        {
            if (!CanAdd(itemId, amount)) return false;

            int chunk = Mathf.Min(amount, maxStack);
            ReplaceSlot(slotIndex, new Slot { itemId = itemId, amount = chunk });
            int remainder = amount - chunk;
            if (remainder > 0)
            {
                int added = AddPartial(itemId, remainder, maxStackOverride, notifyItemGainPopup);
                if (added != remainder)
                {
                    Debug.LogError("[Inventory] TryPlaceExternalAtSlot: overflow after CanAdd — state may be inconsistent.");
                }
            }

            return true;
        }

        if (to.itemId == itemId)
        {
            if (!CanAdd(itemId, amount)) return false;

            int space = maxStack - to.amount;
            int add = Mathf.Min(space, amount);
            if (add <= 0) return false;

            var merged = to;
            merged.amount += add;
            ReplaceSlot(slotIndex, merged);
            int remainder = amount - add;
            if (remainder > 0)
            {
                int added = AddPartial(itemId, remainder, maxStackOverride, notifyItemGainPopup);
                if (added != remainder)
                {
                    Debug.LogError("[Inventory] TryPlaceExternalAtSlot: overflow after merge — state may be inconsistent.");
                }
            }

            return true;
        }

        var displaced = to;
        ReplaceSlot(slotIndex, new Slot { itemId = itemId, amount = amount });
        int placedDisplaced = AddPartial(displaced.itemId, displaced.amount, null, notifyItemGainPopup);
        if (placedDisplaced < displaced.amount)
        {
            if (placedDisplaced > 0)
                Remove(displaced.itemId, placedDisplaced);
            ReplaceSlot(slotIndex, displaced);
            return false;
        }

        return true;
    }

    public bool TryFindMainHandItemByToolKey(ToolKey requiredTool, out string foundItemId)
    {
        foundItemId = null;
        if (requiredTool == ToolKey.None) return false;

        int count = SlotCount; // change this if your inventory uses a different count

        for (int i = 0; i < count; i++)
        {
            var slot = GetSlot(i);
            if (slot.IsEmpty) continue;

            var def = GetItemDef(slot.itemId);
            if (!def) continue;

            if (def.equipSlot != EquipSlot.MainHand) continue;
            if (def.handVisualKey != requiredTool) continue;

            foundItemId = slot.itemId;
            return true;
        }

        return false;
    }

    /// <summary>Shared by <see cref="Inventory"/> and <see cref="PlayerStorage"/> when restoring saves.</summary>
    public static string RemapLegacyItemId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;

        // normalize first
        id = id.Trim().ToLowerInvariant().Replace(" ", "_");

        // legacy -> new
        return id switch
        {
            "log" => "wood_log",
            // add more as you rename things:
            // "ore" => "iron_ore",
            // "vamp ring" => "vamp_ring",
            // "crit ring" => "crit_ring",
            _ => id
        };
    }

    public void SortByDatabaseOrder()
    {
        if (itemDb == null)
        {
            Debug.LogWarning("[Inventory] Cannot sort - ItemDatabase missing.");
            return;
        }

        var filled = new List<Slot>();

        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsEmpty)
                filled.Add(_slots[i]);
        }

        // 1) Sum amounts per item (combine stacks)
        var totals = new Dictionary<string, int>();
        foreach (var s in filled)
        {
            if (!totals.ContainsKey(s.itemId))
                totals[s.itemId] = 0;
            totals[s.itemId] += s.amount;
        }

        // 2) Split totals into legal stacks (maxStack each), in database order
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

        // 3) Order: database index, then larger stacks first (same item type)
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
            Debug.LogError($"[Inventory] After sort/merge need {merged.Count} slots but only {_slots.Count} exist — save data may be invalid (overflowing stacks).");

        NotifyInventoryChanged();
    }
}