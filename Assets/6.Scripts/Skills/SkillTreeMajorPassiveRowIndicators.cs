using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mirrors <see cref="SkillTreeViewUI"/> not-selected / enhance prompts for committed major passive list rows.
/// </summary>
public static class SkillTreeMajorPassiveRowIndicators
{
    public static bool TryGet(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        SkillsManager sm,
        out bool showNotSelectedPrompt,
        out bool showEnhanceButton)
    {
        showNotSelectedPrompt = false;
        showEnhanceButton = false;

        if (skill == null || unlock == null || sm == null || unlock.unlockType != SkillUnlockType.MajorPassive)
            return false;

        int rowLevel = Mathf.Max(1, unlock.requiredLevel);
        if (sm.GetLevel(skill.skillType) < rowLevel)
            return false;

        string spineId = SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock(skill, unlock);
        if (string.IsNullOrEmpty(spineId))
            return false;

        List<SkillChoiceDefinition> choices = GetNonNullChoices(unlock);
        if (choices.Count <= 0)
            return false;

        List<SkillUnlockDefinition> siblings =
            SkillTreeRowPickRules.GetMultiPickSiblingsAtLevel(skill, rowLevel, majorPassivesOnly: true);

        if (siblings.Count >= 2)
        {
            int tierPick = SkillTreeRowPickRules.GetCommittedRowPick(
                sm, skill.skillType, rowLevel, -1, siblings.Count - 1);
            if (tierPick < 0)
                return false;

            int ord = GetSiblingOrdinal(siblings, unlock);
            if (ord < 0 || ord != tierPick)
                return false;
        }

        if (!IsEnhancementGateOpen(rowLevel, choices, sm.GetLevel(skill.skillType)))
            return false;

        bool pendingEnhancement = sm.GetSkillChoiceSelection(skill.skillType, spineId, -1) < 0;
        showNotSelectedPrompt = pendingEnhancement;
        // List rows use the not-selected prompt only; enhancement is picked on the skill tree.
        showEnhanceButton = false;
        return true;
    }

    private static bool IsEnhancementGateOpen(int sourceLevel, List<SkillChoiceDefinition> choices, int playerLevel)
    {
        int minGate = int.MaxValue;
        for (int i = 0; i < choices.Count; i++)
        {
            SkillChoiceDefinition c = choices[i];
            int gate = c != null && c.requiredLevel > 0 ? c.requiredLevel : sourceLevel;
            if (gate < minGate)
                minGate = gate;
        }

        return minGate != int.MaxValue && playerLevel >= minGate;
    }

    private static int GetSiblingOrdinal(List<SkillUnlockDefinition> siblings, SkillUnlockDefinition unlock)
    {
        if (siblings == null || unlock == null)
            return -1;

        for (int i = 0; i < siblings.Count; i++)
        {
            if (ReferenceEquals(siblings[i], unlock))
                return i;
        }

        return -1;
    }

    private static List<SkillChoiceDefinition> GetNonNullChoices(SkillUnlockDefinition unlock)
    {
        var result = new List<SkillChoiceDefinition>();
        if (unlock?.choices == null)
            return result;

        for (int i = 0; i < unlock.choices.Count; i++)
        {
            if (unlock.choices[i] != null)
                result.Add(unlock.choices[i]);
        }

        return result;
    }
}
