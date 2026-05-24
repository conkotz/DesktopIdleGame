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
    public const float FontNameTitle = 19f;
    public const float FontMeta = 14f;
    public const float FontEnhancementHeader = 14f;
    public const float FontEnhancementSubtitle = 13f;
    public const float FontEnhancementDetail = 13f;
    public const float FontEnhancementCardLevel = 11f;
    public const float FontEnhancementCardName = 12f;
    public const float FontEnhancementButton = 13f;
    public const float EnhancementCardHeight = 102f;
    public const float EnhancementButtonsRowHeight = 104f;

    private const float ContentHorizontalPadding = 16f;
    private const float ColumnSpacing = 0f;
    private const float ColumnDividerWidth = 1f;
    private const float RowDividerHeight = 1f;
    private const string DefaultEmptyMessage = "Click a node to view details";
    private static readonly Color ScalingTextColor = new(0.69f, 0.79f, 0.87f, 1f);

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
    private int _previewEnhancementIndex = -1;
    private int _committedEnhancementIndex = -1;
    private bool _detailsInteriorExpanded;
    private Coroutine _deferredColumnsLayoutCo;
    private UnityEngine.Events.UnityAction _enhancementActionHandler;
    private UnityEngine.Events.UnityAction _collapseDetailsHandler;

    /// <summary>Fired when the panel clears (no node selected).</summary>
    public event System.Action DetailsDismissed;

    public SkillTimelineNodeBinding CurrentBinding => _currentBinding;

    public bool HasActiveDetails => _currentBinding != null;

    private void Awake()
    {
        ApplyPanelSize();
        ApplyFixedThirdColumnLayout();
        ApplySectionDividerLayout();
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

    public void Show(SkillTimelineNodeBinding binding)
    {
        if (binding == null)
        {
            ShowEmpty();
            return;
        }

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
        BindRequirementsSection(ability, stats);
        BindTypeSection(ability, details.TypeLabel);
        BindMiddleColumn(ability, skillsManager, stats);
        PopulateEnhancementCards(binding, skillsManager);
        ApplyDetailsTypography();
        ApplySectionDividerLayout();
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

        HorizontalLayoutGroup topRowLayout = topRow.GetComponent<HorizontalLayoutGroup>();
        if (topRowLayout != null)
        {
            topRowLayout.childControlWidth = true;
            topRowLayout.childForceExpandWidth = false;
            topRowLayout.childForceExpandHeight = true;
        }

        Transform nameBlock = topRow.Find("NameBlock");
        if (nameBlock is RectTransform nameRt)
        {
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = Vector2.zero;
            nameRt.sizeDelta = Vector2.zero;

            LayoutElement nameLayout = nameBlock.GetComponent<LayoutElement>();
            if (nameLayout != null)
            {
                nameLayout.flexibleWidth = 1f;
                nameLayout.minWidth = 0f;
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

        for (int i = 0; i < section.childCount; i++)
        {
            if (section.GetChild(i).name != "Divider")
                continue;
            if (section.GetChild(i) is RectTransform divider)
                PrepareVerticalLayoutDivider(divider);
        }
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
        SetFontSize(scalingText, FontBody);
        SetFontSize(effectText, FontBody);
        SetFontSize(costText, FontBody);
        SetFontSize(cooldownText, FontBody);
        SetFontSize(enhancementsTitleText, FontEnhancementHeader);
        SetFontSize(enhancementsSubtitleText, FontEnhancementSubtitle);
        SetFontSize(enhancementDetailText, FontEnhancementDetail);

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

        layout.preferredHeight = EnhancementCardHeight;
        layout.minHeight = EnhancementCardHeight;
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

    private void BindRequirementsSection(AbilityDefinition ability, CharacterStats stats)
    {
        string weaponLine = ability != null
            ? AbilityTooltipDamagePreview.BuildWeaponRequirementRichLine(ability, stats, accentWhenOk: true)
            : string.Empty;

        bool hasWeaponReq = !string.IsNullOrWhiteSpace(weaponLine);
        if (requirementsSectionRoot != null)
            requirementsSectionRoot.SetActive(hasWeaponReq);

        if (requirementWeaponText != null)
        {
            requirementWeaponText.gameObject.SetActive(hasWeaponReq);
            requirementWeaponText.richText = true;
            requirementWeaponText.text = hasWeaponReq ? weaponLine : string.Empty;
        }
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

    private void BindMiddleColumn(AbilityDefinition ability, SkillsManager skillsManager, CharacterStats stats)
    {
        if (ability == null)
        {
            SetSectionActive(scalingSectionRoot, false);
            SetSectionActive(effectSectionRoot, false);
            SetSectionActive(costSectionRoot, false);
            SetSectionActive(cooldownSectionRoot, false);
            return;
        }

        string scaling = AbilityTooltipDamagePreview.BuildAbilityTooltipScalingSection(
            ability, stats, skillsManager, orangeMarkup: false);
        SetRichSection(scalingSectionRoot, scalingText, scaling, ScalingTextColor);

        string effects = AbilityTooltipDamagePreview.BuildAbilityTooltipEffectsSection(ability, skillsManager);
        SetRichSection(effectSectionRoot, effectText, effects, BodyTextColor);

        bool hasResource = AbilityTooltipDamagePreview.TryGetDetailsPanelResourceLines(
            ability, skillsManager, out string costLine, out string cooldownLine);

        SetPlainSection(costSectionRoot, costText, hasResource ? costLine : string.Empty);
        SetPlainSection(cooldownSectionRoot, cooldownText, hasResource ? cooldownLine : string.Empty);
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

        label.richText = true;
        label.text = hasContent ? richText : string.Empty;
        if (!hasContent)
            label.color = fallbackColor;
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
            int level = choice.requiredLevel > 0 ? choice.requiredLevel : binding.Level;
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

        if (enhancementButtonsContainer != null)
        {
            LayoutElement rowLayout = enhancementButtonsContainer.GetComponent<LayoutElement>();
            if (rowLayout == null)
                rowLayout = enhancementButtonsContainer.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = EnhancementButtonsRowHeight;
            rowLayout.minHeight = EnhancementButtonsRowHeight;
        }

        if (_previewEnhancementIndex >= 0)
            SelectEnhancementButton(_previewEnhancementIndex, showDetail: true);
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
        enhancementsSubtitleText.text = string.IsNullOrWhiteSpace(title)
            ? "Enhancement Selected: None"
            : $"Enhancement Selected: {title}";
        enhancementsSubtitleText.gameObject.SetActive(true);
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

        SelectEnhancementButton(button.ChoiceIndex, showDetail: true);
    }

    private void SelectEnhancementButton(int choiceIndex, bool showDetail)
    {
        _previewEnhancementIndex = choiceIndex;

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
            return;
        }

        for (int i = 0; i < _spawnedEnhancementButtons.Count; i++)
        {
            SkillNodeDetailsEnhancementCardUI btn = _spawnedEnhancementButtons[i];
            if (btn == null || btn.ChoiceIndex != choiceIndex)
                continue;

            if (enhancementDetailText != null)
                enhancementDetailText.text = btn.Description;
            SetEnhancementDetailVisible(!string.IsNullOrWhiteSpace(btn.Description));
            return;
        }

        SetEnhancementDetailVisible(false);
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

        int minRequired = int.MaxValue;
        for (int i = 0; i < unlock.choices.Count; i++)
        {
            SkillChoiceDefinition choice = unlock.choices[i];
            if (choice == null)
                continue;

            int level = choice.requiredLevel > 0 ? choice.requiredLevel : binding.Level;
            if (level > 0)
                minRequired = Mathf.Min(minRequired, level);
        }

        if (minRequired == int.MaxValue)
            minRequired = Mathf.Max(1, binding.Level);

        requiredLevel = minRequired;

        int playerLevel = skillsManager != null
            ? Mathf.Max(1, skillsManager.GetLevel(binding.Skill.skillType))
            : 1;

        return playerLevel < requiredLevel;
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
    }
}
