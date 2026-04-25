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
    private const string DefaultRowPrefabAssetPath = "Assets/2.Prefabs/UI/GameLogRow.prefab";
#endif

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
        RebuildFromHistory();
    }

    public void AddLog(string message)
    {
        AddLog(message, GameLog.DefaultTextColor);
    }

    public void AddLog(string message, Color textColor)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureConfigured();
        if (contentRoot == null)
            return;

        TMP_Text row = CreateRow();
        row.text = message.Trim();
        row.color = textColor;
        row.faceColor = textColor;
        row.gameObject.SetActive(true);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

        KeepNewestVisible();
    }

    public void ClearLogs()
    {
        ClearRows();
    }

    private void RebuildFromHistory()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        ClearRows();

        var history = GameLog.History;
        for (int i = 0; i < history.Count; i++)
        {
            GameLog.Entry entry = history[i];
            AddLog(entry.Message, entry.Color);
        }
    }

    private void ClearRows()
    {
        EnsureConfigured();
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    private TMP_Text CreateRow()
    {
        GameObject rowObject;
        TMP_Text rowText;
        if (rowPrefab != null)
        {
            rowObject = Instantiate(rowPrefab, contentRoot);
            rowText = rowObject.GetComponentInChildren<TMP_Text>(true);
        }
        else
        {
            rowObject = new GameObject("GameLogRow", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            rowObject.transform.SetParent(contentRoot, false);
            rowText = rowObject.GetComponent<TMP_Text>();
        }

        rowObject.SetActive(true);
        ConfigureSpawnedRow(rowObject.transform as RectTransform, rowText);
        rowObject.transform.SetAsLastSibling();
        return rowText;
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

    private static void ConfigureSpawnedRow(RectTransform rowRoot, TMP_Text rowText)
    {
        if (rowRoot == null)
            return;

        rowRoot.anchorMin = Vector2.zero;
        rowRoot.anchorMax = new Vector2(1f, 0f);
        rowRoot.pivot = Vector2.zero;
        rowRoot.anchoredPosition = Vector2.zero;
        rowRoot.sizeDelta = new Vector2(0f, Mathf.Max(22f, rowRoot.sizeDelta.y));

        LayoutElement layout = rowRoot.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rowRoot.gameObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        if (layout.preferredHeight <= 0f)
            layout.preferredHeight = Mathf.Max(22f, rowRoot.sizeDelta.y);

        if (rowText == null)
            return;

        rowText.gameObject.SetActive(true);
        rowText.enabled = true;
        if (rowText.font == null && TMP_Settings.defaultFontAsset != null)
            rowText.font = TMP_Settings.defaultFontAsset;
        rowText.alpha = 1f;
        rowText.raycastTarget = false;
        rowText.alignment = TextAlignmentOptions.MidlineLeft;

        CanvasRenderer renderer = rowText.canvasRenderer;
        if (renderer != null)
            renderer.SetAlpha(1f);

        RectTransform textRect = rowText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
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
