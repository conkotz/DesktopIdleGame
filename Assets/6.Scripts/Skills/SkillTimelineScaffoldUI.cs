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
    public const string PrefabConnectorsRootName = "PrefabConnectors";

    private const string LegacyScaffoldRootName = "ScaffoldRoot";

    private const float ContentWidth = 5000f;
    private const float ContentHeight = 300f;
    private const float PaddingLeft = 120f;
    private const float PaddingRight = 80f;
    /// <summary>TimelineContent Y for each row container (children use local X, local Y ≈ 0).</summary>
    private const float TimelineContentYOffset = -12f;
    private const float SpineY = 24f;
    private const float TopRowY = 90f;
    private const float SpineLocalY = 0f;
    private const float SpineProgressHeight = 6f;
    private const float MilestoneLabelFontSize = 14f;
    private const float MinorNodeLabelFontSize = 11f;
    private const float GeneralNodeLabelFontSize = 13f;
    private const float UnlockRowHeight = 108f;
    private const float SpineRowHeight = 72f;
    private const float ChoiceRowHeight = 118f;
    private const float MilestoneLabelYOffset = 22f;
    private const float MilestoneLabelXOffset = 6f;
    private const float BottomRowY = -54f;
    private const float MinorDiamondSize = 17f;
    private const float ChoiceCardWidth = 110f;
    private const float ChoiceCardHeight = 50f;
    private const float DefaultChoiceSpread = 118f;
    private const float StandardPrefabNodeSize = 56f;
    private const float MinorTickWidth = 2f;
    private const float MinorTickHeight = 8f;
    private const float LabeledTickWidth = 2f;
    private const float LabeledTickHeight = 14f;
    private const float ChoiceMilestoneTickWidth = 3f;
    private const float ChoiceMilestoneTickHeight = 26f;
    private const float HelperBarHeight = 60f;
    private const float HorizontalScrollbarHeight = 18f;
    private const float ScrollbarHandleMinWidth = 48f;
    private const float ConnectorThickness = 2f;
    /// <summary>Vertical stems from the spine to milestone nodes (matches choice-group branch lines).</summary>
    public const float MilestoneSpineConnectorThickness = ConnectorThickness;
    private const float ConnectorExtendSpine = 5f;
    private const float ConnectorExtendNode = 10f;
    public const float ConnectorSpineOverlap = 2f;

    private static readonly Color ScrollbarTrackColor = new(0.38f, 0.32f, 0.26f, 1f);
    private static readonly Color ScrollbarHandleColor = new(0.72f, 0.64f, 0.48f, 1f);

    private static readonly Color SpineColor = new(0.18f, 0.14f, 0.11f, 1f);
    private static readonly Color SpineProgressColor = new(0.85f, 0.72f, 0.35f, 1f);
    private static readonly Color TickColor = new(0.22f, 0.18f, 0.14f, 1f);
    private static readonly Color ConnectorColor = new(0.12f, 0.1f, 0.08f, 1f);
    private static readonly Color MinorPassiveColor = new(0.26f, 0.53f, 0.82f, 1f);
    private static readonly Color AbilityColor = new(0.22f, 0.62f, 0.38f, 1f);
    private static readonly Color MajorPassiveColor = new(0.58f, 0.32f, 0.76f, 1f);
    private static readonly Color UnlockColor = new(0.95f, 0.78f, 0.22f, 1f);
    private static readonly Color CardBgColor = new(0.12f, 0.1f, 0.08f, 0.88f);
    private static readonly Color LabelColor = new(0.95f, 0.92f, 0.86f, 1f);
    private static readonly Color MilestoneLabelColor = new(0.2f, 0.14f, 0.1f, 1f);

    [SerializeField] private bool rebuildOnEnable = true;
    [Tooltip("When false, only timeline chrome is built (spine, ticks, scroll). Use with HorizontalSkillTreeScaffoldUI prefab nodes.")]
    [SerializeField] private bool buildPlaceholderNodes = true;

    private void OnEnable()
    {
        if (rebuildOnEnable)
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

        RectTransform unlockRow = EnsureRow(content, UnlockRowName, TopRowY, UnlockRowHeight);
        RectTransform spineRow = EnsureRow(content, SpineRowName, SpineY, SpineRowHeight);
        RectTransform choiceRow = EnsureRow(content, ChoiceRowName, BottomRowY, ChoiceRowHeight);

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

    public float GetTimelineLevelX(int level) => XForLevel(level);

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
    }

    public float TimelineSpineY => SpineY;

    public float TimelineUnlockRowY => TopRowY;

    public float TimelineChoiceRowY => BottomRowY;

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
        float thickness = MilestoneSpineConnectorThickness)
    {
        if (connectorsLayer == null)
            return;
        CreateConnector(connectorsLayer, start, end, extendBeyondStart, extendBeyondEnd, thickness);
    }

    /// <summary>
    /// Viewport = scrollable timeline only. Helper bar and scrollbar sit below it (not masked).
    /// </summary>
    private static void EnsureTimelineChrome(RectTransform container, RectTransform viewport, RectTransform content)
    {
        float bottomReserved = HelperBarHeight + HorizontalScrollbarHeight;

        EnsureHelperOutsideViewport(container, viewport);

        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = Vector2.zero;
        viewport.offsetMin = new Vector2(0f, bottomReserved);
        viewport.offsetMax = Vector2.zero;

        ScrollRect scroll = container.GetComponent<ScrollRect>();
        if (scroll == null)
            scroll = container.gameObject.AddComponent<ScrollRect>();

        Scrollbar hBar = EnsureHorizontalScrollbar(container);
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        scroll.horizontalScrollbar = hBar;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    private static void EnsureHelperOutsideViewport(RectTransform container, RectTransform viewport)
    {
        RectTransform helper = FindChildRect(container, "HelperBar");
        if (helper == null)
            return;

        if (helper.parent != container)
            helper.SetParent(container, false);

        helper.SetAsLastSibling();
        helper.anchorMin = new Vector2(0f, 0f);
        helper.anchorMax = new Vector2(1f, 0f);
        helper.pivot = new Vector2(0.5f, 0f);
        helper.anchoredPosition = Vector2.zero;
        helper.sizeDelta = new Vector2(0f, HelperBarHeight);

        if (viewport.GetSiblingIndex() > helper.GetSiblingIndex())
            viewport.SetAsFirstSibling();
    }

    private static Scrollbar EnsureHorizontalScrollbar(RectTransform container)
    {
        Transform existing = container.Find("TimelineScrollbarHorizontal");
        if (existing != null && existing.TryGetComponent(out Scrollbar bar))
        {
            LayoutScrollbar((RectTransform)existing);
            if (existing.TryGetComponent(out Image trackImg))
                StyleScrollbarChrome(trackImg, bar);
            return bar;
        }

        var barGo = new GameObject("TimelineScrollbarHorizontal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.SetParent(container, false);
        LayoutScrollbar(barRt);

        var scrollbar = barGo.GetComponent<Scrollbar>();
        StyleScrollbarChrome(barGo.GetComponent<Image>(), scrollbar);
        return scrollbar;
    }

    private static void LayoutScrollbar(RectTransform barRt)
    {
        if (barRt == null)
            return;

        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(1f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.sizeDelta = new Vector2(-12f, HorizontalScrollbarHeight);
        barRt.anchoredPosition = new Vector2(0f, HelperBarHeight);
        barRt.SetSiblingIndex(Mathf.Max(0, barRt.parent.childCount - 2));
    }

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

        scrollbar.handleRect = handleRt;
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scrollbar.size = Mathf.Clamp01(scrollbar.size <= 0.001f ? 0.2f : scrollbar.size);
    }

    private static void PrepareContentRect(RectTransform content)
    {
        if (content.TryGetComponent(out HorizontalLayoutGroup hlg))
            DestroyImmediateSafe(hlg);
        if (content.TryGetComponent(out ContentSizeFitter csf))
            DestroyImmediateSafe(csf);

        content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = new Vector2(0f, TimelineContentYOffset);
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
            if (child.name == "SpineLine" || child.name == "SpineProgressLine" || child.name == "LevelTicks")
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
        float left = PaddingLeft;
        float right = ContentWidth - PaddingRight;

        var spine = CreateRect(root, "SpineLine", new Vector2(left, SpineLocalY), new Vector2(right - left, 2f));
        var spineRt = (RectTransform)spine;
        spineRt.anchorMin = spineRt.anchorMax = new Vector2(0f, 0.5f);
        spineRt.pivot = new Vector2(0f, 0.5f);
        var img = spine.gameObject.AddComponent<Image>();
        img.color = SpineColor;
        img.raycastTarget = false;
    }

    private static void BuildSpineProgress(RectTransform root, int playerLevel)
    {
        float left = PaddingLeft;
        float endX = XForLevel(Mathf.Clamp(playerLevel, 1, 50));
        float width = endX - left;
        if (width <= 0.5f)
            return;

        var progress = CreateRect(root, "SpineProgressLine", new Vector2(left, SpineLocalY), new Vector2(width, SpineProgressHeight));
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

    private static void BuildLevelTicks(RectTransform root)
    {
        var ticksParent = CreateRect(root, "LevelTicks", Vector2.zero, Vector2.zero);
        var ticksRt = (RectTransform)ticksParent;
        ticksRt.anchorMin = ticksRt.anchorMax = new Vector2(0f, 0.5f);
        ticksRt.pivot = new Vector2(0f, 0.5f);
        ticksRt.sizeDelta = new Vector2(ContentWidth, ContentHeight);

        for (int level = 1; level <= 50; level++)
        {
            float x = XForLevel(level);
            bool showLevelLabel = level == 1 || level % 5 == 0;
            bool choiceMilestone = level == 5 || level == 10 || level == 15;

            float tickW = choiceMilestone ? ChoiceMilestoneTickWidth : (showLevelLabel ? LabeledTickWidth : MinorTickWidth);
            float tickH = choiceMilestone ? ChoiceMilestoneTickHeight : (showLevelLabel ? LabeledTickHeight : MinorTickHeight);

            var tick = CreateRect(ticksRt, $"Tick_Lv{level}", new Vector2(x, SpineLocalY), new Vector2(tickW, tickH));
            var tickRt = (RectTransform)tick;
            tickRt.anchorMin = tickRt.anchorMax = new Vector2(0f, 0.5f);
            tickRt.pivot = new Vector2(0.5f, 0f);
            var tickImg = tick.gameObject.AddComponent<Image>();
            tickImg.color = TickColor;
            tickImg.raycastTarget = false;

            if (showLevelLabel)
            {
                float labelX = x + MilestoneLabelXOffset;
                float labelY = SpineLocalY + MilestoneLabelYOffset;
                CreateMilestoneLabel(ticksRt, $"Lv {level}", new Vector2(labelX, labelY));
            }
        }
    }

    private static void BuildPlaceholderNodesInRows(
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
            "Crescent Slash"
        });

        CreateUnlockCard(unlockRow, connectors, 20, "Lv 20 Unlock");
        CreateMajorNode(choiceRow, connectors, 20, "Weapon Bond", MajorPassiveColor);

        int[] fillerLevels = { 6, 7, 9, 11, 12, 13, 14, 16, 17, 18, 19 };
        foreach (int level in fillerLevels)
            CreateMinorDiamond(spineRow, level, null);
    }

    private static void CreateUnlockCard(RectTransform unlockRow, RectTransform connectors, int level, string title)
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
        float cardBottom = TopRowY - unlockCardH * 0.5f;
        CreateConnector(connectors, new Vector2(x, SpineY + 5f), new Vector2(x, cardBottom), thickness: MilestoneSpineConnectorThickness);
    }

    private static void CreateMinorDiamond(RectTransform spineRow, int level, string label)
    {
        float x = XForLevel(level);
        CreateDiamond(spineRow, $"Minor_Lv{level}", new Vector2(x, SpineLocalY), MinorDiamondSize, MinorPassiveColor);
        if (!string.IsNullOrEmpty(label))
            CreateLabel(spineRow, label, new Vector2(x, SpineLocalY - 14f), MinorNodeLabelFontSize, TextAlignmentOptions.Top);
    }

    private static void CreateMajorNode(RectTransform choiceRow, RectTransform connectors, int level, string title, Color color)
    {
        float x = XForLevel(level);
        const float majorGemSize = 22f;
        CreateDiamond(choiceRow, $"Major_Lv{level}", new Vector2(x, 0f), majorGemSize, color);
        CreateLabel(choiceRow, title, new Vector2(x, -30f), GeneralNodeLabelFontSize, TextAlignmentOptions.Top);
        CreateConnector(connectors, new Vector2(x, SpineY - 5f), new Vector2(x, BottomRowY + majorGemSize * 0.5f), thickness: MilestoneSpineConnectorThickness);
    }

    private static void CreateChoiceGroup(
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
            CreateConnector(connectors, new Vector2(x, SpineY - 5f), new Vector2(nodePos.x, BottomRowY + choiceTop), thickness: MilestoneSpineConnectorThickness);
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
        float thickness = ConnectorThickness)
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

        float lineThickness = Mathf.Max(2f, thickness);
        Vector2 pos = lineStart + seg * 0.5f;
        float angle = Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg;

        var go = new GameObject("Connector", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(connectorsLayer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(segLen, lineThickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        var img = go.GetComponent<Image>();
        img.color = ConnectorColor;
        img.raycastTarget = false;
        img.maskable = true;

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
