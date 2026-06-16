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

    [Header("Ability presets")]
    [SerializeField] private Button abilityPresetButton1;
    [SerializeField] private Button abilityPresetButton2;

    [Header("Optional labels")]
    [SerializeField] private TMP_Text selectedSkillTitleText;
    [SerializeField] private TMP_Text editingText;

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

    [Header("Action bar")]
    [Tooltip("Auto Assign to Bar — fills loadout slots with active abilities in list order.")]
    [SerializeField] private Button autoAssignAbilitiesButton;
    [Tooltip("Optional — feedback popups when auto-assign fails.")]
    [SerializeField] private PlayerController player;

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
    private UnityEngine.Events.UnityAction _autoAssignClickHandler;
    private readonly UnityEngine.Events.UnityAction[] _abilityPresetLeftClickHandlers =
        new UnityEngine.Events.UnityAction[SkillsManager.AbilityPresetSlotCount];
    private readonly AbilityPresetButtonUI[] _abilityPresetButtonUis =
        new AbilityPresetButtonUI[SkillsManager.AbilityPresetSlotCount];
    private bool _pendingAbilityPresetLabelRefresh;
    private ActionBarUI _cachedActionBar;
    private Coroutine _deferredProgressionRefresh;
    private Coroutine _deferredOpenRefresh;
    private readonly HashSet<SkillType> _pendingEntryGlowBySkill = new();
    private readonly Dictionary<SkillType, HashSet<int>> _pendingTreeGlowLevelsBySkill = new();

    // Per-skill tabs (Melee / Woodcutting) — brown selected style (see UITabBarButtonVisuals).
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
        if (editingText != null)
            editingText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        PreferRuntimeSkillsManager();
        RestoreCategoryModeFromPrefs();
        EnsureHierarchyReferences();
        EnsureDetailsPanelReferences();
        WireCategoryModeButtons();
        WireResetTreeButton();
        WireAbilityPresetButtons();
        WireAutoAssignAbilitiesButton();
        ApplyCategoryMode(showOnly: true);
        WireSkillTabButtons();
        TrySubscribeSkillsEvents();
        HookTreeGlowAcknowledge();
        ResolveInitialSkillSelection();
        RefreshAbilityPresetButtonLabels();
        RefreshEditingPresetText();
        RefreshTabSelectionVisuals();
        RefreshCategoryModeButtonVisuals();
        EnsureHorizontalTimelineReference();
        horizontalSkillTimeline?.SetScrollViewportVisible(false);
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
        SetTimelineScrollViewportVisible(true);
        UnhookTreeGlowAcknowledge();
        UnwireResetTreeButton();
        UnwireAbilityPresetButtons();
        UnwireAutoAssignAbilitiesButton();
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
        SetTimelineScrollViewportVisible(false);
        BeginTimelineScrollRestoreSession(_selectedSkill);
        try
        {
            RefreshView();
            EnsureHorizontalTimelineReference();
            horizontalSkillTimeline?.FlushPendingTimelineLayout();
        }
        finally
        {
            EndTimelineScrollRestoreSession();
            SetTimelineScrollViewportVisible(true);
        }
        RefreshTabSelectionVisuals();
        RefreshSkillsListSelection();
        ApplyActionBarForSelectedSkill();

        RefreshAbilityPresetButtonLabels();

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
        ApplyPendingTreeGlowForSelectedSkill();
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

        SkillsAbilitiesColdStartLevelUpGlow.MergeInto(_pendingEntryGlowBySkill, _pendingTreeGlowLevelsBySkill);
        skillsListPanel.Configure(skillDatabase, skillsManager, SelectSkill, HandleSkillEntryGlowAcknowledgedByHover);
        skillsListPanel.SetVisibleCategory(_categoryMode);
        skillsListPanel.ApplyPendingEntryGlows(_pendingEntryGlowBySkill);
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
        skillsListPanel.Configure(skillDatabase, skillsManager, SelectSkill, HandleSkillEntryGlowAcknowledgedByHover);
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
            skillsManager.ClearActiveAbilityPresetForSkill(_selectedSkill.skillType);
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

    private void WireAbilityPresetButtons()
    {
        EnsureAbilityPresetButtonReferences();

        WireSingleAbilityPresetButton(0, abilityPresetButton1);
        WireSingleAbilityPresetButton(1, abilityPresetButton2);
    }

    private void UnwireAbilityPresetButtons()
    {
        for (int i = 0; i < SkillsManager.AbilityPresetSlotCount; i++)
        {
            Button button = GetAbilityPresetButton(i);
            if (button != null && _abilityPresetLeftClickHandlers[i] != null)
                button.onClick.RemoveListener(_abilityPresetLeftClickHandlers[i]);

            if (_abilityPresetButtonUis[i] != null)
                _abilityPresetButtonUis[i].RightClicked -= HandleAbilityPresetRightClicked;
        }
    }

    private void EnsureAbilityPresetButtonReferences()
    {
        if (abilityPresetButton1 == null)
        {
            Transform topBar = transform.Find("TopBar");
            if (topBar != null)
                abilityPresetButton1 = topBar.Find("AbilityPresetButton1")?.GetComponent<Button>();
        }

        if (abilityPresetButton2 == null)
        {
            Transform topBar = transform.Find("TopBar");
            if (topBar != null)
                abilityPresetButton2 = topBar.Find("AbilityPresetButton2")?.GetComponent<Button>();
        }

        if (abilityPresetButton1 == null)
            abilityPresetButton1 = FindChildButtonNamed("AbilityPresetButton1");
        if (abilityPresetButton2 == null)
            abilityPresetButton2 = FindChildButtonNamed("AbilityPresetButton2");
    }

    private Button GetAbilityPresetButton(int slotIndex) =>
        slotIndex == 0 ? abilityPresetButton1 : abilityPresetButton2;

    private void WireSingleAbilityPresetButton(int slotIndex, Button button)
    {
        if (button == null)
            return;

        if (_abilityPresetLeftClickHandlers[slotIndex] == null)
        {
            int captured = slotIndex;
            _abilityPresetLeftClickHandlers[slotIndex] = () => OnAbilityPresetLeftClicked(captured);
        }

        button.onClick.RemoveListener(_abilityPresetLeftClickHandlers[slotIndex]);
        button.onClick.AddListener(_abilityPresetLeftClickHandlers[slotIndex]);

        AbilityPresetButtonUI relay = button.GetComponent<AbilityPresetButtonUI>();
        if (relay == null)
            relay = button.gameObject.AddComponent<AbilityPresetButtonUI>();

        if (_abilityPresetButtonUis[slotIndex] != null)
            _abilityPresetButtonUis[slotIndex].RightClicked -= HandleAbilityPresetRightClicked;

        relay.Configure(slotIndex);
        relay.RightClicked += HandleAbilityPresetRightClicked;
        _abilityPresetButtonUis[slotIndex] = relay;
    }

    private bool TryResolvePresetSkillType(out SkillType skillType)
    {
        if (_selectedSkill != null)
        {
            skillType = _selectedSkill.skillType;
            return true;
        }

        SkillDefinition hubSkill = SkillsAbilityPageSelectionHub.Current;
        if (hubSkill != null)
        {
            skillType = hubSkill.skillType;
            return true;
        }

        if (SkillsAbilityPageSelectionHub.TryGetLastSkillType(out skillType))
            return true;

        skillType = default;
        return false;
    }

    private void RefreshAbilityPresetButtonLabels()
    {
        PreferRuntimeSkillsManager();
        if (!TryResolvePresetSkillType(out SkillType skillType))
            return;

        for (int i = 0; i < SkillsManager.AbilityPresetSlotCount; i++)
        {
            Button button = GetAbilityPresetButton(i);
            if (button == null)
                continue;

            string label = skillsManager != null
                ? skillsManager.GetAbilityPresetResolvedDisplayName(skillType, i)
                : SkillsManager.GetAbilityPresetDefaultDisplayName(i);

            SetAbilityPresetButtonLabel(button, label);

            bool highlight = skillsManager != null
                && skillsManager.IsAbilityPresetActiveAndMatching(skillType, i);
            UITabBarButtonVisuals.Apply(button, highlight);
        }

        RefreshEditingPresetText();
    }

    private static void SetAbilityPresetButtonLabel(Button button, string label)
    {
        if (button == null || string.IsNullOrEmpty(label))
            return;

        TMP_Text tmp = button.GetComponent<TMP_Text>();
        if (tmp == null)
        {
            Transform textChild = button.transform.Find("Text");
            if (textChild != null)
                tmp = textChild.GetComponent<TMP_Text>();
        }

        if (tmp == null)
        {
            TMP_Text[] tmps = button.GetComponentsInChildren<TMP_Text>(true);
            for (int i = tmps.Length - 1; i >= 0; i--)
            {
                if (tmps[i] != null && tmps[i].transform.IsChildOf(button.transform))
                {
                    tmp = tmps[i];
                    break;
                }
            }
        }

        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
            legacy.text = label;
    }

    private void OnAbilityPresetLeftClicked(int slotIndex)
    {
        PreferRuntimeSkillsManager();
        if (skillsManager == null || !TryResolvePresetSkillType(out SkillType skillType))
            return;

        if (IsPresetSlotActive(skillType, slotIndex))
        {
            skillsManager.ClearActiveAbilityPresetForSkill(skillType);
            SaveManager.Instance?.Save();
            RefreshAbilityPresetButtonLabels();
            ApplyProgressionRefreshToPage();
            return;
        }

        if (!skillsManager.TryLoadAbilityPreset(skillType, slotIndex))
        {
            GameLog.Add("Empty preset");
            return;
        }

        SaveManager.Instance?.Save();
        EnsureHorizontalTimelineReference();
        horizontalSkillTimeline?.DismissOpenDetails();
        RefreshAbilityPresetButtonLabels();
        ApplyProgressionRefreshToPage();
    }

    private void HandleAbilityPresetRightClicked(int slotIndex, Vector2 screenPosition)
    {
        PreferRuntimeSkillsManager();
        if (skillsManager == null || !TryResolvePresetSkillType(out SkillType skillType))
            return;

        string presetName = skillsManager.GetAbilityPresetResolvedDisplayName(skillType, slotIndex);
        bool isSelected = IsPresetSlotActive(skillType, slotIndex);
        var entries = new List<ContextMenuEntry>();
        if (isSelected)
            entries.Add(new ContextMenuEntry("Unselect", () => UnselectAbilityPreset(skillType)));
        else
            entries.Add(new ContextMenuEntry("Select", () => OnAbilityPresetLeftClicked(slotIndex)));

        entries.Add(new ContextMenuEntry($"Save to: <size=82%>{presetName}</size>", () => SaveAbilityPreset(slotIndex)));
        entries.Add(new ContextMenuEntry("Edit", () => OpenAbilityPresetEditPopup(slotIndex)));
        entries.Add(new ContextMenuEntry("Reset", () => ResetAbilityPreset(slotIndex)));

        ContextMenuUI.EnsureInstance().ShowAtScreen(entries, screenPosition, presetName);
    }

    private void UnselectAbilityPreset(SkillType skillType)
    {
        skillsManager?.ClearActiveAbilityPresetForSkill(skillType);
        SaveManager.Instance?.Save();
        RefreshAbilityPresetButtonLabels();
    }

    private void SaveAbilityPreset(int slotIndex)
    {
        PreferRuntimeSkillsManager();
        if (skillsManager == null || !TryResolvePresetSkillType(out SkillType skillType))
            return;

        skillsManager.SaveCurrentSkillTreeToAbilityPreset(skillType, slotIndex);
        SaveManager.Instance?.Save();
        RefreshAbilityPresetButtonLabels();
    }

    private void ResetAbilityPreset(int slotIndex)
    {
        PreferRuntimeSkillsManager();
        if (skillsManager == null || !TryResolvePresetSkillType(out SkillType skillType))
            return;

        skillsManager.ResetAbilityPreset(skillType, slotIndex);
        SaveManager.Instance?.Save();
        RefreshAbilityPresetButtonLabels();
    }

    private void OpenAbilityPresetEditPopup(int slotIndex)
    {
        PreferRuntimeSkillsManager();
        if (skillsManager == null || _selectedSkill == null || !TryResolvePresetSkillType(out SkillType skillType))
            return;

        string current = skillsManager.GetAbilityPresetResolvedDisplayName(skillType, slotIndex);
        SkillAbilityPresetSave snapshot = skillsManager.GetAbilityPresetSlotSnapshot(skillType, slotIndex);
        string summary = SkillAbilityPresetSummaryBuilder.Build(_selectedSkill, snapshot, skillsManager);

        bool showWeaponSet = SkillsManager.IsCombatSkillType(skillType);
        skillsManager.GetAbilityPresetWeaponSetAssignment(
            skillType,
            slotIndex,
            out bool applyToSet,
            out int weaponSetIndex);

        AbilityPresetRenamePopupUI.Show(
            current,
            summary,
            showWeaponSet,
            applyToSet,
            weaponSetIndex,
            skillType,
            slotIndex,
            result =>
            {
                if (skillsManager == null || result == null)
                    return;

                skillsManager.SetAbilityPresetDisplayName(skillType, slotIndex, result.displayName);
                if (showWeaponSet)
                {
                    skillsManager.SetAbilityPresetWeaponSetAssignment(
                        skillType,
                        slotIndex,
                        result.applyToWeaponSet,
                        result.weaponSetIndex);
                }

                SaveManager.Instance?.Save();
                RefreshAbilityPresetButtonLabels();
                AbilityPresetWeaponSetLabelUI.RefreshAll();
            });
    }

    private void WireAutoAssignAbilitiesButton()
    {
        if (autoAssignAbilitiesButton == null)
        {
            Transform found = transform.Find(
                "BottomPanelBar/AbilityList/ScrollView/Viewport/Content/MinorPassiveUnlocksSection/AddToBarButton");
            if (found == null)
                found = transform.Find("BottomPanelBar/DetailsPanel/AbilityList/ScrollView/Viewport/Content/MinorPassiveUnlocksSection/AddToBarButton");
            if (found == null)
                found = transform.Find("AutoAssignAbilitiesButton");
            if (found == null)
                autoAssignAbilitiesButton = FindChildButtonNamed("AddToBarButton");
            else
                autoAssignAbilitiesButton = found.GetComponent<Button>();
        }

        if (autoAssignAbilitiesButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                    continue;

                string name = buttons[i].name;
                if (name.IndexOf("AutoAssign", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("AddToBar", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    autoAssignAbilitiesButton = buttons[i];
                    break;
                }
            }
        }

        if (autoAssignAbilitiesButton == null)
            return;

        if (_autoAssignClickHandler == null)
            _autoAssignClickHandler = HandleAutoAssignAbilitiesToBarClicked;

        autoAssignAbilitiesButton.onClick.RemoveListener(_autoAssignClickHandler);
        autoAssignAbilitiesButton.onClick.AddListener(_autoAssignClickHandler);
    }

    private void UnwireAutoAssignAbilitiesButton()
    {
        if (autoAssignAbilitiesButton == null || _autoAssignClickHandler == null)
            return;

        autoAssignAbilitiesButton.onClick.RemoveListener(_autoAssignClickHandler);
    }

    private void HandleAutoAssignAbilitiesToBarClicked()
    {
        PreferRuntimeSkillsManager();
        if (_selectedSkill == null)
        {
            ShowAutoAssignPopup("Select a skill first.");
            return;
        }

        if (skillsManager == null)
        {
            ShowAutoAssignPopup("Skills not loaded.");
            return;
        }

        int level = skillsManager.GetLevel(_selectedSkill.skillType);
        List<AbilityDefinition> abilities = SkillAbilityCommitRules.CollectUnlockedAbilitiesInPanelOrder(
            _selectedSkill, level, skillsManager);
        if (abilities.Count == 0)
        {
            ShowAutoAssignPopup("No unlocked abilities to assign.");
            return;
        }

        ActionBarUI bar = _cachedActionBar;
        if (bar == null)
            bar = _cachedActionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (bar == null)
        {
            ShowAutoAssignPopup("No action bar found.");
            return;
        }

        if (ActionBarUI.IsGatheringSkillType(_selectedSkill.skillType))
            bar.ShowGatheringBarForSkill(_selectedSkill.skillType, GatheringBarDriveKind.SkillsMenuSelection);
        else
            bar.ExitGatheringBarToCombat();

        int assigned = bar.ReplaceLoadoutAbilitiesInOrder(abilities);
        if (assigned <= 0)
            ShowAutoAssignPopup("Could not assign abilities to the action bar.");
    }

    private void ShowAutoAssignPopup(string message)
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
            player.ShowPopup(message);
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

        if (editingText == null)
        {
            Transform editing = transform.Find("TopBar/EditingText");
            if (editing == null)
                editing = transform.Find("TopBar/Text (TMP)");
            if (editing == null)
                editing = FindChildByName(transform, "EditingText");

            if (editing != null)
                editingText = editing.GetComponent<TMP_Text>();
        }

        if (editingText != null && !Application.isPlaying)
            editingText.gameObject.SetActive(false);
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
            UITabBarButtonVisuals.Apply(pair.Value, _selectedSkill != null && _selectedSkill.skillType == pair.Key);
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
            SetTimelineScrollViewportVisible(true);
            _deferredOpenRefresh = null;
            yield break;
        }

        BeginTimelineScrollRestoreSession(_selectedSkill);
        SetTimelineScrollViewportVisible(false);

        WireSkillTabButtons();
        RefreshSkillsList();
        RefreshPageLabels();
        RefreshTabSelectionVisuals();

        yield return null;
        if (!isActiveAndEnabled)
        {
            EndTimelineScrollRestoreSession();
            SetTimelineScrollViewportVisible(true);
            _deferredOpenRefresh = null;
            yield break;
        }

        SyncTimelineFromPageSelection();

        yield return null;
        if (!isActiveAndEnabled)
        {
            EndTimelineScrollRestoreSession();
            SetTimelineScrollViewportVisible(true);
            _deferredOpenRefresh = null;
            yield break;
        }

        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();

        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline != null)
            yield return horizontalSkillTimeline.CoWaitForPendingTimelineLayout();

        if (!isActiveAndEnabled)
        {
            EndTimelineScrollRestoreSession();
            SetTimelineScrollViewportVisible(true);
            _deferredOpenRefresh = null;
            yield break;
        }

        EndTimelineScrollRestoreSession();
        SetTimelineScrollViewportVisible(true);
        ReplayPendingGlowForVisibleUi();

        if (_pendingAbilityPresetLabelRefresh)
            _pendingAbilityPresetLabelRefresh = false;
        RefreshAbilityPresetButtonLabels();

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

    private float? TryReadTimelineScrollFromPrefs(SkillDefinition skill)
    {
        if (skill == null)
            return null;

        string key = GetTimelineScrollPrefsKey(skill.skillType);
        if (!PlayerPrefs.HasKey(key))
            return null;

        return Mathf.Clamp01(PlayerPrefs.GetFloat(key, 0f));
    }

    private void BeginTimelineScrollRestoreSession(SkillDefinition skill)
    {
        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline == null)
            return;

        horizontalSkillTimeline.BeginScrollRestoreSession(TryReadTimelineScrollFromPrefs(skill));
    }

    private void EndTimelineScrollRestoreSession()
    {
        if (horizontalSkillTimeline == null)
            return;

        horizontalSkillTimeline.EndScrollRestoreSession();
    }

    private void SetTimelineScrollViewportVisible(bool visible)
    {
        EnsureHorizontalTimelineReference();
        horizontalSkillTimeline?.SetScrollViewportVisible(visible);
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
        _pendingAbilityPresetLabelRefresh = true;

        if (isActiveAndEnabled)
        {
            _pendingAbilityPresetLabelRefresh = false;
            RefreshAbilityPresetButtonLabels();
            ApplyProgressionRefreshToPage();
            return;
        }

        AbilityPresetWeaponSetLabelUI.RefreshAll();
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
        ApplyPendingTreeGlowForSelectedSkill();
        RefreshActiveAbilitiesList();
        RefreshActiveBonusesPanel();
        ReplayPendingGlowForVisibleUi();
    }

    private void HandleSkillsLevelChanged(SkillType type, int newLevel)
    {
        SkillsAbilitiesColdStartLevelUpGlow.ConsumeBecauseLiveUiHandled(type);

        _pendingEntryGlowBySkill.Add(type);
        if (!_pendingTreeGlowLevelsBySkill.TryGetValue(type, out HashSet<int> levels))
        {
            levels = new HashSet<int>();
            _pendingTreeGlowLevelsBySkill[type] = levels;
        }

        levels.Add(newLevel);

        if (!isActiveAndEnabled)
            return;

        if (skillsListPanel != null)
            skillsListPanel.ShowUnlockGlowForSkill(type);

        if (_selectedSkill != null && _selectedSkill.skillType == type)
        {
            EnsureHorizontalTimelineReference();
            horizontalSkillTimeline?.HighlightNewUnlocksAtLevel(newLevel);
        }

        QueueDeferredProgressionRefresh();
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

        AutoSaveSelectedPresetForSkill(type);
        RefreshAbilityPresetButtonLabels();
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

        AutoSaveSelectedPresetForSkill(type);
        RefreshAbilityPresetButtonLabels();
    }

    private void AutoSaveSelectedPresetForSkill(SkillType skillType)
    {
        if (skillsManager == null)
            return;

        if (!skillsManager.TryGetActiveAbilityPresetSlot(skillType, out int activeSlot))
            return;

        skillsManager.SaveCurrentSkillTreeToAbilityPreset(skillType, activeSlot);
        SaveManager.Instance?.Save();
    }

    private bool IsPresetSlotActive(SkillType skillType, int slotIndex)
    {
        if (skillsManager == null)
            return false;
        return skillsManager.TryGetActiveAbilityPresetSlot(skillType, out int active)
               && active == Mathf.Clamp(slotIndex, 0, SkillsManager.AbilityPresetSlotCount - 1);
    }

    private void RefreshEditingPresetText()
    {
        if (editingText == null)
            return;

        if (skillsManager == null || !TryResolvePresetSkillType(out SkillType skillType))
        {
            editingText.gameObject.SetActive(false);
            return;
        }

        if (!skillsManager.TryGetActiveAbilityPresetSlot(skillType, out int activeSlot))
        {
            editingText.gameObject.SetActive(false);
            return;
        }

        string presetName = skillsManager.GetAbilityPresetResolvedDisplayName(skillType, activeSlot);
        string skillName = _selectedSkill != null && _selectedSkill.skillType == skillType
            ? SkillsAbilityPresentationResolver.ResolveSkillDisplayName(_selectedSkill)
            : skillType.ToString();
        if (string.IsNullOrWhiteSpace(skillName))
            skillName = skillType.ToString();

        editingText.text = $"Making changes to: {presetName} ({skillName.ToLowerInvariant()})";
        editingText.gameObject.SetActive(true);
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

        if (current.HasValue)
            horizontalSkillTimeline?.BeginScrollRestoreSession(current);

        SyncTimelineFromPageSelection();
        ApplyPendingTreeGlowForSelectedSkill();

        if (horizontalSkillTimeline == null)
            return;

        horizontalSkillTimeline.FlushPendingTimelineLayout();

        if (current.HasValue)
            horizontalSkillTimeline.EndScrollRestoreSession();
        else
            RestoreTimelineScrollForSkill(_selectedSkill);
    }

    private void HookTreeGlowAcknowledge()
    {
        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline == null)
            return;

        horizontalSkillTimeline.UnlockGlowAcknowledgedByHover -= HandleTreeGlowAcknowledgedByHover;
        horizontalSkillTimeline.UnlockGlowAcknowledgedByHover += HandleTreeGlowAcknowledgedByHover;
    }

    private void UnhookTreeGlowAcknowledge()
    {
        if (horizontalSkillTimeline == null)
            return;

        horizontalSkillTimeline.UnlockGlowAcknowledgedByHover -= HandleTreeGlowAcknowledgedByHover;
    }

    private void HandleSkillEntryGlowAcknowledgedByHover(SkillDefinition def)
    {
        if (def == null)
            return;

        _pendingEntryGlowBySkill.Remove(def.skillType);
    }

    private void HandleTreeGlowAcknowledgedByHover(int unlockLevel)
    {
        if (_selectedSkill == null)
            return;

        if (_pendingTreeGlowLevelsBySkill.TryGetValue(_selectedSkill.skillType, out HashSet<int> levels) && levels != null)
            levels.Remove(unlockLevel);
    }

    private void ApplyPendingTreeGlowForSelectedSkill()
    {
        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline == null || _selectedSkill == null)
            return;

        if (_pendingTreeGlowLevelsBySkill.TryGetValue(_selectedSkill.skillType, out HashSet<int> levels)
            && levels != null
            && levels.Count > 0)
        {
            horizontalSkillTimeline.SetPendingUnlockGlowLevels(levels);
            foreach (int lvl in levels)
                horizontalSkillTimeline.HighlightNewUnlocksAtLevel(lvl);
        }
        else
        {
            horizontalSkillTimeline.SetPendingUnlockGlowLevels(null);
        }
    }

    private void ReplayPendingGlowForVisibleUi()
    {
        if (skillsListPanel != null)
            skillsListPanel.ApplyPendingEntryGlows(_pendingEntryGlowBySkill);

        ApplyPendingTreeGlowForSelectedSkill();
    }
}
