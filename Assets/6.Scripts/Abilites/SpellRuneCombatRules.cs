using UnityEngine;

/// <summary>
/// Staff offhand rune validation and consumption for spell abilities.
/// Rune stat bonuses apply once per cast (equipped rune), not per rune consumed.
/// </summary>
public static class SpellRuneCombatRules
{
    public static bool StaffRequiresSpellRunes(ItemDefinition mainHand) =>
        mainHand != null && mainHand.IsMagicStaff;

    public static bool AbilityUsesSpellRunesWithStaff(AbilityDefinition def, ItemDefinition mainHand) =>
        def != null && StaffRequiresSpellRunes(mainHand) && def.RequiresSpellRuneConsumption();

    public static int GetRunesConsumedPerCast(AbilityDefinition def) =>
        def != null ? def.GetSupportRunesConsumedPerCast() : 0;

    public static bool HasRequiredSpellRunes(
        AbilityDefinition def,
        ItemDefinition mainHand,
        ItemDefinition offHand,
        int offHandStackAmount)
    {
        if (!AbilityUsesSpellRunesWithStaff(def, mainHand))
            return true;

        if (!def.OffhandRuneSatisfiesSpell(offHand))
            return false;

        int consume = def.GetSupportRunesConsumedPerCast();
        return offHandStackAmount >= consume;
    }

    public static string ResolveMissingSpellRunesMessage(
        AbilityDefinition def,
        ItemDefinition mainHand,
        ItemDefinition offHand,
        int offHandStackAmount)
    {
        if (!AbilityUsesSpellRunesWithStaff(def, mainHand))
            return "Out of runes.";

        if (offHand == null || offHand.SupportType != CombatSupportType.Runes)
            return "Requires runes in offhand.";

        if (!def.OffhandRuneSatisfiesSpell(offHand))
            return $"Requires {FormatRuneElementLabel(def.requiredChargedRuneElement)} or elemental runes.";

        int consume = def.GetSupportRunesConsumedPerCast();
        if (offHandStackAmount < consume)
            return consume > 1 ? $"Out of runes (need {consume})." : "Out of runes.";

        return "Out of runes.";
    }

    public static string FormatRuneElementLabel(ChargedRuneElement element) =>
        element switch
        {
            ChargedRuneElement.Fire => "fire runes",
            ChargedRuneElement.Ice => "ice runes",
            ChargedRuneElement.Lightning => "lightning runes",
            ChargedRuneElement.Elemental => "elemental runes",
            _ => "runes"
        };

    /// <summary>Tooltip hint under spell rune consumption, e.g. "(Lightning or elemental runes)".</summary>
    public static string FormatSpellRuneRequirementParenthetical(ChargedRuneElement element) =>
        element switch
        {
            ChargedRuneElement.Fire => "(Fire or elemental runes)",
            ChargedRuneElement.Ice => "(Ice or elemental runes)",
            ChargedRuneElement.Lightning => "(Lightning or elemental runes)",
            ChargedRuneElement.Elemental => "(elemental runes)",
            _ => "(Runes)"
        };
}
