using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns timeline prefab placeholders into row containers on the horizontal skill timeline.
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

    [Header("Layout (used when timelineScaffold is missing)")]
    [SerializeField] private float pixelsPerLevel = 90f;
    [SerializeField] private float timelineStartX = 120f;
    [SerializeField] private float spineY = 24f;
    [SerializeField] private float unlockRowY = 90f;
    [SerializeField] private float choiceRowY = -78f;

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;

    private void OnEnable()
    {
        if (!generateOnStart || !Application.isPlaying)
            return;

        CacheRowContainers();
        if (!HasSpawnedTimelineContent())
        {
            GenerateTestTimeline();
            return;
        }

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

    /// <summary>Called by <see cref="SkillTimelineScaffoldUI"/> after each spine rebuild when using prefab nodes.</summary>
    public void OnScaffoldRebuilt(RectTransform content)
    {
        if (content != null)
            timelineContent = content;

        CacheRowContainers();
        if (!HasSpawnedTimelineContent())
            return;

        BuildUnlockConnectors();
    }

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

        EnsureTimelineReady();
        ClearSpawnedContent();

        SpawnUnlock(1, "Beginner Melee Combat");
        SpawnMinorPassive(2);
        SpawnMinorPassive(3);
        SpawnMinorPassive(4);
        SpawnChoiceGroup(5, new[] { "Power Slash", "Rend", "Envenom" }, SkillTimelineNodeUI.SkillTimelineNodeType.Ability);
        SpawnMinorPassive(8);
        SpawnUnlock(8, "Can Catch Trout");
        SpawnChoiceGroup(10, new[] { "Ailment Attunement", "Parry", "Blade Mastery" }, SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive);
        SpawnChoiceGroup(15, new[] { "Whirlwind", "Cleaving Strikes", "Crescent Slash" }, SkillTimelineNodeUI.SkillTimelineNodeType.Ability);

        QueueDeferredConnectorRefresh();

        Debug.Log($"[HorizontalSkillTreeScaffoldUI] Generated test timeline under '{timelineContent.name}'.", this);
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

        BuildUnlockConnectors();
    }

    [ContextMenu("Clear Spawned Nodes")]
    public void ClearSpawnedContent()
    {
        CacheRowContainers();
        ClearRowSpawnedContent(_unlockRow);
        ClearRowSpawnedContent(_spineRow);
        ClearRowSpawnedContent(_choiceRow);
    }

    private void EnsureTimelineReady()
    {
        if (timelineContent == null)
            return;

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

        if (timelineScaffold != null)
            timelineScaffold.RebuildScaffold();
        else
            PrepareContentForAbsoluteNodes(timelineContent);

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
                Object.DestroyImmediate(child.gameObject);
            else
#endif
                Object.Destroy(child.gameObject);
        }
    }

    private void BuildUnlockConnectors() => BuildTimelineConnectors();

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

        ConnectUnlock(connectors, 1, spine);
        ConnectUnlock(connectors, 8, spine);
        ConnectChoiceGroupsToSpine(connectors, spine);
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

            timelineScaffold.DrawConnector(connectors, spineAttach, branchAttach);
        }
    }

    private void ConnectUnlock(RectTransform connectors, int level, float spineY)
    {
        float x = GetLevelX(level);
        if (TryGetUnlockConnectorPoints($"Unlock_Lv{level}", spineY, out Vector2 spineAttach, out Vector2 nodeAttach))
            timelineScaffold.DrawConnector(connectors, spineAttach, nodeAttach);
        else
        {
            float halfH = SkillTimelineNodeUI.StandardNodeHalfHeight;
            timelineScaffold.DrawConnector(connectors, new Vector2(x, spineY + 5f), new Vector2(x, UnlockRowYPos - halfH));
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
            spineAttach = new Vector2(nodeAttach.x, spineY + 5f);
            return true;
        }

        return false;
    }

    private static void PrepareContentForAbsoluteNodes(RectTransform content)
    {
        if (content.TryGetComponent(out HorizontalLayoutGroup hlg))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(hlg);
            else
#endif
                Object.Destroy(hlg);
        }

        if (content.TryGetComponent(out ContentSizeFitter csf))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(csf);
            else
#endif
                Object.Destroy(csf);
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

    private float SpineYPos => timelineScaffold != null ? timelineScaffold.TimelineSpineY : spineY;

    private float UnlockRowYPos => timelineScaffold != null ? timelineScaffold.TimelineUnlockRowY : unlockRowY;

    private float ChoiceRowYPos => timelineScaffold != null ? timelineScaffold.TimelineChoiceRowY : choiceRowY;

    public SkillTimelineNodeUI SpawnMinorPassive(int level)
    {
        float x = GetLevelX(level);
        return SpawnNodeInRow(_spineRow, new Vector2(x, 0f), $"Minor_Lv{level}", node =>
        {
            node.ApplySpineDiamondPreview(SkillTimelineNodeUI.SkillTimelineNodeState.Available);
        });
    }

    public SkillTimelineNodeUI SpawnUnlock(int level, string label)
    {
        float x = GetLevelX(level);
        return SpawnNodeInRow(_unlockRow, new Vector2(x, -4f), $"Unlock_Lv{level}", node =>
        {
            node.ApplyUnlockTimelinePreview(label);
        });
    }

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
        return instance;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (timelineContent == null)
        {
            Transform viewport = transform.Find("TimelineViewport");
            if (viewport != null)
                timelineContent = viewport.Find("TimelineContent") as RectTransform;
        }

        if (timelineScaffold == null)
            timelineScaffold = GetComponent<SkillTimelineScaffoldUI>();

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
    }
#endif
}
