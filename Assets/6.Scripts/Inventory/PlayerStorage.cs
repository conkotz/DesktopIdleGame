using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Town chest / bank storage: same slot shape as <see cref="Inventory"/>, separate grid (UI drives slot count).
/// </summary>
public class PlayerStorage : MonoBehaviour, ISaveable
{
    public const int TabCount = 5;
    public const int MainSlotsPerTab = 88;
    public const int NonMainSlotsPerTab = 40;
    /// <summary>Legacy uniform tab size (pre per-tab capacity split).</summary>
    public const int LegacyUniformSlotsPerTab = 88;
    public const int LegacyTotalSlotCount = TabCount * LegacyUniformSlotsPerTab;
    /// <summary>Main tab capacity; non-Main tabs use <see cref="NonMainSlotsPerTab"/>.</summary>
    public const int SlotsPerTab = MainSlotsPerTab;

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

    private readonly List<Slot> _slots = new List<Slot>(LegacyTotalSlotCount);
    private readonly int[] _tabDisplayOrder = { 0, 1, 2, 3, 4 };
    private readonly bool[] _tabAffinityEnabled = { false, true, true, true, true };
    private readonly int[] _tabBonusSlots = new int[TabCount];

    public event Action OnStorageChanged;
    public event Action OnTabOrderChanged;
    public event Action OnTabAffinityChanged;

    private int _batchChangeNotifyDepth;
    private bool _batchChangeNotifyPending;

    private List<int> _uiDirtySlotIndices;
    private bool _uiRequiresFullRefresh;

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

    private void MarkSlotChanged(int slotIndex)
    {
        RecordUiDirtySlot(slotIndex);
        NotifyStorageChanged();
    }

    private void MarkSlotsChanged(int slotA, int slotB)
    {
        RecordUiDirtySlot(slotA);
        RecordUiDirtySlot(slotB);
        NotifyStorageChanged();
    }

    private void MarkAllSlotsChanged()
    {
        RecordUiFullRefresh();
        NotifyStorageChanged();
    }

    private void RecordUiDirtySlot(int slotIndex)
    {
        if (slotIndex < 0 || _uiRequiresFullRefresh)
            return;

        _uiDirtySlotIndices ??= new List<int>(4);
        if (_uiDirtySlotIndices.Count >= 16)
        {
            RecordUiFullRefresh();
            return;
        }

        for (int i = 0; i < _uiDirtySlotIndices.Count; i++)
        {
            if (_uiDirtySlotIndices[i] == slotIndex)
                return;
        }

        _uiDirtySlotIndices.Add(slotIndex);
    }

    private void RecordUiFullRefresh()
    {
        _uiRequiresFullRefresh = true;
        _uiDirtySlotIndices?.Clear();
    }

    /// <summary>Consumed by <see cref="StorageGridUI"/> when coalescing slot refreshes.</summary>
    public bool TryConsumeUiRefreshHint(out IReadOnlyList<int> dirtySlots, out bool fullRefresh)
    {
        fullRefresh = _uiRequiresFullRefresh;
        dirtySlots = _uiDirtySlotIndices;
        _uiRequiresFullRefresh = false;
        _uiDirtySlotIndices = null;
        return fullRefresh || (dirtySlots != null && dirtySlots.Count > 0);
    }

    private void Awake()
    {
        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        EnsureSlotCount(ComputeTotalSlotCount());
    }

    public static int GetBaseSlotsForTab(StorageTabKind tab) =>
        tab == StorageTabKind.Main ? MainSlotsPerTab : NonMainSlotsPerTab;

    public int GetBonusSlotsForTab(StorageTabKind tab)
    {
        int i = (int)tab;
        if (i < 0 || i >= TabCount)
            return 0;
        return Mathf.Max(0, _tabBonusSlots[i]);
    }

    public int GetSlotsForTab(StorageTabKind tab) =>
        GetBaseSlotsForTab(tab) + GetBonusSlotsForTab(tab);

