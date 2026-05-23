using System;
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

    [Header("Optional labels")]
    [SerializeField] private TMP_Text selectedSkillTitleText;

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
    }

    private void OnEnable()
    {
        PreferRuntimeSkillsManager();
        RestoreCategoryModeFromPrefs();
        EnsureHierarchyReferences();
        WireCategoryModeButtons();
        ApplyCategoryMode(showOnly: true);
        WireSkillTabButtons();
        TrySubscribeSkillsEvents();
        SelectFirstSkillIfNeeded();
        RefreshPageLabels();
        RefreshTabSelectionVisuals();
        RefreshCategoryModeButtonVisuals();
        RefreshSkillsList();
        RefreshActiveBonusesPanel();

        if (_selectedSkill == null && skillDatabase != null)
            SelectSkill(GetDefaultSkillForMode(_categoryMode));
    }

    private void OnDisable()
    {
        if (_selectedSkill != null)
            SkillsAbilityPageSelectionHub.SaveLastSkillType(_selectedSkill.skillType);

        SaveCategoryModeToPrefs();
        TryUnsubscribeSkillsEvents();
    }

    public void SelectSkill(SkillDefinition skill)
    {
        if (skill == null)
            return;

        if (skill.category != _categoryMode)
            SetCategoryMode(skill.category, selectDefaultSkill: false);

        _selectedSkill = skill;
        RefreshView();
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

    private void EnsureBottomPanelReferences()
    {
        EnsureActiveAbilitiesListReference();
        EnsureActiveBonusesPanelReference();
        EnsureSkillsListPanelReference();
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

    private void SelectFirstSkillIfNeeded()
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

        if (_selectedSkill != null)
        {
            _skipSelectionHubNotify = true;
            try
            {
                ApplyCategoryMode(showOnly: true);
                WireSkillTabButtons();
                RefreshView();
                RefreshTabSelectionVisuals();
                RefreshSkillsList();
            }
            finally
            {
                _skipSelectionHubNotify = false;
            }
        }
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
        _skillsEventsSubscribed = false;
    }

    private void HandleSkillsLevelChanged(SkillType type, int _)
    {
        if (!isActiveAndEnabled)
            return;

        if (_selectedSkill != null && _selectedSkill.skillType == type)
        {
            RefreshPageLabels();
            SyncTimelineFromPageSelection();
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

        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();
        SyncTimelineFromPageSelection();
    }

    private void HandleSkillChoiceSelectionChanged(SkillType type, int _, int __)
    {
        if (!isActiveAndEnabled || _selectedSkill == null)
            return;

        if (_selectedSkill.skillType != type)
            return;

        RefreshActiveBonusesPanel();
        SyncTimelineFromPageSelection();
    }
}
