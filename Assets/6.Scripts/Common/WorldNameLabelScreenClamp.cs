using TMPro;
using UnityEngine;

/// <summary>
/// Keeps world-space <c>NameLabel</c> text visible when zoomed in by shifting it downward
/// if the label top would clip above the gameplay camera viewport. Horizontal position is unchanged.
/// Only updates when strip zoom/layout changes — not every frame at default zoom.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class WorldNameLabelScreenClamp : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float screenTopMarginPixels = 6f;

    private TMP_Text _text;
    private Vector3 _restLocalPosition;
    private bool _hasRestLocalPosition;
    private float _restTopWorldOffsetY;
    private bool _hasRestTopWorldOffsetY;
    private bool _isClamped;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        CacheRestLocalPosition();
        ResolveCamera();
    }

    private void OnEnable()
    {
        if (!_hasRestLocalPosition)
            CacheRestLocalPosition();

        if (!Application.isPlaying)
            return;

        StripCameraController.StripLayoutChanged += HandleViewChanged;
        HandleViewChanged();
    }

    private void OnDisable()
    {
        StripCameraController.StripLayoutChanged -= HandleViewChanged;
    }

    /// <summary>Re-capture the authored local position (e.g. after layout tools move the label).</summary>
    public void RecacheRestLocalPosition()
    {
        CacheRestLocalPosition();
        _hasRestTopWorldOffsetY = false;
        _isClamped = false;
        if (Application.isPlaying)
            HandleViewChanged();
    }

    private void HandleViewChanged()
    {
        if (!_text || !_text.isActiveAndEnabled)
            return;

        ResolveCamera();
        if (!targetCamera || !targetCamera.isActiveAndEnabled)
            return;

        transform.localPosition = _restLocalPosition;

        if (TryFastPathNoClampNeeded())
        {
            _isClamped = false;
            return;
        }

        ApplyClampFromMeshBounds();
    }

    private bool TryFastPathNoClampNeeded()
    {
        if (!_hasRestTopWorldOffsetY)
            return false;

        Vector3 topWorld = new Vector3(
            transform.position.x,
            transform.position.y + _restTopWorldOffsetY,
            transform.position.z);
        Vector3 topScreen = targetCamera.WorldToScreenPoint(topWorld);
        if (topScreen.z <= 0f)
            return !_isClamped;

        float maxScreenY = targetCamera.pixelRect.yMax - screenTopMarginPixels;
        return topScreen.y <= maxScreenY;
    }

    private void ApplyClampFromMeshBounds()
    {
        _text.ForceMeshUpdate();
        Bounds bounds = _text.bounds;
        if (bounds.size.sqrMagnitude <= 0f)
            return;

        CacheRestTopWorldOffset(bounds);

        Vector3 topWorld = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        Vector3 topScreen = targetCamera.WorldToScreenPoint(topWorld);
        if (topScreen.z <= 0f)
            return;

        float maxScreenY = targetCamera.pixelRect.yMax - screenTopMarginPixels;
        if (topScreen.y <= maxScreenY)
        {
            _isClamped = false;
            return;
        }

        Vector3 depthRef = targetCamera.WorldToScreenPoint(transform.position);
        Vector3 targetTopWorld = targetCamera.ScreenToWorldPoint(
            new Vector3(topScreen.x, maxScreenY, depthRef.z));
        float shiftDown = topWorld.y - targetTopWorld.y;
        if (shiftDown <= 0f)
            return;

        Vector3 pos = transform.position;
        transform.position = new Vector3(pos.x, pos.y - shiftDown, pos.z);
        _isClamped = true;
    }

    private void CacheRestTopWorldOffset(Bounds bounds)
    {
        _restTopWorldOffsetY = bounds.max.y - transform.position.y;
        _hasRestTopWorldOffsetY = true;
    }

    private void CacheRestLocalPosition()
    {
        _restLocalPosition = transform.localPosition;
        _hasRestLocalPosition = true;
    }

    private void ResolveCamera()
    {
        if (targetCamera && targetCamera.isActiveAndEnabled)
            return;

        targetCamera = GameplayScreenOverlayLayout.TryResolveStripCamera();
        if (!targetCamera)
            targetCamera = Camera.main;
    }
}
