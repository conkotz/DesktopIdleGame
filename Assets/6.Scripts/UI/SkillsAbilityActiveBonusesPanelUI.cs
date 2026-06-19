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
    private const string CapstoneSectionName = "CapstoneUnlocksSection";
    private const string CapstoneContentName = "CapstoneContent";
    private const string CapstoneHeaderName = "CapstoneHeader";
    private const string MajorPassivesSectionName = "MajorPassivesUnlocksSection";
    private const string MajorPassivesContentName = "MajorPassivesContent";
    private const string MajorPassivesEmptyName = "MajorPassivesText";
    private const string MajorPassivesHeaderName = "UnlocksHeader";
    private const string MinorPassivesSectionName = "MinorPassiveUnlocksSection";
    private const string MinorPassivesHeaderName = "MinorPassivesHeader";

    private const float MajorHeaderFontSize = 28f;
    private const float MinorContentFontSize = 18f;
    private const string MinorEmptyText = "No minor passives yet";

    private static readonly Color LightBonusesHeaderColor = new(0.95f, 0.92f, 0.86f, 1f);
    private static readonly Color MinorBonusesBodyColor = new(0.82f, 0.78f, 0.72f, 1f);

    [SerializeField] private Transform scrollContent;
    [SerializeField] private TMP_Text minorPassiveContent;
    [SerializeField] private Transform capstoneSectionRoot;
    [SerializeField] private Transform capstoneListContent;
    [SerializeField] private Transform majorPassivesListContent;
    [SerializeField] private GameObject majorPassivesEmptyText;
    [SerializeField] private MajorPassiveListEntryUI majorPassiveEntryPrefab;
    [SerializeField] private float majorPassiveRowHeight = 48f;
    [SerializeField] private float rowSpacing = 6f;

    private HorizontalSkillTreeScaffoldUI _horizontalTimeline;
    private readonly List<MajorPassiveListEntryUI> _majorRows = new();
    private MajorPassiveListEntryUI _capstoneRow;
    private Canvas _rootCanvas;
    private SkillType _cachedSkillType;
    private int _cachedLevel = -1;

    public void ConfigureTimeline(HorizontalSkillTreeScaffoldUI timeline)
    {
        _horizontalTimeline = timeline;
    }

    public void Refresh(SkillDefinition skill, SkillsManager skillsManager)
    {
        EnsureReferences();

        if (skill == null)
        {
            _cachedSkillType = default;
            _cachedLevel = -1;
            ClearMajorRows();
            ClearCapstoneRow();
            SetMinorText(null);
            SetMajorEmptyVisible(true);
            SetCapstoneSectionVisible(false);
            return;
        }

        int level = skillsManager != null ? skillsManager.GetLevel(skill.skillType) : 1;
        if (skill.skillType == _cachedSkillType && level == _cachedLevel && _majorRows.Count + (_capstoneRow != null ? 1 : 0) > 0)
            return;

        _cachedSkillType = skill.skillType;
        _cachedLevel = level;

        ClearMajorRows();
        ClearCapstoneRow();
        SetMinorText(SkillsAbilitiesPageUI.BuildMinorPassivesDisplay(skill, level));
        RefreshCapstoneRow(skill, level);
        RefreshMajorPassives(skill, level, skillsManager);
    }

    private void RefreshCapstoneRow(SkillDefinition skill, int level)
    {
        EnsureCapstoneSection();
        SkillUnlockDefinition capstone = FindCapstonePassiveUnlockForSkill(skill, level);
        bool showCapstone = capstone != null;
        SetCapstoneSectionVisible(showCapstone);
        if (!showCapstone)
            return;

        if (capstoneListContent == null || majorPassiveEntryPrefab == null)
            return;

        _rootCanvas ??= GetComponentInParent<Canvas>();
        RightPanelMajorPassiveListUtil.EnsureListSpacing(capstoneListContent, rowSpacing);

        _capstoneRow = CreateMajorPassiveRow(capstoneListContent);
        if (_capstoneRow == null)
            return;

        int rowLevel = Mathf.Max(1, capstone.requiredLevel);
        _capstoneRow.Bind(skill, capstone, capstoneStyle: true, tooltip: null, _rootCanvas,
            () => FocusUnlock(skill, capstone, rowLevel));
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
        _rootCanvas ??= GetComponentInParent<Canvas>();

        for (int i = 0; i < tierLevels.Count; i++)
        {
            int rowLevel = tierLevels[i];
            MajorPassiveListEntryUI row = CreateMajorPassiveRow(majorPassivesListContent);
            if (row == null)
                continue;

            if (availablePlaceholder[i])
            {
                row.BindAvailableMajorPassiveTier(rowLevel, tooltip: null, _rootCanvas,
                    () => FocusMajorPassiveTier(skill, rowLevel));
                _majorRows.Add(row);
                continue;
            }

            SkillUnlockDefinition unlock = committed[i];
            if (unlock == null)
                continue;

            row.Bind(skill, unlock, capstoneStyle: false, tooltip: null, _rootCanvas,
                () => FocusUnlock(skill, unlock, rowLevel));

            if (SkillTreeMajorPassiveRowIndicators.TryGet(skill, unlock, skillsManager,
                    out bool showNotSelected, out bool showEnhance))
            {
                row.SetTreeStatusIndicators(
                    showNotSelected && !row.ShowsNoEnhancementPlaceholder,
                    showEnhance,
                    () => FocusUnlock(skill, unlock, rowLevel));
            }

            _majorRows.Add(row);
        }
    }

    private void FocusMajorPassiveTier(SkillDefinition skill, int rowLevel)
    {
        if (_horizontalTimeline == null)
            return;

        _horizontalTimeline.ScrollToLevel(rowLevel);
    }

    private void FocusUnlock(SkillDefinition skill, SkillUnlockDefinition unlock, int rowLevel)
    {
        if (_horizontalTimeline == null || unlock == null)
            return;

        if (_horizontalTimeline.TryFocusUnlock(skill, unlock, rowLevel))
            return;

        _horizontalTimeline.ScrollToLevel(rowLevel);
    }

    private MajorPassiveListEntryUI CreateMajorPassiveRow(Transform parent)
    {
        if (majorPassiveEntryPrefab == null || parent == null)
            return null;

        MajorPassiveListEntryUI row = Instantiate(majorPassiveEntryPrefab, parent);
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

    private void ClearCapstoneRow()
    {
        if (_capstoneRow != null)
        {
            Destroy(_capstoneRow.gameObject);
            _capstoneRow = null;
        }

        if (capstoneListContent != null)
            RightPanelMajorPassiveListUtil.ClearRows(capstoneListContent);
    }

    private void SetCapstoneSectionVisible(bool visible)
    {
        if (capstoneSectionRoot != null)
            capstoneSectionRoot.gameObject.SetActive(visible);
    }

    private void SetMinorText(string text)
    {
        if (minorPassiveContent == null)
            return;

        if (string.IsNullOrEmpty(text) || text == "No unlocks yet.")
            minorPassiveContent.text = MinorEmptyText;
        else
            minorPassiveContent.text = text;
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

        EnsureCapstoneSection();
        EnsureMajorPassivesSection();
        EnsureMinorPassivesSection();
        ApplyBonusesTypography();

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

    private void EnsureCapstoneSection()
    {
        if (scrollContent == null)
            return;

        if (capstoneSectionRoot == null)
            capstoneSectionRoot = scrollContent.Find(CapstoneSectionName);

        if (capstoneSectionRoot == null)
            capstoneSectionRoot = CreateCapstoneSection(scrollContent);

        if (capstoneListContent == null && capstoneSectionRoot != null)
            capstoneListContent = capstoneSectionRoot.Find(CapstoneContentName);
    }

    private static Transform CreateCapstoneSection(Transform scrollContentRoot)
    {
        var sectionGo = new GameObject(CapstoneSectionName, typeof(RectTransform));
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

        var headerGo = new GameObject(CapstoneHeaderName, typeof(RectTransform));
        headerGo.transform.SetParent(sectionRt, false);
        TMP_Text header = headerGo.AddComponent<TextMeshProUGUI>();
        ApplyHeaderStyle(header, "Capstone");
        header.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement headerLayout = headerGo.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 32f;

        var listGo = new GameObject(CapstoneContentName, typeof(RectTransform));
        listGo.transform.SetParent(sectionRt, false);
        var listVlg = listGo.AddComponent<VerticalLayoutGroup>();
        listVlg.spacing = 6;
        listVlg.childAlignment = TextAnchor.UpperLeft;
        listVlg.childControlWidth = true;
        listVlg.childControlHeight = true;
        listVlg.childForceExpandWidth = true;
        listVlg.childForceExpandHeight = false;
        listGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sectionGo.SetActive(false);
        return sectionRt;
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

        Transform capstone = scrollContentRoot.Find(CapstoneSectionName);
        int siblingIndex = capstone != null ? capstone.GetSiblingIndex() + 1 : 0;
        sectionRt.SetSiblingIndex(siblingIndex);

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
        ApplyHeaderStyle(header, "Major Passives");
        header.alignment = TextAlignmentOptions.TopLeft;
        LayoutElement headerLayout = headerGo.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 32f;

        var emptyGo = new GameObject(MajorPassivesEmptyName, typeof(RectTransform));
        emptyGo.transform.SetParent(sectionRt, false);
        TMP_Text empty = emptyGo.AddComponent<TextMeshProUGUI>();
        empty.text = "No major passives yet";
        ApplyEmptyStateStyle(empty);
        empty.alignment = TextAlignmentOptions.TopLeft;
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

    private void EnsureMinorPassivesSection()
    {
        if (scrollContent == null)
            return;

        Transform minorSection = scrollContent.Find(MinorPassivesSectionName);
        if (minorSection == null)
            return;

        Transform header = minorSection.Find(MinorPassivesHeaderName);
        if (header == null)
        {
            var headerGo = new GameObject(MinorPassivesHeaderName, typeof(RectTransform));
            headerGo.transform.SetParent(minorSection, false);
            headerGo.transform.SetAsFirstSibling();
            TMP_Text headerText = headerGo.AddComponent<TextMeshProUGUI>();
            ApplyHeaderStyle(headerText, "Minor Passives");
            headerText.alignment = TextAlignmentOptions.TopLeft;
            LayoutElement headerLayout = headerGo.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 32f;
        }
    }

    private void ApplyBonusesTypography()
    {
        if (scrollContent == null)
            return;

        Transform capstoneSection = scrollContent.Find(CapstoneSectionName);
        if (capstoneSection != null)
        {
            TMP_Text capstoneHeader = capstoneSection.Find(CapstoneHeaderName)?.GetComponent<TMP_Text>();
            ApplyHeaderStyle(capstoneHeader, "Capstone");
        }

        Transform majorSection = scrollContent.Find(MajorPassivesSectionName);
        if (majorSection != null)
        {
            TMP_Text majorHeader = majorSection.Find(MajorPassivesHeaderName)?.GetComponent<TMP_Text>()
                ?? majorSection.Find("MajorPassivesHeader")?.GetComponent<TMP_Text>();
            ApplyHeaderStyle(majorHeader, "Major Passives");

            TMP_Text majorEmpty = majorSection.Find(MajorPassivesEmptyName)?.GetComponent<TMP_Text>();
            ApplyEmptyStateStyle(majorEmpty);
        }

        Transform minorSection = scrollContent.Find(MinorPassivesSectionName);
        if (minorSection != null)
        {
            TMP_Text minorHeader = minorSection.Find(MinorPassivesHeaderName)?.GetComponent<TMP_Text>();
            ApplyHeaderStyle(minorHeader, "Minor Passives");
        }

        if (minorPassiveContent != null)
        {
            minorPassiveContent.fontSize = MinorContentFontSize;
            minorPassiveContent.color = MinorBonusesBodyColor;
        }
    }

    private static void ApplyHeaderStyle(TMP_Text text, string label)
    {
        if (text == null)
            return;

        if (!string.IsNullOrEmpty(label))
            text.text = label;

        text.fontSize = MajorHeaderFontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = LightBonusesHeaderColor;
    }

    private static void ApplyEmptyStateStyle(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontSize = MinorContentFontSize;
        text.color = MinorBonusesBodyColor;
    }

    private static SkillUnlockDefinition FindCapstonePassiveUnlockForSkill(SkillDefinition skill, int currentLevel)
    {
        if (skill?.unlocks == null)
            return null;

        SkillUnlockDefinition best = null;
        int bestLevel = int.MaxValue;
        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition u = skill.unlocks[i];
            if (u == null || u.unlockType != SkillUnlockType.CapstonePassive)
                continue;

            int req = Mathf.Max(1, u.requiredLevel);
            if (currentLevel < req)
                continue;

            if (req < bestLevel)
            {
                bestLevel = req;
                best = u;
            }
        }

        return best;
    }
}
