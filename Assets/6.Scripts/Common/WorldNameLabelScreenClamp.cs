using TMPro;
using UnityEngine;

/// <summary>
/// Keeps world-space <c>NameLabel</c> text inside the gameplay strip camera viewport (top edge).
/// Driven by <see cref="WorldNameLabelScreenClampDriver"/> — updates only on strip zoom/layout changes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class WorldNameLabelScreenClamp : MonoBehaviour
{
    [SerializeField] private float screenTopMarginPixels = 6f;

    private TMP_Text _text;
    private RectTransform _rectTransform;
    private Vector3 _restLocalPosition;
    private Vector2 _restAnchoredPosition;
    private bool _hasRestPosition;
    private bool _useAnchoredRest;
    private float _restTopWorldOffsetY;
    private bool _hasRestTopWorldOffsetY;
    private bool _isClamped;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _rectTransform = transform as RectTransform;
        CacheRestPosition();
    }

    private void OnEnable()
    {
        if (!_hasRestPosition)
            CacheRestPosition();

        if (!Application.isPlaying)
            return;

        WorldNameLabelScreenClampDriver.Register(this);
    }

    private void OnDisable()
    {
        WorldNameLabelScreenClampDriver.Unregister(this);
    }

    public void RecacheRestLocalPosition()
    {
        CacheRestPosition();
        _hasRestTopWorldOffsetY = false;
        _isClamped = false;

        if (Application.isPlaying)
            WorldNameLabelScreenClampDriver.RequestRefresh(this);
    }

    public void RefreshClamp()
    {
        _hasRestTopWorldOffsetY = false;

        if (Application.isPlaying)
            WorldNameLabelScreenClampDriver.RequestRefresh(this);
    }

    public bool NeedsUpdateForView(WorldNameLabelScreenClampDriver.StripViewState view)
    {
        if (!_text || !_text.isActiveAndEnabled || !view.Camera)
            return false;

        if (_isClamped)
            return true;

        if (!_hasRestTopWorldOffsetY)
            return true;

        return WouldClipAtView(view);
    }

    public void ReleaseToRest()
    {
        if (!_isClamped)
            return;

        RestoreRestPosition();
        _isClamped = false;
    }

    public void UpdateForView(WorldNameLabelScreenClampDriver.StripViewState view, bool forceMeasure)
    {
        if (!_text || !_text.isActiveAndEnabled || !view.Camera)
            return;

        RestoreRestPosition();

        if (!forceMeasure && !_isClamped && _hasRestTopWorldOffsetY && !WouldClipAtView(view))
        {
            _isClamped = false;
            return;
        }

        if (forceMeasure || !_hasRestTopWorldOffsetY)
            _text.ForceMeshUpdate();

        if (!TryGetLabelTopWorld(out Vector3 topWorld))
            return;

        CacheRestTopWorldOffset(topWorld);

        if (!TryGetLabelTopScreen(topWorld, view.Camera, out Vector3 topScreen))
            return;

        float maxScreenY = view.MaxLabelTopScreenY - screenTopMarginPixels;
        if (topScreen.y <= maxScreenY)
        {
            _isClamped = false;
            return;
        }

        float excessScreenY = topScreen.y - maxScreenY;
        Vector3 pivotScreen = view.Camera.WorldToScreenPoint(transform.position);
        Vector3 targetPivotScreen = new Vector3(pivotScreen.x, pivotScreen.y - excessScreenY, pivotScreen.z);
        Vector3 targetPivotWorld = view.Camera.ScreenToWorldPoint(targetPivotScreen);

        Vector3 worldPos = transform.position;
        transform.position = new Vector3(worldPos.x, targetPivotWorld.y, worldPos.z);
        _isClamped = true;
    }

    private bool WouldClipAtView(WorldNameLabelScreenClampDriver.StripViewState view)
    {
        if (!view.Camera || !_hasRestTopWorldOffsetY)
            return true;

        Vector3 topWorld = new Vector3(
            transform.position.x,
            transform.position.y + _restTopWorldOffsetY,
            transform.position.z);

        if (!TryGetLabelTopScreen(topWorld, view.Camera, out Vector3 topScreen))
            return _isClamped;

        return topScreen.y > view.MaxLabelTopScreenY - screenTopMarginPixels;
    }

    private bool TryGetLabelTopWorld(out Vector3 topWorld)
    {
        topWorld = default;
        if (!_text)
            return false;

        Bounds localBounds = _text.textBounds;
        if (localBounds.size.sqrMagnitude <= 0f)
            localBounds = _text.bounds;

        if (localBounds.size.sqrMagnitude <= 0f)
            return false;

        Vector3 localTop = new Vector3(localBounds.center.x, localBounds.max.y, localBounds.center.z);
        topWorld = _text.transform.TransformPoint(localTop);
        return true;
    }

    private static bool TryGetLabelTopScreen(Vector3 topWorld, Camera camera, out Vector3 topScreen)
    {
        topScreen = default;
        if (!camera)
            return false;

        topScreen = camera.WorldToScreenPoint(topWorld);
        return topScreen.z > 0f;
    }

    private void CacheRestTopWorldOffset(Vector3 topWorld)
    {
        _restTopWorldOffsetY = topWorld.y - transform.position.y;
        _hasRestTopWorldOffsetY = true;
    }

    private void RestoreRestPosition()
    {
        if (!_hasRestPosition)
            CacheRestPosition();

        if (_useAnchoredRest && _rectTransform)
            _rectTransform.anchoredPosition = _restAnchoredPosition;
        else
            transform.localPosition = _restLocalPosition;
    }

    private void CacheRestPosition()
    {
        _rectTransform = transform as RectTransform;
        if (_rectTransform)
        {
            _restAnchoredPosition = _rectTransform.anchoredPosition;
            _useAnchoredRest = true;
        }
        else
        {
            _restLocalPosition = transform.localPosition;
            _useAnchoredRest = false;
        }

        _hasRestPosition = true;
    }
}
