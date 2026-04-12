using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Reusable runtime config for summons/minions: motion, duration, prefab, and embedded <see cref="MinionCombatConfig"/>
/// (inherit owner weapon vs pure minion source damage). Used by <see cref="SpectralWeaponMinion"/> and future minion types.
/// </summary>
[CreateAssetMenu(fileName = "MinionDefinition_", menuName = "Desktop Idle Game/Minions/Minion Definition")]
public class MinionDefinition : ScriptableObject, ISerializationCallbackReceiver
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

    [Header("Attach & slash (spectral weapon)")]
    [Tooltip(
        "Extra world units above the top of the enemy's sprite/collider bounds (not the pivot). " +
        "Use ~0.3–0.8 so the weapon clears the head like the player spawn anchor.")]
    [Min(0f)]
    public float attachHeightAboveEnemy = 0.45f;

    [Tooltip("World-units along +X from the enemy root (magnitude only; spectral weapon always uses this flank for stable facing).")]
    public float attachHorizontalOffsetTowardPlayer = 0.65f;

    [Tooltip(
        "Local offset from root transform (handle bottom / slash pivot in world) to the sprite's pivot. " +
        "Typical vertical axe art: pivot at sprite center, handle below → positive Y moves sprite up from the handle.")]
    public Vector3 handlePivotToSpritePivotLocal = new Vector3(0f, 0.35f, 0f);

    [Tooltip("Snap to Attached when within this distance of the hover point.")]
    [Min(0.05f)]
    public float attachArrivalDistance = 0.4f;

    [Tooltip("Blade rotation around the handle pivot during each chop (degrees toward the enemy, ~80–90 typical).")]
    [Min(0f)]
    public float attachedSlashMaxRotationDegrees = 80f;

    [Tooltip("Quick strike phase: seconds from upright to full chop (pivot fixed at handle).")]
    [Min(0.02f)]
    public float attachedSlashStrikeSeconds = 0.085f;

    [Tooltip("Time to ease back to upright after the chop. Independent of APS; slow attack speed only adds idle time between chops.")]
    [Min(0.04f)]
    public float attachedSlashReturnMinSeconds = 0.2f;

    [Tooltip("Added on top of the stable attached formula (SignedAngle toward enemy + 180°). Use ±90 if the blade reads sideways.")]
    public float attachedFacingExtraDegrees = 0f;

    [Header("Flight facing (approach — tune in Play Mode)")]
    [Tooltip("Extra Z° after SignedAngle(up, flight dir) + Attached facing extra. Try 180 if the handle leads flight instead of the blade.")]
    public float flightApproachFacingExtraDegrees = 0f;

    [Tooltip("When true, flipX follows player/visuals (home anchor lossyScale). When false, flipX is chosen automatically from which side of the enemy the player is on (world X), unless Manual flight flip X is enabled.")]
    public bool flightApproachFlipXFromPlayer = true;

    [Tooltip("When Flip X from player is off: if true, use Flight Approach Flip X only. If false (default), flipX is true when the player is to the right of the strike target (world X), false when to the left — matches separate left/right tuning.")]
    public bool flightApproachFlipXManual = false;

    [Tooltip("SpriteRenderer.flipX during approach when Flip X from player is off and Manual flight flip X is on.")]
    public bool flightApproachFlipX = false;

    [Tooltip("SpriteRenderer.flipY during approach.")]
    public bool flightApproachFlipY = true;

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

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        combatConfig = MinionCombatConfig.AfterDeserialize(combatConfig);
    }
}