    public int ComputeTotalSlotCount()
    {
        int total = 0;
        for (int i = 0; i < TabCount; i++)
            total += GetSlotsForTab((StorageTabKind)i);
        return total;
    }

    public void UnlockAdditionalTabSlots(StorageTabKind tab, int amount)
    {
        if (amount <= 0)
            return;

        int i = (int)tab;
        if (i < 0 || i >= TabCount)
            return;

        _tabBonusSlots[i] += amount;
        EnsureSlotCount(ComputeTotalSlotCount());
        MarkAllSlotsChanged();
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

    public StorageTabKind GetTabForGlobalSlot(int globalIndex)
    {
        if (globalIndex < 0)
            return StorageTabKind.Main;

        int cursor = 0;
        for (int i = 0; i < TabCount; i++)
        {
            int count = GetSlotsForTab((StorageTabKind)i);
            if (globalIndex < cursor + count)
                return (StorageTabKind)i;
            cursor += count;
        }

        return StorageTabKind.Main;
    }

    public int GetTabStartIndex(StorageTabKind tab)
    {
        int start = 0;
        int target = (int)tab;
        for (int i = 0; i < target && i < TabCount; i++)
            start += GetSlotsForTab((StorageTabKind)i);
        return start;
    }

    public int GetTabEndIndexExclusive(StorageTabKind tab) =>
        GetTabStartIndex(tab) + GetSlotsForTab(tab);

    public bool SlotAcceptsItem(int globalSlot, ItemDefinition def)
    {
        if (def == null)
            return false;
        return StorageTabFilters.PassesTab(def, GetTabForGlobalSlot(globalSlot));
    }

    public int GetTabUsedSlotCount(StorageTabKind tab)
    {
        int used = 0;
        int start = GetTabStartIndex(tab);
        int end = GetTabEndIndexExclusive(tab);
        for (int i = start; i < end && i < _slots.Count; i++)
        {
            if (!_slots[i].IsEmpty)
                used++;
        }

        return used;
    }

    public IReadOnlyList<int> TabDisplayOrder => _tabDisplayOrder;

    public void SetTabDisplayOrder(int[] order)
    {
        if (order == null || order.Length != TabCount)
            return;

        for (int i = 0; i < TabCount; i++)
            _tabDisplayOrder[i] = Mathf.Clamp(order[i], 0, TabCount - 1);

        OnTabOrderChanged?.Invoke();
    }

    /// <summary>When true, auto-deposits route matching items into this tab. Main always returns false.</summary>
    public bool IsTabAffinityEnabled(StorageTabKind tab)
    {
        int i = (int)tab;
        if (tab == StorageTabKind.Main || i < 0 || i >= TabCount)
            return false;

        return _tabAffinityEnabled[i];
    }

    public void SetTabAffinityEnabled(StorageTabKind tab, bool enabled)
    {
        if (tab == StorageTabKind.Main)
            return;

        int i = (int)tab;
        if (i < 0 || i >= TabCount)
            return;

        if (_tabAffinityEnabled[i] == enabled)
            return;

        _tabAffinityEnabled[i] = enabled;
        OnTabAffinityChanged?.Invoke();
    }

    /// <summary>
    /// Moves items from Main into affinity-enabled tabs when they match that tab's filter and space exists.
    /// </summary>
    public void TryRedistributeMainTabAffinityItems()
    {
        int start = GetTabStartIndex(StorageTabKind.Main);
        int end = GetTabEndIndexExclusive(StorageTabKind.Main);
        if (start < 0 || end <= start)
            return;

        BeginBatchChanges();
        try
        {
            for (int i = start; i < end && i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty)
                    continue;

                ItemDefinition def = GetItemDef(slot.itemId);
                StorageTabKind inferred = StorageTabFilters.InferTabForItem(def);
                if (inferred == StorageTabKind.Main || !IsTabAffinityEnabled(inferred))
                    continue;

                TryMoveFromStorageSlotToTab(i, inferred, slot.amount);
            }
        }
        finally
        {
            EndBatchChanges();
        }
    }

