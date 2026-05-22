using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sorts and groups <see cref="SkillUnlockDefinition"/> rows for the horizontal timeline (display only).
/// Mirrors <see cref="SkillTreeViewUI"/> level / tier ordering.
/// </summary>
public static class HorizontalSkillTreeUnlockLayout
{
    public const float SameLevelSlotSpacing = 28f;

    public readonly struct SortedUnlock
    {
        public readonly SkillUnlockDefinition Unlock;
        public readonly int OriginalIndex;
        public readonly int Level;
        public readonly int SlotAtLevel;

        public SortedUnlock(SkillUnlockDefinition unlock, int originalIndex, int level, int slotAtLevel)
        {
            Unlock = unlock;
            OriginalIndex = originalIndex;
            Level = level;
            SlotAtLevel = slotAtLevel;
        }
    }

    public readonly struct LevelGroup
    {
        public readonly int Level;
        public readonly List<SortedUnlock> Unlocks;

        public LevelGroup(int level, List<SortedUnlock> unlocks)
        {
            Level = level;
            Unlocks = unlocks;
        }
    }

    public static List<SortedUnlock> BuildSortedUnlocks(IReadOnlyList<SkillUnlockDefinition> unlocks)
    {
        var sorted = new List<(SkillUnlockDefinition u, int origIdx)>();
        if (unlocks != null)
        {
            for (int i = 0; i < unlocks.Count; i++)
            {
                if (unlocks[i] != null)
                    sorted.Add((unlocks[i], i));
            }
        }

        sorted.Sort((a, b) =>
        {
            int c = a.u.requiredLevel.CompareTo(b.u.requiredLevel);
            if (c != 0)
                return c;
            c = SkillAbilityCommitRules.TierHorizontalSortOrderForUnlocks(a.u.unlockType)
                .CompareTo(SkillAbilityCommitRules.TierHorizontalSortOrderForUnlocks(b.u.unlockType));
            if (c != 0)
                return c;
            return a.origIdx.CompareTo(b.origIdx);
        });

        var slotAtLevel = new Dictionary<int, int>();
        var result = new List<SortedUnlock>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            SkillUnlockDefinition u = sorted[i].u;
            if (u.requiredLevel <= 0)
                continue;

            int lvl = u.requiredLevel;
            int slot = slotAtLevel.TryGetValue(lvl, out int s) ? s : 0;
            slotAtLevel[lvl] = slot + 1;
            result.Add(new SortedUnlock(u, sorted[i].origIdx, lvl, slot));
        }

