using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Shared tooltip / details copy for skill-tree nodes (vertical hover + horizontal selection panel).
/// </summary>
public static class SkillTreeNodeTooltipFormatter
{
    private const string ActiveEnhancementColorHex = "#33CC66";

    public struct DetailsContent
    {
        public string Title;
        public string TypeLabel;
        public string LevelText;
        public string StatusRichText;
        public string Description;
        /// <summary>Major passive stat/effect lines (details panel middle column).</summary>
        public string EffectText;
        /// <summary>Major passive scaling lines (details panel middle column).</summary>
        public string ScalingText;
        public string ActiveEnhancement;
        public string EnhancementsTitle;
        public string EnhancementsList;
        public string RequirementsText;
        public Sprite Icon;
        public bool HasContent;
    }

    public static bool TryBuildForTimelineNode(
        SkillTimelineNodeBinding binding,
        SkillsManager skillsManager,
        out DetailsContent content)
    {
        content = default;
        if (binding?.Skill == null || binding.Unlock == null)
            return false;

        if (binding.IsChoiceNode)
            return TryBuildChoice(binding, skillsManager, out content);

        return TryBuildMain(binding, skillsManager, out content);
    }

    public static bool TryBuildTooltip(
        SkillTimelineNodeBinding binding,
        SkillsManager skillsManager,
        out string title,
        out string body)
    {
        title = null;
        body = null;
        if (!TryBuildForTimelineNode(binding, skillsManager, out DetailsContent content) || !content.HasContent)
            return false;

        title = content.Title;
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(content.TypeLabel))
            sb.Append(content.TypeLabel);
        if (!string.IsNullOrWhiteSpace(content.StatusRichText))
            sb.Append(sb.Length > 0 ? " " : string.Empty).Append(content.StatusRichText);
        if (!string.IsNullOrWhiteSpace(content.RequirementsText))
            sb.Append('\n').Append(content.RequirementsText);
        if (!string.IsNullOrWhiteSpace(content.Description))
            sb.Append("\n\n").Append(content.Description);
        if (!string.IsNullOrWhiteSpace(content.EffectText))
            sb.Append("\n\n").Append(content.EffectText);
        if (!string.IsNullOrWhiteSpace(content.ActiveEnhancement))
            sb.Append('\n').Append(content.ActiveEnhancement);
        if (!string.IsNullOrWhiteSpace(content.EnhancementsTitle) || !string.IsNullOrWhiteSpace(content.EnhancementsList))
        {
            if (!string.IsNullOrWhiteSpace(content.EnhancementsTitle))
                sb.Append("\n\n").Append(content.EnhancementsTitle);
            if (!string.IsNullOrWhiteSpace(content.EnhancementsList))
                sb.Append(content.EnhancementsList);
        }

