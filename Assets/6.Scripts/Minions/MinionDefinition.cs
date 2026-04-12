using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Reusable runtime config for summons/minions: motion, duration, prefab, and embedded <see cref="MinionCombatConfig"/>
/// (inherit owner split vs internal base split). Used by <see cref="SpectralWeaponMinion"/> and future minion types.
/// </summary>
[CreateAssetMenu(fileName = "MinionDefinition_", menuName = "Desktop Idle Game/Minions/Minion Definition")]
public class MinionDefinition : ScriptableObject
{
    [Header("Prefab")]
    [Tooltip("Prefab must include a SpectralWeaponMinion (or future runtime) on the root or a child.")]
    public GameObject runtimePrefab;

    [Header("Lifetime")]
    [FormerlySerializedAs("summonDurationSeconds")]
    [Min(0.1f)]
    [Tooltip("Seconds before the summon despawns.")]
    public float summonDuration = 10f;

    [Header("Minion combat (Phase 2)")]
    public MinionCombatConfig combatConfig;

    [Header("Targeting & motion")]
    [Tooltip("Search radius from home anchor for nearest living enemy.")]
    [Min(0.5f)]
    public float attackRange = 9f;

    [Tooltip("Reach to count as a hit when overlapping target.")]
    [Min(0.05f)]
    public float hitRadius = 0.42f;

    [Min(0.01f)]
    public float idleFollowSpeed = 7f;

    [Min(0.01f)]
    public float launchSpeed = 16f;

    [Min(0.01f)]
    public float returnSpeed = 13f;

    [Tooltip("World units added above the chord for the overhead swing (scaled down for very close targets).")]
    [Min(0f)]
    public float attackArcHeight = 2.35f;

    [Tooltip("Where the arc peaks between home and enemy (lower = more windup behind / higher apex toward you).")]
    [Range(0.18f, 0.5f)]
    public float attackArcPeakAlong = 0.3f;

    [Tooltip("Degrees added to motion tangent for sprite blade alignment (2D: 0 if blade points along travel, often ~±90 if art is vertical at rest).")]
    public float attackSwingRotationOffsetDegrees = 0f;

    [Tooltip("Arc parameter (0–1) where rotation starts easing from path tangent toward a horizontal chop. Lower = longer tangent-following phase.")]
    [Range(0.35f, 0.92f)]
    public float attackStrikeHorizontalBlendStart = 0.58f;

    [Tooltip("Extra degrees on the horizontal finish (e.g. -6 for a slight downward tip — “almost” horizontal).")]
    public float attackStrikeHorizontalOffsetDegrees = 0f;

    [Header("Idle wobble")]
    [Tooltip("Horizontal sway amplitude (world units).")]
    public float wobbleAmplitudeX = 0.035f;

    [Tooltip("Vertical bob amplitude (world units).")]
    public float wobbleAmplitudeY = 0.07f;

    [Min(0.01f)]
    public float wobbleFrequency = 2f;

    [Min(0f)]
    public float rotationWobbleDegrees = 3.5f;

    [Header("Visual")]
    [Tooltip("Uniform world scale for the sprite root.")]
    [Min(0.05f)]
    public float visualWorldScale = 0.85f;

    public Color spectralTint = new Color(0.62f, 0.82f, 1f, 0.9f);

    [Tooltip("Used when no sprite is passed at Initialize.")]
    public Sprite placeholderWeaponSprite;

    [Tooltip("SpriteRenderer.sortingOrder.")]
    public int spriteSortingOrder = 4;
}
