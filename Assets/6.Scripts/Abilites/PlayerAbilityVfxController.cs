using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    [SerializeField, Min(0f)] private float whirlingBladeUpwardDrift = 0.14f;
    [SerializeField, Min(0f)] private float whirlingBladeVerticalWave = 0.06f;

    [Header("Crescent Slash (Melee) VFX")]
    [SerializeField] private Color crescentSlashColor = new Color(0.55f, 0.95f, 1f, 0.9f);
    [SerializeField, Min(0.05f)] private float crescentSlashVfxDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float crescentSlashLineWidth = 0.12f;
    [SerializeField] private Vector3 crescentSlashCenterOffset = new Vector3(0f, 0.65f, 0f);

    [Header("Lumber Frenzy (Woodcutting Lv5) VFX")]
    [Tooltip("Optional root prefab parented under a sweep driver while Lumber Frenzy is active. Leave empty to auto-spawn green sparks.")]
    [SerializeField] private GameObject lumberFrenzyOrbitVfxPrefab;
    [SerializeField] private Vector3 lumberFrenzyOrbitVfxLocalOffset = new Vector3(0f, 0.72f, 0f);
    [Tooltip("Horizontal sweep radius (matches Whirlwind outer ring logic: attack range minus half trail width).")]
    [SerializeField, Min(0.05f)] private float lumberFrenzyOrbitRadius = 0.88f;
    [Tooltip("Seconds for one full horizontal figure-eight sweep (same path shape as Whirlwind).")]
    [SerializeField, Min(0.08f)] private float lumberFrenzySweepPeriodSeconds = 0.95f;
    [SerializeField] private Color lumberFrenzyOrbitVfxColor = new Color(0.35f, 1f, 0.45f, 1f);

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

    private GameObject _cleavingChopIndicatorRoot;
    private LineRenderer _cleavingChopIndicatorLine;
    private float _cleavingChopIndicatorAppliedRadius = float.NaN;

    private GameObject _spectralAxeAreaIndicatorRoot;
    private LineRenderer _spectralAxeAreaIndicatorLine;

    private GameObject _lumberFrenzyAnchorRoot;
    private Transform _lumberFrenzySweepMotion;
    private GameObject _lumberFrenzyOrbitVfxRoot;

    private GameObject _avatarOfForestGlowRoot;

    private const string ResourcesUrParticleMaterialPath = "Vfx/AbilityVfx_ParticlesUnlit";
    private static bool s_LoggedMissingUrParticleMaterial;

    public SoulforgedWeaponMinionPresentation SoulforgedWeaponMinionPresentation => soulforgedWeaponMinionPresentation;

    public Transform LumberFrenzyOrbitVfxTransform =>
        _lumberFrenzyAnchorRoot != null ? _lumberFrenzyAnchorRoot.transform : null;

    public float SpectralAxeVisualLift => spectralAxeVisualLift;
    public float SpectralAxeSpinDegreesPerSecond => spectralAxeSpinDegreesPerSecond;
    public bool SpectralAxeSpinClockwise => spectralAxeSpinClockwise;
    public float SpectralAxeTravelSpeedUnitsPerSecond => spectralAxeTravelSpeedUnitsPerSecond;

    /// <summary>Whirlwind / combat facing from weapon scale; for Lumber Frenzy sweep use <see cref="GetLumberFrenzySweepMirrorSign"/> instead.</summary>
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

    /// <summary>
    /// Mirror sign for the horizontal figure-eight only. Uses <see cref="PlayerController.FacingDirectionX"/> so weapon / gather
    /// animation on the power-slash anchor does not jitter the Lumber Frenzy orbit.
    /// </summary>
    private float GetLumberFrenzySweepMirrorSign()
    {
        if (player != null && !Mathf.Approximately(player.FacingDirectionX, 0f))
            return Mathf.Sign(player.FacingDirectionX);
        return GetCombatFacingSign();
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

        GameObject orbitGO = new GameObject("WhirlwindTrailEmitter");
        orbitGO.transform.position = center.position + whirlingBladeCenterOffset;

        TrailRenderer trail = orbitGO.AddComponent<TrailRenderer>();
        trail.time = Mathf.Max(0.06f, whirlingBladeDuration * 0.75f);
        trail.minVertexDistance = 0.003f;
        trail.widthMultiplier = Mathf.Max(0.01f, whirlingBladeLineWidth);
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.TransformZ;
        trail.textureMode = LineTextureMode.Stretch;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        if (!TryApplyPlayerSpriteSortingToRenderer(trail, 12))
            trail.sortingOrder = 20;
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

        Vector2 startDir = ((Vector2)anchor.position - (Vector2)center.position).normalized;
        if (startDir.sqrMagnitude <= 0.0001f)
            startDir = Vector2.right * ((player != null && player.transform.localScale.x < 0f) ? -1f : 1f);

        StartCoroutine(AnimateWhirlwindTrail(orbitGO.transform, trail, center, radius, startDir));
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

    private IEnumerator AnimateWhirlwindTrail(Transform emitter, TrailRenderer trail, Transform center, float radius, Vector2 startDir)
    {
        if (emitter == null || center == null)
            yield break;

        float duration = Mathf.Max(0.06f, whirlingBladeDuration);
        float elapsed = 0f;
        float width = Mathf.Max(0.01f, trail != null ? trail.widthMultiplier : whirlingBladeLineWidth);
        float visualRadius = Mathf.Max(0.05f, radius - (width * 0.5f));
        float spinScale = Mathf.Clamp(Mathf.Abs(whirlingBladeSpinDegrees) / 720f, 0.25f, 2.5f);
        while (elapsed < duration && emitter != null && center != null)
        {
            float t = elapsed / duration;
            EvaluateWhirlwindStyleSweep(t, visualRadius, spinScale, startDir.x, out float x, out float y);
            emitter.position = center.position + whirlingBladeCenterOffset + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (trail != null)
            trail.emitting = false;
        if (emitter != null)
            Destroy(emitter.gameObject, Mathf.Max(0.04f, whirlingBladeDuration * 0.6f));
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

    public void SpawnLumberFrenzyOrbitVfx()
    {
        DestroyLumberFrenzyOrbitVfx();

        Transform followRoot = player != null ? player.transform : transform;
        if (followRoot == null)
            return;

        _lumberFrenzyAnchorRoot = new GameObject("LumberFrenzyOrbitAnchor");
        // Not parented under the player hierarchy: child bones / weapon / gather animation won't move this anchor.
        // World position is driven each frame from the player root only (see UpdateLumberFrenzyOrbitVfx).
        _lumberFrenzyAnchorRoot.transform.SetParent(null, false);
        _lumberFrenzyAnchorRoot.transform.position = followRoot.TransformPoint(lumberFrenzyOrbitVfxLocalOffset);
        _lumberFrenzyAnchorRoot.transform.rotation = Quaternion.identity;
        _lumberFrenzyAnchorRoot.transform.localScale = Vector3.one;

        var sweepGo = new GameObject("LumberFrenzySweepMotion");
        sweepGo.transform.SetParent(_lumberFrenzyAnchorRoot.transform, false);
        sweepGo.transform.localPosition = Vector3.zero;
        sweepGo.transform.localRotation = Quaternion.identity;
        sweepGo.transform.localScale = Vector3.one;
        _lumberFrenzySweepMotion = sweepGo.transform;

        if (lumberFrenzyOrbitVfxPrefab != null)
        {
            _lumberFrenzyOrbitVfxRoot = Instantiate(lumberFrenzyOrbitVfxPrefab, _lumberFrenzySweepMotion);
            _lumberFrenzyOrbitVfxRoot.name = "LumberFrenzyOrbitVfx";
            Transform t = _lumberFrenzyOrbitVfxRoot.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            TryApplyPlayerSpriteSortingToHierarchy(t, 10);
            return;
        }

        CreateRuntimeLumberFrenzyOrbitParticles(_lumberFrenzySweepMotion);
    }

    private void CreateRuntimeLumberFrenzyOrbitParticles(Transform parentMotion)
    {
        _lumberFrenzyOrbitVfxRoot = new GameObject("LumberFrenzyOrbitVfx_Runtime");
        Transform root = _lumberFrenzyOrbitVfxRoot.transform;
        root.SetParent(parentMotion, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        GameObject emitterGO = new GameObject("GreenSparkEmitter");
        emitterGO.transform.SetParent(root, false);
        emitterGO.transform.localPosition = Vector3.zero;
        emitterGO.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = emitterGO.AddComponent<ParticleSystem>();
        // AddComponent can begin simulating with default playOnAwake before we assign main; duration/lifetime
        // changes are not allowed while playing.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
        main.startColor = lumberFrenzyOrbitVfxColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 320;

        var emission = ps.emission;
        emission.rateOverTime = 48f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(lumberFrenzyOrbitVfxColor, 0f),
                new GradientColorKey(Color.Lerp(lumberFrenzyOrbitVfxColor, Color.white, 0.2f), 0.45f),
                new GradientColorKey(lumberFrenzyOrbitVfxColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(lumberFrenzyOrbitVfxColor.a), 0f),
                new GradientAlphaKey(Mathf.Clamp01(lumberFrenzyOrbitVfxColor.a * 0.75f), 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

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

        ps.Play(true);
    }

    public void UpdateLumberFrenzyOrbitVfx(bool buffActive)
    {
        if (!buffActive || _lumberFrenzySweepMotion == null)
            return;

        Transform followRoot = player != null ? player.transform : transform;
        if (followRoot != null && _lumberFrenzyAnchorRoot != null)
            _lumberFrenzyAnchorRoot.transform.position = followRoot.TransformPoint(lumberFrenzyOrbitVfxLocalOffset);

        float width = Mathf.Max(0.01f, whirlingBladeLineWidth * 0.85f);
        float visualRadius = Mathf.Max(0.05f, lumberFrenzyOrbitRadius - (width * 0.5f));
        float spinScale = Mathf.Clamp(Mathf.Abs(whirlingBladeSpinDegrees) / 720f, 0.25f, 2.5f);

        float sweepMirrorSign = GetLumberFrenzySweepMirrorSign();

        float period = Mathf.Max(0.08f, lumberFrenzySweepPeriodSeconds);
        float t = (Time.time / period) % 1f;
        EvaluateWhirlwindStyleSweep(t, visualRadius, spinScale, sweepMirrorSign, out float x, out float y);
        _lumberFrenzySweepMotion.localPosition = whirlingBladeCenterOffset + new Vector3(x, y, 0f);
    }

    public void DestroyLumberFrenzyOrbitVfx()
    {
        _lumberFrenzySweepMotion = null;
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
}
