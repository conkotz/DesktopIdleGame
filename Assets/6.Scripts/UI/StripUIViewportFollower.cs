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

    private Rect _lastRect = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);

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
        Apply(force: true);
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
        if (!force && Approximately(rect, _lastRect))
            return;

        targetRect.anchorMin = new Vector2(rect.xMin, rect.yMin);
        targetRect.anchorMax = new Vector2(rect.xMax, rect.yMax);
        targetRect.offsetMin = Vector2.zero;
        targetRect.offsetMax = Vector2.zero;

        _lastRect = rect;
    }

    private static bool Approximately(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.width, b.width) &&
               Mathf.Approximately(a.height, b.height);
    }
}