        body = sb.ToString();
        return !string.IsNullOrWhiteSpace(title);
    }

    private static bool TryBuildMain(
        SkillTimelineNodeBinding binding,
        SkillsManager skillsManager,
        out DetailsContent content)
    {
        SkillDefinition skill = binding.Skill;
        SkillUnlockDefinition unlock = binding.Unlock;
        int level = binding.Level > 0 ? binding.Level : Mathf.Max(1, unlock.requiredLevel);
        SkillTreeNodeVisualType visualType = MapTimelineType(binding.TimelineNodeType, unlock);
        bool isUnlocked = IsLevelUnlocked(skill, skillsManager, level);

        string unlockTitle = SkillsAbilityPresentationResolver.ResolveTreeUnlockTitle(unlock);
        if (string.IsNullOrWhiteSpace(unlockTitle))
            unlockTitle = "Untitled";

        string desc = ResolveMainDescription(binding, visualType);
        bool useMajorPassivePresentation = visualType == SkillTreeNodeVisualType.MajorPassive
                                           || visualType == SkillTreeNodeVisualType.CapstonePassive;
        string effectText = null;
        string scalingText = null;
        if (useMajorPassivePresentation)
        {
            effectText = desc;
            if (skill != null && unlock != null && unlock.unlockType == SkillUnlockType.MajorPassive)
            {
                string spineId = binding.ResolveSpineNodeId();
                int selectedChoice = skillsManager != null && !string.IsNullOrEmpty(spineId)
                    ? skillsManager.GetSkillChoiceSelection(skill.skillType, spineId, -1)
                    : -1;
                CharacterStats stats = AbilityTooltipDamagePreview.FindLocalPlayerStats();
                if (skill.skillType == SkillType.Ranged
                    && RangedMajorPassiveTooltipText.TryBuildDetailsPanelSections(
                        spineId, selectedChoice, stats, out string rangedScaling, out string rangedEffect))
                {
                    scalingText = rangedScaling;
                    effectText = rangedEffect;
                }
            }

            if (visualType != SkillTreeNodeVisualType.CapstonePassive && !string.IsNullOrWhiteSpace(effectText))
                effectText = ApplyMajorPassiveValueLineMarkup(skill, effectText);
            desc = ResolveMajorPassiveFlavorDescription(skill, unlock, binding.ResolveSpineNodeId(), effectText);

            if (visualType == SkillTreeNodeVisualType.CapstonePassive && skill != null)
            {
                string spineId = binding.ResolveSpineNodeId();
                int selectedChoice = skillsManager != null && !string.IsNullOrEmpty(spineId)
                    ? skillsManager.GetSkillChoiceSelection(skill.skillType, spineId, -1)
                    : -1;
                if (selectedChoice >= 0
                    && MeleeMajorPassiveTooltipText.TryBuildCapstoneChoiceBody(selectedChoice, out string choiceEffect)
                    && !string.IsNullOrWhiteSpace(choiceEffect))
                {
                    effectText = ApplyMajorPassiveValueLineMarkup(skill, choiceEffect);
                }
            }
        }

        string typeLabel = visualType == SkillTreeNodeVisualType.CapstonePassive
            ? TypeLabel(SkillTreeNodeVisualType.CapstonePassive)
            : useMajorPassivePresentation
                ? TypeLabel(SkillTreeNodeVisualType.MajorPassive)
                : TypeLabel(visualType);

        content = new DetailsContent
        {
            Title = unlockTitle,
            TypeLabel = typeLabel,
            LevelText = $"Lv {level}",
            StatusRichText = BuildStatusRichText(binding.DisplayState, isUnlocked),
            Description = desc,
            EffectText = effectText,
            ScalingText = scalingText,
            RequirementsText = BuildRequirementsText(level, isUnlocked, binding.DisplayState, unlock.unlockType),
            Icon = ResolveIcon(unlock, skill, binding.Choice),
            HasContent = true
        };

        AppendEnhancementSections(skill, unlock, skillsManager, binding.ResolveSpineNodeId(), ref content);
        return true;
    }

    private static bool TryBuildChoice(
        SkillTimelineNodeBinding binding,
        SkillsManager skillsManager,
        out DetailsContent content)
    {
        SkillDefinition skill = binding.Skill;
        SkillUnlockDefinition parentUnlock = binding.Unlock;
        SkillChoiceDefinition choice = binding.Choice;
        int choiceAssetIndex = binding.ChoiceAssetIndex;
        int unlockLevel = ResolveChoiceUnlockLevel(binding.Level, choice, parentUnlock);
        bool isUnlocked = IsLevelUnlocked(skill, skillsManager, unlockLevel);

        string unlockTitle;
        if (choice != null)
        {
            string ct = SkillsAbilityPresentationResolver.ResolveChoiceTitle(choice);
            unlockTitle = !string.IsNullOrWhiteSpace(ct)
                ? ct
                : SkillsAbilityPresentationResolver.ResolveUnlockTitle(parentUnlock);
        }
        else
        {
            unlockTitle = SkillsAbilityPresentationResolver.ResolveUnlockTitle(parentUnlock);
        }

        if (string.IsNullOrWhiteSpace(unlockTitle))
            unlockTitle = "Untitled";

        string desc;
        string spineId = binding.ResolveSpineNodeId();
        if (skill != null && parentUnlock != null && !string.IsNullOrEmpty(spineId))
        {
            if (skill.skillType == SkillType.Ranged
                && RangedMajorPassiveTooltipText.TryBuildChoiceTooltipBody(spineId, choiceAssetIndex, out string rangedChoiceBody))
            {
                desc = rangedChoiceBody;
            }
            else if (skill.skillType == SkillType.Melee
                && MeleeMajorPassiveTooltipText.TryBuildChoiceTooltipBody(spineId, choiceAssetIndex, out string meleeChoiceBody))
            {
                desc = meleeChoiceBody;
            }
            else
            {
                desc = choice != null
                    ? SkillsAbilityPresentationResolver.ResolveChoiceDescription(choice)
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = "No description yet.";
            }
        }
        else
        {
            desc = choice != null
                ? SkillsAbilityPresentationResolver.ResolveChoiceDescription(choice)
                : string.Empty;
            if (string.IsNullOrWhiteSpace(desc))
                desc = "No description yet.";
        }

        if (parentUnlock != null
            && (parentUnlock.unlockType == SkillUnlockType.MajorPassive
                || parentUnlock.unlockType == SkillUnlockType.CapstonePassive))
        {
            desc = ApplyMajorPassiveValueLineMarkup(skill, desc);
        }

        content = new DetailsContent
        {
            Title = unlockTitle,
            TypeLabel = TypeLabel(SkillTreeNodeVisualType.Choice),
            LevelText = $"Lv {unlockLevel}",
            StatusRichText = BuildStatusRichText(binding.DisplayState, isUnlocked),
            Description = desc,
            RequirementsText = BuildRequirementsText(
                unlockLevel,
                isUnlocked,
                binding.DisplayState,
                parentUnlock != null ? parentUnlock.unlockType : null),
            Icon = ResolveIcon(parentUnlock, skill, choice),
            HasContent = true
        };

        AppendEnhancementSections(skill, parentUnlock, skillsManager, spineId, ref content);
        return true;
    }

    private static string ResolveMainDescription(
        SkillTimelineNodeBinding binding,
        SkillTreeNodeVisualType visualType)
    {
        SkillDefinition skill = binding.Skill;
        SkillUnlockDefinition unlock = binding.Unlock;

        string desc;
        if (unlock != null && unlock.unlockType == SkillUnlockType.Ability && unlock.ability != null)
            desc = SkillsAbilityPresentationResolver.ResolveSkillTreeAbilityUnlockDescription(unlock);
        else if (unlock != null && !string.IsNullOrWhiteSpace(unlock.description))
            desc = unlock.description.Trim();
        else
            desc = "No description yet.";

        desc = BuildEffectiveGatheringMajorPassiveDescription(skill, unlock, binding.Level, desc);

        if (unlock != null && unlock.unlockType == SkillUnlockType.Ability && unlock.ability != null)
        {
            desc = ApplyMajorPassiveValueLineMarkup(skill, desc);
            desc = ApplyAbilityDescriptionStrips(skill, unlock, desc);
        }

        return desc;
    }

    private static string ApplyAbilityDescriptionStrips(SkillDefinition skill, SkillUnlockDefinition unlock, string desc)
    {
        if (skill == null || unlock?.ability == null || string.IsNullOrWhiteSpace(desc))
            return desc;

        string aid = unlock.ability.abilityId;
        if (skill.skillType == SkillType.Woodcutting)
        {
            if (string.Equals(aid, AbilityCombatPower.LumberFrenzyAbilityId, StringComparison.OrdinalIgnoreCase))
                return StripWoodcuttingLumberFrenzyTreeDescriptionDuration(desc);
            if (string.Equals(aid, AbilityCombatPower.AvatarOfTheForestAbilityId, StringComparison.OrdinalIgnoreCase))
                return StripAvatarTreeDescriptionLongWording(desc);
        }

        if (skill.skillType == SkillType.Fishing &&
            string.Equals(aid, AbilityCombatPower.FishingFrenzyAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            return StripWoodcuttingLumberFrenzyTreeDescriptionDuration(desc);
        }

        return desc;
    }

    private static string StripWoodcuttingLumberFrenzyTreeDescriptionDuration(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc))
            return desc;

        string d = desc;
        d = d.Replace(" for 20 seconds.", ".", StringComparison.OrdinalIgnoreCase);
        d = d.Replace(" for 20 seconds,", ",", StringComparison.OrdinalIgnoreCase);
        d = d.Replace(" for 20 seconds", string.Empty, StringComparison.OrdinalIgnoreCase);
        while (d.Contains("..", StringComparison.Ordinal))
            d = d.Replace("..", ".", StringComparison.Ordinal);
        return d.Trim();
    }

    private static string StripAvatarTreeDescriptionLongWording(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc))
            return desc;

        return desc.Replace("for a long woodcutting surge", "for a woodcutting surge", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendEnhancementSections(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        SkillsManager skillsManager,
        string spineId,
        ref DetailsContent content)
    {
        if (unlock?.choices == null || unlock.choices.Count == 0 || skill == null)
            return;

        int selectedChoice = skillsManager != null && !string.IsNullOrEmpty(spineId)
            ? skillsManager.GetSkillChoiceSelection(skill.skillType, spineId, -1)
            : -1;

        string activeLine = SkillUnlockPanelTooltipBuilder.FormatActiveEnhancementLine(unlock, selectedChoice);
        if (!string.IsNullOrEmpty(activeLine))
            content.ActiveEnhancement = activeLine.TrimStart();

        var sb = new StringBuilder();
        for (int i = 0; i < unlock.choices.Count; i++)
        {
            SkillChoiceDefinition choice = unlock.choices[i];
            if (choice == null)
                continue;

            string choiceName = !string.IsNullOrWhiteSpace(choice.title)
                ? choice.title.Trim()
                : $"Option {i + 1}";
            bool isSelected = i == selectedChoice;
            sb.Append('\n');
            sb.Append(isSelected ? "<color=#33CC66>• [Selected] " : "• ");
            sb.Append(choiceName);
            if (isSelected)
                sb.Append("</color>");
        }

        content.EnhancementsTitle = "Enhancements:";
        content.EnhancementsList = sb.ToString();
    }

    private static string BuildEffectiveGatheringMajorPassiveDescription(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        int level,
        string fallbackDescription)
    {
        if (skill == null || unlock == null)
            return fallbackDescription;

        int selectedChoice = -1;
        string selectedChoiceTitle = string.Empty;
        string spineId = SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock(skill, unlock);
        if (!string.IsNullOrEmpty(spineId) && SkillsManager.Instance != null)
        {
            selectedChoice = SkillsManager.Instance.GetSkillChoiceSelection(skill.skillType, spineId, -1);
            selectedChoiceTitle = GetChoiceTitle(unlock, selectedChoice);
        }

        string title = !string.IsNullOrWhiteSpace(unlock.title) ? unlock.title.Trim() : string.Empty;

        if ((skill.skillType == SkillType.Woodcutting && level == PlayerController.WoodcuttingMajorPassiveSourceLevel) ||
            (skill.skillType == SkillType.Fishing && level == PlayerController.FishingMajorPassiveSourceLevel))
        {
            if (GatheringPassiveTooltipText.TryBuildSkillTreeMajorPassiveBody(
                    skill.skillType, title, selectedChoiceTitle, out string majorBody))
                return majorBody;
        }

        if (skill.skillType == SkillType.Melee && unlock.unlockType == SkillUnlockType.MajorPassive)
        {
            if (MeleeMajorPassiveTooltipText.TryBuildSkillTreeBody(spineId, selectedChoice, out string meleeBody))
                return meleeBody;
        }

        if (skill.skillType == SkillType.Ranged && unlock.unlockType == SkillUnlockType.MajorPassive)
        {
            if (RangedMajorPassiveTooltipText.TryBuildSkillTreeBody(spineId, selectedChoice, out string rangedBody))
                return rangedBody;
        }

        if (skill.skillType == SkillType.Melee && unlock.unlockType == SkillUnlockType.CapstonePassive)
        {
            if (MeleeMajorPassiveTooltipText.TryBuildCapstoneBody(out string capstoneBody))
                return capstoneBody;
        }

        return fallbackDescription;
    }

    private static string ResolveMajorPassiveFlavorDescription(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        string spineId,
        string effectBody)
    {
        if (skill != null
            && skill.skillType == SkillType.Ranged
            && !string.IsNullOrEmpty(spineId)
            && RangedMajorPassiveTooltipText.TryBuildFlavorDescription(spineId, out string rangedFlavor))
        {
            return rangedFlavor;
        }

        if (skill != null
            && skill.skillType == SkillType.Melee
            && !string.IsNullOrEmpty(spineId)
            && MeleeMajorPassiveTooltipText.TryBuildFlavorDescription(spineId, out string meleeFlavor))
        {
            return meleeFlavor;
        }

        string assetDesc = unlock?.description?.Trim();
        if (string.IsNullOrWhiteSpace(assetDesc))
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(effectBody)
            && string.Equals(
                NormalizeMajorPassiveTooltipText(assetDesc),
                NormalizeMajorPassiveTooltipText(effectBody),
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (LooksLikeMajorPassiveEffectLine(assetDesc))
            return string.Empty;

        return assetDesc;
    }

    private static string NormalizeMajorPassiveTooltipText(string text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : text.Replace("\r\n", "\n").Trim();

    private static bool LooksLikeMajorPassiveEffectLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.TrimStart();
        if (trimmed.StartsWith("+", StringComparison.Ordinal)
            || trimmed.StartsWith("Gain ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Poison ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Burning ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("On death", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Each ability", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Using an ability", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("While using", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("When wielding", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Parries ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.Contains("\n+", StringComparison.Ordinal)
               || trimmed.Contains("\nGain ", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRequirementsText(
        int level,
        bool isUnlocked,
        SkillTimelineNodeUI.SkillTimelineNodeState displayState,
        SkillUnlockType? unlockType = null)
    {
        if (isUnlocked || displayState == SkillTimelineNodeUI.SkillTimelineNodeState.Unlocked)
            return "Already unlocked";

        if (displayState == SkillTimelineNodeUI.SkillTimelineNodeState.Locked)
            return $"Requires Lv{level}";

        if (unlockType == SkillUnlockType.Unlock)
            return $"Unlocks at level {level}";

        return $"Unlocks at Lv{level}";
    }

    private static string BuildStatusRichText(
        SkillTimelineNodeUI.SkillTimelineNodeState displayState,
        bool isUnlocked)
    {
        if (displayState == SkillTimelineNodeUI.SkillTimelineNodeState.Selected)
            return "<color=#FFCC33>Selected</color>";

        if (displayState == SkillTimelineNodeUI.SkillTimelineNodeState.Locked)
            return "<color=#FF4D4D>Node LOCKED</color>";

        if (isUnlocked || displayState == SkillTimelineNodeUI.SkillTimelineNodeState.Unlocked)
            return "<color=#33CC66>Node Unlocked</color>";

        if (displayState == SkillTimelineNodeUI.SkillTimelineNodeState.Available)
            return "<color=#E8C050>Available</color>";

        return "<color=#FF4D4D>Node LOCKED</color>";
    }

    private static bool IsLevelUnlocked(SkillDefinition skill, SkillsManager skillsManager, int level)
    {
        if (skill == null || skillsManager == null)
            return false;

        return skillsManager.IsLevelUnlocked(skill.skillType, level);
    }

    private static int ResolveChoiceUnlockLevel(int bindingLevel, SkillChoiceDefinition choice, SkillUnlockDefinition parentUnlock)
    {
        if (parentUnlock?.unlockType == SkillUnlockType.CapstonePassive)
            return parentUnlock.requiredLevel > 0 ? parentUnlock.requiredLevel : 50;

        if (choice != null && choice.requiredLevel > 0)
            return choice.requiredLevel;

        if (bindingLevel > 0)
            return bindingLevel;

        return parentUnlock != null ? Mathf.Max(1, parentUnlock.requiredLevel) : 1;
    }

    private static Sprite ResolveIcon(SkillUnlockDefinition unlock, SkillDefinition skill, SkillChoiceDefinition choice)
    {
        if (choice?.icon != null)
            return choice.icon;

        if (unlock == null)
            return null;

        if (unlock.icon != null)
            return unlock.icon;

        if (unlock.unlockType == SkillUnlockType.MinorPassive && skill?.icon != null)
            return skill.icon;

        if (unlock.ability != null)
        {
            Sprite abilityIcon = SkillsAbilityPresentationResolver.ResolveAbilityIcon(unlock.ability);
            if (abilityIcon != null)
                return abilityIcon;
        }

        return skill?.icon;
    }

    private static SkillTreeNodeVisualType MapTimelineType(
        SkillTimelineNodeUI.SkillTimelineNodeType timelineType,
        SkillUnlockDefinition unlock)
    {
        if (unlock != null)
        {
            return unlock.unlockType switch
            {
                SkillUnlockType.MinorPassive => SkillTreeNodeVisualType.MinorPassive,
                SkillUnlockType.MajorPassive => SkillTreeNodeVisualType.MajorPassive,
                SkillUnlockType.CapstonePassive => SkillTreeNodeVisualType.CapstonePassive,
                SkillUnlockType.MinorUnlock => SkillTreeNodeVisualType.MinorUnlock,
                SkillUnlockType.Unlock => SkillTreeNodeVisualType.Unlock,
                SkillUnlockType.Ability => SkillTreeNodeVisualType.Ability,
                _ => SkillTreeNodeVisualType.Ability
            };
        }

        return timelineType switch
        {
            SkillTimelineNodeUI.SkillTimelineNodeType.MinorPassive => SkillTreeNodeVisualType.MinorPassive,
            SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive => SkillTreeNodeVisualType.MajorPassive,
            SkillTimelineNodeUI.SkillTimelineNodeType.Capstone => SkillTreeNodeVisualType.CapstonePassive,
            SkillTimelineNodeUI.SkillTimelineNodeType.Unlock => SkillTreeNodeVisualType.Unlock,
            _ => SkillTreeNodeVisualType.Ability
        };
    }

    private static string TypeLabel(SkillTreeNodeVisualType type) =>
        type switch
        {
            SkillTreeNodeVisualType.MinorPassive => "Minor Passive",
            SkillTreeNodeVisualType.MinorUnlock => "Minor Unlock",
            SkillTreeNodeVisualType.MajorPassive => "Major Passive",
            SkillTreeNodeVisualType.Unlock => "Unlock",
            SkillTreeNodeVisualType.Ability => "Ability",
            SkillTreeNodeVisualType.Choice => "Enhancement",
            SkillTreeNodeVisualType.CapstonePassive => "Capstone",
            _ => "Node"
        };

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

    private static string GetChoiceTitle(SkillUnlockDefinition unlock, int choiceIndex)
    {
        if (unlock?.choices == null || choiceIndex < 0 || choiceIndex >= unlock.choices.Count)
            return string.Empty;

        SkillChoiceDefinition choice = unlock.choices[choiceIndex];
        return choice != null && !string.IsNullOrWhiteSpace(choice.title) ? choice.title.Trim() : string.Empty;
    }

    /// <summary>Accent lines starting with "+" (mirrors vertical skill tree tooltip markup).</summary>
    private static string ApplyMajorPassiveValueLineMarkup(SkillDefinition skill, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;
        if (raw.IndexOf("<color=", StringComparison.OrdinalIgnoreCase) >= 0)
            return raw;

        bool greenAccent = skill != null && skill.skillType == SkillType.Woodcutting &&
            UnityEngine.Object.FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include) is { } pac &&
            pac.HasAxeInToolbelt();

        string open = greenAccent ? "<color=#55DD55>" : "<color=#FFB347>";
        const string close = "</color>";
        string norm = raw.Replace("\r\n", "\n");
        string[] lines = norm.Split('\n');
        var sb = new StringBuilder(norm.Length + lines.Length * 32);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                sb.Append('\n');

            string line = lines[i];
            int lead = 0;
            while (lead < line.Length && line[lead] == ' ')
                lead++;

            if (lead >= line.Length)
            {
                sb.Append(line);
                continue;
            }

            string trimmed = line.Substring(lead);
            if (trimmed.StartsWith("+", StringComparison.Ordinal) ||
                (trimmed.Length >= 2 && trimmed[0] == '-' && (char.IsDigit(trimmed[1]) || trimmed[1] == '.')) ||
                trimmed.StartsWith("Flow lasts", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("On proc:", StringComparison.OrdinalIgnoreCase))
            {
                if (lead > 0)
                    sb.Append(line, 0, lead);
                sb.Append(open).Append(trimmed).Append(close);
            }
            else
            {
                sb.Append(line);
            }
        }

        return sb.ToString();
    }
}
