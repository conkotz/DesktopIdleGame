using System;
using UnityEngine;

/// <summary>
/// Shared spell targeting and tooltip rules for magic weapon spells.
/// </summary>
public static class SpellCombatRules
{
    public static bool IsSpellAbility(AbilityDefinition def)
    {
        if (def == null)
            return false;

        if (MagicStarterSpellRules.IsMagicStarterSpellId(def.abilityId))
            return true;

        if (string.Equals(def.abilityId, AbilityCombatPower.ChainLightningAbilityId, StringComparison.OrdinalIgnoreCase))
            return true;

        return def.requiredWeaponType == AbilityWeaponRequirement.Magic
               && def.manaCost > 0f
               && def.tag == AbilityTag.Active;
    }

    /// <summary>Spell initial-hit reach — matches equipped weapon <see cref="CharacterStats.Range"/> / attack-range checks.</summary>
    public static float GetSpellHitRange(CharacterStats stats) =>
        stats != null ? Mathf.Max(0.5f, stats.Range) : 0.5f;
}
