using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Plain-text summary of a per-skill ability preset for the Edit preset popup.</summary>
public static class SkillAbilityPresetSummaryBuilder
{
    public static string Build(SkillDefinition skill, SkillAbilityPresetSave preset, SkillsManager skillsManager)
    {
        if (skill == null)
            return string.Empty;

        if (preset == null || !preset.hasSnapshot)
            return "No saved build in this preset.\nUse Save to from the context menu to store your current tree.";

        var choiceLookup = BuildLookup(preset.choiceKeys, preset.choiceValues);
        var rowLookup = BuildLookup(preset.abilityRowKeys, preset.abilityRowValues);
        int level = skillsManager != null ? skillsManager.GetLevel(skill.skillType) : 50;

        var sb = new StringBuilder();

        AppendAbilitiesSection(sb, skill, level, choiceLookup, rowLookup);
        AppendMajorPassivesSection(sb, skill, level, choiceLookup, rowLookup);
        AppendCapstoneSection(sb, skill, level, choiceLookup, rowLookup);

        string result = sb.ToString().TrimEnd();
        return string.IsNullOrWhiteSpace(result)
            ? "No abilities, major passives, or capstone saved in this preset."
            : result;
    }

    private static Dictionary<string, int> BuildLookup(List<string> keys, List<int> values)
    {
        var lookup = new Dictionary<string, int>();
        if (keys == null || values == null)
            return lookup;

        int count = Mathf.Min(keys.Count, values.Count);
        for (int i = 0; i < count; i++)
        {
            string key = keys[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;
            lookup[key] = values[i];
        }

        return lookup;
    }

    private static void AppendAbilitiesSection(
        StringBuilder sb,
        SkillDefinition skill,
        int playerLevel,
        Dictionary<string, int> choiceLookup,
        Dictionary<string, int> rowLookup)
    {
        var lines = new List<string>();

        if (CombatStarterAttackAbility.TryGetCombatStarterAttackForSkill(skill, out AbilityDefinition starter)
            && starter != null
            && playerLevel >= Mathf.Max(1, starter.unlockLevel))
        {
            string starterName = SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(starter);
            if (!string.IsNullOrWhiteSpace(starterName))
                lines.Add(FormatAbilityLine(starter, starterName, choiceLookup, skill));
        }

        var tierLevels = SkillAbilityCommitRules.CollectSortedAbilityTierLevels(skill);
        for (int i = 0; i < tierLevels.Count; i++)
        {
            int rowLevel = tierLevels[i];
            if (playerLevel < rowLevel)
                continue;

            List<AbilityDefinition> siblings = SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(skill, rowLevel);
            if (siblings == null || siblings.Count == 0)
                continue;

            int pick = GetAbilityRowPick(rowLookup, skill.skillType, rowLevel);
            if (pick < 0 || pick >= siblings.Count)
                continue;

            AbilityDefinition def = siblings[pick];
            if (def == null)
                continue;

            string title = SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            lines.Add(FormatAbilityLine(def, title, choiceLookup, skill));
        }

        if (lines.Count == 0)
            return;

        AppendSectionHeader(sb, "Abilities");
        AppendBulletLines(sb, lines);
        sb.AppendLine();
    }

    private static string FormatAbilityLine(
        AbilityDefinition ability,
        string title,
        Dictionary<string, int> choiceLookup,
        SkillDefinition skill)
    {
        SkillUnlockDefinition unlock = SkillAbilityCommitRules.FindAbilityUnlockOnSkill(skill, ability);
        if (unlock?.choices == null || unlock.choices.Count == 0)
            return title.Trim();

        string spineId = SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock(skill, unlock);
        int choiceIndex = GetChoiceIndex(choiceLookup, skill.skillType, spineId);
        string enhancement = ResolveEnhancementTitle(unlock, choiceIndex);
        if (string.IsNullOrWhiteSpace(enhancement))
            enhancement = "no enhancement";

        return $"{title.Trim()} — {enhancement}";
    }

    private static void AppendMajorPassivesSection(
        StringBuilder sb,
        SkillDefinition skill,
        int playerLevel,
        Dictionary<string, int> choiceLookup,
        Dictionary<string, int> rowLookup)
    {
        var lines = new List<string>();
        CollectMajorPassiveLines(skill, playerLevel, choiceLookup, rowLookup, lines, capstone: false);
        if (lines.Count == 0)
            return;

        AppendSectionHeader(sb, "Major passives");
        AppendBulletLines(sb, lines);
        sb.AppendLine();
    }

    private static void AppendCapstoneSection(
        StringBuilder sb,
        SkillDefinition skill,
        int playerLevel,
        Dictionary<string, int> choiceLookup,
        Dictionary<string, int> rowLookup)
    {
        var lines = new List<string>();
        CollectMajorPassiveLines(skill, playerLevel, choiceLookup, rowLookup, lines, capstone: true);
        if (lines.Count == 0)
            return;

        AppendSectionHeader(sb, "Capstone");
        AppendBulletLines(sb, lines);
    }

    private static void AppendSectionHeader(StringBuilder sb, string header)
    {
        if (sb.Length > 0)
            sb.AppendLine();

        sb.AppendLine(header);
        sb.AppendLine();
    }

    private static void AppendBulletLines(StringBuilder sb, List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
            sb.AppendLine($"• {lines[i]}");
    }

    private static void CollectMajorPassiveLines(
        SkillDefinition skill,
        int playerLevel,
        Dictionary<string, int> choiceLookup,
        Dictionary<string, int> rowLookup,
        List<string> lines,
        bool capstone)
    {
        if (skill?.unlocks == null)
            return;

        var levelSet = new HashSet<int>();
        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = skill.unlocks[i];
            if (unlock == null)
                continue;

            bool isCapstone = unlock.unlockType == SkillUnlockType.CapstonePassive;
            if (capstone != isCapstone)
                continue;

            if (!capstone && unlock.unlockType != SkillUnlockType.MajorPassive)
                continue;

            int req = Mathf.Max(1, unlock.requiredLevel);
            if (playerLevel < req)
                continue;

            levelSet.Add(req);
        }

        var sortedLevels = new List<int>(levelSet);
        sortedLevels.Sort();

        for (int li = 0; li < sortedLevels.Count; li++)
        {
            int rowLevel = sortedLevels[li];
            List<SkillUnlockDefinition> siblings = capstone
                ? FindCapstoneSiblingsAtLevel(skill, rowLevel)
                : SkillTreeRowPickRules.GetMultiPickSiblingsAtLevel(skill, rowLevel, majorPassivesOnly: true);

            if (siblings.Count == 0)
                continue;

            int pick = GetMajorRowPick(rowLookup, skill.skillType, rowLevel, siblings);
            if (pick < 0 || pick >= siblings.Count)
                continue;

            SkillUnlockDefinition unlock = siblings[pick];
            if (unlock == null)
                continue;

            string passiveName = SkillsAbilityPresentationResolver.ResolveUnlockTitle(unlock);
            if (string.IsNullOrWhiteSpace(passiveName))
                passiveName = capstone ? "Capstone" : "Major Passive";

            string spineId = SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock(skill, unlock);
            int choiceIndex = GetChoiceIndex(choiceLookup, skill.skillType, spineId);
            string enhancement = ResolveEnhancementTitle(unlock, choiceIndex);
            if (unlock.choices != null && unlock.choices.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(enhancement))
                    enhancement = "no enhancement";
                lines.Add($"{passiveName.Trim()} — {enhancement.Trim()}");
            }
            else
                lines.Add(passiveName.Trim());
        }
    }

    private static List<SkillUnlockDefinition> FindCapstoneSiblingsAtLevel(SkillDefinition skill, int rowLevel)
    {
        var result = new List<SkillUnlockDefinition>();
        if (skill?.unlocks == null)
            return result;

        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = skill.unlocks[i];
            if (unlock == null || unlock.unlockType != SkillUnlockType.CapstonePassive)
                continue;
            if (Mathf.Max(1, unlock.requiredLevel) != rowLevel)
                continue;
            result.Add(unlock);
        }

        return result;
    }

    private static int GetMajorRowPick(
        Dictionary<string, int> rowLookup,
        SkillType skillType,
        int rowLevel,
        List<SkillUnlockDefinition> siblings)
    {
        int pick = GetAbilityRowPick(rowLookup, skillType, rowLevel);
        if (siblings.Count >= 2 && pick < 0)
            return -1;
        if (pick < 0 && siblings.Count == 1)
            return 0;
        return pick;
    }

    private static int GetAbilityRowPick(Dictionary<string, int> rowLookup, SkillType skillType, int rowLevel)
    {
        string key = $"{skillType}:abilityRow:{Mathf.Max(1, rowLevel)}";
        return rowLookup.TryGetValue(key, out int value) ? value : -1;
    }

    private static int GetChoiceIndex(Dictionary<string, int> choiceLookup, SkillType skillType, string spineId)
    {
        if (string.IsNullOrWhiteSpace(spineId))
            return -1;

        string key = SkillsManager.BuildChoiceKeyFromParentSpine(skillType, spineId);
        if (choiceLookup.TryGetValue(key, out int value))
            return value;

        if (TryParseSourceLevelFromChoiceStorageKey(key, out int legacyLevel))
        {
            string legacyKey = $"{skillType}:{legacyLevel}";
            if (choiceLookup.TryGetValue(legacyKey, out int legacyValue))
                return legacyValue;
        }

        return -1;
    }

    private static bool TryParseSourceLevelFromChoiceStorageKey(string key, out int level) =>
        SkillsManager.TryParseSourceLevelFromChoiceStorageKey(key, out level);

    private static string ResolveEnhancementTitle(SkillUnlockDefinition unlock, int choiceIndex)
    {
        if (unlock?.choices == null || choiceIndex < 0 || choiceIndex >= unlock.choices.Count)
            return null;

        SkillChoiceDefinition choice = unlock.choices[choiceIndex];
        if (choice == null)
            return null;

        string title = SkillsAbilityPresentationResolver.ResolveChoiceTitle(choice);
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }
}
