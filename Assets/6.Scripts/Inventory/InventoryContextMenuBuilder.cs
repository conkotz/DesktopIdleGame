using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds context menu entries for inventory, storage, and equipment slots.</summary>
public static class InventoryContextMenuBuilder
{
    public static List<ContextMenuEntry> BuildForInventorySlot(InventorySlotUI slot)
    {
        var entries = new List<ContextMenuEntry>(8);
        if (!slot || !slot.HasItemContext)
            return entries;

        ItemDefinition def = slot.ContextDefinition;
        if (!def)
            return entries;

        if (StorageUI.IsOpen)
            entries.Add(new ContextMenuEntry("Store", slot.PerformStoreAction));

        if (MerchantClick.MerchantModeOpen)
            entries.Add(new ContextMenuEntry("Sell", slot.PerformSellAction));

        if (CanEquipFromInventory(slot, def))
            entries.Add(new ContextMenuEntry("Equip", slot.PerformEquipAction));

        if (MapEnhancementService.TryGetSourceMapNodeId(slot.ContextItemId, out _))
            entries.Add(new ContextMenuEntry("Equip on map", slot.PerformEquipOnMapAction));

        if (def.IsOpenable)
            entries.Add(new ContextMenuEntry("Open", slot.PerformOpenAction));

        if (CanEat(def))
            entries.Add(new ContextMenuEntry("Eat", slot.PerformEatAction));

        AddAdditionalStatsEntry(entries, slot.ContextItemId, slot.ToggleAdditionalStatsHighlight);
        AddDropEntry(entries, slot.PerformDropAction);
        return entries;
    }

    public static List<ContextMenuEntry> BuildForStorageSlot(StorageSlotUI slot)
    {
        var entries = new List<ContextMenuEntry>(8);
        if (!slot || !slot.HasItemContext)
            return entries;

        ItemDefinition def = slot.ContextDefinition;
        if (!def)
            return entries;

        if (CanEquipFromStorage(slot, def))
            entries.Add(new ContextMenuEntry("Equip", slot.PerformEquipAction));

        if (def.IsOpenable)
            entries.Add(new ContextMenuEntry("Open", slot.PerformOpenAction));

        if (CanEat(def))
            entries.Add(new ContextMenuEntry("Eat", slot.PerformEatAction));

        AddAdditionalStatsEntry(entries, slot.ContextItemId, slot.ToggleAdditionalStatsHighlight);
        AddDropEntry(entries, slot.PerformDropAction);
        return entries;
    }

    public static List<ContextMenuEntry> BuildForEquipmentSlot(EquipmentSlotUI slot)
    {
        var entries = new List<ContextMenuEntry>(4);
        if (!slot || !slot.HasItemContext)
            return entries;

        entries.Add(new ContextMenuEntry("Unequip", slot.PerformUnequipAction));
        AddAdditionalStatsEntry(entries, slot.ContextItemId, slot.ToggleAdditionalStatsHighlight);
        AddDropEntry(entries, slot.PerformDropAction);
        return entries;
    }

    private static void AddAdditionalStatsEntry(
        List<ContextMenuEntry> entries,
        string itemId,
        System.Action toggleHighlight)
    {
        if (!CanShowAdditionalStats(itemId))
            return;

        bool enabled = ItemTooltipHighlightState.IsEnabled(itemId);
        string label = enabled ? "Hide Advanced Stats" : "Advanced Stats";
        entries.Add(new ContextMenuEntry(label, toggleHighlight));
    }

    private static void AddDropEntry(List<ContextMenuEntry> entries, System.Action dropAction) =>
        entries.Add(new ContextMenuEntry("Drop", dropAction));

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

    private static bool CanShowAdditionalStats(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!inventory)
            return false;

        ItemDefinition baseline = ItemTooltipStatHighlight.ResolveBaseline(inventory.GetItemDatabase(), itemId);
        return baseline != null;
    }
}
