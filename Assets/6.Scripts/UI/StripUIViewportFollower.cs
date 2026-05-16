using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(110)]
public sealed class StripUIViewportFollower : MonoBehaviour
{
    [SerializeField] private Camera stripCamera;
    [SerializeField] private RectTransform targetRect;

    [Tooltip("When the strip camera is not assigned, auto-discover it from a StripCameraController in the scene. Lets runtime-created overlays (cave dimmer, helper modal dimmer) reuse this follower without scene wiring.")]
    [SerializeField] private bool autoFindStripCamera = true;

    [Tooltip("Expands the rect beyond the strip camera viewport by this many canvas pixels per edge (negative offsetMin / positive offsetMax). Use 1–2 for black fades to hide fractional gaps next to strip bars.")]
    [SerializeField, Min(0f)] private float viewportBleedPixels;

    private Rect _lastRect = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
    private float _lastAppliedBleed = float.NaN;

    /// <summary>
    /// Rect whose anchors track <see cref="Camera.rect"/> — may differ from <see cref="Component.transform"/> when this component lives on a manager object.
    /// </summary>
    public RectTransform ViewportAlignedRect
    {
        get
        {
            if (!targetRect)
                targetRect = transform as RectTransform;
            return targetRect;
        }
    }

    private void OnEnable()
    {
        CacheTarget();
        CacheStripCamera();
        Apply(force: true);
    }

    private void OnValidate()
    {
        CacheTarget();
    }

    private void LateUpdate()
    {
        Apply(force: false);
    }

    /// <summary>
    /// Ensures anchors match <see cref="Camera.rect"/> immediately (used when strip UI opens before this component's LateUpdate).
    /// </summary>
    public void ForceApplyViewportAnchorsNow()
    {
        CacheTarget();
        CacheStripCamera();
        Apply(force: true);
    }

    /// <summary>
    /// Runtime wiring helper: assigns the strip camera (and optionally a non-self target rect) and snaps anchors immediately.
    /// Used by runtime-created overlays (e.g. cave biome overlay, helper modal dimmer) so they only cover the strip viewport, not the whole canvas/screen.
    /// </summary>
    public void Bind(Camera stripCameraSource, RectTransform optionalTargetRect = null)
    {
        stripCamera = stripCameraSource;
        if (optionalTargetRect)
            targetRect = optionalTargetRect;
        CacheTarget();
        ForceApplyViewportAnchorsNow();
    }

    /// <summary>Per-edge bleed in canvas pixels beyond <see cref="Camera.rect"/> (covers sub-pixel gaps at strip boundaries).</summary>
    public float ViewportBleedPixels
    {
        get => viewportBleedPixels;
        set
        {
            viewportBleedPixels = Mathf.Max(0f, value);
            if (isActiveAndEnabled)
                Apply(force: true);
        }
    }

    private void CacheTarget()
    {
        if (!targetRect)
            targetRect = transform as RectTransform;
    }

    private void CacheStripCamera()
    {
        if (stripCamera || !autoFindStripCamera)
            return;

        StripCameraController ctrl = FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Include);
        if (ctrl)
            stripCamera = ctrl.GetComponent<Camera>();
    }

    private void Apply(bool force)
    {
        CacheTarget();
        CacheStripCamera();

        if (!stripCamera || !targetRect)
            return;

        Rect rect = stripCamera.rect;
        float bleed = viewportBleedPixels;
        if (!force && Approximately(rect, _lastRect) && Mathf.Approximately(bleed, _lastAppliedBleed))
            return;

        targetRect.anchorMin = new Vector2(rect.xMin, rect.yMin);
        targetRect.anchorMax = new Vector2(rect.xMax, rect.yMax);
        targetRect.offsetMin = new Vector2(-bleed, -bleed);
        targetRect.offsetMax = new Vector2(bleed, bleed);

        _lastRect = rect;
        _lastAppliedBleed = bleed;
    }

    private static bool Approximately(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.width, b.width) &&
               Mathf.Approximately(a.height, b.height);
    }
}
