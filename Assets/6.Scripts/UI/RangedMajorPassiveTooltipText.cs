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
        if (!string.Equals(parentSpineNodeId, AbilityCombatPower.SeekerArrowsMajorPassiveSpineNodeId, StringComparison.Ordinal))
            return false;

        body = CombineTooltipLines(
            BuildSeekerArrowsScalingBody(),
            BuildSeekerArrowsEffectBody(stats, selectedChoice));
        return true;
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
        if (!string.Equals(parentSpineNodeId, AbilityCombatPower.SeekerArrowsMajorPassiveSpineNodeId, StringComparison.Ordinal))
            return false;

        scalingText = BuildSeekerArrowsScalingBody();
        effectText = BuildSeekerArrowsEffectBody(stats, selectedChoice);
        return true;
    }

    public static string BuildSeekerArrowsScalingBody() =>
        AbilityTooltipDamagePreview.WrapDetailsScalingAccentLine(
            $"Deals {AbilityCombatPower.SeekerArrowWeaponDamageFraction * 100f:0.#}% of your weapon damage");

    public static bool TryBuildChoiceTooltipBody(string parentSpineNodeId, int choiceIndex, out string body)
    {
        body = null;
        if (!string.Equals(parentSpineNodeId, AbilityCombatPower.SeekerArrowsMajorPassiveSpineNodeId, StringComparison.Ordinal))
            return false;

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

    public static bool TryBuildFlavorDescription(string parentSpineNodeId, out string flavor)
    {
        flavor = null;
        if (!string.Equals(parentSpineNodeId, AbilityCombatPower.SeekerArrowsMajorPassiveSpineNodeId, StringComparison.Ordinal))
            return false;

        flavor =
            "Has a chance on every auto attack to fire a phantom seeker arrow at your target. " +
            "Seeker arrows cannot apply on-hit effects.";
        return true;
    }

    public static string BuildSeekerArrowsEffectBody(CharacterStats stats, int selectedChoice = -1)
    {
        var sb = new StringBuilder();
        AppendParagraph(sb, FormatSeekerArrowDamageLine(stats));
        AppendParagraph(sb, FormatSeekerArrowProcChanceLine());
        if (TryGetCommittedEnhancementEffectLine(selectedChoice, out string enhancementLine))
            AppendParagraph(sb, enhancementLine);

        return sb.ToString();
    }

    private static bool TryGetCommittedEnhancementEffectLine(int selectedChoice, out string line)
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
