using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds right-click menu entries and executes inventory/storage item actions.</summary>
public static class InventoryItemContextMenuBuilder
{
    public static List<InventoryItemContextMenuEntry> BuildForInventorySlot(InventorySlotUI slot)
    {
        var entries = new List<InventoryItemContextMenuEntry>(6);
        if (!slot || !slot.HasItemContext)
            return entries;

        ItemDefinition def = slot.ContextDefinition;
        if (!def)
            return entries;

        if (CanEquipFromInventory(slot, def))
            entries.Add(new InventoryItemContextMenuEntry("Equip", slot.PerformEquipAction));

        if (def.IsOpenable)
            entries.Add(new InventoryItemContextMenuEntry("Open", slot.PerformOpenAction));

        if (CanEat(def))
            entries.Add(new InventoryItemContextMenuEntry("Eat", slot.PerformEatAction));

        entries.Add(new InventoryItemContextMenuEntry("Drop", slot.PerformDropAction));

        if (CanShowAdditionalStats(slot.ContextItemId, def))
        {
            bool enabled = ItemTooltipHighlightState.IsEnabled(slot.ContextItemId);
            string label = enabled ? "Hide Additional Stats" : "Show Additional Stats";
            entries.Add(new InventoryItemContextMenuEntry(label, slot.ToggleAdditionalStatsHighlight));
        }

        return entries;
    }

    public static List<InventoryItemContextMenuEntry> BuildForStorageSlot(StorageSlotUI slot)
    {
        var entries = new List<InventoryItemContextMenuEntry>(6);
        if (!slot || !slot.HasItemContext)
            return entries;

        ItemDefinition def = slot.ContextDefinition;
        if (!def)
            return entries;

        if (CanEquipFromStorage(slot, def))
            entries.Add(new InventoryItemContextMenuEntry("Equip", slot.PerformEquipAction));

        if (def.IsOpenable)
            entries.Add(new InventoryItemContextMenuEntry("Open", slot.PerformOpenAction));

        if (CanEat(def))
            entries.Add(new InventoryItemContextMenuEntry("Eat", slot.PerformEatAction));

        entries.Add(new InventoryItemContextMenuEntry("Drop", slot.PerformDropAction));

        if (CanShowAdditionalStats(slot.ContextItemId, def))
        {
            bool enabled = ItemTooltipHighlightState.IsEnabled(slot.ContextItemId);
            string label = enabled ? "Hide Additional Stats" : "Show Additional Stats";
            entries.Add(new InventoryItemContextMenuEntry(label, slot.ToggleAdditionalStatsHighlight));
        }

        return entries;
    }

    private static bool CanEquipFromInventory(InventorySlotUI slot, ItemDefinition def)
    {
        if (def.IsEquippable && def.equipSlot != EquipSlot.None)
            return true;

        return def.IsFood || def.IsPotion;
    }

    private static bool CanEquipFromStorage(StorageSlotUI slot, ItemDefinition def)
    {
        if (def.IsEquippable && def.equipSlot != EquipSlot.None)
            return slot.CanWithdrawToInventory();

        return (def.IsFood || def.IsPotion) && slot.CanWithdrawToInventory();
    }

    private static bool CanEat(ItemDefinition def) =>
        def.IsConsumable && (def.IsFood || def.IsPotion) && def.ConsumeOnUse;

    private static bool CanShowAdditionalStats(string itemId, ItemDefinition def)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !def)
            return false;

        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!inventory)
            return false;

        ItemDefinition baseline = ItemTooltipStatHighlight.ResolveBaseline(inventory.GetItemDatabase(), itemId);
        return baseline != null;
    }
}
