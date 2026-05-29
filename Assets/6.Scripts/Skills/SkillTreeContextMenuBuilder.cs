using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds context menu entries for horizontal skill timeline nodes.</summary>
public static class SkillTreeContextMenuBuilder
{
    public static List<ContextMenuEntry> BuildForTimelineNode(
        SkillTimelineNodeUI node,
        Action onView,
        Action onSwap)
    {
        var entries = new List<ContextMenuEntry>(2);
        if (!node || onView == null)
            return entries;

        entries.Add(new ContextMenuEntry("View", onView));

        if (onSwap != null && SkillTimelineRowSelectionRules.IsSelectablePickNode(node.Binding))
            entries.Add(new ContextMenuEntry("Swap", onSwap));

        return entries;
    }

    public static string ResolveHeaderTitle(SkillTimelineNodeBinding binding)
    {
        if (binding == null)
            return string.Empty;

        SkillsManager skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            skillsManager = UnityEngine.Object.FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);

        if (SkillTreeNodeTooltipFormatter.TryBuildForTimelineNode(binding, skillsManager, out SkillTreeNodeTooltipFormatter.DetailsContent details)
            && !string.IsNullOrWhiteSpace(details.Title))
        {
            return details.Title;
        }

        if (binding.IsChoiceNode && binding.Choice != null)
        {
            string choiceTitle = SkillsAbilityPresentationResolver.ResolveChoiceTitle(binding.Choice);
            if (!string.IsNullOrWhiteSpace(choiceTitle))
                return choiceTitle;
        }

        if (binding.Unlock != null)
        {
            string unlockTitle = SkillsAbilityPresentationResolver.ResolveTreeUnlockTitle(binding.Unlock);
            if (!string.IsNullOrWhiteSpace(unlockTitle))
                return unlockTitle;
        }

        return string.Empty;
    }
}
