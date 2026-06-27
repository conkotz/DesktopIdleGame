using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires scene-placed <see cref="SkillListEntryUI"/> rows under SkillsProgress.
/// Preserves editor grid settings for the narrow layout; switches to 4 columns when the bottom bar is wide.
/// Rebuilds vertical stacking after grid changes so sections do not overlap on reopen.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillsAbilitySkillsListPanelUI : MonoBehaviour
{
    private const string ScrollViewName = "Scroll View";
    private const string LegacyScrollViewName = "ScrollView";
    private const string ContentName = "Content";

    [Header("Scroll")]
    [SerializeField] private RectTransform listContent;

    [Header("Section headers")]
    [SerializeField] private RectTransform combatSkillsHeader;
    [SerializeField] private RectTransform gatheringSkillsHeader;
    [SerializeField] private RectTransform processingSkillsHeader;

    [Header("Section content (GridLayoutGroup roots)")]
    [SerializeField] private RectTransform combatSkillsContent;
    [SerializeField] private RectTransform gatheringSkillsContent;
    [SerializeField] private RectTransform processingSkillsContent;

    [Header("Combat entries")]
    [SerializeField] private SkillListEntryUI combatMelee;
    [SerializeField] private SkillListEntryUI combatRanged;
    [SerializeField] private SkillListEntryUI combatMagic;
    [SerializeField] private SkillListEntryUI combatEndurance;

    [Header("Gathering entries")]
    [SerializeField] private SkillListEntryUI gatheringWoodcutting;
    [SerializeField] private SkillListEntryUI gatheringMining;
    [SerializeField] private SkillListEntryUI gatheringFishing;

    [Header("Processing entries")]
    [SerializeField] private SkillListEntryUI processingCooking;
    [SerializeField] private SkillListEntryUI processingSmelting;
    [SerializeField] private SkillListEntryUI processingBlacksmithing;

    [Header("Processing placeholders")]
    [SerializeField] private SkillListEntryUI processingMagicCrafting;
    [SerializeField] private SkillListEntryUI processingRangerCrafting;
    [SerializeField] private SkillListEntryUI processingAlchemy;

    private SkillDatabase _skillDatabase;
    private SkillsManager _skillsManager;
    private Action<SkillDefinition> _onSkillClicked;
    private Action<SkillDefinition> _onHoverAcknowledge;
    private SkillsAbilityBottomPanelLayoutUI _bottomPanelLayout;
    private bool _processingSubscribed;
    private bool _layoutSubscribed;
    private bool _authoredCaptured;

    private readonly Dictionary<SkillType, SkillListEntryUI> _entryBySkillType = new();
    private readonly Dictionary<RectTransform, GridLayoutSnapshot> _authoredGridByRoot = new();

    private ScrollRect _scrollRect;
    private Coroutine _layoutCo;

    private struct GridLayoutSnapshot
    {
        public GridLayoutGroup.Constraint Constraint;
        public int ConstraintCount;
        public Vector2 CellSize;
        public Vector2 Spacing;
        public RectOffset Padding;
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureAuthoredGridLayoutsOnce();
        ConfigureScrollContentRoot();
    }

    public void Configure(
        SkillDatabase database,
        SkillsManager skillsManager,
        Action<SkillDefinition> onSkillClicked,
        Action<SkillDefinition> onHoverAcknowledge = null)
    {
        _skillDatabase = database;
        _skillsManager = skillsManager;
        _onSkillClicked = onSkillClicked;
        _onHoverAcknowledge = onHoverAcknowledge;
    }

    public void SetVisibleCategory(SkillCategory category) => RebuildList();

    public void SetEntryPrefab(SkillListEntryUI prefab) { }

    public void RebuildList()
    {
        ResolveReferences();
        WireScrollRect();
        _entryBySkillType.Clear();

        PreferRuntimeSkillsManager();
        if (_skillDatabase == null)
            return;

        WireCombatEntry(combatMelee, SkillType.Melee);
        WireCombatEntry(combatRanged, SkillType.Ranged);
        WireCombatEntry(combatMagic, SkillType.Magic);
        WireCombatEntry(combatEndurance, SkillType.Endurance);

        WireGatheringEntry(gatheringWoodcutting, SkillType.Woodcutting);
        WireGatheringEntry(gatheringMining, SkillType.Mining);
        WireGatheringEntry(gatheringFishing, SkillType.Fishing);

        WireProcessingEntry(processingCooking, ProcessingSkillType.Cooking);
        WireProcessingEntry(processingSmelting, ProcessingSkillType.Smelting);
        WireProcessingPlaceholder(processingBlacksmithing);
        WireProcessingPlaceholder(processingMagicCrafting);
        WireProcessingPlaceholder(processingRangerCrafting);
        WireProcessingPlaceholder(processingAlchemy);

        ScheduleLayoutRefresh();
    }

    public void ShowUnlockGlowForSkill(SkillType skillType)
    {
        if (_entryBySkillType.TryGetValue(skillType, out SkillListEntryUI entry) && entry != null)
            entry.ShowUnlockGlow();
    }

    public void ApplyPendingEntryGlows(IEnumerable<SkillType> pendingSkillTypes)
    {
        if (pendingSkillTypes == null)
            return;

        foreach (SkillType skillType in pendingSkillTypes)
            ShowUnlockGlowForSkill(skillType);
    }

    public void RefreshSelection(SkillDefinition selectedSkill)
    {
        foreach (KeyValuePair<SkillType, SkillListEntryUI> pair in _entryBySkillType)
        {
            bool selected = selectedSkill != null && selectedSkill.skillType == pair.Key;
            pair.Value.SetSelected(selected);
        }
    }

    public void RefreshLevelsForSkill(SkillType skillType)
    {
        if (!_entryBySkillType.TryGetValue(skillType, out SkillListEntryUI entry) || entry == null)
            return;

        PreferRuntimeSkillsManager();
        int level = _skillsManager != null ? _skillsManager.GetLevel(skillType) : 1;
        float progress01 = _skillsManager != null ? _skillsManager.GetProgress01(skillType) : 0f;
        int xp = _skillsManager != null ? _skillsManager.GetXpIntoLevel(skillType) : 0;
        entry.SetLevel(level);
        entry.SetProgress(progress01);
        entry.SetXp(xp);
    }

    public void RefreshAllLevels()
    {
        PreferRuntimeSkillsManager();

        if (_entryBySkillType.Count == 0)
        {
            RebuildList();
            return;
        }

        foreach (SkillType skillType in _entryBySkillType.Keys)
            RefreshLevelsForSkill(skillType);

        RefreshProcessingEntries();
    }

    private void OnEnable()
    {
        SubscribeLayoutChanges();
        SubscribeProcessingChanges();
        RestoreAuthoredGridLayouts();
        RebuildList();
    }

    private void OnDisable()
    {
        UnsubscribeLayoutChanges();
        UnsubscribeProcessingChanges();

        if (_layoutCo != null)
        {
            StopCoroutine(_layoutCo);
            _layoutCo = null;
        }

        RestoreAuthoredGridLayouts();
        RefreshVerticalLayoutImmediate();
    }

    private void WireCombatEntry(SkillListEntryUI entry, SkillType skillType)
    {
        if (entry == null)
            return;

        SkillDefinition def = FindSkill(skillType);
        int level = _skillsManager != null ? _skillsManager.GetLevel(skillType) : 1;
        float progress01 = _skillsManager != null ? _skillsManager.GetProgress01(skillType) : 0f;
        int xp = _skillsManager != null ? _skillsManager.GetXpIntoLevel(skillType) : 0;

        if (def != null)
        {
            entry.SetupWithXp(def, level, progress01, xp, false, HandleEntryClicked, _onHoverAcknowledge);
            _entryBySkillType[skillType] = entry;
        }
        else
        {
            entry.RefreshDisplay(level, progress01, xp, selected: false, interactable: false);
        }
    }

    private void WireGatheringEntry(SkillListEntryUI entry, SkillType skillType)
    {
        if (entry == null)
            return;

        SkillDefinition def = FindSkill(skillType);
        int level = _skillsManager != null ? _skillsManager.GetLevel(skillType) : 1;
        float progress01 = _skillsManager != null ? _skillsManager.GetProgress01(skillType) : 0f;
        int xp = _skillsManager != null ? _skillsManager.GetXpIntoLevel(skillType) : 0;

        if (def != null)
        {
            entry.SetupWithXp(def, level, progress01, xp, false, HandleEntryClicked, _onHoverAcknowledge);
            _entryBySkillType[skillType] = entry;
        }
        else
        {
            entry.RefreshDisplay(level, progress01, xp, selected: false, interactable: false);
        }
    }

    private static void WireProcessingEntry(SkillListEntryUI entry, ProcessingSkillType processingType)
    {
        if (entry == null)
            return;

        ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
        entry.RefreshDisplay(
            runtime.GetLevel(processingType),
            runtime.GetProgress01(processingType),
            runtime.GetXp(processingType),
            selected: false,
            interactable: false);
    }

    private static void WireProcessingPlaceholder(SkillListEntryUI entry)
    {
        if (entry == null)
            return;

        entry.RefreshDisplay(1, 0f, 0, selected: false, interactable: false);
        entry.SetNotCompleteVisible(true);
    }

    private void RefreshProcessingEntries()
    {
        WireProcessingEntry(processingCooking, ProcessingSkillType.Cooking);
        WireProcessingEntry(processingSmelting, ProcessingSkillType.Smelting);
        WireProcessingPlaceholder(processingBlacksmithing);
        WireProcessingPlaceholder(processingMagicCrafting);
        WireProcessingPlaceholder(processingRangerCrafting);
        WireProcessingPlaceholder(processingAlchemy);
    }

    private SkillDefinition FindSkill(SkillType skillType)
    {
        if (_skillDatabase?.Skills == null)
            return null;

        for (int i = 0; i < _skillDatabase.Skills.Count; i++)
        {
            SkillDefinition s = _skillDatabase.Skills[i];
            if (s != null && s.skillType == skillType)
                return s;
        }

        return null;
    }

    private void HandleEntryClicked(SkillDefinition skill) => _onSkillClicked?.Invoke(skill);

    private void SubscribeLayoutChanges()
    {
        _bottomPanelLayout = GetComponentInParent<SkillsAbilityBottomPanelLayoutUI>(true);
        if (_bottomPanelLayout == null)
            return;

        if (!_layoutSubscribed)
        {
            _bottomPanelLayout.ExpandedChanged += OnBottomPanelExpandedChanged;
            _layoutSubscribed = true;
        }
    }

    private void UnsubscribeLayoutChanges()
    {
        if (!_layoutSubscribed || _bottomPanelLayout == null)
            return;

        _bottomPanelLayout.ExpandedChanged -= OnBottomPanelExpandedChanged;
        _layoutSubscribed = false;
    }

    private void OnBottomPanelExpandedChanged(bool detailsExpanded) => ScheduleLayoutRefresh();

    private void SubscribeProcessingChanges()
    {
        if (_processingSubscribed)
            return;

        ProcessingProficiencyRuntime.EnsureInstance().Changed += OnProcessingProficiencyChanged;
        _processingSubscribed = true;
    }

    private void UnsubscribeProcessingChanges()
    {
        if (!_processingSubscribed)
            return;

        if (ProcessingProficiencyRuntime.Instance != null)
            ProcessingProficiencyRuntime.Instance.Changed -= OnProcessingProficiencyChanged;

        _processingSubscribed = false;
    }

    private void OnProcessingProficiencyChanged() => RefreshProcessingEntries();

    private bool IsWideSkillsLayout()
    {
        if (_bottomPanelLayout != null)
            return !_bottomPanelLayout.IsExpanded;

        return ((RectTransform)transform).rect.width >= 240f;
    }

    private void CaptureAuthoredGridLayoutsOnce()
    {
        if (_authoredCaptured)
            return;

        CaptureAuthoredGrid(combatSkillsContent);
        CaptureAuthoredGrid(gatheringSkillsContent);
        CaptureAuthoredGrid(processingSkillsContent);
        _authoredCaptured = true;
    }

    private void CaptureAuthoredGrid(RectTransform root)
    {
        if (root == null || _authoredGridByRoot.ContainsKey(root))
            return;

        GridLayoutGroup grid = root.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        _authoredGridByRoot[root] = new GridLayoutSnapshot
        {
            Constraint = grid.constraint,
            ConstraintCount = grid.constraintCount,
            CellSize = grid.cellSize,
            Spacing = grid.spacing,
            Padding = grid.padding
        };
    }

    private void RestoreAuthoredGridLayouts()
    {
        RestoreAuthoredGrid(combatSkillsContent);
        RestoreAuthoredGrid(gatheringSkillsContent);
        RestoreAuthoredGrid(processingSkillsContent);
    }

    private void RestoreAuthoredGrid(RectTransform root)
    {
        if (root == null)
            return;

        GridLayoutGroup grid = root.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        if (!_authoredGridByRoot.TryGetValue(root, out GridLayoutSnapshot authored))
            return;

        grid.constraint = authored.Constraint;
        grid.constraintCount = authored.ConstraintCount;
        grid.cellSize = authored.CellSize;
        grid.spacing = authored.Spacing;
        grid.padding = authored.Padding;
    }

    private void ScheduleLayoutRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        if (_layoutCo != null)
            StopCoroutine(_layoutCo);

        _layoutCo = StartCoroutine(CoApplyLayoutAfterFrame());
    }

    private IEnumerator CoApplyLayoutAfterFrame()
    {
        yield return null;
        ApplyGridColumnLayout();
        RefreshVerticalLayoutImmediate();

        if (IsWideSkillsLayout())
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            RefreshVerticalLayoutImmediate();
        }

        _layoutCo = null;
    }

    private void ApplyGridColumnLayout()
    {
        RestoreAuthoredGridLayouts();

        if (!IsWideSkillsLayout())
            return;

        ApplyWideGridLayout(combatSkillsContent);
        ApplyWideGridLayout(gatheringSkillsContent);
        ApplyWideGridLayout(processingSkillsContent);
    }

    private void ApplyWideGridLayout(RectTransform contentRoot)
    {
        if (contentRoot == null)
            return;

        GridLayoutGroup grid = contentRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        const int wideColumns = 4;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = wideColumns;

        if (_authoredGridByRoot.TryGetValue(contentRoot, out GridLayoutSnapshot authored))
        {
            grid.cellSize = authored.CellSize;
            grid.spacing = authored.Spacing;
            grid.padding = authored.Padding;
        }
    }

    private void ConfigureScrollContentRoot()
    {
        if (listContent == null)
            return;

        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = listContent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        ContentSizeFitter fitter = listContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void RefreshVerticalLayoutImmediate()
    {
        bool wide = IsWideSkillsLayout();

        PrepareSectionForLayoutGroup(combatSkillsHeader);
        PrepareSectionForLayoutGroup(gatheringSkillsHeader);
        PrepareSectionForLayoutGroup(processingSkillsHeader);
        PrepareGridSectionForLayoutGroup(combatSkillsContent, wide);
        PrepareGridSectionForLayoutGroup(gatheringSkillsContent, wide);
        PrepareGridSectionForLayoutGroup(processingSkillsContent, wide);

        if (combatSkillsContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(combatSkillsContent);
        if (gatheringSkillsContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gatheringSkillsContent);
        if (processingSkillsContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(processingSkillsContent);

        if (listContent != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            Canvas.ForceUpdateCanvases();
        }

        if (_scrollRect != null)
            _scrollRect.normalizedPosition = new Vector2(0f, 1f);
    }

    private static void PrepareSectionForLayoutGroup(RectTransform section)
    {
        if (section == null)
            return;

        section.anchorMin = new Vector2(0f, 1f);
        section.anchorMax = new Vector2(1f, 1f);
        section.pivot = new Vector2(0.5f, 1f);
        section.anchoredPosition = Vector2.zero;

        LayoutElement le = section.GetComponent<LayoutElement>();
        if (le == null)
            le = section.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        float preferredHeight = LayoutUtility.GetPreferredHeight(section);
        if (preferredHeight > 1f)
            le.preferredHeight = preferredHeight;
    }

    private static void PrepareGridSectionForLayoutGroup(RectTransform section, bool wide)
    {
        if (section == null)
            return;

        LayoutElement le = section.GetComponent<LayoutElement>();
        if (le == null)
            le = section.gameObject.AddComponent<LayoutElement>();

        GridLayoutGroup grid = section.GetComponent<GridLayoutGroup>();

        if (wide && grid != null)
        {
            section.anchorMin = new Vector2(0f, 1f);
            section.anchorMax = new Vector2(0f, 1f);
            section.pivot = new Vector2(0f, 1f);
            section.anchoredPosition = Vector2.zero;

            int columns = grid.constraintCount > 0 ? grid.constraintCount : 4;
            int activeChildren = CountActiveLayoutChildren(section);
            int rows = Mathf.Max(1, Mathf.CeilToInt(activeChildren / (float)columns));

            float padH = grid.padding.left + grid.padding.right;
            float padV = grid.padding.top + grid.padding.bottom;
            Vector2 cell = grid.cellSize;
            Vector2 spacing = grid.spacing;

            le.flexibleWidth = 0f;
            le.minWidth = -1f;
            le.preferredWidth = padH + columns * cell.x + (columns - 1) * spacing.x;
            le.preferredHeight = padV + rows * cell.y + (rows - 1) * spacing.y;
            return;
        }

        section.anchorMin = new Vector2(0f, 1f);
        section.anchorMax = new Vector2(1f, 1f);
        section.pivot = new Vector2(0.5f, 1f);
        section.anchoredPosition = Vector2.zero;

        le.flexibleWidth = 1f;
        le.minWidth = -1f;
        le.preferredWidth = -1f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(section);
        le.preferredHeight = LayoutUtility.GetPreferredHeight(section);
    }

    private static int CountActiveLayoutChildren(RectTransform section)
    {
        int count = 0;
        for (int i = 0; i < section.childCount; i++)
        {
            Transform child = section.GetChild(i);
            if (child.gameObject.activeInHierarchy)
                count++;
        }

        return count;
    }

    private void WireScrollRect()
    {
        Transform scrollTransform = transform.Find(ScrollViewName);
        if (scrollTransform == null)
            scrollTransform = transform.Find(LegacyScrollViewName);
        if (scrollTransform == null)
            return;

        _scrollRect = scrollTransform.GetComponent<ScrollRect>();
        if (_scrollRect == null)
            return;

        if (listContent == null)
            listContent = scrollTransform.Find($"Viewport/{ContentName}") as RectTransform;

        RectTransform viewport = scrollTransform.Find("Viewport") as RectTransform;
        if (listContent != null)
            _scrollRect.content = listContent;
        if (viewport != null)
            _scrollRect.viewport = viewport;

        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
    }

    private void ResolveReferences()
    {
        if (listContent == null)
            listContent = FindScrollContent();

        ResolveContentRoot(ref combatSkillsContent, "CombatSkillsContent");
        ResolveContentRoot(ref gatheringSkillsContent, "GatheringSkillsContent");
        ResolveContentRoot(ref processingSkillsContent, "ProcessingSkillsContent");

        if (combatSkillsHeader == null)
            combatSkillsHeader = listContent != null ? listContent.Find("CombatSkillsHeader") as RectTransform : null;
        if (gatheringSkillsHeader == null)
            gatheringSkillsHeader = listContent != null ? listContent.Find("GatheringSkillsHeader") as RectTransform : null;
        if (processingSkillsHeader == null)
            processingSkillsHeader = listContent != null ? listContent.Find("ProcessingSkillsHeader") as RectTransform : null;
    }

    private RectTransform FindScrollContent()
    {
        Transform scroll = transform.Find(ScrollViewName);
        if (scroll == null)
            scroll = transform.Find(LegacyScrollViewName);

        if (scroll == null)
            return null;

        return scroll.Find($"Viewport/{ContentName}") as RectTransform;
    }

    private void ResolveContentRoot(ref RectTransform root, string objectName)
    {
        if (root != null)
            return;

        if (listContent != null)
            root = listContent.Find(objectName) as RectTransform;

        if (root == null)
            root = transform.Find(objectName) as RectTransform;
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            _skillsManager = SkillsManager.Instance;
        else if (_skillsManager == null)
            _skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }
}
