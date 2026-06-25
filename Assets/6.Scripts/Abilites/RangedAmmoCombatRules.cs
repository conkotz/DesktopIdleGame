using System;
using UnityEngine;

/// <summary>
/// Bow/crossbow offhand ammo validation for ranged abilities and tooltips.
/// </summary>
public static class RangedAmmoCombatRules
{
    public static bool BowRequiresArrowAmmo(ItemDefinition mainHand) =>
        mainHand != null && mainHand.IsWeapon && mainHand.RequiredSupportType == CombatSupportType.Arrows;

    public static bool CrossbowRequiresBoltAmmo(ItemDefinition mainHand) =>
        mainHand != null && mainHand.IsWeapon && mainHand.RequiredSupportType == CombatSupportType.Bolts;

    public static bool WeaponRequiresRangedAmmo(ItemDefinition mainHand) =>
        BowRequiresArrowAmmo(mainHand) || CrossbowRequiresBoltAmmo(mainHand);

    /// <summary>Abilities that spend equipped offhand arrows/bolts when used.</summary>
    public static bool AbilityUsesRangedAmmo(AbilityDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.abilityId))
            return false;

        return string.Equals(def.abilityId, CombatStarterAttackAbility.RangedAttackAbilityId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(def.abilityId, AbilityCombatPower.TripleShotAbilityId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(def.abilityId, AbilityCombatPower.StaticArrowsAbilityId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(def.abilityId, AbilityCombatPower.SnipeAbilityId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(def.abilityId, AbilityCombatPower.PenetratingShotAbilityId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasRequiredRangedAmmo(
        ItemDefinition mainHand,
        ItemDefinition offHand,
        int offHandStackAmount)
    {
        if (!WeaponRequiresRangedAmmo(mainHand))
            return true;

        if (offHand == null || !offHand.IsCombatSupport || !offHand.SupportConsumableOnAttack)
            return false;

        if (offHand.SupportType != mainHand.RequiredSupportType)
            return false;

        int consume = Mathf.Max(1, offHand.SupportConsumeAmountPerAttack);
        return offHandStackAmount >= consume;
    }

    public static string FormatAmmoTypeParenthetical(CombatSupportType supportType) =>
        supportType switch
        {
            CombatSupportType.Arrows => "(Arrows)",
            CombatSupportType.Bolts => "(Bolts)",
            _ => "(Ammo)"
        };

    public static string BuildAmmoConsumptionLine(ItemDefinition mainHand)
    {
        CombatSupportType supportType = mainHand != null ? mainHand.RequiredSupportType : CombatSupportType.Arrows;
        string ammoLabel = supportType == CombatSupportType.Bolts ? "bolt" : "arrow";
        return $"Consumes 1 {ammoLabel} per attack";
    }

    public static bool TryBuildAmmoConsumptionLines(
        AbilityDefinition def,
        CharacterStats stats,
        out string consumeLine,
        out string ammoTypeLine)
    {
        consumeLine = null;
        ammoTypeLine = null;

        if (!AbilityUsesRangedAmmo(def))
            return false;

        ItemDefinition mainHand = stats != null ? stats.GetEquippedMainHandWeaponOrNull() : null;
        if (!WeaponRequiresRangedAmmo(mainHand))
            return false;

        consumeLine = BuildAmmoConsumptionLine(mainHand);
        ammoTypeLine = FormatAmmoTypeParenthetical(mainHand.RequiredSupportType);
        return true;
    }
}
