using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    private static readonly Color ConnectorColor = new(63f / 255f, 58f / 255f, 50f / 255f, 1f);

    private static Sprite _connectorSprite;

    private static Sprite ResolveConnectorSprite()
    {
        if (_connectorSprite != null)
            return _connectorSprite;

        _connectorSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f);
        return _connectorSprite;
    }

    [Header("References")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private RectTransform connectorOverlay;
    [SerializeField] private RectTransform stemLine;
    [SerializeField] private Image stemLineImage;
    [SerializeField] private RectTransform branchLine;
    [SerializeField] private Image branchLineImage;
    [SerializeField] private RectTransform choiceNodesContainer;
    [SerializeField] private HorizontalLayoutGroup nodesLayout;
    [SerializeField] private TMP_Text choiceHintLabel;

    [Header("Layout")]
    [SerializeField] private float nodeSpacing = DefaultNodeSpacing;
    [SerializeField] private float connectorThickness = 2f;
    [SerializeField] private float stemSpineInset = 5f;
    [SerializeField] private float branchAboveNodesGap = 2f;
    [SerializeField] private float nodeConnectorEndInset = 0f;
    [SerializeField] private float centerStemAboveBranch = 2f;
    [Tooltip("Optional subtle hint above the branch. Off by default.")]
    [SerializeField] private bool showChoiceHintLabel;

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
    }
#endif

    public void Configure(
        int level,
        float milestoneLevelX,
        float spineY,
        float groupAnchorY,
        string[] nodeNames,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillTimelineNodeUI nodePrefab)
    {
        EnsureHierarchy();

        if (nodeNames == null || nodeNames.Length < MinChoiceCount)
        {
            Debug.LogWarning($"[SkillChoiceGroupUI] Lv{level} needs at least {MinChoiceCount} choices.", this);
            return;
        }

        if (nodeNames.Length > MaxChoiceCount)
            Debug.LogWarning($"[SkillChoiceGroupUI] Lv{level} clamped to {MaxChoiceCount} choices.", this);

        int count = Mathf.Clamp(nodeNames.Length, MinChoiceCount, MaxChoiceCount);

        RectTransform rt = RectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(milestoneLevelX, 0f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        ClearNodes();
        ApplyChoiceHintLabel(count);
        PopulateNodes(level, nodeNames, count, nodeType, nodePrefab);
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

        LayoutConnectors();
        LayoutChoiceHintLabel();
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
        spineAttach = new Vector2(branchAttach.x, spineY - stemSpineInset);
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
        string[] nodeNames,
        int count,
        SkillTimelineNodeUI.SkillTimelineNodeType nodeType,
        SkillTimelineNodeUI nodePrefab)
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

            node.ApplyChoiceGroupNodePreview(nodeType, nodeNames[i]);
            _spawnedNodes.Add(node);
        }
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
            branchLine.gameObject.SetActive(false);
            if (stemLine != null)
                stemLine.gameObject.SetActive(false);
            return;
        }

        AlignConnectorOverlay(containerWidth, containerHeight);

        nodeBoundsList.Sort((a, b) => a.center.x.CompareTo(b.center.x));
        float branchLeftX = nodeBoundsList[0].center.x;
        float branchRightX = nodeBoundsList[nodeBoundsList.Count - 1].center.x;
        float branchSpanWidth = Mathf.Max(connectorThickness, branchRightX - branchLeftX);
        float branchCenterX = (branchLeftX + branchRightX) * 0.5f;

        _lastBranchY = branchY;
        _lastBranchCenterX = branchCenterX;

        branchLine.gameObject.SetActive(true);
        branchLine.anchorMin = branchLine.anchorMax = new Vector2(0.5f, 0.5f);
        branchLine.pivot = new Vector2(0.5f, 0.5f);
        branchLine.anchoredPosition = new Vector2(branchCenterX, branchY);
        branchLine.sizeDelta = new Vector2(branchSpanWidth, connectorThickness);
        ApplyConnectorImage(branchLineImage);

        if (stemLine != null)
        {
            stemLine.gameObject.SetActive(true);
            stemLine.anchorMin = stemLine.anchorMax = new Vector2(0.5f, 0f);
            stemLine.pivot = new Vector2(0.5f, 0f);
            stemLine.anchoredPosition = new Vector2(branchCenterX, branchY);
            stemLine.sizeDelta = new Vector2(connectorThickness, centerStemAboveBranch);
            stemLine.SetSiblingIndex(0);
            ApplyConnectorImage(stemLineImage);
        }

        SetChoiceConnectorCount(nodeBoundsList.Count);
        for (int i = 0; i < nodeBoundsList.Count; i++)
        {
            Bounds nodeBounds = nodeBoundsList[i];
            float nodeCenterX = nodeBounds.center.x;
            float nodeTopY = nodeBounds.max.y - nodeConnectorEndInset;
            float dropHeight = Mathf.Max(2f, branchY - nodeTopY);

            RectTransform drop = _choiceConnectorLines[i];
            drop.gameObject.SetActive(dropHeight > 0.5f);
            drop.anchorMin = drop.anchorMax = new Vector2(0.5f, 0.5f);
            drop.pivot = new Vector2(0.5f, 1f);
            drop.anchoredPosition = new Vector2(nodeCenterX, branchY);
            drop.sizeDelta = new Vector2(connectorThickness, dropHeight);
            drop.SetAsLastSibling();
            ApplyConnectorImage(_choiceConnectorImages[i]);
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

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(choiceNodesContainer, child);
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

    private static void ApplyConnectorImage(Image image)
    {
        if (image == null)
            return;

        image.sprite = ResolveConnectorSprite();

        image.type = Image.Type.Simple;
        image.color = ConnectorColor;
        image.raycastTarget = false;
        image.maskable = true;
        image.enabled = true;
    }

    private void EnsureHierarchy()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;

        MigrateLegacyHierarchyNames();

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

        CleanupLegacyHierarchyObjects();
        EnsureConnectorOverlayHierarchy();
    }

    private void CleanupLegacyHierarchyObjects()
    {
        DestroyLegacyChild(rectTransform, "SpineStemLine");
        DestroyLegacyChild(rectTransform, "ChoiceLabel");

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
        else
            ApplyConnectorImage(stemLineImage != null ? stemLineImage : stemLine.GetComponent<Image>());

        if (branchLine != null)
            ApplyConnectorImage(branchLineImage != null ? branchLineImage : branchLine.GetComponent<Image>());

        for (int i = 0; i < _choiceConnectorLines.Count; i++)
        {
            if (_choiceConnectorImages[i] != null)
                ApplyConnectorImage(_choiceConnectorImages[i]);
        }

        connectorOverlay.SetAsLastSibling();
    }

    private static RectTransform CreateConnectorLineRect(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(2f, 2f);
        ApplyConnectorImage(go.GetComponent<Image>());
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
