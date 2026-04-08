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
    [SerializeField] private SkillTreeNodeUI nodePrefab;
    [SerializeField] private SkillTreeConnectorUI connectorPrefab;

    [Header("Layout (placeholder scaffold)")]
    [SerializeField] private float startY = -48f;
    [Tooltip("Minimum clear space between adjacent spine node edges.")]
    [SerializeField] private float rowGap = 20f;
    [Tooltip("Optional extra separation before level 50 capstone row.")]
    [SerializeField] private float extraGapBeforeCapstone = 10f;
    [Tooltip("Horizontal offset of choice nodes from spine (most rows).")]
    [SerializeField] private float choiceOffsetX = 210f;
    [Tooltip("Horizontal offset of choice nodes from spine on the capstone row.")]
    [SerializeField] private float capstoneChoiceOffsetX = 210f;
    [Tooltip("Vertical offset for choice branches on single center-node rows (Ability/Major). Negative moves choices downward.")]
    [SerializeField] private float singleNodeChoiceYOffset = -80f;
    [Tooltip("Horizontal offset for side unlock nodes on Lv20/Lv40.")]
    [SerializeField] private float sideUnlockOffsetX = 120f;
    [Tooltip("If true, show Lv labels for every level (including minor filler rows).")]
    [SerializeField] private bool showAllLevelLabels = true;

    private readonly List<SkillTreeNodeUI> spawnedNodes = new();
    private readonly List<SkillTreeConnectorUI> spawnedConnectors = new();
    private readonly List<TMP_Text> spawnedLevelLabels = new();
    private readonly Dictionary<string, SkillTreeNodeUI> nodeLookup = new();
    private readonly Dictionary<int, float> spineRowCenterY = new();

    private SkillTreeNodeUI selectedNode;

    private void Start()
    {
        // TEMPORARY: placeholder scaffold only — replace with data-driven build from skill definitions.
        BuildPlaceholderScaffold();
    }

    /// <summary>
    /// TEMPORARY: visual-only placeholder for one skill tree spine (levels 1–50) plus choice nodes.
    /// Replace later with: load nodes + edges from ScriptableObject / JSON / DB per skill.
    /// </summary>
    public void BuildPlaceholderScaffold()
    {
        ClearTree();
        BuildPlaceholderSpineRowLayout();

        // TEMPORARY: left-side level column (labels only; node labels are disabled for tooltips later).
        SpawnLevelColumnLabels();

        // --- Spawn nodes by level row (some rows contain multiple nodes) ---
        for (int level = 1; level <= 50; level++)
        {
            float y = GetSpineRowY(level);
            foreach (var def in GetRowNodeDefs(level, y))
                SpawnNode(def.id, def.pos, def.type);
        }

        // --- Connectors: spine chain ---
        for (int level = 2; level <= 50; level++)
            SpawnConnector(SpineId(level - 1), SpineId(level));

        // --- For every Ability/MajorPassive center node, attach a choice branch ---
        for (int level = 1; level <= 50; level++)
            SpawnChoiceConnectorsForSource(level);
    }

    private void SpawnChoiceConnectorsForSource(int sourceLevel)
    {
        string left = ChoiceId(sourceLevel, sourceLevel, 0);
        string right = ChoiceId(sourceLevel, sourceLevel, 1);
        if (!nodeLookup.ContainsKey(left) || !nodeLookup.ContainsKey(right))
            return;

        SpawnConnector(SpineId(sourceLevel), left);
        SpawnConnector(SpineId(sourceLevel), right);
    }

    private readonly struct NodeDef
    {
        public readonly string id;
        public readonly Vector2 pos;
        public readonly SkillTreeNodeVisualType type;

        public NodeDef(string id, Vector2 pos, SkillTreeNodeVisualType type)
        {
            this.id = id;
            this.pos = pos;
            this.type = type;
        }
    }

    /// <summary>
    /// TEMPORARY scaffold row contents for the new progression structure.
    /// Rows may contain multiple nodes (eg. Major + Unlock side-by-side).
    /// </summary>
    private IEnumerable<NodeDef> GetRowNodeDefs(int level, float y)
    {
        // Simplified rows: keep only center node + downward choices (no extra unlock side nodes).
        if (level == 20 || level == 40)
        {
            yield return new NodeDef(SpineId(level), new Vector2(0f, y), SkillTreeNodeVisualType.MajorPassive);
            yield return new NodeDef(SideUnlockId(level), new Vector2(sideUnlockOffsetX, y), SkillTreeNodeVisualType.Unlock);
            yield return new NodeDef(ChoiceId(level, level, 0), new Vector2(-choiceOffsetX, y + singleNodeChoiceYOffset), SkillTreeNodeVisualType.Choice);
            yield return new NodeDef(ChoiceId(level, level, 1), new Vector2(choiceOffsetX, y + singleNodeChoiceYOffset), SkillTreeNodeVisualType.Choice);
            yield break;
        }

        // Ability rows with choice branches.
        if (level is 15 or 25 or 35 or 45)
        {
            yield return new NodeDef(SpineId(level), new Vector2(0f, y), SkillTreeNodeVisualType.Ability);
            yield return new NodeDef(ChoiceId(level, level, 0), new Vector2(-choiceOffsetX, y + singleNodeChoiceYOffset), SkillTreeNodeVisualType.Choice);
            yield return new NodeDef(ChoiceId(level, level, 1), new Vector2(choiceOffsetX, y + singleNodeChoiceYOffset), SkillTreeNodeVisualType.Choice);
            yield break;
        }

        // Lv5 starting ability with choice branch.
        if (level == 5)
        {
            yield return new NodeDef(SpineId(level), new Vector2(0f, y), SkillTreeNodeVisualType.Ability);
            yield return new NodeDef(ChoiceId(level, level, 0), new Vector2(-choiceOffsetX, y + singleNodeChoiceYOffset), SkillTreeNodeVisualType.Choice);
            yield return new NodeDef(ChoiceId(level, level, 1), new Vector2(choiceOffsetX, y + singleNodeChoiceYOffset), SkillTreeNodeVisualType.Choice);
            yield break;
        }

        // Capstone row with immediate capstone choices.
        if (level == 50)
        {
            yield return new NodeDef(SpineId(level), new Vector2(0f, y), SkillTreeNodeVisualType.CapstonePassive);
            yield return new NodeDef(ChoiceId(level, 50, 0), new Vector2(-capstoneChoiceOffsetX, y), SkillTreeNodeVisualType.Choice);
            yield return new NodeDef(ChoiceId(level, 50, 1), new Vector2(capstoneChoiceOffsetX, y), SkillTreeNodeVisualType.Choice);
            yield break;
        }

        // Major passive rows with own choice branches.
        if (level is 10 or 30)
        {
            yield return new NodeDef(SpineId(level), new Vector2(0f, y), SkillTreeNodeVisualType.MajorPassive);
            yield return new NodeDef(ChoiceId(level, level, 0), new Vector2(-choiceOffsetX, y + singleNodeChoiceYOffset), SkillTreeNodeVisualType.Choice);
            yield return new NodeDef(ChoiceId(level, level, 1), new Vector2(choiceOffsetX, y + singleNodeChoiceYOffset), SkillTreeNodeVisualType.Choice);
            yield break;
        }

        // Single spine node rows.
        GetSpineNodePresentation(level, out SkillTreeNodeVisualType visual, out string typeDisplay);
        yield return new NodeDef(SpineId(level), new Vector2(0f, y), visual);
    }

    /// <summary>
    /// Placeholder mapping: which visual type and TypeText each spine level uses for this scaffold pattern.
    /// Real data: each skill tree may define its own level → node entries.
    /// </summary>
    private static void GetSpineNodePresentation(int level, out SkillTreeNodeVisualType visual, out string typeDisplay)
    {
        // New scaffold mapping (single-node rows only; multi-node rows handled in GetRowNodeDefs).
        if (level == 1)
        {
            visual = SkillTreeNodeVisualType.Unlock;
            typeDisplay = string.Empty;
            return;
        }

        if (level is 5)
        {
            visual = SkillTreeNodeVisualType.Ability;
            typeDisplay = string.Empty;
            return;
        }

        if (level is 10 or 30)
        {
            visual = SkillTreeNodeVisualType.MajorPassive;
            typeDisplay = string.Empty;
            return;
        }

        if (level is 20 or 40)
        {
            // Special row handled in GetRowNodeDefs, but center node is still MajorPassive.
            visual = SkillTreeNodeVisualType.MajorPassive;
            typeDisplay = string.Empty;
            return;
        }

        if (level is 15 or 25 or 35 or 45)
        {
            // Ability + choices row handled elsewhere.
            visual = SkillTreeNodeVisualType.Ability;
            typeDisplay = string.Empty;
            return;
        }

        if (level == 50)
        {
            visual = SkillTreeNodeVisualType.CapstonePassive;
            typeDisplay = string.Empty;
            return;
        }

        // Everything else: minor filler.
        visual = SkillTreeNodeVisualType.MinorPassive;
        typeDisplay = string.Empty;
    }

    private static string SpineId(int level) => $"Lv{level}";
    private static string SideUnlockId(int level) => $"Lv{level}_SideUnlock";
    private static string ChoiceId(int unlockLevel, int sourceMilestoneLevel, int index) =>
        $"Lv{unlockLevel}_ChoiceFrom{sourceMilestoneLevel}_{index}";
    private static string LevelLabel(int level) => $"Lv{level}";

    /// <summary>
    /// TEMPORARY scaffold layout pass.
    /// Computes center Y for each level using node heights + minimum row gap.
    /// This keeps mixed-size rows visually consistent and is easy to swap for real data later.
    /// </summary>
    private void BuildPlaceholderSpineRowLayout()
    {
        spineRowCenterY.Clear();

        for (int level = 1; level <= 50; level++)
        {
            float currentHalf = GetRowVisualHalfHeight(level);

            if (level == 1)
            {
                spineRowCenterY[level] = startY;
                continue;
            }

            int prevLevel = level - 1;
            float prevHalf = GetRowVisualHalfHeight(prevLevel);

            float gap = Mathf.Max(0f, rowGap);
            if (level == 50)
                gap += Mathf.Max(0f, extraGapBeforeCapstone);

            spineRowCenterY[level] = spineRowCenterY[prevLevel] - prevHalf - gap - currentHalf;
        }
    }

    private float GetRowVisualHalfHeight(int level)
    {
        float maxH = 0f;

        foreach (var def in GetRowNodeDefs(level, 0f))
        {
            float h = SkillTreeNodeUI.GetVisualBoxSize(def.type).y;
            if (h > maxH) maxH = h;
        }

        return maxH * 0.5f;
    }

    private float GetSpineRowY(int level)
    {
        if (spineRowCenterY.TryGetValue(level, out float y))
            return y;

        return startY;
    }

    public void ClearTree()
    {
        foreach (var n in spawnedNodes)
            if (n != null) Destroy(n.gameObject);

        foreach (var c in spawnedConnectors)
            if (c != null) Destroy(c.gameObject);

        foreach (var t in spawnedLevelLabels)
            if (t != null) Destroy(t.gameObject);

        spawnedNodes.Clear();
        spawnedConnectors.Clear();
        spawnedLevelLabels.Clear();
        nodeLookup.Clear();
        spineRowCenterY.Clear();
        selectedNode = null;
    }

    private void SpawnLevelColumnLabels()
    {
        if (levelsRoot == null || levelRowLabelPrefab == null)
            return;

        for (int level = 1; level <= 50; level++)
        {
            if (!showAllLevelLabels)
            {
                // Show labels only for non-minor rows unless explicitly enabled.
                bool isSpecialRow = level is 1 or 5 or 10 or 15 or 20 or 25 or 30 or 35 or 40 or 45 or 50;
                if (!isSpecialRow)
                    continue;
            }

            var t = Instantiate(levelRowLabelPrefab, levelsRoot);
            t.text = $"Lv{level}";
            t.alignment = TextAlignmentOptions.MidlineRight;

            RectTransform rt = t.rectTransform;
            // LevelsRoot defines the whole column placement. Labels position locally within it.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, GetSpineRowY(level));

            spawnedLevelLabels.Add(t);
        }
    }

    private void SpawnNode(string nodeId, Vector2 pos, SkillTreeNodeVisualType type)
    {
        if (nodePrefab == null || nodesRoot == null) return;

        var node = Instantiate(nodePrefab, nodesRoot);
        node.RectTransform.anchoredPosition = pos;

        node.ApplyVisualType(type);
        node.SetLocked(false);
        node.SetSelected(false);
        node.SetClick(() => OnNodeClicked(node));

        spawnedNodes.Add(node);
        nodeLookup[nodeId] = node;
    }

    private void SpawnConnector(string from, string to)
    {
        if (!nodeLookup.TryGetValue(from, out var a)) return;
        if (!nodeLookup.TryGetValue(to, out var b)) return;

        var conn = Instantiate(connectorPrefab, connectorsRoot);
        conn.SetPositions(a, b);

        spawnedConnectors.Add(conn);
    }

    private void OnNodeClicked(SkillTreeNodeUI node)
    {
        if (node == null || node.IsLocked()) return;

        if (selectedNode != null)
            selectedNode.SetSelected(false);

        selectedNode = node;
        selectedNode.SetSelected(true);
    }
}