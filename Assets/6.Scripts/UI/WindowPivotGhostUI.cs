using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Colored semi-transparent rectangle placeholder shown in move-pivots mode (not the real window UI).
/// </summary>
[DisallowMultipleComponent]
public sealed class WindowPivotGhostUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const float BringToFrontButtonSize = 56f;
    private const string BringToFrontVisualName = "GhostBringToFrontVisual";
    private const string ShopCenterInMenuButtonName = "GhostShopCenterInMenuButton";

    private static Sprite s_whiteSprite;

    private UIWindowLayoutBinding _binding;

    public UIWindowLayoutBinding Binding => _binding;
    private RectTransform _rect;
    private RectTransform _parent;
    private Vector2 _dragOffsetLocal;
    private Color _fillColor;
    private Color _borderColor;
    private Color _labelColor;
    private readonly List<Rect> _slotHintRects = new(16);
    private bool _initialized;

    public void Initialize(
        UIWindowLayoutBinding binding,
        RectTransform parent,
        UIWindowLayoutPrefs.Snapshot snapshot,
        Color fillColor,
        IReadOnlyList<Rect> slotHintRects = null)
    {
        _binding = binding;
        _parent = parent;
        _fillColor = fillColor;
        _borderColor = UIWindowLayoutBinding.GetPivotGhostBorderColor(fillColor);
        _labelColor = UIWindowLayoutBinding.GetPivotGhostLabelColor(fillColor);
        _slotHintRects.Clear();
        if (slotHintRects != null)
        {
            for (int i = 0; i < slotHintRects.Count; i++)
                _slotHintRects.Add(slotHintRects[i]);
        }

        _rect = transform as RectTransform;
        if (!_rect)
            return;

        _rect.SetParent(parent, false);
        ApplySnapshot(snapshot);
        _initialized = true;
        RefreshPivotChrome();

        transform.SetAsLastSibling();
    }

    private void OnEnable()
    {
        if (_initialized)
            RefreshPivotChrome();
    }

    public void RefreshPivotChrome()
    {
        if (!_rect)
            _rect = transform as RectTransform;

        if (!_rect)
            return;

        EnsureVisualChrome();
        EnsureSlotHints();
        EnsureBringToFrontVisual();
        EnsureShopCenterInMenuButton();
        EnsureResizeHandles();
    }

    public Color GetResizeHandleColor() =>
        new Color(_borderColor.r, _borderColor.g, _borderColor.b, 0.85f);

    public Color GetBringToFrontButtonColor()
    {
        return new Color(
            Mathf.Clamp01(_fillColor.r * 0.88f),
            Mathf.Clamp01(_fillColor.g * 0.88f),
            Mathf.Clamp01(_fillColor.b * 0.88f),
            1f);
    }

    public void RefreshInteractionChrome()
    {
        EnsureBringToFrontVisual();
        EnsureShopCenterInMenuButton();
        EnsureResizeHandles();
    }

    private void ApplySnapshot(UIWindowLayoutPrefs.Snapshot snapshot)
    {
        if (!_rect)
            return;

        UIWindowLayoutPrefs.Snapshot prepared = UIWindowLayoutBinding.PreparePivotGhostSnapshot(_binding, snapshot);
        UIWindowLayoutPrefs.Apply(_rect, prepared);

        if (_binding != null && UIWindowLayoutBinding.IsQuestTrackerWindow(_binding.MemoryKey))
        {
            QuestTrackerWindowUI.ApplyTopAnchoredPivotLayout(
                _rect,
                UIWindowLayoutBinding.GetPivotGhostMinimumHeight(_binding.MemoryKey));
        }
    }

    public UIWindowLayoutPrefs.Snapshot GetCurrentSnapshot() =>
        _rect ? UIWindowLayoutPrefs.Capture(_rect) : default;

    public void ApplyToBindingAndSave()
    {
        if (!_binding || !_rect)
            return;

        _binding.ApplySnapshot(GetCurrentSnapshot());
        _binding.SaveCurrentLayout();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        Transform fill = transform.Find("GhostFill");
        if (fill && fill.TryGetComponent(out Image fillImage))
            fillImage.raycastTarget = enabled;

        Transform shopCenterButton = transform.Find(ShopCenterInMenuButtonName);
        if (shopCenterButton && shopCenterButton.TryGetComponent(out Image shopCenterImage))
            shopCenterImage.raycastTarget = enabled;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("ResizeHandle_", System.StringComparison.Ordinal))
                continue;

            if (child.TryGetComponent(out Image handleImage))
                handleImage.raycastTarget = enabled;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (MovePivotsModeController.IsTestViewActive || !_rect || !_parent)
            return;

        BringSelfToFront();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerLocal);

        _dragOffsetLocal = pointerLocal - _rect.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (MovePivotsModeController.IsTestViewActive || !_rect || !_parent)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerLocal);

        _rect.anchoredPosition = pointerLocal - _dragOffsetLocal;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (MovePivotsModeController.IsTestViewActive)
            return;

        ApplyToBindingAndSave();
        ClampToScreen();
        ApplyToBindingAndSave();
        RefreshInteractionChrome();
    }

    private void ClampToScreen()
    {
        if (!_rect)
            return;

        Vector3[] corners = new Vector3[4];
        _rect.GetWorldCorners(corners);

        float left = Mathf.Min(corners[0].x, corners[1].x);
        float right = Mathf.Max(corners[2].x, corners[3].x);
        float bottom = Mathf.Min(corners[0].y, corners[3].y);
        float top = Mathf.Max(corners[1].y, corners[2].y);

        Vector2 deltaScreen = Vector2.zero;
        if (left < 0f) deltaScreen.x += -left;
        if (right > Screen.width) deltaScreen.x += Screen.width - right;
        if (bottom < 0f) deltaScreen.y += -bottom;
        if (top > Screen.height) deltaScreen.y += Screen.height - top;

        if (deltaScreen == Vector2.zero)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, Vector2.zero, null, out Vector2 local0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, deltaScreen, null, out Vector2 localDelta);
        _rect.anchoredPosition += localDelta - local0;
    }

    private void EnsureVisualChrome()
    {
        Transform existingFill = transform.Find("GhostFill");
        if (existingFill != null)
        {
            ApplyVisualChromeColors(existingFill);
            return;
        }

        GameObject fill = new GameObject("GhostFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.layer = gameObject.layer;
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.SetParent(transform, false);
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        Image fillImage = fill.GetComponent<Image>();
        fillImage.sprite = GetWhiteSprite();
        fillImage.color = _fillColor;
        fillImage.raycastTarget = true;

        Outline outline = fill.AddComponent<Outline>();
        outline.effectColor = _borderColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.layer = gameObject.layer;
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(transform, false);
        PositionLabelAtTop(labelRt);

        TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = _binding != null ? _binding.DisplayLabel : "Window";
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = _labelColor;
        label.raycastTarget = false;
    }

    private static void PositionLabelAtTop(RectTransform labelRt)
    {
        if (!labelRt)
            return;

        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0f, -10f);
        labelRt.sizeDelta = new Vector2(-16f, 30f);
    }

    private void ApplyVisualChromeColors(Transform fillTransform)
    {
        if (fillTransform.TryGetComponent(out Image fillImage))
        {
            fillImage.sprite = GetWhiteSprite();
            fillImage.color = _fillColor;
        }

        if (fillTransform.TryGetComponent(out Outline outline))
        {
            outline.effectColor = _borderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        Transform labelTransform = transform.Find("Label");
        if (labelTransform)
        {
            if (labelTransform is RectTransform labelRt)
                PositionLabelAtTop(labelRt);

            if (labelTransform.TryGetComponent(out TextMeshProUGUI label))
            {
                label.text = _binding != null ? _binding.DisplayLabel : "Window";
                label.color = _labelColor;
                label.fontSize = 18f;
            }
        }
    }

    public void BringSelfToFront()
    {
        if (MovePivotsModeController.IsTestViewActive)
            return;

        transform.SetAsLastSibling();

        if (_rect && _rect.TryGetComponent(out UIWindowCornerResize resize))
            resize.BringHandlesToFront();

        MovePivotsModeController.NotifyGhostBroughtToFront(this);
        RefreshInteractionChrome();
    }

    private void EnsureBringToFrontVisual()
    {
        Transform existing = transform.Find(BringToFrontVisualName);
        Image image;
        if (existing)
        {
            image = existing.GetComponent<Image>();
        }
        else
        {
            GameObject visualGo = new GameObject(
                BringToFrontVisualName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            visualGo.layer = gameObject.layer;

            RectTransform visualRt = visualGo.GetComponent<RectTransform>();
            visualRt.SetParent(transform, false);
            visualRt.anchorMin = new Vector2(0.5f, 0.5f);
            visualRt.anchorMax = new Vector2(0.5f, 0.5f);
            visualRt.pivot = new Vector2(0.5f, 0.5f);
            visualRt.anchoredPosition = Vector2.zero;
            visualRt.sizeDelta = new Vector2(BringToFrontButtonSize, BringToFrontButtonSize);

            image = visualGo.GetComponent<Image>();
            image.sprite = GetWhiteSprite();
            image.raycastTarget = false;
            visualGo.transform.SetAsLastSibling();
        }

        if (image)
            image.color = GetBringToFrontButtonColor();
    }

    private void EnsureShopCenterInMenuButton()
    {
        if (_binding == null || !UIWindowLayoutBinding.IsShopWindow(_binding.MemoryKey))
            return;

        Transform existing = transform.Find(ShopCenterInMenuButtonName);
        Image image;
        Button button;
        TMP_Text label;
        Outline outline;

        if (existing)
        {
            image = existing.GetComponent<Image>();
            button = existing.GetComponent<Button>();
            outline = existing.GetComponent<Outline>();
            label = existing.GetComponentInChildren<TMP_Text>(true);
        }
        else
        {
            GameObject buttonGo = new GameObject(
                ShopCenterInMenuButtonName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Button));
            buttonGo.layer = gameObject.layer;

            RectTransform buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.SetParent(transform, false);
            buttonRt.anchorMin = new Vector2(1f, 1f);
            buttonRt.anchorMax = new Vector2(1f, 1f);
            buttonRt.pivot = new Vector2(1f, 1f);
            buttonRt.anchoredPosition = new Vector2(-8f, -8f);
            buttonRt.sizeDelta = new Vector2(220f, 34f);

            image = buttonGo.GetComponent<Image>();
            image.sprite = GetWhiteSprite();

            outline = buttonGo.GetComponent<Outline>();

            button = buttonGo.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            button.colors = colors;
            button.onClick.AddListener(CenterShopInMainMenuWindow);

            GameObject labelGo = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelGo.layer = gameObject.layer;
            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(buttonRt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8f, 4f);
            labelRt.offsetMax = new Vector2(-8f, -4f);

            label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "center in main menu window";
            label.fontSize = 12f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        Color panelColor = new Color(0.22f, 0.2f, 0.16f, 0.94f);
        Color panelBorder = new Color(0.08f, 0.07f, 0.06f, 0.95f);
        Color labelColor = new Color(0.95f, 0.9f, 0.82f, 1f);

        if (image)
        {
            image.sprite = GetWhiteSprite();
            image.color = panelColor;
        }

        if (!outline && existing)
            outline = existing.gameObject.AddComponent<Outline>();
        if (outline)
        {
            outline.effectColor = panelBorder;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        if (label == null && existing)
            label = existing.GetComponentInChildren<TMP_Text>(true);
        if (label)
            label.color = labelColor;
    }

    private void CenterShopInMainMenuWindow()
    {
        if (MovePivotsModeController.IsTestViewActive || !_rect)
            return;

        if (!MovePivotsModeController.TryGetPivotGhost("MainMenuWindow", out WindowPivotGhostUI menuGhost))
            return;

        RectTransform menuRect = menuGhost.transform as RectTransform;
        if (!menuRect)
            return;

        UIPinNextToMenuWindow.AlignCenterTo(_rect, menuRect);
        ApplyToBindingAndSave();
        BringSelfToFront();
    }

    private void EnsureSlotHints()
    {
        if (_slotHintRects.Count == 0)
            return;

        Transform hintsRoot = transform.Find("GhostSlotHints");
        if (!hintsRoot)
        {
            GameObject hintsGo = new GameObject("GhostSlotHints", typeof(RectTransform));
            hintsGo.layer = gameObject.layer;
            RectTransform hintsRt = hintsGo.GetComponent<RectTransform>();
            hintsRt.SetParent(transform, false);
            hintsRt.anchorMin = Vector2.zero;
            hintsRt.anchorMax = Vector2.one;
            hintsRt.offsetMin = Vector2.zero;
            hintsRt.offsetMax = Vector2.zero;
            hintsRoot = hintsRt;
        }

        for (int i = hintsRoot.childCount - 1; i >= 0; i--)
            Destroy(hintsRoot.GetChild(i).gameObject);

        Color fill = new Color(_labelColor.r, _labelColor.g, _labelColor.b, 0.1f);
        Color edge = new Color(_labelColor.r, _labelColor.g, _labelColor.b, 0.5f);

        for (int i = 0; i < _slotHintRects.Count; i++)
        {
            Rect normalized = _slotHintRects[i];
            GameObject hintGo = new GameObject(
                $"SlotHint_{i}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            hintGo.layer = gameObject.layer;

            RectTransform hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.SetParent(hintsRoot, false);
            hintRt.anchorMin = new Vector2(normalized.x, normalized.y);
            hintRt.anchorMax = new Vector2(normalized.x + normalized.width, normalized.y + normalized.height);
            hintRt.offsetMin = Vector2.zero;
            hintRt.offsetMax = Vector2.zero;

            Image hintImage = hintGo.GetComponent<Image>();
            hintImage.sprite = GetWhiteSprite();
            hintImage.color = fill;
            hintImage.raycastTarget = false;

            Outline outline = hintGo.GetComponent<Outline>();
            outline.effectColor = edge;
            outline.effectDistance = new Vector2(1f, -1f);

            hintGo.transform.SetAsLastSibling();
        }
    }

    private void EnsureResizeHandles()
    {
        UIWindowCornerResize resize = UIWindowCornerResize.EnsureOn(_rect);
        if (resize == null)
            return;

        resize.SetPersistCornerScaleToPlayerPrefs(false);
        resize.ForcePivotGhostHandlesVisible = true;
        resize.ResizeEnded -= OnGhostResizeEnded;
        resize.ResizeEnded += OnGhostResizeEnded;
        resize.BringHandlesToFront();
        ForceResizeHandlesVisible();
    }

    private void OnGhostResizeEnded()
    {
        ApplyToBindingAndSave();
        RefreshInteractionChrome();
    }

    private void ForceResizeHandlesVisible()
    {
        Color handleColor = GetResizeHandleColor();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("ResizeHandle_", System.StringComparison.Ordinal))
                continue;

            if (child.TryGetComponent(out Image image))
                image.color = handleColor;
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite)
            return s_whiteSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        s_whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }
}
