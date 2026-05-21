using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standard rules for multi-option skill tree rows (2+ <see cref="SkillUnlockType.MajorPassive"/> or
/// <see cref="SkillUnlockType.Ability"/> siblings at the same level). The player commits exactly one
/// via <see cref="SkillsManager.SetSkillAbilityRowPick"/>; <see cref="SkillTreeViewUI"/> collapses the
/// chosen node onto the central spine and hides siblings until Reset Tree.
/// Row ordering matches <see cref="SkillTreeViewUI"/> / <see cref="SkillAbilityCommitRules"/> (level → unlock type → asset index).
/// </summary>
public static class SkillTreeRowPickRules
{
    public static int TierHorizontalSortOrder(SkillUnlockType t) =>
        SkillAbilityCommitRules.TierHorizontalSortOrderForUnlocks(t);

    /// <summary>Committed sibling index at <paramref name="requiredLevel"/>, or <paramref name="defaultValue"/>.</summary>
    public static int GetCommittedRowPick(
        SkillsManager sm,
        SkillType skillType,
        int requiredLevel,
        int defaultValue = -1,
        int maxOrdinalInclusive = 7)
    {
        if (sm == null)
            return defaultValue;

        int pick = sm.GetSkillAbilityRowPick(skillType, requiredLevel, defaultValue);
        if (pick < 0 || pick > maxOrdinalInclusive)
            return defaultValue;

        return pick;
    }

    /// <summary>
    /// Sorted unlock rows at one level that share a multi-pick group (major passives or abilities only).
    /// </summary>
    public static List<SkillUnlockDefinition> GetMultiPickSiblingsAtLevel(
        SkillDefinition skill,
        int requiredLevel,
        bool majorPassivesOnly)
    {
        var result = new List<SkillUnlockDefinition>();
        if (skill == null || skill.unlocks == null)
            return result;

        var sorted = new List<(SkillUnlockDefinition u, int origIdx)>();
        for (int i = 0; i < skill.unlocks.Count; i++)
            if (skill.unlocks[i] != null)
                sorted.Add((skill.unlocks[i], i));

        sorted.Sort((a, b) =>
        {
            int c = a.u.requiredLevel.CompareTo(b.u.requiredLevel);
            if (c != 0) return c;
            c = TierHorizontalSortOrder(a.u.unlockType).CompareTo(TierHorizontalSortOrder(b.u.unlockType));
            if (c != 0) return c;
            return a.origIdx.CompareTo(b.origIdx);
        });

        for (int i = 0; i < sorted.Count; i++)
        {
            SkillUnlockDefinition u = sorted[i].u;
            if (u.requiredLevel != requiredLevel)
                continue;

            if (majorPassivesOnly)
            {
                if (u.unlockType != SkillUnlockType.MajorPassive)
                    continue;
            }
            else
            {
                bool abilityLike =
                    u.unlockType == SkillUnlockType.Ability ||
                    u.unlockType == SkillUnlockType.CapstonePassive;
                if (!abilityLike || u.ability == null)
                    continue;
            }

            result.Add(u);
        }

        return result;
    }

    /// <summary>
    /// Spine id <c>Lv{level}_{slot}</c> for the committed row pick at this level, or null if none / invalid.
    /// </summary>
    public static bool TryGetCommittedSpineNodeId(
        SkillDefinition skill,
        SkillsManager sm,
        int requiredLevel,
        bool majorPassivesOnly,
        out string spineNodeId)
    {
        spineNodeId = null;
        if (skill == null || sm == null)
            return false;

        var siblings = GetMultiPickSiblingsAtLevel(skill, requiredLevel, majorPassivesOnly);
        if (siblings.Count < 2)
            return false;

        int pick = GetCommittedRowPick(sm, skill.skillType, requiredLevel, -1, siblings.Count - 1);
        if (pick < 0 || pick >= siblings.Count)
            return false;

        int slot = 0;
        var sorted = new List<(SkillUnlockDefinition u, int origIdx)>();
        for (int i = 0; i < skill.unlocks.Count; i++)
            if (skill.unlocks[i] != null)
                sorted.Add((skill.unlocks[i], i));
        sorted.Sort((a, b) =>
        {
            int c = a.u.requiredLevel.CompareTo(b.u.requiredLevel);
            if (c != 0) return c;
            c = TierHorizontalSortOrder(a.u.unlockType).CompareTo(TierHorizontalSortOrder(b.u.unlockType));
            if (c != 0) return c;
            return a.origIdx.CompareTo(b.origIdx);
        });

        SkillUnlockDefinition target = siblings[pick];
        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].u != target)
                continue;

            if (sorted[i].u.requiredLevel != requiredLevel)
                return false;

            for (int j = 0; j <= i; j++)
            {
                if (sorted[j].u.requiredLevel == requiredLevel)
                    slot++;
            }

            slot--;
            spineNodeId = $"Lv{requiredLevel}_{slot}";
            return true;
        }

        return false;
    }

    /// <summary>Row-pick ordinal (0..n-1) for a major-passive spine at this level, or -1.</summary>
    public static int GetMajorPassiveRowPickOrdinal(
        SkillDefinition skill,
        SkillsManager sm,
        int requiredLevel,
        string parentSpineNodeId)
    {
        if (skill == null || sm == null || string.IsNullOrWhiteSpace(parentSpineNodeId))
            return -1;

        var siblings = GetMultiPickSiblingsAtLevel(skill, requiredLevel, majorPassivesOnly: true);
        if (siblings.Count < 2)
            return -1;

        if (!TryParseSpineSlot(parentSpineNodeId, out int level, out int slot) || level != requiredLevel)
            return -1;

        int ordinal = 0;
        var sorted = BuildSortedUnlockRefs(skill);
        for (int i = 0; i < sorted.Count; i++)
        {
            SkillUnlockDefinition u = sorted[i].u;
            if (u.requiredLevel != requiredLevel || u.unlockType != SkillUnlockType.MajorPassive)
                continue;

            int s = CountSlotAtLevel(sorted, i, requiredLevel);
            if (s == slot)
                return ordinal;
            ordinal++;
        }

        return -1;
    }

    public static bool IsMajorPassiveRowActive(
        SkillDefinition skill,
        SkillsManager sm,
        int requiredLevel,
        int majorOrdinal)
    {
        if (skill == null || sm == null)
            return false;

        var siblings = GetMultiPickSiblingsAtLevel(skill, requiredLevel, majorPassivesOnly: true);
        if (siblings.Count < 2)
            return siblings.Count == 1 && majorOrdinal == 0;

        return GetCommittedRowPick(sm, skill.skillType, requiredLevel, -1, siblings.Count - 1) == majorOrdinal;
    }

    private static List<(SkillUnlockDefinition u, int origIdx)> BuildSortedUnlockRefs(SkillDefinition skill)
    {
        var sorted = new List<(SkillUnlockDefinition u, int origIdx)>();
        for (int i = 0; i < skill.unlocks.Count; i++)
            if (skill.unlocks[i] != null)
                sorted.Add((skill.unlocks[i], i));

        sorted.Sort((a, b) =>
        {
            int c = a.u.requiredLevel.CompareTo(b.u.requiredLevel);
            if (c != 0) return c;
            c = TierHorizontalSortOrder(a.u.unlockType).CompareTo(TierHorizontalSortOrder(b.u.unlockType));
            if (c != 0) return c;
            return a.origIdx.CompareTo(b.origIdx);
        });

        return sorted;
    }

    private static int CountSlotAtLevel(List<(SkillUnlockDefinition u, int origIdx)> sorted, int index, int level)
    {
        int slot = 0;
        for (int i = 0; i <= index; i++)
        {
            if (sorted[i].u.requiredLevel == level)
                slot++;
        }

        return slot - 1;
    }

    private static bool TryParseSpineSlot(string spineNodeId, out int level, out int slot)
    {
        level = 0;
        slot = 0;
        if (string.IsNullOrWhiteSpace(spineNodeId))
            return false;

        int us = spineNodeId.IndexOf('_');
        if (us <= 2 || us >= spineNodeId.Length - 1)
            return false;
        if (!spineNodeId.StartsWith("Lv", System.StringComparison.Ordinal))
            return false;

        string lvlPart = spineNodeId.Substring(2, us - 2);
        string slotPart = spineNodeId.Substring(us + 1);
        return int.TryParse(lvlPart, out level) && int.TryParse(slotPart, out slot);
    }
}
