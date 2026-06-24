using System;
using System.Text;
using UnityEngine;

/// <summary>Skill-tree and details copy for Endurance major passives.</summary>
public static class EnduranceMajorPassiveTooltipText
{
    public const string DetailsEffectParagraphGap = "\n\n";

    public static bool TryBuildSkillTreeBody(string parentSpineNodeId, int selectedChoice, out string body) =>
        TryBuildSkillTreeBody(parentSpineNodeId, selectedChoice, AbilityTooltipDamagePreview.FindLocalPlayerStats(), out body);

    public static bool TryBuildSkillTreeBody(
        string parentSpineNodeId,
        int selectedChoice,
        CharacterStats stats,
        out string body)
    {
        body = null;
        if (string.Equals(parentSpineNodeId, AbilityCombatPower.AlchemistsBoonMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            body = BuildAlchemistsBoonEffectBody(stats, selectedChoice);
            return true;
        }

        return false;
    }

    public static bool TryBuildDetailsPanelSections(
        string parentSpineNodeId,
        int selectedChoice,
        CharacterStats stats,
        out string scalingText,
        out string effectText)
    {
        scalingText = null;
        effectText = null;

        if (!string.Equals(parentSpineNodeId, AbilityCombatPower.AlchemistsBoonMajorPassiveSpineNodeId, StringComparison.Ordinal))
            return false;

        effectText = BuildAlchemistsBoonEffectBody(stats, selectedChoice);
        return true;
    }

    public static bool TryBuildChoiceTooltipBody(string parentSpineNodeId, int choiceIndex, out string body)
    {
        body = null;
        if (!string.Equals(parentSpineNodeId, AbilityCombatPower.AlchemistsBoonMajorPassiveSpineNodeId, StringComparison.Ordinal))
            return false;

        if (choiceIndex == AbilityCombatPower.AlchemistsBoonEnhancementFoodCooldownChoiceIndex)
        {
            body =
                $"Reduce food cooldown by {AbilityCombatPower.AlchemistsBoonConsumableCooldownReductionFraction * 100f:0.#}%.";
            return true;
        }

        if (choiceIndex == AbilityCombatPower.AlchemistsBoonEnhancementPotionCooldownChoiceIndex)
        {
            body =
                $"Reduce potion cooldown by {AbilityCombatPower.AlchemistsBoonConsumableCooldownReductionFraction * 100f:0.#}%.";
            return true;
        }

        return false;
    }

    public static string BuildAlchemistsBoonEffectBody(CharacterStats stats, int selectedChoice = -1)
    {
        var sb = new StringBuilder();
        AppendParagraph(sb,
            $"Potion buffs last +{AbilityCombatPower.AlchemistsBoonConsumableDurationBonusSeconds:0.#}s.");
        AppendParagraph(sb,
            $"Food heals for an extra {AbilityCombatPower.AlchemistsBoonFoodHealBonusFraction * 100f:0.#}% health.");
        AppendParagraph(sb,
            $"Food can overheal up to {AbilityCombatPower.AlchemistsBoonFoodOverhealMaxHpFraction * 100f:0.#}% of your maximum HP.");

        if (selectedChoice == AbilityCombatPower.AlchemistsBoonEnhancementFoodCooldownChoiceIndex)
        {
            AppendParagraph(sb,
                $"Swift Meals: food cooldown reduced by {AbilityCombatPower.AlchemistsBoonConsumableCooldownReductionFraction * 100f:0.#}%.");
        }
        else if (selectedChoice == AbilityCombatPower.AlchemistsBoonEnhancementPotionCooldownChoiceIndex)
        {
            AppendParagraph(sb,
                $"Quick Brew: potion cooldown reduced by {AbilityCombatPower.AlchemistsBoonConsumableCooldownReductionFraction * 100f:0.#}%.");
        }

        return sb.ToString();
    }

    private static void AppendParagraph(StringBuilder sb, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (sb.Length > 0)
            sb.Append(DetailsEffectParagraphGap);

        sb.Append(line.Trim());
    }
}
