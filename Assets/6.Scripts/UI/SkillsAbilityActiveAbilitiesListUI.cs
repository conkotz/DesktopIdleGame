using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates the NEW skills page Active Abilities list (BottomPanelBar / AbilityList scroll content).
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillsAbilityActiveAbilitiesListUI : MonoBehaviour
{
    private const string RowsContainerName = "ActiveAbilitiesRows";

    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private AbilityEntryUI entryPrefab;
    [SerializeField] private float rowSpacing = 6f;

    private readonly List<AbilityEntryUI> _rows = new();
    private readonly List<GameObject> _placeholderRoots = new();
    private GameObject _autoAssignBarRoot;
    private GameObject _minorPassiveContentRoot;
    private GameObject _autoAssignSectionRoot;
    private Transform _rowsContainer;
    private Canvas _rootCanvas;
    private HorizontalSkillTreeScaffoldUI _horizontalTimeline;
    private SkillsAbilityBottomPanelLayoutUI _bottomPanelLayout;

    public AbilityEntryUI EntryPrefab => entryPrefab;

    public void SetEntryPrefab(AbilityEntryUI prefab)
    {
        if (prefab != null)
            entryPrefab = prefab;
    }

    public void ConfigureTimeline(HorizontalSkillTreeScaffoldUI timeline)
    {
        _horizontalTimeline = timeline;
    }

    private void OnEnable()
    {
        EnsureReferences();
        SubscribeBottomPanelLayout();
        ApplyAbilityNameCompactLayout();
        RebuildAbilityListLayout();
    }

    private void OnDisable() => UnsubscribeBottomPanelLayout();

    private void Awake() => EnsureReferences();

    public void Refresh(SkillDefinition skill, SkillsManager skillsManager)
    {
        EnsureReferences();
        ClearRows();

        if (skill == null)
        {
            SetPlaceholderVisible(true);
            if (summaryText != null)
                summaryText.text = string.Empty;
            return;
        }

        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;

        int level = skillsManager != null ? skillsManager.GetLevel(skill.skillType) : 1;
        var abilityTierLevels = SkillAbilityCommitRules.CollectSortedAbilityTierLevels(skill);
        bool hasStarterAttack = CombatStarterAttackAbility.TryGetCombatStarterAttackForSkill(skill, out AbilityDefinition starterAttack)
            && starterAttack != null
            && level >= Mathf.Max(1, starterAttack.unlockLevel)
            && (skillsManager == null
                || CombatStarterAttackAbility.IsCombatStarterAttackUnlockedForGameplay(skill, starterAttack, skillsManager));

        if (abilityTierLevels.Count == 0 && !hasStarterAttack)
        {
            SetPlaceholderVisible(true);
            if (summaryText != null)
                summaryText.text = "No abilities yet";
            return;
        }

        int unlockedTiersCount = CountUnlockedAbilityTiers(abilityTierLevels, level);
        int selectedInTreeCount = CountSelectedAbilitiesInTree(skill, level, skillsManager);
        if (hasStarterAttack)
        {
            unlockedTiersCount += 1;
            selectedInTreeCount += 1;
        }

        if (summaryText != null)
            summaryText.text = $"Abilities unlocked: {unlockedTiersCount}\nAbilities selected: {selectedInTreeCount}";

        Transform rowsParent = EnsureRowsContainer();
        if (rowsParent == null)
            return;

        _rootCanvas ??= GetComponentInParent<Canvas>();

        if (hasStarterAttack)
            SpawnCommittedAbilityRow(skill, starterAttack, Mathf.Max(1, starterAttack.unlockLevel), skillsManager);

        for (int i = 0; i < abilityTierLevels.Count; i++)
        {
            int rowLevel = abilityTierLevels[i];
            if (level < rowLevel)
                continue;

            List<AbilityDefinition> siblings = SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(skill, rowLevel);
            if (siblings == null || siblings.Count == 0)
                continue;

            int pick = skillsManager != null ? skillsManager.GetSkillAbilityRowPick(skill.skillType, rowLevel, -1) : -1;

            if (pick < 0)
            {
                SpawnAvailableAbilityRow(rowLevel);
                continue;
            }

            if (pick >= siblings.Count)
                continue;

            AbilityDefinition def = siblings[pick];
            if (def == null)
                continue;

            if (skillsManager != null
                && !SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skill, def, skillsManager))
                continue;

            SpawnCommittedAbilityRow(skill, def, rowLevel, skillsManager);
        }

        bool hasRows = _rows.Count > 0;
        SetPlaceholderVisible(!hasRows);
        EnsureAutoAssignToolbarLayout();
        EnsureToolbarAboveAbilityRows();
        ApplyAbilityNameCompactLayout();
        RebuildAbilityListLayout();
    }

    private void SpawnCommittedAbilityRow(
        SkillDefinition skill,
        AbilityDefinition def,
        int rowLevel,
        SkillsManager skillsManager)
    {
        Transform rowsParent = EnsureRowsContainer();
        if (rowsParent == null || entryPrefab == null || def == null)
            return;

        AbilityEntryUI row = Instantiate(entryPrefab, rowsParent);
        row.gameObject.SetActive(true);
        row.SetRowLevelContext(rowLevel, HandleAbilityRowRightClick);
        row.Bind(def, unlocked: true, tooltip: null, _rootCanvas, () => FocusAbilityInTimeline(def));
        row.SetDoubleClickAssignHandler(HandleDoubleClickAssign);

        bool showNotSelected = SkillTimelineRowSelectionRules.HasPendingAbilityEnhancementChoice(
            skillsManager, skill, def);
        row.SetNotSelectedPrompt(showNotSelected);

        _rows.Add(row);
    }

    private void SpawnAvailableAbilityRow(int rowLevel)
    {
        Transform rowsParent = EnsureRowsContainer();
        if (rowsParent == null || entryPrefab == null)
            return;

        int scrollLevel = rowLevel;
        AbilityEntryUI row = Instantiate(entryPrefab, rowsParent);
        row.gameObject.SetActive(true);
        row.SetRowLevelContext(rowLevel, HandleAbilityRowRightClick);
        row.BindAvailableAbilityTier(rowLevel, tooltip: null, _rootCanvas, () => ScrollAbilityTierIntoView(scrollLevel));
        _rows.Add(row);
    }

    private void HandleAbilityRowRightClick(int rowLevel)
    {
        ScrollAbilityTierIntoView(rowLevel);
    }

    private void ScrollAbilityTierIntoView(int rowLevel)
    {
        if (_horizontalTimeline == null)
            return;

        _horizontalTimeline.ScrollToLevel(Mathf.Max(1, rowLevel));
    }

    private void FocusAbilityInTimeline(AbilityDefinition def)
    {
        if (def == null)
            return;

        if (_horizontalTimeline != null && _horizontalTimeline.TryFocusAbility(def))
            return;

        _horizontalTimeline?.ScrollToLevel(Mathf.Max(1, def.unlockLevel));
    }

    private void HandleDoubleClickAssign(AbilityDefinition def)
    {
        if (def == null)
            return;

        ActionBarUI bar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (bar == null)
            return;

        if (ActionBarUI.IsGatheringSkillType(def.sourceSkill))
            bar.ShowGatheringBarForSkill(def.sourceSkill, GatheringBarDriveKind.SkillsMenuSelection);

        bar.TryAssignAbilityToFirstEmptySlot(def);
    }

    private void ClearRows()
    {
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (_rows[i] != null)
                Destroy(_rows[i].gameObject);
        }

        _rows.Clear();

        Transform rowsParent = _rowsContainer != null ? _rowsContainer : listContent;
        if (rowsParent == null)
            return;

        for (int i = rowsParent.childCount - 1; i >= 0; i--)
        {
            Transform child = rowsParent.GetChild(i);
            if (child != null && child.GetComponent<AbilityEntryUI>() != null)
                Destroy(child.gameObject);
        }
    }

    private void EnsureReferences()
    {
        if (listContent == null)
            listContent = FindScrollContent(transform);

        if (listContent == null)
        {
            Transform page = transform;
            while (page != null && page.name != "SkillsAbilityPageNEW")
                page = page.parent;

            if (page != null)
            {
                Transform abilityList = page.Find("BottomPanelBar/AbilityList");
                if (abilityList != null)
                    listContent = FindScrollContent(abilityList);
            }
        }

        CachePlaceholderRoots();
        EnsureAutoAssignToolbarLayout();
        EnsureRowsContainer();
        EnsureToolbarAboveAbilityRows();

        if (summaryText == null)
            summaryText = transform.Find("AbilitiesUnlockedText")?.GetComponent<TMP_Text>();

        if (entryPrefab == null)
        {
            SkillsAbilitiesPageUI legacyPage =
                FindFirstObjectByType<SkillsAbilitiesPageUI>(FindObjectsInactive.Include);
            if (legacyPage != null && legacyPage.AbilityEntryPrefab != null)
                entryPrefab = legacyPage.AbilityEntryPrefab;
        }

        if (entryPrefab == null && _rowsContainer != null)
        {
            for (int i = 0; i < _rowsContainer.childCount; i++)
            {
                AbilityEntryUI template = _rowsContainer.GetChild(i).GetComponent<AbilityEntryUI>();
                if (template == null)
                    continue;

                entryPrefab = template;
                template.gameObject.SetActive(false);
                break;
            }
        }

        if (_rowsContainer is RectTransform rowsRt)
            ConfigureListLayout(rowsRt);

        if (listContent is RectTransform contentRt)
            ConfigureListLayout(contentRt);

        SubscribeBottomPanelLayout();
    }

    private void SubscribeBottomPanelLayout()
    {
        SkillsAbilityBottomPanelLayoutUI layout = ResolveBottomPanelLayout();
        if (layout == _bottomPanelLayout)
            return;

        UnsubscribeBottomPanelLayout();
        _bottomPanelLayout = layout;
        if (_bottomPanelLayout != null)
            _bottomPanelLayout.ExpandedChanged += OnBottomPanelExpandedChanged;
    }

    private void UnsubscribeBottomPanelLayout()
    {
        if (_bottomPanelLayout == null)
            return;

        _bottomPanelLayout.ExpandedChanged -= OnBottomPanelExpandedChanged;
        _bottomPanelLayout = null;
    }

    private void OnBottomPanelExpandedChanged(bool expanded)
    {
        ApplyAbilityNameCompactLayout();
        RebuildAbilityListLayout();
    }

    private void ApplyAbilityNameCompactLayout()
    {
        bool compact = _bottomPanelLayout != null && _bottomPanelLayout.IsExpanded;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
                _rows[i].SetNameLayoutCompact(compact);
        }
    }

    private void RebuildAbilityListLayout()
    {
        Transform rowsParent = _rowsContainer != null ? _rowsContainer : listContent;
        if (rowsParent is not RectTransform listRt)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(listRt);

        // Row heights depend on the name column width assigned by the horizontal layout group.
        if (_bottomPanelLayout != null && _bottomPanelLayout.IsExpanded)
            ApplyAbilityNameCompactLayout();

        LayoutRebuilder.ForceRebuildLayoutImmediate(listRt);
        if (listContent is RectTransform contentRt && contentRt != listRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
    }

    private SkillsAbilityBottomPanelLayoutUI ResolveBottomPanelLayout()
    {
        Transform page = transform;
        while (page != null && page.name != "SkillsAbilityPageNEW")
            page = page.parent;

        if (page == null)
            return null;

        Transform bar = page.Find("BottomPanelBar");
        return bar != null ? bar.GetComponent<SkillsAbilityBottomPanelLayoutUI>() : null;
    }

    private Transform EnsureRowsContainer()
    {
        if (_rowsContainer != null)
            return _rowsContainer;

        if (listContent == null)
            return null;

        Transform existing = listContent.Find(RowsContainerName);
        if (existing != null)
        {
            _rowsContainer = existing;
            return _rowsContainer;
        }

        var rowsGo = new GameObject(RowsContainerName, typeof(RectTransform));
        var rowsRt = rowsGo.GetComponent<RectTransform>();
        rowsRt.SetParent(listContent, false);
        rowsRt.SetAsLastSibling();
        rowsRt.anchorMin = new Vector2(0f, 1f);
        rowsRt.anchorMax = new Vector2(1f, 1f);
        rowsRt.pivot = new Vector2(0.5f, 1f);
        rowsRt.anchoredPosition = Vector2.zero;
        rowsRt.sizeDelta = new Vector2(0f, 0f);

        ConfigureListLayout(rowsRt);
        _rowsContainer = rowsRt;
        return _rowsContainer;
    }

    private static Transform FindScrollContent(Transform root)
    {
        if (root == null)
            return null;

        Transform scroll = root.Find("ScrollView");
        if (scroll != null)
        {
            Transform viewport = scroll.Find("Viewport");
            Transform content = viewport != null ? viewport.Find("Content") : null;
            if (content != null)
                return content;
        }

        Transform abilityContent = root.Find("AbilityContent");
        return abilityContent;
    }

    private void CachePlaceholderRoots()
    {
        if (_placeholderRoots.Count > 0 || listContent == null)
            return;

        Transform minorSection = listContent.Find("MinorPassiveUnlocksSection");
        if (minorSection != null)
        {
            _autoAssignSectionRoot = minorSection.gameObject;

            Transform minorContent = minorSection.Find("MinorPassiveContent");
            if (minorContent != null)
                _minorPassiveContentRoot = minorContent.gameObject;

            Transform addToBar = minorSection.Find("AddToBarButton");
            if (addToBar != null)
                _autoAssignBarRoot = addToBar.gameObject;
        }

        TMP_Text[] texts = listContent.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            if (_autoAssignBarRoot != null && text.transform.IsChildOf(_autoAssignBarRoot.transform))
                continue;

            if (text.text != "No unlocks yet" && text.text != "No abilities yet")
                continue;

            if (!_placeholderRoots.Contains(text.gameObject))
                _placeholderRoots.Add(text.gameObject);
        }
    }

    private void EnsureAutoAssignToolbarLayout()
    {
        CachePlaceholderRoots();

        Transform minorSection = _autoAssignSectionRoot != null
            ? _autoAssignSectionRoot.transform
            : listContent != null ? listContent.Find("MinorPassiveUnlocksSection") : null;

        if (minorSection == null)
            return;

        _autoAssignSectionRoot = minorSection.gameObject;

        Transform addToBar = _autoAssignBarRoot != null
            ? _autoAssignBarRoot.transform
            : minorSection.Find("AddToBarButton");

        if (addToBar == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                    continue;

                string name = button.name;
                if (name.IndexOf("AddToBar", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("AutoAssign", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    addToBar = button.transform;
                    break;
                }
            }
        }

        if (addToBar == null)
            return;

        _autoAssignBarRoot = addToBar.gameObject;

        // Keep the user's hierarchy: button lives in MinorPassiveUnlocksSection, not the header bar.
        if (addToBar.parent != minorSection)
        {
            addToBar.SetParent(minorSection, false);
            addToBar.SetAsFirstSibling();
        }

        if (minorSection is RectTransform sectionRt)
        {
            sectionRt.anchorMin = new Vector2(0f, 1f);
            sectionRt.anchorMax = new Vector2(1f, 1f);
            sectionRt.pivot = new Vector2(0.5f, 1f);
            sectionRt.sizeDelta = new Vector2(0f, 0f);
        }

        LayoutElement sectionLayout = minorSection.GetComponent<LayoutElement>();
        if (sectionLayout == null)
            sectionLayout = minorSection.gameObject.AddComponent<LayoutElement>();
        sectionLayout.minHeight = 25f;

        LayoutElement buttonLayout = addToBar.GetComponent<LayoutElement>();
        if (buttonLayout == null)
            buttonLayout = addToBar.gameObject.AddComponent<LayoutElement>();
        if (buttonLayout.preferredHeight < 1f)
            buttonLayout.preferredHeight = 25f;
        buttonLayout.minHeight = 25f;

        if (addToBar is RectTransform buttonRt)
        {
            buttonRt.anchorMin = new Vector2(0f, 1f);
            buttonRt.anchorMax = new Vector2(1f, 1f);
            buttonRt.pivot = new Vector2(0.5f, 1f);
            buttonRt.sizeDelta = new Vector2(0f, 25f);
        }

        TMP_Text label = addToBar.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            if (label.rectTransform != null)
            {
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(8f, 2f);
                label.rectTransform.offsetMax = new Vector2(-8f, -2f);
            }
        }

        minorSection.gameObject.SetActive(true);
        EnsureAutoAssignBarVisible();
    }

    private void EnsureToolbarAboveAbilityRows()
    {
        if (listContent == null)
            return;

        Transform minorSection = _autoAssignSectionRoot != null
            ? _autoAssignSectionRoot.transform
            : listContent.Find("MinorPassiveUnlocksSection");

        if (minorSection != null)
            minorSection.SetAsFirstSibling();

        if (_rowsContainer != null)
        {
            int targetIndex = minorSection != null ? 1 : 0;
            if (_rowsContainer.GetSiblingIndex() != targetIndex)
                _rowsContainer.SetSiblingIndex(targetIndex);
        }
    }

    private void EnsureAutoAssignBarVisible()
    {
        if (_autoAssignBarRoot != null)
            _autoAssignBarRoot.SetActive(true);
    }

    private void SetPlaceholderVisible(bool visible)
    {
        CachePlaceholderRoots();
        for (int i = 0; i < _placeholderRoots.Count; i++)
        {
            if (_placeholderRoots[i] != null)
                _placeholderRoots[i].SetActive(visible);
        }

        if (_minorPassiveContentRoot != null)
            _minorPassiveContentRoot.SetActive(visible);

        if (_autoAssignSectionRoot != null)
            _autoAssignSectionRoot.SetActive(true);

        EnsureAutoAssignBarVisible();
    }

    private void ConfigureListLayout(RectTransform listRt)
    {
        VerticalLayoutGroup vlg = listRt.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
            vlg = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = rowSpacing;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = listRt.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = listRt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static int CountUnlockedAbilityTiers(List<int> rows, int playerLevel)
    {
        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (playerLevel >= rows[i])
                count++;
        }

        return count;
    }

    private static int CountSelectedAbilitiesInTree(SkillDefinition skill, int playerLevel, SkillsManager skillsManager)
    {
        if (skill == null)
            return 0;

        var rows = SkillAbilityCommitRules.CollectSortedAbilityTierLevels(skill);
        int selected = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            int rowLevel = rows[i];
            if (playerLevel < rowLevel)
                continue;

            int pick = skillsManager != null ? skillsManager.GetSkillAbilityRowPick(skill.skillType, rowLevel, -1) : -1;
            if (pick >= 0)
                selected++;
        }

        return selected;
    }
}
