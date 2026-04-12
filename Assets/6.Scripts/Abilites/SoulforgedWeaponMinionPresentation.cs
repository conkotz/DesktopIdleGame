using System;
using UnityEngine;

/// <summary>
/// Motion, attach, slash, and VFX for <see cref="SoulforgedWeaponMinion"/>.
/// Serialized on <see cref="PlayerAbilityController"/> so a shared <see cref="MinionDefinition"/> stays prefab + combat + lifetime only.
/// </summary>
[Serializable]
public struct SoulforgedWeaponMinionPresentation
{
    [Header("Targeting & motion")]
    [Tooltip("Search radius from home anchor for nearest living enemy.")]
    [Min(0.5f)]
    public float attackRange;

    [Tooltip("Reach to count as a hit when overlapping target.")]
    [Min(0.05f)]
    public float hitRadius;

    [Min(0.01f)]
    public float idleFollowSpeed;

    [Min(0.01f)]
    public float launchSpeed;

    [Min(0.01f)]
    public float returnSpeed;

    [Header("Attach & slash (Soulforged Weapon)")]
    [Tooltip(
        "Extra world units above the top of the enemy's sprite/collider bounds (not the pivot). " +
        "Use ~0.3–0.8 so the weapon clears the head like the player spawn anchor.")]
    [Min(0f)]
    public float attachHeightAboveEnemy;

    [Tooltip("World-units along +X from the enemy root (magnitude only; Soulforged Weapon always uses this flank for stable facing).")]
    public float attachHorizontalOffsetTowardPlayer;

    [Tooltip(
        "Local offset from root transform (handle bottom / slash pivot in world) to the sprite's pivot. " +
        "Typical vertical axe art: pivot at sprite center, handle below → positive Y moves sprite up from the handle.")]
    public Vector3 handlePivotToSpritePivotLocal;

    [Tooltip("Snap to Attached when within this distance of the hover point.")]
    [Min(0.05f)]
    public float attachArrivalDistance;

    [Tooltip("Blade rotation around the handle pivot during each chop (degrees toward the enemy, ~80–90 typical).")]
    [Min(0f)]
    public float attachedSlashMaxRotationDegrees;

    [Tooltip("Quick strike phase: seconds from upright to full chop (pivot fixed at handle).")]
    [Min(0.02f)]
    public float attachedSlashStrikeSeconds;

    [Tooltip("Time to ease back to upright after the chop. Independent of APS; slow attack speed only adds idle time between chops.")]
    [Min(0.04f)]
    public float attachedSlashReturnMinSeconds;

    [Tooltip("Added on top of the stable attached formula (SignedAngle toward enemy + 180°). Use ±90 if the blade reads sideways.")]
    public float attachedFacingExtraDegrees;

    [Header("Flight facing (approach — tune in Play Mode)")]
    [Tooltip("Extra Z° after SignedAngle(up, flight dir) + Attached facing extra. Try 180 if the handle leads flight instead of the blade.")]
    public float flightApproachFacingExtraDegrees;

    [Tooltip("When true, flipX follows player/visuals (home anchor lossyScale). When false, flipX is chosen automatically from which side of the enemy the player is on (world X), unless Manual flight flip X is enabled.")]
    public bool flightApproachFlipXFromPlayer;

    [Tooltip("When Flip X from player is off: if true, use Flight Approach Flip X only. If false (default), flipX is true when the player is to the right of the strike target (world X), false when to the left — matches separate left/right tuning.")]
    public bool flightApproachFlipXManual;

    [Tooltip("SpriteRenderer.flipX during approach when Flip X from player is off and Manual flight flip X is on.")]
    public bool flightApproachFlipX;

    [Tooltip("SpriteRenderer.flipY during approach.")]
    public bool flightApproachFlipY;

    [Header("Idle wobble")]
    [Tooltip("Horizontal sway amplitude (world units).")]
    public float wobbleAmplitudeX;

    [Tooltip("Vertical bob amplitude (world units).")]
    public float wobbleAmplitudeY;

    [Min(0.01f)]
    public float wobbleFrequency;

    [Min(0f)]
    public float rotationWobbleDegrees;

    [Header("Visual")]
    [Tooltip("Uniform world scale for the sprite root.")]
    [Min(0.05f)]
    public float visualWorldScale;

    public Color spectralTint;

    [Tooltip("Used when no sprite is passed at Initialize.")]
    public Sprite placeholderWeaponSprite;

    [Tooltip("SpriteRenderer.sortingOrder.")]
    public int spriteSortingOrder;

    /// <summary>Matches legacy <see cref="MinionDefinition"/> defaults when an ability has no presentation data yet.</summary>
    public static SoulforgedWeaponMinionPresentation Default => new SoulforgedWeaponMinionPresentation
    {
        attackRange = 9f,
        hitRadius = 0.42f,
        idleFollowSpeed = 7f,
        launchSpeed = 16f,
        returnSpeed = 13f,
        attachHeightAboveEnemy = 0.45f,
        attachHorizontalOffsetTowardPlayer = 0.65f,
        handlePivotToSpritePivotLocal = new Vector3(0f, 0.35f, 0f),
        attachArrivalDistance = 0.4f,
        attachedSlashMaxRotationDegrees = 80f,
        attachedSlashStrikeSeconds = 0.085f,
        attachedSlashReturnMinSeconds = 0.2f,
        attachedFacingExtraDegrees = 0f,
        flightApproachFacingExtraDegrees = 0f,
        flightApproachFlipXFromPlayer = true,
        flightApproachFlipXManual = false,
        flightApproachFlipX = false,
        flightApproachFlipY = true,
        wobbleAmplitudeX = 0.035f,
        wobbleAmplitudeY = 0.07f,
        wobbleFrequency = 2f,
        rotationWobbleDegrees = 3.5f,
        visualWorldScale = 0.85f,
        spectralTint = new Color(0.62f, 0.82f, 1f, 0.9f),
        placeholderWeaponSprite = null,
        spriteSortingOrder = 4
    };

    /// <summary>Use serialized values from <see cref="PlayerAbilityController"/> when attack range is set; otherwise fall back to <see cref="Default"/>.</summary>
    public static SoulforgedWeaponMinionPresentation Resolve(SoulforgedWeaponMinionPresentation configured)
    {
        if (configured.attackRange >= 0.5f)
            return configured;
        return Default;
    }
}
