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

    private void Awake()
    {
        PreferRuntimeSkillsManager();

        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (abilityListPadding == null)
            abilityListPadding = new RectOffset(0, 0, 0, 0);
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
            rightUnlocksText.text = BuildUnlocksDisplay(_selectedSkill, level, skillsManager);
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
        EnsureCenterTreeReference();
        if (centerSkillTreeView) centerSkillTreeView.SetSkill(_selectedSkill);
        RefreshView();
        RefreshListSelection();
    }

    private void EnsureCenterTreeReference()
    {
        if (centerSkillTreeView) return;

        // Auto-resolve from this UI panel first, then anywhere in scene as fallback.
        centerSkillTreeView = GetComponentInChildren<SkillTreeViewUI>(true);
        if (!centerSkillTreeView)
            centerSkillTreeView = FindFirstObjectByType<SkillTreeViewUI>(FindObjectsInactive.Include);
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

        list.Sort(CompareSkillOrder);
        return list;
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
            if (centerTitleText) centerTitleText.text = "No Skill Selected";
            if (centerSkillTreePlaceholderText) centerSkillTreePlaceholderText.text = "";
            if (centerSkillTreeView) centerSkillTreeView.SetSkill(null);
            if (rightUnlocksText) rightUnlocksText.text = "";
            if (rightAbilitiesText) rightAbilitiesText.text = "";
            return;
        }

        int level = skillsManager ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        string displayName = string.IsNullOrWhiteSpace(_selectedSkill.displayName)
            ? _selectedSkill.skillType.ToString()
            : _selectedSkill.displayName;

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
            rightUnlocksText.text = BuildUnlocksDisplay(_selectedSkill, level, skillsManager);

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
    }

    private void UnhookTreeGlowAcknowledge()
    {
        if (centerSkillTreeView == null)
            return;
        centerSkillTreeView.UnlockGlowAcknowledgedByHover -= HandleTreeGlowAcknowledgedByHover;
    }

    private void HandleTreeGlowAcknowledgedByHover(int unlockLevel)
    {
        if (_selectedSkill == null)
            return;

        if (_pendingTreeGlowLevelsBySkill.TryGetValue(_selectedSkill.skillType, out HashSet<int> levels) && levels != null)
            levels.Remove(unlockLevel);
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

        var abilities = abilityDatabase ? abilityDatabase.GetBySkill(skill.skillType) : new List<AbilityDefinition>();
        if (abilities.Count == 0)
        {
            if (rightAbilitiesText)
                rightAbilitiesText.text = "No abilities yet";
            ClearAbilityRows();
            return;
        }

        int availableInTreeCount = CountTotalAbilitiesAvailableInTree(skill, level);
        int selectedInTreeCount = CountTotalAbilitiesSelectedInTree(skill, level);
        if (rightAbilitiesText)
            rightAbilitiesText.text = $"Abilities available: {availableInTreeCount}\nAbilities selected: {selectedInTreeCount}";

        ClearAbilityRows();

        var tooltip = FindBestSharedTooltip();
        var canvas = GetComponentInParent<Canvas>();
        RectTransform abilityPanelRect = rightAbilitiesText ? rightAbilitiesText.transform.parent as RectTransform : null;

        foreach (var a in abilities)
        {
            if (!a) continue;
            if (!SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skill, a, skillsManager))
                continue;
            var row = CreateAbilityRow(rightAbilitiesListParent);
            row.Bind(a, unlocked: true, tooltip, canvas);
            row.SetTooltipDocking(abilityPanelRect, FlipInsideBounds.PreferredSide.Left);
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

    private int CountTotalAbilitiesAvailableInTree(SkillDefinition skill, int level)
    {
        if (skill == null || skill.unlocks == null || skill.unlocks.Count == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = skill.unlocks[i];
            if (unlock == null || unlock.unlockType != SkillUnlockType.Ability || unlock.ability == null)
                continue;

            if (level >= Mathf.Max(1, unlock.requiredLevel))
                count++;
        }

        return count;
    }

    private int CountTotalAbilitiesSelectedInTree(SkillDefinition skill, int level)
    {
        if (skill == null || skill.unlocks == null || skill.unlocks.Count == 0 || skillsManager == null)
            return 0;

        HashSet<int> abilityRowLevels = new HashSet<int>();
        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = skill.unlocks[i];
            if (unlock == null || unlock.unlockType != SkillUnlockType.Ability || unlock.ability == null)
                continue;

            int rowLevel = Mathf.Max(1, unlock.requiredLevel);
            if (rowLevel <= level)
                abilityRowLevels.Add(rowLevel);
        }

        int selectedRows = 0;
        foreach (int rowLevel in abilityRowLevels)
        {
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

    private static string BuildUnlocksDisplay(SkillDefinition skill, int currentLevel, SkillsManager skillManager)
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
        if (skill.skillType == SkillType.Melee)
        {
            AppendFlat(sb, minMeleeDamage, "Min Melee Damage");
            AppendFlat(sb, maxMeleeDamage, "Max Melee Damage");
            AppendPct(sb, meleeDamage, "Melee Damage");
            AppendPct(sb, meleeAttackSpeed, "Melee Attack Speed");
            AppendPct(sb, meleeMoveSpeed, "Melee Move Speed");
            AppendPct(sb, meleeCritChance, "Melee Crit Chance");
            AppendPct(sb, meleeCritDamage, "Melee Crit Damage");
            AppendPct(sb, bleedChance, "Melee Bleed Chance");
            AppendPct(sb, bleedDamage, "Melee Bleed Multiplier");
            AppendPct(sb, poisonChance, "Melee Poison Chance");
            AppendPct(sb, poisonDuration, "Melee Poison Duration");
            AppendPct(sb, ailmentDamage, "Melee Bleed, Poison, Burn Multipliers");
            AppendPct(sb, shockChance, "Melee Shock Chance");
            AppendPct(sb, vsBleeding, "Melee Damage to Bleeding Enemies");
            AppendPct(sb, vsPoisoned, "Melee Damage to Poisoned Enemies");
            AppendPct(sb, vsShocked, "Melee Damage to Shocked Enemies");
            AppendPct(sb, vsLowHp, "Melee Damage to Low HP Enemies (<35% HP)");
            AppendPct(sb, lifeSteal, "Melee Lifesteal");
        }
        else if (skill.skillType == SkillType.Ranged)
        {
            AppendPct(sb, rangedDamage, "Ranged Damage");
            AppendPct(sb, rangedAttackSpeed, "Ranged Attack Speed");
            AppendPct(sb, rangedCritChance, "Ranged Crit Chance");
            AppendPct(sb, rangedMoveSpeed, "Ranged Move Speed");
        }
        else if (skill.skillType == SkillType.Magic)
        {
            AppendPct(sb, magicDamage, "Magic Damage");
            AppendPct(sb, magicAttackSpeed, "Cast Speed");
            AppendPct(sb, magicCritChance, "Crit Chance");
            AppendPct(sb, magicCritDamage, "Crit Damage");
        }
        else if (skill.skillType == SkillType.Endurance)
        {
            AppendFlat(sb, enduranceArmor, "Armour");
            AppendFlat(sb, enduranceMagicResist, "Magic Resist");
            AppendFlat(sb, enduranceHp, "Max HP");
            AppendFlat(sb, enduranceHpRegen, "HP Regen");
        }
        else if (skill.skillType == SkillType.Mining || skill.skillType == SkillType.Woodcutting || skill.skillType == SkillType.Fishing)
        {
            if (gatherSpeedFlat > 0f)
            {
                sb.Append("• +");
                sb.Append(gatherSpeedFlat.ToString("0.##"));
                sb.Append(" Gathering Speed");
                sb.AppendLine();
            }
            AppendPct(sb, gatherGrit, "Grit");
            AppendPct(sb, gatherEnergyEfficiency, "Energy Efficiency");
            AppendPct(sb, gatherBonusItemChance, "Bonus Item Chance");
        }

        // Major passive conversion summary (currently Melee Lv10 Bloodletting branch).
        if (skill.skillType == SkillType.Melee && currentLevel >= 10)
        {
            sb.AppendLine("• Bloodletting (Major Passive)");
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

        if (sb.Length == 0)
            return "No unlocks yet.";

        return sb.ToString();
    }

    private static void AppendFlat(StringBuilder sb, float value, string label)
    {
        if (value <= 0f) return;
        sb.Append("• +");
        sb.Append(Mathf.RoundToInt(value));
        sb.Append(' ');
        sb.Append(label);
        sb.AppendLine();
    }

    private static void AppendPct(StringBuilder sb, float value, string label)
    {
        if (value <= 0f) return;
        sb.Append("• +");
        sb.Append(Mathf.RoundToInt(value * 100f));
        sb.Append("% ");
        sb.Append(label);
        sb.AppendLine();
    }
}
