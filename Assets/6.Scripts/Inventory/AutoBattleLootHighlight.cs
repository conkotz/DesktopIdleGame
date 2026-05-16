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
    private static readonly List<InventorySlotUI> RegisteredInventorySlotUIs = new();
    private static readonly List<StorageSlotUI> RegisteredStorageSlotUIs = new();

    public static void RegisterInventorySlotUi(InventorySlotUI slotUi)
    {
        if (!slotUi || RegisteredInventorySlotUIs.Contains(slotUi))
            return;
        RegisteredInventorySlotUIs.Add(slotUi);
    }

    public static void UnregisterInventorySlotUi(InventorySlotUI slotUi)
    {
        if (!slotUi)
            return;
        RegisteredInventorySlotUIs.Remove(slotUi);
    }

    public static void RegisterStorageSlotUi(StorageSlotUI slotUi)
    {
        if (!slotUi || RegisteredStorageSlotUIs.Contains(slotUi))
            return;
        RegisteredStorageSlotUIs.Add(slotUi);
    }

    public static void UnregisterStorageSlotUi(StorageSlotUI slotUi)
    {
        if (!slotUi)
            return;
        RegisteredStorageSlotUIs.Remove(slotUi);
    }

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
        if (InventorySlots.Count > 0)
            RefreshMarkedInventorySlots();

        if (StorageSlots.Count > 0)
            RefreshMarkedStorageSlots();
    }

    private static void RefreshMarkedInventorySlots()
    {
        for (int m = 0; m < RegisteredInventorySlotUIs.Count; m++)
        {
            InventorySlotUI ui = RegisteredInventorySlotUIs[m];
            if (!ui)
            {
                RegisteredInventorySlotUIs.RemoveAt(m);
                m--;
                continue;
            }

            if (!IsInventorySlotMarked(ui.SlotIndex))
                continue;

            ui.RefreshLootHighlightVisual();
        }
    }

    private static void RefreshMarkedStorageSlots()
    {
        for (int m = 0; m < RegisteredStorageSlotUIs.Count; m++)
        {
            StorageSlotUI ui = RegisteredStorageSlotUIs[m];
            if (!ui)
            {
                RegisteredStorageSlotUIs.RemoveAt(m);
                m--;
                continue;
            }

            if (!IsStorageSlotMarked(ui.SlotIndex))
                continue;

            ui.RefreshLootHighlightVisual();
        }
    }
}