        return result;
    }

    public static List<LevelGroup> GroupByLevel(List<SortedUnlock> sorted)
    {
        var groups = new List<LevelGroup>();
        if (sorted == null || sorted.Count == 0)
            return groups;

        int g = 0;
        while (g < sorted.Count)
        {
            int level = sorted[g].Level;
            var list = new List<SortedUnlock>();
            while (g < sorted.Count && sorted[g].Level == level)
            {
                list.Add(sorted[g]);
                g++;
            }

            groups.Add(new LevelGroup(level, list));
        }

        return groups;
    }

    public static float SlotAnchoredX(float milestoneX, int slot, int slotCount)
    {
        if (slotCount <= 1)
            return milestoneX;

        float center = (slotCount - 1) * 0.5f;
        return milestoneX + (slot - center) * SameLevelSlotSpacing;
    }

    /// <summary>
    /// Below-spine milestone names for one level. Matches vertical tree row-pick: sibling ability/major rows
    /// use unlock titles; <c>choices[]</c> with a later <c>requiredLevel</c> are enhancements, not extra picks.
    /// </summary>
    public static void CollectBelowSpineMilestoneNames(
        IReadOnlyList<SortedUnlock> levelUnlocks,
        SkillUnlockType bucketType,
        List<string> dest)
    {
        if (levelUnlocks == null || dest == null)
            return;

        var siblings = new List<SkillUnlockDefinition>();
        for (int i = 0; i < levelUnlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = levelUnlocks[i].Unlock;
            if (unlock != null && MatchesBelowSpineBucket(unlock.unlockType, bucketType))
                siblings.Add(unlock);
        }

        if (siblings.Count == 0)
            return;

        if (siblings.Count >= 2)
        {
            for (int i = 0; i < siblings.Count; i++)
                AddUnlockTitle(siblings[i], dest);
            return;
        }

        SkillUnlockDefinition single = siblings[0];
        if (!TryCollectSameLevelChoiceTitles(single, dest))
            AddUnlockTitle(single, dest);
    }

    private static bool MatchesBelowSpineBucket(SkillUnlockType unlockType, SkillUnlockType bucketType) =>
        bucketType == SkillUnlockType.CapstonePassive
            ? unlockType == SkillUnlockType.CapstonePassive
            : unlockType == bucketType;

    private static bool TryCollectSameLevelChoiceTitles(SkillUnlockDefinition unlock, List<string> dest)
    {
        if (unlock?.choices == null || unlock.choices.Count == 0)
            return false;

        int milestoneLevel = unlock.requiredLevel;
        var titles = new List<string>();
        for (int i = 0; i < unlock.choices.Count; i++)
        {
            SkillChoiceDefinition choice = unlock.choices[i];
            if (choice == null)
                continue;

            if (choice.requiredLevel > 0 && choice.requiredLevel != milestoneLevel)
                continue;

            string title = SkillsAbilityPresentationResolver.ResolveChoiceTitle(choice);
            if (!string.IsNullOrWhiteSpace(title))
                titles.Add(title);
        }

        if (titles.Count < SkillChoiceGroupUI.MinChoiceCount)
            return false;

        for (int i = 0; i < titles.Count; i++)
            dest.Add(titles[i]);

        return true;
    }

    private static void AddUnlockTitle(SkillUnlockDefinition unlock, List<string> dest)
    {
        string unlockTitle = SkillsAbilityPresentationResolver.ResolveTreeUnlockTitle(unlock);
        if (!string.IsNullOrWhiteSpace(unlockTitle))
            dest.Add(unlockTitle);
    }

    public static SkillTimelineNodeUI.SkillTimelineNodeType MapToTimelineNodeType(SkillUnlockType unlockType)
    {
        return unlockType switch
        {
            SkillUnlockType.MinorPassive => SkillTimelineNodeUI.SkillTimelineNodeType.MinorPassive,
            SkillUnlockType.Ability => SkillTimelineNodeUI.SkillTimelineNodeType.Ability,
            SkillUnlockType.Unlock => SkillTimelineNodeUI.SkillTimelineNodeType.Unlock,
            SkillUnlockType.MinorUnlock => SkillTimelineNodeUI.SkillTimelineNodeType.Unlock,
            SkillUnlockType.MajorPassive => SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive,
            SkillUnlockType.CapstonePassive => SkillTimelineNodeUI.SkillTimelineNodeType.Capstone,
            _ => SkillTimelineNodeUI.SkillTimelineNodeType.Ability
        };
    }

    public static bool IsAboveSpineType(SkillUnlockType unlockType) =>
        unlockType == SkillUnlockType.Unlock || unlockType == SkillUnlockType.MinorUnlock;

    public static bool IsSpineMinorType(SkillUnlockType unlockType) =>
        unlockType == SkillUnlockType.MinorPassive;

    public static bool IsBelowSpineChoiceType(SkillUnlockType unlockType) =>
        unlockType == SkillUnlockType.Ability ||
        unlockType == SkillUnlockType.MajorPassive ||
        unlockType == SkillUnlockType.CapstonePassive;

    private static int CountChoices(SkillUnlockDefinition unlock)
    {
        if (unlock?.choices == null)
            return 0;

        int count = 0;
        for (int i = 0; i < unlock.choices.Count; i++)
        {
            if (unlock.choices[i] != null)
                count++;
        }

        return count;
    }
}
