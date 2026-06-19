using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Spawns horizontal timeline nodes from <see cref="SkillDefinition"/> unlock data (display only).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class HorizontalSkillTreeScaffoldUI : MonoBehaviour
{
    private Coroutine _deferredConnectorRefresh;
    private RectTransform _unlockRow;
    private RectTransform _spineRow;
    private RectTransform _choiceRow;

    [Header("References")]
    [SerializeField] private RectTransform timelineContent;
    [SerializeField] private SkillTimelineNodeUI nodePrefab;
    [SerializeField] private SkillChoiceGroupUI choiceGroupPrefab;
    [SerializeField] private SkillTimelineScaffoldUI timelineScaffold;
    [SerializeField] private SkillNodeDetailsPanelUI detailsPanel;
    [SerializeField] private SkillsManager skillsManager;
    [SerializeField] private TMP_Text skillLevelText;

    [Header("Layout")]
    [Tooltip("Horizontal spacing per level when Timeline Scaffold is missing.")]
    [SerializeField] private float pixelsPerLevel = 90f;
    [SerializeField] private float timelineStartX = 120f;
    [Tooltip("TimelineContent Y for SpineRow. Pushed to Timeline Scaffold on each build when assigned.")]
    [SerializeField] private float spineY = 24f;
    [Tooltip("TimelineContent Y for ChoiceRow. Pushed to Timeline Scaffold on each build when assigned.")]
    [SerializeField] private float choiceRowY = -54f;

    [Header("Choice group node offsets (from spine baseline)")]
    [Tooltip("Tune vertical position per milestone type. Abilities/minors/unlocks use 0 unless changed.")]
    [SerializeField] private SkillTimelineChoiceGroupLayout choiceGroupLayout = SkillTimelineChoiceGroupLayout.Default;

    [Header("Skill selection (drives timeline + node details)")]
    [Tooltip("Primary skill tree to render. Assign directly, or leave empty and use Skill Type + Database.")]
    [SerializeField] private SkillDefinition selectedSkill;

    [Tooltip("Used when Selected Skill is empty and Skill Database is assigned.")]
    [SerializeField] private SkillType selectedSkillType = SkillType.Melee;

    [Tooltip("Resolves Selected Skill Type when Selected Skill is not assigned.")]
    [SerializeField] private SkillDatabase skillDatabase;

    [Tooltip("When true, changing Selected Skill / Type / Database rebuilds the timeline in the Editor.")]
    [SerializeField] private bool rebuildOnInspectorChange;

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [Tooltip("When true and no skill is selected, runs the hardcoded test timeline.")]
    [SerializeField] private bool useTestTimelineFallback = true;
    [Tooltip("Logs one build summary line. Per-level logs are never written.")]
    [SerializeField] private bool verboseBuildLogs;

    private SkillDefinition _builtSkill;
    private int _builtAtPlayerLevel = -1;
    private bool _isBuilding;
    private SkillTimelineNodeUI _detailsFocusedTimelineNode;
    private float? _scrollRestoreAfterLayout;
    private float? _scrollRestoreSessionTarget;
    private bool _scrollRestoreSessionActive;
    private CanvasGroup _scrollViewportCanvasGroup;
    private readonly List<SkillTimelineNodeUI> _spawnedTimelineNodes = new();
    private readonly HashSet<int> _pendingUnlockGlowLevels = new();
    private Transform _timelineCacheRoot;
    private readonly Dictionary<SkillType, TimelineSkillCacheEntry> _timelineCacheBySkill = new();
    private bool _skipNextConnectorBuild;

    private sealed class TimelineSkillCacheEntry
    {
        public SkillDefinition Skill;
        public int BuiltAtPlayerLevel;
        public readonly List<ParkedTimelineObject> Parked = new();
        public readonly List<GameObject> ParkedConnectors = new();
    }

    private sealed class ParkedTimelineObject
    {
        public GameObject Go;
        public RectTransform Row;
        public int SiblingIndex;
    }

    public event Action<int> UnlockGlowAcknowledgedByHover;

    public static bool AreAllSkillTimelinesPrewarmed { get; private set; }

    public static void MarkAllSkillTimelinesPrewarmed() => AreAllSkillTimelinesPrewarmed = true;

    public static void ResetSkillTimelinePrewarmState() => AreAllSkillTimelinesPrewarmed = false;

    /// <summary>Clears a cached timeline so the next build reflects fresh tree data (e.g. after Reset Tree).</summary>
    public void InvalidateTimelineCacheForSkill(SkillType skillType)
    {
        if (_timelineCacheBySkill.TryGetValue(skillType, out TimelineSkillCacheEntry entry))
        {
            for (int i = 0; i < entry.Parked.Count; i++)
            {
                if (entry.Parked[i]?.Go != null)
                    Destroy(entry.Parked[i].Go);
            }

            entry.Parked.Clear();
            DestroyParkedConnectors(entry);
            _timelineCacheBySkill.Remove(skillType);
        }

        if (_builtSkill != null && _builtSkill.skillType == skillType)
        {
            _builtSkill = null;
            _builtAtPlayerLevel = -1;
            ClearSpawnedContent();
        }
    }

    /// <summary>Inspector skill used by <see cref="BuildFromSelectedSkill"/>.</summary>
    public SkillDefinition SelectedSkill => selectedSkill;

    public SkillType SelectedSkillType => selectedSkillType;

    public SkillDatabase SkillDatabase => skillDatabase;

    private void Awake()
    {
        PreferRuntimeSkillsManager();
        EnsureDetailsPanelReference();
        EnsureSkillLevelTextReference();
    }

    private bool _pendingConnectorRefreshWhileInactive;

    private void OnEnable()
    {
        if (_pendingConnectorRefreshWhileInactive)
        {
            _pendingConnectorRefreshWhileInactive = false;
            RunConnectorRefreshImmediate(forceWhileInactive: true);
        }

        if (!generateOnStart || !Application.isPlaying)
            return;

        EnsureDetailsPanelReference();
        CacheRowContainers();

        if (HasSpawnedTimelineContent())
            BringSpineMinorNodesToFront();

        // SkillsAbilityPageNewUI drives open refresh and scroll restore; skip connector pass here (causes scroll jiggle).
        if (GetComponentInParent<SkillsAbilityPageNewUI>(true) != null)
            return;

        if (!BuildFromSelectedSkill() && useTestTimelineFallback && !HasSpawnedTimelineContent())
            GenerateTestTimeline();
        else
            QueueDeferredConnectorRefresh();
    }

    private void OnDisable()
    {
        if (_deferredConnectorRefresh != null)
        {
            StopCoroutine(_deferredConnectorRefresh);
            _deferredConnectorRefresh = null;
        }
    }

    /// <summary>Called by <see cref="SkillTimelineScaffoldUI"/> after spine/row chrome is rebuilt. Does not call <see cref="Build"/> (that would recurse).</summary>
    public void OnScaffoldRebuilt(RectTransform content)
    {
        if (content != null)
            timelineContent = content;

        CacheRowContainers();

        if (!HasSpawnedTimelineContent())
            return;

        RefreshMinorTickVisibility(_builtSkill);
        BringSpineMinorNodesToFront();
        QueueDeferredConnectorRefresh();
    }

    /// <summary>Rebuilds the horizontal timeline from unlock data on <paramref name="skill"/> (display only).</summary>
    public bool Build(SkillDefinition skill)
    {
        if (_isBuilding)
            return false;

        if (skill != null && skill == _builtSkill && HasSpawnedTimelineContent())
        {
            int playerLevel = ResolvePlayerSkillLevel(skill);
            if (playerLevel == _builtAtPlayerLevel)
                return RefreshBuiltTimeline(skill);
            return RefreshProgressIfSameSkill(skill, playerLevel);
        }

        if (skill != null && skill != _builtSkill && HasSpawnedTimelineContent())
            ParkCurrentTimelineInCache();

        if (skill != null && TryRestoreTimelineFromCache(skill))
            return true;

        if (timelineContent == null || nodePrefab == null)
        {
            Debug.LogWarning("[HorizontalSkillTreeScaffoldUI] timelineContent or nodePrefab is not assigned.", this);
            return false;
        }

        if (choiceGroupPrefab == null && skill != null && skill.unlocks != null && skill.unlocks.Count > 0)
        {
            Debug.LogWarning("[HorizontalSkillTreeScaffoldUI] choiceGroupPrefab is not assigned.", this);
            return false;
        }

        _isBuilding = true;
        try
        {
            float? savedScroll = CaptureTimelineScrollPosition();
            SkillTimelineNodeBinding restoreDetailsBinding = CaptureOpenDetailsBinding();
            EnsureTimelineReady();
            ClearSpawnedContent(dismissDetailsPanel: restoreDetailsBinding == null);
            RefreshSpineRowChrome(skill);
            if (skill == null || skill.unlocks == null || skill.unlocks.Count == 0)
            {
                _builtSkill = skill;
                _builtAtPlayerLevel = -1;
                return false;
            }

            int playerLevel = ResolvePlayerSkillLevel(skill);
            _builtSkill = skill;
            _builtAtPlayerLevel = playerLevel;
            List<HorizontalSkillTreeUnlockLayout.SortedUnlock> sorted =
                HorizontalSkillTreeUnlockLayout.BuildSortedUnlocks(skill.unlocks);
            List<HorizontalSkillTreeUnlockLayout.LevelGroup> levelGroups =
                HorizontalSkillTreeUnlockLayout.GroupByLevel(sorted);

            LogBuildHeader(skill, sorted.Count, levelGroups.Count, playerLevel);

            var spineSlotCounts = CountSlotsPerLevel(sorted, HorizontalSkillTreeUnlockLayout.IsSpineMinorType);
            var aboveSlotCounts = CountSlotsPerLevel(sorted, HorizontalSkillTreeUnlockLayout.IsAboveSpineType);

            for (int g = 0; g < levelGroups.Count; g++)
            {
                RenderLevelGroup(levelGroups[g], playerLevel, spineSlotCounts, aboveSlotCounts);
            }

            RefreshSkillLevelLabel(skill, playerLevel);
            RefreshSpineProgress(playerLevel);
            RefreshMinorTickVisibility(skill);
            RefreshRowSelectionVisuals();
            _scrollRestoreAfterLayout = savedScroll;
            RestoreTimelineScrollPosition(savedScroll);
            QueueDeferredConnectorRefresh();

            if (restoreDetailsBinding != null)
                RestoreOpenDetails(restoreDetailsBinding);

            return true;
        }
        finally
        {
            _isBuilding = false;
        }
    }

    /// <summary>Refreshes row pick / enhancement chrome without rebuilding the timeline.</summary>
    public void RefreshTimelineSelectionVisuals()
    {
        RefreshRowSelectionVisuals();
        QueueDeferredConnectorRefresh();
    }

    /// <summary>Pulses newly-unlocked nodes for a specific unlock level (clears when hovered).</summary>
    public void HighlightNewUnlocksAtLevel(int unlockLevel)
    {
        if (_spawnedTimelineNodes.Count == 0)
            return;

        _pendingUnlockGlowLevels.Add(unlockLevel);

        ApplyUnlockGlowAtLevelWithoutTracking(unlockLevel);
    }

    public void ClearPendingUnlockGlowLevel(int unlockLevel) => _pendingUnlockGlowLevels.Remove(unlockLevel);

    public void SetPendingUnlockGlowLevels(IEnumerable<int> unlockLevels)
    {
        _pendingUnlockGlowLevels.Clear();
        if (unlockLevels == null)
            return;

        foreach (int lvl in unlockLevels)
            _pendingUnlockGlowLevels.Add(lvl);
    }

    /// <summary>Updates level/spine/selection when <paramref name="skill"/> is already built (avoids destroy/instantiate hitch).</summary>
    private bool RefreshBuiltTimeline(SkillDefinition skill)
    {
        if (skill == null || !HasSpawnedTimelineContent())
            return false;

        int playerLevel = ResolvePlayerSkillLevel(skill);
        RefreshSkillLevelLabel(skill, playerLevel);
        RefreshSpineProgress(playerLevel);
        RefreshMinorTickVisibility(skill);
        RefreshRowSelectionVisuals();
        return true;
    }

    /// <summary>
    /// Updates lock/unlock visuals without destroying nodes (matches <see cref="SkillTreeViewUI.RefreshProgressIfSameSkill"/>).
    /// </summary>
    public bool RefreshProgressIfSameSkill(SkillDefinition skill, int currentSkillLevel)
    {
        if (skill == null || skill != _builtSkill || !HasSpawnedTimelineContent())
            return false;

        _builtAtPlayerLevel = currentSkillLevel;
        RefreshSkillLevelLabel(skill, currentSkillLevel);
        RefreshSpineProgress(currentSkillLevel);
        RefreshMinorTickVisibility(skill);

        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            SkillTimelineNodeUI node = _spawnedTimelineNodes[i];
            if (node?.Binding == null)
                continue;

            SkillTimelineNodeUI.SkillTimelineNodeState state =
                ResolveDisplayState(node.Binding.Level, currentSkillLevel);
            node.RefreshProgressState(state);
        }

        RefreshRowSelectionVisuals();
        return true;
    }

    /// <summary>During page open, defer scroll apply until layout is stable (see <see cref="EndScrollRestoreSession"/>).</summary>
    public void BeginScrollRestoreSession(float? normalizedHorizontal)
    {
        _scrollRestoreSessionTarget = normalizedHorizontal;
        _scrollRestoreSessionActive = true;
    }

    /// <summary>Applies the session scroll target once after layout, then clears the session.</summary>
    public void EndScrollRestoreSession()
    {
        _scrollRestoreSessionActive = false;
        float? target = _scrollRestoreSessionTarget;
        _scrollRestoreSessionTarget = null;
        _scrollRestoreAfterLayout = null;

        if (target.HasValue)
            ApplyScrollPositionImmediate(target.Value);
    }

    public void FlushPendingTimelineLayout()
    {
        if (_deferredConnectorRefresh != null)
        {
            StopCoroutine(_deferredConnectorRefresh);
            _deferredConnectorRefresh = null;
        }

        RunConnectorRefreshImmediate(forceWhileInactive: !isActiveAndEnabled);
        Canvas.ForceUpdateCanvases();
    }

    public IEnumerator CoWaitForPendingTimelineLayout()
    {
        while (_deferredConnectorRefresh != null)
            yield return null;

        yield return null;
        Canvas.ForceUpdateCanvases();
    }

    public void SetScrollViewportVisible(bool visible)
    {
        ScrollRect scroll = ResolveTimelineScrollRect();
        if (scroll?.viewport == null)
            return;

        if (_scrollViewportCanvasGroup == null)
        {
            _scrollViewportCanvasGroup = scroll.viewport.GetComponent<CanvasGroup>();
            if (_scrollViewportCanvasGroup == null)
                _scrollViewportCanvasGroup = scroll.viewport.gameObject.AddComponent<CanvasGroup>();
        }

        _scrollViewportCanvasGroup.alpha = visible ? 1f : 0f;
        _scrollViewportCanvasGroup.blocksRaycasts = visible;
        _scrollViewportCanvasGroup.interactable = visible;
    }

    /// <summary>Clears focused node and details (e.g. after Reset Tree).</summary>
    public void DismissOpenDetails()
    {
        _detailsFocusedTimelineNode = null;
        EnsureDetailsPanelReference();
        detailsPanel?.ShowEmpty();
        RefreshRowSelectionVisuals();
    }

    /// <summary>Re-applies the open details panel after choice/enhancement data changes.</summary>
    public void RefreshOpenDetailsAfterDataChange()
    {
        EnsureDetailsPanelReference();
        SkillTimelineNodeBinding binding = detailsPanel != null ? detailsPanel.CurrentBinding : null;
        if (binding == null)
            return;

        RestoreOpenDetails(binding);
    }

    /// <summary>Sets the inspector skill and rebuilds the timeline (display only).</summary>
    public void SetSelectedSkill(SkillDefinition skill)
    {
        selectedSkill = skill;
        if (skill != null)
            selectedSkillType = skill.skillType;
    }

    /// <summary>Rebuilds from inspector <see cref="selectedSkill"/> or <see cref="selectedSkillType"/> + <see cref="skillDatabase"/> only.</summary>
    public bool BuildFromSelectedSkill()
    {
        SkillDefinition skill = ResolveInspectorSkill();
        return Build(skill);
    }

    /// <summary>Uses the skill currently selected on <see cref="SkillsAbilityPageNewUI"/> (ignores inspector selection).</summary>
    public bool BuildFromCurrentSkill()
    {
        SkillsAbilityPageNewUI page = GetComponentInParent<SkillsAbilityPageNewUI>(true);
        if (page == null)
            page = FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);

        if (page == null || page.SelectedSkill == null)
            return false;

        return Build(page.SelectedSkill);
    }

    private SkillDefinition ResolveInspectorSkill()
    {
        if (selectedSkill != null)
            return selectedSkill;

        EnsureSkillDatabaseReference();
        if (skillDatabase != null)
            return skillDatabase.Get(selectedSkillType);

        return null;
    }

    private void EnsureSkillDatabaseReference()
    {
        if (skillDatabase != null)
            return;

        skillDatabase = SkillDatabase.LoadDefault();
    }

    [ContextMenu("Build From Selected Skill")]
    private void EditorBuildFromSelectedSkill() => BuildFromSelectedSkill();

    [ContextMenu("Build From Skills Page Tab")]
    private void EditorBuildFromCurrentSkill() => BuildFromCurrentSkill();

    [ContextMenu("Generate Test Timeline")]
    public void GenerateTestTimeline()
    {
        if (timelineContent == null)
        {
            Debug.LogWarning("[HorizontalSkillTreeScaffoldUI] timelineContent is not assigned.", this);
            return;
        }

        if (nodePrefab == null)
        {
            Debug.LogWarning("[HorizontalSkillTreeScaffoldUI] nodePrefab is not assigned.", this);
            return;
        }

        if (choiceGroupPrefab == null)
        {
            Debug.LogWarning("[HorizontalSkillTreeScaffoldUI] choiceGroupPrefab is not assigned.", this);
            return;
        }

        _builtSkill = null;
        EnsureTimelineReady();
        ClearSpawnedContent();
        RefreshSpineRowChrome();

        SpawnMinorPassive(1, 0, 1);
        SpawnMinorPassive(2, 0, 1);
        SpawnMinorPassive(3, 0, 1);
        SpawnMinorPassive(4, 0, 1);
        SpawnUnlock(1, 0, 1, "Beginner Melee Combat");
        SpawnChoiceGroup(5, new[] { "Power Slash", "Rend", "Envenom" }, SkillTimelineNodeUI.SkillTimelineNodeType.Ability);
        SpawnMinorPassive(8, 0, 1);
        SpawnUnlock(8, 0, 1, "Can Catch Trout");
        SpawnChoiceGroup(10, new[] { "Ailment Attunement", "Parry", "Blade Mastery" }, SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive);
        SpawnChoiceGroup(15, new[] { "Whirlwind", "Cleaving Strikes", "Crescent Slash", "Guardian's Hammer" }, SkillTimelineNodeUI.SkillTimelineNodeType.Ability);

        LogBuildHeader(null, 0, 0, 1);

        RefreshSpineProgress(1);
        RefreshMinorTickVisibility();
        BringSpineMinorNodesToFront();
        QueueDeferredConnectorRefresh();
    }

    [ContextMenu("Refresh Connector Lines")]
    public void RefreshConnectorsOnly()
    {
        if (timelineContent == null)
            return;

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        CacheRowContainers();
        BuildTimelineConnectors();
        RefreshRowSelectionVisuals();
    }

    private void QueueDeferredConnectorRefresh()
    {
        if (!isActiveAndEnabled)
        {
            _pendingConnectorRefreshWhileInactive = true;
            RunConnectorRefreshImmediate(forceWhileInactive: true);
            return;
        }

        if (_deferredConnectorRefresh != null)
            StopCoroutine(_deferredConnectorRefresh);

        _deferredConnectorRefresh = StartCoroutine(DeferredConnectorRefresh());
    }

    private IEnumerator DeferredConnectorRefresh()
    {
        yield return null;
        _deferredConnectorRefresh = null;

        if (!isActiveAndEnabled || timelineContent == null)
            yield break;

        RunConnectorRefreshImmediate();
    }

    private void RunConnectorRefreshImmediate(bool forceWhileInactive = false)
    {
        if (timelineContent == null)
            return;

        if (!forceWhileInactive && !isActiveAndEnabled)
            return;

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        CacheRowContainers();
        if (timelineScaffold == null || !HasSpawnedTimelineContent())
            return;

        if (_skipNextConnectorBuild)
        {
            _skipNextConnectorBuild = false;
            RefreshConnectorSelectionHighlights();
        }
        else
        {
            BuildTimelineConnectors();
        }

        RefreshMinorTickVisibility(_builtSkill);
        BringSpineMinorNodesToFront();
        RefreshRowSelectionVisuals();

        if (!_scrollRestoreSessionActive)
        {
            RestoreTimelineScrollPosition(_scrollRestoreAfterLayout);
            _scrollRestoreAfterLayout = null;
        }
    }

    [ContextMenu("Clear Spawned Nodes")]
    public void ClearSpawnedContent(bool dismissDetailsPanel = true)
    {
        _detailsFocusedTimelineNode = null;
        UnregisterAllTimelineNodes();
        CacheRowContainers();
        ClearRowSpawnedContent(_unlockRow);
        ClearRowSpawnedContent(_spineRow);
        ClearRowSpawnedContent(_choiceRow);
        if (dismissDetailsPanel)
            detailsPanel?.ShowEmpty();
    }

    private void EnsureTimelineCacheRoot()
    {
        if (_timelineCacheRoot != null)
            return;

        var rootGo = new GameObject("TimelineSkillCache", typeof(RectTransform));
        _timelineCacheRoot = rootGo.transform;
        _timelineCacheRoot.SetParent(transform, false);
        _timelineCacheRoot.gameObject.SetActive(false);
    }

    private TimelineSkillCacheEntry GetOrCreateTimelineCacheEntry(SkillType skillType)
    {
        if (!_timelineCacheBySkill.TryGetValue(skillType, out TimelineSkillCacheEntry entry))
        {
            entry = new TimelineSkillCacheEntry();
            _timelineCacheBySkill[skillType] = entry;
        }

        return entry;
    }

    private void ParkCurrentTimelineInCache()
    {
        if (_builtSkill == null || !HasSpawnedTimelineContent())
            return;

        EnsureTimelineCacheRoot();
        TimelineSkillCacheEntry entry = GetOrCreateTimelineCacheEntry(_builtSkill.skillType);
        entry.Skill = _builtSkill;
        entry.BuiltAtPlayerLevel = _builtAtPlayerLevel;

        for (int i = entry.Parked.Count - 1; i >= 0; i--)
        {
            if (entry.Parked[i]?.Go != null)
                Destroy(entry.Parked[i].Go);
        }

        entry.Parked.Clear();
        ParkRowSpawnedContent(_unlockRow, entry);
        ParkRowSpawnedContent(_spineRow, entry);
        ParkRowSpawnedContent(_choiceRow, entry);
        ParkConnectorLines(entry);
        UnregisterAllTimelineNodes();
    }

    private void ParkConnectorLines(TimelineSkillCacheEntry entry)
    {
        if (timelineContent == null || entry == null)
            return;

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        RectTransform connectors = timelineScaffold != null
            ? timelineScaffold.GetOrCreatePrefabConnectorsLayer(timelineContent)
            : null;
        if (connectors == null)
            return;

        DestroyParkedConnectors(entry);
        EnsureTimelineCacheRoot();

        for (int i = connectors.childCount - 1; i >= 0; i--)
        {
            Transform child = connectors.GetChild(i);
            if (child == null)
                continue;

            entry.ParkedConnectors.Add(child.gameObject);
            child.SetParent(_timelineCacheRoot, false);
        }
    }

    private static void DestroyParkedConnectors(TimelineSkillCacheEntry entry)
    {
        if (entry == null)
            return;

        for (int i = entry.ParkedConnectors.Count - 1; i >= 0; i--)
        {
            if (entry.ParkedConnectors[i] != null)
                Destroy(entry.ParkedConnectors[i]);
        }

        entry.ParkedConnectors.Clear();
    }

    private bool RestoreConnectorLines(TimelineSkillCacheEntry entry)
    {
        if (timelineContent == null || entry == null || entry.ParkedConnectors.Count == 0)
            return false;

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        RectTransform connectors = timelineScaffold != null
            ? timelineScaffold.GetOrCreatePrefabConnectorsLayer(timelineContent)
            : null;
        if (connectors == null)
            return false;

        SkillTimelineScaffoldUI.ClearConnectorChildren(connectors);

        for (int i = 0; i < entry.ParkedConnectors.Count; i++)
        {
            GameObject go = entry.ParkedConnectors[i];
            if (go == null)
                continue;

            go.transform.SetParent(connectors, false);
            go.transform.SetSiblingIndex(i);
        }

        entry.ParkedConnectors.Clear();
        return true;
    }

    private void ParkRowSpawnedContent(RectTransform row, TimelineSkillCacheEntry entry)
    {
        if (row == null || entry == null)
            return;

        for (int i = row.childCount - 1; i >= 0; i--)
        {
            Transform child = row.GetChild(i);
            if (child.GetComponent<SkillTimelineNodeUI>() == null &&
                child.GetComponent<SkillChoiceGroupUI>() == null)
                continue;

            entry.Parked.Add(new ParkedTimelineObject
            {
                Go = child.gameObject,
                Row = row,
                SiblingIndex = child.GetSiblingIndex()
            });
            child.SetParent(_timelineCacheRoot, false);
        }
    }

    private bool TryRestoreTimelineFromCache(SkillDefinition skill)
    {
        if (skill == null)
            return false;

        if (!_timelineCacheBySkill.TryGetValue(skill.skillType, out TimelineSkillCacheEntry entry) ||
            entry.Parked.Count == 0)
        {
            return false;
        }

        float? savedScroll = CaptureTimelineScrollPosition();
        SkillTimelineNodeBinding restoreDetailsBinding = CaptureOpenDetailsBinding();
        EnsureTimelineReady();
        CacheRowContainers();

        entry.Parked.Sort((a, b) =>
        {
            int rowOrder = a.Row.GetInstanceID().CompareTo(b.Row.GetInstanceID());
            return rowOrder != 0 ? rowOrder : a.SiblingIndex.CompareTo(b.SiblingIndex);
        });

        for (int i = 0; i < entry.Parked.Count; i++)
        {
            ParkedTimelineObject parked = entry.Parked[i];
            if (parked?.Go == null || parked.Row == null)
                continue;

            parked.Go.transform.SetParent(parked.Row, false);
            parked.Go.transform.SetSiblingIndex(Mathf.Min(parked.SiblingIndex, parked.Row.childCount - 1));
        }

        _builtSkill = skill;
        _builtAtPlayerLevel = entry.BuiltAtPlayerLevel;
        RegisterSpawnedNodesFromRows();

        int playerLevel = ResolvePlayerSkillLevel(skill);
        if (playerLevel != _builtAtPlayerLevel)
            RefreshProgressIfSameSkill(skill, playerLevel);
        else
            RefreshBuiltTimeline(skill);

        _skipNextConnectorBuild = playerLevel == entry.BuiltAtPlayerLevel && RestoreConnectorLines(entry);

        _scrollRestoreAfterLayout = savedScroll;
        RestoreTimelineScrollPosition(savedScroll);
        QueueDeferredConnectorRefresh();

        if (restoreDetailsBinding != null)
            RestoreOpenDetails(restoreDetailsBinding);

        if (!HasSpawnedTimelineContent())
        {
            InvalidateTimelineCacheForSkill(skill.skillType);
            return false;
        }

        return true;
    }

    private void RegisterSpawnedNodesFromRows()
    {
        UnregisterAllTimelineNodes();
        RegisterNodesInRow(_unlockRow);
        RegisterNodesInRow(_spineRow);
        RegisterNodesInRow(_choiceRow);
    }

    private void RegisterNodesInRow(RectTransform row)
    {
        if (row == null)
            return;

        for (int i = 0; i < row.childCount; i++)
        {
            Transform child = row.GetChild(i);
            SkillTimelineNodeUI node = child.GetComponent<SkillTimelineNodeUI>();
            if (node != null)
            {
                RegisterTimelineNode(node);
                continue;
            }

            SkillChoiceGroupUI group = child.GetComponent<SkillChoiceGroupUI>();
            if (group == null)
                continue;

            IReadOnlyList<SkillTimelineNodeUI> nodes = group.SpawnedNodes;
            if (nodes == null)
                continue;

            for (int n = 0; n < nodes.Count; n++)
                RegisterTimelineNode(nodes[n]);
        }
    }

    private void RenderLevelGroup(
        HorizontalSkillTreeUnlockLayout.LevelGroup group,
        int playerLevel,
        Dictionary<int, int> spineSlotCounts,
        Dictionary<int, int> aboveSlotCounts)
    {
        int level = group.Level;
        float milestoneX = GetLevelX(level);

        var abilityEntries = new List<HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry>();
        var majorEntries = new List<HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry>();
        var capstoneEntries = new List<HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry>();

        int spineMinorSlot = 0;
        int aboveSpineSlot = 0;

        for (int i = 0; i < group.Unlocks.Count; i++)
        {
            HorizontalSkillTreeUnlockLayout.SortedUnlock entry = group.Unlocks[i];
            SkillUnlockDefinition unlock = entry.Unlock;
            if (unlock == null)
                continue;

            SkillUnlockType type = unlock.unlockType;
            SkillTimelineNodeUI.SkillTimelineNodeState state = ResolveDisplayState(level, playerLevel);

            if (HorizontalSkillTreeUnlockLayout.IsSpineMinorType(type))
            {
                int count = spineSlotCounts.TryGetValue(level, out int c) ? c : 1;
                float x = HorizontalSkillTreeUnlockLayout.SlotAnchoredX(milestoneX, spineMinorSlot, count);
                spineMinorSlot++;
                SpawnMinorPassive(level, entry, x, state);
            }
            else if (HorizontalSkillTreeUnlockLayout.IsAboveSpineType(type))
            {
                int count = aboveSlotCounts.TryGetValue(level, out int c) ? c : 1;
                float x = HorizontalSkillTreeUnlockLayout.SlotAnchoredX(milestoneX, aboveSpineSlot, count);
                string title = SkillsAbilityPresentationResolver.ResolveTreeUnlockTitle(unlock);
                var labelPlacement = HorizontalSkillTreeUnlockLayout.ResolveUnlockLabelPlacement(aboveSpineSlot, count);
                aboveSpineSlot++;
                SpawnUnlock(level, entry, count, title, x, state, labelPlacement);
            }
        }

        HorizontalSkillTreeUnlockLayout.CollectBelowSpineMilestoneEntries(
            group.Unlocks, SkillUnlockType.Ability, abilityEntries);
        HorizontalSkillTreeUnlockLayout.CollectBelowSpineMilestoneEntries(
            group.Unlocks, SkillUnlockType.MajorPassive, majorEntries);
        HorizontalSkillTreeUnlockLayout.CollectBelowSpineMilestoneEntries(
            group.Unlocks, SkillUnlockType.CapstonePassive, capstoneEntries);

        SpawnBelowSpineForLevel(level, milestoneX, abilityEntries, SkillTimelineNodeUI.SkillTimelineNodeType.Ability, playerLevel);
        float majorX = abilityEntries.Count > 0 ? milestoneX + 52f : milestoneX;
        SpawnBelowSpineForLevel(level, majorX, majorEntries, SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive, playerLevel);
        SpawnBelowSpineForLevel(level, milestoneX, capstoneEntries, SkillTimelineNodeUI.SkillTimelineNodeType.Capstone, playerLevel, capstoneScale: true);
    }

    private void SpawnBelowSpineForLevel(
        int level,
        float anchorX,
        List<HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry> entries,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        int playerLevel,
        bool capstoneScale = false)
    {
        if (entries == null || entries.Count == 0)
            return;

        SkillTimelineNodeUI.SkillTimelineNodeState state = ResolveDisplayState(level, playerLevel);
        string[] labels = BuildDisplayLabels(entries);

        if (entries.Count >= SkillChoiceGroupUI.MinChoiceCount)
        {
            if (choiceGroupPrefab == null)
                return;

            SkillChoiceGroupUI group = Instantiate(choiceGroupPrefab, _choiceRow);
            group.name = $"ChoiceGroup_Lv{level}_{nodeType}";
            group.SetAfterConnectorLayoutRefresh(RefreshRowSelectionButtonsForGroup);
            if (capstoneScale)
                group.RectTransform.localScale = Vector3.one * 1.08f;

            group.Configure(
                level,
                anchorX,
                SpineYPos,
                ChoiceRowYPos,
                entries.ToArray(),
                labels,
                nodeType,
                nodePrefab,
                state,
                choiceGroupLayout.GetNodeOffsetY(nodeType));

            RegisterChoiceGroupNodes(group, entries, level, nodeType, state);
            RefreshRowSelectionButtonsForGroup(group);
            return;
        }

        HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry entry = entries[0];
        string label = labels != null && labels.Length > 0 ? labels[0] : "Node";
        SpawnBelowSpineNode(level, entry, 0, 1, label, anchorX, nodeType, state, capstoneScale);
    }

    private void EnsureTimelineReady()
    {
        if (timelineContent == null)
            return;

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        if (timelineScaffold != null)
        {
            timelineScaffold.ApplyRowLayout(spineY, choiceRowY);
            CacheRowContainers();
            bool rowsMissing = _unlockRow == null || _spineRow == null || _choiceRow == null;
            if (rowsMissing && !HasSpawnedTimelineContent())
                timelineScaffold.RebuildScaffold(notifyHorizontalTree: !_isBuilding);
            else
                timelineScaffold.PrepareContentForAbsoluteNodes(timelineContent);
        }
        else
        {
            PrepareContentForAbsoluteNodes(timelineContent);
        }

        CacheRowContainers();
    }

    private void RefreshSpineRowChrome(SkillDefinition skill = null)
    {
        CacheRowContainers();
        if (timelineScaffold != null && _spineRow != null)
        {
            HashSet<int> skipMinorTickLevels = CollectMinorPassiveSpineLevels(skill);
            timelineScaffold.RefreshSpineChrome(_spineRow, skipMinorTickLevels);
        }
    }

    private void CacheRowContainers()
    {
        if (timelineContent == null)
        {
            _unlockRow = _spineRow = _choiceRow = null;
            return;
        }

        if (timelineScaffold != null)
        {
            _unlockRow = timelineScaffold.GetUnlockRow(timelineContent);
            _spineRow = timelineScaffold.GetSpineRow(timelineContent);
            _choiceRow = timelineScaffold.GetChoiceRow(timelineContent);
            return;
        }

        _unlockRow = timelineContent.Find(SkillTimelineScaffoldUI.UnlockRowName) as RectTransform;
        _spineRow = timelineContent.Find(SkillTimelineScaffoldUI.SpineRowName) as RectTransform;
        _choiceRow = timelineContent.Find(SkillTimelineScaffoldUI.ChoiceRowName) as RectTransform;
    }

    private static void ClearRowSpawnedContent(RectTransform row)
    {
        if (row == null)
            return;

        for (int i = row.childCount - 1; i >= 0; i--)
        {
            Transform child = row.GetChild(i);
            if (child.GetComponent<SkillTimelineNodeUI>() == null &&
                child.GetComponent<SkillChoiceGroupUI>() == null)
                continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            else
#endif
                Destroy(child.gameObject);
        }
    }

    private void BuildTimelineConnectors()
    {
        if (timelineScaffold == null || timelineContent == null)
            return;

        RectTransform connectors = timelineScaffold.GetOrCreatePrefabConnectorsLayer(timelineContent);
        if (connectors == null)
        {
            Debug.LogWarning("[HorizontalSkillTreeScaffoldUI] PrefabConnectors layer missing.", this);
            return;
        }

        SkillTimelineScaffoldUI.ClearConnectorChildren(connectors);
        RefreshChoiceGroupLayouts();

        float spine = ResolveSpineConnectorY();
        ConnectChoiceGroupsToSpine(connectors, spine);
        ConnectSingleChoiceRowNodesToSpine(connectors, spine);
    }

    private void RefreshChoiceGroupLayouts()
    {
        if (_choiceRow == null)
            return;

        for (int i = 0; i < _choiceRow.childCount; i++)
        {
            if (!_choiceRow.GetChild(i).TryGetComponent(out SkillChoiceGroupUI group))
                continue;

            group.RefreshConnectorLayout();
        }
    }

    private void ConnectChoiceGroupsToSpine(RectTransform connectors, float spineY)
    {
        if (_choiceRow == null || timelineContent == null)
            return;

        for (int i = 0; i < _choiceRow.childCount; i++)
        {
            if (!_choiceRow.GetChild(i).TryGetComponent(out SkillChoiceGroupUI group))
                continue;

            if (!group.TryGetSpineConnectorPoints(timelineContent, spineY, out Vector2 spineAttach, out Vector2 branchAttach))
                continue;

            bool highlightSpineStem = TryGetCommittedChoiceSlotIndex(group, out _);
            timelineScaffold.DrawConnector(
                connectors,
                spineAttach,
                branchAttach,
                extendBeyondStart: 0f,
                extendBeyondEnd: highlightSpineStem ? SkillTimelineScaffoldUI.GoldSpineConnectorExtendPastBranch : 0f,
                useProgressColor: highlightSpineStem);
        }
    }

    private void ConnectSingleChoiceRowNodesToSpine(RectTransform connectors, float spineY)
    {
        if (_choiceRow == null || timelineContent == null)
            return;

        for (int i = 0; i < _choiceRow.childCount; i++)
        {
            Transform child = _choiceRow.GetChild(i);
            if (child.GetComponent<SkillChoiceGroupUI>() != null)
                continue;

            if (!child.TryGetComponent(out SkillTimelineNodeUI node))
                continue;

            if (!TryGetBelowSpineNodeConnectorPoints(child, spineY, out Vector2 spineAttach, out Vector2 nodeAttach))
                continue;

            bool highlight = node.Binding != null
                && skillsManager != null
                && SkillTimelineRowSelectionRules.IsNodeCommittedSelected(skillsManager, node.Binding);

            timelineScaffold.DrawConnector(
                connectors,
                spineAttach,
                nodeAttach,
                extendBeyondStart: SkillTimelineScaffoldUI.ConnectorSpineOverlap,
                extendBeyondEnd: highlight ? SkillTimelineScaffoldUI.GoldSpineConnectorExtendPastBranch : 0f,
                useProgressColor: highlight);
        }
    }

    private bool HasSpawnedTimelineContent()
    {
        return RowHasSpawnedContent(_unlockRow) ||
               RowHasSpawnedContent(_spineRow) ||
               RowHasSpawnedContent(_choiceRow);
    }

    private static bool RowHasSpawnedContent(RectTransform row)
    {
        if (row == null)
            return false;

        for (int i = 0; i < row.childCount; i++)
        {
            Transform child = row.GetChild(i);
            if (child.GetComponent<SkillTimelineNodeUI>() != null ||
                child.GetComponent<SkillChoiceGroupUI>() != null)
                return true;
        }

        return false;
    }

    private bool TryGetBelowSpineNodeConnectorPoints(
        Transform nodeTransform,
        float spineY,
        out Vector2 spineAttach,
        out Vector2 nodeAttach)
    {
        spineAttach = default;
        nodeAttach = default;
        if (timelineContent == null || nodeTransform == null)
            return false;

        Transform rootButton = nodeTransform.Find("RootButton");
        RectTransform measureRt = rootButton != null ? rootButton as RectTransform : nodeTransform as RectTransform;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(timelineContent, measureRt);
        nodeAttach = new Vector2(bounds.center.x, bounds.max.y);
        spineAttach = new Vector2(nodeAttach.x, spineY);
        return true;
    }

    private static void PrepareContentForAbsoluteNodes(RectTransform content)
    {
        if (content.TryGetComponent(out HorizontalLayoutGroup hlg))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(hlg);
            else
#endif
                Destroy(hlg);
        }

        if (content.TryGetComponent(out ContentSizeFitter csf))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(csf);
            else