    /// <summary>Tab used for automatic deposits (inventory deposit, unequip-to-storage, etc.).</summary>
    public StorageTabKind ResolveAutoDepositTab(ItemDefinition def)
    {
        StorageTabKind inferred = StorageTabFilters.InferTabForItem(def);
        if (inferred == StorageTabKind.Main)
            return StorageTabKind.Main;

        return IsTabAffinityEnabled(inferred) ? inferred : StorageTabKind.Main;
    }

    public void EnsureSlotCount(int count)
    {
        int minimum = ComputeTotalSlotCount();
        count = Mathf.Max(minimum, Mathf.Max(1, count));

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

    /// <summary>First global slot index in <paramref name="tab"/> holding <paramref name="itemId"/>, or -1.</summary>
    public int FindFirstSlotWithItemInTab(string itemId, StorageTabKind tab)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return -1;

        string target = itemId.Trim();
        int start = GetTabStartIndex(tab);
        int end = GetTabEndIndexExclusive(tab);
        for (int i = start; i < end && i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty)
                continue;

            if (string.Equals(s.itemId, target, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
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

        MarkAllSlotsChanged();
        return amount == 0;
    }

    public void ReplaceSlot(int slotIndex, Slot newSlot)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return;
        _slots[slotIndex] = newSlot;
        MarkSlotChanged(slotIndex);
    }

    public bool SwapSlots(int slotA, int slotB)
    {
        if (slotA == slotB) return false;
        if (slotA < 0 || slotB < 0) return false;
        if (slotA >= _slots.Count || slotB >= _slots.Count) return false;

        var a = _slots[slotA];
        var b = _slots[slotB];
        if (!a.IsEmpty && !SlotAcceptsItem(slotB, GetItemDef(a.itemId)))
            return false;
        if (!b.IsEmpty && !SlotAcceptsItem(slotA, GetItemDef(b.itemId)))
            return false;

        (_slots[slotA], _slots[slotB]) = (b, a);
        MarkSlotsChanged(slotA, slotB);
        return true;
    }

    public void SortByDatabaseOrder()
    {
        for (int t = 0; t < TabCount; t++)
            SortTabByDatabaseOrder((StorageTabKind)t);
    }

    public void SortTabByDatabaseOrder(StorageTabKind tab)
    {
        SortSlotRangeByDatabaseOrder(GetTabStartIndex(tab), GetSlotsForTab(tab));
    }

    private void SortSlotRangeByDatabaseOrder(int rangeStart, int rangeLength)
    {
        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        if (itemDb == null)
        {
            Debug.LogWarning("[PlayerStorage] Cannot sort — ItemDatabase missing.");
            return;
        }

        int rangeEnd = Mathf.Min(rangeStart + rangeLength, _slots.Count);
        if (rangeStart < 0 || rangeStart >= _slots.Count)
            return;

        var filled = new List<Slot>();
        for (int i = rangeStart; i < rangeEnd; i++)
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

        int capacity = rangeEnd - rangeStart;
        if (merged.Count > capacity)
        {
            for (int i = capacity; i < merged.Count; i++)
            {
                Slot overflow = merged[i];
                if (!overflow.IsEmpty)
                    PendingLootRecoveryStore.Enqueue(overflow.itemId, overflow.amount);
            }

            Debug.LogWarning(
                $"[PlayerStorage] Tab sort overflow: need {merged.Count} slots but range only has {capacity} — parked extras in pending loot.");
            merged.RemoveRange(capacity, merged.Count - capacity);
        }

        for (int i = rangeStart; i < rangeEnd; i++)
        {
            var s = _slots[i];
            s.Clear();
            _slots[i] = s;
        }

        for (int i = 0; i < merged.Count; i++)
            _slots[rangeStart + i] = merged[i];

        MarkAllSlotsChanged();
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
        MarkSlotChanged(slotIndex);
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

        ItemDefinition fromDef = GetItemDef(from.itemId);
        if (!SlotAcceptsItem(toSlot, fromDef))
            return 0;

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
            MarkSlotsChanged(fromSlot, toSlot);
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
            MarkSlotsChanged(fromSlot, toSlot);
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

        ItemDefinition def = GetItemDef(from.itemId);
        if (!SlotAcceptsItem(toStorageSlot, def))
            return 0;

        var to = _slots[toStorageSlot];

        inv.BeginBatchChanges();
        BeginBatchChanges();
        try
        {
            if (to.IsEmpty)
            {
                // Match merge path / TryDepositAmountToTab: never write over-max into an empty slot.
                int maxStack = GetMaxStack(from.itemId, maxStackOverride);
                int move = Mathf.Min(amount, maxStack);
                if (move <= 0) return 0;

                to.itemId = from.itemId;
                to.amount = move;
                _slots[toStorageSlot] = to;
                inv.RemoveAmountAtSlot(fromInvSlot, move);
                MarkSlotChanged(toStorageSlot);
                return move;
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
                MarkSlotChanged(toStorageSlot);
                return add;
            }

            return 0;
        }
        finally
        {
            inv.EndBatchChanges();
            EndBatchChanges();
        }
    }

    public int TryMoveFromInventoryToTab(Inventory inv, int fromInvSlot, StorageTabKind tab, int amount)
    {
        if (inv == null || amount <= 0)
            return 0;

        var from = inv.GetSlot(fromInvSlot);
        if (from.IsEmpty)
            return 0;

        ItemDefinition def = GetItemDef(from.itemId);
        if (!StorageTabFilters.PassesTab(def, tab))
            return 0;

        int movedTotal = 0;
        int start = GetTabStartIndex(tab);
        int end = GetTabEndIndexExclusive(tab);

        while (amount > 0)
        {
            bool progressed = false;
            for (int i = start; i < end && amount > 0; i++)
            {
                int moved = TryMoveFromInventoryToStorage(inv, fromInvSlot, i, amount, null);
                if (moved <= 0)
                    continue;

                movedTotal += moved;
                amount -= moved;
                progressed = true;
                from = inv.GetSlot(fromInvSlot);
                if (from.IsEmpty)
                    break;
                amount = from.amount;
            }

            if (!progressed)
                break;
        }

        return movedTotal;
    }

    /// <summary>
    /// Deposits into the preferred affinity tab first; any remainder goes to Main when the affinity tab is full.
    /// </summary>
    public int TryMoveFromInventoryToTabWithMainFallback(Inventory inv, int fromInvSlot, StorageTabKind preferredTab, int amount)
    {
        int moved = TryMoveFromInventoryToTab(inv, fromInvSlot, preferredTab, amount);
        if (preferredTab == StorageTabKind.Main || inv == null)
            return moved;

        var from = inv.GetSlot(fromInvSlot);
        if (!from.IsEmpty)
            moved += TryMoveFromInventoryToTab(inv, fromInvSlot, StorageTabKind.Main, from.amount);

        return moved;
    }

    public int TryMoveFromStorageSlotToTab(int fromGlobalSlot, StorageTabKind tab, int amount)
    {
        if (fromGlobalSlot < 0 || fromGlobalSlot >= _slots.Count || amount <= 0)
            return 0;

        var from = _slots[fromGlobalSlot];
        if (from.IsEmpty)
            return 0;

        ItemDefinition def = GetItemDef(from.itemId);
        if (!StorageTabFilters.PassesTab(def, tab))
            return 0;

        int movedTotal = 0;
        int start = GetTabStartIndex(tab);
        int end = GetTabEndIndexExclusive(tab);

        while (amount > 0)
        {
            bool progressed = false;
            for (int i = start; i < end && amount > 0; i++)
            {
                if (i == fromGlobalSlot)
                    continue;

                int moved = MoveAmount(fromGlobalSlot, i, amount);
                if (moved <= 0)
                    continue;

                movedTotal += moved;
                amount -= moved;
                progressed = true;
                from = _slots[fromGlobalSlot];
                if (from.IsEmpty)
                    break;
                amount = from.amount;
            }

            if (!progressed)
                break;
        }

        return movedTotal;
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

        inv.BeginBatchChanges();
        BeginBatchChanges();
        try
        {
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
        finally
        {
            inv.EndBatchChanges();
            EndBatchChanges();
        }
    }

    public bool SwapInventorySlotWithStorage(Inventory inv, int invSlot, int storageSlot)
    {
        if (inv == null) return false;
        if (storageSlot < 0 || storageSlot >= _slots.Count) return false;

        var a = inv.GetSlot(invSlot);
        var b = _slots[storageSlot];

        if (!a.IsEmpty && !SlotAcceptsItem(storageSlot, GetItemDef(a.itemId)))
            return false;

        inv.BeginBatchChanges();
        BeginBatchChanges();
        try
        {
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
            MarkSlotChanged(storageSlot);
            return true;
        }
        finally
        {
            inv.EndBatchChanges();
            EndBatchChanges();
        }
    }

    /// <summary>
    /// Places items not coming from an inventory slot (e.g. unequipped gear) into storage,
    /// merging into matching stacks then filling empty slots up to max stack.
    /// </summary>
    /// <param name="touchedSlotIndices">If non-null, each storage slot index that received items is appended (merge or new stack).</param>
    public int TryDepositAmountFromExternal(string itemId, int amount, IList<int> touchedSlotIndices = null)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return 0;

        ItemDefinition def = GetItemDef(itemId);
        StorageTabKind tab = ResolveAutoDepositTab(def);
        int moved = TryDepositAmountToTab(itemId, amount, tab, touchedSlotIndices);
        if (tab != StorageTabKind.Main && moved < amount)
            moved += TryDepositAmountToTab(itemId, amount - moved, StorageTabKind.Main, touchedSlotIndices);

        return moved;
    }

    public int TryDepositAmountToTab(string itemId, int amount, StorageTabKind tab, IList<int> touchedSlotIndices = null)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return 0;

        ItemDefinition def = GetItemDef(itemId);
        if (!StorageTabFilters.PassesTab(def, tab))
            tab = StorageTabKind.Main;

        int movedTotal = 0;
        int remaining = amount;
        int start = GetTabStartIndex(tab);
        int end = GetTabEndIndexExclusive(tab);

        while (remaining > 0)
        {
            bool progressed = false;

            for (int i = start; i < end && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty || s.itemId != itemId)
                    continue;

                int maxStack = GetMaxStack(itemId);
                int space = maxStack - s.amount;
                if (space <= 0)
                    continue;

                int add = Mathf.Min(space, remaining);
                s.amount += add;
                remaining -= add;
                movedTotal += add;
                _slots[i] = s;
                touchedSlotIndices?.Add(i);
                progressed = true;
            }

            if (remaining <= 0)
                break;

            for (int i = start; i < end && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty)
                    continue;

                int maxStack = GetMaxStack(itemId);
                int chunk = Mathf.Min(remaining, maxStack);
                _slots[i] = new Slot { itemId = itemId, amount = chunk };
                remaining -= chunk;
                movedTotal += chunk;
                touchedSlotIndices?.Add(i);
                progressed = true;
                break;
            }

