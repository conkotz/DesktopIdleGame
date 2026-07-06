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

        if (MerchantClick.MerchantModeOpen)
        {
            bool canSell = slot.CanSellToActiveMerchant(out string cantSellLabel);
            if (canSell)
            {
                entries.Add(new ContextMenuEntry("Sell 1", slot.PerformSellOneAction));
                entries.Add(new ContextMenuEntry("Sell All", slot.PerformSellAllAction));
            }
            else
                entries.Add(new ContextMenuEntry(cantSellLabel, null, disabled: true));
            if (slot.CanIdentifyStats())
                entries.Add(new ContextMenuEntry("Identify Stats", slot.PerformIdentifyStatsAction));
            entries.Add(new ContextMenuEntry("Lookup", slot.PerformLookupAction));
            AddDropEntry(entries, slot.PerformDropAction);
            return entries;
        }

        if (StorageUI.IsOpen)
            entries.Add(new ContextMenuEntry("Store", slot.PerformStoreAction));

        if (CanEquipFromInventory(slot, def))
            entries.Add(new ContextMenuEntry("Equip", slot.PerformEquipAction));

        if (CanUpgradeFromInventory(def))
            entries.Add(new ContextMenuEntry("Enhance", slot.PerformUpgradeAction));

        if (MapEnhancementService.TryGetSourceMapNodeId(slot.ContextItemId, out _))
            entries.Add(new ContextMenuEntry("Equip on map", slot.PerformEquipOnMapAction));

        if (def.IsOpenable)
        {
            entries.Add(new ContextMenuEntry("Open", slot.PerformOpenAction));
            if (slot.CanShowOpenAllAction())
                entries.Add(new ContextMenuEntry("Open All", slot.PerformOpenAllAction));
        }

        if (CanEat(def))
            entries.Add(new ContextMenuEntry("Eat", slot.PerformEatAction));

        if (slot.CanIdentifyStats())
            entries.Add(new ContextMenuEntry("Identify Stats", slot.PerformIdentifyStatsAction));

        entries.Add(new ContextMenuEntry("Lookup", slot.PerformLookupAction));

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

        if (slot.CanIdentifyStats())
            entries.Add(new ContextMenuEntry("Identify Stats", slot.PerformIdentifyStatsAction));

        entries.Add(new ContextMenuEntry("Lookup", slot.PerformLookupAction));

        AddDropEntry(entries, slot.PerformDropAction);
        return entries;
    }

    public static List<ContextMenuEntry> BuildForEquipmentSlot(EquipmentSlotUI slot)
    {
        var entries = new List<ContextMenuEntry>(4);
        if (!slot || !slot.HasItemContext)
            return entries;

        entries.Add(new ContextMenuEntry("Unequip", slot.PerformUnequipAction));

        if (slot.CanIdentifyStats())
            entries.Add(new ContextMenuEntry("Identify Stats", slot.PerformIdentifyStatsAction));

        if (CanUpgradeFromEquipment(slot))
            entries.Add(new ContextMenuEntry("Enhance", slot.PerformUpgradeAction));

        AddDropEntry(entries, slot.PerformDropAction);
        return entries;
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

    private static bool CanUpgradeFromInventory(ItemDefinition def) => CanUpgradeGear(def);

    private static bool CanUpgradeFromEquipment(EquipmentSlotUI slot)
    {
        ItemDefinition def = slot != null ? slot.ContextDefinition : null;
        if (!CanUpgradeGear(def))
            return false;

        return !def.IsCombatSupport;
    }

    private static bool CanUpgradeGear(ItemDefinition def)
    {
        if (!def || !def.HasUpgradeSlots)
            return false;

        return def.IsWeapon || def.IsArmour || def.IsTool;
    }

}
