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
    private static Sprite s_whiteSprite;

    private UIWindowLayoutBinding _binding;

    public UIWindowLayoutBinding Binding => _binding;
    private RectTransform _rect;
    private RectTransform _parent;
    private Vector2 _dragOffsetLocal;
    private Color _fillColor;
    private Color _borderColor;
    private Color _labelColor;

    public void Initialize(
        UIWindowLayoutBinding binding,
        RectTransform parent,
        UIWindowLayoutPrefs.Snapshot snapshot,
        Color fillColor)
    {
        _binding = binding;
        _parent = parent;
        _fillColor = fillColor;
        _borderColor = UIWindowLayoutBinding.GetPivotGhostBorderColor(fillColor);
        _labelColor = UIWindowLayoutBinding.GetPivotGhostLabelColor(fillColor);

        _rect = transform as RectTransform;
        if (!_rect)
            return;

        _rect.SetParent(parent, false);
        ApplySnapshot(snapshot);
        EnsureVisualChrome();
        EnsureResizeHandles();

        transform.SetAsLastSibling();
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_rect || !_parent)
            return;

        transform.SetAsLastSibling();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerLocal);

        _dragOffsetLocal = pointerLocal - _rect.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_rect || !_parent)
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
        ApplyToBindingAndSave();
        ClampToScreen();
        ApplyToBindingAndSave();
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
        if (transform.Find("GhostFill") != null)
            return;

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
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0f, -8f);
        labelRt.sizeDelta = new Vector2(-16f, 24f);

        TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = _binding != null ? _binding.DisplayLabel : "Window";
        label.fontSize = 14f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = _labelColor;
        label.raycastTarget = false;
    }

    private void EnsureResizeHandles()
    {
        UIWindowCornerResize resize = UIWindowCornerResize.EnsureOn(_rect);
        if (resize == null)
            return;

        resize.SetPersistCornerScaleToPlayerPrefs(false);
        resize.ResizeEnded -= ApplyToBindingAndSave;
        resize.ResizeEnded += ApplyToBindingAndSave;
        resize.BringHandlesToFront();
        ForceResizeHandlesVisible();
    }

    private void ForceResizeHandlesVisible()
    {
        Color handleColor = new Color(_borderColor.r, _borderColor.g, _borderColor.b, 0.75f);

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
