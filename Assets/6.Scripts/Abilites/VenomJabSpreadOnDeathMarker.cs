using UnityEngine;

/// <summary>
/// Attached to an enemy when Venom Jab's spread upgrade is active.
/// If the enemy dies before expiry, spread the stored poison payload to nearby enemies.
/// </summary>
[DisallowMultipleComponent]
public class VenomJabSpreadOnDeathMarker : MonoBehaviour
{
    private PoisonPayload payload;
    private float expiresAt;
    private float range;

    private EnemyBaseController enemy;

    public void Arm(PoisonPayload payload, float expiresAt, float range)
    {
        this.payload = payload;
        this.expiresAt = expiresAt;
        this.range = Mathf.Max(0f, range);
    }

    private void Awake()
    {
        enemy = GetComponent<EnemyBaseController>();
    }

    private void OnEnable()
    {
        if (enemy == null)
            enemy = GetComponent<EnemyBaseController>();
        if (enemy != null)
            enemy.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        if (Time.time >= expiresAt)
            Destroy(this);
    }

    private void HandleDeath()
    {
        if (Time.time > expiresAt)
            return;

        EnemyBaseController[] all = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Vector3 origin = transform.position;
        float r2 = range * range;
        EnemyBaseController best = null;
        float bestSqr = float.PositiveInfinity;

        for (int i = 0; i < all.Length; i++)
        {
            EnemyBaseController e = all[i];
            if (e == null || e.IsDead || e == enemy)
                continue;

            float sqr = (e.transform.position - origin).sqrMagnitude;
            if (sqr > r2)
                continue;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = e;
            }
        }

        if (best == null)
            return;

        var bestAilments = best.GetComponent<AilmentController>();
        if (bestAilments == null)
            return;

        for (int s = 0; s < payload.maxStacks; s++)
            bestAilments.ApplyPoisonFromHit(payload);
    }
}

