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

    private RegionDefinition _selectedRegion;
    private MapNodeDefinition _selectedNode;
    private bool _selectionInitialized;

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
            bool greyOneShotDone = progress && node.IsPermanentlyCompleted(progress);
            bool unavailable = state == "Map locked" || state == "Skill locked";
            bool atThisMap = !string.IsNullOrWhiteSpace(activeNodeId) &&
                !string.IsNullOrWhiteSpace(node.nodeId) &&
                string.Equals(node.nodeId.Trim(), activeNodeId.Trim(), StringComparison.Ordinal);
            row.Bind(node, state, sel, OnNodeSelected, greyOneShotDone, unavailable, atThisMap);
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
                selectedNodeState.text = $"State: {n.GetUiStateLabel(progress, skills)}";
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

        bool hideEnter = n && progress && n.IsPermanentlyCompleted(progress);
        bool canEnter = n && n.CanEnter(progress, skills);
        if (enterNodeButton)
        {
            enterNodeButton.gameObject.SetActive(!hideEnter);
            if (!hideEnter)
                enterNodeButton.interactable = canEnter;
        }

        ApplyPanelThemeColors(n);
        PublishHudPreview();
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
        sb.Append(n.BuildRequirementsDisplayText());

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
}
