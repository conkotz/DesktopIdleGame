using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileVisual : MonoBehaviour
{
    public enum FlightPathMode
    {
        /// <summary>Homes toward target each frame (straight line).</summary>
        Straight,
        /// <summary>Quadratic Bezier lob; end point locked at launch (stable arc).</summary>
        Arc
    }

    [Header("Flight")]
    [SerializeField] private FlightPathMode flightPath = FlightPathMode.Straight;
    [Tooltip("Minimum world-space lift at the arc midpoint (always added).")]
    [SerializeField] private float arcHeight = 0.35f;
    [Tooltip("Extra lift per unit of horizontal separation between start and end (2D lane: |Δx|). Makes long shots more arched so the arrow banks more in flight.")]
    [SerializeField] private float arcHeightPerHorizontalUnit = 0.32f;
    [SerializeField] private float arcPeakMin = 0.2f;
    [SerializeField] private float arcPeakMax = 6f;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float maxLifetime = 4f;
    [Tooltip("Offset in degrees if sprite forward is not +X (right).")]
    [SerializeField] private float rotationOffsetDegrees = 0f;

    [Header("Impact (Optional)")]
    [SerializeField] private Transform impactSpawnPoint;
    [SerializeField] private GameObject impactEffectPrefab;

    private Transform _target;
    private Vector3 _targetFallbackWorld;
    private float _spawnTime;
    private bool _launched;

    // Arc path (locked at launch)
    private Vector3 _arcP0;
    private Vector3 _arcP1;
    private Vector3 _arcP2;
    private float _arcLength;
    private float _arcT;
    private float _arcDuration;

    /// <summary>Seconds to complete flight at current speed (set in Launch). Use for syncing damage delay.</summary>
    public float EstimatedTravelTime { get; private set; }

    public float ConfiguredSpeed => Mathf.Max(0.01f, speed);

    public void Launch(Vector3 startPosition, Transform target, Vector3 fallbackTargetPosition, float? speedOverride = null, float? rotationOffsetOverride = null)
    {
        transform.position = startPosition;
        _target = target;
        _targetFallbackWorld = fallbackTargetPosition;
        _spawnTime = Time.time;
        _launched = true;
        EstimatedTravelTime = 0f;

        if (speedOverride.HasValue)
            speed = Mathf.Max(0.01f, speedOverride.Value);

        if (rotationOffsetOverride.HasValue)
            rotationOffsetDegrees = rotationOffsetOverride.Value;

        float v = Mathf.Max(0.01f, speed);

        if (flightPath == FlightPathMode.Arc)
        {
            _arcP0 = startPosition;
            _arcP2 = fallbackTargetPosition;
            float horizontalSpan = Mathf.Abs(_arcP2.x - _arcP0.x);
            float peak = arcHeight + arcHeightPerHorizontalUnit * horizontalSpan;
            peak = Mathf.Clamp(peak, arcPeakMin, arcPeakMax);
            _arcP1 = (_arcP0 + _arcP2) * 0.5f + Vector3.up * peak;
            _arcLength = ApproximateQuadraticBezierLength(_arcP0, _arcP1, _arcP2, 24);
            _arcDuration = _arcLength / v;
            _arcT = 0f;
            EstimatedTravelTime = _arcDuration;
        }
        else
        {
            Vector3 end = fallbackTargetPosition;
            float d = Vector3.Distance(startPosition, end);
            EstimatedTravelTime = d / v;
        }
    }

    private void Update()
    {
        if (!_launched)
            return;

        if (Time.time - _spawnTime >= Mathf.Max(0.1f, maxLifetime))
        {
            Destroy(gameObject);
            return;
        }

        if (flightPath == FlightPathMode.Arc)
            TickArc();
        else
            TickStraight();
    }

    private void TickStraight()
    {
        Vector3 destination = ResolveDestination();
        Vector3 toDest = destination - transform.position;
        float distance = toDest.magnitude;

        if (distance <= 0.02f)
        {
            OnArrived();
            return;
        }

        Vector3 dir = toDest / distance;
        float step = Mathf.Max(0.01f, speed) * Time.deltaTime;
        transform.position += dir * Mathf.Min(step, distance);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void TickArc()
    {
        // Rebuild the arc each frame so the end follows a moving target (locked-at-launch P2 made shots land behind runners).
        _arcP2 = ResolveDestination();
        float horizontalSpan = Mathf.Abs(_arcP2.x - _arcP0.x);
        float peak = arcHeight + arcHeightPerHorizontalUnit * horizontalSpan;
        peak = Mathf.Clamp(peak, arcPeakMin, arcPeakMax);
        _arcP1 = (_arcP0 + _arcP2) * 0.5f + Vector3.up * peak;

        _arcLength = Mathf.Max(0.001f, ApproximateQuadraticBezierLength(_arcP0, _arcP1, _arcP2, 24));
        float deltaT = (Mathf.Max(0.01f, speed) * Time.deltaTime) / _arcLength;
        _arcT += deltaT;

        if (_arcT >= 1f)
        {
            transform.position = _arcP2;
            OnArrived();
            return;
        }

        float t = Mathf.Clamp01(_arcT);
        Vector3 pos = QuadraticBezier(_arcP0, _arcP1, _arcP2, t);
        Vector3 tan = QuadraticBezierTangent(_arcP0, _arcP1, _arcP2, t);
        transform.position = pos;

        if (tan.sqrMagnitude > 1e-8f)
        {
            tan.Normalize();
            float angle = Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private Vector3 ResolveDestination()
    {
        if (_target == null)
            return _targetFallbackWorld;

        if (_target.TryGetComponent<Collider2D>(out var col) && col != null)
            return col.bounds.center;

        var childCol = _target.GetComponentInChildren<Collider2D>();
        if (childCol != null)
            return childCol.bounds.center;

        var sr = _target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        return _target.position;
    }

    private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private static Vector3 QuadraticBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
    }

    private static float ApproximateQuadraticBezierLength(Vector3 p0, Vector3 p1, Vector3 p2, int segments)
    {
        segments = Mathf.Max(2, segments);
        Vector3 prev = p0;
        float len = 0f;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = QuadraticBezier(p0, p1, p2, t);
            len += Vector3.Distance(prev, p);
            prev = p;
        }

        return len;
    }

    private void OnArrived()
    {
        if (impactEffectPrefab != null)
        {
            Vector3 spawnPos = impactSpawnPoint != null ? impactSpawnPoint.position : transform.position;
            Instantiate(impactEffectPrefab, spawnPos, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
