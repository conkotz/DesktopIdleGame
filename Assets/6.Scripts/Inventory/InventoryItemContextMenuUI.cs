using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public readonly struct InventoryItemContextMenuEntry
{
    public readonly string Label;
    public readonly Action Callback;

    public InventoryItemContextMenuEntry(string label, Action callback)
    {
        Label = label;
        Callback = callback;
    }
}

/// <summary>Right-click popup menu for inventory and storage item slots.</summary>
[DisallowMultipleComponent]
public class InventoryItemContextMenuUI : MonoBehaviour
{
    private static InventoryItemContextMenuUI _instance;

    [Header("Optional prefab wiring")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private RectTransform buttonContainer;
    [SerializeField] private Button buttonTemplate;

    [Header("Layout")]
    [SerializeField] private Vector2 screenOffset = new Vector2(8f, -8f);
    [SerializeField] private float minButtonWidth = 168f;
    [SerializeField] private float buttonHeight = 30f;

    private RectTransform _overlayRoot;
    private CanvasGroup _panelCanvasGroup;
    private readonly List<Button> _spawnedButtons = new List<Button>(8);
    private bool _isOpen;

    public static InventoryItemContextMenuUI Instance
    {
        get
        {
            if (_instance)
                return _instance;

            _instance = FindFirstObjectByType<InventoryItemContextMenuUI>(FindObjectsInactive.Include);
            return _instance;
        }
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
        EnsureBuilt();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!_isOpen)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (!IsPointerOverMenu())
                Hide();
        }
    }

    public void Show(RectTransform anchor, IReadOnlyList<InventoryItemContextMenuEntry> entries)
    {
        if (!anchor || entries == null || entries.Count == 0)
        {
            Hide();
            return;
        }

        EnsureBuilt();
        RebuildButtons(entries);

        if (_spawnedButtons.Count == 0)
        {
            HideImmediate();
            return;
        }

        PositionNearAnchor(anchor);
        _isOpen = true;
        if (_overlayRoot)
            _overlayRoot.gameObject.SetActive(true);
        if (panelRoot)
            panelRoot.gameObject.SetActive(true);
        if (_panelCanvasGroup)
        {
            _panelCanvasGroup.alpha = 1f;
            _panelCanvasGroup.blocksRaycasts = true;
            _panelCanvasGroup.interactable = true;
        }

        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        HideImmediate();
    }

    private void HideImmediate()
    {
        _isOpen = false;
        if (_overlayRoot)
            _overlayRoot.gameObject.SetActive(false);
        if (panelRoot)
            panelRoot.gameObject.SetActive(false);
        if (_panelCanvasGroup)
        {
            _panelCanvasGroup.alpha = 0f;
            _panelCanvasGroup.blocksRaycasts = false;
            _panelCanvasGroup.interactable = false;
        }
    }

    private void EnsureBuilt()
    {
        if (panelRoot && buttonContainer)
            return;

        if (!rootCanvas)
            rootCanvas = GetComponentInParent<Canvas>(true);

        if (!rootCanvas)
            rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (!rootCanvas)
            return;

        var rootGo = new GameObject("InventoryItemContextMenu", typeof(RectTransform), typeof(CanvasGroup));
        rootGo.transform.SetParent(rootCanvas.transform, false);
        _overlayRoot = rootGo.GetComponent<RectTransform>();
        StretchFull(_overlayRoot);

        var overlayImage = rootGo.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.01f);
        overlayImage.raycastTarget = true;

        var overlayButton = rootGo.AddComponent<Button>();
        overlayButton.transition = Selectable.Transition.None;
        overlayButton.onClick.AddListener(Hide);

        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        panelRoot.SetParent(_overlayRoot, false);
        panelRoot.pivot = new Vector2(0f, 1f);
        panelRoot.anchorMin = panelRoot.anchorMax = new Vector2(0f, 1f);

        _panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
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

        buttonContainer = panelRoot;

        var templateGo = new GameObject("MenuButtonTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        templateGo.transform.SetParent(panelRoot, false);
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
        tmp.fontSize = 16f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.margin = new Vector4(10f, 0f, 10f, 0f);
        tmp.color = Color.white;
        tmp.text = "Action";
        tmp.raycastTarget = false;
    }

    private void RebuildButtons(IReadOnlyList<InventoryItemContextMenuEntry> entries)
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
            InventoryItemContextMenuEntry entry = entries[i];
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
    }

    private void PositionNearAnchor(RectTransform anchor)
    {
        if (!panelRoot || !rootCanvas)
            return;

        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, anchor.position);
        screenPoint += screenOffset;

        RectTransform canvasRt = rootCanvas.transform as RectTransform;
        if (!canvasRt)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPoint, cam, out Vector2 localPoint))
            panelRoot.anchoredPosition = localPoint;
    }

    private bool IsPointerOverMenu()
    {
        if (!EventSystem.current)
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
            if (panelRoot && hit.IsChildOf(panelRoot))
                return true;
        }

        return false;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
