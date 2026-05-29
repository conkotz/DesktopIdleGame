/// <summary>
/// Player-facing labels for equipment slots. Internal enum/save names stay unchanged.
/// </summary>
public static class EquipSlotDisplayNames
{
    public static string GetDisplayName(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Helmet: return "Head";
            case EquipSlot.Boots: return "Feet";
            case EquipSlot.Pendant: return "Neck";
            case EquipSlot.MainHand: return "Main Hand";
            case EquipSlot.OffHand: return "Off Hand";
            case EquipSlot.Body: return "Body";
            case EquipSlot.Trinket: return "Trinket";
            case EquipSlot.Ring: return "Ring";
            case EquipSlot.None: return "None";
            default: return slot.ToString();
        }
    }

    public static string GetDisplayName(EquipmentUISlotType slot)
    {
        switch (slot)
        {
            case EquipmentUISlotType.MainHand: return "Main Hand";
            case EquipmentUISlotType.OffHand: return "Off Hand";
            case EquipmentUISlotType.Helmet: return "Head";
            case EquipmentUISlotType.Body: return "Body";
            case EquipmentUISlotType.Boots: return "Feet";
            case EquipmentUISlotType.Trinket: return "Trinket";
            case EquipmentUISlotType.Pendant: return "Neck";
            case EquipmentUISlotType.Ring1: return "Ring 1";
            case EquipmentUISlotType.Ring2: return "Ring 2";
            case EquipmentUISlotType.Toolbelt0: return "Tool 1";
            case EquipmentUISlotType.Toolbelt1: return "Tool 2";
            case EquipmentUISlotType.Toolbelt2: return "Tool 3";
            case EquipmentUISlotType.Toolbelt3: return "Tool 4";
            default: return "Slot";
        }
    }
}
