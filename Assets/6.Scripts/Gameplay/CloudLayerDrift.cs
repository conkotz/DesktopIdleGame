using UnityEngine;

/// <summary>
/// Horizontally drifts a cloud layer by moving this parent; all child clouds follow.
/// Attach only to layer roots such as CloudsBack / CloudsFront (not individual clouds).
/// </summary>
[DisallowMultipleComponent]
public class CloudLayerDrift : MonoBehaviour
{
    [Tooltip("Units per second along local X.")]
    [SerializeField] private float speed = 0.05f;

    [Tooltip("Drift direction along local X. Use -1 for left, +1 for right.")]
    [SerializeField] private float direction = -1f;

    [Tooltip("Distance traveled before the layer wraps back (world/local X span of the cloud strip).")]
    [SerializeField, Min(0f)] private float loopWidth = 20f;

    private Vector3 _baseLocalPosition;
    private float _scrollOffset;
    private bool _hasBase;

    private void OnEnable()
    {
        CaptureBaseLocalPosition();
    }

    private void OnValidate()
    {
        if (loopWidth < 0f)
            loopWidth = 0f;
    }

    private void Update()
    {
        if (!_hasBase)
            CaptureBaseLocalPosition();

        if (speed <= 0f || Mathf.Approximately(direction, 0f))
        {
            transform.localPosition = _baseLocalPosition;
            return;
        }

        float step = speed * Mathf.Sign(direction) * Time.deltaTime;
        _scrollOffset += step;
        WrapScrollOffset();

        transform.localPosition = _baseLocalPosition + new Vector3(_scrollOffset, 0f, 0f);
    }

    private void WrapScrollOffset()
    {
        if (loopWidth <= 0.001f)
            return;

        float width = loopWidth;
        while (_scrollOffset >= width)
            _scrollOffset -= width;
        while (_scrollOffset < 0f)
            _scrollOffset += width;
    }

    private void CaptureBaseLocalPosition()
    {
        _baseLocalPosition = transform.localPosition;
        _scrollOffset = 0f;
        _hasBase = true;
    }
}
