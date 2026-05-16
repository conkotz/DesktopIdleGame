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
    private static readonly Color ActivityTimestampColor = new Color32(43, 33, 24, 255);
    private const int ActivityRowTextGroupLeftPadding = 10;
    /// <summary>Fixed width so the message column always starts at the same X for every row.</summary>
    private const float ActivityTimestampColumnWidth = 152f;

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
        RebuildFromHistory();
    }

    private void OnEnable()
    {
        _configured = false;
        EnsureConfigured();
        ToggleSettingsStore.Changed += OnToggleSettingChanged;
        RebuildFromHistory();
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingChanged;
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
        AddLogInternal(message, textColor, timeText, true);
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
            row.MessageText.text = message.Trim();
            row.MessageText.color = textColor;
            row.MessageText.faceColor = textColor;
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

        TrimVisibleRowsToMax();
        if (refreshLayout)
            RefreshLayoutAndScroll();
    }

    public void ClearLogs()
    {
        ClearRows();
    }

    public void RebuildFromHistory()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        ClearRows();

        var history = GameLog.History;
        for (int i = 0; i < history.Count; i++)
        {
            GameLog.Entry entry = history[i];
            if (!GameLog.ShouldShowInActivityLog(entry.Message))
                continue;
            AddLogInternal(entry.Message, entry.Color, GameLog.FormatClock(entry.TimestampLocal), false);
        }

        RefreshLayoutAndScroll();
    }

    private void OnToggleSettingChanged(ToggleSettingId setting, bool _)
    {
        if (setting == ToggleSettingId.UseTwentyFourHourTime)
            RebuildFromHistory();
    }

    private void ClearRows()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            DestroyRow(child);
        }
    }

    private void TrimVisibleRowsToMax()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        int overflow = contentRoot.childCount - GameLog.MaxEntries;
        for (int i = 0; i < overflow; i++)
        {
            Transform oldest = contentRoot.GetChild(0);
            DestroyRow(oldest);
        }
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

        // Fallback: single text cell fills the row (no timestamp / prefab chrome).
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

    private void RefreshLayoutAndScroll()
    {
        if (contentRoot == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        SyncAllActivityRowHeights();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        Canvas.ForceUpdateCanvases();
        KeepNewestVisible();
    }

    /// <summary>
    /// Wrapped TMP needs a stable width before <see cref="TMP_Text.GetPreferredValues"/>; run after layout assigns flex widths.
    /// </summary>
    private void SyncAllActivityRowHeights()
    {
        if (contentRoot == null)
            return;

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            TMP_Text msg = FindChildText(contentRoot.GetChild(i), "ActivityRowText");
            if (msg)
                ApplyActivityMessageTypography(msg);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            Transform rowT = contentRoot.GetChild(i);
            TMP_Text msg = FindChildText(rowT, "ActivityRowText");
            if (!msg)
                continue;

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
                w = contentRoot.rect.width > 8f ? contentRoot.rect.width - 24f : 200f;

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
