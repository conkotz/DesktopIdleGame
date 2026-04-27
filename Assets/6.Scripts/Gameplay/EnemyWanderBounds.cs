using UnityEngine;

/// <summary>
/// Optional gameplay strip: assign a <see cref="RectTransform"/> (frame / strip under your UI canvas).
/// Enemies with idle wander read world-space X bounds from this rect so they do not walk off-screen.
/// Place one instance in the gameplay scene; <see cref="Instance"/> is used by <see cref="EnemyBaseController"/>.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay/Enemy Wander Bounds")]
[DisallowMultipleComponent]
public class EnemyWanderBounds : MonoBehaviour
{
    public static EnemyWanderBounds Instance { get; private set; }

    [Header("Strip Camera")]
    [Tooltip("When enabled, enemies use the visible horizontal world bounds of the strip camera.")]
    [SerializeField] private bool useStripCameraBounds = true;

    [Tooltip("Camera that renders the gameplay strip. If empty, this is resolved from StripCameraController.")]
    [SerializeField] private Camera stripCamera;

    [Tooltip("UI frame or strip. World X extent of this rect is used as the horizontal wander corridor.")]
    [SerializeField] private RectTransform boundsRect;

    [Tooltip("Shrinks the usable range from each horizontal edge (world units).")]
    [Min(0f)]
    [SerializeField] private float horizontalEdgePadding = 0.2f;

    private readonly Vector3[] _corners = new Vector3[4];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        ResolveStripCamera();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        horizontalEdgePadding = Mathf.Max(0f, horizontalEdgePadding);
    }

    /// <summary>
    /// Returns horizontal world X limits enemies may use for idle pacing (after padding).
    /// </summary>
    public bool TryGetWorldXBounds(out float minX, out float maxX)
    {
        minX = maxX = 0f;

        if (useStripCameraBounds && TryGetStripCameraWorldXBounds(out minX, out maxX))
            return true;

        if (!boundsRect)
            return false;

        boundsRect.GetWorldCorners(_corners);
        float rawMin = Mathf.Min(_corners[0].x, _corners[1].x, _corners[2].x, _corners[3].x);
        float rawMax = Mathf.Max(_corners[0].x, _corners[1].x, _corners[2].x, _corners[3].x);

        minX = rawMin + horizontalEdgePadding;
        maxX = rawMax - horizontalEdgePadding;
        if (minX > maxX)
        {
            float t = minX;
            minX = maxX;
            maxX = t;
        }

        return minX < maxX;
    }

    private bool TryGetStripCameraWorldXBounds(out float minX, out float maxX)
    {
        minX = maxX = 0f;
        ResolveStripCamera();

        if (!stripCamera)
            return false;

        float rawMin;
        float rawMax;

        if (stripCamera.orthographic)
        {
            float halfWidth = stripCamera.orthographicSize * stripCamera.aspect;
            rawMin = stripCamera.transform.position.x - halfWidth;
            rawMax = stripCamera.transform.position.x + halfWidth;
        }
        else
        {
            float zDistance = Mathf.Abs(transform.position.z - stripCamera.transform.position.z);
            Vector3 left = stripCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, zDistance));
            Vector3 right = stripCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, zDistance));
            rawMin = Mathf.Min(left.x, right.x);
            rawMax = Mathf.Max(left.x, right.x);
        }

        minX = rawMin + horizontalEdgePadding;
        maxX = rawMax - horizontalEdgePadding;
        if (minX > maxX)
        {
            float center = (rawMin + rawMax) * 0.5f;
            minX = center;
            maxX = center;
        }

        return minX < maxX;
    }

    private void ResolveStripCamera()
    {
        if (stripCamera)
            return;

        StripCameraController controller = FindFirstObjectByType<StripCameraController>();
        if (controller)
            stripCamera = controller.GetComponent<Camera>();
    }
}