            if (!progressed)
                break;
        }

        if (movedTotal > 0)
            MarkAllSlotsChanged();

        return movedTotal;
    }

    public int GetReceivableAmountFromExternal(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return 0;

        ItemDefinition def = GetItemDef(itemId);
        StorageTabKind tab = ResolveAutoDepositTab(def);
        int inPreferred = GetReceivableAmountInTab(itemId, amount, tab);
        if (tab == StorageTabKind.Main || inPreferred >= amount)
            return inPreferred;

        // Mirror TryDepositAmountFromExternal: overflow from affinity tabs spills into Main.
        int left = amount - inPreferred;
        return inPreferred + GetReceivableAmountInTab(itemId, left, StorageTabKind.Main);
    }

    public int GetReceivableAmountInTab(string itemId, int amount, StorageTabKind tab)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return 0;

        ItemDefinition def = GetItemDef(itemId);
        if (!StorageTabFilters.PassesTab(def, tab))
            return 0;

        int remaining = amount;
        int total = 0;
        int start = GetTabStartIndex(tab);
        int end = GetTabEndIndexExclusive(tab);

        while (remaining > 0)
        {
            bool progressed = false;

            for (int i = start; i < end && remaining > 0; i++)
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

            for (int i = start; i < end && remaining > 0; i++)
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

    /// <summary>
    /// Undoes a prior <see cref="TryDepositAmountFromExternal"/> / <see cref="TryDepositAmountToTab"/>
    /// by removing only from the slots it reported as touched. Prefer this over
    /// <see cref="RemoveItemAmountAcrossSlots"/> when rolling back a failed transfer.
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

    /// <summary>Deposit as much as possible from one inventory slot (double-click from inventory).</summary>
    public int TryDepositAllFromInventorySlot(Inventory inv, int invSlot)
    {
        if (inv == null) return 0;

        inv.BeginBatchChanges();
        BeginBatchChanges();
        try
        {
            var from = inv.GetSlot(invSlot);
            if (from.IsEmpty)
                return 0;

            ItemDefinition def = GetItemDef(from.itemId);
            StorageTabKind tab = ResolveAutoDepositTab(def);
            return TryMoveFromInventoryToTabWithMainFallback(inv, invSlot, tab, from.amount);
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
        if (data.storageTabOrder == null)
            data.storageTabOrder = new List<int>(TabCount);
        else
            data.storageTabOrder.Clear();
        for (int i = 0; i < TabCount; i++)
            data.storageTabOrder.Add(_tabDisplayOrder[i]);

        if (data.storageTabAffinity == null)
            data.storageTabAffinity = new List<bool>(TabCount);
        else
            data.storageTabAffinity.Clear();
        for (int i = 0; i < TabCount; i++)
            data.storageTabAffinity.Add(_tabAffinityEnabled[i]);

        if (data.storageTabBonusSlots == null)
            data.storageTabBonusSlots = new List<int>(TabCount);
        else
            data.storageTabBonusSlots.Clear();
        for (int i = 0; i < TabCount; i++)
            data.storageTabBonusSlots.Add(_tabBonusSlots[i]);

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
            itemDb = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
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

        for (int i = 0; i < TabCount; i++)
            _tabBonusSlots[i] = 0;

        if (data.storageTabBonusSlots != null && data.storageTabBonusSlots.Count == TabCount)
        {
            for (int i = 0; i < TabCount; i++)
                _tabBonusSlots[i] = Mathf.Max(0, data.storageTabBonusSlots[i]);
        }

        int savedCount = data.storageSlotCount > 0 ? data.storageSlotCount : ComputeTotalSlotCount();
        savedCount = Mathf.Max(1, savedCount);
        bool legacyUniformLayout = savedCount >= LegacyTotalSlotCount;
        EnsureSlotCount(Mathf.Max(savedCount, ComputeTotalSlotCount()));

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
                    if (!Inventory.IsObsoleteWandItemId(d.itemId))
                    {
                        Debug.LogWarning(
                            $"[PlayerStorage] Unknown itemId '{d.itemId}' remapped to '{id}' but still not found. Clearing storage slot {i}.");
                    }

                    continue;
                }

                if (!canValidateDefs)
                    Debug.LogWarning($"[PlayerStorage] ItemDatabase unavailable during LoadFrom; preserving storage slot {i} item '{id}' without validation.");

                _slots[i] = new Slot { itemId = id, amount = d.amount };
            }
        }

        if (data.storageTabOrder != null && data.storageTabOrder.Count == TabCount)
        {
            for (int i = 0; i < TabCount; i++)
                _tabDisplayOrder[i] = Mathf.Clamp(data.storageTabOrder[i], 0, TabCount - 1);
        }

        if (data.storageTabAffinity != null && data.storageTabAffinity.Count == TabCount)
        {
            for (int i = 0; i < TabCount; i++)
                _tabAffinityEnabled[i] = data.storageTabAffinity[i];
        }
        else
        {
            _tabAffinityEnabled[0] = false;
            for (int i = 1; i < TabCount; i++)
                _tabAffinityEnabled[i] = true;
        }

        if (legacyUniformLayout)
            MigrateLegacyUniformTabStorage();
        else if (savedCount < ComputeTotalSlotCount())
            MigrateLegacyFlatStorage(savedCount);

        MarkAllSlotsChanged();
    }

    private void MigrateLegacyFlatStorage(int previousCount)
    {
        if (previousCount <= MainSlotsPerTab)
            return;

        var overflow = new List<Slot>();
        for (int i = MainSlotsPerTab; i < previousCount && i < _slots.Count; i++)
        {
            if (!_slots[i].IsEmpty)
                overflow.Add(_slots[i]);
            _slots[i].Clear();
        }

        RedepositOverflowStacks(overflow);
    }

    private void MigrateLegacyUniformTabStorage()
    {
        var preserved = new List<(int globalIndex, Slot slot)>();
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsEmpty)
                preserved.Add((i, _slots[i]));
            _slots[i].Clear();
        }

        int targetCount = ComputeTotalSlotCount();
        if (_slots.Count != targetCount)
        {
            _slots.Clear();
            while (_slots.Count < targetCount)
                _slots.Add(new Slot());
        }

        var overflow = new List<Slot>();
        for (int p = 0; p < preserved.Count; p++)
        {
            (int globalIndex, Slot slot) = preserved[p];
            StorageTabKind oldTab = (StorageTabKind)(globalIndex / LegacyUniformSlotsPerTab);
            int oldTabOffset = globalIndex % LegacyUniformSlotsPerTab;
            if (oldTabOffset < GetSlotsForTab(oldTab))
            {
                int newIndex = GetTabStartIndex(oldTab) + oldTabOffset;
                if (newIndex >= 0 && newIndex < _slots.Count && _slots[newIndex].IsEmpty)
                    _slots[newIndex] = slot;
                else
                    overflow.Add(slot);
            }
            else
            {
                overflow.Add(slot);
            }
        }

        RedepositOverflowStacks(overflow);
    }

    private void RedepositOverflowStacks(List<Slot> overflow)
    {
        foreach (var stack in overflow)
        {
            if (stack.IsEmpty)
                continue;

            ItemDefinition def = GetItemDef(stack.itemId);
            StorageTabKind tab = ResolveAutoDepositTab(def);
            int left = stack.amount;
            left -= TryDepositAmountToTab(stack.itemId, left, tab);
            if (left > 0 && tab != StorageTabKind.Main)
                left -= TryDepositAmountToTab(stack.itemId, left, StorageTabKind.Main);

            // Legacy layout migration can shrink total capacity — never silently discard overflow.
            if (left > 0)
            {
                Inventory inv = Inventory.ResolvePlayer();
                if (inv != null)
                    left -= inv.AddPartial(stack.itemId, left, notifyItemGainPopup: false);

                if (left > 0)
                {
                    PendingLootRecoveryStore.Enqueue(stack.itemId, left);
                    GameLog.Add(
                        "Storage layout migration could not fit all items — held the overflow until you free space.",
                        GameLog.CannotMessageColor);
                    SaveManager.Instance?.NotifyInventoryChangedDebounced();
                }
            }
        }
    }
}
