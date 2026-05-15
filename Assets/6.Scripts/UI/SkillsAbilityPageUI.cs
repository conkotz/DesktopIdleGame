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

    [Header("Right panel")]
    [FormerlySerializedAs("unlocksText")]
    [Tooltip("Unlock list from SkillDefinition.unlocks (display-only).")]
    [SerializeField] private TMP_Text rightUnlocksText;

    [FormerlySerializedAs("abilitiesText")]
    [Tooltip("Abilities section (placeholder until drag/drop).")]
    [SerializeField] private TMP_Text rightAbilitiesText;

    [Header("Right panel — abilities list (optional)")]
    [Tooltip("If set, abilities are shown as draggable entries. If empty, the placeholder TMP text is used.")]
    [SerializeField] private Transform rightAbilitiesListParent;

    [Tooltip("Optional prefab for one ability row. If empty, a simple row is created at runtime.")]
    [SerializeField] private AbilityEntryUI abilityEntryPrefab;

    [Header("Right panel — layout auto-fix")]
    [Tooltip("Auto-configures runtime abilities list to stretch/fill properly in right panel layouts.")]
    [SerializeField] private bool autoFixRightPanelLayout = true;
    [Tooltip("Spacing between ability rows in the right panel.")]
    [SerializeField] private float abilityRowSpacing = 6f;
    [Tooltip("Padding inside right abilities list content.")]
    [SerializeField] private RectOffset abilityListPadding;

    private SkillDefinition _selectedSkill;
    private Coroutine _deferredRefreshRoutine;
    private bool _loggedMissingRefs;
    private readonly Dictionary<SkillType, SkillListEntryUI> _entryBySkillType = new();
    private readonly HashSet<SkillType> _pendingEntryGlowBySkill = new();
    private readonly Dictionary<SkillType, HashSet<int>> _pendingTreeGlowLevelsBySkill = new();
    private bool _skillsEventsSubscribed;

    private string _passiveUnlockHighlightKey;

    private void Awake()
    {
        PreferRuntimeSkillsManager();

        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (abilityListPadding == null)
            abilityListPadding = new RectOffset(0, 0, 0, 0);

        if (rightUnlocksText)
            rightUnlocksText.richText = true;
        if (!abilityDatabase)
            abilityDatabase = AbilityDatabase.LoadDefault();

        ValidateRefsOnce();
        EnsureCenterTreeReference();
        EnsureRightPanelLayoutConfigured();
        TrySubscribeSkillsEvents();
        HookTreeGlowAcknowledge();
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
        // Layout / tree bootstrap order: one frame later matches level-up deferred refresh so center tree + ability rows match the selected skill on first open.
        ScheduleDeferredProgressRefresh();
    }

    private void OnDisable()
    {
        if (_deferredRefreshRoutine != null)
        {
            StopCoroutine(_deferredRefreshRoutine);
            _deferredRefreshRoutine = null;
        }
    }

    private void OnDestroy()
    {
        TryUnsubscribeSkillsEvents();
        UnhookTreeGlowAcknowledge();
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled)
            return;

        PreferRuntimeSkillsManager();
        TrySubscribeSkillsEvents();
        if (!skillsManager)
            return;

        RefreshAllEntryLevels();
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
        skillsManager.OnSkillChoiceSelectionChanged += HandleSkillChoiceSelectionChanged;
        skillsManager.OnSkillAbilityRowPickChanged += HandleSkillAbilityRowPickChanged;
        _skillsEventsSubscribed = true;
    }

    private void TryUnsubscribeSkillsEvents()
    {
        if (!skillsManager) return;
        skillsManager.OnLevelUp -= HandleSkillsLevelUp;
        skillsManager.OnSkillLevelDecreased -= HandleSkillsLevelUp;
        skillsManager.OnSkillChoiceSelectionChanged -= HandleSkillChoiceSelectionChanged;
        skillsManager.OnSkillAbilityRowPickChanged -= HandleSkillAbilityRowPickChanged;
        _skillsEventsSubscribed = false;
    }

    /// <summary>
    /// Refresh skills UI only when a level changes.
    /// This avoids rebuilding the tree every combat XP tick (which hides hover tooltips).
    /// </summary>
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
        if (rightUnlocksText)
            rightUnlocksText.text = BuildUnlocksDisplay(_selectedSkill, level, skillsManager, _passiveUnlockHighlightKey);
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
        EnsureCenterTreeReference();
        if (centerSkillTreeView) centerSkillTreeView.SetSkill(_selectedSkill);
        RefreshView();
        RefreshListSelection();

        ActionBarUI gatherBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (gatherBar != null)
        {
            if (ActionBarUI.IsGatheringSkillType(skill.skillType))
                gatherBar.ShowGatheringBarForSkill(skill.skillType, GatheringBarDriveKind.SkillsMenuSelection);
            else
                gatherBar.ExitGatheringBarToCombat();
        }
    }

    private void EnsureCenterTreeReference()
    {
        if (centerSkillTreeView) return;

        // Prefer a tree under this skills window — scene-wide lookup can bind the wrong SkillTreeViewUI when multiple exist.
        centerSkillTreeView = GetComponentInChildren<SkillTreeViewUI>(true);
        if (!centerSkillTreeView && transform.root != null)
            centerSkillTreeView = transform.root.GetComponentInChildren<SkillTreeViewUI>(true);
    }

    private void OnSkillEntryClicked(SkillDefinition skill)
    {
        // When browsing skills in the menu, temporarily drive the bottom XP strip to this skill.
        // Normal gameplay XP gain (AddXp) will still override ActiveSkill/Source as soon as XP is earned.
        if (!IsActionOccurring() && skillsManager != null && skill != null)
            skillsManager.SetActiveXpDisplay(skill.skillType, "");

        SelectSkill(skill);
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
        if (_selectedSkill != null) return;
        if (skillDatabase == null) return;

        PreferRuntimeSkillsManager();

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
        if (!parent) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
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
            if (centerTitleText) centerTitleText.text = "No Skill Selected";
            if (centerSkillTreePlaceholderText) centerSkillTreePlaceholderText.text = "";
            if (centerSkillTreeView) centerSkillTreeView.SetSkill(null);
            if (rightUnlocksText) rightUnlocksText.text = "";
            if (rightAbilitiesText) rightAbilitiesText.text = "";
            return;
        }

        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
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

        if (rightUnlocksText)
            rightUnlocksText.text = BuildUnlocksDisplay(_selectedSkill, level, skillsManager, _passiveUnlockHighlightKey);

        RefreshAbilitiesPanel(_selectedSkill, level);
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
        if (!isActiveAndEnabled || _selectedSkill == null || rightUnlocksText == null)
            return;

        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        rightUnlocksText.text = BuildUnlocksDisplay(_selectedSkill, level, skillsManager, _passiveUnlockHighlightKey);
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

    private void RefreshAbilitiesPanel(SkillDefinition skill, int level)
    {
        if (skill == null)
        {
            if (rightAbilitiesText) rightAbilitiesText.text = "";
            ClearAbilityRows();
            return;
        }

        // If no list parent is assigned, create a simple one under the abilities text's parent.
        if (!rightAbilitiesListParent)
            rightAbilitiesListParent = EnsureRuntimeAbilitiesListParent();

        if (!rightAbilitiesListParent)
        {
            if (rightAbilitiesText)
                rightAbilitiesText.text = "Abilities\n(placeholder — drag/drop not implemented yet)";
            return;
        }

        PreferRuntimeSkillsManager();

        var abilityTierLevels = CollectSortedAbilityTierLevels(skill);
        if (abilityTierLevels.Count == 0)
        {
            if (rightAbilitiesText)
                rightAbilitiesText.text = "No abilities yet";
            ClearAbilityRows();
            return;
        }

        int unlockedTiersCount = CountUnlockedAbilityTiers(skill, level);
        int selectedInTreeCount = CountTotalAbilitiesSelectedInTree(skill, level);
        if (rightAbilitiesText)
            rightAbilitiesText.text = $"Abilities unlocked: {unlockedTiersCount}\nAbilities selected: {selectedInTreeCount}";

        ClearAbilityRows();

        var tooltip = FindBestSharedTooltip();
        var canvas = GetComponentInParent<Canvas>();
        RectTransform abilityPanelRect = rightAbilitiesText ? rightAbilitiesText.transform.parent as RectTransform : null;

        for (int i = 0; i < abilityTierLevels.Count; i++)
        {
            int rowLevel = abilityTierLevels[i];
            if (level < rowLevel)
                continue;

            var siblings = SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(skill, rowLevel);
            if (siblings == null || siblings.Count == 0)
                continue;

            int pick = skillsManager != null ? skillsManager.GetSkillAbilityRowPick(skill.skillType, rowLevel, -1) : -1;

            AbilityEntryUI row = CreateAbilityRow(rightAbilitiesListParent);
            row.SetTooltipDocking(abilityPanelRect, FlipInsideBounds.PreferredSide.Left);

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
        }

        if (rightAbilitiesListParent is RectTransform abilitiesListRt)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(abilitiesListRt);
        }
    }

    private void HandleAbilityDoubleClickAssignToActionBar(AbilityDefinition def)
    {
        if (def == null)
            return;

        PreferRuntimeSkillsManager();
        ActionBarUI bar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
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
        if (rightAbilitiesText == null)
            return null;

        Transform parent = rightAbilitiesText.transform.parent;
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

    /// <summary>Distinct ability tier rows (Lv5 / Lv25 / …) that have at least one ability sibling on the skill tree.</summary>
    private static List<int> CollectSortedAbilityTierLevels(SkillDefinition skill)
    {
        var candidate = new HashSet<int>();
        if (skill?.unlocks == null)
            return new List<int>();

        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = skill.unlocks[i];
            if (unlock == null || unlock.ability == null)
                continue;

            bool abilityLike =
                unlock.unlockType == SkillUnlockType.Ability ||
                unlock.unlockType == SkillUnlockType.CapstonePassive;
            if (!abilityLike)
                continue;

            candidate.Add(Mathf.Max(1, unlock.requiredLevel));
        }

        var result = new List<int>();
        foreach (int rl in candidate)
        {
            if (SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(skill, rl).Count > 0)
                result.Add(rl);
        }

        result.Sort();
        return result;
    }

    private int CountUnlockedAbilityTiers(SkillDefinition skill, int playerLevel)
    {
        if (skill == null)
            return 0;

        var rows = CollectSortedAbilityTierLevels(skill);
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

        var rowLevels = CollectSortedAbilityTierLevels(skill);
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
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(32f, 32f);

        var nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(go.transform, false);
        var nameText = nameGO.GetComponent<TextMeshProUGUI>();
        nameText.fontSize = 18;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;

        var reqGO = new GameObject("Req", typeof(RectTransform), typeof(TextMeshProUGUI));
        reqGO.transform.SetParent(go.transform, false);
        var reqText = reqGO.GetComponent<TextMeshProUGUI>();
        reqText.fontSize = 16;
        reqText.alignment = TextAlignmentOptions.MidlineRight;

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

        if (rightAbilitiesListParent is RectTransform rt)
            ConfigureAbilitiesListLayout(rt);
        else if (rightAbilitiesListParent == null)
            rightAbilitiesListParent = EnsureRuntimeAbilitiesListParent();
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

    private void ClearAbilityRows()
    {
        if (!rightAbilitiesListParent)
            return;

        for (int i = rightAbilitiesListParent.childCount - 1; i >= 0; i--)
            Destroy(rightAbilitiesListParent.GetChild(i).gameObject);
    }

    private static string BuildUnlocksDisplay(SkillDefinition skill, int currentLevel, SkillsManager skillManager, string passiveHighlightKey = null)
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
        float lifeSteal = 0f;
        float vsBleeding = 0f;
        float vsPoisoned = 0f;
        float vsShocked = 0f;
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
                    case MeleeMinorNodeStatOption.MeleeLifeStealPercent1: lifeSteal += 0.01f; break;
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
        void Pct(float value, string label) => AppendPct(sb, value, label, passiveHighlightKey);
        void Flat(float value, string label) => AppendFlat(sb, value, label, passiveHighlightKey);
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
            Pct(vsBleeding, "Melee Damage to Bleeding Enemies");
            Pct(vsPoisoned, "Melee Damage to Poisoned Enemies");
            Pct(vsShocked, "Melee Damage to Shocked Enemies");
            Pct(vsLowHp, "Melee Damage to Low HP Enemies (<35% HP)");
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
                    sb.Append("• +");
                    sb.Append(gatherSpeedFlat.ToString("0.##"));
                    sb.Append(" Gathering Speed");
                    sb.AppendLine();
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
                    string line = "• Woodcutting Grit procs restore +"
                        + Mathf.RoundToInt(woodcuttingGritProcRestoreStaminaFraction * 100f)
                        + "% stamina";
                    AppendBuiltLineWithOptionalBold(sb, line, passiveHighlightKey);
                }
                if (woodcuttingBonusXpChance > 0f)
                {
                    string line = "• +"
                        + Mathf.RoundToInt(woodcuttingBonusXpChance * 100f)
                        + "% chance to double XP gained from Woodcutting";
                    AppendBuiltLineWithOptionalBold(sb, line, passiveHighlightKey);
                }
                Pct(woodcuttingNoStaminaSwingChance, "Woodcutting No-Stamina Swing Chance");
                Pct(woodcuttingChanceNotToCountTowardTreeDepletion, "Woodcutting chance not to count toward tree depletion");
                if (woodcuttingFrenzyStacks > 0)
                {
                    int pct = 5 * woodcuttingFrenzyStacks;
                    string line = "• After a Woodcutting Grit proc: +" + pct + "% Woodcutting Speed for 7 seconds";
                    AppendBuiltLineWithOptionalBold(sb, line, passiveHighlightKey);
                }
                if (woodcuttingForestFlowStacks > 0)
                {
                    int sp = 3 * woodcuttingForestFlowStacks;
                    int se = 3 * woodcuttingForestFlowStacks;
                    string line = "• While continuously woodcutting (after 15 seconds): +" + sp
                        + "% Woodcutting Speed, +" + se + "% Woodcutting Stamina Efficiency";
                    AppendBuiltLineWithOptionalBold(sb, line, passiveHighlightKey);
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
                        sb.Append("• Fishing Grit catches restore +");
                        sb.Append(stamina);
                        sb.Append(" stamina");
                        sb.AppendLine();
                    }
                    if (fishingDoubleXpChance > 0f)
                    {
                        sb.Append("• +");
                        sb.Append(Mathf.RoundToInt(fishingDoubleXpChance * 100f));
                        sb.Append("% chance to gain double Fishing XP");
                        sb.AppendLine();
                    }
                    Pct(fishingNoStaminaSwingChance, "chance for Fishing casts to cost no stamina");
                    if (fishingFrenzyStacks > 0)
                    {
                        int frenzyPct = 5 * fishingFrenzyStacks;
                        sb.Append("• After a Fishing Grit catch: +");
                        sb.Append(frenzyPct);
                        sb.Append("% Fishing Speed for 7 seconds");
                        sb.AppendLine();
                    }
                    if (fishingCalmWatersStacks > 0)
                    {
                        int calmSp = 3 * fishingCalmWatersStacks;
                        int calmSe = 3 * fishingCalmWatersStacks;
                        sb.Append("• While continuously fishing: +");
                        sb.Append(calmSp);
                        sb.Append("% Fishing Speed, +");
                        sb.Append(calmSe);
                        sb.Append("% Fishing Stamina Efficiency");
                        sb.AppendLine();
                    }
                    Pct(fishingBaitConservationChance, "chance to not consume bait durability");
                    Pct(fishingAutoCookChance, "chance for caught fish to be automatically cooked");
                    if (fishingTreasureMinorStacks > 0)
                        sb.AppendLine("• Small chance to catch treasure while fishing");
                }
                else
                {
                    Pct(gatherGrit, "Grit");
                    Pct(gatherEnergyEfficiency, "Energy Efficiency");
                    Pct(gatherBonusItemChance, "Bonus Item Chance");
                }
            }
        }

        if (skill.skillType == SkillType.Woodcutting && currentLevel >= PlayerController.WoodcuttingMajorPassiveSourceLevel &&
            skill.unlocks != null && skillManager != null)
        {
            AppendWoodcuttingMajorPassiveSummary(
                sb,
                skill,
                skillManager,
                PlayerController.WoodcuttingMajorPassiveSourceLevel,
                level => PlayerController.WoodcuttingLevel15ChoiceSpineId(level),
                AppendWoodcuttingLevel15EffectLines);
        }

        if (skill.skillType == SkillType.Woodcutting && currentLevel >= PlayerController.WoodcuttingLv35MajorPassiveSourceLevel &&
            skill.unlocks != null && skillManager != null)
        {
            AppendWoodcuttingMajorPassiveSummary(
                sb,
                skill,
                skillManager,
                PlayerController.WoodcuttingLv35MajorPassiveSourceLevel,
                level => PlayerController.WoodcuttingLevel35ChoiceSpineId(level),
                AppendWoodcuttingLevel35EffectLines);
        }

        if (skill.skillType == SkillType.Fishing && currentLevel >= PlayerController.FishingMajorPassiveSourceLevel &&
            skill.unlocks != null && skillManager != null)
        {
            AppendWoodcuttingMajorPassiveSummary(
                sb,
                skill,
                skillManager,
                PlayerController.FishingMajorPassiveSourceLevel,
                level => PlayerController.FishingLevel15ChoiceSpineId(level),
                AppendFishingLevel15EffectLines,
                "Fishing");
        }

        if ((skill.skillType == SkillType.Woodcutting || skill.skillType == SkillType.Fishing) && skill.unlocks != null)
        {
            SkillUnlockDefinition capstone = FindCapstonePassiveUnlockForSkill(skill, currentLevel);
            if (capstone != null)
            {
                string capTitle = string.IsNullOrWhiteSpace(capstone.title) ? "Capstone" : capstone.title.Trim();
                int capLv = Mathf.Max(1, capstone.requiredLevel);
                sb.AppendLine($"<b>• {capTitle} (Capstone) (Lv{capLv})</b>");
                if (!string.IsNullOrWhiteSpace(capstone.description))
                {
                    string[] parts = capstone.description.Trim().Split(
                        new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries);
                    for (int p = 0; p < parts.Length; p++)
                        parts[p] = parts[p].Trim();
                    string desc = string.Join(" ", parts);
                    sb.Append("   ");
                    sb.AppendLine(desc);
                }
            }
        }

        // Major passive conversion summary (currently Melee Lv10 Bloodletting branch).
        if (skill.skillType == SkillType.Melee && currentLevel >= 10)
        {
            sb.AppendLine("<b>• Bloodletting (Major Passive) (Lv10)</b>");
            int selected = skillManager != null ? skillManager.GetSkillChoiceSelection(SkillType.Melee, 10, -1) : -1;
            if (selected == 0)
            {
                sb.AppendLine("   - Venom Edge (Enhancement)");
                sb.AppendLine("     +10% Melee Poison Chance");
                sb.AppendLine("     +10% Melee Damage to Poisoned Targets");
            }
            else if (selected == 1)
            {
                sb.AppendLine("   - Hemorrhage (Enhancement — adds to Bloodletting)");
                sb.AppendLine("     +10% Melee Bleed Chance");
                sb.AppendLine("     +10% Melee Damage to Bleeding Targets");
                sb.AppendLine("     +5% Melee Bleed Multiplier");
                sb.AppendLine("     +1s Melee Bleed Duration");
            }
            else
            {
                sb.AppendLine("   - Base Effect");
                sb.AppendLine("     +10% Melee Bleed Chance");
                sb.AppendLine("     +10% Melee Damage to Bleeding Targets");
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
                    sb.Append("• +");
                    sb.Append(levelsPastCap);
                    sb.Append("% Melee Damage");
                    sb.AppendLine();
                    break;
                case SkillType.Ranged:
                    sb.Append("• +");
                    sb.Append(levelsPastCap);
                    sb.Append("% Ranged Damage");
                    sb.AppendLine();
                    break;
                case SkillType.Magic:
                    sb.Append("• +");
                    sb.Append(levelsPastCap);
                    sb.Append("% Magic Damage");
                    sb.AppendLine();
                    break;
                case SkillType.Endurance:
                    sb.Append("• +");
                    sb.Append(Mathf.RoundToInt(levelsPastCap * 5f));
                    sb.Append(" Max HP, +");
                    sb.Append(levelsPastCap);
                    sb.Append(" Armour");
                    sb.AppendLine();
                    break;
                case SkillType.Woodcutting:
                    sb.Append("• +");
                    sb.Append(levelsPastCap);
                    sb.Append("% Woodcutting Speed");
                    sb.AppendLine();
                    break;
                case SkillType.Mining:
                    sb.Append("• +");
                    sb.Append(levelsPastCap);
                    sb.Append("% Mining Speed");
                    sb.AppendLine();
                    break;
                case SkillType.Fishing:
                    sb.Append("• +");
                    sb.Append(levelsPastCap);
                    sb.Append("% Fishing Speed");
                    sb.AppendLine();
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

    private static void AppendBuiltLineWithOptionalBold(StringBuilder sb, string line, string passiveHighlightKey)
    {
        if (!string.IsNullOrEmpty(passiveHighlightKey)
            && line.IndexOf(passiveHighlightKey, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            sb.Append("<b>");
            sb.Append(line);
            sb.Append("</b>");
        }
        else
            sb.Append(line);

        sb.AppendLine();
    }

    private static void AppendFlat(StringBuilder sb, float value, string label, string passiveHighlightKey = null)
    {
        if (value <= 0f) return;
        bool bold = !string.IsNullOrEmpty(passiveHighlightKey)
            && string.Equals(label, passiveHighlightKey, StringComparison.Ordinal);
        if (bold) sb.Append("<b>");
        sb.Append("• +");
        sb.Append(Mathf.RoundToInt(value));
        sb.Append(' ');
        sb.Append(label);
        if (bold) sb.Append("</b>");
        sb.AppendLine();
    }

    private static void AppendPct(StringBuilder sb, float value, string label, string passiveHighlightKey = null)
    {
        if (value <= 0f) return;
        bool bold = !string.IsNullOrEmpty(passiveHighlightKey)
            && string.Equals(label, passiveHighlightKey, StringComparison.Ordinal);
        if (bold) sb.Append("<b>");
        sb.Append("• +");
        sb.Append(Mathf.RoundToInt(value * 100f));
        sb.Append("% ");
        sb.Append(label);
        if (bold) sb.Append("</b>");
        sb.AppendLine();
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

    private static void AppendWoodcuttingLevel15EffectLines(StringBuilder sb, string majorTitle, string enhancementTitle)
    {
        if (string.IsNullOrWhiteSpace(majorTitle))
            return;

        if (string.Equals(majorTitle, "Conservationist", System.StringComparison.OrdinalIgnoreCase))
        {
            int skipChance = 15;
            bool sustainableHarvest = string.Equals(enhancementTitle, "Sustainable Harvest", System.StringComparison.OrdinalIgnoreCase);
            bool ancientPreservation = string.Equals(enhancementTitle, "Ancient Preservation", System.StringComparison.OrdinalIgnoreCase);
            if (ancientPreservation)
                skipChance += 10;
            sb.AppendLine($"     +{skipChance}% Tree Depletion Skip Chance");
            if (sustainableHarvest)
                sb.AppendLine("     +10% Max Stamina restored when tree depletion is skipped");
            return;
        }

        if (string.Equals(majorTitle, "Heavy Swing", System.StringComparison.OrdinalIgnoreCase))
        {
            int extraResourceChance = 15;
            bool controlledForce = string.Equals(enhancementTitle, "Controlled Force", System.StringComparison.OrdinalIgnoreCase);
            bool crushingSwing = string.Equals(enhancementTitle, "Crushing Swing", System.StringComparison.OrdinalIgnoreCase);
            if (crushingSwing)
                extraResourceChance += 5;
            sb.AppendLine($"     +{extraResourceChance}% Extra Resource Chance when Woodcutting Grit procs");
            if (controlledForce)
                sb.AppendLine("     +10% Bonus Find Chance when Woodcutting Grit procs");
            return;
        }

        if (string.Equals(majorTitle, "Flow State", System.StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("     +10% Chopping Speed while Flow is active");
            sb.AppendLine("     +10% Stamina Efficiency while Flow is active");
            if (string.Equals(enhancementTitle, "Lasting Focus", System.StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("     Flow lasts 5 seconds after you stop gathering");
            else if (string.Equals(enhancementTitle, "Deep Focus", System.StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("     +10% Woodcutting Grit Chance while Flow is active");
        }
    }

    private static void AppendFishingLevel15EffectLines(StringBuilder sb, string majorTitle, string enhancementTitle)
    {
        if (string.IsNullOrWhiteSpace(majorTitle))
            return;

        if (string.Equals(majorTitle, "Sustainable Catch", System.StringComparison.OrdinalIgnoreCase))
        {
            int skipChance = 15;
            bool tidalRecovery = string.Equals(enhancementTitle, "Tidal Recovery", System.StringComparison.OrdinalIgnoreCase);
            bool deepRuns = string.Equals(enhancementTitle, "Deep Runs", System.StringComparison.OrdinalIgnoreCase);
            if (deepRuns)
                skipChance += 10;
            sb.AppendLine($"     +{skipChance}% Spot Depletion Skip Chance");
            if (tidalRecovery)
                sb.AppendLine("     +10% Max Stamina restored when a spot depletion skip triggers");
            return;
        }

        if (string.Equals(majorTitle, "Powered Reel", System.StringComparison.OrdinalIgnoreCase))
        {
            int extraFishChance = 15;
            bool tightLine = string.Equals(enhancementTitle, "Tight Line", System.StringComparison.OrdinalIgnoreCase);
            bool doubleHaul = string.Equals(enhancementTitle, "Double Haul", System.StringComparison.OrdinalIgnoreCase);
            if (doubleHaul)
                extraFishChance += 5;
            sb.AppendLine($"     +{extraFishChance}% Extra Fish Chance when Fishing Grit procs");
            if (tightLine)
                sb.AppendLine("     +10% Bonus Find Chance when Fishing Grit procs");
            return;
        }

        if (string.Equals(majorTitle, "Calm Waters", System.StringComparison.OrdinalIgnoreCase))
        {
            int maxStacks = string.Equals(enhancementTitle, "Deep Waters", System.StringComparison.OrdinalIgnoreCase) ? 7 : 5;
            sb.AppendLine($"     Gain Calm stacks every 4s while fishing (max {maxStacks})");
            sb.AppendLine("     +2% Fishing Speed per Calm stack");
            sb.AppendLine("     +1% Bonus Find Chance per Calm stack");
            if (string.Equals(enhancementTitle, "Lasting Waters", System.StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("     Lose 1 Calm stack every 2s after you stop fishing");
        }
    }
}
