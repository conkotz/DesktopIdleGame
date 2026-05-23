using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates the NEW skills page Active Bonuses column (major passive rows + minor passive stat text).
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillsAbilityActiveBonusesPanelUI : MonoBehaviour
{
    private const string MajorPassivesSectionName = "MajorPassivesUnlocksSection";
    private const string MajorPassivesContentName = "MajorPassivesContent";
    private const string MajorPassivesEmptyName = "MajorPassivesText";

    [SerializeField] private Transform scrollContent;
    [SerializeField] private TMP_Text minorPassiveContent;
    [SerializeField] private Transform majorPassivesListContent;
    [SerializeField] private GameObject majorPassivesEmptyText;
    [SerializeField] private MajorPassiveListEntryUI majorPassiveEntryPrefab;
    [SerializeField] private float majorPassiveRowHeight = 40f;
    [SerializeField] private float rowSpacing = 6f;

    private HorizontalSkillTreeScaffoldUI _horizontalTimeline;
    private readonly List<MajorPassiveListEntryUI> _majorRows = new();
    private SharedTooltipUI _tooltip;
    private Canvas _rootCanvas;

    public void ConfigureTimeline(HorizontalSkillTreeScaffoldUI timeline)
    {
        _horizontalTimeline = timeline;
    }

    public void Refresh(SkillDefinition skill, SkillsManager skillsManager)
    {
        EnsureReferences();
        ClearMajorRows();

        if (skill == null)
        {
            SetMinorText("No unlocks yet.");
            SetMajorEmptyVisible(true);
            return;
        }

        int level = skillsManager != null ? skillsManager.GetLevel(skill.skillType) : 1;
        SetMinorText(SkillsAbilitiesPageUI.BuildMinorPassivesDisplay(skill, level));
        RefreshMajorPassives(skill, level, skillsManager);
    }

    private void RefreshMajorPassives(SkillDefinition skill, int level, SkillsManager skillsManager)
    {
        if (majorPassivesListContent == null)
        {
            SetMajorEmptyVisible(true);
            return;
        }

        SkillsAbilitiesPageUI.CollectMajorPassiveTierRows(
            skill,
            level,
            skillsManager,
            out List<int> tierLevels,
            out List<SkillUnlockDefinition> committed,
            out List<bool> availablePlaceholder);

        bool hasCommitted = false;
        for (int i = 0; i < tierLevels.Count; i++)
        {
            if (availablePlaceholder[i])
                continue;
            if (committed[i] != null)
            {
                hasCommitted = true;
                break;
            }
        }

        SetMajorEmptyVisible(!hasCommitted);
        if (!hasCommitted)
            return;

        RightPanelMajorPassiveListUtil.EnsureListSpacing(majorPassivesListContent, rowSpacing);
        _tooltip ??= FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
        _rootCanvas ??= GetComponentInParent<Canvas>();
        RectTransform panelRect = majorPassivesListContent as RectTransform;

        for (int i = 0; i < tierLevels.Count; i++)
        {
            int rowLevel = tierLevels[i];
            MajorPassiveListEntryUI row = CreateMajorPassiveRow();
            if (row == null)
                continue;

            row.SetTooltipDocking(panelRect, FlipInsideBounds.PreferredSide.Right);

            if (availablePlaceholder[i])
            {
                row.BindAvailableMajorPassiveTier(rowLevel, _tooltip, _rootCanvas, () => ScrollTimelineToLevel(rowLevel));
                _majorRows.Add(row);
                continue;
            }

            SkillUnlockDefinition unlock = committed[i];
            if (unlock == null)
                continue;

            row.Bind(skill, unlock, capstoneStyle: false, _tooltip, _rootCanvas, () => ScrollTimelineToLevel(rowLevel));

            if (SkillTreeMajorPassiveRowIndicators.TryGet(skill, unlock, skillsManager,
                    out bool showNotSelected, out bool showEnhance))
            {
                row.SetTreeStatusIndicators(showNotSelected, showEnhance, () =>
                {
                    // Enhancement branch selection is handled on the vertical tree; horizontal view scrolls to tier.
                    ScrollTimelineToLevel(rowLevel);
                });
            }

            _majorRows.Add(row);
        }
    }

    private void ScrollTimelineToLevel(int rowLevel)
    {
        if (_horizontalTimeline != null)
            _horizontalTimeline.ScrollToLevel(rowLevel);
    }

    private MajorPassiveListEntryUI CreateMajorPassiveRow()
    {
        if (majorPassiveEntryPrefab == null || majorPassivesListContent == null)
            return null;

        MajorPassiveListEntryUI row = Instantiate(majorPassiveEntryPrefab, majorPassivesListContent);
        if (row.transform is RectTransform rt)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, majorPassiveRowHeight);
            LayoutElement layout = rt.GetComponent<LayoutElement>();
            if (layout == null)
                layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = majorPassiveRowHeight;
            layout.preferredHeight = majorPassiveRowHeight;
        }

        return row;
    }

    private void ClearMajorRows()
    {
        for (int i = _majorRows.Count - 1; i >= 0; i--)
        {
            if (_majorRows[i] != null)
                Destroy(_majorRows[i].gameObject);
        }

        _majorRows.Clear();

        if (majorPassivesListContent != null)
            RightPanelMajorPassiveListUtil.ClearRows(majorPassivesListContent);
    }

    private void SetMinorText(string text)
    {
        if (minorPassiveContent != null)
            minorPassiveContent.text = string.IsNullOrEmpty(text) ? "No unlocks yet." : text;
    }

    private void SetMajorEmptyVisible(bool showEmpty)
    {
        if (majorPassivesEmptyText != null)
            majorPassivesEmptyText.SetActive(showEmpty);

        if (majorPassivesListContent != null)
            majorPassivesListContent.gameObject.SetActive(!showEmpty);
    }

    private void EnsureReferences()
    {
        if (scrollContent == null)
        {
            Transform scrollView = transform.Find("ScrollView");
            scrollContent = scrollView != null
                ? scrollView.Find("Viewport/Content")
                : transform.Find("ScrollView/Viewport/Content");
        }

        if (scrollContent == null)
            return;

        if (minorPassiveContent == null)
        {
            Transform minorSection = scrollContent.Find("MinorPassiveUnlocksSection");
            minorPassiveContent = minorSection != null
                ? minorSection.Find("MinorPassiveContent")?.GetComponent<TMP_Text>()
                : scrollContent.GetComponentInChildren<TMP_Text>(true);
        }

        EnsureMajorPassivesSection();

        if (majorPassiveEntryPrefab == null)
        {
            SkillsAbilitiesPageUI legacy = FindFirstObjectByType<SkillsAbilitiesPageUI>(FindObjectsInactive.Include);
            if (legacy != null)
            {
                MajorPassiveListEntryUI[] templates = legacy.GetComponentsInChildren<MajorPassiveListEntryUI>(true);
                if (templates != null && templates.Length > 0)
                    majorPassiveEntryPrefab = templates[0];
            }
        }

    }

    private void EnsureMajorPassivesSection()
    {
        if (scrollContent == null)
            return;

        Transform majorSection = scrollContent.Find(MajorPassivesSectionName);
        if (majorSection == null)
            majorSection = CreateMajorPassivesSection(scrollContent);

        if (majorPassivesListContent == null)
            majorPassivesListContent = majorSection.Find(MajorPassivesContentName);

        if (majorPassivesEmptyText == null)
        {
            Transform empty = majorSection.Find(MajorPassivesEmptyName);
            if (empty != null)
                majorPassivesEmptyText = empty.gameObject;
        }

        if (majorPassivesListContent == null)
            majorPassivesListContent = majorSection.Find(MajorPassivesContentName);
    }

    private static Transform CreateMajorPassivesSection(Transform scrollContentRoot)
    {
        var sectionGo = new GameObject(MajorPassivesSectionName, typeof(RectTransform));
        var sectionRt = (RectTransform)sectionGo.transform;
        sectionRt.SetParent(scrollContentRoot, false);
        sectionRt.SetAsFirstSibling();

        var sectionVlg = sectionGo.AddComponent<VerticalLayoutGroup>();
        sectionVlg.spacing = 8;
        sectionVlg.childAlignment = TextAnchor.UpperLeft;
        sectionVlg.childControlWidth = true;
        sectionVlg.childControlHeight = true;
        sectionVlg.childForceExpandWidth = true;
        sectionVlg.childForceExpandHeight = false;
        sectionGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var headerGo = new GameObject("MajorPassivesHeader", typeof(RectTransform));
        headerGo.transform.SetParent(sectionRt, false);
        TMP_Text header = headerGo.AddComponent<TextMeshProUGUI>();
        header.text = "Major Passives";
        header.fontSize = 16;
        header.fontStyle = FontStyles.Bold;
        header.color = new Color(0.17f, 0.13f, 0.09f, 1f);
        header.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement headerLayout = headerGo.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 22f;

        var emptyGo = new GameObject(MajorPassivesEmptyName, typeof(RectTransform));
        emptyGo.transform.SetParent(sectionRt, false);
        TMP_Text empty = emptyGo.AddComponent<TextMeshProUGUI>();
        empty.text = "No major passives yet";
        empty.fontSize = 13;
        empty.color = new Color(0.55f, 0.5f, 0.45f, 1f);
        empty.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement emptyLayout = emptyGo.AddComponent<LayoutElement>();
        emptyLayout.preferredHeight = 20f;

        var listGo = new GameObject(MajorPassivesContentName, typeof(RectTransform));
        var listRt = (RectTransform)listGo.transform;
        listGo.transform.SetParent(sectionRt, false);
        var listVlg = listGo.AddComponent<VerticalLayoutGroup>();
        listVlg.spacing = 6;
        listVlg.childAlignment = TextAnchor.UpperLeft;
        listVlg.childControlWidth = true;
        listVlg.childControlHeight = true;
        listVlg.childForceExpandWidth = true;
        listVlg.childForceExpandHeight = false;
        listGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return sectionRt;
    }
}
