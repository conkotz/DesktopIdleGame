using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks inventory/storage slot indices that should show the "new from idle auto-battle loot" tint until the player hovers them.
/// Also used for quest reward delivery and optional helper-driven highlights (same yellow slot tint).
/// </summary>
public static class AutoBattleLootHighlight
{
    private static readonly HashSet<int> InventorySlots = new HashSet<int>();
    private static readonly HashSet<int> StorageSlots = new HashSet<int>();

    public static void MarkInventorySlot(int index)
    {
        if (index >= 0) InventorySlots.Add(index);
    }

    public static void MarkStorageSlot(int index)
    {
        if (index >= 0) StorageSlots.Add(index);
    }

    public static bool IsInventorySlotMarked(int index) => index >= 0 && InventorySlots.Contains(index);

    public static bool IsStorageSlotMarked(int index) => index >= 0 && StorageSlots.Contains(index);

    public static void ClearInventorySlot(int index) => InventorySlots.Remove(index);

    public static void ClearStorageSlot(int index) => StorageSlots.Remove(index);

    /// <summary>
    /// Marks every bag and storage slot that currently holds <paramref name="itemId"/> (e.g. helper points at a tutorial item).
    /// </summary>
    public static void MarkSlotsContainingItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        itemId = itemId.Trim();

        Inventory inv = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inv != null)
        {
            int n = inv.SlotCount;
            for (int i = 0; i < n; i++)
            {
                Inventory.Slot s = inv.GetSlot(i);
                if (!s.IsEmpty && string.Equals(s.itemId, itemId, System.StringComparison.Ordinal))
                    MarkInventorySlot(i);
            }
        }

        PlayerStorage st = Object.FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (st != null)
        {
            int n = st.SlotCount;
            for (int i = 0; i < n; i++)
            {
                PlayerStorage.Slot s = st.GetSlot(i);
                if (!s.IsEmpty && string.Equals(s.itemId, itemId, System.StringComparison.Ordinal))
                    MarkStorageSlot(i);
            }
        }
    }

    /// <summary>
    /// Re-applies slot background colours so new marks show without waiting for an inventory rebuild.
    /// </summary>
    public static void RefreshLootHighlightUIs()
    {
        InventorySlotUI[] inv = Object.FindObjectsByType<InventorySlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < inv.Length; i++)
        {
            if (inv[i])
                inv[i].RefreshLootHighlightVisual();
        }

        StorageSlotUI[] st = Object.FindObjectsByType<StorageSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < st.Length; i++)
        {
            if (st[i])
                st[i].RefreshLootHighlightVisual();
        }
    }
}
