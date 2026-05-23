using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    [Header("Layout (used when timelineScaffold is missing)")]
    [SerializeField] private float pixelsPerLevel = 90f;
    [SerializeField] private float timelineStartX = 120f;
    [SerializeField] private float spineY = 24f;
    [SerializeField] private float choiceRowY = -78f;

    [Header("Skill selection (drives timeline + node details)")]
    [Tooltip("Primary skill tree to render. Assign directly, or leave empty and use Skill Type + Database.")]
    [SerializeField] private SkillDefinition selectedSkill;

    [Tooltip("Used when Selected Skill is empty and Skill Database is assigned.")]
    [SerializeField] private SkillType selectedSkillType = SkillType.Melee;

    [Tooltip("Resolves Selected Skill Type when Selected Skill is not assigned.")]
    [SerializeField] private SkillDatabase skillDatabase;

    [Tooltip("When true, changing Selected Skill / Type / Database rebuilds the timeline in the Editor.")]
    [SerializeField] private bool rebuildOnInspectorChange = true;

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [Tooltip("When true and no skill is selected, runs the hardcoded test timeline.")]
    [SerializeField] private bool useTestTimelineFallback = true;
    [Tooltip("Logs one build summary line. Per-level logs are never written.")]
    [SerializeField] private bool verboseBuildLogs;

    private SkillDefinition _builtSkill;
    private bool _isBuilding;
    private readonly List<SkillTimelineNodeUI> _spawnedTimelineNodes = new();

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

    private void OnEnable()
    {
        if (!generateOnStart || !Application.isPlaying)
            return;

        EnsureDetailsPanelReference();
        CacheRowContainers();
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

        QueueDeferredConnectorRefresh();
    }

    /// <summary>Rebuilds the horizontal timeline from unlock data on <paramref name="skill"/> (display only).</summary>
    public bool Build(SkillDefinition skill)
    {
        if (_isBuilding)
            return false;

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
            EnsureTimelineReady();
            ClearSpawnedContent();
            _builtSkill = skill;

            if (skill == null || skill.unlocks == null || skill.unlocks.Count == 0)
                return false;

            int playerLevel = ResolvePlayerSkillLevel(skill);
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
            QueueDeferredConnectorRefresh();
            return true;
        }
        finally
        {
            _isBuilding = false;
        }
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

        SpawnMinorPassive(1, 0, 1);
        SpawnMinorPassive(2, 0, 1);
        SpawnMinorPassive(3, 0, 1);
        SpawnMinorPassive(4, 0, 1);
        SpawnUnlock(1, 0, 1, "Beginner Melee Combat");
        SpawnChoiceGroup(5, new[] { "Power Slash", "Rend", "Envenom" }, SkillTimelineNodeUI.SkillTimelineNodeType.Ability);
        SpawnMinorPassive(8, 0, 1);
        SpawnUnlock(8, 0, 1, "Can Catch Trout");
        SpawnChoiceGroup(10, new[] { "Ailment Attunement", "Parry", "Blade Mastery" }, SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive);
        SpawnChoiceGroup(15, new[] { "Whirlwind", "Cleaving Strikes", "Crescent Slash" }, SkillTimelineNodeUI.SkillTimelineNodeType.Ability);

        LogBuildHeader(null, 0, 0, 1);

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
    }

    private void QueueDeferredConnectorRefresh()
    {
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

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        CacheRowContainers();
        if (timelineScaffold == null || !HasSpawnedTimelineContent())
            yield break;

        BuildTimelineConnectors();
    }

    [ContextMenu("Clear Spawned Nodes")]
    public void ClearSpawnedContent()
    {
        UnregisterAllTimelineNodes();
        CacheRowContainers();
        ClearRowSpawnedContent(_unlockRow);
        ClearRowSpawnedContent(_spineRow);
        ClearRowSpawnedContent(_choiceRow);
        detailsPanel?.ShowEmpty();
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
                float x = HorizontalSkillTreeUnlockLayout.SlotAnchoredX(milestoneX, entry.SlotAtLevel, count);
                SpawnMinorPassive(level, entry, x, state);
            }
            else if (HorizontalSkillTreeUnlockLayout.IsAboveSpineType(type))
            {
                int count = aboveSlotCounts.TryGetValue(level, out int c) ? c : 1;
                float x = HorizontalSkillTreeUnlockLayout.SlotAnchoredX(milestoneX, entry.SlotAtLevel, count);
                string title = SkillsAbilityPresentationResolver.ResolveTreeUnlockTitle(unlock);
                SpawnUnlock(level, entry, count, title, x, state);
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
                state);

            RegisterChoiceGroupNodes(group, entries, level, nodeType, state);
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
            CacheRowContainers();
            bool rowsMissing = _unlockRow == null || _spineRow == null || _choiceRow == null;
            if (rowsMissing)
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

        float spine = SpineYPos;
        ConnectAllUnlockNodes(connectors, spine);
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

            timelineScaffold.DrawConnector(
                connectors,
                spineAttach,
                branchAttach,
                extendBeyondStart: SkillTimelineScaffoldUI.ConnectorSpineOverlap,
                extendBeyondEnd: 0f);
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

            if (!child.TryGetComponent(out SkillTimelineNodeUI _))
                continue;

            if (!TryGetBelowSpineNodeConnectorPoints(child, spineY, out Vector2 spineAttach, out Vector2 nodeAttach))
                continue;

            timelineScaffold.DrawConnector(
                connectors,
                spineAttach,
                nodeAttach,
                extendBeyondStart: SkillTimelineScaffoldUI.ConnectorSpineOverlap,
                extendBeyondEnd: 0f);
        }
    }

    private void ConnectAllUnlockNodes(RectTransform connectors, float spineY)
    {
        if (_unlockRow == null || timelineContent == null)
            return;

        for (int i = 0; i < _unlockRow.childCount; i++)
        {
            Transform child = _unlockRow.GetChild(i);
            if (!child.TryGetComponent(out SkillTimelineNodeUI _))
                continue;

            if (TryGetUnlockConnectorPoints(child.name, spineY, out Vector2 spineAttach, out Vector2 nodeAttach))
            {
                timelineScaffold.DrawConnector(
                    connectors,
                    spineAttach,
                    nodeAttach,
                    extendBeyondStart: SkillTimelineScaffoldUI.ConnectorSpineOverlap,
                    extendBeyondEnd: 0f);
            }
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

    private bool TryGetUnlockConnectorPoints(string objectName, float spineY, out Vector2 spineAttach, out Vector2 nodeAttach)
    {
        spineAttach = default;
        nodeAttach = default;
        if (_unlockRow == null || timelineContent == null || string.IsNullOrEmpty(objectName))
            return false;

        for (int i = 0; i < _unlockRow.childCount; i++)
        {
            Transform child = _unlockRow.GetChild(i);
            if (child.name != objectName || !child.TryGetComponent(out SkillTimelineNodeUI _))
                continue;

            Transform rootButton = child.Find("RootButton");
            RectTransform measureRt = rootButton != null ? rootButton as RectTransform : (RectTransform)child;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(timelineContent, measureRt);
            nodeAttach = new Vector2(bounds.center.x, bounds.min.y);
            spineAttach = new Vector2(nodeAttach.x, spineY);
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

    private float SpineYPos => timelineScaffold != null ? timelineScaffold.TimelineSpineY : spineY;

    private float ChoiceRowYPos => timelineScaffold != null ? timelineScaffold.TimelineChoiceRowY : choiceRowY;

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
        SkillTimelineNodeUI.SkillTimelineNodeState state)
    {
        SpawnNodeInRow(_unlockRow, new Vector2(x, -4f), $"Unlock_Lv{level}_{entry.SlotAtLevel}", node =>
        {
            node.ApplyUnlockTimelinePreview(label, state);
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
            nodePrefab);
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
        return SpawnNodeInRow(_choiceRow, new Vector2(x, 0f), $"ChoiceNode_Lv{level}_{slot}", node =>
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
        _spawnedTimelineNodes.Add(node);
    }

    private void UnregisterAllTimelineNodes()
    {
        for (int i = 0; i < _spawnedTimelineNodes.Count; i++)
        {
            if (_spawnedTimelineNodes[i] != null)
                _spawnedTimelineNodes[i].Clicked -= HandleTimelineNodeClicked;
        }

        _spawnedTimelineNodes.Clear();
    }

    private void HandleTimelineNodeClicked(SkillTimelineNodeUI node)
    {
        EnsureDetailsPanelReference();
        if (detailsPanel == null || node == null)
            return;

        detailsPanel.Show(node.Binding);
    }

    private void EnsureDetailsPanelReference()
    {
        if (detailsPanel != null)
            return;

        detailsPanel = GetComponentInChildren<SkillNodeDetailsPanelUI>(true);
        if (detailsPanel == null)
        {
            SkillsAbilityPageNewUI page = GetComponentInParent<SkillsAbilityPageNewUI>(true);
            if (page != null)
                detailsPanel = page.GetComponentInChildren<SkillNodeDetailsPanelUI>(true);
        }
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            skillsManager = SkillsManager.Instance;
        else if (skillsManager == null)
            skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }

#if UNITY_EDITOR
    private bool _deferredInspectorRebuildQueued;

    private void OnValidate()
    {
        if (selectedSkill != null)
            selectedSkillType = selectedSkill.skillType;

        AutoWireEditorReferences();
        QueueInspectorRebuild();
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
            if (loaded == null)
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillChoiceGroupUI>(
                    "Assets/2.Prefabs/UI/SkillsAbilityNew/SkillChoiceGroupUI.prefab");
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
