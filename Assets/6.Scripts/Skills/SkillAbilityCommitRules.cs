using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared rules for when an ability counts as "committed" on the skill tree (row pick among siblings).
/// Used by the abilities panel, action bar, and <see cref="PlayerAbilityController"/>.
/// </summary>
public static class SkillAbilityCommitRules
{
    /// <summary>
    /// Matches <see cref="SkillTreeViewUI"/> row ordering: multiple Ability unlocks at the same level require a row pick before listing in the panel.
    /// </summary>
    public static int TierHorizontalSortOrderForUnlocks(SkillUnlockType t)
    {
        return t switch
        {
            SkillUnlockType.MinorPassive => 0,
            SkillUnlockType.MajorPassive => 1,
            SkillUnlockType.Unlock => 2,
            SkillUnlockType.Ability => 3,
            SkillUnlockType.CapstonePassive => 4,
            _ => 99
        };
    }

    public static SkillUnlockDefinition FindAbilityUnlockOnSkill(SkillDefinition skill, AbilityDefinition ability)
    {
        if (skill == null || skill.unlocks == null || ability == null)
            return null;

        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition u = skill.unlocks[i];
            if (u == null || u.unlockType != SkillUnlockType.Ability || u.ability == null)
                continue;
            if (u.ability == ability)
                return u;
            if (!string.IsNullOrEmpty(ability.abilityId)
                && string.Equals(u.ability.abilityId, ability.abilityId, System.StringComparison.OrdinalIgnoreCase))
                return u;
        }

        return null;
    }

    public static List<AbilityDefinition> GetAbilitySiblingsOnSkillRow(SkillDefinition skill, int requiredLevel)
    {
        var result = new List<AbilityDefinition>();
        if (skill == null || skill.unlocks == null)
            return result;

        // Pair each unlock with its original index so we can fall back to that as a stable
        // tiebreaker. This must match SkillTreeViewUI.BuildRows ordering (level → unlockType
        // horizontal order → original asset index); otherwise the visual ordinal saved via
        // SetSkillAbilityRowPick won't match the index looked up here, and the wrong sibling
        // will be reported as the picked ability (e.g. picking Spectral Axe equips Cleaving
        // Chop because List<T>.Sort is not stable when the comparator returns 0).
        var sorted = new List<(SkillUnlockDefinition u, int origIdx)>();
        for (int i = 0; i < skill.unlocks.Count; i++)
            if (skill.unlocks[i] != null)
                sorted.Add((skill.unlocks[i], i));

        sorted.Sort((a, b) =>
        {
            int c = a.u.requiredLevel.CompareTo(b.u.requiredLevel);
            if (c != 0) return c;
            c = TierHorizontalSortOrderForUnlocks(a.u.unlockType).CompareTo(TierHorizontalSortOrderForUnlocks(b.u.unlockType));
            if (c != 0) return c;
            return a.origIdx.CompareTo(b.origIdx);
        });

        for (int i = 0; i < sorted.Count; i++)
        {
            SkillUnlockDefinition u = sorted[i].u;
            if (u.requiredLevel != requiredLevel || u.unlockType != SkillUnlockType.Ability || u.ability == null)
                continue;
            result.Add(u.ability);
        }

        return result;
    }

    /// <summary>
    /// Whether the abilities panel should list this ability (level already checked by caller).
    /// </summary>
    public static bool ShouldShowAbilityInRightPanel(SkillDefinition skill, AbilityDefinition ability, SkillsManager sm)
    {
        if (skill == null || ability == null)
            return true;
        if (sm == null)
            return true;

        SkillUnlockDefinition treeUnlock = FindAbilityUnlockOnSkill(skill, ability);
        if (treeUnlock == null)
            return true;

        int req = Mathf.Max(1, treeUnlock.requiredLevel);
        var siblings = GetAbilitySiblingsOnSkillRow(skill, req);
        if (siblings.Count == 0)
            return true;

        // Only one ability on this tier row: nothing to choose between — do not require a committed row pick.
        // (Otherwise pick stays -1 until the player clicks the node, and the right-hand abilities list stays empty.)
        if (siblings.Count == 1)
            return IndexOfAbilityInSiblingList(siblings, ability) == 0;

        int pick = sm.GetSkillAbilityRowPick(skill.skillType, req, -1);
        if (pick < 0)
            return false;

        int idx = IndexOfAbilityInSiblingList(siblings, ability);
        if (idx < 0)
            return false;

        return idx == pick;
    }

    /// <summary>
    /// Level requirement + skill-tree row commit (same bar as the right-hand abilities list).
    /// </summary>
    public static bool IsAbilityFullyUnlockedForGameplay(SkillDefinition skill, AbilityDefinition ability, SkillsManager sm)
    {
        if (ability == null)
            return false;
        if (sm == null)
            return true;

        if (!sm.IsLevelUnlocked(ability.sourceSkill, Mathf.Max(1, ability.unlockLevel)))
            return false;

        return ShouldShowAbilityInRightPanel(skill, ability, sm);
    }

    public static int IndexOfAbilityInSiblingList(List<AbilityDefinition> siblings, AbilityDefinition ability)
    {
        if (siblings == null || ability == null)
            return -1;
        for (int i = 0; i < siblings.Count; i++)
        {
            AbilityDefinition s = siblings[i];
            if (s == null)
                continue;
            if (s == ability)
                return i;
            if (!string.IsNullOrEmpty(ability.abilityId)
                && string.Equals(s.abilityId, ability.abilityId, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
