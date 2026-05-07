using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelectPageUI : MonoBehaviour
{
    [Header("Theme — Region Buttons")]
    [SerializeField] private Color regionUnlockedColor = new Color32(104, 111, 122, 255);
    [SerializeField] private Color regionLockedColor = new Color32(56, 58, 62, 255);
    [SerializeField] private Color regionHoverColor = new Color32(126, 133, 146, 255);
    [SerializeField] private Color regionPressedColor = new Color32(92, 99, 113, 255);
    [SerializeField] private Color regionNameUnlockedColor = new Color32(247, 225, 190, 255);
    [SerializeField] private Color regionNameLockedColor = new Color32(160, 155, 148, 200);

    [Header("Data")]
    [SerializeField] private WorldMapDefinition worldMap;

    [Header("Loading")]
    [Tooltip("Single gameplay scene; level content comes from MapNodeDefinition via ActiveLevelContext.")]
    [SerializeField] private string gameplaySceneName = "GamePlay";

    [Header("Left — Regions")]
    [SerializeField] private Transform regionListParent;
    [SerializeField] private GameObject regionRowPrefab;
    [Tooltip("Fallback only: child name under each region row used as selection highlight when WorldMapRegionRowUI is not present.")]
    [SerializeField] private string regionSelectedChildName = "Selected";

    [Header("Center — Nodes")]
    [SerializeField] private Transform nodeListParent;
    [SerializeField] private WorldMapNodeButtonUI nodeButtonPrefab;
    [Header("Center — Node Filters (optional)")]
    [SerializeField] private Button filterAllButton;
    [SerializeField] private Button filterCombatButton;
    [SerializeField] private Button filterGatheringButton;
    [SerializeField] private Button filterOtherButton;

    [Header("Right — Details")]
    [FormerlySerializedAs("detailNameText")]
    [SerializeField] private TMP_Text selectedNodeName;
    [SerializeField] private Image selectedNodeIcon;
    [FormerlySerializedAs("detailTypeText")]
    [SerializeField] private TMP_Text selectedNodeType;
    [SerializeField] private TMP_Text selectedNodeState;
    [FormerlySerializedAs("detailRecommendedLevelText")]
    [Tooltip("Recommended CP: Endurance Trial nodes use endurance waves + wave stress; other nodes use spawn group plans (single-encounter stress). Else fallbackRecommendedCombatPower.")]
    [SerializeField] private TMP_Text selectedNodeRecommendedCp;
    [FormerlySerializedAs("detailRepeatableText")]
    [SerializeField] private TMP_Text selectedNodeRepeatable;
    [FormerlySerializedAs("detailDescriptionText")]
    [SerializeField] private TMP_Text selectedNodeDescription;
    [Header("Right — Details Scroll (optional auto-resolve)")]
    [Tooltip("Details ScrollRect that hosts node detail lines. If empty, resolved from details text parents.")]
    [SerializeField] private ScrollRect detailsScrollRect;
    [Tooltip("Content root under the details ScrollRect. If empty, resolved from details text parents.")]
    [SerializeField] private RectTransform detailsContentRoot;
    [SerializeField] private GameObject selectedNodeRequirements;
    [SerializeField] private TMP_Text selectedNodeRequirementsText;
    [Tooltip("Optional row root (e.g. NodeContains). Hidden when the map node has no detected interactables.")]
    [SerializeField] private GameObject selectedNodeContainsRoot;
    [Tooltip("Shows merchants (Merchant Name), Storage, Notice Board, and quest NPCs inferred from MapNodeDefinition prefabs / spawn plans.")]
    [SerializeField] private TMP_Text selectedNodeContainsText;
    [Tooltip("Optional NPC/interactables line text (e.g. NodeContainsNPCText). Hidden when empty.")]
    [SerializeField] private TMP_Text selectedNodeContainsNpcText;
    [Tooltip("Optional row root for NPC/interactables line. Hidden when line is empty.")]
    [SerializeField] private GameObject selectedNodeContainsNpcRoot;
    [Tooltip("Optional resources/enemies line text (e.g. NodeContainsResourcesEnemiesText). Hidden when empty.")]
    [SerializeField] private TMP_Text selectedNodeContainsResourcesEnemiesText;
    [Tooltip("Optional row root for resources/enemies line. Hidden when line is empty.")]
    [SerializeField] private GameObject selectedNodeContainsResourcesEnemiesRoot;
    [SerializeField] private Button enterNodeButton;

    [Header("Right — Panel backgrounds (node type theme)")]
    [Tooltip("Assign the Image that paints the Details panel (e.g. DetailsScrollView or its background child). Colors come from the same palette as WorldMapNodeButtonUI on Node Button Prefab.")]
    [SerializeField] private Image detailsPanelBackgroundImage;
    [Tooltip("Optional. Tint the center Locations list panel to match the selected node type.")]
    [SerializeField] private Image locationsPanelBackgroundImage;


    private readonly List<GameObject> _regionRows = new();
    private readonly List<RegionDefinition> _regionRowRegions = new();
    private readonly List<WorldMapNodeButtonUI> _nodeButtons = new();
    private readonly List<GameObject> _nodeSectionRows = new();

    private enum NodeListFilter
    {
        All,
        Combat,
        Gathering,
        Other
    }

    private NodeListFilter _nodeListFilter = NodeListFilter.All;

    private RegionDefinition _selectedRegion;
    private MapNodeDefinition _selectedNode;
    private bool _selectionInitialized;
    private string _lastDetailsNodeId;

    /// <summary>
    /// Consumed on next <see cref="OnEnable"/> (e.g. quest journal "Go to location"). Cleared after one attempt.
    /// </summary>
    private static string s_pendingFocusNodeId;

    /// <summary>
    /// Focus this map node when the level-select page next enables (after <see cref="MainMenuWindowUI"/> opens that tab).
    /// </summary>
    public static void SetPendingMapNodeFocus(string nodeId)
    {
        s_pendingFocusNodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId.Trim();
    }

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

        if (filterAllButton)
            filterAllButton.onClick.AddListener(() => OnNodeFilterClicked(NodeListFilter.All));
        if (filterCombatButton)
            filterCombatButton.onClick.AddListener(() => OnNodeFilterClicked(NodeListFilter.Combat));
        if (filterGatheringButton)
            filterGatheringButton.onClick.AddListener(() => OnNodeFilterClicked(NodeListFilter.Gathering));
        if (filterOtherButton)
            filterOtherButton.onClick.AddListener(() => OnNodeFilterClicked(NodeListFilter.Other));
    }

    private void OnDestroy()
    {
        if (enterNodeButton)
            enterNodeButton.onClick.RemoveListener(OnEnterNodeClicked);

        if (filterAllButton)
            filterAllButton.onClick.RemoveAllListeners();
        if (filterCombatButton)
            filterCombatButton.onClick.RemoveAllListeners();
        if (filterGatheringButton)
            filterGatheringButton.onClick.RemoveAllListeners();
        if (filterOtherButton)
            filterOtherButton.onClick.RemoveAllListeners();
    }

    private void OnEnable()
    {
        TrySubscribeProgressChanged();
        TrySubscribeSkillsLevelEvents();

        ResolveDefaults();
        if (worldMap)
        {
            bool usedJournalFocus = TryConsumePendingMapNodeFocus();
            if (usedJournalFocus)
            {
                _selectionInitialized = true;
            }
            else
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
        _skillsLevelEventsTarget.OnSkillLevelDecreased += OnPlayerSkillLevelChanged;
    }

    private void UnsubscribeSkillsLevelEvents()
    {
        if (_skillsLevelEventsTarget != null)
        {
            _skillsLevelEventsTarget.OnLevelUp -= OnPlayerSkillLevelChanged;
            _skillsLevelEventsTarget.OnSkillLevelDecreased -= OnPlayerSkillLevelChanged;
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
        RebuildRegionList();
        RebuildNodeList();
        RefreshDetails();
    }

    /// <summary>
    /// Select region + node for the level currently loaded in GamePlay (if any).
    /// </summary>
    private bool TryConsumePendingMapNodeFocus()
    {
        if (string.IsNullOrEmpty(s_pendingFocusNodeId) || !worldMap)
            return false;

        string id = s_pendingFocusNodeId;
        s_pendingFocusNodeId = null;

        RegionDefinition region = worldMap.FindRegionContainingNode(id);
        if (!region)
            return false;

        MapNodeDefinition nodeInList = region.FindNodeById(id);
        if (!nodeInList)
            return false;

        _selectedRegion = region;
        _selectedNode = nodeInList;
        return true;
    }

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

        WorldMapProgressManager progress = FindProgressManager();

        if (!string.IsNullOrEmpty(worldMap.startingRegionId))
        {
            RegionDefinition preferred = worldMap.FindRegionById(worldMap.startingRegionId);
            if (preferred && ShouldShowRegionInPicker(preferred, progress) && IsRegionAvailable(preferred, progress))
                _selectedRegion = preferred;
        }

        if (!_selectedRegion)
        {
            for (int i = 0; i < worldMap.regions.Count; i++)
            {
                RegionDefinition r = worldMap.regions[i];
                if (r && ShouldShowRegionInPicker(r, progress) && IsRegionAvailable(r, progress))
                {
                    _selectedRegion = r;
                    break;
                }
            }
        }

        if (!_selectedRegion)
        {
            for (int i = 0; i < worldMap.regions.Count; i++)
            {
                RegionDefinition r = worldMap.regions[i];
                if (r && ShouldShowRegionInPicker(r, progress))
                {
                    _selectedRegion = r;
                    break;
                }
            }
        }

        if (!_selectedRegion && worldMap.regions.Count > 0)
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

        WorldMapProgressManager progress = FindProgressManager();

        for (int i = 0; i < worldMap.regions.Count; i++)
        {
            RegionDefinition region = worldMap.regions[i];
            if (!region) continue;
            if (!region.ShouldListInRegionPicker(progress, ResolveActiveMapNodeIdForRegionUi(), worldMap))
                continue;

            GameObject row = Instantiate(regionRowPrefab, regionListParent);
            _regionRows.Add(row);
            _regionRowRegions.Add(region);

            WorldMapRegionRowUI rowUI = row.GetComponent<WorldMapRegionRowUI>();
            Button b = rowUI != null ? rowUI.Button : row.GetComponentInChildren<Button>(true);
            TMP_Text label = rowUI != null ? rowUI.NameText : row.GetComponentInChildren<TMP_Text>(true);
            bool unlocked = IsRegionAvailable(region, progress);
            if (label)
                label.text = unlocked ? region.displayName : $"{region.displayName} (Locked)";

            RegionDefinition captured = region;
            if (b)
            {
                // Keep interactable so locked regions can show feedback (e.g. activity log) on click.
                b.interactable = true;
                b.onClick.AddListener(() => OnRegionClicked(captured));
            }

            SetRegionRowSelected(row, unlocked, unlocked && region == _selectedRegion);
        }

        MigrateRegionSelectionIfNeeded(progress);
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

    private void SetRegionRowSelected(GameObject row, bool unlocked, bool selected)
    {
        if (!row) return;

        Button b = row.GetComponentInChildren<Button>(true);
        ApplyRegionButtonTheme(b, unlocked);

        WorldMapRegionRowUI rowUI = row.GetComponent<WorldMapRegionRowUI>();
        ApplyRegionNameStyle(row, rowUI, unlocked);

        if (rowUI != null)
        {
            rowUI.SetSelected(selected);
            return;
        }

        if (!string.IsNullOrEmpty(regionSelectedChildName))
        {
            Transform t = row.transform.Find(regionSelectedChildName);
            if (t)
                t.gameObject.SetActive(selected);
        }
    }

    private void ApplyRegionNameStyle(GameObject row, WorldMapRegionRowUI rowUI, bool unlocked)
    {
        TMP_Text label = rowUI != null ? rowUI.NameText : row.GetComponentInChildren<TMP_Text>(true);
        if (label)
            label.color = unlocked ? regionNameUnlockedColor : regionNameLockedColor;
    }

    private void ApplyRegionButtonTheme(Button button, bool unlocked)
    {
        if (!button)
            return;

        Color baseCol = unlocked ? regionUnlockedColor : regionLockedColor;
        Color hoverCol = regionHoverColor;
        Color pressedCol = regionPressedColor;

        ColorBlock cb = button.colors;
        cb.normalColor = baseCol;
        cb.highlightedColor = hoverCol;
        cb.selectedColor = hoverCol;
        cb.pressedColor = pressedCol;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        button.colors = cb;

        if (button.targetGraphic)
            button.targetGraphic.color = baseCol;
    }

    private static Color Lift(Color c, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color(
            Mathf.Clamp01(c.r + (1f - c.r) * amount),
            Mathf.Clamp01(c.g + (1f - c.g) * amount),
            Mathf.Clamp01(c.b + (1f - c.b) * amount),
            c.a);
    }

    private void OnRegionClicked(RegionDefinition region)
    {
        WorldMapProgressManager progress = FindProgressManager();
        if (!IsRegionAvailable(region, progress))
        {
            LockedRegionClickFeedback.LogLockedRegionNotice(region);
            return;
        }

        // Prevent flicker: if user clicks the already-selected region, keep current rows/selection as-is.
        if (_selectedRegion == region)
            return;

        _selectedRegion = region;
        _selectedNode = null;

        if (_selectedRegion != null && _selectedRegion.nodes is { Count: > 0 } && _selectedRegion.nodes[0])
            _selectedNode = _selectedRegion.nodes[0];

        for (int i = 0; i < _regionRows.Count && i < _regionRowRegions.Count; i++)
        {
            bool unlocked = IsRegionAvailable(_regionRowRegions[i], progress);
            bool selected = unlocked && _regionRowRegions[i] == _selectedRegion;
            SetRegionRowSelected(_regionRows[i], unlocked, selected);
        }

        RebuildNodeList();
        RefreshDetails();
    }

    private void RebuildNodeList()
    {
        ClearNodeButtons();

        WorldMapProgressManager progress = FindProgressManager();
        if (!nodeListParent || !nodeButtonPrefab || !_selectedRegion || _selectedRegion.nodes == null ||
            !IsRegionAvailable(_selectedRegion, progress))
            return;
        SkillsManager skills = FindSkillsManager();

        string activeNodeId = ResolveActiveMapNodeIdForRegionUi();
        List<MapNodeDefinition> filteredInRegionOrder = BuildFilteredRegionNodes(_selectedRegion);

        var unlockedAvailable = new List<MapNodeDefinition>(filteredInRegionOrder.Count);
        var unlockedCleared = new List<MapNodeDefinition>(filteredInRegionOrder.Count);
        var locked = new List<MapNodeDefinition>(filteredInRegionOrder.Count);
        for (int i = 0; i < filteredInRegionOrder.Count; i++)
        {
            MapNodeDefinition node = filteredInRegionOrder[i];
            string state = progress ? node.GetUiStateLabel(progress, skills) : "Unlocked";
            bool isLocked =
                string.Equals(state, "Map locked", StringComparison.Ordinal) ||
                string.Equals(state, "Skill locked", StringComparison.Ordinal) ||
                string.Equals(state, "Progress locked", StringComparison.Ordinal);
            if (isLocked)
            {
                locked.Add(node);
                continue;
            }

            bool atThisMap = !string.IsNullOrWhiteSpace(activeNodeId) &&
                !string.IsNullOrWhiteSpace(node.nodeId) &&
                string.Equals(node.nodeId.Trim(), activeNodeId.Trim(), StringComparison.OrdinalIgnoreCase);
            bool isCleared = IsNodeClearedForList(node, progress, atThisMap);
            if (isCleared)
                unlockedCleared.Add(node);
            else
                unlockedAvailable.Add(node);
        }

        MoveTownNodesToFront(unlockedAvailable);
        MoveTownNodesToFront(unlockedCleared);
        MoveTownNodesToFront(locked);

        // Keep selection valid for current filter/sectioned list.
        if (_selectedNode == null || !filteredInRegionOrder.Contains(_selectedNode))
            _selectedNode = unlockedAvailable.Count > 0
                ? unlockedAvailable[0]
                : (unlockedCleared.Count > 0 ? unlockedCleared[0] : (locked.Count > 0 ? locked[0] : null));

        if (unlockedAvailable.Count > 0 || unlockedCleared.Count > 0)
        {
            AddNodeSectionHeader("Unlocked");
            if (unlockedAvailable.Count > 0)
                SpawnNodeRows(unlockedAvailable, progress, skills, activeNodeId);
            if (unlockedCleared.Count > 0)
                SpawnNodeRows(unlockedCleared, progress, skills, activeNodeId);
        }

        if (locked.Count > 0)
        {
            AddNodeSectionHeader("Locked");
            SpawnNodeRows(locked, progress, skills, activeNodeId);
        }

        RefreshFilterButtonVisuals();
    }

    private void ClearNodeButtons()
    {
        for (int i = 0; i < _nodeButtons.Count; i++)
        {
            if (_nodeButtons[i])
                Destroy(_nodeButtons[i].gameObject);
        }

        _nodeButtons.Clear();

        for (int i = 0; i < _nodeSectionRows.Count; i++)
        {
            if (_nodeSectionRows[i])
                Destroy(_nodeSectionRows[i]);
        }
        _nodeSectionRows.Clear();
    }

    private void SpawnNodeRows(List<MapNodeDefinition> nodes, WorldMapProgressManager progress, SkillsManager skills, string activeNodeId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            MapNodeDefinition node = nodes[i];
            if (!node)
                continue;

            WorldMapNodeButtonUI row = Instantiate(nodeButtonPrefab, nodeListParent);
            _nodeButtons.Add(row);

            string state = progress
                ? node.GetUiStateLabel(progress, skills)
                : "Unlocked";

            bool atThisMap = !string.IsNullOrWhiteSpace(activeNodeId) &&
                !string.IsNullOrWhiteSpace(node.nodeId) &&
                string.Equals(node.nodeId.Trim(), activeNodeId.Trim(), StringComparison.OrdinalIgnoreCase);
            bool oneShotCleared = IsNodeClearedForList(node, progress, atThisMap);
            if (oneShotCleared)
                state = "Cleared";

            bool sel = _selectedNode && _selectedNode == node;
            bool greyOneShotDone = oneShotCleared;
            bool unavailable = !node.CanEnterFromLevelMenu(progress, skills);
            row.Bind(node, state, sel, OnNodeSelected, greyOneShotDone, unavailable, atThisMap);
        }
    }

    private List<MapNodeDefinition> BuildFilteredRegionNodes(RegionDefinition region)
    {
        var result = new List<MapNodeDefinition>();
        if (region == null || region.nodes == null)
            return result;

        for (int i = 0; i < region.nodes.Count; i++)
        {
            MapNodeDefinition node = region.nodes[i];
            if (!node)
                continue;
            if (!PassesNodeFilter(node))
                continue;
            result.Add(node);
        }

        return result;
    }

    private bool PassesNodeFilter(MapNodeDefinition node)
    {
        if (node == null)
            return false;

        return _nodeListFilter switch
        {
            NodeListFilter.All => true,
            NodeListFilter.Combat => node.nodeType == MapNodeType.Combat,
            NodeListFilter.Gathering => node.nodeType == MapNodeType.Gathering,
            NodeListFilter.Other => node.nodeType != MapNodeType.Combat && node.nodeType != MapNodeType.Gathering,
            _ => true
        };
    }

    private static void MoveTownNodesToFront(List<MapNodeDefinition> list)
    {
        if (list == null || list.Count <= 1)
            return;

        var towns = new List<MapNodeDefinition>(2);
        var nonTowns = new List<MapNodeDefinition>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            MapNodeDefinition n = list[i];
            if (n != null && n.nodeType == MapNodeType.Town)
                towns.Add(n);
            else
                nonTowns.Add(n);
        }

        list.Clear();
        list.AddRange(towns);
        list.AddRange(nonTowns);
    }

    private void AddNodeSectionHeader(string label)
    {
        if (nodeListParent == null || string.IsNullOrWhiteSpace(label))
            return;

        GameObject go = new GameObject($"Section_{label}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(nodeListParent, false);
        _nodeSectionRows.Add(go);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 24f;
        le.minHeight = 24f;

        Image bg = go.GetComponent<Image>();
        bg.color = new Color32(186, 178, 156, 255);
        bg.raycastTarget = false;

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 0f);
        textRt.offsetMax = new Vector2(-10f, 0f);

        TMP_Text text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = 20f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color32(70, 70, 70, 255);
        text.raycastTarget = false;
    }

    private void OnNodeFilterClicked(NodeListFilter filter)
    {
        if (_nodeListFilter == filter)
            return;
        _nodeListFilter = filter;
        RebuildNodeList();
        RefreshDetails();
    }

    private void RefreshFilterButtonVisuals()
    {
        ApplyFilterButtonSelected(filterAllButton, _nodeListFilter == NodeListFilter.All);
        ApplyFilterButtonSelected(filterCombatButton, _nodeListFilter == NodeListFilter.Combat);
        ApplyFilterButtonSelected(filterGatheringButton, _nodeListFilter == NodeListFilter.Gathering);
        ApplyFilterButtonSelected(filterOtherButton, _nodeListFilter == NodeListFilter.Other);
    }

    private static void ApplyFilterButtonSelected(Button b, bool selected)
    {
        if (!b)
            return;

        ColorBlock cb = b.colors;
        Color normal = selected ? new Color32(216, 206, 176, 255) : new Color32(238, 238, 238, 255);
        cb.normalColor = normal;
        cb.highlightedColor = normal;
        cb.selectedColor = normal;
        cb.pressedColor = normal;
        cb.colorMultiplier = 1f;
        b.colors = cb;
        if (b.targetGraphic)
            b.targetGraphic.color = normal;
    }

    private void OnNodeSelected(MapNodeDefinition node)
    {
        _selectedNode = node;
        RefreshNodeSelectionVisuals();
        RefreshDetails();
    }

    private void RefreshNodeSelectionVisuals()
    {
        for (int i = 0; i < _nodeButtons.Count; i++)
        {
            WorldMapNodeButtonUI row = _nodeButtons[i];
            if (!row) continue;
            row.SetSelected(row.Node == _selectedNode);
        }
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
        bool regionUnlocked = _selectedRegion == null || IsRegionAvailable(_selectedRegion, progress);

        if (!regionUnlocked)
            n = null;

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
            selectedNodeType.text = n ? $"Type: {n.nodeType}" : "";

        SkillsManager skills = FindSkillsManager();

        if (selectedNodeState)
        {
            if (!regionUnlocked)
                selectedNodeState.text = "State: Region locked";
            else if (n && progress)
                selectedNodeState.text = BuildStateText(n, progress, skills);
            else if (n)
                selectedNodeState.text = "State: Unlocked";
            else
                selectedNodeState.text = "";
        }

        if (selectedNodeRecommendedCp)
        {
            if (!n)
            {
                selectedNodeRecommendedCp.text = "";
                selectedNodeRecommendedCp.gameObject.SetActive(true);
            }
            else
            {
                int recCp = RecommendedCombatPower.GetRecommendedCombatPowerForDisplay(n);
                if (recCp <= 1)
                {
                    selectedNodeRecommendedCp.text = "";
                    selectedNodeRecommendedCp.gameObject.SetActive(false);
                }
                else
                {
                    selectedNodeRecommendedCp.gameObject.SetActive(true);
                    selectedNodeRecommendedCp.text = $"Recommended cp: {recCp}";
                }
            }
        }

        if (selectedNodeRepeatable)
            selectedNodeRepeatable.text = n ? (n.isRepeatable ? "Repeatable: Yes" : "Repeatable: No") : "";

        if (selectedNodeDescription)
        {
            if (!n || string.IsNullOrWhiteSpace(n.description))
                selectedNodeDescription.text = "";
            else
                selectedNodeDescription.text = $"Description: {n.description.Trim()}";
        }

        RefreshRequirementsBlock(n);
        RefreshNodeContainsSummary(n);

        string activeNodeId = ActiveLevelContext.ResolveActiveMapNodeIdForUi();
        bool isCurrentMap =
            n != null &&
            !string.IsNullOrWhiteSpace(activeNodeId) &&
            !string.IsNullOrWhiteSpace(n.nodeId) &&
            string.Equals(n.nodeId.Trim(), activeNodeId.Trim(), StringComparison.Ordinal);

        bool hideEnter = (n && progress && n.IsPermanentlyCompleted(progress)) || isCurrentMap || (n && n.entranceOnlyAccess);
        bool canEnterFromMenu = n && n.CanEnterFromLevelMenu(progress, skills);
        if (enterNodeButton)
        {
            enterNodeButton.gameObject.SetActive(!hideEnter);
            if (!hideEnter)
                enterNodeButton.interactable = canEnterFromMenu;
        }

        ApplyPanelThemeColors(n);
        RefreshDetailsScrollLayout(n);
        PublishHudPreview();
    }

    private void RefreshDetailsScrollLayout(MapNodeDefinition node)
    {
        ResolveDetailsScrollRefs();

        RectTransform content = detailsContentRoot;
        if (content == null && detailsScrollRect != null)
            content = detailsScrollRect.content;
        if (content == null)
            return;

        // Rebuild multiple layers because detail sections toggle active/inactive per-node.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        RectTransform p = content.parent as RectTransform;
        int guard = 0;
        while (p != null && guard < 6)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(p);
            p = p.parent as RectTransform;
            guard++;
        }
        Canvas.ForceUpdateCanvases();

        if (detailsScrollRect == null)
            return;

        string id = node != null ? node.nodeId : null;
        bool changedNode = !string.Equals(_lastDetailsNodeId, id, StringComparison.Ordinal);
        _lastDetailsNodeId = id;

        // New selection: start at top; same selection: preserve current scroll.
        if (changedNode)
            detailsScrollRect.verticalNormalizedPosition = 1f;

        detailsScrollRect.StopMovement();
    }

    private void ResolveDetailsScrollRefs()
    {
        if (!detailsScrollRect)
        {
            if (selectedNodeDescription)
                detailsScrollRect = selectedNodeDescription.GetComponentInParent<ScrollRect>(true);
            if (!detailsScrollRect && selectedNodeName)
                detailsScrollRect = selectedNodeName.GetComponentInParent<ScrollRect>(true);
        }

        if (!detailsContentRoot)
        {
            if (detailsScrollRect)
                detailsContentRoot = detailsScrollRect.content;
            else if (selectedNodeDescription)
                detailsContentRoot = selectedNodeDescription.rectTransform.parent as RectTransform;
        }
    }

    private void RefreshNodeContainsSummary(MapNodeDefinition n)
    {
        bool hasLegacy = selectedNodeContainsText || selectedNodeContainsRoot;
        bool hasSplit = selectedNodeContainsNpcText || selectedNodeContainsNpcRoot ||
                        selectedNodeContainsResourcesEnemiesText || selectedNodeContainsResourcesEnemiesRoot;
        if (!hasLegacy && !hasSplit)
            return;

        MapNodeInteractablesPreview.ContainsSummary split = MapNodeInteractablesPreview.BuildSplitSummary(n);
        string npcLine = string.IsNullOrEmpty(split.npcsLine) ? "" : $"Contains NPC's: {split.npcsLine}";
        string resourcesEnemiesLine = string.IsNullOrEmpty(split.resourcesEnemiesLine)
            ? ""
            : $"Contains: {split.resourcesEnemiesLine}";
        string legacyLine = npcLine;

        if (selectedNodeContainsNpcText)
            selectedNodeContainsNpcText.text = npcLine;
        if (selectedNodeContainsNpcRoot)
            selectedNodeContainsNpcRoot.SetActive(!string.IsNullOrEmpty(npcLine));
        else if (selectedNodeContainsNpcText)
            selectedNodeContainsNpcText.gameObject.SetActive(!string.IsNullOrEmpty(npcLine));

        if (selectedNodeContainsResourcesEnemiesText)
            selectedNodeContainsResourcesEnemiesText.text = resourcesEnemiesLine;
        if (selectedNodeContainsResourcesEnemiesRoot)
            selectedNodeContainsResourcesEnemiesRoot.SetActive(!string.IsNullOrEmpty(resourcesEnemiesLine));
        else if (selectedNodeContainsResourcesEnemiesText)
            selectedNodeContainsResourcesEnemiesText.gameObject.SetActive(!string.IsNullOrEmpty(resourcesEnemiesLine));

        if (selectedNodeContainsText)
        {
            selectedNodeContainsText.text = legacyLine;
            if (!selectedNodeContainsRoot)
                selectedNodeContainsText.gameObject.SetActive(!string.IsNullOrEmpty(legacyLine));
        }

        if (selectedNodeContainsRoot)
            selectedNodeContainsRoot.SetActive(!string.IsNullOrEmpty(legacyLine));
    }

    private void ApplyPanelThemeColors(MapNodeDefinition n)
    {
        if (!nodeButtonPrefab)
            return;
        Color c = nodeButtonPrefab.GetThemeColorForNode(n);
        SetImageColorPreserveAlpha(detailsPanelBackgroundImage, c);
        SetImageColorPreserveAlpha(locationsPanelBackgroundImage, c);
    }

    private static void SetImageColorPreserveAlpha(Image img, Color rgb)
    {
        if (!img)
            return;
        Color a = img.color;
        img.color = new Color(rgb.r, rgb.g, rgb.b, a.a);
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
        sb.Append(n.BuildRequirementsDisplayText(worldMap));

        // Map lock is already shown in the State line (GetUiStateLabel → "Map locked"); do not repeat here.

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
            selectedNodeRequirementsText.text = hasAny ? $"Requirements: {combined}" : "";

        if (selectedNodeRequirements)
            selectedNodeRequirements.SetActive(hasAny);
    }

    private void OnEnterNodeClicked()
    {
        WorldMapProgressManager progress = FindProgressManager();
        if (_selectedRegion != null && !IsRegionAvailable(_selectedRegion, progress))
        {
            Debug.LogWarning($"[LevelSelectPageUI] Enter blocked: region '{_selectedRegion.regionId}' is locked.");
            return;
        }

        if (!_selectedNode)
        {
            Debug.LogWarning("[LevelSelectPageUI] Enter: no node selected.");
            return;
        }

        SkillsManager skills = FindSkillsManager();
        if (!_selectedNode.CanEnterFromLevelMenu(progress, skills))
        {
            Debug.LogWarning($"[LevelSelectPageUI] Enter blocked (map/skills or entrance-only): {_selectedNode.nodeId}");
            return;
        }

        ActiveLevelContext.SetPendingLevel(_selectedNode);

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogError("[LevelSelectPageUI] Gameplay scene name is not set.");
            return;
        }

        Debug.Log($"[LevelSelectPageUI] Loading '{gameplaySceneName}' for node: {_selectedNode.nodeId}");
        PlayerLevelTransition.LoadSceneWithEffectOrImmediate(gameplaySceneName);
    }

    private bool IsRegionAvailable(RegionDefinition region, WorldMapProgressManager progress)
    {
        if (!region)
            return false;
        return region.IsRegionUnlocked(progress, ResolveActiveMapNodeIdForRegionUi(), worldMap);
    }

    private bool ShouldShowRegionInPicker(RegionDefinition region, WorldMapProgressManager progress)
    {
        if (!region)
            return false;
        return region.ShouldListInRegionPicker(progress, ResolveActiveMapNodeIdForRegionUi(), worldMap);
    }

    private void MigrateRegionSelectionIfNeeded(WorldMapProgressManager progress)
    {
        if (_regionRowRegions.Count == 0)
        {
            _selectedRegion = null;
            _selectedNode = null;
            return;
        }

        if (_selectedRegion != null && _regionRowRegions.Contains(_selectedRegion))
            return;

        _selectedRegion = _regionRowRegions[0];
        _selectedNode = null;
        if (_selectedRegion != null && _selectedRegion.nodes is { Count: > 0 })
            _selectedNode = _selectedRegion.nodes[0];

        for (int i = 0; i < _regionRows.Count && i < _regionRowRegions.Count; i++)
        {
            bool unlocked = IsRegionAvailable(_regionRowRegions[i], progress);
            SetRegionRowSelected(_regionRows[i], unlocked, unlocked && _regionRowRegions[i] == _selectedRegion);
        }
    }

    private static string ResolveActiveMapNodeIdForRegionUi()
    {
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition.nodeId;
        if (ActiveLevelContext.Current != null)
            return ActiveLevelContext.Current.nodeId;
        return null;
    }

    private string BuildStateText(MapNodeDefinition node, WorldMapProgressManager progress, SkillsManager skills)
    {
        if (node == null)
            return "";

        string activeNodeId = ResolveActiveMapNodeIdForRegionUi();
        bool atThisMap = !string.IsNullOrWhiteSpace(activeNodeId) &&
            !string.IsNullOrWhiteSpace(node.nodeId) &&
            string.Equals(node.nodeId.Trim(), activeNodeId.Trim(), StringComparison.OrdinalIgnoreCase);
        if (IsNodeClearedForList(node, progress, atThisMap))
            return "State: Cleared";

        string entranceNote = node.entranceOnlyAccess ? " (Can only be accessed from its entrance)" : "";
        if (node.entranceOnlyAccess && node.CanEnter(progress, skills))
            return $"State: Locked from menu{entranceNote}";
        string state = node.GetUiStateLabel(progress, skills);
        if (!string.Equals(state, "Progress locked", StringComparison.Ordinal))
            return $"State: {state}{entranceNote}";

        string progressText = BuildProgressLockDetails(node, progress);
        if (string.IsNullOrEmpty(progressText))
            return $"State: {state}{entranceNote}";

        return $"State: {state} ({progressText}){entranceNote}";
    }

    private static bool IsNodeClearedForList(
        MapNodeDefinition node,
        WorldMapProgressManager progress,
        bool atThisMap)
    {
        if (node == null || progress == null || node.isRepeatable)
            return false;
        if (!progress.IsNodeCompleted(node.nodeId))
            return false;
        if (atThisMap)
            return false;
        return true;
    }

    private static string BuildProgressLockDetails(MapNodeDefinition node, WorldMapProgressManager progress)
    {
        if (node == null || progress == null || node.requiredPreviousMapCompletions == null)
            return "";

        for (int i = 0; i < node.requiredPreviousMapCompletions.Count; i++)
        {
            PreviousMapCompletionRequirement req = node.requiredPreviousMapCompletions[i];
            if (req == null || !req.enabled || !req.requireEnemyKillsOnMap || req.requiredEnemyKillsOnMap <= 0)
                continue;

            string requiredNodeId = string.IsNullOrWhiteSpace(req.requiredMapNodeId) ? "" : req.requiredMapNodeId.Trim();
            if (string.IsNullOrEmpty(requiredNodeId))
                continue;

            int requiredKills = Mathf.Max(1, req.requiredEnemyKillsOnMap);
            int currentKills = Mathf.Max(0, progress.GetEnemyKillsOnNode(requiredNodeId));
            if (currentKills >= requiredKills)
                continue;

            string currentKillLabel = currentKills == 1 ? "kill" : "kills";
            string requiredKillLabel = requiredKills == 1 ? "kill" : "kills";
            return $"{currentKills} {currentKillLabel} / {requiredKills} {requiredKillLabel}";
        }

        return "";
    }
}
