using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Milestone choice group for the horizontal skill timeline (visual scaffold only).
/// Use the <see cref="MilestoneGroupUI"/> prefab. Per-type Y offsets are set on
/// <see cref="HorizontalSkillTreeScaffoldUI"/> (Choice Group Layout), not here.
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
#pragma warning disable CS0414 // Serialized for inspector/backward compatibility; runtime uses shared line style constants.
    [SerializeField] private float connectorThickness = SkillTimelineScaffoldUI.TimelineConnectorThickness;
#pragma warning restore CS0414
    [SerializeField] private float branchAboveNodesGap = 2f;
    [SerializeField] private float nodeConnectorEndInset = 0f;
    [Tooltip("How far each vertical drop extends down into the node chrome (below the branch).")]
    [SerializeField] private float nodeConnectorReachIntoNode = 14f;
    [Tooltip("Extra drop depth for major passive groups so the line tucks farther under the diamond/icon chrome.")]
    [SerializeField] private float majorPassiveConnectorExtraReachIntoNode = 4f;
    [Tooltip("Tiny horizontal overlap to hide 1 px seams where branch corners meet vertical drops.")]
    [SerializeField] private float connectorCornerOverlap = 1f;
    [Tooltip("Extra upward overlap so each vertical drop tucks slightly into the horizontal branch.")]
    [SerializeField] private float connectorTopOverlap = 1f;
    [Tooltip("Additional upward overlap for the selected gold connector only.")]
    [SerializeField] private float selectedConnectorTopExtraOverlap = 0f;
    [Tooltip("Additional downward reach for the selected gold connector so it tucks farther under the node.")]
    [SerializeField] private float selectedConnectorExtraReachIntoNode = 5f;
    [Tooltip("Extra horizontal overlap into the center junction for the selected gold branch only.")]
    [SerializeField] private float selectedConnectorExtraJunctionOverlap = 1f;
    [SerializeField] private float centerStemAboveBranch = 2f;
    [Tooltip("Optional subtle hint above the branch. Off by default.")]
    [SerializeField] private bool showChoiceHintLabel;

    private SkillTimelineNodeUI.SkillTimelineNodeType _configuredNodeType;
    private float _milestoneLevelX;
    private float _choiceNodeOffsetY;

    public SkillTimelineNodeUI.SkillTimelineNodeType ConfiguredNodeType => _configuredNodeType;
    private readonly List<SkillTimelineNodeUI> _spawnedNodes = new();
    private readonly List<RectTransform> _choiceConnectorLines = new();
    private readonly List<Image> _choiceConnectorImages = new();

    private float _lastBranchY;
    private float _lastBranchCenterX;
    private int _connectorSelectionSlotIndex = -1;
    private System.Action<SkillChoiceGroupUI> _afterConnectorLayout;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;
    public IReadOnlyList<SkillTimelineNodeUI> SpawnedNodes => _spawnedNodes;
    public int ChoiceCount => _spawnedNodes.Count;

    public void SetAfterConnectorLayoutRefresh(System.Action<SkillChoiceGroupUI> callback) =>
        _afterConnectorLayout = callback;

    /// <summary>Gold connector path on branch + drop to the committed row pick (-1 = default lines).</summary>
    public void SetConnectorSelectionHighlight(int committedSlotIndex)
    {
        _connectorSelectionSlotIndex = committedSlotIndex;
        RefreshConnectorLayout();
    }

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
        SkillTimelineNodeUI.SkillTimelineNodeState displayState = SkillTimelineNodeUI.SkillTimelineNodeState.Available,
        float choiceNodeOffsetY = 0f)
    {
        if (nodeNames == null || nodeNames.Length < MinChoiceCount)
        {
            Debug.LogWarning($"[SkillChoiceGroupUI] Lv{level} needs at least {MinChoiceCount} choices.", this);
            return;
        }

        var entries = new HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry[nodeNames.Length];
        for (int i = 0; i < nodeNames.Length; i++)
            entries[i] = new HorizontalSkillTreeUnlockLayout.BelowSpineSpawnEntry(null, null, -1);

        Configure(level, milestoneLevelX, spineY, groupAnchorY, entries, nodeNames, nodeType, nodePrefab, displayState, choiceNodeOffsetY);
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
        SkillTimelineNodeUI.SkillTimelineNodeState displayState = SkillTimelineNodeUI.SkillTimelineNodeState.Available,
        float choiceNodeOffsetY = 0f)
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
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        _milestoneLevelX = milestoneLevelX;
        _configuredNodeType = nodeType;
        _choiceNodeOffsetY = choiceNodeOffsetY;
        ClearNodes();
        ApplyChoiceHintLabel(count);
        PopulateNodes(level, entries, displayNames, count, nodeType, nodePrefab, displayState);
        ApplyGroupVerticalOffset();
        RefreshConnectorLayout();
    }

    /// <summary>Live refresh when <see cref="HorizontalSkillTreeScaffoldUI"/> choice group layout changes.</summary>
    public void ApplyChoiceGroupVerticalOffset(float offsetY)
    {
        _choiceNodeOffsetY = offsetY;
        ApplyGroupVerticalOffset();
        RefreshConnectorLayout();
    }

    private void ApplyGroupVerticalOffset()
    {
        RectTransform rt = RectTransform;
        rt.anchoredPosition = new Vector2(_milestoneLevelX, _choiceNodeOffsetY);
    }

    public void RefreshConnectorLayout()
    {
        if (choiceNodesContainer == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(choiceNodesContainer);
        Canvas.ForceUpdateCanvases();

        ApplyNodesContainerPreferredSize();
        LayoutRebuilder.ForceRebuildLayoutImmediate(choiceNodesContainer);

        LayoutConnectors();
        LayoutChoiceHintLabel();
        _afterConnectorLayout?.Invoke(this);
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

    /// <summary>Extra length for the spine stem so gold continues through the horizontal branch.</summary>
    public float GetSpineStemExtensionBelowBranch() =>
        SkillTimelineLineStyle.LineThickness + branchAboveNodesGap + centerStemAboveBranch + SkillTimelineLineStyle.LineThickness;

    private float GetSelectedDropTopOverlap() =>
        SkillTimelineLineStyle.LineThickness + centerStemAboveBranch;

    public float GetConnectorCornerOverlap() => connectorCornerOverlap;

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
        if (connectorOverlay == null || choiceNodesContainer == null)
            return;

        if (!TryCollectNodeBoundsInOverlay(out List<Bounds> nodeBoundsList, out _, out float containerWidth, out float containerHeight, out float branchY))
        {
            SetChoiceConnectorCount(0);
            HidePrefabConnectorLines();
            if (branchLine != null)
                branchLine.gameObject.SetActive(false);
            if (stemLine != null)
                stemLine.gameObject.SetActive(false);
            return;
        }

        AlignConnectorOverlay(containerWidth, containerHeight);

        int nodeCount = nodeBoundsList.Count;
        float junctionX = (nodeBoundsList[0].center.x + nodeBoundsList[nodeCount - 1].center.x) * 0.5f;
        int selectedIndex = _connectorSelectionSlotIndex;

        _lastBranchY = branchY;
        _lastBranchCenterX = junctionX;

        int lineCount = nodeCount + Mathf.Max(0, nodeCount - 1);
        SetChoiceConnectorCount(lineCount);

        if (branchLine != null)
            branchLine.gameObject.SetActive(false);

        for (int i = 0; i < nodeCount - 1; i++)
        {
            RectTransform segment = _choiceConnectorLines[nodeCount + i];
            float x0 = nodeBoundsList[i].center.x - connectorCornerOverlap;
            float x1 = nodeBoundsList[i + 1].center.x + connectorCornerOverlap;
            segment.gameObject.SetActive(true);
            SkillTimelineLineStyle.ApplyHorizontalBarBetween(segment, x0, x1, branchY, useProgressColor: false);
        }

        for (int i = 0; i < nodeCount; i++)
        {
            Bounds nodeBounds = nodeBoundsList[i];
            float nodeCenterX = nodeBounds.center.x;
            bool isSelected = selectedIndex == i;
            float nodeAttachY = nodeBounds.max.y - GetNodeConnectorReachIntoNode(isSelected) - nodeConnectorEndInset;
            float dropHeight = branchY - nodeAttachY;
            float branchHalfThickness = (isSelected ? SkillTimelineLineStyle.ProgressThickness : SkillTimelineLineStyle.LineThickness) * 0.5f;
            float topOverlap = branchHalfThickness + connectorTopOverlap + (isSelected ? selectedConnectorTopExtraOverlap : 0f);

            RectTransform drop = _choiceConnectorLines[i];
            bool showDrop = dropHeight > 0.5f;
            drop.gameObject.SetActive(showDrop);
            if (!showDrop)
                continue;

            SkillTimelineLineStyle.ApplyVerticalBar(
                drop,
                nodeCenterX,
                branchY + topOverlap,
                dropHeight + topOverlap,
                isSelected);
        }

        ApplySelectedPathHorizontal(nodeBoundsList, branchY, junctionX, selectedIndex);

        for (int i = 0; i < lineCount; i++)
        {
            if (_choiceConnectorLines[i] == null)
                continue;
            _choiceConnectorLines[i].SetSiblingIndex(i);
        }

        if (stemLine != null && stemLine.gameObject.activeSelf)
            stemLine.SetAsLastSibling();

        if (selectedIndex >= 0 && selectedIndex < nodeCount)
        {
            RectTransform selectedDrop = _choiceConnectorLines[selectedIndex];
            if (selectedDrop != null && selectedDrop.gameObject.activeSelf)
                selectedDrop.SetAsLastSibling();
        }
    }

    private void ApplySelectedPathHorizontal(
        List<Bounds> nodeBoundsList,
        float branchY,
        float junctionX,
        int selectedIndex)
    {
        if (stemLine == null)
            return;

        stemLine.gameObject.SetActive(false);

        if (selectedIndex < 0 || selectedIndex >= nodeBoundsList.Count)
            return;

        float selectedX = nodeBoundsList[selectedIndex].center.x;
        if (Mathf.Abs(selectedX - junctionX) <= SkillTimelineLineStyle.LineThickness * 0.5f)
            return;

        float selectedHalfThickness = SkillTimelineLineStyle.ProgressThickness * 0.5f;
        float x0;
        float x1;
        if (selectedX < junctionX)
        {
            x0 = selectedX + selectedHalfThickness - connectorCornerOverlap;
            x1 = junctionX + connectorCornerOverlap + selectedConnectorExtraJunctionOverlap;
        }
        else
        {
            x0 = junctionX - connectorCornerOverlap - selectedConnectorExtraJunctionOverlap;
            x1 = selectedX - selectedHalfThickness + connectorCornerOverlap;
        }

        if (x1 - x0 <= 0.25f)
            return;

        stemLine.gameObject.SetActive(true);
        SkillTimelineLineStyle.ApplyHorizontalBarBetween(stemLine, x0, x1, branchY, useProgressColor: true);
        stemLine.SetAsLastSibling();
    }

    private float GetNodeConnectorReachIntoNode(bool isSelected = false)
    {
        float reach = nodeConnectorReachIntoNode;
        if (_configuredNodeType == SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive)
            reach += majorPassiveConnectorExtraReachIntoNode;
        if (isSelected)
            reach += selectedConnectorExtraReachIntoNode;

        return reach;
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
        if (!TryCollectNodeBoundsInOverlay(out _, out _, out containerWidth, out _, out branchY))
            return false;

        if (TryCollectNodeBoundsInOverlay(out List<Bounds> bounds, out _, out _, out _, out _))
        {
            bounds.Sort((a, b) => a.center.x.CompareTo(b.center.x));
            branchCenterX = (bounds[0].center.x + bounds[bounds.Count - 1].center.x) * 0.5f;
        }

        return true;
    }

    private bool TryCollectNodeBoundsInOverlay(
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

        if (choiceNodesContainer == null || connectorOverlay == null)
            return false;

        bool hasAny = false;
        for (int i = 0; i < choiceNodesContainer.childCount; i++)
        {
            Transform child = choiceNodesContainer.GetChild(i);
            if (child == connectorOverlay)
                continue;
            if (!child.TryGetComponent(out SkillTimelineNodeUI _))
                continue;

            Bounds bounds = GetNodeChromeBoundsInOverlay(child);
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

    private Bounds GetNodeChromeBoundsInOverlay(Transform nodeTransform)
    {
        if (nodeTransform is RectTransform nodeRt && connectorOverlay != null)
            return RectTransformUtility.CalculateRelativeRectTransformBounds(connectorOverlay, nodeRt);

        return default;
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
        else if (stemLineImage == null)
            stemLineImage = stemLine.GetComponent<Image>();

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
