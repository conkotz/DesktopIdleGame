using TMPro;
using UnityEngine;

/// <summary>
/// Overlay-layer window name label for a pivot ghost. Drawn above bring-to-front center squares
/// so small placeholders remain readable.
/// </summary>
[DisallowMultipleComponent]
public sealed class PivotGhostLabelOverlay : MonoBehaviour
{
    private WindowPivotGhostUI _ghost;
    private RectTransform _rect;
    private RectTransform _overlayRoot;
    private TextMeshProUGUI _label;

    public WindowPivotGhostUI Ghost => _ghost;

    public void Initialize(WindowPivotGhostUI ghost, RectTransform overlayRoot)
    {
        _ghost = ghost;
        _overlayRoot = overlayRoot;
        _rect = transform as RectTransform;

        if (!_rect || !_overlayRoot)
            return;

        _rect.SetParent(_overlayRoot, false);
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 1f);

        if (!_label)
            _label = GetComponent<TextMeshProUGUI>();
        if (_label == null)
            _label = gameObject.AddComponent<TextMeshProUGUI>();

        _label.fontSize = 18f;
        _label.fontStyle = FontStyles.Bold;
        _label.alignment = TextAlignmentOptions.Center;
        _label.raycastTarget = false;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.overflowMode = TextOverflowModes.Overflow;

        RefreshFromGhost();
        SyncPosition();
    }

    public void RefreshFromGhost()
    {
        if (!_ghost || !_label)
            return;

        _label.text = _ghost.GetDisplayLabel();
        _label.color = _ghost.GetLabelColor();
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

        Canvas canvas = _overlayRoot.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenTopLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[1]);
        Vector2 screenTopRight = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
        Vector2 screenTopCenter = (screenTopLeft + screenTopRight) * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayRoot,
            screenTopLeft,
            eventCamera,
            out Vector2 localTopLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayRoot,
            screenTopRight,
            eventCamera,
            out Vector2 localTopRight);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayRoot,
            screenTopCenter,
            eventCamera,
            out Vector2 localTopCenter);

        float localWidth = Mathf.Abs(localTopRight.x - localTopLeft.x);
        _rect.sizeDelta = new Vector2(Mathf.Max(80f, localWidth - 16f), 30f);
        _rect.anchoredPosition = localTopCenter + new Vector2(0f, -10f);
    }
}
