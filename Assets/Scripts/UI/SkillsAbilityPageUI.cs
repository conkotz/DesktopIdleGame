using System.Text;
using TMPro;
using UnityEngine;

public class SkillsAbilitiesPageUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private SkillsManager skillsManager;

    [Header("Left Panel")]
    [SerializeField] private Transform skillsListContent;
    [SerializeField] private SkillListEntryUI skillListEntryPrefab;

    [Header("UI")]
    [SerializeField] private TMP_Text selectedSkillTitleText;
    [SerializeField] private TMP_Text unlocksText;
    [SerializeField] private TMP_Text abilitiesText;

    private SkillDefinition _selectedSkill;

    private void Awake()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
    }

    private void OnEnable()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;

        SelectFirstSkillIfNeeded();
        RebuildSkillList();
        RefreshView();
    }

    public void SelectSkill(SkillDefinition skill)
    {
        if (skill == null) return;

        _selectedSkill = skill;
        RefreshView();
        RefreshListSelection();
    }

    private void SelectFirstSkillIfNeeded()
    {
        if (_selectedSkill != null) return;
        if (skillDatabase == null) return;
        if (skillDatabase.Skills == null || skillDatabase.Skills.Count == 0) return;

        _selectedSkill = skillDatabase.Skills[0];
    }

    private void RebuildSkillList()
    {
        if (!skillsListContent || !skillListEntryPrefab || skillDatabase == null)
            return;

        for (int i = skillsListContent.childCount - 1; i >= 0; i--)
            Destroy(skillsListContent.GetChild(i).gameObject);

        foreach (var skill in skillDatabase.Skills)
        {
            if (skill == null) continue;

            var entry = Instantiate(skillListEntryPrefab, skillsListContent);
            entry.Bind(skill, skillsManager, this);
        }

        RefreshListSelection();
    }

    private void RefreshListSelection()
    {
        if (!skillsListContent) return;

        for (int i = 0; i < skillsListContent.childCount; i++)
        {
            var entry = skillsListContent.GetChild(i).GetComponent<SkillListEntryUI>();
            if (entry != null)
                entry.SetSelected(entry.Definition == _selectedSkill);
        }
    }

    private void RefreshView()
    {
        if (_selectedSkill == null)
        {
            if (selectedSkillTitleText) selectedSkillTitleText.text = "No Skill Selected";
            if (unlocksText) unlocksText.text = "";
            if (abilitiesText) abilitiesText.text = "";
            return;
        }

        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;

        if (selectedSkillTitleText)
            selectedSkillTitleText.text = $"{_selectedSkill.displayName}  Lv {level}";

        if (unlocksText)
            unlocksText.text = BuildUnlocksText(_selectedSkill, level);

        if (abilitiesText)
            abilitiesText.text = $"Abilities for {_selectedSkill.displayName}\n(placeholder for now)";
    }

    private string BuildUnlocksText(SkillDefinition skill, int currentLevel)
    {
        if (skill.unlocks == null || skill.unlocks.Count == 0)
            return "No unlocks defined yet.";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Unlocks");

        foreach (var unlock in skill.unlocks)
        {
            if (unlock == null) continue;

            bool unlocked = currentLevel >= unlock.requiredLevel;
            sb.Append(unlocked ? "• " : "🔒 ");
            sb.Append($"Lv {unlock.requiredLevel} - {unlock.title}");

            if (!string.IsNullOrWhiteSpace(unlock.description))
            {
                sb.Append(": ");
                sb.Append(unlock.description);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}