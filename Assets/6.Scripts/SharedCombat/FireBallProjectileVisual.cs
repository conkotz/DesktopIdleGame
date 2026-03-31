using System;
using UnityEngine;

[DisallowMultipleComponent]
public class FireBallProjectileVisual : MonoBehaviour, IMagicProjectileVisual
{
    [Header("Charge")]
    [SerializeField, Min(0f)] private float chargeDuration = 0.22f;
    [SerializeField] private Vector3 chargeStartScale = new(0.12f, 0.12f, 1f);
    [SerializeField] private Vector3 chargeEndScale = new(0.5f, 0.5f, 1f);

    [Header("Flight")]
    [SerializeField, Min(0.01f)] private float speed = 14f;
    [SerializeField, Min(0.1f)] private float maxLifetime = 4f;
    [Tooltip("Offset in degrees if sprite forward is not +X (right).")]
    [SerializeField] private float rotationOffsetDegrees = 0f;

    [Header("Impact (Optional)")]
    [SerializeField] private Transform impactSpawnPoint;
    [SerializeField] private GameObject impactEffectPrefab;

    public event Action OnImpact;

    private Transform _target;
    private Vector3 _fallbackTargetPosition;
    private float _flightSpawnTime;
    private float _chargingElapsed;
    private bool _launched;
    private bool _charging;

    public void Launch(Vector3 startPosition, Transform target, Vector3 fallbackTargetPosition, float? speedOverride = null, float? rotationOffsetOverride = null)
    {
        transform.position = startPosition;
        transform.localScale = chargeStartScale;
        _chargingElapsed = 0f;
        _target = target;
        _fallbackTargetPosition = fallbackTargetPosition;
        _launched = true;
        _charging = true;

        if (speedOverride.HasValue)
            speed = Mathf.Max(0.01f, speedOverride.Value);

        if (rotationOffsetOverride.HasValue)
            rotationOffsetDegrees = rotationOffsetOverride.Value;
    }

    private void Update()
    {
        if (!_launched)
            return;

        if (_charging)
        {
            float dur = Mathf.Max(0.0001f, chargeDuration);
            _chargingElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(_chargingElapsed / dur);
            transform.localScale = Vector3.Lerp(chargeStartScale, chargeEndScale, u);

            if (u >= 1f)
            {
                _charging = false;
                _flightSpawnTime = Time.time;
            }

            return;
        }

        if (Time.time - _flightSpawnTime >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = MagicProjectileTargeting.ResolveDestination(_target, _fallbackTargetPosition);
        Vector3 toDest = destination - transform.position;
        float distance = toDest.magnitude;

        if (distance <= 0.02f)
        {
            Arrive();
            return;
        }

        Vector3 dir = toDest / distance;
        float step = Mathf.Max(0.01f, speed) * Time.deltaTime;
        transform.position += dir * Mathf.Min(step, distance);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Arrive()
    {
        if (impactEffectPrefab != null)
        {
            Vector3 spawnPos = impactSpawnPoint != null ? impactSpawnPoint.position : transform.position;
            Instantiate(impactEffectPrefab, spawnPos, Quaternion.identity);
        }

        OnImpact?.Invoke();
        Destroy(gameObject);
    }
}
