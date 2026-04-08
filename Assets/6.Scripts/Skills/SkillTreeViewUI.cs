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
    [SerializeField] private SharedTooltipUI sharedTooltip;
    [SerializeField] private RectTransform tooltipBoundsRect;

    [Header("Data")]
    [SerializeField] private SkillDefinition selectedSkill;
    [SerializeField] private SkillsManager skillsManager;
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

    private readonly List<SkillTreeNodeUI> spawnedNodes = new();
    private readonly List<SkillTreeConnectorUI> spawnedConnectors = new();
    private readonly List<TMP_Text> spawnedLevelLabels = new();
    private readonly Dictionary<string, SkillTreeNodeUI> nodeLookup = new();
    private readonly Dictionary<int, float> rowYByLevel = new();
    private readonly Dictionary<string, string> tooltipTitleByNodeId = new();
    private readonly Dictionary<string, string> tooltipBodyByNodeId = new();
    private readonly Dictionary<string, int> nodeLevelById = new();
    private readonly Dictionary<int, RowDef> rowByLevel = new();
    private readonly Dictionary<string, ChoiceNodeMeta> choiceMetaByNodeId = new();

    private SkillTreeNodeUI selectedNode;

    private readonly struct ChoiceNodeMeta
    {
        public readonly int sourceLevel;
        public readonly int choiceIndex;
        public readonly int unlockLevel;

        public ChoiceNodeMeta(int sourceLevel, int choiceIndex, int unlockLevel)
        {
            this.sourceLevel = sourceLevel;
            this.choiceIndex = choiceIndex;
            this.unlockLevel = unlockLevel;
        }
    }

    private readonly struct RowDef
    {
        public readonly int level;
        public readonly SkillUnlockDefinition unlock;
        public readonly SkillTreeNodeVisualType type;
        public readonly int choiceCount;

        public RowDef(int level, SkillUnlockDefinition unlock, SkillTreeNodeVisualType type, int choiceCount)
        {
            this.level = level;
            this.unlock = unlock;
            this.type = type;
            this.choiceCount = Mathf.Max(0, choiceCount);
        }
    }

    private void Start()
    {
        BuildForSelectedSkill();
    }

    public void SetSkill(SkillDefinition skill)
    {
        selectedSkill = skill;
        BuildForSelectedSkill();
    }

    public void BuildForSelectedSkill()
    {
        ClearTree();
        if (selectedSkill == null || selectedSkill.unlocks == null || selectedSkill.unlocks.Count == 0)
            return;
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;

        int currentSkillLevel = skillsManager ? skillsManager.GetLevel(selectedSkill.skillType) : 1;

        List<RowDef> rows = BuildRows(selectedSkill.unlocks);
        if (rows.Count == 0)
            return;

        BuildRowY(rows);
        SpawnLevelColumnLabels(rows);
        SpawnRows(rows, currentSkillLevel);
        SpawnConnectors(rows);
    }

    private List<RowDef> BuildRows(List<SkillUnlockDefinition> unlocks)
    {
        var sorted = new List<SkillUnlockDefinition>();
        for (int i = 0; i < unlocks.Count; i++)
            if (unlocks[i] != null)
                sorted.Add(unlocks[i]);

        sorted.Sort((a, b) => a.requiredLevel.CompareTo(b.requiredLevel));

        var rows = new List<RowDef>(sorted.Count);
        var seen = new HashSet<int>();

        for (int i = 0; i < sorted.Count; i++)
        {
            SkillUnlockDefinition u = sorted[i];
            if (u == null || u.requiredLevel <= 0)
            {
                if (stopAfterFirstMissingUnlock) break;
                continue;
            }

            if (seen.Contains(u.requiredLevel))
            {
                // Keep only first unlock per level for now (simple spine).
                continue;
            }

            SkillTreeNodeVisualType visual = MapUnlockToNodeType(u);
            int choices = GetChoiceCount(u);
            rows.Add(new RowDef(u.requiredLevel, u, visual, choices));
            seen.Add(u.requiredLevel);
        }

        return rows;
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

    private void BuildRowY(List<RowDef> rows)
    {
        rowYByLevel.Clear();
        rowByLevel.Clear();
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            rowByLevel[row.level] = row;
            float currentHalf = SkillTreeNodeUI.GetVisualBoxSize(row.type).y * 0.5f;

            if (i == 0)
            {
                rowYByLevel[row.level] = startY;
                continue;
            }

            RowDef prev = rows[i - 1];
            float prevHalf = SkillTreeNodeUI.GetVisualBoxSize(prev.type).y * 0.5f;
            rowYByLevel[row.level] = rowYByLevel[prev.level] - prevHalf - Mathf.Max(0f, rowGap) - currentHalf;
        }
    }

    private void SpawnLevelColumnLabels(List<RowDef> rows)
    {
        if (levelsRoot == null || levelRowLabelPrefab == null)
            return;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!showAllLevelLabels && row.type == SkillTreeNodeVisualType.MinorPassive)
                continue;

            var t = Instantiate(levelRowLabelPrefab, levelsRoot);
            t.text = $"Lv{row.level}";
            t.alignment = TextAlignmentOptions.MidlineRight;

            RectTransform rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, rowYByLevel[row.level]);
            spawnedLevelLabels.Add(t);
        }
    }

    private void SpawnRows(List<RowDef> rows, int currentSkillLevel)
    {
        // Spawn main spine nodes first.
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            float y = rowYByLevel[row.level];

            string spineId = SpineId(row.level);
            bool unlocked = row.level <= currentSkillLevel;
            BuildTooltipCopy(row.level, row.type, row.unlock, unlocked, out string mainTitle, out string mainBody);
            SpawnNode(spineId, new Vector2(0f, y), row.type, mainTitle, mainBody, unlocked);
        }

        // Spawn choices using per-choice unlock levels.
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            List<SkillChoiceDefinition> choices = GetNonNullChoices(row.unlock);
            if (choices.Count <= 0)
                continue;

            float center = (choices.Count - 1) * 0.5f;
            for (int choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
            {
                SkillChoiceDefinition choice = choices[choiceIndex];
                int choiceUnlockLevel = ResolveChoiceUnlockLevel(row.level, row.type);
                if (!rowYByLevel.TryGetValue(choiceUnlockLevel, out float targetY))
                    continue; // no authored row at that level yet

                float xStep = row.type == SkillTreeNodeVisualType.CapstonePassive ? capstoneChoiceOffsetX : choiceOffsetX;
                float yOffset;
                if (choiceUnlockLevel != row.level)
                {
                    // Choice rows that unlock later than their source can use a dedicated offset.
                    yOffset = explicitChoiceRowYOffset;
                }
                else
                {
                    yOffset = row.type == SkillTreeNodeVisualType.CapstonePassive ? capstoneChoiceYOffset : choiceYOffset;
                }
                float x = (choiceIndex - center) * xStep;
                bool unlocked = row.level <= currentSkillLevel && choiceUnlockLevel <= currentSkillLevel;
                BuildChoiceTooltipCopy(choiceUnlockLevel, choice, row.unlock, unlocked, out string cTitle, out string cBody);
                SpawnNode(
                    ChoiceId(row.level, choiceUnlockLevel, choiceIndex),
                    new Vector2(x, targetY + yOffset),
                    SkillTreeNodeVisualType.Choice,
                    cTitle,
                    cBody,
                    unlocked
                );
                choiceMetaByNodeId[ChoiceId(row.level, choiceUnlockLevel, choiceIndex)] =
                    new ChoiceNodeMeta(row.level, choiceIndex, choiceUnlockLevel);
            }
        }

        RefreshChoiceSelectionVisuals();
    }

    private void SpawnConnectors(List<RowDef> rows)
    {
        // Spine
        for (int i = 1; i < rows.Count; i++)
            SpawnConnector(SpineId(rows[i - 1].level), SpineId(rows[i].level));

        // Choices
        for (int i = 0; i < rows.Count; i++)
        {
            RowDef row = rows[i];
            List<SkillChoiceDefinition> choices = GetNonNullChoices(row.unlock);
            if (choices.Count <= 0) continue;

            string source = SpineId(row.level);
            for (int choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
            {
                SkillChoiceDefinition choice = choices[choiceIndex];
                int choiceUnlockLevel = ResolveChoiceUnlockLevel(row.level, row.type);
                SpawnConnector(source, ChoiceId(row.level, choiceUnlockLevel, choiceIndex));
            }
        }
    }

    private static int ResolveChoiceUnlockLevel(int sourceLevel, SkillTreeNodeVisualType sourceType)
    {
        if (sourceType == SkillTreeNodeVisualType.CapstonePassive)
            return 50;
        return sourceLevel + 3;
    }

    private static string SpineId(int level) => $"Lv{level}";
    private static string ChoiceId(int sourceLevel, int unlockLevel, int index) =>
        $"Lv{sourceLevel}_ChoiceLv{unlockLevel}_{index}";

    private static void BuildTooltipCopy(int level, SkillTreeNodeVisualType type, SkillUnlockDefinition unlock, bool isUnlocked, out string title, out string body)
    {
        string typeLabel = TypeLabel(type);
        string unlockTitle = unlock != null && !string.IsNullOrWhiteSpace(unlock.title) ? unlock.title.Trim() : "Untitled";
        string desc = unlock != null && !string.IsNullOrWhiteSpace(unlock.description) ? unlock.description.Trim() : "No description yet.";
        title = $"{typeLabel} - {unlockTitle}";
        body = $"{BuildStatusLine(isUnlocked)}\nUnlocks at Lv{level}\n\n{desc}";
    }

    private static void BuildChoiceTooltipCopy(int unlockLevel, SkillChoiceDefinition choice, SkillUnlockDefinition parentUnlock, bool isUnlocked, out string title, out string body)
    {
        string unlockTitle = choice != null && !string.IsNullOrWhiteSpace(choice.title)
            ? choice.title.Trim()
            : (parentUnlock != null && !string.IsNullOrWhiteSpace(parentUnlock.title) ? parentUnlock.title.Trim() : "Untitled");
        string desc = choice != null && !string.IsNullOrWhiteSpace(choice.description)
            ? choice.description.Trim()
            : "No description yet.";
        title = $"Choice - {unlockTitle}";
        body = $"{BuildStatusLine(isUnlocked)}\nUnlocks at Lv{unlockLevel}\n\n{desc}";
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
            SkillTreeNodeVisualType.Choice => "Choice",
            SkillTreeNodeVisualType.CapstonePassive => "Capstone",
            _ => "Node"
        };
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
        rowYByLevel.Clear();
        rowByLevel.Clear();
        tooltipTitleByNodeId.Clear();
        tooltipBodyByNodeId.Clear();
        nodeLevelById.Clear();
        choiceMetaByNodeId.Clear();
        selectedNode = null;
        sharedTooltip?.Hide();
    }

    private void SpawnNode(string nodeId, Vector2 pos, SkillTreeNodeVisualType type, string tooltipTitle, string tooltipBody, bool unlocked)
    {
        if (nodePrefab == null || nodesRoot == null) return;

        var node = Instantiate(nodePrefab, nodesRoot);
        node.RectTransform.anchoredPosition = pos;
        node.ApplyVisualType(type);
        node.SetLocked(!unlocked);
        node.SetSelected(false);
        node.SetClick(() => OnNodeClicked(nodeId, node));
        node.SetHover(
            () => ShowTooltip(nodeId, node.transform),
            HideTooltip
        );

        spawnedNodes.Add(node);
        nodeLookup[nodeId] = node;
        if (TryGetNodeLevel(nodeId, out int nodeLevel))
            nodeLevelById[nodeId] = nodeLevel;
        tooltipTitleByNodeId[nodeId] = string.IsNullOrWhiteSpace(tooltipTitle) ? "Node" : tooltipTitle;
        tooltipBodyByNodeId[nodeId] = tooltipBody ?? string.Empty;
    }

    private void SpawnConnector(string from, string to)
    {
        if (!nodeLookup.TryGetValue(from, out var a)) return;
        if (!nodeLookup.TryGetValue(to, out var b)) return;

        var conn = Instantiate(connectorPrefab, connectorsRoot);
        conn.SetPositions(a, b);
        spawnedConnectors.Add(conn);
    }

    private void OnNodeClicked(string nodeId, SkillTreeNodeUI node)
    {
        if (node == null || node.IsLocked()) return;

        if (choiceMetaByNodeId.TryGetValue(nodeId, out ChoiceNodeMeta choiceMeta))
        {
            if (!selectedSkill || !skillsManager)
                return;

            skillsManager.SetSkillChoiceSelection(selectedSkill.skillType, choiceMeta.sourceLevel, choiceMeta.choiceIndex);
            RefreshChoiceSelectionVisuals();
            ShowTooltip(nodeId, node.transform);
            return;
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
        if (!string.IsNullOrWhiteSpace(nodeId) && nodeLevelById.TryGetValue(nodeId, out int level) && rowByLevel.TryGetValue(level, out RowDef row))
            body = AppendChoiceTooltipState(row, body);
        sharedTooltip.ShowTextAt(
            anchor,
            string.IsNullOrWhiteSpace(title) ? "Node" : title,
            string.IsNullOrWhiteSpace(body) ? "No node data yet." : body,
            measureRect: tooltipBoundsRect != null ? tooltipBoundsRect : nodesRoot,
            heightRect: tooltipBoundsRect != null ? tooltipBoundsRect : nodesRoot,
            preferredSide: FlipInsideBounds.PreferredSide.Right
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
            sb.Append("\n\nActive Choice: ");
            sb.Append("<color=#33CC66>");
            sb.Append(!string.IsNullOrWhiteSpace(choices[selectedChoice].title) ? choices[selectedChoice].title.Trim() : $"Option {selectedChoice + 1}");
            sb.Append("</color>");
        }
        sb.Append("\n\nChoices:");
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
}