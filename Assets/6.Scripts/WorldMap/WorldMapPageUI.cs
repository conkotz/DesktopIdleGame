using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// World Map view (dropdown regions + locations list + details + graph).
/// Attach this to `FullMapPage`.
/// </summary>
public class WorldMapPageUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WorldMapDefinition worldMap;

    [Header("Regions (dropdown)")]
    [SerializeField] private TMP_Dropdown regionDropdown;
    [SerializeField] private TMP_Text regionNameLabel;

    [Header("Locations (left)")]
    [SerializeField] private Transform nodeListParent;
    [SerializeField] private WorldMapNodeButtonUI nodeRowPrefab;
    [Header("Filters (optional)")]
    [SerializeField] private Button filterAllButton;
    [SerializeField] private Button filterCombatButton;
    [SerializeField] private Button filterGatheringButton;
    [SerializeField] private Button filterOtherButton;

    [Header("Details (center)")]
    [SerializeField] private TMP_Text selectedNodeName;
    [SerializeField] private Image selectedNodeIcon;
    [SerializeField] private TMP_Text selectedNodeType;
    [SerializeField] private TMP_Text selectedNodeState;
    [SerializeField] private TMP_Text selectedNodeRecommendedCp;
    [SerializeField] private TMP_Text selectedNodeRepeatable;
    [SerializeField] private TMP_Text selectedNodeDescription;
    [SerializeField] private ScrollRect detailsScrollRect;
    [SerializeField] private RectTransform detailsContentRoot;
    [SerializeField] private GameObject selectedNodeRequirements;
    [SerializeField] private TMP_Text selectedNodeRequirementsText;
    [FormerlySerializedAs("selectedNodeContainsNpcText")]
    [SerializeField] private TMP_Text selectedNodeContainsNpcMerchantsText;
    [FormerlySerializedAs("selectedNodeContainsResourcesEnemiesText")]
    [SerializeField] private TMP_Text selectedNodeContainsEnemiesText;
    [FormerlySerializedAs("selectedNodeContainsText")]
    [SerializeField] private TMP_Text selectedNodeContainsOtherText;
    [SerializeField] private Button enterNodeButton;
    [Header("Details layout (dynamic height)")]
    [SerializeField] private float minDescriptionHeight = 24f;
    [SerializeField] private float minRequirementsHeight = 20f;
    [SerializeField] private float minContainsLineHeight = 20f;
    [SerializeField] private float dynamicTextBottomPadding = 2f;

    [Header("Graph")]
    [SerializeField] private RectTransform nodesRoot;
    [SerializeField] private RectTransform connectorsRoot;
    [SerializeField] private Transform anchorsRoot;
    [SerializeField] private WorldMapGraphNodeUI nodePrefab;
    [SerializeField] private SkillTreeConnectorUI connectorPrefab;

    [Header("Presentation swap")]
    [SerializeField] private GameObject listPresentationRoot;
    [SerializeField] private GameObject worldMapPresentationRoot;
    [SerializeField] private Button returnToListButton;
    [Tooltip("Optional extra roots to hide while world-map presentation is active (e.g. old list header bars).")]
    [SerializeField] private GameObject[] hideWhenWorldMapActive;

    [Header("Theme — panel backgrounds (optional)")]
    [SerializeField] private Image detailsPanelBackgroundImage;
    [SerializeField] private Image locationsPanelBackgroundImage;

    private readonly List<RegionDefinition> _regions = new();
    private readonly List<WorldMapNodeButtonUI> _nodeRows = new();
    private readonly List<GameObject> _nodeSectionRows = new();
    private readonly List<WorldMapGraphNodeUI> _spawnedNodes = new();
    private readonly List<Edge> _edges = new();
    private bool _connectorsDirty;

    private RegionDefinition _selectedRegion;
    private MapNodeDefinition _selectedNode;
    private string _lastDetailsNodeId;
    private int _detailsLayoutStabilizeFrames;

    private WorldMapProgressManager _progressEventsTarget;
    private SkillsManager _skillsLevelEventsTarget;

    private readonly struct Edge
    {
        public readonly SkillTreeConnectorUI connector;
        public readonly WorldMapGraphNodeUI from;
        public readonly WorldMapGraphNodeUI to;
        public Edge(SkillTreeConnectorUI connector, WorldMapGraphNodeUI from, WorldMapGraphNodeUI to)
        {
            this.connector = connector;
            this.from = from;
            this.to = to;
        }
    }

    private void Awake()
    {
        if (returnToListButton)
        {
            returnToListButton.onClick.RemoveAllListeners();
            returnToListButton.onClick.AddListener(ReturnToList);
        }
        if (enterNodeButton)
        {
            enterNodeButton.onClick.RemoveAllListeners();
            enterNodeButton.onClick.AddListener(OnEnterNodeClicked);
        }
        if (regionDropdown)
        {
            regionDropdown.onValueChanged.RemoveAllListeners();
            regionDropdown.onValueChanged.AddListener(OnRegionDropdownChanged);
        }
        if (filterAllButton)
        {
            filterAllButton.onClick.RemoveAllListeners();
            filterAllButton.onClick.AddListener(() => OnFilterClicked(LevelSelectSharedState.NodeListFilter.All));
        }
        if (filterCombatButton)
        {
            filterCombatButton.onClick.RemoveAllListeners();
            filterCombatButton.onClick.AddListener(() => OnFilterClicked(LevelSelectSharedState.NodeListFilter.Combat));
        }
        if (filterGatheringButton)
        {
            filterGatheringButton.onClick.RemoveAllListeners();
            filterGatheringButton.onClick.AddListener(() => OnFilterClicked(LevelSelectSharedState.NodeListFilter.Gathering));
        }
        if (filterOtherButton)
        {
            filterOtherButton.onClick.RemoveAllListeners();
            filterOtherButton.onClick.AddListener(() => OnFilterClicked(LevelSelectSharedState.NodeListFilter.Other));
        }
    }

    private void OnDestroy()
    {
        if (returnToListButton)
            returnToListButton.onClick.RemoveAllListeners();
        if (enterNodeButton)
            enterNodeButton.onClick.RemoveAllListeners();
        if (regionDropdown)
            regionDropdown.onValueChanged.RemoveListener(OnRegionDropdownChanged);
        if (filterAllButton) filterAllButton.onClick.RemoveAllListeners();
        if (filterCombatButton) filterCombatButton.onClick.RemoveAllListeners();
        if (filterGatheringButton) filterGatheringButton.onClick.RemoveAllListeners();
        if (filterOtherButton) filterOtherButton.onClick.RemoveAllListeners();
    }

    private void OnEnable()
    {
        LevelSelectSharedState.LastPresentation = LevelSelectSharedState.Presentation.WorldMap;
        SetExtraRootsWorldMapVisibility(true);

        TrySubscribeProgressChanged();
        TrySubscribeSkillsLevelEvents();

        ResolveDefaults();

        ApplyOrRestoreSelection();
        RebuildRegionsDropdown();
        RebuildNodeList();
        RefreshDetails();
        RebuildGraph();
    }

    private void OnDisable()
    {
        // Restore hidden roots first; this must happen even when we're being disabled externally.
        SetExtraRootsWorldMapVisibility(false);

        UnsubscribeProgressChanged();
        UnsubscribeSkillsLevelEvents();
        LevelSelectSharedState.HudPreviewSelection = null;

        ClearGraph();
        ClearNodeRows();
        _detailsLayoutStabilizeFrames = 0;

    }

    private void LateUpdate()
    {
        EnsureClosedWhenMenuLeavesLevelSelect();

        if (_detailsLayoutStabilizeFrames > 0)
        {
            _detailsLayoutStabilizeFrames--;
            StabilizeDetailsLayoutPass();
        }

        if (!_connectorsDirty)
            return;
        _connectorsDirty = false;
        SyncConnectorRects();
    }

    private void EnsureClosedWhenMenuLeavesLevelSelect()
    {
        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        bool menuOnLevelSelect =
            menu != null &&
            menu.IsOpen &&
            listPresentationRoot != null &&
            menu.CurrentPage == listPresentationRoot;
        if (menuOnLevelSelect)
            return;

        // If user switched tabs or closed the main menu, hide world map and restore title/header roots.
        SetExtraRootsWorldMapVisibility(false);
        if (worldMapPresentationRoot && worldMapPresentationRoot.activeSelf)
            worldMapPresentationRoot.SetActive(false);
    }

    private void ResolveDefaults()
    {
        if (worldMap)
            return;
        WorldMapProgressManager p = FindProgressManager();
        if (p)
            worldMap = p.WorldMap;
    }

    private void ApplyOrRestoreSelection()
    {
        if (!worldMap)
            return;

        string rid = LevelSelectSharedState.Norm(LevelSelectSharedState.SelectedRegionId);
        if (!string.IsNullOrEmpty(rid))
            _selectedRegion = worldMap.FindRegionById(rid);

        if (!_selectedRegion && worldMap.regions is { Count: > 0 })
            _selectedRegion = worldMap.regions[0];

        string nid = LevelSelectSharedState.Norm(LevelSelectSharedState.SelectedNodeId);
        if (!string.IsNullOrEmpty(nid) && _selectedRegion)
            _selectedNode = _selectedRegion.FindNodeById(nid);

        if (!_selectedNode && _selectedRegion && _selectedRegion.nodes is { Count: > 0 })
            _selectedNode = _selectedRegion.nodes[0];
    }

    private void RebuildRegionsDropdown()
    {
        _regions.Clear();
        if (!worldMap || worldMap.regions == null)
            return;

        WorldMapProgressManager progress = FindProgressManager();
        for (int i = 0; i < worldMap.regions.Count; i++)
        {
            RegionDefinition r = worldMap.regions[i];
            if (!r) continue;
            if (!r.ShouldListInRegionPicker(progress, ResolveActiveMapNodeIdForRegionUi(), worldMap))
                continue;
            _regions.Add(r);
        }

        if (_regions.Count == 0)
            return;

        if (_selectedRegion == null || !_regions.Contains(_selectedRegion))
            _selectedRegion = _regions[0];

        if (regionDropdown)
        {
            regionDropdown.ClearOptions();
            var options = new List<string>(_regions.Count);
            for (int i = 0; i < _regions.Count; i++)
            {
                RegionDefinition r = _regions[i];
                bool unlocked = IsRegionAvailable(r, progress);
                options.Add(unlocked ? r.displayName : $"{r.displayName} (Locked)");
            }
            regionDropdown.AddOptions(options);
            int idx = Mathf.Max(0, _regions.IndexOf(_selectedRegion));
            regionDropdown.SetValueWithoutNotify(idx);
        }

        RefreshDropdownCaption();
    }

    private void RefreshDropdownCaption()
    {
        string caption = _selectedRegion ? _selectedRegion.displayName : "";
        if (regionDropdown && regionDropdown.captionText)
            regionDropdown.captionText.text = caption;
        if (regionNameLabel)
            regionNameLabel.text = caption;
    }

    private void OnRegionDropdownChanged(int idx)
    {
        if (idx < 0 || idx >= _regions.Count)
            return;

        RegionDefinition next = _regions[idx];
        WorldMapProgressManager progress = FindProgressManager();
        if (!IsRegionAvailable(next, progress))
        {
            LockedRegionClickFeedback.LogLockedRegionNotice(next);
            RebuildRegionsDropdown();
            return;
        }

        if (_selectedRegion == next)
            return;

        _selectedRegion = next;
        _selectedNode = _selectedRegion.nodes is { Count: > 0 } ? _selectedRegion.nodes[0] : null;

        LevelSelectSharedState.SelectedRegionId = _selectedRegion ? _selectedRegion.regionId : "";
        LevelSelectSharedState.SelectedNodeId = _selectedNode ? _selectedNode.nodeId : "";

        RefreshDropdownCaption();
        RebuildNodeList();
        RefreshDetails();
        RebuildGraph();
    }

    private void RebuildNodeList()
    {
        ClearNodeRows();

        WorldMapProgressManager progress = FindProgressManager();
        if (!nodeListParent || !nodeRowPrefab || !_selectedRegion || _selectedRegion.nodes == null ||
            !IsRegionAvailable(_selectedRegion, progress))
            return;

        SkillsManager skills = FindSkillsManager();
        string activeNodeId = ResolveActiveMapNodeIdForRegionUi();

        List<MapNodeDefinition> filtered = BuildFilteredRegionNodes(_selectedRegion);
        var unlocked = new List<MapNodeDefinition>(filtered.Count);
        var locked = new List<MapNodeDefinition>(filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
        {
            MapNodeDefinition node = filtered[i];
            if (!node) continue;
            string state = progress ? node.GetUiStateLabel(progress, skills) : "Unlocked";
            bool isLocked =
                string.Equals(state, "Map locked", StringComparison.Ordinal) ||
                string.Equals(state, "Skill locked", StringComparison.Ordinal) ||
                string.Equals(state, "Progress locked", StringComparison.Ordinal);
            if (isLocked) locked.Add(node);
            else unlocked.Add(node);
        }

        if (_selectedNode == null || !filtered.Contains(_selectedNode))
            _selectedNode = unlocked.Count > 0 ? unlocked[0] : (locked.Count > 0 ? locked[0] : null);

        if (unlocked.Count > 0)
        {
            AddSectionHeader("Unlocked");
            SpawnRows(unlocked, progress, skills, activeNodeId);
        }
        if (locked.Count > 0)
        {
            AddSectionHeader("Locked");
            SpawnRows(locked, progress, skills, activeNodeId);
        }

        RefreshFilterButtonVisuals();
    }

    private void OnFilterClicked(LevelSelectSharedState.NodeListFilter filter)
    {
        if (LevelSelectSharedState.Filter == filter)
            return;

        LevelSelectSharedState.Filter = filter;
        RebuildNodeList();
        RefreshDetails();
        RebuildGraph();
    }

    private void RefreshFilterButtonVisuals()
    {
        ApplyFilterSelected(filterAllButton, LevelSelectSharedState.Filter == LevelSelectSharedState.NodeListFilter.All);
        ApplyFilterSelected(filterCombatButton, LevelSelectSharedState.Filter == LevelSelectSharedState.NodeListFilter.Combat);
        ApplyFilterSelected(filterGatheringButton, LevelSelectSharedState.Filter == LevelSelectSharedState.NodeListFilter.Gathering);
        ApplyFilterSelected(filterOtherButton, LevelSelectSharedState.Filter == LevelSelectSharedState.NodeListFilter.Other);
    }

    private static void ApplyFilterSelected(Button b, bool selected)
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

    private List<MapNodeDefinition> BuildFilteredRegionNodes(RegionDefinition region)
    {
        var result = new List<MapNodeDefinition>();
        if (region == null || region.nodes == null)
            return result;

        for (int i = 0; i < region.nodes.Count; i++)
        {
            MapNodeDefinition node = region.nodes[i];
            if (!node) continue;

            bool ok = LevelSelectSharedState.Filter switch
            {
                LevelSelectSharedState.NodeListFilter.All => true,
                LevelSelectSharedState.NodeListFilter.Combat => node.nodeType == MapNodeType.Combat,
                LevelSelectSharedState.NodeListFilter.Gathering => node.nodeType == MapNodeType.Gathering,
                LevelSelectSharedState.NodeListFilter.Other => node.nodeType != MapNodeType.Combat && node.nodeType != MapNodeType.Gathering,
                _ => true
            };
            if (ok)
                result.Add(node);
        }
        return result;
    }

    private void ClearNodeRows()
    {
        for (int i = 0; i < _nodeRows.Count; i++)
        {
            if (_nodeRows[i])
                Destroy(_nodeRows[i].gameObject);
        }
        _nodeRows.Clear();

        for (int i = 0; i < _nodeSectionRows.Count; i++)
        {
            if (_nodeSectionRows[i])
                Destroy(_nodeSectionRows[i]);
        }
        _nodeSectionRows.Clear();
    }

    private void AddSectionHeader(string label)
    {
        if (!nodeListParent || string.IsNullOrWhiteSpace(label))
            return;

        GameObject go = new GameObject($"Section_{label}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(nodeListParent, false);
        _nodeSectionRows.Add(go);

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

    private void SpawnRows(List<MapNodeDefinition> nodes, WorldMapProgressManager progress, SkillsManager skills, string activeNodeId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            MapNodeDefinition node = nodes[i];
            if (!node) continue;

            WorldMapNodeButtonUI row = Instantiate(nodeRowPrefab, nodeListParent);
            _nodeRows.Add(row);

            string state = progress ? node.GetUiStateLabel(progress, skills) : "Unlocked";
            bool atThisMap = !string.IsNullOrWhiteSpace(activeNodeId) &&
                !string.IsNullOrWhiteSpace(node.nodeId) &&
                string.Equals(node.nodeId.Trim(), activeNodeId.Trim(), StringComparison.OrdinalIgnoreCase);
            bool oneShotCleared = progress != null && !node.isRepeatable && progress.IsNodeCompleted(node.nodeId) && !atThisMap;
            if (oneShotCleared)
                state = "Cleared";

            bool sel = _selectedNode && _selectedNode == node;
            bool unavailable = !node.CanEnterFromLevelMenu(progress, skills);
            row.Bind(node, state, sel, OnNodeSelected, oneShotCleared, unavailable, atThisMap);
        }
    }

    private void OnNodeSelected(MapNodeDefinition node)
    {
        if (!node)
            return;

        // Graph click should always drive the left Locations list selection too.
        // If the clicked node belongs to a different region (future-proof), switch region first.
        RegionDefinition clickedRegion = worldMap ? worldMap.FindRegionContainingNode(node.nodeId) : null;
        if (clickedRegion != null && clickedRegion != _selectedRegion)
            _selectedRegion = clickedRegion;

        _selectedNode = node;
        LevelSelectSharedState.SelectedRegionId = _selectedRegion ? _selectedRegion.regionId : "";
        LevelSelectSharedState.SelectedNodeId = _selectedNode ? _selectedNode.nodeId : "";

        // Rebuild rows so the Locations panel and graph stay visually in sync after graph clicks.
        RebuildRegionsDropdown();
        RebuildNodeList();
        RefreshNodeSelectionVisuals();
        RefreshDetails();
        RebuildGraph();
    }

    private void RefreshNodeSelectionVisuals()
    {
        for (int i = 0; i < _nodeRows.Count; i++)
        {
            WorldMapNodeButtonUI row = _nodeRows[i];
            if (!row) continue;
            row.SetSelected(row.Node == _selectedNode);
        }
    }

    private void RefreshDetails()
    {
        MapNodeDefinition n = _selectedNode;
        WorldMapProgressManager progress = FindProgressManager();
        bool regionUnlocked = _selectedRegion == null || IsRegionAvailable(_selectedRegion, progress);
        if (!regionUnlocked)
            n = null;

        if (selectedNodeName) selectedNodeName.text = n ? n.displayName : "—";

        if (selectedNodeIcon)
        {
            bool hasIcon = n && n.icon;
            selectedNodeIcon.enabled = hasIcon;
            selectedNodeIcon.sprite = hasIcon ? n.icon : null;
        }

        if (selectedNodeType) selectedNodeType.text = n ? $"Type: {n.nodeType}" : "";

        SkillsManager skills = FindSkillsManager();
        if (selectedNodeState)
        {
            if (!regionUnlocked) selectedNodeState.text = "State: Region locked";
            else if (n && progress) selectedNodeState.text = $"State: {n.GetUiStateLabel(progress, skills)}";
            else if (n) selectedNodeState.text = "State: Unlocked";
            else selectedNodeState.text = "";
        }

        if (selectedNodeRecommendedCp)
        {
            if (!n) selectedNodeRecommendedCp.text = "";
            else
            {
                int recCp = RecommendedCombatPower.GetRecommendedCombatPowerForDisplay(n);
                selectedNodeRecommendedCp.text = recCp <= 1 ? "" : $"Recommended cp: {recCp}";
                selectedNodeRecommendedCp.gameObject.SetActive(recCp > 1);
            }
        }

        if (selectedNodeRepeatable) selectedNodeRepeatable.text = n ? (n.isRepeatable ? "Repeatable: Yes" : "Repeatable: No") : "";
        if (selectedNodeDescription) selectedNodeDescription.text = n && !string.IsNullOrWhiteSpace(n.description) ? $"Description: {n.description.Trim()}" : "";

        RefreshRequirementsBlock(n);
        RefreshNodeContainsSummary(n);

        bool canEnterFromMenu = n && n.CanEnterFromLevelMenu(progress, skills);
        if (enterNodeButton)
        {
            enterNodeButton.gameObject.SetActive(n != null);
            enterNodeButton.interactable = canEnterFromMenu;
        }

        ApplyPanelThemeColors(n);
        RefreshDetailsScrollLayout(n);

        LevelSelectSharedState.HudPreviewSelection = n;
    }

    private void ApplyPanelThemeColors(MapNodeDefinition n)
    {
        if (!nodeRowPrefab)
            return;
        Color c = nodeRowPrefab.GetThemeColorForNode(n);
        SetImageColorPreserveAlpha(detailsPanelBackgroundImage, c);
        SetImageColorPreserveAlpha(locationsPanelBackgroundImage, c);
    }

    private static void SetImageColorPreserveAlpha(Image img, Color rgb)
    {
        if (!img) return;
        Color a = img.color;
        img.color = new Color(rgb.r, rgb.g, rgb.b, a.a);
    }

    private void RefreshRequirementsBlock(MapNodeDefinition n)
    {
        if (!selectedNodeRequirementsText && !selectedNodeRequirements)
            return;
        if (!n)
        {
            if (selectedNodeRequirementsText) selectedNodeRequirementsText.text = "";
            if (selectedNodeRequirements) selectedNodeRequirements.SetActive(false);
            return;
        }

        var sb = new StringBuilder();
        sb.Append(n.BuildRequirementsDisplayText(worldMap));
        SkillsManager skills = FindSkillsManager();
        if (skills != null && n.HasSkillGates())
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(n.MeetsSkillRequirements(skills) ? "Status: Skill requirements met." : "Status: Skill requirements not met.");
        }
        string combined = sb.ToString().Trim();
        bool hasAny = combined.Length > 0;
        if (selectedNodeRequirementsText)
            selectedNodeRequirementsText.text = hasAny ? $"Requirements: {combined}" : "";
        if (selectedNodeRequirements)
            selectedNodeRequirements.SetActive(hasAny);
    }

    private void RefreshNodeContainsSummary(MapNodeDefinition n)
    {
        bool hasAny =
            selectedNodeContainsNpcMerchantsText ||
            selectedNodeContainsEnemiesText ||
            selectedNodeContainsOtherText;
        if (!hasAny)
            return;

        MapNodeInteractablesPreview.ContainsSummary split = MapNodeInteractablesPreview.BuildSplitSummary(n);
        string npcLine = string.IsNullOrEmpty(split.npcMerchantsLine) ? "" : $"Contains NPC's/Merchants: {split.npcMerchantsLine}";
        string enemiesLine = string.IsNullOrEmpty(split.enemiesLine) ? "" : $"Contains Enemies: {split.enemiesLine}";
        string otherLine = string.IsNullOrEmpty(split.otherLine) ? "" : $"Contains Other: {split.otherLine}";

        if (selectedNodeContainsNpcMerchantsText)
        {
            selectedNodeContainsNpcMerchantsText.text = npcLine;
            selectedNodeContainsNpcMerchantsText.gameObject.SetActive(!string.IsNullOrEmpty(npcLine));
        }
        if (selectedNodeContainsEnemiesText)
        {
            selectedNodeContainsEnemiesText.text = enemiesLine;
            selectedNodeContainsEnemiesText.gameObject.SetActive(!string.IsNullOrEmpty(enemiesLine));
        }
        if (selectedNodeContainsOtherText)
        {
            selectedNodeContainsOtherText.text = otherLine;
            selectedNodeContainsOtherText.gameObject.SetActive(!string.IsNullOrEmpty(otherLine));
        }
    }

    private void RefreshDetailsScrollLayout(MapNodeDefinition node)
    {
        ResolveDetailsScrollRefs();

        EnsureDynamicDetailsTextHeights();

        RectTransform content = detailsContentRoot;
        if (content == null && detailsScrollRect != null)
            content = detailsScrollRect.content;
        if (content == null)
            return;

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
        if (changedNode)
            detailsScrollRect.verticalNormalizedPosition = 1f;
        detailsScrollRect.StopMovement();

        // Layout groups / TMP sizing can settle across multiple frames.
        // Run one or two extra passes to avoid occasional spacing jitter.
        _detailsLayoutStabilizeFrames = 2;
    }

    private void EnsureDynamicDetailsTextHeights()
    {
        EnsureDynamicTextHeight(selectedNodeDescription, minDescriptionHeight);
        EnsureDynamicTextHeight(selectedNodeRequirementsText, minRequirementsHeight);
        EnsureDynamicTextHeight(selectedNodeContainsNpcMerchantsText, minContainsLineHeight);
        EnsureDynamicTextHeight(selectedNodeContainsEnemiesText, minContainsLineHeight);
        EnsureDynamicTextHeight(selectedNodeContainsOtherText, minContainsLineHeight);
    }

    private void EnsureDynamicTextHeight(TMP_Text text, float minHeight)
    {
        if (!text || !text.gameObject.activeInHierarchy)
            return;

        RectTransform rt = text.rectTransform;
        float width = rt.rect.width;
        if (width <= 1f)
            width = Mathf.Max(1f, rt.sizeDelta.x);

        text.ForceMeshUpdate();
        Vector2 preferred = text.GetPreferredValues(text.text, width, 0f);
        float targetHeight = Mathf.Max(minHeight, preferred.y + Mathf.Max(0f, dynamicTextBottomPadding));

        LayoutElement le = text.GetComponent<LayoutElement>();
        if (!le)
            le = text.gameObject.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
        le.preferredHeight = targetHeight;
        le.flexibleHeight = 0f;
    }

    private void StabilizeDetailsLayoutPass()
    {
        ResolveDetailsScrollRefs();
        EnsureDynamicDetailsTextHeights();

        RectTransform content = detailsContentRoot;
        if (content == null && detailsScrollRect != null)
            content = detailsScrollRect.content;
        if (content == null)
            return;

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

    private void OnEnterNodeClicked()
    {
        WorldMapProgressManager progress = FindProgressManager();
        if (_selectedRegion != null && !IsRegionAvailable(_selectedRegion, progress))
            return;
        if (!_selectedNode)
            return;

        SkillsManager skills = FindSkillsManager();
        if (!_selectedNode.CanEnterFromLevelMenu(progress, skills))
            return;

        ActiveLevelContext.SetPendingLevel(_selectedNode);
        PlayerLevelTransition.LoadSceneWithEffectOrImmediate("GamePlay");
    }

    private void ReturnToList()
    {
        LevelSelectSharedState.LastPresentation = LevelSelectSharedState.Presentation.List;
        SetExtraRootsWorldMapVisibility(false);
        if (worldMapPresentationRoot)
            worldMapPresentationRoot.SetActive(false);
        if (listPresentationRoot)
            listPresentationRoot.SetActive(true);
    }

    private void SetExtraRootsWorldMapVisibility(bool worldMapActive)
    {
        // 1) Local assignments on WorldMapPageUI
        if (hideWhenWorldMapActive != null)
        {
            for (int i = 0; i < hideWhenWorldMapActive.Length; i++)
            {
                GameObject go = hideWhenWorldMapActive[i];
                if (!go) continue;
                go.SetActive(!worldMapActive);
            }
        }

        // 2) Shared assignments captured from LevelSelectListViewUI
        IReadOnlyList<GameObject> shared = LevelSelectSharedState.HideRoots;
        for (int i = 0; i < shared.Count; i++)
        {
            GameObject go = shared[i];
            if (!go) continue;
            go.SetActive(!worldMapActive);
        }
    }

    private void RebuildGraph()
    {
        if (!nodesRoot || !anchorsRoot || !nodePrefab)
            return;

        ClearGraph();

        WorldMapProgressManager progress = FindProgressManager();
        if (!_selectedRegion || _selectedRegion.nodes == null || !IsRegionAvailable(_selectedRegion, progress))
            return;

        WorldMapNodeAnchor[] anchors = CollectAnchorsForSelectedRegion();
        if (anchors == null || anchors.Length == 0)
            return;

        SkillsManager skills = FindSkillsManager();
        // Graph follows current filter selection: hide non-filtered nodes.
        List<MapNodeDefinition> graphNodes = BuildFilteredRegionNodes(_selectedRegion);
        if (graphNodes.Count == 0)
            return;
        var graphNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < graphNodes.Count; i++)
        {
            if (!graphNodes[i] || string.IsNullOrWhiteSpace(graphNodes[i].nodeId))
                continue;
            graphNodeIds.Add(graphNodes[i].nodeId.Trim());
        }

        string activeNodeId = ResolveActiveMapNodeIdForRegionUi();
        var nodesById = new Dictionary<string, WorldMapGraphNodeUI>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < anchors.Length; i++)
        {
            WorldMapNodeAnchor anchor = anchors[i];
            if (!anchor) continue;

            string id = anchor.ResolveTrimmedId();
            if (string.IsNullOrEmpty(id)) continue;

            MapNodeDefinition node = _selectedRegion.FindNodeById(id);
            if (!node) continue;
            string nodeId = node.nodeId?.Trim() ?? "";
            if (string.IsNullOrEmpty(nodeId) || !graphNodeIds.Contains(nodeId)) continue;

            RectTransform anchorRt = anchor.transform as RectTransform;
            if (!anchorRt) continue;

            WorldMapGraphNodeUI nodeUi = Instantiate(nodePrefab, nodesRoot);
            _spawnedNodes.Add(nodeUi);

            RectTransform rt = nodeUi.RectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = GetLocalPos(nodesRoot, anchorRt);

            string state = progress ? node.GetUiStateLabel(progress, skills) : "Unlocked";
            bool atThisMap = !string.IsNullOrWhiteSpace(activeNodeId) &&
                !string.IsNullOrWhiteSpace(node.nodeId) &&
                string.Equals(node.nodeId.Trim(), activeNodeId.Trim(), StringComparison.OrdinalIgnoreCase);
            bool oneShotCleared = progress != null && !node.isRepeatable && progress.IsNodeCompleted(node.nodeId) && !atThisMap;
            if (oneShotCleared) state = "Cleared";

            bool sel = _selectedNode && _selectedNode == node;
            bool unavailable = !node.CanEnterFromLevelMenu(progress, skills);
            nodeUi.Bind(node, state, sel, OnNodeSelected, oneShotCleared, unavailable, nodeRowPrefab);

            string dictKey = node.nodeId?.Trim() ?? "";
            if (!string.IsNullOrEmpty(dictKey))
                nodesById[dictKey] = nodeUi;
        }

        if (connectorPrefab && connectorsRoot)
        {
            var usedEdges = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, WorldMapGraphNodeUI> kv in nodesById)
            {
                MapNodeDefinition def = kv.Value ? kv.Value.Node : null;
                if (!def) continue;
                AddEdgesForNode(def, nodesById, usedEdges);
            }
        }

        if (connectorsRoot == nodesRoot)
        {
            for (int i = 0; i < _spawnedNodes.Count; i++)
                if (_spawnedNodes[i]) _spawnedNodes[i].transform.SetAsLastSibling();
        }

        _connectorsDirty = true;
    }

    private static Vector2 GetLocalPos(RectTransform parent, RectTransform child)
    {
        Vector3 world = child.position;
        Vector3 local = parent.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    private WorldMapNodeAnchor[] CollectAnchorsForSelectedRegion()
    {
        if (!anchorsRoot || _selectedRegion == null)
            return Array.Empty<WorldMapNodeAnchor>();

        string rid = _selectedRegion.regionId?.Trim() ?? "";
        WorldMapRegionAnchorGroup[] groups = anchorsRoot.GetComponentsInChildren<WorldMapRegionAnchorGroup>(true);
        if (groups.Length > 0)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                WorldMapRegionAnchorGroup g = groups[i];
                if (!g) continue;
                if (string.Equals(g.ResolveTrimmedRegionId(), rid, StringComparison.OrdinalIgnoreCase))
                    return g.GetComponentsInChildren<WorldMapNodeAnchor>(true);
            }
            return Array.Empty<WorldMapNodeAnchor>();
        }
        return anchorsRoot.GetComponentsInChildren<WorldMapNodeAnchor>(true);
    }

    private static string EdgeKey(string a, string b)
    {
        string x = LevelSelectSharedState.Norm(a);
        string y = LevelSelectSharedState.Norm(b);
        if (string.Compare(x, y, StringComparison.Ordinal) <= 0) return x + "\u001f" + y;
        return y + "\u001f" + x;
    }

    private void AddEdgesForNode(MapNodeDefinition node, Dictionary<string, WorldMapGraphNodeUI> nodesById, HashSet<string> usedEdges)
    {
        if (!node) return;
        string fromId = node.nodeId?.Trim() ?? "";
        if (string.IsNullOrEmpty(fromId) || !nodesById.TryGetValue(fromId, out var fromUi) || !fromUi)
            return;

        void TryEdge(string rawTarget)
        {
            if (string.IsNullOrWhiteSpace(rawTarget)) return;
            string toId = rawTarget.Trim();
            if (!nodesById.TryGetValue(toId, out var toUi) || !toUi) return;
            string key = EdgeKey(fromId, toId);
            if (!usedEdges.Add(key)) return;

            SkillTreeConnectorUI conn = Instantiate(connectorPrefab, connectorsRoot);
            conn.SetPositions(fromUi, toUi, trimToNodeEdges: false, pixelSnap: true);
            _edges.Add(new Edge(conn, fromUi, toUi));
        }

        if (node.connectedNodeIds != null)
            for (int i = 0; i < node.connectedNodeIds.Count; i++) TryEdge(node.connectedNodeIds[i]);
        if (node.nextNodeIds != null)
            for (int i = 0; i < node.nextNodeIds.Count; i++) TryEdge(node.nextNodeIds[i]);
    }

    private void ClearGraph()
    {
        for (int i = 0; i < _edges.Count; i++)
        {
            Edge e = _edges[i];
            if (e.connector)
                Destroy(e.connector.gameObject);
        }
        _edges.Clear();

        for (int i = 0; i < _spawnedNodes.Count; i++)
            if (_spawnedNodes[i]) Destroy(_spawnedNodes[i].gameObject);
        _spawnedNodes.Clear();
        _connectorsDirty = false;
    }

    private void SyncConnectorRects()
    {
        for (int i = 0; i < _edges.Count; i++)
        {
            Edge e = _edges[i];
            if (e.connector && e.from && e.to)
                e.connector.SetPositions(e.from, e.to, trimToNodeEdges: false, pixelSnap: true);
        }
    }

    private static WorldMapProgressManager FindProgressManager()
    {
        if (WorldMapProgressManager.Instance != null)
            return WorldMapProgressManager.Instance;
        return FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
    }

    private static SkillsManager FindSkillsManager()
    {
        if (SkillsManager.Instance != null)
            return SkillsManager.Instance;
        return FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
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

    private void OnProgressChanged()
    {
        RebuildRegionsDropdown();
        RebuildNodeList();
        RefreshDetails();
        RebuildGraph();
    }

    private bool IsRegionAvailable(RegionDefinition region, WorldMapProgressManager progress)
    {
        if (!region) return false;
        return region.IsRegionUnlocked(progress, ResolveActiveMapNodeIdForRegionUi(), worldMap);
    }

    private static string ResolveActiveMapNodeIdForRegionUi()
    {
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition.nodeId;
        if (ActiveLevelContext.Current != null)
            return ActiveLevelContext.Current.nodeId;
        return null;
    }
}

