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

    public static EnhancementOptionPayment Resolve(
        Inventory inventory,
        EnhancementOptionEntry option,
        ItemDefinition gear,
        bool preferScroll)
    {
        if (inventory == null || option == null || gear == null)
            return None;

        if (preferScroll && !string.IsNullOrWhiteSpace(option.linkedScrollItemId))
        {
            int scrollSlot = FindItemSlot(inventory, option.linkedScrollItemId);
            if (scrollSlot >= 0)
                return new EnhancementOptionPayment(EnhancementPaymentKind.Scroll, scrollSlot);
        }

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

        if (!preferScroll && !string.IsNullOrWhiteSpace(option.linkedScrollItemId))
        {
            int scrollSlot = FindItemSlot(inventory, option.linkedScrollItemId);
            if (scrollSlot >= 0)
                return new EnhancementOptionPayment(EnhancementPaymentKind.Scroll, scrollSlot);
        }

        return None;
    }

    public static bool HasAnyPayment(Inventory inventory, EnhancementOptionEntry option, ItemDefinition gear)
    {
        return Resolve(inventory, option, gear, preferScroll: true).Kind != EnhancementPaymentKind.None ||
               Resolve(inventory, option, gear, preferScroll: false).Kind != EnhancementPaymentKind.None;
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
