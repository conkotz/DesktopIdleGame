using UnityEngine;

/// <summary>
/// Tooltip copy for major passive / capstone rows on the Skills &amp; Abilities right panel.
/// </summary>
public static class SkillUnlockPanelTooltipBuilder
{
    private const string ActiveEnhancementColorHex = "#33CC66";

    public static bool TryBuildListEntryTooltip(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        SkillsManager skillsManager,
        out string title,
        out string body)
    {
        title = null;
        body = null;
        if (skill == null || unlock == null)
            return false;

        title = SkillsAbilityPresentationResolver.ResolveUnlockTitle(unlock);
        if (string.IsNullOrWhiteSpace(title))
            title = "Untitled";

        string spineId = ResolveSpineNodeIdForUnlock(skill, unlock);
        int choiceIndex = skillsManager != null && !string.IsNullOrEmpty(spineId)
            ? skillsManager.GetSkillChoiceSelection(skill.skillType, spineId, -1)
            : -1;

        if (unlock.unlockType == SkillUnlockType.CapstonePassive)
        {
            if (choiceIndex >= 0
                && MeleeMajorPassiveTooltipText.TryBuildCapstoneChoiceBody(choiceIndex, out string choiceBody)
                && !string.IsNullOrWhiteSpace(choiceBody))
            {
                body = choiceBody;
            }
            else
            {
                body = BuildCapstoneOrFallbackBody(unlock);
            }

            return FinalizeListEntryTooltipBody(ref body, unlock, choiceIndex);
        }

        if (skill.skillType == SkillType.Melee
            && !string.IsNullOrEmpty(spineId)
            && MeleeMajorPassiveTooltipText.TryBuildSkillTreeBody(spineId, choiceIndex, out string meleeBody))
        {
            body = meleeBody;
            return FinalizeListEntryTooltipBody(ref body, unlock, choiceIndex);
        }

        if (skill.skillType == SkillType.Ranged
            && !string.IsNullOrEmpty(spineId)
            && RangedMajorPassiveTooltipText.TryBuildSkillTreeBody(spineId, choiceIndex, out string rangedBody))
        {
            body = rangedBody;
            return FinalizeListEntryTooltipBody(ref body, unlock, choiceIndex);
        }

        if (skill.skillType == SkillType.Endurance
            && !string.IsNullOrEmpty(spineId)
            && EnduranceMajorPassiveTooltipText.TryBuildSkillTreeBody(spineId, choiceIndex, out string enduranceBody))
        {
            body = enduranceBody;
            return FinalizeListEntryTooltipBody(ref body, unlock, choiceIndex);
        }

        string majorTitle = title.Trim();
        string enhancementTitle = ResolveEnhancementTitle(unlock, choiceIndex);
        if (GatheringPassiveTooltipText.TryBuildSkillTreeMajorPassiveBody(
                skill.skillType, majorTitle, enhancementTitle, out string gatheringBody))
        {
            body = gatheringBody;
            return FinalizeListEntryTooltipBody(ref body, unlock, choiceIndex);
        }

        body = BuildCapstoneOrFallbackBody(unlock);
        return FinalizeListEntryTooltipBody(ref body, unlock, choiceIndex);
    }

    /// <summary>
    /// Trailing green Active Enhancement line (skill-tree choice name + description), matching ability tooltips.
    /// </summary>
    public static string FormatActiveEnhancementLine(SkillUnlockDefinition unlock, int choiceIndex)
    {
        if (unlock?.choices == null || choiceIndex < 0 || choiceIndex >= unlock.choices.Count)
            return string.Empty;

        SkillChoiceDefinition choice = unlock.choices[choiceIndex];
        if (choice == null)
            return string.Empty;

        string title = ResolveEnhancementTitle(unlock, choiceIndex);
        if (string.IsNullOrWhiteSpace(title))
            title = $"Enhancement {choiceIndex + 1}";

        string choiceDesc = SkillsAbilityPresentationResolver.ResolveChoiceDescription(choice);
        if (string.IsNullOrWhiteSpace(choiceDesc) && !string.IsNullOrWhiteSpace(choice.description))
            choiceDesc = choice.description.Trim();

        string safeTitle = EscapeTmpRichText(title);
        string safeDesc = EscapeTmpRichText(choiceDesc?.Trim());

        return string.IsNullOrEmpty(safeDesc)
            ? $"\n\nActive Enhancement: <color={ActiveEnhancementColorHex}>{safeTitle}</color>"
            : $"\n\nActive Enhancement: <color={ActiveEnhancementColorHex}>{safeTitle} ({safeDesc})</color>";
    }

