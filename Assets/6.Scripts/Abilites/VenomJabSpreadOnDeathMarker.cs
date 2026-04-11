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
    private PlayerCombatController combat;

    private EnemyBaseController enemy;

    public void Arm(PoisonPayload payload, float expiresAt, PlayerCombatController combat)
    {
        this.payload = payload;
        this.expiresAt = expiresAt;
        this.combat = combat;
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

        EnemyBaseController best = combat != null ? combat.FindEnemyForContagionPoisonSpread(enemy) : null;
        if (best == null)
            return;

        var bestAilments = best.GetComponent<AilmentController>();
        if (bestAilments == null)
            return;

        for (int s = 0; s < payload.maxStacks; s++)
            bestAilments.ApplyPoisonFromHit(payload);
    }
}

