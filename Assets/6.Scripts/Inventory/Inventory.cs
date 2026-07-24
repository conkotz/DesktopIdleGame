using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        itemDb = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
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
        if (!def && !IsObsoleteWandItemId(itemId))
            Debug.LogWarning($"[Inventory] ItemDefinition not found for id '{itemId}' in ItemDatabase '{itemDb.name}'.");

        return def;
    }

    public ItemDatabase GetItemDatabase()
    {
        EnsureItemDatabaseRef();
        return itemDb;
    }

    public int GetMaxStackForItem(string itemId) => GetMaxStack(itemId, null);

    public ItemDefinition CreateRuntimeEnhancedItem(ItemDefinition baseDef, string runtimeItemId = null)
    {
        return itemDb ? itemDb.CreateRuntimeEnhancedItem(baseDef, runtimeItemId) : null;
    }

    public bool IsRuntimeEnhancedItem(string itemId)
    {
        return itemDb && itemDb.IsRuntimeEnhancedItem(itemId);
    }

    /// <summary>Authored id used for merchant stock matching (rolls/enhancements map back to base).</summary>
    public string ResolveStockItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return itemId;

        EnsureItemDatabaseRef();
        return itemDb ? itemDb.GetBaseItemId(itemId) : itemId;
    }

    private bool ShouldRollRandomStatsOnAcquire(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        EnsureItemDatabaseRef();
        if (!itemDb || itemDb.IsRuntimeEnhancedItem(itemId))
            return false;

        ItemDefinition def = itemDb.Get(itemId);
        return ItemRandomStatRoller.ShouldRollOnAcquire(def, itemDb);
    }

    private string ResolveAcquiredItemId(string itemId)
    {
        if (!ShouldRollRandomStatsOnAcquire(itemId))
            return itemId;

        EnsureItemDatabaseRef();
        if (!itemDb)
            return itemId;

        ItemDefinition baseDef = itemDb.Get(itemId);
        if (!baseDef)
        {
            Debug.LogWarning($"[Inventory] Could not resolve item definition for '{itemId}' when rolling random stats.");
            return itemId;
        }

        if (!baseDef.HasRandomStatPool)
            return itemId;

        ItemDefinition rolled = ItemRandomStatRoller.CreateRolledItem(itemDb, baseDef);
        if (rolled == null)
        {
            Debug.LogWarning(
                $"[Inventory] Failed to roll random stats for '{itemId}'. " +
                $"Pool entries={baseDef.RandomStatPoolEntries?.Count ?? 0}. Using base item.");
            return itemId;
        }

        return rolled.itemId;
    }

    private int FindFirstEmptySlotIndex()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].IsEmpty)
                return i;
        }

        return -1;
    }

    private bool TryPlaceRolledUnit(string sourceItemId, IList<int> touchedSlotIndices, out int added)
    {
        added = 0;
        int slotIndex = FindFirstEmptySlotIndex();
        if (slotIndex < 0)
            return false;

        string resolvedId = ResolveAcquiredItemId(sourceItemId);
        _slots[slotIndex] = new Slot { itemId = resolvedId, amount = 1 };
        touchedSlotIndices?.Add(slotIndex);
        added = 1;
        return true;
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
    private bool _totalValueCacheValid;
    private int _cachedTotalInventoryValue;

    public event Action OnInventoryChanged;
    public event Action OnInventoryFull;

    /// <summary>Player inventory used by shop, character sheet, enhance, and save/load.</summary>
    public static Inventory ResolvePlayer()
    {
        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            Inventory onPlayer = player.GetComponent<Inventory>();
            if (onPlayer != null)
                return onPlayer;
        }

        Inventory[] all = UnityEngine.Object.FindObjectsByType<Inventory>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (all == null || all.Length == 0)
            return null;

        if (all.Length == 1)
            return all[0];

        Scene active = SceneManager.GetActiveScene();
        Transform playerRoot = player != null ? player.transform : null;

        Inventory best = null;
        int bestScore = -1;

        for (int i = 0; i < all.Length; i++)
        {
            Inventory inv = all[i];
            if (!inv)
                continue;

            int score = Mathf.Max(0, inv.SlotCount);
            if (playerRoot != null && inv.transform.IsChildOf(playerRoot))
                score += 10000;
            if (inv.gameObject.scene == active)
                score += 1000;

            if (score > bestScore)
            {
                bestScore = score;
                best = inv;
            }
        }

        return best != null ? best : all[0];
    }

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
        _totalValueCacheValid = false;

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
        // Hard cap matches SaveDataIntegrity — corrupt saves must not allocate unbounded slots.
        count = Mathf.Clamp(count, 1, 512);
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
        if (ShouldRollRandomStatsOnAcquire(itemId))
        {
            while (remaining > 0)
            {
                if (!TryPlaceRolledUnit(itemId, touchedSlotIndices, out int placed))
                    break;

                remaining -= placed;
                addedTotal += placed;
            }
        }
        else
        {
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;

                int add = Mathf.Min(maxStack, remaining);
                _slots[i] = new Slot { itemId = itemId, amount = add };
                touchedSlotIndices?.Add(i);

                remaining -= add;
                addedTotal += add;
            }
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
        int addedTotal = 0;

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
            addedTotal += add;
        }

        // 2) Create new stacks in empty slots
        if (ShouldRollRandomStatsOnAcquire(itemId))
        {
            while (amount > 0)
            {
                if (!TryPlaceRolledUnit(itemId, null, out int placed))
                    break;

                amount -= placed;
                addedTotal += placed;
            }
        }
        else
        {
            for (int i = 0; i < _slots.Count && amount > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;

                int add = Mathf.Min(maxStack, amount);
                _slots[i] = new Slot { itemId = itemId, amount = add };
                amount -= add;
                addedTotal += add;
            }
        }

        bool overflow = amount > 0;

        NotifyInventoryChanged();     

        if (overflow)
            OnInventoryFull?.Invoke();

        if (addedTotal <= 0 && !ShouldRollRandomStatsOnAcquire(itemId))
            addedTotal = GetTotalAmount(itemId) - beforeTotal;

        if (addedTotal > 0 && notifyItemGainPopup)
            ItemGainPopupNotifier.Notify(itemId, addedTotal);

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
        if (_totalValueCacheValid)
            return _cachedTotalInventoryValue;

        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
            total += GetSlotValue(i);

        _cachedTotalInventoryValue = total;
        _totalValueCacheValid = true;
        return total;
    }

    /// <summary>
    /// Undoes a prior <see cref="AddPartial"/> by removing only from the slots it reported as touched.
    /// Prefer this over <see cref="Remove"/> when rolling back a failed transfer — global Remove can
    /// delete pre-existing same-item stacks that were never part of the tentative add.
    /// </summary>
    public int RemoveAmountFromTouchedSlots(int amount, IList<int> touchedSlotIndices)
    {
        if (amount <= 0 || touchedSlotIndices == null || touchedSlotIndices.Count == 0)
            return 0;

        BeginBatchChanges();
        int left = amount;
        try
        {
            for (int t = touchedSlotIndices.Count - 1; t >= 0 && left > 0; t--)
            {
                int removed = RemoveAmountAtSlot(touchedSlotIndices[t], left);
                left -= removed;
            }
        }
        finally
        {
            EndBatchChanges();
        }

        return amount - left;
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

    public int CountItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        string target = itemId.Trim();
        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            if (slot.IsEmpty)
                continue;

            if (string.Equals(slot.itemId, target, System.StringComparison.OrdinalIgnoreCase))
                total += slot.amount;
        }

        return total;
    }

    public bool TryConsumeItem(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return false;

        if (CountItem(itemId) < amount)
            return false;

        int remaining = amount;
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            Slot slot = _slots[i];
            if (slot.IsEmpty)
                continue;

            if (!string.Equals(slot.itemId, itemId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                continue;

            int removed = RemoveAmountAtSlot(i, remaining);
            remaining -= removed;
        }

        return remaining <= 0;
    }

    public bool HasItems(IReadOnlyList<GearUpgradeMaterialRequirement> requirements)
    {
        if (requirements == null || requirements.Count == 0)
            return false;

        // Aggregate duplicate item ids so [{iron,2},{iron,2}] requires 4, not 2.
        var neededByItemId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < requirements.Count; i++)
        {
            GearUpgradeMaterialRequirement req = requirements[i];
            if (string.IsNullOrWhiteSpace(req.ItemId) || req.Amount <= 0)
                return false;

            string id = req.ItemId.Trim();
            neededByItemId.TryGetValue(id, out int soFar);
            neededByItemId[id] = soFar + req.Amount;
        }

        foreach (KeyValuePair<string, int> kv in neededByItemId)
        {
            if (CountItem(kv.Key) < kv.Value)
                return false;
        }

        return true;
    }

    public bool TryConsumeItems(IReadOnlyList<GearUpgradeMaterialRequirement> requirements)
    {
        if (!HasItems(requirements))
            return false;

        // Consume aggregated totals so duplicate rows cannot partially drain then fail.
        var neededByItemId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < requirements.Count; i++)
        {
            GearUpgradeMaterialRequirement req = requirements[i];
            if (string.IsNullOrWhiteSpace(req.ItemId) || req.Amount <= 0)
                continue;

            string id = req.ItemId.Trim();
            neededByItemId.TryGetValue(id, out int soFar);
            neededByItemId[id] = soFar + req.Amount;
        }

        var consumed = new List<KeyValuePair<string, int>>(neededByItemId.Count);
        foreach (KeyValuePair<string, int> kv in neededByItemId)
        {
            if (!TryConsumeItem(kv.Key, kv.Value))
            {
                // Roll back anything already removed this attempt (race / mismatch).
                PlayerStorage storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
                for (int r = 0; r < consumed.Count; r++)
                {
                    int left = consumed[r].Value;
                    left -= AddPartial(consumed[r].Key, left, notifyItemGainPopup: false);
                    if (left > 0 && storage != null)
                        left -= storage.TryDepositAmountFromExternal(consumed[r].Key, left);
                    if (left > 0)
                        PendingLootRecoveryStore.Enqueue(consumed[r].Key, left);
                }

                return false;
            }

            consumed.Add(kv);
        }

        return true;
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
            int maxStack = GetMaxStack(from.itemId, maxStackOverride);
            move = Mathf.Min(move, maxStack);
            if (move <= 0) return 0;

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

        EnsureItemDatabaseRef();
        if (itemDb)
        {
            itemDb.SaveRuntimeEnhancedItemsInto(data);
            MapEnhancementRegistry.SaveInto(data);
        }

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
        {
            itemDb.LoadRuntimeEnhancedItemsFrom(data);
            MapEnhancementRegistry.LoadFrom(data, itemDb);
        }

        // Make sure we have the right slot count first (hard-capped against corrupt saves).
        int count = Mathf.Clamp(data.inventorySlotCount > 0 ? data.inventorySlotCount : 32, 1, 512);
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
                    if (!IsObsoleteWandItemId(d.itemId))
                    {
                        Debug.LogWarning(
                            $"[Inventory] Unknown itemId '{d.itemId}' remapped to '{id}' but still not found. Clearing slot {i}.");
                    }

                    continue;
                }

                if (!canValidateDefs)
                    Debug.LogWarning($"[Inventory] ItemDatabase unavailable during LoadFrom; preserving slot {i} item '{id}' without validation.");

                int amount = Mathf.Max(0, d.amount);
                int maxStack = GetMaxStack(id);
                if (amount > maxStack)
                {
                    int overflow = amount - maxStack;
                    amount = maxStack;
                    PendingLootRecoveryStore.EnsureLists(data);
                    data.pendingLootRecoveryItemIds.Add(id);
                    data.pendingLootRecoveryAmounts.Add(overflow);
                    Debug.LogWarning(
                        $"[Inventory] Slot {i} amount {d.amount} exceeds maxStack {maxStack}; clamped and parked overflow into pending loot.");
                }

                // Persist clamp so a later LoadFrom on the same SaveData cannot re-park the overflow.
                data.inventorySlots[i] = new SaveData.InventorySlotData
                {
                    itemId = id,
                    amount = amount
                };

                _slots[i] = new Slot
                {
                    itemId = id,
                    amount = amount
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
                var touched = new List<int>(4);
                int added = AddPartial(itemId, remainder, maxStackOverride, notifyItemGainPopup, touched);
                if (added != remainder)
                {
                    // CanAdd raced or capacity rules diverged — never report success after a shortfall
                    // or the caller will unequip/destroy the source while leftover units vanish.
                    if (added > 0)
                        RemoveAmountFromTouchedSlots(added, touched);
                    ReplaceSlot(slotIndex, default);
                    return false;
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

            var previous = to;
            var merged = to;
            merged.amount += add;
            ReplaceSlot(slotIndex, merged);
            int remainder = amount - add;
            if (remainder > 0)
            {
                var touched = new List<int>(4);
                int added = AddPartial(itemId, remainder, maxStackOverride, notifyItemGainPopup, touched);
                if (added != remainder)
                {
                    // Undo overflow fills before restoring the target slot so we never strip
                    // pre-existing same-item stacks via a global Remove.
                    if (added > 0)
                        RemoveAmountFromTouchedSlots(added, touched);
                    ReplaceSlot(slotIndex, previous);
                    return false;
                }
            }

            return true;
        }

        // Different item: clamp to maxStack in the target slot (same as empty/same-item paths).
        // Never write an over-max amount — that persists across save/load and breaks capacity rules.
        var displaced = to;
        int placedHere = Mathf.Min(amount, maxStack);
        int incomingLeft = amount - placedHere;

        ReplaceSlot(slotIndex, new Slot { itemId = itemId, amount = placedHere });

        var touchedIncoming = new List<int>(4);
        int addedIncoming = 0;
        if (incomingLeft > 0)
            addedIncoming = AddPartial(itemId, incomingLeft, maxStackOverride, notifyItemGainPopup, touchedIncoming);

        var touchedDisplaced = new List<int>(4);
        int placedDisplaced = AddPartial(displaced.itemId, displaced.amount, null, notifyItemGainPopup, touchedDisplaced);
        if (addedIncoming < incomingLeft || placedDisplaced < displaced.amount)
        {
            if (placedDisplaced > 0)
                RemoveAmountFromTouchedSlots(placedDisplaced, touchedDisplaced);
            if (addedIncoming > 0)
                RemoveAmountFromTouchedSlots(addedIncoming, touchedIncoming);
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
        id = RemapObsoleteWandItemId(id);

        const string runtimeEnhancedSeparator = "__enh_";
        int marker = id.IndexOf(runtimeEnhancedSeparator, System.StringComparison.Ordinal);
        string suffix = marker > 0 ? id.Substring(marker) : string.Empty;
        string baseId = marker > 0 ? id.Substring(0, marker) : id;

        // legacy -> new (base ids)
        string remappedBaseId = baseId switch
        {
            "log" => "wood_log",
            "ability_power_pendant" => "amethyst_pendant",
            "bone_ring" => "citrine_ring",
            "crit_ring" => "diamond_ring",
            "physical_damage_pendant" => "ruby_pendant",
            "vamp_ring" => "quartz_ring",
            _ => baseId
        };

        return remappedBaseId + suffix;
    }

    /// <summary>Old per-element basic wands were consolidated into <c>basic_wand</c>.</summary>
    public static bool IsObsoleteWandItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        string normalized = itemId.Trim().ToLowerInvariant().Replace(" ", "_");
        return normalized.StartsWith("basic_fire_wand", System.StringComparison.Ordinal)
               || normalized.StartsWith("basic_ice_wand", System.StringComparison.Ordinal)
               || normalized.StartsWith("basic_lightning_wand", System.StringComparison.Ordinal);
    }

    private static string RemapObsoleteWandItemId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return id;

        if (id.StartsWith("basic_fire_wand", System.StringComparison.Ordinal)
            || id.StartsWith("basic_ice_wand", System.StringComparison.Ordinal)
            || id.StartsWith("basic_lightning_wand", System.StringComparison.Ordinal))
            return "basic_wand";

        return id;
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

        // 1) Sum amounts per item (combine stacks) — use long so over-max legacy stacks cannot wrap to ≤0.
        var totals = new Dictionary<string, long>();
        foreach (var s in filled)
        {
            if (!totals.ContainsKey(s.itemId))
                totals[s.itemId] = 0;
            totals[s.itemId] += Math.Max(0, s.amount);
        }

        // 2) Split totals into legal stacks (maxStack each), in database order.
        // Cap growth to slot capacity while splitting so a single corrupt 2e9 stack cannot OOM.
        var merged = new List<Slot>();
        var itemIds = new List<string>(totals.Keys);
        itemIds.Sort((a, b) => itemDb.GetIndex(a).CompareTo(itemDb.GetIndex(b)));

        foreach (var itemId in itemIds)
        {
            long remaining = totals[itemId];
            int maxStack = GetMaxStack(itemId);
            while (remaining > 0)
            {
                if (merged.Count >= _slots.Count)
                {
                    EnqueuePendingLootInChunks(itemId, remaining);
                    remaining = 0;
                    break;
                }

                int chunk = (int)Math.Min(maxStack, remaining);
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

        // Preserve stacks that cannot fit after re-splitting over-max legacy amounts.
        if (merged.Count > _slots.Count)
        {
            for (int i = _slots.Count; i < merged.Count; i++)
            {
                Slot overflow = merged[i];
                if (!overflow.IsEmpty)
                    PendingLootRecoveryStore.Enqueue(overflow.itemId, overflow.amount);
            }

            Debug.LogWarning(
                $"[Inventory] Sort overflow: need {merged.Count} slots but only {_slots.Count} exist — parked extras in pending loot.");
            merged.RemoveRange(_slots.Count, merged.Count - _slots.Count);
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            s.Clear();
            _slots[i] = s;
        }

        for (int i = 0; i < merged.Count; i++)
            _slots[i] = merged[i];

        NotifyInventoryChanged();
    }

    private static void EnqueuePendingLootInChunks(string itemId, long amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        long left = amount;
        while (left > 0)
        {
            int chunk = (int)Math.Min(left, int.MaxValue);
            PendingLootRecoveryStore.Enqueue(itemId, chunk);
            left -= chunk;
        }
    }
}