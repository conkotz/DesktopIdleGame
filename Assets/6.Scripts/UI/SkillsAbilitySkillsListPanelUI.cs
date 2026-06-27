using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates the NEW skills page Skills column with grouped combat, gathering, and processing rows.
/// Collapsed bottom bar: one column per section. Expanded: two-column grid per section.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillsAbilitySkillsListPanelUI : MonoBehaviour
{
    private const string ScrollViewName = "ScrollView";
    private const string ContentName = "Content";
    private const float HeaderHeight = 28f;
    private const float RowSpacing = 6f;
    private const float GridCellSize = 100f;

    [Header("Section headers")]
    [SerializeField] private RectTransform combatSkillsHeader;
    [SerializeField] private RectTransform gatheringSkillsHeader;
    [SerializeField] private RectTransform processingSkillsHeader;

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

    [SerializeField] private Transform listContent;
    [SerializeField] private SkillListEntryUI entryPrefab;

    private SkillDatabase _skillDatabase;
    private SkillsManager _skillsManager;
    private Action<SkillDefinition> _onSkillClicked;
    private Action<SkillDefinition> _onHoverAcknowledge;
    private SkillsAbilityBottomPanelLayoutUI _bottomPanelLayout;
    private bool _processingSubscribed;
    private bool _layoutSubscribed;
    private bool _gridExpanded;

    private RectTransform _combatEntriesRoot;
    private RectTransform _gatheringEntriesRoot;
    private RectTransform _processingEntriesRoot;

    private readonly Dictionary<SkillType, SkillListEntryUI> _entryBySkillType = new();
    private readonly List<SkillListEntryUI> _rows = new();

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

    public void SetEntryPrefab(SkillListEntryUI prefab)
    {
        if (prefab != null)
            entryPrefab = prefab;
    }

    /// <summary>Legacy hook — list always shows all skill groups now.</summary>
    public void SetVisibleCategory(SkillCategory category) => RebuildList();

    public void RebuildList()
    {
        EnsureReferences();
        SubscribeLayoutChanges();
        SubscribeProcessingChanges();
        ClearRowTracking();

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

        ApplyGridColumns(_gridExpanded);

        if (listContent is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
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
        if (_rows.Count == 0)
            RebuildList();
    }

    private void OnDisable()
    {
        UnsubscribeLayoutChanges();
        UnsubscribeProcessingChanges();
    }

    private void WireCombatEntry(SkillListEntryUI entry, SkillType skillType)
    {
        SkillDefinition def = FindSkill(skillType);
        entry = EnsureEntry(entry, _combatEntriesRoot);
        if (entry == null || def == null)
        {
            if (entry != null)
                entry.gameObject.SetActive(false);
            return;
        }

        entry.gameObject.SetActive(true);
        int level = _skillsManager != null ? _skillsManager.GetLevel(skillType) : 1;
        float progress01 = _skillsManager != null ? _skillsManager.GetProgress01(skillType) : 0f;
        int xp = _skillsManager != null ? _skillsManager.GetXpIntoLevel(skillType) : 0;
        entry.SetupWithXp(def, level, progress01, xp, false, HandleEntryClicked, _onHoverAcknowledge);
        TrackRow(entry, skillType);
    }

    private void WireGatheringEntry(SkillListEntryUI entry, SkillType skillType)
    {
        SkillDefinition def = FindSkill(skillType);
        entry = EnsureEntry(entry, _gatheringEntriesRoot);
        if (entry == null || def == null)
        {
            if (entry != null)
                entry.gameObject.SetActive(false);
            return;
        }

        entry.gameObject.SetActive(true);
        int level = _skillsManager != null ? _skillsManager.GetLevel(skillType) : 1;
        float progress01 = _skillsManager != null ? _skillsManager.GetProgress01(skillType) : 0f;
        int xp = _skillsManager != null ? _skillsManager.GetXpIntoLevel(skillType) : 0;
        entry.SetupWithXp(def, level, progress01, xp, false, HandleEntryClicked, _onHoverAcknowledge);
        TrackRow(entry, skillType);
    }

    private void WireProcessingEntry(SkillListEntryUI entry, ProcessingSkillType processingType)
    {
        entry = EnsureEntry(entry, _processingEntriesRoot);
        if (entry == null)
            return;

        ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
        int level = runtime.GetLevel(processingType);
        float progress01 = runtime.GetProgress01(processingType);
        int xp = runtime.GetXp(processingType);
        Sprite icon = entry.transform.Find("Icon")?.GetComponent<Image>()?.sprite;

        entry.gameObject.SetActive(true);
        entry.SetupDisplay(icon, level, progress01, xp, selected: false, interactable: false);
    }

    private static void WireProcessingPlaceholder(SkillListEntryUI entry)
    {
        if (entry == null)
            return;

        entry.gameObject.SetActive(true);
        Sprite icon = entry.transform.Find("Icon")?.GetComponent<Image>()?.sprite;
        entry.SetupDisplay(icon, level: 1, progress01: 0f, currentXp: 0, selected: false, interactable: false);
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

    private SkillListEntryUI EnsureEntry(SkillListEntryUI assigned, RectTransform parent)
    {
        if (parent == null)
            return assigned;

        if (assigned != null)
        {
            assigned.transform.SetParent(parent, false);
            return assigned;
        }

        if (entryPrefab == null)
            return null;

        SkillListEntryUI created = Instantiate(entryPrefab, parent);
        created.gameObject.SetActive(true);
        return created;
    }

    private void TrackRow(SkillListEntryUI entry, SkillType skillType)
    {
        if (entry == null)
            return;

        _entryBySkillType[skillType] = entry;
        if (!_rows.Contains(entry))
            _rows.Add(entry);
    }

    private void ClearRowTracking()
    {
        _rows.Clear();
        _entryBySkillType.Clear();
    }

    private void HandleEntryClicked(SkillDefinition skill) => _onSkillClicked?.Invoke(skill);

    private void SubscribeLayoutChanges()
    {
        if (_layoutSubscribed)
            return;

        _bottomPanelLayout = GetComponentInParent<SkillsAbilityBottomPanelLayoutUI>(true);
        if (_bottomPanelLayout == null)
            return;

        _bottomPanelLayout.ExpandedChanged += OnBottomPanelExpandedChanged;
        _gridExpanded = _bottomPanelLayout.IsExpanded;
        _layoutSubscribed = true;
    }

    private void UnsubscribeLayoutChanges()
    {
        if (!_layoutSubscribed || _bottomPanelLayout == null)
            return;

        _bottomPanelLayout.ExpandedChanged -= OnBottomPanelExpandedChanged;
        _layoutSubscribed = false;
    }

    private void OnBottomPanelExpandedChanged(bool expanded)
    {
        _gridExpanded = expanded;
        ApplyGridColumns(expanded);
    }

    private void SubscribeProcessingChanges()
    {
        if (_processingSubscribed)
            return;

        ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
        runtime.Changed += OnProcessingProficiencyChanged;
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

    private void ApplyGridColumns(bool expanded)
    {
        int columns = expanded ? 2 : 1;
        ApplyGridColumns(_combatEntriesRoot, columns);
        ApplyGridColumns(_gatheringEntriesRoot, columns);
        ApplyGridColumns(_processingEntriesRoot, columns);
    }

    private static void ApplyGridColumns(RectTransform entriesRoot, int columns)
    {
        if (entriesRoot == null)
            return;

        GridLayoutGroup grid = entriesRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        columns = Mathf.Clamp(columns, 1, 2);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        float width = entriesRoot.rect.width;
        if (width < 32f)
            width = GridCellSize * columns + grid.spacing.x;

        float spacing = grid.spacing.x;
        float pad = grid.padding.left + grid.padding.right;
        float cellWidth = (width - pad - spacing * (columns - 1)) / columns;
        cellWidth = Mathf.Max(72f, cellWidth);
        grid.cellSize = new Vector2(cellWidth, GridCellSize);

        LayoutRebuilder.ForceRebuildLayoutImmediate(entriesRoot);
    }

    private void EnsureReferences()
    {
        ResolveHeader(ref combatSkillsHeader, "CombatSkillsHeader");
        ResolveHeader(ref gatheringSkillsHeader, "GatheringSkillsHeader");
        ResolveHeader(ref processingSkillsHeader, "ProcessingSkillsHeader");

        EnsureScrollStructure();
        EnsureSectionStructure(
            combatSkillsHeader,
            "CombatSkillsEntries",
            ref _combatEntriesRoot);
        EnsureSectionStructure(
            gatheringSkillsHeader,
            "GatheringSkillsEntries",
            ref _gatheringEntriesRoot);
        EnsureSectionStructure(
            processingSkillsHeader,
            "ProcessingSkillsEntries",
            ref _processingEntriesRoot);

        if (entryPrefab == null)
        {
            SkillsAbilityPageNewUI page =
                GetComponentInParent<SkillsAbilityPageNewUI>(true)
                ?? FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);

            if (page != null && page.SkillEntryPrefab != null)
                entryPrefab = page.SkillEntryPrefab;
        }
    }

    private void ResolveHeader(ref RectTransform header, string objectName)
    {
        if (header == null)
            header = transform.Find(objectName) as RectTransform;
    }

    private void EnsureScrollStructure()
    {
        Transform scroll = transform.Find(ScrollViewName);
        if (scroll == null)
            scroll = BuildScrollView();

        Transform content = scroll.Find($"Viewport/{ContentName}");
        if (content == null)
            return;

        listContent = content;

        MoveHeaderIntoContent(combatSkillsHeader, content);
        MoveHeaderIntoContent(gatheringSkillsHeader, content);
        MoveHeaderIntoContent(processingSkillsHeader, content);

        var contentLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentLayout == null)
        {
            contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(4, 4, 4, 4);
            contentLayout.spacing = RowSpacing;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
        }

        if (content.GetComponent<ContentSizeFitter>() == null)
        {
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }

    private static void MoveHeaderIntoContent(RectTransform header, Transform content)
    {
        if (header == null || content == null || header.parent == content)
            return;

        header.SetParent(content, false);
    }

    private void EnsureSectionStructure(RectTransform header, string entriesName, ref RectTransform entriesRoot)
    {
        if (header == null)
            return;

        Transform content = listContent != null ? listContent : transform;
        entriesRoot = content.Find(entriesName) as RectTransform;
        if (entriesRoot == null)
        {
            var entriesGo = new GameObject(entriesName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            entriesRoot = entriesGo.GetComponent<RectTransform>();
            entriesRoot.SetParent(content, false);
            entriesRoot.SetSiblingIndex(header.GetSiblingIndex() + 1);
        }

        var grid = entriesRoot.GetComponent<GridLayoutGroup>();
        grid.spacing = new Vector2(RowSpacing, RowSpacing);
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.cellSize = new Vector2(GridCellSize, GridCellSize);

        var layoutElement = entriesRoot.GetComponent<LayoutElement>();
        layoutElement.minHeight = GridCellSize;
        layoutElement.flexibleWidth = 1f;

        if (header.GetComponent<LayoutElement>() == null)
        {
            var headerLe = header.gameObject.AddComponent<LayoutElement>();
            headerLe.preferredHeight = HeaderHeight;
            headerLe.flexibleWidth = 1f;
        }
    }

    private Transform BuildScrollView()
    {
        Transform selectionBar = transform.Find("CurrentSelectionBar");

        var scrollGo = new GameObject(ScrollViewName, typeof(RectTransform), typeof(ScrollRect));
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(transform, false);
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;
        if (selectionBar is RectTransform headerRt)
            scrollRt.offsetMax = new Vector2(0f, -(headerRt.rect.height > 1f ? headerRt.rect.height : HeaderHeight));

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.SetParent(scrollRt, false);
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

        var contentGo = new GameObject(ContentName, typeof(RectTransform));
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(viewportRt, false);
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        scrollGo.transform.SetAsLastSibling();
        return scrollRt;
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            _skillsManager = SkillsManager.Instance;
        else if (_skillsManager == null)
            _skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }
}