    private static bool FinalizeListEntryTooltipBody(ref string body, SkillUnlockDefinition unlock, int choiceIndex)
    {
        string enhancementLine = FormatActiveEnhancementLine(unlock, choiceIndex);
        if (!string.IsNullOrEmpty(enhancementLine))
        {
            if (string.IsNullOrWhiteSpace(body))
                body = enhancementLine.TrimStart();
            else
                body = body.TrimEnd() + enhancementLine;
        }

        return !string.IsNullOrWhiteSpace(body);
    }

    private static string BuildCapstoneOrFallbackBody(SkillUnlockDefinition unlock)
    {
        string desc = unlock != null
            ? SkillsAbilityPresentationResolver.ResolveUnlockDescription(unlock)
            : string.Empty;

        return string.IsNullOrWhiteSpace(desc) ? null : desc.Trim();
    }

    /// <summary>Chosen enhancement title for a committed unlock row, or null if none selected.</summary>
    public static string TryResolveCommittedEnhancementTitle(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        SkillsManager skillsManager)
    {
        if (skill == null || unlock == null)
            return null;

        string spineId = ResolveSpineNodeIdForUnlock(skill, unlock);
        int choiceIndex = skillsManager != null && !string.IsNullOrEmpty(spineId)
            ? skillsManager.GetSkillChoiceSelection(skill.skillType, spineId, -1)
            : -1;

        return ResolveEnhancementTitle(unlock, choiceIndex);
    }

    /// <summary>Passive name with optional green enhancement label for list rows.</summary>
    public static string FormatPassiveNameWithEnhancement(
        string passiveName,
        string enhancementTitle,
        string enhancementColorHex = "#55DD55")
    {
        string name = string.IsNullOrWhiteSpace(passiveName) ? "Major Passive" : passiveName.Trim();
        if (string.IsNullOrWhiteSpace(enhancementTitle))
            return EscapeTmpRichText(name);

        string color = string.IsNullOrWhiteSpace(enhancementColorHex) ? "#55DD55" : enhancementColorHex.Trim();
        return $"{EscapeTmpRichText(name)}  <color={color}>{EscapeTmpRichText(enhancementTitle.Trim())}</color>";
    }

    private static string ResolveEnhancementTitle(SkillUnlockDefinition unlock, int choiceIndex)
    {
        if (unlock?.choices == null || choiceIndex < 0 || choiceIndex >= unlock.choices.Count)
            return null;

        SkillChoiceDefinition choice = unlock.choices[choiceIndex];
        if (choice == null)
            return null;

        string t = SkillsAbilityPresentationResolver.ResolveChoiceTitle(choice);
        return string.IsNullOrWhiteSpace(t) ? null : t.Trim();
    }

    private static string EscapeTmpRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>Spine id <c>Lv{level}_{slot}</c> for a unlock row (matches <see cref="SkillTreeViewUI"/>).</summary>
    public static string ResolveSpineNodeIdForUnlock(SkillDefinition skill, SkillUnlockDefinition target)
    {
        if (skill?.unlocks == null || target == null)
            return null;

        int level = Mathf.Max(1, target.requiredLevel);
        var sorted = BuildSortedUnlockRefs(skill);
        int slot = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            SkillUnlockDefinition u = sorted[i].u;
            if (u.requiredLevel != level)
                continue;

            if (ReferenceEquals(u, target) || UnlockRowsMatch(u, target))
                return $"Lv{level}_{slot}";

            slot++;
        }

        return null;
    }

    private static bool UnlockRowsMatch(SkillUnlockDefinition a, SkillUnlockDefinition b)
    {
        if (a == b)
            return true;
        if (a == null || b == null)
            return false;

        return a.requiredLevel == b.requiredLevel
            && a.unlockType == b.unlockType
            && string.Equals(a.title, b.title, System.StringComparison.Ordinal);
    }

    private static System.Collections.Generic.List<(SkillUnlockDefinition u, int origIdx)> BuildSortedUnlockRefs(
        SkillDefinition skill)
    {
        var sorted = new System.Collections.Generic.List<(SkillUnlockDefinition u, int origIdx)>();
        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            if (skill.unlocks[i] != null)
                sorted.Add((skill.unlocks[i], i));
        }

        sorted.Sort((a, b) =>
        {
            int c = a.u.requiredLevel.CompareTo(b.u.requiredLevel);
            if (c != 0)
                return c;

            c = SkillTreeRowPickRules.TierHorizontalSortOrder(a.u.unlockType)
                .CompareTo(SkillTreeRowPickRules.TierHorizontalSortOrder(b.u.unlockType));
            if (c != 0)
                return c;

            return a.origIdx.CompareTo(b.origIdx);
        });

        return sorted;
    }
}
