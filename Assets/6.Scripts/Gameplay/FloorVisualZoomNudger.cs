using UnityEngine;

/// <summary>
/// Nudges a floor visuals transform in world space based on orthographic zoom so it stays in view.
/// Designed for the strip camera "Zoom %" where 100% == the camera's baseline orthographicSize.
/// </summary>
[DefaultExecutionOrder(125)]
public sealed class FloorVisualZoomNudger : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Orthographic camera whose zoom should drive the offset (usually StripCamera).")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Transform to move up/down (e.g. WorldVisuals/FloorVisuals root).")]
    [SerializeField] private Transform floorVisuals;

    [Tooltip("Optional. If set, uses DefaultOrthoBaseline as the 100% reference.")]
    [SerializeField] private StripCameraController stripCameraController;

    [Header("Baseline (100%)")]
    [Tooltip("When enabled, captures current floorVisuals.localPosition as the baseline at Start.")]
    [SerializeField] private bool captureBaselineOnStart = true;

    [SerializeField] private Vector3 baselineLocalPosition;

    [Tooltip("If 0, will be inferred from StripCameraController.DefaultOrthoBaseline or targetCamera.orthographicSize.")]
    [SerializeField, Min(0f)] private float baselineOrthoSize = 0f;

    [Header("Step tuning")]
    [Tooltip("Percent step size. Example: 25 means apply another nudge at 125%, 150%, 175%... and at 75%, 50%, 25%...")]
    [SerializeField, Range(1f, 100f)] private float stepPercent = 25f;

    [Tooltip("If on, uses discrete steps (every stepPercent). If off, scales continuously.")]
    [SerializeField] private bool useDiscreteSteps = true;

    [Header("Pixel nudges per step")]
    [Tooltip("At >100% (zooming out), move DOWN by this many pixels per step.")]
    [SerializeField, Min(0f)] private float zoomOutPixelsPerStep = 6f;

    [Tooltip("At <100% (zooming in), move UP by this many pixels per step.")]
    [SerializeField, Min(0f)] private float zoomInPixelsPerStep = 3f;

    [Header("Smoothing")]
    [Tooltip("0 = snap. Higher = smoother (approx Hz).")]
    [SerializeField, Min(0f)] private float smoothHz = 0f;

    private Vector3 _velocity;

    private void Reset()
    {
        targetCamera = Camera.main;
        floorVisuals = transform;
        stripCameraController = FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Exclude);
    }

    private void Awake()
    {
        if (!targetCamera && stripCameraController)
            targetCamera = stripCameraController.GetComponent<Camera>();

        if (baselineOrthoSize <= 0.0001f)
        {
            if (stripCameraController && stripCameraController.DefaultOrthoBaseline > 0.0001f)
                baselineOrthoSize = stripCameraController.DefaultOrthoBaseline;
            else if (targetCamera && targetCamera.orthographic)
                baselineOrthoSize = Mathf.Max(0.01f, targetCamera.orthographicSize);
        }
    }

    private void Start()
    {
        if (captureBaselineOnStart && floorVisuals)
            baselineLocalPosition = floorVisuals.localPosition;
    }

    private void LateUpdate()
    {
        if (!targetCamera || !targetCamera.orthographic || !floorVisuals)
            return;

        float baseline = Mathf.Max(0.01f, baselineOrthoSize);
        float zoomPercent = targetCamera.orthographicSize / baseline * 100f;

        // Pixels → world units at current zoom (in the camera's pixelRect).
        float pixelHeight = Mathf.Max(1f, targetCamera.pixelRect.height);
        float worldUnitsPerPixel = (2f * targetCamera.orthographicSize) / pixelHeight;

        float stepsSigned;
        if (useDiscreteSteps)
            stepsSigned = Mathf.Floor((zoomPercent - 100f) / Mathf.Max(1f, stepPercent));
        else
            stepsSigned = (zoomPercent - 100f) / Mathf.Max(1f, stepPercent);

        // Positive stepsSigned means zooming out (>100): move down.
        float pixels = 0f;
        if (stepsSigned > 0f)
            pixels = -stepsSigned * zoomOutPixelsPerStep;
        else if (stepsSigned < 0f)
            pixels = (-stepsSigned) * zoomInPixelsPerStep;

        Vector3 targetLocal = baselineLocalPosition + new Vector3(0f, pixels * worldUnitsPerPixel, 0f);

        if (smoothHz <= 0f)
        {
            floorVisuals.localPosition = targetLocal;
            return;
        }

        float t = 1f - Mathf.Exp(-smoothHz * Time.deltaTime);
        floorVisuals.localPosition = Vector3.Lerp(floorVisuals.localPosition, targetLocal, t);
    }
}

