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

        if (def.tag == AbilityTag.Spell)
            return true;

        return MagicStarterSpellRules.IsMagicStarterSpellId(def.abilityId)
               || string.Equals(def.abilityId, AbilityCombatPower.ChainLightningAbilityId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Spell initial-hit reach — matches equipped weapon <see cref="CharacterStats.Range"/> / attack-range checks.</summary>
    public static float GetSpellHitRange(CharacterStats stats) =>
        stats != null ? Mathf.Max(0.5f, stats.Range) : 0.5f;

    /// <summary>Element used for spell damage scaling lines. Returns false for non-spells.</summary>
    public static bool TryGetSpellElement(AbilityDefinition def, out MagicAttackType element)
    {
        element = MagicAttackType.Lightning;
        if (!IsSpellAbility(def))
            return false;

        if (MagicStarterSpellRules.IsMagicStarterSpellId(def.abilityId))
        {
            element = MagicStarterSpellRules.GetMagicAttackTypeForAbilityId(def.abilityId);
            return true;
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.ChainLightningAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            element = MagicAttackType.Lightning;
            return true;
        }

        if (def.fireDamageMultiplier > 0f)
        {
            element = MagicAttackType.Fire;
            return true;
        }

        if (def.iceDamageMultiplier > 0f)
        {
            element = MagicAttackType.Ice;
            return true;
        }

        if (def.lightningDamageMultiplier > 0f)
        {
            element = MagicAttackType.Lightning;
            return true;
        }

        return true;
    }

    public static bool SpellUsesElementScaling(AbilityDefinition def)
    {
        if (!IsSpellAbility(def))
            return false;

        if (MagicStarterSpellRules.IsMagicStarterSpellId(def.abilityId))
            return true;

        if (string.Equals(def.abilityId, AbilityCombatPower.ChainLightningAbilityId, StringComparison.OrdinalIgnoreCase))
            return true;

        return def.fireDamageMultiplier > 0f
               || def.iceDamageMultiplier > 0f
               || def.lightningDamageMultiplier > 0f;
    }
}
