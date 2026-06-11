using UnityEngine;

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
    [Tooltip("Seconds after combat starts before the first dash.")]
    public float shadowDashFirstDelaySeconds = 5f;

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

    [Header("Ability VFX")]
    [Tooltip("When enabled, disengage/shadow-dash teleports use Ability Smoke colors instead of player Shadow Strike purple.")]
    public bool useCustomAbilitySmokeColor;

    public Color abilitySmokeCoreColor = new Color(0.35f, 0.82f, 0.4f, 0.9f);

    public Color abilitySmokeEdgeColor = new Color(0.2f, 0.58f, 0.28f, 0.85f);
}
