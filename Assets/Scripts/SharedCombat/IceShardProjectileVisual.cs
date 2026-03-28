using System;
using UnityEngine;

[DisallowMultipleComponent]
public class IceShardProjectileVisual : MonoBehaviour, IMagicProjectileVisual
{
    [Header("Spawn")]
    [Tooltip("Added to the enemy center so the shard appears above and offset (e.g. up-left for a ~45° approach).")]
    [SerializeField] private Vector2 spawnOffsetFromTarget = new(-2.2f, 3.4f);

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

    public void Launch(Vector3 _, Transform target, Vector3 fallbackTargetPosition, float? speedOverride = null, float? rotationOffsetOverride = null)
    {
        _target = target;
        _fallbackTargetPosition = fallbackTargetPosition;

        Vector3 aimPoint = MagicProjectileTargeting.ResolveDestination(target, fallbackTargetPosition);
        transform.position = aimPoint + (Vector3)spawnOffsetFromTarget;

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
