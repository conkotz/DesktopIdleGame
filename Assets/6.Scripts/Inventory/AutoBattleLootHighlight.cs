using System.Collections.Generic;

/// <summary>
/// Tracks inventory/storage slot indices that should show the "new from idle auto-battle loot" tint until the player hovers them.
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
}
