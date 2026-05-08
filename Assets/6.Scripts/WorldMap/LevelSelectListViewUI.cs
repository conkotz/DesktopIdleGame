using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// List-style Level Select view (Regions list + Locations list + Details).
/// Attach this to the old `LevelSelectPage` root.
/// </summary>
public class LevelSelectListViewUI : MonoBehaviour
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

    [Header("Regions (left)")]
    [SerializeField] private Transform regionListParent;
    [SerializeField] private GameObject regionRowPrefab;
    [SerializeField] private string regionSelectedChildName = "Selected";

    [Header("Locations (center)")]
    [SerializeField] private Transform nodeListParent;
    [SerializeField] private WorldMapNodeButtonUI nodeRowPrefab;

    [Header("Filters (optional)")]
    [SerializeField] private Button filterAllButton;
    [SerializeField] private Button filterCombatButton;
    [SerializeField] private Button filterGatheringButton;
    [SerializeField] private Button filterOtherButton;

    [Header("Details (right)")]
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

    [Header("Presentation swap")]
    [Tooltip("Optional. If assigned, this GameObject is hidden when world map view is shown.")]
    [SerializeField] private GameObject listPresentationRoot;
    [Tooltip("Optional. Full map page root to show when swapping to world map view.")]
    [SerializeField] private GameObject worldMapPresentationRoot;
    [SerializeField] private Button openWorldMapViewButton;
    [Tooltip("Optional extra roots to hide while world-map presentation is active (e.g. old list header bars).")]
    [SerializeField] private GameObject[] hideWhenWorldMapActive;

    [Header("Theme — panel backgrounds (optional)")]
    [SerializeField] private Image detailsPanelBackgroundImage;
    [SerializeField] private Image locationsPanelBackgroundImage;

    private readonly List<GameObject> _regionRows = new();
    private readonly List<RegionDefinition> _regionRowRegions = new();
    private readonly List<WorldMapNodeButtonUI> _nodeRows = new();
    private readonly List<GameObject> _nodeSectionRows = new();

    private RegionDefinition _selectedRegion;
    private MapNodeDefinition _selectedNode;
    private bool _selectionInitialized;
    private string _lastDetailsNodeId;
    private int _detailsLayoutStabilizeFrames;

    private WorldMapProgressManager _progressEventsTarget;
    private SkillsManager _skillsLevelEventsTarget;

    private void Awake()
    {
        if (enterNodeButton)
            enterNodeButton.onClick.AddListener(OnEnterNodeClicked);

        if (openWorldMapViewButton)
        {
            openWorldMapViewButton.onClick.RemoveAllListeners();
            openWorldMapViewButton.onClick.AddListener(OpenWorldMapView);
        }

        if (filterAllButton)
            filterAllButton.onClick.AddListener(() => OnNodeFilterClicked(LevelSelectSharedState.NodeListFilter.All));
        if (filterCombatButton)
            filterCombatButton.onClick.AddListener(() => OnNodeFilterClicked(LevelSelectSharedState.NodeListFilter.Combat));
        if (filterGatheringButton)
            filterGatheringButton.onClick.AddListener(() => OnNodeFilterClicked(LevelSelectSharedState.NodeListFilter.Gathering));
        if (filterOtherButton)
            filterOtherButton.onClick.AddListener(() => OnNodeFilterClicked(LevelSelectSharedState.NodeListFilter.Other));
    }

    private void OnDestroy()
    {
        if (enterNodeButton)
            enterNodeButton.onClick.RemoveListener(OnEnterNodeClicked);
        if (openWorldMapViewButton)
            openWorldMapViewButton.onClick.RemoveAllListeners();

        if (filterAllButton) filterAllButton.onClick.RemoveAllListeners();
        if (filterCombatButton) filterCombatButton.onClick.RemoveAllListeners();
        if (filterGatheringButton) filterGatheringButton.onClick.RemoveAllListeners();
        if (filterOtherButton) filterOtherButton.onClick.RemoveAllListeners();
    }

    private void OnEnable()
    {
        TrySubscribeProgressChanged();
        TrySubscribeSkillsLevelEvents();

        ResolveDefaults();

        // Bottom-bar reopen: if last used view was WorldMap, immediately open it.
        if (LevelSelectSharedState.LastPresentation == LevelSelectSharedState.Presentation.WorldMap &&
            worldMapPresentationRoot != null)
        {
            OpenWorldMapView();
            return;
        }

        SetExtraRootsWorldMapVisibility(false);
        if (listPresentationRoot) listPresentationRoot.SetActive(true);
        if (worldMapPresentationRoot) worldMapPresentationRoot.SetActive(false);

        ApplyOrRestoreSelection();
        RebuildRegionList();
        RebuildNodeList();
        RefreshDetails();
    }

    private void OnDisable()
    {
        UnsubscribeProgressChanged();
        UnsubscribeSkillsLevelEvents();
        LevelSelectSharedState.HudPreviewSelection = null;
        _detailsLayoutStabilizeFrames = 0;
        SetExtraRootsWorldMapVisibility(false);
    }

    private void LateUpdate()
    {
        if (_detailsLayoutStabilizeFrames <= 0)
            return;

        _detailsLayoutStabilizeFrames--;
        StabilizeDetailsLayoutPass();
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

        bool usedPending = TryConsumePendingFocus();
        if (usedPending)
            _selectionInitialized = true;
        else
        {
            bool synced = TrySelectActiveMapNode();
            if (!synced && !_selectionInitialized)
            {
                ApplyDefaultSelection();
                _selectionInitialized = true;
            }
            else if (synced)
                _selectionInitialized = true;
        }

        // Restore selection from shared ids if possible.
        string sharedRegionId = LevelSelectSharedState.Norm(LevelSelectSharedState.SelectedRegionId);
        if (!string.IsNullOrEmpty(sharedRegionId))
        {
            RegionDefinition r = worldMap.FindRegionById(sharedRegionId);
            if (r)
                _selectedRegion = r;
        }

        string sharedNodeId = LevelSelectSharedState.Norm(LevelSelectSharedState.SelectedNodeId);
        if (!string.IsNullOrEmpty(sharedNodeId) && _selectedRegion)
        {
            MapNodeDefinition n = _selectedRegion.FindNodeById(sharedNodeId);
            if (n)
                _selectedNode = n;
        }
    }

    private bool TryConsumePendingFocus()
    {
        string id = LevelSelectSharedState.Norm(LevelSelectSharedState.PendingFocusNodeId);
        if (string.IsNullOrEmpty(id) || !worldMap)
            return false;

        LevelSelectSharedState.PendingFocusNodeId = "";

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
        for (int i = 0; i < worldMap.regions.Count; i++)
        {
            RegionDefinition r = worldMap.regions[i];
            if (r && r.ShouldListInRegionPicker(progress, ResolveActiveMapNodeIdForRegionUi(), worldMap) && IsRegionAvailable(r, progress))
            {
                _selectedRegion = r;
                break;
            }
        }

        if (!_selectedRegion)
            _selectedRegion = worldMap.regions[0];

        if (_selectedRegion != null && _selectedRegion.nodes is { Count: > 0 })
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

            Button b = row.GetComponentInChildren<Button>(true);
            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            bool unlocked = IsRegionAvailable(region, progress);
            if (label)
                label.text = unlocked ? region.displayName : $"{region.displayName} (Locked)";

            RegionDefinition captured = region;
            if (b)
            {
                b.interactable = true;
                b.onClick.AddListener(() => OnRegionClicked(captured));
            }

            SetRegionRowSelected(row, unlocked, unlocked && region == _selectedRegion);
        }

        if (_regionRowRegions.Count == 0)
        {
            _selectedRegion = null;
            _selectedNode = null;
        }
        else if (_selectedRegion == null || !_regionRowRegions.Contains(_selectedRegion))
        {
            _selectedRegion = _regionRowRegions[0];
            _selectedNode = _selectedRegion.nodes is { Count: > 0 } ? _selectedRegion.nodes[0] : null;
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

    private void SetRegionRowSelected(GameObject row, bool unlocked, bool selected)
    {
        if (!row) return;

        Button b = row.GetComponentInChildren<Button>(true);
        ApplyRegionButtonTheme(b, unlocked);

        TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
        if (label)
            label.color = unlocked ? regionNameUnlockedColor : regionNameLockedColor;

        if (!string.IsNullOrEmpty(regionSelectedChildName))
        {
            Transform t = row.transform.Find(regionSelectedChildName);
            if (t)
                t.gameObject.SetActive(selected);
        }
    }

    private void ApplyRegionButtonTheme(Button button, bool unlocked)
    {
        if (!button)
            return;

        Color baseCol = unlocked ? regionUnlockedColor : regionLockedColor;
        ColorBlock cb = button.colors;
        cb.normalColor = baseCol;
        cb.highlightedColor = regionHoverColor;
        cb.selectedColor = regionHoverColor;
        cb.pressedColor = regionPressedColor;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        button.colors = cb;

        if (button.targetGraphic)
            button.targetGraphic.color = baseCol;
    }

    private void OnRegionClicked(RegionDefinition region)
    {
        WorldMapProgressManager progress = FindProgressManager();
        if (!IsRegionAvailable(region, progress))
        {
            LockedRegionClickFeedback.LogLockedRegionNotice(region);
            return;
        }

        if (_selectedRegion == region)
            return;

        _selectedRegion = region;
        _selectedNode = _selectedRegion.nodes is { Count: > 0 } ? _selectedRegion.nodes[0] : null;

        LevelSelectSharedState.SelectedRegionId = _selectedRegion ? _selectedRegion.regionId : "";
        LevelSelectSharedState.SelectedNodeId = _selectedNode ? _selectedNode.nodeId : "";

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
        _selectedNode = node;
        LevelSelectSharedState.SelectedNodeId = _selectedNode ? _selectedNode.nodeId : "";
        RefreshNodeSelectionVisuals();
        RefreshDetails();
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

    private void OnNodeFilterClicked(LevelSelectSharedState.NodeListFilter filter)
    {
        if (LevelSelectSharedState.Filter == filter)
            return;
        LevelSelectSharedState.Filter = filter;
        RebuildNodeList();
        RefreshDetails();
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
        if (string.IsNullOrWhiteSpace(gameplaySceneName))
            return;
        PlayerLevelTransition.LoadSceneWithEffectOrImmediate(gameplaySceneName);
    }

    private void OpenWorldMapView()
    {
        if (!worldMapPresentationRoot)
        {
            Debug.LogError("[LevelSelectListViewUI] World Map Presentation Root is not assigned.");
            return;
        }

        // Guard against assigning a prefab asset instead of the in-scene FullMapPage object.
        if (!worldMapPresentationRoot.scene.IsValid())
        {
            Debug.LogError("[LevelSelectListViewUI] World Map Presentation Root is not a scene object (likely a prefab asset). Assign the FullMapPage from the Hierarchy.");
            return;
        }

        LevelSelectSharedState.LastPresentation = LevelSelectSharedState.Presentation.WorldMap;
        LevelSelectSharedState.SetHideRoots(hideWhenWorldMapActive);
        worldMapPresentationRoot.SetActive(true);
        if (listPresentationRoot)
            listPresentationRoot.SetActive(false);
        SetExtraRootsWorldMapVisibility(true);
    }

    private void SetExtraRootsWorldMapVisibility(bool worldMapActive)
    {
        if (hideWhenWorldMapActive == null || hideWhenWorldMapActive.Length == 0)
            return;
        for (int i = 0; i < hideWhenWorldMapActive.Length; i++)
        {
            GameObject go = hideWhenWorldMapActive[i];
            if (!go) continue;
            go.SetActive(!worldMapActive);
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
        RebuildRegionList();
        RebuildNodeList();
        RefreshDetails();
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

