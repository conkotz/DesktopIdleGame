using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gameplay anchor for a Galeforce twister VFX root. Damage ticks while the twister is alive.
/// </summary>
[DisallowMultipleComponent]
public sealed class GaleforceTwisterInstance : MonoBehaviour
{
    [SerializeField] private float hitRadius;
    [SerializeField] private float damageMultiplier;

    private readonly Dictionary<int, float> _lastHitTimeByEnemyId = new();

    public float HitRadius => hitRadius;
    public float DamageMultiplier => damageMultiplier;

    public void Configure(float radius, float damageMult)
    {
        hitRadius = Mathf.Max(0.05f, radius);
        damageMultiplier = Mathf.Max(0f, damageMult);
    }

    public bool CanHitEnemy(int enemyId, float hitIntervalSeconds)
    {
        if (_lastHitTimeByEnemyId.TryGetValue(enemyId, out float lastHitAt) &&
            Time.time + 0.0001f < lastHitAt + hitIntervalSeconds)
        {
            return false;
        }

        return true;
    }

    public void RecordHit(int enemyId) => _lastHitTimeByEnemyId[enemyId] = Time.time;
}
