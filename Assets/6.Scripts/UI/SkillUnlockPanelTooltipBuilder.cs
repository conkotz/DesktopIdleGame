using System.Text;
using UnityEngine;

/// <summary>
/// Tooltip copy for major passive / capstone rows on the Skills &amp; Abilities right panel.
/// </summary>
public static class SkillUnlockPanelTooltipBuilder
{
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
            body = BuildCapstoneOrFallbackBody(unlock, choiceIndex);
            return !string.IsNullOrWhiteSpace(body);
        }

        if (skill.skillType == SkillType.Melee
            && !string.IsNullOrEmpty(spineId)
            && MeleeMajorPassiveTooltipText.TryBuildSkillTreeBody(spineId, choiceIndex, out string meleeBody))
        {
            body = meleeBody;
            return true;
        }

        string majorTitle = title.Trim();
        string enhancementTitle = ResolveEnhancementTitle(unlock, choiceIndex);
        if (GatheringPassiveTooltipText.TryBuildSkillTreeMajorPassiveBody(
                skill.skillType, majorTitle, enhancementTitle, out string gatheringBody))
        {
            body = gatheringBody;
            return true;
        }

        body = BuildCapstoneOrFallbackBody(unlock, choiceIndex);
        return !string.IsNullOrWhiteSpace(body);
    }

    private static string BuildCapstoneOrFallbackBody(SkillUnlockDefinition unlock, int choiceIndex)
    {
        var sb = new StringBuilder();
        string desc = unlock != null
            ? SkillsAbilityPresentationResolver.ResolveUnlockDescription(unlock)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(desc))
            sb.AppendLine(desc.Trim());

        string enhancementTitle = ResolveEnhancementTitle(unlock, choiceIndex);
        if (!string.IsNullOrWhiteSpace(enhancementTitle))
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(enhancementTitle.Trim());
            sb.AppendLine(" (Enhancement)");
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
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
