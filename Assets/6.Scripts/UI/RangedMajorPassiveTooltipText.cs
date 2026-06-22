using System;
using System.Text;
using UnityEngine;

/// <summary>Skill-tree and details copy for Ranged major passives.</summary>
public static class RangedMajorPassiveTooltipText
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
        if (string.Equals(parentSpineNodeId, AbilityCombatPower.SeekerArrowsMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            body = CombineTooltipLines(
                BuildSeekerArrowsScalingBody(),
                BuildSeekerArrowsEffectBody(stats, selectedChoice));
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.HuntersMarkMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            body = BuildHuntersMarkEffectBody(stats, selectedChoice);
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.EnchantedQuiverMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            body = BuildEnchantedQuiverEffectBody(stats, selectedChoice);
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
        if (string.Equals(parentSpineNodeId, AbilityCombatPower.SeekerArrowsMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            scalingText = BuildSeekerArrowsScalingBody();
            effectText = BuildSeekerArrowsEffectBody(stats, selectedChoice);
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.HuntersMarkMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            effectText = BuildHuntersMarkEffectBody(stats, selectedChoice);
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.EnchantedQuiverMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            effectText = BuildEnchantedQuiverEffectBody(stats, selectedChoice);
            return true;
        }

        return false;
    }

    public static string BuildSeekerArrowsScalingBody() =>
        AbilityTooltipDamagePreview.WrapDetailsScalingAccentLine(
            $"Deals {AbilityCombatPower.SeekerArrowWeaponDamageFraction * 100f:0.#}% of your weapon damage");

    public static bool TryBuildChoiceTooltipBody(string parentSpineNodeId, int choiceIndex, out string body)
    {
        body = null;
        if (string.Equals(parentSpineNodeId, AbilityCombatPower.SeekerArrowsMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            if (choiceIndex == AbilityCombatPower.SeekerArrowsEnhancementChainChoiceIndex)
            {
                body = "When this effect triggers, it can also trigger itself.";
                return true;
            }

            if (choiceIndex == AbilityCombatPower.SeekerArrowsEnhancementVolleyChoiceIndex)
            {
                body =
                    $"{AbilityCombatPower.SeekerArrowVolleyProcChance * 100f:0.#}% chance to trigger seeker arrow " +
                    $"{AbilityCombatPower.SeekerArrowVolleyCount} consecutive times.";
                return true;
            }

            return false;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.HuntersMarkMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            if (choiceIndex == AbilityCombatPower.HuntersMarkEnhancementDeepMarkChoiceIndex)
            {
                body =
                    $"Increase the minion damage bonus of Hunter's Mark by " +
                    $"{AbilityCombatPower.HuntersMarkDeepMarkExtraMinionDamageBonusFraction * 100f:0.#}% " +
                    $"({(AbilityCombatPower.HuntersMarkBaseMinionDamageBonusFraction + AbilityCombatPower.HuntersMarkDeepMarkExtraMinionDamageBonusFraction) * 100f:0.#}% total).";
                return true;
            }

            if (choiceIndex == AbilityCombatPower.HuntersMarkEnhancementPredatorsBroodChoiceIndex)
            {
                body =
                    $"Minions gain +{AbilityCombatPower.HuntersMarkPredatorsBroodMinionCritChanceBonus * 100f:0.#}% crit chance " +
                    $"and +{AbilityCombatPower.HuntersMarkPredatorsBroodMinionAilmentChanceBonus * 100f:0.#}% chance to apply ailments.";
                return true;
            }

            return false;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.EnchantedQuiverMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            if (choiceIndex == AbilityCombatPower.EnchantedQuiverEnhancementArrowRecoveryChoiceIndex)
            {
                body =
                    $"{AbilityCombatPower.EnchantedQuiverArrowRecoveryOnKillChance * 100f:0.#}% chance to gain " +
                    $"{AbilityCombatPower.EnchantedQuiverArrowRecoveryMinAmount}-{AbilityCombatPower.EnchantedQuiverArrowRecoveryMaxAmount} arrows " +
                    "(of the same type as currently equipped arrows) when killing an enemy.";
                return true;
            }

            if (choiceIndex == AbilityCombatPower.EnchantedQuiverEnhancementConservationChoiceIndex)
            {
                body =
                    $"Gain a further {AbilityCombatPower.EnchantedQuiverConservationExtraArrowSaveChance * 100f:0.#}% chance on auto attacks to save arrows " +
                    $"and increase the next ranged auto attack hit to " +
                    $"{AbilityCombatPower.EnchantedQuiverConservationSavedArrowDamageBonusFraction * 100f:0.#}% bonus damage.";
                return true;
            }

            return false;
        }

        return false;
    }

    public static bool TryBuildFlavorDescription(string parentSpineNodeId, out string flavor)
    {
        flavor = null;
        if (string.Equals(parentSpineNodeId, AbilityCombatPower.SeekerArrowsMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            flavor =
                "Has a chance on every auto attack to fire a phantom seeker arrow at your target. " +
                "Seeker arrows cannot apply on-hit effects.";
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.HuntersMarkMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            flavor =
                "Enemies hit by auto attacks or other non-minion abilities apply Hunter's Mark for 10 seconds. " +
                "Marked enemies take increased damage from minions.";
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.EnchantedQuiverMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            flavor =
                "Gain a chance to not consume arrows when using ranged auto attacks. " +
                "When an arrow is saved, increase the damage of your next ranged auto attack hit.";
            return true;
        }

        return false;
    }

    public static string BuildSeekerArrowsEffectBody(CharacterStats stats, int selectedChoice = -1)
    {
        var sb = new StringBuilder();
        AppendParagraph(sb, FormatSeekerArrowDamageLine(stats));
        AppendParagraph(sb, FormatSeekerArrowProcChanceLine());
        if (TryGetSeekerArrowsCommittedEnhancementEffectLine(selectedChoice, out string enhancementLine))
            AppendParagraph(sb, enhancementLine);

        return sb.ToString();
    }

    public static string BuildHuntersMarkEffectBody(CharacterStats stats, int selectedChoice = -1)
    {
        var sb = new StringBuilder();
        float bonus = stats != null
            ? stats.GetHuntersMarkMinionDamageBonusFraction()
            : AbilityCombatPower.HuntersMarkBaseMinionDamageBonusFraction;
        AppendParagraph(sb,
            "Enemies hit by auto attacks or other non-minion abilities apply Hunter's Mark.");
        AppendParagraph(sb,
            $"Marked enemies take +{bonus * 100f:0.#}% damage from minions for {AbilityCombatPower.HuntersMarkDurationSeconds:0.#}s (refreshed on hit).");

        if (selectedChoice == AbilityCombatPower.HuntersMarkEnhancementDeepMarkChoiceIndex)
        {
            AppendParagraph(sb,
                $"Deep Mark: +{AbilityCombatPower.HuntersMarkDeepMarkExtraMinionDamageBonusFraction * 100f:0.#}% minion damage bonus " +
                $"({(AbilityCombatPower.HuntersMarkBaseMinionDamageBonusFraction + AbilityCombatPower.HuntersMarkDeepMarkExtraMinionDamageBonusFraction) * 100f:0.#}% total).");
        }
        else if (selectedChoice == AbilityCombatPower.HuntersMarkEnhancementPredatorsBroodChoiceIndex)
        {
            AppendParagraph(sb,
                $"Predator's Brood: minions gain +{AbilityCombatPower.HuntersMarkPredatorsBroodMinionCritChanceBonus * 100f:0.#}% crit chance " +
                $"and +{AbilityCombatPower.HuntersMarkPredatorsBroodMinionAilmentChanceBonus * 100f:0.#}% chance to apply ailments.");
        }

        return sb.ToString();
    }

    public static string BuildEnchantedQuiverEffectBody(CharacterStats stats, int selectedChoice = -1)
    {
        var sb = new StringBuilder();
        float saveChance = stats != null
            ? stats.GetEnchantedQuiverArrowSaveChanceFraction()
            : AbilityCombatPower.EnchantedQuiverBaseArrowSaveChance;
        float savedHitBonus = stats != null
            ? stats.GetEnchantedQuiverSavedArrowDamageBonusFraction()
            : AbilityCombatPower.EnchantedQuiverBaseSavedArrowDamageBonusFraction;

        AppendParagraph(sb,
            $"{saveChance * 100f:0.#}% chance to not consume arrows on ranged auto attacks.");
        AppendParagraph(sb,
            $"When an arrow is saved, your next ranged auto attack hit deals +{savedHitBonus * 100f:0.#}% damage.");

        return sb.ToString();
    }

    private static bool TryGetSeekerArrowsCommittedEnhancementEffectLine(int selectedChoice, out string line)
    {
        line = null;
        if (selectedChoice == AbilityCombatPower.SeekerArrowsEnhancementChainChoiceIndex)
        {
            line = "When this effect triggers, it can also trigger itself.";
            return true;
        }

        if (selectedChoice == AbilityCombatPower.SeekerArrowsEnhancementVolleyChoiceIndex)
        {
            line =
                $"{AbilityCombatPower.SeekerArrowVolleyProcChance * 100f:0.#}% chance to trigger seeker arrow " +
                $"{AbilityCombatPower.SeekerArrowVolleyCount} consecutive times.";
            return true;
        }

        return false;
    }

    private static string FormatSeekerArrowDamageLine(CharacterStats stats)
    {
        if (stats == null)
            return $"{AbilityCombatPower.SeekerArrowWeaponDamageFraction * 100f:0.#}% weapon damage per seeker arrow on hit";

        float avgPhys = (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        float avgMag = (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f;
        float avgCorr = (Mathf.Max(0f, stats.MinSplitDamage.corruptionDamage) + Mathf.Max(0f, stats.MaxSplitDamage.corruptionDamage)) * 0.5f;
        float scaledTotal = (avgPhys + avgMag + avgCorr) * AbilityCombatPower.SeekerArrowWeaponDamageFraction;
        if (scaledTotal <= 0.0001f)
            return $"{AbilityCombatPower.SeekerArrowWeaponDamageFraction * 100f:0.#}% weapon damage per seeker arrow on hit";

        return $"{Mathf.RoundToInt(scaledTotal)} damage per seeker arrow on hit";
    }

    private static string FormatSeekerArrowProcChanceLine() =>
        $"{AbilityCombatPower.SeekerArrowProcChance * 100f:0.#}% chance to proc on auto attack or seeker arrow hit";

    private static void AppendParagraph(StringBuilder sb, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (sb.Length > 0)
            sb.Append(DetailsEffectParagraphGap);

        sb.Append(line.Trim());
    }

    private static string CombineTooltipLines(params string[] lines)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
            AppendParagraph(sb, lines[i]);

        return sb.ToString();
    }
}
