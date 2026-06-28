using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Horizontal skill-tree Details panel (3-column RPG layout).
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillNodeDetailsPanelUI : MonoBehaviour
{
    public const float PanelWidth = 905f;
    public const float PanelHeight = 300f;

    public const float LeftColumnWidthRatio = 0.333f;
    public const float MiddleColumnWidthRatio = 0.333f;
    public const float RightColumnWidthRatio = 0.334f;

    public const float FontSectionHeader = 13f;
    public const float FontBody = 14f;
    public const float EffectBodyParagraphSpacing = 14f;
    public const float FontNameTitle = 19f;
    public const float FontCapstoneEnhancementName = 18f;
    public const float FontMeta = 14f;
    public const float FontEnhancementHeader = 14f;
    public const float FontEnhancementSubtitle = 13f;
    public const float FontEnhancementDetail = 13f;
    public const float FontEnhancementCardLevel = 12f;
    public const float FontEnhancementCardName = 14f;
    public const float FontEnhancementButton = 13f;
    public const float EnhancementCardHeight = 112f;
    public const float EnhancementButtonsRowHeight = 112f;
    public const int EnhancementButtonsGridColumnCount = 2;
    public const float EnhancementButtonsGridSpacing = 8f;
    public const int EnhancementButtonsGridPadding = 6;
    public const float EnhancementCardOutlineInset = 8f;
    public const float HeaderIconSize = 80f;

    private const float ContentHorizontalPadding = 16f;
    private const float ColumnSpacing = 0f;
    private const float ColumnDividerWidth = 1f;
    private const float RowDividerHeight = 2f;
    private const string DefaultEmptyMessage = "Click a node to view details";
    private static readonly Color ScalingTextColor = new(0.69f, 0.79f, 0.87f, 1f);
    private static readonly Color SectionDividerColor = new(0.55f, 0.48f, 0.36f, 0.65f);

    [Header("Roots")]
    [SerializeField] private GameObject emptyStateRoot;
    [SerializeField] private GameObject contentRoot;

    [Header("Left — Summary")]
    [SerializeField] private Image skillIconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelReqText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text unlockStateText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private GameObject requirementsSectionRoot;
    [SerializeField] private TMP_Text requirementWeaponText;
    [SerializeField] private GameObject typeSectionRoot;
    [SerializeField] private TMP_Text typeValueText;

    [Header("Middle — Combat")]
    [SerializeField] private GameObject scalingSectionRoot;
    [SerializeField] private TMP_Text scalingText;
    [SerializeField] private GameObject effectSectionRoot;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private GameObject costSectionRoot;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private GameObject cooldownSectionRoot;
    [SerializeField] private TMP_Text cooldownText;

    [Header("Right — Enhancements")]
    [SerializeField] private TMP_Text enhancementsTitleText;
    [SerializeField] private TMP_Text enhancementsSubtitleText;
    [SerializeField] private RectTransform enhancementsSectionRoot;
    [SerializeField] private Image enhancementsLockedOverlay;
    [SerializeField] private RectTransform enhancementButtonsContainer;
    [SerializeField] private SkillNodeDetailsEnhancementCardUI enhancementButtonTemplate;
    [SerializeField] private GameObject enhancementDetailRoot;
    [SerializeField] private TMP_Text enhancementDetailText;
    [SerializeField] private Button changeEnhancementButton;
    [SerializeField] private Button collapseDetailsButton;

    private const string SelectEnhancementButtonLabel = "SELECT ENHANCEMENT";
    private const string ChangeEnhancementButtonLabel = "CHANGE ENHANCEMENT";

    private static readonly Color EnhancementsLockedOverlayColor = new(0.72f, 0.1f, 0.08f, 0.48f);

    private readonly List<SkillNodeDetailsEnhancementCardUI> _spawnedEnhancementButtons = new();
    private SkillTimelineNodeBinding _currentBinding;
    private SkillDefinition _currentSkill;
    private ProcessingSkillDisplayCatalog.Id? _currentProcessingSkill;
    private AbilityIconDragAssignUI _abilityIconDragAssign;
    private int _previewEnhancementIndex = -1;
    private int _committedEnhancementIndex = -1;
    private bool _detailsInteriorExpanded;
    private int _visibleEnhancementChoiceCount;
    private Coroutine _enhancementScrollRoutine;
    private bool _columnScrollViewsEnsured;
    private Coroutine _deferredColumnsLayoutCo;
    private Coroutine _deferredDividerRefreshCo;
    private UnityEngine.Events.UnityAction _enhancementActionHandler;
    private UnityEngine.Events.UnityAction _collapseDetailsHandler;

    /// <summary>Fired when the panel clears (no node selected).</summary>
    public event System.Action DetailsDismissed;

    public SkillTimelineNodeBinding CurrentBinding => _currentBinding;

    public SkillDefinition CurrentSkill => _currentSkill;

    public bool HasActiveDetails =>
        _currentBinding != null || _currentSkill != null || _currentProcessingSkill.HasValue;

    private void Awake()
    {
        ApplyPanelSize();
        ApplyFixedThirdColumnLayout();
        ApplySectionDividerLayout();
        ApplySectionTextStackLayout();
        EnsureColumnBodyScrollViews();
        ApplyDetailsTypography();
        EnsureEnhancementsLockedOverlay();
        WireEnhancementActionButton();
        RefreshColumnsLayout();
        ShowEmpty();
    }

    private void OnEnable()
    {
        WireCollapseDetailsButton();
        RefreshCollapseButtonVisible();
    }

    private void OnDisable()
    {
        if (collapseDetailsButton != null && _collapseDetailsHandler != null)
            collapseDetailsButton.onClick.RemoveListener(_collapseDetailsHandler);
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyPanelSize();
        RefreshColumnsLayout();
    }

    /// <summary>Re-applies details column widths (call after panel resize).</summary>
    public void RefreshColumnsLayout()
    {
        ApplyDetailsInteriorLayout(_detailsInteriorExpanded);
        ApplySectionDividerLayout();
        ApplySectionTextStackLayout();
        ConfigureEnhancementColumnLayout();
    }

    private void ScheduleDeferredColumnsLayout()
    {
        if (!isActiveAndEnabled)
            return;

        if (_deferredColumnsLayoutCo != null)
            StopCoroutine(_deferredColumnsLayoutCo);

        _deferredColumnsLayoutCo = StartCoroutine(DeferredColumnsLayoutRoutine());
    }

    private IEnumerator DeferredColumnsLayoutRoutine()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        RefreshColumnsLayout();
        ApplyEnhancementButtonsContainerLayout(_spawnedEnhancementButtons.Count);
        _deferredColumnsLayoutCo = null;
    }

    public static float ComputeColumnWidth(float ratio)
    {
        float inner = PanelWidth - ContentHorizontalPadding - ColumnSpacing * 2f - ColumnDividerWidth * 2f;
        return inner * ratio;
    }

    public void ApplyPanelSize()
    {
        RectTransform rt = transform as RectTransform;
        if (rt == null)
            return;

        RectTransform parent = rt.parent as RectTransform;
        bool stretchInParent = parent != null
            && (parent.name == "ViewDetailsContent"
                || parent.name == "CurrentSelectionContent"
                || parent.GetComponent<HorizontalLayoutGroup>() != null
                || parent.GetComponent<VerticalLayoutGroup>() != null);

        if (stretchInParent)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PanelWidth);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, PanelHeight);
        }

        LayoutElement le = GetComponent<LayoutElement>();
        if (le == null)
            le = gameObject.AddComponent<LayoutElement>();

        if (stretchInParent)
        {
            le.minWidth = 0f;
            le.minHeight = 0f;
            le.preferredWidth = -1f;
            le.preferredHeight = -1f;
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
        }
        else
        {
            le.preferredWidth = PanelWidth;
            le.preferredHeight = PanelHeight;
            le.minWidth = PanelWidth;
            le.minHeight = PanelHeight;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
        }
    }

    /// <summary>Clears the open node, collapses the bottom details column, and notifies listeners.</summary>
    public void Dismiss() => ShowEmpty();

    public void ShowEmpty(string message = DefaultEmptyMessage)
    {
        _currentBinding = null;
        _currentSkill = null;
        _currentProcessingSkill = null;
        ClearEnhancementButtons();
        SetEnhancementsLockedOverlay(false);
        if (enhancementsSubtitleText != null)
            enhancementsSubtitleText.gameObject.SetActive(false);
        RefreshEnhancementActionButton(locked: true, committedIndex: -1);
        DetailsDismissed?.Invoke();

        if (levelReqText != null)
        {
            levelReqText.text = string.Empty;
            levelReqText.gameObject.SetActive(false);
        }

        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(true);
        if (contentRoot != null)
            contentRoot.SetActive(false);

        BindAbilityIconDragAssign(null, null);

        TMP_Text emptyLabel = emptyStateRoot != null
            ? emptyStateRoot.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (emptyLabel != null)
            emptyLabel.text = message;

        NotifyBottomPanelLayout(null);
        ApplyDetailsInteriorLayout(expanded: false);
        ScheduleDeferredColumnsLayout();
        RefreshCollapseButtonVisible();
    }

    public void Show(SkillDefinition skill, SkillsManager skillsManager = null, SkillsAbilitySkillsListPanelUI listPanel = null)
    {
        if (skill == null)
        {
            ShowEmpty();
            return;
        }

        _currentBinding = null;
        _currentSkill = skill;
        _currentProcessingSkill = null;
        _previewEnhancementIndex = -1;
        _committedEnhancementIndex = -1;

        if (skillsManager == null)
        {
            skillsManager = SkillsManager.Instance;
            if (skillsManager == null)
                skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
        }

        int level = skillsManager != null ? skillsManager.GetLevel(skill.skillType) : 1;

        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(false);
        if (contentRoot != null)
            contentRoot.SetActive(true);

        ClearEnhancementButtons();
        SetEnhancementsLockedOverlay(false);
        if (enhancementsSubtitleText != null)
            enhancementsSubtitleText.gameObject.SetActive(false);
        RefreshEnhancementActionButton(locked: true, committedIndex: -1);

        if (skillIconImage != null)
        {
            Sprite icon = ResolveSkillListIcon(skill, listPanel) ?? skill.icon;
            skillIconImage.sprite = icon;
            skillIconImage.enabled = icon != null;
        }

        EnsureLevelReqTextReference();

        if (nameText != null)
            nameText.text = skill.displayName ?? skill.skillType.ToString();

        if (levelReqText != null)
        {
            levelReqText.gameObject.SetActive(true);
            levelReqText.text = $"Lv {level}";
        }

        if (typeText != null)
            typeText.text = "Skill";

        if (unlockStateText != null)
        {
            unlockStateText.text = string.Empty;
            unlockStateText.gameObject.SetActive(false);
        }

        if (descriptionText != null)
            descriptionText.text = skill.description ?? string.Empty;

        BindTypeSection(null, FormatSkillCategoryTypeLabel(skill.category));
        BindAbilityIconDragAssign(null, null);
        SetSectionActive(scalingSectionRoot, false);
        SetSectionActive(effectSectionRoot, false);
        SetSectionActive(costSectionRoot, false);
        SetSectionActive(cooldownSectionRoot, false);
        SetCostCooldownRowVisible(false);
        BindRequirementsSection(null, null, null, -1);

        ApplyDetailsTypography();
        ApplySectionDividerLayout();
        EnsureDetailsPanelDividerLines();
        UpdateDetailsPanelDividerVisibility();
        ScheduleDeferredDividerRefresh();
        ResetColumnBodyScrollPositions();
        NotifyBottomPanelLayout(null);
        ApplyDetailsInteriorLayout(expanded: false);
        ScheduleDeferredColumnsLayout();
        RefreshCollapseButtonVisible();
    }

    public void Show(ProcessingSkillDisplayCatalog.Id processingSkill, SkillsAbilitySkillsListPanelUI listPanel = null)
    {
        _currentBinding = null;
        _currentSkill = null;
        _currentProcessingSkill = processingSkill;
        _previewEnhancementIndex = -1;
        _committedEnhancementIndex = -1;

        int level = 1;
        if (ProcessingSkillDisplayCatalog.TryGetProficiencyType(processingSkill, out ProcessingSkillType proficiencyType))
        {
            ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
            level = runtime.GetLevel(proficiencyType);
        }

        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(false);
        if (contentRoot != null)
            contentRoot.SetActive(true);

        ClearEnhancementButtons();
        SetEnhancementsLockedOverlay(false);
        if (enhancementsSubtitleText != null)
            enhancementsSubtitleText.gameObject.SetActive(false);
        RefreshEnhancementActionButton(locked: true, committedIndex: -1);

        if (skillIconImage != null)
        {
            Sprite icon = listPanel != null ? listPanel.TryGetProcessingListIcon(processingSkill) : null;
            skillIconImage.sprite = icon;
            skillIconImage.enabled = icon != null;
        }

        EnsureLevelReqTextReference();

        if (nameText != null)
            nameText.text = ProcessingSkillDisplayCatalog.GetDisplayName(processingSkill);

        if (levelReqText != null)
        {
            levelReqText.gameObject.SetActive(true);
            levelReqText.text = $"Lv {level}";
        }

        if (typeText != null)
            typeText.text = "Skill";

        if (unlockStateText != null)
        {
            unlockStateText.text = string.Empty;
            unlockStateText.gameObject.SetActive(false);
        }

        if (descriptionText != null)
            descriptionText.text = ProcessingSkillDisplayCatalog.GetDescription(processingSkill);

        BindTypeSection(null, "Processing Skill");
        BindAbilityIconDragAssign(null, null);
        SetSectionActive(scalingSectionRoot, false);
        SetSectionActive(effectSectionRoot, false);
        SetSectionActive(costSectionRoot, false);
        SetSectionActive(cooldownSectionRoot, false);
        SetCostCooldownRowVisible(false);
        BindRequirementsSection(null, null, null, -1);

        ApplyDetailsTypography();
        ApplySectionDividerLayout();
        EnsureDetailsPanelDividerLines();
        UpdateDetailsPanelDividerVisibility();
        ScheduleDeferredDividerRefresh();
        ResetColumnBodyScrollPositions();
        NotifyBottomPanelLayout(null);
        ApplyDetailsInteriorLayout(expanded: false);
        ScheduleDeferredColumnsLayout();
        RefreshCollapseButtonVisible();
    }

    public void Show(SkillTimelineNodeBinding binding)
    {
        if (binding == null)
        {
            ShowEmpty();
            return;
        }

        _currentSkill = null;
        _currentProcessingSkill = null;

        bool switchingNode = !IsSameEnhancementBinding(_currentBinding, binding);
        _currentBinding = binding;

        if (switchingNode)
        {
            _previewEnhancementIndex = -1;
            _committedEnhancementIndex = -1;
        }

        SkillsManager skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);

        if (!SkillTreeNodeTooltipFormatter.TryBuildForTimelineNode(binding, skillsManager, out SkillTreeNodeTooltipFormatter.DetailsContent details)
            || !details.HasContent)
        {
            ShowEmpty("No details for this node.");
            return;
        }

        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(false);
        if (contentRoot != null)
            contentRoot.SetActive(true);

        CharacterStats stats = AbilityTooltipDamagePreview.FindLocalPlayerStats();

        if (skillIconImage != null)
        {
            skillIconImage.sprite = details.Icon;
            skillIconImage.enabled = details.Icon != null;
        }

        EnsureLevelReqTextReference();

        if (nameText != null)
            nameText.text = details.Title ?? string.Empty;

        if (levelReqText != null)
        {
            bool hasLevel = !string.IsNullOrWhiteSpace(details.LevelText);
            levelReqText.gameObject.SetActive(hasLevel);
            if (hasLevel)
                levelReqText.text = details.LevelText;
        }

        if (typeText != null)
            typeText.text = details.TypeLabel;

        if (unlockStateText != null)
            unlockStateText.text = details.StatusRichText;

        if (descriptionText != null)
            descriptionText.text = details.Description ?? string.Empty;

        AbilityDefinition ability = ResolveAbility(binding);
        BindTypeSection(ability, details.TypeLabel);
        BindAbilityIconDragAssign(ability, skillsManager);
        BindMiddleColumn(ability, skillsManager, stats, binding, details.EffectText, details.ScalingText);
        PopulateEnhancementCards(binding, skillsManager);
        BindRequirementsSection(
            ability,
            stats,
            binding?.Unlock,
            ResolveDisplayedCapstoneChoiceIndex(binding, skillsManager));
        ApplyDetailsTypography();
        ApplySectionDividerLayout();
        EnsureDetailsPanelDividerLines();
        UpdateDetailsPanelDividerVisibility();
        ScheduleDeferredDividerRefresh();
        ResetColumnBodyScrollPositions();
        bool expandBottomBar = SkillsAbilityBottomPanelLayoutRules.ShouldExpandBottomBar(binding);
        NotifyBottomPanelLayout(binding);
        ApplyDetailsInteriorLayout(expandBottomBar);
        ScheduleDeferredColumnsLayout();
        RefreshCollapseButtonVisible();
    }

    private void ApplyDetailsInteriorLayout(bool expanded)
    {
        _detailsInteriorExpanded = expanded;

        if (contentRoot == null)
            return;

        Transform columnsRoot = contentRoot.transform.Find("ColumnsRoot");
        if (columnsRoot == null)
            return;

        RectTransform middle = columnsRoot.Find("MiddleSection") as RectTransform;
        RectTransform right = columnsRoot.Find("RightSection") as RectTransform;

        for (int i = 0; i < columnsRoot.childCount; i++)
        {
            Transform child = columnsRoot.GetChild(i);
            if (child.name == "ColumnDivider")
                child.gameObject.SetActive(expanded);
        }

        if (middle != null)
            middle.gameObject.SetActive(expanded);
        if (right != null)
            right.gameObject.SetActive(expanded);

        if (expanded)
            ApplyFixedThirdColumnLayout();
        else
            ApplyCollapsedDetailsColumnLayout();

        RebuildLeftSectionSummaryLayout();
    }

    private void ApplyCollapsedDetailsColumnLayout()
    {
        if (contentRoot == null)
            return;

        Transform columnsRoot = contentRoot.transform.Find("ColumnsRoot");
        if (columnsRoot is not RectTransform columnsRt)
            return;

        HorizontalLayoutGroup columnsLayout = columnsRoot.GetComponent<HorizontalLayoutGroup>();
        if (columnsLayout != null)
            columnsLayout.enabled = false;

        float totalWidth = ResolveCollapsedColumnWidth(columnsRt);

        RectTransform left = columnsRoot.Find("LeftSection") as RectTransform;
        if (left != null)
            PlaceFixedWidthColumn(left, 0f, totalWidth);
    }

    private float ResolveCollapsedColumnWidth(RectTransform columnsRt)
    {
        if (columnsRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(columnsRt);

        RectTransform panelRt = transform as RectTransform;
        if (panelRt != null && panelRt.rect.width > 1f)
            return panelRt.rect.width;

        if (contentRoot != null && contentRoot.transform is RectTransform contentRt && contentRt.rect.width > 1f)
            return contentRt.rect.width;

        if (columnsRt != null && columnsRt.rect.width > 1f && columnsRt.rect.width <= CollapsedBarWidth + 48f)
            return columnsRt.rect.width;

        return collapsedDetailsPreferredWidthFallback();
    }

    private void RebuildLeftSectionSummaryLayout()
    {
        if (contentRoot == null)
            return;

        Transform left = contentRoot.transform.Find("ColumnsRoot/LeftSection");
        if (left == null)
            return;

        Transform topRow = left.Find("TopRow");
        if (topRow == null)
            return;

        if (topRow.TryGetComponent(out LayoutElement topRowElement))
        {
            topRowElement.minHeight = HeaderIconSize;
            topRowElement.preferredHeight = -1f;
            topRowElement.flexibleHeight = 0f;
        }

        if (topRow.TryGetComponent(out HorizontalLayoutGroup topRowLayout))
        {
            topRowLayout.childAlignment = TextAnchor.UpperLeft;
            topRowLayout.spacing = 12f;
            topRowLayout.childControlWidth = true;
            topRowLayout.childControlHeight = true;
            topRowLayout.childForceExpandWidth = false;
            topRowLayout.childForceExpandHeight = true;
        }

        Transform skillIcon = topRow.Find("SkillIcon");
        if (skillIcon != null && skillIcon.TryGetComponent(out LayoutElement iconElement))
        {
            iconElement.minWidth = HeaderIconSize;
            iconElement.minHeight = HeaderIconSize;
            iconElement.preferredWidth = HeaderIconSize;
            iconElement.preferredHeight = HeaderIconSize;
            iconElement.flexibleWidth = 0f;
            iconElement.flexibleHeight = 0f;
        }

        if (skillIconImage != null)
            skillIconImage.preserveAspect = true;

        Transform nameBlock = topRow.Find("NameBlock");
        if (nameBlock != null)
        {
            if (nameBlock.TryGetComponent(out LayoutElement nameLayout))
            {
                nameLayout.minWidth = 0f;
                nameLayout.flexibleWidth = 1f;
                nameLayout.flexibleHeight = 1f;
                nameLayout.minHeight = HeaderIconSize;
            }

            if (nameBlock.TryGetComponent(out VerticalLayoutGroup nameVlg))
            {
                nameVlg.childAlignment = TextAnchor.UpperLeft;
                nameVlg.childControlWidth = true;
                nameVlg.childControlHeight = true;
                nameVlg.childForceExpandWidth = true;
                nameVlg.childForceExpandHeight = false;
            }
        }

        if (left is RectTransform leftRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(leftRt);

        if (topRow is RectTransform topRowRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(topRowRt);
    }

    public static float CollapsedBarWidth => PanelWidth / 3f;

    private static float collapsedDetailsPreferredWidthFallback() => CollapsedBarWidth;

    private static void NotifyBottomPanelLayout(SkillTimelineNodeBinding binding)
    {
        SkillsAbilityBottomPanelLayoutUI layout = FindBottomPanelLayout();
        if (layout == null)
            return;

        if (binding == null)
            layout.SetExpanded(false);
        else
            layout.ApplyForBinding(binding);
    }

    private static SkillsAbilityBottomPanelLayoutUI FindBottomPanelLayout()
    {
        SkillsAbilityPageNewUI page = FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);
        if (page == null)
            return null;

        Transform bar = page.transform.Find("BottomPanelBar");
        if (bar == null)
            return null;

        SkillsAbilityBottomPanelLayoutUI layout = bar.GetComponent<SkillsAbilityBottomPanelLayoutUI>();
        if (layout == null)
            layout = bar.gameObject.AddComponent<SkillsAbilityBottomPanelLayoutUI>();
        return layout;
    }

    /// <summary>
    /// Three columns at fixed 1/3 width each (independent of node content).
    /// Disables <see cref="HorizontalLayoutGroup"/> so empty middle columns do not collapse.
    /// </summary>
    private void ApplyFixedThirdColumnLayout()
    {
        if (contentRoot == null)
            return;

        RectTransform contentRt = contentRoot.transform as RectTransform;
        if (contentRt != null)
        {
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
        }

        Transform columnsRoot = contentRoot.transform.Find("ColumnsRoot");
        if (columnsRoot == null)
            return;

        RectTransform columnsRt = columnsRoot as RectTransform;
        if (columnsRt == null)
            return;

        columnsRt.anchorMin = Vector2.zero;
        columnsRt.anchorMax = Vector2.one;
        columnsRt.offsetMin = Vector2.zero;
        columnsRt.offsetMax = Vector2.zero;

        HorizontalLayoutGroup columnsLayout = columnsRoot.GetComponent<HorizontalLayoutGroup>();
        if (columnsLayout != null)
            columnsLayout.enabled = false;

        float totalWidth = columnsRt.rect.width;
        if (totalWidth < 50f)
            totalWidth = PanelWidth;

        float columnWidth = (totalWidth - ColumnDividerWidth * 2f) / 3f;
        if (columnWidth < 1f)
            columnWidth = 1f;

        RectTransform left = columnsRoot.Find("LeftSection") as RectTransform;
        RectTransform middle = columnsRoot.Find("MiddleSection") as RectTransform;
        RectTransform right = columnsRoot.Find("RightSection") as RectTransform;

        var columnDividers = new List<RectTransform>(2);
        for (int i = 0; i < columnsRoot.childCount; i++)
        {
            if (columnsRoot.GetChild(i).name != "ColumnDivider")
                continue;
            if (columnsRoot.GetChild(i) is RectTransform dividerRt)
                columnDividers.Add(dividerRt);
        }

        float x = 0f;
        if (left != null)
        {
            PlaceFixedWidthColumn(left, x, columnWidth);
            x += columnWidth;
        }

        if (columnDividers.Count > 0)
        {
            PlaceFixedWidthColumnDivider(columnDividers[0], x);
            x += ColumnDividerWidth;
        }

        if (middle != null)
        {
            PlaceFixedWidthColumn(middle, x, columnWidth);
            x += columnWidth;
        }

        if (columnDividers.Count > 1)
        {
            PlaceFixedWidthColumnDivider(columnDividers[1], x);
            x += ColumnDividerWidth;
        }

        if (right != null)
            PlaceFixedWidthColumn(right, x, columnWidth);
    }

    private static void PlaceFixedWidthColumn(RectTransform column, float x, float width)
    {
        column.anchorMin = new Vector2(0f, 0f);
        column.anchorMax = new Vector2(0f, 1f);
        column.pivot = new Vector2(0f, 0.5f);
        column.anchoredPosition = new Vector2(x, 0f);
        column.sizeDelta = new Vector2(width, 0f);

        LayoutElement layout = column.GetComponent<LayoutElement>();
        if (layout != null)
            layout.ignoreLayout = true;
    }

    private static void PlaceFixedWidthColumnDivider(RectTransform divider, float x)
    {
        divider.anchorMin = new Vector2(0f, 0f);
        divider.anchorMax = new Vector2(0f, 1f);
        divider.pivot = new Vector2(0f, 0.5f);
        divider.anchoredPosition = new Vector2(x, 0f);
        divider.sizeDelta = new Vector2(ColumnDividerWidth, 0f);

        LayoutElement layout = divider.GetComponent<LayoutElement>();
        if (layout != null)
            layout.ignoreLayout = true;
    }

    public static void PrepareHorizontalLayoutColumn(RectTransform rt)
    {
        PlaceFixedWidthColumn(rt, 0f, rt.rect.width > 1f ? rt.rect.width : ComputeColumnWidth(LeftColumnWidthRatio));
    }

    public static void PrepareHorizontalLayoutDivider(RectTransform rt)
    {
        PlaceFixedWidthColumnDivider(rt, 0f);
    }

    public static void PrepareVerticalLayoutDivider(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, RowDividerHeight);

        LayoutElement layout = rt.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rt.gameObject.AddComponent<LayoutElement>();

        layout.ignoreLayout = false;
        layout.minHeight = RowDividerHeight;
        layout.preferredHeight = RowDividerHeight;
        layout.flexibleHeight = 0f;
        layout.flexibleWidth = 1f;
    }

    private void ApplySectionDividerLayout()
    {
        if (contentRoot == null)
            return;

        Transform columnsRoot = contentRoot.transform.Find("ColumnsRoot");
        if (columnsRoot == null)
            return;

        ApplySectionDividerLayoutOnColumn(columnsRoot.Find("LeftSection") as RectTransform);
        ApplySectionDividerLayoutOnColumn(columnsRoot.Find("MiddleSection") as RectTransform);
        ApplySectionDividerLayoutOnColumn(columnsRoot.Find("RightSection") as RectTransform);
    }

    private static void ApplySectionDividerLayoutOnColumn(RectTransform section)
    {
        if (section == null)
            return;

        VerticalLayoutGroup columnVlg = section.GetComponent<VerticalLayoutGroup>();
        if (columnVlg != null)
        {
            columnVlg.childForceExpandWidth = true;
            columnVlg.childForceExpandHeight = false;
        }

        StyleDividersUnder(section);
    }

    private static void StyleDividersUnder(Transform root)
    {
        if (root == null)
            return;

        if (root.name == "Divider" && root is RectTransform dividerRt)
            PrepareVerticalLayoutDivider(dividerRt);

        for (int i = 0; i < root.childCount; i++)
            StyleDividersUnder(root.GetChild(i));
    }

    /// <summary>
    /// Ensures section dividers exist in scroll content and before pinned footers. Safe to call when opening a node.
    /// </summary>
    private void EnsureDetailsPanelDividerLines()
    {
        if (contentRoot == null)
            return;

        Transform columnsRoot = contentRoot.transform.Find("ColumnsRoot");
        if (columnsRoot == null)
            return;

        Transform left = columnsRoot.Find("LeftSection");
        Transform middle = columnsRoot.Find("MiddleSection");

        Transform leftScrollContent = left != null ? left.Find("LeftBodyScroll/Viewport/Content") : null;
        if (leftScrollContent != null)
        {
            Transform description = leftScrollContent.Find("DescriptionSection");
            Transform requirements = leftScrollContent.Find("RequirementsSection");
            Transform typeSection = leftScrollContent.Find("TypeSection");

            if (description != null)
                EnsureDividerBefore(leftScrollContent, description);
            if (requirements != null)
            {
                EnsureDividerBefore(leftScrollContent, requirements);
                EnsureDividerAfter(leftScrollContent, requirements);
            }
            if (typeSection != null)
                EnsureDividerBefore(leftScrollContent, typeSection);
        }
        else if (left != null)
        {
            Transform description = left.Find("DescriptionSection");
            Transform requirements = left.Find("RequirementsSection");
            Transform typeSection = left.Find("TypeSection");
            if (description != null)
                EnsureDividerBefore(left, description);
            if (requirements != null)
            {
                EnsureDividerBefore(left, requirements);
                EnsureDividerAfter(left, requirements);
            }
            if (typeSection != null)
                EnsureDividerBefore(left, typeSection);
        }

        Transform middleScrollContent = middle != null ? middle.Find("MiddleBodyScroll/Viewport/Content") : null;
        if (middleScrollContent != null)
        {
            Transform scaling = middleScrollContent.Find("ScalingSection");
            Transform effect = middleScrollContent.Find("EffectSection");
            if (effect != null)
                EnsureDividerBefore(middleScrollContent, effect);
            else if (scaling != null)
                EnsureDividerAfter(middleScrollContent, scaling);
        }
        else if (middle != null)
        {
            Transform effect = middle.Find("EffectSection");
            if (effect != null)
                EnsureDividerBefore(middle, effect);
        }

        Transform costRow = middle != null ? middle.Find("CostCooldownRow") : null;
        if (costRow != null)
            EnsureDividerBefore(middle, costRow);

        UpdateDetailsPanelDividerVisibility();

        if (left != null)
            StyleDividersUnder(left);
        if (middle != null)
            StyleDividersUnder(middle);
    }

    private void UpdateDetailsPanelDividerVisibility()
    {
        if (contentRoot == null)
            return;

        Transform columnsRoot = contentRoot.transform.Find("ColumnsRoot");
        if (columnsRoot == null)
            return;

        Transform left = columnsRoot.Find("LeftSection");
        Transform middle = columnsRoot.Find("MiddleSection");
        Transform leftContent = left != null ? left.Find("LeftBodyScroll/Viewport/Content") : null;
        Transform middleContent = middle != null ? middle.Find("MiddleBodyScroll/Viewport/Content") : null;

        bool hasRequirements = requirementsSectionRoot != null && requirementsSectionRoot.activeInHierarchy;
        bool hasType = typeSectionRoot != null && typeSectionRoot.activeInHierarchy;
        bool hasScaling = scalingSectionRoot != null && scalingSectionRoot.activeInHierarchy;
        bool hasEffect = effectSectionRoot != null && effectSectionRoot.activeInHierarchy;

        SetDividerBeforeSection(leftContent ?? left, "RequirementsSection", hasRequirements);
        SetDividerBeforeSection(leftContent ?? left, "TypeSection", hasType);

        Transform requirementsSection = leftContent != null
            ? leftContent.Find("RequirementsSection")
            : left != null ? left.Find("RequirementsSection") : null;
        if (requirementsSection != null)
            SetRequirementsTrailingDividerVisible(requirementsSection, hasType);

        SetDividerBeforeSection(middleContent ?? middle, "EffectSection", hasScaling && hasEffect);
        SetDividerAfterSection(middleContent != null ? middleContent.Find("ScalingSection") : middle?.Find("ScalingSection"), hasScaling && hasEffect);

        Transform costRow = middle != null ? middle.Find("CostCooldownRow") : null;
        if (costRow != null)
        {
            Transform costDivider = FindDividerImmediatelyBefore(costRow);
            if (costDivider != null)
                costDivider.gameObject.SetActive(costRow.gameObject.activeInHierarchy);
        }
    }

    private static void SetDividerBeforeSection(Transform parent, string sectionName, bool visible)
    {
        if (parent == null || string.IsNullOrEmpty(sectionName))
            return;

        Transform section = parent.Find(sectionName);
        if (section == null)
            return;

        Transform divider = FindDividerImmediatelyBefore(section);
        if (divider != null)
            divider.gameObject.SetActive(visible);
    }

    private static void SetRequirementsTrailingDividerVisible(Transform requirementsSection, bool visible)
    {
        if (requirementsSection == null)
            return;

        SetDividerAfterSection(requirementsSection, visible);

        int nextIndex = requirementsSection.GetSiblingIndex() + 1;
        if (nextIndex >= requirementsSection.parent.childCount)
            return;

        Transform next = requirementsSection.parent.GetChild(nextIndex);
        if (next != null && next.name == "TypeSection")
            SetDividerBeforeSection(requirementsSection.parent, "TypeSection", visible);
    }

    private static void SetDividerAfterSection(Transform section, bool visible)
    {
        if (section == null || section.parent == null)
            return;

        int nextIndex = section.GetSiblingIndex() + 1;
        if (nextIndex >= section.parent.childCount)
            return;

        Transform next = section.parent.GetChild(nextIndex);
        if (next != null && next.name == "Divider")
            next.gameObject.SetActive(visible);
    }

    private void ScheduleDeferredDividerRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        if (_deferredDividerRefreshCo != null)
            StopCoroutine(_deferredDividerRefreshCo);

        _deferredDividerRefreshCo = StartCoroutine(DeferredDividerRefreshRoutine());
    }

    private IEnumerator DeferredDividerRefreshRoutine()
    {
        yield return null;

        if (requirementWeaponText != null)
            requirementWeaponText.ForceMeshUpdate();

        if (requirementsSectionRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(requirementsSectionRoot.transform as RectTransform);

        if (contentRoot != null)
        {
            Transform leftContent = contentRoot.transform.Find("ColumnsRoot/LeftSection/LeftBodyScroll/Viewport/Content");
            if (leftContent is RectTransform leftContentRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(leftContentRt);
        }

        Canvas.ForceUpdateCanvases();
        EnsureDetailsPanelDividerLines();
        UpdateDetailsPanelDividerVisibility();
        _deferredDividerRefreshCo = null;
    }

    private static void EnsureDividerBefore(Transform parent, Transform sibling)
    {
        if (parent == null || sibling == null || sibling.parent != parent)
            return;

        Transform existing = FindDividerImmediatelyBefore(sibling);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        CreateSectionDivider(parent, sibling.GetSiblingIndex());
    }

    private static void EnsureDividerAfter(Transform parent, Transform sibling)
    {
        if (parent == null || sibling == null || sibling.parent != parent)
            return;

        int nextIndex = sibling.GetSiblingIndex() + 1;
        if (nextIndex < parent.childCount && parent.GetChild(nextIndex).name == "Divider")
        {
            parent.GetChild(nextIndex).gameObject.SetActive(true);
            return;
        }

        CreateSectionDivider(parent, nextIndex);
    }

    private static Transform CreateSectionDivider(Transform parent, int siblingIndex)
    {
        var dividerGo = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var dividerRt = dividerGo.GetComponent<RectTransform>();
        dividerRt.SetParent(parent, false);
        dividerRt.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));

        Image img = dividerGo.GetComponent<Image>();
        img.color = SectionDividerColor;
        img.raycastTarget = false;

        PrepareVerticalLayoutDivider(dividerRt);
        return dividerRt;
    }

    private void ApplySectionTextStackLayout()
    {
        ConfigureSectionTextStack(scalingSectionRoot);
        ConfigureSectionTextStack(effectSectionRoot);
        ConfigureSectionTextStack(costSectionRoot);
        ConfigureSectionTextStack(cooldownSectionRoot);
        ConfigureSectionTextStack(enhancementDetailRoot);
        ConfigureRequirementsTextLayout();
    }

    private void ConfigureRequirementsTextLayout()
    {
        ConfigureSectionTextStack(requirementsSectionRoot);
        if (requirementWeaponText == null)
            return;

        requirementWeaponText.textWrappingMode = TextWrappingModes.Normal;
        requirementWeaponText.overflowMode = TextOverflowModes.Overflow;

        if (requirementWeaponText.GetComponent<ContentSizeFitter>() == null)
        {
            ContentSizeFitter fitter = requirementWeaponText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        LayoutElement layout = requirementWeaponText.GetComponent<LayoutElement>();
        if (layout == null)
            layout = requirementWeaponText.gameObject.AddComponent<LayoutElement>();
        layout.flexibleHeight = 0f;
    }

    private static void ConfigureSectionTextStack(GameObject sectionRoot)
    {
        if (sectionRoot == null)
            return;

        VerticalLayoutGroup vlg = sectionRoot.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
            return;

        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    private void EnsureColumnBodyScrollViews()
    {
        if (contentRoot == null)
            return;

        Transform columnsRoot = contentRoot.transform.Find("ColumnsRoot");
        if (columnsRoot == null)
            return;

        Transform left = columnsRoot.Find("LeftSection");
        Transform middle = columnsRoot.Find("MiddleSection");

        if (_columnScrollViewsEnsured)
        {
            if (columnsRoot.Find("RightSection") != null)
                EnsureRightEnhancementsBodyScrollView(columnsRoot.Find("RightSection"));
            EnsureDetailsPanelDividerLines();
            return;
        }

        if (left != null)
            EnsureSectionBodyScrollView(left, "LeftBodyScroll", "TopRow", insertAfterPinned: true, excludeDividerBeforePinned: false);
        if (middle != null)
            EnsureSectionBodyScrollView(middle, "MiddleBodyScroll", "CostCooldownRow", insertAfterPinned: false, excludeDividerBeforePinned: true);
        if (columnsRoot.Find("RightSection") != null)
            EnsureRightEnhancementsBodyScrollView(columnsRoot.Find("RightSection"));

        _columnScrollViewsEnsured = true;
        EnsureDetailsPanelDividerLines();
    }

    private static void EnsureSectionBodyScrollView(
        Transform section,
        string scrollName,
        string pinnedChildName,
        bool insertAfterPinned,
        bool excludeDividerBeforePinned)
    {
        if (section == null || section.Find(scrollName) != null)
            return;

        Transform pinned = section.Find(pinnedChildName);
        if (pinned == null)
            return;

        Transform excludedDivider = excludeDividerBeforePinned ? FindDividerImmediatelyBefore(pinned) : null;

        var toMove = new List<Transform>();
        for (int i = 0; i < section.childCount; i++)
        {
            Transform child = section.GetChild(i);
            if (child == pinned || child == excludedDivider || child.name == scrollName)
                continue;
            toMove.Add(child);
        }

        if (toMove.Count == 0)
            return;

        int insertIndex = insertAfterPinned ? pinned.GetSiblingIndex() + 1 : pinned.GetSiblingIndex();
        ScrollRect scroll = CreateDetailsColumnScrollRect(section, scrollName, insertIndex);
        RectTransform content = scroll.content;

        for (int i = 0; i < toMove.Count; i++)
            toMove[i].SetParent(content, false);

        LayoutElement scrollLayout = scroll.GetComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = 0f;
        scrollLayout.flexibleWidth = 1f;
        scrollLayout.minWidth = 0f;
    }

    private static Transform FindDividerImmediatelyBefore(Transform node)
    {
        if (node == null || node.parent == null)
            return null;

        int index = node.GetSiblingIndex();
        if (index <= 0)
            return null;

        Transform previous = node.parent.GetChild(index - 1);
        return previous != null && previous.name == "Divider" ? previous : null;
    }

    private static ScrollRect CreateDetailsColumnScrollRect(Transform section, string scrollName, int insertIndex)
    {
        var scrollGo = new GameObject(scrollName, typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(section, false);
        scrollRt.SetSiblingIndex(Mathf.Clamp(insertIndex, 0, section.childCount - 1));
        StretchDetailsScrollRect(scrollRt);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.SetParent(scrollRt, false);
        StretchDetailsScrollRect(viewportRt);
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(viewportRt, false);
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentVlg = contentGo.GetComponent<VerticalLayoutGroup>();
        contentVlg.spacing = 6f;
        contentVlg.childAlignment = TextAnchor.UpperLeft;
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = true;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;
        scroll.verticalScrollbar = null;
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        return scroll;
    }

    private static void EnsureRightEnhancementsBodyScrollView(Transform rightSection)
    {
        if (rightSection == null || rightSection.Find("RightBodyScroll") != null)
            return;

        Transform header = rightSection.Find("EnhancementsHeader");
        Transform actionButton = rightSection.Find("ChangeEnhancementButton");
        if (header == null || actionButton == null)
            return;

        var toMove = new List<Transform>();
        for (int i = 0; i < rightSection.childCount; i++)
        {
            Transform child = rightSection.GetChild(i);
            if (child == header
                || child == actionButton
                || child.name == "EnhancementsLockedOverlay"
                || child.name == "RightBodyScroll")
            {
                continue;
            }

            toMove.Add(child);
        }

        if (toMove.Count == 0)
            return;

        int insertIndex = header.GetSiblingIndex() + 1;
        ScrollRect scroll = CreateDetailsColumnScrollRect(rightSection, "RightBodyScroll", insertIndex);
        RectTransform content = scroll.content;

        for (int i = 0; i < toMove.Count; i++)
            toMove[i].SetParent(content, false);

        LayoutElement scrollLayout = scroll.GetComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = 0f;
        scrollLayout.flexibleWidth = 1f;
        scrollLayout.minWidth = 0f;
    }

    private static void StretchDetailsScrollRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private void ResetColumnBodyScrollPositions()
    {
        if (contentRoot == null)
            return;

        Transform columnsRoot = contentRoot.transform.Find("ColumnsRoot");
        if (columnsRoot == null)
            return;

        ResetScrollToTop(columnsRoot.Find("LeftSection/LeftBodyScroll"));
        ResetScrollToTop(columnsRoot.Find("MiddleSection/MiddleBodyScroll"));
        ResetScrollToTop(columnsRoot.Find("RightSection/RightBodyScroll"));
    }

    private static void ResetScrollToTop(Transform scrollTransform)
    {
        if (scrollTransform == null || !scrollTransform.TryGetComponent(out ScrollRect scroll))
            return;

        scroll.verticalNormalizedPosition = 1f;
        scroll.StopMovement();
    }

    private void ApplyDetailsTypography()
    {
        SetFontSize(nameText, FontNameTitle);
        SetFontSize(levelReqText, FontMeta);
        SetFontSize(typeText, FontMeta);
        SetFontSize(unlockStateText, FontMeta);
        SetFontSize(descriptionText, FontBody);
        SetFontSize(requirementWeaponText, FontBody);
        SetFontSize(typeValueText, FontBody);
        if (IsCapstoneEnhancementNameInScalingSection())
        {
            SetFontSize(scalingText, FontCapstoneEnhancementName);
            if (scalingText != null)
            {
                scalingText.fontStyle = FontStyles.Bold;
                scalingText.color = BodyTextColor;
            }
        }
        else
            SetFontSize(scalingText, FontBody);

        SetFontSize(effectText, FontBody);
        SetFontSize(costText, FontBody);
        SetFontSize(cooldownText, FontBody);
        SetFontSize(enhancementsTitleText, FontEnhancementHeader);
        SetFontSize(enhancementsSubtitleText, FontEnhancementSubtitle);
        if (enhancementDetailText != null)
            ApplyEffectBodyTextStyle(enhancementDetailText);

        if (contentRoot == null)
            return;

        TMP_Text[] allTexts = contentRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text tmp = allTexts[i];
            if (tmp == null)
                continue;

            string n = tmp.gameObject.name;
            if (n == "SectionTitle")
                SetFontSize(tmp, FontSectionHeader);
            else if (n == "ChangeEnhancementButton" || (tmp.transform.parent != null && tmp.transform.parent.name == "ChangeEnhancementButton"))
                SetFontSize(tmp, FontEnhancementButton);
        }

        if (emptyStateRoot != null)
        {
            TMP_Text emptyLabel = emptyStateRoot.GetComponentInChildren<TMP_Text>(true);
            SetFontSize(emptyLabel, FontBody);
        }
    }

    private static void ApplyEnhancementCardLayout(SkillNodeDetailsEnhancementCardUI card)
    {
        if (card == null)
            return;

        LayoutElement layout = card.GetComponent<LayoutElement>();
        if (layout == null)
            layout = card.gameObject.AddComponent<LayoutElement>();

        layout.preferredHeight = EnhancementCardHeight - EnhancementCardOutlineInset;
        layout.minHeight = EnhancementCardHeight - EnhancementCardOutlineInset;
    }

    private static void ApplyEnhancementCardTypography(SkillNodeDetailsEnhancementCardUI card)
    {
        if (card == null)
            return;

        TMP_Text[] texts = card.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text tmp = texts[i];
            if (tmp == null)
                continue;

            if (tmp.gameObject.name.Contains("Level"))
                SetFontSize(tmp, FontEnhancementCardLevel);
            else
                SetFontSize(tmp, FontEnhancementCardName);
        }
    }

    private static void SetFontSize(TMP_Text text, float size)
    {
        if (text == null)
            return;

        text.fontSize = size;
    }

    private static AbilityDefinition ResolveAbility(SkillTimelineNodeBinding binding)
    {
        if (binding == null)
            return null;

        if (binding.Choice?.ability != null)
            return binding.Choice.ability;

        if (binding.Unlock?.ability != null)
            return binding.Unlock.ability;

        return null;
    }

    private void BindAbilityIconDragAssign(AbilityDefinition ability, SkillsManager skillsManager)
    {
        if (skillIconImage == null)
            return;

        _abilityIconDragAssign ??= skillIconImage.GetComponent<AbilityIconDragAssignUI>();
        if (_abilityIconDragAssign == null)
            _abilityIconDragAssign = skillIconImage.gameObject.AddComponent<AbilityIconDragAssignUI>();

        SkillDefinition skill = null;
        if (ability != null)
        {
            SkillDatabase db = SkillDatabase.LoadDefault();
            skill = db != null ? db.Get(ability.sourceSkill) : null;
        }

        bool canAssign = ability != null
            && SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skill, ability, skillsManager);
        _abilityIconDragAssign.Bind(ability, canAssign);
    }

    private void BindRequirementsSection(
        AbilityDefinition ability,
        CharacterStats stats,
        SkillUnlockDefinition unlock = null,
        int displayedCapstoneChoiceIndex = -1)
    {
        string requirements;
        if (ability != null)
        {
            requirements = AbilityTooltipDamagePreview.BuildAbilityRequirementsRichText(ability, stats, accentWhenOk: true);
        }
        else if (!CombatPassiveWeaponRequirementText.TryBuildUnlockRequirementsRichText(
                     _currentBinding?.Skill,
                     unlock,
                     stats,
                     displayedCapstoneChoiceIndex,
                     accentWhenOk: true,
                     out requirements))
        {
            requirements = string.Empty;
        }

        bool hasRequirements = !string.IsNullOrWhiteSpace(requirements);
        if (requirementsSectionRoot != null)
            requirementsSectionRoot.SetActive(hasRequirements);

        if (requirementWeaponText != null)
        {
            requirementWeaponText.gameObject.SetActive(hasRequirements);
            requirementWeaponText.richText = true;
            requirementWeaponText.textWrappingMode = TextWrappingModes.Normal;
            requirementWeaponText.overflowMode = TextOverflowModes.Overflow;
            requirementWeaponText.text = hasRequirements ? requirements : string.Empty;
            if (hasRequirements)
                requirementWeaponText.ForceMeshUpdate();
        }
    }

    private static string FormatSkillCategoryTypeLabel(SkillCategory category) =>
        category switch
        {
            SkillCategory.Gathering => "Gathering Skill",
            SkillCategory.Combat => "Combat Skill",
            SkillCategory.Crafting => "Crafting Skill",
            SkillCategory.Utility => "Utility Skill",
            _ => "Skill"
        };

    private static Sprite ResolveSkillListIcon(SkillDefinition skill, SkillsAbilitySkillsListPanelUI listPanel)
    {
        if (skill == null)
            return null;

        if (listPanel != null)
            return listPanel.TryGetSkillListIcon(skill.skillType);

        SkillsAbilitySkillsListPanelUI resolved = FindSkillsListPanel();
        return resolved != null ? resolved.TryGetSkillListIcon(skill.skillType) : null;
    }

    private static SkillsAbilitySkillsListPanelUI FindSkillsListPanel()
    {
        SkillsAbilityPageNewUI page = FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);
        if (page == null)
            return null;

        return page.GetComponentInChildren<SkillsAbilitySkillsListPanelUI>(true);
    }

    private void BindTypeSection(AbilityDefinition ability, string fallbackTypeLabel)
    {
        string tagLabel = ability != null
            ? AbilityTooltipDamagePreview.ResolveAbilityCategoryTagLabel(ability)
            : null;

        if (string.IsNullOrWhiteSpace(tagLabel))
            tagLabel = fallbackTypeLabel;

        bool hasType = !string.IsNullOrWhiteSpace(tagLabel);
        if (typeSectionRoot != null)
            typeSectionRoot.SetActive(hasType);

        if (typeValueText != null)
            typeValueText.text = hasType ? $"Type: {tagLabel}" : string.Empty;
    }

    private void BindMiddleColumn(
        AbilityDefinition ability,
        SkillsManager skillsManager,
        CharacterStats stats,
        SkillTimelineNodeBinding binding,
        string majorPassiveEffectText = null,
        string majorPassiveScalingText = null)
    {
        if (ability == null)
        {
            bool isCapstone = binding?.Unlock?.unlockType == SkillUnlockType.CapstonePassive;
            if (isCapstone)
            {
                int choiceIndex = ResolveCommittedEnhancementChoiceIndex(binding, skillsManager);
                BindCapstoneEnhancementNameSection(binding.Unlock, choiceIndex);
                SetSectionActive(scalingSectionRoot, false);
            }
            else if (!string.IsNullOrWhiteSpace(majorPassiveScalingText))
            {
                SetScalingSectionHeaderVisible(true);
                if (scalingText != null)
                    SetFontSize(scalingText, FontBody);
                SetRichSection(scalingSectionRoot, scalingText, majorPassiveScalingText, ScalingTextColor);
            }
            else
            {
                SetSectionActive(scalingSectionRoot, false);
            }

            SetRichSection(effectSectionRoot, effectText, majorPassiveEffectText, BodyTextColor);
            SetSectionActive(costSectionRoot, false);
            SetSectionActive(cooldownSectionRoot, false);
            SetCostCooldownRowVisible(false);
            return;
        }

        SetCostCooldownRowVisible(true);
        SetScalingSectionHeaderVisible(true);
        if (scalingText != null)
            SetFontSize(scalingText, FontBody);

        string scaling = AbilityTooltipDamagePreview.BuildAbilityTooltipScalingSection(
            ability, stats, skillsManager, orangeMarkup: false);
        SetRichSection(scalingSectionRoot, scalingText, scaling, ScalingTextColor);

        int committedEnhancementChoice = ResolveCommittedEnhancementChoiceIndex(binding, skillsManager);
        string effects = AbilityTooltipDamagePreview.BuildAbilityTooltipEffectsSection(
            ability, skillsManager, committedEnhancementChoice);
        SetRichSection(effectSectionRoot, effectText, effects, BodyTextColor);

        bool hasResource = AbilityTooltipDamagePreview.TryGetDetailsPanelResourceLines(
            ability, skillsManager, out string costLine, out string cooldownLine);

        SetPlainSection(costSectionRoot, costText, hasResource ? costLine : string.Empty);
        SetPlainSection(cooldownSectionRoot, cooldownText, hasResource ? cooldownLine : string.Empty);
    }

    private void SetCostCooldownRowVisible(bool visible)
    {
        if (contentRoot == null)
            return;

        Transform costRow = contentRoot.transform.Find("ColumnsRoot/MiddleSection/CostCooldownRow");
        if (costRow != null)
            costRow.gameObject.SetActive(visible);
    }

    private static readonly Color BodyTextColor = new(0.93f, 0.9f, 0.84f, 1f);

    private static void SetSectionActive(GameObject sectionRoot, bool active)
    {
        if (sectionRoot != null)
            sectionRoot.SetActive(active);
    }

    private static void SetRichSection(GameObject sectionRoot, TMP_Text label, string richText, Color fallbackColor)
    {
        bool hasContent = !string.IsNullOrWhiteSpace(richText);
        SetSectionActive(sectionRoot, hasContent);
        if (label == null)
            return;

        ApplyEffectBodyTextStyle(label);
        if (hasContent && richText.IndexOf("<color=", StringComparison.OrdinalIgnoreCase) < 0)
            label.color = fallbackColor;
        label.text = hasContent ? richText : string.Empty;
    }

    private static void ApplyEffectBodyTextStyle(TMP_Text label)
    {
        if (label == null)
            return;

        label.richText = true;
        label.color = BodyTextColor;
        label.fontSize = FontBody;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.fontStyle = FontStyles.Normal;
        label.lineSpacing = 0f;
        label.paragraphSpacing = EffectBodyParagraphSpacing;

        if (label.GetComponent<ContentSizeFitter>() == null)
        {
            ContentSizeFitter fitter = label.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        LayoutElement layout = label.GetComponent<LayoutElement>();
        if (layout == null)
            layout = label.gameObject.AddComponent<LayoutElement>();
        layout.flexibleHeight = 0f;
    }

    private static void SetPlainSection(GameObject sectionRoot, TMP_Text label, string text)
    {
        bool hasContent = !string.IsNullOrWhiteSpace(text);
        SetSectionActive(sectionRoot, hasContent);
        if (label == null)
            return;

        label.richText = false;
        label.text = hasContent ? text : string.Empty;
        label.color = BodyTextColor;
    }

    private string ResolveEnhancementDetailEffectText(int choiceIndex, string fallbackDescription)
    {
        SkillTimelineNodeBinding binding = _currentBinding;
        SkillUnlockDefinition unlock = binding?.Unlock;
        if (binding?.Skill != null && unlock != null)
        {
            string spineId = binding.ResolveSpineNodeId();
            if (string.Equals(spineId, AbilityCombatPower.SnipeEnhancementParentSpineNodeId, StringComparison.Ordinal))
            {
                if (choiceIndex == AbilityCombatPower.SnipeFasterChargeChoiceIndex)
                    return "Reduce charge time by 0.5 seconds.";
                if (choiceIndex == AbilityCombatPower.SnipeGuaranteedBleedChoiceIndex)
                    return "Gains 100% chance to bleed";
            }

            if (unlock.unlockType == SkillUnlockType.CapstonePassive
                && MeleeMajorPassiveTooltipText.TryBuildCapstoneChoiceBody(choiceIndex, out string capstoneBody)
                && !string.IsNullOrWhiteSpace(capstoneBody))
                return capstoneBody;

            if (!string.IsNullOrWhiteSpace(spineId))
            {
                SkillType skillType = binding.Skill.skillType;
                if (skillType == SkillType.Ranged
                    && RangedMajorPassiveTooltipText.TryBuildChoiceTooltipBody(spineId, choiceIndex, out string rangedBody)
                    && !string.IsNullOrWhiteSpace(rangedBody))
                    return rangedBody;

                if (skillType == SkillType.Endurance
                    && EnduranceMajorPassiveTooltipText.TryBuildChoiceTooltipBody(spineId, choiceIndex, out string enduranceBody)
                    && !string.IsNullOrWhiteSpace(enduranceBody))
                    return enduranceBody;

                if (skillType == SkillType.Melee
                    && MeleeMajorPassiveTooltipText.TryBuildChoiceTooltipBody(spineId, choiceIndex, out string meleeBody)
                    && !string.IsNullOrWhiteSpace(meleeBody))
                    return meleeBody;
            }

            if (string.Equals(spineId, AbilityCombatPower.SoulforgedWeaponEnhancementParentSpineNodeId, StringComparison.Ordinal))
            {
                if (choiceIndex == AbilityCombatPower.SoulforgedWeaponSwarmChoiceIndex)
                {
                    return
                        "Summons 3 soulforged weapons.\n" +
                        "Each weapon 15% less damage.\n" +
                        "Recast to collapse all weapons onto your current target, or find new targets if you don't have one.";
                }

                if (choiceIndex == AbilityCombatPower.SoulforgedWeaponExtendedDurationChoiceIndex)
                {
                    return
                        $"Soulforged Weapon now lasts {AbilityCombatPower.SoulforgedWeaponExtendedDurationSeconds:0.#}s.";
                }
            }

            if (string.Equals(spineId, AbilityCombatPower.SoulforgedWarriorEnhancementParentSpineNodeId, StringComparison.Ordinal))
            {
                if (choiceIndex == AbilityCombatPower.SoulforgedWarriorTauntingShoutChoiceIndex)
                {
                    return
                        $"Warcry also taunts enemies within {AbilityCombatPower.SoulforgedWarriorTauntRange:0.#} range, forcing them to attack the warrior. Taunted enemies deal {AbilityCombatPower.SoulforgedWarriorTauntingShoutOutgoingDamageReduction * 100f:0.#}% reduced damage for {AbilityCombatPower.SoulforgedWarriorTauntingShoutDebuffDurationSeconds:0.#}s.";
                }

                if (choiceIndex == AbilityCombatPower.SoulforgedWarriorFuriousSlamChoiceIndex)
                {
                    return
                        $"Slams the ground in front ({AbilityCombatPower.SoulforgedWarriorFuriousSlamRange:0.#} range), hitting all enemies for {AbilityCombatPower.SoulforgedWarriorFuriousSlamDamageMultiplier * 100f:0.#}% of the warrior's strike damage.";
                }
            }
        }

        return fallbackDescription ?? string.Empty;
    }

    private bool IsCapstoneEnhancementNameInScalingSection() =>
        ResolveAbility(_currentBinding) == null
        && _currentBinding?.Unlock?.unlockType == SkillUnlockType.CapstonePassive
        && scalingSectionRoot != null
        && scalingSectionRoot.activeInHierarchy;

    private static int ResolveCommittedEnhancementChoiceIndex(
        SkillTimelineNodeBinding binding,
        SkillsManager skillsManager)
    {
        if (binding?.Unlock?.choices == null || binding.Skill == null || skillsManager == null)
            return -1;

        string spineId = binding.ResolveSpineNodeId();
        if (string.IsNullOrEmpty(spineId))
            return -1;

        return skillsManager.GetSkillChoiceSelection(binding.Skill.skillType, spineId, -1);
    }

    private int ResolveDisplayedCapstoneChoiceIndex(SkillTimelineNodeBinding binding, SkillsManager skillsManager)
    {
        if (_previewEnhancementIndex >= 0)
            return _previewEnhancementIndex;

        return ResolveCommittedEnhancementChoiceIndex(binding, skillsManager);
    }

    private void RefreshCapstoneRequirementsDisplay()
    {
        if (_currentBinding?.Unlock == null)
            return;

        SkillUnlockType unlockType = _currentBinding.Unlock.unlockType;
        if (unlockType == SkillUnlockType.CapstonePassive)
        {
            CharacterStats stats = AbilityTooltipDamagePreview.FindLocalPlayerStats();
            BindRequirementsSection(null, stats, _currentBinding.Unlock, _previewEnhancementIndex);
            return;
        }

        if (unlockType == SkillUnlockType.MajorPassive
            && _currentBinding.Skill?.skillType == SkillType.Melee)
        {
            CharacterStats stats = AbilityTooltipDamagePreview.FindLocalPlayerStats();
            BindRequirementsSection(null, stats, _currentBinding.Unlock);
        }
    }

    private void BindCapstoneEnhancementNameSection(SkillUnlockDefinition unlock, int choiceIndex)
    {
        string title = ResolveEnhancementChoiceTitle(unlock, choiceIndex);
        bool hasTitle = !string.IsNullOrWhiteSpace(title);

        SetScalingSectionHeaderVisible(false);
        SetSectionActive(scalingSectionRoot, hasTitle);

        if (scalingText == null)
            return;

        scalingText.richText = false;
        scalingText.color = BodyTextColor;
        scalingText.fontStyle = FontStyles.Bold;
        scalingText.textWrappingMode = TextWrappingModes.Normal;
        scalingText.text = hasTitle ? title : string.Empty;
        SetFontSize(scalingText, FontCapstoneEnhancementName);
    }

    private void SetScalingSectionHeaderVisible(bool visible)
    {
        if (scalingSectionRoot == null)
            return;

        Transform header = scalingSectionRoot.transform.Find("SectionTitle");
        if (header != null)
            header.gameObject.SetActive(visible);
    }

    private void PopulateEnhancementCards(SkillTimelineNodeBinding binding, SkillsManager skillsManager)
    {
        ClearEnhancementButtons();
        _previewEnhancementIndex = -1;
        SetEnhancementDetailVisible(false);

        SkillUnlockDefinition unlock = binding?.Unlock;
        if (unlock?.choices == null || unlock.choices.Count == 0
            || enhancementButtonsContainer == null || enhancementButtonTemplate == null)
        {
            if (enhancementsSubtitleText != null)
            {
                enhancementsSubtitleText.text = string.Empty;
                enhancementsSubtitleText.gameObject.SetActive(false);
            }

            SetEnhancementsLockedOverlay(false);
            RefreshEnhancementActionButton(locked: true, committedIndex: -1);
            RefreshEnhancementSubtitle(locked: true, committedIndex: -1, unlock);
            return;
        }

        bool enhancementsLocked = AreEnhancementsLocked(binding, skillsManager, out _);
        SetEnhancementsLockedOverlay(enhancementsLocked);

        string spineId = binding.ResolveSpineNodeId();
        int savedIndex = skillsManager != null && binding.Skill != null && !string.IsNullOrEmpty(spineId)
            ? skillsManager.GetSkillChoiceSelection(binding.Skill.skillType, spineId, -1)
            : -1;

        _committedEnhancementIndex = savedIndex;
        _previewEnhancementIndex = savedIndex >= 0 ? savedIndex : -1;

        RefreshEnhancementSubtitle(enhancementsLocked, savedIndex, unlock);
        RefreshEnhancementActionButton(enhancementsLocked, savedIndex);

        int visibleCount = 0;
        for (int i = 0; i < unlock.choices.Count; i++)
        {
            SkillChoiceDefinition choice = unlock.choices[i];
            if (choice == null)
                continue;

            int assetIndex = i;
            int level = ResolveEnhancementChoiceDisplayLevel(unlock, choice, binding);
            string levelLabel = level > 0 ? $"Lv {level}" : string.Empty;

            SkillNodeDetailsEnhancementCardUI buttonUi =
                Instantiate(enhancementButtonTemplate, enhancementButtonsContainer);
            buttonUi.gameObject.SetActive(true);
            bool committed = assetIndex == savedIndex;
            bool preview = assetIndex == _previewEnhancementIndex;
            buttonUi.Configure(
                assetIndex,
                SkillsAbilityPresentationResolver.ResolveChoiceIcon(choice),
                SkillsAbilityPresentationResolver.ResolveChoiceTitle(choice),
                SkillsAbilityPresentationResolver.ResolveChoiceDescription(choice),
                levelLabel,
                preview,
                committed);
            buttonUi.Clicked += HandleEnhancementButtonClicked;
            ApplyEnhancementCardTypography(buttonUi);
            ApplyEnhancementCardLayout(buttonUi);
            _spawnedEnhancementButtons.Add(buttonUi);
            visibleCount++;
        }

        ApplyEnhancementButtonsContainerLayout(visibleCount);
        _visibleEnhancementChoiceCount = visibleCount;

        if (_previewEnhancementIndex >= 0)
            SelectEnhancementButton(_previewEnhancementIndex, showDetail: true, scrollToDetail: false);
    }

    private void ApplyEnhancementButtonsContainerLayout(int visibleCount)
    {
        if (enhancementButtonsContainer == null)
            return;

        int columns = EnhancementButtonsGridColumnCount;
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(visibleCount / (float)columns));
        GridLayoutGroup gridLayout = EnsureEnhancementButtonsGridLayout();

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columns;
        gridLayout.spacing = new Vector2(EnhancementButtonsGridSpacing, EnhancementButtonsGridSpacing);
        gridLayout.padding = new RectOffset(
            EnhancementButtonsGridPadding,
            EnhancementButtonsGridPadding,
            EnhancementButtonsGridPadding,
            EnhancementButtonsGridPadding);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperCenter;

        float columnWidth = ComputeColumnWidth(RightColumnWidthRatio);

        float cellWidth = (columnWidth
            - EnhancementButtonsGridSpacing * (columns - 1)
            - EnhancementButtonsGridPadding * 2) / columns;
        float cellHeight = EnhancementCardHeight;
        gridLayout.cellSize = new Vector2(
            Mathf.Max(84f, cellWidth - EnhancementCardOutlineInset),
            cellHeight - EnhancementCardOutlineInset);

        LayoutElement rowLayout = enhancementButtonsContainer.GetComponent<LayoutElement>();
        if (rowLayout == null)
            rowLayout = enhancementButtonsContainer.gameObject.AddComponent<LayoutElement>();

        float cardRowHeight = EnhancementButtonsRowHeight - EnhancementCardOutlineInset;
        float preferredHeight = cardRowHeight * rowCount
            + EnhancementButtonsGridSpacing * Mathf.Max(0, rowCount - 1)
            + EnhancementButtonsGridPadding * 2;
        rowLayout.preferredHeight = preferredHeight;
        rowLayout.minHeight = preferredHeight;
        rowLayout.flexibleHeight = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(enhancementButtonsContainer);
    }

    private GridLayoutGroup EnsureEnhancementButtonsGridLayout()
    {
        if (enhancementButtonsContainer.TryGetComponent(out HorizontalLayoutGroup horizontalLayout))
            DestroyImmediate(horizontalLayout);

        GridLayoutGroup gridLayout = enhancementButtonsContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
            gridLayout = enhancementButtonsContainer.gameObject.AddComponent<GridLayoutGroup>();

        return gridLayout;
    }

    private static int ResolveEnhancementChoiceDisplayLevel(
        SkillUnlockDefinition unlock,
        SkillChoiceDefinition choice,
        SkillTimelineNodeBinding binding)
    {
        if (unlock?.unlockType == SkillUnlockType.CapstonePassive)
            return ResolveCapstoneEnhancementUnlockLevel(unlock, binding);

        if (choice != null && choice.requiredLevel > 0)
            return choice.requiredLevel;

        if (binding != null && binding.Level > 0)
            return binding.Level;

        return unlock != null ? Mathf.Max(1, unlock.requiredLevel) : 1;
    }

    private static int ResolveCapstoneEnhancementUnlockLevel(
        SkillUnlockDefinition unlock,
        SkillTimelineNodeBinding binding)
    {
        int capstoneLevel = unlock != null ? Mathf.Max(1, unlock.requiredLevel) : 50;
        if (binding != null && binding.Level > 0)
            capstoneLevel = Mathf.Max(capstoneLevel, binding.Level);

        return capstoneLevel;
    }

    private static bool IsSameEnhancementBinding(SkillTimelineNodeBinding a, SkillTimelineNodeBinding b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null)
            return false;

        return a.Skill == b.Skill
            && ReferenceEquals(a.Unlock, b.Unlock)
            && a.Level == b.Level
            && a.SlotAtLevel == b.SlotAtLevel
            && a.ChoiceAssetIndex == b.ChoiceAssetIndex;
    }

    private void RefreshEnhancementSubtitle(bool locked, int committedIndex, SkillUnlockDefinition unlock)
    {
        if (enhancementsSubtitleText == null)
            return;

        if (locked)
        {
            enhancementsSubtitleText.text = "Unlocked at higher levels";
            enhancementsSubtitleText.gameObject.SetActive(true);
            return;
        }

        string title = ResolveEnhancementChoiceTitle(unlock, committedIndex);
        enhancementsSubtitleText.richText = true;
        if (string.IsNullOrWhiteSpace(title))
        {
            enhancementsSubtitleText.text = "<color=#FF4D4D>Enhancement Selected: None</color>";
        }
        else
        {
            enhancementsSubtitleText.text =
                $"<color=#33CC66>Enhancement Selected: {EscapeEnhancementSubtitleText(title)}</color>";
        }

        enhancementsSubtitleText.gameObject.SetActive(true);
    }

    private static string EscapeEnhancementSubtitleText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("<", string.Empty).Replace(">", string.Empty);
    }

    private static string ResolveEnhancementChoiceTitle(SkillUnlockDefinition unlock, int choiceIndex)
    {
        if (unlock?.choices == null || choiceIndex < 0 || choiceIndex >= unlock.choices.Count)
            return string.Empty;

        SkillChoiceDefinition choice = unlock.choices[choiceIndex];
        return choice != null ? SkillsAbilityPresentationResolver.ResolveChoiceTitle(choice) : string.Empty;
    }

    private void RefreshEnhancementActionButton(bool locked, int committedIndex)
    {
        if (changeEnhancementButton == null)
            return;

        changeEnhancementButton.interactable = !locked;

        TMP_Text label = changeEnhancementButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = committedIndex >= 0 ? ChangeEnhancementButtonLabel : SelectEnhancementButtonLabel;
    }

    private void WireEnhancementActionButton()
    {
        if (changeEnhancementButton == null)
            return;

        if (_enhancementActionHandler != null)
            changeEnhancementButton.onClick.RemoveListener(_enhancementActionHandler);

        _enhancementActionHandler = HandleEnhancementActionButtonClicked;
        changeEnhancementButton.onClick.AddListener(_enhancementActionHandler);
    }

    /// <summary>Inspector assignment from <see cref="SkillsAbilityPageNewUI"/> (preferred over auto-find).</summary>
    public void AssignCollapseDetailsButton(Button button)
    {
        if (collapseDetailsButton != null
            && collapseDetailsButton != button
            && _collapseDetailsHandler != null)
        {
            collapseDetailsButton.onClick.RemoveListener(_collapseDetailsHandler);
        }

        collapseDetailsButton = button;
        WireCollapseDetailsButton();
    }

    private void WireCollapseDetailsButton()
    {
        if (collapseDetailsButton == null)
            collapseDetailsButton = ResolveCollapseDetailsButton();

        if (collapseDetailsButton == null)
            return;

        if (_collapseDetailsHandler != null)
            collapseDetailsButton.onClick.RemoveListener(_collapseDetailsHandler);

        _collapseDetailsHandler = HandleCollapseDetailsClicked;
        collapseDetailsButton.onClick.AddListener(_collapseDetailsHandler);
        collapseDetailsButton.interactable = true;
        RefreshCollapseButtonVisible();
    }

    private Button ResolveCollapseDetailsButton()
    {
        if (collapseDetailsButton != null)
            return collapseDetailsButton;

        Transform bar = FindViewDetailsBarTransform();
        if (bar == null)
            return null;

        string[] collapseNames =
        {
            "CollapseDetailsButton",
            "CollapseDetails",
            "CollapseButton"
        };

        for (int i = 0; i < collapseNames.Length; i++)
        {
            Button collapse = bar.Find(collapseNames[i])?.GetComponent<Button>();
            if (collapse != null)
                return collapse;
        }

        return null;
    }

    private Transform FindViewDetailsBarTransform()
    {
        Transform walk = transform;
        for (int depth = 0; depth < 12 && walk != null; depth++)
        {
            Transform direct = walk.Find("ViewDetailsBar");
            if (direct != null)
                return direct;

            walk = walk.parent;
        }

        SkillsAbilityPageNewUI page = GetComponentInParent<SkillsAbilityPageNewUI>(true);
        if (page == null)
            return null;

        return page.transform.Find("BottomPanelBar/DetailsPanel/ViewDetailsBar");
    }

    private void HandleCollapseDetailsClicked()
    {
        if (!HasActiveDetails)
            return;

        ShowEmpty();
    }

    private void RefreshCollapseButtonVisible()
    {
        if (collapseDetailsButton != null)
            collapseDetailsButton.gameObject.SetActive(HasActiveDetails);
    }

    private void HandleEnhancementActionButtonClicked()
    {
        if (_currentBinding == null || _currentBinding.Unlock == null)
            return;

        if (AreEnhancementsLocked(_currentBinding, SkillsManager.Instance, out _))
            return;

        if (_previewEnhancementIndex < 0)
            return;

        SkillsManager skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);

        if (skillsManager == null || _currentBinding.Skill == null)
            return;

        string spineId = _currentBinding.ResolveSpineNodeId();
        if (string.IsNullOrEmpty(spineId))
            return;

        skillsManager.SetSkillChoiceSelection(
            _currentBinding.Skill.skillType,
            spineId,
            _previewEnhancementIndex);

        PopulateEnhancementCards(_currentBinding, skillsManager);
        Show(_currentBinding);
    }

    private void HandleEnhancementButtonClicked(SkillNodeDetailsEnhancementCardUI button)
    {
        if (button == null)
            return;

        SelectEnhancementButton(button.ChoiceIndex, showDetail: true, scrollToDetail: true);
    }

    private void SelectEnhancementButton(int choiceIndex, bool showDetail, bool scrollToDetail = false)
    {
        _previewEnhancementIndex = choiceIndex;
        RefreshCapstoneRequirementsDisplay();

        for (int i = 0; i < _spawnedEnhancementButtons.Count; i++)
        {
            SkillNodeDetailsEnhancementCardUI btn = _spawnedEnhancementButtons[i];
            if (btn == null)
                continue;

            bool committed = btn.ChoiceIndex == _committedEnhancementIndex;
            bool preview = btn.ChoiceIndex == choiceIndex;
            btn.SetHighlight(preview, committed);
        }

        if (!showDetail)
        {
            SetEnhancementDetailVisible(false);
            if (scrollToDetail)
                ScheduleScrollEnhancementDetailIntoView();
            return;
        }

        for (int i = 0; i < _spawnedEnhancementButtons.Count; i++)
        {
            SkillNodeDetailsEnhancementCardUI btn = _spawnedEnhancementButtons[i];
            if (btn == null || btn.ChoiceIndex != choiceIndex)
                continue;

            string detail = ResolveEnhancementDetailEffectText(choiceIndex, btn.Description);
            if (enhancementDetailText != null)
            {
                ApplyEffectBodyTextStyle(enhancementDetailText);
                enhancementDetailText.text = detail ?? string.Empty;
            }

            SetEnhancementDetailVisible(!string.IsNullOrWhiteSpace(detail));
            if (scrollToDetail)
                ScheduleScrollEnhancementDetailIntoView();
            return;
        }

        SetEnhancementDetailVisible(false);
        if (scrollToDetail)
            ScheduleScrollEnhancementDetailIntoView();
    }

    private void ScheduleScrollEnhancementDetailIntoView()
    {
        if (_visibleEnhancementChoiceCount <= 2)
            return;

        if (_enhancementScrollRoutine != null)
            StopCoroutine(_enhancementScrollRoutine);

        _enhancementScrollRoutine = StartCoroutine(CoScrollEnhancementDetailIntoView());
    }

    private IEnumerator CoScrollEnhancementDetailIntoView()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (contentRoot == null || enhancementDetailText == null)
        {
            _enhancementScrollRoutine = null;
            yield break;
        }

        Transform scrollTransform = contentRoot.transform.Find("ColumnsRoot/RightSection/RightBodyScroll");
        if (scrollTransform == null || !scrollTransform.TryGetComponent(out ScrollRect scroll))
        {
            _enhancementScrollRoutine = null;
            yield break;
        }

        RectTransform content = scroll.content;
        RectTransform viewport = scroll.viewport;
        RectTransform detailRt = enhancementDetailText.rectTransform;
        if (content == null || viewport == null || detailRt == null)
        {
            _enhancementScrollRoutine = null;
            yield break;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        if (contentHeight <= viewportHeight + 0.5f)
        {
            _enhancementScrollRoutine = null;
            yield break;
        }

        Bounds detailBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, detailRt);
        float scrollOffset = Mathf.Max(0f, -detailBounds.max.y - 4f);
        float scrollableRange = Mathf.Max(1f, contentHeight - viewportHeight);
        scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(scrollOffset / scrollableRange);
        scroll.StopMovement();
        _enhancementScrollRoutine = null;
    }

    private void SetEnhancementDetailVisible(bool visible)
    {
        if (enhancementDetailText != null && !visible)
            enhancementDetailText.text = string.Empty;
    }

    private void EnsureLevelReqTextReference()
    {
        if (levelReqText != null)
            return;

        if (contentRoot != null)
        {
            Transform nameBlock = contentRoot.transform.Find("ColumnsRoot/LeftSection/TopRow/NameBlock");
            if (nameBlock != null)
                levelReqText = nameBlock.Find("LevelReqText")?.GetComponent<TMP_Text>();
        }

        if (levelReqText == null)
            levelReqText = transform.Find("LevelReqText")?.GetComponent<TMP_Text>();
    }

    /// <summary>Top-packs header + buttons; only the detail area grows (fixes stretched column on older prefabs).</summary>
    private void ConfigureEnhancementColumnLayout()
    {
        if (enhancementDetailRoot == null)
            return;

        enhancementDetailRoot.SetActive(true);

        Transform columnRoot = enhancementDetailRoot.transform.parent;
        VerticalLayoutGroup column = columnRoot != null
            ? columnRoot.GetComponent<VerticalLayoutGroup>()
            : null;
        if (column != null)
            column.childForceExpandHeight = false;

        LayoutElement detailLayout = enhancementDetailRoot.GetComponent<LayoutElement>();
        if (detailLayout == null)
            detailLayout = enhancementDetailRoot.AddComponent<LayoutElement>();
        detailLayout.flexibleHeight = 1f;
        detailLayout.flexibleWidth = 1f;
        detailLayout.minHeight = 32f;

        if (enhancementDetailText != null)
        {
            LayoutElement textLayout = enhancementDetailText.GetComponent<LayoutElement>();
            if (textLayout == null)
                textLayout = enhancementDetailText.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleHeight = 1f;
            textLayout.flexibleWidth = 1f;
        }
    }

    private static bool AreEnhancementsLocked(
        SkillTimelineNodeBinding binding,
        SkillsManager skillsManager,
        out int requiredLevel)
    {
        requiredLevel = 0;
        SkillUnlockDefinition unlock = binding?.Unlock;
        if (unlock?.choices == null || unlock.choices.Count == 0 || binding.Skill == null)
            return false;

        if (unlock.unlockType == SkillUnlockType.CapstonePassive)
        {
            requiredLevel = ResolveCapstoneEnhancementUnlockLevel(unlock, binding);
            int playerLevel = skillsManager != null
                ? Mathf.Max(1, skillsManager.GetLevel(binding.Skill.skillType))
                : 1;
            return playerLevel < requiredLevel;
        }

        int minRequired = int.MaxValue;
        for (int i = 0; i < unlock.choices.Count; i++)
        {
            SkillChoiceDefinition choice = unlock.choices[i];
            if (choice == null)
                continue;

            int level = ResolveEnhancementChoiceDisplayLevel(unlock, choice, binding);
            if (level > 0)
                minRequired = Mathf.Min(minRequired, level);
        }

        if (minRequired == int.MaxValue)
            minRequired = Mathf.Max(1, binding.Level);

        requiredLevel = minRequired;

        int skillLevel = skillsManager != null
            ? Mathf.Max(1, skillsManager.GetLevel(binding.Skill.skillType))
            : 1;

        return skillLevel < requiredLevel;
    }

    private void EnsureEnhancementsLockedOverlay()
    {
        if (enhancementsLockedOverlay != null)
            return;

        RectTransform host = enhancementsSectionRoot;
        if (host == null && enhancementButtonsContainer != null)
            host = enhancementButtonsContainer.parent as RectTransform;

        if (host == null)
            return;

        var overlayGo = new GameObject("EnhancementsLockedOverlay", typeof(RectTransform), typeof(Image));
        var overlayRt = overlayGo.GetComponent<RectTransform>();
        overlayRt.SetParent(host, false);
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        overlayRt.SetAsLastSibling();

        LayoutElement ignoreLayout = overlayGo.GetComponent<LayoutElement>();
        if (ignoreLayout == null)
            ignoreLayout = overlayGo.AddComponent<LayoutElement>();
        ignoreLayout.ignoreLayout = true;

        enhancementsLockedOverlay = overlayGo.GetComponent<Image>();
        enhancementsLockedOverlay.color = EnhancementsLockedOverlayColor;
        enhancementsLockedOverlay.raycastTarget = false;
        overlayGo.SetActive(false);
    }

    private void SetEnhancementsLockedOverlay(bool visible)
    {
        EnsureEnhancementsLockedOverlay();
        if (enhancementsLockedOverlay == null)
            return;

        enhancementsLockedOverlay.gameObject.SetActive(visible);
        if (visible)
            enhancementsLockedOverlay.transform.SetAsLastSibling();
    }

    private void ClearEnhancementButtons()
    {
        for (int i = 0; i < _spawnedEnhancementButtons.Count; i++)
        {
            if (_spawnedEnhancementButtons[i] == null)
                continue;

            _spawnedEnhancementButtons[i].Clicked -= HandleEnhancementButtonClicked;
            Destroy(_spawnedEnhancementButtons[i].gameObject);
        }

        _spawnedEnhancementButtons.Clear();
        _visibleEnhancementChoiceCount = 0;
    }
}
