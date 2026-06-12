using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay-layer center square for a pivot ghost. Stays above all ghosts so it remains visible and clickable
/// even when another ghost overlaps it.
/// </summary>
[DisallowMultipleComponent]
public sealed class PivotGhostBringToFrontOverlayButton : MonoBehaviour
{
    private static Sprite s_whiteSprite;

    private WindowPivotGhostUI _ghost;
    private RectTransform _rect;
    private RectTransform _overlayRoot;
    private Image _image;
    private bool _interactionEnabled = true;

    public WindowPivotGhostUI Ghost => _ghost;

    public void Initialize(WindowPivotGhostUI ghost, RectTransform overlayRoot, Color color)
    {
        _ghost = ghost;
        _overlayRoot = overlayRoot;
        _rect = transform as RectTransform;

        if (!_rect || !_overlayRoot)
            return;

        _rect.SetParent(_overlayRoot, false);
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = new Vector2(WindowPivotGhostUI.BringToFrontButtonSize, WindowPivotGhostUI.BringToFrontButtonSize);

        if (!_image)
            _image = GetComponent<Image>();
        if (_image == null)
            _image = gameObject.AddComponent<Image>();

        _image.sprite = GetWhiteSprite();
        _image.raycastTarget = true;
        ApplyColor(color);

        Button button = GetComponent<Button>();
        if (button == null)
            button = gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);

        SyncPosition();
    }

    public void ApplyColor(Color color)
    {
        if (_image)
            _image.color = color;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;
        if (_image)
            _image.raycastTarget = enabled;
    }

    private void LateUpdate()
    {
        if (_ghost && _ghost.isActiveAndEnabled)
            SyncPosition();
    }

    private void SyncPosition()
    {
        if (!_ghost || !_rect || !_overlayRoot)
            return;

        RectTransform ghostRt = _ghost.transform as RectTransform;
        if (!ghostRt)
            return;

        Vector3[] corners = new Vector3[4];
        ghostRt.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;

        Canvas canvas = _overlayRoot.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayRoot,
            screenPoint,
            eventCamera,
            out Vector2 localPoint);

        _rect.anchoredPosition = localPoint;
    }

    private void OnClicked()
    {
        if (!_interactionEnabled || MovePivotsModeController.IsTestViewActive || _ghost == null)
            return;

        _ghost.BringSelfToFront();
        MovePivotsModeController.NotifyGhostBroughtToFront(_ghost);
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
