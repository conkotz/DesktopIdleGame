using System.Collections.Generic;
using UnityEngine;

public class SkillTreeViewUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform nodesRoot;
    [SerializeField] private RectTransform connectorsRoot;
    [SerializeField] private SkillTreeNodeUI nodePrefab;
    [SerializeField] private SkillTreeConnectorUI connectorPrefab;

    [Header("Layout (placeholder scaffold)")]
    [SerializeField] private float startY = -48f;
    [Tooltip("Minimum clear space between adjacent spine node edges.")]
    [SerializeField] private float rowGap = 20f;
    [Tooltip("Optional extra separation before level 50 capstone row.")]
    [SerializeField] private float extraGapBeforeCapstone = 10f;
    [Tooltip("Horizontal offset of choice nodes from spine — increase if side labels overlap the center column.")]
    [SerializeField] private float branchOffsetX = 210f;

    private readonly List<SkillTreeNodeUI> spawnedNodes = new();
    private readonly List<SkillTreeConnectorUI> spawnedConnectors = new();
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

        // --- Spawn center spine: Lv1 … Lv50 ---
        for (int level = 1; level <= 50; level++)
        {
            string id = SpineId(level);
            Vector2 pos = new Vector2(0f, GetSpineRowY(level));
            GetSpineNodePresentation(level, out SkillTreeNodeVisualType visual, out string typeDisplay);
            SpawnNode(id, LevelLabel(level), typeDisplay, pos, visual);
        }

        // --- Choice nodes (same row Y as unlock tier; diagonals connect from earlier milestone) ---
        // Ability milestones:
        // Lv10 Ability milestone → choices unlock at Lv15
        SpawnChoiceRow(choiceUnlockLevel: 15, sourceMilestoneLevel: 10);
        // Lv30 Ability → choices at Lv35
        SpawnChoiceRow(choiceUnlockLevel: 35, sourceMilestoneLevel: 30);

        // Major passive milestones (scaffold example: choices unlock later at the next Unlock tier):
        // Lv15 Major Passive → choices at Lv20
        SpawnChoiceRow(choiceUnlockLevel: 20, sourceMilestoneLevel: 15);
        // Lv35 Major Passive → choices at Lv40
        SpawnChoiceRow(choiceUnlockLevel: 40, sourceMilestoneLevel: 35);

        // Capstone: choices unlock immediately at Lv50
        SpawnChoiceRow(choiceUnlockLevel: 50, sourceMilestoneLevel: 50);

        // --- Connectors: full spine chain ---
        for (int level = 2; level <= 50; level++)
            SpawnConnector(SpineId(level - 1), SpineId(level));

        // --- Diagonals: milestone → later choice nodes (or same tier at 50) ---
        // Abilities
        SpawnConnector(SpineId(10), ChoiceId(15, 10, 0));
        SpawnConnector(SpineId(10), ChoiceId(15, 10, 1));
        SpawnConnector(SpineId(30), ChoiceId(35, 30, 0));
        SpawnConnector(SpineId(30), ChoiceId(35, 30, 1));

        // Major passives
        SpawnConnector(SpineId(15), ChoiceId(20, 15, 0));
        SpawnConnector(SpineId(15), ChoiceId(20, 15, 1));
        SpawnConnector(SpineId(35), ChoiceId(40, 35, 0));
        SpawnConnector(SpineId(35), ChoiceId(40, 35, 1));

        // Capstone (immediate)
        SpawnConnector(SpineId(50), ChoiceId(50, 50, 0));
        SpawnConnector(SpineId(50), ChoiceId(50, 50, 1));
    }

    private void SpawnChoiceRow(int choiceUnlockLevel, int sourceMilestoneLevel)
    {
        float y = GetSpineRowY(choiceUnlockLevel);
        SpawnNode(
            ChoiceId(choiceUnlockLevel, sourceMilestoneLevel, 0),
            LevelLabel(choiceUnlockLevel),
            "Choice",
            new Vector2(-branchOffsetX, y),
            SkillTreeNodeVisualType.Choice);
        SpawnNode(
            ChoiceId(choiceUnlockLevel, sourceMilestoneLevel, 1),
            LevelLabel(choiceUnlockLevel),
            "Choice",
            new Vector2(branchOffsetX, y),
            SkillTreeNodeVisualType.Choice);
    }

    /// <summary>
    /// Placeholder mapping: which visual type and TypeText each spine level uses for this scaffold pattern.
    /// Real data: each skill tree may define its own level → node entries.
    /// </summary>
    private static void GetSpineNodePresentation(int level, out SkillTreeNodeVisualType visual, out string typeDisplay)
    {
        switch (level)
        {
            case 1:
            case 20:
            case 40:
                visual = SkillTreeNodeVisualType.Unlock;
                typeDisplay = "Unlock";
                return;

            case 5:
            case 15:
            case 25:
            case 35:
            case 45:
                visual = SkillTreeNodeVisualType.MajorPassive;
                typeDisplay = "Major\nPassive";
                return;

            case 10:
            case 30:
                visual = SkillTreeNodeVisualType.Ability;
                typeDisplay = "Ability";
                return;

            case 50:
                visual = SkillTreeNodeVisualType.CapstonePassive;
                typeDisplay = "Capstone";
                return;

            default:
                visual = SkillTreeNodeVisualType.MinorPassive;
                typeDisplay = "Minor\nPassive";
                return;
        }
    }

    private static string SpineId(int level) => $"Lv{level}";
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
            GetSpineNodePresentation(level, out SkillTreeNodeVisualType currentType, out _);
            float currentHalf = SkillTreeNodeUI.GetVisualBoxSize(currentType).y * 0.5f;

            if (level == 1)
            {
                spineRowCenterY[level] = startY;
                continue;
            }

            int prevLevel = level - 1;
            GetSpineNodePresentation(prevLevel, out SkillTreeNodeVisualType prevType, out _);
            float prevHalf = SkillTreeNodeUI.GetVisualBoxSize(prevType).y * 0.5f;

            float gap = Mathf.Max(0f, rowGap);
            if (level == 50)
                gap += Mathf.Max(0f, extraGapBeforeCapstone);

            spineRowCenterY[level] = spineRowCenterY[prevLevel] - prevHalf - gap - currentHalf;
        }
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

        spawnedNodes.Clear();
        spawnedConnectors.Clear();
        nodeLookup.Clear();
        spineRowCenterY.Clear();
        selectedNode = null;
    }

    private void SpawnNode(string nodeId, string levelLabel, string typeLabel, Vector2 pos, SkillTreeNodeVisualType type)
    {
        if (nodePrefab == null || nodesRoot == null) return;

        var node = Instantiate(nodePrefab, nodesRoot);
        node.RectTransform.anchoredPosition = pos;

        node.ApplyVisualType(type);
        if (type is not (SkillTreeNodeVisualType.MinorPassive or SkillTreeNodeVisualType.Choice))
        {
            node.SetLevelText(levelLabel);
            node.SetTypeText(typeLabel);
        }
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