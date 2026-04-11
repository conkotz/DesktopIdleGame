using UnityEngine;

/// <summary>
/// Attached to an enemy when Venom Jab's spread upgrade is active.
/// If the enemy dies before expiry, spread the stored poison payload to all other enemies in radial range.
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

        combat?.ApplyPoisonContagionSpread(enemy, payload);
    }
}

