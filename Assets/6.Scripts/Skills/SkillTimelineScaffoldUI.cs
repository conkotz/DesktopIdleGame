using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a static horizontal skill-timeline layout under SkillsAbilityPageNEW.
/// Visual scaffold only — no unlock, save, or selection wiring.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class SkillTimelineScaffoldUI : MonoBehaviour
{
    public const string UnlockRowName = "UnlockRow";
    public const string SpineRowName = "SpineRow";
    public const string ChoiceRowName = "ChoiceRow";
    public const string MinorLevelTicksName = "MinorLevelTicks";
    public const string MilestoneLevelTicksName = "MilestoneLevelTicks";
    public const string PrefabConnectorsRootName = "PrefabConnectors";

    private const string LegacyScaffoldRootName = "ScaffoldRoot";

    private const float ContentWidth = 5000f;
    private const float ContentHeight = 300f;
    private const float PaddingLeft = 220f;
    private const float PaddingRight = 80f;
    /// <summary>Fallback TimelineContent Y when not set in the inspector.</summary>
    private const float DefaultTimelineContentYOffset = 4f;
    private const float DefaultSpineRowY = 24f;
    private const float DefaultUnlockRowY = 90f;
    private const float DefaultChoiceRowY = -54f;
    private const float SpineLocalY = 0f;
    /// <summary>Main spine track + milestone connectors + choice-group branch/drops (shared thickness).</summary>
    public const float TimelineConnectorThickness = SkillTimelineLineStyle.LineThickness;
    /// <summary>Gold progress overlay on the spine — slightly thicker than <see cref="TimelineConnectorThickness"/>.</summary>
    public const float TimelineSpineProgressThickness = SkillTimelineLineStyle.ProgressThickness;
    private const float MilestoneLabelFontSize = 14f;
    private const float MinorNodeLabelFontSize = 11f;
    private const float GeneralNodeLabelFontSize = 13f;
    private const float UnlockRowHeight = 108f;
    private const float SpineRowHeight = 72f;
    private const float ChoiceRowHeight = 118f;
    private const float MilestoneLabelYOffset = 22f;
    private const float MilestoneLabelXOffset = 6f;
    private const float MinorDiamondSize = 17f;
    private const float ChoiceCardWidth = 110f;
    private const float ChoiceCardHeight = 50f;
    private const float DefaultChoiceSpread = 118f;
    private const float StandardPrefabNodeSize = 56f;
    private const float LevelTickWidth = 2f;
    /// <summary>Uniform upward stem above the spine for milestone levels (Lv 1, 5, 10, …).</summary>
    private const float StandardLevelUpwardStemHeight = 26f;
    /// <summary>Small tick centered on the spine between milestone levels (Lv 2–4, 6–9, …).</summary>
    private const float MinorLevelTickHeight = 10f;
    private const float DefaultHelperBarHeight = 60f;
    private const float DefaultHorizontalScrollbarHeight = 22f;
    private const float ScrollbarHandleMinWidth = 48f;
    /// <summary>Vertical stems from the spine to milestone nodes (matches choice-group branch lines).</summary>
    public const float MilestoneSpineConnectorThickness = TimelineConnectorThickness;
    private const float ConnectorExtendSpine = 5f;
    private const float ConnectorExtendNode = 10f;
    public const float ConnectorSpineOverlap = 2f;
    /// <summary>Gold spine stems extend past the branch junction to hide black corner pixels.</summary>
    public const float GoldSpineConnectorExtendPastBranch = 2f;

    private static readonly Color ScrollbarTrackColor = new(0.38f, 0.32f, 0.26f, 1f);
    private static readonly Color ScrollbarHandleColor = new(0.72f, 0.64f, 0.48f, 1f);

    private static readonly Color SpineProgressColor = new(0.85f, 0.72f, 0.35f, 1f);
    private static readonly Color TickColor = new(0.22f, 0.18f, 0.14f, 1f);
    private static readonly Color MinorPassiveColor = new(0.26f, 0.53f, 0.82f, 1f);
    private static readonly Color AbilityColor = new(0.22f, 0.62f, 0.38f, 1f);
    private static readonly Color MajorPassiveColor = new(0.58f, 0.32f, 0.76f, 1f);
    private static readonly Color UnlockColor = new(0.95f, 0.78f, 0.22f, 1f);
    private static readonly Color CardBgColor = new(0.12f, 0.1f, 0.08f, 0.88f);
    private static readonly Color LabelColor = new(0.95f, 0.92f, 0.86f, 1f);
    private static readonly Color MilestoneLabelColor = new(0.2f, 0.14f, 0.1f, 1f);

    [SerializeField] private bool rebuildOnEnable;
    [Tooltip("When false, only timeline chrome is built (spine, ticks, scroll). Use with HorizontalSkillTreeScaffoldUI prefab nodes.")]
    [SerializeField] private bool buildPlaceholderNodes = true;

    [Header("Timeline rows (TimelineContent local Y)")]
    [Tooltip("Shifts unlock / spine / choice rows together (positive = up). Keeps spacing between rows.")]
    [SerializeField] private float timelineRowsOffsetY = 12f;
    [SerializeField] private float unlockRowAnchoredY = DefaultUnlockRowY;
    [SerializeField] private float spineRowAnchoredY = DefaultSpineRowY;
    [SerializeField] private float choiceRowAnchoredY = DefaultChoiceRowY;
    [Tooltip("Whole TimelineContent anchor Y (positive = shift tree up in viewport).")]
    [SerializeField] private float timelineContentOffsetY = DefaultTimelineContentYOffset;

    [Header("Timeline chrome (HelperBar / scrollbar)")]
    [Tooltip("Fallback height only. At runtime the HelperBar RectTransform height saved in the scene is used — resize HelperBar, save the scene, not the old 60 default here.")]
    [SerializeField] private float helperBarHeight = DefaultHelperBarHeight;
    [SerializeField] private float horizontalScrollbarHeight = DefaultHorizontalScrollbarHeight;

    private void OnEnable()
    {
        if (!rebuildOnEnable)
            return;

        // Runtime prefab trees use HorizontalSkillTreeScaffoldUI — avoid wiping saved scene chrome on open.
        if (Application.isPlaying && !buildPlaceholderNodes)
            return;

        RebuildScaffold();
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Timeline Scaffold")]
    private void EditorRebuild() => RebuildScaffold();
#endif

    public void RebuildScaffold(bool notifyHorizontalTree = true)
    {
        RectTransform container = transform as RectTransform;
        if (container == null)
            return;

        RectTransform viewport = FindChildRect(container, "TimelineViewport");
        RectTransform content = FindChildRect(viewport, "TimelineContent");
        if (viewport == null || content == null)
        {
            Debug.LogWarning("[SkillTimelineScaffoldUI] TimelineViewport or TimelineContent not found.", this);
            return;
        }

        EnsureTimelineChrome(container, viewport, content);
        PrepareContentRect(content);
        ClearLegacyLayout(content);

        RectTransform unlockRow = EnsureRow(content, UnlockRowName, unlockRowAnchoredY, UnlockRowHeight);
        RectTransform spineRow = EnsureRow(content, SpineRowName, spineRowAnchoredY, SpineRowHeight);
        RectTransform choiceRow = EnsureRow(content, ChoiceRowName, choiceRowAnchoredY, ChoiceRowHeight);

        ClearSpineChrome(spineRow);
        BuildSpine(spineRow);
        BuildLevelTicks(spineRow);

        if (buildPlaceholderNodes)
        {
            RectTransform connectors = GetOrCreatePrefabConnectorsLayer(content);
            BuildPlaceholderNodesInRows(unlockRow, spineRow, choiceRow, connectors);
        }

        if (!buildPlaceholderNodes && notifyHorizontalTree)
        {
            var horizontal = container.GetComponent<HorizontalSkillTreeScaffoldUI>();
            horizontal?.OnScaffoldRebuilt(content);
        }
    }

    public RectTransform GetUnlockRow(RectTransform content) => FindRow(content, UnlockRowName);

    public RectTransform GetSpineRow(RectTransform content) => FindRow(content, SpineRowName);

    public RectTransform GetChoiceRow(RectTransform content) => FindRow(content, ChoiceRowName);

    /// <summary>Removes layout groups from content so prefab nodes can use absolute positions.</summary>
    public void PrepareContentForAbsoluteNodes(RectTransform content)
    {
        if (content == null)
            return;
        PrepareContentRect(content);
    }

    /// <summary>Rebuilds spine line + uniform level ticks (runtime refresh when tick layout code changes).</summary>
    public void RefreshSpineChrome(RectTransform spineRow, IEnumerable<int> skipMinorTickAtLevels = null)
    {
        if (spineRow == null)
            return;

        var skipLevels = skipMinorTickAtLevels as HashSet<int> ?? (
            skipMinorTickAtLevels != null ? new HashSet<int>(skipMinorTickAtLevels) : null);

        ClearSpineChrome(spineRow);
        BuildSpine(spineRow);
        BuildLevelTicks(spineRow, skipLevels);
        ApplySpineRowDrawOrder(spineRow);
    }

    public float GetTimelineLevelX(int level) => XForLevel(level);

    /// <summary>Lv 1, Lv 5, Lv 10, … — levels that show a label and may have below-spine milestone content.</summary>
    public static bool IsLabeledMilestoneLevel(int level) => level == 1 || level % 5 == 0;

    /// <summary>Spine line → progress → minor ticks (behind nodes) → minor nodes → milestone ticks.</summary>
    public static void ApplySpineRowDrawOrder(RectTransform spineRow)
    {
        if (spineRow == null)
            return;

        Transform spineLine = spineRow.Find("SpineLine");
        Transform progress = spineRow.Find("SpineProgressLine");
        Transform minorTicks = spineRow.Find(MinorLevelTicksName);
        Transform milestoneTicks = spineRow.Find(MilestoneLevelTicksName);
        Transform legacyTicks = spineRow.Find("LevelTicks");

        var minorNodes = new List<Transform>();
        for (int i = 0; i < spineRow.childCount; i++)
        {
            Transform child = spineRow.GetChild(i);
            if (child == spineLine || child == progress || child == minorTicks ||
                child == milestoneTicks || child == legacyTicks)
                continue;
            if (child.GetComponent<SkillTimelineNodeUI>() != null)
                minorNodes.Add(child);
        }

        int index = 0;
        if (spineLine != null)
            spineLine.SetSiblingIndex(index++);
        if (progress != null)
            progress.SetSiblingIndex(index++);
        if (minorTicks != null)
            minorTicks.SetSiblingIndex(index++);
        if (legacyTicks != null)
            legacyTicks.SetSiblingIndex(index++);

        for (int i = 0; i < minorNodes.Count; i++)
            minorNodes[i].SetSiblingIndex(index++);

        if (milestoneTicks != null)
            milestoneTicks.SetSiblingIndex(index++);
    }

    /// <summary>Local Y for spine minor nodes — centered on the main spine line.</summary>
    public static float SpineMinorNodeLocalY => SpineLocalY;

    /// <summary>Overlays a thicker golden progress strip on the spine up to <paramref name="playerLevel"/>.</summary>
    public void UpdateSpineProgress(int playerLevel)
    {
        RectTransform container = transform as RectTransform;
        if (container == null)
            return;

        RectTransform viewport = FindChildRect(container, "TimelineViewport");
        RectTransform content = FindChildRect(viewport, "TimelineContent");
        RectTransform spineRow = FindRow(content, SpineRowName);
        if (spineRow == null)
            return;

        ClearSpineProgress(spineRow);
        BuildSpineProgress(spineRow, playerLevel);
        ApplySpineRowDrawOrder(spineRow);
    }

    public float TimelineSpineY => spineRowAnchoredY;

    public float TimelineUnlockRowY => unlockRowAnchoredY;

    public float TimelineChoiceRowY => choiceRowAnchoredY;

    /// <summary>
    /// Main spine line center Y in <paramref name="timelineContent"/> local space.
    /// Uses the live SpineLine rect so row/content offsets stay aligned with cross-row connectors.
    /// </summary>
    public float ResolveSpineLineYInContent(RectTransform timelineContent)
    {
        if (timelineContent == null)
            return spineRowAnchoredY + SpineMinorNodeLocalY;

        RectTransform spineRow = FindRow(timelineContent, SpineRowName);
        if (spineRow == null)
            return spineRowAnchoredY + SpineMinorNodeLocalY;

        Transform spineLine = spineRow.Find("SpineLine");
        if (spineLine is RectTransform spineLineRt)
        {
            Vector3 worldCenter = spineLineRt.TransformPoint(spineLineRt.rect.center);
            return timelineContent.InverseTransformPoint(worldCenter).y;
        }

        Vector3 rowPoint = spineRow.TransformPoint(new Vector3(0f, SpineMinorNodeLocalY, 0f));
        return timelineContent.InverseTransformPoint(rowPoint).y;
    }

    /// <summary>Updates row Y positions without clearing spawned timeline nodes. Called by <see cref="HorizontalSkillTreeScaffoldUI"/> before each build.</summary>
    public void ApplyRowLayout(float spineRowY, float choiceRowY, float? unlockRowY = null)
    {
        float shift = timelineRowsOffsetY;
        spineRowAnchoredY = spineRowY + shift;
        choiceRowAnchoredY = choiceRowY + shift;
        if (unlockRowY.HasValue)
            unlockRowAnchoredY = unlockRowY.Value + shift;

        RepositionTimelineRows();
    }

    private void RepositionTimelineRows()
    {
        RectTransform container = transform as RectTransform;
        if (container == null)
            return;

        RectTransform viewport = FindChildRect(container, "TimelineViewport");
        RectTransform content = FindChildRect(viewport, "TimelineContent");
        if (content == null)
            return;

        EnsureRow(content, UnlockRowName, unlockRowAnchoredY, UnlockRowHeight);
        EnsureRow(content, SpineRowName, spineRowAnchoredY, SpineRowHeight);
        EnsureRow(content, ChoiceRowName, choiceRowAnchoredY, ChoiceRowHeight);
    }

    public float TimelineChoiceSpread => DefaultChoiceSpread;

    public float TimelineStandardNodeHalfHeight => SkillTimelineNodeUI.StandardNodeHalfHeight;

    public RectTransform GetConnectorsLayer(RectTransform content) => GetOrCreatePrefabConnectorsLayer(content);

    /// <summary>Cross-row connector overlay on TimelineContent (content-space coordinates).</summary>
    public RectTransform GetOrCreatePrefabConnectorsLayer(RectTransform content)
    {
        if (content == null)
            return null;

        PrepareContentRect(content);

        Transform existing = content.Find(PrefabConnectorsRootName);
        if (existing != null)
            return (RectTransform)existing;

        var layer = (RectTransform)CreateRect(content, PrefabConnectorsRootName, Vector2.zero, new Vector2(ContentWidth, ContentHeight));
        layer.anchorMin = layer.anchorMax = new Vector2(0f, 0.5f);
        layer.pivot = new Vector2(0f, 0.5f);
        layer.anchoredPosition = Vector2.zero;
        layer.SetAsFirstSibling();
        return layer;
    }

    public static void ClearConnectorChildren(RectTransform connectorsLayer)
    {
        if (connectorsLayer == null)
            return;

        for (int i = connectorsLayer.childCount - 1; i >= 0; i--)
            DestroyImmediateSafe(connectorsLayer.GetChild(i).gameObject);
    }

    public void DrawConnector(
        RectTransform connectorsLayer,
        Vector2 start,
        Vector2 end,
        float extendBeyondStart = 0f,
        float extendBeyondEnd = 0f,
        float thickness = MilestoneSpineConnectorThickness,
        bool useProgressColor = false)
    {
        if (connectorsLayer == null)
            return;
        CreateConnector(connectorsLayer, start, end, extendBeyondStart, extendBeyondEnd, thickness, useProgressColor);
    }

    /// <summary>
    /// Viewport = scrollable timeline only. Helper bar and scrollbar sit below it (not masked).
    /// </summary>
    private void EnsureTimelineChrome(RectTransform container, RectTransform viewport, RectTransform content)
    {
        float helperHeight = ResolveHelperBarHeight(container);
        float scrollbarHeight = horizontalScrollbarHeight;
        float bottomReserved = helperHeight + scrollbarHeight;

        EnsureHelperOutsideViewport(container, viewport, helperHeight);

        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = Vector2.zero;
        viewport.offsetMin = new Vector2(0f, bottomReserved);
        viewport.offsetMax = Vector2.zero;

        ScrollRect scroll = container.GetComponent<ScrollRect>();
        if (scroll == null)
            scroll = container.gameObject.AddComponent<ScrollRect>();

        Scrollbar hBar = EnsureHorizontalScrollbar(container, helperHeight, scrollbarHeight);
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        scroll.horizontalScrollbar = hBar;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        EnsureChromeDrawOrder(container);
    }

    private void EnsureHelperOutsideViewport(RectTransform container, RectTransform viewport, float height)
    {
        RectTransform helper = FindChildRect(container, "HelperBar");
        if (helper == null)
            return;

        if (helper.parent != container)
            helper.SetParent(container, false);

        Vector2 savedPos = helper.anchoredPosition;
        float savedWidth = helper.sizeDelta.x;

        helper.anchorMin = new Vector2(0f, 0f);
        helper.anchorMax = new Vector2(1f, 0f);
        helper.pivot = new Vector2(0.5f, 0f);
        helper.anchoredPosition = savedPos;
        helper.sizeDelta = new Vector2(savedWidth, height);

        if (viewport != null && viewport.GetSiblingIndex() > helper.GetSiblingIndex())
            viewport.SetAsFirstSibling();
    }

    /// <summary>Scene HelperBar height wins (saved in GamePlay). Inspector fallback used only when the rect has no height.</summary>
    private float ResolveHelperBarHeight(RectTransform container)
    {
        RectTransform helper = FindChildRect(container, "HelperBar");
        if (helper != null && helper.sizeDelta.y >= 1f)
        {
            helperBarHeight = helper.sizeDelta.y;
            return helperBarHeight;
        }

        return Mathf.Max(1f, helperBarHeight);
    }

    /// <summary>SkillLevelPanel stays above the scroll viewport so Play mode does not visually cover the Melee label.</summary>
    private static void EnsureChromeDrawOrder(RectTransform container)
    {
        if (container == null)
            return;

        RectTransform viewport = FindChildRect(container, "TimelineViewport");
        Transform scrollbar = container.Find("TimelineScrollbarHorizontal");
        RectTransform helper = FindChildRect(container, "HelperBar");
        RectTransform skillLevelPanel = FindChildRect(container, "SkillLevelPanel");

        int index = 0;
        if (viewport != null)
            viewport.SetSiblingIndex(index++);
        if (helper != null)
            helper.SetSiblingIndex(index++);
        if (skillLevelPanel != null)
        {
            skillLevelPanel.SetSiblingIndex(index++);
            if (skillLevelPanel.GetComponent<SkillLevelPanelHoverDimUI>() == null)
                skillLevelPanel.gameObject.AddComponent<SkillLevelPanelHoverDimUI>();
        }
        if (scrollbar != null)
            scrollbar.SetSiblingIndex(index++);
    }

    private Scrollbar EnsureHorizontalScrollbar(RectTransform container, float helperHeight, float scrollbarHeight)
    {
        Transform existing = container.Find("TimelineScrollbarHorizontal");
        if (existing != null && existing.TryGetComponent(out Scrollbar bar))
        {
            LayoutScrollbar((RectTransform)existing, helperHeight, scrollbarHeight);
            if (existing.TryGetComponent(out Image trackImg))
                StyleScrollbarChrome(trackImg, bar);
            return bar;
        }

        var barGo = new GameObject("TimelineScrollbarHorizontal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.SetParent(container, false);
        LayoutScrollbar(barRt, helperHeight, scrollbarHeight);

        var scrollbar = barGo.GetComponent<Scrollbar>();
        StyleScrollbarChrome(barGo.GetComponent<Image>(), scrollbar);
        return scrollbar;
    }

    private static void LayoutScrollbar(RectTransform barRt, float helperHeight, float scrollbarHeight)
    {
        if (barRt == null)
            return;

        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(1f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.sizeDelta = new Vector2(-12f, scrollbarHeight);
        barRt.anchoredPosition = new Vector2(0f, helperHeight);
        barRt.SetSiblingIndex(Mathf.Max(0, barRt.parent.childCount - 2));
    }

#if UNITY_EDITOR
    private bool _deferredRowLayoutRefreshQueued;

    private void OnValidate()
    {
        // Preserve the scene exactly as saved when the project reopens.
        // Editor-time row/layout refreshes are available via explicit rebuild tools/context menus instead.
    }

    private void QueueDeferredRowLayoutRefresh()
    {
        if (!rebuildOnEnable || !isActiveAndEnabled || _deferredRowLayoutRefreshQueued)
            return;

        _deferredRowLayoutRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += EditorDeferredRowLayoutRefresh;
    }

    private void EditorDeferredRowLayoutRefresh()
    {
        UnityEditor.EditorApplication.delayCall -= EditorDeferredRowLayoutRefresh;
        _deferredRowLayoutRefreshQueued = false;

        if (this == null || !isActiveAndEnabled)
            return;

        RectTransform container = transform as RectTransform;
        if (container == null)
            return;

        RectTransform viewport = FindChildRect(container, "TimelineViewport");
        RectTransform content = FindChildRect(viewport, "TimelineContent");
        if (content == null)
            return;

        PrepareContentForAbsoluteNodes(content);
        RepositionTimelineRows();

        var horizontal = GetComponent<HorizontalSkillTreeScaffoldUI>();
        if (horizontal != null)
            horizontal.RefreshConnectorsOnly();
    }
#endif

    private static void StyleScrollbarChrome(Image trackImg, Scrollbar scrollbar)
    {
        if (trackImg != null)
        {
            trackImg.color = ScrollbarTrackColor;
            trackImg.raycastTarget = true;
        }

        if (scrollbar == null)
            return;

        Transform slide = scrollbar.transform.Find("Sliding Area");
        if (slide == null)
        {
            slide = CreateRect(scrollbar.transform, "Sliding Area", Vector2.zero, Vector2.zero);
            var slideRt = (RectTransform)slide;
            Stretch(slideRt);
        }

        var slideRt2 = (RectTransform)slide;
        slideRt2.offsetMin = new Vector2(10f, 4f);
        slideRt2.offsetMax = new Vector2(-10f, -4f);
        if (slide.GetComponent<RectMask2D>() == null)
            slide.gameObject.AddComponent<RectMask2D>();

        Transform handle = slide.Find("Handle");
        RectTransform handleRt;
        Image handleImg;
        if (handle == null)
        {
            handle = CreateRect(slideRt2, "Handle", Vector2.zero, Vector2.zero);
            handleRt = (RectTransform)handle;
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(0f, 1f);
            handleRt.pivot = new Vector2(0f, 0.5f);
            handleRt.sizeDelta = new Vector2(ScrollbarHandleMinWidth, 0f);
            handleImg = handle.gameObject.AddComponent<Image>();
        }
        else
        {
            handleRt = (RectTransform)handle;
            handleImg = handle.GetComponent<Image>();
            if (handleImg == null)
                handleImg = handle.gameObject.AddComponent<Image>();
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(0f, 1f);
            handleRt.pivot = new Vector2(0f, 0.5f);
            handleRt.sizeDelta = new Vector2(ScrollbarHandleMinWidth, 0f);
        }

        if (handleImg != null)
        {
            handleImg.color = ScrollbarHandleColor;
            handleImg.raycastTarget = true;
        }

        if (scrollbar != null && scrollbar.GetComponent<TimelineScrollbarInputGuard>() == null)
            scrollbar.gameObject.AddComponent<TimelineScrollbarInputGuard>();

        scrollbar.handleRect = handleRt;
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scrollbar.size = Mathf.Clamp01(scrollbar.size <= 0.001f ? 0.2f : scrollbar.size);
    }

    private void PrepareContentRect(RectTransform content)
    {
        if (content.TryGetComponent(out HorizontalLayoutGroup hlg))
            DestroyImmediateSafe(hlg);
        if (content.TryGetComponent(out ContentSizeFitter csf))
            DestroyImmediateSafe(csf);

        content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = new Vector2(0f, timelineContentOffsetY);
        content.sizeDelta = new Vector2(ContentWidth, ContentHeight);
    }

    private static void ClearLegacyLayout(RectTransform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child.name == LegacyScaffoldRootName)
                DestroyImmediateSafe(child.gameObject);
        }
    }

    private static RectTransform EnsureRow(RectTransform content, string rowName, float anchoredY, float height)
    {
        RectTransform existing = FindRow(content, rowName);
        if (existing != null)
        {
            existing.anchorMin = existing.anchorMax = new Vector2(0f, 0.5f);
            existing.pivot = new Vector2(0f, 0.5f);
            existing.anchoredPosition = new Vector2(0f, anchoredY);
            existing.sizeDelta = new Vector2(ContentWidth, height);
            return existing;
        }

        var row = (RectTransform)CreateRect(content, rowName, new Vector2(0f, anchoredY), new Vector2(ContentWidth, height));
        row.anchorMin = row.anchorMax = new Vector2(0f, 0.5f);
        row.pivot = new Vector2(0f, 0.5f);
        return row;
    }

    private static RectTransform FindRow(RectTransform content, string rowName)
    {
        if (content == null || string.IsNullOrEmpty(rowName))
            return null;
        return content.Find(rowName) as RectTransform;
    }

    private static void ClearSpineChrome(RectTransform spineRow)
    {
        if (spineRow == null)
            return;

        for (int i = spineRow.childCount - 1; i >= 0; i--)
        {
            Transform child = spineRow.GetChild(i);
            if (child.name == "SpineLine" || child.name == "SpineProgressLine" ||
                child.name == MinorLevelTicksName || child.name == MilestoneLevelTicksName ||
                child.name == "LevelTicks")
                DestroyImmediateSafe(child.gameObject);
        }
    }

    private static void ClearSpineProgress(RectTransform spineRow)
    {
        if (spineRow == null)
            return;

        Transform existing = spineRow.Find("SpineProgressLine");
        if (existing != null)
            DestroyImmediateSafe(existing.gameObject);
    }

    private static void BuildSpine(RectTransform root)
    {
        float left = XForLevel(1);
        float right = ContentWidth - PaddingRight;

        var spine = CreateRect(root, "SpineLine", new Vector2(left, SpineLocalY), new Vector2(right - left, TimelineConnectorThickness));
        var spineRt = (RectTransform)spine;
        spineRt.anchorMin = spineRt.anchorMax = new Vector2(0f, 0.5f);
        spineRt.pivot = new Vector2(0f, 0.5f);
        spineRt.SetAsFirstSibling();
        SkillTimelineLineStyle.Apply(spine.gameObject.AddComponent<Image>());
    }

    private static void BuildSpineProgress(RectTransform root, int playerLevel)
    {
        float left = XForLevel(1);
        float endX = XForLevel(Mathf.Clamp(playerLevel, 1, 50));
        float width = endX - left;
        if (width <= 0.5f)
            return;

        var progress = CreateRect(root, "SpineProgressLine", new Vector2(left, SpineLocalY), new Vector2(width, TimelineSpineProgressThickness));
        var progressRt = (RectTransform)progress;
        progressRt.anchorMin = progressRt.anchorMax = new Vector2(0f, 0.5f);
        progressRt.pivot = new Vector2(0f, 0.5f);
        int insertIndex = 0;
        Transform spineLine = root.Find("SpineLine");
        if (spineLine != null)
            insertIndex = spineLine.GetSiblingIndex() + 1;
        progressRt.SetSiblingIndex(insertIndex);
        var img = progress.gameObject.AddComponent<Image>();
        img.color = SpineProgressColor;
        img.raycastTarget = false;
    }

    private static void BuildLevelTicks(RectTransform root, HashSet<int> skipMinorTickAtLevels = null)
    {
        var minorTicksParent = CreateRect(root, MinorLevelTicksName, Vector2.zero, Vector2.zero);
        var minorTicksRt = (RectTransform)minorTicksParent;
        minorTicksRt.anchorMin = minorTicksRt.anchorMax = new Vector2(0f, 0.5f);
        minorTicksRt.pivot = new Vector2(0f, 0.5f);
        minorTicksRt.sizeDelta = new Vector2(ContentWidth, ContentHeight);

        var milestoneTicksParent = CreateRect(root, MilestoneLevelTicksName, Vector2.zero, Vector2.zero);
        var milestoneTicksRt = (RectTransform)milestoneTicksParent;
        milestoneTicksRt.anchorMin = milestoneTicksRt.anchorMax = new Vector2(0f, 0.5f);
        milestoneTicksRt.pivot = new Vector2(0f, 0.5f);
        milestoneTicksRt.sizeDelta = new Vector2(ContentWidth, ContentHeight);

        for (int level = 1; level <= 50; level++)
        {
            float x = XForLevel(level);

            if (IsLabeledMilestoneLevel(level))
            {
                var tick = CreateRect(
                    milestoneTicksRt,
                    $"Tick_Lv{level}",
                    new Vector2(x, SpineLocalY),
                    new Vector2(LevelTickWidth, StandardLevelUpwardStemHeight));
                var tickRt = (RectTransform)tick;
                tickRt.anchorMin = tickRt.anchorMax = new Vector2(0f, 0.5f);
                tickRt.pivot = new Vector2(0.5f, 0f);
                var tickImg = tick.gameObject.AddComponent<Image>();
                tickImg.color = TickColor;
                tickImg.raycastTarget = false;

                float labelX = x + MilestoneLabelXOffset;
                float labelY = SpineLocalY + MilestoneLabelYOffset;
                CreateMilestoneLabel(milestoneTicksRt, $"Lv {level}", new Vector2(labelX, labelY));
            }
            else if (skipMinorTickAtLevels == null || !skipMinorTickAtLevels.Contains(level))
            {
                var tick = CreateRect(
                    minorTicksRt,
                    $"Tick_Lv{level}",
                    new Vector2(x, SpineLocalY),
                    new Vector2(LevelTickWidth, MinorLevelTickHeight));
                var tickRt = (RectTransform)tick;
                tickRt.anchorMin = tickRt.anchorMax = new Vector2(0f, 0.5f);
                tickRt.pivot = new Vector2(0.5f, 0.5f);
                var tickImg = tick.gameObject.AddComponent<Image>();
                tickImg.color = TickColor;
                tickImg.raycastTarget = false;
            }
        }

        ApplySpineRowDrawOrder(root);
    }

    /// <summary>Hides non-milestone level ticks where a spine minor node occupies that level.</summary>
    public static void HideMinorTicksForLevels(RectTransform spineRow, IEnumerable<int> levelsWithMinorNodes)
    {
        if (spineRow == null || levelsWithMinorNodes == null)
            return;

        var hiddenLevels = levelsWithMinorNodes as HashSet<int> ?? new HashSet<int>(levelsWithMinorNodes);
        if (hiddenLevels.Count == 0)
            return;

        Transform[] tickTransforms = spineRow.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < tickTransforms.Length; i++)
        {
            Transform tick = tickTransforms[i];
            if (tick == spineRow || !tick.name.StartsWith("Tick_Lv", System.StringComparison.Ordinal))
                continue;

            if (!TryParseTickLevel(tick.name, out int level))
                continue;

            if (!hiddenLevels.Contains(level))
                continue;

            tick.gameObject.SetActive(false);
        }
    }

    private static bool TryParseTickLevel(string tickName, out int level)
    {
        level = 0;
        if (string.IsNullOrEmpty(tickName) || !tickName.StartsWith("Tick_Lv", System.StringComparison.Ordinal))
            return false;

        return int.TryParse(tickName.Substring("Tick_Lv".Length), out level);
    }

    private void BuildPlaceholderNodesInRows(
        RectTransform unlockRow,
        RectTransform spineRow,
        RectTransform choiceRow,
        RectTransform connectors)
    {
        CreateUnlockCard(unlockRow, connectors, 1, "Beginner Melee Combat");

        for (int level = 2; level <= 4; level++)
            CreateMinorDiamond(spineRow, level, $"Minor Lv{level}");

        CreateChoiceGroup(choiceRow, connectors, 5, "Ability", AbilityColor, new[]
        {
            "Power Slash",
            "Rend",
            "Envenom"
        });

        CreateMinorDiamond(spineRow, 8, null);
        CreateUnlockCard(unlockRow, connectors, 8, "Can Catch Trout");

        CreateChoiceGroup(choiceRow, connectors, 10, "Major Passive", MajorPassiveColor, new[]
        {
            "Ailment Attunement",
            "Parry",
            "Blade Mastery"
        });

        CreateChoiceGroup(choiceRow, connectors, 15, "Ability", AbilityColor, new[]
        {
            "Whirlwind",
            "Cleaving Strikes",
            "Crescent Slash",
            "Guardian's Hammer"
        });

        CreateUnlockCard(unlockRow, connectors, 20, "Lv 20 Unlock");
        CreateMajorNode(choiceRow, connectors, 20, "Weapon Bond", MajorPassiveColor);

        int[] fillerLevels = { 6, 7, 9, 11, 12, 13, 14, 16, 17, 18, 19 };
        foreach (int level in fillerLevels)
            CreateMinorDiamond(spineRow, level, null);
    }

    private void CreateUnlockCard(RectTransform unlockRow, RectTransform connectors, int level, string title)
    {
        float x = XForLevel(level);
        const float unlockCardH = 48f;
        var card = CreateRect(unlockRow, $"Unlock_Lv{level}", new Vector2(x, 0f), new Vector2(148f, unlockCardH));
        var cardRt = (RectTransform)card;
        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);

        var bg = card.gameObject.AddComponent<Image>();
        bg.color = CardBgColor;
        bg.raycastTarget = false;

        CreateDiamond(cardRt, "Icon", new Vector2(0f, 24f), 16f, UnlockColor);
        CreateLabel(cardRt, title, new Vector2(0f, -6f), GeneralNodeLabelFontSize, TextAlignmentOptions.Center);
        float cardBottom = unlockRowAnchoredY - unlockCardH * 0.5f;
        CreateConnector(connectors, new Vector2(x, spineRowAnchoredY + 5f), new Vector2(x, cardBottom), thickness: MilestoneSpineConnectorThickness);
    }

    private static void CreateMinorDiamond(RectTransform spineRow, int level, string label)
    {
        float x = XForLevel(level);
        CreateDiamond(spineRow, $"Minor_Lv{level}", new Vector2(x, SpineLocalY), MinorDiamondSize, MinorPassiveColor);
        if (!string.IsNullOrEmpty(label))
            CreateLabel(spineRow, label, new Vector2(x, SpineLocalY - 14f), MinorNodeLabelFontSize, TextAlignmentOptions.Top);
    }

    private void CreateMajorNode(RectTransform choiceRow, RectTransform connectors, int level, string title, Color color)
    {
        float x = XForLevel(level);
        const float majorGemSize = 22f;
        CreateDiamond(choiceRow, $"Major_Lv{level}", new Vector2(x, 0f), majorGemSize, color);
        CreateLabel(choiceRow, title, new Vector2(x, -30f), GeneralNodeLabelFontSize, TextAlignmentOptions.Top);
        CreateConnector(connectors, new Vector2(x, spineRowAnchoredY - 5f), new Vector2(x, choiceRowAnchoredY + majorGemSize * 0.5f), thickness: MilestoneSpineConnectorThickness);
    }

    private void CreateChoiceGroup(
        RectTransform choiceRow,
        RectTransform connectors,
        int level,
        string groupLabel,
        Color nodeColor,
        string[] choices)
    {
        float x = XForLevel(level);
        float spread = DefaultChoiceSpread;
        int count = choices.Length;
        float start = -(count - 1) * 0.5f * spread;
        float choiceTop = ChoiceCardHeight * 0.5f;

        CreateLabel(choiceRow, groupLabel, new Vector2(x, -58f), 10f, TextAlignmentOptions.Top);

        for (int i = 0; i < count; i++)
        {
            float offsetX = start + i * spread;
            Vector2 nodePos = new Vector2(x + offsetX, 0f);
            CreateChoiceCard(choiceRow, level, i, nodePos, choices[i], nodeColor);
            CreateConnector(connectors, new Vector2(x, spineRowAnchoredY - 5f), new Vector2(nodePos.x, choiceRowAnchoredY + choiceTop), thickness: MilestoneSpineConnectorThickness);
        }
    }

    private static void CreateChoiceCard(RectTransform choiceRow, int level, int index, Vector2 pos, string title, Color color)
    {
        var card = CreateRect(choiceRow, $"Choice_Lv{level}_{index}", pos, new Vector2(ChoiceCardWidth, ChoiceCardHeight));
        var cardRt = (RectTransform)card;
        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);

        var bg = card.gameObject.AddComponent<Image>();
        bg.color = CardBgColor;
        bg.raycastTarget = false;

        CreateDiamond(cardRt, "Gem", new Vector2(0f, 24f), 15f, color);
        CreateLabel(cardRt, title, new Vector2(0f, -8f), GeneralNodeLabelFontSize, TextAlignmentOptions.Center);
    }

    private static void CreateDiamond(RectTransform parent, string name, Vector2 anchoredPos, float size, Color color)
    {
        var gem = CreateRect(parent, name, anchoredPos, new Vector2(size, size));
        var gemRt = (RectTransform)gem;
        gemRt.anchorMin = gemRt.anchorMax = new Vector2(0f, 0.5f);
        gemRt.pivot = new Vector2(0.5f, 0.5f);
        gemRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var img = gem.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static void CreateConnector(
        RectTransform connectorsLayer,
        Vector2 start,
        Vector2 end,
        float extendBeyondStart = ConnectorExtendSpine,
        float extendBeyondEnd = ConnectorExtendNode,
        float thickness = TimelineConnectorThickness,
        bool useProgressColor = false)
    {
        Vector2 delta = end - start;
        float len = delta.magnitude;
        if (len <= 0.001f)
            return;

        Vector2 dir = delta / len;
        Vector2 lineStart = start - dir * extendBeyondStart;
        Vector2 lineEnd = end + dir * extendBeyondEnd;

        Vector2 seg = lineEnd - lineStart;
        float segLen = seg.magnitude;
        if (segLen <= 0.001f)
            return;

        float lineThickness = Mathf.Max(TimelineConnectorThickness, thickness);

        var go = new GameObject("Connector", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(connectorsLayer, false);
        SkillTimelineLineStyle.ApplySegment(rt, lineStart, lineEnd, lineThickness, useProgressColor);

        var renderer = go.GetComponent<CanvasRenderer>();
        renderer.cullTransparentMesh = false;
    }

    private static void CreateMilestoneLabel(Transform parent, string text, Vector2 anchoredPos)
    {
        var go = new GameObject("MilestoneLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(56f, 20f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = MilestoneLabelFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = MilestoneLabelColor;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void CreateLabel(Transform parent, string text, Vector2 anchoredPos, float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(150f, 36f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = LabelColor;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
    }

    private static float XForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, 50);
        float usable = ContentWidth - PaddingLeft - PaddingRight;
        return PaddingLeft + (level - 1) / 49f * usable;
    }

    private static Transform CreateRect(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static RectTransform FindChildRect(Transform parent, string childName)
    {
        if (parent == null)
            return null;
        Transform t = parent.Find(childName);
        return t != null ? t as RectTransform : null;
    }

    private static void DestroyImmediateSafe(Object obj)
    {
        if (obj == null)
            return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(obj);
        else
#endif
            Object.Destroy(obj);
    }
}