#endif
                Destroy(csf);
        }

        content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        if (content.sizeDelta.x < 2000f)
            content.sizeDelta = new Vector2(5000f, Mathf.Max(content.sizeDelta.y, 300f));
    }

    public float GetLevelX(int level)
    {
        if (timelineScaffold != null)
            return timelineScaffold.GetTimelineLevelX(level);
        return timelineStartX + (level - 1) * pixelsPerLevel;
    }

    /// <summary>Scrolls the horizontal timeline so the given level is near the center of the viewport.</summary>
    public void ScrollToLevel(int level)
    {
        ScrollRect scroll = timelineScaffold != null
            ? timelineScaffold.GetComponentInChildren<ScrollRect>(true)
            : GetComponentInChildren<ScrollRect>(true);

        if (scroll == null || scroll.content == null)
            return;

        RectTransform viewport = scroll.viewport != null
            ? scroll.viewport
            : scroll.transform as RectTransform;
        if (viewport == null)
            return;

        float targetX = GetLevelX(Mathf.Max(1, level));
        float contentWidth = scroll.content.rect.width;
        float viewportWidth = viewport.rect.width;
        if (contentWidth <= viewportWidth + 1f)
            return;

        float scrollable = contentWidth - viewportWidth;
        float normalized = Mathf.Clamp01((targetX - viewportWidth * 0.5f) / scrollable);
        scroll.horizontalNormalizedPosition = normalized;
    }

    /// <summary>
    /// Scrolls to the ability tier, selects the matching timeline node, and opens the details panel.
    /// </summary>
    /// <summary>
    /// Scrolls to the unlock tier, selects the matching timeline node, and opens the details panel.
    /// </summary>
    public bool TryFocusUnlock(SkillDefinition skill, SkillUnlockDefinition unlock, int level)
    {
        if (unlock == null)
            return false;

        EnsureDetailsPanelReference();
        int scrollLevel = Mathf.Max(1, level > 0 ? level : unlock.requiredLevel);
        ScrollToLevel(scrollLevel);

        SkillTimelineNodeUI node = FindTimelineNodeForUnlock(unlock);
        if (node == null)
            return false;

        HandleTimelineNodeClicked(node);
        return true;
    }

    public bool TryFocusAbility(AbilityDefinition ability)
    {
        if (ability == null)
            return false;

        EnsureDetailsPanelReference();
        int scrollLevel = Mathf.Max(1, ability.unlockLevel);
        SkillTimelineNodeUI node = FindTimelineNodeForAbility(ability);
        if (node?.Binding != null)
            scrollLevel = Mathf.Max(1, node.Binding.Level);

        ScrollToLevel(scrollLevel);
        node = FindTimelineNodeForAbility(ability);
        if (node == null)
            return false;

        HandleTimelineNodeClicked(node);
        return true;
    }

    private SkillTimelineNodeUI FindTimelineNodeForUnlock(SkillUnlockDefinition unlock)
    {
        if (unlock == null || _spawnedTimelineNodes.Count == 0)
            return null;

        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            SkillTimelineNodeUI node = _spawnedTimelineNodes[i];
            if (node?.Binding?.Unlock == unlock)
                return node;
        }

        return null;
    }

    private SkillTimelineNodeUI FindTimelineNodeForAbility(AbilityDefinition ability)
    {
        if (ability == null || _spawnedTimelineNodes.Count == 0)
            return null;

        string abilityId = ability.abilityId;
        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            SkillTimelineNodeUI node = _spawnedTimelineNodes[i];
            if (node?.Binding == null)
                continue;

            SkillUnlockDefinition unlock = node.Binding.Unlock;
            if (unlock?.ability == ability)
                return node;

            if (unlock?.ability != null
                && !string.IsNullOrEmpty(abilityId)
                && unlock.ability.abilityId == abilityId)
                return node;

            SkillChoiceDefinition choice = node.Binding.Choice;
            if (choice?.ability == ability)
                return node;

            if (choice?.ability != null
                && !string.IsNullOrEmpty(abilityId)
                && choice.ability.abilityId == abilityId)
                return node;
        }

        return null;
    }

    private float SpineYPos => spineY;

    private float ChoiceRowYPos => choiceRowY;

    private float ResolveSpineConnectorY()
    {
        if (timelineScaffold != null && timelineContent != null)
            return timelineScaffold.ResolveSpineLineYInContent(timelineContent);

        if (timelineScaffold != null)
            return timelineScaffold.TimelineSpineY;

        return spineY;
    }

    private int ResolvePlayerSkillLevel(SkillDefinition skill)
    {
        if (skill == null)
            return 1;

        if (skillsManager == null)
            skillsManager = SkillsManager.Instance != null
                ? SkillsManager.Instance
                : FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);

        return skillsManager != null ? Mathf.Max(1, skillsManager.GetLevel(skill.skillType)) : 1;
    }

    private void EnsureSkillLevelTextReference()
    {
        if (skillLevelText != null)
            return;

        skillLevelText = transform.Find("SkillLevelText")?.GetComponent<TMP_Text>();
    }

    private void RefreshSkillLevelLabel(SkillDefinition skill, int playerLevel)
    {
        EnsureSkillLevelTextReference();
        if (skillLevelText == null)
            return;

        if (skill == null)
        {
            skillLevelText.text = "No Skill Selected";
            return;
        }

        string displayName = SkillsAbilityPresentationResolver.ResolveSkillDisplayName(skill);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = skill.skillType.ToString();

        skillLevelText.text = $"{displayName}: Lv {playerLevel}";
    }

    private void RefreshSpineProgress(int playerLevel)
    {
        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        timelineScaffold?.UpdateSpineProgress(playerLevel);
        BringSpineMinorNodesToFront();
    }

    /// <summary>Keeps spine minor gems above spine progress and minor level ticks.</summary>
    private void BringSpineMinorNodesToFront()
    {
        if (_spineRow == null && timelineContent != null)
            CacheRowContainers();
        if (_spineRow == null)
            return;

        SkillTimelineScaffoldUI.ApplySpineRowDrawOrder(_spineRow);
    }

    /// <summary>Hides level ticks on non-milestone levels that have a spine minor node.</summary>
    private void RefreshMinorTickVisibility(SkillDefinition skill = null)
    {
        if (_spineRow == null)
            CacheRowContainers();
        if (_spineRow == null)
            return;

        HashSet<int> levelsWithMinors = CollectMinorPassiveSpineLevels(skill);
        if (levelsWithMinors.Count == 0)
            return;

        SkillTimelineScaffoldUI.HideMinorTicksForLevels(_spineRow, levelsWithMinors);
    }

    private HashSet<int> CollectMinorPassiveSpineLevels(SkillDefinition skill = null)
    {
        skill ??= _builtSkill;
        var levelsWithMinors = new HashSet<int>();

        if (skill?.unlocks != null)
        {
            for (int i = 0; i < skill.unlocks.Count; i++)
            {
                SkillUnlockDefinition unlock = skill.unlocks[i];
                if (unlock == null || unlock.unlockType != SkillUnlockType.MinorPassive)
                    continue;

                int level = unlock.requiredLevel;
                if (SkillTimelineScaffoldUI.IsLabeledMilestoneLevel(level))
                    continue;

                levelsWithMinors.Add(level);
            }
        }

        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
            TryAddMinorPassiveLevel(levelsWithMinors, _spawnedTimelineNodes[i]);

        if (_spineRow != null)
        {
            for (int i = 0; i < _spineRow.childCount; i++)
            {
                if (!_spineRow.GetChild(i).TryGetComponent(out SkillTimelineNodeUI node))
                    continue;

                TryAddMinorPassiveLevel(levelsWithMinors, node);
            }
        }

        return levelsWithMinors;
    }

    private static void TryAddMinorPassiveLevel(HashSet<int> levelsWithMinors, SkillTimelineNodeUI node)
    {
        if (levelsWithMinors == null || node == null)
            return;

        SkillTimelineNodeBinding binding = node.Binding;
        if (binding != null)
        {
            if (binding.TimelineNodeType != SkillTimelineNodeUI.SkillTimelineNodeType.MinorPassive)
                return;

            if (SkillTimelineScaffoldUI.IsLabeledMilestoneLevel(binding.Level))
                return;

            levelsWithMinors.Add(binding.Level);
            return;
        }

        if (!node.name.StartsWith("Minor_Lv", System.StringComparison.Ordinal))
            return;

        int underscore = node.name.IndexOf('_', "Minor_Lv".Length);
        string levelText = underscore > 0
            ? node.name.Substring("Minor_Lv".Length, underscore - "Minor_Lv".Length)
            : node.name.Substring("Minor_Lv".Length);

        if (!int.TryParse(levelText, out int level))
            return;

        if (SkillTimelineScaffoldUI.IsLabeledMilestoneLevel(level))
            return;

        levelsWithMinors.Add(level);
    }

    private static SkillTimelineNodeUI.SkillTimelineNodeState ResolveDisplayState(int unlockLevel, int playerLevel) =>
        unlockLevel <= playerLevel
            ? SkillTimelineNodeUI.SkillTimelineNodeState.Available
            : SkillTimelineNodeUI.SkillTimelineNodeState.Locked;

    private static Dictionary<int, int> CountSlotsPerLevel(
        List<HorizontalSkillTreeUnlockLayout.SortedUnlock> sorted,
        System.Func<SkillUnlockType, bool> filter)
    {
        var counts = new Dictionary<int, int>();
        if (sorted == null)
            return counts;

        for (int i = 0; i < sorted.Count; i++)
        {
            SkillUnlockDefinition u = sorted[i].Unlock;
            if (u == null || !filter(u.unlockType))
                continue;

            int lvl = sorted[i].Level;
            counts.TryGetValue(lvl, out int c);
            counts[lvl] = c + 1;
        }

        return counts;
    }

    private void LogBuildHeader(SkillDefinition skill, int unlockCount, int levelCount, int playerLevel)
    {
        if (!verboseBuildLogs)
            return;

        string skillName = skill != null
            ? SkillsAbilityPresentationResolver.ResolveSkillDisplayName(skill)
            : "(test)";
        if (string.IsNullOrWhiteSpace(skillName) && skill != null)
            skillName = skill.skillType.ToString();

        Debug.Log(
            $"[HorizontalSkillTreeScaffoldUI] Build: skill='{skillName}' unlocks={unlockCount} levels={levelCount} playerLv={playerLevel}",
            this);
    }

    private SkillTimelineNodeUI SpawnMinorPassive(
        int level,
        HorizontalSkillTreeUnlockLayout.SortedUnlock entry,
        float x,
        SkillTimelineNodeUI.SkillTimelineNodeState state)
    {
        return SpawnNodeInRow(_spineRow, new Vector2(x, SkillTimelineScaffoldUI.SpineMinorNodeLocalY), $"Minor_Lv{level}_{entry.SlotAtLevel}", node =>
        {
            node.ApplySpineDiamondPreview(state);
            BindTimelineNode(node, entry.Unlock, null, -1, level, entry.SlotAtLevel,
                SkillTimelineNodeUI.SkillTimelineNodeType.MinorPassive, state);
        });
    }

    private void SpawnMinorPassive(int level, int slot, int slotCount) =>
        SpawnMinorPassive(
            level,
            new HorizontalSkillTreeUnlockLayout.SortedUnlock(null, -1, level, slot),
            HorizontalSkillTreeUnlockLayout.SlotAnchoredX(GetLevelX(level), slot, slotCount),
            SkillTimelineNodeUI.SkillTimelineNodeState.Available);

    private void SpawnUnlock(
        int level,
        HorizontalSkillTreeUnlockLayout.SortedUnlock entry,
        int slotCount,
        string label,
        float x,
        SkillTimelineNodeUI.SkillTimelineNodeState state,
        HorizontalSkillTreeUnlockLayout.UnlockLabelPlacement labelPlacement =
            HorizontalSkillTreeUnlockLayout.UnlockLabelPlacement.CenterAbove)
    {
        SpawnNodeInRow(_unlockRow, new Vector2(x, -4f), $"Unlock_Lv{level}_{entry.SlotAtLevel}", node =>
        {
            node.ApplyUnlockTimelinePreview(label, state, labelPlacement);
            BindTimelineNode(node, entry.Unlock, null, -1, level, entry.SlotAtLevel,
                SkillTimelineNodeUI.SkillTimelineNodeType.Unlock, state);
        });
    }

    private void SpawnUnlock(int level, int slot, int slotCount, string label) =>
        SpawnUnlock(
            level,
            new HorizontalSkillTreeUnlockLayout.SortedUnlock(null, -1, level, slot),
            slotCount,
            label,
            HorizontalSkillTreeUnlockLayout.SlotAnchoredX(GetLevelX(level), slot, slotCount),
            SkillTimelineNodeUI.SkillTimelineNodeState.Available);

    public SkillChoiceGroupUI SpawnChoiceGroup(
        int level,
        string[] nodeNames,
        SkillTimelineNodeUI.SkillTimelineNodeType type)
    {
        if (choiceGroupPrefab == null || _choiceRow == null || nodeNames == null || nodeNames.Length == 0)
            return null;

        SkillChoiceGroupUI group = Instantiate(choiceGroupPrefab, _choiceRow);
        group.name = $"ChoiceGroup_Lv{level}";
        group.Configure(
            level,
            GetLevelX(level),
            SpineYPos,
            ChoiceRowYPos,
            nodeNames,
            type,
            nodePrefab,
            SkillTimelineNodeUI.SkillTimelineNodeState.Available,
            choiceGroupLayout.GetNodeOffsetY(type));
        return group;
    }

    private SkillTimelineNodeUI SpawnBelowSpineNode(
        int level,
        HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry entry,
        int slot,
        int slotCount,
        string label,
        float x,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillTimelineNodeUI.SkillTimelineNodeState state,
        bool capstoneScale)
    {
        float nodeY = choiceGroupLayout.GetNodeOffsetY(nodeType);
        return SpawnNodeInRow(_choiceRow, new Vector2(x, nodeY), $"ChoiceNode_Lv{level}_{slot}", node =>
        {
            node.ApplyBelowSpineNodePreview(nodeType, label, state, capstoneScale);
            BindTimelineNode(
                node,
                entry.Unlock,
                entry.Choice,
                entry.ChoiceAssetIndex,
                level,
                slot,
                nodeType,
                state);
        });
    }

    private SkillTimelineNodeUI SpawnNodeInRow(
        RectTransform row,
        Vector2 localPosition,
        string objectName,
        System.Action<SkillTimelineNodeUI> apply)
    {
        if (row == null || nodePrefab == null)
            return null;

        SkillTimelineNodeUI instance = Instantiate(nodePrefab, row);
        instance.name = objectName;

        RectTransform rt = instance.RectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPosition;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        apply?.Invoke(instance);
        RegisterTimelineNode(instance);
        return instance;
    }

    private static string[] BuildDisplayLabels(List<HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry> entries)
    {
        if (entries == null)
            return Array.Empty<string>();

        var labels = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry entry = entries[i];
            if (entry.Choice != null)
            {
                string title = SkillsAbilityPresentationResolver.ResolveChoiceTitle(entry.Choice);
                labels[i] = !string.IsNullOrWhiteSpace(title) ? title : "Node";
            }
            else if (entry.Unlock != null)
            {
                string title = SkillsAbilityPresentationResolver.ResolveTreeUnlockTitle(entry.Unlock);
                labels[i] = !string.IsNullOrWhiteSpace(title) ? title : "Node";
            }
            else
            {
                labels[i] = "Node";
            }
        }

        return labels;
    }

    private void RegisterChoiceGroupNodes(
        SkillChoiceGroupUI group,
        List<HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry> entries,
        int level,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillTimelineNodeUI.SkillTimelineNodeState state)
    {
        if (group == null)
            return;

        IReadOnlyList<SkillTimelineNodeUI> nodes = group.SpawnedNodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            SkillTimelineNodeUI node = nodes[i];
            if (entries != null && i < entries.Count)
            {
                HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry entry = entries[i];
                BindTimelineNode(
                    node,
                    entry.Unlock,
                    entry.Choice,
                    entry.ChoiceAssetIndex,
                    level,
                    i,
                    nodeType,
                    state);
            }

            RegisterTimelineNode(node);
        }
    }

    private void BindTimelineNode(
        SkillTimelineNodeUI node,
        SkillUnlockDefinition unlock,
        SkillChoiceDefinition choice,
        int choiceAssetIndex,
        int level,
        int slotAtLevel,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillTimelineNodeUI.SkillTimelineNodeState state)
    {
        if (node == null || unlock == null || _builtSkill == null)
            return;

        node.Bind(new SkillTimelineNodeBinding
        {
            Skill = _builtSkill,
            Unlock = unlock,
            Choice = choice,
            ChoiceAssetIndex = choiceAssetIndex,
            Level = level,
            SlotAtLevel = slotAtLevel,
            TimelineNodeType = nodeType,
            DisplayState = state
        });
    }

    private void RegisterTimelineNode(SkillTimelineNodeUI node)
    {
        if (node == null || _spawnedTimelineNodes.Contains(node))
            return;

        node.Clicked -= HandleTimelineNodeClicked;
        node.Clicked += HandleTimelineNodeClicked;
        node.RightClicked -= HandleTimelineNodeRightClicked;
        node.RightClicked += HandleTimelineNodeRightClicked;
        node.Hovered -= HandleTimelineNodeHovered;
        node.Hovered += HandleTimelineNodeHovered;
        _spawnedTimelineNodes.Add(node);
    }

    private void UnregisterAllTimelineNodes()
    {
        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            if (_spawnedTimelineNodes[i] != null)
            {
                _spawnedTimelineNodes[i].Clicked -= HandleTimelineNodeClicked;
                _spawnedTimelineNodes[i].RightClicked -= HandleTimelineNodeRightClicked;
                _spawnedTimelineNodes[i].Hovered -= HandleTimelineNodeHovered;
            }
        }

        _spawnedTimelineNodes.Clear();
    }

    private void HandleTimelineNodeHovered(SkillTimelineNodeUI node)
    {
        if (node?.Binding == null)
            return;

        int unlockLevel = node.Binding.Level;
        if (_pendingUnlockGlowLevels.Remove(unlockLevel))
            UnlockGlowAcknowledgedByHover?.Invoke(unlockLevel);
    }

    private void ApplyUnlockGlowAtLevelWithoutTracking(int unlockLevel)
    {
        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            SkillTimelineNodeUI node = _spawnedTimelineNodes[i];
            if (node == null || node.Binding == null)
                continue;
            if (node.Binding.Level != unlockLevel)
                continue;
            if (node.Binding.DisplayState == SkillTimelineNodeUI.SkillTimelineNodeState.Locked)
                continue;
            if (!node.gameObject.activeInHierarchy)
                continue;

            node.ShowUnlockGlow();
        }
    }

    private void HandleTimelineNodeClicked(SkillTimelineNodeUI node)
    {
        EnsureDetailsPanelReference();
        if (detailsPanel == null || node == null)
            return;

        float? savedScroll = CaptureTimelineScrollPosition();
        _detailsFocusedTimelineNode = node;
        detailsPanel.Show(node.Binding);
        RestoreTimelineScrollPosition(savedScroll);
        RefreshRowSelectionButtons();
    }

    private void HandleTimelineNodeRightClicked(SkillTimelineNodeUI node, PointerEventData eventData)
    {
        if (node == null)
            return;

        var entries = SkillTreeContextMenuBuilder.BuildForTimelineNode(
            node,
            () => HandleTimelineNodeClicked(node),
            () => CommitTimelineNodeSelection(node));

        if (entries.Count == 0)
            return;

        string header = SkillTreeContextMenuBuilder.ResolveHeaderTitle(node.Binding);
        ContextMenuUI.EnsureInstance().ShowAtScreen(
            entries,
            eventData != null ? eventData.position : (Vector2?)null,
            header);
    }

    private void HandleSelectNodeClicked(SkillTimelineNodeUI node) => CommitTimelineNodeSelection(node);

    private void HandleChangeNodeClicked(SkillTimelineNodeUI node) => CommitTimelineNodeSelection(node);

    private void CommitTimelineNodeSelection(SkillTimelineNodeUI node)
    {
        if (node?.Binding == null)
            return;

        PreferRuntimeSkillsManager();
        if (skillsManager == null)
            return;

        if (node.Binding.DisplayState == SkillTimelineNodeUI.SkillTimelineNodeState.Locked)
            return;

        float? savedScroll = CaptureTimelineScrollPosition();
        SkillTimelineRowSelectionRules.CommitSelection(skillsManager, node.Binding);
        SaveManager.Instance?.Save();
        RefreshRowSelectionVisuals();

        EnsureDetailsPanelReference();
        _detailsFocusedTimelineNode = node;
        if (detailsPanel != null)
            detailsPanel.Show(node.Binding);

        RestoreTimelineScrollPosition(savedScroll);
        RefreshRowSelectionButtons();
    }

    private void RefreshRowSelectionVisuals()
    {
        PreferRuntimeSkillsManager();
        if (_builtSkill == null || skillsManager == null)
        {
            for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
            {
                SkillTimelineNodeUI node = _spawnedTimelineNodes[i];
                if (node == null)
                    continue;
                node.ApplyRowPickSelectionVisual(false);
                node.SetNotSelectedPrompt(false);
                node.ConfigureRowSelectionButtons(false, false, false, null, null);
            }

            return;
        }

        SuppressSelectionChromeOnNonPickNodes();
        RefreshChoiceGroupRowVisuals();
        RefreshStandaloneChoiceRowNodes();
        RefreshRowSelectionButtons();
        RefreshConnectorSelectionHighlights();
        BringSpineMinorNodesToFront();
    }

    private void RefreshConnectorSelectionHighlights()
    {
        if (_choiceRow == null)
            return;

        for (int i = 0; i < _choiceRow.childCount; i++)
        {
            if (!_choiceRow.GetChild(i).TryGetComponent(out SkillChoiceGroupUI group))
                continue;

            TryGetCommittedChoiceSlotIndex(group, out int slotIndex);
            group.SetConnectorSelectionHighlight(slotIndex);
        }
    }

    private void RefreshSpineChoiceGroupConnectors()
    {
        if (timelineScaffold == null || timelineContent == null || _choiceRow == null)
            return;

        RectTransform connectors = timelineScaffold.GetOrCreatePrefabConnectorsLayer(timelineContent);
        if (connectors == null)
            return;

        float spineY = ResolveSpineConnectorY();
        SkillTimelineScaffoldUI.ClearConnectorChildren(connectors);
        ConnectChoiceGroupsToSpine(connectors, spineY);
        ConnectSingleChoiceRowNodesToSpine(connectors, spineY);
    }

    private bool TryGetCommittedChoiceSlotIndex(SkillChoiceGroupUI group, out int slotIndex)
    {
        slotIndex = -1;
        if (group == null || skillsManager == null)
            return false;

        IReadOnlyList<SkillTimelineNodeUI> nodes = group.SpawnedNodes;
        if (nodes == null)
            return false;

        for (int i = 0; i < nodes.Count; i++)
        {
            SkillTimelineNodeUI node = nodes[i];
            SkillTimelineNodeBinding binding = node != null ? node.Binding : null;
            if (binding == null)
                continue;

            if (!SkillTimelineRowSelectionRules.IsNodeCommittedSelected(skillsManager, binding))
                continue;

            slotIndex = i;
            return true;
        }

        return false;
    }

    /// <summary>Forces Select/Selected/Change hidden on unlock, minor, and capstone nodes.</summary>
    private void SuppressSelectionChromeOnNonPickNodes()
    {
        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            SkillTimelineNodeUI node = _spawnedTimelineNodes[i];
            if (node == null)
                continue;

            SkillTimelineNodeBinding binding = node.Binding;
            if (binding != null && SkillTimelineNodeUI.UsesRowSelectionButtons(binding.TimelineNodeType))
                continue;

            node.ConfigureRowSelectionButtons(false, false, false, null, null);
        }
    }

    private void RefreshRowSelectionButtons()
    {
        PreferRuntimeSkillsManager();

        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            SkillTimelineNodeUI node = _spawnedTimelineNodes[i];
            if (node == null)
                continue;

            node.ConfigureRowSelectionButtons(false, false, false, null, null);
        }

        if (_choiceRow == null || skillsManager == null)
            return;

        for (int g = 0; g < _choiceRow.childCount; g++)
        {
            if (!_choiceRow.GetChild(g).TryGetComponent(out SkillChoiceGroupUI group))
                continue;

            IReadOnlyList<SkillTimelineNodeUI> nodes = group.SpawnedNodes;
            if (nodes == null || nodes.Count == 0)
                continue;

            var bindings = new SkillTimelineNodeBinding[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                bindings[i] = nodes[i] != null ? nodes[i].Binding : null;

            bool rowHasCommittedPick = RowHasCommittedRowPick(bindings);
            for (int i = 0; i < nodes.Count; i++)
                ApplyRowSelectionButtonsForNode(nodes[i], bindings, rowHasCommittedPick);
        }

        for (int i = 0; i < _choiceRow.childCount; i++)
        {
            Transform child = _choiceRow.GetChild(i);
            if (child.GetComponent<SkillChoiceGroupUI>() != null)
                continue;

            if (!child.TryGetComponent(out SkillTimelineNodeUI node))
                continue;

            SkillTimelineNodeBinding binding = node.Binding;
            if (binding == null)
                continue;

            var bindings = new[] { binding };
            ApplyRowSelectionButtonsForNode(node, bindings, RowHasCommittedRowPick(bindings));
        }
    }

    /// <summary>Called after <see cref="SkillChoiceGroupUI"/> finishes connector layout so Select sits above lines.</summary>
    public void RefreshRowSelectionButtonsForGroup(SkillChoiceGroupUI group)
    {
        PreferRuntimeSkillsManager();

        if (group == null || skillsManager == null)
            return;

        IReadOnlyList<SkillTimelineNodeUI> nodes = group.SpawnedNodes;
        if (nodes == null || nodes.Count == 0)
            return;

        var bindings = new SkillTimelineNodeBinding[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
            bindings[i] = nodes[i] != null ? nodes[i].Binding : null;

        bool rowHasCommittedPick = RowHasCommittedRowPick(bindings);
        for (int i = 0; i < nodes.Count; i++)
        {
            SkillTimelineNodeUI node = nodes[i];
            ApplyRowSelectionButtonsForNode(node, bindings, rowHasCommittedPick);
            if (node != null && SupportsRowSelectionChrome(node.Binding))
                node.RectTransform.SetAsLastSibling();
        }
    }

    private void ApplyRowSelectionButtonsForNode(
        SkillTimelineNodeUI node,
        SkillTimelineNodeBinding[] groupBindings,
        bool rowHasCommittedPick)
    {
        if (node?.Binding == null)
            return;

        SkillTimelineNodeBinding binding = node.Binding;
        if (!SupportsRowSelectionChrome(binding))
        {
            node.ConfigureRowSelectionButtons(false, false, false, null, null);
            return;
        }

        bool locked = binding.DisplayState == SkillTimelineNodeUI.SkillTimelineNodeState.Locked;
        bool committed = SkillTimelineRowSelectionRules.IsNodeCommittedSelected(skillsManager, binding);
        bool showSelect = !rowHasCommittedPick && !locked;
        bool showSelected = committed;
        bool showChange = rowHasCommittedPick
            && node == _detailsFocusedTimelineNode
            && !committed
            && !locked;

        node.ConfigureRowSelectionButtons(
            showSelect,
            showSelected,
            showChange,
            HandleSelectNodeClicked,
            HandleChangeNodeClicked);
    }

    private static bool SupportsRowSelectionChrome(SkillTimelineNodeBinding binding)
    {
        if (binding == null || !SkillTimelineNodeUI.UsesRowSelectionButtons(binding.TimelineNodeType))
            return false;

        if (!binding.IsChoiceNode)
            return true;

        // Enhancement branches (Lv 8+ on a Lv 5 ability) use the details panel — not timeline Select.
        return binding.Choice == null || binding.Choice.requiredLevel <= binding.Level;
    }

    /// <summary>True when this milestone row already has an ability/major sibling pick (not enhancement sub-choices).</summary>
    private bool RowHasCommittedRowPick(SkillTimelineNodeBinding[] groupBindings)
    {
        if (groupBindings == null || skillsManager == null)
            return false;

        SkillTimelineNodeBinding anchor = null;
        for (int i = 0; i < groupBindings.Length; i++)
        {
            if (groupBindings[i]?.Skill != null)
            {
                anchor = groupBindings[i];
                break;
            }
        }

        if (anchor == null)
            return false;

        if (skillsManager.GetSkillAbilityRowPick(anchor.Skill.skillType, anchor.Level, -1) >= 0)
            return true;

        for (int i = 0; i < groupBindings.Length; i++)
        {
            SkillTimelineNodeBinding binding = groupBindings[i];
            if (binding == null || !SupportsRowSelectionChrome(binding))
                continue;

            if (SkillTimelineRowSelectionRules.IsNodeCommittedSelected(skillsManager, binding))
                return true;
        }

        return false;
    }

    private void RefreshChoiceGroupRowVisuals()
    {
        if (_choiceRow == null)
            return;

        for (int g = 0; g < _choiceRow.childCount; g++)
        {
            if (!_choiceRow.GetChild(g).TryGetComponent(out SkillChoiceGroupUI group))
                continue;

            IReadOnlyList<SkillTimelineNodeUI> nodes = group.SpawnedNodes;
            if (nodes == null || nodes.Count == 0)
                continue;

            var bindings = new SkillTimelineNodeBinding[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                bindings[i] = nodes[i] != null ? nodes[i].Binding : null;

            for (int i = 0; i < nodes.Count; i++)
            {
                SkillTimelineNodeUI node = nodes[i];
                if (node == null)
                    continue;

                ApplyRowVisualsForNode(node, bindings);
            }
        }
    }

    private void RefreshStandaloneChoiceRowNodes()
    {
        if (_choiceRow == null)
            return;

        for (int i = 0; i < _choiceRow.childCount; i++)
        {
            Transform child = _choiceRow.GetChild(i);
            if (child.GetComponent<SkillChoiceGroupUI>() != null)
                continue;

            if (!child.TryGetComponent(out SkillTimelineNodeUI node))
                continue;

            SkillTimelineNodeBinding binding = node.Binding;
            if (binding == null)
                continue;

            ApplyRowVisualsForNode(node, new[] { binding });
        }
    }

    private void ApplyRowVisualsForNode(SkillTimelineNodeUI node, SkillTimelineNodeBinding[] groupBindings)
    {
        SkillTimelineNodeBinding binding = node?.Binding;
        if (binding == null)
            return;

        if (!SupportsRowSelectionChrome(binding))
        {
            node.ApplyRowPickSelectionVisual(false);
            node.SetNotSelectedPrompt(false);
            node.ConfigureRowSelectionButtons(false, false, false, null, null);
            return;
        }

        bool selected = SkillTimelineRowSelectionRules.IsNodeCommittedSelected(skillsManager, binding);
        bool showNotSelected = SkillTimelineRowSelectionRules.ShouldShowNotSelectedPrompt(
            skillsManager,
            binding,
            groupBindings);

        node.ApplyRowPickSelectionVisual(selected);
        node.SetNotSelectedPrompt(showNotSelected);
    }

    private void EnsureDetailsPanelReference()
    {
        if (detailsPanel == null)
        {
            detailsPanel = GetComponentInChildren<SkillNodeDetailsPanelUI>(true);
            if (detailsPanel == null)
            {
                SkillsAbilityPageNewUI page = GetComponentInParent<SkillsAbilityPageNewUI>(true);
                if (page != null)
                    detailsPanel = page.GetComponentInChildren<SkillNodeDetailsPanelUI>(true);
            }
        }

        WireDetailsPanelDismissed();
    }

    private void WireDetailsPanelDismissed()
    {
        if (detailsPanel == null)
            return;

        detailsPanel.DetailsDismissed -= HandleDetailsPanelDismissed;
        detailsPanel.DetailsDismissed += HandleDetailsPanelDismissed;
    }

    private void HandleDetailsPanelDismissed()
    {
        _detailsFocusedTimelineNode = null;
        RefreshRowSelectionButtons();
    }

    private SkillTimelineNodeBinding CaptureOpenDetailsBinding()
    {
        if (_detailsFocusedTimelineNode?.Binding != null)
            return _detailsFocusedTimelineNode.Binding;

        EnsureDetailsPanelReference();
        return detailsPanel != null && detailsPanel.HasActiveDetails
            ? detailsPanel.CurrentBinding
            : null;
    }

    private void RestoreOpenDetails(SkillTimelineNodeBinding binding)
    {
        if (binding == null)
            return;

        float? savedScroll = CaptureTimelineScrollPosition();
        EnsureDetailsPanelReference();
        _detailsFocusedTimelineNode = FindTimelineNodeForBinding(binding);
        detailsPanel?.Show(binding);
        RestoreTimelineScrollPosition(savedScroll);
        RefreshRowSelectionButtons();
    }

    private SkillTimelineNodeUI FindTimelineNodeForBinding(SkillTimelineNodeBinding binding)
    {
        if (binding == null)
            return null;

        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            SkillTimelineNodeUI node = _spawnedTimelineNodes[i];
            if (node?.Binding != null && BindingsMatch(node.Binding, binding))
                return node;
        }

        return null;
    }

    private static bool BindingsMatch(SkillTimelineNodeBinding a, SkillTimelineNodeBinding b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null)
            return false;

        return a.Skill == b.Skill
            && ReferenceEquals(a.Unlock, b.Unlock)
            && a.Level == b.Level
            && a.SlotAtLevel == b.SlotAtLevel
            && a.ChoiceAssetIndex == b.ChoiceAssetIndex
            && ReferenceEquals(a.Choice, b.Choice);
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            skillsManager = SkillsManager.Instance;
        else if (skillsManager == null)
            skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }

    private ScrollRect ResolveTimelineScrollRect()
    {
        if (timelineScaffold != null)
        {
            ScrollRect fromScaffold = timelineScaffold.GetComponentInChildren<ScrollRect>(true);
            if (fromScaffold != null)
                return fromScaffold;
        }

        return GetComponentInChildren<ScrollRect>(true);
    }

    private float? CaptureTimelineScrollPosition()
    {
        ScrollRect scroll = ResolveTimelineScrollRect();
        return scroll != null ? scroll.horizontalNormalizedPosition : null;
    }

    public float? TryGetTimelineScrollNormalizedPosition() => CaptureTimelineScrollPosition();

    private void RestoreTimelineScrollPosition(float? normalized)
    {
        if (_scrollRestoreSessionActive)
            return;

        if (!normalized.HasValue)
            return;

        ApplyScrollPositionImmediate(normalized.Value);
    }

    private void ApplyScrollPositionImmediate(float normalized)
    {
        ScrollRect scroll = ResolveTimelineScrollRect();
        if (scroll == null)
            return;

        scroll.velocity = Vector2.zero;
        scroll.StopMovement();
        scroll.horizontalNormalizedPosition = Mathf.Clamp01(normalized);
    }

    public void ApplyTimelineScrollNormalizedPosition(float normalized) =>
        ApplyScrollPositionImmediate(normalized);

    /// <summary>Moves choice groups / standalone nodes per <see cref="choiceGroupLayout"/> (no connector destroy).</summary>
    public void ApplyChoiceGroupLayoutOffsets()
    {
        CacheRowContainers();
        if (_choiceRow == null)
            return;

        for (int i = 0; i < _choiceRow.childCount; i++)
        {
            Transform child = _choiceRow.GetChild(i);
            if (child.TryGetComponent(out SkillChoiceGroupUI group))
            {
                group.ApplyChoiceGroupVerticalOffset(choiceGroupLayout.GetNodeOffsetY(group.ConfiguredNodeType));
                continue;
            }

            if (!child.TryGetComponent(out SkillTimelineNodeUI node))
                continue;

            SkillTimelineNodeUI.SkillTimelineNodeType nodeType = node.Binding != null
                ? node.Binding.TimelineNodeType
                : SkillTimelineNodeUI.SkillTimelineNodeType.Ability;

            float offsetY = choiceGroupLayout.GetNodeOffsetY(nodeType);
            RectTransform rt = node.RectTransform;
            Vector2 pos = rt.anchoredPosition;
            pos.y = offsetY;
            rt.anchoredPosition = pos;
        }
    }

    /// <summary>Applies offsets and rebuilds spine connectors (not safe inside OnValidate — use deferred queue).</summary>
    public void RefreshChoiceGroupLayoutOffsets()
    {
        ApplyChoiceGroupLayoutOffsets();
        BuildTimelineConnectors();
    }

