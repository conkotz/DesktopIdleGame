using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class LevelSelectPageUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WorldMapDefinition worldMap;

    [Header("Left — Regions")]
    [SerializeField] private Transform regionListParent;
    [SerializeField] private GameObject regionRowPrefab;
    [Tooltip("Child name under each region row used as selection highlight (optional).")]
    [SerializeField] private string regionSelectedChildName = "Selected";

    [Header("Center — Nodes")]
    [SerializeField] private Transform nodeListParent;
    [SerializeField] private WorldMapNodeButtonUI nodeButtonPrefab;

    [Header("Right — Details")]
    [FormerlySerializedAs("detailNameText")]
    [SerializeField] private TMP_Text selectedNodeName;
    [SerializeField] private Image selectedNodeIcon;
    [FormerlySerializedAs("detailTypeText")]
    [SerializeField] private TMP_Text selectedNodeType;
    [SerializeField] private TMP_Text selectedNodeState;
    [FormerlySerializedAs("detailRecommendedLevelText")]
    [Tooltip("Shows recommended CP from the node definition (maps from recommendedLevel until a dedicated CP field exists).")]
    [SerializeField] private TMP_Text selectedNodeRecommendedCp;
    [FormerlySerializedAs("detailRepeatableText")]
    [SerializeField] private TMP_Text selectedNodeRepeatable;
    [FormerlySerializedAs("detailDescriptionText")]
    [SerializeField] private TMP_Text selectedNodeDescription;
    [SerializeField] private GameObject selectedNodeRequirements;
    [SerializeField] private TMP_Text selectedNodeRequirementsText;
    [SerializeField] private Button enterNodeButton;

    private readonly List<GameObject> _regionRows = new();
    private readonly List<RegionDefinition> _regionRowRegions = new();
    private readonly List<WorldMapNodeButtonUI> _nodeButtons = new();

    private RegionDefinition _selectedRegion;
    private MapNodeDefinition _selectedNode;
    private bool _selectionInitialized;

    /// <summary>Non-serialized: UI may load before Bootstrap; we re-resolve until found.</summary>
    private WorldMapProgressManager _progressEventsTarget;

    private void Awake()
    {
        if (enterNodeButton)
            enterNodeButton.onClick.AddListener(OnEnterNodeClicked);
    }

    private void OnDestroy()
    {
        if (enterNodeButton)
            enterNodeButton.onClick.RemoveListener(OnEnterNodeClicked);
    }

    private void OnEnable()
    {
        TrySubscribeProgressChanged();

        ResolveDefaults();
        if (!_selectionInitialized && worldMap)
        {
            ApplyDefaultSelection();
            _selectionInitialized = true;
        }

        RebuildRegionList();
        RebuildNodeList();
        RefreshDetails();
    }

    private void OnDisable()
    {
        UnsubscribeProgressChanged();
    }

    private void Update()
    {
        if (_progressEventsTarget != null)
            return;

        TrySubscribeProgressChanged();
        if (_progressEventsTarget != null)
        {
            RebuildNodeList();
            RefreshDetails();
        }
    }

    private static WorldMapProgressManager FindProgressManager()
    {
        if (WorldMapProgressManager.Instance != null)
            return WorldMapProgressManager.Instance;

        return FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
    }

    private void TrySubscribeProgressChanged()
    {
        WorldMapProgressManager p = FindProgressManager();
        if (!p || p == _progressEventsTarget)
            return;

        UnsubscribeProgressChanged();
        _progressEventsTarget = p;
        _progressEventsTarget.ProgressChanged += OnProgressChanged;
    }

    private void UnsubscribeProgressChanged()
    {
        if (_progressEventsTarget != null)
        {
            _progressEventsTarget.ProgressChanged -= OnProgressChanged;
            _progressEventsTarget = null;
        }
    }

    private void ResolveDefaults()
    {
        if (!worldMap)
        {
            WorldMapProgressManager p = FindProgressManager();
            if (p)
                worldMap = p.WorldMap;
        }
    }

    private void OnProgressChanged()
    {
        RebuildNodeList();
        RefreshDetails();
    }

    private void ApplyDefaultSelection()
    {
        _selectedRegion = null;
        _selectedNode = null;

        if (!worldMap || worldMap.regions == null || worldMap.regions.Count == 0)
            return;

        if (!string.IsNullOrEmpty(worldMap.startingRegionId))
            _selectedRegion = worldMap.FindRegionById(worldMap.startingRegionId);

        if (!_selectedRegion)
            _selectedRegion = worldMap.regions[0];

        if (!string.IsNullOrEmpty(worldMap.startingNodeId))
        {
            MapNodeDefinition startNode = worldMap.FindNodeById(worldMap.startingNodeId);
            if (startNode && _selectedRegion && _selectedRegion.FindNodeById(startNode.nodeId))
                _selectedNode = startNode;
        }

        if (!_selectedNode && _selectedRegion != null && _selectedRegion.nodes is { Count: > 0 })
            _selectedNode = _selectedRegion.nodes[0];
    }

    private void RebuildRegionList()
    {
        ClearRegionRows();

        if (!regionListParent || !regionRowPrefab || !worldMap || worldMap.regions == null)
            return;

        for (int i = 0; i < worldMap.regions.Count; i++)
        {
            RegionDefinition region = worldMap.regions[i];
            if (!region) continue;

            GameObject row = Instantiate(regionRowPrefab, regionListParent);
            _regionRows.Add(row);
            _regionRowRegions.Add(region);

            Button b = row.GetComponentInChildren<Button>(true);
            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            if (label)
                label.text = region.displayName;

            RegionDefinition captured = region;
            if (b)
                b.onClick.AddListener(() => OnRegionClicked(captured));

            SetRegionRowSelected(row, region == _selectedRegion);
        }
    }

    private void ClearRegionRows()
    {
        for (int i = 0; i < _regionRows.Count; i++)
        {
            if (_regionRows[i])
                Destroy(_regionRows[i]);
        }

        _regionRows.Clear();
        _regionRowRegions.Clear();
    }

    private void SetRegionRowSelected(GameObject row, bool selected)
    {
        if (!row || string.IsNullOrEmpty(regionSelectedChildName)) return;

        Transform t = row.transform.Find(regionSelectedChildName);
        if (t)
            t.gameObject.SetActive(selected);
    }

    private void OnRegionClicked(RegionDefinition region)
    {
        _selectedRegion = region;
        _selectedNode = null;

        if (_selectedRegion != null && _selectedRegion.nodes is { Count: > 0 } && _selectedRegion.nodes[0])
            _selectedNode = _selectedRegion.nodes[0];

        for (int i = 0; i < _regionRows.Count && i < _regionRowRegions.Count; i++)
            SetRegionRowSelected(_regionRows[i], _regionRowRegions[i] == _selectedRegion);

        RebuildNodeList();
        RefreshDetails();
    }

    private void RebuildNodeList()
    {
        ClearNodeButtons();

        if (!nodeListParent || !nodeButtonPrefab || !_selectedRegion || _selectedRegion.nodes == null)
            return;

        WorldMapProgressManager progress = FindProgressManager();

        for (int i = 0; i < _selectedRegion.nodes.Count; i++)
        {
            MapNodeDefinition node = _selectedRegion.nodes[i];
            if (!node) continue;

            WorldMapNodeButtonUI row = Instantiate(nodeButtonPrefab, nodeListParent);
            _nodeButtons.Add(row);

            string state = progress
                ? progress.GetStateLabel(node.nodeId)
                : "Unlocked";

            bool sel = _selectedNode && _selectedNode == node;
            row.Bind(node, state, sel, OnNodeSelected);
        }
    }

    private void ClearNodeButtons()
    {
        for (int i = 0; i < _nodeButtons.Count; i++)
        {
            if (_nodeButtons[i])
                Destroy(_nodeButtons[i].gameObject);
        }

        _nodeButtons.Clear();
    }

    private void OnNodeSelected(MapNodeDefinition node)
    {
        _selectedNode = node;
        RebuildNodeList();
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        MapNodeDefinition n = _selectedNode;
        WorldMapProgressManager progress = FindProgressManager();

        if (selectedNodeName)
            selectedNodeName.text = n ? n.displayName : "—";

        if (selectedNodeIcon)
        {
            bool hasIcon = n && n.icon;
            selectedNodeIcon.enabled = hasIcon;
            if (hasIcon)
                selectedNodeIcon.sprite = n.icon;
            else
                selectedNodeIcon.sprite = null;
        }

        if (selectedNodeType)
            selectedNodeType.text = n ? n.nodeType.ToString() : "";

        if (selectedNodeState)
        {
            if (n && progress)
                selectedNodeState.text = progress.GetStateLabel(n.nodeId);
            else if (n)
                selectedNodeState.text = "Unlocked";
            else
                selectedNodeState.text = "";
        }

        if (selectedNodeRecommendedCp)
            selectedNodeRecommendedCp.text = n ? $"Recommended CP: {n.recommendedLevel}" : "";

        if (selectedNodeRepeatable)
            selectedNodeRepeatable.text = n ? (n.isRepeatable ? "Repeatable: Yes" : "Repeatable: No") : "";

        if (selectedNodeDescription)
            selectedNodeDescription.text = n ? n.description : "";

        RefreshRequirementsBlock(n);

        bool canEnter = n && progress && progress.IsNodeUnlocked(n.nodeId);
        if (enterNodeButton)
            enterNodeButton.interactable = canEnter;
    }

    private void RefreshRequirementsBlock(MapNodeDefinition n)
    {
        if (!selectedNodeRequirementsText && !selectedNodeRequirements)
            return;

        if (!n)
        {
            if (selectedNodeRequirementsText)
                selectedNodeRequirementsText.text = "";
            if (selectedNodeRequirements)
                selectedNodeRequirements.SetActive(false);
            return;
        }

        var sb = new StringBuilder();
        if (n.requiredPlayerLevelPlaceholder > 0)
            sb.AppendLine($"Requires player level: {n.requiredPlayerLevelPlaceholder}");

        if (!string.IsNullOrWhiteSpace(n.unlockRequirementNotes))
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(n.unlockRequirementNotes.Trim());
        }

        string combined = sb.ToString().Trim();
        bool hasAny = combined.Length > 0;

        if (selectedNodeRequirementsText)
            selectedNodeRequirementsText.text = hasAny ? combined : "";

        if (selectedNodeRequirements)
            selectedNodeRequirements.SetActive(hasAny);
    }

    private void OnEnterNodeClicked()
    {
        if (!_selectedNode)
        {
            Debug.LogWarning("[LevelSelectPageUI] Enter: no node selected.");
            return;
        }

        WorldMapProgressManager progress = FindProgressManager();
        if (progress && !progress.IsNodeUnlocked(_selectedNode.nodeId))
        {
            Debug.LogWarning($"[LevelSelectPageUI] Enter blocked (locked): {_selectedNode.nodeId}");
            return;
        }

        Debug.Log($"[LevelSelectPageUI] Enter node: {_selectedNode.nodeId}");
    }
}
