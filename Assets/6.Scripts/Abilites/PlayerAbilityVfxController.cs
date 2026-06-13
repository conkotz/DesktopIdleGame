using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

/// <summary>
/// World VFX for player abilities (trails, particles, range rings). Ability rules stay on <see cref="PlayerAbilityController"/>.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAbilityVfxController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCombatController combat;

    [Header("Runtime particle material (optional)")]
    [Tooltip("Optional override for runtime particle/trail materials. Leave empty to use Sprites/Default (tinted by particle color), then Resources/Vfx/AbilityVfx_ParticlesUnlit, then URP particle shaders.")]
    [SerializeField] private Material runtimeParticleMaterialTemplate;

    [Header("Power Slash (Melee) VFX")]
    [SerializeField] private Transform powerSlashTrailAnchor;
    [SerializeField] private string[] powerSlashAnchorNameCandidates = { "Weapon", "MainHandItem", "MainHand" };
    [SerializeField] private Color powerSlashTrailColor = new Color(1f, 0.88f, 0.22f, 0.95f);
    [SerializeField, Min(0.01f)] private float powerSlashTrailTime = 0.18f;
    [SerializeField, Min(0.01f)] private float powerSlashSwingDuration = 0.14f;
    [SerializeField, Min(0.01f)] private float powerSlashTrailWidth = 0.4f;
    [SerializeField] private Vector2 powerSlashAngleRange = new Vector2(155f, -30f);
    [SerializeField] private Vector3 powerSlashLocalOffset = new Vector3(0.04f, 0.02f, 0f);
    [SerializeField] private Vector2 powerSlashTipLocalOffset = new Vector2(0.52f, 0.06f);
    [SerializeField, Min(0f)] private float powerSlashEdgeFollowSmoothing = 0.06f;
    [SerializeField] private bool powerSlashUseDoubleSwipe = true;
    [SerializeField, Min(0f)] private float powerSlashSecondSwipeDelay = 0.035f;
    [SerializeField] private float powerSlashSecondSwipeAngleOffset = 18f;

    [Header("Parry (Melee Lv10) VFX")]
    [SerializeField] private Color parrySlashColor = new Color(1f, 0.82f, 0.15f, 0.95f);
    [SerializeField, Min(0.01f)] private float parrySlashLineWidth = 0.1f;
    [SerializeField, Min(0.05f)] private float parrySlashDuration = 0.2f;
    [SerializeField] private Vector3 parrySlashHeightOffset = new Vector3(0f, 0.55f, 0f);

    [Header("Crusader Strike (Melee) VFX")]
    [SerializeField] private Color crusaderStrikeBeamOuterColor = new Color(1f, 0.7f, 0.16f, 0.9f);
    [SerializeField] private Color crusaderStrikeBeamInnerColor = new Color(1f, 0.95f, 0.5f, 0.95f);
    [SerializeField] private Color crusaderStrikeBeamFinalColor = new Color(1f, 0.5f, 0.08f, 0.95f);
    [SerializeField, Min(0.05f)] private float crusaderStrikeBeamDuration = 0.18f;
    [SerializeField, Min(0.1f)] private float crusaderStrikeBeamSkyHeight = 3.1f;
    [SerializeField, Min(0f)] private float crusaderStrikeBeamBehindPlayerX = 1.15f;
    [SerializeField, Min(0f)] private float crusaderStrikeBeamBehindPlayerY = 0.9f;
    [SerializeField, Min(0.02f)] private float crusaderStrikeBeamPointSize = 0.2f;
    [SerializeField, Min(0.02f)] private float crusaderStrikeBeamTrailTime = 0.12f;
    [SerializeField, Min(0.005f)] private float crusaderStrikeBeamTrailWidth = 0.12f;
    [SerializeField] private Vector3 crusaderStrikeBeamCenterOffset = new Vector3(0f, 0.55f, 0f);
    [SerializeField, Min(1f)] private float crusaderStrikeBeamFinalSizeScale = 1.2f;

    [Header("Whirlwind (Melee) VFX")]
    [SerializeField] private Color whirlingBladeColor = new Color(1f, 0.88f, 0.22f, 0.95f);
    [SerializeField, Min(0.01f)] private float whirlingBladeDuration = 0.22f;
    [SerializeField, Min(90f)] private float whirlingBladeSpinDegrees = 720f;
    [SerializeField, Min(0.01f)] private float whirlingBladeLineWidth = 0.14f;
    [SerializeField] private Vector3 whirlingBladeCenterOffset = new Vector3(0f, 0.65f, 0f);
    [Tooltip("Number of staggered slash trails on the same horizontal orbit.")]
    [SerializeField, Range(2, 5)] private int whirlingBladeSlashCount = 5;
    [Tooltip("Per-blade base Y offsets relative to the whirlwind center for blades 1-4. One blade should usually stay at 0.")]
    [SerializeField] private Vector4 whirlingBladeLaneBaseYOffsets = new Vector4(0.55f, 0.18f, -0.16f, -0.42f);
    [SerializeField] private float whirlingBladeFifthLaneBaseYOffset = -0.68f;
    [Tooltip("Per-blade vertical orbit heights for blades 1-4. Higher values make that blade arc more above and below its base Y.")]
    [SerializeField] private Vector4 whirlingBladeLaneOrbitHeights = new Vector4(0.10f, 0.08f, 0.05f, 0.035f);
    [SerializeField] private float whirlingBladeFifthLaneOrbitHeight = 0.025f;
    [SerializeField, Min(0f)] private float whirlingBladeSpawnVerticalJitter = 0.01f;
    [Tooltip("Minimum local Y offset from the whirlwind center that blade trails can reach.")]
    [SerializeField] private float whirlingBladeMinYOffset = -0.8f;
    [Tooltip("Maximum local Y offset from the whirlwind center that blade trails can reach.")]
    [SerializeField] private float whirlingBladeMaxYOffset = 0.75f;

    [Header("Galeforce Twister (Whirlwind Lv18) VFX")]
    [SerializeField] private Color galeforceTwisterColor = new Color(0.95f, 0.98f, 1f, 0.82f);
    [SerializeField, Min(0.01f)] private float galeforceTwisterDuration = 0.22f;
    [SerializeField, Min(90f)] private float galeforceTwisterSpinDegrees = 720f;
    [SerializeField, Min(0.01f)] private float galeforceTwisterLineWidthScale = 0.62f;
    [SerializeField] private Vector3 galeforceTwisterCenterOffset = new Vector3(0f, 0.65f, 0f);
    [SerializeField, Range(2, 4)] private int galeforceTwisterSlashCount = 3;
    [SerializeField] private Vector4 galeforceTwisterLaneBaseYOffsets = new Vector4(0.34f, 0.11f, -0.1f, -0.26f);
    [SerializeField] private float galeforceTwisterFifthLaneBaseYOffset = -0.42f;
    [SerializeField] private Vector4 galeforceTwisterLaneOrbitHeights = new Vector4(0.062f, 0.05f, 0.031f, 0.022f);
    [SerializeField] private float galeforceTwisterFifthLaneOrbitHeight = 0.016f;
    [SerializeField, Min(0.1f)] private float galeforceTwisterSizeScale = 0.62f;
    [SerializeField, Min(0.05f)] private float galeforceTwisterLifetimeSeconds = 8f;
    [SerializeField, Min(0f)] private float galeforceTwisterLingerAfterChannelSeconds = 5f;
    [SerializeField, Min(0f)] private float galeforceTwisterDriftSpeed = 1.5f;
    [SerializeField, Min(0.5f)] private float galeforceTwisterMinDirectionSeconds = 2f;
    [SerializeField, Min(0.5f)] private float galeforceTwisterMaxDirectionSeconds = 5f;

    [Header("Crescent Slash (Melee) VFX")]
    [SerializeField] private Color crescentSlashColor = new Color(0.55f, 0.95f, 1f, 0.9f);
    [SerializeField, Min(0.05f)] private float crescentSlashVfxDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float crescentSlashLineWidth = 0.12f;
    [SerializeField] private Vector3 crescentSlashCenterOffset = new Vector3(0f, 0.65f, 0f);

    [Header("Guardian's Hammer (Melee Lv15) VFX")]
    [SerializeField] private Sprite guardiansHammerSprite;
    [SerializeField] private Color guardiansHammerHammerTint = new Color(1f, 0.9f, 0.3f, 1f);
    [SerializeField] private Color guardiansHammerShockwaveColor = new Color(1f, 0.65f, 0.12f, 0.9f);
    [SerializeField] private Color guardiansHammerBurnFlareColor = new Color(1f, 0.32f, 0.08f, 0.95f);
    [SerializeField] private Vector3 guardiansHammerPivotOffset = new Vector3(0f, 0.95f, 0f);
    [SerializeField, Min(0.1f)] private float guardiansHammerOrbitRadius = 1.55f;
    [SerializeField, Min(0.05f)] private float guardiansHammerSwingDuration = 0.32f;
    [SerializeField] private float guardiansHammerStartAngle = 215f;
    [SerializeField] private float guardiansHammerEndAngle = 8f;
    [SerializeField, Min(0.1f)] private float guardiansHammerWorldScale = 1.55f;
    [SerializeField, Min(0f)] private float guardiansHammerShockwaveGroundOffset = 0.12f;
    [SerializeField, Min(0.05f)] private float guardiansHammerShockwaveDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float guardiansHammerShockwaveLineWidth = 0.18f;
    [SerializeField, Min(0.05f)] private float guardiansHammerShockwaveVerticalScale = 0.22f;
    [SerializeField, Min(0.05f)] private float guardiansHammerBurnFlareDuration = 0.34f;
    [SerializeField, Min(0.05f)] private float guardiansHammerBurnFlareRadius = 1.25f;

    [Header("Executioner's Descent (Melee Lv45) VFX")]
    [Tooltip("Assign the axe sprite in the inspector.")]
    [SerializeField] private Sprite executionersDescentAxeSprite;
    [SerializeField] private Sprite executionersDescentMarkSprite;
    [SerializeField] private Sprite executionersDescentShockwaveSprite;
    [SerializeField] private Color executionersDescentAxeTint = new Color(0.95f, 0.22f, 0.18f, 1f);
    [SerializeField] private Color executionersDescentMarkTint = new Color(1f, 0.12f, 0.08f, 0.85f);
    [SerializeField] private Color executionersDescentShockwaveColor = new Color(1f, 0.45f, 0.12f, 0.75f);
    [SerializeField, Min(0.5f)] private float executionersDescentAxeWorldScale = 2.4f;
    [SerializeField, Min(0.1f)] private float executionersDescentMarkWorldScale = 0.9f;
    [SerializeField] private Vector3 executionersDescentMarkOffset = new Vector3(0f, 0.35f, 0f);
    [Tooltip("World Y above the target where the axe first appears and lingers during hang time.")]
    [SerializeField, Min(0.1f)] private float executionersDescentSpawnHeightAboveTarget = 4.8f;
    [Tooltip("Seconds the axe lingers at spawn height before descending.")]
    [SerializeField, Min(0f)] private float executionersDescentSpawnHoldSeconds = 1.5f;
    [Tooltip("Lowest world Y above the target the axe reaches before impact. Descent speed uses the remaining ability channel time.")]
    [SerializeField, Min(0f), FormerlySerializedAs("executionersDescentHangHeightAboveTarget")]
    private float executionersDescentMinimumHeightAboveTarget = 1.8f;
    [Tooltip("Sorting layer for axe / mark / shockwave (Foreground renders above Background clouds).")]
    [SerializeField] private string executionersDescentSortingLayer = "Foreground";
    [SerializeField] private int executionersDescentSortingOrder = 200;
    [SerializeField, Min(0.05f)] private float executionersDescentShockwaveDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float executionersDescentShockwaveLineWidth = 0.22f;
    [SerializeField] private float executionersDescentShockwaveGroundOffset = 0.15f;

    [Header("Shadow Strike (Melee Lv25) VFX")]
    [SerializeField] private Color shadowStrikeBurstColor = new Color(0.35f, 0.15f, 0.55f, 0.9f);
    [SerializeField, Min(0.05f)] private float shadowStrikeBurstDuration = 0.22f;
    [SerializeField, Min(0.1f)] private float shadowStrikeBurstRadius = 1.1f;
    [SerializeField, Min(0.01f)] private float shadowStrikeBurstLineWidth = 0.16f;
    [SerializeField] private Vector3 shadowStrikeBurstOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("Purple smoke puff left at the player's pre-teleport position.")]
    [SerializeField] private Color shadowStrikeDepartSmokeColor = new Color(0.42f, 0.12f, 0.62f, 0.72f);
    [SerializeField] private Vector3 shadowStrikeDepartSmokeOffset = new Vector3(0f, 0.45f, 0f);
    [SerializeField, Min(0.5f)] private float shadowStrikeDepartSmokeLingerSeconds = 2f;
    [SerializeField, Min(1)] private int shadowStrikeDepartSmokeBurstCount = 52;
    [SerializeField, Min(0f)] private float shadowStrikeDepartSmokeWispEmitSeconds = 0.35f;

    [Header("Bladestorm (Melee Lv45) VFX")]
    [Tooltip("Yellow diagonal slash over the target on each Relentless Execution hit (parry-style).")]
    [SerializeField] private Color bladestormHitSlashColor = new Color(1f, 0.88f, 0.18f, 1f);
    [SerializeField, Min(0.01f)] private float bladestormHitSlashLineWidth = 0.17f;
    [SerializeField, Min(0.05f)] private float bladestormHitSlashDuration = 0.22f;
    [SerializeField] private Vector3 bladestormHitSlashCenterOffset = new Vector3(0f, 0.55f, 0f);
    [SerializeField] private Vector2 bladestormHitSlashSpawnJitter = new Vector2(0.22f, 0.18f);
    [SerializeField, Range(0f, 60f)] private float bladestormHitSlashAngleJitterDegrees = 28f;
    [SerializeField, Min(0.1f)] private float bladestormHitSlashHalfLength = 0.52f;
    [SerializeField, Min(1f)] private float bladestormFinaleSlashWidthScale = 1.5f;
    [SerializeField, Min(1f)] private float bladestormFinaleSlashDurationScale = 1.2f;
    [SerializeField, Min(1f)] private float bladestormFinaleSlashLengthScale = 1.25f;

    [Header("Final Severance (Melee Lv45) VFX")]
    [SerializeField] private Color finalSeveranceWindupStartColor = new Color(1f, 0.92f, 0.2f, 0.6f);
    [SerializeField] private Color finalSeveranceWindupEndColor = new Color(1f, 0.82f, 0.12f, 0.6f);
    [SerializeField] private Color finalSeveranceStrikeStartColor = new Color(1f, 0.18f, 0.05f, 1f);
    [SerializeField] private Color finalSeveranceStrikeEndColor = new Color(0.55f, 0f, 0f, 0.9f);
    [SerializeField, Min(0.05f)] private float finalSeveranceWindupVfxDuration = 0.42f;
    [SerializeField, Min(0.05f)] private float finalSeveranceStrikeVfxDuration = 0.38f;
    [SerializeField, Min(0.01f)] private float finalSeveranceLineWidth = 0.28f;
    [SerializeField] private Vector3 finalSeveranceCenterOffset = new Vector3(0f, 0.75f, 0f);
    [SerializeField] private float finalSeveranceDiagonalRise = 2.2f;
    [SerializeField] private float finalSeveranceDiagonalDrop = 0.9f;
    [SerializeField] private float finalSeveranceStrikeDiagonalRise = 3.1f;
    [SerializeField] private float finalSeveranceStrikeDiagonalDrop = 1.35f;

    [Header("Gathering Frenzy (Lumber / Fishing Lv5) VFX")]
    [Tooltip("Optional root prefab parented behind the player while a gathering frenzy buff is active. Leave empty to auto-spawn sparks.")]
    [SerializeField] private GameObject lumberFrenzyOrbitVfxPrefab;
    [SerializeField] private Vector3 lumberFrenzyOrbitVfxLocalOffset = new Vector3(0f, 0.72f, 0f);
    [Tooltip("How far behind the player (opposite facing) the VFX sits. Does not sweep left/right.")]
    [SerializeField, Min(0f)] private float gatheringFrenzyBehindDistance = 0.38f;
    [SerializeField] private Color lumberFrenzyOrbitVfxColor = new Color(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color fishingFrenzyOrbitVfxColor = new Color(0.32f, 0.62f, 1f, 0.95f);

    [Header("Soulforged Weapon Minion VFX")]
    [SerializeField] private SoulforgedWeaponMinionPresentation soulforgedWeaponMinionPresentation;

    [Header("Spectral Axe (Woodcutting Lv25) VFX")]
    [SerializeField] private Color spectralAxeTint = new Color(0.55f, 0.80f, 1f, 0.85f);
    [SerializeField, Min(0f)] private float spectralAxeVisualLift = 1.2f;
    [SerializeField, Min(0f)] private float spectralAxeSpinDegreesPerSecond = 720f;
    [SerializeField] private bool spectralAxeSpinClockwise = true;
    [SerializeField, Min(0.1f)] private float spectralAxeTravelSpeedUnitsPerSecond = 12f;

    [Header("Spectral Axe Blue Trail (Woodcutting Lv25)")]
    [SerializeField, Min(0.01f)] private float spectralAxeTrailLifetimeSeconds = 0.32f;
    [SerializeField, Min(0f)] private float spectralAxeTrailStartWidth = 0.18f;
    [SerializeField, Min(0f)] private float spectralAxeTrailEndWidth = 0f;
    [SerializeField, Range(0f, 1f)] private float spectralAxeTrailStartAlpha = 0.75f;
    [SerializeField] private float spectralAxeTrailAnchorXFrac = 0.28f;
    [SerializeField] private float spectralAxeTrailAnchorYFrac = 0.55f;
    [SerializeField] private Color spectralAxeTrailColorStart = new Color(0.45f, 0.78f, 1f);
    [SerializeField] private Color spectralAxeTrailColorEnd = new Color(0.30f, 0.55f, 1f);

    [Header("Cleaving Chop range indicator (Woodcutting Lv25)")]
    [SerializeField] private bool cleavingChopShowRangeIndicator = true;
    [SerializeField] private Color cleavingChopIndicatorColor = new Color(0.55f, 0.95f, 0.30f, 0.85f);
    [SerializeField, Range(16, 128)] private int cleavingChopIndicatorSegments = 64;
    [SerializeField, Min(0.005f)] private float cleavingChopIndicatorLineWidth = 0.14f;
    [Tooltip("When Sorting Layer is empty: offset added to the player's SpriteRenderer sorting order. When set: absolute order on that layer.")]
    [SerializeField] private int cleavingChopIndicatorSortingOrder = 50;
    [Tooltip("Optional sorting layer. Leave blank to match the player sprite layer.")]
    [SerializeField] private string cleavingChopIndicatorSortingLayer = "";

    [Header("Spectral Axe area indicator (Woodcutting Lv25)")]
    [SerializeField] private bool spectralAxeShowAreaIndicator = true;
    [SerializeField] private Color spectralAxeAreaIndicatorColor = new Color(0.55f, 0.95f, 0.30f, 0.85f);
    [SerializeField, Range(16, 128)] private int spectralAxeAreaIndicatorSegments = 64;
    [SerializeField, Min(0.005f)] private float spectralAxeAreaIndicatorLineWidth = 0.12f;
    [SerializeField] private int spectralAxeAreaIndicatorSortingOrder = 50;
    [SerializeField] private string spectralAxeAreaIndicatorSortingLayer = "";

    [Header("Woodcutting tree range outlines (screen overlay setting)")]
    [SerializeField] private bool woodcuttingTreeRangeOutlinesEnabled = true;
    [SerializeField] private Color woodcuttingTreeOutlineInRangeColor = new Color(0.35f, 0.95f, 0.35f, 0.95f);
    [SerializeField] private Color woodcuttingTreeOutlineOutOfRangeColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField, Min(0.002f)] private float woodcuttingTreeOutlineLineWidth = 0.035f;
    [SerializeField] private int woodcuttingTreeOutlineSortingOrder = 55;
    [SerializeField] private string woodcuttingTreeOutlineSortingLayer = "";
    [Tooltip("Extra padding added when deciding which nearby trees get an outline.")]
    [SerializeField, Min(0f)] private float woodcuttingTreeOutlineScanPadding = 2f;

    [Header("Avatar of the Forest (Woodcutting Lv45) VFX")]
    [SerializeField] private Vector3 avatarOfForestGlowLocalOffset = new Vector3(0f, 0.18f, 0f);
    [SerializeField] private Color avatarOfForestGlowColor = new Color(0.58f, 1f, 0.42f, 0.96f);
    [Tooltip("Cone base radius at the feet; particles rise along the cone axis.")]
    [SerializeField, Min(0.02f)] private float avatarOfForestGlowSphereRadius = 0.2f;
    [SerializeField, Range(4f, 32f)] private float avatarOfForestGlowConeAngle = 14f;
    [SerializeField, Min(4f)] private float avatarOfForestGlowEmissionRate = 32f;
    [Tooltip("Random range for particle size when each particle is born (world units). Set min = max for uniform size.")]
    [SerializeField, Min(0.001f)] private float avatarOfForestParticleStartSizeMin = 0.028f;
    [SerializeField, Min(0.001f)] private float avatarOfForestParticleStartSizeMax = 0.052f;
    [Tooltip("How long each particle's ribbon trail lasts (seconds).")]
    [SerializeField, Min(0.02f)] private float avatarOfForestTrailLifetime = 0.16f;
    [Tooltip("Trail width at the head (narrows along the trail).")]
    [SerializeField, Min(0.004f)] private float avatarOfForestTrailWidth = 0.034f;

    [Header("Energy Infusion / Arcane Battery (Melee Lv25) VFX")]
    [SerializeField] private Vector3 energyInfusionGlowLocalOffset = new Vector3(0f, 0.12f, 0f);
    [SerializeField] private Color energyInfusionGlowColor = new Color(0.32f, 0.62f, 1f, 0.92f);
    [SerializeField, Min(0.02f)] private float energyInfusionGlowSphereRadius = 0.42f;
    [SerializeField, Min(4f)] private float energyInfusionGlowEmissionRate = 38f;
    [SerializeField, Min(0.001f)] private float energyInfusionParticleStartSizeMin = 0.032f;
    [SerializeField, Min(0.001f)] private float energyInfusionParticleStartSizeMax = 0.058f;

    [Header("Battle Trance (Melee Lv35) VFX")]
    [SerializeField] private Vector3 battleTranceGlowLocalOffset = new Vector3(0f, 0.12f, 0f);
    [SerializeField] private Color battleTranceGlowColor = new Color(1f, 0.18f, 0.12f, 0.92f);
    [SerializeField, Min(0.02f)] private float battleTranceGlowSphereRadius = 0.42f;
    [SerializeField, Min(4f)] private float battleTranceGlowEmissionRate = 38f;
    [SerializeField, Min(0.001f)] private float battleTranceParticleStartSizeMin = 0.032f;
    [SerializeField, Min(0.001f)] private float battleTranceParticleStartSizeMax = 0.058f;

    [Header("Hammer Tempest (Melee Lv35) VFX")]
    [Tooltip("Sprite for each orbiting hammer (e.g. SmallGoldenHammer). Uses Guardian's Hammer sprite if empty.")]
    [SerializeField] private Sprite hammerTempestHammerSprite;
    [SerializeField] private Color hammerTempestHammerTint = new Color(1f, 0.92f, 0.35f, 1f);
    [Tooltip("World offset from the player for the center of the hammer orbit ring.")]
    [SerializeField] private Vector3 hammerTempestCenterOffset = new Vector3(0f, 0.75f, 0f);
    [Tooltip("Distance from the orbit center to each hammer (ring radius).")]
    [SerializeField, Min(0.1f)] private float hammerTempestOrbitRadius = 1.35f;
    [Tooltip("Uniform scale applied to each hammer sprite in world space.")]
    [SerializeField, Min(0.05f)] private float hammerTempestWorldScale = 0.85f;
    [Tooltip("Main rotation: how fast the hammer ring orbits around the player (degrees per second). 360 = one full circle per second.")]
    [SerializeField, Min(0f)]
    [FormerlySerializedAs("hammerTempestOrbitDegreesPerSecond")]
    [InspectorName("Main Orbit Speed (deg/s)")]
    private float hammerTempestMainOrbitDegreesPerSecond = 360f;
    [Tooltip("Individual hammer rotation: how fast each hammer spins on its own axis while orbiting (degrees per second).")]
    [SerializeField, Min(0f)]
    [FormerlySerializedAs("hammerTempestSelfSpinDegreesPerSecond")]
    [InspectorName("Hammer Spin Speed (deg/s)")]
    private float hammerTempestHammerSpinDegreesPerSecond = 720f;
    [Tooltip("Golden disc inside the hammer orbit: world radius = Orbit Radius minus this value.")]
    [SerializeField, Min(0f)] private float hammerTempestRingRadiusInset = 0.5f;
    [SerializeField] private Color hammerTempestRingColor = new Color(1f, 0.88f, 0.22f, 1f);
    [Tooltip("Face opacity at the outer edge of the golden ring (center fades to 0%).")]
    [SerializeField, Range(0f, 1f)] private float hammerTempestRingEdgeOpacity = 0.5f;

    [Header("Phoenix Soul — Ashen Rebirth (Melee Lv40) VFX")]
    [SerializeField] private Sprite ashenRebirthPhoenixSprite;
    [SerializeField] private Color ashenRebirthPhoenixTint = Color.white;
    [SerializeField, Min(0.5f)] private float ashenRebirthPhoenixWorldScale = 1.5f;
    [Tooltip("World Y above the player where the phoenix sprite appears.")]
    [SerializeField, Min(0.1f)] private float ashenRebirthPhoenixSpawnHeightAbovePlayer = 4.8f;
    [Tooltip("Seconds the phoenix stays visible above the player.")]
    [SerializeField, Min(0f)] private float ashenRebirthPhoenixSpawnHoldSeconds = 2f;
    [SerializeField] private string ashenRebirthPhoenixSortingLayer = "Foreground";
    [SerializeField] private int ashenRebirthPhoenixSortingOrder = 120;

    [Header("Flame Charge (Melee Lv25) VFX")]
    [SerializeField] private Color flameChargePlayerGlowColor = new Color(1f, 0.38f, 0.12f, 0.88f);
    [SerializeField] private Vector3 flameChargePlayerGlowLocalOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField, Min(0.02f)] private float flameChargePlayerGlowRadius = 0.52f;
    [SerializeField, Min(4f)] private float flameChargePlayerGlowEmissionRate = 72f;
    [SerializeField] private Color flameChargeGroundFireColor = new Color(1f, 0.32f, 0.08f, 0.92f);
    [SerializeField, Min(0.04f)] private float flameChargeGroundFireRadius = 1.05f;
    [SerializeField, Min(4f)] private float flameChargeGroundFireEmissionRate = 48f;
    [SerializeField] private Color flameChargeVolcanicBurstColor = new Color(1f, 0.45f, 0.1f, 0.95f);
    [SerializeField, Min(0.05f)] private float flameChargeVolcanicBurstDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float flameChargeVolcanicBurstMaxRadius = 5.5f;
    [SerializeField, Min(0f)] private float flameChargeGroundFloorYOffset = 0.1f;
    [SerializeField, Min(0.02f)] private float flameChargeGroundTrailHeight = 0.14f;
    [Tooltip("Renders ground fire above the lane floor sprite.")]
    [SerializeField] private string flameChargeGroundSortingLayer = "Foreground";
    [SerializeField] private int flameChargeGroundSortingOrder = 42;

    private GameObject _cleavingChopIndicatorRoot;
    private LineRenderer _cleavingChopIndicatorLine;
    private float _cleavingChopIndicatorAppliedRadius = float.NaN;

    private GameObject _spectralAxeAreaIndicatorRoot;
    private LineRenderer _spectralAxeAreaIndicatorLine;
    private Transform _spectralAxeAreaIndicatorFollow;
    private float _spectralAxeAreaIndicatorAppliedRadius = float.NaN;

    private readonly Dictionary<ResourceNode, LineRenderer> _woodcuttingTreeOutlineByNode = new();
    private readonly List<ResourceNode> _woodcuttingTreeOutlineScratch = new();
    private readonly List<ResourceNode> _woodcuttingTreeOutlineRemoveScratch = new();

    private GameObject _lumberFrenzyAnchorRoot;
    private GameObject _lumberFrenzyOrbitVfxRoot;
    private ParticleSystem _gatheringFrenzyRuntimeParticles;

    private GameObject _avatarOfForestGlowRoot;
    private GameObject _energyInfusionGlowRoot;
    private GameObject _battleTranceGlowRoot;
    private ParticleSystem _battleTranceGlowParticles;
    private GameObject _hammerTempestOrbitRoot;
    private Coroutine _hammerTempestOrbitRoutine;
    private readonly List<SpriteRenderer> _hammerTempestHammerRenderers = new();
    private GameObject _hammerTempestRingObject;
    private SpriteRenderer _hammerTempestRingRenderer;
    private GameObject _flameChargePlayerGlowRoot;
    private Coroutine _flameChargeVolcanicBurstRoutine;
    private GameObject _ashenRebirthPhoenixRoot;
    private SpriteRenderer _ashenRebirthPhoenixRenderer;
    private Coroutine _ashenRebirthPhoenixRoutine;
    private readonly List<GameObject> _activeFlameChargeDashTrailRoots = new();
    private readonly List<GameObject> _activeGaleforceTwisterRoots = new();

    private sealed class GaleforceTwisterLifetime : MonoBehaviour
    {
        public float EndTime;

        public void ExtendToAtLeast(float newEndTime) => EndTime = Mathf.Max(EndTime, newEndTime);
    }

    private GameObject _executionersDescentAxeRoot;
    private SpriteRenderer _executionersDescentAxeRenderer;
    private GameObject _executionersDescentMarkRoot;
    private SpriteRenderer _executionersDescentMarkRenderer;
    private Coroutine _executionersDescentShockwaveRoutine;
    private float _executionersDescentTotalSeconds = 3f;

    private const string ResourcesUrParticleMaterialPath = "Vfx/AbilityVfx_ParticlesUnlit";
    private static bool s_LoggedMissingUrParticleMaterial;
    private static Sprite s_RuntimeCircleSprite;
    private static Sprite s_HammerTempestRadialRingSprite;

    public SoulforgedWeaponMinionPresentation SoulforgedWeaponMinionPresentation => soulforgedWeaponMinionPresentation;

    public Transform LumberFrenzyOrbitVfxTransform =>
        _lumberFrenzyAnchorRoot != null ? _lumberFrenzyAnchorRoot.transform : null;

    public float SpectralAxeVisualLift => spectralAxeVisualLift;
    public float SpectralAxeSpinDegreesPerSecond => spectralAxeSpinDegreesPerSecond;
    public bool SpectralAxeSpinClockwise => spectralAxeSpinClockwise;
    public float SpectralAxeTravelSpeedUnitsPerSecond => spectralAxeTravelSpeedUnitsPerSecond;

    /// <summary>Whirlwind / combat facing from weapon scale.</summary>
    public float GetCombatFacingSign()
    {
        Transform anchor = ResolvePowerSlashAnchor();
        if (anchor != null)
        {
            float sx = anchor.lossyScale.x;
            if (Mathf.Abs(sx) > 0.0001f)
                return -Mathf.Sign(sx);
        }

        if (player != null)
            return Mathf.Sign(player.transform.localScale.x >= 0f ? 1f : -1f);
        return 1f;
    }

    private void Awake()
    {
        if (!player)
            player = GetComponent<PlayerController>();
        if (!combat)
            combat = GetComponent<PlayerCombatController>();
    }

    private void OnDisable()
    {
        DestroyLumberFrenzyOrbitVfx();
        DestroySpectralAxeAreaIndicator();
        ClearWoodcuttingTreeRangeOutlines();
        DestroyAvatarOfTheForestGlowVfx();
        DestroyEnergyInfusionGlowVfx();
        DestroyBattleTranceGlowVfx();
        DestroyHammerTempestOrbitVfx();
        EndFlameChargePlayerGlow();
        DestroyAllFlameChargeDashTrailVfx();
        StopAshenRebirthPhoenixVfx();
    }

    private static bool AreAbilityRangeIndicatorsEnabled() =>
        !ToggleSettingsStore.Get(ToggleSettingId.DisableScreenOverlayVisuals);

    private SpriteRenderer ResolvePlayerPrimarySpriteRenderer()
    {
        if (player != null)
            return player.GetComponentInChildren<SpriteRenderer>(true);
        return GetComponentInChildren<SpriteRenderer>(true);
    }

    private bool TryApplyPlayerSpriteSortingToRenderer(Renderer targetRenderer, int sortingOrderOffsetFromPlayer)
    {
        if (targetRenderer == null)
            return false;
        SpriteRenderer playerSr = ResolvePlayerPrimarySpriteRenderer();
        if (playerSr == null)
            return false;
        targetRenderer.sortingLayerID = playerSr.sortingLayerID;
        targetRenderer.sortingOrder = playerSr.sortingOrder + sortingOrderOffsetFromPlayer;
        return true;
    }

    private void TryApplyPlayerSpriteSortingToHierarchy(Transform root, int sortingOrderOffsetFromPlayer)
    {
        if (root == null)
            return;
        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
            TryApplyPlayerSpriteSortingToRenderer(rends[i], sortingOrderOffsetFromPlayer);
    }

    public void SpawnWhirlwind(float radius)
    {
        Transform anchor = ResolvePowerSlashAnchor();
        Transform center = player != null ? player.transform : transform;
        if (center == null)
            return;
        if (anchor == null)
            anchor = center;

        Vector2 startDir = ((Vector2)anchor.position - (Vector2)center.position).normalized;
        if (startDir.sqrMagnitude <= 0.0001f)
            startDir = Vector2.right * ((player != null && player.transform.localScale.x < 0f) ? -1f : 1f);

        int slashCount = Mathf.Clamp(whirlingBladeSlashCount, 2, 5);
        var emitters = new Transform[slashCount];
        var trails = new TrailRenderer[slashCount];
        var phases = new float[slashCount];
        var laneBaseY = new float[slashCount];
        var laneOrbitHeight = new float[slashCount];
        var laneHorizontalScale = new float[slashCount];

        GameObject root = new GameObject("WhirlwindBlades");
        float spawnYOffset = UnityEngine.Random.Range(-whirlingBladeSpawnVerticalJitter, whirlingBladeSpawnVerticalJitter);
        root.transform.position = center.position + whirlingBladeCenterOffset + Vector3.up * spawnYOffset;

        for (int i = 0; i < slashCount; i++)
        {
            phases[i] = i / (float)slashCount;
            float widthScale = 0.82f + 0.18f * (1f - Mathf.Abs((i / (float)slashCount) - 0.5f) * 2f);
            laneBaseY[i] = GetWhirlwindBladeBaseY(i);
            laneOrbitHeight[i] = GetWhirlwindBladeOrbitHeight(i);
            laneHorizontalScale[i] = ResolveWhirlwindBladeHorizontalScale(i);

            GameObject orbitGO = new GameObject($"WhirlwindBlade_{i}");
            orbitGO.transform.SetParent(root.transform, false);
            emitters[i] = orbitGO.transform;
            trails[i] = CreateWhirlwindBladeTrail(orbitGO, widthScale, 10 + i);
        }

        StartCoroutine(AnimateWhirlwindBlades(
            root,
            emitters,
            trails,
            center,
            radius,
            startDir.x,
            phases,
            laneBaseY,
            laneOrbitHeight,
            laneHorizontalScale));
    }

    private TrailRenderer CreateWhirlwindBladeTrail(
        GameObject owner,
        float widthScale,
        int sortingOrderOffsetFromPlayer,
        Color? bladeColorOverride = null,
        float widthScaleMultiplier = 1f)
    {
        Color bladeColor = bladeColorOverride ?? whirlingBladeColor;
        TrailRenderer trail = owner.AddComponent<TrailRenderer>();
        trail.time = Mathf.Max(0.06f, whirlingBladeDuration * 0.75f);
        trail.minVertexDistance = 0.003f;
        trail.widthMultiplier = Mathf.Max(0.01f, whirlingBladeLineWidth * widthScale * widthScaleMultiplier);
        trail.numCornerVertices = 4;
        trail.numCapVertices = 0;
        trail.alignment = LineAlignment.TransformZ;
        trail.textureMode = LineTextureMode.Stretch;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.05f, 0.015f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.8f, 1f),
            new Keyframe(0.95f, 0.015f),
            new Keyframe(1f, 0f));
        if (!TryApplyPlayerSpriteSortingToRenderer(trail, sortingOrderOffsetFromPlayer))
            trail.sortingOrder = 18 + sortingOrderOffsetFromPlayer;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(bladeColor, 0f),
                new GradientColorKey(Color.Lerp(bladeColor, Color.white, 0.45f), 0.45f),
                new GradientColorKey(bladeColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(bladeColor.a, 0f),
                new GradientAlphaKey(Mathf.Clamp01(bladeColor.a * 0.85f), 0.4f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
        return trail;
    }

    public float GaleforceTwisterLingerAfterChannelSeconds => galeforceTwisterLingerAfterChannelSeconds;

    public void SpawnGaleforceTwisterNearPlayer(
        float horizontalOffsetFromPlayer,
        float hitRadius,
        float damageMultiplier)
    {
        Transform center = player != null ? player.transform : transform;
        if (center == null)
            return;

        Vector3 worldCenter = center.position + galeforceTwisterCenterOffset + Vector3.right * horizontalOffsetFromPlayer;
        SpawnGaleforceTwister(worldCenter, hitRadius, damageMultiplier);
    }

    public void CollectActiveGaleforceTwisters(List<GaleforceTwisterInstance> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();
        for (int i = _activeGaleforceTwisterRoots.Count - 1; i >= 0; i--)
        {
            GameObject root = _activeGaleforceTwisterRoots[i];
            if (!root)
            {
                _activeGaleforceTwisterRoots.RemoveAt(i);
                continue;
            }

            if (root.TryGetComponent(out GaleforceTwisterInstance instance))
                buffer.Add(instance);
        }
    }

    public void SpawnGaleforceTwister(Vector3 worldCenter, float hitRadius, float damageMultiplier)
    {
        int slashCount = Mathf.Clamp(galeforceTwisterSlashCount, 2, 4);
        var emitters = new Transform[slashCount];
        var trails = new TrailRenderer[slashCount];
        var phases = new float[slashCount];
        var laneBaseY = new float[slashCount];
        var laneOrbitHeight = new float[slashCount];
        var laneHorizontalScale = new float[slashCount];
        float sizeScale = Mathf.Max(0.1f, galeforceTwisterSizeScale);
        float lineWidthScale = Mathf.Max(0.01f, galeforceTwisterLineWidthScale);

        GameObject root = new GameObject("GaleforceTwister");
        root.transform.position = worldCenter;
        _activeGaleforceTwisterRoots.Add(root);

        var lifetime = root.AddComponent<GaleforceTwisterLifetime>();
        float initialLifetime = Mathf.Max(
            galeforceTwisterMinDirectionSeconds * 2f,
            galeforceTwisterLifetimeSeconds);
        lifetime.EndTime = Time.time + initialLifetime;

        GaleforceTwisterInstance damageInstance = root.AddComponent<GaleforceTwisterInstance>();
        damageInstance.Configure(hitRadius, damageMultiplier);

        for (int i = 0; i < slashCount; i++)
        {
            phases[i] = i / (float)slashCount;
            float widthScale = (0.82f + 0.18f * (1f - Mathf.Abs((i / (float)slashCount) - 0.5f) * 2f)) * lineWidthScale;
            laneBaseY[i] = GetGaleforceTwisterBladeBaseY(i);
            laneOrbitHeight[i] = GetGaleforceTwisterBladeOrbitHeight(i);
            laneHorizontalScale[i] = ResolveWhirlwindBladeHorizontalScale(i) * sizeScale;

            GameObject orbitGO = new GameObject($"GaleforceTwisterBlade_{i}");
            orbitGO.transform.SetParent(root.transform, false);
            emitters[i] = orbitGO.transform;
            trails[i] = CreateWhirlwindBladeTrail(
                orbitGO,
                widthScale,
                8 + i,
                galeforceTwisterColor,
                lineWidthScale);
        }

        StartCoroutine(AnimateGaleforceTwister(
            root,
            emitters,
            trails,
            hitRadius,
            galeforceTwisterDriftSpeed,
            phases,
            laneBaseY,
            laneOrbitHeight,
            laneHorizontalScale,
            lifetime));
    }

    public void LingerGaleforceTwistersAfterChannelEnd(float extraSeconds)
    {
        if (extraSeconds <= 0f)
            return;

        float minEnd = Time.time + extraSeconds;
        for (int i = _activeGaleforceTwisterRoots.Count - 1; i >= 0; i--)
        {
            GameObject root = _activeGaleforceTwisterRoots[i];
            if (!root)
            {
                _activeGaleforceTwisterRoots.RemoveAt(i);
                continue;
            }

            if (root.TryGetComponent(out GaleforceTwisterLifetime lifetime))
                lifetime.ExtendToAtLeast(minEnd);
        }
    }

    public void EndAllGaleforceTwisterVfx()
    {
        for (int i = _activeGaleforceTwisterRoots.Count - 1; i >= 0; i--)
        {
            GameObject root = _activeGaleforceTwisterRoots[i];
            if (root)
                Destroy(root);
        }

        _activeGaleforceTwisterRoots.Clear();
    }

    public void SpawnCrescentSlash(float reach, float combatFacingSign)
    {
        Transform center = player != null ? player.transform : transform;
        if (center == null)
            return;

        Vector3 startPos = center.position + crescentSlashCenterOffset;
        Vector3 dir = Vector3.right * (Mathf.Approximately(combatFacingSign, 0f) ? 1f : Mathf.Sign(combatFacingSign));
        StartCoroutine(SpawnProjectedCrescentWaveAfterDelay(startPos, dir, reach, 0f, 1f, 1f, 14));
        StartCoroutine(SpawnProjectedCrescentWaveAfterDelay(startPos, dir, reach, 0.045f, 0.92f, 0.62f, 13));
        StartCoroutine(SpawnProjectedCrescentWaveAfterDelay(startPos, dir, reach, 0.09f, 0.84f, 0.38f, 12));
    }

    public void SpawnGuardiansHammerSlam(float reach, float combatFacingSign)
    {
        Transform center = player != null ? player.transform : transform;
        if (center == null)
            return;

        float sign = Mathf.Approximately(combatFacingSign, 0f) ? 1f : Mathf.Sign(combatFacingSign);
        StartCoroutine(CoGuardiansHammerSlam(center, Mathf.Max(0.5f, reach), sign));
    }

    /// <summary>Ground shockwave only (e.g. Soulforged Warrior Furious Slam at minion position).</summary>
    public void SpawnGuardiansHammerShockwaveAt(Vector3 worldOrigin, float reach, float combatFacingSign)
    {
        SpawnGuardiansHammerShockwaveAt(worldOrigin, reach, combatFacingSign, null);
    }

    /// <summary>Ground shockwave with optional color override (warrior furious slam uses bright red).</summary>
    public void SpawnGuardiansHammerShockwaveAt(Vector3 worldOrigin, float reach, float combatFacingSign, Color? shockwaveColorOverride)
    {
        float sign = Mathf.Approximately(combatFacingSign, 0f) ? 1f : Mathf.Sign(combatFacingSign);
        Color color = shockwaveColorOverride ?? guardiansHammerShockwaveColor;
        StartCoroutine(CoGuardiansHammerShockwave(worldOrigin, Mathf.Max(0.5f, reach), sign, color));
    }

    public void SpawnGuardiansHammerBurnFlare(Vector3 worldPosition)
    {
        StartCoroutine(CoGuardiansHammerBurningVerdictBurst(
            worldPosition,
            guardiansHammerBurnFlareRadius,
            guardiansHammerBurnFlareDuration,
            guardiansHammerBurnFlareColor));
    }

    private IEnumerator CoGuardiansHammerSlam(Transform center, float reach, float sign)
    {
        GameObject root = new GameObject("GuardiansHammerVfx");
        GameObject hammerGo = null;
        SpriteRenderer hammerRenderer = null;
        if (guardiansHammerSprite != null)
        {
            hammerGo = new GameObject("GuardiansHammerSprite");
            hammerGo.transform.SetParent(root.transform, false);
            hammerRenderer = hammerGo.AddComponent<SpriteRenderer>();
            hammerRenderer.sprite = guardiansHammerSprite;
            hammerRenderer.flipY = sign < 0f;
            hammerRenderer.flipX = false;
            hammerRenderer.color = guardiansHammerHammerTint;
            if (!TryApplyPlayerSpriteSortingToRenderer(hammerRenderer, 20))
                hammerRenderer.sortingOrder = 30;
        }

        float swingDuration = Mathf.Max(0.05f, guardiansHammerSwingDuration);
        float elapsed = 0f;
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / swingDuration);
            float eased = t * t * t;
            float angleDeg = Mathf.Lerp(guardiansHammerStartAngle, guardiansHammerEndAngle, eased);
            float radians = angleDeg * Mathf.Deg2Rad;
            Vector3 pivot = (center != null ? center.position : Vector3.zero) + guardiansHammerPivotOffset;

            if (hammerGo != null)
            {
                Vector3 offset = new Vector3(
                    Mathf.Cos(radians) * guardiansHammerOrbitRadius * sign,
                    Mathf.Sin(radians) * guardiansHammerOrbitRadius,
                    0f);
                hammerGo.transform.position = pivot + offset;
                float rotationZ = sign >= 0f
                    ? angleDeg - 90f
                    : 270f - angleDeg;
                hammerGo.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
                hammerGo.transform.localScale = new Vector3(
                    guardiansHammerWorldScale,
                    guardiansHammerWorldScale,
                    1f);
            }

            yield return null;
        }

        if (hammerGo != null)
            Destroy(hammerGo);

        Vector3 shockwaveOrigin = center != null ? center.position : Vector3.zero;
        yield return CoGuardiansHammerShockwave(shockwaveOrigin, reach, sign, guardiansHammerShockwaveColor);

        if (root != null)
            Destroy(root);
    }

    private IEnumerator CoGuardiansHammerShockwave(Vector3 playerWorld, float reach, float sign, Color shockwaveColor)
    {
        GameObject ringRoot = new GameObject("GuardiansHammerShockwave");
        LineRenderer ring = ringRoot.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = false;
        ring.positionCount = 18;
        ring.widthMultiplier = guardiansHammerShockwaveLineWidth;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = shockwaveColor;
        ring.endColor = shockwaveColor;
        if (!TryApplyPlayerSpriteSortingToRenderer(ring, 16))
            ring.sortingOrder = 26;

        float duration = Mathf.Max(0.05f, guardiansHammerShockwaveDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Max(0.1f, reach) * t;
            float alpha = shockwaveColor.a * (1f - t);
            RebuildGuardiansHammerShockwave(ring, playerWorld, radius, sign, alpha, shockwaveColor);
            yield return null;
        }

        if (ringRoot != null)
            Destroy(ringRoot);
    }

    private void RebuildGuardiansHammerShockwave(LineRenderer ring, Vector3 playerWorld, float radius, float sign, float alpha, Color shockwaveColor)
    {
        if (ring == null)
            return;

        Color c = shockwaveColor;
        c.a = alpha;
        ring.startColor = c;
        ring.endColor = c;

        float arcRadius = Mathf.Max(0.05f, radius * 0.5f);
        Vector3 center = new Vector3(
            playerWorld.x + sign * arcRadius,
            playerWorld.y + guardiansHammerShockwaveGroundOffset,
            playerWorld.z);

        int maxIndex = ring.positionCount - 1;
        for (int i = 0; i <= maxIndex; i++)
        {
            float u = maxIndex <= 0 ? 0f : i / (float)maxIndex;
            float angle = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, u);
            float x = Mathf.Cos(angle) * arcRadius * sign;
            float y = Mathf.Sin(angle) * arcRadius * guardiansHammerShockwaveVerticalScale;
            ring.SetPosition(i, center + new Vector3(x, y, 0f));
        }
    }

    private IEnumerator CoGuardiansHammerBurningVerdictBurst(
        Vector3 center,
        float maxRadius,
        float duration,
        Color color)
    {
        GameObject root = new GameObject("BurningVerdictBurst");
        root.transform.position = center;

        SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
        sprite.sprite = GetRuntimeCircleSprite();
        if (!TryApplyPlayerSpriteSortingToRenderer(sprite, 18))
            sprite.sortingOrder = 28;

        float elapsed = 0f;
        float burstDuration = Mathf.Max(0.05f, duration);
        float radiusMax = Mathf.Max(0.05f, maxRadius);
        float startRadius = Mathf.Min(0.28f, radiusMax * 0.18f);
        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / burstDuration);
            float eased = 1f - Mathf.Pow(1f - t, 2.2f);
            float radius = Mathf.Lerp(startRadius, radiusMax, eased);
            Color c = color;
            c.a *= 1f - (t * t);
            sprite.color = c;
            root.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            yield return null;
        }

        if (root != null)
            Destroy(root);
    }

    /// <summary>Wide yellow warning slash on button press (span matches gameplay half-reach).</summary>
    public void SpawnFinalSeveranceChannelWindup(float halfReach, float combatFacingSign)
    {
        if (!TryGetFinalSeveranceSlashEndpoints(
                halfReach,
                combatFacingSign,
                finalSeveranceDiagonalRise,
                finalSeveranceDiagonalDrop,
                out Vector3 start,
                out Vector3 end))
            return;

        StartCoroutine(AnimateFinalSeveranceSlash(
            start,
            end,
            finalSeveranceWindupVfxDuration,
            finalSeveranceWindupStartColor,
            finalSeveranceWindupEndColor,
            widthScale: 1.55f,
            revealInstantly: true,
            fadeOut: true));
        StartCoroutine(AnimateFinalSeveranceSlash(
            start,
            end,
            finalSeveranceWindupVfxDuration * 0.88f,
            finalSeveranceWindupStartColor,
            finalSeveranceWindupEndColor,
            widthScale: 1.15f,
            startDelay: 0.03f,
            revealInstantly: true,
            fadeOut: true));
    }

    /// <summary>Epic red severing slash when channel completes and damage lands.</summary>
    public void SpawnFinalSeveranceStrike(float halfReach, float combatFacingSign)
    {
        if (!TryGetFinalSeveranceSlashEndpoints(
                halfReach,
                combatFacingSign,
                finalSeveranceStrikeDiagonalRise,
                finalSeveranceStrikeDiagonalDrop,
                out Vector3 start,
                out Vector3 end))
            return;

        StartCoroutine(AnimateFinalSeveranceSlash(
            start,
            end,
            finalSeveranceStrikeVfxDuration,
            finalSeveranceStrikeStartColor,
            finalSeveranceStrikeEndColor,
            widthScale: 2.35f,
            revealInstantly: true,
            fadeOut: true,
            flashIn: true));
        StartCoroutine(AnimateFinalSeveranceSlash(
            start,
            end,
            finalSeveranceStrikeVfxDuration * 0.9f,
            finalSeveranceStrikeStartColor,
            finalSeveranceStrikeEndColor,
            widthScale: 1.75f,
            startDelay: 0.04f,
            revealInstantly: true,
            fadeOut: true));
        StartCoroutine(AnimateFinalSeveranceSlash(
            start + Vector3.up * 0.22f,
            end + Vector3.down * 0.14f,
            finalSeveranceStrikeVfxDuration * 0.82f,
            new Color(1f, 0.55f, 0.12f, 0.92f),
            new Color(1f, 0.1f, 0.05f, 0.75f),
            widthScale: 1.25f,
            startDelay: 0.07f,
            revealInstantly: true,
            fadeOut: true));
    }

    private bool TryGetFinalSeveranceSlashEndpoints(
        float halfReach,
        float combatFacingSign,
        float diagonalRise,
        float diagonalDrop,
        out Vector3 start,
        out Vector3 end)
    {
        start = default;
        end = default;

        Transform center = player != null ? player.transform : transform;
        if (center == null)
            return false;

        float sign = Mathf.Approximately(combatFacingSign, 0f) ? 1f : Mathf.Sign(combatFacingSign);
        Vector3 mid = center.position + finalSeveranceCenterOffset;
        float span = Mathf.Max(1f, halfReach);
        start = mid + Vector3.right * (-span * sign) + Vector3.up * diagonalRise;
        end = mid + Vector3.right * (span * sign) + Vector3.down * diagonalDrop;
        return true;
    }

    private IEnumerator AnimateFinalSeveranceSlash(
        Vector3 start,
        Vector3 end,
        float duration,
        Color lineStartColor,
        Color lineEndColor,
        float widthScale = 1f,
        float startDelay = 0f,
        bool revealInstantly = false,
        bool fadeOut = true,
        bool flashIn = false)
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        GameObject slashGo = new GameObject("FinalSeveranceSlashVfx");
        LineRenderer line = slashGo.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.positionCount = 2;
        line.startWidth = finalSeveranceLineWidth * widthScale;
        line.endWidth = finalSeveranceLineWidth * 0.55f * widthScale;
        line.numCapVertices = 10;
        line.numCornerVertices = 6;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.TransformZ;
        if (!TryApplyPlayerSpriteSortingToRenderer(line, 16))
            line.sortingOrder = 24;

        float d = Mathf.Max(0.05f, duration);
        float t = 0f;
        while (t < d)
        {
            if (line == null || slashGo == null)
                yield break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            float alphaMul = 1f;
            if (fadeOut)
                alphaMul = 1f - u;
            if (flashIn && u < 0.12f)
                alphaMul = Mathf.Max(alphaMul, 1f + (1f - u / 0.12f) * 0.35f);

            Color c0 = lineStartColor;
            Color c1 = lineEndColor;
            if (!revealInstantly && u < 0.08f)
            {
                float grow = u / 0.08f;
                alphaMul *= grow;
            }

            c0.a *= alphaMul;
            c1.a *= alphaMul;
            line.startColor = c0;
            line.endColor = c1;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            yield return null;
        }

        if (slashGo != null)
            Destroy(slashGo);
    }

    private IEnumerator SpawnProjectedCrescentWaveAfterDelay(
        Vector3 startPos,
        Vector3 direction,
        float reach,
        float delay,
        float reachScale,
        float alphaScale,
        int sortingOrderOffsetFromPlayer)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        GameObject arcGO = new GameObject("CrescentSlashArcVfx");
        arcGO.transform.position = startPos;
        LineRenderer line = arcGO.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startWidth = crescentSlashLineWidth;
        line.endWidth = crescentSlashLineWidth * 0.75f;
        line.numCapVertices = 6;
        line.numCornerVertices = 6;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.TransformZ;
        line.positionCount = 18;

        Color c = new Color(crescentSlashColor.r, crescentSlashColor.g, crescentSlashColor.b, crescentSlashColor.a * Mathf.Clamp01(alphaScale));
        line.startColor = c;
        line.endColor = new Color(c.r, c.g, c.b, 0f);
        if (!TryApplyPlayerSpriteSortingToRenderer(line, sortingOrderOffsetFromPlayer))
            line.sortingOrder = 20 + sortingOrderOffsetFromPlayer;

        yield return AnimateProjectedCrescentVfx(line, arcGO, startPos, direction, reach * Mathf.Max(0.1f, reachScale), crescentSlashVfxDuration);
    }

    private IEnumerator AnimateProjectedCrescentVfx(
        LineRenderer line,
        GameObject owner,
        Vector3 startPos,
        Vector3 direction,
        float reach,
        float duration)
    {
        if (line == null || owner == null)
            yield break;

        float d = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        Color baseColor = line.startColor;
        float visualRadius = Mathf.Clamp(reach * 0.22f, 0.9f, 2.8f);
        float startDeg = -52f;
        float endDeg = 52f;
        Vector3 endPos = startPos + (direction.normalized * Mathf.Max(0.1f, reach));

        while (elapsed < d && line != null)
        {
            float t = elapsed / d;
            Vector3 c = Vector3.Lerp(startPos, endPos, t);
            for (int i = 0; i < line.positionCount; i++)
            {
                float pt = i / Mathf.Max(1f, line.positionCount - 1f);
                float deg = Mathf.Lerp(startDeg, endDeg, pt);
                float rad = deg * Mathf.Deg2Rad;
                Vector3 local = new Vector3(
                    Mathf.Cos(rad) * visualRadius * Mathf.Sign(direction.x == 0f ? 1f : direction.x),
                    Mathf.Sin(rad) * visualRadius,
                    0f);
                line.SetPosition(i, c + local);
            }

            float a = Mathf.Lerp(baseColor.a, 0f, t);
            line.startColor = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            line.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (owner != null)
            Destroy(owner);
    }

    private IEnumerator AnimateWhirlwindBlades(
        GameObject root,
        Transform[] emitters,
        TrailRenderer[] trails,
        Transform center,
        float radius,
        float facingSignX,
        float[] phases,
        float[] laneBaseY,
        float[] laneOrbitHeight,
        float[] laneHorizontalScale)
    {
        if (root == null || emitters == null || center == null)
            yield break;

        float duration = Mathf.Max(0.06f, whirlingBladeDuration);
        float elapsed = 0f;
        float width = Mathf.Max(0.01f, whirlingBladeLineWidth);
        float visualRadius = Mathf.Max(0.05f, radius - (width * 0.5f));
        float spinCycles = Mathf.Max(1f, Mathf.Abs(whirlingBladeSpinDegrees) / 360f);
        float facingSign = Mathf.Sign(facingSignX == 0f ? 1f : facingSignX);
        int count = emitters.Length;

        while (elapsed < duration && center != null)
        {
            float t = elapsed / duration;
            Vector3 basePos = center.position + whirlingBladeCenterOffset;

            for (int i = 0; i < count; i++)
            {
                Transform emitter = emitters[i];
                if (emitter == null)
                    continue;

                EvaluateWhirlwindHorizontalSlashOrbit(
                    t,
                    visualRadius,
                    phases[i],
                    spinCycles,
                    facingSign,
                    laneBaseY != null && i < laneBaseY.Length ? laneBaseY[i] : 0f,
                    laneOrbitHeight != null && i < laneOrbitHeight.Length ? laneOrbitHeight[i] : 0f,
                    laneHorizontalScale != null && i < laneHorizontalScale.Length ? laneHorizontalScale[i] : 1f,
                    out float x,
                    out float y);
                emitter.position = basePos + new Vector3(x, y, 0f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (trails != null)
        {
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null)
                    trails[i].emitting = false;
            }
        }

        if (root != null)
            Destroy(root, Mathf.Max(0.04f, whirlingBladeDuration * 0.6f));
    }

    private IEnumerator AnimateGaleforceTwister(
        GameObject root,
        Transform[] emitters,
        TrailRenderer[] trails,
        float radius,
        float driftSpeed,
        float[] phases,
        float[] laneBaseY,
        float[] laneOrbitHeight,
        float[] laneHorizontalScale,
        GaleforceTwisterLifetime lifetime)
    {
        if (root == null || emitters == null || lifetime == null)
            yield break;

        float width = Mathf.Max(0.01f, whirlingBladeLineWidth * galeforceTwisterLineWidthScale);
        float visualRadius = Mathf.Max(0.05f, radius * galeforceTwisterSizeScale - (width * 0.5f));
        float spinCycles = Mathf.Max(1f, Mathf.Abs(galeforceTwisterSpinDegrees) / 360f);
        float spinRate = spinCycles / Mathf.Max(0.06f, galeforceTwisterDuration);
        int count = emitters.Length;
        float spinElapsed = 0f;
        float directionSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float directionTimer = 0f;
        float directionDuration = UnityEngine.Random.Range(
            galeforceTwisterMinDirectionSeconds,
            galeforceTwisterMaxDirectionSeconds);

        while (root != null && Time.time < lifetime.EndTime)
        {
            spinElapsed += Time.deltaTime;
            float t = Mathf.Repeat(spinElapsed * spinRate, 1f);
            Vector3 basePos = root.transform.position;

            for (int i = 0; i < count; i++)
            {
                Transform emitter = emitters[i];
                if (emitter == null)
                    continue;

                EvaluateWhirlwindHorizontalSlashOrbit(
                    t,
                    visualRadius,
                    phases[i],
                    spinCycles,
                    1f,
                    laneBaseY != null && i < laneBaseY.Length ? laneBaseY[i] : 0f,
                    laneOrbitHeight != null && i < laneOrbitHeight.Length ? laneOrbitHeight[i] : 0f,
                    laneHorizontalScale != null && i < laneHorizontalScale.Length ? laneHorizontalScale[i] : 1f,
                    out float x,
                    out float y);
                emitter.position = basePos + new Vector3(x, y, 0f);
            }

            directionTimer += Time.deltaTime;
            if (directionTimer >= directionDuration)
            {
                directionSign *= -1f;
                directionTimer = 0f;
                directionDuration = UnityEngine.Random.Range(
                    galeforceTwisterMinDirectionSeconds,
                    galeforceTwisterMaxDirectionSeconds);
            }

            root.transform.position += Vector3.right * (directionSign * driftSpeed * Time.deltaTime);
            yield return null;
        }

        if (trails != null)
        {
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null)
                    trails[i].emitting = false;
            }
        }

        if (root != null)
        {
            _activeGaleforceTwisterRoots.Remove(root);
            Destroy(root, Mathf.Max(0.04f, galeforceTwisterDuration * 0.6f));
        }
    }

    private float GetGaleforceTwisterBladeBaseY(int index)
    {
        if (index < 4)
            return GetWhirlwindBladeLaneValue(galeforceTwisterLaneBaseYOffsets, index);
        return galeforceTwisterFifthLaneBaseYOffset;
    }

    private float GetGaleforceTwisterBladeOrbitHeight(int index)
    {
        if (index < 4)
            return GetWhirlwindBladeLaneValue(galeforceTwisterLaneOrbitHeights, index);
        return galeforceTwisterFifthLaneOrbitHeight;
    }

    /// <summary>
    /// Layered side-view sweep around the player. Each trail uses a different phase/arc so the stack reads as
    /// overlapping cyclone motion instead of clean concentric rings.
    /// </summary>
    private void EvaluateWhirlwindHorizontalSlashOrbit(
        float normalizedTime,
        float visualRadius,
        float phaseOffset,
        float spinCycles,
        float facingSign,
        float laneBaseY,
        float laneOrbitHeight,
        float laneHorizontalScale,
        out float x,
        out float y)
    {
        float cycleT = Mathf.Repeat(normalizedTime * Mathf.Max(1f, spinCycles) + phaseOffset, 1f);
        float angle = cycleT * Mathf.PI * 2f;
        x = Mathf.Cos(angle) * visualRadius * Mathf.Max(0.1f, laneHorizontalScale) * Mathf.Sign(facingSign == 0f ? 1f : facingSign);
        y = laneBaseY + Mathf.Sin(angle) * laneOrbitHeight;
        y = Mathf.Clamp(y, Mathf.Min(whirlingBladeMinYOffset, whirlingBladeMaxYOffset), Mathf.Max(whirlingBladeMinYOffset, whirlingBladeMaxYOffset));
    }

    private float GetWhirlwindBladeBaseY(int index)
    {
        return index switch
        {
            0 => whirlingBladeLaneBaseYOffsets.x,
            1 => whirlingBladeLaneBaseYOffsets.y,
            2 => whirlingBladeLaneBaseYOffsets.z,
            3 => whirlingBladeLaneBaseYOffsets.w,
            _ => whirlingBladeFifthLaneBaseYOffset
        };
    }

    private float GetWhirlwindBladeOrbitHeight(int index)
    {
        return Mathf.Abs(index switch
        {
            0 => whirlingBladeLaneOrbitHeights.x,
            1 => whirlingBladeLaneOrbitHeights.y,
            2 => whirlingBladeLaneOrbitHeights.z,
            3 => whirlingBladeLaneOrbitHeights.w,
            _ => whirlingBladeFifthLaneOrbitHeight
        });
    }

    private static float GetWhirlwindBladeLaneValue(Vector4 values, int index)
    {
        return Mathf.Abs(index % 4) switch
        {
            0 => values.x,
            1 => values.y,
            2 => values.z,
            _ => values.w
        };
    }

    private static float ResolveWhirlwindBladeHorizontalScale(int bladeIndex)
    {
        if (bladeIndex < 0)
            return 1f;

        return bladeIndex switch
        {
            0 => 1f,
            1 => 0.85f,
            2 => 0.6f,
            3 => 0.45f,
            4 => 0.3f,
            _ => 0.3f
        };
    }

    private static float ResolveWhirlwindTrailTiltFactor(int index)
    {
        return index switch
        {
            0 => 0f,
            1 => 1f,
            2 => -1f,
            3 => 0.5f,
            4 => -0.5f,
            _ => index % 2 == 0 ? -0.75f : 0.75f
        };
    }

    /// <summary>Smoke puff at the player's departure point; lingers after teleport.</summary>
    public void SpawnShadowStrikeDepartSmoke(
        Vector3 departureWorldPosition,
        Color? coreColorOverride = null,
        Color? edgeColorOverride = null)
    {
        StartCoroutine(CoShadowStrikeDepartSmoke(
            departureWorldPosition + shadowStrikeDepartSmokeOffset,
            coreColorOverride,
            edgeColorOverride));
    }

    public void SpawnShadowStrikeBurst(Vector3 targetWorldPosition, Color? burstColorOverride = null)
    {
        Vector3 center = targetWorldPosition + shadowStrikeBurstOffset;
        StartCoroutine(CoShadowStrikeBurst(center, burstColorOverride));
    }

    private IEnumerator CoShadowStrikeDepartSmoke(
        Vector3 center,
        Color? coreColorOverride = null,
        Color? edgeColorOverride = null)
    {
        GameObject root = new GameObject("ShadowStrikeDepartSmoke");
        root.transform.position = center;

        ParticleSystem puff = CreateShadowStrikeDepartSmokeParticleSystem(root.transform, wispy: false, coreColorOverride, edgeColorOverride);
        ParticleSystem wisps = CreateShadowStrikeDepartSmokeParticleSystem(root.transform, wispy: true, coreColorOverride, edgeColorOverride);

        int burst = Mathf.Max(1, shadowStrikeDepartSmokeBurstCount);
        puff.Emit(burst);
        wisps.Emit(Mathf.Max(8, burst / 3));

        float linger = Mathf.Max(0.5f, shadowStrikeDepartSmokeLingerSeconds);
        float wispEmitWindow = Mathf.Min(linger * 0.5f, shadowStrikeDepartSmokeWispEmitSeconds);
        float elapsed = 0f;
        while (elapsed < linger)
        {
            elapsed += Time.deltaTime;
            if (elapsed < wispEmitWindow && wisps != null)
            {
                var emission = wisps.emission;
                emission.rateOverTime = 18f;
            }
            else if (wisps != null)
            {
                var emission = wisps.emission;
                emission.rateOverTime = 0f;
            }

            yield return null;
        }

        if (puff != null)
            puff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (wisps != null)
            wisps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Destroy(root);
    }

    private ParticleSystem CreateShadowStrikeDepartSmokeParticleSystem(
        Transform parent,
        bool wispy,
        Color? coreColorOverride = null,
        Color? edgeColorOverride = null)
    {
        string childName = wispy ? "ShadowStrikeDepartWisps" : "ShadowStrikeDepartPuff";
        GameObject emitterGO = new GameObject(childName);
        emitterGO.transform.SetParent(parent, false);
        emitterGO.transform.localPosition = Vector3.zero;
        emitterGO.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = emitterGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = Mathf.Max(0.5f, shadowStrikeDepartSmokeLingerSeconds);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = wispy ? -0.08f : -0.12f;

        if (wispy)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        }
        else
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.58f);
        }

        Color core = coreColorOverride ?? shadowStrikeDepartSmokeColor;
        Color edge = edgeColorOverride ?? Color.Lerp(core, new Color(0.62f, 0.28f, 0.88f, core.a), 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(core, edge);

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = wispy ? 0.22f : 0.38f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(wispy ? 0.55f : 0.35f, wispy ? 1.2f : 0.85f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, wispy ? 0.45f : 0.55f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, wispy ? 1.35f : 1.55f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(core, 0f),
                new GradientColorKey(edge, 0.35f),
                new GradientColorKey(Color.Lerp(core, Color.black, 0.25f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(core.a * (wispy ? 0.85f : 1f)), 0f),
                new GradientAlphaKey(Mathf.Clamp01(core.a * 0.55f), 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(g);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = wispy ? 0.22f : 0.35f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (!TryApplyPlayerSpriteSortingToRenderer(renderer, wispy ? 9 : 11))
            renderer.sortingOrder = wispy ? 20 : 22;
        ApplyRuntimeParticleMaterialIfNeeded(renderer);

        ps.Play(true);
        return ps;
    }

    private IEnumerator CoShadowStrikeBurst(Vector3 center, Color? burstColorOverride = null)
    {
        Color burstColor = burstColorOverride ?? shadowStrikeBurstColor;

        GameObject root = new GameObject("ShadowStrikeBurst");
        LineRenderer ring = root.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 24;
        ring.widthMultiplier = shadowStrikeBurstLineWidth;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = burstColor;
        ring.endColor = burstColor;
        if (!TryApplyPlayerSpriteSortingToRenderer(ring, 12))
            ring.sortingOrder = 24;

        float duration = Mathf.Max(0.05f, shadowStrikeBurstDuration);
        float maxRadius = Mathf.Max(0.1f, shadowStrikeBurstRadius);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float radius = maxRadius * t;
            float alpha = burstColor.a * (1f - t);
            Color c = burstColor;
            c.a = alpha;
            ring.startColor = c;
            ring.endColor = c;

            for (int i = 0; i < ring.positionCount; i++)
            {
                float ang = i / (float)ring.positionCount * Mathf.PI * 2f;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius * 0.35f, 0f));
            }

            yield return null;
        }

        Destroy(root);
    }

    public void SpawnPowerSlashTrail()
    {
        Transform anchor = ResolvePowerSlashAnchor();
        if (anchor == null)
            return;

        SpawnSinglePowerSlashTrail(anchor, 0f, 0f);
        if (powerSlashUseDoubleSwipe)
            SpawnSinglePowerSlashTrail(anchor, powerSlashSecondSwipeAngleOffset, powerSlashSecondSwipeDelay);
    }

    /// <summary>Simple diagonal slash between player and attacker (default Parry feedback; no swing cadence).</summary>
    public void SpawnParrySlashLine(Vector3 playerWorld, Vector3 enemyWorld)
    {
        Vector3 height = parrySlashHeightOffset;
        SpawnDiagonalMeleeSlashLine(
            playerWorld + height,
            enemyWorld + height,
            parrySlashColor,
            parrySlashLineWidth,
            parrySlashDuration,
            sortingOrderBump: 12,
            objectName: "ParrySlash");
    }

    public void SpawnCrusaderStrikeBeam(Vector3 enemyWorld, bool finalStrike)
    {
        StartCoroutine(CoCrusaderStrikeBeam(enemyWorld, finalStrike));
    }

    /// <summary>Parry-style yellow slash on each Relentless Execution (Bladestorm) hit.</summary>
    public void SpawnBladestormHitSlash(Vector3 playerWorld, Vector3 enemyWorld)
    {
        SpawnBladestormSlashOverEnemy(
            playerWorld,
            enemyWorld,
            bladestormHitSlashLineWidth,
            bladestormHitSlashDuration,
            bladestormHitSlashHalfLength,
            sortingOrderBump: 16,
            objectName: "BladestormHitSlash");
    }

    private IEnumerator CoCrusaderStrikeBeam(Vector3 enemyWorld, bool finalStrike)
    {
        float facingSign = GetCombatFacingSign();
        if (Mathf.Abs(facingSign) < 0.01f)
            facingSign = 1f;

        Vector3 playerWorld = player != null ? player.transform.position : transform.position;
        Vector3 impact = enemyWorld + crusaderStrikeBeamCenterOffset;
        Vector3 originBase =
            playerWorld +
            Vector3.right * (-facingSign * crusaderStrikeBeamBehindPlayerX) +
            Vector3.up * crusaderStrikeBeamBehindPlayerY;

        float duration = Mathf.Max(0.05f, crusaderStrikeBeamDuration);
        Color coreColor = finalStrike ? crusaderStrikeBeamFinalColor : crusaderStrikeBeamInnerColor;
        Color outerColor = finalStrike
            ? Color.Lerp(crusaderStrikeBeamOuterColor, crusaderStrikeBeamFinalColor, 0.55f)
            : crusaderStrikeBeamOuterColor;

        float sizeScale = finalStrike ? crusaderStrikeBeamFinalSizeScale : 1f;
        GameObject root = new GameObject(finalStrike ? "CrusaderStrikeFinalBolt" : "CrusaderStrikeBolt");
        root.transform.position = originBase + Vector3.up * crusaderStrikeBeamSkyHeight;

        SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
        sprite.sprite = GetRuntimeCircleSprite();
        sprite.color = coreColor;
        root.transform.localScale = Vector3.one * Mathf.Max(0.02f, crusaderStrikeBeamPointSize * sizeScale);
        if (!TryApplyPlayerSpriteSortingToRenderer(sprite, 20))
            sprite.sortingOrder = 42;

        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        ConfigureCrusaderBoltTrail(trail, outerColor, coreColor, sizeScale);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;
            float eased = 1f - Mathf.Pow(1f - t, 2.35f);
            float lateralTighten = Mathf.Lerp(1f, 0.78f, eased);
            Vector3 origin =
                originBase +
                Vector3.right * (-facingSign * 0.35f * lateralTighten) +
                Vector3.up * crusaderStrikeBeamSkyHeight;
            Vector3 pos = Vector3.Lerp(origin, impact, eased);
            root.transform.position = pos;
            sprite.color = WithAlpha(coreColor, coreColor.a * Mathf.Lerp(1f, 0.65f, t));

            yield return null;
        }

        trail.emitting = false;
        float fadeTime = Mathf.Max(0.04f, crusaderStrikeBeamTrailTime * 0.55f);
        float fadeElapsed = 0f;
        Vector3 impactScale = Vector3.one * Mathf.Max(0.02f, crusaderStrikeBeamPointSize * sizeScale * 1.5f);
        while (fadeElapsed < fadeTime)
        {
            fadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(fadeElapsed / fadeTime);
            sprite.color = WithAlpha(coreColor, coreColor.a * (1f - t));
            root.transform.localScale = Vector3.Lerp(Vector3.one * Mathf.Max(0.02f, crusaderStrikeBeamPointSize * sizeScale), impactScale, t);
            yield return null;
        }

        Destroy(root);
    }

    /// <summary>Larger finale slash (same style as channel hits).</summary>
    public void SpawnBladestormFinaleSlash(Vector3 playerWorld, Vector3 enemyWorld)
    {
        SpawnBladestormSlashOverEnemy(
            playerWorld,
            enemyWorld,
            bladestormHitSlashLineWidth * bladestormFinaleSlashWidthScale,
            bladestormHitSlashDuration * bladestormFinaleSlashDurationScale,
            bladestormHitSlashHalfLength * bladestormFinaleSlashLengthScale,
            sortingOrderBump: 18,
            objectName: "BladestormFinaleSlash");
    }

    private void SpawnBladestormSlashOverEnemy(
        Vector3 playerWorld,
        Vector3 enemyWorld,
        float lineWidth,
        float durationSeconds,
        float halfLength,
        int sortingOrderBump,
        string objectName)
    {
        Vector3 center = enemyWorld + bladestormHitSlashCenterOffset;
        if (bladestormHitSlashSpawnJitter.x > 0f)
            center.x += UnityEngine.Random.Range(-bladestormHitSlashSpawnJitter.x, bladestormHitSlashSpawnJitter.x);
        if (bladestormHitSlashSpawnJitter.y > 0f)
            center.y += UnityEngine.Random.Range(-bladestormHitSlashSpawnJitter.y, bladestormHitSlashSpawnJitter.y);

        float facingX = enemyWorld.x - playerWorld.x;
        if (Mathf.Abs(facingX) < 0.01f)
            facingX = player != null ? GetCombatFacingSign() : 1f;
        facingX = Mathf.Sign(facingX);

        // Default slash leans toward the player (readable chop on the target).
        float baseAngleDeg = facingX > 0f ? -48f : -132f;
        float jitter = Mathf.Max(0f, bladestormHitSlashAngleJitterDegrees);
        float angleDeg = baseAngleDeg + UnityEngine.Random.Range(-jitter, jitter);
        float angleRad = angleDeg * Mathf.Deg2Rad;
        Vector3 slashDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

        float halfLen = Mathf.Max(0.1f, halfLength) * UnityEngine.Random.Range(0.88f, 1.12f);
        Vector3 start = center + slashDir * halfLen;
        Vector3 end = center - slashDir * halfLen;

        StartCoroutine(CoMeleeSlashSegment(
            start,
            end,
            bladestormHitSlashColor,
            lineWidth,
            durationSeconds,
            sortingOrderBump,
            objectName));
    }

    private void SpawnDiagonalMeleeSlashLine(
        Vector3 playerPos,
        Vector3 enemyPos,
        Color color,
        float lineWidth,
        float durationSeconds,
        int sortingOrderBump,
        string objectName)
    {
        Vector3 mid = (playerPos + enemyPos) * 0.5f;
        Vector3 toEnemy = enemyPos - playerPos;
        float span = Mathf.Max(0.35f, toEnemy.magnitude);
        Vector3 dir = toEnemy.sqrMagnitude > 1e-6f ? toEnemy.normalized : Vector3.right;
        Vector3 slashDir = (dir + Vector3.down * 0.85f).normalized;
        Vector3 start = mid + slashDir * (span * 0.42f);
        Vector3 end = mid - slashDir * (span * 0.42f);

        StartCoroutine(CoMeleeSlashSegment(
            start, end, color, lineWidth, durationSeconds, sortingOrderBump, objectName));
    }

    private void ConfigureCrusaderBoltTrail(TrailRenderer trail, Color outerColor, Color coreColor, float sizeScale)
    {
        if (trail == null)
            return;

        trail.time = Mathf.Max(0.02f, crusaderStrikeBeamTrailTime);
        trail.minVertexDistance = 0.01f;
        trail.widthMultiplier = Mathf.Max(0.01f, crusaderStrikeBeamTrailWidth * sizeScale);
        trail.numCornerVertices = 3;
        trail.numCapVertices = 1;
        trail.alignment = LineAlignment.TransformZ;
        trail.textureMode = LineTextureMode.Stretch;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.95f),
            new Keyframe(0.18f, 1f),
            new Keyframe(1f, 0f));
        if (!TryApplyPlayerSpriteSortingToRenderer(trail, 19))
            trail.sortingOrder = 40;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(coreColor, Color.white, 0.35f), 0f),
                new GradientColorKey(coreColor, 0.3f),
                new GradientColorKey(outerColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(coreColor.a), 0f),
                new GradientAlphaKey(Mathf.Clamp01(coreColor.a * 0.9f), 0.18f),
                new GradientAlphaKey(Mathf.Clamp01(outerColor.a * 0.4f), 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
        trail.Clear();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static Sprite GetRuntimeCircleSprite()
    {
        if (s_RuntimeCircleSprite != null)
            return s_RuntimeCircleSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "RuntimeCircleSprite"
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        float radius = (size - 1) * 0.5f;
        float radiusSq = radius * radius;
        Vector2 center = new Vector2(radius, radius);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float distSq = delta.sqrMagnitude;
                if (distSq > radiusSq)
                {
                    pixels[y * size + x] = clear;
                    continue;
                }

                float dist = Mathf.Sqrt(distSq);
                float edgeAlpha = Mathf.Clamp01((radius - dist) / 1.35f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, edgeAlpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        s_RuntimeCircleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        return s_RuntimeCircleSprite;
    }

    /// <summary>Filled disc: 0 alpha at center, full alpha at outer edge (tint via <see cref="hammerTempestRingColor"/>).</summary>
    private static Sprite GetHammerTempestRadialRingSprite()
    {
        if (s_HammerTempestRadialRingSprite != null)
            return s_HammerTempestRadialRingSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "HammerTempestRadialRingSprite"
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        float radius = (size - 1) * 0.5f;
        float radiusSq = radius * radius;
        Vector2 center = new Vector2(radius, radius);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float distSq = delta.sqrMagnitude;
                if (distSq > radiusSq)
                {
                    pixels[y * size + x] = clear;
                    continue;
                }

                float dist = Mathf.Sqrt(distSq);
                float t = radius > 0.0001f ? dist / radius : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, t);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        s_HammerTempestRadialRingSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        return s_HammerTempestRadialRingSprite;
    }

    private float GetHammerTempestRingWorldRadius() =>
        Mathf.Max(0.1f, hammerTempestOrbitRadius - hammerTempestRingRadiusInset);

    /// <summary>World-space orbit radius; combat hits anything inside this disc from the orbit center.</summary>
    public float GetHammerTempestOrbitRadiusWorld() => Mathf.Max(0.1f, hammerTempestOrbitRadius);

    public Vector3 GetHammerTempestCenterOffsetWorld() => hammerTempestCenterOffset;
    public float GetHammerTempestMainOrbitDegreesPerSecond() => Mathf.Max(0f, hammerTempestMainOrbitDegreesPerSecond);

    private void CreateHammerTempestRingVisual()
    {
        if (_hammerTempestOrbitRoot == null)
            return;

        _hammerTempestRingObject = new GameObject("HammerTempestRing");
        _hammerTempestRingObject.transform.SetParent(_hammerTempestOrbitRoot.transform, false);
        _hammerTempestRingRenderer = _hammerTempestRingObject.AddComponent<SpriteRenderer>();
        _hammerTempestRingRenderer.sprite = GetHammerTempestRadialRingSprite();
        ApplyHammerTempestRingColor();
        if (!TryApplyPlayerSpriteSortingToRenderer(_hammerTempestRingRenderer, 4))
            _hammerTempestRingRenderer.sortingOrder = 18;
    }

    private void UpdateHammerTempestRingVisual(Vector3 pivot, float facingSign)
    {
        if (_hammerTempestRingObject == null || _hammerTempestRingRenderer == null)
            return;

        float ringRadius = GetHammerTempestRingWorldRadius();
        float diameter = ringRadius * 2f;
        float sign = Mathf.Sign(facingSign == 0f ? 1f : facingSign);
        _hammerTempestRingObject.transform.position = pivot;
        _hammerTempestRingObject.transform.localScale = new Vector3(diameter * sign, diameter, 1f);
        ApplyHammerTempestRingColor();
    }

    private void ApplyHammerTempestRingColor()
    {
        if (_hammerTempestRingRenderer == null)
            return;

        Color c = hammerTempestRingColor;
        c.a *= hammerTempestRingEdgeOpacity;
        _hammerTempestRingRenderer.color = c;
    }

    private IEnumerator CoMeleeSlashSegment(
        Vector3 start,
        Vector3 end,
        Color color,
        float lineWidth,
        float durationSeconds,
        int sortingOrderBump,
        string objectName)
    {
        GameObject root = new GameObject(string.IsNullOrWhiteSpace(objectName) ? "MeleeSlash" : objectName);
        LineRenderer line = root.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        float width = Mathf.Max(0.01f, lineWidth);
        line.startWidth = width;
        line.endWidth = width * 0.35f;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;
        if (!TryApplyPlayerSpriteSortingToRenderer(line, sortingOrderBump))
            line.sortingOrder = 24 + sortingOrderBump;

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        float duration = Mathf.Max(0.05f, durationSeconds);
        float elapsed = 0f;
        Color baseColor = color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);
            Color c = baseColor;
            c.a *= alpha;
            line.startColor = c;
            line.endColor = c;
            yield return null;
        }

        Destroy(root);
    }

    private void SpawnSinglePowerSlashTrail(Transform anchor, float angleOffset, float delay)
    {
        GameObject slashGO = new GameObject("PowerSlashTrail");
        slashGO.transform.SetParent(anchor, false);
        slashGO.transform.localPosition = Vector3.zero;

        TrailRenderer trail = slashGO.AddComponent<TrailRenderer>();
        trail.time = Mathf.Max(0.01f, powerSlashTrailTime);
        trail.minVertexDistance = 0.004f;
        trail.widthMultiplier = Mathf.Max(0.01f, powerSlashTrailWidth);
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.TransformZ;
        trail.textureMode = LineTextureMode.Stretch;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.emitting = false;
        if (!TryApplyPlayerSpriteSortingToRenderer(trail, 11))
            trail.sortingOrder = 22;

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.95f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f));
        trail.widthCurve = widthCurve;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(powerSlashTrailColor, 0f),
                new GradientColorKey(Color.Lerp(powerSlashTrailColor, Color.white, 0.25f), 0.45f),
                new GradientColorKey(powerSlashTrailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(powerSlashTrailColor.a, 0f),
                new GradientAlphaKey(Mathf.Clamp01(powerSlashTrailColor.a * 0.75f), 0.35f),
                new GradientAlphaKey(Mathf.Clamp01(powerSlashTrailColor.a * 0.4f), 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;

        StartCoroutine(AnimatePowerSlashTrail(
            slashGO.transform,
            anchor,
            powerSlashSwingDuration,
            trail.time + 0.08f,
            angleOffset,
            Mathf.Max(0f, delay)));
    }

    private IEnumerator AnimatePowerSlashTrail(
        Transform slashTransform,
        Transform anchor,
        float swingDuration,
        float lingerAfter,
        float angleOffset,
        float startDelay)
    {
        if (slashTransform == null || anchor == null)
            yield break;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float duration = Mathf.Max(0.01f, swingDuration);
        float elapsed = 0f;

        float facing = 1f;
        EnemyBaseController target = combat != null ? combat.CurrentTarget : null;
        if (target != null)
            facing = target.transform.position.x >= transform.position.x ? 1f : -1f;
        else if (player != null)
            facing = player.transform.localScale.x >= 0f ? 1f : -1f;

        float startAngle = powerSlashAngleRange.x;
        float endAngle = powerSlashAngleRange.y;
        Vector3 smoothWorldPos = slashTransform.position;
        slashTransform.localPosition = powerSlashLocalOffset;
        TrailRenderer trail = slashTransform.GetComponent<TrailRenderer>();
        if (trail != null)
            trail.Clear();

        while (elapsed < duration && slashTransform != null && anchor != null)
        {
            float t = elapsed / duration;
            float angle = Mathf.Lerp(startAngle, endAngle, t) + angleOffset;
            float signedAngle = angle * facing;

            Vector2 dir = new Vector2(Mathf.Cos(signedAngle * Mathf.Deg2Rad), Mathf.Sin(signedAngle * Mathf.Deg2Rad));
            Vector3 tipLocal = powerSlashLocalOffset + new Vector3(
                dir.x * powerSlashTipLocalOffset.x * facing,
                dir.y * powerSlashTipLocalOffset.y,
                0f);

            Vector3 desiredWorld = anchor.TransformPoint(tipLocal);
            float smooth = Mathf.Clamp01(powerSlashEdgeFollowSmoothing <= 0.0001f ? 1f : (Time.deltaTime / powerSlashEdgeFollowSmoothing));
            smoothWorldPos = Vector3.Lerp(smoothWorldPos, desiredWorld, smooth);
            slashTransform.position = smoothWorldPos;

            if (trail != null && !trail.emitting && t >= 0.05f)
                trail.emitting = true;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (slashTransform != null)
            Destroy(slashTransform.gameObject, Mathf.Max(0.05f, lingerAfter));
    }

    private Transform ResolvePowerSlashAnchor()
    {
        if (powerSlashTrailAnchor != null)
            return powerSlashTrailAnchor;

        MainHandEquipper mainHand = GetComponentInChildren<MainHandEquipper>(true);
        if (mainHand != null)
        {
            Transform root = mainHand.transform.root;
            Transform found = FindChildByCandidateName(root, powerSlashAnchorNameCandidates);
            if (found != null)
            {
                powerSlashTrailAnchor = found;
                return powerSlashTrailAnchor;
            }
        }

        powerSlashTrailAnchor = FindChildByCandidateName(transform.root, powerSlashAnchorNameCandidates);
        if (powerSlashTrailAnchor != null)
            return powerSlashTrailAnchor;

        return transform;
    }

    private static Transform FindChildByCandidateName(Transform root, string[] candidates)
    {
        if (root == null || candidates == null || candidates.Length == 0)
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null)
                continue;

            for (int c = 0; c < candidates.Length; c++)
            {
                string candidate = candidates[c];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (string.Equals(t.name, candidate, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
        }

        return null;
    }

    public void SpawnLumberFrenzyOrbitVfx(bool isFishingFrenzy = false)
    {
        DestroyLumberFrenzyOrbitVfx();

        Transform followRoot = player != null ? player.transform : transform;
        if (followRoot == null)
            return;

        Color vfxColor = isFishingFrenzy ? fishingFrenzyOrbitVfxColor : lumberFrenzyOrbitVfxColor;

        _lumberFrenzyAnchorRoot = new GameObject(isFishingFrenzy ? "FishingFrenzyVfxAnchor" : "LumberFrenzyVfxAnchor");
        _lumberFrenzyAnchorRoot.transform.SetParent(followRoot, false);
        _lumberFrenzyAnchorRoot.transform.localRotation = Quaternion.identity;
        _lumberFrenzyAnchorRoot.transform.localScale = Vector3.one;
        SyncGatheringFrenzyAnchorLocalPosition();

        if (lumberFrenzyOrbitVfxPrefab != null)
        {
            _lumberFrenzyOrbitVfxRoot = Instantiate(lumberFrenzyOrbitVfxPrefab, _lumberFrenzyAnchorRoot.transform);
            _lumberFrenzyOrbitVfxRoot.name = isFishingFrenzy ? "FishingFrenzyOrbitVfx" : "LumberFrenzyOrbitVfx";
            Transform t = _lumberFrenzyOrbitVfxRoot.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            TryApplyPlayerSpriteSortingToHierarchy(t, 10);
            ApplyGatheringFrenzyColorToHierarchy(t, vfxColor);
            return;
        }

        CreateRuntimeGatheringFrenzyOrbitParticles(_lumberFrenzyAnchorRoot.transform, vfxColor);
    }

    private void SyncGatheringFrenzyAnchorLocalPosition()
    {
        if (_lumberFrenzyAnchorRoot == null)
            return;

        float facingX = 1f;
        if (player != null && !Mathf.Approximately(player.FacingDirectionX, 0f))
            facingX = Mathf.Sign(player.FacingDirectionX);
        else if (player != null)
            facingX = Mathf.Sign(player.transform.localScale.x >= 0f ? 1f : -1f);

        float behindX = -facingX * gatheringFrenzyBehindDistance;
        _lumberFrenzyAnchorRoot.transform.localPosition = new Vector3(
            behindX + lumberFrenzyOrbitVfxLocalOffset.x,
            lumberFrenzyOrbitVfxLocalOffset.y,
            lumberFrenzyOrbitVfxLocalOffset.z);
    }

    private void CreateRuntimeGatheringFrenzyOrbitParticles(Transform parent, Color vfxColor)
    {
        _lumberFrenzyOrbitVfxRoot = new GameObject("GatheringFrenzyOrbitVfx_Runtime");
        Transform root = _lumberFrenzyOrbitVfxRoot.transform;
        root.SetParent(parent, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        GameObject emitterGO = new GameObject("GatheringFrenzySparkEmitter");
        emitterGO.transform.SetParent(root, false);
        emitterGO.transform.localPosition = Vector3.zero;
        emitterGO.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = emitterGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
        main.startColor = vfxColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 320;

        var emission = ps.emission;
        emission.rateOverTime = 48f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;

        ApplyGatheringFrenzyParticleColorOverLifetime(ps, vfxColor);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.12f, 1f),
            new Keyframe(1f, 0.15f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (!TryApplyPlayerSpriteSortingToRenderer(renderer, 10))
            renderer.sortingOrder = 19;
        ApplyRuntimeParticleMaterialIfNeeded(renderer);

        _gatheringFrenzyRuntimeParticles = ps;
        ps.Play(true);
    }

    private static void ApplyGatheringFrenzyParticleColorOverLifetime(ParticleSystem ps, Color vfxColor)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(vfxColor, 0f),
                new GradientColorKey(Color.Lerp(vfxColor, Color.white, 0.2f), 0.45f),
                new GradientColorKey(vfxColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(vfxColor.a), 0f),
                new GradientAlphaKey(Mathf.Clamp01(vfxColor.a * 0.75f), 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private void ApplyGatheringFrenzyColorToHierarchy(Transform root, Color vfxColor)
    {
        if (root == null)
            return;

        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (!ps)
                continue;

            var main = ps.main;
            main.startColor = vfxColor;
            ApplyGatheringFrenzyParticleColorOverLifetime(ps, vfxColor);
        }
    }

    private void ApplyGatheringFrenzyOrbitColor(Color vfxColor)
    {
        if (_gatheringFrenzyRuntimeParticles != null)
        {
            var main = _gatheringFrenzyRuntimeParticles.main;
            main.startColor = vfxColor;
            ApplyGatheringFrenzyParticleColorOverLifetime(_gatheringFrenzyRuntimeParticles, vfxColor);
            return;
        }

        if (_lumberFrenzyOrbitVfxRoot != null)
            ApplyGatheringFrenzyColorToHierarchy(_lumberFrenzyOrbitVfxRoot.transform, vfxColor);
    }

    public void UpdateLumberFrenzyOrbitVfx(bool lumberFrenzyActive, bool fishingFrenzyActive)
    {
        if (!lumberFrenzyActive && !fishingFrenzyActive)
            return;
        if (_lumberFrenzyAnchorRoot == null)
            return;

        SyncGatheringFrenzyAnchorLocalPosition();

        bool useFishingColor = fishingFrenzyActive && !lumberFrenzyActive;
        Color vfxColor = useFishingColor ? fishingFrenzyOrbitVfxColor : lumberFrenzyOrbitVfxColor;
        ApplyGatheringFrenzyOrbitColor(vfxColor);
    }

    public void DestroyLumberFrenzyOrbitVfx()
    {
        _gatheringFrenzyRuntimeParticles = null;
        _lumberFrenzyOrbitVfxRoot = null;
        if (_lumberFrenzyAnchorRoot != null)
        {
            Destroy(_lumberFrenzyAnchorRoot);
            _lumberFrenzyAnchorRoot = null;
        }
    }

    public void UpdateCleavingChopRangeIndicator(bool buffActive, float radiusWorld, Vector3? worldCenter)
    {
        if (!cleavingChopShowRangeIndicator || !AreAbilityRangeIndicatorsEnabled())
        {
            if (_cleavingChopIndicatorRoot != null && _cleavingChopIndicatorRoot.activeSelf)
                _cleavingChopIndicatorRoot.SetActive(false);
            return;
        }

        if (!buffActive || !worldCenter.HasValue || radiusWorld <= 0f)
        {
            if (_cleavingChopIndicatorRoot != null && _cleavingChopIndicatorRoot.activeSelf)
                _cleavingChopIndicatorRoot.SetActive(false);
            _cleavingChopIndicatorAppliedRadius = float.NaN;
            return;
        }

        EnsureCleavingChopIndicatorBuilt();
        if (_cleavingChopIndicatorRoot == null || _cleavingChopIndicatorLine == null)
            return;

        if (!_cleavingChopIndicatorRoot.activeSelf)
            _cleavingChopIndicatorRoot.SetActive(true);

        _cleavingChopIndicatorRoot.transform.position = worldCenter.Value;

        float radius = Mathf.Max(0f, radiusWorld);
        if (!Mathf.Approximately(_cleavingChopIndicatorAppliedRadius, radius))
        {
            RebuildCleavingChopIndicatorCircle(radius);
            _cleavingChopIndicatorAppliedRadius = radius;
        }
    }

    /// <summary>
    /// Draws green/white outlines on woodcutting tree colliders while gathering buffs are active.
    /// Spectral Axe: green box only on its locked gather target (including when depleted).
    /// Avatar of the Forest: green on the tree the player is actively cutting; other trees use replenish range.
    /// Cleaving Chop: green in range, white on other nearby trees.
    /// </summary>
    public void UpdateWoodcuttingTreeRangeOutlines(PlayerAbilityController abilities)
    {
        if (!woodcuttingTreeRangeOutlinesEnabled || !AreAbilityRangeIndicatorsEnabled() || abilities == null || player == null)
        {
            ClearWoodcuttingTreeRangeOutlines();
            return;
        }

        bool cleavingActive = abilities.IsCleavingChopActive;
        ResourceNode spectralTarget = abilities.SpectralAxeGatherTarget;
        bool spectralActive = spectralTarget != null;
        bool avatarActive = abilities.IsAvatarOfTheForestActive;
        ResourceNode primaryTarget = player.CurrentTarget;
        bool avatarPrimaryGatherActive = avatarActive && IsPlayerWoodcuttingTarget(primaryTarget);
        if (!cleavingActive && !spectralActive && !avatarActive)
        {
            ClearWoodcuttingTreeRangeOutlines();
            return;
        }

        _woodcuttingTreeOutlineScratch.Clear();

        if (spectralActive && TryGetResourceNodeColliderBounds(spectralTarget, out Bounds spectralBounds))
        {
            LineRenderer spectralOutline = EnsureWoodcuttingTreeOutline(spectralTarget);
            if (spectralOutline != null)
            {
                ApplyWoodcuttingTreeOutlineBounds(spectralOutline, spectralBounds, woodcuttingTreeOutlineInRangeColor);
                _woodcuttingTreeOutlineScratch.Add(spectralTarget);
            }
        }

        if (avatarPrimaryGatherActive &&
            TryGetResourceNodeColliderBounds(primaryTarget, out Bounds avatarPrimaryBounds))
        {
            LineRenderer avatarPrimaryOutline = EnsureWoodcuttingTreeOutline(primaryTarget);
            if (avatarPrimaryOutline != null)
            {
                ApplyWoodcuttingTreeOutlineBounds(avatarPrimaryOutline, avatarPrimaryBounds, woodcuttingTreeOutlineInRangeColor);
                _woodcuttingTreeOutlineScratch.Add(primaryTarget);
            }
        }

        if (!cleavingActive && !avatarActive)
        {
            RemoveStaleWoodcuttingTreeOutlines();
            return;
        }

        Vector3 cleavingOrigin = default;
        float cleavingRange = 0f;
        bool cleavingOriginReady = cleavingActive &&
                                   primaryTarget != null &&
                                   primaryTarget.ActionType == NodeAction.Woodcutting;
        if (cleavingOriginReady)
        {
            cleavingOrigin = primaryTarget.transform.position;
            cleavingRange = abilities.GetCleavingChopRange();
        }

        Vector3 avatarOrigin = player.transform.position;
        float avatarRange = avatarActive ? abilities.GetAvatarOfTheForestReplenishRadiusWorld() : 0f;

        float scanRadius = woodcuttingTreeOutlineScanPadding;
        if (cleavingRange > 0f)
            scanRadius = Mathf.Max(scanRadius, cleavingRange);
        if (avatarRange > 0f)
            scanRadius = Mathf.Max(scanRadius, avatarRange);

        Vector3 playerPos = player.transform.position;
        float scanRadiusSqr = scanRadius * scanRadius;

        ResourceNode[] all = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            ResourceNode node = all[i];
            if (!ShouldDrawWoodcuttingTreeRangeOutline(node))
                continue;

            if (spectralActive && node == spectralTarget)
                continue;

            if (avatarPrimaryGatherActive && node == primaryTarget)
                continue;

            if (spectralActive &&
                spectralTarget != null &&
                node == primaryTarget &&
                node != spectralTarget)
                continue;

            if (!TryGetResourceNodeColliderBounds(node, out Bounds bounds))
                continue;

            Vector3 boundsCenter = bounds.center;
            float dx = boundsCenter.x - playerPos.x;
            float dy = boundsCenter.y - playerPos.y;
            if (dx * dx + dy * dy > scanRadiusSqr)
                continue;

            bool inRange = false;
            if (cleavingOriginReady &&
                node != primaryTarget &&
                IsWoodcuttingNodeWithinPivotRange(node, cleavingOrigin, cleavingRange))
            {
                inRange = true;
            }

            if (avatarActive &&
                node.UsesDepletion &&
                IsWoodcuttingNodeWithinPivotRange(node, avatarOrigin, avatarRange))
            {
                inRange = true;
            }

            Color color = inRange ? woodcuttingTreeOutlineInRangeColor : woodcuttingTreeOutlineOutOfRangeColor;
            LineRenderer outline = EnsureWoodcuttingTreeOutline(node);
            if (outline == null)
                continue;

            ApplyWoodcuttingTreeOutlineBounds(outline, bounds, color);
            _woodcuttingTreeOutlineScratch.Add(node);
        }

        RemoveStaleWoodcuttingTreeOutlines();
    }

    private void RemoveStaleWoodcuttingTreeOutlines()
    {
        if (_woodcuttingTreeOutlineByNode.Count == 0)
            return;

        _woodcuttingTreeOutlineRemoveScratch.Clear();
        foreach (KeyValuePair<ResourceNode, LineRenderer> pair in _woodcuttingTreeOutlineByNode)
        {
            if (pair.Key == null || !_woodcuttingTreeOutlineScratch.Contains(pair.Key))
                _woodcuttingTreeOutlineRemoveScratch.Add(pair.Key);
        }

        for (int i = 0; i < _woodcuttingTreeOutlineRemoveScratch.Count; i++)
            RemoveWoodcuttingTreeOutline(_woodcuttingTreeOutlineRemoveScratch[i]);
    }

    private void ClearWoodcuttingTreeRangeOutlines()
    {
        if (_woodcuttingTreeOutlineByNode.Count == 0)
            return;

        _woodcuttingTreeOutlineRemoveScratch.Clear();
        foreach (KeyValuePair<ResourceNode, LineRenderer> pair in _woodcuttingTreeOutlineByNode)
            _woodcuttingTreeOutlineRemoveScratch.Add(pair.Key);

        for (int i = 0; i < _woodcuttingTreeOutlineRemoveScratch.Count; i++)
            RemoveWoodcuttingTreeOutline(_woodcuttingTreeOutlineRemoveScratch[i]);
    }

    private bool IsPlayerWoodcuttingTarget(ResourceNode node)
    {
        if (player == null || node == null || node.ActionType != NodeAction.Woodcutting)
            return false;

        return player.CurrentAction == PlayerController.PlayerAction.Woodcutting && player.CurrentTarget == node;
    }

    private static bool ShouldDrawWoodcuttingTreeRangeOutline(ResourceNode node)
    {
        if (node == null || node.ActionType != NodeAction.Woodcutting || node.IsDepleted)
            return false;
        return node.Definition != null && node.Definition.HasMainYield;
    }

    private static bool TryGetResourceNodeColliderBounds(ResourceNode node, out Bounds bounds)
    {
        bounds = default;
        if (node == null)
            return false;

        Collider2D col = node.GetComponent<Collider2D>() ?? node.GetComponentInChildren<Collider2D>();
        if (col == null || !col.enabled)
            return false;

        bounds = col.bounds;
        return true;
    }

    private static bool IsWoodcuttingNodeWithinPivotRange(ResourceNode node, Vector3 origin, float radius)
    {
        if (node == null || radius <= 0f)
            return false;

        float dx = node.transform.position.x - origin.x;
        float dy = node.transform.position.y - origin.y;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static bool IsWoodcuttingNodeWithinColliderEdgeRange(ResourceNode node, Vector3 origin, float radius)
    {
        if (node == null || radius <= 0f)
            return false;

        Vector3 measureFrom = ResolveResourceNodeMeasurePoint(node, origin);
        float dx = measureFrom.x - origin.x;
        float dy = measureFrom.y - origin.y;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static Vector3 ResolveResourceNodeMeasurePoint(ResourceNode node, Vector3 worldPos)
    {
        if (node == null)
            return worldPos;

        Collider2D col = node.GetComponent<Collider2D>() ?? node.GetComponentInChildren<Collider2D>();
        if (col != null && col.enabled)
        {
            Vector2 cp = col.bounds.ClosestPoint(new Vector2(worldPos.x, worldPos.y));
            return new Vector3(cp.x, cp.y, node.transform.position.z);
        }

        return node.transform.position;
    }

    private LineRenderer EnsureWoodcuttingTreeOutline(ResourceNode node)
    {
        if (node == null)
            return null;

        if (_woodcuttingTreeOutlineByNode.TryGetValue(node, out LineRenderer existing) && existing != null)
            return existing;

        var go = new GameObject("WoodcuttingRangeOutline");
        go.transform.SetParent(node.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.alignment = LineAlignment.View;
        lr.startWidth = woodcuttingTreeOutlineLineWidth;
        lr.endWidth = woodcuttingTreeOutlineLineWidth;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 0;
        lr.positionCount = 5;
        if (!string.IsNullOrWhiteSpace(woodcuttingTreeOutlineSortingLayer))
        {
            lr.sortingLayerName = woodcuttingTreeOutlineSortingLayer;
            lr.sortingOrder = woodcuttingTreeOutlineSortingOrder;
        }
        else if (!TryApplyPlayerSpriteSortingToRenderer(lr, woodcuttingTreeOutlineSortingOrder))
        {
            lr.sortingOrder = woodcuttingTreeOutlineSortingOrder;
        }

        Shader spritesDefault = Shader.Find("Sprites/Default");
        if (spritesDefault != null)
            lr.material = new Material(spritesDefault) { color = Color.white };

        _woodcuttingTreeOutlineByNode[node] = lr;
        return lr;
    }

    private static void ApplyWoodcuttingTreeOutlineBounds(LineRenderer outline, Bounds bounds, Color color)
    {
        if (outline == null)
            return;

        float z = bounds.center.z;
        Vector3 bl = new Vector3(bounds.min.x, bounds.min.y, z);
        Vector3 br = new Vector3(bounds.max.x, bounds.min.y, z);
        Vector3 tr = new Vector3(bounds.max.x, bounds.max.y, z);
        Vector3 tl = new Vector3(bounds.min.x, bounds.max.y, z);
        outline.SetPosition(0, bl);
        outline.SetPosition(1, br);
        outline.SetPosition(2, tr);
        outline.SetPosition(3, tl);
        outline.SetPosition(4, bl);
        outline.startColor = color;
        outline.endColor = color;
    }

    private void RemoveWoodcuttingTreeOutline(ResourceNode node)
    {
        if (node == null)
            return;

        if (!_woodcuttingTreeOutlineByNode.TryGetValue(node, out LineRenderer lr))
            return;

        _woodcuttingTreeOutlineByNode.Remove(node);
        if (lr != null && lr.gameObject != null)
            Destroy(lr.gameObject);
    }

    private void EnsureCleavingChopIndicatorBuilt()
    {
        if (_cleavingChopIndicatorRoot != null && _cleavingChopIndicatorLine != null)
            return;

        Transform existing = transform.Find("CleavingChopRangeIndicator");
        if (existing != null)
        {
            _cleavingChopIndicatorRoot = existing.gameObject;
            _cleavingChopIndicatorRoot.transform.SetParent(null, true);
            _cleavingChopIndicatorLine = existing.GetComponent<LineRenderer>();
            if (_cleavingChopIndicatorLine == null)
                _cleavingChopIndicatorLine = existing.gameObject.AddComponent<LineRenderer>();
        }
        else
        {
            _cleavingChopIndicatorRoot = new GameObject("CleavingChopRangeIndicator");
            _cleavingChopIndicatorRoot.transform.SetParent(null, true);
            _cleavingChopIndicatorLine = _cleavingChopIndicatorRoot.AddComponent<LineRenderer>();
        }

        var lr = _cleavingChopIndicatorLine;
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.alignment = LineAlignment.View;
        lr.startWidth = cleavingChopIndicatorLineWidth;
        lr.endWidth = cleavingChopIndicatorLineWidth;
        lr.startColor = cleavingChopIndicatorColor;
        lr.endColor = cleavingChopIndicatorColor;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 0;
        if (!string.IsNullOrWhiteSpace(cleavingChopIndicatorSortingLayer))
        {
            lr.sortingLayerName = cleavingChopIndicatorSortingLayer;
            lr.sortingOrder = cleavingChopIndicatorSortingOrder;
        }
        else if (!TryApplyPlayerSpriteSortingToRenderer(lr, cleavingChopIndicatorSortingOrder))
            lr.sortingOrder = cleavingChopIndicatorSortingOrder;

        Shader spritesDefault = Shader.Find("Sprites/Default");
        if (spritesDefault != null)
            lr.material = new Material(spritesDefault) { color = Color.white };
    }

    private void RebuildCleavingChopIndicatorCircle(float radius)
    {
        if (_cleavingChopIndicatorLine == null)
            return;

        int segs = Mathf.Clamp(cleavingChopIndicatorSegments, 8, 256);
        _cleavingChopIndicatorLine.positionCount = segs;
        if (radius <= 0f)
        {
            for (int i = 0; i < segs; i++)
                _cleavingChopIndicatorLine.SetPosition(i, Vector3.zero);
            return;
        }

        float step = (Mathf.PI * 2f) / segs;
        for (int i = 0; i < segs; i++)
        {
            float a = step * i;
            _cleavingChopIndicatorLine.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }

    public GameObject CreateSpectralAxeProjectile(Vector3 worldOrigin, float facingSign, Sprite axeSprite)
    {
        if (axeSprite == null)
            return null;

        var go = new GameObject("SpectralAxeProjectile");
        go.transform.position = worldOrigin;
        go.transform.localScale = new Vector3(facingSign < 0f ? -1f : 1f, 1f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = axeSprite;
        sr.color = spectralAxeTint;

        if (!TryApplyPlayerSpriteSortingToRenderer(sr, 8))
            sr.sortingOrder = 50;

        AttachSpectralAxeBlueTrail(go, sr, axeSprite);
        return go;
    }

    private void AttachSpectralAxeBlueTrail(GameObject projectile, SpriteRenderer axeSr, Sprite axeSprite)
    {
        if (projectile == null || axeSr == null)
            return;

        Vector2 size = axeSprite != null ? (Vector2)axeSprite.bounds.size : new Vector2(0.6f, 0.6f);
        Vector3 anchor = new Vector3(-size.x * spectralAxeTrailAnchorXFrac, size.y * spectralAxeTrailAnchorYFrac, 0f);

        var trailGo = new GameObject("SpectralAxeBlueTrail");
        trailGo.transform.SetParent(projectile.transform, false);
        trailGo.transform.localPosition = anchor;
        trailGo.transform.localRotation = Quaternion.identity;
        trailGo.transform.localScale = Vector3.one;

        var trail = trailGo.AddComponent<TrailRenderer>();
        trail.time = spectralAxeTrailLifetimeSeconds;
        trail.startWidth = spectralAxeTrailStartWidth;
        trail.endWidth = spectralAxeTrailEndWidth;
        trail.minVertexDistance = 0.02f;
        trail.autodestruct = false;
        trail.emitting = true;
        trail.numCornerVertices = 2;
        trail.numCapVertices = 2;

        Shader spritesDefault = Shader.Find("Sprites/Default");
        if (spritesDefault != null)
            trail.material = new Material(spritesDefault) { color = Color.white };

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(spectralAxeTrailColorStart, 0f),
                new GradientColorKey(spectralAxeTrailColorEnd, 1f)
            },
            new[]
            {
                new GradientAlphaKey(spectralAxeTrailStartAlpha, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;

        if (!TryApplyPlayerSpriteSortingToRenderer(trail, 7))
        {
            trail.sortingLayerID = axeSr.sortingLayerID;
            trail.sortingOrder = axeSr.sortingOrder - 1;
        }
    }

    public void EnsureSpectralAxeAreaIndicatorBuilt(Transform followTransform = null)
    {
        if (!spectralAxeShowAreaIndicator || !AreAbilityRangeIndicatorsEnabled())
        {
            DestroySpectralAxeAreaIndicator();
            return;
        }

        if (_spectralAxeAreaIndicatorRoot != null &&
            _spectralAxeAreaIndicatorLine != null &&
            _spectralAxeAreaIndicatorFollow == followTransform)
        {
            return;
        }

        DestroySpectralAxeAreaIndicator();

        _spectralAxeAreaIndicatorFollow = followTransform;
        _spectralAxeAreaIndicatorRoot = new GameObject("SpectralAxeAreaIndicator");
        if (followTransform != null)
        {
            _spectralAxeAreaIndicatorRoot.transform.SetParent(followTransform, false);
            _spectralAxeAreaIndicatorRoot.transform.localPosition = Vector3.zero;
            _spectralAxeAreaIndicatorRoot.transform.localRotation = Quaternion.identity;
            _spectralAxeAreaIndicatorRoot.transform.localScale = Vector3.one;
        }

        _spectralAxeAreaIndicatorLine = _spectralAxeAreaIndicatorRoot.AddComponent<LineRenderer>();

        var lr = _spectralAxeAreaIndicatorLine;
        lr.useWorldSpace = followTransform == null;
        lr.loop = true;
        lr.alignment = LineAlignment.View;
        lr.startWidth = spectralAxeAreaIndicatorLineWidth;
        lr.endWidth = spectralAxeAreaIndicatorLineWidth;
        lr.startColor = spectralAxeAreaIndicatorColor;
        lr.endColor = spectralAxeAreaIndicatorColor;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 0;
        if (!string.IsNullOrWhiteSpace(spectralAxeAreaIndicatorSortingLayer))
        {
            lr.sortingLayerName = spectralAxeAreaIndicatorSortingLayer;
            lr.sortingOrder = spectralAxeAreaIndicatorSortingOrder;
        }
        else if (!TryApplyPlayerSpriteSortingToRenderer(lr, spectralAxeAreaIndicatorSortingOrder))
            lr.sortingOrder = spectralAxeAreaIndicatorSortingOrder;

        Shader spritesDefault = Shader.Find("Sprites/Default");
        if (spritesDefault != null)
            lr.material = new Material(spritesDefault) { color = Color.white };

        _spectralAxeAreaIndicatorAppliedRadius = float.NaN;
    }

    public void UpdateSpectralAxeAreaIndicator(Vector3 axeCenter, float areaRadiusWorld)
    {
        if (_spectralAxeAreaIndicatorLine == null)
            return;

        if (_spectralAxeAreaIndicatorFollow != null)
        {
            RebuildSpectralAxeAreaIndicatorCircle(areaRadiusWorld);
            return;
        }

        RebuildSpectralAxeAreaIndicatorCircleAtWorldCenter(axeCenter, areaRadiusWorld);
    }

    public void UpdateSpectralAxeAreaIndicator(float areaRadiusWorld)
    {
        if (_spectralAxeAreaIndicatorLine == null)
            return;

        RebuildSpectralAxeAreaIndicatorCircle(areaRadiusWorld);
    }

    private void RebuildSpectralAxeAreaIndicatorCircle(float radius)
    {
        if (_spectralAxeAreaIndicatorLine == null)
            return;

        int segs = Mathf.Clamp(spectralAxeAreaIndicatorSegments, 8, 256);
        if (_spectralAxeAreaIndicatorLine.positionCount != segs)
            _spectralAxeAreaIndicatorLine.positionCount = segs;

        float clampedRadius = Mathf.Max(0.01f, radius);
        if (Mathf.Approximately(_spectralAxeAreaIndicatorAppliedRadius, clampedRadius))
            return;

        float step = (Mathf.PI * 2f) / segs;
        for (int i = 0; i < segs; i++)
        {
            float a = step * i;
            _spectralAxeAreaIndicatorLine.SetPosition(i, new Vector3(
                Mathf.Cos(a) * clampedRadius,
                Mathf.Sin(a) * clampedRadius,
                0f));
        }

        _spectralAxeAreaIndicatorAppliedRadius = clampedRadius;
    }

    private void RebuildSpectralAxeAreaIndicatorCircleAtWorldCenter(Vector3 axeCenter, float areaRadiusWorld)
    {
        if (_spectralAxeAreaIndicatorLine == null)
            return;

        int segs = Mathf.Clamp(spectralAxeAreaIndicatorSegments, 8, 256);
        if (_spectralAxeAreaIndicatorLine.positionCount != segs)
            _spectralAxeAreaIndicatorLine.positionCount = segs;

        float radius = Mathf.Max(0.01f, areaRadiusWorld);
        float step = (Mathf.PI * 2f) / segs;
        for (int i = 0; i < segs; i++)
        {
            float a = step * i;
            _spectralAxeAreaIndicatorLine.SetPosition(i, new Vector3(
                axeCenter.x + Mathf.Cos(a) * radius,
                axeCenter.y + Mathf.Sin(a) * radius,
                axeCenter.z));
        }

        _spectralAxeAreaIndicatorAppliedRadius = radius;
    }

    public void DestroySpectralAxeAreaIndicator()
    {
        _spectralAxeAreaIndicatorFollow = null;
        _spectralAxeAreaIndicatorAppliedRadius = float.NaN;
        if (_spectralAxeAreaIndicatorRoot != null)
        {
            Destroy(_spectralAxeAreaIndicatorRoot);
            _spectralAxeAreaIndicatorRoot = null;
            _spectralAxeAreaIndicatorLine = null;
        }
    }

    public void SpawnAvatarOfTheForestGlowVfx()
    {
        DestroyAvatarOfTheForestGlowVfx();
        Transform parent = player != null ? player.transform : transform;
        if (parent == null)
            return;

        _avatarOfForestGlowRoot = new GameObject("AvatarOfTheForestGlow");
        _avatarOfForestGlowRoot.transform.SetParent(parent, false);
        _avatarOfForestGlowRoot.transform.localPosition = avatarOfForestGlowLocalOffset;
        _avatarOfForestGlowRoot.transform.localRotation = Quaternion.identity;
        _avatarOfForestGlowRoot.transform.localScale = Vector3.one;

        GameObject emitterGO = new GameObject("ForestRadianceEmitter");
        emitterGO.transform.SetParent(_avatarOfForestGlowRoot.transform, false);
        emitterGO.transform.localPosition = Vector3.zero;

        ParticleSystem ps = emitterGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.72f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        float sizeMin = Mathf.Min(avatarOfForestParticleStartSizeMin, avatarOfForestParticleStartSizeMax);
        float sizeMax = Mathf.Max(avatarOfForestParticleStartSizeMin, avatarOfForestParticleStartSizeMax);
        sizeMin = Mathf.Max(0.001f, sizeMin);
        sizeMax = Mathf.Max(sizeMin, sizeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = avatarOfForestGlowColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 420;
        main.simulationSpeed = 1f;

        var emission = ps.emission;
        emission.rateOverTime = avatarOfForestGlowEmissionRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = avatarOfForestGlowConeAngle;
        shape.radius = Mathf.Max(0.02f, avatarOfForestGlowSphereRadius);
        shape.arc = 360f;
        shape.randomDirectionAmount = 0.06f;
        shape.alignToDirection = false;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        // Unity requires every velocity axis use the same MinMaxCurve mode (here: TwoCurves).
        AnimationCurve rise = new AnimationCurve(
            new Keyframe(0f, 0.22f, 0f, 0.45f),
            new Keyframe(0.2f, 0.48f, 1.15f, 1.45f),
            new Keyframe(0.48f, 1.05f, 1.7f, 1.95f),
            new Keyframe(1f, 2.85f, 2.15f, 0f));
        vel.y = new ParticleSystem.MinMaxCurve(1f, ScaleAnimationCurveValues(rise, 0.9f), ScaleAnimationCurveValues(rise, 1.1f));

        AnimationCurve outPos = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.12f, 0.06f, 0.55f, 0.55f),
            new Keyframe(1f, 0.62f, 0.95f, 0f));
        AnimationCurve outNeg = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.12f, -0.06f, -0.55f, -0.55f),
            new Keyframe(1f, -0.62f, -0.95f, 0f));
        vel.x = new ParticleSystem.MinMaxCurve(1f, outNeg, outPos);

        AnimationCurve outZPos = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 0.22f, 0.45f, 0f));
        AnimationCurve outZNeg = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, -0.22f, -0.45f, 0f));
        vel.z = new ParticleSystem.MinMaxCurve(1f, outZNeg, outZPos);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve shrink = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.25f, 0.78f, -0.35f, -0.35f),
            new Keyframe(1f, 0.12f, -0.4f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, shrink);

        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        float tLife = Mathf.Max(0.02f, avatarOfForestTrailLifetime);
        trails.lifetime = new ParticleSystem.MinMaxCurve(tLife * 0.88f, tLife * 1.12f);
        trails.minVertexDistance = 0.008f;
        trails.worldSpace = false;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = false;
        trails.sizeAffectsLifetime = false;
        trails.inheritParticleColor = true;
        AnimationCurve trailW = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.35f, 0.55f, -0.8f, -0.8f),
            new Keyframe(1f, 0.04f, -0.2f, 0f));
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(Mathf.Max(0.004f, avatarOfForestTrailWidth), trailW);

        Gradient trailTailFade = new Gradient();
        trailTailFade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.35f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(trailTailFade);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Color c = avatarOfForestGlowColor;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(c, Color.white, 0.22f), 0f),
                new GradientColorKey(c, 0.35f),
                new GradientColorKey(Color.Lerp(c, new Color(0.2f, 0.75f, 0.35f), 0.5f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(c.a), 0f),
                new GradientAlphaKey(Mathf.Clamp01(c.a * 0.72f), 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (!TryApplyPlayerSpriteSortingToRenderer(renderer, 12))
            renderer.sortingOrder = 24;
        ApplyRuntimeParticleMaterialIfNeeded(renderer);

        ps.Play(true);
    }

    /// <summary>
    /// Runtime-spawned particle renderers need a URP-safe material. By default we use <c>Sprites/Default</c> so
    /// <see cref="ParticleSystem"/> vertex colors show (same idea as the first green VFX). Optional template /
    /// Resources URP mat / <see cref="Shader.Find"/> are fallbacks.
    /// </summary>
    private void ApplyRuntimeParticleMaterialIfNeeded(ParticleSystemRenderer renderer)
    {
        if (renderer == null)
            return;

        Material mat = TryCreateRuntimeUrParticleMaterial();
        if (mat == null)
            return;

        renderer.material = mat;
        renderer.trailMaterial = mat;
    }

    private Material TryCreateRuntimeUrParticleMaterial()
    {
        if (runtimeParticleMaterialTemplate != null && runtimeParticleMaterialTemplate.shader != null)
            return new Material(runtimeParticleMaterialTemplate);

        // Tinted billboards: Sprites/Default respects ParticleSystem vertex colors (the original green look in URP 2D).
        // URP Particles/Unlit can end up fully invisible with some trail + sheet setups; keep it as fallback below.
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            var spriteMat = new Material(spriteShader) { color = Color.white };
            spriteMat.mainTexture = Texture2D.whiteTexture;
            return spriteMat;
        }

        Material fromResources = Resources.Load<Material>(ResourcesUrParticleMaterialPath);
        if (fromResources != null && fromResources.shader != null)
            return new Material(fromResources);

        Shader shader = TryResolveUrParticleShader();
        if (shader == null)
        {
            if (!s_LoggedMissingUrParticleMaterial)
            {
                s_LoggedMissingUrParticleMaterial = true;
                Debug.LogWarning(
                    "PlayerAbilityVfxController: No particle material/shader available (Sprites/Default missing and no URP fallback). Assign Runtime Particle Material Template or keep Assets/Resources/Vfx/AbilityVfx_ParticlesUnlit.mat.");
            }

            return null;
        }

        return new Material(shader);
    }

    private static Shader TryResolveUrParticleShader()
    {
        try
        {
            foreach (ShaderPathID id in new[]
                     {
                         ShaderPathID.ParticlesUnlit,
                         ShaderPathID.ParticlesSimpleLit,
                         ShaderPathID.ParticlesLit
                     })
            {
                string path = ShaderUtils.GetShaderPath(id);
                if (string.IsNullOrEmpty(path))
                    continue;
                Shader s = Shader.Find(path);
                if (s != null)
                    return s;
            }
        }
        catch (Exception)
        {
            // ShaderUtils / enum mismatch on unexpected URP versions — fall through to string paths.
        }

        foreach (string path in new[]
                 {
                     "Universal Render Pipeline/Particles/Unlit",
                     "Universal Render Pipeline/Particles/Simple Lit",
                     "Universal Render Pipeline/Particles/Lit",
                     "Universal Render Pipeline/Unlit",
                     "Universal Render Pipeline/Lit",
                     "Universal Render Pipeline/2D/Sprite-Unlit-Default",
                     "Sprites/Default",
                     "Hidden/Internal-Colored"
                 })
        {
            Shader s = Shader.Find(path);
            if (s != null)
                return s;
        }

        return null;
    }

    /// <summary>Vertical scale of keyframe values/tangents (keeps curve shape for MinMaxCurve TwoCurves).</summary>
    private static AnimationCurve ScaleAnimationCurveValues(AnimationCurve source, float valueMultiplier)
    {
        if (source == null || source.length == 0)
            return new AnimationCurve();

        Keyframe[] keys = source.keys;
        var scaled = new AnimationCurve();
        for (int i = 0; i < keys.Length; i++)
        {
            Keyframe k = keys[i];
            k.value *= valueMultiplier;
            k.inTangent *= valueMultiplier;
            k.outTangent *= valueMultiplier;
            scaled.AddKey(k);
        }

        return scaled;
    }

    public void UpdateAvatarOfTheForestGlowVfx(bool buffActive)
    {
        if (!buffActive || _avatarOfForestGlowRoot == null)
            return;
        _avatarOfForestGlowRoot.transform.localPosition = avatarOfForestGlowLocalOffset;
    }

    public void DestroyAvatarOfTheForestGlowVfx()
    {
        if (_avatarOfForestGlowRoot != null)
        {
            Destroy(_avatarOfForestGlowRoot);
            _avatarOfForestGlowRoot = null;
        }
    }

    public void SpawnEnergyInfusionGlowVfx()
    {
        DestroyEnergyInfusionGlowVfx();
        Transform parent = player != null ? player.transform : transform;
        if (parent == null)
            return;

        _energyInfusionGlowRoot = new GameObject("EnergyInfusionGlow");
        _energyInfusionGlowRoot.transform.SetParent(parent, false);
        _energyInfusionGlowRoot.transform.localPosition = energyInfusionGlowLocalOffset;
        _energyInfusionGlowRoot.transform.localRotation = Quaternion.identity;
        _energyInfusionGlowRoot.transform.localScale = Vector3.one;

        GameObject emitterGO = new GameObject("ArcaneBatteryEmitter");
        emitterGO.transform.SetParent(_energyInfusionGlowRoot.transform, false);
        emitterGO.transform.localPosition = Vector3.zero;

        ParticleSystem ps = emitterGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.82f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        float sizeMin = Mathf.Max(0.001f, Mathf.Min(energyInfusionParticleStartSizeMin, energyInfusionParticleStartSizeMax));
        float sizeMax = Mathf.Max(sizeMin, Mathf.Max(energyInfusionParticleStartSizeMin, energyInfusionParticleStartSizeMax));
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = energyInfusionGlowColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 360;
        main.simulationSpeed = 1f;

        var emission = ps.emission;
        emission.rateOverTime = energyInfusionGlowEmissionRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.08f, energyInfusionGlowSphereRadius * 0.55f);
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 0.35f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        AnimationCurve pulseOut = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.35f, 0.18f, 0.6f, 0.6f),
            new Keyframe(1f, 0.08f, -0.2f, 0f));
        AnimationCurve pulseIn = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.35f, -0.18f, -0.6f, -0.6f),
            new Keyframe(1f, -0.08f, 0.2f, 0f));
        vel.x = new ParticleSystem.MinMaxCurve(1f, pulseIn, pulseOut);
        vel.y = new ParticleSystem.MinMaxCurve(1f, pulseIn, pulseOut);
        vel.z = new ParticleSystem.MinMaxCurve(1f, pulseIn, pulseOut);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve breathe = new AnimationCurve(
            new Keyframe(0f, 0.85f, 0f, 0f),
            new Keyframe(0.5f, 1.05f, 0f, 0f),
            new Keyframe(1f, 0.75f, 0f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, breathe);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Color c = energyInfusionGlowColor;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(c, Color.white, 0.35f), 0f),
                new GradientColorKey(c, 0.4f),
                new GradientColorKey(Color.Lerp(c, new Color(0.12f, 0.28f, 0.95f), 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(c.a), 0f),
                new GradientAlphaKey(Mathf.Clamp01(c.a * 0.65f), 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (!TryApplyPlayerSpriteSortingToRenderer(renderer, 12))
            renderer.sortingOrder = 24;
        ApplyRuntimeParticleMaterialIfNeeded(renderer);

        ps.Play(true);
    }

    public void UpdateEnergyInfusionGlowVfx(bool buffActive)
    {
        if (!buffActive || _energyInfusionGlowRoot == null)
            return;
        _energyInfusionGlowRoot.transform.localPosition = energyInfusionGlowLocalOffset;
    }

    public void DestroyEnergyInfusionGlowVfx()
    {
        if (_energyInfusionGlowRoot != null)
        {
            Destroy(_energyInfusionGlowRoot);
            _energyInfusionGlowRoot = null;
        }
    }

    public void SpawnBattleTranceGlowVfx()
    {
        Transform parent = player != null ? player.transform : transform;
        if (parent == null)
            return;

        if (_battleTranceGlowRoot == null || _battleTranceGlowParticles == null)
            CreateBattleTranceGlowVfx(parent);

        if (_battleTranceGlowRoot == null || _battleTranceGlowParticles == null)
            return;

        _battleTranceGlowRoot.transform.SetParent(parent, false);
        _battleTranceGlowRoot.transform.localPosition = battleTranceGlowLocalOffset;
        _battleTranceGlowRoot.transform.localRotation = Quaternion.identity;
        _battleTranceGlowRoot.transform.localScale = Vector3.one;
        _battleTranceGlowRoot.SetActive(true);
        _battleTranceGlowParticles.Play(true);
    }

    public void UpdateBattleTranceGlowVfx(bool buffActive)
    {
        if (!buffActive || _battleTranceGlowRoot == null)
            return;
        _battleTranceGlowRoot.transform.localPosition = battleTranceGlowLocalOffset;
    }

    public void DestroyBattleTranceGlowVfx()
    {
        if (_battleTranceGlowRoot != null)
        {
            if (_battleTranceGlowParticles != null)
                _battleTranceGlowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _battleTranceGlowRoot.SetActive(false);
        }
    }

    private void CreateBattleTranceGlowVfx(Transform parent)
    {
        _battleTranceGlowRoot = new GameObject("BattleTranceGlow");
        _battleTranceGlowRoot.transform.SetParent(parent, false);
        _battleTranceGlowRoot.transform.localPosition = battleTranceGlowLocalOffset;
        _battleTranceGlowRoot.transform.localRotation = Quaternion.identity;
        _battleTranceGlowRoot.transform.localScale = Vector3.one;

        GameObject emitterGO = new GameObject("BattleTranceEmitter");
        emitterGO.transform.SetParent(_battleTranceGlowRoot.transform, false);
        emitterGO.transform.localPosition = Vector3.zero;

        ParticleSystem ps = emitterGO.AddComponent<ParticleSystem>();
        _battleTranceGlowParticles = ps;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.82f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        float sizeMin = Mathf.Max(0.001f, Mathf.Min(battleTranceParticleStartSizeMin, battleTranceParticleStartSizeMax));
        float sizeMax = Mathf.Max(sizeMin, Mathf.Max(battleTranceParticleStartSizeMin, battleTranceParticleStartSizeMax));
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = battleTranceGlowColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 360;
        main.simulationSpeed = 1f;

        var emission = ps.emission;
        emission.rateOverTime = battleTranceGlowEmissionRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.08f, battleTranceGlowSphereRadius * 0.55f);
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = 0.35f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        AnimationCurve pulseOut = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.35f, 0.18f, 0.6f, 0.6f),
            new Keyframe(1f, 0.08f, -0.2f, 0f));
        AnimationCurve pulseIn = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.35f, -0.18f, -0.6f, -0.6f),
            new Keyframe(1f, -0.08f, 0.2f, 0f));
        vel.x = new ParticleSystem.MinMaxCurve(1f, pulseIn, pulseOut);
        vel.y = new ParticleSystem.MinMaxCurve(1f, pulseIn, pulseOut);
        vel.z = new ParticleSystem.MinMaxCurve(1f, pulseIn, pulseOut);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve breathe = new AnimationCurve(
            new Keyframe(0f, 0.85f, 0f, 0f),
            new Keyframe(0.5f, 1.05f, 0f, 0f),
            new Keyframe(1f, 0.75f, 0f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, breathe);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Color c = battleTranceGlowColor;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(c, Color.white, 0.35f), 0f),
                new GradientColorKey(c, 0.4f),
                new GradientColorKey(Color.Lerp(c, new Color(0.55f, 0.05f, 0.05f), 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(c.a), 0f),
                new GradientAlphaKey(Mathf.Clamp01(c.a * 0.65f), 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (!TryApplyPlayerSpriteSortingToRenderer(renderer, 12))
            renderer.sortingOrder = 24;
        ApplyRuntimeParticleMaterialIfNeeded(renderer);
    }

    public void SpawnHammerTempestOrbitVfx(float durationSeconds, int hammerCount)
    {
        DestroyHammerTempestOrbitVfx();

        Transform followRoot = player != null ? player.transform : transform;
        if (followRoot == null)
            return;

        Sprite sprite = hammerTempestHammerSprite != null ? hammerTempestHammerSprite : guardiansHammerSprite;
        if (sprite == null)
            return;

        hammerCount = Mathf.Clamp(hammerCount, 1, 12);
        _hammerTempestOrbitRoot = new GameObject("HammerTempestOrbit");
        _hammerTempestOrbitRoot.transform.SetParent(followRoot, false);
        _hammerTempestOrbitRoot.transform.localPosition = Vector3.zero;
        _hammerTempestOrbitRoot.transform.localRotation = Quaternion.identity;
        _hammerTempestOrbitRoot.transform.localScale = Vector3.one;

        CreateHammerTempestRingVisual();

        _hammerTempestHammerRenderers.Clear();
        for (int i = 0; i < hammerCount; i++)
        {
            GameObject hammerGo = new GameObject($"HammerTempestHammer_{i}");
            hammerGo.transform.SetParent(_hammerTempestOrbitRoot.transform, false);
            SpriteRenderer sr = hammerGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = hammerTempestHammerTint;
            if (!TryApplyPlayerSpriteSortingToRenderer(sr, 12 + i))
                sr.sortingOrder = 22 + i;
            _hammerTempestHammerRenderers.Add(sr);
        }

        float duration = Mathf.Max(0.05f, durationSeconds);
        _hammerTempestOrbitRoutine = StartCoroutine(CoHammerTempestOrbit(duration, hammerCount));
    }

    public void DestroyHammerTempestOrbitVfx()
    {
        if (_hammerTempestOrbitRoutine != null)
        {
            StopCoroutine(_hammerTempestOrbitRoutine);
            _hammerTempestOrbitRoutine = null;
        }

        _hammerTempestHammerRenderers.Clear();
        _hammerTempestRingObject = null;
        _hammerTempestRingRenderer = null;
        if (_hammerTempestOrbitRoot != null)
        {
            Destroy(_hammerTempestOrbitRoot);
            _hammerTempestOrbitRoot = null;
        }
    }

    private IEnumerator CoHammerTempestOrbit(float durationSeconds, int hammerCount)
    {
        if (_hammerTempestOrbitRoot == null || hammerCount <= 0)
            yield break;

        Transform center = player != null ? player.transform : transform;
        float elapsed = 0f;
        float orbitSpeedRad = hammerTempestMainOrbitDegreesPerSecond * Mathf.Deg2Rad;
        float facingSign = GetCombatFacingSign();

        while (elapsed < durationSeconds && center != null && _hammerTempestOrbitRoot != null)
        {
            elapsed += Time.deltaTime;
            float orbitAngle = elapsed * orbitSpeedRad;
            Vector3 pivot = center.position + hammerTempestCenterOffset;
            UpdateHammerTempestRingVisual(pivot, facingSign);

            for (int i = 0; i < _hammerTempestHammerRenderers.Count; i++)
            {
                SpriteRenderer sr = _hammerTempestHammerRenderers[i];
                if (sr == null)
                    continue;

                float phase = i / (float)Mathf.Max(1, hammerCount);
                float angle = orbitAngle + phase * Mathf.PI * 2f;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * hammerTempestOrbitRadius * Mathf.Sign(facingSign == 0f ? 1f : facingSign),
                    Mathf.Sin(angle) * hammerTempestOrbitRadius,
                    0f);
                Transform t = sr.transform;
                t.position = pivot + offset;
                float spinDeg = (angle * Mathf.Rad2Deg) + (elapsed * hammerTempestHammerSpinDegreesPerSecond);
                t.rotation = Quaternion.Euler(0f, 0f, spinDeg - 90f);
                t.localScale = new Vector3(
                    hammerTempestWorldScale * Mathf.Sign(facingSign == 0f ? 1f : facingSign),
                    hammerTempestWorldScale,
                    1f);
            }

            yield return null;
        }

        DestroyHammerTempestOrbitVfx();
    }

    public void BeginFlameChargePlayerGlow()
    {
        EndFlameChargePlayerGlow();
        Transform parent = player != null ? player.transform : transform;
        if (parent == null)
            return;

        _flameChargePlayerGlowRoot = new GameObject("FlameChargePlayerGlow");
        _flameChargePlayerGlowRoot.transform.SetParent(parent, false);
        _flameChargePlayerGlowRoot.transform.localPosition = flameChargePlayerGlowLocalOffset;

        GameObject emitterGO = new GameObject("FlameChargeBodyEmitter");
        emitterGO.transform.SetParent(_flameChargePlayerGlowRoot.transform, false);
        ParticleSystem ps = emitterGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startColor = flameChargePlayerGlowColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 280;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = flameChargePlayerGlowEmissionRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = flameChargePlayerGlowRadius;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (!TryApplyPlayerSpriteSortingToRenderer(renderer, 18))
            renderer.sortingOrder = 32;
        ApplyRuntimeParticleMaterialIfNeeded(renderer);
        ps.Clear(true);
        ps.Play(true);
    }

    public void SpawnFlameChargeDashTrailVisual(Vector3 start, Vector3 end, float durationSeconds)
    {
        StartCoroutine(CoFlameChargeDashTrailVisual(start, end, durationSeconds));
    }

    private void DestroyAllFlameChargeDashTrailVfx()
    {
        for (int i = _activeFlameChargeDashTrailRoots.Count - 1; i >= 0; i--)
        {
            GameObject root = _activeFlameChargeDashTrailRoots[i];
            if (root)
                Destroy(root);
        }

        _activeFlameChargeDashTrailRoots.Clear();
    }

    private IEnumerator CoFlameChargeDashTrailVisual(Vector3 start, Vector3 end, float durationSeconds)
    {
        float length = Mathf.Max(0.5f, Vector3.Distance(start, end));
        Vector3 mid = (start + end) * 0.5f;
        float emitSeconds = Mathf.Max(0.1f, durationSeconds);
        const float maxParticleLifetime = 0.5f;
        const float particleFadeBuffer = 0.12f;

        GameObject root = null;
        ParticleSystem ps = null;

        try
        {
            root = new GameObject("FlameChargeDashTrail");
            PlaceFlameChargeGroundEffect(root.transform, mid);
            _activeFlameChargeDashTrailRoots.Add(root);

            GameObject emitterGO = new GameObject("DashTrailEmitter");
            emitterGO.transform.SetParent(root.transform, false);
            emitterGO.transform.localPosition = Vector3.zero;

            ps = emitterGO.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, maxParticleLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = flameChargeGroundFireColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = 320;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = flameChargeGroundFireEmissionRate * 1.35f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(length, flameChargeGroundTrailHeight, flameChargeGroundFireRadius * 0.11f);

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ApplyFlameChargeGroundSorting(renderer);
            ApplyRuntimeParticleMaterialIfNeeded(renderer);

            ps.Clear(true);
            ps.Play(true);

            yield return new WaitForSeconds(emitSeconds);

            if (ps)
            {
                var emissionModule = ps.emission;
                emissionModule.enabled = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            yield return new WaitForSeconds(maxParticleLifetime + particleFadeBuffer);
        }
        finally
        {
            if (root)
            {
                _activeFlameChargeDashTrailRoots.Remove(root);
                Destroy(root);
            }
        }
    }

    public void EndFlameChargePlayerGlow()
    {
        if (_flameChargePlayerGlowRoot != null)
        {
            Destroy(_flameChargePlayerGlowRoot);
            _flameChargePlayerGlowRoot = null;
        }
    }

    /// <summary>Phoenix Soul — Ashen Rebirth: sprite above the player (Executioner's Descent–style spawn hold).</summary>
    public void SpawnAshenRebirthPhoenixVfx()
    {
        StopAshenRebirthPhoenixVfx();

        if (ashenRebirthPhoenixSprite == null)
            return;

        EnsureAshenRebirthPhoenixVisual();
        if (_ashenRebirthPhoenixRenderer == null || _ashenRebirthPhoenixRoot == null)
            return;

        _ashenRebirthPhoenixRenderer.sprite = ashenRebirthPhoenixSprite;
        _ashenRebirthPhoenixRenderer.color = ashenRebirthPhoenixTint;
        _ashenRebirthPhoenixRenderer.enabled = true;
        _ashenRebirthPhoenixRoot.transform.localScale = Vector3.one * ashenRebirthPhoenixWorldScale;
        _ashenRebirthPhoenixRoot.transform.position = ResolveAshenRebirthPhoenixWorldPosition();

        float holdSeconds = Mathf.Max(0f, ashenRebirthPhoenixSpawnHoldSeconds);
        _ashenRebirthPhoenixRoutine = StartCoroutine(CoAshenRebirthPhoenixHold(holdSeconds));
    }

    public void StopAshenRebirthPhoenixVfx()
    {
        if (_ashenRebirthPhoenixRoutine != null)
        {
            StopCoroutine(_ashenRebirthPhoenixRoutine);
            _ashenRebirthPhoenixRoutine = null;
        }

        if (_ashenRebirthPhoenixRenderer != null)
            _ashenRebirthPhoenixRenderer.enabled = false;
    }

    private Vector3 ResolveAshenRebirthPhoenixWorldPosition()
    {
        Transform root = player != null ? player.transform : transform;
        return root.position + Vector3.up * ashenRebirthPhoenixSpawnHeightAbovePlayer;
    }

    private IEnumerator CoAshenRebirthPhoenixHold(float holdSeconds)
    {
        float elapsed = 0f;
        while (elapsed < holdSeconds)
        {
            if (_ashenRebirthPhoenixRoot != null)
                _ashenRebirthPhoenixRoot.transform.position = ResolveAshenRebirthPhoenixWorldPosition();

            elapsed += Time.deltaTime;
            yield return null;
        }

        _ashenRebirthPhoenixRoutine = null;
        if (_ashenRebirthPhoenixRenderer != null)
            _ashenRebirthPhoenixRenderer.enabled = false;
    }

    private void ApplyAshenRebirthPhoenixSorting(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (!string.IsNullOrWhiteSpace(ashenRebirthPhoenixSortingLayer))
            renderer.sortingLayerName = ashenRebirthPhoenixSortingLayer;

        renderer.sortingOrder = ashenRebirthPhoenixSortingOrder;
    }

    private void EnsureAshenRebirthPhoenixVisual()
    {
        if (_ashenRebirthPhoenixRoot != null)
            return;

        _ashenRebirthPhoenixRoot = new GameObject("AshenRebirthPhoenixVfx");
        _ashenRebirthPhoenixRenderer = _ashenRebirthPhoenixRoot.AddComponent<SpriteRenderer>();
        ApplyAshenRebirthPhoenixSorting(_ashenRebirthPhoenixRenderer);
    }

    public void SpawnFlameChargeVolcanicBurst(Vector3 worldPosition)
    {
        if (_flameChargeVolcanicBurstRoutine != null)
            StopCoroutine(_flameChargeVolcanicBurstRoutine);
        _flameChargeVolcanicBurstRoutine = StartCoroutine(CoFlameChargeVolcanicBurst(worldPosition));
    }

    private IEnumerator CoFlameChargeVolcanicBurst(Vector3 worldPosition)
    {
        GameObject burstRoot = new GameObject("FlameChargeVolcanicBurst");
        PlaceFlameChargeGroundEffect(burstRoot.transform, worldPosition);

        GameObject ring = new GameObject("VolcanicRing");
        ring.transform.SetParent(burstRoot.transform, false);
        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 48;
        line.startWidth = 0.22f;
        line.endWidth = 0.04f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = flameChargeVolcanicBurstColor;
        line.endColor = new Color(flameChargeVolcanicBurstColor.r, flameChargeVolcanicBurstColor.g, flameChargeVolcanicBurstColor.b, 0f);
        ApplyFlameChargeGroundSorting(line);

        float duration = Mathf.Max(0.05f, flameChargeVolcanicBurstDuration);
        float maxR = Mathf.Max(0.5f, flameChargeVolcanicBurstMaxRadius);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float r = maxR * u;
            for (int i = 0; i < line.positionCount; i++)
            {
                float ang = i / (float)line.positionCount * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * 0.35f, 0f));
            }

            yield return null;
        }

        Destroy(burstRoot);
        _flameChargeVolcanicBurstRoutine = null;
    }

    private void PlaceFlameChargeGroundEffect(Transform root, Vector3 worldPoint)
    {
        LaneGroundEffectPlacement.PlaceOnLaneFloor(root, worldPoint, flameChargeGroundFloorYOffset);
    }

    private void ApplyFlameChargeGroundSorting(Renderer renderer)
    {
        if (!renderer)
            return;

        if (!string.IsNullOrWhiteSpace(flameChargeGroundSortingLayer))
            renderer.sortingLayerName = flameChargeGroundSortingLayer;
        renderer.sortingOrder = flameChargeGroundSortingOrder;
    }

    public void BeginExecutionersDescent(EnemyBaseController target, Vector3 targetWorld, float descentSeconds)
    {
        StopExecutionersDescentVfx();
        _executionersDescentTotalSeconds = Mathf.Max(0.1f, descentSeconds);

        EnsureExecutionersDescentVisuals();
        if (_executionersDescentAxeRenderer != null)
        {
            _executionersDescentAxeRenderer.sprite = executionersDescentAxeSprite;
            _executionersDescentAxeRenderer.color = executionersDescentAxeTint;
            _executionersDescentAxeRenderer.enabled = executionersDescentAxeSprite != null;
            _executionersDescentAxeRoot.transform.localScale = Vector3.one * executionersDescentAxeWorldScale;
        }

        EvaluateExecutionersDescentAxePose(targetWorld, 0f, out Vector3 axePos, out Vector3 hangPoint);
        if (_executionersDescentAxeRoot != null)
            _executionersDescentAxeRoot.transform.position = axePos;

        SyncExecutionersDescentMark(target, hangPoint);
    }

    /// <summary>
    /// Executioner's Continuum — axe at the same lowest descent impact point (caller passes
    /// <see cref="ResolveExecutionersDescentMinimumImpactWorldPosition"/> once). Fixed world position.
    /// </summary>
    public void BeginExecutionersDescentContinuum(Vector3 impactWorldPosition)
    {
        StopExecutionersDescentVfx();

        EnsureExecutionersDescentVisuals();
        if (_executionersDescentAxeRenderer != null)
        {
            _executionersDescentAxeRenderer.sprite = executionersDescentAxeSprite;
            _executionersDescentAxeRenderer.color = executionersDescentAxeTint;
            _executionersDescentAxeRenderer.enabled = executionersDescentAxeSprite != null;
            _executionersDescentAxeRoot.transform.localScale = Vector3.one * executionersDescentAxeWorldScale;
        }

        if (_executionersDescentAxeRoot != null)
            _executionersDescentAxeRoot.transform.position = impactWorldPosition;

        HideExecutionersDescentMark();
    }

    public void UpdateExecutionersDescent(EnemyBaseController target, Vector3 targetWorld, float elapsedSeconds)
    {
        if (_executionersDescentAxeRoot == null)
            return;

        EvaluateExecutionersDescentAxePose(targetWorld, elapsedSeconds, out Vector3 axePos, out Vector3 hangPoint);
        _executionersDescentAxeRoot.transform.position = axePos;
        SyncExecutionersDescentMark(target, hangPoint);
    }

    public bool TryGetExecutionersDescentAxeWorldPosition(out Vector3 worldPosition)
    {
        if (_executionersDescentAxeRoot == null)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = _executionersDescentAxeRoot.transform.position;
        return true;
    }

    public void SetExecutionersDescentAxeWorldPosition(Vector3 worldPosition)
    {
        if (_executionersDescentAxeRoot != null)
            _executionersDescentAxeRoot.transform.position = worldPosition;
    }

    public Vector3 ResolveExecutionersDescentMinimumImpactWorldPosition(Vector3 targetWorld) =>
        ResolveExecutionersDescentMinimumWorldPosition(targetWorld);

    public void HideExecutionersDescentMark()
    {
        if (_executionersDescentMarkRoot == null)
            return;

        _executionersDescentMarkRoot.transform.SetParent(null, true);
        if (_executionersDescentMarkRenderer != null)
            _executionersDescentMarkRenderer.enabled = false;
    }

    public void SpawnExecutionersDescentImpactShockwave(Vector3 targetWorld) =>
        SpawnEnemyAbilityImpactShockwave(targetWorld, AbilityCombatPower.ExecutionersDescentShockwaveRadius);

    public GameObject SpawnEnemyPounceTelegraph(
        Vector3 worldPosition,
        float shockwaveRadius,
        float directHitRadius)
    {
        shockwaveRadius = Mathf.Max(0.1f, shockwaveRadius);
        directHitRadius = Mathf.Clamp(Mathf.Max(0.1f, directHitRadius), 0.1f, shockwaveRadius);
        Vector3 groundCenter = new Vector3(
            worldPosition.x,
            worldPosition.y + executionersDescentShockwaveGroundOffset,
            worldPosition.z);

        var root = new GameObject("EnemyPounceTelegraph");
        root.transform.position = groundCenter;

        const float groundEllipseYSquash = 0.07f;
        Color outerFillColor = new Color(1f, 0.18f, 0.1f, 0.3f);
        Color outerRingColor = new Color(1f, 0.22f, 0.12f, 0.5f);
        Color innerFillColor = new Color(1f, 0.1f, 0.06f, 0.92f);
        Color innerRingColor = new Color(1f, 0.14f, 0.08f, 0.95f);

        Sprite areaSprite = executionersDescentShockwaveSprite != null
            ? executionersDescentShockwaveSprite
            : executionersDescentMarkSprite;
        if (areaSprite != null)
        {
            var outerFill = new GameObject("OuterRadiusFill");
            outerFill.transform.SetParent(root.transform, false);
            var outerSr = outerFill.AddComponent<SpriteRenderer>();
            outerSr.sprite = areaSprite;
            outerSr.color = outerFillColor;
            ApplyExecutionersDescentSorting(outerSr);
            outerSr.sortingOrder = executionersDescentSortingOrder - 2;
            float outerScale = shockwaveRadius * 0.72f;
            outerFill.transform.localScale = new Vector3(outerScale, outerScale * groundEllipseYSquash, 1f);
        }

        CreateEnemyPounceTelegraphRing(
            root.transform,
            shockwaveRadius,
            outerRingColor,
            executionersDescentShockwaveLineWidth,
            groundEllipseYSquash,
            "OuterRadiusRing");

        float innerLandingRadius = directHitRadius;
        if (executionersDescentMarkSprite != null)
        {
            var innerFill = new GameObject("InnerLandingFill");
            innerFill.transform.SetParent(root.transform, false);
            var innerSr = innerFill.AddComponent<SpriteRenderer>();
            innerSr.sprite = executionersDescentMarkSprite;
            innerSr.color = innerFillColor;
            ApplyExecutionersDescentSorting(innerSr);
            innerSr.sortingOrder = executionersDescentSortingOrder - 1;
            float innerScale = Mathf.Max(0.2f, executionersDescentMarkWorldScale * 0.82f);
            innerFill.transform.localScale = new Vector3(innerScale, innerScale * 0.55f, 1f);
        }

        CreateEnemyPounceTelegraphRing(
            root.transform,
            innerLandingRadius,
            innerRingColor,
            executionersDescentShockwaveLineWidth * 0.9f,
            groundEllipseYSquash,
            "InnerLandingRing");

        return root;
    }

    private void CreateEnemyPounceTelegraphRing(
        Transform parent,
        float radiusWorld,
        Color color,
        float lineWidth,
        float ySquash,
        string objectName)
    {
        var ringGo = new GameObject(objectName);
        ringGo.transform.SetParent(parent, false);
        var lr = ringGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.alignment = LineAlignment.View;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = color;
        lr.endColor = color;
        ApplyExecutionersDescentSorting(lr);
        lr.sortingOrder = executionersDescentSortingOrder - 1;

        Shader spritesDefault = Shader.Find("Sprites/Default");
        if (spritesDefault != null)
            lr.material = new Material(spritesDefault);

        const int segments = 48;
        lr.positionCount = segments;
        float radius = Mathf.Max(0.05f, radiusWorld);
        float step = (Mathf.PI * 2f) / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = step * i;
            lr.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * ySquash,
                0f));
        }
    }

    public void SpawnEnemyAbilityImpactShockwave(Vector3 targetWorld, float maxRadius)
    {
        if (_executionersDescentShockwaveRoutine != null)
            StopCoroutine(_executionersDescentShockwaveRoutine);

        _executionersDescentShockwaveRoutine = StartCoroutine(CoExecutionersDescentImpactShockwave(targetWorld, maxRadius));
    }

    public void StopExecutionersDescentVfx()
    {
        if (_executionersDescentMarkRoot != null)
        {
            Destroy(_executionersDescentMarkRoot);
            _executionersDescentMarkRoot = null;
            _executionersDescentMarkRenderer = null;
        }

        if (_executionersDescentAxeRoot != null)
        {
            Destroy(_executionersDescentAxeRoot);
            _executionersDescentAxeRoot = null;
            _executionersDescentAxeRenderer = null;
        }
    }

    private Vector3 ResolveExecutionersDescentMinimumWorldPosition(Vector3 targetWorld)
    {
        float spawnHeight = Mathf.Max(0.1f, executionersDescentSpawnHeightAboveTarget);
        float minimumHeight = Mathf.Clamp(
            executionersDescentMinimumHeightAboveTarget,
            0f,
            spawnHeight);
        return new Vector3(
            targetWorld.x,
            targetWorld.y + minimumHeight,
            targetWorld.z);
    }

    private void EvaluateExecutionersDescentAxePose(
        Vector3 targetWorld,
        float elapsedSeconds,
        out Vector3 axeWorldPosition,
        out Vector3 hangWorldPosition)
    {
        float spawnHeight = Mathf.Max(0.1f, executionersDescentSpawnHeightAboveTarget);

        Vector3 spawnWorld = new Vector3(
            targetWorld.x,
            targetWorld.y + spawnHeight,
            targetWorld.z);
        Vector3 minimumWorld = ResolveExecutionersDescentMinimumWorldPosition(targetWorld);
        hangWorldPosition = minimumWorld;

        float holdSeconds = Mathf.Clamp(executionersDescentSpawnHoldSeconds, 0f, _executionersDescentTotalSeconds);
        float dropDuration = Mathf.Max(0.01f, _executionersDescentTotalSeconds - holdSeconds);

        if (elapsedSeconds <= holdSeconds)
        {
            axeWorldPosition = spawnWorld;
            return;
        }

        float dropElapsed = elapsedSeconds - holdSeconds;
        float dropT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dropElapsed / dropDuration));
        axeWorldPosition = Vector3.Lerp(spawnWorld, minimumWorld, dropT);
    }

    private void SyncExecutionersDescentMark(EnemyBaseController target, Vector3 hangWorldPosition)
    {
        if (_executionersDescentMarkRoot == null)
            return;

        if (_executionersDescentMarkRenderer != null)
        {
            _executionersDescentMarkRenderer.sprite = executionersDescentMarkSprite;
            _executionersDescentMarkRenderer.color = executionersDescentMarkTint;
            _executionersDescentMarkRenderer.enabled = executionersDescentMarkSprite != null;
        }

        if (target != null)
        {
            if (_executionersDescentMarkRoot.transform.parent != target.transform)
            {
                _executionersDescentMarkRoot.transform.SetParent(target.transform, false);
                _executionersDescentMarkRoot.transform.localPosition = executionersDescentMarkOffset;
            }

            _executionersDescentMarkRoot.transform.localScale = Vector3.one * executionersDescentMarkWorldScale;
        }
        else
        {
            _executionersDescentMarkRoot.transform.SetParent(null, true);
            _executionersDescentMarkRoot.transform.position = hangWorldPosition;
        }
    }

    private IEnumerator CoExecutionersDescentImpactShockwave(Vector3 targetWorld, float maxRadius)
    {
        Vector3 groundCenter = new Vector3(
            targetWorld.x,
            targetWorld.y + executionersDescentShockwaveGroundOffset,
            targetWorld.z);

        GameObject root = new GameObject("ExecutionersDescentShockwave");
        LineRenderer leftArc = CreateExecutionersDescentShockwaveArc(root.transform, true);
        LineRenderer rightArc = CreateExecutionersDescentShockwaveArc(root.transform, false);

        GameObject spriteGo = null;
        SpriteRenderer spriteSr = null;
        if (executionersDescentShockwaveSprite != null)
        {
            spriteGo = new GameObject("ExecutionersDescentShockwaveSprite");
            spriteGo.transform.SetParent(root.transform, false);
            spriteSr = spriteGo.AddComponent<SpriteRenderer>();
            spriteSr.sprite = executionersDescentShockwaveSprite;
            spriteSr.color = executionersDescentShockwaveColor;
            ApplyExecutionersDescentSorting(spriteSr);
            spriteGo.transform.position = groundCenter;
        }

        float duration = Mathf.Max(0.05f, executionersDescentShockwaveDuration);
        maxRadius = Mathf.Max(0.1f, maxRadius);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (root == null)
                yield break;

            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float radius = maxRadius * u;
            float alpha = executionersDescentShockwaveColor.a * (1f - u);

            RebuildExecutionersDescentShockwaveArc(leftArc, groundCenter, radius, true, alpha);
            RebuildExecutionersDescentShockwaveArc(rightArc, groundCenter, radius, false, alpha);

            if (spriteSr != null)
            {
                float scale = Mathf.Lerp(0.35f, 1.35f, u) * (maxRadius * 0.35f);
                spriteGo.transform.position = groundCenter;
                spriteGo.transform.localScale = new Vector3(scale, scale * 0.35f, 1f);
                Color c = executionersDescentShockwaveColor;
                c.a = alpha;
                spriteSr.color = c;
            }

            yield return null;
        }

        if (root != null)
            Destroy(root);

        _executionersDescentShockwaveRoutine = null;
    }

    private LineRenderer CreateExecutionersDescentShockwaveArc(Transform parent, bool leftSide)
    {
        var go = new GameObject(leftSide ? "ShockwaveArcLeft" : "ShockwaveArcRight");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = 4;
        lr.startWidth = executionersDescentShockwaveLineWidth;
        lr.endWidth = executionersDescentShockwaveLineWidth * 0.65f;
        Shader spritesDefault = Shader.Find("Sprites/Default");
        if (spritesDefault != null)
            lr.material = new Material(spritesDefault);
        ApplyExecutionersDescentSorting(lr);
        return lr;
    }

    private static void RebuildExecutionersDescentShockwaveArc(
        LineRenderer lr,
        Vector3 center,
        float radius,
        bool leftSide,
        float alpha)
    {
        if (lr == null)
            return;

        const int segments = 14;
        lr.positionCount = segments + 1;
        float startAngle = leftSide ? Mathf.PI * 0.55f : -Mathf.PI * 0.45f;
        float endAngle = leftSide ? Mathf.PI * 1.45f : Mathf.PI * 0.45f;
        float step = (endAngle - startAngle) / segments;
        float ySquash = 0.22f;

        Color c = lr.startColor;
        c.a = alpha;
        lr.startColor = c;
        lr.endColor = c;

        for (int i = 0; i <= segments; i++)
        {
            float a = startAngle + step * i;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * ySquash, 0f);
            lr.SetPosition(i, p);
        }
    }

    private void ApplyExecutionersDescentSorting(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (!string.IsNullOrWhiteSpace(executionersDescentSortingLayer))
            renderer.sortingLayerName = executionersDescentSortingLayer;

        renderer.sortingOrder = executionersDescentSortingOrder;
    }

    private void EnsureExecutionersDescentVisuals()
    {
        if (_executionersDescentAxeRoot == null)
        {
            _executionersDescentAxeRoot = new GameObject("ExecutionersDescentAxeVfx");
            _executionersDescentAxeRenderer = _executionersDescentAxeRoot.AddComponent<SpriteRenderer>();
            ApplyExecutionersDescentSorting(_executionersDescentAxeRenderer);
        }

        if (_executionersDescentMarkRoot == null)
        {
            _executionersDescentMarkRoot = new GameObject("ExecutionersDescentMarkVfx");
            _executionersDescentMarkRenderer = _executionersDescentMarkRoot.AddComponent<SpriteRenderer>();
            ApplyExecutionersDescentSorting(_executionersDescentMarkRenderer);
            _executionersDescentMarkRenderer.sortingOrder = executionersDescentSortingOrder - 1;
        }
    }
}
