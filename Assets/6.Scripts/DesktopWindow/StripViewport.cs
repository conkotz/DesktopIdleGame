using UnityEngine;

[ExecuteAlways]
public class StripViewport : MonoBehaviour
{
    [SerializeField] private Camera stripCamera;

    [Header("Strip size in pixels")]
    [SerializeField] private int stripHeightPx = 360;

    [Header("Position (normalized)")]
    [Tooltip("Bottom of the strip in normalized screen space. 0 = bottom, 1 = top.")]
    [Range(0f, 1f)]
    [SerializeField] private float bottomNormalized = 0f;

    [Header("Width (normalized)")]
    [Tooltip("Width of the strip in normalized screen space. 1 = full width.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float widthNormalized = 1f;

    [Tooltip("Left edge of the strip in normalized screen space.")]
    [Range(0f, 1f)]
    [SerializeField] private float leftNormalized = 0f;

    [Header("Behaviour")]
    [Tooltip("Apply only when values/resolution change (recommended).")]
    [SerializeField] private bool applyOnlyWhenDirty = true;

    private int _lastScreenW;
    private int _lastScreenH;
    private float _lastBottom;
    private int _lastStripHeightPx;
    private float _lastWidth;
    private float _lastLeft;

    private void OnEnable()
    {
        if (!stripCamera) stripCamera = GetComponent<Camera>();
        MarkDirty();
        ApplyIfNeeded(force: true);
    }

    private void OnValidate()
    {
        MarkDirty();
        ApplyIfNeeded(force: true);
    }

    private void Update()
    {
        ApplyIfNeeded(force: false);
    }

    private void MarkDirty()
    {
        _lastScreenW = -1;
        _lastScreenH = -1;
        _lastBottom = float.NaN;
        _lastStripHeightPx = -1;
        _lastWidth = float.NaN;
        _lastLeft = float.NaN;
    }

    private void ApplyIfNeeded(bool force)
    {
        if (!stripCamera) return;
        if (Screen.height <= 0 || Screen.width <= 0) return;

        bool resolutionChanged = (Screen.width != _lastScreenW) || (Screen.height != _lastScreenH);
        bool valuesChanged =
            !Mathf.Approximately(bottomNormalized, _lastBottom) ||
            stripHeightPx != _lastStripHeightPx ||
            !Mathf.Approximately(widthNormalized, _lastWidth) ||
            !Mathf.Approximately(leftNormalized, _lastLeft);

        if (!force && applyOnlyWhenDirty && !resolutionChanged && !valuesChanged)
            return;

        Apply();

        _lastScreenW = Screen.width;
        _lastScreenH = Screen.height;
        _lastBottom = bottomNormalized;
        _lastStripHeightPx = stripHeightPx;
        _lastWidth = widthNormalized;
        _lastLeft = leftNormalized;
    }

    private void Apply()
    {
        float h = Mathf.Clamp01((float)stripHeightPx / Screen.height);

        float y = Mathf.Clamp01(bottomNormalized);
        y = Mathf.Clamp(y, 0f, 1f - h);

        float w = Mathf.Clamp(widthNormalized, 0.1f, 1f);
        float x = Mathf.Clamp01(leftNormalized);

        // Ensure the strip stays fully on screen
        x = Mathf.Clamp(x, 0f, 1f - w);

        stripCamera.rect = new Rect(x, y, w, h);
    }

    // ---- Public API ----

    public void SetBottomNormalized(float value)
    {
        bottomNormalized = value;
        ApplyIfNeeded(force: true);
    }

    public void SetWidthNormalized(float value)
    {
        widthNormalized = Mathf.Clamp(value, 0.1f, 1f);
        ApplyIfNeeded(force: true);
    }

    public void SetLeftNormalized(float value)
    {
        leftNormalized = Mathf.Clamp01(value);
        ApplyIfNeeded(force: true);
    }

    // Drag using pixels and convert internally
    public void AddPixels(float deltaYPx)
    {
        if (Screen.height <= 0) return;
        float deltaNorm = deltaYPx / Screen.height;
        SetBottomNormalized(bottomNormalized + deltaNorm);
    }

    public float GetBottomNormalized() => bottomNormalized;
    public int GetStripHeightPx() => stripHeightPx;

    public float GetWidthNormalized() => widthNormalized;
    public float GetLeftNormalized() => leftNormalized;
}