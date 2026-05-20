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

    [Header("Whirlwind (Melee) VFX")]
    [SerializeField] private Color whirlingBladeColor = new Color(1f, 0.88f, 0.22f, 0.95f);
    [SerializeField, Min(0.01f)] private float whirlingBladeDuration = 0.22f;
    [SerializeField, Min(90f)] private float whirlingBladeSpinDegrees = 720f;
    [SerializeField, Min(0.01f)] private float whirlingBladeLineWidth = 0.14f;
    [SerializeField] private Vector3 whirlingBladeCenterOffset = new Vector3(0f, 0.65f, 0f);
    [Tooltip("Number of staggered slash trails on the same horizontal orbit.")]
    [SerializeField, Range(2, 6)] private int whirlingBladeSlashCount = 4;
    [Tooltip("How flat the orbit is in side view (0 = pure left-right line, ~0.12 = subtle arc).")]
    [SerializeField, Range(0f, 0.25f)] private float whirlingBladeOrbitVerticalScale = 0.08f;
    [SerializeField, Min(0f)] private float whirlingBladeUpwardDrift = 0.14f;
    [SerializeField, Min(0f)] private float whirlingBladeVerticalWave = 0.06f;

    [Header("Crescent Slash (Melee) VFX")]
    [SerializeField] private Color crescentSlashColor = new Color(0.55f, 0.95f, 1f, 0.9f);
    [SerializeField, Min(0.05f)] private float crescentSlashVfxDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float crescentSlashLineWidth = 0.12f;
    [SerializeField] private Vector3 crescentSlashCenterOffset = new Vector3(0f, 0.65f, 0f);

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
    [SerializeField, Min(0.5f)] private float executionersDescentShockwaveMaxRadius = 5.5f;
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
    [SerializeField] private Color bladestormStabColor = new Color(0.92f, 0.95f, 1f, 0.9f);
    [SerializeField, Min(0.02f)] private float bladestormStabFadeSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float bladestormStabExtendSeconds = 0.045f;
    [SerializeField, Min(0.01f)] private float bladestormStabLineWidth = 0.16f;
    [SerializeField, Min(0.3f)] private float bladestormStabReach = 1.15f;
    [Tooltip("How often a random outward stab line spawns during the channel.")]
    [SerializeField, Min(0.05f)] private float bladestormStabEmitIntervalSeconds = 0.3f;
    [Tooltip("Stabs fan up and down from the horizontal facing axis (± degrees pitch).")]
    [SerializeField, Range(5f, 75f), FormerlySerializedAs("bladestormStabConeHalfDegrees")]
    private float bladestormStabVerticalHalfDegrees = 35f;
    [SerializeField] private Vector3 bladestormStabOriginOffset = new Vector3(0.12f, 0.55f, 0f);
    [SerializeField] private Color bladestormFinaleSlashColor = new Color(1f, 0.72f, 0.28f, 0.95f);
    [SerializeField, Min(0.3f)] private float bladestormFinaleSlashDropHeight = 2.2f;
    [SerializeField, Min(0.2f)] private float bladestormFinaleSlashDepth = 1.35f;
    [SerializeField, Min(0.01f)] private float bladestormFinaleSlashLineWidth = 0.34f;
    [SerializeField, Min(0.01f)] private float bladestormFinaleSlashExtendSeconds = 0.06f;
    [SerializeField, Min(0.02f)] private float bladestormFinaleSlashFadeSeconds = 0.14f;
    [SerializeField] private float bladestormFinaleSlashTargetYOffset = 0.45f;

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

    private GameObject _lumberFrenzyAnchorRoot;
    private GameObject _lumberFrenzyOrbitVfxRoot;
    private ParticleSystem _gatheringFrenzyRuntimeParticles;

    private GameObject _avatarOfForestGlowRoot;
    private GameObject _energyInfusionGlowRoot;
    private GameObject _battleTranceGlowRoot;
    private GameObject _flameChargePlayerGlowRoot;
    private Coroutine _flameChargeVolcanicBurstRoutine;
    private readonly List<GameObject> _activeFlameChargeDashTrailRoots = new();

    private GameObject _executionersDescentAxeRoot;
    private SpriteRenderer _executionersDescentAxeRenderer;
    private GameObject _executionersDescentMarkRoot;
    private SpriteRenderer _executionersDescentMarkRenderer;
    private Coroutine _executionersDescentShockwaveRoutine;
    private float _executionersDescentTotalSeconds = 3f;
    private Coroutine _bladestormStabSprayRoutine;

    private const string ResourcesUrParticleMaterialPath = "Vfx/AbilityVfx_ParticlesUnlit";
    private static bool s_LoggedMissingUrParticleMaterial;

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
        DestroyAvatarOfTheForestGlowVfx();
        DestroyEnergyInfusionGlowVfx();
        DestroyBattleTranceGlowVfx();
        EndFlameChargePlayerGlow();
        DestroyAllFlameChargeDashTrailVfx();
        EndBladestormStabSpray();
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

        int slashCount = Mathf.Clamp(whirlingBladeSlashCount, 2, 6);
        var emitters = new Transform[slashCount];
        var trails = new TrailRenderer[slashCount];
        var phases = new float[slashCount];

        GameObject root = new GameObject("WhirlwindBlades");
        root.transform.position = center.position + whirlingBladeCenterOffset;

        for (int i = 0; i < slashCount; i++)
        {
            phases[i] = (i / (float)slashCount) * Mathf.PI * 2f;
            float widthScale = 0.82f + 0.18f * (1f - Mathf.Abs((i / (float)slashCount) - 0.5f) * 2f);

            GameObject orbitGO = new GameObject($"WhirlwindSlash_{i}");
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
            phases));
    }

    private TrailRenderer CreateWhirlwindBladeTrail(GameObject owner, float widthScale, int sortingOrderOffsetFromPlayer)
    {
        TrailRenderer trail = owner.AddComponent<TrailRenderer>();
        trail.time = Mathf.Max(0.06f, whirlingBladeDuration * 0.75f);
        trail.minVertexDistance = 0.003f;
        trail.widthMultiplier = Mathf.Max(0.01f, whirlingBladeLineWidth * widthScale);
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.TransformZ;
        trail.textureMode = LineTextureMode.Stretch;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        if (!TryApplyPlayerSpriteSortingToRenderer(trail, sortingOrderOffsetFromPlayer))
            trail.sortingOrder = 18 + sortingOrderOffsetFromPlayer;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(whirlingBladeColor, 0f),
                new GradientColorKey(Color.Lerp(whirlingBladeColor, Color.white, 0.25f), 0.45f),
                new GradientColorKey(whirlingBladeColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(whirlingBladeColor.a, 0f),
                new GradientAlphaKey(Mathf.Clamp01(whirlingBladeColor.a * 0.85f), 0.4f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
        return trail;
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
        float[] phases)
    {
        if (root == null || emitters == null || center == null)
            yield break;

        float duration = Mathf.Max(0.06f, whirlingBladeDuration);
        float elapsed = 0f;
        float width = Mathf.Max(0.01f, whirlingBladeLineWidth);
        float visualRadius = Mathf.Max(0.05f, radius - (width * 0.5f));
        float spinRad = Mathf.Abs(whirlingBladeSpinDegrees) * Mathf.Deg2Rad;
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
                    spinRad,
                    facingSign,
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

    /// <summary>
    /// Flat horizontal circle around the player (side-view orbit on X). Multiple slash trails use different phase offsets on the same path.
    /// </summary>
    private void EvaluateWhirlwindHorizontalSlashOrbit(
        float normalizedTime,
        float visualRadius,
        float phaseOffset,
        float spinRadians,
        float facingSign,
        out float x,
        out float y)
    {
        float angle = phaseOffset + spinRadians * normalizedTime;
        x = Mathf.Cos(angle) * visualRadius * facingSign;
        float verticalScale = Mathf.Max(0f, whirlingBladeOrbitVerticalScale);
        y = Mathf.Sin(angle) * visualRadius * verticalScale;
        y += Mathf.Sin(angle * 2f) * whirlingBladeVerticalWave * 0.5f;
        y += normalizedTime * whirlingBladeUpwardDrift * 0.2f;
    }

    /// <summary>Same horizontal figure-eight as Whirlwind: t in [0,1], x/y offset from sweep center.</summary>
    private void EvaluateWhirlwindStyleSweep(float t, float visualRadius, float spinScale, float startDirX, out float x, out float y)
    {
        if (t < 0.5f)
        {
            float p = t / 0.5f;
            x = Mathf.Lerp(visualRadius, -visualRadius, p);
            y = Mathf.Lerp(0f, -whirlingBladeUpwardDrift, p);
            y += Mathf.Sin(p * Mathf.PI) * whirlingBladeVerticalWave * spinScale;
        }
        else
        {
            float p = (t - 0.5f) / 0.5f;
            x = Mathf.Lerp(-visualRadius, visualRadius * 0.92f, p);
            y = Mathf.Lerp(-whirlingBladeUpwardDrift, whirlingBladeUpwardDrift * 0.35f, p);
            y += Mathf.Sin(p * Mathf.PI) * (whirlingBladeVerticalWave * 0.65f) * spinScale;
        }

        x *= Mathf.Sign(startDirX == 0f ? 1f : startDirX);
    }

    /// <summary>Smoke puff at the player's departure point; lingers after teleport.</summary>
    public void SpawnShadowStrikeDepartSmoke(Vector3 departureWorldPosition)
    {
        StartCoroutine(CoShadowStrikeDepartSmoke(departureWorldPosition + shadowStrikeDepartSmokeOffset));
    }

    public void SpawnShadowStrikeBurst(Vector3 targetWorldPosition)
    {
        Vector3 center = targetWorldPosition + shadowStrikeBurstOffset;
        StartCoroutine(CoShadowStrikeBurst(center));
    }

    private IEnumerator CoShadowStrikeDepartSmoke(Vector3 center)
    {
        GameObject root = new GameObject("ShadowStrikeDepartSmoke");
        root.transform.position = center;

        ParticleSystem puff = CreateShadowStrikeDepartSmokeParticleSystem(root.transform, wispy: false);
        ParticleSystem wisps = CreateShadowStrikeDepartSmokeParticleSystem(root.transform, wispy: true);

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

    private ParticleSystem CreateShadowStrikeDepartSmokeParticleSystem(Transform parent, bool wispy)
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

        Color core = shadowStrikeDepartSmokeColor;
        Color edge = Color.Lerp(core, new Color(0.62f, 0.28f, 0.88f, core.a), 0.35f);
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

    private IEnumerator CoShadowStrikeBurst(Vector3 center)
    {
        GameObject root = new GameObject("ShadowStrikeBurst");
        LineRenderer ring = root.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 24;
        ring.widthMultiplier = shadowStrikeBurstLineWidth;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = shadowStrikeBurstColor;
        ring.endColor = shadowStrikeBurstColor;
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
            float alpha = shadowStrikeBurstColor.a * (1f - t);
            Color c = shadowStrikeBurstColor;
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

    public void UpdateCleavingChopRangeIndicator(bool buffActive, float radiusWorld)
    {
        if (!cleavingChopShowRangeIndicator || !AreAbilityRangeIndicatorsEnabled())
        {
            if (_cleavingChopIndicatorRoot != null && _cleavingChopIndicatorRoot.activeSelf)
                _cleavingChopIndicatorRoot.SetActive(false);
            return;
        }

        if (!buffActive)
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

        float radius = Mathf.Max(0f, radiusWorld);
        if (!Mathf.Approximately(_cleavingChopIndicatorAppliedRadius, radius))
        {
            RebuildCleavingChopIndicatorCircle(radius);
            _cleavingChopIndicatorAppliedRadius = radius;
        }
    }

    private void EnsureCleavingChopIndicatorBuilt()
    {
        if (_cleavingChopIndicatorRoot != null && _cleavingChopIndicatorLine != null)
            return;

        Transform existing = transform.Find("CleavingChopRangeIndicator");
        if (existing != null)
        {
            _cleavingChopIndicatorRoot = existing.gameObject;
            _cleavingChopIndicatorLine = existing.GetComponent<LineRenderer>();
            if (_cleavingChopIndicatorLine == null)
                _cleavingChopIndicatorLine = existing.gameObject.AddComponent<LineRenderer>();
        }
        else
        {
            _cleavingChopIndicatorRoot = new GameObject("CleavingChopRangeIndicator");
            _cleavingChopIndicatorRoot.transform.SetParent(transform, false);
            _cleavingChopIndicatorRoot.transform.localPosition = Vector3.zero;
            _cleavingChopIndicatorRoot.transform.localRotation = Quaternion.identity;
            _cleavingChopIndicatorRoot.transform.localScale = Vector3.one;
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

    public void EnsureSpectralAxeAreaIndicatorBuilt()
    {
        if (!spectralAxeShowAreaIndicator || !AreAbilityRangeIndicatorsEnabled())
        {
            DestroySpectralAxeAreaIndicator();
            return;
        }

        if (_spectralAxeAreaIndicatorRoot != null && _spectralAxeAreaIndicatorLine != null)
            return;

        _spectralAxeAreaIndicatorRoot = new GameObject("SpectralAxeAreaIndicator");
        _spectralAxeAreaIndicatorLine = _spectralAxeAreaIndicatorRoot.AddComponent<LineRenderer>();

        var lr = _spectralAxeAreaIndicatorLine;
        lr.useWorldSpace = true;
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
    }

    public void UpdateSpectralAxeAreaIndicator(Vector3 axeCenter, float areaRadiusWorld)
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
    }

    public void DestroySpectralAxeAreaIndicator()
    {
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
        DestroyBattleTranceGlowVfx();
        Transform parent = player != null ? player.transform : transform;
        if (parent == null)
            return;

        _battleTranceGlowRoot = new GameObject("BattleTranceGlow");
        _battleTranceGlowRoot.transform.SetParent(parent, false);
        _battleTranceGlowRoot.transform.localPosition = battleTranceGlowLocalOffset;
        _battleTranceGlowRoot.transform.localRotation = Quaternion.identity;
        _battleTranceGlowRoot.transform.localScale = Vector3.one;

        GameObject emitterGO = new GameObject("BattleTranceEmitter");
        emitterGO.transform.SetParent(_battleTranceGlowRoot.transform, false);
        emitterGO.transform.localPosition = Vector3.zero;

        ParticleSystem ps = emitterGO.AddComponent<ParticleSystem>();
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

        ps.Play(true);
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
            Destroy(_battleTranceGlowRoot);
            _battleTranceGlowRoot = null;
        }
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

    public void BeginBladestormStabSpray(float channelSeconds, EnemyBaseController targetBias)
    {
        EndBladestormStabSpray();
        if (player == null || channelSeconds <= 0f)
            return;

        _ = targetBias;
        _bladestormStabSprayRoutine = StartCoroutine(CoBladestormStabSpray(channelSeconds));
    }

    public void EndBladestormStabSpray()
    {
        if (_bladestormStabSprayRoutine == null)
            return;

        StopCoroutine(_bladestormStabSprayRoutine);
        _bladestormStabSprayRoutine = null;
    }

    private IEnumerator CoBladestormStabSpray(float channelSeconds)
    {
        float interval = Mathf.Max(0.05f, bladestormStabEmitIntervalSeconds);
        float endTime = Time.time + channelSeconds;

        while (Time.time < endTime)
        {
            if (player == null)
                yield break;

            SpawnBladestormRandomStab();
            yield return new WaitForSeconds(interval);
        }

        _bladestormStabSprayRoutine = null;
    }

    private void SpawnBladestormRandomStab()
    {
        if (player == null)
            return;

        float facingX = GetCombatFacingSign();
        Vector3 origin = player.transform.position + new Vector3(
            bladestormStabOriginOffset.x * facingX,
            bladestormStabOriginOffset.y,
            bladestormStabOriginOffset.z);

        // Flat horizontal axis in the facing direction; pitch up/down from that axis only.
        float verticalHalf = Mathf.Max(5f, bladestormStabVerticalHalfDegrees);
        float pitchDeg = UnityEngine.Random.Range(-verticalHalf, verticalHalf);
        float angleDeg = facingX > 0f ? pitchDeg : 180f - pitchDeg;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
        float reach = bladestormStabReach * UnityEngine.Random.Range(0.75f, 1.2f);
        Vector3 end = origin + dir * reach;

        StartCoroutine(CoBladestormStabLineExtend(origin, end));
    }

    public void SpawnBladestormFinaleDownwardSlash(Vector3 targetWorld)
    {
        Vector3 anchor = new Vector3(
            targetWorld.x,
            targetWorld.y + bladestormFinaleSlashTargetYOffset,
            targetWorld.z);
        Vector3 start = anchor + Vector3.up * bladestormFinaleSlashDropHeight;
        Vector3 end = anchor - Vector3.up * bladestormFinaleSlashDepth;
        StartCoroutine(CoBladestormFinaleDownwardSlash(start, end));
    }

    private IEnumerator CoBladestormFinaleDownwardSlash(Vector3 start, Vector3 end)
    {
        GameObject root = new GameObject("BladestormFinaleSlash");
        LineRenderer lr = root.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, start);
        lr.SetPosition(1, start);
        lr.startWidth = bladestormFinaleSlashLineWidth;
        lr.endWidth = bladestormFinaleSlashLineWidth * 0.18f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        Color tipColor = new Color(
            bladestormFinaleSlashColor.r,
            bladestormFinaleSlashColor.g,
            bladestormFinaleSlashColor.b,
            bladestormFinaleSlashColor.a * 0.2f);
        lr.startColor = bladestormFinaleSlashColor;
        lr.endColor = tipColor;
        if (!TryApplyPlayerSpriteSortingToRenderer(lr, 18))
            lr.sortingOrder = 28;

        float extendSeconds = Mathf.Max(0.01f, bladestormFinaleSlashExtendSeconds);
        float fadeSeconds = Mathf.Max(0.02f, bladestormFinaleSlashFadeSeconds);
        float extendElapsed = 0f;
        while (extendElapsed < extendSeconds)
        {
            extendElapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(extendElapsed / extendSeconds));
            Vector3 tip = Vector3.Lerp(start, end, u);
            lr.SetPosition(0, start);
            lr.SetPosition(1, tip);
            float widthT = Mathf.Lerp(bladestormFinaleSlashLineWidth, bladestormFinaleSlashLineWidth * 0.35f, u);
            lr.startWidth = widthT;
            lr.endWidth = widthT * 0.12f;
            yield return null;
        }

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        float fadeElapsed = 0f;
        while (fadeElapsed < fadeSeconds)
        {
            fadeElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(fadeElapsed / fadeSeconds);
            float alpha = bladestormFinaleSlashColor.a * (1f - u);
            Color c0 = bladestormFinaleSlashColor;
            Color c1 = tipColor;
            c0.a = alpha;
            c1.a = alpha * 0.2f;
            lr.startColor = c0;
            lr.endColor = c1;
            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator CoBladestormStabLineExtend(Vector3 origin, Vector3 end)
    {
        GameObject root = new GameObject("BladestormStab");
        LineRenderer lr = root.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin);
        lr.startWidth = bladestormStabLineWidth;
        lr.endWidth = bladestormStabLineWidth * 0.2f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        Color tipColor = new Color(bladestormStabColor.r, bladestormStabColor.g, bladestormStabColor.b, 0.12f);
        lr.startColor = bladestormStabColor;
        lr.endColor = tipColor;
        if (!TryApplyPlayerSpriteSortingToRenderer(lr, 14))
            lr.sortingOrder = 24;

        float extendSeconds = Mathf.Max(0.01f, bladestormStabExtendSeconds);
        float fadeSeconds = Mathf.Max(0.02f, bladestormStabFadeSeconds);
        float extendElapsed = 0f;
        while (extendElapsed < extendSeconds)
        {
            extendElapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(extendElapsed / extendSeconds));
            Vector3 tip = Vector3.Lerp(origin, end, u);
            lr.SetPosition(0, origin);
            lr.SetPosition(1, tip);
            float widthT = Mathf.Lerp(bladestormStabLineWidth, bladestormStabLineWidth * 0.25f, u);
            lr.startWidth = widthT;
            lr.endWidth = widthT * 0.15f;
            yield return null;
        }

        lr.SetPosition(0, origin);
        lr.SetPosition(1, end);

        float fadeElapsed = 0f;
        while (fadeElapsed < fadeSeconds)
        {
            fadeElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(fadeElapsed / fadeSeconds);
            float alpha = bladestormStabColor.a * (1f - u);
            Color c0 = bladestormStabColor;
            c0.a = alpha;
            Color c1 = tipColor;
            c1.a = alpha * 0.15f;
            lr.startColor = c0;
            lr.endColor = c1;
            yield return null;
        }

        Destroy(root);
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

    public void SpawnExecutionersDescentImpactShockwave(Vector3 targetWorld)
    {
        if (_executionersDescentShockwaveRoutine != null)
            StopCoroutine(_executionersDescentShockwaveRoutine);

        _executionersDescentShockwaveRoutine = StartCoroutine(CoExecutionersDescentImpactShockwave(targetWorld));
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

    private IEnumerator CoExecutionersDescentImpactShockwave(Vector3 targetWorld)
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
        float maxRadius = executionersDescentShockwaveMaxRadius;
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
