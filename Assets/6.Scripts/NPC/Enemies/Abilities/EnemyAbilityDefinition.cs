using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Desktop Idle Game/Enemy/Enemy Ability Definition", fileName = "EnemyAbility_")]
public class EnemyAbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    public string abilityId;
    public string displayName = "Enemy Ability";

    [TextArea(2, 4)]
    public string description;

    public EnemyAbilityKind kind = EnemyAbilityKind.ShadowDashBehindPlayer;

    [Header("Shadow Dash Behind Player")]
    [Min(0f)]
    [FormerlySerializedAs("shadowDashFirstDelaySeconds")]
    [Tooltip("Minimum seconds after combat starts before the first dash.")]
    public float shadowDashFirstDelayMinSeconds = 3f;

    [Min(0f)]
    [Tooltip("Maximum seconds after combat starts before the first dash.")]
    public float shadowDashFirstDelayMaxSeconds = 6f;

    [Min(0.1f)]
    [Tooltip("Seconds between dashes after the first one.")]
    public float shadowDashRepeatIntervalSeconds = 10f;

    [Min(0f)]
    [Tooltip("How far past the player to land on the far side (matches player Shadow Strike spacing).")]
    public float shadowDashBehindDistance = AbilityCombatPower.ShadowStrikeLandBehindTargetDistance;

    [Header("Melee Disengage")]
    [Min(0f)]
    [Tooltip("Horizontal distance to move away from the player.")]
    public float disengageDistance = 5f;

    [Range(0f, 1f)]
    [Tooltip("Chance the disengage triggers on the first qualifying melee hit this engagement.")]
    public float disengageFirstHitChance = 0.5f;

    [Min(0f)]
    [Tooltip("Player must be within this horizontal distance (center-to-center) for a melee hit to trigger disengage.")]
    public float disengageMeleeRange = 3f;

    [Min(0.1f)]
    [Tooltip("Horizontal speed while disengaging backward.")]
    public float disengageMoveSpeed = 12f;

    [Min(0f)]
    [Tooltip("Y offset above the floor while moving backward.")]
    public float disengageAirHeight = 0.5f;

    [Header("Pounce On Range Break")]
    [Min(0.1f)]
    [Tooltip("Player must exceed this horizontal distance from the enemy to trigger a pounce.")]
    public float pounceTriggerRange = 5f;

    [Min(0f)]
    [Tooltip("Cooldown between pounce attempts.")]
    public float pounceCooldownSeconds = 5f;

    [Min(0.05f)]
    [Tooltip("Seconds the target circle is shown before the bear launches.")]
    public float pounceTelegraphSeconds = 2f;

    [Min(0.1f)]
    [Tooltip("Horizontal flight speed during the pounce arc.")]
    public float pounceMoveSpeed = 14f;

    [Min(0f)]
    [Tooltip("Peak Y offset during the pounce flight path.")]
    public float pounceAirHeight = 0.5f;

    [Min(0.1f)]
    [Tooltip("Shockwave radius on landing; damages the player if within range on X.")]
    public float pounceShockwaveRadius = 7f;

    [Min(0.1f)]
    [Tooltip("Horizontal distance from the landing point treated as a direct hit (extra damage).")]
    public float pounceDirectHitRadius = 1.1f;

    [Min(1f)]
    [Tooltip("Damage multiplier when the player is within pounceDirectHitRadius of the landing point.")]
    public float pounceDirectHitDamageMultiplier = 1.5f;

    [Header("Low Health Enrage")]
    [Range(0.01f, 1f)]
    [Tooltip("Enrage triggers when current HP fraction falls below this value.")]
    public float enrageHealthThreshold = 0.3f;

    [Min(1f)]
    [Tooltip("Visual scale multiplier applied once when enrage triggers.")]
    public float enrageScaleMultiplier = 1.5f;

    [Min(1f)]
    [Tooltip("Multiplies attack speed when enrage triggers.")]
    public float enrageAttackSpeedMultiplier = 1.5f;

    [Min(1f)]
    [Tooltip("Multiplies chase move speed when enrage triggers.")]
    public float enrageMoveSpeedMultiplier = 1.5f;

    [Header("Toxic Fangs Attack Proc")]
    [Range(0f, 1f)]
    [Tooltip("Chance each auto-attack triggers toxic fangs.")]
    public float toxicFangsChance = 0.15f;

    [Min(0.05f)]
    [Tooltip("Seconds the spider uses a green overlay while toxic fangs is active on a hit.")]
    public float toxicFangsOverlaySeconds = 1f;

    [Min(0f)]
    [Tooltip("Bonus corruption damage added to a toxic fangs hit.")]
    public float toxicFangsBonusCorruptionDamage = 3f;

    [Header("Ability VFX")]
    [Tooltip("When enabled, disengage/shadow-dash teleports use Ability Smoke colors instead of player Shadow Strike purple.")]
    public bool useCustomAbilitySmokeColor;

    public Color abilitySmokeCoreColor = new Color(0.35f, 0.82f, 0.4f, 0.9f);

    public Color abilitySmokeEdgeColor = new Color(0.2f, 0.58f, 0.28f, 0.85f);

    public float RollShadowDashFirstDelaySeconds()
    {
        float min = Mathf.Max(0f, shadowDashFirstDelayMinSeconds);
        float max = Mathf.Max(min, shadowDashFirstDelayMaxSeconds);
        return Mathf.Approximately(min, max) ? min : Random.Range(min, max);
    }
}
