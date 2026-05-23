using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Row-pick and enhancement-choice rules for the horizontal timeline (mirrors <see cref="SkillTreeViewUI"/>).
/// </summary>
public static class SkillTimelineRowSelectionRules
{
    public static bool IsSelectablePickNode(SkillTimelineNodeBinding binding)
    {
        if (binding == null || binding.Skill == null || binding.Unlock == null)
            return false;

        if (binding.DisplayState == SkillTimelineNodeUI.SkillTimelineNodeState.Locked)
            return false;

        return binding.TimelineNodeType switch
        {
            SkillTimelineNodeUI.SkillTimelineNodeType.Ability => true,
            SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive => true,
            SkillTimelineNodeUI.SkillTimelineNodeType.Capstone => true,
            _ => false
        };
    }

    public static bool IsRowPickGroup(IReadOnlyList<SkillTimelineNodeBinding> bindings)
    {
        if (bindings == null || bindings.Count < SkillChoiceGroupUI.MinChoiceCount)
            return false;

        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i] == null || bindings[i].IsChoiceNode)
                return false;
        }

        return true;
    }

    public static bool IsEnhancementChoiceGroup(IReadOnlyList<SkillTimelineNodeBinding> bindings)
    {
        if (bindings == null || bindings.Count < SkillChoiceGroupUI.MinChoiceCount)
            return false;

        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i] == null || !bindings[i].IsChoiceNode)
                return false;
        }

        return true;
    }

    public static void CommitSelection(SkillsManager skillsManager, SkillTimelineNodeBinding binding)
    {
        if (skillsManager == null || binding?.Skill == null)
            return;

        if (binding.IsChoiceNode)
        {
            string parentSpine = binding.ResolveSpineNodeId();
            if (string.IsNullOrWhiteSpace(parentSpine))
                return;

            int choiceIndex = binding.ChoiceAssetIndex >= 0 ? binding.ChoiceAssetIndex : binding.SlotAtLevel;
            skillsManager.SetSkillChoiceSelection(binding.Skill.skillType, parentSpine, choiceIndex);
            return;
        }

        skillsManager.SetSkillAbilityRowPick(binding.Skill.skillType, binding.Level, binding.SlotAtLevel);
        ClearEnhancementChoicesForRowSiblings(binding.Skill, binding.Unlock, binding.Level, binding.TimelineNodeType, skillsManager);
    }

    /// <summary>Clears committed enhancement picks on other row options when a new ability/major/capstone is selected.</summary>
    public static void ClearEnhancementChoicesForRowSiblings(
        SkillDefinition skill,
        SkillUnlockDefinition selectedUnlock,
        int level,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillsManager skillsManager)
    {
        if (skill == null || selectedUnlock == null || skillsManager == null)
            return;

        bool majorPassivesOnly = nodeType == SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive;
        System.Collections.Generic.List<SkillUnlockDefinition> siblings =
            SkillTreeRowPickRules.GetMultiPickSiblingsAtLevel(skill, level, majorPassivesOnly);

        if (siblings.Count < 2)
            return;

        for (int i = 0; i < siblings.Count; i++)
        {
            SkillUnlockDefinition sibling = siblings[i];
            if (sibling == null || ReferenceEquals(sibling, selectedUnlock))
                continue;

            string spineId = SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock(skill, sibling);
            if (string.IsNullOrWhiteSpace(spineId))
                continue;

            skillsManager.ClearSkillChoiceSelectionForParentSpine(skill.skillType, spineId);
        }
    }

    public static bool IsNodeCommittedSelected(SkillsManager skillsManager, SkillTimelineNodeBinding binding)
    {
        if (skillsManager == null || binding?.Skill == null)
            return false;

        if (binding.IsChoiceNode)
        {
            string parentSpine = binding.ResolveSpineNodeId();
            if (string.IsNullOrWhiteSpace(parentSpine))
                return false;

            int choiceIndex = binding.ChoiceAssetIndex >= 0 ? binding.ChoiceAssetIndex : binding.SlotAtLevel;
            int selected = skillsManager.GetSkillChoiceSelection(binding.Skill.skillType, parentSpine, -1);
            return selected == choiceIndex;
        }

        int pick = skillsManager.GetSkillAbilityRowPick(binding.Skill.skillType, binding.Level, -1);
        return pick == binding.SlotAtLevel;
    }

    public static bool ShouldShowNotSelectedPrompt(
        SkillsManager skillsManager,
        SkillTimelineNodeBinding binding,
        IReadOnlyList<SkillTimelineNodeBinding> groupBindings)
    {
        if (!IsSelectablePickNode(binding) || skillsManager == null)
            return false;

        if (IsEnhancementChoiceGroup(groupBindings))
            return ShouldShowEnhancementChoiceNotSelected(skillsManager, binding);

        if (IsRowPickGroup(groupBindings))
            return ShouldShowRowPickNotSelected(skillsManager, binding, groupBindings);

        return ShouldShowSingleUnlockNotSelected(skillsManager, binding);
    }

    private static bool ShouldShowEnhancementChoiceNotSelected(
        SkillsManager skillsManager,
        SkillTimelineNodeBinding binding)
    {
        string parentSpine = binding.ResolveSpineNodeId();
        if (string.IsNullOrWhiteSpace(parentSpine))
            return false;

        int selected = skillsManager.GetSkillChoiceSelection(binding.Skill.skillType, parentSpine, -1);
        return selected < 0;
    }

    private static bool ShouldShowRowPickNotSelected(
        SkillsManager skillsManager,
        SkillTimelineNodeBinding binding,
        IReadOnlyList<SkillTimelineNodeBinding> groupBindings)
    {
        int pick = skillsManager.GetSkillAbilityRowPick(binding.Skill.skillType, binding.Level, -1);
        if (pick < 0)
            return true;

        if (pick != binding.SlotAtLevel)
            return false;

        return ShouldShowPendingEnhancementChoice(skillsManager, binding);
    }

    private static bool ShouldShowSingleUnlockNotSelected(SkillsManager skillsManager, SkillTimelineNodeBinding binding)
    {
        string spineId = binding.ResolveSpineNodeId();
        if (string.IsNullOrWhiteSpace(spineId))
            return false;

        int pick = skillsManager.GetSkillAbilityRowPick(binding.Skill.skillType, binding.Level, -1);
        if (pick < 0)
            return true;

        if (pick != binding.SlotAtLevel)
            return false;

        return ShouldShowPendingEnhancementChoice(skillsManager, binding);
    }

    private static bool ShouldShowPendingEnhancementChoice(SkillsManager skillsManager, SkillTimelineNodeBinding binding)
    {
        if (binding?.Unlock == null)
            return false;

        List<SkillChoiceDefinition> choices = GetNonNullChoices(binding.Unlock);
        if (choices.Count == 0)
            return false;

        string spineId = binding.ResolveSpineNodeId();
        if (string.IsNullOrWhiteSpace(spineId))
            return false;

        int playerLv = skillsManager.GetLevel(binding.Skill.skillType);
        int minGate = int.MaxValue;
        for (int i = 0; i < choices.Count; i++)
        {
            int gate = ResolveChoiceUnlockLevel(binding.Level, binding.TimelineNodeType, choices[i]);
            if (gate < minGate)
                minGate = gate;
        }

        if (minGate != int.MaxValue && playerLv < minGate)
            return false;

        return skillsManager.GetSkillChoiceSelection(binding.Skill.skillType, spineId, -1) < 0;
    }

    private static List<SkillChoiceDefinition> GetNonNullChoices(SkillUnlockDefinition unlock)
    {
        var list = new List<SkillChoiceDefinition>();
        if (unlock?.choices == null)
            return list;

        for (int i = 0; i < unlock.choices.Count; i++)
        {
            if (unlock.choices[i] != null)
                list.Add(unlock.choices[i]);
        }

        return list;
    }

    private static int ResolveChoiceUnlockLevel(
        int sourceLevel,
        SkillTimelineNodeUI.SkillTimelineNodeType sourceType,
        SkillChoiceDefinition choice)
    {
        if (choice == null)
            return sourceLevel;

        if (choice.requiredLevel > 0)
            return choice.requiredLevel;

        return sourceLevel;
    }
}