#if UNITY_EDITOR
    private bool _deferredInspectorRebuildQueued;
    private bool _deferredLayoutOffsetRefreshQueued;

    private void OnValidate()
    {
        if (selectedSkill != null)
            selectedSkillType = selectedSkill.skillType;

        AutoWireEditorReferences();
    }

    private void QueueDeferredLayoutOffsetRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        if (_deferredLayoutOffsetRefreshQueued)
            return;

        _deferredLayoutOffsetRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += EditorDeferredLayoutOffsetRefresh;
    }

    private void EditorDeferredLayoutOffsetRefresh()
    {
        UnityEditor.EditorApplication.delayCall -= EditorDeferredLayoutOffsetRefresh;
        _deferredLayoutOffsetRefreshQueued = false;

        if (this == null || !isActiveAndEnabled)
            return;

        if (!HasSpawnedTimelineContent())
            return;

        RefreshChoiceGroupLayoutOffsets();
    }

    private void AutoWireEditorReferences()
    {
        if (timelineContent == null)
        {
            Transform viewport = transform.Find("TimelineViewport");
            if (viewport != null)
                timelineContent = viewport.Find("TimelineContent") as RectTransform;
        }

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        if (nodePrefab == null)
        {
            SkillTimelineNodeUI loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillTimelineNodeUI>(
                "Assets/2.Prefabs/UI/SkillsAbilityNew/SkillTimelineNodeUI.prefab");
            if (loaded != null)
                nodePrefab = loaded;
        }

        if (choiceGroupPrefab == null)
        {
            SkillChoiceGroupUI loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillChoiceGroupUI>(
                "Assets/2.Prefabs/UI/SkillsAbilityNew/MilestoneGroupUI.prefab");
            if (loaded != null)
                choiceGroupPrefab = loaded;
        }

        if (detailsPanel == null)
            EnsureDetailsPanelReference();

        if (detailsPanel == null)
        {
            const string prefabPath = "Assets/2.Prefabs/UI/SkillsAbilityNew/SkillNodeDetailsPanelUI.prefab";
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
                Debug.LogWarning(
                    "[HorizontalSkillTreeScaffoldUI] Details Panel is not in the scene. " +
                    "Run Tools → Skills → Install Skill Node Details Panel, or drag the prefab under " +
                    "SkillsAbilityPageNEW → BottomPanelBar → DetailsPanel → ViewDetailsContent.",
                    this);
        }
    }

    private void QueueInspectorRebuild()
    {
        if (!rebuildOnInspectorChange || !isActiveAndEnabled)
            return;

        if (timelineContent == null || nodePrefab == null)
            return;

        if (_deferredInspectorRebuildQueued)
            return;

        _deferredInspectorRebuildQueued = true;
        UnityEditor.EditorApplication.delayCall += EditorDeferredInspectorRebuild;
    }

    private void EditorDeferredInspectorRebuild()
    {
        UnityEditor.EditorApplication.delayCall -= EditorDeferredInspectorRebuild;
        _deferredInspectorRebuildQueued = false;

        if (this == null || !isActiveAndEnabled)
            return;

        AutoWireEditorReferences();
        if (timelineContent == null || nodePrefab == null)
            return;

        CacheRowContainers();
        if (!BuildFromSelectedSkill() && useTestTimelineFallback && !HasSpawnedTimelineContent())
            GenerateTestTimeline();
        else
            QueueDeferredConnectorRefresh();
    }
#endif
}
