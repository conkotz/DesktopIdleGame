using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public readonly struct ContextMenuEntry
{
    public readonly string Label;
    public readonly Action Callback;

    public ContextMenuEntry(string label, Action callback)
    {
        Label = label;
        Callback = callback;
    }
}

/// <summary>Shared right-click popup menu for inventory items, NPCs, resources, etc.</summary>
[DisallowMultipleComponent]
public class ContextMenuUI : MonoBehaviour
{
    private const string FullscreenCanvasName = "ContextMenuFullscreenCanvas";

    private static ContextMenuUI _instance;
    private static Canvas _fullscreenCanvas;
    private static RectTransform _fullscreenCanvasRt;

    [Header("Optional prefab wiring")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private RectTransform headerRoot;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private GameObject headerSeparator;
    [SerializeField] private RectTransform buttonContainer;
    [SerializeField] private Button buttonTemplate;

    private const int DefaultTopSortingOrder = short.MaxValue - 100;

    [Header("Layout")]
    [SerializeField] private Vector2 screenOffset = new Vector2(8f, -8f);
    [SerializeField] private float minButtonWidth = 168f;
    [SerializeField] private float buttonHeight = 30f;
    [SerializeField] private float headerHeight = 28f;
    [SerializeField] private int topSortingOrder = DefaultTopSortingOrder;

    private readonly List<Button> _spawnedButtons = new List<Button>(8);
    private bool _isOpen;
    private int _dismissAllowedAfterFrame = -1;
    private int _openedFrame = -1;
    private Vector2 _pendingScreenPosition;

    public static ContextMenuUI Instance
    {
        get
        {
            if (_instance)
                return _instance;

            _instance = FindFirstObjectByType<ContextMenuUI>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    public static ContextMenuUI EnsureInstance()
    {
        ContextMenuUI menu = Instance;
        if (menu != null)
            return menu;

        var host = new GameObject("ContextMenuUI", typeof(ContextMenuUI));
        return host.GetComponent<ContextMenuUI>();
    }

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void LateUpdate()
    {
        if (!_isOpen)
            return;

        if (_openedFrame >= 0 && Time.frameCount <= _openedFrame + 1)
            PositionPanel(_pendingScreenPosition);

        if (Time.frameCount <= _dismissAllowedAfterFrame)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (!IsPointerOverMenu())
                Hide();
        }
    }

    public void ShowAtScreen(
        IReadOnlyList<ContextMenuEntry> entries,
        Vector2? screenPosition = null,
        string headerTitle = null)
    {
        Canvas fullscreenCanvas = GetOrCreateFullscreenCanvas(topSortingOrder);
        RectTransform anchor = fullscreenCanvas.transform as RectTransform;
        Show(anchor, entries, fullscreenCanvas, null, screenPosition, headerTitle);
    }

    public void Show(
        RectTransform anchor,
        IReadOnlyList<ContextMenuEntry> entries,
        Canvas preferredCanvas = null,
        RectTransform hostPanel = null,
        Vector2? screenPosition = null,
        string headerTitle = null)
    {
        if (!anchor || entries == null || entries.Count == 0)
        {
            Hide();
            return;
        }

        Canvas fullscreenCanvas = GetOrCreateFullscreenCanvas(topSortingOrder);
        EnsureBuilt(fullscreenCanvas.transform);
        SetHeader(headerTitle);
        RebuildButtons(entries);

        if (_spawnedButtons.Count == 0 || !panelRoot)
        {
            HideImmediate();
            return;
        }

        if (panelRoot.parent != fullscreenCanvas.transform)
            panelRoot.SetParent(fullscreenCanvas.transform, false);

        _pendingScreenPosition = screenPosition ?? Input.mousePosition;
        _isOpen = true;
        _openedFrame = Time.frameCount;
        _dismissAllowedAfterFrame = Time.frameCount + 1;

        if (!_fullscreenCanvas.gameObject.activeSelf)
            _fullscreenCanvas.gameObject.SetActive(true);

        panelRoot.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(buttonContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
        Canvas.ForceUpdateCanvases();

        PositionPanel(_pendingScreenPosition);

        panelRoot.SetAsLastSibling();
    }

    public void Hide()
    {
        HideImmediate();
    }

    private void HideImmediate()
    {
        _isOpen = false;
        _dismissAllowedAfterFrame = -1;
        _openedFrame = -1;
        if (panelRoot)
            panelRoot.gameObject.SetActive(false);
    }

    private static Canvas GetOrCreateFullscreenCanvas(int sortingOrder)
    {
        if (_fullscreenCanvas)
        {
            _fullscreenCanvas.sortingOrder = sortingOrder > 0 ? sortingOrder : DefaultTopSortingOrder;
            if (_fullscreenCanvas.TryGetComponent(out CanvasScaler staleScaler))
                UnityEngine.Object.Destroy(staleScaler);
            return _fullscreenCanvas;
        }

        GameObject existing = GameObject.Find(FullscreenCanvasName);
        if (existing)
            _fullscreenCanvas = existing.GetComponent<Canvas>();

        if (!_fullscreenCanvas)
        {
            var go = new GameObject(
                FullscreenCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            _fullscreenCanvas = go.GetComponent<Canvas>();
        }

        _fullscreenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _fullscreenCanvas.overrideSorting = true;
        _fullscreenCanvas.sortingOrder = sortingOrder > 0 ? sortingOrder : DefaultTopSortingOrder;

        if (!_fullscreenCanvas.TryGetComponent(out GraphicRaycaster _))
            _fullscreenCanvas.gameObject.AddComponent<GraphicRaycaster>();

        _fullscreenCanvasRt = _fullscreenCanvas.transform as RectTransform;
        StretchFull(_fullscreenCanvasRt);
        _fullscreenCanvas.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();

        return _fullscreenCanvas;
    }

    private static void GetCanvasPixelSize(out float width, out float height)
    {
        if (_fullscreenCanvasRt)
        {
            Rect canvasRect = _fullscreenCanvasRt.rect;
            if (canvasRect.width > 1f && canvasRect.height > 1f)
            {
                width = canvasRect.width;
                height = canvasRect.height;
                return;
            }
        }

        width = Screen.width;
        height = Screen.height;
    }

    private void EnsureBuilt(Transform parent)
    {
        if (panelRoot && buttonContainer && buttonTemplate)
        {
            if (!headerRoot)
                EnsureHeaderBuilt();
            if (buttonContainer == panelRoot)
            {
                EnsureButtonContainerBuilt();
                if (buttonTemplate.transform.parent == panelRoot)
                    buttonTemplate.transform.SetParent(buttonContainer, false);
            }

            panelRoot.pivot = new Vector2(0f, 1f);
            panelRoot.anchorMin = panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            RemoveNestedPanelCanvas();
            return;
        }

        if (!parent)
            return;

        panelRoot = new GameObject(
            "ContextMenuPanel",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        panelRoot.SetParent(parent, false);
        panelRoot.pivot = new Vector2(0f, 1f);
        panelRoot.anchorMin = panelRoot.anchorMax = new Vector2(0.5f, 0.5f);

        var panelGroup = panelRoot.GetComponent<CanvasGroup>();
        panelGroup.alpha = 1f;
        panelGroup.blocksRaycasts = true;
        panelGroup.interactable = true;

        var panelImage = panelRoot.GetComponent<Image>();
        panelImage.color = new Color32(24, 24, 28, 245);
        panelImage.raycastTarget = true;

        var outline = panelRoot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(198, 184, 158, 255);
        outline.effectDistance = new Vector2(2f, -2f);

        var layout = panelRoot.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelRoot.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        EnsureHeaderBuilt();
        EnsureButtonContainerBuilt();

        var templateGo = new GameObject("MenuButtonTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        templateGo.transform.SetParent(buttonContainer, false);
        templateGo.SetActive(false);

        var templateImage = templateGo.GetComponent<Image>();
        templateImage.color = new Color32(46, 48, 54, 255);

        buttonTemplate = templateGo.GetComponent<Button>();
        var colors = buttonTemplate.colors;
        colors.normalColor = templateImage.color;
        colors.highlightedColor = new Color32(72, 76, 86, 255);
        colors.pressedColor = new Color32(198, 184, 158, 255);
        colors.selectedColor = colors.highlightedColor;
        buttonTemplate.colors = colors;

        var templateLayout = templateGo.GetComponent<LayoutElement>();
        templateLayout.minHeight = buttonHeight;
        templateLayout.preferredHeight = buttonHeight;
        templateLayout.minWidth = minButtonWidth;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(templateGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        StretchFull(textRt);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 16f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.margin = new Vector4(10f, 0f, 10f, 0f);
        tmp.color = Color.white;
        tmp.text = "Action";
        tmp.raycastTarget = false;

        panelRoot.gameObject.SetActive(false);
    }

    private void EnsureHeaderBuilt()
    {
        if (!panelRoot || headerRoot)
            return;

        headerRoot = new GameObject(
            "ContextMenuHeader",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement)).GetComponent<RectTransform>();
        headerRoot.SetParent(panelRoot, false);
        headerRoot.SetAsFirstSibling();

        var headerLayout = headerRoot.GetComponent<LayoutElement>();
        headerLayout.minHeight = headerHeight;
        headerLayout.preferredHeight = headerHeight;
        headerLayout.minWidth = minButtonWidth;

        var headerImage = headerRoot.GetComponent<Image>();
        headerImage.color = new Color32(18, 18, 22, 255);
        headerImage.raycastTarget = true;

        var headerTextGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerTextGo.transform.SetParent(headerRoot, false);
        StretchFull(headerTextGo.GetComponent<RectTransform>());
        headerText = headerTextGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset)
            headerText.font = TMP_Settings.defaultFontAsset;
        headerText.fontSize = 14f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.MidlineLeft;
        headerText.margin = new Vector4(10f, 0f, 10f, 0f);
        headerText.color = new Color32(198, 184, 158, 255);
        headerText.raycastTarget = false;

        headerSeparator = new GameObject(
            "ContextMenuHeaderSeparator",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        headerSeparator.transform.SetParent(panelRoot, false);
        var separatorLayout = headerSeparator.GetComponent<LayoutElement>();
        separatorLayout.minHeight = 1f;
        separatorLayout.preferredHeight = 1f;
        separatorLayout.minWidth = minButtonWidth;
        var separatorImage = headerSeparator.GetComponent<Image>();
        separatorImage.color = new Color32(198, 184, 158, 120);
        separatorImage.raycastTarget = false;

        headerRoot.gameObject.SetActive(false);
        headerSeparator.SetActive(false);
    }

    private void EnsureButtonContainerBuilt()
    {
        if (!panelRoot)
            return;

        if (buttonContainer && buttonContainer != panelRoot)
            return;

        buttonContainer = new GameObject(
            "ContextMenuButtons",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        buttonContainer.SetParent(panelRoot, false);

        if (headerSeparator)
            buttonContainer.SetSiblingIndex(headerSeparator.transform.GetSiblingIndex() + 1);
        else if (headerRoot)
            buttonContainer.SetSiblingIndex(headerRoot.GetSiblingIndex() + 1);

        var buttonLayout = buttonContainer.GetComponent<VerticalLayoutGroup>();
        buttonLayout.spacing = 4f;
        buttonLayout.childAlignment = TextAnchor.UpperLeft;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = false;
    }

    private void SetHeader(string title)
    {
        EnsureHeaderBuilt();

        bool show = !string.IsNullOrWhiteSpace(title);
        if (headerRoot)
            headerRoot.gameObject.SetActive(show);
        if (headerSeparator)
            headerSeparator.SetActive(show);
        if (headerText)
            headerText.text = show ? title.Trim() : string.Empty;
    }

    private void RemoveNestedPanelCanvas()
    {
        if (!panelRoot)
            return;

        if (panelRoot.TryGetComponent(out Canvas nestedCanvas))
            Destroy(nestedCanvas);

        if (panelRoot.TryGetComponent(out GraphicRaycaster nestedRaycaster))
            Destroy(nestedRaycaster);
    }

    private void RebuildButtons(IReadOnlyList<ContextMenuEntry> entries)
    {
        for (int i = 0; i < _spawnedButtons.Count; i++)
        {
            if (_spawnedButtons[i])
                Destroy(_spawnedButtons[i].gameObject);
        }
        _spawnedButtons.Clear();

        if (!buttonTemplate || !buttonContainer)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            ContextMenuEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.Label) || entry.Callback == null)
                continue;

            Button button = Instantiate(buttonTemplate, buttonContainer);
            button.gameObject.SetActive(true);
            button.name = $"Menu_{entry.Label}";

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label)
                label.text = entry.Label;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Hide();
                entry.Callback.Invoke();
            });

            _spawnedButtons.Add(button);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(buttonContainer);
        if (panelRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
    }

    private void PositionPanel(Vector2 screenPoint)
    {
        if (!panelRoot || !_fullscreenCanvasRt)
            GetOrCreateFullscreenCanvas(topSortingOrder);

        if (!panelRoot || !_fullscreenCanvasRt)
            return;

        Vector2 localPoint = ScreenPointToCanvasLocal(screenPoint);
        localPoint += screenOffset;
        localPoint = ClampPanelPosition(localPoint);
        panelRoot.anchoredPosition = localPoint;
    }

    private static Vector2 ScreenPointToCanvasLocal(Vector2 screenPoint)
    {
        GetCanvasPixelSize(out float width, out float height);

        if (_fullscreenCanvasRt &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _fullscreenCanvasRt,
                screenPoint,
                null,
                out Vector2 converted))
        {
            Rect canvasRect = _fullscreenCanvasRt.rect;
            if (canvasRect.width > 1f && canvasRect.height > 1f)
                return converted;
        }

        // First frame after canvas creation: rect is often still 100x100 — use screen pixels directly.
        return new Vector2(
            screenPoint.x - width * 0.5f,
            screenPoint.y - height * 0.5f);
    }

    private Vector2 ClampPanelPosition(Vector2 anchoredPosition)
    {
        if (!panelRoot)
            return anchoredPosition;

        Vector2 size = panelRoot.rect.size;
        GetCanvasPixelSize(out float width, out float height);
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        if (size.x <= 0f || size.y <= 0f)
            return anchoredPosition;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, -halfW, halfW - size.x);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, -halfH + size.y, halfH);
        return anchoredPosition;
    }

    private bool IsPointerOverMenu()
    {
        if (!EventSystem.current || !panelRoot)
            return false;

        var data = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>(8);
        EventSystem.current.RaycastAll(data, results);
        for (int i = 0; i < results.Count; i++)
        {
            Transform hit = results[i].gameObject.transform;
            if (hit.IsChildOf(panelRoot))
                return true;
        }

        return false;
    }

    private static void StretchFull(RectTransform rt)
    {
        if (!rt)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
