using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnergyBoltVisual : MonoBehaviour
{
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
    private float _spawnTime;
    private bool _launched;

    public void Launch(Vector3 startPosition, Transform target, Vector3 fallbackTargetPosition, float? speedOverride = null, float? rotationOffsetOverride = null)
    {
        transform.position = startPosition;
        _target = target;
        _fallbackTargetPosition = fallbackTargetPosition;
        _spawnTime = Time.time;
        _launched = true;

        if (speedOverride.HasValue)
            speed = Mathf.Max(0.01f, speedOverride.Value);

        if (rotationOffsetOverride.HasValue)
            rotationOffsetDegrees = rotationOffsetOverride.Value;
    }

    private void Update()
    {
        if (!_launched)
            return;

        if (Time.time - _spawnTime >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = ResolveDestination();
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

    private Vector3 ResolveDestination()
    {
        if (_target == null)
            return _fallbackTargetPosition;

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
