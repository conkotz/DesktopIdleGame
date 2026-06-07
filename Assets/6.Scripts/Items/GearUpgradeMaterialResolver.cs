using System;

public static class GearUpgradeMaterialResolver
{
    public static GearUpgradeMaterialFamily ResolveFamily(ItemDefinition gear)
    {
        if (gear == null)
            return GearUpgradeMaterialFamily.Unknown;

        string itemId = gear.itemId ?? string.Empty;
        if (itemId.StartsWith("stone_", StringComparison.OrdinalIgnoreCase))
            return GearUpgradeMaterialFamily.Stone;
        if (itemId.StartsWith("leather_", StringComparison.OrdinalIgnoreCase))
            return GearUpgradeMaterialFamily.Leather;
        if (itemId.StartsWith("linen_", StringComparison.OrdinalIgnoreCase))
            return GearUpgradeMaterialFamily.Linen;
        if (itemId.StartsWith("splitwood_", StringComparison.OrdinalIgnoreCase) ||
            itemId.StartsWith("hardwood_", StringComparison.OrdinalIgnoreCase))
            return GearUpgradeMaterialFamily.Wood;

        if (gear.IsWeapon && gear.weaponStats.attackSkill == AttackSkill.Magic)
            return GearUpgradeMaterialFamily.Linen;

        if (gear.IsTool)
            return GearUpgradeMaterialFamily.Wood;

        if (gear.IsArmor)
            return GearUpgradeMaterialFamily.Leather;

        if (gear.IsWeapon)
            return GearUpgradeMaterialFamily.Stone;

        return GearUpgradeMaterialFamily.Unknown;
    }

    public static string ResolveMaterialItemId(ItemDefinition gear)
    {
        return ResolveMaterialItemId(ResolveFamily(gear));
    }

    public static string ResolveMaterialItemId(GearUpgradeMaterialFamily family)
    {
        return family switch
        {
            GearUpgradeMaterialFamily.Stone => "stone_chunk",
            GearUpgradeMaterialFamily.Leather => "leather",
            GearUpgradeMaterialFamily.Linen => "linen",
            GearUpgradeMaterialFamily.Wood => "splitwood_log",
            _ => null,
        };
    }

    public static string ResolveMaterialDisplayName(GearUpgradeMaterialFamily family)
    {
        return family switch
        {
            GearUpgradeMaterialFamily.Stone => "Stone",
            GearUpgradeMaterialFamily.Leather => "Leather",
            GearUpgradeMaterialFamily.Linen => "Linen",
            GearUpgradeMaterialFamily.Wood => "Wood",
            _ => "Materials",
        };
    }
}
