using UnityEngine;

public readonly struct EnhancementOptionPayment
{
    public EnhancementPaymentKind Kind { get; }
    public int ScrollSlotIndex { get; }
    public string MaterialItemId { get; }
    public int MaterialAmount { get; }

    public EnhancementOptionPayment(
        EnhancementPaymentKind kind,
        int scrollSlotIndex = -1,
        string materialItemId = null,
        int materialAmount = 0)
    {
        Kind = kind;
        ScrollSlotIndex = scrollSlotIndex;
        MaterialItemId = materialItemId;
        MaterialAmount = materialAmount;
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
        ItemDefinition gear)
    {
        if (inventory == null || option == null)
            return None;

        if (HasScrollPayment(inventory, option))
            return new EnhancementOptionPayment(EnhancementPaymentKind.Scroll, FindItemSlot(inventory, option.linkedScrollItemId));

        if (RequiresScrollOnlyPayment(option) || gear == null)
            return None;

        string materialId = GearUpgradeMaterialResolver.ResolveMaterialItemId(gear);
        int materialCost = EnhancementTierRules.GetMaterialCost(option.tier);
        if (!string.IsNullOrWhiteSpace(materialId) &&
            inventory.CountItem(materialId) >= materialCost)
        {
            return new EnhancementOptionPayment(
                EnhancementPaymentKind.Materials,
                materialItemId: materialId,
                materialAmount: materialCost);
        }

        return None;
    }

    public static bool HasScrollPayment(Inventory inventory, EnhancementOptionEntry option)
    {
        if (inventory == null || option == null || string.IsNullOrWhiteSpace(option.linkedScrollItemId))
            return false;

        return FindItemSlot(inventory, option.linkedScrollItemId) >= 0;
    }

    public static bool HasMaterialPayment(Inventory inventory, EnhancementOptionEntry option, ItemDefinition gear)
    {
        if (inventory == null || option == null || gear == null || RequiresScrollOnlyPayment(option))
            return false;

        string materialId = GearUpgradeMaterialResolver.ResolveMaterialItemId(gear);
        int materialCost = EnhancementTierRules.GetMaterialCost(option.tier);
        return !string.IsNullOrWhiteSpace(materialId) &&
               inventory.CountItem(materialId) >= materialCost;
    }

    public static bool HasAnyPayment(Inventory inventory, EnhancementOptionEntry option, ItemDefinition gear)
    {
        return HasScrollPayment(inventory, option) || HasMaterialPayment(inventory, option, gear);
    }

    public static string FormatCostLabel(EnhancementOptionPayment payment, ItemDatabase db)
    {
        if (payment.Kind == EnhancementPaymentKind.Scroll)
            return "Scroll";

        if (payment.Kind == EnhancementPaymentKind.Materials)
        {
            ItemDefinition mat = db != null ? db.Get(payment.MaterialItemId) : null;
            string name = mat != null && !string.IsNullOrWhiteSpace(mat.displayName)
                ? mat.displayName.Trim()
                : payment.MaterialItemId;
            return $"{payment.MaterialAmount} {name}";
        }

        return "Unavailable";
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

            if (string.Equals(slot.itemId, target, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}
