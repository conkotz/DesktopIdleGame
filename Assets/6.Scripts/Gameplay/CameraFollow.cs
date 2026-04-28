using UnityEngine;

/// <summary>
/// Orthographic camera: smooth horizontal follow with optional dead zone and world bounds clamp.
/// On play, X snaps once to the target (clamped) so the view starts on the player instead of easing from the scene pose.
/// Y/Z stay fixed at initial values. Zoom is via <see cref="orthographicSize"/> only — not altered here each frame.
/// View half-width = <see cref="Camera.orthographicSize"/> * <see cref="Camera.aspect"/> (for bounds clamp).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(103)]
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private string targetTag = "Player";

    [Tooltip("Smooth follow; higher = snappier. Applied as Lerp factor via 1 - exp(-followSpeed * deltaTime).")]
    [Min(0f)]
    [SerializeField] private float followSpeed = 5f;

    [Tooltip("Horizontal dead zone width in world units, centered on the camera (plus forward offset below): midpoint at camera.x + forward lead. Typical 3–6.")]
    [Min(0f)]
    [SerializeField] private float deadZoneWidth = 4f;

    [Tooltip("Shifts the dead-zone center along +X (world units). Use a small positive value to keep the playable area slightly ahead of the rig; negative shifts the zone backward.")]
    [SerializeField] private float deadZoneForwardOffset = 0.4f;

    [Tooltip("Scales horizontal target velocity into forward lead (dead zone shifts with movement); result is clamped by velocityLeadMax. 0 disables.")]
    [Min(0f)]
    [SerializeField] private float velocityLeadPerSpeed = 0.06f;

    [Tooltip("Caps the velocity-driven part of the forward lead (world units).")]
    [Min(0f)]
    [SerializeField] private float velocityLeadMax = 2f;

    [Tooltip("|Horizontal velocity| below this (world units/s) does not add velocity-based lead; static deadZoneForwardOffset still applies.")]
    [Min(0f)]
    [SerializeField] private float velocityLeadDeadzone = 0.08f;

    [Tooltip("Max horizontal speed of the camera rig in world units per second after smoothing (0 = no cap). Helps when the target teleports/dashes fast.")]
    [Min(0f)]
    [SerializeField] private float maxCameraSpeed = 22f;

    [Tooltip("Initial orthographic half-height; also controlled at runtime via OrthographicSize / SetOrthographicSize.")]
    [Min(0.01f)]
    [SerializeField] private float orthographicSize = 5f;

    private Camera _cam;
    private float _fixedY;
    private float _fixedZ;
    private Rigidbody2D _targetRb;

    /// <summary>First play-mode LateUpdate aligns X to the player so the scene/default camera pose does not tween in from nowhere.</summary>
    private bool _didInitialSnapToTarget;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (!_cam.orthographic)
            Debug.LogWarning("[CameraFollow] Camera should be orthographic.", this);

        _fixedY = transform.position.y;
        _fixedZ = transform.position.z;

        if (!target && !string.IsNullOrWhiteSpace(targetTag))
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }

        CacheTargetFollowExtras();
        ApplyOrthographicSizeInternal(orthographicSize);
    }

    private void OnValidate()
    {
        CacheTargetFollowExtras();
    }

    private void CacheTargetFollowExtras()
    {
        _targetRb = null;
        if (!target)
            return;

        target.TryGetComponent(out _targetRb);
    }

    /// <summary>Dead-zone midpoint offset from camera X: static bias + clamped horizontal-velocity lead.</summary>
    private float GetDeadZoneForwardLead(float playerXVelocity)
    {
        float dynamicLead = 0f;
        if (velocityLeadPerSpeed > 0f &&
            velocityLeadMax > 0f &&
            Mathf.Abs(playerXVelocity) > velocityLeadDeadzone)
        {
            dynamicLead = Mathf.Clamp(
                playerXVelocity * velocityLeadPerSpeed,
                -velocityLeadMax,
                velocityLeadMax);
        }

        return deadZoneForwardOffset + dynamicLead;
    }

    /// <summary>Orthographic half-height (vertical). Smaller = more zoomed in.</summary>
    public float OrthographicSize
    {
        get => _cam ? _cam.orthographicSize : orthographicSize;
        set => SetOrthographicSize(value);
    }

    public void SetOrthographicSize(float size)
    {
        orthographicSize = Mathf.Max(0.01f, size);
        ApplyOrthographicSizeInternal(orthographicSize);
    }

    private void ApplyOrthographicSizeInternal(float size)
    {
        if (_cam)
            _cam.orthographicSize = size;
    }

    /// <summary>
    /// First frame: place camera X on the player (world-bounds clamp only) so gameplay does not ease in from the prefab pose.
    /// </summary>
    /// <returns>true if this frame only performed the snap — skip smooth follow until next frame.</returns>
    private bool TryInitialSnapToPlayerX(float halfWidth)
    {
        if (_didInitialSnapToTarget || !Application.isPlaying)
            return false;

        if (!target)
        {
            _didInitialSnapToTarget = true;
            return false;
        }

        float startX = target.position.x;

        WorldBounds wb = WorldBounds.Instance;
        if (wb != null)
        {
            float minX = wb.Left + halfWidth;
            float maxX = wb.Right - halfWidth;
            if (minX > maxX)
            {
                float laneMid = (wb.Left + wb.Right) * 0.5f;
                minX = maxX = laneMid;
            }

            startX = Mathf.Clamp(startX, minX, maxX);
        }

        Vector3 p = transform.position;
        p.x = startX;
        p.y = _fixedY;
        p.z = _fixedZ;
        transform.position = p;

        _didInitialSnapToTarget = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_cam || !_cam.orthographic)
            return;

        float halfWidth = _cam.orthographicSize * _cam.aspect;

        if (TryInitialSnapToPlayerX(halfWidth))
            return;

        float camX = transform.position.x;
        float px = target ? target.position.x : camX;
        float vx = _targetRb ? _targetRb.linearVelocity.x : 0f;
        float lead = GetDeadZoneForwardLead(vx);

        float targetCamX;
        if (deadZoneWidth <= 0f)
        {
            targetCamX = px;
        }
        else
        {
            float halfDead = deadZoneWidth * 0.5f;
            float mid = camX + lead;
            float leftEdge = mid - halfDead;
            float rightEdge = mid + halfDead;

            if (px < leftEdge)
                targetCamX = px - lead + halfDead;
            else if (px > rightEdge)
                targetCamX = px - lead - halfDead;
            else
                targetCamX = camX;
        }

        WorldBounds wb = WorldBounds.Instance;
        if (wb != null)
        {
            float minX = wb.Left + halfWidth;
            float maxX = wb.Right - halfWidth;
            if (minX > maxX)
            {
                float mid = (wb.Left + wb.Right) * 0.5f;
                minX = maxX = mid;
            }

            targetCamX = Mathf.Clamp(targetCamX, minX, maxX);
        }

        float smoothT = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        float desiredX = Mathf.Lerp(camX, targetCamX, smoothT);

        float dx = desiredX - camX;
        if (maxCameraSpeed > 0f)
        {
            float maxDx = maxCameraSpeed * Time.deltaTime;
            dx = Mathf.Clamp(dx, -maxDx, maxDx);
        }

        float x = camX + dx;

        Vector3 p = transform.position;
        p.x = x;
        p.y = _fixedY;
        p.z = _fixedZ;
        transform.position = p;
    }
}
