using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct EnhancementOptionPayment
{
    public EnhancementPaymentKind Kind { get; }
    public int ScrollSlotIndex { get; }
    public bool ScrollFromStorage { get; }
    public IReadOnlyList<GearUpgradeMaterialRequirement> MaterialRequirements { get; }

    public EnhancementOptionPayment(
        EnhancementPaymentKind kind,
        int scrollSlotIndex = -1,
        IReadOnlyList<GearUpgradeMaterialRequirement> materialRequirements = null,
        bool scrollFromStorage = false)
    {
        Kind = kind;
        ScrollSlotIndex = scrollSlotIndex;
        ScrollFromStorage = scrollFromStorage;
        MaterialRequirements = materialRequirements ?? Array.Empty<GearUpgradeMaterialRequirement>();
    }

    public static EnhancementOptionPayment None => new(EnhancementPaymentKind.None);

    /// <summary>
    /// Picks what will be consumed on apply. An owned applicable scroll always beats materials.
    /// </summary>
    public static bool RequiresScrollOnlyPayment(EnhancementOptionEntry option) =>
        option != null && option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction;

    public static EnhancementOptionPayment Resolve(
        Inventory inventory,
        EnhancementOptionEntry option,
        ItemDefinition gear) =>
        Resolve(inventory, null, option, gear);

    public static EnhancementOptionPayment Resolve(
        Inventory inventory,
        PlayerStorage storage,
        EnhancementOptionEntry option,
        ItemDefinition gear)
    {
        if (option == null)
            return None;

        if (HasScrollPayment(inventory, storage, option))
        {
            int invSlot = FindItemSlot(inventory, option.linkedScrollItemId);
            if (invSlot >= 0)
                return new EnhancementOptionPayment(EnhancementPaymentKind.Scroll, invSlot);

            int storageSlot = FindScrollInStorageEnhanceTab(storage, option.linkedScrollItemId);
            if (storageSlot >= 0)
                return new EnhancementOptionPayment(EnhancementPaymentKind.Scroll, storageSlot, scrollFromStorage: true);
        }

        if (inventory == null || RequiresScrollOnlyPayment(option) || gear == null)
            return None;

        IReadOnlyList<GearUpgradeMaterialRequirement> requirements =
            GearUpgradeMaterialResolver.ResolveMaterialRequirements(gear, option.tier, gear.SuccessfulEnhancements, option);
        if (HasMaterialRequirements(inventory, requirements))
            return new EnhancementOptionPayment(EnhancementPaymentKind.Materials, materialRequirements: requirements);

        return None;
    }

    public static bool HasScrollPayment(Inventory inventory, EnhancementOptionEntry option) =>
        HasScrollPayment(inventory, null, option);

    public static bool HasScrollPayment(Inventory inventory, PlayerStorage storage, EnhancementOptionEntry option)
    {
        if (option == null || string.IsNullOrWhiteSpace(option.linkedScrollItemId))
            return false;

        return FindItemSlot(inventory, option.linkedScrollItemId) >= 0
            || FindScrollInStorageEnhanceTab(storage, option.linkedScrollItemId) >= 0;
    }

    public static bool HasMaterialPayment(Inventory inventory, EnhancementOptionEntry option, ItemDefinition gear)
    {
        if (inventory == null || option == null || gear == null || RequiresScrollOnlyPayment(option))
            return false;

        IReadOnlyList<GearUpgradeMaterialRequirement> requirements =
            GearUpgradeMaterialResolver.ResolveMaterialRequirements(gear, option.tier, gear.SuccessfulEnhancements, option);
        return HasMaterialRequirements(inventory, requirements);
    }

    public static bool HasAnyPayment(Inventory inventory, EnhancementOptionEntry option, ItemDefinition gear) =>
        HasAnyPayment(inventory, null, option, gear);

    public static bool HasAnyPayment(
        Inventory inventory,
        PlayerStorage storage,
        EnhancementOptionEntry option,
        ItemDefinition gear) =>
        HasScrollPayment(inventory, storage, option) || HasMaterialPayment(inventory, option, gear);

    public static string FormatCostLabel(EnhancementOptionPayment payment, ItemDatabase db)
    {
        if (payment.Kind == EnhancementPaymentKind.Scroll)
            return "Scroll";

        if (payment.Kind == EnhancementPaymentKind.Materials)
            return FormatMaterialRequirements(payment.MaterialRequirements, db);

        return "Unavailable";
    }

    private static bool HasMaterialRequirements(
        Inventory inventory,
        IReadOnlyList<GearUpgradeMaterialRequirement> requirements)
    {
        if (inventory == null || requirements == null || requirements.Count == 0)
            return false;

        for (int i = 0; i < requirements.Count; i++)
        {
            GearUpgradeMaterialRequirement req = requirements[i];
            if (string.IsNullOrWhiteSpace(req.ItemId) || req.Amount <= 0)
                return false;

            if (inventory.CountItem(req.ItemId) < req.Amount)
                return false;
        }

        return true;
    }

    private static string FormatMaterialRequirements(
        IReadOnlyList<GearUpgradeMaterialRequirement> requirements,
        ItemDatabase db)
    {
        if (requirements == null || requirements.Count == 0)
            return "Unavailable";

        var parts = new List<string>(requirements.Count);
        for (int i = 0; i < requirements.Count; i++)
        {
            GearUpgradeMaterialRequirement req = requirements[i];
            ItemDefinition mat = db != null ? db.Get(req.ItemId) : null;
            string name = mat != null && !string.IsNullOrWhiteSpace(mat.displayName)
                ? mat.displayName.Trim()
                : req.ItemId;
            parts.Add($"{req.Amount} {name}");
        }

        return string.Join(", ", parts);
    }

    private static int FindItemSlot(Inventory inventory, string itemId)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(itemId))
            return -1;

        string target = itemId.Trim();
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            Inventory.Slot slot = inventory.GetSlot(i);
            if (slot.IsEmpty)
                continue;

            if (string.Equals(slot.itemId, target, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static int FindScrollInStorageEnhanceTab(PlayerStorage storage, string itemId)
    {
        if (storage == null || string.IsNullOrWhiteSpace(itemId))
            return -1;

        return storage.FindFirstSlotWithItemInTab(itemId, StorageTabKind.Enhance);
    }
}
