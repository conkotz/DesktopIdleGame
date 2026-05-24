using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Milestone choice group for the horizontal skill timeline (visual scaffold only).
/// Hierarchy: ConnectorOverlay (StemLine, BranchLine, ChoiceConnectorLines) + ChoiceNodesContainer.
/// </summary>
[DisallowMultipleComponent]
public class SkillChoiceGroupUI : MonoBehaviour
{
    public const int MinChoiceCount = 2;
    public const int MaxChoiceCount = 5;
    public const float DefaultNodeSpacing = 40f;

    [Header("References")]
    [SerializeField] private RectTransform rectTransform;
    [FormerlySerializedAs("branchContainer")]
    [SerializeField] private RectTransform connectorOverlay;
    [FormerlySerializedAs("spineStemLine")]
    [SerializeField] private RectTransform stemLine;
    [FormerlySerializedAs("spineStemLineImage")]
    [SerializeField] private Image stemLineImage;
    [FormerlySerializedAs("horizontalBranchLine")]
    [SerializeField] private RectTransform branchLine;
    [FormerlySerializedAs("horizontalBranchLineImage")]
    [SerializeField] private Image branchLineImage;
    [FormerlySerializedAs("nodesContainer")]
    [SerializeField] private RectTransform choiceNodesContainer;
    [SerializeField] private HorizontalLayoutGroup nodesLayout;
    [SerializeField] private TMP_Text choiceHintLabel;

    [Header("Layout")]
    [SerializeField] private float nodeSpacing = DefaultNodeSpacing;
    [SerializeField] private float connectorThickness = SkillTimelineScaffoldUI.TimelineConnectorThickness;
    [SerializeField] private float branchAboveNodesGap = 2f;
    [SerializeField] private float nodeConnectorEndInset = 0f;
    [SerializeField] private float centerStemAboveBranch = 2f;
    [Tooltip("Raises major / capstone nodes only (abilities unchanged). Tune in Play mode.")]
    [SerializeField] private float majorCapstoneNodeLiftY = 16f;
    [Tooltip("Extra lift for capstone groups on top of majorCapstoneNodeLiftY.")]
    [SerializeField] private float capstoneExtraNodeLiftY = 4f;
    [Tooltip("Optional subtle hint above the branch. Off by default.")]
    [SerializeField] private bool showChoiceHintLabel;

    private SkillTimelineNodeUI.SkillTimelineNodeType _configuredNodeType;
    private readonly List<SkillTimelineNodeUI> _spawnedNodes = new();
    private readonly List<RectTransform> _choiceConnectorLines = new();
    private readonly List<Image> _choiceConnectorImages = new();

    private float _lastBranchY;
    private float _lastBranchCenterX;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;
    public IReadOnlyList<SkillTimelineNodeUI> SpawnedNodes => _spawnedNodes;
    public int ChoiceCount => _spawnedNodes.Count;

    private void Awake() => EnsureHierarchy();

    public void EnsurePrefabHierarchy() => EnsureHierarchy();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (nodesLayout != null)
            nodesLayout.spacing = nodeSpacing;
        connectorThickness = SkillTimelineScaffoldUI.TimelineConnectorThickness;
    }
