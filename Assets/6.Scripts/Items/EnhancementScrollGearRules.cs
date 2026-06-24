using UnityEngine;

public static class EnhancementScrollGearRules
{
    public static EnhancementScrollGearMask AllArmourSlots =>
        EnhancementScrollGearMask.Helmet |
        EnhancementScrollGearMask.Body |
        EnhancementScrollGearMask.Boots |
        EnhancementScrollGearMask.OffHand;

    public static EnhancementScrollGearMask NormalizeMask(EnhancementScrollGearMask mask)
    {
        if ((mask & EnhancementScrollGearMask.Armour) != 0)
            mask = (mask & ~EnhancementScrollGearMask.Armour) | AllArmourSlots;

        return mask;
    }

    public static bool MaskAllowsGear(EnhancementScrollGearMask mask, ItemDefinition gear)
    {
        EnhancementScrollGearMask gearMask = GetMaskForGear(gear);
        if (gearMask == EnhancementScrollGearMask.None)
            return false;

        return (NormalizeMask(mask) & gearMask) != 0;
    }

    public static bool MaskTargetsArmourSlots(EnhancementScrollGearMask mask) =>
        (NormalizeMask(mask) & AllArmourSlots) != 0;

    public static EnhancementScrollGearMask GetMaskForGear(ItemDefinition gear)
    {
        if (gear == null)
            return EnhancementScrollGearMask.None;

        if (gear.itemKind == ItemKind.Weapon)
        {
            EnhancementScrollGearMask weaponMask = EnhancementScrollGearMask.Weapon;
            weaponMask |= gear.weaponStats.attackSkill switch
            {
                AttackSkill.Melee => EnhancementScrollGearMask.MeleeWeapon,
                AttackSkill.Ranged => EnhancementScrollGearMask.RangedWeapon,
                AttackSkill.Magic => EnhancementScrollGearMask.MagicWeapon,
                _ => EnhancementScrollGearMask.None
            };

            return weaponMask;
        }

        if (gear.itemKind == ItemKind.Armour)
        {
            return gear.equipSlot switch
            {
                EquipSlot.Helmet => EnhancementScrollGearMask.Helmet,
                EquipSlot.Body => EnhancementScrollGearMask.Body,
                EquipSlot.Boots => EnhancementScrollGearMask.Boots,
                EquipSlot.OffHand => EnhancementScrollGearMask.OffHand,
                _ => EnhancementScrollGearMask.None
            };
        }

        if (gear.itemKind == ItemKind.CombatSupport && gear.equipSlot == EquipSlot.OffHand)
            return EnhancementScrollGearMask.OffHand;

        if (gear.itemKind == ItemKind.Jewelry)
            return EnhancementScrollGearMask.Jewelry;

        return gear.itemKind switch
        {
            ItemKind.Tool => EnhancementScrollGearMask.Tool,
            _ => EnhancementScrollGearMask.None
        };
    }
}
