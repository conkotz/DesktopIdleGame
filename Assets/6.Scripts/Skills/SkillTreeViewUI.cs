using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
    [Tooltip("Optional one-line hint: Tier 1/2/3 gates at skill L1 / L20 / L40. Leave empty to hide.")]
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
    [Tooltip("Center-to-center spacing for multiple Ability unlocks at the same level (symmetric around the vertical spine).")]
    [SerializeField] private float abilitySiblingSpacing = 140f;
    [Header("Choice Selection Rules")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCombatState playerCombatState;
    [SerializeField] private float choiceChangePostCombatLockSeconds = 5f;

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

    private SkillTreeNodeUI selectedNode;
    private SkillDefinition _lastBuiltSkill;
    private float choiceChangeUnlockedAt;
    private bool isCombatStateSubscribed;
    private readonly HashSet<int> _pendingUnlockGlowLevels = new();

    public event System.Action<int> UnlockGlowAcknowledgedByHover;

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
        if (!playerController)
            playerController = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        ResolveCombatStateReference();
        SubscribeCombatState();
        PreferRuntimeSkillsManager();
        BuildForSelectedSkill();
    }

    private void OnEnable()
    {
        SubscribeCombatState();
    }

    private void OnDisable()
    {
        UnsubscribeCombatState();
    }

    private void OnDestroy()
    {
        UnsubscribeCombatState();
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
                BuildTooltipCopy(row.level, row.type, row.unlock, unlocked, out string mainTitle, out string mainBody);
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
                BuildChoiceTooltipCopy(cm.unlockLevel, choice, parentRow.unlock, unlocked, out string cTitle, out string cBody);
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
    /// Assign to the Reset Tree button OnClick: clears <b>all</b> skills’ choice branches and multi-ability picks, then rebuilds this tree.
    /// </summary>
    public void OnResetSkillTreeButtonClicked()
    {
        PreferRuntimeSkillsManager();
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (skillsManager != null)
            skillsManager.ResetAllSkillTreeSelections();

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
            SkillUnlockType.MinorPassive => 0,
            SkillUnlockType.MajorPassive => 1,
            SkillUnlockType.Unlock => 2,
            SkillUnlockType.Ability => 3,
            SkillUnlockType.CapstonePassive => 4,
            _ => 99
        };
    }

    private static SkillTreeNodeVisualType MapUnlockToNodeType(SkillUnlockDefinition u)
    {
        return u.unlockType switch
        {
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
            float currentHalf = SkillTreeNodeUI.GetVisualBoxSize(row.type).y * 0.5f;
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
                y = layoutRowY[i - 1] - prevHalf - Mathf.Max(0f, ScaledLayout(rowGap)) - currentHalf;
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
                float xPos = 0f;
                for (int i = g; i <= end; i++)
                {
                    layoutRowX[i] = xPos;
                    if (i < end)
                    {
                        float halfA = SkillTreeNodeUI.GetVisualBoxSize(rows[i].type).x * 0.5f;
                        float halfB = SkillTreeNodeUI.GetVisualBoxSize(rows[i + 1].type).x * 0.5f;
                        xPos += halfA + Mathf.Max(0f, ScaledLayout(sameLevelNodeGap)) + halfB;
                    }
                }
            }

            g = end + 1;
        }
    }

    private static bool TierIsMultiAbilityOnly(List<RowDef> rows, int tierStart, int tierEndInclusive)
    {
        int count = tierEndInclusive - tierStart + 1;
        if (count < 2)
            return false;
        for (int i = tierStart; i <= tierEndInclusive; i++)
        {
            if (rows[i].type != SkillTreeNodeVisualType.Ability)
                return false;
        }
        return true;
    }

    private static int TierVerticalAnchorIndex(List<RowDef> rows, int tierStart, int tierEndInclusive)
    {
        int count = tierEndInclusive - tierStart + 1;
        if (TierIsMultiAbilityOnly(rows, tierStart, tierEndInclusive))
            return tierStart + (count - 1) / 2;
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
                while (labelIndex < idx && rows[labelIndex].type == SkillTreeNodeVisualType.MinorPassive)
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

            string tierCaption = TierRowCaptionForSkillLevel(tierLevel);
            if (levelTierRowLabelPrefab != null && !string.IsNullOrEmpty(tierCaption))
            {
                RectTransform tierParent = ResolveLevelTierRowLabelParent();
                if (tierParent != null)
                {
                    var tr = Instantiate(levelTierRowLabelPrefab, tierParent);
                    tr.text = tierCaption;
                    tr.alignment = TextAlignmentOptions.MidlineRight;

                    RectTransform rtt = tr.rectTransform;
                    rtt.anchorMin = new Vector2(1f, 1f);
                    rtt.anchorMax = new Vector2(1f, 1f);
                    rtt.pivot = new Vector2(1f, 0.5f);
                    float tierY = ResolveTierLabelAnchoredY(tierParent, layoutRowY[labelIndex]);
                    float inset = Mathf.Max(0f, levelTierLabelRightInset) + Mathf.Max(0f, levelTierLabelExtraRightPaddingPx);
                    rtt.anchoredPosition = new Vector2(-inset, tierY);
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
    /// Display names for the right-side tier row labels.
    /// Combat skills keep the existing mapping; gathering skills (woodcutting/fishing/mining) use the authored schedule.
    /// </summary>
    private string TierRowCaptionForSkillLevel(int level)
    {
        bool isGatheringSkill = selectedSkill != null &&
            (selectedSkill.skillType == SkillType.Woodcutting ||
             selectedSkill.skillType == SkillType.Fishing ||
             selectedSkill.skillType == SkillType.Mining);

        if (isGatheringSkill)
        {
            return level switch
            {
                1 => "Unlock",
                5 => "Ability",
                10 or 20 or 30 or 40 => "",
                15 => "Major Passive",
                25 => "Ability",
                35 => "Major Passive",
                45 => "Ability",
                50 => "Capstone",
                _ => ""
            };
        }

        return level switch
        {
            1 => "Unlock",
            5 or 15 or 25 or 35 or 45 => "Ability",
            10 or 20 or 30 or 40 => "Major Passive",
            50 => "Capstone",
            _ => ""
        };
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
            BuildTooltipCopy(row.level, row.type, row.unlock, unlocked, out string mainTitle, out string mainBody);
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

        // Spawn choices using per-choice unlock levels.
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            List<SkillChoiceDefinition> choices = GetNonNullChoices(row.unlock);
            if (choices.Count <= 0)
                continue;

            string parentSpineId = SpineNodeId(row);
            float parentX = layoutRowX[i];
            float center = (choices.Count - 1) * 0.5f;
            for (int choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
            {
                SkillChoiceDefinition choice = choices[choiceIndex];
                int choiceUnlockLevel = ResolveChoiceUnlockLevel(row.level, row.type);
                if (!rowYByLevel.TryGetValue(choiceUnlockLevel, out float targetY))
                    continue; // no authored row at that level yet

                float xStep = ScaledLayout(row.type == SkillTreeNodeVisualType.CapstonePassive ? capstoneChoiceOffsetX : choiceOffsetX);
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
                float offsetX = (choiceIndex - center) * xStep;
                float choiceY = targetY + yOffset;
                float choiceX = parentX + offsetX;
                bool unlocked = row.level <= currentSkillLevel && choiceUnlockLevel <= currentSkillLevel;
                BuildChoiceTooltipCopy(choiceUnlockLevel, choice, row.unlock, unlocked, out string cTitle, out string cBody);
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
        }

        // Choices
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            List<SkillChoiceDefinition> choices = GetNonNullChoices(row.unlock);
            if (choices.Count <= 0) continue;

            string source = SpineNodeId(row);
            for (int choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
            {
                SkillChoiceDefinition choice = choices[choiceIndex];
                int choiceUnlockLevel = ResolveChoiceUnlockLevel(row.level, row.type);
                string choiceNodeId = ChoiceId(source, choiceUnlockLevel, choiceIndex);
                if (TrySpawnConnectorInternal(source, choiceNodeId, out var branchConn))
                    choiceBranchConnectors.Add(new ChoiceBranchConnectorRecord(source, choiceNodeId, row.level, branchConn));
            }
        }
    }

    private static int ResolveChoiceUnlockLevel(int sourceLevel, SkillTreeNodeVisualType sourceType)
    {
        if (sourceType == SkillTreeNodeVisualType.CapstonePassive)
            return 50;
        return sourceLevel + 3;
    }

    private static string SpineNodeId(RowDef row) => $"Lv{row.level}_{row.slotAtLevel}";

    private static string ChoiceId(string parentSpineNodeId, int unlockLevel, int index) =>
        $"{parentSpineNodeId}_ChoiceLv{unlockLevel}_{index}";

    private static void BuildTooltipCopy(int level, SkillTreeNodeVisualType type, SkillUnlockDefinition unlock, bool isUnlocked, out string title, out string body)
    {
        string unlockTitle = unlock != null && !string.IsNullOrWhiteSpace(unlock.title) ? unlock.title.Trim() : "Untitled";
        string desc = unlock != null && !string.IsNullOrWhiteSpace(unlock.description) ? unlock.description.Trim() : "No description yet.";

        if (unlock != null && unlock.unlockType == SkillUnlockType.Unlock)
        {
            title = unlockTitle;
            string unlockTypeLabel = TypeLabel(SkillTreeNodeVisualType.Unlock);
            body = $"{unlockTypeLabel} {BuildStatusLine(isUnlocked)}\nUnlocks at level {level}\n\n{desc}";
            return;
        }

        string typeLabel = TypeLabel(type);
        title = unlockTitle;
        body = $"{typeLabel} {BuildStatusLine(isUnlocked)}\nUnlocks at Lv{level}\n\n{desc}";
    }

    private static void BuildChoiceTooltipCopy(int unlockLevel, SkillChoiceDefinition choice, SkillUnlockDefinition parentUnlock, bool isUnlocked, out string title, out string body)
    {
        string unlockTitle = choice != null && !string.IsNullOrWhiteSpace(choice.title)
            ? choice.title.Trim()
            : (parentUnlock != null && !string.IsNullOrWhiteSpace(parentUnlock.title) ? parentUnlock.title.Trim() : "Untitled");
        string desc = choice != null && !string.IsNullOrWhiteSpace(choice.description)
            ? choice.description.Trim()
            : "No description yet.";
        string typeLabel = TypeLabel(SkillTreeNodeVisualType.Choice);
        title = unlockTitle;
        body = $"{typeLabel} {BuildStatusLine(isUnlocked)}\nUnlocks at Lv{unlockLevel}\n\n{desc}";
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

    private static string TypeLabel(SkillTreeNodeVisualType type)
    {
        return type switch
        {
            SkillTreeNodeVisualType.MinorPassive => "Minor Passive",
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

        if (unlock.ability != null && unlock.ability.icon != null)
            return unlock.ability.icon;
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
        if (choice.ability != null && choice.ability.icon != null)
            return choice.ability.icon;
        return null;
    }

    private void SpawnNode(string nodeId, Vector2 pos, SkillTreeNodeVisualType type, string tooltipTitle, string tooltipBody, bool unlocked, Sprite iconSprite = null)
    {
        if (nodePrefab == null || nodesRoot == null) return;

        var node = Instantiate(nodePrefab, nodesRoot);
        node.RectTransform.anchoredPosition = pos;
        node.ApplyVisualType(type);
        node.SetIcon(iconSprite, iconSprite != null);
        node.SetLocked(!unlocked);
        node.SetSelected(false);
        node.SetClick(() => OnNodeClicked(nodeId, node));
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
            if (!selectedSkill || !skillsManager)
                return;
            if (!CanChangeChoiceNow(out string reason))
            {
                if (playerController != null && !string.IsNullOrWhiteSpace(reason))
                    playerController.ShowPopup(reason);
                return;
            }

            int current = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, choiceMeta.sourceLevel, -1);
            if (current != choiceMeta.choiceIndex)
                skillsManager.SetSkillChoiceSelection(selectedSkill.skillType, choiceMeta.sourceLevel, choiceMeta.choiceIndex);

            expandedChoiceBranchesBySourceLevel.Remove(choiceMeta.sourceLevel);
            RefreshChoiceSelectionVisuals();
            RefreshChoiceBranchVisibility();
            ShowTooltip(nodeId, node.transform);
            return;
        }

        if (abilityTierPickMetaBySpineId.TryGetValue(nodeId, out AbilityTierPickMeta tierMeta))
        {
            if (!selectedSkill || !skillsManager)
                return;
            if (!CanChangeChoiceNow(out string reason))
            {
                if (playerController != null && !string.IsNullOrWhiteSpace(reason))
                    playerController.ShowPopup(reason);
                return;
            }

            if (tierMeta.groupSize >= 2)
            {
                int pick = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, tierMeta.level, -1);

                if (pick >= 0 && pick == tierMeta.ordinal)
                {
                    // Committed ability: never re-open the multi-ability row (reset only). Toggle this row's choice branch only.
                    if (rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef rowForChoices)
                        && GetNonNullChoices(rowForChoices.unlock).Count > 0)
                        ToggleExpandedChoiceBranchForSourceLevel(tierMeta.level);
                    RefreshAbilityTierLayoutAndVisibility();
                    ShowTooltip(nodeId, node.transform);
                    return;
                }

                skillsManager.SetSkillAbilityRowPick(selectedSkill.skillType, tierMeta.level, tierMeta.ordinal);
            }
            else
            {
                int pickOne = skillsManager.GetSkillAbilityRowPick(selectedSkill.skillType, tierMeta.level, -1);
                if (pickOne == 0)
                {
                    if (rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef rowOne)
                        && GetNonNullChoices(rowOne.unlock).Count > 0)
                        ToggleExpandedChoiceBranchForSourceLevel(tierMeta.level);
                    RefreshAbilityTierLayoutAndVisibility();
                    ShowTooltip(nodeId, node.transform);
                    return;
                }

                skillsManager.SetSkillAbilityRowPick(selectedSkill.skillType, tierMeta.level, 0);
            }

            RefreshAbilityTierLayoutAndVisibility();
            RefreshChoiceBranchVisibility();
            ShowTooltip(nodeId, node.transform);
            return;
        }

        if (rowDefBySpineNodeId.TryGetValue(nodeId, out RowDef spineToggleRow)
            && GetNonNullChoices(spineToggleRow.unlock).Count > 0
            && selectedSkill
            && skillsManager)
        {
            if (spineToggleRow.type == SkillTreeNodeVisualType.MajorPassive
                || spineToggleRow.type == SkillTreeNodeVisualType.CapstonePassive)
            {
                if (!CanChangeChoiceNow(out string spineReason))
                {
                    if (playerController != null && !string.IsNullOrWhiteSpace(spineReason))
                        playerController.ShowPopup(spineReason);
                    return;
                }

                ToggleExpandedChoiceBranchForSourceLevel(spineToggleRow.level);
                ShowTooltip(nodeId, node.transform);
                return;
            }
        }

        if (selectedNode != null)
            selectedNode.SetSelected(false);

        selectedNode = node;
        selectedNode.SetSelected(true);

        ShowTooltip(nodeId, node.transform);
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
                body = AppendChoiceTooltipState(spineRow, body);
            else if (choiceMetaByNodeId.TryGetValue(nodeId, out ChoiceNodeMeta cm)
                     && rowDefBySpineNodeId.TryGetValue(cm.parentSpineNodeId, out RowDef choiceParentRow))
                body = AppendChoiceTooltipState(choiceParentRow, body);
        }
        sharedTooltip.ShowTextAt(
            anchor,
            string.IsNullOrWhiteSpace(title) ? "Node" : title,
            string.IsNullOrWhiteSpace(body) ? "No node data yet." : body,
            measureRect: tooltipBoundsRect != null ? tooltipBoundsRect : nodesRoot,
            heightRect: tooltipBoundsRect != null ? tooltipBoundsRect : nodesRoot,
            preferredSide: FlipInsideBounds.PreferredSide.Left
        );
    }

    private void HideTooltip()
    {
        sharedTooltip?.Hide();
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

        int selectedChoice = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, row.level, -1);
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
        if (!selectedSkill || !skillsManager)
            return;

        foreach (var kv in choiceMetaByNodeId)
        {
            if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                continue;

            ChoiceNodeMeta meta = kv.Value;
            int selectedChoice = skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, meta.sourceLevel, -1);
            node.SetSelected(selectedChoice == meta.choiceIndex);
        }
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
            return;

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
            if (!TryGetTierRangeForLevel(iv.upperLevel, out int ug, out int ue))
                continue;
            if (!TryGetTierRangeForLevel(iv.lowerLevel, out int lg, out int le))
                continue;

            string fromId = ResolveTierDisplaySpineId(ug, ue);
            string toId = ResolveTierDisplaySpineId(lg, le);
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
                continue;
            if (!nodeLookup.TryGetValue(fromId, out var a) || !nodeLookup.TryGetValue(toId, out var b) || a == null || b == null)
                continue;

            iv.conn.SetPositions(a, b);
            iv.conn.gameObject.SetActive(a.gameObject.activeInHierarchy && b.gameObject.activeInHierarchy);
        }
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
            bool passiveOk = !canQuery
                || skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, rec.sourceLevel, -1) < 0
                || expandedChoiceBranchesBySourceLevel.Contains(rec.sourceLevel);
            rec.conn.gameObject.SetActive(multiOk && passiveOk);
        }

        foreach (var kv in choiceMetaByNodeId)
        {
            ChoiceNodeMeta meta = kv.Value;
            if (!nodeLookup.TryGetValue(kv.Key, out SkillTreeNodeUI node) || node == null)
                continue;

            bool multiOk = ShouldExposeChoicesForMultiAbilityParent(meta.parentSpineNodeId, meta.sourceLevel);
            bool passiveOk = !canQuery
                || skillsManager.GetSkillChoiceSelection(selectedSkill.skillType, meta.sourceLevel, -1) < 0
                || expandedChoiceBranchesBySourceLevel.Contains(meta.sourceLevel);
            node.gameObject.SetActive(multiOk && passiveOk);
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

    private bool CanChangeChoiceNow(out string reason)
    {
        reason = string.Empty;
        if (!playerController)
            return true;

        if (playerController.InCombat)
        {
            reason = "Cannot change nodes during combat.";
            return false;
        }

        float remaining = choiceChangeUnlockedAt - Time.time;
        if (remaining > 0f)
        {
            reason = $"Cannot change nodes during combat. Wait {Mathf.CeilToInt(remaining)}s after combat.";
            return false;
        }

        return true;
    }

    private void ResolveCombatStateReference()
    {
        if (!playerController)
            playerController = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!playerCombatState && playerController)
            playerCombatState = playerController.GetComponent<PlayerCombatState>();
    }

    private void SubscribeCombatState()
    {
        ResolveCombatStateReference();
        if (isCombatStateSubscribed || playerCombatState == null)
            return;

        if (playerCombatState != null)
        {
            playerCombatState.OnCombatStateChanged += HandleCombatStateChanged;
            isCombatStateSubscribed = true;
        }
    }

    private void UnsubscribeCombatState()
    {
        if (!isCombatStateSubscribed || playerCombatState == null)
            return;

        if (playerCombatState != null)
            playerCombatState.OnCombatStateChanged -= HandleCombatStateChanged;
        isCombatStateSubscribed = false;
    }

    private void HandleCombatStateChanged(bool inCombat)
    {
        if (!inCombat)
            choiceChangeUnlockedAt = Time.time + Mathf.Max(0f, choiceChangePostCombatLockSeconds);
    }
}