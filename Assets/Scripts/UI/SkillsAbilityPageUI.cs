using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Skills &amp; Abilities page: two-column left list (Gathering / Combat), center + right detail.
/// </summary>
public class SkillsAbilitiesPageUI : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Registry of SkillDefinition assets.")]
    [SerializeField] private SkillDatabase skillDatabase;

    [Tooltip("Player progression; auto-resolves to SkillsManager.Instance if unset.")]
    [SerializeField] private SkillsManager skillsManager;

    [Header("Left panel — columns")]
    [Tooltip("Parent for Gathering skill rows (e.g. GatheringContent).")]
    [SerializeField] private Transform gatheringContent;

    [Tooltip("Parent for Combat skill rows (e.g. CombatContent).")]
    [SerializeField] private Transform combatContent;

    [FormerlySerializedAs("skillListEntryPrefab")]
    [Tooltip("Prefab for one skill row.")]
    [SerializeField] private SkillListEntryUI skillEntryPrefab;

    [Header("Center — selected skill")]
    [FormerlySerializedAs("selectedSkillTitleText")]
    [Tooltip("Title line, e.g. Mining (lv 2).")]
    [SerializeField] private TMP_Text centerTitleText;

    [FormerlySerializedAs("centerDetailPlaceholderText")]
    [Tooltip("Skill tree / center body placeholder until real UI exists.")]
    [SerializeField] private TMP_Text centerSkillTreePlaceholderText;

    [Header("Right panel")]
    [FormerlySerializedAs("unlocksText")]
    [Tooltip("Unlock list from SkillDefinition.unlocks (display-only).")]
    [SerializeField] private TMP_Text rightUnlocksText;

    [FormerlySerializedAs("abilitiesText")]
    [Tooltip("Abilities section (placeholder until drag/drop).")]
    [SerializeField] private TMP_Text rightAbilitiesText;

    private SkillDefinition _selectedSkill;
    private Coroutine _deferredRefreshRoutine;
    private bool _loggedMissingRefs;

    private void Awake()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;

        ValidateRefsOnce();
    }

    private void OnEnable()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;

        SelectFirstSkillIfNeeded();
        RebuildSkillList();
        RefreshView();
        TrySubscribeSkillsEvents();
    }

    private void OnDisable()
    {
        TryUnsubscribeSkillsEvents();
        if (_deferredRefreshRoutine != null)
        {
            StopCoroutine(_deferredRefreshRoutine);
            _deferredRefreshRoutine = null;
        }
    }

    /// <summary>Logs missing required references once (Awake only — not per-frame).</summary>
    private void ValidateRefsOnce()
    {
        if (_loggedMissingRefs) return;
        _loggedMissingRefs = true;

        if (skillDatabase == null)
            Debug.LogWarning("[SkillsAbilitiesPageUI] skillDatabase is not assigned.", this);

        if (skillEntryPrefab == null)
            Debug.LogWarning("[SkillsAbilitiesPageUI] skillEntryPrefab is not assigned.", this);

        if (gatheringContent == null && combatContent == null)
            Debug.LogWarning("[SkillsAbilitiesPageUI] Assign at least one of gatheringContent or combatContent.", this);

        if (skillsManager == null)
            Debug.LogWarning("[SkillsAbilitiesPageUI] skillsManager not found (SkillsManager.Instance is null). Progression UI may show defaults.", this);
    }

    private void TrySubscribeSkillsEvents()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager) return;

        skillsManager.OnXpGained += HandleSkillsXpGained;
    }

    private void TryUnsubscribeSkillsEvents()
    {
        if (!skillsManager) return;
        skillsManager.OnXpGained -= HandleSkillsXpGained;
    }

    /// <summary>
    /// Invoked during AddXp before the level-up loop; defer one frame so GetLevel reflects final state.
    /// Covers both XP-only gains and multi-level ups in one call.
    /// </summary>
    private void HandleSkillsXpGained(SkillType type, int amount, string source)
    {
        ScheduleDeferredProgressRefresh();
    }

    /// <summary>One deferred refresh per burst of XP (restarts if another gain queues before the frame runs).</summary>
    private void ScheduleDeferredProgressRefresh()
    {
        if (!isActiveAndEnabled) return;
        if (_deferredRefreshRoutine != null)
            StopCoroutine(_deferredRefreshRoutine);
        _deferredRefreshRoutine = StartCoroutine(DeferredProgressRefreshRoutine());
    }

    private IEnumerator DeferredProgressRefreshRoutine()
    {
        yield return null;
        _deferredRefreshRoutine = null;
        RefreshEntryLevelsAndView();
    }

    private void RefreshEntryLevelsAndView()
    {
        RefreshAllEntryLevels();
        RefreshView();
        RefreshListSelection();
    }

    private void RefreshAllEntryLevels()
    {
        RefreshEntryLevelsIn(gatheringContent);
        RefreshEntryLevelsIn(combatContent);
    }

    private void RefreshEntryLevelsIn(Transform parent)
    {
        if (!parent || !skillsManager) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            var entry = parent.GetChild(i).GetComponent<SkillListEntryUI>();
            if (entry == null || entry.Definition == null) continue;

            int level = skillsManager.GetLevel(entry.Definition.skillType);
            entry.SetLevel(level);
        }
    }

    public void SelectSkill(SkillDefinition skill)
    {
        if (skill == null) return;

        _selectedSkill = skill;
        RefreshView();
        RefreshListSelection();
    }

    private void OnSkillEntryClicked(SkillDefinition skill)
    {
        SelectSkill(skill);
    }

    private void SelectFirstSkillIfNeeded()
    {
        if (_selectedSkill != null) return;
        if (skillDatabase == null) return;

        _selectedSkill = GetFirstSkillInCategory(SkillCategory.Gathering)
                         ?? GetFirstSkillInCategory(SkillCategory.Combat);
    }

    private SkillDefinition GetFirstSkillInCategory(SkillCategory category)
    {
        var list = CollectSkillsForCategory(category);
        return list.Count > 0 ? list[0] : null;
    }

    private void RebuildSkillList()
    {
        if (!skillEntryPrefab || skillDatabase == null)
            return;

        if (!gatheringContent && !combatContent)
            return;

        ClearChildren(gatheringContent);
        ClearChildren(combatContent);

        PopulateColumn(gatheringContent, SkillCategory.Gathering);
        PopulateColumn(combatContent, SkillCategory.Combat);
        // Crafting / Utility: no column yet — not listed here.

        RefreshListSelection();
    }

    private void PopulateColumn(Transform parent, SkillCategory category)
    {
        if (!parent) return;

        foreach (var skill in CollectSkillsForCategory(category))
        {
            var entry = Instantiate(skillEntryPrefab, parent);
            int level = skillsManager ? skillsManager.GetLevel(skill.skillType) : 1;
            bool selected = skill == _selectedSkill;
            entry.Setup(skill, level, selected, OnSkillEntryClicked);
        }
    }

    private List<SkillDefinition> CollectSkillsForCategory(SkillCategory category)
    {
        var list = new List<SkillDefinition>();
        if (skillDatabase == null || skillDatabase.Skills == null)
            return list;

        foreach (var s in skillDatabase.Skills)
        {
            if (s == null) continue;
            if (s.category != category) continue;
            list.Add(s);
        }

        list.Sort(CompareSkillOrder);
        return list;
    }

    private static int CompareSkillOrder(SkillDefinition a, SkillDefinition b)
    {
        int byOrder = a.listSortOrder.CompareTo(b.listSortOrder);
        if (byOrder != 0) return byOrder;
        return a.skillType.CompareTo(b.skillType);
    }

    private static void ClearChildren(Transform parent)
    {
        if (!parent) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void RefreshListSelection()
    {
        ApplySelectionHighlight(gatheringContent);
        ApplySelectionHighlight(combatContent);
    }

    private void ApplySelectionHighlight(Transform parent)
    {
        if (!parent) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            var entry = parent.GetChild(i).GetComponent<SkillListEntryUI>();
            if (entry != null)
                entry.SetSelected(entry.Definition == _selectedSkill);
        }
    }

    private void RefreshView()
    {
        if (_selectedSkill == null)
        {
            if (centerTitleText) centerTitleText.text = "No Skill Selected";
            if (centerSkillTreePlaceholderText) centerSkillTreePlaceholderText.text = "";
            if (rightUnlocksText) rightUnlocksText.text = "";
            if (rightAbilitiesText) rightAbilitiesText.text = "";
            return;
        }

        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        string displayName = string.IsNullOrWhiteSpace(_selectedSkill.displayName)
            ? _selectedSkill.skillType.ToString()
            : _selectedSkill.displayName;

        if (centerTitleText)
            centerTitleText.text = $"{displayName} (lv {level})";

        if (centerSkillTreePlaceholderText)
            centerSkillTreePlaceholderText.text = "Skill tree\n(placeholder — layout only)";

        if (rightUnlocksText)
            rightUnlocksText.text = BuildUnlocksDisplay(_selectedSkill, level);

        if (rightAbilitiesText)
            rightAbilitiesText.text = "Abilities\n(placeholder — drag/drop not implemented yet)";
    }

    private static string BuildUnlocksDisplay(SkillDefinition skill, int currentLevel)
    {
        if (skill == null || skill.unlocks == null || skill.unlocks.Count == 0)
            return "No unlocks yet.";

        var sb = new StringBuilder();
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
