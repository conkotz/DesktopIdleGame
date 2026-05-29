using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Skills &amp; Abilities page: two-column left list (Gathering / Combat), center + right detail.
/// </summary>
public class SkillsAbilitiesPageUI : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Registry of SkillDefinition assets.")]
    [SerializeField] private SkillDatabase skillDatabase;
    [Tooltip("Registry of AbilityDefinition assets.")]
    [SerializeField] private AbilityDatabase abilityDatabase;

    [Tooltip("Player progression; auto-resolves to SkillsManager.Instance if unset.")]
    [SerializeField] private SkillsManager skillsManager;

    [Header("Optional gameplay context")]
    [Tooltip("If assigned, we can detect when an action is occurring and avoid overriding the bottom XP strip while busy.")]
    [SerializeField] private PlayerController player;

    [Header("Left panel — columns")]
    [Tooltip("Parent for Gathering skill rows (e.g. GatheringContent).")]
    [SerializeField] private Transform gatheringContent;

    [Tooltip("Parent for Combat skill rows (e.g. CombatContent).")]
    [SerializeField] private Transform combatContent;

    [FormerlySerializedAs("skillListEntryPrefab")]
    [Tooltip("Prefab for one skill row.")]
    [SerializeField] private SkillListEntryUI skillEntryPrefab;

    [Header("Center — selected skill")]
    [FormerlySerializedAs("selectedSkillTitleText")]
    [Tooltip("Title line, e.g. Mining (lv 2).")]
    [SerializeField] private TMP_Text centerTitleText;

    [FormerlySerializedAs("centerDetailPlaceholderText")]
    [Tooltip("Skill tree / center body placeholder until real UI exists.")]
    [SerializeField] private TMP_Text centerSkillTreePlaceholderText;
    [Tooltip("Center skill tree renderer (data-driven from selected SkillDefinition).")]
    [SerializeField] private SkillTreeViewUI centerSkillTreeView;

    [Header("Right panel — capstone")]
    [Tooltip("CapstoneHeader + CapstoneContent shown at skill level 50+. Hidden before that.")]
    [SerializeField] private RightPanelSectionRefs capstoneSection = new();

    [Header("Right panel — abilities")]
    [Tooltip("Assign section, header, summary text (AbilitiesUnlockedText), and list content (AbilityContent).")]
    [SerializeField] private RightPanelAbilitiesSectionRefs abilitiesSection = new();

    [Header("Right panel — major passives")]
    [Tooltip("MajorPassivesText until first major passive committed; then MajorPassivesContent rows.")]
    [SerializeField] private RightPanelMajorPassivesSectionRefs majorPassivesSection = new();

    [Header("Right panel — minor passives")]
    [Tooltip("Aggregated minor passive stat lines in content TMP.")]
    [SerializeField] private RightPanelTextSectionRefs minorPassivesSection = new();

    [Header("Right panel — additional unlocks")]
    [Tooltip("Unlock / minor-unlock rows as text lines in content TMP.")]
    [SerializeField] private RightPanelTextSectionRefs additionalUnlocksSection = new();

    [Header("Right panel — auto assign")]
    [Tooltip("Assigns unlocked abilities from this panel (top to bottom) into action-bar slots 1–5, replacing existing abilities.")]
    [SerializeField] private Button autoAssignAbilitiesButton;

    [Tooltip("Optional prefab for one ability row. If empty, a simple row is created at runtime.")]
    [SerializeField] private AbilityEntryUI abilityEntryPrefab;

    [SerializeField] private MajorPassiveListEntryUI majorPassiveEntryPrefab;
    [Tooltip("Height of major passive / capstone list rows in the right panel.")]
    [SerializeField] private float majorPassiveRowHeight = 40f;

    [Header("Right panel — layout auto-fix")]
    [Tooltip("Auto-configures runtime abilities list to stretch/fill properly in right panel layouts.")]
    [SerializeField] private bool autoFixRightPanelLayout = true;
    [Tooltip("Spacing between ability rows in the right panel.")]
    [SerializeField] private float abilityRowSpacing = 6f;
    [Tooltip("Padding inside right abilities list content.")]
    [SerializeField] private RectOffset abilityListPadding;

    [Header("Dev — completion progress banner (testers)")]
    [SerializeField] private GameObject completionProgressBannerRoot;
    [SerializeField] private Image completionProgressBannerBackground;
    [SerializeField] private TMP_Text completionProgressText;
    [Tooltip("Per-skill completion tier shown when that skill is selected. Update manually as you build content.")]
    [SerializeField] private List<SkillDevCompletionEntry> skillDevCompletionTiers = new();
    [SerializeField] private Color devTierVeryIncompleteColor = new Color(0.82f, 0.22f, 0.22f, 0.45f);
    [SerializeField] private Color devTierPartiallyCompleteColor = new Color(0.22f, 0.42f, 0.88f, 0.45f);
    [SerializeField] private Color devTierCompleteColor = new Color(0.22f, 0.72f, 0.32f, 0.45f);

    private readonly Dictionary<SkillType, SkillDevCompletionTier> _devTierBySkill = new();

    private SkillDefinition _selectedSkill;
    private bool _skipSelectionHubNotify;
    private Coroutine _deferredRefreshRoutine;
    private bool _loggedMissingRefs;
    private readonly Dictionary<SkillType, SkillListEntryUI> _entryBySkillType = new();
    private readonly HashSet<SkillType> _pendingEntryGlowBySkill = new();
    private readonly Dictionary<SkillType, HashSet<int>> _pendingTreeGlowLevelsBySkill = new();
    private bool _skillsEventsSubscribed;

    private string _passiveUnlockHighlightKey;

    private SharedTooltipUI _cachedSharedTooltip;
    private ActionBarUI _cachedActionBar;
    private SkillDefinition _cachedAbilitiesPanelSkill;
    private int _cachedAbilitiesPanelLevel = -1;
    private int _cachedAbilitiesPanelFingerprint = int.MinValue;

    private SkillDefinition _cachedMinorUnlocksSkill;
    private int _cachedMinorUnlocksLevel = -1;
    private string _cachedMinorUnlocksHighlightKey;
    private string _cachedMinorUnlocksDisplayText;

    private SkillDefinition _cachedAdditionalUnlocksSkill;
    private int _cachedAdditionalUnlocksLevel = -1;
    private string _cachedAdditionalUnlocksDisplayText;

    private SkillDefinition _cachedCapstoneSkill;
    private int _cachedCapstoneLevel = -1;
    private int _cachedCapstoneFingerprint = int.MinValue;

    private SkillDefinition _cachedMajorPassivesSkill;
    private int _cachedMajorPassivesLevel = -1;
    private int _cachedMajorPassivesFingerprint = int.MinValue;

    public SkillDefinition SelectedSkill => _selectedSkill;

    public AbilityEntryUI AbilityEntryPrefab => abilityEntryPrefab;

    private void Awake()
    {
        PreferRuntimeSkillsManager();

        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        _cachedSharedTooltip = FindBestSharedTooltip();
        _cachedActionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);

        if (abilityListPadding == null)
            abilityListPadding = new RectOffset(0, 0, 0, 0);

        if (minorPassivesSection.content)
            minorPassivesSection.content.richText = true;
        if (additionalUnlocksSection.content)
            additionalUnlocksSection.content.richText = true;

        if (!majorPassiveEntryPrefab)
        {
            MajorPassiveListEntryUI[] entries = GetComponentsInChildren<MajorPassiveListEntryUI>(true);
            if (entries != null && entries.Length > 0)
                majorPassiveEntryPrefab = entries[0];
        }

        if (!abilityDatabase)
            abilityDatabase = AbilityDatabase.LoadDefault();

        ValidateRefsOnce();
        EnsureCenterTreeReference();
        EnsureRightPanelLayoutConfigured();
        RebuildDevCompletionTierLookup();
        TrySubscribeSkillsEvents();
        HookTreeGlowAcknowledge();
        WireAutoAssignAbilitiesButton();
        WireResetTreeButton();
    }

    private void OnEnable()
    {
        PreferRuntimeSkillsManager();
        TrySubscribeSkillsEvents();
        if (!abilityDatabase)
            abilityDatabase = AbilityDatabase.LoadDefault();
        EnsureCenterTreeReference();
        HookTreeGlowAcknowledge();

        SelectFirstSkillIfNeeded();
        EnsureRightPanelLayoutConfigured();
        RebuildSkillList();
        RefreshView();
        ReplayPendingGlowForVisibleUi();
        RefreshDevCompletionBanner();
        // Layout / tree bootstrap order: one frame later matches level-up deferred refresh so center tree + ability rows match the selected skill on first open.
        ScheduleDeferredProgressRefresh();
        WireAutoAssignAbilitiesButton();
        WireResetTreeButton();
    }

    private void OnDisable()
    {
        if (autoAssignAbilitiesButton != null)
            autoAssignAbilitiesButton.onClick.RemoveListener(HandleAutoAssignAbilitiesToBarClicked);

        if (_deferredRefreshRoutine != null)
        {
            StopCoroutine(_deferredRefreshRoutine);
            _deferredRefreshRoutine = null;
        }

        InvalidateAbilitiesPanelCache();
        InvalidateMinorUnlocksDisplayCache();
        InvalidateAdditionalUnlocksDisplayCache();
        InvalidateCapstonePanelCache();
        InvalidateMajorPassivesPanelCache();
        SetDevCompletionBannerVisible(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildDevCompletionTierLookup();
    }
#endif

    private void OnDestroy()
    {
        TryUnsubscribeSkillsEvents();
        UnhookTreeGlowAcknowledge();
    }

    /// <summary>
    /// Menu prefabs often serialize a scene SkillsManager; the real progression lives on <see cref="SkillsManager.Instance"/> (DontDestroyOnLoad).
    /// </summary>
    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            skillsManager = SkillsManager.Instance;
        else if (!skillsManager)
            skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }

    /// <summary>Logs missing required references once (Awake only — not per-frame).</summary>
    private void ValidateRefsOnce()
    {
        if (_loggedMissingRefs) return;
        _loggedMissingRefs = true;

        if (skillDatabase == null)
            Debug.LogWarning("[SkillsAbilitiesPageUI] skillDatabase is not assigned.", this);

        if (skillEntryPrefab == null)
            Debug.LogWarning("[SkillsAbilitiesPageUI] skillEntryPrefab is not assigned.", this);

        if (gatheringContent == null && combatContent == null)
            Debug.LogWarning("[SkillsAbilitiesPageUI] Assign at least one of gatheringContent or combatContent.", this);

        if (skillsManager == null)
            Debug.LogWarning("[SkillsAbilitiesPageUI] skillsManager not found (SkillsManager.Instance is null). Progression UI may show defaults.", this);

        if (abilityDatabase == null)
            Debug.LogWarning(
                "[SkillsAbilitiesPageUI] abilityDatabase could not be loaded. Assign it in the Inspector or place AbilityDatabase.asset under a folder named Resources (e.g. Assets/Resources/Databases/). Player builds cannot use Editor-only asset lookup.",
                this);
    }

    private void TrySubscribeSkillsEvents()
    {
        if (_skillsEventsSubscribed)
            return;

        TryUnsubscribeSkillsEvents();
        PreferRuntimeSkillsManager();
        if (!skillsManager) return;

        skillsManager.OnLevelUp += HandleSkillsLevelUp;
        skillsManager.OnSkillLevelDecreased += HandleSkillsLevelUp;
        skillsManager.OnXpGained += HandleSkillsXpGained;
        skillsManager.OnSkillChoiceSelectionChanged += HandleSkillChoiceSelectionChanged;
        skillsManager.OnSkillAbilityRowPickChanged += HandleSkillAbilityRowPickChanged;
        _skillsEventsSubscribed = true;
    }

    private void TryUnsubscribeSkillsEvents()
    {
        if (!skillsManager) return;
        skillsManager.OnLevelUp -= HandleSkillsLevelUp;
        skillsManager.OnSkillLevelDecreased -= HandleSkillsLevelUp;
        skillsManager.OnXpGained -= HandleSkillsXpGained;
        skillsManager.OnSkillChoiceSelectionChanged -= HandleSkillChoiceSelectionChanged;
        skillsManager.OnSkillAbilityRowPickChanged -= HandleSkillAbilityRowPickChanged;
        _skillsEventsSubscribed = false;
    }

    /// <summary>
    /// Refresh skills UI only when a level changes.
    /// This avoids rebuilding the tree every combat XP tick (which hides hover tooltips).
    /// </summary>
    private void HandleSkillsXpGained(SkillType type, int amount, string source)
    {
        if (!isActiveAndEnabled || amount <= 0)
            return;

        RefreshEntryLevelForSkill(type);
    }

    private void HandleSkillsLevelUp(SkillType type, int newLevel)
    {
        // Same frame: SkillsManager already appended to cold-start; take ownership into this instance so tab
        // switches do not re-inject stale cold-start rows for this notification.
        SkillsAbilitiesColdStartLevelUpGlow.ConsumeBecauseLiveUiHandled(type);

        _pendingEntryGlowBySkill.Add(type);
        if (!_pendingTreeGlowLevelsBySkill.TryGetValue(type, out HashSet<int> levels))
        {
            levels = new HashSet<int>();
            _pendingTreeGlowLevelsBySkill[type] = levels;
        }
        levels.Add(newLevel);

        if (_entryBySkillType.TryGetValue(type, out SkillListEntryUI entry) && entry != null)
            entry.ShowUnlockGlow();

        if (_selectedSkill != null && _selectedSkill.skillType == type && centerSkillTreeView != null)
            centerSkillTreeView.HighlightNewUnlocksAtLevel(newLevel);

        ScheduleDeferredProgressRefresh();
    }

    private void HandleSkillChoiceSelectionChanged(SkillType skillType, int sourceLevel, int choiceIndex)
    {
        if (!isActiveAndEnabled || _selectedSkill == null)
            return;
        if (_selectedSkill.skillType != skillType)
            return;

        RefreshRightPanelForCurrentSkill();
    }

    private void HandleSkillAbilityRowPickChanged(SkillType skillType, int requiredLevel, int pickIndex)
    {
        if (!isActiveAndEnabled || _selectedSkill == null)
            return;
        if (_selectedSkill.skillType != skillType)
            return;

        RefreshRightPanelForCurrentSkill();
    }

    private void RefreshRightPanelForCurrentSkill()
    {
        if (_selectedSkill == null)
            return;

        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        ApplyRightPanelSectionVisibility(_selectedSkill, level);
        RefreshMinorUnlocksDisplayText(_selectedSkill, level);
        RefreshAdditionalUnlocksDisplayText(_selectedSkill, level);
        RefreshCapstonePanel(_selectedSkill, level);
        RefreshMajorPassivesPanel(_selectedSkill, level);
        RefreshAbilitiesPanel(_selectedSkill, level);
    }

    /// <summary>One deferred refresh per burst of XP (restarts if another gain queues before the frame runs).</summary>
    private void ScheduleDeferredProgressRefresh()
    {
        if (!isActiveAndEnabled) return;
        if (_deferredRefreshRoutine != null)
            StopCoroutine(_deferredRefreshRoutine);
        _deferredRefreshRoutine = StartCoroutine(DeferredProgressRefreshRoutine());
    }

    private IEnumerator DeferredProgressRefreshRoutine()
    {
        yield return null;
        _deferredRefreshRoutine = null;
        RefreshEntryLevelsAndView();
        // Re-apply unlock pulse after layout/tree refresh so it isn't lost the frame after level-up.
        ReplayPendingGlowForVisibleUi();
    }

    private void RefreshEntryLevelsAndView()
    {
        RefreshAllEntryLevels();
        RefreshView();
        RefreshListSelection();
    }

    private void RefreshAllEntryLevels()
    {
        RefreshEntryLevelsIn(gatheringContent);
        RefreshEntryLevelsIn(combatContent);
    }

    private void RefreshEntryLevelForSkill(SkillType type)
    {
        if (!skillsManager)
            return;

        if (_entryBySkillType.TryGetValue(type, out SkillListEntryUI entry) && entry != null && entry.Definition != null)
        {
            entry.SetLevel(skillsManager.GetLevel(type));
            entry.SetProgress(skillsManager.GetProgress01(type));
            return;
        }

        RefreshEntryLevelsIn(gatheringContent);
        RefreshEntryLevelsIn(combatContent);
    }

    private void RefreshEntryLevelsIn(Transform parent)
    {
        if (!parent || !skillsManager) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            var entry = parent.GetChild(i).GetComponent<SkillListEntryUI>();
            if (entry == null || entry.Definition == null) continue;

            int level = skillsManager.GetLevel(entry.Definition.skillType);
            entry.SetLevel(level);

            float p01 = skillsManager.GetProgress01(entry.Definition.skillType);
            entry.SetProgress(p01);
        }
    }

    public void SelectSkill(SkillDefinition skill)
    {
        if (skill == null) return;

        _selectedSkill = skill;
        _passiveUnlockHighlightKey = null;
        InvalidateMinorUnlocksDisplayCache();
        InvalidateAdditionalUnlocksDisplayCache();
        InvalidateCapstonePanelCache();
        InvalidateMajorPassivesPanelCache();
        EnsureCenterTreeReference();
        if (centerSkillTreeView) centerSkillTreeView.SetSkill(_selectedSkill);
        RefreshView();
        RefreshListSelection();
        RefreshDevCompletionBanner();

        ActionBarUI gatherBar = _cachedActionBar;
        if (gatherBar == null)
            gatherBar = _cachedActionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (gatherBar != null)
        {
            if (ActionBarUI.IsGatheringSkillType(skill.skillType))
                gatherBar.ShowGatheringBarForSkill(skill.skillType, GatheringBarDriveKind.SkillsMenuSelection);
            else
                gatherBar.ExitGatheringBarToCombat();
        }

        if (!_skipSelectionHubNotify)
            SkillsAbilityPageSelectionHub.NotifySelection(skill, this);
    }

    public void ApplySelectionFromOtherPage(SkillDefinition skill)
    {
        if (skill == null)
            return;

        if (_selectedSkill == skill)
        {
            RefreshListSelection();
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

    private void EnsureCenterTreeReference()
    {
        if (!centerSkillTreeView)
        {
            // Prefer a tree under this skills window — scene-wide lookup can bind the wrong SkillTreeViewUI when multiple exist.
            centerSkillTreeView = GetComponentInChildren<SkillTreeViewUI>(true);
            if (!centerSkillTreeView && transform.root != null)
                centerSkillTreeView = transform.root.GetComponentInChildren<SkillTreeViewUI>(true);
        }

        WireMajorPassiveRowIndicatorSprites();
    }

    private void WireMajorPassiveRowIndicatorSprites()
    {
        if (centerSkillTreeView != null &&
            centerSkillTreeView.TryGetNodeIndicatorSprites(out Sprite notSelected, out Sprite enhance))
        {
            MajorPassiveListEntryUI.SetSharedIndicatorSprites(notSelected, enhance);
        }
    }

    private void WireResetTreeButton()
    {
        EnsureCenterTreeReference();
        centerSkillTreeView?.WireResetTreeButton();
    }

    private void OnSkillEntryClicked(SkillDefinition skill)
    {
        // When browsing skills in the menu, temporarily drive the bottom XP strip to this skill.
        // Normal gameplay XP gain (AddXp) will still override ActiveSkill/Source as soon as XP is earned.
        if (!IsActionOccurring() && skillsManager != null && skill != null)
            skillsManager.SetActiveXpDisplay(skill.skillType, "");

        SelectSkill(skill);
    }

    private void RebuildDevCompletionTierLookup()
    {
        _devTierBySkill.Clear();
        if (skillDevCompletionTiers == null)
            return;

        for (int i = 0; i < skillDevCompletionTiers.Count; i++)
        {
            SkillDevCompletionEntry entry = skillDevCompletionTiers[i];
            _devTierBySkill[entry.skill] = entry.tier;
        }
    }

    private SkillDevCompletionTier GetDevCompletionTier(SkillType skillType)
    {
        if (_devTierBySkill.TryGetValue(skillType, out SkillDevCompletionTier tier))
            return tier;
        return SkillDevCompletionTier.VeryIncomplete;
    }

    private void RefreshDevCompletionBanner()
    {
        if (!completionProgressBannerRoot && !completionProgressBannerBackground && !completionProgressText)
            return;

        SetDevCompletionBannerVisible(true);

        if (_selectedSkill == null)
        {
            ApplyDevCompletionTierPresentation(SkillDevCompletionTier.VeryIncomplete);
            return;
        }

        ApplyDevCompletionTierPresentation(GetDevCompletionTier(_selectedSkill.skillType));
    }

    private void SetDevCompletionBannerVisible(bool visible)
    {
        if (completionProgressBannerRoot)
            completionProgressBannerRoot.SetActive(visible);
    }

    private void ApplyDevCompletionTierPresentation(SkillDevCompletionTier tier)
    {
        Color background;
        string message;
        switch (tier)
        {
            case SkillDevCompletionTier.PartiallyComplete:
                background = devTierPartiallyCompleteColor;
                message = "Skill is partially complete";
                break;
            case SkillDevCompletionTier.Complete:
                background = devTierCompleteColor;
                message = "Skill is mostly complete";
                break;
            default:
                background = devTierVeryIncompleteColor;
                message = "Skill is very incomplete";
                break;
        }

        if (completionProgressBannerBackground)
            completionProgressBannerBackground.color = background;

        if (completionProgressText)
            completionProgressText.text = message;
    }

    private bool IsActionOccurring()
    {
        if (!player) return false;

        var a = player.CurrentAction;
        return a == PlayerController.PlayerAction.Mining ||
               a == PlayerController.PlayerAction.Woodcutting ||
               a == PlayerController.PlayerAction.Fishing ||
               a == PlayerController.PlayerAction.Fighting;
    }

    private void SelectFirstSkillIfNeeded()
    {
        if (_selectedSkill != null)
            return;

        if (skillDatabase == null)
            return;

        PreferRuntimeSkillsManager();

        if (SkillsAbilityPageSelectionHub.TryGetLastSkillType(out SkillType savedType))
        {
            SkillDefinition fromSaved = GetSkillByType(savedType);
            if (fromSaved != null)
            {
                _selectedSkill = fromSaved;
                return;
            }
        }

        if (skillsManager != null)
        {
            SkillDefinition fromActiveXp = GetSkillByType(skillsManager.ActiveSkill);
            if (fromActiveXp != null)
            {
                _selectedSkill = fromActiveXp;
                return;
            }
        }

        _selectedSkill = GetSkillByType(SkillType.Melee)
                         ?? GetFirstSkillInCategory(SkillCategory.Combat)
                         ?? GetFirstSkillInCategory(SkillCategory.Gathering);
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
        var list = CollectSkillsForCategory(category);
        return list.Count > 0 ? list[0] : null;
    }

    private void RebuildSkillList()
    {
        if (!skillEntryPrefab || skillDatabase == null)
            return;

        if (!gatheringContent && !combatContent)
            return;

        // Level-ups can happen while this tab is still inactive; SkillsManager buffers those in cold-start storage.
        SkillsAbilitiesColdStartLevelUpGlow.MergeInto(_pendingEntryGlowBySkill, _pendingTreeGlowLevelsBySkill);

        ClearChildren(gatheringContent);
        ClearChildren(combatContent);
        _entryBySkillType.Clear();

        PopulateColumn(gatheringContent, SkillCategory.Gathering);
        PopulateColumn(combatContent, SkillCategory.Combat);
        // Crafting / Utility: no column yet — not listed here.

        RefreshListSelection();
    }

    private void PopulateColumn(Transform parent, SkillCategory category)
    {
        if (!parent) return;

        foreach (var skill in CollectSkillsForCategory(category))
        {
            var entry = Instantiate(skillEntryPrefab, parent);
            int level = skillsManager ? skillsManager.GetLevel(skill.skillType) : 1;
            float progress01 = skillsManager ? skillsManager.GetProgress01(skill.skillType) : 0f;
            bool selected = skill == _selectedSkill;
            entry.Setup(skill, level, progress01, selected, OnSkillEntryClicked, HandleSkillEntryGlowAcknowledgedByHover);

            _entryBySkillType[skill.skillType] = entry;
            if (_pendingEntryGlowBySkill.Contains(skill.skillType))
                entry.ShowUnlockGlow();
        }
    }

    private List<SkillDefinition> CollectSkillsForCategory(SkillCategory category)
    {
        var list = new List<SkillDefinition>();
        if (skillDatabase == null || skillDatabase.Skills == null)
            return list;

        foreach (var s in skillDatabase.Skills)
        {
            if (s == null) continue;
            if (s.category != category) continue;
            list.Add(s);
        }

        if (category == SkillCategory.Gathering)
            list.Sort(CompareGatheringSkillRowOrder);
        else
            list.Sort(CompareSkillOrder);
        return list;
    }

    /// <summary>
    /// Gathering column order: Woodcutting, Mining, Fishing (matches W/M/F action bar), then any other gathering skills by <see cref="SkillDefinition.listSortOrder"/>.
    /// </summary>
    private static int CompareGatheringSkillRowOrder(SkillDefinition a, SkillDefinition b)
    {
        if (a == null || b == null)
            return 0;

        int rowRank(SkillType t)
        {
            switch (t)
            {
                case SkillType.Woodcutting:
                    return 0;
                case SkillType.Mining:
                    return 1;
                case SkillType.Fishing:
                    return 2;
                default:
                    return 100;
            }
        }

        int ra = rowRank(a.skillType);
        int rb = rowRank(b.skillType);
        int byRow = ra.CompareTo(rb);
        if (byRow != 0)
            return byRow;

        return CompareSkillOrder(a, b);
    }

    private static int CompareSkillOrder(SkillDefinition a, SkillDefinition b)
    {
        int byOrder = a.listSortOrder.CompareTo(b.listSortOrder);
        if (byOrder != 0) return byOrder;
        return a.skillType.CompareTo(b.skillType);
    }

    private static void ClearChildren(Transform parent)
    {
        UiDestroyUtility.DestroyChildren(parent);
    }

    private void RefreshListSelection()
    {
        ApplySelectionHighlight(gatheringContent);
        ApplySelectionHighlight(combatContent);
    }

    private void ApplySelectionHighlight(Transform parent)
    {
        if (!parent) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            var entry = parent.GetChild(i).GetComponent<SkillListEntryUI>();
            if (entry != null)
                entry.SetSelected(entry.Definition == _selectedSkill);
        }
    }

    private void RefreshView()
    {
        PreferRuntimeSkillsManager();

        if (_selectedSkill == null)
        {
            _passiveUnlockHighlightKey = null;
            InvalidateMinorUnlocksDisplayCache();
            InvalidateAdditionalUnlocksDisplayCache();
            InvalidateCapstonePanelCache();
            InvalidateMajorPassivesPanelCache();
            if (centerTitleText) centerTitleText.text = "No Skill Selected";
            if (centerSkillTreePlaceholderText) centerSkillTreePlaceholderText.text = "";
            if (centerSkillTreeView) centerSkillTreeView.SetSkill(null);
            if (minorPassivesSection.content) minorPassivesSection.content.text = "";
            if (additionalUnlocksSection.content) additionalUnlocksSection.content.text = "";
            RightPanelMajorPassiveListUtil.ClearRows(capstoneSection.content);
            RightPanelMajorPassiveListUtil.ClearRows(majorPassivesSection.listContent);
            SetMajorPassivesEmptyStateVisible(showEmpty: true);
            if (abilitiesSection.summaryText) abilitiesSection.summaryText.text = "";
            ApplyRightPanelSectionVisibility(null, 0);
            return;
        }

        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        ApplyRightPanelSectionVisibility(_selectedSkill, level);
        string displayName = SkillsAbilityPresentationResolver.ResolveSkillDisplayName(_selectedSkill);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = _selectedSkill.skillType.ToString();

        if (centerTitleText)
            centerTitleText.text = $"{displayName} (lv {level})";

        if (centerSkillTreePlaceholderText)
            centerSkillTreePlaceholderText.text = "";

        if (centerSkillTreeView)
        {
            if (!centerSkillTreeView.RefreshProgressIfSameSkill(_selectedSkill, level))
                centerSkillTreeView.SetSkill(_selectedSkill);

            if (_pendingTreeGlowLevelsBySkill.TryGetValue(_selectedSkill.skillType, out HashSet<int> levels) && levels != null)
            {
                centerSkillTreeView.SetPendingUnlockGlowLevels(levels);
                foreach (int lvl in levels)
                    centerSkillTreeView.HighlightNewUnlocksAtLevel(lvl);
            }
            else
            {
                centerSkillTreeView.SetPendingUnlockGlowLevels(null);
            }
        }

        RefreshMinorUnlocksDisplayText(_selectedSkill, level);
        RefreshAdditionalUnlocksDisplayText(_selectedSkill, level);
        RefreshCapstonePanel(_selectedSkill, level);
        RefreshMajorPassivesPanel(_selectedSkill, level);
        RefreshAbilitiesPanel(_selectedSkill, level);
        RefreshDevCompletionBanner();
    }

    private void HandleSkillEntryGlowAcknowledgedByHover(SkillDefinition def)
    {
        if (def == null)
            return;
        _pendingEntryGlowBySkill.Remove(def.skillType);
    }

    private void HookTreeGlowAcknowledge()
    {
        EnsureCenterTreeReference();
        if (centerSkillTreeView == null)
            return;
        centerSkillTreeView.UnlockGlowAcknowledgedByHover -= HandleTreeGlowAcknowledgedByHover;
        centerSkillTreeView.UnlockGlowAcknowledgedByHover += HandleTreeGlowAcknowledgedByHover;
        centerSkillTreeView.PassiveUnlockLineHighlightChanged -= HandlePassiveUnlockLineHighlightChanged;
        centerSkillTreeView.PassiveUnlockLineHighlightChanged += HandlePassiveUnlockLineHighlightChanged;
    }

    private void UnhookTreeGlowAcknowledge()
    {
        if (centerSkillTreeView == null)
            return;
        centerSkillTreeView.UnlockGlowAcknowledgedByHover -= HandleTreeGlowAcknowledgedByHover;
        centerSkillTreeView.PassiveUnlockLineHighlightChanged -= HandlePassiveUnlockLineHighlightChanged;
    }

    private void HandleTreeGlowAcknowledgedByHover(int unlockLevel)
    {
        if (_selectedSkill == null)
            return;

        if (_pendingTreeGlowLevelsBySkill.TryGetValue(_selectedSkill.skillType, out HashSet<int> levels) && levels != null)
            levels.Remove(unlockLevel);
    }

    private void HandlePassiveUnlockLineHighlightChanged(string highlightKey)
    {
        _passiveUnlockHighlightKey = highlightKey;
        if (!isActiveAndEnabled || _selectedSkill == null)
            return;

        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        RefreshMinorUnlocksDisplayText(_selectedSkill, level);
        RefreshAdditionalUnlocksDisplayText(_selectedSkill, level);
        RefreshCapstonePanel(_selectedSkill, level);
        RefreshMajorPassivesPanel(_selectedSkill, level);
    }

    private void InvalidateMinorUnlocksDisplayCache()
    {
        _cachedMinorUnlocksSkill = null;
        _cachedMinorUnlocksLevel = -1;
        _cachedMinorUnlocksHighlightKey = null;
        _cachedMinorUnlocksDisplayText = null;
    }

    private void InvalidateAdditionalUnlocksDisplayCache()
    {
        _cachedAdditionalUnlocksSkill = null;
        _cachedAdditionalUnlocksLevel = -1;
        _cachedAdditionalUnlocksDisplayText = null;
    }

    private void InvalidateCapstonePanelCache()
    {
        _cachedCapstoneSkill = null;
        _cachedCapstoneLevel = -1;
        _cachedCapstoneFingerprint = int.MinValue;
    }

    private void InvalidateMajorPassivesPanelCache()
    {
        _cachedMajorPassivesSkill = null;
        _cachedMajorPassivesLevel = -1;
        _cachedMajorPassivesFingerprint = int.MinValue;
    }

    /// <summary>Minor passive stat summary; cache until skill, level, or tree highlight changes.</summary>
    private void RefreshMinorUnlocksDisplayText(SkillDefinition skill, int level)
    {
        TMP_Text text = minorPassivesSection.content;
        if (!text)
            return;

        string highlightKey = _passiveUnlockHighlightKey;
        if (skill == _cachedMinorUnlocksSkill &&
            level == _cachedMinorUnlocksLevel &&
            highlightKey == _cachedMinorUnlocksHighlightKey &&
            _cachedMinorUnlocksDisplayText != null)
        {
            text.text = _cachedMinorUnlocksDisplayText;
            return;
        }

        _cachedMinorUnlocksDisplayText = BuildMinorPassivesDisplay(skill, level);
        _cachedMinorUnlocksSkill = skill;
        _cachedMinorUnlocksLevel = level;
        _cachedMinorUnlocksHighlightKey = highlightKey;
        text.text = _cachedMinorUnlocksDisplayText;
    }

    private void RefreshAdditionalUnlocksDisplayText(SkillDefinition skill, int level)
    {
        TMP_Text text = additionalUnlocksSection.content;
        if (!text)
            return;

        if (skill == _cachedAdditionalUnlocksSkill &&
            level == _cachedAdditionalUnlocksLevel &&
            _cachedAdditionalUnlocksDisplayText != null)
        {
            text.text = _cachedAdditionalUnlocksDisplayText;
            return;
        }

        _cachedAdditionalUnlocksDisplayText = BuildAdditionalUnlocksDisplay(skill, level);
        _cachedAdditionalUnlocksSkill = skill;
        _cachedAdditionalUnlocksLevel = level;
        text.text = _cachedAdditionalUnlocksDisplayText;
    }

    private void RefreshCapstonePanel(SkillDefinition skill, int level)
    {
        if (skill == null || level < CharacterStats.SkillPostCapThresholdLevel)
        {
            RightPanelMajorPassiveListUtil.ClearRows(capstoneSection.content);
            InvalidateCapstonePanelCache();
            return;
        }

        Transform listParent = capstoneSection.content;
        if (!listParent)
            return;

        int fingerprint = ComputeCapstonePanelFingerprint(skill, level);
        if (skill == _cachedCapstoneSkill &&
            level == _cachedCapstoneLevel &&
            fingerprint == _cachedCapstoneFingerprint)
            return;

        RightPanelMajorPassiveListUtil.ClearRows(listParent);
        RightPanelMajorPassiveListUtil.EnsureListSpacing(listParent, abilityRowSpacing);

        SkillUnlockDefinition capstone = FindCapstonePassiveUnlockForSkill(skill, level);
        if (capstone == null)
        {
            CommitCapstonePanelCache(skill, level, fingerprint);
            return;
        }

        PreferRuntimeSkillsManager();
        SharedTooltipUI tooltip = _cachedSharedTooltip ??= FindBestSharedTooltip();
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform panelRect = listParent as RectTransform;

        MajorPassiveListEntryUI row = CreateMajorPassiveRow(listParent);
        if (row == null)
        {
            CommitCapstonePanelCache(skill, level, fingerprint);
            return;
        }

        row.SetTooltipDocking(panelRect, FlipInsideBounds.PreferredSide.Right);
        int scrollLevel = Mathf.Max(1, capstone.requiredLevel);
        row.Bind(skill, capstone, capstoneStyle: true, tooltip, canvas, () =>
        {
            if (centerSkillTreeView != null)
                centerSkillTreeView.ScrollAbilityTierRowIntoView(scrollLevel);
        });

        CommitCapstonePanelCache(skill, level, fingerprint);
    }

    private void RefreshMajorPassivesPanel(SkillDefinition skill, int level)
    {
        WireMajorPassiveRowIndicatorSprites();
        int fingerprint = ComputeMajorPassivesPanelFingerprint(skill, level);
        if (skill == _cachedMajorPassivesSkill &&
            level == _cachedMajorPassivesLevel &&
            fingerprint == _cachedMajorPassivesFingerprint)
            return;

        if (skill == null)
        {
            RightPanelMajorPassiveListUtil.ClearRows(majorPassivesSection.listContent);
            SetMajorPassivesEmptyStateVisible(showEmpty: true);
            CommitMajorPassivesPanelCache(skill, level, fingerprint);
            return;
        }

        CollectMajorPassiveTierRows(skill, level, skillsManager, out List<int> tierLevels, out List<SkillUnlockDefinition> committed, out List<bool> availablePlaceholder);
        bool hasCommittedMajorPassive = HasAnyCommittedMajorPassive(tierLevels, committed, availablePlaceholder);

        SetMajorPassivesEmptyStateVisible(showEmpty: !hasCommittedMajorPassive);

        Transform listParent = majorPassivesSection.listContent;
        if (!listParent)
        {
            CommitMajorPassivesPanelCache(skill, level, fingerprint);
            return;
        }

        RightPanelMajorPassiveListUtil.ClearRows(listParent);

        if (!hasCommittedMajorPassive)
        {
            CommitMajorPassivesPanelCache(skill, level, fingerprint);
            return;
        }

        RightPanelMajorPassiveListUtil.EnsureListSpacing(listParent, abilityRowSpacing);

        PreferRuntimeSkillsManager();
        SharedTooltipUI tooltip = _cachedSharedTooltip ??= FindBestSharedTooltip();
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform panelRect = listParent as RectTransform;

        for (int i = 0; i < tierLevels.Count; i++)
        {
            int rowLevel = tierLevels[i];
            MajorPassiveListEntryUI row = CreateMajorPassiveRow(listParent);
            row.SetTooltipDocking(panelRect, FlipInsideBounds.PreferredSide.Right);

            if (availablePlaceholder[i])
            {
                row.BindAvailableMajorPassiveTier(rowLevel, tooltip, canvas, () =>
                {
                    if (centerSkillTreeView != null)
                        centerSkillTreeView.ScrollAbilityTierRowIntoView(rowLevel);
                });
                continue;
            }

            SkillUnlockDefinition unlock = committed[i];
            if (unlock == null)
                continue;

            row.Bind(skill, unlock, capstoneStyle: false, tooltip, canvas, () =>
            {
                if (centerSkillTreeView != null)
                    centerSkillTreeView.ScrollAbilityTierRowIntoView(rowLevel);
            });

            PreferRuntimeSkillsManager();
            if (SkillTreeMajorPassiveRowIndicators.TryGet(skill, unlock, skillsManager,
                    out bool showNotSelected, out bool showEnhance))
            {
                row.SetTreeStatusIndicators(
                    showNotSelected && !row.ShowsNoEnhancementPlaceholder,
                    showEnhance,
                    () =>
                {
                    if (centerSkillTreeView != null)
                        centerSkillTreeView.OpenEnhancementBranchForTier(rowLevel);
                });
            }
        }

        CommitMajorPassivesPanelCache(skill, level, fingerprint);
    }

    private void SetMajorPassivesEmptyStateVisible(bool showEmpty)
    {
        if (majorPassivesSection.emptyText)
            majorPassivesSection.emptyText.SetActive(showEmpty);

        if (majorPassivesSection.listContent)
            majorPassivesSection.listContent.gameObject.SetActive(!showEmpty);
    }

    private static bool HasAnyCommittedMajorPassive(
        List<int> tierLevels,
        List<SkillUnlockDefinition> committed,
        List<bool> availablePlaceholder)
    {
        for (int i = 0; i < tierLevels.Count; i++)
        {
            if (availablePlaceholder[i])
                continue;
            if (committed[i] != null)
                return true;
        }

        return false;
    }

    private void ReplayPendingGlowForVisibleUi()
    {
        foreach (var kv in _entryBySkillType)
        {
            if (kv.Value == null)
                continue;
            if (_pendingEntryGlowBySkill.Contains(kv.Key))
                kv.Value.ShowUnlockGlow();
        }

        if (_selectedSkill == null || centerSkillTreeView == null)
            return;
        if (!_pendingTreeGlowLevelsBySkill.TryGetValue(_selectedSkill.skillType, out HashSet<int> levels) || levels == null)
            return;

        centerSkillTreeView.SetPendingUnlockGlowLevels(levels);
        foreach (int lvl in levels)
            centerSkillTreeView.HighlightNewUnlocksAtLevel(lvl);
    }

    private void InvalidateAbilitiesPanelCache()
    {
        _cachedAbilitiesPanelSkill = null;
        _cachedAbilitiesPanelLevel = -1;
        _cachedAbilitiesPanelFingerprint = int.MinValue;
    }

    private void CommitAbilitiesPanelCache(SkillDefinition skill, int level, int fingerprint)
    {
        _cachedAbilitiesPanelSkill = skill;
        _cachedAbilitiesPanelLevel = level;
        _cachedAbilitiesPanelFingerprint = fingerprint;
    }

    private int ComputeAbilitiesPanelFingerprint(SkillDefinition skill, int level)
    {
        if (skill == null || skillsManager == null)
            return 0;

        unchecked
        {
            int h = ((int)skill.skillType * 397) ^ level;
            List<int> tiers = SkillAbilityCommitRules.CollectSortedAbilityTierLevels(skill);
            for (int i = 0; i < tiers.Count; i++)
            {
                int rowLevel = tiers[i];
                if (level < rowLevel)
                    continue;
                if (SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(skill, rowLevel).Count == 0)
                    continue;
                h = (h * 31) ^ (rowLevel * 17 + skillsManager.GetSkillAbilityRowPick(skill.skillType, rowLevel, -1));
            }

            return h;
        }
    }

    private void RefreshAbilitiesPanel(SkillDefinition skill, int level)
    {
        TMP_Text summaryText = abilitiesSection.summaryText;

        if (skill == null)
        {
            InvalidateAbilitiesPanelCache();
            if (summaryText) summaryText.text = "";
            abilitiesSection.ClearAbilityRows();
            return;
        }

        int fingerprint = ComputeAbilitiesPanelFingerprint(skill, level);
        if (skill == _cachedAbilitiesPanelSkill &&
            level == _cachedAbilitiesPanelLevel &&
            fingerprint == _cachedAbilitiesPanelFingerprint)
            return;

        Transform listParent = abilitiesSection.content;
        if (!listParent)
            listParent = EnsureRuntimeAbilitiesListParent();

        if (!listParent)
        {
            if (summaryText)
                summaryText.text = "Abilities\n(placeholder — drag/drop not implemented yet)";
            CommitAbilitiesPanelCache(skill, level, fingerprint);
            return;
        }

        PreferRuntimeSkillsManager();

        var abilityTierLevels = SkillAbilityCommitRules.CollectSortedAbilityTierLevels(skill);
        bool hasStarterAttack = CombatStarterAttackAbility.TryGetCombatStarterAttackForSkill(skill, out AbilityDefinition starterAttack)
            && starterAttack != null
            && level >= Mathf.Max(1, starterAttack.unlockLevel)
            && (skillsManager == null
                || CombatStarterAttackAbility.IsCombatStarterAttackUnlockedForGameplay(skill, starterAttack, skillsManager));

        if (abilityTierLevels.Count == 0 && !hasStarterAttack)
        {
            if (summaryText)
                summaryText.text = "No abilities yet";
            abilitiesSection.ClearAbilityRows();
            CommitAbilitiesPanelCache(skill, level, fingerprint);
            return;
        }

        int unlockedTiersCount = CountUnlockedAbilityTiers(skill, level);
        int selectedInTreeCount = CountTotalAbilitiesSelectedInTree(skill, level);
        if (hasStarterAttack)
        {
            unlockedTiersCount += 1;
            selectedInTreeCount += 1;
        }

        if (summaryText)
            summaryText.text = $"Abilities unlocked: {unlockedTiersCount}\nAbilities selected: {selectedInTreeCount}";

        abilitiesSection.ClearAbilityRows();

        SharedTooltipUI tooltip = _cachedSharedTooltip ??= FindBestSharedTooltip();
        var canvas = GetComponentInParent<Canvas>();
        RectTransform abilityPanelRect = summaryText ? summaryText.transform.parent as RectTransform : null;

        if (hasStarterAttack)
        {
            AbilityEntryUI starterRow = CreateAbilityRow(listParent);
            starterRow.SetTooltipDocking(abilityPanelRect, FlipInsideBounds.PreferredSide.Right);
            starterRow.SetRowLevelContext(Mathf.Max(1, starterAttack.unlockLevel), HandleAbilityRowRightClick);
            starterRow.Bind(starterAttack, unlocked: true, tooltip, canvas, null);
            starterRow.SetDoubleClickAssignHandler(HandleAbilityDoubleClickAssignToActionBar);
        }

        for (int i = 0; i < abilityTierLevels.Count; i++)
        {
            int rowLevel = abilityTierLevels[i];
            if (level < rowLevel)
                continue;

            var siblings = SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(skill, rowLevel);
            if (siblings == null || siblings.Count == 0)
                continue;

            int pick = skillsManager != null ? skillsManager.GetSkillAbilityRowPick(skill.skillType, rowLevel, -1) : -1;

            AbilityEntryUI row = CreateAbilityRow(listParent);
            row.SetTooltipDocking(abilityPanelRect, FlipInsideBounds.PreferredSide.Right);
            row.SetRowLevelContext(rowLevel, HandleAbilityRowRightClick);

            if (pick < 0)
            {
                int scrollLevel = rowLevel;
                row.BindAvailableAbilityTier(rowLevel, tooltip, canvas, () =>
                {
                    if (centerSkillTreeView != null)
                        centerSkillTreeView.ScrollAbilityTierRowIntoView(scrollLevel);
                });
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

            row.Bind(def, unlocked: true, tooltip, canvas, () =>
            {
                if (centerSkillTreeView != null)
                    centerSkillTreeView.ScrollAbilityTierRowIntoView(Mathf.Max(1, def.unlockLevel));
            });
            row.SetDoubleClickAssignHandler(HandleAbilityDoubleClickAssignToActionBar);
            row.SetNotSelectedPrompt(
                SkillTimelineRowSelectionRules.HasPendingAbilityEnhancementChoice(skillsManager, skill, def));
        }

        CommitAbilitiesPanelCache(skill, level, fingerprint);

        if (listParent is RectTransform abilitiesListRt)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(abilitiesListRt);
        }
    }

    private void HandleAbilityRowRightClick(int rowLevel)
    {
        if (centerSkillTreeView != null)
            centerSkillTreeView.HandleAbilityListRowRightClick(rowLevel);

        if (_selectedSkill == null)
            return;

        InvalidateAbilitiesPanelCache();
        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        RefreshAbilitiesPanel(_selectedSkill, level);
    }

    private void WireAutoAssignAbilitiesButton()
    {
        if (!autoAssignAbilitiesButton)
        {
            Transform found = transform.Find("AutoAssignAbilitiesButton");
            if (!found)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null &&
                        buttons[i].name.IndexOf("AutoAssign", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        autoAssignAbilitiesButton = buttons[i];
                        break;
                    }
                }
            }
            else
            {
                autoAssignAbilitiesButton = found.GetComponent<Button>();
            }
        }

        if (!autoAssignAbilitiesButton)
            return;

        autoAssignAbilitiesButton.onClick.RemoveListener(HandleAutoAssignAbilitiesToBarClicked);
        autoAssignAbilitiesButton.onClick.AddListener(HandleAutoAssignAbilitiesToBarClicked);
    }

    private void HandleAutoAssignAbilitiesToBarClicked()
    {
        PreferRuntimeSkillsManager();
        if (_selectedSkill == null)
        {
            if (player)
                player.ShowPopup("Select a skill first.");
            return;
        }

        if (skillsManager == null)
        {
            if (player)
                player.ShowPopup("Skills not loaded.");
            return;
        }

        int level = skillsManager.GetLevel(_selectedSkill.skillType);
        List<AbilityDefinition> abilities = SkillAbilityCommitRules.CollectUnlockedAbilitiesInPanelOrder(
            _selectedSkill, level, skillsManager);
        if (abilities.Count == 0)
        {
            if (player)
                player.ShowPopup("No unlocked abilities to assign.");
            return;
        }

        ActionBarUI bar = _cachedActionBar;
        if (bar == null)
            bar = _cachedActionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (bar == null)
        {
            if (player)
                player.ShowPopup("No action bar found.");
            return;
        }

        if (ActionBarUI.IsGatheringSkillType(_selectedSkill.skillType))
            bar.ShowGatheringBarForSkill(_selectedSkill.skillType, GatheringBarDriveKind.SkillsMenuSelection);
        else
            bar.ExitGatheringBarToCombat();

        int assigned = bar.ReplaceLoadoutAbilitiesInOrder(abilities);
        if (assigned <= 0 && player)
            player.ShowPopup("Could not assign abilities to the action bar.");
    }

    private void HandleAbilityDoubleClickAssignToActionBar(AbilityDefinition def)
    {
        if (def == null)
            return;

        PreferRuntimeSkillsManager();
        ActionBarUI bar = _cachedActionBar;
        if (bar == null)
            bar = _cachedActionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (bar == null)
        {
            if (player)
                player.ShowPopup("No action bar found.");
            return;
        }

        if (ActionBarUI.IsGatheringSkillType(def.sourceSkill))
            bar.ShowGatheringBarForSkill(def.sourceSkill, GatheringBarDriveKind.SkillsMenuSelection);

        if (!bar.TryAssignAbilityToFirstEmptySlot(def))
        {
            if (player)
                player.ShowPopup("No empty ability slot on the action bar.");
        }
    }

    private Transform EnsureRuntimeAbilitiesListParent()
    {
        if (abilitiesSection.summaryText == null)
            return null;

        Transform parent = abilitiesSection.summaryText.transform.parent;
        if (!parent)
            return null;

        Transform existing = parent.Find("AbilitiesList");
        if (existing)
        {
            ConfigureAbilitiesListLayout(existing as RectTransform);
            return existing;
        }

        var go = new GameObject("AbilitiesList", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        ConfigureAbilitiesListLayout(rt);

        return go.transform;
    }

    private int CountUnlockedAbilityTiers(SkillDefinition skill, int playerLevel)
    {
        if (skill == null)
            return 0;

        var rows = SkillAbilityCommitRules.CollectSortedAbilityTierLevels(skill);
        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (playerLevel >= rows[i])
                count++;
        }

        return count;
    }

    private int CountTotalAbilitiesSelectedInTree(SkillDefinition skill, int level)
    {
        if (skill == null || skillsManager == null)
            return 0;

        var rowLevels = SkillAbilityCommitRules.CollectSortedAbilityTierLevels(skill);
        int selectedRows = 0;
        for (int i = 0; i < rowLevels.Count; i++)
        {
            int rowLevel = rowLevels[i];
            if (level < rowLevel)
                continue;
            if (skillsManager.GetSkillAbilityRowPick(skill.skillType, rowLevel, -1) >= 0)
                selectedRows++;
        }

        return selectedRows;
    }

    private AbilityEntryUI CreateAbilityRow(Transform parent)
    {
        if (abilityEntryPrefab)
            return Instantiate(abilityEntryPrefab, parent);

        // Build a minimal row at runtime (Icon + Name).
        var go = new GameObject("AbilityEntry", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rowBg = go.AddComponent<UnityEngine.UI.Image>();
        rowBg.color = new Color(1f, 1f, 1f, 0.02f); // transparent but raycastable

        var rowBtn = go.AddComponent<UnityEngine.UI.Button>();
        rowBtn.targetGraphic = rowBg;
        rowBtn.transition = UnityEngine.UI.Selectable.Transition.None;

        var h = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.spacing = 8f;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = true;
        h.childControlHeight = true;
        h.childControlWidth = false;

        var rowLayout = go.AddComponent<LayoutElement>();
        rowLayout.minHeight = 44f;
        rowLayout.preferredHeight = 50f;
        rowLayout.flexibleWidth = 1f;

        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        iconGO.transform.SetParent(go.transform, false);
        var icon = iconGO.GetComponent<UnityEngine.UI.Image>();
        icon.raycastTarget = false;
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(32f, 32f);

        var nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(go.transform, false);
        var nameText = nameGO.GetComponent<TextMeshProUGUI>();
        nameText.fontSize = 18;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.raycastTarget = false;

        var reqGO = new GameObject("Req", typeof(RectTransform), typeof(TextMeshProUGUI));
        reqGO.transform.SetParent(go.transform, false);
        var reqText = reqGO.GetComponent<TextMeshProUGUI>();
        reqText.fontSize = 16;
        reqText.alignment = TextAlignmentOptions.MidlineRight;
        reqText.raycastTarget = false;

        var entry = go.AddComponent<AbilityEntryUI>();

        // Wire serialized fields via reflection-free GetComponent by name:
        // AbilityEntryUI uses [SerializeField] fields; set via inspector normally, so here we rely on Unity's
        // default to keep them null-safe. We'll manually assign through local components using SendMessage.
        // To avoid SendMessage, just set them via public method by adding a small internal hook.
        // Minimal: rely on AbilityEntryUI null checks for icon/name/req and just use its drag logic.
        // But we DO want the icon and name to render, so assign by setting the components on the created object:
        var cg = go.AddComponent<UnityEngine.CanvasGroup>();

        // Use UnityEngine.Object.FindObjectOfType is slow; we're already here; just set private fields via helper.
        // We'll add a tiny internal setup method by using components added on same GO.
        // (AbilityEntryUI's Awake isn't used; fields can be assigned with GetComponents in Bind if null.)

        // Hack-free approach: add same components as serialized references exist on this GO and children,
        // then AbilityEntryUI will still work even if fields are null (it won't show icon/name).
        // Instead, we'll set them via a small internal setter added below (see file).
        entry.SendMessage("EditorAutoWire", new object[] { icon, nameText, reqText, cg }, SendMessageOptions.DontRequireReceiver);

        return entry;
    }

    private SharedTooltipUI FindBestSharedTooltip()
    {
        SharedTooltipUI[] allTooltips =
            FindObjectsByType<SharedTooltipUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var t in allTooltips)
        {
            if (t != null && t.name == "SharedToolTipInfoPanel")
                return t;
        }

        foreach (var t in allTooltips)
        {
            if (t != null && t.name != "HUDToolInfoPanel")
                return t;
        }

        return allTooltips != null && allTooltips.Length > 0 ? allTooltips[0] : null;
    }

    private void EnsureRightPanelLayoutConfigured()
    {
        if (!autoFixRightPanelLayout)
            return;

        if (abilitiesSection.content is RectTransform rt)
            ConfigureAbilitiesListLayout(rt);
        else if (abilitiesSection.content == null)
            abilitiesSection.content = EnsureRuntimeAbilitiesListParent();
    }

    private void ConfigureAbilitiesListLayout(RectTransform rt)
    {
        if (rt == null)
            return;

        // Do NOT force anchors/offsets here.
        // This list typically lives under a right-panel layout that also contains header text
        // (e.g. "Abilities unlocked: 1/1"). Stretching to full parent makes the list overlap the header.
        // Let the prefab/scene control RectTransform placement; we only ensure layout components exist.

        VerticalLayoutGroup v = rt.GetComponent<VerticalLayoutGroup>();
        if (v == null)
            v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperLeft;
        v.spacing = abilityRowSpacing;
        v.padding = abilityListPadding;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;
        v.childControlHeight = true;
        v.childControlWidth = true;

        ContentSizeFitter fitter = rt.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement listLayout = rt.GetComponent<LayoutElement>();
        if (listLayout == null)
            listLayout = rt.gameObject.AddComponent<LayoutElement>();
        listLayout.flexibleHeight = 1f;
        listLayout.flexibleWidth = 1f;
    }

    public static string BuildMinorPassivesDisplay(SkillDefinition skill, int currentLevel)
    {
        if (skill == null || skill.unlocks == null || skill.unlocks.Count == 0)
            return "No unlocks yet.";

        float minMeleeDamage = 0f;
        float maxMeleeDamage = 0f;
        float meleeAttackSpeed = 0f;
        float meleeDamage = 0f;
        float meleeCritChance = 0f;
        float meleeCritDamage = 0f;
        float meleeMoveSpeed = 0f;
        float bleedChance = 0f;
        float bleedDamage = 0f;
        float poisonChance = 0f;
        float poisonDuration = 0f;
        float ailmentDamage = 0f;
        float shockChance = 0f;
        float burnChance = 0f;
        float lifeSteal = 0f;
        float vsBleeding = 0f;
        float vsPoisoned = 0f;
        float vsShocked = 0f;
        float vsBurning = 0f;
        float vsLowHp = 0f;
        float rangedDamage = 0f;
        float gatherSpeedFlat = 0f;
        float gatherGrit = 0f;
        float gatherEnergyEfficiency = 0f;
        float gatherBonusItemChance = 0f;
        float gatherExtraLogChance = 0f;
        float gatherYieldPercent = 0f;
        float woodcuttingGritProcRestoreStaminaFraction = 0f;
        float woodcuttingBonusXpChance = 0f;
        float woodcuttingNoStaminaSwingChance = 0f;
        int woodcuttingFrenzyStacks = 0;
        int woodcuttingForestFlowStacks = 0;
        float woodcuttingChanceNotToCountTowardTreeDepletion = 0f;
        int fishingGritRestoreStacks = 0;
        float fishingDoubleXpChance = 0f;
        float fishingNoStaminaSwingChance = 0f;
        int fishingFrenzyStacks = 0;
        int fishingCalmWatersStacks = 0;
        float fishingBaitConservationChance = 0f;
        float fishingAutoCookChance = 0f;
        int fishingTreasureMinorStacks = 0;
        float enduranceArmor = 0f;
        float enduranceMagicResist = 0f;
        float enduranceHp = 0f;
        float enduranceHpRegen = 0f;
        float rangedAttackSpeed = 0f;
        float rangedCritChance = 0f;
        float rangedMoveSpeed = 0f;
        float magicDamage = 0f;
        float magicAttackSpeed = 0f;
        float magicCritChance = 0f;
        float magicCritDamage = 0f;

        foreach (var unlock in skill.unlocks)
        {
            if (unlock == null) continue;

            if (currentLevel < unlock.requiredLevel)
                continue; // Hide future unlocks in the right-side unlocks content.
            if (unlock.unlockType != SkillUnlockType.MinorPassive)
                continue;

            if (skill.skillType == SkillType.Melee)
            {
                switch (unlock.meleeMinorStatOption)
                {
                    case MeleeMinorNodeStatOption.MinMeleeDamageFlat2: minMeleeDamage += 2f; break;
                    case MeleeMinorNodeStatOption.MaxMeleeDamageFlat2: maxMeleeDamage += 2f; break;
                    case MeleeMinorNodeStatOption.MeleeAttackSpeedPercent3: meleeAttackSpeed += 0.03f; break;
                    case MeleeMinorNodeStatOption.MeleeDamagePercent3: meleeDamage += 0.03f; break;
                    case MeleeMinorNodeStatOption.MeleeCritChancePercent2: meleeCritChance += 0.02f; break;
                    case MeleeMinorNodeStatOption.MeleeDamageVsLowHpPercent10: vsLowHp += 0.10f; break;
                    case MeleeMinorNodeStatOption.MeleeBleedChancePercent5: bleedChance += 0.05f; break;
                    case MeleeMinorNodeStatOption.MeleeBleedDamagePercent10: bleedDamage += 0.10f; break;
                    case MeleeMinorNodeStatOption.MeleeMoveSpeedPercent2: meleeMoveSpeed += 0.02f; break;
                    case MeleeMinorNodeStatOption.MeleeMoveSpeedPercent5: meleeMoveSpeed += 0.05f; break;
                    case MeleeMinorNodeStatOption.MeleeCritDamagePercent8: meleeCritDamage += 0.08f; break;
                    case MeleeMinorNodeStatOption.MeleePoisonChancePercent5: poisonChance += 0.05f; break;
                    case MeleeMinorNodeStatOption.MeleePoisonDurationPercent10: poisonDuration += 0.10f; break;
                    case MeleeMinorNodeStatOption.MeleeAilmentDamagePercent4: ailmentDamage += 0.04f; break;
                    case MeleeMinorNodeStatOption.MeleeDamageVsPoisonedPercent10: vsPoisoned += 0.10f; break;
                    case MeleeMinorNodeStatOption.MeleeShockChancePercent5: shockChance += 0.05f; break;
                    case MeleeMinorNodeStatOption.MeleeDamageVsShockedPercent10: vsShocked += 0.10f; break;
                    case MeleeMinorNodeStatOption.MeleeBurnChancePercent5: burnChance += 0.05f; break;
                    case MeleeMinorNodeStatOption.MeleeDamageVsBurningPercent10: vsBurning += 0.10f; break;
                    case MeleeMinorNodeStatOption.MeleeLifeStealPercent1: lifeSteal += 0.01f; break;
                    case MeleeMinorNodeStatOption.MeleeLifeStealPercent2: lifeSteal += 0.02f; break;
                    case MeleeMinorNodeStatOption.MeleeDamageVsBleedingPercent10: vsBleeding += 0.10f; break;
                }
            }
            else if (skill.skillType == SkillType.Ranged)
            {
                switch (unlock.rangedMinorStatOption)
                {
                    case RangedMinorNodeStatOption.RangedDamagePercent3:
                        rangedDamage += 0.03f;
                        break;
                    case RangedMinorNodeStatOption.RangedAttackSpeedPercent3:
                        rangedAttackSpeed += 0.03f;
                        break;
                    case RangedMinorNodeStatOption.RangedCritChancePercent2:
                        rangedCritChance += 0.02f;
                        break;
                    case RangedMinorNodeStatOption.RangedMoveSpeedPercent5:
                        rangedMoveSpeed += 0.05f;
                        break;
                }
            }

            if (skill.skillType == SkillType.Woodcutting)
            {
                switch (unlock.woodcuttingMinorStatOption)
                {
                    case WoodcuttingMinorNodeStatOption.WoodcuttingGatherSpeedFlat01: gatherSpeedFlat += 0.1f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingGritPercent2: gatherGrit += 0.02f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingEnergyEfficiencyPercent2: gatherEnergyEfficiency += 0.02f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingBonusItemChancePercent2: gatherBonusItemChance += 0.02f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingSpeedPercent2: gatherSpeedFlat += 0.02f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingSpeedPercent3: gatherSpeedFlat += 0.03f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingSpeedPercent4: gatherSpeedFlat += 0.04f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingStaminaEfficiencyPercent2: gatherEnergyEfficiency += 0.02f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingGritPercent1: gatherGrit += 0.01f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingGritPercent3: gatherGrit += 0.03f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingBonusFindPercent1: gatherBonusItemChance += 0.01f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingBonusFindPercent3: gatherBonusItemChance += 0.03f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingExtraLogChancePercent2: gatherExtraLogChance += 0.02f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingYieldPercent2: gatherYieldPercent += 0.02f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingYieldPercent3: gatherYieldPercent += 0.03f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingStaminaEfficiencyPercent3: gatherEnergyEfficiency += 0.03f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingGritPercent4: gatherGrit += 0.04f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingBonusFindPercent6: gatherBonusItemChance += 0.06f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingCritRestoreEnergy10OnGrit:
                        woodcuttingGritProcRestoreStaminaFraction += 0.07f;
                        break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingGritProcRestoresMaxStaminaPercent7:
                        woodcuttingGritProcRestoreStaminaFraction += 0.07f;
                        break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingGritProcRestoresMaxStaminaPercent8:
                        woodcuttingGritProcRestoreStaminaFraction += 0.08f;
                        break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingBonusXpChancePercent2: woodcuttingBonusXpChance += 0.04f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingNoStaminaSwingChancePercent3: woodcuttingNoStaminaSwingChance += 0.03f; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingFrenzyAfterGritSpeedPercent5Duration7s: woodcuttingFrenzyStacks += 1; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingForestFlowContinuousSpeedPercent3RecoveryPercent3: woodcuttingForestFlowStacks += 1; break;
                    case WoodcuttingMinorNodeStatOption.WoodcuttingChanceNotToCountTowardTreeDepletionPercent10:
                        woodcuttingChanceNotToCountTowardTreeDepletion += 0.10f;
                        break;
                }
            }
            else if (skill.skillType == SkillType.Mining)
            {
                switch (unlock.miningMinorStatOption)
                {
                    case MiningMinorNodeStatOption.MiningGatherSpeedFlat01: gatherSpeedFlat += 0.1f; break;
                    case MiningMinorNodeStatOption.MiningGritPercent2: gatherGrit += 0.02f; break;
                    case MiningMinorNodeStatOption.MiningEnergyEfficiencyPercent2: gatherEnergyEfficiency += 0.02f; break;
                    case MiningMinorNodeStatOption.MiningBonusItemChancePercent2: gatherBonusItemChance += 0.02f; break;
                }
            }
            else if (skill.skillType == SkillType.Fishing)
            {
                switch (unlock.fishingMinorStatOption)
                {
                    case FishingMinorNodeStatOption.FishingGatherSpeedFlat01: gatherSpeedFlat += 0.1f; break;
                    case FishingMinorNodeStatOption.FishingGritPercent2: gatherGrit += 0.02f; break;
                    case FishingMinorNodeStatOption.FishingEnergyEfficiencyPercent2: gatherEnergyEfficiency += 0.02f; break;
                    case FishingMinorNodeStatOption.FishingBonusItemChancePercent2: gatherBonusItemChance += 0.02f; break;
                    case FishingMinorNodeStatOption.FishingSpeedPercent2: gatherSpeedFlat += 0.02f; break;
                    case FishingMinorNodeStatOption.FishingSpeedPercent3: gatherSpeedFlat += 0.03f; break;
                    case FishingMinorNodeStatOption.FishingSpeedPercent4: gatherSpeedFlat += 0.04f; break;
                    case FishingMinorNodeStatOption.FishingStaminaEfficiencyPercent1: gatherEnergyEfficiency += 0.01f; break;
                    case FishingMinorNodeStatOption.FishingStaminaEfficiencyPercent3: gatherEnergyEfficiency += 0.03f; break;
                    case FishingMinorNodeStatOption.FishingGritPercent1: gatherGrit += 0.01f; break;
                    case FishingMinorNodeStatOption.FishingGritPercent3: gatherGrit += 0.03f; break;
                    case FishingMinorNodeStatOption.FishingBonusFindPercent1: gatherBonusItemChance += 0.01f; break;
                    case FishingMinorNodeStatOption.FishingBonusFindPercent3: gatherBonusItemChance += 0.03f; break;
                    case FishingMinorNodeStatOption.FishingBonusFindPercent5: gatherBonusItemChance += 0.05f; break;
                    case FishingMinorNodeStatOption.FishingGritRestoreStaminaFlat10: fishingGritRestoreStacks++; break;
                    case FishingMinorNodeStatOption.FishingDoubleXpChancePercent3: fishingDoubleXpChance += 0.03f; break;
                    case FishingMinorNodeStatOption.FishingNoStaminaSwingChancePercent3: fishingNoStaminaSwingChance += 0.03f; break;
                    case FishingMinorNodeStatOption.FishingFrenzyAfterGritSpeedPercent5Duration7s: fishingFrenzyStacks++; break;
                    case FishingMinorNodeStatOption.FishingCalmWatersContinuousSpeedPercent3EfficiencyPercent3: fishingCalmWatersStacks++; break;
                    case FishingMinorNodeStatOption.FishingBaitConservationChancePercent10: fishingBaitConservationChance += 0.10f; break;
                    case FishingMinorNodeStatOption.FishingAutoCookChancePercent2: fishingAutoCookChance += 0.02f; break;
                    case FishingMinorNodeStatOption.FishingAutoCookChancePercent4: fishingAutoCookChance += 0.04f; break;
                    case FishingMinorNodeStatOption.FishingTreasureCatchChanceSmall: fishingTreasureMinorStacks++; break;
                }
            }
            else if (skill.skillType == SkillType.Endurance)
            {
                switch (unlock.enduranceMinorStatOption)
                {
                    case EnduranceMinorNodeStatOption.EnduranceArmorFlat2: enduranceArmor += 2f; break;
                    case EnduranceMinorNodeStatOption.EnduranceMagicResistFlat2: enduranceMagicResist += 2f; break;
                    case EnduranceMinorNodeStatOption.EnduranceHealthFlat5: enduranceHp += 5f; break;
                    case EnduranceMinorNodeStatOption.EnduranceLifeRegenFlat1: enduranceHpRegen += 1f; break;
                }
            }
            else if (skill.skillType == SkillType.Magic)
            {
                switch (unlock.magicMinorStatOption)
                {
                    case MagicMinorNodeStatOption.MagicDamagePercent3: magicDamage += 0.03f; break;
                    case MagicMinorNodeStatOption.MagicAttackSpeedPercent3: magicAttackSpeed += 0.03f; break;
                    case MagicMinorNodeStatOption.MagicCritChancePercent2: magicCritChance += 0.02f; break;
                    case MagicMinorNodeStatOption.MagicCritDamagePercent8: magicCritDamage += 0.08f; break;
                }
            }
        }

        var sb = new StringBuilder();
        void Pct(float value, string label) => AppendPct(sb, value, label);
        void Flat(float value, string label) => AppendFlat(sb, value, label);
        void Line(string text) => AppendPassiveLine(sb, text);
        if (skill.skillType == SkillType.Melee)
        {
            Flat(minMeleeDamage, "Min Melee Damage");
            Flat(maxMeleeDamage, "Max Melee Damage");
            Pct(meleeDamage, "Melee Damage");
            Pct(meleeAttackSpeed, "Melee Attack Speed");
            Pct(meleeMoveSpeed, "Melee Move Speed");
            Pct(meleeCritChance, "Melee Crit Chance");
            Pct(meleeCritDamage, "Melee Crit Damage");
            Pct(bleedChance, "Melee Bleed Chance");
            Pct(bleedDamage, "Melee Bleed Multiplier");
            Pct(poisonChance, "Melee Poison Chance");
            Pct(poisonDuration, "Melee Poison Duration");
            Pct(ailmentDamage, "Melee Bleed, Poison, Burn Multipliers");
            Pct(shockChance, "Melee Shock Chance");
            Pct(burnChance, "Melee Burn Chance");
            Pct(vsBleeding, "Melee Damage to Bleeding Enemies");
            Pct(vsPoisoned, "Melee Damage to Poisoned Enemies");
            Pct(vsShocked, "Melee Damage to Shocked Enemies");
            Pct(vsBurning, "Melee Damage to Burning Enemies");
            Pct(vsLowHp, $"Melee Damage to Low HP Enemies {CharacterStats.MeleeLowHpDisplaySuffix}");
            Pct(lifeSteal, "Melee Lifesteal");
        }
        else if (skill.skillType == SkillType.Ranged)
        {
            Pct(rangedDamage, "Ranged Damage");
            Pct(rangedAttackSpeed, "Ranged Attack Speed");
            Pct(rangedCritChance, "Ranged Crit Chance");
            Pct(rangedMoveSpeed, "Ranged Move Speed");
        }
        else if (skill.skillType == SkillType.Magic)
        {
            Pct(magicDamage, "Magic Damage");
            Pct(magicAttackSpeed, "Cast Speed");
            Pct(magicCritChance, "Crit Chance");
            Pct(magicCritDamage, "Crit Damage");
        }
        else if (skill.skillType == SkillType.Endurance)
        {
            Flat(enduranceArmor, "Armour");
            Flat(enduranceMagicResist, "Magic Resist");
            Flat(enduranceHp, "Max HP");
            Flat(enduranceHpRegen, "HP Regen");
        }
        else if (skill.skillType == SkillType.Mining || skill.skillType == SkillType.Woodcutting || skill.skillType == SkillType.Fishing)
        {
            bool isWoodcuttingSkill = skill.skillType == SkillType.Woodcutting;
            bool isFishingSkill = skill.skillType == SkillType.Fishing;
            if (gatherSpeedFlat > 0f)
            {
                if (isWoodcuttingSkill)
                    Pct(gatherSpeedFlat, "Woodcutting Speed");
                else if (isFishingSkill)
                    Pct(gatherSpeedFlat, "Fishing Speed");
                else
                {
                    Line("• +" + gatherSpeedFlat.ToString("0.##") + " Gathering Speed");
                }
            }
            if (isWoodcuttingSkill)
            {
                Pct(gatherGrit, "Woodcutting Grit Chance");
                Pct(gatherEnergyEfficiency, "Woodcutting Stamina Efficiency");
                Pct(gatherBonusItemChance, "Woodcutting Bonus Find Chance");
                Pct(gatherExtraLogChance, "Woodcutting Chance for +1 Extra Main Resource");
                Pct(gatherYieldPercent, "Woodcutting Base Resource Yield");
                if (woodcuttingGritProcRestoreStaminaFraction > 0f)
                {
                    Line("• Woodcutting Grit procs restore +"
                        + Mathf.RoundToInt(woodcuttingGritProcRestoreStaminaFraction * 100f)
                        + "% stamina");
                }
                if (woodcuttingBonusXpChance > 0f)
                {
                    Line("• +"
                        + Mathf.RoundToInt(woodcuttingBonusXpChance * 100f)
                        + "% chance to double XP gained from Woodcutting");
                }
                Pct(woodcuttingNoStaminaSwingChance, "Woodcutting No-Stamina Swing Chance");
                Pct(woodcuttingChanceNotToCountTowardTreeDepletion, "Woodcutting chance not to count toward tree depletion");
                if (woodcuttingFrenzyStacks > 0)
                {
                    int pct = 5 * woodcuttingFrenzyStacks;
                    Line("• After a Woodcutting Grit proc: +" + pct + "% Woodcutting Speed for 7 seconds");
                }
                if (woodcuttingForestFlowStacks > 0)
                {
                    int sp = 3 * woodcuttingForestFlowStacks;
                    int se = 3 * woodcuttingForestFlowStacks;
                    Line("• While continuously woodcutting (after 15 seconds): +" + sp
                        + "% Woodcutting Speed, +" + se + "% Woodcutting Stamina Efficiency");
                }
            }
            else
            {
                if (isFishingSkill)
                {
                    Pct(gatherGrit, "Fishing Grit Chance");
                    Pct(gatherEnergyEfficiency, "Fishing Stamina Efficiency");
                    Pct(gatherBonusItemChance, "Fishing Bonus Find Chance");
                    if (fishingGritRestoreStacks > 0)
                    {
                        int stamina = 10 * fishingGritRestoreStacks;
                        Line("• Fishing Grit catches restore +" + stamina + " stamina");
                    }
                    if (fishingDoubleXpChance > 0f)
                    {
                        Line("• +"
                            + Mathf.RoundToInt(fishingDoubleXpChance * 100f)
                            + "% chance to gain double Fishing XP");
                    }
                    Pct(fishingNoStaminaSwingChance, "chance for Fishing casts to cost no stamina");
                    if (fishingFrenzyStacks > 0)
                    {
                        int frenzyPct = 5 * fishingFrenzyStacks;
                        Line("• After a Fishing Grit catch: +" + frenzyPct + "% Fishing Speed for 7 seconds");
                    }
                    if (fishingCalmWatersStacks > 0)
                    {
                        int calmSp = 3 * fishingCalmWatersStacks;
                        int calmSe = 3 * fishingCalmWatersStacks;
                        Line("• While continuously fishing: +" + calmSp + "% Fishing Speed, +"
                            + calmSe + "% Fishing Stamina Efficiency");
                    }
                    Pct(fishingBaitConservationChance, "chance to not consume bait durability");
                    Pct(fishingAutoCookChance, "chance for caught fish to be automatically cooked");
                    if (fishingTreasureMinorStacks > 0)
                        Line("• Small chance to catch treasure while fishing");
                }
                else
                {
                    Pct(gatherGrit, "Grit");
                    Pct(gatherEnergyEfficiency, "Energy Efficiency");
                    Pct(gatherBonusItemChance, "Bonus Item Chance");
                }
            }
        }

        int levelsPastCap = Mathf.Max(0, currentLevel - CharacterStats.SkillPostCapThresholdLevel);
        if (levelsPastCap > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Passive Bonuses Past 50</b>");
            switch (skill.skillType)
            {
                case SkillType.Melee:
                    Line("• +" + levelsPastCap + "% Melee Damage");
                    break;
                case SkillType.Ranged:
                    Line("• +" + levelsPastCap + "% Ranged Damage");
                    break;
                case SkillType.Magic:
                    Line("• +" + levelsPastCap + "% Magic Damage");
                    break;
                case SkillType.Endurance:
                    Line("• +" + Mathf.RoundToInt(levelsPastCap * 5f) + " Max HP, +" + levelsPastCap + " Armour");
                    break;
                case SkillType.Woodcutting:
                    Line("• +" + levelsPastCap + "% Woodcutting Speed");
                    break;
                case SkillType.Mining:
                    Line("• +" + levelsPastCap + "% Mining Speed");
                    break;
                case SkillType.Fishing:
                    Line("• +" + levelsPastCap + "% Fishing Speed");
                    break;
            }
        }

        if (sb.Length == 0)
            return "No unlocks yet.";

        return sb.ToString();
    }

    /// <summary>First capstone passive row at or below <paramref name="currentLevel"/> (e.g. Woodcutting Lv50 Bountiful Chop).</summary>
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

    private static void AppendPassiveLine(StringBuilder sb, string line)
    {
        sb.Append(line);
        sb.AppendLine();
    }

    private static void AppendFlat(StringBuilder sb, float value, string label)
    {
        if (value <= 0f)
            return;

        string line = "• +" + Mathf.RoundToInt(value) + " " + label;
        AppendPassiveLine(sb, line);
    }

    private static void AppendPct(StringBuilder sb, float value, string label)
    {
        if (value <= 0f)
            return;

        string line = "• +" + Mathf.RoundToInt(value * 100f) + "% " + label;
        AppendPassiveLine(sb, line);
    }

    private static void AppendWoodcuttingMajorPassiveSummary(
        StringBuilder sb,
        SkillDefinition skill,
        SkillsManager skillManager,
        int sourceLevel,
        System.Func<int, string> choiceSpineIdResolver,
        System.Action<StringBuilder, string, string> appendEffectLines,
        string unsetMajorSkillLabel = "Woodcutting")
    {
        var majors = new List<(SkillUnlockDefinition u, int idx)>();
        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition u = skill.unlocks[i];
            if (u != null && u.requiredLevel == sourceLevel &&
                (u.unlockType == SkillUnlockType.MajorPassive || u.unlockType == SkillUnlockType.Ability))
                majors.Add((u, i));
        }

        if (majors.Count <= 0)
            return;

        majors.Sort((a, b) => a.idx.CompareTo(b.idx));
        int rowPick = skillManager.GetSkillAbilityRowPick(skill.skillType, sourceLevel, -1);
        int enh = rowPick >= 0 && rowPick < majors.Count
            ? skillManager.GetSkillChoiceSelection(skill.skillType, choiceSpineIdResolver(rowPick), -1)
            : -1;

        if (rowPick >= 0 && rowPick < majors.Count && !string.IsNullOrWhiteSpace(majors[rowPick].u.title))
        {
            SkillUnlockDefinition committed = majors[rowPick].u;
            string majorTitle = committed.title.Trim();
            sb.AppendLine($"<b>• {majorTitle} (Major Passive) (Lv {sourceLevel})</b>");

            string enhancementTitle = enh >= 0 && committed.choices != null && enh < committed.choices.Count &&
                                      committed.choices[enh] != null && !string.IsNullOrWhiteSpace(committed.choices[enh].title)
                ? committed.choices[enh].title.Trim()
                : null;

            if (!string.IsNullOrWhiteSpace(enhancementTitle))
                sb.AppendLine($"   - {enhancementTitle} (Enhancement)");
            else
                sb.AppendLine("   - Base Effect");

            appendEffectLines?.Invoke(sb, majorTitle, enhancementTitle);
        }
        else
        {
            sb.AppendLine($"<b>• {unsetMajorSkillLabel} Major Passive (Lv {sourceLevel})</b>");
            sb.AppendLine("   - (major not selected)");
        }
    }

    private static void AppendWoodcuttingLevel35EffectLines(StringBuilder sb, string majorTitle, string enhancementTitle)
    {
        if (string.IsNullOrWhiteSpace(majorTitle))
            return;

        if (string.Equals(majorTitle, "Ancient Lumbercraft", System.StringComparison.OrdinalIgnoreCase))
        {
            int hiddenChance = 10;
            bool experiencedGatherer = string.Equals(enhancementTitle, "Experienced Gatherer", System.StringComparison.OrdinalIgnoreCase);
            bool treasureHunter = string.Equals(enhancementTitle, "Treasure Hunter", System.StringComparison.OrdinalIgnoreCase);
            if (experiencedGatherer)
                hiddenChance += 5;
            sb.AppendLine($"     +{hiddenChance}% chance to find hidden resources");
            if (treasureHunter)
                sb.AppendLine("     +10% chance for Hidden Resources to double");
            return;
        }

        if (string.Equals(majorTitle, "Forest's Favor", System.StringComparison.OrdinalIgnoreCase))
        {
            int extraItemChance = 25;
            bool richHarvest = string.Equals(enhancementTitle, "Rich Harvest", System.StringComparison.OrdinalIgnoreCase);
            bool hiddenRiches = string.Equals(enhancementTitle, "Hidden Riches", System.StringComparison.OrdinalIgnoreCase);
            if (hiddenRiches)
                extraItemChance += 10;
            sb.AppendLine($"     +{extraItemChance}% Bonus Find Extra Item Chance");
            if (richHarvest)
                sb.AppendLine("     +10% Bonus Find Chance");
        }
    }

    private static void AppendWoodcuttingLevel15EffectLines(StringBuilder sb, string majorTitle, string enhancementTitle) =>
        GatheringPassiveTooltipText.AppendMajorPassiveEffectLines(sb, SkillType.Woodcutting, majorTitle, enhancementTitle);

    private static void AppendFishingLevel15EffectLines(StringBuilder sb, string majorTitle, string enhancementTitle) =>
        GatheringPassiveTooltipText.AppendMajorPassiveEffectLines(sb, SkillType.Fishing, majorTitle, enhancementTitle);

    private void ApplyRightPanelSectionVisibility(SkillDefinition skill, int level)
    {
        bool showCapstone = skill != null && level >= CharacterStats.SkillPostCapThresholdLevel;

        if (capstoneSection.section)
            capstoneSection.section.SetActive(showCapstone);

        if (capstoneSection.header)
            capstoneSection.header.SetActive(showCapstone);

        if (capstoneSection.content)
            capstoneSection.content.gameObject.SetActive(showCapstone);
    }

    private MajorPassiveListEntryUI CreateMajorPassiveRow(Transform parent)
    {
        if (!majorPassiveEntryPrefab)
        {
            Debug.LogWarning("[SkillsAbilitiesPageUI] majorPassiveEntryPrefab is not assigned.", this);
            return null;
        }

        MajorPassiveListEntryUI row = Instantiate(majorPassiveEntryPrefab, parent);
        ApplyMajorPassiveRowHeight(row.transform as RectTransform);
        return row;
    }

    private void ApplyMajorPassiveRowHeight(RectTransform rt)
    {
        if (!rt)
            return;

        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, majorPassiveRowHeight);

        LayoutElement layout = rt.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rt.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = majorPassiveRowHeight;
        layout.preferredHeight = majorPassiveRowHeight;
    }

    private void CommitCapstonePanelCache(SkillDefinition skill, int level, int fingerprint)
    {
        _cachedCapstoneSkill = skill;
        _cachedCapstoneLevel = level;
        _cachedCapstoneFingerprint = fingerprint;
    }

    private void CommitMajorPassivesPanelCache(SkillDefinition skill, int level, int fingerprint)
    {
        _cachedMajorPassivesSkill = skill;
        _cachedMajorPassivesLevel = level;
        _cachedMajorPassivesFingerprint = fingerprint;
    }

    private int ComputeCapstonePanelFingerprint(SkillDefinition skill, int level)
    {
        if (skill == null || skillsManager == null)
            return 0;

        SkillUnlockDefinition cap = FindCapstonePassiveUnlockForSkill(skill, level);
        if (cap == null)
            return 0;

        int h = cap.requiredLevel * 31;
        string spine = SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock(skill, cap);
        if (!string.IsNullOrEmpty(spine))
            h = (h * 31) ^ (skillsManager.GetSkillChoiceSelection(skill.skillType, spine, -1) + 1);

        return h;
    }

    private int ComputeMajorPassivesPanelFingerprint(SkillDefinition skill, int level)
    {
        if (skill == null || skillsManager == null)
            return 0;

        CollectMajorPassiveTierRows(skill, level, skillsManager,
            out List<int> tierLevels, out List<SkillUnlockDefinition> committed, out List<bool> available);

        int h = level;
        for (int i = 0; i < tierLevels.Count; i++)
        {
            h = (h * 31) ^ tierLevels[i];
            h = (h * 31) ^ (available[i] ? -1 : (committed[i]?.requiredLevel ?? 0));
            if (!available[i] && committed[i] != null)
            {
                string spine = SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock(skill, committed[i]);
                if (!string.IsNullOrEmpty(spine))
                    h = (h * 31) ^ skillsManager.GetSkillChoiceSelection(skill.skillType, spine, -1);

                if (SkillTreeMajorPassiveRowIndicators.TryGet(skill, committed[i], skillsManager,
                        out bool notSelected, out bool enhance))
                {
                    h = (h * 31) ^ (notSelected ? 1 : 0);
                    h = (h * 31) ^ (enhance ? 2 : 0);
                }
            }
        }

        return h;
    }

    public static void CollectMajorPassiveTierRows(
        SkillDefinition skill,
        int playerLevel,
        SkillsManager sm,
        out List<int> tierLevels,
        out List<SkillUnlockDefinition> committedUnlocks,
        out List<bool> availablePlaceholders)
    {
        tierLevels = new List<int>();
        committedUnlocks = new List<SkillUnlockDefinition>();
        availablePlaceholders = new List<bool>();

        if (skill?.unlocks == null)
            return;

        var levelSet = new HashSet<int>();
        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition u = skill.unlocks[i];
            if (u == null || u.unlockType != SkillUnlockType.MajorPassive)
                continue;

            int req = Mathf.Max(1, u.requiredLevel);
            if (playerLevel < req)
                continue;

            levelSet.Add(req);
        }

        var sortedLevels = new List<int>(levelSet);
        sortedLevels.Sort();

        for (int li = 0; li < sortedLevels.Count; li++)
        {
            int rowLevel = sortedLevels[li];
            List<SkillUnlockDefinition> siblings =
                SkillTreeRowPickRules.GetMultiPickSiblingsAtLevel(skill, rowLevel, majorPassivesOnly: true);
            if (siblings.Count == 0)
                continue;

            int pick = sm != null
                ? SkillTreeRowPickRules.GetCommittedRowPick(sm, skill.skillType, rowLevel, -1, siblings.Count - 1)
                : -1;

            if (siblings.Count >= 2 && pick < 0)
            {
                tierLevels.Add(rowLevel);
                committedUnlocks.Add(null);
                availablePlaceholders.Add(true);
                continue;
            }

            if (pick < 0)
                pick = 0;

            if (pick >= siblings.Count)
                continue;

            tierLevels.Add(rowLevel);
            committedUnlocks.Add(siblings[pick]);
            availablePlaceholders.Add(false);
        }
    }

    private static string BuildAdditionalUnlocksDisplay(SkillDefinition skill, int currentLevel)
    {
        if (skill?.unlocks == null || skill.unlocks.Count == 0)
            return string.Empty;

        var rows = new List<SkillUnlockDefinition>();
        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition u = skill.unlocks[i];
            if (u == null)
                continue;

            if (currentLevel < u.requiredLevel)
                continue;

            if (u.unlockType != SkillUnlockType.Unlock && u.unlockType != SkillUnlockType.MinorUnlock)
                continue;

            rows.Add(u);
        }

        if (rows.Count == 0)
            return string.Empty;

        rows.Sort((a, b) =>
        {
            int c = a.requiredLevel.CompareTo(b.requiredLevel);
            if (c != 0)
                return c;

            return string.Compare(
                SkillsAbilityPresentationResolver.ResolveUnlockTitle(a),
                SkillsAbilityPresentationResolver.ResolveUnlockTitle(b),
                StringComparison.Ordinal);
        });

        var sb = new StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            SkillUnlockDefinition u = rows[i];
            string title = SkillsAbilityPresentationResolver.ResolveUnlockTitle(u);
            if (string.IsNullOrWhiteSpace(title))
                title = "Unlock";

            int lv = Mathf.Max(1, u.requiredLevel);
            sb.Append("• ");
            sb.Append(title);
            sb.Append(" (Lv ");
            sb.Append(lv);
            sb.AppendLine(")");

            string desc = SkillsAbilityPresentationResolver.ResolveUnlockDescription(u);
            if (!string.IsNullOrWhiteSpace(desc))
            {
                string flat = desc.Trim().Replace('\r', ' ').Replace('\n', ' ');
                sb.Append("   ");
                sb.AppendLine(flat);
            }
        }

        return sb.ToString().TrimEnd();
    }
}

[Serializable]
public struct SkillDevCompletionEntry
{
    public SkillType skill;
    public SkillDevCompletionTier tier;
}
