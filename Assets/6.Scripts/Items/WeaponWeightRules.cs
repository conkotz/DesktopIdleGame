using UnityEngine;

public static class WeaponWeightRules
{
    public static WeaponWeight GetEffectiveWeight(ItemDefinition gear)
    {
        if (gear == null || !gear.IsWeapon)
            return WeaponWeight.Medium;

        WeaponWeight configured = gear.weaponStats.weaponWeight;
        if (configured != WeaponWeight.NotApplicable)
            return configured;

        return InferWeight(gear);
    }

    public static WeaponWeight InferWeight(ItemDefinition gear)
    {
        if (gear == null || !gear.IsWeapon)
            return WeaponWeight.NotApplicable;

        WeaponStats stats = gear.weaponStats;

        if (stats.attackSkill == AttackSkill.Ranged)
        {
            return stats.rangedBowType == RangedBowType.Longbow
                ? WeaponWeight.Heavy
                : WeaponWeight.Light;
        }

        if (stats.attackSkill == AttackSkill.Magic)
        {
            return stats.mainHandArchetype == MainHandWeaponArchetype.Wand
                ? WeaponWeight.Light
                : WeaponWeight.Medium;
        }

        string id = (gear.itemId ?? string.Empty).ToLowerInvariant();
        if (id.Contains("dagger"))
            return WeaponWeight.Light;
        if (id.Contains("polearm") || id.Contains("longbow"))
            return WeaponWeight.Heavy;
        if (id.Contains("swiftbow") || id.Contains("wand"))
            return WeaponWeight.Light;
        if (id.Contains("spear") || id.Contains("mace") || id.Contains("sword"))
            return WeaponWeight.Medium;

        float aps = stats.attacksPerSecond;
        if (aps >= 0.85f)
            return WeaponWeight.Light;
        if (aps <= 0.5f)
            return WeaponWeight.Heavy;

        return WeaponWeight.Medium;
    }
}
