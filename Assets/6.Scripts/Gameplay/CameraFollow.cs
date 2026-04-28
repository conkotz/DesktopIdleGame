using UnityEngine;

/// <summary>
/// Orthographic camera: smooth X follow with world bounds clamp. Y/Z stay fixed at initial values.
/// Half-width = <see cref="Camera.orthographicSize"/> * <see cref="Camera.aspect"/> (recalculated each frame).
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

    [Tooltip("Initial orthographic half-height; also controlled at runtime via OrthographicSize / SetOrthographicSize.")]
    [Min(0.01f)]
    [SerializeField] private float orthographicSize = 5f;

    private Camera _cam;
    private float _fixedY;
    private float _fixedZ;

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

        ApplyOrthographicSizeInternal(orthographicSize);
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

    private void LateUpdate()
    {
        if (!_cam || !_cam.orthographic)
            return;

        float halfWidth = _cam.orthographicSize * _cam.aspect;

        float targetX = target ? target.position.x : transform.position.x;

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

            targetX = Mathf.Clamp(targetX, minX, maxX);
        }

        float smoothT = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        float x = Mathf.Lerp(transform.position.x, targetX, smoothT);

        Vector3 p = transform.position;
        p.x = x;
        p.y = _fixedY;
        p.z = _fixedZ;
        transform.position = p;
    }
}
