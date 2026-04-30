using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orthographic camera: smooth horizontal follow with optional dead zone and world bounds clamp.
/// On play, X snaps once to the target (clamped) so the view starts on the player instead of easing from the scene pose.
/// Y/Z stay fixed at initial values. Zoom is via <see cref="orthographicSize"/> only — not altered here each frame.
/// View half-width = <see cref="Camera.orthographicSize"/> * <see cref="Camera.aspect"/> (for bounds clamp).
/// Dead zone scales with <see cref="StripCameraController.widthNormalized"/> (not strip height): narrow strip (≤ half
/// screen width) → 0 dead zone; full width → <see cref="deadZoneWidth"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(103)]
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private string targetTag = "Player";

    [Tooltip("Strip layout driver (same GameObject as strip camera). Dead zone ramps from strip width normalized.")]
    [SerializeField] private StripCameraController stripController;

    [Tooltip("Smooth follow; higher = snappier. Applied as Lerp factor via 1 - exp(-followSpeed * deltaTime).")]
    [Min(0f)]
    [SerializeField] private float followSpeed = 5f;

    [Tooltip("Dead zone width in world units when the strip uses full width (width normalized = 1). Below Dead Zone Zero Below Width it goes to 0.")]
    [Min(0f)]
    [SerializeField] private float deadZoneWidth = 5f;

    [Tooltip("When strip width normalized is at or below this (fraction of screen width), dead zone is 0 so the camera tracks every move. Between this and 1, dead zone ramps up linearly to Dead Zone Width.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float deadZoneZeroBelowWidthNormalized = 0.5f;

    [Tooltip("Shifts the dead-zone center along +X (world units). Scales with dead zone ramp.")]
    [SerializeField] private float deadZoneForwardOffset = 0.4f;

    [Tooltip("Scales horizontal target velocity into forward lead (dead zone shifts with movement); result is clamped by velocityLeadMax. 0 disables.")]
    [Min(0f)]
    [SerializeField] private float velocityLeadPerSpeed = 0.06f;

    [Tooltip("Caps the velocity-driven part of the forward lead (world units). Scales with dead zone ramp.")]
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

    [Tooltip("Frames after a scene load before the first X snap runs (spawn/teleport coroutines usually move the player a few frames in).")]
    [Min(0)]
    [SerializeField] private int initialSnapDelayFramesAfterLoad = 3;

    /// <summary>Do not snap before this <see cref="Time.frameCount"/> so <see cref="PlayerSpawnController"/> can reposition first.</summary>
    private int _snapEarliestFrame;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (!_cam.orthographic)
            Debug.LogWarning("[CameraFollow] Camera should be orthographic.", this);

        if (!stripController)
            TryGetComponent(out stripController);

        _fixedY = transform.position.y;
        _fixedZ = transform.position.z;

        if (!target && !string.IsNullOrWhiteSpace(targetTag))
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }

        CacheTargetFollowExtras();
        // StripCameraController owns Camera.orthographicSize (keyboard + save). Applying our serialized ortho here
        // runs after StripCamera Awake but before StripCamera OnEnable and overwrites persisted zoom until the next Apply.
        if (!stripController)
            ApplyOrthographicSizeInternal(orthographicSize);

        ScheduleInitialSnapAfterDelay();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
            return;

        _didInitialSnapToTarget = false;
        RebindTargetFromTag();
        ScheduleInitialSnapAfterDelay();
    }

    private void ScheduleInitialSnapAfterDelay()
    {
        if (!Application.isPlaying)
            return;

        _snapEarliestFrame = Time.frameCount + Mathf.Max(0, initialSnapDelayFramesAfterLoad);
    }

    private void RebindTargetFromTag()
    {
        if (target && target.gameObject.activeInHierarchy)
        {
            CacheTargetFollowExtras();
            return;
        }

        if (!string.IsNullOrWhiteSpace(targetTag))
        {
            GameObject go = GameObject.FindGameObjectWithTag(targetTag);
            if (go)
                target = go.transform;
        }

        CacheTargetFollowExtras();
    }

    private void OnValidate()
    {
        if (!stripController && TryGetComponent(out StripCameraController sc))
            stripController = sc;

        CacheTargetFollowExtras();
    }

    private void CacheTargetFollowExtras()
    {
        _targetRb = null;
        if (!target)
            return;

        target.TryGetComponent(out _targetRb);
    }

    /// <summary>0 = no dead zone (narrow strip). 1 = full <see cref="deadZoneWidth"/> / offset / velocity lead.</summary>
    private float GetDeadZoneRampFactor()
    {
        if (!stripController)
            return 1f;

        float w = stripController.WidthNormalized;
        float cutoff = Mathf.Clamp(deadZoneZeroBelowWidthNormalized, 0.05f, 0.95f);
        if (w <= cutoff)
            return 0f;

        float denom = 1f - cutoff;
        return denom > 1e-5f ? Mathf.Clamp01((w - cutoff) / denom) : 1f;
    }

    /// <summary>Dead-zone midpoint offset from camera X: static bias + clamped horizontal-velocity lead.</summary>
    private float GetDeadZoneForwardLead(float playerXVelocity, float ramp)
    {
        float maxLead = velocityLeadMax * ramp;
        float offset = deadZoneForwardOffset * ramp;

        float dynamicLead = 0f;
        if (velocityLeadPerSpeed > 0f &&
            maxLead > 0f &&
            Mathf.Abs(playerXVelocity) > velocityLeadDeadzone)
        {
            dynamicLead = Mathf.Clamp(
                playerXVelocity * velocityLeadPerSpeed,
                -maxLead,
                maxLead);
        }

        return offset + dynamicLead;
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

        if (Time.frameCount < _snapEarliestFrame)
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
        float ramp = GetDeadZoneRampFactor();
        float effDeadZoneWidth = deadZoneWidth * ramp;

        if (TryInitialSnapToPlayerX(halfWidth))
            return;

        float camX = transform.position.x;
        float px = target ? target.position.x : camX;
        float vx = _targetRb ? _targetRb.linearVelocity.x : 0f;
        float lead = GetDeadZoneForwardLead(vx, ramp);

        float targetCamX;
        if (effDeadZoneWidth <= 0f)
        {
            targetCamX = px;
        }
        else
        {
            float halfDead = effDeadZoneWidth * 0.5f;
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
