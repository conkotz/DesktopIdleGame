using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelectPageUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WorldMapDefinition worldMap;

    [Header("Loading")]
    [Tooltip("Single gameplay scene; level content comes from MapNodeDefinition via ActiveLevelContext.")]
    [SerializeField] private string gameplaySceneName = "GamePlay";

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
    [Tooltip("Shows Recommended CP from MapNodeDefinition.recommendedCombatPower.")]
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

    /// <summary>
    /// While the level-select page is active, HUD can read this to show the highlighted node
    /// (before the player presses Enter).
    /// </summary>
    public static MapNodeDefinition HudPreviewSelection { get; private set; }

    /// <summary>Non-serialized: UI may load before Bootstrap; we re-resolve until found.</summary>
    private WorldMapProgressManager _progressEventsTarget;

    private SkillsManager _skillsLevelEventsTarget;

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
        TrySubscribeSkillsLevelEvents();

        ResolveDefaults();
        if (worldMap)
        {
            // Prefer the map we're actually playing (bootstrap / ActiveLevelContext) so the grey
            // selection matches the current area — not only startingNodeId / first row.
            bool syncedToActive = TrySelectActiveMapNode();
            if (!syncedToActive && !_selectionInitialized)
            {
                ApplyDefaultSelection();
                _selectionInitialized = true;
            }
            else if (syncedToActive)
                _selectionInitialized = true;
        }

        RebuildRegionList();
        RebuildNodeList();
        RefreshDetails();
    }

    private void OnDisable()
    {
        UnsubscribeProgressChanged();
        UnsubscribeSkillsLevelEvents();
        HudPreviewSelection = null;
    }

    private void Update()
    {
        bool subscribed = false;
        if (_progressEventsTarget == null)
        {
            TrySubscribeProgressChanged();
            if (_progressEventsTarget != null)
                subscribed = true;
        }

        if (_skillsLevelEventsTarget == null)
        {
            TrySubscribeSkillsLevelEvents();
            if (_skillsLevelEventsTarget != null)
                subscribed = true;
        }

        if (subscribed)
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

    private void TrySubscribeSkillsLevelEvents()
    {
        SkillsManager s = FindSkillsManager();
        if (!s || s == _skillsLevelEventsTarget)
            return;

        UnsubscribeSkillsLevelEvents();
        _skillsLevelEventsTarget = s;
        _skillsLevelEventsTarget.OnLevelUp += OnPlayerSkillLevelChanged;
    }

    private void UnsubscribeSkillsLevelEvents()
    {
        if (_skillsLevelEventsTarget != null)
        {
            _skillsLevelEventsTarget.OnLevelUp -= OnPlayerSkillLevelChanged;
            _skillsLevelEventsTarget = null;
        }
    }

    private void OnPlayerSkillLevelChanged(SkillType _, int __)
    {
        if (!isActiveAndEnabled)
            return;

        RefreshDetails();
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

    /// <summary>
    /// Select region + node for the level currently loaded in GamePlay (if any).
    /// </summary>
    private bool TrySelectActiveMapNode()
    {
        if (!worldMap || worldMap.regions == null || worldMap.regions.Count == 0)
            return false;

        MapNodeDefinition active = null;
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            active = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        else if (ActiveLevelContext.Current != null)
            active = ActiveLevelContext.Current;

        if (active == null || string.IsNullOrEmpty(active.nodeId))
            return false;

        RegionDefinition region = worldMap.FindRegionContainingNode(active.nodeId);
        if (!region)
            return false;

        MapNodeDefinition nodeInList = region.FindNodeById(active.nodeId);
        if (!nodeInList)
            return false;

        _selectedRegion = region;
        _selectedNode = nodeInList;
        return true;
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
        SkillsManager skills = FindSkillsManager();

        for (int i = 0; i < _selectedRegion.nodes.Count; i++)
        {
            MapNodeDefinition node = _selectedRegion.nodes[i];
            if (!node) continue;

            WorldMapNodeButtonUI row = Instantiate(nodeButtonPrefab, nodeListParent);
            _nodeButtons.Add(row);

            string state = progress
                ? node.GetUiStateLabel(progress, skills)
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

    private void PublishHudPreview()
    {
        if (!isActiveAndEnabled)
        {
            HudPreviewSelection = null;
            return;
        }

        HudPreviewSelection = _selectedNode;
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

        SkillsManager skills = FindSkillsManager();

        if (selectedNodeState)
        {
            if (n && progress)
                selectedNodeState.text = n.GetUiStateLabel(progress, skills);
            else if (n)
                selectedNodeState.text = "Unlocked";
            else
                selectedNodeState.text = "";
        }

        if (selectedNodeRecommendedCp)
            selectedNodeRecommendedCp.text = n ? $"Recommended CP: {n.recommendedCombatPower}" : "";

        if (selectedNodeRepeatable)
            selectedNodeRepeatable.text = n ? (n.isRepeatable ? "Repeatable: Yes" : "Repeatable: No") : "";

        if (selectedNodeDescription)
            selectedNodeDescription.text = n ? n.description : "";

        RefreshRequirementsBlock(n);

        bool canEnter = n && n.CanEnter(progress, skills);
        if (enterNodeButton)
            enterNodeButton.interactable = canEnter;

        PublishHudPreview();
    }

    private static SkillsManager FindSkillsManager()
    {
        if (SkillsManager.Instance != null)
            return SkillsManager.Instance;

        return FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
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
        sb.Append(n.BuildRequirementsDisplayText());

        WorldMapProgressManager progress = FindProgressManager();
        if (n.requiresMapUnlock && progress != null && !progress.IsNodeUnlocked(n.nodeId))
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(
                "Map: Locked — not unlocked in WorldMapProgressManager yet. Add this node id to unlocks, or turn off Requires Map Unlock on the asset if skills alone should open it.");
        }

        SkillsManager skills = FindSkillsManager();
        if (skills != null && n.HasSkillGates())
        {
            if (sb.Length > 0)
                sb.AppendLine();
            bool met = n.MeetsSkillRequirements(skills);
            sb.Append(met ? "Status: Skill requirements met." : "Status: Skill requirements not met.");
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
        SkillsManager skills = FindSkillsManager();
        if (!_selectedNode.CanEnter(progress, skills))
        {
            Debug.LogWarning($"[LevelSelectPageUI] Enter blocked (map and/or skills): {_selectedNode.nodeId}");
            return;
        }

        ActiveLevelContext.SetPendingLevel(_selectedNode);

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogError("[LevelSelectPageUI] Gameplay scene name is not set.");
            return;
        }

        Debug.Log($"[LevelSelectPageUI] Loading '{gameplaySceneName}' for node: {_selectedNode.nodeId}");
        SceneManager.LoadScene(gameplaySceneName);
    }
}
