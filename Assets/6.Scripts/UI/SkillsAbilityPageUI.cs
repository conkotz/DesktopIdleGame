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

    private void Awake()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;

        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (abilityListPadding == null)
            abilityListPadding = new RectOffset(0, 0, 0, 0);
        if (!abilityDatabase)
            abilityDatabase = AbilityDatabase.LoadDefault();

        ValidateRefsOnce();
        EnsureRightPanelLayoutConfigured();
    }

    private void OnEnable()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!abilityDatabase)
            abilityDatabase = AbilityDatabase.LoadDefault();

        SelectFirstSkillIfNeeded();
        EnsureRightPanelLayoutConfigured();
        RebuildSkillList();
        RefreshView();
        TrySubscribeSkillsEvents();
    }

    private void OnDisable()
    {
        TryUnsubscribeSkillsEvents();
        if (_deferredRefreshRoutine != null)
        {
            StopCoroutine(_deferredRefreshRoutine);
            _deferredRefreshRoutine = null;
        }
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
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager) return;

        skillsManager.OnXpGained += HandleSkillsXpGained;
    }

    private void TryUnsubscribeSkillsEvents()
    {
        if (!skillsManager) return;
        skillsManager.OnXpGained -= HandleSkillsXpGained;
    }

    /// <summary>
    /// Invoked during AddXp before the level-up loop; defer one frame so GetLevel reflects final state.
    /// Covers both XP-only gains and multi-level ups in one call.
    /// </summary>
    private void HandleSkillsXpGained(SkillType type, int amount, string source)
    {
        ScheduleDeferredProgressRefresh();
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
        RefreshView();
        RefreshListSelection();
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

        ClearChildren(gatheringContent);
        ClearChildren(combatContent);

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
            entry.Setup(skill, level, progress01, selected, OnSkillEntryClicked);
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
        if (_selectedSkill == null)
        {
            if (centerTitleText) centerTitleText.text = "No Skill Selected";
            if (centerSkillTreePlaceholderText) centerSkillTreePlaceholderText.text = "";
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
            centerSkillTreePlaceholderText.text = "Skill tree\n(placeholder — layout only)";

        if (rightUnlocksText)
            rightUnlocksText.text = BuildUnlocksDisplay(_selectedSkill, level);

        RefreshAbilitiesPanel(_selectedSkill, level);
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

        int unlockedCount = 0;
        for (int i = 0; i < abilities.Count; i++)
        {
            var ability = abilities[i];
            if (ability != null && level >= Mathf.Max(1, ability.unlockLevel))
                unlockedCount++;
        }

        if (rightAbilitiesText)
            rightAbilitiesText.text = $"Abilities unlocked: {unlockedCount}/{abilities.Count}";

        ClearAbilityRows();

        var tooltip = FindBestSharedTooltip();
        var canvas = GetComponentInParent<Canvas>();
        RectTransform abilityPanelRect = rightAbilitiesText ? rightAbilitiesText.transform.parent as RectTransform : null;

        foreach (var a in abilities)
        {
            if (!a) continue;
            bool unlocked = level >= Mathf.Max(1, a.unlockLevel);
            var row = CreateAbilityRow(rightAbilitiesListParent);
            row.Bind(a, unlocked, tooltip, canvas);
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

    private static string BuildUnlocksDisplay(SkillDefinition skill, int currentLevel)
    {
        if (skill == null || skill.unlocks == null || skill.unlocks.Count == 0)
            return "No unlocks yet.";

        var sb = new StringBuilder();
        foreach (var unlock in skill.unlocks)
        {
            if (unlock == null) continue;

            bool unlocked = currentLevel >= unlock.requiredLevel;
            sb.Append(unlocked ? "• " : "🔒 ");
            sb.Append($"Lv {unlock.requiredLevel} - {unlock.title}");

            if (!string.IsNullOrWhiteSpace(unlock.description))
            {
                sb.Append(": ");
                sb.Append(unlock.description);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
