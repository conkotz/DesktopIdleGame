using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class GameLogWindowUI : MonoBehaviour
{
    private const string DefaultWindowName = "GameActivityWindow";
    private const string LegacyWindowName = "GameLogWindow";

    [Header("Refs")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject rowPrefab;

    private bool _configured;
#if UNITY_EDITOR
    private const string DefaultRowPrefabAssetPath = "Assets/2.Prefabs/UI/GameActivityRow.prefab";
#endif

    private const float MinActivityRowHeight = 44f;
    private const float ActivityRowVerticalPadding = 16f;
    private const float ActivityMessageFontSize = 24f;
    private const float ActivityTimestampFontSize = 16f;
    private static readonly Color ActivityBodyTextColor = new Color32(43, 33, 24, 255);
    private static readonly Color ActivityTimestampColor = ActivityBodyTextColor;
    private const int ActivityRowTextGroupLeftPadding = 10;
    private const float ActivityTimestampColumnWidth = 152f;
    private const float PinnedToBottomThreshold = 0.04f;
    private const int MaxTailVisibleRows = 15;
    private const float ExpandedScrollThreshold = 0.05f;
    private const float ExpandOnScrollWheelThreshold = 0.01f;
    private const float AutoReturnToTailIdleSeconds = 5f;
    private const float ScrollPositionChangeEpsilon = 0.001f;

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.5f;

    private bool _historyDirty;
    private bool _showFullHistory;
    private bool _scrollValueListenerBound;
    private float _lastScrollInteractionUnscaledTime;
    private float _lastScrollNormalizedY = -1f;
    private int _syncedRevision = -1;
    private float _nextRefreshUnscaledTime;
    private readonly List<GameLog.Entry> _syncedVisibleEntries = new(96);
    private readonly List<GameLog.Entry> _fullVisibleHistoryScratch = new(96);
    private readonly List<GameLog.Entry> _visibleHistoryScratch = new(96);

    public static GameLogWindowUI ResolveOrCreate()
    {
        GameLogWindowUI existing = Object.FindFirstObjectByType<GameLogWindowUI>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject go = FindInactiveGameObjectByName(DefaultWindowName);
        if (go == null)
            go = FindInactiveGameObjectByName(LegacyWindowName);
        if (go == null)
            return null;

        return go.AddComponent<GameLogWindowUI>();
    }

    private static GameObject FindInactiveGameObjectByName(string objectName)
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (t.name == objectName)
                return t.gameObject;
        }

        return null;
    }

    private void Awake()
    {
        EnsureConfigured();
        MarkHistoryDirty();
        FlushNow();
    }

    private void OnEnable()
    {
        _configured = false;
        EnsureConfigured();
        ToggleSettingsStore.Changed += OnToggleSettingChanged;
        GameLog.HistoryChanged += OnGameLogHistoryChanged;
        MarkHistoryDirty();
        FlushNow();
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingChanged;
        GameLog.HistoryChanged -= OnGameLogHistoryChanged;
    }

    private void Update()
    {
        if (_historyDirty && Time.unscaledTime >= _nextRefreshUnscaledTime)
            FlushPendingRefresh();

        if (!_showFullHistory && TryExpandOnScrollWheel())
            ExpandToFullHistory();

        if (_showFullHistory &&
            Mathf.Abs(Input.mouseScrollDelta.y) > ExpandOnScrollWheelThreshold &&
            PointerIsOverScrollArea())
        {
            RecordScrollInteraction();
        }
    }

    private void OnGameLogHistoryChanged()
    {
        if (GameLog.History.Count == 0)
        {
            ClearRows();
            _syncedVisibleEntries.Clear();
            _showFullHistory = false;
            _historyDirty = false;
            _syncedRevision = GameLog.Revision;
            return;
        }

        if (ShouldAutoReturnToTailOnNewEntry())
            CollapseToTailView();

        MarkHistoryDirty();
    }

    private void MarkHistoryDirty()
    {
        _historyDirty = true;
    }

    public void FlushNow()
    {
        _nextRefreshUnscaledTime = 0f;
        FlushPendingRefresh();
    }

    private void FlushPendingRefresh()
    {
        if (!_historyDirty && _syncedRevision == GameLog.Revision)
            return;

        _historyDirty = false;
        _syncedRevision = GameLog.Revision;
        _nextRefreshUnscaledTime = Time.unscaledTime + Mathf.Max(0.05f, refreshIntervalSeconds);

        if (!isActiveAndEnabled)
            return;

        SyncDisplayWindowFromHistory();
    }

    private void SyncDisplayWindowFromHistory()
    {
        PrepareDisplayHistoryEntries(_visibleHistoryScratch);
        if (_visibleHistoryScratch.Count == 0)
        {
            ClearRows();
            _syncedVisibleEntries.Clear();
            return;
        }

        if (TrySyncHistoryIncremental())
            return;

        RebuildDisplayWindow();
    }

    public void AddLog(string message)
    {
        AddLog(message, GameLog.DefaultTextColor, "");
    }

    public void AddLog(string message, Color textColor)
    {
        AddLog(message, textColor, "");
    }

    public void AddLog(string message, Color textColor, string timeText)
    {
        AddLogInternal(message, textColor, timeText, false);
    }

    private void AddLogInternal(string message, Color textColor, string timeText, bool refreshLayout)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureConfigured();
        if (contentRoot == null)
            return;

        ActivityRow row = CreateRow();
        if (row.MessageText != null)
        {
            Color displayColor = ResolveActivityDisplayColor(textColor);
            row.MessageText.text = message.Trim();
            row.MessageText.color = displayColor;
            row.MessageText.faceColor = displayColor;
            row.MessageText.gameObject.SetActive(true);
        }

        if (row.TimestampText != null)
        {
            row.TimestampText.text = string.IsNullOrWhiteSpace(timeText) ? "" : timeText.Trim();
            bool showTime = !string.IsNullOrWhiteSpace(row.TimestampText.text);
            row.TimestampText.gameObject.SetActive(showTime);
            if (showTime)
                ApplyActivityTimestampColor(row.TimestampText);
        }

        TrimRowsToMaxEntries();
        if (refreshLayout)
            RefreshLayoutAndScroll();
    }

    public void ClearLogs()
    {
        ClearRows();
        _syncedVisibleEntries.Clear();
    }

    public void RebuildFromHistory()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        RebuildDisplayWindow();
    }

    private void RebuildDisplayWindow()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        PrepareDisplayHistoryEntries(_visibleHistoryScratch);
        List<GameLog.Entry> visible = _visibleHistoryScratch;
        int count = visible.Count;

        ClearRows();
        _syncedVisibleEntries.Clear();

        if (count == 0)
            return;

        _syncedVisibleEntries.AddRange(visible);

        for (int i = 0; i < count; i++)
        {
            GameLog.Entry entry = visible[i];
            AddLogInternal(entry.Message, entry.Color, GameLog.FormatClock(entry.TimestampLocal), false);
        }

        RefreshLayoutAndScroll();
        if (IsPinnedToBottom())
            KeepNewestVisible();
    }

    private bool TrySyncHistoryIncremental()
    {
        if (contentRoot == null)
            return false;

        List<GameLog.Entry> visibleNow = _visibleHistoryScratch;
        int count = visibleNow.Count;
        if (count == 0)
            return false;

        if (_syncedVisibleEntries.Count == 0 || count < _syncedVisibleEntries.Count)
            return false;

        for (int i = 0; i < _syncedVisibleEntries.Count; i++)
        {
            if (!VisibleEntryEquals(_syncedVisibleEntries[i], visibleNow[i]))
                return false;
        }

        if (count == _syncedVisibleEntries.Count)
        {
            int last = count - 1;
            if (last < 0 || VisibleEntryEquals(_syncedVisibleEntries[last], visibleNow[last]))
                return true;

            if (TrySyncTailWindowSlide(visibleNow))
                return true;

            if (!UpdateLastVisibleRow(visibleNow[last]))
                return false;

            _syncedVisibleEntries[last] = visibleNow[last];
            RefreshLayoutAndScroll(lightPass: true, layoutRowsFromIndex: last);
            if (IsPinnedToBottom())
                KeepNewestVisible();
            return true;
        }

        if (TrySyncTailWindowSlide(visibleNow))
            return true;

        int rowsBefore = contentRoot.childCount;

        for (int i = _syncedVisibleEntries.Count; i < count; i++)
        {
            GameLog.Entry entry = visibleNow[i];
            AddLogInternal(entry.Message, entry.Color, GameLog.FormatClock(entry.TimestampLocal), false);
        }

        TrimRowsToMaxEntries();
        _syncedVisibleEntries.Clear();
        _syncedVisibleEntries.AddRange(visibleNow);

        RefreshLayoutAndScroll(lightPass: true, layoutRowsFromIndex: rowsBefore);
        if (IsPinnedToBottom())
            KeepNewestVisible();
        return true;
    }

    private static void CollectVisibleHistoryEntries(List<GameLog.Entry> into)
    {
        into.Clear();
        IReadOnlyList<GameLog.Entry> history = GameLog.History;
        for (int i = 0; i < history.Count; i++)
        {
            GameLog.Entry entry = history[i];
            if (GameLog.ShouldShowInActivityLog(entry.Message))
                into.Add(entry);
        }
    }

    private void PrepareDisplayHistoryEntries(List<GameLog.Entry> displayOut)
    {
        CollectVisibleHistoryEntries(_fullVisibleHistoryScratch);
        int fullCount = _fullVisibleHistoryScratch.Count;
        displayOut.Clear();

        if (fullCount == 0)
            return;

        if (_showFullHistory || fullCount <= MaxTailVisibleRows)
        {
            displayOut.AddRange(_fullVisibleHistoryScratch);
            return;
        }

        int start = fullCount - MaxTailVisibleRows;
        for (int i = start; i < fullCount; i++)
            displayOut.Add(_fullVisibleHistoryScratch[i]);
    }

    private bool TrySyncTailWindowSlide(List<GameLog.Entry> visibleNow)
    {
        if (_showFullHistory || contentRoot == null)
            return false;

        int count = visibleNow.Count;
        if (count < MaxTailVisibleRows || _syncedVisibleEntries.Count != count)
            return false;

        if (VisibleEntryEquals(_syncedVisibleEntries[0], visibleNow[0]))
            return false;

        for (int i = 1; i < count; i++)
        {
            if (!VisibleEntryEquals(_syncedVisibleEntries[i], visibleNow[i - 1]))
                return false;
        }

        if (contentRoot.childCount > 0)
            DestroyRow(contentRoot.GetChild(0));

        GameLog.Entry newest = visibleNow[count - 1];
        AddLogInternal(newest.Message, newest.Color, GameLog.FormatClock(newest.TimestampLocal), false);
        _syncedVisibleEntries.Clear();
        _syncedVisibleEntries.AddRange(visibleNow);
        TrimRowsToMaxEntries();
        RefreshLayoutAndScroll(lightPass: true, layoutRowsFromIndex: Mathf.Max(0, contentRoot.childCount - 1));
        if (IsPinnedToBottom())
            KeepNewestVisible();
        return true;
    }

    private void ExpandToFullHistory()
    {
        if (_showFullHistory)
            return;

        _showFullHistory = true;
        RecordScrollInteraction();
        RebuildDisplayWindow();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = Mathf.Max(ExpandedScrollThreshold, scrollRect.verticalNormalizedPosition);
    }

    private void CollapseToTailView()
    {
        if (!_showFullHistory)
            return;

        CollectVisibleHistoryEntries(_fullVisibleHistoryScratch);
        if (_fullVisibleHistoryScratch.Count <= MaxTailVisibleRows)
            return;

        _showFullHistory = false;
        RebuildDisplayWindow();
        KeepNewestVisible();
        _lastScrollNormalizedY = 0f;
    }

    private bool ShouldAutoReturnToTailOnNewEntry()
    {
        if (!_showFullHistory || !isActiveAndEnabled)
            return false;

        CollectVisibleHistoryEntries(_fullVisibleHistoryScratch);
        if (_fullVisibleHistoryScratch.Count <= MaxTailVisibleRows)
            return false;

        if (IsPinnedToBottom())
            return false;

        return Time.unscaledTime - _lastScrollInteractionUnscaledTime >= AutoReturnToTailIdleSeconds;
    }

    private void RecordScrollInteraction(float? normalizedY = null)
    {
        _lastScrollInteractionUnscaledTime = Time.unscaledTime;
        if (normalizedY.HasValue)
            _lastScrollNormalizedY = normalizedY.Value;
    }

    private bool HasScrollPositionMoved(float normalizedY)
    {
        if (_lastScrollNormalizedY < 0f)
        {
            _lastScrollNormalizedY = normalizedY;
            return false;
        }

        if (Mathf.Abs(normalizedY - _lastScrollNormalizedY) <= ScrollPositionChangeEpsilon)
            return false;

        _lastScrollNormalizedY = normalizedY;
        return true;
    }

    private void OnScrollValueChanged(Vector2 _)
    {
        if (scrollRect == null)
            return;

        float y = scrollRect.verticalNormalizedPosition;
        if (HasScrollPositionMoved(y))
            RecordScrollInteraction(y);

        if (_showFullHistory)
        {
            if (y <= PinnedToBottomThreshold)
                CollapseToTailView();
            return;
        }

        if (y > ExpandedScrollThreshold)
            ExpandToFullHistory();
    }

    private bool TryExpandOnScrollWheel()
    {
        if (scrollRect == null)
            return false;

        CollectVisibleHistoryEntries(_fullVisibleHistoryScratch);
        if (_fullVisibleHistoryScratch.Count <= MaxTailVisibleRows)
            return false;

        float wheel = Input.mouseScrollDelta.y;
        if (wheel <= ExpandOnScrollWheelThreshold)
            return false;

        if (!PointerIsOverScrollArea())
            return false;

        return true;
    }

    private bool PointerIsOverScrollArea()
    {
        if (scrollRect == null)
            return false;

        RectTransform target = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
        if (target == null)
            return false;

        Camera eventCamera = scrollRect.viewport != null ? scrollRect.viewport.GetComponentInParent<Canvas>()?.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(target, Input.mousePosition, eventCamera);
    }

    private static bool VisibleEntryEquals(GameLog.Entry a, GameLog.Entry b) =>
        a.Message == b.Message &&
        a.Color == b.Color &&
        a.TimestampLocal == b.TimestampLocal;

    private bool UpdateLastVisibleRow(GameLog.Entry entry)
    {
        if (contentRoot == null || contentRoot.childCount == 0)
            return false;

        Transform lastRow = contentRoot.GetChild(contentRoot.childCount - 1);
        TMP_Text msg = FindChildText(lastRow, "ActivityRowText");
        TMP_Text time = FindChildText(lastRow, "TimeStamp");
        if (!msg)
            return false;

        Color displayColor = ResolveActivityDisplayColor(entry.Color);
        msg.text = entry.Message.Trim();
        msg.color = displayColor;
        msg.faceColor = displayColor;

        if (time)
        {
            string clock = GameLog.FormatClock(entry.TimestampLocal);
            time.text = clock;
            bool showTime = !string.IsNullOrWhiteSpace(clock);
            time.gameObject.SetActive(showTime);
            if (showTime)
                ApplyActivityTimestampColor(time);
        }

        return true;
    }

    private void OnToggleSettingChanged(ToggleSettingId setting, bool _)
    {
        if (setting == ToggleSettingId.UseTwentyFourHourTime)
        {
            MarkHistoryDirty();
            FlushNow();
        }
    }

    private void ClearRows()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            DestroyRow(contentRoot.GetChild(i));
    }

    private void TrimRowsToMaxEntries()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        int maxRows = _showFullHistory ? GameLog.MaxEntries : MaxTailVisibleRows;
        while (contentRoot.childCount > maxRows)
            DestroyRow(contentRoot.GetChild(0));
    }

    private bool IsPinnedToBottom()
    {
        if (scrollRect == null)
            return true;

        return scrollRect.verticalNormalizedPosition <= PinnedToBottomThreshold;
    }

    private static void DestroyRow(Transform row)
    {
        if (row == null)
            return;

        GameObject rowObject = row.gameObject;
        row.SetParent(null, false);

        if (Application.isPlaying)
            Destroy(rowObject);
        else
            DestroyImmediate(rowObject);
    }

    private readonly struct ActivityRow
    {
        public readonly TMP_Text MessageText;
        public readonly TMP_Text TimestampText;

        public ActivityRow(TMP_Text messageText, TMP_Text timestampText)
        {
            MessageText = messageText;
            TimestampText = timestampText;
        }
    }

    private ActivityRow CreateRow()
    {
        GameObject rowObject;
        TMP_Text messageText;
        TMP_Text timestampText;
        if (rowPrefab != null)
        {
            rowObject = Instantiate(rowPrefab, contentRoot);
            messageText = FindChildText(rowObject.transform, "ActivityRowText") ??
                rowObject.GetComponentInChildren<TMP_Text>(true);
            timestampText = FindChildText(rowObject.transform, "TimeStamp");
        }
        else
        {
            rowObject = new GameObject("GameLogRow", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            rowObject.transform.SetParent(contentRoot, false);
            messageText = rowObject.GetComponent<TMP_Text>();
            timestampText = null;
        }

        rowObject.SetActive(true);
        ConfigureSpawnedRow(rowObject.transform as RectTransform, messageText, timestampText);
        rowObject.transform.SetAsLastSibling();
        return new ActivityRow(messageText, timestampText);
    }

    private void EnsureConfigured()
    {
        if (_configured)
            return;

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (contentRoot == null && scrollRect != null)
            contentRoot = scrollRect.content;
        if (contentRoot == null)
            contentRoot = FindChildByName(transform, "Content") as RectTransform;
#if UNITY_EDITOR
        if (rowPrefab == null)
            rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultRowPrefabAssetPath);
#endif

        ConfigureScrollView();
        ConfigureContentLayout();
        EnsureScrollReceivesWheelEvents();
        ApplyActivityWindowChromeTextColors();

        _configured = true;
    }

    private void ConfigureScrollView()
    {
        if (scrollRect == null)
            return;

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.content = contentRoot;

        if (!_scrollValueListenerBound)
        {
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            _scrollValueListenerBound = true;
        }
    }

    private void EnsureScrollReceivesWheelEvents()
    {
        if (scrollRect == null)
            return;

        RectTransform target = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
        if (target == null)
            return;

        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic == null)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
        }
        else
        {
            graphic.raycastTarget = true;
        }
    }

    private void ConfigureContentLayout()
    {
        if (contentRoot == null)
            return;

        contentRoot.anchorMin = new Vector2(0f, 0f);
        contentRoot.anchorMax = new Vector2(1f, 0f);
        contentRoot.pivot = new Vector2(0.5f, 0f);
        contentRoot.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.reverseArrangement = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void ConfigureSpawnedRow(RectTransform rowRoot, TMP_Text rowText, TMP_Text timestampText)
    {
        if (rowRoot == null)
            return;

        rowRoot.anchorMin = Vector2.zero;
        rowRoot.anchorMax = new Vector2(1f, 0f);
        rowRoot.pivot = Vector2.zero;
        rowRoot.anchoredPosition = Vector2.zero;
        rowRoot.sizeDelta = new Vector2(0f, rowRoot.sizeDelta.y);

        LayoutElement layout = rowRoot.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rowRoot.gameObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minHeight = Mathf.Max(layout.minHeight, MinActivityRowHeight);

        if (rowText == null)
            return;

        rowText.gameObject.SetActive(true);
        rowText.enabled = true;
        if (rowText.font == null && TMP_Settings.defaultFontAsset != null)
            rowText.font = TMP_Settings.defaultFontAsset;
        rowText.alpha = 1f;
        rowText.raycastTarget = false;
        ApplyActivityMessageTypography(rowText);

        CanvasRenderer renderer = rowText.canvasRenderer;
        if (renderer != null)
            renderer.SetAlpha(1f);

        if (timestampText != null)
        {
            ApplyActivityTimestampTypography(timestampText);
            timestampText.gameObject.SetActive(true);
            timestampText.enabled = true;
            if (timestampText.font == null && TMP_Settings.defaultFontAsset != null)
                timestampText.font = TMP_Settings.defaultFontAsset;
            timestampText.alpha = 1f;
            timestampText.raycastTarget = false;

            CanvasRenderer timestampRenderer = timestampText.canvasRenderer;
            if (timestampRenderer != null)
                timestampRenderer.SetAlpha(1f);

            ConfigureActivityRowTwoColumnLayout(rowRoot, timestampText, rowText);
            return;
        }

        rowText.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform textRect = rowText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
    }

    private static void ConfigureActivityRowTwoColumnLayout(RectTransform rowRoot, TMP_Text timestampText, TMP_Text messageText)
    {
        if (!rowRoot || timestampText == null)
            return;

        Transform textGroupT = rowRoot.Find("TextGroup");
        if (textGroupT != null)
        {
            HorizontalLayoutGroup hlg = textGroupT.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                RectOffset p = hlg.padding;
                hlg.padding = new RectOffset(ActivityRowTextGroupLeftPadding, p.right, p.top, p.bottom);
            }
        }

        LayoutElement tsLe = timestampText.GetComponent<LayoutElement>();
        if (tsLe == null)
            tsLe = timestampText.gameObject.AddComponent<LayoutElement>();
        tsLe.minWidth = ActivityTimestampColumnWidth;
        tsLe.preferredWidth = ActivityTimestampColumnWidth;
        tsLe.flexibleWidth = 0f;

        if (messageText != null)
        {
            LayoutElement msgLe = messageText.GetComponent<LayoutElement>();
            if (msgLe == null)
                msgLe = messageText.gameObject.AddComponent<LayoutElement>();
            msgLe.minWidth = 0f;
            msgLe.flexibleWidth = 1f;
        }
    }

    private static TMP_Text FindChildText(Transform root, string childName)
    {
        Transform t = FindChildByName(root, childName);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private void RefreshLayoutAndScroll(bool lightPass = false, int layoutRowsFromIndex = 0)
    {
        if (contentRoot == null)
            return;

        if (lightPass)
        {
            SyncRenderedRowHeights(layoutRowsFromIndex);
            LayoutRebuilder.MarkLayoutForRebuild(contentRoot);
            if (IsPinnedToBottom())
                KeepNewestVisible();
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        SyncRenderedRowHeights(0);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        Canvas.ForceUpdateCanvases();
    }

    private void SyncRenderedRowHeights(int startRowIndex = 0)
    {
        if (contentRoot == null)
            return;

        startRowIndex = Mathf.Clamp(startRowIndex, 0, contentRoot.childCount);

        if (startRowIndex == 0)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        for (int i = startRowIndex; i < contentRoot.childCount; i++)
        {
            Transform rowT = contentRoot.GetChild(i);
            TMP_Text msg = FindChildText(rowT, "ActivityRowText");
            if (msg)
                ApplyActivityMessageTypography(msg);
            SyncSingleRowHeight(rowT);
        }
    }

    private void SyncSingleRowHeight(Transform rowT)
    {
        if (!rowT)
            return;

        TMP_Text msg = FindChildText(rowT, "ActivityRowText");
        if (!msg)
            return;

        float w = msg.rectTransform.rect.width;
        if (w < 8f && rowT is RectTransform rowRt)
        {
            Transform tg = rowT.Find("TextGroup");
            HorizontalLayoutGroup hlg = tg != null ? tg.GetComponent<HorizontalLayoutGroup>() : null;
            float spacing = hlg != null ? hlg.spacing : 18f;
            int padH = hlg != null ? hlg.padding.left + hlg.padding.right : ActivityRowTextGroupLeftPadding;
            w = Mathf.Max(50f, rowRt.rect.width - padH - ActivityTimestampColumnWidth - spacing);
        }
        else if (w < 8f)
            w = contentRoot != null && contentRoot.rect.width > 8f ? contentRoot.rect.width - 24f : 200f;

        msg.ForceMeshUpdate(true);
        float textHeight = msg.GetPreferredValues(msg.text, w, 0).y;
        float rowH = Mathf.Max(MinActivityRowHeight, textHeight + ActivityRowVerticalPadding);

        LayoutElement rowLe = rowT.GetComponent<LayoutElement>();
        if (rowLe)
        {
            rowLe.minHeight = rowH;
            rowLe.preferredHeight = rowH;
        }

        Transform textGroup = rowT.Find("TextGroup");
        if (textGroup)
        {
            LayoutElement tgLe = textGroup.GetComponent<LayoutElement>();
            if (tgLe)
            {
                tgLe.minHeight = rowH;
                tgLe.preferredHeight = rowH;
            }
        }
    }

    private static void ApplyActivityMessageTypography(TMP_Text rowText)
    {
        if (!rowText)
            return;

        rowText.enableAutoSizing = false;
        rowText.fontSize = ActivityMessageFontSize;
        rowText.fontSizeMin = ActivityMessageFontSize;
        rowText.fontSizeMax = ActivityMessageFontSize;
        rowText.textWrappingMode = TextWrappingModes.Normal;
        rowText.overflowMode = TextOverflowModes.Overflow;
        rowText.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyActivityDisplayColorIfDefault(rowText);
    }

    private static void ApplyActivityDisplayColorIfDefault(TMP_Text rowText)
    {
        if (!rowText)
            return;

        Color current = rowText.color;
        if (current.r > 0.99f && current.g > 0.99f && current.b > 0.99f)
        {
            rowText.color = ActivityBodyTextColor;
            rowText.faceColor = ActivityBodyTextColor;
        }
    }

    private static void ApplyActivityTimestampTypography(TMP_Text timeText)
    {
        if (!timeText)
            return;

        timeText.enableAutoSizing = false;
        timeText.fontSize = ActivityTimestampFontSize;
        timeText.fontSizeMin = ActivityTimestampFontSize;
        timeText.fontSizeMax = ActivityTimestampFontSize;
        timeText.textWrappingMode = TextWrappingModes.NoWrap;
        timeText.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyActivityTimestampColor(timeText);
    }

    private static void ApplyActivityTimestampColor(TMP_Text timeText)
    {
        if (!timeText)
            return;

        timeText.color = ActivityTimestampColor;
        timeText.faceColor = ActivityTimestampColor;
    }

    private static Color ResolveActivityDisplayColor(Color storedColor)
    {
        if (storedColor.a <= 0.01f)
            return ActivityBodyTextColor;

        if (storedColor.r > 0.99f && storedColor.g > 0.99f && storedColor.b > 0.99f)
            return ActivityBodyTextColor;

        return storedColor;
    }

    private void ApplyActivityWindowChromeTextColors()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (!text)
                continue;

            if (text.name == "TimeStamp")
                continue;

            Color current = text.color;
            if (current.r > 0.99f && current.g > 0.99f && current.b > 0.99f)
            {
                text.color = ActivityBodyTextColor;
                text.faceColor = ActivityBodyTextColor;
            }
        }
    }

    private void KeepNewestVisible()
    {
        if (scrollRect == null || contentRoot == null)
            return;

        RectTransform viewport = scrollRect.viewport;
        float contentHeight = contentRoot.rect.height;
        float viewportHeight = viewport != null ? viewport.rect.height : 0f;

        if (viewportHeight <= 0f || contentHeight <= viewportHeight)
        {
            contentRoot.anchoredPosition = Vector2.zero;
            scrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        scrollRect.verticalNormalizedPosition = 0f;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
