using System;
using UnityEngine;

/// <summary>
/// Keeps skill selection in sync between the legacy skills page and SkillsAbilityPageNEW.
/// Also persists the last selected combat skill tab for reopening the page.
/// </summary>
public static class SkillsAbilityPageSelectionHub
{
    private const string LastSkillTypeKey = "SkillsAbilityPage.LastSelectedSkillType";

    private static SkillDefinition _current;
    private static bool _syncInProgress;

    public static event Action<SkillDefinition> SelectionChanged;

    public static SkillDefinition Current => _current;

    public static void SaveLastSkillType(SkillType skillType) =>
        PlayerPrefs.SetInt(LastSkillTypeKey, (int)skillType);

    public static bool TryGetLastSkillType(out SkillType skillType)
    {
        if (!PlayerPrefs.HasKey(LastSkillTypeKey))
        {
            skillType = SkillType.Melee;
            return false;
        }

        skillType = (SkillType)PlayerPrefs.GetInt(LastSkillTypeKey, (int)SkillType.Melee);
        return true;
    }

    public static void NotifySelection(SkillDefinition skill, UnityEngine.Object source)
    {
        if (skill == null || _syncInProgress)
            return;

        bool changed = _current != skill;
        _current = skill;
        SaveLastSkillType(skill.skillType);

        if (!changed)
            return;

        _syncInProgress = true;
        try
        {
            SelectionChanged?.Invoke(skill);

            SkillsAbilitiesPageUI legacyPage = FindLegacyPage();
            if (legacyPage != null && !ReferenceEquals(source, legacyPage) && legacyPage.SelectedSkill != skill)
                legacyPage.ApplySelectionFromOtherPage(skill);

            SkillsAbilityPageNewUI newPage = FindNewPage();
            if (newPage != null && !ReferenceEquals(source, newPage) && newPage.SelectedSkill != skill)
                newPage.ApplySelectionFromOtherPage(skill);
        }
        finally
        {
            _syncInProgress = false;
        }
    }

    private static SkillsAbilitiesPageUI FindLegacyPage() =>
        UnityEngine.Object.FindFirstObjectByType<SkillsAbilitiesPageUI>(FindObjectsInactive.Include);

    private static SkillsAbilityPageNewUI FindNewPage() =>
        UnityEngine.Object.FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);
}
