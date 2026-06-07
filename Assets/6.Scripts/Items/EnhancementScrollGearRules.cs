using UnityEngine;

public static class EnhancementScrollGearRules
{
    public static EnhancementScrollGearMask AllArmorSlots =>
        EnhancementScrollGearMask.Helmet | EnhancementScrollGearMask.Body | EnhancementScrollGearMask.Boots;

    public static EnhancementScrollGearMask NormalizeMask(EnhancementScrollGearMask mask)
    {
        if ((mask & EnhancementScrollGearMask.Armor) != 0)
            mask = (mask & ~EnhancementScrollGearMask.Armor) | AllArmorSlots;

        return mask;
    }

    public static bool MaskAllowsGear(EnhancementScrollGearMask mask, ItemDefinition gear)
    {
        EnhancementScrollGearMask gearMask = GetMaskForGear(gear);
        if (gearMask == EnhancementScrollGearMask.None)
            return false;

        return (NormalizeMask(mask) & gearMask) != 0;
    }

    public static bool MaskTargetsArmorSlots(EnhancementScrollGearMask mask) =>
        (NormalizeMask(mask) & AllArmorSlots) != 0;

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

        if (gear.itemKind == ItemKind.Armor)
        {
            return gear.equipSlot switch
            {
                EquipSlot.Helmet => EnhancementScrollGearMask.Helmet,
                EquipSlot.Body => EnhancementScrollGearMask.Body,
                EquipSlot.Boots => EnhancementScrollGearMask.Boots,
                _ => EnhancementScrollGearMask.None
            };
        }

        return gear.itemKind switch
        {
            ItemKind.Tool => EnhancementScrollGearMask.Tool,
            _ => EnhancementScrollGearMask.None
        };
    }
}