#endif

    public void Configure(
        int level,
        float milestoneLevelX,
        float spineY,
        float groupAnchorY,
        string[] nodeNames,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillTimelineNodeUI nodePrefab,
        SkillTimelineNodeUI.SkillTimelineNodeState displayState = SkillTimelineNodeUI.SkillTimelineNodeState.Available)
    {
        if (nodeNames == null || nodeNames.Length < MinChoiceCount)
        {
            Debug.LogWarning($"[SkillChoiceGroupUI] Lv{level} needs at least {MinChoiceCount} choices.", this);
            return;
        }

        var entries = new HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry[nodeNames.Length];
        for (int i = 0; i < nodeNames.Length; i++)
            entries[i] = new HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry(null, null, -1);

        Configure(level, milestoneLevelX, spineY, groupAnchorY, entries, nodeNames, nodeType, nodePrefab, displayState);
    }

    public void Configure(
        int level,
        float milestoneLevelX,
        float spineY,
        float groupAnchorY,
        HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry[] entries,
        string[] displayNames,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillTimelineNodeUI nodePrefab,
        SkillTimelineNodeUI.SkillTimelineNodeState displayState = SkillTimelineNodeUI.SkillTimelineNodeState.Available)
    {
        EnsureHierarchy();

        if (entries == null || entries.Length < MinChoiceCount)
        {
            Debug.LogWarning($"[SkillChoiceGroupUI] Lv{level} needs at least {MinChoiceCount} choices.", this);
            return;
        }

        if (entries.Length > MaxChoiceCount)
            Debug.LogWarning($"[SkillChoiceGroupUI] Lv{level} clamped to {MaxChoiceCount} choices.", this);

        int count = Mathf.Clamp(entries.Length, MinChoiceCount, MaxChoiceCount);

        RectTransform rt = RectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(milestoneLevelX, 0f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        _configuredNodeType = nodeType;
        ClearNodes();
        ApplyChoiceHintLabel(count);
        PopulateNodes(level, entries, displayNames, count, nodeType, nodePrefab, displayState);
        RefreshConnectorLayout();
    }

    public void RefreshConnectorLayout()
    {
        if (choiceNodesContainer == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(choiceNodesContainer);
        Canvas.ForceUpdateCanvases();

        ApplyNodesContainerPreferredSize();
        LayoutRebuilder.ForceRebuildLayoutImmediate(choiceNodesContainer);

        ApplyMajorCapstoneNodeLift();
        LayoutConnectors();
        LayoutChoiceHintLabel();
    }

    private bool UsesMajorCapstoneLift =>
        _configuredNodeType == SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive
        || _configuredNodeType == SkillTimelineNodeUI.SkillTimelineNodeType.Capstone;

    private float ResolveMajorCapstoneLiftY()
    {
        if (!UsesMajorCapstoneLift)
            return 0f;

        float lift = majorCapstoneNodeLiftY;
        if (_configuredNodeType == SkillTimelineNodeUI.SkillTimelineNodeType.Capstone)
            lift += capstoneExtraNodeLiftY;

        return lift;
    }

    /// <summary>Moves major/capstone gems up; branch + drops are rebuilt afterward so spine stems stay attached.</summary>
    private void ApplyMajorCapstoneNodeLift()
    {
        float lift = ResolveMajorCapstoneLiftY();
        if (lift <= 0f || _spawnedNodes.Count == 0)
            return;

        for (int i = 0; i < _spawnedNodes.Count; i++)
        {
            SkillTimelineNodeUI node = _spawnedNodes[i];
            if (node == null)
                continue;

            RectTransform nodeRt = node.RectTransform;
            Vector2 pos = nodeRt.anchoredPosition;
            pos.y = lift;
            nodeRt.anchoredPosition = pos;
        }
    }

    /// <summary>Branch junction in timeline content space for the spine stem connector.</summary>
    public bool TryGetSpineConnectorPoints(RectTransform timelineContent, float spineY, out Vector2 spineAttach, out Vector2 branchAttach)
    {
        spineAttach = default;
        branchAttach = default;

        if (timelineContent == null || !TryGetBranchLayout(out float branchY, out float branchCenterX, out _))
            return false;

        Vector3 branchWorld = rectTransform.TransformPoint(new Vector3(branchCenterX, branchY, 0f));
        branchAttach = timelineContent.InverseTransformPoint(branchWorld);
        spineAttach = new Vector2(branchAttach.x, spineY);
        return true;
    }

    public void ClearNodes()
    {
        for (int i = _spawnedNodes.Count - 1; i >= 0; i--)
        {
            if (_spawnedNodes[i] != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(_spawnedNodes[i].gameObject);
                else
#endif
                    Destroy(_spawnedNodes[i].gameObject);
            }
        }

        _spawnedNodes.Clear();
    }

    private void PopulateNodes(
        int level,
        HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry[] entries,
        string[] displayNames,
        int count,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillTimelineNodeUI nodePrefab,
        SkillTimelineNodeUI.SkillTimelineNodeState displayState)
    {
        if (nodePrefab == null || choiceNodesContainer == null)
            return;

        for (int i = 0; i < count; i++)
        {
            SkillTimelineNodeUI node = Instantiate(nodePrefab, choiceNodesContainer);
            node.name = $"Choice_Lv{level}_{i}";

            RectTransform nodeRt = node.RectTransform;
            nodeRt.anchorMin = new Vector2(0.5f, 0.5f);
            nodeRt.anchorMax = new Vector2(0.5f, 0.5f);
            nodeRt.pivot = new Vector2(0.5f, 0.5f);
            nodeRt.anchoredPosition = Vector2.zero;
            nodeRt.localScale = Vector3.one;
            nodeRt.localRotation = Quaternion.identity;

            string label = displayNames != null && i < displayNames.Length
                ? displayNames[i]
                : ResolveEntryLabel(entries[i]);
            node.ApplyChoiceGroupNodePreview(nodeType, label, displayState);
            _spawnedNodes.Add(node);
        }
    }

    private static string ResolveEntryLabel(HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry entry)
    {
        if (entry.Choice != null)
        {
            string title = SkillsAbilityPresentationResolver.ResolveChoiceTitle(entry.Choice);
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }

        if (entry.Unlock != null)
        {
            string unlockTitle = SkillsAbilityPresentationResolver.ResolveTreeUnlockTitle(entry.Unlock);
            if (!string.IsNullOrWhiteSpace(unlockTitle))
                return unlockTitle;
        }

        return "Node";
    }

    private void ApplyNodesContainerPreferredSize()
    {
        if (choiceNodesContainer == null || nodesLayout == null)
            return;

        float preferredW = LayoutUtility.GetPreferredWidth(choiceNodesContainer);
        float preferredH = LayoutUtility.GetPreferredHeight(choiceNodesContainer);
        if (preferredW > 1f && preferredH > 1f)
        {
            choiceNodesContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, preferredW);
            choiceNodesContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredH);
        }
    }

    private void ApplyChoiceHintLabel(int count)
    {
        if (choiceHintLabel == null)
            return;

        choiceHintLabel.text = $"Choose 1 of {count}";
        choiceHintLabel.fontSize = 9f;
        choiceHintLabel.fontStyle = FontStyles.Italic;
        choiceHintLabel.color = new Color(0.2f, 0.14f, 0.1f, 0.42f);
        choiceHintLabel.alignment = TextAlignmentOptions.Center;
        choiceHintLabel.textWrappingMode = TextWrappingModes.NoWrap;
        choiceHintLabel.gameObject.SetActive(showChoiceHintLabel);
    }

    private void LayoutChoiceHintLabel()
    {
        if (choiceHintLabel == null || !showChoiceHintLabel)
            return;

        if (!TryGetBranchLayout(out float branchY, out float branchCenterX, out float containerWidth))
            return;

        RectTransform hintRt = choiceHintLabel.rectTransform;
        hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0.5f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(branchCenterX, branchY + centerStemAboveBranch + 2f);
        hintRt.sizeDelta = new Vector2(Mathf.Min(containerWidth, 140f), 12f);
        choiceHintLabel.gameObject.SetActive(true);
    }

    private void LayoutConnectors()
    {
        if (connectorOverlay == null || branchLine == null || choiceNodesContainer == null)
            return;

        if (!TryCollectNodeBoundsInContainer(out List<Bounds> nodeBoundsList, out _, out float containerWidth, out float containerHeight, out float branchY))
        {
            SetChoiceConnectorCount(0);
            HidePrefabConnectorLines();
            return;
        }

        AlignConnectorOverlay(containerWidth, containerHeight);

        nodeBoundsList.Sort((a, b) => a.center.x.CompareTo(b.center.x));
        float branchLeftX = nodeBoundsList[0].center.x;
        float branchRightX = nodeBoundsList[nodeBoundsList.Count - 1].center.x;
        float branchSpanWidth = Mathf.Max(SkillTimelineLineStyle.LineThickness, branchRightX - branchLeftX);
        float branchCenterX = (branchLeftX + branchRightX) * 0.5f;

        _lastBranchY = branchY;
        _lastBranchCenterX = branchCenterX;

        branchLine.gameObject.SetActive(true);
        SkillTimelineLineStyle.ApplyHorizontalBar(branchLine, branchCenterX, branchY, branchSpanWidth);

        if (stemLine != null)
            stemLine.gameObject.SetActive(false);

        SetChoiceConnectorCount(nodeBoundsList.Count);
        for (int i = 0; i < nodeBoundsList.Count; i++)
        {
            Bounds nodeBounds = nodeBoundsList[i];
            float nodeCenterX = nodeBounds.center.x;
            float nodeTopY = nodeBounds.max.y - nodeConnectorEndInset;
            float dropHeight = branchY - nodeTopY;

            RectTransform drop = _choiceConnectorLines[i];
            bool showDrop = dropHeight > 0.5f;
            drop.gameObject.SetActive(showDrop);
            if (!showDrop)
                continue;

            SkillTimelineLineStyle.ApplyVerticalBar(drop, nodeCenterX, branchY, dropHeight);
            drop.SetAsLastSibling();
        }
    }

    private void HidePrefabConnectorLines()
    {
        if (branchLine != null)
            branchLine.gameObject.SetActive(false);
        if (stemLine != null)
            stemLine.gameObject.SetActive(false);

        for (int i = 0; i < _choiceConnectorLines.Count; i++)
        {
            if (_choiceConnectorLines[i] != null)
                _choiceConnectorLines[i].gameObject.SetActive(false);
        }
    }

    private void AlignConnectorOverlay(float containerWidth, float containerHeight)
    {
        connectorOverlay.anchorMin = connectorOverlay.anchorMax = new Vector2(0.5f, 0.5f);
        connectorOverlay.pivot = new Vector2(0.5f, 0.5f);
        connectorOverlay.anchoredPosition = Vector2.zero;
        connectorOverlay.sizeDelta = new Vector2(containerWidth, containerHeight);
        connectorOverlay.SetAsLastSibling();
    }

    private bool TryGetBranchLayout(out float branchY, out float branchCenterX, out float containerWidth)
    {
        branchY = _lastBranchY;
        branchCenterX = _lastBranchCenterX;
        containerWidth = 0f;
        if (!TryCollectNodeBoundsInContainer(out _, out _, out containerWidth, out _, out branchY))
            return false;

        if (TryCollectNodeBoundsInContainer(out List<Bounds> bounds, out _, out _, out _, out _))
        {
            bounds.Sort((a, b) => a.center.x.CompareTo(b.center.x));
            branchCenterX = (bounds[0].center.x + bounds[bounds.Count - 1].center.x) * 0.5f;
        }

        return true;
    }

    private bool TryCollectNodeBoundsInContainer(
        out List<Bounds> nodeBoundsList,
        out Bounds unionBounds,
        out float containerWidth,
        out float containerHeight,
        out float branchY)
    {
        nodeBoundsList = new List<Bounds>();
        unionBounds = default;
        containerWidth = 0f;
        containerHeight = 0f;
        branchY = 0f;

        if (choiceNodesContainer == null)
            return false;

        bool hasAny = false;
        for (int i = 0; i < choiceNodesContainer.childCount; i++)
        {
            Transform child = choiceNodesContainer.GetChild(i);
            if (child == connectorOverlay)
                continue;
            if (!child.TryGetComponent(out SkillTimelineNodeUI _))
                continue;

            Bounds bounds = GetNodeChromeBoundsInContainer(child);
            nodeBoundsList.Add(bounds);
            if (!hasAny)
            {
                unionBounds = bounds;
                hasAny = true;
            }
            else
                unionBounds.Encapsulate(bounds);
        }

        nodeBoundsList.Sort((a, b) => a.center.x.CompareTo(b.center.x));

        containerWidth = choiceNodesContainer.rect.width;
        containerHeight = choiceNodesContainer.rect.height;
        branchY = unionBounds.max.y + branchAboveNodesGap;
        return hasAny && nodeBoundsList.Count >= MinChoiceCount;
    }

    private Bounds GetNodeChromeBoundsInContainer(Transform nodeTransform)
    {
        Transform chrome = nodeTransform.Find("RootButton");
        RectTransform measure = chrome != null ? chrome as RectTransform : nodeTransform as RectTransform;
        return RectTransformUtility.CalculateRelativeRectTransformBounds(choiceNodesContainer, measure);
    }

    private void SetChoiceConnectorCount(int count)
    {
        while (_choiceConnectorLines.Count < count)
        {
            RectTransform line = CreateConnectorLineRect(connectorOverlay, $"ChoiceConnectorLine_{_choiceConnectorLines.Count}");
            _choiceConnectorLines.Add(line);
            _choiceConnectorImages.Add(line.GetComponent<Image>());
        }

        for (int i = _choiceConnectorLines.Count - 1; i >= count; i--)
        {
            if (_choiceConnectorLines[i] != null)
                DestroyImmediateSafe(_choiceConnectorLines[i].gameObject);
            _choiceConnectorLines.RemoveAt(i);
            _choiceConnectorImages.RemoveAt(i);
        }
    }

    private void EnsureHierarchy()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;

        MigrateLegacyHierarchyNames();
        CleanupLegacyHierarchyObjects();

        if (choiceNodesContainer == null)
        {
            for (int i = rectTransform.childCount - 1; i >= 0; i--)
                DestroyImmediateSafe(rectTransform.GetChild(i).gameObject);

            _choiceConnectorLines.Clear();
            _choiceConnectorImages.Clear();

            choiceHintLabel = CreateChoiceHintLabel(rectTransform);
            choiceNodesContainer = CreateChoiceNodesContainer(rectTransform);
            nodesLayout = choiceNodesContainer.GetComponent<HorizontalLayoutGroup>();
            EnsureConnectorOverlayHierarchy();

            choiceHintLabel.rectTransform.SetAsFirstSibling();
            choiceNodesContainer.SetAsLastSibling();
        }

        if (nodesLayout != null)
            nodesLayout.spacing = nodeSpacing;

        EnsureConnectorOverlayHierarchy();
    }

    private void CleanupLegacyHierarchyObjects()
    {
        DestroyLegacyChild(rectTransform, "SpineStemLine");
        DestroyLegacyChild(rectTransform, "ChoiceLabel");
        DestroyLegacyChild(rectTransform, "HorizontalBranchLine");
        DestroyLegacyChild(rectTransform, "LabelBranchStem");

        if (choiceNodesContainer != null)
        {
            Transform legacyBranch = choiceNodesContainer.Find("BranchContainer");
            if (legacyBranch != null && connectorOverlay != null && legacyBranch != connectorOverlay)
                DestroyImmediateSafe(legacyBranch.gameObject);
        }
    }

    private static void DestroyLegacyChild(Transform parent, string childName)
    {
        if (parent == null)
            return;

        Transform legacy = parent.Find(childName);
        if (legacy != null)
            DestroyImmediateSafe(legacy.gameObject);
    }

    private void MigrateLegacyHierarchyNames()
    {
        if (choiceNodesContainer == null)
        {
            Transform legacy = transform.Find("NodesContainer");
            if (legacy != null)
                choiceNodesContainer = legacy as RectTransform;
        }

        if (connectorOverlay == null)
        {
            Transform legacy = choiceNodesContainer != null
                ? choiceNodesContainer.Find("BranchContainer")
                : transform.Find("BranchContainer");
            if (legacy != null)
                connectorOverlay = legacy as RectTransform;
        }

        if (branchLine == null && connectorOverlay != null)
        {
            Transform legacy = connectorOverlay.Find("HorizontalBranchLine");
            if (legacy != null)
                branchLine = legacy as RectTransform;
        }

        if (stemLine == null && connectorOverlay != null)
        {
            Transform legacy = connectorOverlay.Find("StemLine");
            if (legacy == null)
                legacy = transform.Find("SpineStemLine");
            if (legacy != null)
                stemLine = legacy as RectTransform;
        }

        if (choiceHintLabel == null)
        {
            Transform legacy = transform.Find("ChoiceLabel");
            if (legacy != null)
                choiceHintLabel = legacy.GetComponent<TMP_Text>();
        }
    }

    private void EnsureConnectorOverlayHierarchy()
    {
        if (choiceNodesContainer == null)
            return;

        if (connectorOverlay == null)
            connectorOverlay = CreateConnectorOverlay(choiceNodesContainer);

        if (branchLine == null)
        {
            branchLine = CreateConnectorLineRect(connectorOverlay, "BranchLine");
            branchLineImage = branchLine.GetComponent<Image>();
        }
        else if (branchLineImage == null)
            branchLineImage = branchLine.GetComponent<Image>();

        if (stemLine == null)
        {
            stemLine = CreateConnectorLineRect(connectorOverlay, "StemLine");
            stemLineImage = stemLine.GetComponent<Image>();
        }
        HidePrefabConnectorLines();
        ResetConnectorLineRect(branchLine);
        ResetConnectorLineRect(stemLine);

        connectorOverlay.SetAsLastSibling();
    }

    private static void ResetConnectorLineRect(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(SkillTimelineLineStyle.LineThickness, SkillTimelineLineStyle.LineThickness);
        SkillTimelineLineStyle.Apply(rt.GetComponent<Image>());
    }

    private static RectTransform CreateConnectorLineRect(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.SetActive(false);
        ResetConnectorLineRect(rt);
        return rt;
    }

    private static RectTransform CreateConnectorOverlay(RectTransform nodesContainerParent)
    {
        var go = new GameObject("ConnectorOverlay", typeof(RectTransform), typeof(LayoutElement), typeof(CanvasRenderer));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(nodesContainerParent, false);

        var layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private static RectTransform CreateChoiceNodesContainer(RectTransform parent)
    {
        var go = new GameObject("ChoiceNodesContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = DefaultNodeSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    private static TMP_Text CreateChoiceHintLabel(RectTransform parent)
    {
        var go = new GameObject("ChoiceHintLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 9f;
        tmp.fontStyle = FontStyles.Italic;
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = "Choose 1 of 3";
        tmp.gameObject.SetActive(false);
        return tmp;
    }

    private static void DestroyImmediateSafe(Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
#endif
            Destroy(obj);
    }
}
