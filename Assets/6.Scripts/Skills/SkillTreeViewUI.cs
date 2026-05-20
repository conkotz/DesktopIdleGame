using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillTreeViewUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform nodesRoot;
    [SerializeField] private RectTransform connectorsRoot;
    [SerializeField] private RectTransform levelsRoot;
    [SerializeField] private TMP_Text levelRowLabelPrefab;
    [Tooltip("Optional right-side mirror of level labels (e.g. Unlock / Ability / Major Passive / Capstone). Same Y as each Lv row.")]
    [SerializeField] private TMP_Text levelTierRowLabelPrefab;
    [Tooltip(
        "Wide rect for tier captions (e.g. SkillTreeRoot). If empty, uses Levels Root’s parent so labels sit on the panel’s right edge, not inside the narrow Lv column.")]
    [SerializeField] private RectTransform levelTierRowLabelsRoot;
    [SerializeField] private float levelTierLabelRightInset = 22f;
    [SerializeField] private float levelTierLabelExtraRightPaddingPx = 10f;
    [SerializeField] private SkillTreeNodeUI nodePrefab;
    [SerializeField] private SkillTreeConnectorUI connectorPrefab;
    [SerializeField] private SharedTooltipUI sharedTooltip;
    [SerializeField] private RectTransform tooltipBoundsRect;

    [Header("Data")]
    [SerializeField] private SkillDefinition selectedSkill;
    [SerializeField] private SkillsManager skillsManager;
    [Tooltip("Optional; found at runtime if unset. Used for ability cooldown overlay and input lock.")]
    [SerializeField] private PlayerAbilityController abilityController;
    [Tooltip("Optional one-line hint: Tier 1–5 gates at skill L1 / L10 / L20 / L30 / L50. Leave empty to hide.")]
    [SerializeField] private TMP_Text equipmentTierHint;
    [Tooltip("When true, stop rendering rows after the first invalid/missing unlock row.")]
    [SerializeField] private bool stopAfterFirstMissingUnlock = true;

    [Header("Layout")]
    [SerializeField] private float startY = -48f;
    [SerializeField] private float rowGap = 20f;
    [SerializeField] private float choiceOffsetX = 210f;
    [SerializeField] private float choiceYOffset = -80f;
    [SerializeField] private float capstoneChoiceOffsetX = 210f;
    [SerializeField] private float capstoneChoiceYOffset = 0f;
    [Tooltip("Extra Y offset for choices that use an explicit requiredLevel (eg. Lv8). 0 keeps them exactly on that row.")]
    [SerializeField] private float explicitChoiceRowYOffset = 0f;
    [SerializeField] private bool showAllLevelLabels = true;
    [Tooltip("Left column (combat / non-gathering skills): show Lv labels every N levels, plus Lv1. 0 = show a label for every tier. Gathering skills (woodcutting, fishing, mining) always use Lv1, Lv5, Lv10, … Lv50.")]
    [SerializeField] private int levelLabelShowEveryNLevels = 5;
    [Tooltip("Horizontal gap between nodes that share the same required level (e.g. major passive + unlock).")]
    [SerializeField] private float sameLevelNodeGap = 28f;
    [Tooltip(
        "Horizontal distance from the spine column (x = 0) to the **center** of the Minor Unlock nearest the spine. " +
        "Minor Unlock nodes use this fixed column so they line up across rows regardless of neighbouring spine node width.")]
    [SerializeField] private float minorUnlockSpineOffsetPixels = 72f;
    [Tooltip("Center-to-center spacing for multiple Ability unlocks at the same level (symmetric around the vertical spine).")]
    [SerializeField] private float abilitySiblingSpacing = 140f;

    private readonly List<SkillTreeNodeUI> spawnedNodes = new();
    private readonly List<SkillTreeConnectorUI> spawnedConnectors = new();
    private readonly List<TMP_Text> spawnedLevelLabels = new();
    private readonly List<TMP_Text> spawnedTierRowLabels = new();
    private readonly Dictionary<string, SkillTreeNodeUI> nodeLookup = new();
    private readonly Dictionary<string, int> unlockLevelByNodeId = new();
    private readonly Dictionary<int, float> rowYByLevel = new();
    private readonly Dictionary<string, string> tooltipTitleByNodeId = new();
    private readonly Dictionary<string, string> tooltipBodyByNodeId = new();
    // Legacy: previously used TryGetNodeLevel(nodeId) which doesn't work for choice nodes (id begins with parent spine id).
    // Kept only to avoid noisy diffs; new highlight uses unlockLevelByNodeId.
    private readonly Dictionary<string, int> nodeLevelById = new();
    private readonly Dictionary<string, RowDef> rowDefBySpineNodeId = new();
    private readonly List<float> layoutRowY = new();
    private readonly List<float> layoutRowX = new();
    private readonly Dictionary<string, ChoiceNodeMeta> choiceMetaByNodeId = new();
    private readonly HashSet<int> expandedChoiceBranchesBySourceLevel = new();
    private readonly List<ChoiceBranchConnectorRecord> choiceBranchConnectors = new();
    private readonly Dictionary<string, float> spineLayoutXBySpineId = new();
    private readonly Dictionary<string, AbilityTierPickMeta> abilityTierPickMetaBySpineId = new();
    private readonly List<InterTierVerticalRecord> interTierVerticalConnectors = new();
    private readonly List<RowDef> layoutRowsCache = new();

    /// <summary>Scales serialized layout distances to match <see cref="SkillTreeNodeUI.NodeVisualScale"/>.</summary>
    private static float ScaledLayout(float value) => value * SkillTreeNodeUI.NodeVisualScale;

    /// <summary>Max authored enhancements per unlock (skill tree layout is tuned for up to four).</summary>
    public const int MaxEnhancementChoicesSupported = 4;

    // Left → right slot order: 2 = [1,2]; 3 = [1,2,3] off-spine; 4 = [3,1,2,4] off-spine.
    private static readonly int[] EnhancementLayoutOrderTwo = { 0, 1 };
    private static readonly int[] EnhancementLayoutOrderThree = { 0, 1, 2 };
    private static readonly int[] EnhancementLayoutOrderFour = { 2, 0, 1, 3 };

    private SkillTreeNodeUI selectedNode;
    private SkillDefinition _lastBuiltSkill;
    private readonly HashSet<int> _pendingUnlockGlowLevels = new();
    private readonly List<string> _abilityGroupSpineScratch = new();

    public event System.Action<int> UnlockGlowAcknowledgedByHover;

    /// <summary>Invoked with a passive-list highlight key (see <see cref="PassiveUnlocksLineHighlight"/>) or null to clear.</summary>
    public event System.Action<string> PassiveUnlockLineHighlightChanged;

    private readonly struct AbilityTierPickMeta
    {
        public readonly int level;
        public readonly int ordinal;
        public readonly int groupSize;

        public AbilityTierPickMeta(int level, int ordinal, int groupSize)
        {
            this.level = level;
            this.ordinal = ordinal;
            this.groupSize = groupSize;
        }
    }

    private struct InterTierVerticalRecord
    {
        public SkillTreeConnectorUI conn;
        public int upperLevel;
        public int lowerLevel;
        /// <summary>When set, render between these explicit spine ids and skip anchor retargeting. Visibility still ties to endpoint nodes being active.</summary>
        public string explicitFromId;
        public string explicitToId;
    }

    private readonly struct ChoiceBranchConnectorRecord
    {
        public readonly string parentSpineId;
        public readonly string choiceNodeId;
        public readonly int sourceLevel;
        public readonly SkillTreeConnectorUI conn;

        public ChoiceBranchConnectorRecord(string parentSpineId, string choiceNodeId, int sourceLevel, SkillTreeConnectorUI conn)
        {
            this.parentSpineId = parentSpineId;
            this.choiceNodeId = choiceNodeId;
            this.sourceLevel = sourceLevel;
            this.conn = conn;
        }
    }

    private readonly struct ChoiceNodeMeta
    {
        public readonly string parentSpineNodeId;
        public readonly int sourceLevel;
        public readonly int choiceIndex;
        public readonly int unlockLevel;
        public readonly float offsetXFromParent;
        public readonly float anchorY;

        public ChoiceNodeMeta(string parentSpineNodeId, int sourceLevel, int choiceIndex, int unlockLevel, float offsetXFromParent, float anchorY)
        {
            this.parentSpineNodeId = parentSpineNodeId;
            this.sourceLevel = sourceLevel;
            this.choiceIndex = choiceIndex;
            this.unlockLevel = unlockLevel;
            this.offsetXFromParent = offsetXFromParent;
            this.anchorY = anchorY;
        }
    }

    private readonly struct RowDef
    {
        public readonly int level;
        public readonly int slotAtLevel;
        public readonly SkillUnlockDefinition unlock;
        public readonly SkillTreeNodeVisualType type;
        public readonly int choiceCount;

        public RowDef(int level, int slotAtLevel, SkillUnlockDefinition unlock, SkillTreeNodeVisualType type, int choiceCount)
        {
            this.level = level;
            this.slotAtLevel = slotAtLevel;
            this.unlock = unlock;
            this.type = type;
            this.choiceCount = Mathf.Max(0, choiceCount);
        }
    }

    private void Start()
    {
        PreferRuntimeSkillsManager();
        // SkillsAbilitiesPageUI (and others) call SetSkill in OnEnable before this Start runs; do not rebuild from
        // serialized selectedSkill and wipe the page-driven tree / ability rows on first open.
        if (_lastBuiltSkill == null)
            BuildForSelectedSkill();
    }

    public void SetSkill(SkillDefinition skill)
    {
        selectedSkill = skill;
        BuildForSelectedSkill();
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            skillsManager = SkillsManager.Instance;
        else if (!skillsManager)
            skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }

    private void ResolveAbilityController()
    {
        if (abilityController != null)
            return;
        abilityController = FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);
    }

    private string ResolveCooldownAbilityId(RowDef row)
    {
        if (selectedSkill == null || skillsManager == null || row.unlock == null || row.unlock.ability == null)
            return null;

        string spine = SpineNodeId(row);
        int enh = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, spine, -1);
        List<SkillChoiceDefinition> choices = GetNonNullChoices(row.unlock);
        if (enh >= 0 && enh < choices.Count)
        {
            SkillChoiceDefinition ch = choices[enh];
            if (ch != null && ch.ability != null && !string.IsNullOrWhiteSpace(ch.ability.abilityId))
                return ch.ability.abilityId;
        }

        return row.unlock.ability.abilityId;
    }

    private bool TryGetCommittedAbilityRowDefAtLevel(int level, out RowDef pickedRow)
    {
        pickedRow = default;
        if (selectedSkill == null || skillsManager == null)
            return false;

        int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, level, -1);
        if (pick < 0)
            return false;

        foreach (var kv in abilityTierPickMetaBySpineId)
        {
            AbilityTierPickMeta m = kv.Value;
            if (m.level != level || m.ordinal != pick)
                continue;
            if (!rowDefBySpineNodeId.TryGetValue(kv.Key, out pickedRow))
                return false;
            return pickedRow.unlock != null && pickedRow.unlock.ability != null;
        }

        return false;
    }

    private bool IsRowLevelAbilityCooldownActive(int level)
    {
        ResolveAbilityController();
        if (selectedSkill == null || skillsManager == null || abilityController == null)
            return false;

        if (!TryGetCommittedAbilityRowDefAtLevel(level, out RowDef pickedRow))
            return false;

        string id = ResolveCooldownAbilityId(pickedRow);
        return !string.IsNullOrEmpty(id) && abilityController.IsOnCooldown(id, out _);
    }

    private bool IsRowLevelAbilityCooldownActiveForParentSpine(string parentSpineNodeId)
    {
        if (string.IsNullOrWhiteSpace(parentSpineNodeId) || !rowDefBySpineNodeId.TryGetValue(parentSpineNodeId, out RowDef row))
            return false;
        return IsRowLevelAbilityCooldownActive(row.level);
    }

    private bool ShouldBlockRightClickResetForSpine(string spineTarget)
    {
        ResolveAbilityController();
        if (selectedSkill == null || skillsManager == null || abilityController == null ||
            string.IsNullOrWhiteSpace(spineTarget))
            return false;

        return abilityTierPickMetaBySpineId.TryGetValue(spineTarget, out AbilityTierPickMeta m)
            && IsRowLevelAbilityCooldownActive(m.level);
    }

    private const float SkillUnlearnCooldownLogIntervalSeconds = 10f;
    private const string SkillUnlearnCooldownLogMessage = "must wait for skill too be off cooldown";
    private float _lastSkillUnlearnCooldownLogUnscaledTime = -999f;

    private void TryLogSkillUnlearnCooldownBlocked()
    {
        if (Time.unscaledTime - _lastSkillUnlearnCooldownLogUnscaledTime < SkillUnlearnCooldownLogIntervalSeconds)
            return;

        _lastSkillUnlearnCooldownLogUnscaledTime = Time.unscaledTime;
        GameLog.Add(SkillUnlearnCooldownLogMessage, GameLog.CannotMessageColor);
    }

    private void TryExpireCommittedAbilityLingeringStateForSkillTreeReset(string spineTarget)
    {
        ResolveAbilityController();
        if (abilityController == null || selectedSkill == null || skillsManager == null)
            return;
        if (!abilityTierPickMetaBySpineId.TryGetValue(spineTarget, out AbilityTierPickMeta m))
            return;
        if (!TryGetCommittedAbilityRowDefAtLevel(m.level, out RowDef pickedRow))
            return;

        string aid = ResolveCooldownAbilityId(pickedRow);
        if (string.IsNullOrEmpty(aid))
            return;

        if (abilityController.IsAbilityBuffOrLingeringActive(aid))
            abilityController.ForceEndLingeringAbilityForSkillTreeReset(aid);
    }

    private void RefreshSkillTreeAbilityStatePresentation()
    {
        ResolveAbilityController();

        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            if (spawnedNodes[i] == null)
                continue;
            spawnedNodes[i].SetSkillTreeCooldownPresentation(false, 0f, 0f);
            spawnedNodes[i].SetSkillTreeActiveBuffPresentation(false, 0f);
        }

        if (abilityController == null || selectedSkill == null || skillsManager == null)
            return;

        foreach (var kv in abilityTierPickMetaBySpineId)
        {
            if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                continue;

            AbilityTierPickMeta m = kv.Value;
            bool collapsed = IsAbilityTierCollapsed(m.level);
            int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, m.level, -1);
            bool committedThis = pick == m.ordinal && (m.groupSize < 2 || collapsed);
            if (!committedThis || !rowDefBySpineNodeId.TryGetValue(kv.Key, out RowDef row))
                continue;

            string aid = ResolveCooldownAbilityId(row);
            if (string.IsNullOrEmpty(aid))
                continue;

            if (abilityController.IsOnCooldown(aid, out float cdRem))
            {
                float norm = abilityController.GetCooldownNormalized(aid);
                node.SetSkillTreeCooldownPresentation(true, norm, cdRem);
                continue;
            }

            if (abilityController.TryGetAbilitySkillTreeActiveBuffTimer(aid, out float buffRem))
                node.SetSkillTreeActiveBuffPresentation(true, buffRem);
        }

        foreach (var kv in choiceMetaByNodeId)
        {
            if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                continue;

            ChoiceNodeMeta meta = kv.Value;
            int sel = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, meta.parentSpineNodeId, -1);
            if (sel != meta.choiceIndex)
                continue;

            if (!rowDefBySpineNodeId.TryGetValue(meta.parentSpineNodeId, out RowDef parentRow))
                continue;

            string aid = ResolveCooldownAbilityId(parentRow);
            if (string.IsNullOrEmpty(aid))
                continue;

            if (abilityController.IsOnCooldown(aid, out float cdRem))
            {
                float norm = abilityController.GetCooldownNormalized(aid);
                node.SetSkillTreeCooldownPresentation(true, norm, cdRem);
                continue;
            }

            if (abilityController.TryGetAbilitySkillTreeActiveBuffTimer(aid, out float buffRem))
                node.SetSkillTreeActiveBuffPresentation(true, buffRem);
        }
    }

    private float _nextSkillTreePresentationRefreshTime;
    /// <summary>True after a refresh that showed cooldown/buff overlays; one more pass clears them when timers end.</summary>
    private bool _lastRefreshHadActivePresentation;

    /// <summary>True when a committed ability on this tree has a live cooldown or buff timer.</summary>
    private bool AnyCommittedAbilityHasLivePresentation()
    {
        ResolveAbilityController();
        if (abilityController == null || selectedSkill == null || skillsManager == null)
            return false;

        foreach (var kv in abilityTierPickMetaBySpineId)
        {
            AbilityTierPickMeta m = kv.Value;
            bool collapsed = IsAbilityTierCollapsed(m.level);
            int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, m.level, -1);
            bool committedThis = pick == m.ordinal && (m.groupSize < 2 || collapsed);
            if (!committedThis || !rowDefBySpineNodeId.TryGetValue(kv.Key, out RowDef row))
                continue;

            string aid = ResolveCooldownAbilityId(row);
            if (string.IsNullOrEmpty(aid))
                continue;

            if (abilityController.IsOnCooldown(aid, out _) ||
                abilityController.IsAbilityBuffOrLingeringActive(aid))
                return true;
        }

        foreach (var kv in choiceMetaByNodeId)
        {
            ChoiceNodeMeta meta = kv.Value;
            int sel = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, meta.parentSpineNodeId, -1);
            if (sel != meta.choiceIndex)
                continue;

            if (!rowDefBySpineNodeId.TryGetValue(meta.parentSpineNodeId, out RowDef parentRow))
                continue;

            string aid = ResolveCooldownAbilityId(parentRow);
            if (string.IsNullOrEmpty(aid))
                continue;

            if (abilityController.IsOnCooldown(aid, out _) ||
                abilityController.IsAbilityBuffOrLingeringActive(aid))
                return true;
        }

        return false;
    }

    private void Update()
    {
        if (!isActiveAndEnabled || spawnedNodes.Count == 0)
            return;

        bool hasLivePresentation = AnyCommittedAbilityHasLivePresentation();
        if (!hasLivePresentation && !_lastRefreshHadActivePresentation)
            return;

        if (Time.unscaledTime < _nextSkillTreePresentationRefreshTime)
            return;

        _nextSkillTreePresentationRefreshTime = Time.unscaledTime + 0.1f;
        RefreshSkillTreeAbilityStatePresentation();
        _lastRefreshHadActivePresentation = hasLivePresentation;
    }

    public void BuildForSelectedSkill()
    {
        ClearTree();
        RefreshEquipmentTierHint();

        if (selectedSkill == null || selectedSkill.unlocks == null || selectedSkill.unlocks.Count == 0)
            return;
        PreferRuntimeSkillsManager();
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;

        int currentSkillLevel = skillsManager ? skillsManager.GetLevel(selectedSkill.skillType) : 1;

        List<RowDef> rows = BuildRows(selectedSkill.unlocks);
        if (rows.Count == 0)
            return;

        layoutRowsCache.Clear();
        layoutRowsCache.AddRange(rows);

        ComputeTierLayout(rows);
        SpawnLevelColumnLabels(rows);
        SpawnRows(rows, currentSkillLevel);
        SpawnConnectors(rows);
        RefreshAbilityTierLayoutAndVisibility();
        RefreshChoiceBranchVisibility();
        ApplyPendingUnlockGlowLevels();
        _lastBuiltSkill = selectedSkill;
        RefreshSkillTreeAbilityStatePresentation();
        _lastRefreshHadActivePresentation = AnyCommittedAbilityHasLivePresentation();
    }

    /// <summary>
    /// Pulses the newly-unlocked nodes for a specific unlock level (clears when hovered).
    /// </summary>
    public void HighlightNewUnlocksAtLevel(int unlockLevel)
    {
        if (unlockLevelByNodeId.Count == 0 || nodeLookup.Count == 0)
            return;

        _pendingUnlockGlowLevels.Add(unlockLevel);

        foreach (var kv in unlockLevelByNodeId)
        {
            if (kv.Value != unlockLevel)
                continue;
            if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                continue;
            if (!node.gameObject.activeInHierarchy)
                continue;
            node.ShowUnlockGlow();
        }
    }

    public void ClearPendingUnlockGlowLevel(int unlockLevel)
    {
        _pendingUnlockGlowLevels.Remove(unlockLevel);
    }

    public void SetPendingUnlockGlowLevels(IEnumerable<int> unlockLevels)
    {
        _pendingUnlockGlowLevels.Clear();
        if (unlockLevels == null)
            return;

        foreach (int lvl in unlockLevels)
            _pendingUnlockGlowLevels.Add(lvl);
    }

    /// <summary>
    /// Updates lock state and tooltip strings without destroying nodes. Use when only the character's skill level changed
    /// (full rebuild breaks hover tooltips if the pointer never exits destroyed nodes).
    /// </summary>
    public bool RefreshProgressIfSameSkill(SkillDefinition skill, int currentSkillLevel)
    {
        if (skill == null || selectedSkill != skill || skill != _lastBuiltSkill || nodeLookup.Count == 0)
            return false;

        PreferRuntimeSkillsManager();

        sharedTooltip?.Hide();

        foreach (var kv in nodeLookup)
        {
            string nodeId = kv.Key;
            SkillTreeNodeUI nodeUi = kv.Value;
            if (nodeUi == null)
                continue;

            if (rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef row))
            {
                bool unlocked = row.level <= currentSkillLevel;
                BuildTooltipCopy(row, unlocked, out string mainTitle, out string mainBody);
                tooltipTitleByNodeId[nodeId] = string.IsNullOrWhiteSpace(mainTitle) ? "Node" : mainTitle;
                tooltipBodyByNodeId[nodeId] = mainBody ?? string.Empty;
                nodeUi.SetLocked(!unlocked);
            }
            else if (choiceMetaByNodeId.TryGetValue(nodeId, out ChoiceNodeMeta cm))
            {
                if (!rowDefBySpineNodeId.TryGetValue(cm.parentSpineNodeId, out RowDef parentRow))
                    continue;

                List<SkillChoiceDefinition> choices = GetNonNullChoices(parentRow.unlock);
                SkillChoiceDefinition choice = cm.choiceIndex >= 0 && cm.choiceIndex < choices.Count
                    ? choices[cm.choiceIndex]
                    : null;
                bool unlocked = parentRow.level <= currentSkillLevel && cm.unlockLevel <= currentSkillLevel;
                BuildChoiceTooltipCopy(cm.unlockLevel, choice, parentRow, unlocked, out string cTitle, out string cBody);
                tooltipTitleByNodeId[nodeId] = string.IsNullOrWhiteSpace(cTitle) ? "Node" : cTitle;
                tooltipBodyByNodeId[nodeId] = cBody ?? string.Empty;
                nodeUi.SetLocked(!unlocked);
            }
        }

        RefreshChoiceSelectionVisuals();
        RefreshAbilityTierLayoutAndVisibility();
        RefreshChoiceBranchVisibility();
        return true;
    }

    /// <summary>
    /// Call after <see cref="SkillsManager.ClearSkillAbilityRowPicksForSkill"/> / <see cref="SkillsManager.ClearAllSkillAbilityRowPicks"/> to refresh this tree.
    /// </summary>
    public void RebuildAfterAbilityRowReset()
    {
        BuildForSelectedSkill();
    }

    /// <summary>Clears committed ability picks for the skill currently shown in this tree, then rebuilds.</summary>
    public void ResetAbilityRowPicksForCurrentSkillAndRebuild()
    {
        PreferRuntimeSkillsManager();
        if (selectedSkill != null && skillsManager)
            skillsManager.ClearSkillAbilityRowPicksForSkill(selectedSkill.skillType);
        RebuildAfterAbilityRowReset();
    }

    /// <summary>
    /// Some prefabs / Inspector entries use this shorter name by mistake — forwards to <see cref="OnResetSkillTreeButtonClicked"/>.
    /// </summary>
    public void OnResetSkillTreeButtonClick() => OnResetSkillTreeButtonClicked();

    /// <summary>
    /// Assign to the Reset Tree button OnClick: clears choice branches and multi-ability row picks for the <b>currently shown</b> skill only, then rebuilds this tree.
    /// </summary>
    public void OnResetSkillTreeButtonClicked()
    {
        PreferRuntimeSkillsManager();
        if (skillsManager == null)
        {
            Debug.LogWarning(
                "[SkillTreeViewUI] Reset Tree: no SkillsManager found (Instance is null and none in scene). Progression will not clear until a SkillsManager exists.",
                this);
        }
        else if (selectedSkill != null)
        {
            skillsManager.ResetSkillTreeSelectionsForSkill(selectedSkill.skillType);
        }
        else
            Debug.LogWarning("[SkillTreeViewUI] Reset Tree: no skill selected on this tree; nothing to reset.", this);

        expandedChoiceBranchesBySourceLevel.Clear();
        BuildForSelectedSkill();
    }

    private List<RowDef> BuildRows(List<SkillUnlockDefinition> unlocks)
    {
        var sorted = new List<(SkillUnlockDefinition u, int origIdx)>();
        for (int i = 0; i < unlocks.Count; i++)
            if (unlocks[i] != null)
                sorted.Add((unlocks[i], i));

        sorted.Sort((a, b) =>
        {
            int c = a.u.requiredLevel.CompareTo(b.u.requiredLevel);
            if (c != 0) return c;
            c = TierHorizontalSortOrder(a.u.unlockType).CompareTo(TierHorizontalSortOrder(b.u.unlockType));
            if (c != 0) return c;
            return a.origIdx.CompareTo(b.origIdx);
        });

        var rows = new List<RowDef>(sorted.Count);
        var slotAtLevel = new Dictionary<int, int>();

        for (int i = 0; i < sorted.Count; i++)
        {
            SkillUnlockDefinition u = sorted[i].u;
            if (u == null || u.requiredLevel <= 0)
            {
                if (stopAfterFirstMissingUnlock) break;
                continue;
            }

            int lvl = u.requiredLevel;
            int slot = slotAtLevel.TryGetValue(lvl, out int s) ? s : 0;
            slotAtLevel[lvl] = slot + 1;

            SkillTreeNodeVisualType visual = MapUnlockToNodeType(u);
            int choices = GetChoiceCount(u);
            rows.Add(new RowDef(lvl, slot, u, visual, choices));
        }

        return rows;
    }

    /// <summary>Lower value = further left when multiple unlocks share the same required level.</summary>
    private static int TierHorizontalSortOrder(SkillUnlockType t)
    {
        return t switch
        {
            SkillUnlockType.MinorUnlock => 0,
            SkillUnlockType.MinorPassive => 1,
            SkillUnlockType.MajorPassive => 2,
            SkillUnlockType.Unlock => 3,
            SkillUnlockType.Ability => 4,
            SkillUnlockType.CapstonePassive => 5,
            _ => 99
        };
    }

    private static SkillTreeNodeVisualType MapUnlockToNodeType(SkillUnlockDefinition u)
    {
        return u.unlockType switch
        {
            SkillUnlockType.MinorUnlock => SkillTreeNodeVisualType.MinorUnlock,
            SkillUnlockType.MinorPassive => SkillTreeNodeVisualType.MinorPassive,
            SkillUnlockType.MajorPassive => SkillTreeNodeVisualType.MajorPassive,
            SkillUnlockType.Unlock => SkillTreeNodeVisualType.Unlock,
            SkillUnlockType.Ability => SkillTreeNodeVisualType.Ability,
            SkillUnlockType.CapstonePassive => SkillTreeNodeVisualType.CapstonePassive,
            _ => SkillTreeNodeVisualType.MinorPassive
        };
    }

    private static int GetChoiceCount(SkillUnlockDefinition unlock)
    {
        if (unlock == null || unlock.choices == null)
            return 0;

        int count = 0;
        for (int i = 0; i < unlock.choices.Count; i++)
            if (unlock.choices[i] != null)
                count++;
        return count;
    }

    private void ComputeTierLayout(List<RowDef> rows)
    {
        rowYByLevel.Clear();
        layoutRowY.Clear();
        layoutRowX.Clear();

        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            float y;
            if (i == 0)
            {
                y = startY;
            }
            else if (rows[i].level == rows[i - 1].level)
            {
                y = layoutRowY[i - 1];
            }
            else
            {
                RowDef prev = rows[i - 1];
                float prevHalf = SkillTreeNodeUI.GetVisualBoxSize(prev.type).y * 0.5f;
                // Use the same vertical anchor as connectors (skip lateral MinorUnlock when a spine passive exists).
                // Otherwise the first row of a tier (often MinorUnlock before MinorPassive in sort order) inflates Y spacing.
                int tierEnd = i;
                while (tierEnd + 1 < rows.Count && rows[tierEnd + 1].level == rows[i].level)
                    tierEnd++;
                int anchorIdx = TierVerticalAnchorIndex(rows, i, tierEnd);
                float lowerHalf = SkillTreeNodeUI.GetVisualBoxSize(rows[anchorIdx].type).y * 0.5f;
                y = layoutRowY[i - 1] - prevHalf - Mathf.Max(0f, ScaledLayout(rowGap)) - lowerHalf;
            }

            layoutRowY.Add(y);
            rowYByLevel[row.level] = y;
        }

        for (int i = 0; i < rows.Count; i++)
            layoutRowX.Add(0f);

        int g = 0;
        while (g < rows.Count)
        {
            int end = g;
            while (end + 1 < rows.Count && rows[end + 1].level == rows[g].level)
                end++;

            if (TierIsMultiAbilityOnly(rows, g, end))
            {
                int n = end - g + 1;
                float step = Mathf.Max(1f, ScaledLayout(abilitySiblingSpacing));
                for (int k = 0; k < n; k++)
                {
                    float t = k - (n - 1) * 0.5f;
                    layoutRowX[g + k] = t * step;
                }
            }
            else
            {
                var spineIdx = new System.Collections.Generic.List<int>(end - g + 1);
                for (int i = g; i <= end; i++)
                {
                    if (rows[i].type != SkillTreeNodeVisualType.MinorUnlock)
                        spineIdx.Add(i);
                }

                if (spineIdx.Count == 0)
                {
                    for (int i = g; i <= end; i++)
                        layoutRowX[i] = 0f;
                }
                else
                {
                    float xPos = 0f;
                    for (int j = 0; j < spineIdx.Count; j++)
                    {
                        int i = spineIdx[j];
                        layoutRowX[i] = xPos;
                        if (j + 1 < spineIdx.Count)
                        {
                            int i2 = spineIdx[j + 1];
                            float halfA = SkillTreeNodeUI.GetVisualBoxSize(rows[i].type).x * 0.5f;
                            float halfB = SkillTreeNodeUI.GetVisualBoxSize(rows[i2].type).x * 0.5f;
                            xPos += halfA + Mathf.Max(0f, ScaledLayout(sameLevelNodeGap)) + halfB;
                        }
                    }

                    float gap = Mathf.Max(0f, ScaledLayout(sameLevelNodeGap));

                    var mus = new System.Collections.Generic.List<int>();
                    for (int i = g; i <= end; i++)
                    {
                        if (rows[i].type == SkillTreeNodeVisualType.MinorUnlock)
                            mus.Add(i);
                    }

                    // Fixed column from spine (x = 0): do not derive from the leftmost spine node's width.
                    float columnAnchorX = -ScaledLayout(minorUnlockSpineOffsetPixels);
                    float cx = columnAnchorX;
                    for (int m = 0; m < mus.Count; m++)
                    {
                        int i = mus[m];
                        float halfMu = SkillTreeNodeUI.GetVisualBoxSize(rows[i].type).x * 0.5f;
                        layoutRowX[i] = cx;
                        cx -= 2f * halfMu + gap;
                    }
                }
            }

            g = end + 1;
        }
    }

    /// <summary>
    /// True when a tier contains 2+ rows that are all the same multi-stack-eligible type
    /// (Ability or MajorPassive). Drives the horizontal sibling spacing + row-pick collapse
    /// shared between ability tiers and multi-major-passive tiers (e.g. Woodcutting Lv15).
    /// </summary>
    private static bool TierIsMultiAbilityOnly(List<RowDef> rows, int tierStart, int tierEndInclusive)
    {
        int count = tierEndInclusive - tierStart + 1;
        if (count < 2)
            return false;
        SkillTreeNodeVisualType first = rows[tierStart].type;
        if (!IsMultiStackEligibleType(first))
            return false;
        for (int i = tierStart + 1; i <= tierEndInclusive; i++)
        {
            if (rows[i].type != first)
                return false;
        }
        return true;
    }

    private static bool IsMultiStackEligibleType(SkillTreeNodeVisualType type)
    {
        return type == SkillTreeNodeVisualType.Ability || type == SkillTreeNodeVisualType.MajorPassive;
    }

    private static int TierVerticalAnchorIndex(List<RowDef> rows, int tierStart, int tierEndInclusive)
    {
        int count = tierEndInclusive - tierStart + 1;
        if (TierIsMultiAbilityOnly(rows, tierStart, tierEndInclusive))
            return tierStart + (count - 1) / 2;

        for (int i = tierStart; i <= tierEndInclusive; i++)
        {
            if (rows[i].type != SkillTreeNodeVisualType.MinorUnlock)
                return i;
        }

        return tierStart;
    }

    private static bool IsGatheringSkillSelected(SkillDefinition skill) =>
        skill != null &&
        (skill.skillType == SkillType.Woodcutting ||
         skill.skillType == SkillType.Fishing ||
         skill.skillType == SkillType.Mining);

    /// <summary>
    /// Gathering skills always show Lv1, Lv5, Lv10, … Lv50 on the left. Other skills use <see cref="levelLabelShowEveryNLevels"/>.
    /// </summary>
    private bool ShouldShowLeftLevelNumber(int tierLevel)
    {
        if (IsGatheringSkillSelected(selectedSkill))
            return tierLevel == 1 || tierLevel % 5 == 0;

        if (levelLabelShowEveryNLevels <= 0)
            return true;

        int n = Mathf.Max(1, levelLabelShowEveryNLevels);
        return tierLevel == 1 || tierLevel % n == 0;
    }

    private void SpawnLevelColumnLabels(List<RowDef> rows)
    {
        if (levelsRoot == null || levelRowLabelPrefab == null)
            return;

        int idx = 0;
        while (idx < rows.Count)
        {
            int tierStart = idx;
            int tierLevel = rows[tierStart].level;
            while (idx < rows.Count && rows[idx].level == tierLevel)
                idx++;

            int labelIndex = tierStart;
            if (!showAllLevelLabels)
            {
                while (labelIndex < idx &&
                       (rows[labelIndex].type == SkillTreeNodeVisualType.MinorPassive ||
                        rows[labelIndex].type == SkillTreeNodeVisualType.MinorUnlock))
                    labelIndex++;
                // Tiers that are only minor passives (common on gathering “Skip” levels) still need Lv5/Lv10/… labels.
                if (labelIndex >= idx && !ShouldShowLeftLevelNumber(tierLevel))
                    continue;
                if (labelIndex >= idx)
                    labelIndex = tierStart;
            }

            if (ShouldShowLeftLevelNumber(tierLevel))
            {
                var t = Instantiate(levelRowLabelPrefab, levelsRoot);
                t.text = $"Lv{tierLevel}";
                t.alignment = TextAlignmentOptions.MidlineRight;

                RectTransform rt = t.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, layoutRowY[labelIndex]);
                spawnedLevelLabels.Add(t);
            }

            string tierCaption = BuildTierRowCaptionForRowSpan(rows, tierStart, idx);
            if (levelTierRowLabelPrefab != null && !string.IsNullOrEmpty(tierCaption))
            {
                RectTransform tierParent = ResolveLevelTierRowLabelParent();
                if (tierParent != null)
                {
                    var tr = Instantiate(levelTierRowLabelPrefab, tierParent);
                    tr.alignment = TextAlignmentOptions.MidlineRight;

                    RectTransform rtt = tr.rectTransform;
                    rtt.anchorMin = new Vector2(1f, 1f);
                    rtt.anchorMax = new Vector2(1f, 1f);
                    rtt.pivot = new Vector2(1f, 0.5f);
                    float tierY = ResolveTierLabelAnchoredY(tierParent, layoutRowY[labelIndex]);
                    float inset = Mathf.Max(0f, levelTierLabelRightInset) + Mathf.Max(0f, levelTierLabelExtraRightPaddingPx);
                    rtt.anchoredPosition = new Vector2(-inset, tierY);
                    ApplyTierRowLabelPreferredHeight(tr, tierCaption);
                    spawnedTierRowLabels.Add(tr);
                }
            }
        }
    }

    private RectTransform ResolveLevelTierRowLabelParent()
    {
        if (levelTierRowLabelsRoot != null)
            return levelTierRowLabelsRoot;
        if (levelsRoot != null && levelsRoot.parent is RectTransform p)
            return p;
        return nodesRoot;
    }

    /// <summary>Maps a row Y from <see cref="levelsRoot"/> space into <paramref name="tierParent"/>’s anchored space so rows line up.</summary>
    private float ResolveTierLabelAnchoredY(RectTransform tierParent, float rowYInLevelsRootSpace)
    {
        if (levelsRoot == null || tierParent == null || tierParent == levelsRoot)
            return rowYInLevelsRootSpace;

        Vector3 world = levelsRoot.TransformPoint(new Vector3(0f, rowYInLevelsRootSpace, 0f));
        Canvas canvas = tierParent.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null &&
            (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace))
            cam = canvas.worldCamera;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(tierParent, screen, cam, out Vector2 local))
            return local.y;

        return rowYInLevelsRootSpace;
    }

    /// <summary>
    /// Right-side tier caption: unique spine types on this skill level row (order preserved), excluding minor passives only.
    /// </summary>
    private static string BuildTierRowCaptionForRowSpan(List<RowDef> rows, int tierStart, int tierEndExclusive)
    {
        if (rows == null || tierStart < 0 || tierEndExclusive > rows.Count || tierStart >= tierEndExclusive)
            return string.Empty;

        var order = new List<SkillTreeNodeVisualType>();
        for (int i = tierStart; i < tierEndExclusive; i++)
        {
            SkillTreeNodeVisualType t = rows[i].type;
            if (t == SkillTreeNodeVisualType.MinorPassive)
                continue;

            bool dup = false;
            for (int o = 0; o < order.Count; o++)
            {
                if (order[o] == t)
                {
                    dup = true;
                    break;
                }
            }

            if (!dup)
                order.Add(t);
        }

        if (order.Count == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < order.Count; i++)
        {
            if (i > 0)
                sb.Append(" / ");
            sb.Append(TypeLabel(order[i]));
        }

        return sb.ToString();
    }

    private static bool TierRowContainsType(List<RowDef> rows, int level, SkillTreeNodeVisualType match)
    {
        if (rows == null)
            return false;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].level != level)
                continue;
            if (rows[i].type == match)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Keeps label width; grows height from wrapped TMP preferred size so multi-type rows stack vertically.
    /// </summary>
    private static void ApplyTierRowLabelPreferredHeight(TMP_Text label, string caption)
    {
        if (label == null)
            return;

        label.text = caption ?? string.Empty;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;

        RectTransform rt = label.rectTransform;
        float fixedWidth = Mathf.Abs(rt.sizeDelta.x);
        if (fixedWidth < 8f)
            fixedWidth = 80f;

        label.ForceMeshUpdate();
        Vector2 pref = label.GetPreferredValues(caption, fixedWidth, 0f);
        float h = Mathf.Max(pref.y, 18f);
        rt.sizeDelta = new Vector2(fixedWidth, h);
    }

    private void SpawnRows(List<RowDef> rows, int currentSkillLevel)
    {
        // Spawn main spine nodes first.
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            float y = layoutRowY[i];
            float x = layoutRowX[i];

            string spineId = SpineNodeId(row);
            bool unlocked = row.level <= currentSkillLevel;
            BuildTooltipCopy(row, unlocked, out string mainTitle, out string mainBody);
            Sprite mainIcon = ResolveUnlockNodeIcon(row.unlock, selectedSkill);
            SpawnNode(spineId, new Vector2(x, y), row.type, mainTitle, mainBody, unlocked, mainIcon);
            unlockLevelByNodeId[spineId] = row.level;
            spineLayoutXBySpineId[spineId] = x;
            rowDefBySpineNodeId[spineId] = row;
        }

        abilityTierPickMetaBySpineId.Clear();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].type != SkillTreeNodeVisualType.Ability)
                continue;

            int g = i;
            while (g > 0 && rows[g - 1].level == rows[i].level)
                g--;
            int e = i;
            while (e + 1 < rows.Count && rows[e + 1].level == rows[i].level)
                e++;

            var abilityRowIndices = new List<int>();
            for (int k = g; k <= e; k++)
            {
                if (rows[k].type == SkillTreeNodeVisualType.Ability)
                    abilityRowIndices.Add(k);
            }

            int groupSize = abilityRowIndices.Count;
            if (groupSize <= 0)
                continue;

            for (int ord = 0; ord < abilityRowIndices.Count; ord++)
            {
                int rowIdx = abilityRowIndices[ord];
                string sid = SpineNodeId(rows[rowIdx]);
                abilityTierPickMetaBySpineId[sid] = new AbilityTierPickMeta(rows[rowIdx].level, ord, groupSize);
            }
        }

        // Multi-major-passive tiers (e.g. Woodcutting Lv15) reuse the same row-pick + collapse
        // pipeline as ability tiers. Single MajorPassive rows keep their normal toggle behavior.
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].type != SkillTreeNodeVisualType.MajorPassive)
                continue;

            int g = i;
            while (g > 0 && rows[g - 1].level == rows[i].level)
                g--;
            int e = i;
            while (e + 1 < rows.Count && rows[e + 1].level == rows[i].level)
                e++;

            if (!TierIsMultiAbilityOnly(rows, g, e))
            {
                i = e;
                continue;
            }

            int groupSize = e - g + 1;
            for (int ord = 0; ord < groupSize; ord++)
            {
                int rowIdx = g + ord;
                string sid = SpineNodeId(rows[rowIdx]);
                abilityTierPickMetaBySpineId[sid] = new AbilityTierPickMeta(rows[rowIdx].level, ord, groupSize);
            }

            i = e;
        }

        // Spawn choices using per-choice unlock levels.
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            List<int> choiceAssetIndices = GetNonNullChoiceAssetIndices(row.unlock);
            if (choiceAssetIndices.Count <= 0)
                continue;

            string parentSpineId = SpineNodeId(row);
            float parentX = layoutRowX[i];
            int[] layoutOrder = GetEnhancementChoiceLayoutOrder(choiceAssetIndices.Count);
            float xStep = ResolveChoiceHorizontalStep(row.type, choiceAssetIndices.Count);
            for (int slot = 0; slot < layoutOrder.Length; slot++)
            {
                int logicalChoice = layoutOrder[slot];
                if (logicalChoice < 0 || logicalChoice >= choiceAssetIndices.Count)
                    continue;

                int choiceIndex = choiceAssetIndices[logicalChoice];
                SkillChoiceDefinition choice = row.unlock.choices[choiceIndex];
                int choiceUnlockLevel = ResolveChoiceUnlockLevel(row.level, row.type, choice);
                if (!rowYByLevel.TryGetValue(choiceUnlockLevel, out float targetY))
                    continue; // no authored row at that level yet

                float yOffset;
                if (choiceUnlockLevel != row.level)
                {
                    // Choice rows that unlock later than their source can use a dedicated offset.
                    yOffset = ScaledLayout(explicitChoiceRowYOffset);
                }
                else
                {
                    yOffset = ScaledLayout(row.type == SkillTreeNodeVisualType.CapstonePassive ? capstoneChoiceYOffset : choiceYOffset);
                }

                float offsetX = GetEnhancementChoiceSlotOffsetMultiplier(choiceAssetIndices.Count, slot) * xStep;
                float choiceY = targetY + yOffset;
                float choiceX = parentX + offsetX;
                bool unlocked = row.level <= currentSkillLevel && choiceUnlockLevel <= currentSkillLevel;
                BuildChoiceTooltipCopy(choiceUnlockLevel, choice, row, unlocked, out string cTitle, out string cBody);
                string choiceNodeId = ChoiceId(parentSpineId, choiceUnlockLevel, choiceIndex);
                Sprite choiceIcon = ResolveChoiceNodeIcon(choice);
                SpawnNode(
                    choiceNodeId,
                    new Vector2(choiceX, choiceY),
                    SkillTreeNodeVisualType.Choice,
                    cTitle,
                    cBody,
                    unlocked,
                    choiceIcon
                );
                unlockLevelByNodeId[choiceNodeId] = choiceUnlockLevel;
                choiceMetaByNodeId[choiceNodeId] =
                    new ChoiceNodeMeta(parentSpineId, row.level, choiceIndex, choiceUnlockLevel, offsetX, choiceY);
            }
        }

        RefreshChoiceSelectionVisuals();
    }

    private void SpawnConnectors(List<RowDef> rows)
    {
        interTierVerticalConnectors.Clear();

        var tierStarts = new List<int>();
        int tIdx = 0;
        while (tIdx < rows.Count)
        {
            tierStarts.Add(tIdx);
            int lv = rows[tIdx].level;
            while (tIdx < rows.Count && rows[tIdx].level == lv)
                tIdx++;
        }

        for (int t = 0; t + 1 < tierStarts.Count; t++)
        {
            int g0 = tierStarts[t];
            int e0 = tierStarts[t + 1] - 1;
            int g1 = tierStarts[t + 1];
            int e1 = t + 2 < tierStarts.Count ? tierStarts[t + 2] - 1 : rows.Count - 1;

            int anchorUp = TierVerticalAnchorIndex(rows, g0, e0);
            int anchorLow = TierVerticalAnchorIndex(rows, g1, e1);

            if (TrySpawnConnectorInternal(SpineNodeId(rows[anchorUp]), SpineNodeId(rows[anchorLow]), out var vConn))
            {
                interTierVerticalConnectors.Add(new InterTierVerticalRecord
                {
                    conn = vConn,
                    upperLevel = rows[g0].level,
                    lowerLevel = rows[g1].level
                });
            }

            // Multi-stack tiers (Ability or MajorPassive groups of 2+) fan connectors out to each lateral row
            // so the upper/lower spine reaches every node in the row, not just the middle anchor.
            SpawnMultiStackFanConnectors(rows, g0, e0, g1, e1, anchorUp, anchorLow);
        }

        // Choices
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            List<int> choiceAssetIndices = GetNonNullChoiceAssetIndices(row.unlock);
            if (choiceAssetIndices.Count <= 0)
                continue;

            string source = SpineNodeId(row);
            for (int c = 0; c < choiceAssetIndices.Count; c++)
            {
                int choiceIndex = choiceAssetIndices[c];
                SkillChoiceDefinition choice = row.unlock.choices[choiceIndex];
                int choiceUnlockLevel = ResolveChoiceUnlockLevel(row.level, row.type, choice);
                string choiceNodeId = ChoiceId(source, choiceUnlockLevel, choiceIndex);
                if (TrySpawnConnectorInternal(source, choiceNodeId, out var branchConn))
                    choiceBranchConnectors.Add(new ChoiceBranchConnectorRecord(source, choiceNodeId, row.level, branchConn));
            }
        }
    }

    private void SpawnMultiStackFanConnectors(
        List<RowDef> rows,
        int upperStart, int upperEndInclusive,
        int lowerStart, int lowerEndInclusive,
        int anchorUpIdx, int anchorLowIdx)
    {
        bool upperMulti = TierIsMultiAbilityOnly(rows, upperStart, upperEndInclusive);
        bool lowerMulti = TierIsMultiAbilityOnly(rows, lowerStart, lowerEndInclusive);
        if (!upperMulti && !lowerMulti)
            return;

        // Fans connect every non-anchor row in the multi-stack tier to the opposite tier's anchor.
        // The anchor↔anchor connector spawned just before this method covers the central line.
        if (lowerMulti)
        {
            string fromId = SpineNodeId(rows[anchorUpIdx]);
            for (int li = lowerStart; li <= lowerEndInclusive; li++)
            {
                if (li == anchorLowIdx)
                    continue;
                string toId = SpineNodeId(rows[li]);
                AddExplicitInterTierConnector(rows[upperStart].level, rows[lowerStart].level, fromId, toId);
            }
        }

        if (upperMulti)
        {
            string toId = SpineNodeId(rows[anchorLowIdx]);
            for (int ui = upperStart; ui <= upperEndInclusive; ui++)
            {
                if (ui == anchorUpIdx)
                    continue;
                string fromId = SpineNodeId(rows[ui]);
                AddExplicitInterTierConnector(rows[upperStart].level, rows[lowerStart].level, fromId, toId);
            }
        }
    }

    private void AddExplicitInterTierConnector(int upperLevel, int lowerLevel, string fromId, string toId)
    {
        if (!TrySpawnConnectorInternal(fromId, toId, out var conn))
            return;
        interTierVerticalConnectors.Add(new InterTierVerticalRecord
        {
            conn = conn,
            upperLevel = upperLevel,
            lowerLevel = lowerLevel,
            explicitFromId = fromId,
            explicitToId = toId
        });
    }

    private static int ResolveChoiceUnlockLevel(int sourceLevel, SkillTreeNodeVisualType sourceType, SkillChoiceDefinition choice)
    {
        if (sourceType == SkillTreeNodeVisualType.CapstonePassive)
            return 50;
        if (choice != null && choice.requiredLevel > 0)
            return choice.requiredLevel;
        return sourceLevel;
    }

    private static string SpineNodeId(RowDef row) => $"Lv{row.level}_{row.slotAtLevel}";

    private static string ChoiceId(string parentSpineNodeId, int unlockLevel, int index) =>
        $"{parentSpineNodeId}_ChoiceLv{unlockLevel}_{index}";

    private void BuildTooltipCopy(RowDef row, bool isUnlocked, out string title, out string body)
    {
        int level = row.level;
        SkillTreeNodeVisualType type = row.type;
        SkillUnlockDefinition unlock = row.unlock;

        string unlockTitle = unlock != null
            ? SkillsAbilityPresentationResolver.ResolveTreeUnlockTitle(unlock)
            : "Untitled";
        if (string.IsNullOrWhiteSpace(unlockTitle))
            unlockTitle = "Untitled";

        string desc;
        if (unlock != null && unlock.unlockType == SkillUnlockType.Ability && unlock.ability != null)
            desc = SkillsAbilityPresentationResolver.ResolveSkillTreeAbilityUnlockDescription(unlock);
        else if (unlock != null && !string.IsNullOrWhiteSpace(unlock.description))
            desc = unlock.description.Trim();
        else
            desc = "No description yet.";
        desc = BuildEffectiveGatheringMajorPassiveDescription(row, desc);

        bool useMajorPassivePresentation = ShouldUseMajorPassiveLinePresentation(row);
        if (useMajorPassivePresentation)
            desc = ApplyMajorPassiveValueLineMarkup(desc);

        if (unlock != null && unlock.unlockType == SkillUnlockType.Ability && unlock.ability != null && selectedSkill != null)
        {
            string aid = unlock.ability.abilityId;
            if (selectedSkill.skillType == SkillType.Woodcutting)
            {
                if (string.Equals(aid, AbilityCombatPower.LumberFrenzyAbilityId, StringComparison.OrdinalIgnoreCase))
                    desc = StripWoodcuttingLumberFrenzyTreeDescriptionDuration(desc);
                else if (string.Equals(aid, AbilityCombatPower.AvatarOfTheForestAbilityId, StringComparison.OrdinalIgnoreCase))
                    desc = StripAvatarTreeDescriptionLongWording(desc);
            }
            else if (selectedSkill.skillType == SkillType.Fishing &&
                string.Equals(aid, AbilityCombatPower.FishingFrenzyAbilityId, StringComparison.OrdinalIgnoreCase))
            {
                desc = StripWoodcuttingLumberFrenzyTreeDescriptionDuration(desc);
            }
        }

        if (unlock != null && unlock.unlockType == SkillUnlockType.Unlock)
        {
            title = unlockTitle;
            string unlockTypeLabel = TypeLabel(SkillTreeNodeVisualType.Unlock);
            body = $"{unlockTypeLabel} {BuildStatusLine(isUnlocked)}\nUnlocks at level {level}\n\n{desc}";
            return;
        }

        if (unlock != null && unlock.unlockType == SkillUnlockType.MinorUnlock)
        {
            title = unlockTitle;
            string minorUnlockLabel = TypeLabel(SkillTreeNodeVisualType.MinorUnlock);
            body = $"{minorUnlockLabel} {BuildStatusLine(isUnlocked)}\nUnlocks at Lv{level}\n\n{desc}";
            return;
        }

        string typeLabel = useMajorPassivePresentation ? TypeLabel(SkillTreeNodeVisualType.MajorPassive) : TypeLabel(type);
        title = unlockTitle;
        body = $"{typeLabel} {BuildStatusLine(isUnlocked)}\nUnlocks at Lv{level}\n\n{desc}";
    }

    private void BuildChoiceTooltipCopy(int unlockLevel, SkillChoiceDefinition choice, RowDef parentRow, bool isUnlocked, out string title, out string body)
    {
        SkillUnlockDefinition parentUnlock = parentRow.unlock;

        string unlockTitle;
        if (choice != null)
        {
            string ct = SkillsAbilityPresentationResolver.ResolveChoiceTitle(choice);
            unlockTitle = !string.IsNullOrWhiteSpace(ct)
                ? ct
                : (parentUnlock != null ? SkillsAbilityPresentationResolver.ResolveUnlockTitle(parentUnlock) : string.Empty);
        }
        else
        {
            unlockTitle = parentUnlock != null ? SkillsAbilityPresentationResolver.ResolveUnlockTitle(parentUnlock) : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(unlockTitle))
            unlockTitle = "Untitled";

        string desc = choice != null
            ? SkillsAbilityPresentationResolver.ResolveChoiceDescription(choice)
            : string.Empty;
        if (string.IsNullOrWhiteSpace(desc))
            desc = "No description yet.";

        if (ShouldUseMajorPassiveLinePresentation(parentRow))
            desc = ApplyMajorPassiveValueLineMarkup(desc);

        string typeLabel = TypeLabel(SkillTreeNodeVisualType.Choice);
        title = unlockTitle;
        body = $"{typeLabel} {BuildStatusLine(isUnlocked)}\nUnlocks at Lv{unlockLevel}\n\n{desc}";
    }

    private string BuildEffectiveGatheringMajorPassiveDescription(RowDef row, string fallbackDescription)
    {
        if (selectedSkill == null || row.unlock == null)
            return fallbackDescription;

        int selectedChoice = skillsManager != null
            ? skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, SpineNodeId(row), -1)
            : -1;
        string selectedChoiceTitle = GetChoiceTitle(row.unlock, selectedChoice);
        string title = !string.IsNullOrWhiteSpace(row.unlock.title) ? row.unlock.title.Trim() : string.Empty;

        if ((selectedSkill.skillType == SkillType.Woodcutting && row.level == PlayerController.WoodcuttingMajorPassiveSourceLevel) ||
            (selectedSkill.skillType == SkillType.Fishing && row.level == PlayerController.FishingMajorPassiveSourceLevel))
        {
            if (GatheringPassiveTooltipText.TryBuildSkillTreeMajorPassiveBody(
                    selectedSkill.skillType, title, selectedChoiceTitle, out string majorBody))
                return majorBody;
        }

        if (selectedSkill.skillType == SkillType.Woodcutting)
        {
            if (row.level != PlayerController.WoodcuttingMajorPassiveSourceLevel &&
                row.level != PlayerController.WoodcuttingLv35MajorPassiveSourceLevel)
                return fallbackDescription;

            if (string.Equals(title, "Ancient Lumbercraft", StringComparison.OrdinalIgnoreCase))
            {
                int hiddenChance = 10;
                bool experiencedGatherer = string.Equals(selectedChoiceTitle, "Experienced Gatherer", StringComparison.OrdinalIgnoreCase);
                bool treasureHunter = string.Equals(selectedChoiceTitle, "Treasure Hunter", StringComparison.OrdinalIgnoreCase);
                if (experiencedGatherer)
                    hiddenChance += 5;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Hidden items can be found when a bonus item is discovered.");
                sb.AppendLine();
                sb.Append("+");
                sb.Append(hiddenChance);
                sb.Append("% chance to find hidden resources");
                if (treasureHunter)
                {
                    sb.AppendLine();
                    sb.Append("+10% chance for Hidden Resources to double");
                }
                return sb.ToString();
            }

            if (string.Equals(title, "Forest's Favor", StringComparison.OrdinalIgnoreCase))
            {
                int extraItemChance = 25;
                bool richHarvest = string.Equals(selectedChoiceTitle, "Rich Harvest", StringComparison.OrdinalIgnoreCase);
                bool hiddenRiches = string.Equals(selectedChoiceTitle, "Hidden Riches", StringComparison.OrdinalIgnoreCase);
                if (hiddenRiches)
                    extraItemChance += 10;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Bonus Finds have a chance to grant +1 additional items.");
                sb.AppendLine();
                sb.Append("+");
                sb.Append(extraItemChance);
                sb.Append("% Bonus Find Extra Item Chance");
                if (richHarvest)
                {
                    sb.AppendLine();
                    sb.Append("+10% Bonus Find Chance");
                }
                return sb.ToString();
            }

            return fallbackDescription;
        }

        return fallbackDescription;
    }

    private static string GetChoiceTitle(SkillUnlockDefinition unlock, int choiceIndex)
    {
        if (unlock == null || unlock.choices == null || choiceIndex < 0 || choiceIndex >= unlock.choices.Count)
            return string.Empty;
        SkillChoiceDefinition choice = unlock.choices[choiceIndex];
        return choice != null && !string.IsNullOrWhiteSpace(choice.title) ? choice.title.Trim() : string.Empty;
    }

    private bool ShouldUseMajorPassiveLinePresentation(RowDef row)
    {
        if (row.unlock == null)
            return false;
        if (row.type == SkillTreeNodeVisualType.MajorPassive)
            return true;
        if (!IsGatheringSkillSelected(selectedSkill))
            return false;
        if (row.type != SkillTreeNodeVisualType.Ability)
            return false;
        if (GetNonNullChoices(row.unlock).Count <= 0)
            return false;
        return TierRowContainsType(layoutRowsCache, row.level, SkillTreeNodeVisualType.MajorPassive);
    }

    /// <summary>Lines that start with "+" after whitespace get accent markup (green when an axe is in the toolbelt on Woodcutting).</summary>
    private string ApplyMajorPassiveValueLineMarkup(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;
        if (raw.IndexOf("<color=", StringComparison.OrdinalIgnoreCase) >= 0)
            return raw;

        bool greenAccent = selectedSkill != null && selectedSkill.skillType == SkillType.Woodcutting &&
            UnityEngine.Object.FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include) is { } pac && pac.HasAxeInToolbelt();
        string open = greenAccent ? "<color=#55DD55>" : "<color=#FFB347>";
        const string close = "</color>";
        string norm = raw.Replace("\r\n", "\n");
        var lines = norm.Split('\n');
        var sb = new System.Text.StringBuilder(norm.Length + lines.Length * 32);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                sb.Append('\n');
            string line = lines[i];
            int lead = 0;
            while (lead < line.Length && line[lead] == ' ')
                lead++;
            if (lead >= line.Length)
            {
                sb.Append(line);
                continue;
            }

            string trimmed = line.Substring(lead);
            if (trimmed.StartsWith("+", StringComparison.Ordinal) ||
                trimmed.StartsWith("Flow lasts", StringComparison.OrdinalIgnoreCase))
            {
                if (lead > 0)
                    sb.Append(line, 0, lead);
                sb.Append(open);
                sb.Append(trimmed);
                sb.Append(close);
            }
            else
                sb.Append(line);
        }

        return sb.ToString();
    }

    private static string StripWoodcuttingLumberFrenzyTreeDescriptionDuration(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc))
            return desc;
        string d = desc;
        d = d.Replace(" for 20 seconds.", ".", StringComparison.OrdinalIgnoreCase);
        d = d.Replace(" for 20 seconds,", ",", StringComparison.OrdinalIgnoreCase);
        d = d.Replace(" for 20 seconds", "", StringComparison.OrdinalIgnoreCase);
        while (d.Contains("..", StringComparison.Ordinal))
            d = d.Replace("..", ".", StringComparison.Ordinal);
        return d.Trim();
    }

    private static string StripAvatarTreeDescriptionLongWording(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc))
            return desc;
        return desc.Replace("for a long woodcutting surge", "for a woodcutting surge", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildStatusLine(bool isUnlocked)
    {
        return isUnlocked
            ? "<color=#33CC66>Node Unlocked</color>"
            : "<color=#FF4D4D>Node Locked</color>";
    }

    private static List<SkillChoiceDefinition> GetNonNullChoices(SkillUnlockDefinition unlock)
    {
        var list = new List<SkillChoiceDefinition>();
        if (unlock == null || unlock.choices == null)
            return list;

        for (int i = 0; i < unlock.choices.Count; i++)
        {
            SkillChoiceDefinition c = unlock.choices[i];
            if (c != null) list.Add(c);
        }
        return list;
    }

    /// <summary>Indices into <see cref="SkillUnlockDefinition.choices"/> for non-null entries (preserves authored order).</summary>
    private static List<int> GetNonNullChoiceAssetIndices(SkillUnlockDefinition unlock)
    {
        var indices = new List<int>();
        if (unlock == null || unlock.choices == null)
            return indices;

        for (int i = 0; i < unlock.choices.Count; i++)
        {
            if (unlock.choices[i] != null)
                indices.Add(i);
        }

        return indices;
    }

    /// <summary>
    /// Maps authored choice count → left-to-right slot order (logical indices 0..n-1).
    /// Two choices stay centered; three/four fan off the spine (see <see cref="GetEnhancementChoiceSlotOffsetMultiplier"/>).
    /// </summary>
    private static int[] GetEnhancementChoiceLayoutOrder(int authoredChoiceCount)
    {
        return authoredChoiceCount switch
        {
            <= 0 => Array.Empty<int>(),
            1 => new[] { 0 },
            2 => EnhancementLayoutOrderTwo,
            3 => EnhancementLayoutOrderThree,
            4 => EnhancementLayoutOrderFour,
            _ => BuildLinearEnhancementLayoutOrder(authoredChoiceCount)
        };
    }

    /// <summary>
    /// Horizontal slot multipliers × <see cref="ResolveChoiceHorizontalStep"/>. Keeps 3–4 choices off the spine.
    /// </summary>
    private static float GetEnhancementChoiceSlotOffsetMultiplier(int choiceCount, int slotIndex)
    {
        if (choiceCount == 3)
        {
            return slotIndex switch
            {
                0 => -1.5f,
                1 => -0.5f,
                2 => 1.5f,
                _ => slotIndex - 1f
            };
        }

        float center = (choiceCount - 1) * 0.5f;
        return slotIndex - center;
    }

    private static int[] BuildLinearEnhancementLayoutOrder(int count)
    {
        var order = new int[count];
        for (int i = 0; i < count; i++)
            order[i] = i;
        return order;
    }

    private float ResolveChoiceHorizontalStep(SkillTreeNodeVisualType rowType, int choiceCount)
    {
        bool capstone = rowType == SkillTreeNodeVisualType.CapstonePassive;
        if (choiceCount <= 2)
            return ScaledLayout(capstone ? capstoneChoiceOffsetX : choiceOffsetX);

        // 3–4 enhancements: same center-to-center rhythm as spine / same-level rows (not the wide 2-choice fan).
        Vector2 choiceBox = SkillTreeNodeUI.GetVisualBoxSize(SkillTreeNodeVisualType.Choice);
        float step = choiceBox.x + Mathf.Max(0f, ScaledLayout(sameLevelNodeGap));
        if (capstone)
            step *= 0.85f;
        return step;
    }

    private static string TypeLabel(SkillTreeNodeVisualType type)
    {
        return type switch
        {
            SkillTreeNodeVisualType.MinorPassive => "Minor Passive",
            SkillTreeNodeVisualType.MinorUnlock => "Minor Unlock",
            SkillTreeNodeVisualType.MajorPassive => "Major Passive",
            SkillTreeNodeVisualType.Unlock => "Unlock",
            SkillTreeNodeVisualType.Ability => "Ability",
            SkillTreeNodeVisualType.Choice => "Enhancement",
            SkillTreeNodeVisualType.CapstonePassive => "Capstone",
            _ => "Node"
        };
    }

    public void ClearTree()
    {
        sharedTooltip?.Hide();

        _lastBuiltSkill = null;
        _lastRefreshHadActivePresentation = false;

        foreach (var n in spawnedNodes)
            if (n != null) Destroy(n.gameObject);

        foreach (var c in spawnedConnectors)
            if (c != null) Destroy(c.gameObject);

        expandedChoiceBranchesBySourceLevel.Clear();
        choiceBranchConnectors.Clear();
        abilityTierPickMetaBySpineId.Clear();
        interTierVerticalConnectors.Clear();
        layoutRowsCache.Clear();
        spineLayoutXBySpineId.Clear();

        foreach (var t in spawnedLevelLabels)
            if (t != null) Destroy(t.gameObject);

        foreach (var t in spawnedTierRowLabels)
            if (t != null) Destroy(t.gameObject);

        spawnedNodes.Clear();
        spawnedConnectors.Clear();
        spawnedLevelLabels.Clear();
        spawnedTierRowLabels.Clear();
        nodeLookup.Clear();
        unlockLevelByNodeId.Clear();
        rowYByLevel.Clear();
        rowDefBySpineNodeId.Clear();
        layoutRowY.Clear();
        layoutRowX.Clear();
        tooltipTitleByNodeId.Clear();
        tooltipBodyByNodeId.Clear();
        nodeLevelById.Clear();
        choiceMetaByNodeId.Clear();
        selectedNode = null;
        PassiveUnlockLineHighlightChanged?.Invoke(null);
    }

    private void RefreshEquipmentTierHint()
    {
        if (equipmentTierHint == null)
            return;

        if (selectedSkill == null)
        {
            equipmentTierHint.gameObject.SetActive(false);
            return;
        }

        string h = EquipmentTierRules.BuildSkillTreeHint(selectedSkill.skillType);
        if (string.IsNullOrEmpty(h))
        {
            equipmentTierHint.gameObject.SetActive(false);
            return;
        }

        equipmentTierHint.gameObject.SetActive(true);
        equipmentTierHint.text = h;
    }

    private static Sprite ResolveUnlockNodeIcon(SkillUnlockDefinition unlock, SkillDefinition skill)
    {
        if (unlock == null)
            return null;
        if (unlock.icon != null)
            return unlock.icon;

        if (unlock.unlockType == SkillUnlockType.MinorPassive)
        {
            Sprite sharedMinor = FindFirstAuthoredMinorPassiveIcon(skill);
            if (sharedMinor != null)
                return sharedMinor;
            if (skill != null && skill.icon != null)
                return skill.icon;
        }

        if (unlock.unlockType == SkillUnlockType.MinorUnlock)
        {
            if (skill != null && skill.icon != null)
                return skill.icon;
            return null;
        }

        if (unlock.ability != null)
        {
            Sprite spr = SkillsAbilityPresentationResolver.ResolveAbilityIcon(unlock.ability);
            if (spr != null)
                return spr;
        }
        return null;
    }

    /// <summary>First minor-passive row on this skill with a non-null <see cref="SkillUnlockDefinition.icon"/> (source for all other minor nodes).</summary>
    private static Sprite FindFirstAuthoredMinorPassiveIcon(SkillDefinition skill)
    {
        if (skill?.unlocks == null || skill.unlocks.Count == 0)
            return null;

        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition u = skill.unlocks[i];
            if (u != null && u.unlockType == SkillUnlockType.MinorPassive && u.icon != null)
                return u.icon;
        }

        return null;
    }

    private static Sprite ResolveChoiceNodeIcon(SkillChoiceDefinition choice)
    {
        if (choice == null)
            return null;
        if (choice.icon != null)
            return choice.icon;
        if (choice.ability != null)
        {
            Sprite spr = SkillsAbilityPresentationResolver.ResolveAbilityIcon(choice.ability);
            if (spr != null)
                return spr;
        }
        return null;
    }

    private void SpawnNode(string nodeId, Vector2 pos, SkillTreeNodeVisualType type, string tooltipTitle, string tooltipBody, bool unlocked, Sprite iconSprite = null)
    {
        if (nodePrefab == null || nodesRoot == null) return;

        var node = Instantiate(nodePrefab, nodesRoot);
        node.ResetSkillTreePresentationCache();
        node.SetSkillTreeCooldownPresentation(false, 0f, 0f);
        node.SetSkillTreeActiveBuffPresentation(false, 0f);
        node.RectTransform.anchoredPosition = pos;
        node.ApplyVisualType(type);
        node.SetIcon(iconSprite, iconSprite != null);
        node.SetLocked(!unlocked);
        node.SetSelected(false);
        node.SetClick(() => OnNodeClicked(nodeId, node));
        node.SetRightClick(() => OnNodeRightClicked(nodeId, node));
        node.SetHover(
            () => HandleNodeHover(nodeId, node.transform),
            HideTooltip
        );

        spawnedNodes.Add(node);
        nodeLookup[nodeId] = node;
        // Note: choice node ids do not start with their unlock level; store explicit unlock levels instead.
        tooltipTitleByNodeId[nodeId] = string.IsNullOrWhiteSpace(tooltipTitle) ? "Node" : tooltipTitle;
        tooltipBodyByNodeId[nodeId] = tooltipBody ?? string.Empty;
    }

    private void HandleNodeHover(string nodeId, Transform anchor)
    {
        if (!string.IsNullOrWhiteSpace(nodeId) && unlockLevelByNodeId.TryGetValue(nodeId, out int lvl))
        {
            if (_pendingUnlockGlowLevels.Remove(lvl))
                UnlockGlowAcknowledgedByHover?.Invoke(lvl);
        }

        ShowTooltip(nodeId, anchor);
    }

    private void ApplyPendingUnlockGlowLevels()
    {
        if (_pendingUnlockGlowLevels.Count == 0)
            return;

        foreach (int lvl in _pendingUnlockGlowLevels)
            HighlightNewUnlocksAtLevel(lvl);
    }

    private void SpawnConnector(string from, string to)
    {
        if (!TrySpawnConnectorInternal(from, to, out _))
            return;
    }

    private bool TrySpawnConnectorInternal(string from, string to, out SkillTreeConnectorUI connector)
    {
        connector = null;
        if (!nodeLookup.TryGetValue(from, out var a)) return false;
        if (!nodeLookup.TryGetValue(to, out var b)) return false;

        var conn = Instantiate(connectorPrefab, connectorsRoot);
        conn.SetPositions(a, b);
        spawnedConnectors.Add(conn);
        connector = conn;
        return true;
    }

    private void OnNodeClicked(string nodeId, SkillTreeNodeUI node)
    {
        if (node == null || node.IsLocked()) return;

        if (choiceMetaByNodeId.TryGetValue(nodeId, out ChoiceNodeMeta choiceMeta))
        {
            PassiveUnlockLineHighlightChanged?.Invoke(null);
            if (!selectedSkill || !skillsManager)
                return;

            int current = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, choiceMeta.parentSpineNodeId, -1);
            if (current != choiceMeta.choiceIndex)
            {
                if (IsRowLevelAbilityCooldownActiveForParentSpine(choiceMeta.parentSpineNodeId))
                    return;
                skillsManager.SetSkillChoiceSelection(selectedSkill.skillType, choiceMeta.parentSpineNodeId, choiceMeta.choiceIndex);
            }

            expandedChoiceBranchesBySourceLevel.Remove(choiceMeta.sourceLevel);
            RefreshChoiceSelectionVisuals();
            RefreshChoiceBranchVisibility();
            ShowTooltip(nodeId, node.transform);
            return;
        }

        if (abilityTierPickMetaBySpineId.TryGetValue(nodeId, out AbilityTierPickMeta tierMeta))
        {
            PassiveUnlockLineHighlightChanged?.Invoke(null);
            if (!selectedSkill || !skillsManager)
                return;

            if (selectedNode != null)
            {
                selectedNode.SetSelected(false);
                selectedNode = null;
            }

            if (tierMeta.groupSize >= 2)
            {
                int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, tierMeta.level, -1);

                if (pick >= 0 && pick == tierMeta.ordinal)
                {
                    RefreshAbilityTierLayoutAndVisibility();
                    ShowTooltip(nodeId, node.transform);
                    return;
                }

                if (pick >= 0 && pick != tierMeta.ordinal && IsRowLevelAbilityCooldownActive(tierMeta.level))
                    return;

                skillsManager.SetSkillAbilityRowPick(selectedSkill.skillType, tierMeta.level, tierMeta.ordinal);
            }
            else
            {
                int pickOne = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, tierMeta.level, -1);
                if (pickOne == 0)
                {
                    RefreshAbilityTierLayoutAndVisibility();
                    ShowTooltip(nodeId, node.transform);
                    return;
                }

                if (pickOne >= 0 && pickOne != tierMeta.ordinal && IsRowLevelAbilityCooldownActive(tierMeta.level))
                    return;

                skillsManager.SetSkillAbilityRowPick(selectedSkill.skillType, tierMeta.level, 0);
            }

            RefreshAbilityTierLayoutAndVisibility();
            RefreshChoiceBranchVisibility();
            ShowTooltip(nodeId, node.transform);
            return;
        }

        if (rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef spineRow)
            && spineRow.type == SkillTreeNodeVisualType.MinorPassive
            && spineRow.unlock != null)
        {
            if (selectedNode == node)
            {
                selectedNode.SetSelected(false);
                selectedNode = null;
                PassiveUnlockLineHighlightChanged?.Invoke(null);
            }
            else
            {
                if (selectedNode != null)
                    selectedNode.SetSelected(false);
                selectedNode = node;
                selectedNode.SetSelected(true);
                string label = null;
                if (selectedSkill != null
                    && PassiveUnlocksLineHighlight.TryGetLineLabel(selectedSkill, spineRow.unlock, out string hl))
                    label = hl;
                PassiveUnlockLineHighlightChanged?.Invoke(label);
            }

            ShowTooltip(nodeId, node.transform);
            return;
        }

        PassiveUnlockLineHighlightChanged?.Invoke(null);

        if (selectedNode != null)
            selectedNode.SetSelected(false);

        selectedNode = node;
        selectedNode.SetSelected(true);

        ShowTooltip(nodeId, node.transform);
    }

    private void OnNodeRightClicked(string nodeId, SkillTreeNodeUI node)
    {
        if (node == null || node.IsLocked())
            return;

        PreferRuntimeSkillsManager();
        if (selectedSkill == null || skillsManager == null)
            return;

        string spineTarget = nodeId;
        if (choiceMetaByNodeId.TryGetValue(nodeId, out ChoiceNodeMeta cm))
            spineTarget = cm.parentSpineNodeId;

        if (string.IsNullOrEmpty(spineTarget) || !rowDefBySpineNodeId.ContainsKey(spineTarget))
            return;

        if (ShouldBlockRightClickResetForSpine(spineTarget))
        {
            TryLogSkillUnlearnCooldownBlocked();
            return;
        }

        TryExpireCommittedAbilityLingeringStateForSkillTreeReset(spineTarget);

        ResetCommittedSkillRowStateForSpine(spineTarget);
    }

    /// <summary>
    /// Clears the ability-row pick (when this tier participates) and passive-branch choices for this spine row
    /// (and sibling spines in the same multi-pick tier), matching per-row reset semantics used by the old Reset Tree flow.
    /// </summary>
    private void ResetCommittedSkillRowStateForSpine(string spineNodeId)
    {
        SkillType st = selectedSkill.skillType;

        if (!rowDefBySpineNodeId.TryGetValue(spineNodeId, out RowDef row))
            return;

        int expandKeyLevel = row.level;

        _abilityGroupSpineScratch.Clear();
        if (abilityTierPickMetaBySpineId.TryGetValue(spineNodeId, out AbilityTierPickMeta tm))
        {
            foreach (var kv in abilityTierPickMetaBySpineId)
            {
                AbilityTierPickMeta m = kv.Value;
                if (m.level == tm.level && m.groupSize == tm.groupSize)
                    _abilityGroupSpineScratch.Add(kv.Key);
            }

            skillsManager.ClearSkillAbilityRowPickForLevel(st, tm.level);
            for (int i = 0; i < _abilityGroupSpineScratch.Count; i++)
                skillsManager.ClearSkillChoiceSelectionForParentSpine(st, _abilityGroupSpineScratch[i]);
        }
        else
            skillsManager.ClearSkillChoiceSelectionForParentSpine(st, spineNodeId);

        expandedChoiceBranchesBySourceLevel.Remove(expandKeyLevel);
        BuildForSelectedSkill();
    }

    private void ShowTooltip(string nodeId, Transform anchor)
    {
        if (sharedTooltip == null)
            return;

        tooltipTitleByNodeId.TryGetValue(nodeId, out string title);
        tooltipBodyByNodeId.TryGetValue(nodeId, out string body);
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            if (rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef spineRow))
            {
                BuildTooltipCopy(spineRow, IsRowUnlocked(spineRow), out title, out body);
                body = AppendChoiceTooltipState(spineRow, body);
            }
            else if (choiceMetaByNodeId.TryGetValue(nodeId, out ChoiceNodeMeta cm)
                     && rowDefBySpineNodeId.TryGetValue(cm.parentSpineNodeId, out RowDef choiceParentRow))
                body = AppendChoiceTooltipState(choiceParentRow, body);
        }
        SkillTreeTooltipChrome chrome = SkillTreeTooltipChrome.None;
        if (rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef rowChrome) && ShouldUseMajorPassiveLinePresentation(rowChrome))
            chrome = SkillTreeTooltipChrome.MajorPassivePanel;
        else if (choiceMetaByNodeId.TryGetValue(nodeId, out ChoiceNodeMeta cmChrome)
                 && rowDefBySpineNodeId.TryGetValue(cmChrome.parentSpineNodeId, out RowDef parentChrome)
                 && ShouldUseMajorPassiveLinePresentation(parentChrome))
            chrome = SkillTreeTooltipChrome.MajorPassivePanel;

        sharedTooltip.ShowTextAt(
            anchor,
            string.IsNullOrWhiteSpace(title) ? "Node" : title,
            string.IsNullOrWhiteSpace(body) ? "No node data yet." : body,
            measureRect: tooltipBoundsRect != null ? tooltipBoundsRect : nodesRoot,
            heightRect: tooltipBoundsRect != null ? tooltipBoundsRect : nodesRoot,
            preferredSide: FlipInsideBounds.PreferredSide.Left,
            titleColor: null,
            useStatsDisplayHeader: false,
            skillTreeChrome: chrome,
            useHudTooltipScale: false);
    }

    private void HideTooltip()
    {
        sharedTooltip?.Hide();
    }

    private bool IsRowUnlocked(RowDef row)
    {
        if (selectedSkill == null || skillsManager == null)
            return false;
        return skillsManager.IsLevelUnlocked(selectedSkill.skillType, row.level);
    }

    private static bool TryGetNodeLevel(string nodeId, out int level)
    {
        level = 0;
        if (string.IsNullOrWhiteSpace(nodeId) || !nodeId.StartsWith("Lv"))
            return false;

        int end = nodeId.IndexOf('_');
        string numberPart = end > 2 ? nodeId.Substring(2, end - 2) : nodeId.Substring(2);
        return int.TryParse(numberPart, out level);
    }

    private string AppendChoiceTooltipState(RowDef row, string body)
    {
        List<SkillChoiceDefinition> choices = GetNonNullChoices(row.unlock);
        if (choices.Count <= 0 || !selectedSkill || !skillsManager)
            return body;

        int selectedChoice = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, SpineNodeId(row), -1);
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(body))
            sb.Append(body.Trim());
        if (selectedChoice >= 0 && selectedChoice < choices.Count)
        {
            sb.Append("\n\nActive Enhancement: ");
            sb.Append("<color=#33CC66>");
            sb.Append(!string.IsNullOrWhiteSpace(choices[selectedChoice].title) ? choices[selectedChoice].title.Trim() : $"Option {selectedChoice + 1}");
            sb.Append("</color>");
        }
        sb.Append("\n\nEnhancements:");
        for (int i = 0; i < choices.Count; i++)
        {
            string choiceName = !string.IsNullOrWhiteSpace(choices[i].title) ? choices[i].title.Trim() : $"Option {i + 1}";
            bool isSelected = i == selectedChoice;
            sb.Append("\n");
            sb.Append(isSelected ? "<color=#33CC66>• [Selected] " : "• ");
            sb.Append(choiceName);
            if (isSelected) sb.Append("</color>");
        }

        return sb.ToString();
    }

    private void RefreshChoiceSelectionVisuals()
    {
        if (selectedSkill && skillsManager)
        {
            foreach (var kv in choiceMetaByNodeId)
            {
                if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                    continue;

                ChoiceNodeMeta meta = kv.Value;
                int selectedChoice = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, meta.parentSpineNodeId, -1);
                node.SetSelected(selectedChoice == meta.choiceIndex);
            }
        }

        RefreshEnhanceButtonVisibility();
        RefreshNotSelectedPrompts();
    }

    private void RefreshNotSelectedPrompts()
    {
        if (nodeLookup.Count == 0)
            return;

        bool canQuery = selectedSkill != null && skillsManager != null;

        foreach (var kv in nodeLookup)
        {
            SkillTreeNodeUI node = kv.Value;
            if (node == null)
                continue;

            bool show = canQuery && ShouldShowNotSelectedPrompt(kv.Key, node);
            node.SetNotSelectedPrompt(show);
        }
    }

    private bool ShouldShowPassiveSpinePendingEnhancementChoice(string spineNodeId, RowDef row)
    {
        if (row.unlock == null)
            return false;
        List<SkillChoiceDefinition> choices = GetNonNullChoices(row.unlock);
        if (choices.Count <= 0)
            return false;
        if (skillsManager == null || selectedSkill == null)
            return false;

        // Major / capstone / ability: base node can be unlocked (or row-pick committed) before enhancement rows exist.
        // Only nudge with NotSelected once the earliest authored enhancement tier is reachable.
        if (row.type == SkillTreeNodeVisualType.MajorPassive
            || row.type == SkillTreeNodeVisualType.CapstonePassive
            || row.type == SkillTreeNodeVisualType.Ability)
        {
            int playerLv = skillsManager.GetLevel(selectedSkill.skillType);
            int minChoiceGate = int.MaxValue;
            for (int i = 0; i < choices.Count; i++)
            {
                int gate = ResolveChoiceUnlockLevel(row.level, row.type, choices[i]);
                if (gate < minChoiceGate)
                    minChoiceGate = gate;
            }

            if (minChoiceGate != int.MaxValue && playerLv < minChoiceGate)
                return false;
        }

        return skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, spineNodeId, -1) < 0;
    }

    private bool ShouldShowMajorPassiveNotSelected(string spineNodeId, RowDef row, AbilityTierPickMeta m)
    {
        if (m.groupSize < 2)
            return ShouldShowPassiveSpinePendingEnhancementChoice(spineNodeId, row);

        int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, m.level, -1);
        if (pick >= 0 && pick != m.ordinal)
            return false;
        if (pick >= 0 && pick == m.ordinal)
            return ShouldShowPassiveSpinePendingEnhancementChoice(spineNodeId, row);

        return true;
    }

    private bool ShouldShowNotSelectedPrompt(string nodeId, SkillTreeNodeUI node)
    {
        if (node == null || node.IsLocked() || !node.gameObject.activeInHierarchy)
            return false;

        if (selectedSkill == null || skillsManager == null)
            return false;

        if (choiceMetaByNodeId.ContainsKey(nodeId))
            return false;

        if (abilityTierPickMetaBySpineId.TryGetValue(nodeId, out AbilityTierPickMeta m))
        {
            if (!rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef row))
                return false;
            if (row.type == SkillTreeNodeVisualType.Ability)
            {
                if (!node.IsSelected())
                    return true;
                return ShouldShowPassiveSpinePendingEnhancementChoice(nodeId, row);
            }
            if (row.type == SkillTreeNodeVisualType.MajorPassive)
                return ShouldShowMajorPassiveNotSelected(nodeId, row, m);
            return false;
        }

        if (!rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef rowSpine))
            return false;

        if (rowSpine.type == SkillTreeNodeVisualType.CapstonePassive || rowSpine.type == SkillTreeNodeVisualType.MajorPassive)
            return ShouldShowPassiveSpinePendingEnhancementChoice(nodeId, rowSpine);

        return false;
    }

    private static bool SpineTypeSupportsEnhanceButton(SkillTreeNodeVisualType type)
    {
        return type == SkillTreeNodeVisualType.Ability
            || type == SkillTreeNodeVisualType.MajorPassive
            || type == SkillTreeNodeVisualType.CapstonePassive;
    }

    private bool ShouldShowEnhanceButtonForSpine(string spineNodeId, RowDef row, SkillTreeNodeUI node)
    {
        if (node == null || node.IsLocked())
            return false;
        if (!SpineTypeSupportsEnhanceButton(row.type))
            return false;
        if (GetNonNullChoices(row.unlock).Count <= 0)
            return false;
        if (selectedSkill == null || skillsManager == null)
            return false;
        if (skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, spineNodeId, -1) >= 0)
            return false;
        int playerSkillLevel = skillsManager.GetLevel(selectedSkill.skillType);
        if (playerSkillLevel < row.level)
            return false;
        return ShouldExposeChoicesForMultiAbilityParent(spineNodeId, row.level);
    }

    private void RefreshEnhanceButtonVisibility()
    {
        foreach (var kv in nodeLookup)
        {
            SkillTreeNodeUI n = kv.Value;
            if (n == null)
                continue;

            if (!rowDefBySpineNodeId.TryGetValue(kv.Key, out RowDef row))
            {
                n.SetEnhanceControl(false, null);
                continue;
            }

            if (ShouldShowEnhanceButtonForSpine(kv.Key, row, n))
            {
                string spineId = kv.Key;
                n.SetEnhanceControl(true, () => OnEnhanceButtonClicked(spineId));
            }
            else
                n.SetEnhanceControl(false, null);
        }
    }

    private void OnEnhanceButtonClicked(string spineNodeId)
    {
        if (!rowDefBySpineNodeId.TryGetValue(spineNodeId, out RowDef row))
            return;
        if (!SpineTypeSupportsEnhanceButton(row.type))
            return;
        if (GetNonNullChoices(row.unlock).Count <= 0)
            return;
        if (!nodeLookup.TryGetValue(spineNodeId, out SkillTreeNodeUI node) || node == null || node.IsLocked())
            return;

        PreferRuntimeSkillsManager();
        if (selectedSkill == null || skillsManager == null)
            return;
        if (skillsManager.GetLevel(selectedSkill.skillType) < row.level)
            return;
        if (skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, spineNodeId, -1) >= 0)
            return;
        if (!ShouldExposeChoicesForMultiAbilityParent(spineNodeId, row.level))
            return;

        ToggleExpandedChoiceBranchForSourceLevel(row.level);
    }

    private bool TryGetTierRangeForLevel(int level, out int tierStart, out int tierEndInclusive)
    {
        tierStart = tierEndInclusive = -1;
        for (int i = 0; i < layoutRowsCache.Count; i++)
        {
            if (layoutRowsCache[i].level != level)
                continue;
            tierStart = i;
            int j = i;
            while (j < layoutRowsCache.Count && layoutRowsCache[j].level == level)
                j++;
            tierEndInclusive = j - 1;
            return true;
        }
        return false;
    }

    private bool IsAbilityTierCollapsed(int level)
    {
        if (selectedSkill == null || skillsManager == null)
            return false;
        if (!TryGetTierRangeForLevel(level, out int g, out int e))
            return false;
        if (!TierIsMultiAbilityOnly(layoutRowsCache, g, e))
            return false;
        int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, level, -1);
        // After a pick, the tier stays collapsed (siblings hidden) until Reset Tree clears the pick.
        return pick >= 0;
    }

    private string ResolveTierDisplaySpineId(int tierStart, int tierEndInclusive)
    {
        if (layoutRowsCache.Count == 0 || tierStart < 0 || tierEndInclusive >= layoutRowsCache.Count)
            return string.Empty;

        if (!TierIsMultiAbilityOnly(layoutRowsCache, tierStart, tierEndInclusive))
            return SpineNodeId(layoutRowsCache[TierVerticalAnchorIndex(layoutRowsCache, tierStart, tierEndInclusive)]);

        int count = tierEndInclusive - tierStart + 1;
        int level = layoutRowsCache[tierStart].level;
        int pick = selectedSkill != null && skillsManager != null
            ? skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, level, -1)
            : -1;
        if (pick >= 0 && pick < count)
            return SpineNodeId(layoutRowsCache[tierStart + pick]);

        int anchor = TierVerticalAnchorIndex(layoutRowsCache, tierStart, tierEndInclusive);
        return SpineNodeId(layoutRowsCache[anchor]);
    }

    private float GetEffectiveSpineLayoutX(string spineId)
    {
        if (!spineLayoutXBySpineId.TryGetValue(spineId, out float baseX))
            return 0f;
        if (!abilityTierPickMetaBySpineId.TryGetValue(spineId, out AbilityTierPickMeta m))
            return baseX;
        if (selectedSkill == null || skillsManager == null)
            return baseX;

        int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, m.level, -1);
        if (pick >= 0 && pick == m.ordinal)
            return 0f;
        return baseX;
    }

    private bool ShouldExposeChoicesForMultiAbilityParent(string parentSpineId, int sourceLevel)
    {
        if (!TryGetTierRangeForLevel(sourceLevel, out int g, out int e))
            return true;
        if (!TierIsMultiAbilityOnly(layoutRowsCache, g, e))
            return true;
        if (selectedSkill == null || skillsManager == null)
            return true;

        int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, sourceLevel, -1);
        if (pick < 0)
            return false;

        string pickedSpine = SpineNodeId(layoutRowsCache[g + pick]);
        return parentSpineId == pickedSpine;
    }

    private void ToggleExpandedChoiceBranchForSourceLevel(int sourceLevel)
    {
        if (expandedChoiceBranchesBySourceLevel.Contains(sourceLevel))
            expandedChoiceBranchesBySourceLevel.Remove(sourceLevel);
        else
            expandedChoiceBranchesBySourceLevel.Add(sourceLevel);
        RefreshChoiceBranchVisibility();
    }

    private void RefreshAbilityTierLayoutAndVisibility()
    {
        if (layoutRowsCache.Count == 0)
        {
            RefreshEnhanceButtonVisibility();
            RefreshNotSelectedPrompts();
            return;
        }

        bool canQuery = selectedSkill != null && skillsManager != null;

        foreach (var kv in abilityTierPickMetaBySpineId)
        {
            if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                continue;

            AbilityTierPickMeta m = kv.Value;
            bool collapsed = canQuery && IsAbilityTierCollapsed(m.level);
            bool show = !canQuery || !collapsed
                || skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, m.level, -1) == m.ordinal;
            node.gameObject.SetActive(show);

            int pick = canQuery ? skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, m.level, -1) : -1;
            // Ability glow: only the committed row pick (not merely unlocked). Multi-row uses collapsed spine; single-row has no collapse.
            bool abilitySelectedForGlow = pick == m.ordinal && (m.groupSize < 2 || collapsed);
            node.SetSelected(abilitySelectedForGlow);

            float y = node.RectTransform.anchoredPosition.y;
            node.RectTransform.anchoredPosition = new Vector2(GetEffectiveSpineLayoutX(kv.Key), y);
        }

        foreach (var iv in interTierVerticalConnectors)
        {
            if (iv.conn == null)
                continue;

            string fromId;
            string toId;
            bool hasExplicit = !string.IsNullOrEmpty(iv.explicitFromId) && !string.IsNullOrEmpty(iv.explicitToId);
            if (hasExplicit)
            {
                fromId = iv.explicitFromId;
                toId = iv.explicitToId;
            }
            else
            {
                if (!TryGetTierRangeForLevel(iv.upperLevel, out int ug, out int ue))
                    continue;
                if (!TryGetTierRangeForLevel(iv.lowerLevel, out int lg, out int le))
                    continue;

                fromId = ResolveTierDisplaySpineId(ug, ue);
                toId = ResolveTierDisplaySpineId(lg, le);
            }
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
                continue;
            if (!nodeLookup.TryGetValue(fromId, out var a) || !nodeLookup.TryGetValue(toId, out var b) || a == null || b == null)
                continue;

            iv.conn.SetPositions(a, b);
            iv.conn.gameObject.SetActive(a.gameObject.activeInHierarchy && b.gameObject.activeInHierarchy);
        }

        RefreshEnhanceButtonVisibility();
        RefreshNotSelectedPrompts();
    }

    private void RefreshChoiceBranchVisibility()
    {
        if (choiceMetaByNodeId.Count == 0 && choiceBranchConnectors.Count == 0)
            return;

        bool canQuery = selectedSkill != null && skillsManager != null;

        for (int i = 0; i < choiceBranchConnectors.Count; i++)
        {
            ChoiceBranchConnectorRecord rec = choiceBranchConnectors[i];
            if (rec.conn == null)
                continue;

            bool multiOk = ShouldExposeChoicesForMultiAbilityParent(rec.parentSpineId, rec.sourceLevel);
            int sel = canQuery
                ? skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, rec.parentSpineId, -1)
                : -1;
            bool branchExpanded = expandedChoiceBranchesBySourceLevel.Contains(rec.sourceLevel);
            // Connectors: only while branch expanded and no pick yet; hide diagonals after a choice is committed.
            bool connectorsOk = multiOk && (!canQuery || (sel < 0 && branchExpanded));
            rec.conn.gameObject.SetActive(connectorsOk);
        }

        foreach (var kv in choiceMetaByNodeId)
        {
            ChoiceNodeMeta meta = kv.Value;
            if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                continue;

            bool multiOk = ShouldExposeChoicesForMultiAbilityParent(meta.parentSpineNodeId, meta.sourceLevel);
            int sel = canQuery
                ? skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, meta.parentSpineNodeId, -1)
                : -1;
            bool branchExpanded = expandedChoiceBranchesBySourceLevel.Contains(meta.sourceLevel);
            // Choice nodes: same visibility as branch connectors (hide after a pick).
            bool choiceNodeOk = multiOk && (!canQuery || (sel < 0 && branchExpanded));
            node.gameObject.SetActive(choiceNodeOk);
        }

        SyncChoiceNodesAndBranchConnectorPositions();
    }

    private void SyncChoiceNodesAndBranchConnectorPositions()
    {
        foreach (var kv in choiceMetaByNodeId)
        {
            if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                continue;
            ChoiceNodeMeta meta = kv.Value;
            float px = GetEffectiveSpineLayoutX(meta.parentSpineNodeId);
            node.RectTransform.anchoredPosition = new Vector2(px + meta.offsetXFromParent, meta.anchorY);
        }

        for (int i = 0; i < choiceBranchConnectors.Count; i++)
        {
            ChoiceBranchConnectorRecord rec = choiceBranchConnectors[i];
            if (rec.conn == null)
                continue;
            if (!nodeLookup.TryGetValue(rec.parentSpineId, out SkillTreeNodeUI a) || a == null)
                continue;
            if (!nodeLookup.TryGetValue(rec.choiceNodeId, out SkillTreeNodeUI b) || b == null)
                continue;
            rec.conn.SetPositions(a, b);
        }
    }

    /// <summary>
    /// Right-click from the abilities list: always scrolls to the tier; unlearns the committed pick when off cooldown.
    /// </summary>
    public void HandleAbilityListRowRightClick(int requiredLevel)
    {
        ScrollAbilityTierRowIntoView(requiredLevel);

        PreferRuntimeSkillsManager();
        if (selectedSkill == null || skillsManager == null)
            return;

        if (!TryResolveCommittedAbilitySpineAtLevel(requiredLevel, out string spineTarget))
            return;

        if (ShouldBlockRightClickResetForSpine(spineTarget))
        {
            TryLogSkillUnlearnCooldownBlocked();
            return;
        }

        TryExpireCommittedAbilityLingeringStateForSkillTreeReset(spineTarget);
        ResetCommittedSkillRowStateForSpine(spineTarget);
    }

    private bool TryResolveCommittedAbilitySpineAtLevel(int level, out string spineNodeId)
    {
        spineNodeId = null;
        if (selectedSkill == null || skillsManager == null)
            return false;

        int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, level, -1);
        if (pick < 0)
            return false;

        foreach (var kv in abilityTierPickMetaBySpineId)
        {
            AbilityTierPickMeta m = kv.Value;
            if (m.level != level || m.ordinal != pick)
                continue;

            spineNodeId = kv.Key;
            return !string.IsNullOrEmpty(spineNodeId) && rowDefBySpineNodeId.ContainsKey(spineNodeId);
        }

        return false;
    }

    /// <summary>
    /// Scrolls the nearest parent <see cref="ScrollRect"/> so a node on the given ability tier row is centered vertically in the viewport.
    /// </summary>
    public void ScrollAbilityTierRowIntoView(int requiredLevel)
    {
        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || nodesRoot == null)
            return;

        if (!rowYByLevel.TryGetValue(requiredLevel, out float rowY))
            return;

        SkillTreeNodeUI targetNode = null;
        const float tol = 1f;
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            SkillTreeNodeUI node = spawnedNodes[i];
            if (node == null || !node.gameObject.activeInHierarchy)
                continue;
            if (Mathf.Abs(node.RectTransform.anchoredPosition.y - rowY) <= tol)
            {
                targetNode = node;
                break;
            }
        }

        if (targetNode == null)
            return;

        RectTransform content = scroll.content;
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
        if (viewport == null)
            return;

        RectTransform target = targetNode.RectTransform;
        Transform contentParent = content.parent;
        if (contentParent == null)
            return;

        Canvas.ForceUpdateCanvases();

        Vector3 worldItem = target.TransformPoint(target.rect.center);
        Vector3 worldViewCenter = viewport.TransformPoint(viewport.rect.center);
        Vector3 worldDelta = worldItem - worldViewCenter;

        Vector3 localDelta = contentParent.InverseTransformVector(worldDelta);

        Vector2 next = content.anchoredPosition;
        next.y -= localDelta.y;
        content.anchoredPosition = next;
        scroll.velocity = Vector2.zero;
    }
}