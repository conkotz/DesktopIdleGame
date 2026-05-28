using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skills &amp; Abilities page (NEW layout): skill tab selection and horizontal timeline display.
/// Display-only for the timeline; does not change save data, unlock rules, or the vertical tree.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillsAbilityPageNewUI : MonoBehaviour
{
    private const string PrefsCategoryModeKey = "SkillsAbilityPageNEW.CategoryMode";
    private const string PrefsTimelineScrollPerSkillPrefix = "SkillsAbilityPageNEW.TimelineScroll.";

    [Header("Data")]
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private SkillsManager skillsManager;

    [Header("Category mode (Combat / Gathering)")]
    [SerializeField] private Button combatCategoryButton;
    [SerializeField] private Button gatheringCategoryButton;
    [Tooltip("Per-skill tabs for combat (Melee, Magic, Ranged, Endurance).")]
    [SerializeField] private Transform combatSkillsTabBarRoot;
    [Tooltip("Per-skill tabs for gathering (Woodcutting, Mining, Fishing). Hidden when combat mode is active.")]
    [SerializeField] private Transform gatheringSkillsTabBarRoot;

    [Header("Timeline")]
    [SerializeField] private HorizontalSkillTreeScaffoldUI horizontalSkillTimeline;
    [Tooltip("TopBar ResetTreeButton — clears all tree picks for the active skill tab.")]
    [SerializeField] private Button resetTreeButton;

    [Header("Optional labels")]
    [SerializeField] private TMP_Text selectedSkillTitleText;

    [Header("Details panel")]
    [SerializeField] private SkillNodeDetailsPanelUI skillNodeDetailsPanel;
    [Tooltip("Back arrow on ViewDetailsBar — collapses details and clears tree selection.")]
    [SerializeField] private Button collapseDetailsButton;

    [Header("Bottom panels")]
    [SerializeField] private SkillsAbilityActiveAbilitiesListUI activeAbilitiesList;
    [SerializeField] private AbilityEntryUI abilityEntryPrefab;
    [SerializeField] private SkillsAbilityActiveBonusesPanelUI activeBonusesPanel;
    [SerializeField] private SkillsAbilitySkillsListPanelUI skillsListPanel;
    [SerializeField] private SkillListEntryUI skillEntryPrefab;

    public SkillListEntryUI SkillEntryPrefab => skillEntryPrefab;

    private SkillDefinition _selectedSkill;
    private SkillCategory _categoryMode = SkillCategory.Combat;
    private bool _skipSelectionHubNotify;
    private Transform _activeSkillTabBarRoot;
    private readonly Dictionary<SkillType, Button> _tabButtonBySkillType = new();
    private readonly Dictionary<SkillType, UnityEngine.Events.UnityAction> _tabClickHandlers = new();
    private bool _skillsEventsSubscribed;
    private UnityEngine.Events.UnityAction _combatCategoryHandler;
    private UnityEngine.Events.UnityAction _gatheringCategoryHandler;
    private UnityEngine.Events.UnityAction _resetTreeClickHandler;
    private Coroutine _deferredProgressionRefresh;
    private Coroutine _deferredOpenRefresh;

    // Per-skill tabs (Melee / Woodcutting) — brown selected style.
    private static readonly Color TabSelectedImageColor = new Color(0.36078432f, 0.26666668f, 0.12941177f, 1f);
    private static readonly Color TabNormalImageColor = new Color(0.18431373f, 0.16078432f, 0.13725491f, 1f);
    private static readonly Color TabSelectedOutlineColor = new Color(0.6039216f, 0.48235294f, 0.2627451f, 0.5f);
    private static readonly Vector2 TabSelectedOutlineDistance = new Vector2(4f, -4f);

    // Combat Skills / Gathering Skills category toggle — cream selected, dark unselected (NodeToggleBar).
    private static readonly Color CategorySelectedImageColor = new Color(0.8784314f, 0.827451f, 0.7372549f, 1f);
    private static readonly Color CategoryNormalImageColor = new Color(0.20392157f, 0.1764706f, 0.15294118f, 1f);
    private static readonly Color CategorySelectedTextColor = new Color(0.1254902f, 0.101960786f, 0.08235294f, 1f);
    private static readonly Color CategoryNormalTextColor = new Color(0.68235296f, 0.63529414f, 0.5803922f, 1f);
    private static readonly Color CategorySelectedOutlineColor = new Color(0.6039216f, 0.48235294f, 0.2627451f, 0.5f);
    private static readonly Vector2 CategorySelectedOutlineDistance = new Vector2(4f, -4f);

    public SkillDefinition SelectedSkill => _selectedSkill;

    public SkillDatabase SkillDatabase => skillDatabase;

    public SkillCategory CategoryMode => _categoryMode;

    private void Awake()
    {
        PreferRuntimeSkillsManager();
        EnsureHierarchyReferences();
        EnsureHorizontalTimelineReference();
        EnsureSkillLevelTextReference();
        EnsureBottomPanelReferences();
        EnsureDetailsPanelReferences();
    }

    private void OnEnable()
    {
        PreferRuntimeSkillsManager();
        RestoreCategoryModeFromPrefs();
        EnsureHierarchyReferences();
        EnsureDetailsPanelReferences();
        WireCategoryModeButtons();
        WireResetTreeButton();
        ApplyCategoryMode(showOnly: true);
        WireSkillTabButtons();
        TrySubscribeSkillsEvents();
        ResolveInitialSkillSelection();
        RefreshTabSelectionVisuals();
        RefreshCategoryModeButtonVisuals();
        QueueDeferredOpenRefresh();
    }

    private void OnDisable()
    {
        if (_deferredOpenRefresh != null)
        {
            StopCoroutine(_deferredOpenRefresh);
            _deferredOpenRefresh = null;
        }

        if (_deferredProgressionRefresh != null)
        {
            StopCoroutine(_deferredProgressionRefresh);
            _deferredProgressionRefresh = null;
        }

        if (_selectedSkill != null)
            SkillsAbilityPageSelectionHub.SaveLastSkillType(_selectedSkill.skillType);
        SaveTimelineScrollForSkill(_selectedSkill);

        SaveCategoryModeToPrefs();
        UnwireResetTreeButton();
        TryUnsubscribeSkillsEvents();
    }

    public void SelectSkill(SkillDefinition skill)
    {
        if (skill == null)
            return;

        SaveTimelineScrollForSkill(_selectedSkill);

        if (skill.category != _categoryMode)
            SetCategoryMode(skill.category, selectDefaultSkill: false);

        _selectedSkill = skill;
        RefreshView();
        RestoreTimelineScrollForSkill(_selectedSkill);
        RefreshTabSelectionVisuals();
        RefreshSkillsListSelection();
        ApplyActionBarForSelectedSkill();

        if (!_skipSelectionHubNotify)
            SkillsAbilityPageSelectionHub.NotifySelection(skill, this);
    }

    public void ApplySelectionFromOtherPage(SkillDefinition skill)
    {
        if (skill == null)
            return;

        if (_selectedSkill == skill)
        {
            RefreshTabSelectionVisuals();
            RefreshSkillsListSelection();
            return;
        }

        _skipSelectionHubNotify = true;
        try
        {
            SelectSkill(skill);
        }
        finally
        {
            _skipSelectionHubNotify = false;
        }
    }

    public void SelectSkill(SkillType skillType)
    {
        SkillDefinition skill = GetSkillByType(skillType);
        if (skill != null)
            SelectSkill(skill);
    }

    public void SetCategoryMode(SkillCategory mode, bool selectDefaultSkill = true)
    {
        if (mode != SkillCategory.Combat && mode != SkillCategory.Gathering)
            mode = SkillCategory.Combat;

        if (_categoryMode == mode && !selectDefaultSkill)
        {
            RefreshCategoryModeButtonVisuals();
            return;
        }

        _categoryMode = mode;
        SaveCategoryModeToPrefs();
        ApplyCategoryMode(showOnly: false);

        if (!selectDefaultSkill)
        {
            RefreshCategoryModeButtonVisuals();
            return;
        }

        SkillDefinition pick = GetDefaultSkillForMode(_categoryMode);
        if (pick != null)
            SelectSkill(pick);
        else
        {
            _selectedSkill = null;
            RefreshView();
            RefreshSkillsList();
            RefreshActiveBonusesPanel();
            RefreshCategoryModeButtonVisuals();
        }
    }

    private void RefreshView()
    {
        RefreshPageLabels();
        SyncTimelineFromPageSelection();
        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();
        RefreshSkillsListLevels();
        RefreshSkillsListSelection();
    }

    private void RefreshPageLabels()
    {
        PreferRuntimeSkillsManager();

        if (_selectedSkill == null)
        {
            if (selectedSkillTitleText != null)
                selectedSkillTitleText.text = "No Skill Selected";
            return;
        }

        int level = skillsManager != null ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        string displayName = SkillsAbilityPresentationResolver.ResolveSkillDisplayName(_selectedSkill);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = _selectedSkill.skillType.ToString();

        if (selectedSkillTitleText != null)
            selectedSkillTitleText.text = $"{displayName}: Lv {level}";
    }

    private void ApplyCategoryMode(bool showOnly)
    {
        if (combatSkillsTabBarRoot != null)
            combatSkillsTabBarRoot.gameObject.SetActive(_categoryMode == SkillCategory.Combat);

        if (gatheringSkillsTabBarRoot != null)
            gatheringSkillsTabBarRoot.gameObject.SetActive(_categoryMode == SkillCategory.Gathering);

        _activeSkillTabBarRoot = _categoryMode == SkillCategory.Gathering
            ? gatheringSkillsTabBarRoot
            : combatSkillsTabBarRoot;

        if (!showOnly)
        {
            WireSkillTabButtons();
            RefreshSkillsList();
        }

        RefreshCategoryModeButtonVisuals();
    }

    private void WireCategoryModeButtons()
    {
        if (combatCategoryButton != null)
        {
            if (_combatCategoryHandler != null)
                combatCategoryButton.onClick.RemoveListener(_combatCategoryHandler);

            _combatCategoryHandler = () => SetCategoryMode(SkillCategory.Combat);
            combatCategoryButton.onClick.AddListener(_combatCategoryHandler);
        }

        if (gatheringCategoryButton != null)
        {
            if (_gatheringCategoryHandler != null)
                gatheringCategoryButton.onClick.RemoveListener(_gatheringCategoryHandler);

            _gatheringCategoryHandler = () => SetCategoryMode(SkillCategory.Gathering);
            gatheringCategoryButton.onClick.AddListener(_gatheringCategoryHandler);
        }
    }

    private void RefreshCategoryModeButtonVisuals()
    {
        ApplyCategoryModeButtonVisual(combatCategoryButton, _categoryMode == SkillCategory.Combat);
        ApplyCategoryModeButtonVisual(gatheringCategoryButton, _categoryMode == SkillCategory.Gathering);
    }

    private static void ApplyCategoryModeButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();

        if (image != null)
            image.color = selected ? CategorySelectedImageColor : CategoryNormalImageColor;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = selected ? CategorySelectedTextColor : CategoryNormalTextColor;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        Outline outline = button.GetComponent<Outline>();
        if (selected)
        {
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();

            outline.effectColor = CategorySelectedOutlineColor;
            outline.effectDistance = CategorySelectedOutlineDistance;
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }
        else if (outline != null)
        {
            outline.enabled = false;
        }
    }

    private void EnsureSkillLevelTextReference()
    {
        if (selectedSkillTitleText != null)
            return;

        Transform timelineContainer = transform.Find("TimelineContainer");
        if (timelineContainer == null)
            return;

        selectedSkillTitleText = timelineContainer.Find("SkillLevelText")?.GetComponent<TMP_Text>();
    }

    private void RefreshActiveAbilitiesList()
    {
        EnsureBottomPanelReferences();
        if (activeAbilitiesList == null)
            return;

        PreferRuntimeSkillsManager();
        activeAbilitiesList.Refresh(_selectedSkill, skillsManager);
    }

    private void RefreshActiveBonusesPanel()
    {
        EnsureBottomPanelReferences();
        if (activeBonusesPanel == null)
            return;

        PreferRuntimeSkillsManager();
        activeBonusesPanel.Refresh(_selectedSkill, skillsManager);
    }

    private void RefreshSkillsList()
    {
        EnsureBottomPanelReferences();
        if (skillsListPanel == null)
            return;

        skillsListPanel.Configure(skillDatabase, skillsManager, SelectSkill);
        skillsListPanel.SetVisibleCategory(_categoryMode);
        RefreshSkillsListSelection();
    }

    private void RefreshSkillsListSelection()
    {
        if (skillsListPanel != null)
            skillsListPanel.RefreshSelection(_selectedSkill);
    }

    private void RefreshSkillsListLevels()
    {
        EnsureBottomPanelReferences();
        if (skillsListPanel == null)
            return;

        PreferRuntimeSkillsManager();
        skillsListPanel.Configure(skillDatabase, skillsManager, SelectSkill);
        skillsListPanel.RefreshAllLevels();
    }

    private void EnsureBottomPanelReferences()
    {
        EnsureActiveAbilitiesListReference();
        EnsureActiveBonusesPanelReference();
        EnsureSkillsListPanelReference();
    }

    private void EnsureDetailsPanelReferences()
    {
        if (skillNodeDetailsPanel == null)
            skillNodeDetailsPanel = GetComponentInChildren<SkillNodeDetailsPanelUI>(true);

        if (collapseDetailsButton == null)
        {
            Transform bar = transform.Find("BottomPanelBar/DetailsPanel/ViewDetailsBar");
            if (bar == null)
                bar = transform.Find("DetailsPanel/ViewDetailsBar");

            if (bar != null)
            {
                string[] collapseNames = { "CollapseDetailsButton", "CollapseDetails", "CollapseButton" };
                for (int i = 0; i < collapseNames.Length && collapseDetailsButton == null; i++)
                    collapseDetailsButton = bar.Find(collapseNames[i])?.GetComponent<Button>();
            }
        }

        if (collapseDetailsButton != null)
            collapseDetailsButton.interactable = true;

        if (skillNodeDetailsPanel != null && collapseDetailsButton != null)
            skillNodeDetailsPanel.AssignCollapseDetailsButton(collapseDetailsButton);
    }

    private void EnsureActiveAbilitiesListReference()
    {
        if (activeAbilitiesList == null)
            activeAbilitiesList = GetComponentInChildren<SkillsAbilityActiveAbilitiesListUI>(true);

        if (activeAbilitiesList == null)
        {
            Transform abilityList = transform.Find("BottomPanelBar/AbilityList");
            if (abilityList == null)
                abilityList = transform.Find("AbilityList");

            if (abilityList != null)
            {
                activeAbilitiesList = abilityList.GetComponent<SkillsAbilityActiveAbilitiesListUI>();
                if (activeAbilitiesList == null)
                    activeAbilitiesList = abilityList.gameObject.AddComponent<SkillsAbilityActiveAbilitiesListUI>();
            }
        }

        if (activeAbilitiesList != null)
        {
            EnsureAbilityEntryPrefabReference();
            if (abilityEntryPrefab != null)
                activeAbilitiesList.SetEntryPrefab(abilityEntryPrefab);

            if (horizontalSkillTimeline != null)
                activeAbilitiesList.ConfigureTimeline(horizontalSkillTimeline);
        }
    }

    private void EnsureActiveBonusesPanelReference()
    {
        if (activeBonusesPanel != null)
        {
            if (horizontalSkillTimeline != null)
                activeBonusesPanel.ConfigureTimeline(horizontalSkillTimeline);
            return;
        }

        Transform unlocks = transform.Find("BottomPanelBar/Unlocks");
        if (unlocks == null)
            unlocks = transform.Find("Unlocks");

        if (unlocks != null)
        {
            activeBonusesPanel = unlocks.GetComponent<SkillsAbilityActiveBonusesPanelUI>();
            if (activeBonusesPanel == null)
                activeBonusesPanel = unlocks.gameObject.AddComponent<SkillsAbilityActiveBonusesPanelUI>();
        }

        if (activeBonusesPanel != null && horizontalSkillTimeline != null)
            activeBonusesPanel.ConfigureTimeline(horizontalSkillTimeline);
    }

    private void EnsureSkillsListPanelReference()
    {
        if (skillsListPanel == null)
        {
            Transform skillsProgress = transform.Find("BottomPanelBar/SkillsProgress");
            if (skillsProgress == null)
                skillsProgress = transform.Find("SkillsProgress");

            if (skillsProgress != null)
            {
                skillsListPanel = skillsProgress.GetComponent<SkillsAbilitySkillsListPanelUI>();
                if (skillsListPanel == null)
                    skillsListPanel = skillsProgress.gameObject.AddComponent<SkillsAbilitySkillsListPanelUI>();
            }
        }

        if (skillEntryPrefab == null)
        {
            SkillsAbilitiesPageUI legacyPage =
                FindFirstObjectByType<SkillsAbilitiesPageUI>(FindObjectsInactive.Include);
            if (legacyPage != null)
            {
                SkillListEntryUI[] templates = legacyPage.GetComponentsInChildren<SkillListEntryUI>(true);
                if (templates != null && templates.Length > 0)
                    skillEntryPrefab = templates[0];
            }
        }

        if (skillsListPanel != null && skillEntryPrefab != null)
            skillsListPanel.SetEntryPrefab(skillEntryPrefab);
    }

    private void EnsureAbilityEntryPrefabReference()
    {
        if (abilityEntryPrefab != null)
            return;

        SkillsAbilitiesPageUI legacyPage =
            FindFirstObjectByType<SkillsAbilitiesPageUI>(FindObjectsInactive.Include);
        if (legacyPage != null && legacyPage.AbilityEntryPrefab != null)
            abilityEntryPrefab = legacyPage.AbilityEntryPrefab;
    }

    /// <summary>
    /// Reset Tree: clears every committed pick on the active skill — ability rows, major passives,
    /// capstones, and enhancement branches. Skill level / XP are unchanged.
    /// </summary>
    public void OnResetSkillTreeButtonClicked()
    {
        PreferRuntimeSkillsManager();
        bool clearedAny = false;

        if (skillsManager == null)
        {
            Debug.LogWarning(
                "[SkillsAbilityPageNewUI] Reset Tree: no SkillsManager found. Progression will not clear until one exists.",
                this);
        }
        else if (_selectedSkill != null)
        {
            skillsManager.ResetSkillTreeSelectionsForSkill(_selectedSkill.skillType);
            clearedAny = true;
            SaveManager.Instance?.Save();
        }
        else
            Debug.LogWarning("[SkillsAbilityPageNewUI] Reset Tree: no skill selected; nothing to reset.", this);

        EnsureHorizontalTimelineReference();
        horizontalSkillTimeline?.DismissOpenDetails();

        SyncTimelineFromPageSelection();
        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();

        if (clearedAny && _selectedSkill != null)
            GameLog.Add($"Reset {_selectedSkill.displayName} skill tree selections.");
    }

    private void WireResetTreeButton()
    {
        if (resetTreeButton == null)
        {
            Transform topBar = transform.Find("TopBar");
            if (topBar != null)
                resetTreeButton = topBar.Find("ResetTreeButton")?.GetComponent<Button>();
        }

        if (resetTreeButton == null)
            resetTreeButton = FindChildButtonNamed("ResetTreeButton");

        if (resetTreeButton == null)
            return;

        if (_resetTreeClickHandler == null)
            _resetTreeClickHandler = OnResetSkillTreeButtonClicked;

        resetTreeButton.onClick.RemoveListener(_resetTreeClickHandler);
        resetTreeButton.onClick.AddListener(_resetTreeClickHandler);
    }

    private void UnwireResetTreeButton()
    {
        if (resetTreeButton == null || _resetTreeClickHandler == null)
            return;

        resetTreeButton.onClick.RemoveListener(_resetTreeClickHandler);
    }

    private Button FindChildButtonNamed(string leafName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == leafName)
                return buttons[i];
        }

        return null;
    }

    private void EnsureHierarchyReferences()
    {
        if (combatCategoryButton == null)
            combatCategoryButton = transform.Find("TopBar/NodeToggleBar/CombatSkills")?.GetComponent<Button>();

        if (gatheringCategoryButton == null)
            gatheringCategoryButton = transform.Find("TopBar/NodeToggleBar/GatheringSkills")?.GetComponent<Button>();

        if (combatSkillsTabBarRoot == null)
        {
            combatSkillsTabBarRoot = transform.Find("TopBar/CombatSkillsTabBar");
            if (combatSkillsTabBarRoot == null)
                combatSkillsTabBarRoot = transform.Find("TopBar/SkillsTabBar");
            if (combatSkillsTabBarRoot == null)
                combatSkillsTabBarRoot = FindChildByName(transform, "CombatSkillsTabBar");
        }

        if (gatheringSkillsTabBarRoot == null)
        {
            gatheringSkillsTabBarRoot = transform.Find("TopBar/GatheringSkillsTabBar");
            if (gatheringSkillsTabBarRoot == null)
                gatheringSkillsTabBarRoot = FindChildByName(transform, "GatheringSkillsTabBar");
        }
    }

    private static Transform FindChildByName(Transform root, string leafName)
    {
        if (root == null || string.IsNullOrEmpty(leafName))
            return null;

        if (root.name == leafName)
            return root;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == leafName)
                return all[i];
        }

        return null;
    }

    private void SyncTimelineFromPageSelection()
    {
        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline == null)
            return;

        horizontalSkillTimeline.SetSelectedSkill(_selectedSkill);
        horizontalSkillTimeline.BuildFromSelectedSkill();
    }

    private void WireSkillTabButtons()
    {
        _tabButtonBySkillType.Clear();
        if (_activeSkillTabBarRoot == null)
            _activeSkillTabBarRoot = _categoryMode == SkillCategory.Gathering
                ? gatheringSkillsTabBarRoot
                : combatSkillsTabBarRoot;

        if (_activeSkillTabBarRoot == null)
            return;

        Button[] buttons = _activeSkillTabBarRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            if (!Enum.TryParse(button.gameObject.name, ignoreCase: true, out SkillType skillType))
                continue;

            SkillDefinition tabSkill = GetSkillByType(skillType);
            if (tabSkill != null && tabSkill.category != _categoryMode)
                continue;

            _tabButtonBySkillType[skillType] = button;
            if (_tabClickHandlers.TryGetValue(skillType, out UnityEngine.Events.UnityAction existing))
                button.onClick.RemoveListener(existing);

            SkillType captured = skillType;
            UnityEngine.Events.UnityAction handler = () => OnSkillTabClicked(captured);
            _tabClickHandlers[skillType] = handler;
            button.onClick.AddListener(handler);
        }
    }

    private void OnSkillTabClicked(SkillType skillType)
    {
        if (skillsManager != null)
            skillsManager.SetActiveXpDisplay(skillType, string.Empty);

        SelectSkill(skillType);
    }

    private void RefreshTabSelectionVisuals()
    {
        foreach (KeyValuePair<SkillType, Button> pair in _tabButtonBySkillType)
            ApplySkillTabVisual(pair.Value, _selectedSkill != null && _selectedSkill.skillType == pair.Key);
    }

    private static void ApplySkillTabVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();

        if (image != null)
            image.color = selected ? TabSelectedImageColor : TabNormalImageColor;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
        colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        Outline outline = button.GetComponent<Outline>();
        if (selected)
        {
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();

            outline.effectColor = TabSelectedOutlineColor;
            outline.effectDistance = TabSelectedOutlineDistance;
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }
        else if (outline != null)
        {
            outline.enabled = false;
        }
    }

    /// <summary>Pick default tab/skill without rebuilding timeline or bottom panels (deferred on enable).</summary>
    private void ResolveInitialSkillSelection()
    {
        if (skillDatabase == null)
            return;

        PreferRuntimeSkillsManager();

        if (_selectedSkill == null)
        {
            SkillDefinition hubSkill = SkillsAbilityPageSelectionHub.Current;
            if (hubSkill != null)
                _selectedSkill = hubSkill;
        }

        if (_selectedSkill == null && SkillsAbilityPageSelectionHub.TryGetLastSkillType(out SkillType savedType))
        {
            SkillDefinition fromSaved = GetSkillByType(savedType);
            if (fromSaved != null)
                _selectedSkill = fromSaved;
        }

        if (_selectedSkill == null && skillsManager != null)
        {
            SkillDefinition fromActiveXp = GetSkillByType(skillsManager.ActiveSkill);
            if (fromActiveXp != null)
                _selectedSkill = fromActiveXp;
        }

        if (_selectedSkill != null)
            _categoryMode = _selectedSkill.category;

        if (_selectedSkill == null)
            _selectedSkill = GetDefaultSkillForMode(_categoryMode);
    }

    private void QueueDeferredOpenRefresh()
    {
        if (_deferredOpenRefresh != null)
            StopCoroutine(_deferredOpenRefresh);

        _deferredOpenRefresh = StartCoroutine(CoDeferredOpenRefresh());
    }

    /// <summary>Spreads timeline/list rebuild across frames so opening the page does not hitch.</summary>
    private IEnumerator CoDeferredOpenRefresh()
    {
        yield return null;
        if (!isActiveAndEnabled)
        {
            _deferredOpenRefresh = null;
            yield break;
        }

        WireSkillTabButtons();
        RefreshSkillsList();
        RefreshPageLabels();
        RefreshTabSelectionVisuals();

        yield return null;
        if (!isActiveAndEnabled)
        {
            _deferredOpenRefresh = null;
            yield break;
        }

        SyncTimelineFromPageSelection();

        yield return null;
        if (!isActiveAndEnabled)
        {
            _deferredOpenRefresh = null;
            yield break;
        }

        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();

        // Final post-layout reapply: some downstream UI rebuilds can nudge the timeline once after open.
        yield return null;
        if (!isActiveAndEnabled)
        {
            _deferredOpenRefresh = null;
            yield break;
        }
        RestoreTimelineScrollForSkill(_selectedSkill);

        _deferredOpenRefresh = null;
    }

    private static string GetTimelineScrollPrefsKey(SkillType skillType) =>
        PrefsTimelineScrollPerSkillPrefix + ((int)skillType).ToString();

    private void SaveTimelineScrollForSkill(SkillDefinition skill)
    {
        if (skill == null)
            return;

        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline == null)
            return;

        float? normalized = horizontalSkillTimeline.TryGetTimelineScrollNormalizedPosition();
        if (!normalized.HasValue)
            return;

        PlayerPrefs.SetFloat(GetTimelineScrollPrefsKey(skill.skillType), Mathf.Clamp01(normalized.Value));
    }

    private void RestoreTimelineScrollForSkill(SkillDefinition skill)
    {
        if (skill == null)
            return;

        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline == null)
            return;

        string key = GetTimelineScrollPrefsKey(skill.skillType);
        if (!PlayerPrefs.HasKey(key))
            return;

        float normalized = Mathf.Clamp01(PlayerPrefs.GetFloat(key, 0f));
        horizontalSkillTimeline.ApplyTimelineScrollNormalizedPosition(normalized);
    }

    private SkillDefinition GetDefaultSkillForMode(SkillCategory mode)
    {
        if (mode == SkillCategory.Gathering)
            return GetSkillByType(SkillType.Woodcutting)
                   ?? GetFirstSkillInCategory(SkillCategory.Gathering);

        return GetSkillByType(SkillType.Melee)
               ?? GetFirstSkillInCategory(SkillCategory.Combat);
    }

    private SkillDefinition GetSkillByType(SkillType type)
    {
        if (skillDatabase == null || skillDatabase.Skills == null)
            return null;

        for (int i = 0; i < skillDatabase.Skills.Count; i++)
        {
            SkillDefinition s = skillDatabase.Skills[i];
            if (s != null && s.skillType == type)
                return s;
        }

        return null;
    }

    private SkillDefinition GetFirstSkillInCategory(SkillCategory category)
    {
        if (skillDatabase == null || skillDatabase.Skills == null)
            return null;

        for (int i = 0; i < skillDatabase.Skills.Count; i++)
        {
            SkillDefinition s = skillDatabase.Skills[i];
            if (s != null && s.category == category)
                return s;
        }

        return null;
    }

    private void ApplyActionBarForSelectedSkill()
    {
        if (_selectedSkill == null)
            return;

        ActionBarUI bar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (bar == null)
            return;

        if (ActionBarUI.IsGatheringSkillType(_selectedSkill.skillType))
            bar.ShowGatheringBarForSkill(_selectedSkill.skillType, GatheringBarDriveKind.SkillsMenuSelection);
        else
            bar.ExitGatheringBarToCombat();
    }

    private void RestoreCategoryModeFromPrefs()
    {
        string saved = PlayerPrefs.GetString(PrefsCategoryModeKey, SkillCategory.Combat.ToString());
        if (Enum.TryParse(saved, ignoreCase: true, out SkillCategory mode)
            && (mode == SkillCategory.Combat || mode == SkillCategory.Gathering))
        {
            _categoryMode = mode;
        }
        else
        {
            _categoryMode = SkillCategory.Combat;
        }
    }

    private void SaveCategoryModeToPrefs()
    {
        PlayerPrefs.SetString(PrefsCategoryModeKey, _categoryMode.ToString());
        PlayerPrefs.Save();
    }

    private void EnsureHorizontalTimelineReference()
    {
        if (horizontalSkillTimeline == null)
            horizontalSkillTimeline = GetComponentInChildren<HorizontalSkillTreeScaffoldUI>(true);
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            skillsManager = SkillsManager.Instance;
        else if (skillsManager == null)
            skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }

    private void TrySubscribeSkillsEvents()
    {
        if (_skillsEventsSubscribed)
            return;

        PreferRuntimeSkillsManager();
        if (skillsManager == null)
            return;

        skillsManager.OnLevelUp += HandleSkillsLevelChanged;
        skillsManager.OnSkillLevelDecreased += HandleSkillsLevelChanged;
        skillsManager.OnXpGained += HandleSkillsXpGained;
        skillsManager.OnSkillAbilityRowPickChanged += HandleSkillAbilityRowPickChanged;
        skillsManager.OnSkillChoiceSelectionChanged += HandleSkillChoiceSelectionChanged;
        skillsManager.OnSkillProgressionLoaded += HandleSkillProgressionLoaded;
        _skillsEventsSubscribed = true;
    }

    private void TryUnsubscribeSkillsEvents()
    {
        if (skillsManager == null)
            return;

        skillsManager.OnLevelUp -= HandleSkillsLevelChanged;
        skillsManager.OnSkillLevelDecreased -= HandleSkillsLevelChanged;
        skillsManager.OnXpGained -= HandleSkillsXpGained;
        skillsManager.OnSkillAbilityRowPickChanged -= HandleSkillAbilityRowPickChanged;
        skillsManager.OnSkillChoiceSelectionChanged -= HandleSkillChoiceSelectionChanged;
        skillsManager.OnSkillProgressionLoaded -= HandleSkillProgressionLoaded;
        _skillsEventsSubscribed = false;
    }

    private void HandleSkillProgressionLoaded()
    {
        if (!isActiveAndEnabled)
            return;

        ApplyProgressionRefreshToPage();
    }

    private void QueueDeferredProgressionRefresh()
    {
        if (_deferredProgressionRefresh != null)
            StopCoroutine(_deferredProgressionRefresh);

        _deferredProgressionRefresh = StartCoroutine(CoDeferredProgressionRefresh());
    }

    private IEnumerator CoDeferredProgressionRefresh()
    {
        yield return null;
        _deferredProgressionRefresh = null;

        if (!isActiveAndEnabled)
            yield break;

        ApplyProgressionRefreshToPage();
    }

    private void ApplyProgressionRefreshToPage()
    {
        RefreshPageLabels();
        RefreshSkillsListLevels();
        SyncTimelineFromPageSelectionPreservingCurrentScroll();
        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();
    }

    private void HandleSkillsLevelChanged(SkillType type, int _)
    {
        if (!isActiveAndEnabled)
            return;

        if (_selectedSkill != null && _selectedSkill.skillType == type)
        {
            RefreshPageLabels();
            SyncTimelineFromPageSelectionPreservingCurrentScroll();
            RefreshActiveAbilitiesList();
            RefreshActiveBonusesPanel();
        }

        if (skillsListPanel != null)
            skillsListPanel.RefreshLevelsForSkill(type);
    }

    private void HandleSkillsXpGained(SkillType type, int _, string __)
    {
        if (!isActiveAndEnabled)
            return;

        if (skillsListPanel != null)
            skillsListPanel.RefreshLevelsForSkill(type);
    }

    private void HandleSkillAbilityRowPickChanged(SkillType type, int _, int __)
    {
        if (!isActiveAndEnabled || _selectedSkill == null)
            return;

        if (_selectedSkill.skillType != type)
            return;

        float? preservedScroll = horizontalSkillTimeline != null
            ? horizontalSkillTimeline.TryGetTimelineScrollNormalizedPosition()
            : null;

        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();
        RefreshTimelineAfterPickOrEnhancementChange();

        if (preservedScroll.HasValue && horizontalSkillTimeline != null)
            horizontalSkillTimeline.ApplyTimelineScrollNormalizedPosition(preservedScroll.Value);
    }

    private void HandleSkillChoiceSelectionChanged(SkillType type, int _, int __)
    {
        if (!isActiveAndEnabled || _selectedSkill == null)
            return;

        if (_selectedSkill.skillType != type)
            return;

        float? preservedScroll = horizontalSkillTimeline != null
            ? horizontalSkillTimeline.TryGetTimelineScrollNormalizedPosition()
            : null;

        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();
        RefreshTimelineAfterPickOrEnhancementChange();

        if (preservedScroll.HasValue && horizontalSkillTimeline != null)
            horizontalSkillTimeline.ApplyTimelineScrollNormalizedPosition(preservedScroll.Value);
    }

    private void RefreshTimelineAfterPickOrEnhancementChange()
    {
        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline == null)
            return;

        horizontalSkillTimeline.RefreshTimelineSelectionVisuals();
        horizontalSkillTimeline.RefreshOpenDetailsAfterDataChange();
    }

    /// <summary>
    /// Rebuilds timeline content for the same selected skill while preserving the user's current horizontal scroll.
    /// Falls back to saved per-skill scroll when current position is unavailable.
    /// </summary>
    private void SyncTimelineFromPageSelectionPreservingCurrentScroll()
    {
        EnsureHorizontalTimelineReference();
        float? current = horizontalSkillTimeline != null
            ? horizontalSkillTimeline.TryGetTimelineScrollNormalizedPosition()
            : null;

        SyncTimelineFromPageSelection();

        if (horizontalSkillTimeline == null)
            return;

        if (current.HasValue)
            horizontalSkillTimeline.ApplyTimelineScrollNormalizedPosition(current.Value);
        else
            RestoreTimelineScrollForSkill(_selectedSkill);
    }
}
