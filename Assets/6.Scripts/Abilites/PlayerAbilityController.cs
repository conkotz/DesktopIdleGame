using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles ability cooldowns + executing ability effects.
/// Minimal implementation for "Power Slash" style abilities.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAbilityController : MonoBehaviour
{
    private enum CrescentConvertedElement
    {
        Fire,
        Ice,
        Lightning
    }

    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CharacterStats stats;
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private AbilityDatabase abilityDatabase;
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private SkillsManager skillsManager;
    [SerializeField] private ActionBarUI actionBar;
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ToolbeltManager toolbelt;
    [SerializeField] private PlayerBuffController buffController;

    [Header("Global Cooldown")]
    [SerializeField, Min(0f)] private float globalCooldownSeconds = 0.15f;
    private float _globalCooldownEndsAt;

    // Power Slash VFX — grouped in Inspector via PlayerAbilityControllerEditor
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

    // Whirlwind VFX — grouped in Inspector via PlayerAbilityControllerEditor
    [SerializeField] private Color whirlingBladeColor = new Color(1f, 0.88f, 0.22f, 0.95f);
    [SerializeField, Min(0.01f)] private float whirlingBladeDuration = 0.22f;
    [SerializeField, Min(90f)] private float whirlingBladeSpinDegrees = 720f;
    [SerializeField, Min(0.01f)] private float whirlingBladeLineWidth = 0.14f;
    [SerializeField] private Vector3 whirlingBladeCenterOffset = new Vector3(0f, 0.65f, 0f);
    [SerializeField, Min(0f)] private float whirlingBladeUpwardDrift = 0.14f;
    [SerializeField, Min(0f)] private float whirlingBladeVerticalWave = 0.06f;

    // Crescent Slash VFX — grouped in Inspector via PlayerAbilityControllerEditor
    [SerializeField] private Color crescentSlashColor = new Color(0.55f, 0.95f, 1f, 0.9f);
    [SerializeField, Min(0.05f)] private float crescentSlashVfxDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float crescentSlashLineWidth = 0.12f;
    [SerializeField] private Vector3 crescentSlashCenterOffset = new Vector3(0f, 0.65f, 0f);

    /// <summary>
    /// Motion, attach, slash, and visuals for Soulforged Weapon minion (tune on Player prefab).
    /// </summary>
    [SerializeField] private SoulforgedWeaponMinionPresentation soulforgedWeaponMinionPresentation;

    private readonly Dictionary<string, float> _cooldownEndsById = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cooldown end time per skill-tree row (skillType:Lv{requiredLevel}). When the tree is reset
    /// and the player picks a different ability in the same row, the new ability inherits this
    /// remaining cooldown so swapping isn't a free reset.
    /// </summary>
    private readonly Dictionary<string, float> _cooldownEndsByRowKey = new(StringComparer.OrdinalIgnoreCase);
    private const string PowerSlashId = "power_slash";
    private const string WhirlwindId = "whirlwind";
    private const string RendId = "rend";
    private const string EnvenomId = "envenom";
    private const string CleavingStrikesId = "cleaving_strikes";
    private const string CrescentSlashId = "crescent_slash";
    private const string LumberFrenzyId = "lumber_frenzy";
    private const float LumberFrenzyDurationSeconds = 20f;
    private const float LumberFrenzyChoppingSpeedBonus = 0.20f;
    private const float LumberFrenzyGritChanceBonus = 0.10f;
    private const float LumberFrenzyStaminaEfficiencyEnhancementBonus = 0.15f;
    private const float LumberFrenzyExtraGritEnhancementBonus = 0.05f;
    private const int LumberFrenzyChoiceSourceLevel = 5;
    private const int LumberFrenzyStaminaEnhancementChoiceIndex = 0;
    private const int LumberFrenzyExtraGritEnhancementChoiceIndex = 1;

    private const string SpectralAxeId = "spectral_axe";
    /// <summary>Spectral Axe stays out for this long before returning. Cooldown starts after the return finishes.</summary>
    private const float SpectralAxeBaseDurationSeconds = 60f;
    /// <summary>How far in front of the player (world units, signed by facing) the axe parks while chopping.</summary>
    private const float SpectralAxeProjectDistance = 5f;
    /// <summary>Distance test for Cleaving Flight: counts as a "tree collision" when the axe passes within this radius.</summary>
    private const float SpectralAxeTravelCollisionRadius = 0.7f;
    /// <summary>Yield multiplier applied to each gather while Spectral Axe is active (matches Cleaving Chop's secondary efficiency).</summary>
    private const float SpectralAxeYieldEfficiency = 0.6f;
    /// <summary>Radius of the spectral axe's circular gather area (world units from the rotating point).</summary>
    private const float SpectralAxeAreaRadius = 1.5f;
    /// <summary>
    /// Fallback cooldown applied when the axe parks but finds no tree inside its gather area.
    /// Kept short on purpose so a missed cast is recoverable — long enough to discourage spam,
    /// short enough to feel forgiving.
    /// </summary>
    private const float SpectralAxeMissedCastCooldownSeconds = 10f;

    [Header("Spectral Axe VFX")]
    [Tooltip("Blue hue tint applied to the cloned axe sprite. Alpha drives the translucency of the whole projectile.")]
    [SerializeField] private Color spectralAxeTint = new Color(0.55f, 0.80f, 1f, 0.85f);
    [Tooltip("Vertical offset added to the spinning axe so it floats at roughly chest height. Gather/targeting logic still uses ground-level positions, so this is purely cosmetic.")]
    [SerializeField, Min(0f)] private float spectralAxeVisualLift = 1.2f;
    [Tooltip("Continuous spin rate of the axe sprite while deployed (degrees per second).")]
    [SerializeField, Min(0f)] private float spectralAxeSpinDegreesPerSecond = 720f;
    [Tooltip("When true the axe spins clockwise (negative Z rotation in 2D); when false it spins counter-clockwise.")]
    [SerializeField] private bool spectralAxeSpinClockwise = true;
    [Tooltip("World units per second the axe travels along the outbound and return flight phases.")]
    [SerializeField, Min(0.1f)] private float spectralAxeTravelSpeedUnitsPerSecond = 12f;

    [Header("Spectral Axe Blue Trail")]
    [Tooltip("Total lifetime of the blue trail behind the spinning axe. Lower = shorter wisp; higher = longer arc.")]
    [SerializeField, Min(0.01f)] private float spectralAxeTrailLifetimeSeconds = 0.32f;
    [Tooltip("Starting width of the trail ribbon at the anchor point on the axe.")]
    [SerializeField, Min(0f)] private float spectralAxeTrailStartWidth = 0.18f;
    [Tooltip("Final width of the trail ribbon as it fades out.")]
    [SerializeField, Min(0f)] private float spectralAxeTrailEndWidth = 0f;
    [Tooltip("Starting alpha of the trail ribbon at the anchor point. Final alpha is always 0.")]
    [SerializeField, Range(0f, 1f)] private float spectralAxeTrailStartAlpha = 0.75f;
    [Tooltip("Fraction of the axe sprite's width subtracted from center to place the trail at the back of the head. Negative = front edge.")]
    [SerializeField] private float spectralAxeTrailAnchorXFrac = 0.28f;
    [Tooltip("Fraction of the axe sprite's height added above center to place the trail near the top of the head. Higher = closer to the top edge.")]
    [SerializeField] private float spectralAxeTrailAnchorYFrac = 0.55f;
    [Tooltip("Trail color at the moment it leaves the axe.")]
    [SerializeField] private Color spectralAxeTrailColorStart = new Color(0.45f, 0.78f, 1f);
    [Tooltip("Trail color at the tail end as it dissipates.")]
    [SerializeField] private Color spectralAxeTrailColorEnd = new Color(0.30f, 0.55f, 1f);

    private const int SpectralAxeChoiceSourceLevel = 25;
    private const int SpectralAxePhantomHarvestChoiceIndex = 0;
    private const int SpectralAxeCleavingFlightChoiceIndex = 1;

    private const string CleavingChopId = "cleaving_chop";
    /// <summary>Default Cleaving Chop buff duration in seconds (40s base, +5s with Prolonged Cleave enhancement).</summary>
    private const float CleavingChopBaseDurationSeconds = 40f;
    /// <summary>Bonus seconds added to Cleaving Chop duration when Prolonged Cleave is selected.</summary>
    private const float CleavingChopProlongedDurationBonusSeconds = 5f;
    /// <summary>Base world-units search radius for secondary tree strikes during Cleaving Chop.</summary>
    private const float CleavingChopBaseRange = 10f;
    /// <summary>Bonus range added to Cleaving Chop when Extended Reach is selected.</summary>
    private const float CleavingChopExtendedReachRangeBonus = 4f;
    /// <summary>Yield multiplier applied to each secondary tree gather while Cleaving Chop is active.</summary>
    private const float CleavingChopSecondaryYieldEfficiency = 0.6f;
    private const int CleavingChopChoiceSourceLevel = 25;
    private const int CleavingChopExtendedReachChoiceIndex = 0;
    private const int CleavingChopProlongedCleaveChoiceIndex = 1;
    private const int WhirlwindChoiceSourceLevel = 15;
    private const int SoulforgedWeaponChoiceSourceLevel = 35;
    private const int SoulforgedWeaponSwarmChoiceIndex = 0;
    private const int SoulforgedWeaponIndefiniteChoiceIndex = 1;
    private const int SoulforgedWeaponSwarmCount = 3;
    private const float SoulforgedWeaponSwarmDamageMultiplier = 0.75f;
    private const float SoulforgedWeaponSwarmDurationSeconds = 20f;
    private const float SoulforgedWeaponSceneLoadActionBarGraceSeconds = 2f;
    private static readonly float WhirlwindSecondHitMultiplier = AbilityCombatPower.WhirlwindTwinCycloneSecondHitFraction;
    private const float WhirlwindTwinCycloneSecondHitDelay = 0.5f;
    private const float WhirlwindRadiusBonus = 3f;
    private bool _powerSlashQueued;
    private bool _rendQueued;
    private bool _envenomQueued;
    private bool _crescentSlashQueued;
    private int _cleavingHitsRemaining;
    private int _cleavingAdditionalTargets;
    private float _cleavingBuffEndsAt;
    private float _cleavingBuffDuration;
    private bool _cleavingBuffActive;
    private int _lastSyncedCleavingHudStacks = int.MinValue;
    private float _lastSyncedCleavingHudEnd = float.NaN;
    private bool _lumberFrenzyActive;
    private float _lumberFrenzyEndsAt;
    private float _lumberFrenzyDuration;
    private float _lastSyncedLumberFrenzyHudEnd = float.NaN;
    /// <summary>When the Lumber Frenzy buff expires, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _lumberFrenzyCooldownAbilityDef;

    private bool _cleavingChopActive;
    private float _cleavingChopEndsAt;
    private float _cleavingChopDuration;
    private float _lastSyncedCleavingChopHudEnd = float.NaN;
    /// <summary>When the Cleaving Chop buff expires, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _cleavingChopCooldownAbilityDef;

    private bool _spectralAxeActive;
    private float _spectralAxeEndsAt;
    private float _spectralAxeDuration;
    private float _lastSyncedSpectralAxeHudEnd = float.NaN;
    /// <summary>When the Spectral Axe returns and despawns, this ability gets <see cref="StartCooldown"/>.</summary>
    private AbilityDefinition _spectralAxeCooldownAbilityDef;
    private Coroutine _spectralAxeRoutine;
    private GameObject _spectralAxeProjectile;
    /// <summary>Per-buff per-tree gather timer matching the secondary-chop pattern in PlayerController.</summary>
    private float _spectralAxeGatherAccum;
    private float _spectralAxeGatherNextInterval;
    private ResourceNode _spectralAxeGatherTarget;
    /// <summary>True when the axe parked but found no tree in its area → cooldown is overridden to <see cref="SpectralAxeMissedCastCooldownSeconds"/>.</summary>
    private bool _spectralAxeMissedCast;
    /// <summary>Visualizer rectangle showing the ±2 gather area around the rotating axe; lifecycle matches the projectile.</summary>
    private GameObject _spectralAxeAreaIndicatorRoot;
    private LineRenderer _spectralAxeAreaIndicatorLine;

    [Header("Cleaving Chop range indicator")]
    [Tooltip("Auto-spawned LineRenderer circle drawn around the player while Cleaving Chop is active.")]
    [SerializeField] private bool cleavingChopShowRangeIndicator = true;
    [SerializeField] private Color cleavingChopIndicatorColor = new Color(0.55f, 0.95f, 0.30f, 0.85f);
    [SerializeField, Range(16, 128)] private int cleavingChopIndicatorSegments = 64;
    [SerializeField, Min(0.005f)] private float cleavingChopIndicatorLineWidth = 0.14f;
    [Tooltip("Sorting order on the indicator LineRenderer. Higher = draws over more sprites. Default 50 draws above the lane backgrounds.")]
    [SerializeField] private int cleavingChopIndicatorSortingOrder = 50;
    [Tooltip("Sorting layer for the indicator. Leave blank for the project's default layer.")]
    [SerializeField] private string cleavingChopIndicatorSortingLayer = "";
    private GameObject _cleavingChopIndicatorRoot;
    private LineRenderer _cleavingChopIndicatorLine;
    private float _cleavingChopIndicatorAppliedRadius = float.NaN;

    [Header("Spectral Axe area indicator")]
    [Tooltip("Auto-spawned LineRenderer circle drawn around the spectral axe while it's deployed (same style as Cleaving Chop).")]
    [SerializeField] private bool spectralAxeShowAreaIndicator = true;
    [SerializeField] private Color spectralAxeAreaIndicatorColor = new Color(0.55f, 0.95f, 0.30f, 0.85f);
    [SerializeField, Range(16, 128)] private int spectralAxeAreaIndicatorSegments = 64;
    [SerializeField, Min(0.005f)] private float spectralAxeAreaIndicatorLineWidth = 0.12f;
    [SerializeField] private int spectralAxeAreaIndicatorSortingOrder = 50;
    [SerializeField] private string spectralAxeAreaIndicatorSortingLayer = "";
    private float _queuedPowerSlashPhysicalMultiplier = 1f;
    private float _queuedPowerSlashMagicMultiplier = 1f;
    private float _queuedPowerSlashCorruptionMultiplier;
    private float _queuedPowerSlashAllDamageMultiplier = 1f;
    private QueuedHitEffect _queuedConsumedThisHit;
    private int _queuedConsumedFrame = -1;

    /// <summary>Same object as <see cref="stats"/>; cached for summon spawn clarity.</summary>
    private CharacterStats _ownerStats;

    /// <summary>Player root transform (this component lives on the player).</summary>
    private Transform _ownerTransform;

    private readonly List<SoulforgedWeaponMinion> _activeSoulforgedWeaponMinions = new();
    private bool _activeSoulforgedWeaponIsPersistent;
    private float _soulforgedAvailabilityCheckPausedUntil;

    /// <summary>When the Soulforged Weapon summon despawns, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _soulforgedWeaponCooldownAbilityDef;

    /// <summary>
    /// HUD buff bookkeeping for Soulforged Weapon. Swarm variant uses a real countdown; the indefinite
    /// variant parks <c>endsAt</c> in the past so <see cref="BuffIconUI"/> hides its timer/overlay.
    /// </summary>
    private float _soulforgedHudBuffEndsAt;
    private float _soulforgedHudBuffDuration;
    private float _lastSyncedSoulforgedHudEnd = float.NaN;
    private int _lastSyncedSoulforgedHudStacks = int.MinValue;

    private static string s_pendingSoulforgedRestoreAbilityId;

    private enum QueuedHitEffect
    {
        None,
        PowerSlash,
        Rend,
        Envenom,
        CrescentSlash
    }

    public struct QueuedHitEffectResult
    {
        public bool suppressDefaultBleed;
        public bool suppressDefaultPoison;
        public bool triggerCrescentSlash;
        public bool crescentAppliesElemental;
        public bool crescentPenetrating;
    }

    private void Awake()
    {
        if (!player) player = GetComponent<PlayerController>();
        if (!stats) stats = GetComponent<CharacterStats>();
        _ownerStats = stats;
        _ownerTransform = transform;
        if (!combat) combat = GetComponent<PlayerCombatController>();
        if (!equipment) equipment = GetComponent<EquipmentManager>();
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!toolbelt) toolbelt = GetComponent<ToolbeltManager>();
        if (!buffController) buffController = GetComponent<PlayerBuffController>();
        if (!abilityDatabase) abilityDatabase = AbilityDatabase.LoadDefault();
        if (!skillDatabase) skillDatabase = SkillDatabase.LoadDefault();
        if (!skillsManager) skillsManager = SkillsManager.Instance;
        if (!actionBar) actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(RestorePendingSoulforgedAfterSceneLoad());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        TryAutoReleaseQueuedCrescentSlash();
        CleanupCleavingStrikesIfExpired();
        SyncCleavingStrikesHudBuff();
        CleanupLumberFrenzyIfExpired();
        SyncLumberFrenzyHudBuff();
        CleanupCleavingChopIfExpired();
        SyncCleavingChopHudBuff();
        UpdateCleavingChopRangeIndicator();
        SyncSpectralAxeHudBuff();
        CleanupSoulforgedWeaponIfUnavailable();
        SyncSoulforgedWeaponHudBuff();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _soulforgedAvailabilityCheckPausedUntil = Time.time + SoulforgedWeaponSceneLoadActionBarGraceSeconds;

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);

        CleanupSoulforgedWeaponList();
        for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
        {
            SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
            if (!minion)
                continue;

            minion.PersistAcrossSceneLoads();
            minion.ReturnHomeAfterSceneLoad();
        }

        // PlayerBuffController is fresh in the new scene — force the next Soulforged HUD sync to
        // re-push the buff entry instead of skipping because the cached "last synced" values match.
        _lastSyncedSoulforgedHudEnd = float.NaN;
        _lastSyncedSoulforgedHudStacks = int.MinValue;
    }

    private IEnumerator RestorePendingSoulforgedAfterSceneLoad()
    {
        yield return null;

        if (string.IsNullOrWhiteSpace(s_pendingSoulforgedRestoreAbilityId) || _activeSoulforgedWeaponMinions.Count > 0)
            yield break;

        string abilityId = s_pendingSoulforgedRestoreAbilityId;
        s_pendingSoulforgedRestoreAbilityId = null;
        AbilityDefinition def = GetAbilityDefinition(abilityId);
        if (!def || !def.minionSpawnDefinition || IsOnCooldown(def.abilityId, out _))
            yield break;

        if (!IsAbilityAllowedBySkillProgress(def) || !CanUseWithEquippedWeapon(def))
            yield break;

        TrySpawnSoulforgedWeaponMinion(def);
    }

    public bool IsOnCooldown(string abilityId, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        bool found = false;
        float end = 0f;

        if (_cooldownEndsById.TryGetValue(abilityId, out float idEnd))
        {
            end = idEnd;
            found = true;
        }

        // Row-keyed lookup: lets another ability in the same skill-tree row inherit the remaining
        // cooldown when the player resets the tree and picks a different ability on that row.
        string rowKey = BuildAbilityRowKey(GetAbilityDefinition(abilityId));
        if (rowKey != null && _cooldownEndsByRowKey.TryGetValue(rowKey, out float rowEnd) && rowEnd > end)
        {
            end = rowEnd;
            found = true;
        }

        if (!found)
            return false;

        remainingSeconds = Mathf.Max(0f, end - Time.time);
        return remainingSeconds > 0f;
    }

    /// <summary>Stable row key for cooldown sharing. Null when the ability isn't tied to a skill tree row.</summary>
    private static string BuildAbilityRowKey(AbilityDefinition def)
    {
        if (def == null || def.unlockLevel <= 0)
            return null;
        return $"row:{def.sourceSkill}:Lv{def.unlockLevel}";
    }

    public float GetCooldownNormalized(string abilityId)
    {
        var def = GetAbilityDefinition(abilityId);
        if (!def || def.cooldown <= 0f)
            return 0f;

        if (!IsOnCooldown(abilityId, out float remaining))
            return 0f;

        return Mathf.Clamp01(remaining / Mathf.Max(0.01f, def.cooldown));
    }

    public bool IsOnGlobalCooldown(out float remainingSeconds)
    {
        remainingSeconds = Mathf.Max(0f, _globalCooldownEndsAt - Time.time);
        return remainingSeconds > 0f;
    }

    public float GetGlobalCooldownNormalized()
    {
        if (globalCooldownSeconds <= 0f)
            return 0f;

        if (!IsOnGlobalCooldown(out float remaining))
            return 0f;

        return Mathf.Clamp01(remaining / Mathf.Max(0.01f, globalCooldownSeconds));
    }

    /// <param name="allowSoulforgedRecastWhileActive">
    /// When false (e.g. idle auto-abilities), an active Soulforged Weapon minion does not receive recast/retarget — use fails so other bar abilities can run.
    /// Manual bar use keeps default true (player can recast while the summon is up).
    /// </param>
    public bool TryUseAbility(
        string abilityId,
        bool showLockedFeedback = true,
        bool allowSoulforgedRecastWhileActive = true,
        bool requireCrescentSlashTargetInFacingLane = false)
    {
        AbilityDefinition def = GetAbilityDefinition(abilityId);
        if (!def)
            return false;

        if (!IsAbilityAllowedBySkillProgress(def))
        {
            if (showLockedFeedback && player)
                player.ShowPopup("Ability not available.");
            return false;
        }

        if (!player || !stats)
            return false;

        if (player.IsDead || stats.IsDead)
            return false;

        if (!CanUseWithEquippedWeapon(def))
        {
            player.ShowPopup("Ability cant be used with this weapon");
            return false;
        }

        if (globalCooldownSeconds > 0f && Time.time < _globalCooldownEndsAt)
            return false;

        CleanupSoulforgedWeaponList();
        if (def.minionSpawnDefinition && _activeSoulforgedWeaponMinions.Count > 0)
        {
            if (!allowSoulforgedRecastWhileActive)
                return false;

            RecastActiveSoulforgedWeapons();
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }

        if (IsOnCooldown(def.abilityId, out _))
            return false;

        // Lumber Frenzy: block recast while the buff is still active; cooldown
        // does not begin until the buff expires.
        if (string.Equals(def.abilityId, LumberFrenzyId, StringComparison.OrdinalIgnoreCase) && _lumberFrenzyActive)
            return false;

        // Cleaving Chop: same deferred-cooldown contract as Lumber Frenzy.
        if (string.Equals(def.abilityId, CleavingChopId, StringComparison.OrdinalIgnoreCase) && _cleavingChopActive)
            return false;

        // Spectral Axe: deferred cooldown starts when the projectile returns. Block recast while deployed.
        if (string.Equals(def.abilityId, SpectralAxeId, StringComparison.OrdinalIgnoreCase) && _spectralAxeActive)
            return false;

        if (string.Equals(def.abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
        {
            if (_powerSlashQueued)
                return false;
        }
        if (string.Equals(def.abilityId, RendId, StringComparison.OrdinalIgnoreCase))
        {
            if (_rendQueued)
                return false;
        }
        if (string.Equals(def.abilityId, EnvenomId, StringComparison.OrdinalIgnoreCase))
        {
            if (_envenomQueued)
                return false;
        }
        if (string.Equals(def.abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase))
        {
            if (_crescentSlashQueued)
                return false;
        }

        bool isCrescentSlash = string.Equals(def.abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase);
        if (!isCrescentSlash && def.energyCost > 0f && !player.SpendEnergy(def.energyCost))
        {
            player.ShowPopup("Not enough energy.");
            return false;
        }

        // Summon abilities: no current-target requirement (unlike the generic instant-hit block below).
        if (def.minionSpawnDefinition)
        {
            if (!def.minionSpawnDefinition.runtimePrefab)
            {
                player.AddEnergy(def.energyCost);
                return false;
            }

            if (!TrySpawnSoulforgedWeaponMinion(def))
            {
                player.AddEnergy(def.energyCost);
                return false;
            }

            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }

        if (string.Equals(def.abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
        {
            if (_powerSlashQueued)
                return false;

            _powerSlashQueued = true;
            float powerSlashAnyTypeBonus = GetPowerSlashAnyTypeMultiplierBonus();
            float allTypeCombo = def.physicalDamageMultiplier + powerSlashAnyTypeBonus;
            float allTypeEff = allTypeCombo <= 0f ? 1f : allTypeCombo;
            _queuedPowerSlashPhysicalMultiplier = allTypeEff;
            _queuedPowerSlashMagicMultiplier = allTypeEff;
            _queuedPowerSlashCorruptionMultiplier = allTypeEff;
            _queuedPowerSlashAllDamageMultiplier = def.GetEffectiveAllDamageMultiplier();
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }

        if (string.Equals(def.abilityId, RendId, StringComparison.OrdinalIgnoreCase))
        {
            if (_rendQueued)
                return false;
            _rendQueued = true;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }

        if (string.Equals(def.abilityId, EnvenomId, StringComparison.OrdinalIgnoreCase))
        {
            if (_envenomQueued)
                return false;
            _envenomQueued = true;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }
        if (string.Equals(def.abilityId, CleavingStrikesId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateCleavingStrikesBuff();
            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }
        if (string.Equals(def.abilityId, LumberFrenzyId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateLumberFrenzyBuff();
            // Cooldown is deferred to start when the buff expires (see CleanupLumberFrenzyIfExpired).
            _lumberFrenzyCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }
        if (string.Equals(def.abilityId, CleavingChopId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateCleavingChopBuff();
            _cleavingChopCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }
        if (string.Equals(def.abilityId, SpectralAxeId, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryActivateSpectralAxe(def))
                return false;
            _spectralAxeCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }
        if (isCrescentSlash)
        {
            if (requireCrescentSlashTargetInFacingLane && !CanHitAnyEnemyWithCrescentSlash())
                return false;

            bool castNow = combat != null && combat.TryConsumeAttackCycleForAbilityCast();
            if (castNow)
            {
                if (!ExecuteCrescentSlashCast(def, requireCrescentSlashTargetInFacingLane))
                    return false;
            }
            else
            {
                // Cadence is still cooling down: queue like Power Slash and fire on next eligible swing.
                _crescentSlashQueued = true;
            }

            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }

        if (string.Equals(def.abilityId, WhirlwindId, StringComparison.OrdinalIgnoreCase))
        {
            bool usedWhirl = TryUseWhirlwind(def);
            if (!usedWhirl)
                return false;

            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }

        EnemyBaseController target = combat != null ? combat.CurrentTarget : null;
        if (target == null || target.IsDead)
            return false;

        // Instant-cast damage model: physical + magic weapon averages, optional element lines, AP, small ailment hook.
        float basePhysical =
            (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        float baseMagic =
            (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f;

        float baseCorruption =
            (Mathf.Max(0f, stats.MinSplitDamage.corruptionDamage) + Mathf.Max(0f, stats.MaxSplitDamage.corruptionDamage)) * 0.5f;
        float allM = def.GetEffectiveAllDamageMultiplier();
        float pM = def.GetPhysicalHitScalingMultiplier();
        float mM = def.GetMagicHitScalingMultiplier();
        float cM = def.GetCorruptionHitScalingMultiplier();
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apM = stats.GetAbilityPowerDamageMultiplier(AbilityDefinition.StandardAbilityPowerCoefficient);
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
        float physLine = basePhysical * pM + ailmentBonus;
        float magLine = baseMagic * mM * elemM + elementBonus * elemM;
        float physPart = physLine * allM * apM;
        float magPart = magLine * allM * apM;
        float corrPart = (baseCorruption * cM) * allM * apM;
        float raw = Mathf.Max(0f, physPart + magPart + corrPart);

        bool wasCrit = false;
        float critMult = 1f;
        if (raw > 0f && UnityEngine.Random.value < Mathf.Clamp01(stats.CritChance))
        {
            wasCrit = true;
            critMult = Mathf.Max(1f, stats.CritMultiplier);
        }

        int phys = Mathf.Max(0, Mathf.RoundToInt(physPart * critMult));
        int mag = Mathf.Max(0, Mathf.RoundToInt(magPart * critMult));
        int corr = Mathf.Max(0, Mathf.RoundToInt(corrPart * critMult));
        int dealt = 0;
        if (phys > 0)
            dealt += target.TakeDamage(phys, DamageType.Physical, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null);
        if (mag > 0)
            dealt += target.TakeDamage(mag, DamageType.Magic, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null);
        if (corr > 0)
            dealt += target.TakeDamage(corr, DamageType.Corruption, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null);

        // Fire the attack anim as feedback, but do not modify basic attack cooldown timing.
        player.TriggerAttackAnim();
        StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        return true;
    }

    private struct DealtHit
    {
        public float physical;
        public float magic;
        public float corruptionDamage;
        /// <summary>Fraction of pre-mitigation magic in the hit that is lightning (weapon split + lightning attack-type line + Crescent conversion).</summary>
        public float meleeMagicLightningFraction;
        public float Total => physical + magic + corruptionDamage;
    }

    /// <summary>Builds one independent ability hit roll (per target): attack roll + ability scaling + independent crit.</summary>
    private void BuildWhirlwindAbilityScaledSplit(AbilityDefinition def, out SplitDamage nonCritBase, out bool wasCrit, out float lightningMagicNonCrit)
    {
        SplitDamage baseRolled = stats.RollSplitAttackDamage(out bool baseWasCrit);
        float critMult = Mathf.Max(1f, stats.CritMultiplier);
        if (baseWasCrit && critMult > 1f)
        {
            baseRolled.physical /= critMult;
            baseRolled.magic /= critMult;
            // corruption damage is not crit-scaled on the base roll.
        }

        float basePhysical = Mathf.Max(0f, baseRolled.physical);
        float baseMagic = Mathf.Max(0f, baseRolled.magic);
        float baseCorruption = Mathf.Max(0f, baseRolled.corruptionDamage);

        float allM = def.GetEffectiveAllDamageMultiplier();
        float pM = def.GetPhysicalHitScalingMultiplier();
        float mM = def.GetMagicHitScalingMultiplier();
        float cM = def.GetCorruptionHitScalingMultiplier();
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apM = stats.GetAbilityPowerDamageMultiplier(AbilityDefinition.StandardAbilityPowerCoefficient);
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);

        float physLine = basePhysical * pM + ailmentBonus;
        float magLine = baseMagic * mM * elemM + elementBonus * elemM;
        float physPart = physLine * allM * apM;
        float magPart = magLine * allM * apM;
        float corruptionPart = (baseCorruption * cM) * allM * apM;

        float fWeaponLightning = stats.GetMeleeMagicLightningFraction();
        float weaponLightMag = baseMagic * mM * elemM * allM * apM * fWeaponLightning;
        float elemLightMag = stats.CurrentMagicAttackType == MagicAttackType.Lightning ? elementBonus * elemM * allM * apM : 0f;
        lightningMagicNonCrit = weaponLightMag + elemLightMag;

        nonCritBase = new SplitDamage(physPart, magPart, corruptionPart);

        wasCrit = false;
        float raw = physPart + magPart + corruptionPart;
        if (raw > 0f && UnityEngine.Random.value < Mathf.Clamp01(stats.CritChance))
            wasCrit = true;
    }

    private bool TryUseWhirlwind(AbilityDefinition def)
    {
        if (stats == null)
            return false;

        int selectedChoice = GetWhirlwindSelectedChoice();
        // Twin Cyclone (Lv18 choice index 0): second wave only when that upgrade is committed — not by default.
        bool twinCyclone = selectedChoice == 0;
        bool expansiveWhirl = selectedChoice == 1;

        float baseWeaponRange = GetWhirlwindHitRadius();
        float radius = baseWeaponRange + (expansiveWhirl ? WhirlwindRadiusBonus : 0f);

        EnemyBaseController[] allEnemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<EnemyBaseController> targets = new List<EnemyBaseController>(allEnemies.Length);
        float ownerX = transform.position.x;
        float ownerHalf = GetOwnerHalfWidthX();
        for (int i = 0; i < allEnemies.Length; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;
            bool inRange = IsEnemyWithinWhirlRange(enemy, radius, ownerX, ownerHalf, out _);
            if (inRange)
                targets.Add(enemy);
        }

        player?.TriggerAttackAnimVisualOnly();
        SpawnWhirlwindVfx(radius);

        if (targets.Count <= 0)
            return true; // ability cast still consumes resources/cooldown.

        List<(EnemyBaseController target, SplitDamage secondHitBase, float secondLightningMagNonCrit)> secondWaveTargets =
            twinCyclone ? new List<(EnemyBaseController, SplitDamage, float)>(targets.Count) : null;

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyBaseController target = targets[i];
            if (!target || target.IsDead)
                continue;

            BuildWhirlwindAbilityScaledSplit(def, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
            float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
            SplitDamage rolled = new SplitDamage(
                rolledNonCrit.physical * critMult,
                rolledNonCrit.magic * critMult,
                rolledNonCrit.corruptionDamage * critMult);
            SplitDamage firstHit = rolled;

            float lightningAfterCrit = lightningMagNonCrit * critMult;
            float firstFrac = firstHit.magic > 1e-8f ? Mathf.Clamp01(lightningAfterCrit / firstHit.magic) : 0f;

            DealtHit dealt = ApplySplitDamageToEnemy(target, firstHit, wasCrit, firstFrac);
            ApplyOnHitEffects(target, dealt);
            if (twinCyclone && secondWaveTargets != null)
            {
                SplitDamage secondHitBase = rolledNonCrit * WhirlwindSecondHitMultiplier;
                float secondLightningMagNonCrit = lightningMagNonCrit * WhirlwindSecondHitMultiplier;
                secondWaveTargets.Add((target, secondHitBase, secondLightningMagNonCrit));
            }
        }

        if (twinCyclone && secondWaveTargets != null && secondWaveTargets.Count > 0)
            StartCoroutine(ApplyTwinCycloneSecondWave(secondWaveTargets, radius));

        return true;
    }

    private void TryUseCrescentSlash(AbilityDefinition def)
    {
        if (stats == null)
            return;

        int selected = GetCrescentSlashSelectedChoice();
        bool elementalCrescent = selected == 0;
        bool penetrating = selected == 1;

        float reach = GetWhirlwindBaseRange() + 6f;
        SpawnCrescentSlashVfx(reach);

        List<(EnemyBaseController enemy, float dist)> forwardHits = CollectCrescentSlashForwardHits(reach);

        forwardHits.Sort((a, b) => a.dist.CompareTo(b.dist));
        int cap = penetrating ? forwardHits.Count : Mathf.Min(3, forwardHits.Count);
        for (int i = 0; i < cap; i++)
        {
            EnemyBaseController target = forwardHits[i].enemy;
            if (!target || target.IsDead)
                continue;

            ApplyCrescentSlashSingleTargetHit(target, def, elementalCrescent);
        }
    }

    private bool CanHitAnyEnemyWithCrescentSlash()
    {
        float reach = GetWhirlwindBaseRange() + 6f;
        List<(EnemyBaseController enemy, float dist)> forwardHits = CollectCrescentSlashForwardHits(reach);
        return forwardHits.Count > 0;
    }

    private List<(EnemyBaseController enemy, float dist)> CollectCrescentSlashForwardHits(float reach)
    {
        EnemyBaseController[] allEnemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<(EnemyBaseController enemy, float dist)> forwardHits = new List<(EnemyBaseController enemy, float dist)>(allEnemies.Length);
        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        float laneWidth = Mathf.Max(0.6f, reach * 0.35f);

        for (int i = 0; i < allEnemies.Length; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            Vector3 to = enemy.transform.position - origin;
            float forwardDist = to.x * facing;
            if (forwardDist <= 0f || forwardDist > reach)
                continue;
            if (Mathf.Abs(to.y) > laneWidth)
                continue;

            forwardHits.Add((enemy, forwardDist));
        }

        return forwardHits;
    }

    /// <summary>
    /// One Crescent Slash damage packet (ability scaling + optional Elemental Crescent conversion). Also used when Crescent is consumed on a melee swing.
    /// </summary>
    private void ApplyCrescentSlashSingleTargetHit(EnemyBaseController target, AbilityDefinition def, bool elementalCrescent)
    {
        if (target == null || target.IsDead || stats == null || def == null)
            return;

        BuildWhirlwindAbilityScaledSplit(def, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        SplitDamage hitForTarget = new SplitDamage(
            rolledNonCrit.physical * critMult,
            rolledNonCrit.magic * critMult,
            rolledNonCrit.corruptionDamage * critMult);
        float lightningMag = lightningMagNonCrit * critMult;
        CrescentConvertedElement convertedElement = CrescentConvertedElement.Lightning;
        float convertedDamage = 0f;
        bool hasConvertedDamage = elementalCrescent &&
                                  TryApplyElementalConversionForCrescent(ref hitForTarget, out convertedElement, out convertedDamage);

        if (hasConvertedDamage && convertedElement == CrescentConvertedElement.Lightning)
            lightningMag += convertedDamage;

        float crescentFrac = hitForTarget.magic > 1e-8f ? Mathf.Clamp01(lightningMag / hitForTarget.magic) : 0f;

        DealtHit dealt = ApplySplitDamageToEnemy(target, hitForTarget, wasCrit, crescentFrac);
        ApplyOnHitEffects(target, dealt);
        if (hasConvertedDamage)
            ApplyElementalAilmentForCrescent(target, convertedElement, convertedDamage);

        if (player != null && dealt.Total > 0f)
            player.ApplyLifeSteal(dealt.Total);
    }

    /// <summary>
    /// Extra Crescent Slash hit on a target after the normal melee swing damage (queued Crescent consumption).
    /// </summary>
    public void ApplyMeleeQueuedCrescentSlashExtraHit(EnemyBaseController target, bool elementalCrescent)
    {
        if (target == null || target.IsDead || stats == null)
            return;

        AbilityDefinition def = GetAbilityDefinition(CrescentSlashId);
        if (!def)
            return;

        ApplyCrescentSlashSingleTargetHit(target, def, elementalCrescent);
    }

    /// <summary>
    /// Convert 50% of physical into magic as the final split step for this hit.
    /// Each call rolls its own element so multi-target hits can differ per enemy.
    /// </summary>
    private static bool TryApplyElementalConversionForCrescent(
        ref SplitDamage hit,
        out CrescentConvertedElement element,
        out float convertedDamage)
    {
        element = CrescentConvertedElement.Lightning;
        convertedDamage = 0f;

        float physical = Mathf.Max(0f, hit.physical);
        if (physical <= 0f)
            return false;

        convertedDamage = physical * 0.5f;
        hit.physical = Mathf.Max(0f, physical - convertedDamage);
        hit.magic = Mathf.Max(0f, hit.magic) + convertedDamage;

        int roll = UnityEngine.Random.Range(0, 3);
        element = roll switch
        {
            0 => CrescentConvertedElement.Fire,
            1 => CrescentConvertedElement.Ice,
            _ => CrescentConvertedElement.Lightning
        };
        return true;
    }

    private void ApplyElementalAilmentForCrescent(EnemyBaseController target, CrescentConvertedElement element, float convertedDamage)
    {
        if (target == null || stats == null)
            return;
        if (convertedDamage <= 0f)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        switch (element)
        {
            case CrescentConvertedElement.Fire:
                ailments.TryApplyBurnFromFireHit(
                    convertedDamage,
                    1f,
                    stats.BurnExplosionMultiplier,
                    transform);
                break;
            case CrescentConvertedElement.Ice:
                ailments.ApplyChillFromHit(new ChillPayload(
                    duration: stats.ChillDuration,
                    maxStacks: stats.ChillMaxStacks,
                    slowPerStack: stats.ChillSlowPerStack,
                    source: transform
                ));
                break;
            case CrescentConvertedElement.Lightning:
            default:
                ailments.ApplyShockFromHit(new ShockPayload(
                    duration: stats.ShockDuration,
                    damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
                    source: transform
                ));
                break;
        }
    }

    private IEnumerator ApplyTwinCycloneSecondWave(List<(EnemyBaseController target, SplitDamage secondHitBase, float secondLightningMagNonCrit)> targets, float radius)
    {
        yield return new WaitForSeconds(WhirlwindTwinCycloneSecondHitDelay);

        // Replay only the Whirlwind VFX; do not retrigger the attack animation on second wave.
        SpawnWhirlwindVfx(radius);

        if (targets == null || targets.Count == 0)
            yield break;

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyBaseController target = targets[i].target;
            if (!target || target.IsDead)
                continue;

            SplitDamage secondHit = targets[i].secondHitBase;
            float secondLightningBase = targets[i].secondLightningMagNonCrit;
            bool secondWasCrit = TryRollIndependentCrit(ref secondHit);
            float crit2 = secondWasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
            float lightningSecond = secondLightningBase * crit2;
            float secondFrac = secondHit.magic > 1e-8f ? Mathf.Clamp01(lightningSecond / secondHit.magic) : 0f;
            DealtHit dealtSecond = ApplySplitDamageToEnemy(target, secondHit, secondWasCrit, secondFrac);
            ApplyOnHitEffects(target, dealtSecond); // Re-triggers on-hit effects.
        }
    }

    private float GetOwnerHalfWidthX()
    {
        Collider2D c = player != null ? player.GetComponent<Collider2D>() : GetComponent<Collider2D>();
        if (c == null)
            c = GetComponentInChildren<Collider2D>();
        return c != null ? Mathf.Max(0f, c.bounds.extents.x) : 0f;
    }

    /// <summary>Whirlwind AoE radius: same basis as cleaving secondaries (max(3, stats.Range) + padding).</summary>
    private float GetWhirlwindHitRadius()
    {
        const float cleavingMinWeaponRange = 3f;
        float weaponRange = stats != null ? Mathf.Max(0f, stats.Range) : 0f;
        float pad = combat != null ? combat.GetMeleeRangePadding() : 0.05f;
        return Mathf.Max(cleavingMinWeaponRange, weaponRange) + pad;
    }

    private float GetWhirlwindBaseRange()
    {
        float best = stats != null ? Mathf.Max(0f, stats.Range) : 0f;

        if (equipment == null)
            equipment = GetComponent<EquipmentManager>();
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (equipment != null && inventory != null && !string.IsNullOrWhiteSpace(equipment.MainHandItemId))
        {
            ItemDefinition main = inventory.GetItemDef(equipment.MainHandItemId);
            if (main != null && main.IsWeapon)
                best = Mathf.Max(best, Mathf.Max(0f, main.AttackRange));
        }

        // If no valid range could be resolved, keep a minimal sane fallback.
        return Mathf.Max(0.1f, best);
    }

    private int GetWhirlwindSelectedChoice()
    {
        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            return -1;

        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, "Lv15_0", -1);
        if (selected >= 0)
            return selected;

        // Legacy level-only keys (pre spine-disambiguation).
        selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, WhirlwindChoiceSourceLevel, -1);
        if (selected >= 0)
            return selected;

        selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, WhirlwindChoiceSourceLevel + 3, -1);
        return selected;
    }

    private static bool IsEnemyWithinWhirlRange(EnemyBaseController enemy, float radius, float ownerX, float ownerHalf, out float edgeGapX)
    {
        edgeGapX = float.PositiveInfinity;
        if (enemy == null)
            return false;

        Collider2D enemyCol = enemy.GetComponent<Collider2D>();
        if (enemyCol == null)
            enemyCol = enemy.GetComponentInChildren<Collider2D>();
        float enemyHalf = enemyCol != null ? Mathf.Max(0f, enemyCol.bounds.extents.x) : 0f;
        float centerDistX = Mathf.Abs(enemy.transform.position.x - ownerX);
        edgeGapX = centerDistX - (ownerHalf + enemyHalf);
        return edgeGapX <= Mathf.Max(0f, radius);
    }

    private DealtHit ApplySplitDamageToEnemy(EnemyBaseController target, SplitDamage hit, bool wasCrit, float meleeMagicLightningFraction = -1f)
    {
        DealtHit result = default;
        if (target == null || target.IsDead)
            return result;

        if (meleeMagicLightningFraction < 0f)
            meleeMagicLightningFraction = stats != null ? stats.GetMeleeMagicLightningFraction() : 0f;
        result.meleeMagicLightningFraction = Mathf.Clamp01(meleeMagicLightningFraction);

        float cond = GetConditionalMeleeDamageMultiplier(target);
        float phys = Mathf.Max(0f, hit.physical * cond);
        float mag = Mathf.Max(0f, hit.magic * cond);
        float corrRaw = Mathf.Max(0f, hit.corruptionDamage * cond);

        if (phys > 0f)
            result.physical = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(phys), DamageType.Physical, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null));
        if (mag > 0f)
            result.magic = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(mag), DamageType.Magic, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null));
        if (corrRaw > 0f)
            result.corruptionDamage = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(corrRaw), DamageType.Corruption, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null));

        return result;
    }

    private float GetConditionalMeleeDamageMultiplier(EnemyBaseController target)
    {
        if (stats == null || target == null)
            return 1f;

        float bonus = 0f;
        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments != null)
        {
            if (ailments.HasBleed) bonus += stats.MeleeDamageVsBleeding;
            if (ailments.HasPoison) bonus += stats.MeleeDamageVsPoisoned;
            if (ailments.HasShock) bonus += stats.MeleeDamageVsShocked;
        }

        CharacterStats targetStats = target.GetComponent<CharacterStats>();
        if (targetStats != null && targetStats.MaxHP > 0f)
        {
            float hp01 = targetStats.HP / Mathf.Max(1f, targetStats.MaxHP);
            if (hp01 <= stats.MeleeLowHpThreshold01)
                bonus += stats.MeleeDamageVsLowHp;
        }

        return 1f + Mathf.Max(0f, bonus);
    }

    private bool TryRollIndependentCrit(ref SplitDamage hit)
    {
        if (stats == null)
            return false;
        if (hit.physical <= 0f && hit.magic <= 0f)
            return false;

        float critChance = Mathf.Clamp01(stats.CritChance);
        if (UnityEngine.Random.value > critChance)
            return false;

        float critMult = Mathf.Max(1f, stats.CritMultiplier);
        hit.physical *= critMult;
        hit.magic *= critMult;
        return true;
    }

    private void ApplyOnHitEffects(EnemyBaseController target, DealtHit dealt)
    {
        if (target == null || stats == null)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        if (dealt.physical > 0f && stats.BleedChance > 0f && UnityEngine.Random.value <= stats.BleedChance)
        {
            float duration = Mathf.Max(1f, stats.BleedDuration);
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
            float baseDuration = Mathf.Max(1f, stats.BleedBaseDuration);
            float bleedTickDamage = dealt.physical * (1f + stats.BleedMultiplier) / baseDuration;
            if (bleedTickDamage > 0f)
            {
                float totalBleedDamage = bleedTickDamage * ticks;
                ailments.ApplyBleedFromHit(new BleedPayload(totalBleedDamage, duration, ticks, transform));
            }
        }

        if (dealt.corruptionDamage > 0f && stats.PoisonChance > 0f && stats.PoisonMultiplier >= 0f && UnityEngine.Random.value <= stats.PoisonChance)
        {
            float totalPoisonDamage =
                dealt.corruptionDamage * stats.PoisonPoolFractionOfCorruptionDamage * (1f + stats.PoisonMultiplier);
            if (totalPoisonDamage > 0f)
            {
                float duration = Mathf.Max(0.1f, stats.PoisonDuration);
                int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
                int maxStacks = Mathf.Max(1, stats.PoisonMaxStacks);
                ailments.ApplyPoisonFromHit(new PoisonPayload(totalPoisonDamage, duration, ticks, maxStacks, transform));
            }
        }

        if (dealt.magic > 0f &&
            dealt.meleeMagicLightningFraction > 1e-5f &&
            stats.MeleeShockChance > 0f &&
            UnityEngine.Random.value <= stats.MeleeShockChance)
        {
            ailments.ApplyShockFromHit(new ShockPayload(
                duration: stats.ShockDuration,
                damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
                source: transform));
        }
    }

    private void SpawnWhirlwindVfx(float radius)
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
            }
        );
        trail.colorGradient = gradient;

        Vector2 startDir = ((Vector2)anchor.position - (Vector2)center.position).normalized;
        if (startDir.sqrMagnitude <= 0.0001f)
            startDir = Vector2.right * ((player != null && player.transform.localScale.x < 0f) ? -1f : 1f);

        StartCoroutine(AnimateWhirlwindTrail(orbitGO.transform, trail, center, radius, startDir));
    }

    private void SpawnCrescentSlashVfx(float reach)
    {
        Transform center = player != null ? player.transform : transform;
        if (center == null)
            return;

        Vector3 startPos = center.position + crescentSlashCenterOffset;
        float facing = GetCombatFacingSign();
        Vector3 dir = Vector3.right * facing;
        // Main wave + two quick echoes for a fuller slash-wave look.
        StartCoroutine(SpawnProjectedCrescentWaveAfterDelay(startPos, dir, reach, 0f, 1f, 1f, 25));
        StartCoroutine(SpawnProjectedCrescentWaveAfterDelay(startPos, dir, reach, 0.045f, 0.92f, 0.62f, 24));
        StartCoroutine(SpawnProjectedCrescentWaveAfterDelay(startPos, dir, reach, 0.09f, 0.84f, 0.38f, 23));
    }

    private IEnumerator SpawnProjectedCrescentWaveAfterDelay(
        Vector3 startPos,
        Vector3 direction,
        float reach,
        float delay,
        float reachScale,
        float alphaScale,
        int sortingOrder)
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
        line.sortingOrder = sortingOrder;

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
            Vector3 center = Vector3.Lerp(startPos, endPos, t);
            for (int i = 0; i < line.positionCount; i++)
            {
                float pt = i / Mathf.Max(1f, line.positionCount - 1f);
                float deg = Mathf.Lerp(startDeg, endDeg, pt);
                float rad = deg * Mathf.Deg2Rad;
                Vector3 local = new Vector3(
                    Mathf.Cos(rad) * visualRadius * Mathf.Sign(direction.x == 0f ? 1f : direction.x),
                    Mathf.Sin(rad) * visualRadius,
                    0f);
                line.SetPosition(i, center + local);
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

        // Use effective attack range as the orbit size (includes +3 when Expansive is active).
        // Keep the OUTER edge aligned to that range.
        float width = Mathf.Max(0.01f, trail != null ? trail.widthMultiplier : whirlingBladeLineWidth);
        // Use exact effective skill range for the trail orbit.
        float visualRadius = Mathf.Max(0.05f, radius - (width * 0.5f));
        float spinScale = Mathf.Clamp(Mathf.Abs(whirlingBladeSpinDegrees) / 720f, 0.25f, 2.5f);
        while (elapsed < duration && emitter != null && center != null)
        {
            float t = elapsed / duration;
            float x;
            float y;
            if (t < 0.5f)
            {
                // Pass 1: +X -> -X while dipping slightly down.
                float p = t / 0.5f;
                x = Mathf.Lerp(visualRadius, -visualRadius, p);
                y = Mathf.Lerp(0f, -whirlingBladeUpwardDrift, p);
                y += Mathf.Sin(p * Mathf.PI) * whirlingBladeVerticalWave * spinScale; // arc
            }
            else
            {
                // Pass 2: -X -> near +X while rising slightly up.
                float p = (t - 0.5f) / 0.5f;
                x = Mathf.Lerp(-visualRadius, visualRadius * 0.92f, p);
                y = Mathf.Lerp(-whirlingBladeUpwardDrift, whirlingBladeUpwardDrift * 0.35f, p);
                y += Mathf.Sin(p * Mathf.PI) * (whirlingBladeVerticalWave * 0.65f) * spinScale; // softer return arc
            }

            // Respect initial facing from anchor direction.
            x *= Mathf.Sign(startDir.x == 0f ? 1f : startDir.x);
            emitter.position = center.position + whirlingBladeCenterOffset + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (trail != null)
            trail.emitting = false;
        if (emitter != null)
            Destroy(emitter.gameObject, Mathf.Max(0.04f, whirlingBladeDuration * 0.6f));
    }

    public bool TryConsumeQueuedAttackModifier(ref SplitDamage rolled)
    {
        if (rolled.IsEmpty)
            return false;

        if (_powerSlashQueued)
        {
            _powerSlashQueued = false;
            _queuedConsumedThisHit = QueuedHitEffect.PowerSlash;
            _queuedConsumedFrame = Time.frameCount;

            float apM = stats != null
                ? stats.GetAbilityPowerDamageMultiplier(AbilityDefinition.StandardAbilityPowerCoefficient)
                : 1f;
            float allM = _queuedPowerSlashAllDamageMultiplier;
            AbilityDefinition slashDef = GetAbilityDefinition(PowerSlashId);
            float elementBonus = slashDef != null && stats != null ? AbilityElementScaling.GetElementDamageBonus(slashDef, stats) : 0f;
            float ailmentBonus = slashDef != null && stats != null ? AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(slashDef, stats) : 0f;

            // Mult scales the rolled basic hit (150% = 1.5× that swing), not an extra additive copy of it.
            rolled.physical = (rolled.physical * _queuedPowerSlashPhysicalMultiplier + ailmentBonus) * apM * allM;
            rolled.magic = (rolled.magic * _queuedPowerSlashMagicMultiplier + elementBonus) * apM * allM;
            rolled.corruptionDamage = (rolled.corruptionDamage * _queuedPowerSlashCorruptionMultiplier) * apM * allM;
            rolled.physical = Mathf.Max(0f, rolled.physical);
            rolled.magic = Mathf.Max(0f, rolled.magic);

            AbilityDefinition def = GetAbilityDefinition(PowerSlashId);
            if (def)
                StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;

            SpawnPowerSlashTrail();
            return true;
        }

        if (_rendQueued)
        {
            _rendQueued = false;
            _queuedConsumedThisHit = QueuedHitEffect.Rend;
            _queuedConsumedFrame = Time.frameCount;
            return true;
        }

        if (_envenomQueued)
        {
            _envenomQueued = false;
            _queuedConsumedThisHit = QueuedHitEffect.Envenom;
            _queuedConsumedFrame = Time.frameCount;
            return true;
        }

        return false;
    }

    private void TryAutoReleaseQueuedCrescentSlash()
    {
        if (!_crescentSlashQueued)
            return;
        if (!CanHitAnyEnemyWithCrescentSlash())
            return;
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null || !combat.TryConsumeAttackCycleForAbilityCast())
            return;

        AbilityDefinition def = GetAbilityDefinition(CrescentSlashId);
        if (!def)
        {
            _crescentSlashQueued = false;
            return;
        }

        ExecuteCrescentSlashCast(def, requireTargetInFacingLane: true);
    }

    /// <summary>
    /// Called from combat cadence; guarantees queued Crescent Slash gets priority over normal auto attacks.
    /// </summary>
    public bool TryAutoReleaseQueuedCrescentSlashFromCadence()
    {
        if (!_crescentSlashQueued)
            return false;
        if (!CanHitAnyEnemyWithCrescentSlash())
            return false;
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null || !combat.TryConsumeAttackCycleForAbilityCast())
            return false;

        AbilityDefinition def = GetAbilityDefinition(CrescentSlashId);
        if (!def)
        {
            _crescentSlashQueued = false;
            return false;
        }

        return ExecuteCrescentSlashCast(def, requireTargetInFacingLane: true);
    }

    private bool ExecuteCrescentSlashCast(AbilityDefinition def, bool requireTargetInFacingLane = false)
    {
        if (!def || player == null)
            return false;
        if (requireTargetInFacingLane && !CanHitAnyEnemyWithCrescentSlash())
            return false;
        if (def.energyCost > 0f && !player.SpendEnergy(def.energyCost))
            return false;

        _crescentSlashQueued = false;
        TryUseCrescentSlash(def);
        player?.TriggerAttackAnim();
        StartCooldown(def);
        return true;
    }

    private float GetCombatFacingSign()
    {
        // Player visuals are flipped on visualsRoot, not necessarily on player transform.
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
    /// Called by <see cref="PlayerCombatController"/> after a hit lands, to apply queued on-hit logic that needs the target.
    /// Returns suppression flags for the default bleed/poison application.
    /// </summary>
    public QueuedHitEffectResult ConsumeQueuedHitEffects(EnemyBaseController target, float physicalDealt, float corruptionDealtPostMitigation)
    {
        QueuedHitEffectResult result = default;
        if (target == null || target.IsDead)
            return result;

        // Melee resolves damage on the same frame as TryConsumeQueuedAttackModifier; ranged/projectile hits
        // often land later, so Rend/Envenom must not require the same frame.
        bool requiresSameFrameAsConsume =
            _queuedConsumedThisHit != QueuedHitEffect.Rend && _queuedConsumedThisHit != QueuedHitEffect.Envenom;
        if (requiresSameFrameAsConsume && _queuedConsumedFrame != Time.frameCount)
            return result;

        if (_queuedConsumedThisHit == QueuedHitEffect.Rend)
        {
            result.suppressDefaultBleed = true;
            TryApplyRendBleed(target, physicalDealt);

            AbilityDefinition def = GetAbilityDefinition(RendId);
            if (def)
                StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        }
        else if (_queuedConsumedThisHit == QueuedHitEffect.Envenom)
        {
            result.suppressDefaultPoison = true;
            TryApplyEnvenomPoison(target, corruptionDealtPostMitigation);

            AbilityDefinition def = GetAbilityDefinition(EnvenomId);
            if (def)
                StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        }
        else if (_queuedConsumedThisHit == QueuedHitEffect.CrescentSlash)
        {
            result.triggerCrescentSlash = true;
            int selected = GetCrescentSlashSelectedChoice();
            result.crescentAppliesElemental = selected == 0;
            result.crescentPenetrating = selected == 1;

            AbilityDefinition def = GetAbilityDefinition(CrescentSlashId);
            if (def)
                StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        }

        _queuedConsumedThisHit = QueuedHitEffect.None;
        _queuedConsumedFrame = -1;
        return result;
    }

    private void TryApplyRendBleed(EnemyBaseController target, float physicalDealt)
    {
        if (stats == null || target == null || target.IsDead)
            return;
        if (physicalDealt <= 0f)
            return;

        float duration = Mathf.Max(1f, stats.BleedDuration) + 3f;
        float baseDuration = Mathf.Max(1f, stats.BleedBaseDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        float baseTickDamage = physicalDealt * (1f + stats.BleedMultiplier) / baseDuration;
        if (baseTickDamage <= 0f)
            return;

        float totalDamage = baseTickDamage * ticks;

        int selected = GetRendSelectedChoice();
        if (selected == 0)
        {
            // Upgrade 1: same total damage in half the duration.
            duration = Mathf.Max(1f, duration * 0.5f);
            ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        }

        var ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        // Exclusive bleed: blocks other bleed applications while active; also does not override normal bleed.
        ailments.ApplyExclusiveBleedFromHit(new BleedPayload(totalDamage, duration, ticks, transform));

        if (selected == 1)
        {
            // Crimson Spread: same bleed to all other enemies in radial range of the struck target.
            var spreadPayload = new BleedPayload(totalDamage, duration, ticks, transform);
            combat?.ApplyBleedToEnemiesInRadialSpread(target, spreadPayload);
        }
    }

    private void TryApplyEnvenomPoison(EnemyBaseController target, float corruptionDealtPostMitigation)
    {
        if (stats == null || target == null || target.IsDead)
            return;

        if (corruptionDealtPostMitigation <= 0f)
            return;

        float perStackTotal =
            corruptionDealtPostMitigation * stats.PoisonPoolFractionOfCorruptionDamage *
            (1f + Mathf.Max(0f, stats.PoisonMultiplier));
        if (perStackTotal <= 0f)
            return;

        int selected = GetEnvenomSelectedChoice();

        float duration = Mathf.Max(0.1f, stats.PoisonDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        int baseMaxStacks = Mathf.Max(1, stats.PoisonMaxStacks);
        int stacksToApply = baseMaxStacks;

        if (selected == 0)
        {
            // Upgrade 1: +2 max poison stacks on hit for 6 seconds.
            duration = 6f;
            ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        }

        var ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        if (selected == 0)
            ailments.GrantTemporaryPoisonMaxStacksBonus(2, 6f);

        stacksToApply = ailments.GetEffectivePoisonMaxStacks(baseMaxStacks);
        var payload = new PoisonPayload(perStackTotal, duration, ticks, baseMaxStacks, transform);
        for (int i = 0; i < stacksToApply; i++)
            ailments.ApplyPoisonFromHit(payload);

        if (selected == 1)
        {
            // Upgrade 2: on death within 6s, poison spreads to all other enemies in radial range of the victim.
            var marker = target.GetComponent<EnvenomSpreadOnDeathMarker>();
            if (marker == null)
                marker = target.gameObject.AddComponent<EnvenomSpreadOnDeathMarker>();
            marker.Arm(payload, expiresAt: Time.time + 6f, combat);
        }
    }

    private void ActivateCleavingStrikesBuff()
    {
        int selected = GetCleavingStrikesSelectedChoice();
        _cleavingBuffActive = true;
        if (selected == 0)
        {
            // Greater Cleave: primary + 2 extra targets per swing (cleave hits use reduced damage).
            _cleavingAdditionalTargets = 2;
            _cleavingHitsRemaining = 3;
            _cleavingBuffDuration = 5f;
            _cleavingBuffEndsAt = Time.time + _cleavingBuffDuration;
        }
        else if (selected == 1)
        {
            // Lasting Momentum
            _cleavingAdditionalTargets = 1;
            _cleavingHitsRemaining = 6;
            _cleavingBuffDuration = 10f;
            _cleavingBuffEndsAt = Time.time + _cleavingBuffDuration;
        }
        else
        {
            // Base (no Lv18 enhancement)
            _cleavingAdditionalTargets = 1;
            _cleavingHitsRemaining = 4;
            _cleavingBuffDuration = 7f;
            _cleavingBuffEndsAt = Time.time + _cleavingBuffDuration;
        }

        _lastSyncedCleavingHudStacks = int.MinValue;
        _lastSyncedCleavingHudEnd = float.NaN;
        SyncCleavingStrikesHudBuff();
    }

    private void CleanupCleavingStrikesIfExpired()
    {
        if (!_cleavingBuffActive)
            return;

        // End only when both duration and hit budget are satisfied: slow weapons can finish all swings after
        // the timer; fast weapons keep cleaving until the timer after spending all hit charges.
        bool hitsConsumed = _cleavingHitsRemaining <= 0;
        bool durationElapsed = Time.time >= _cleavingBuffEndsAt;
        if (hitsConsumed && durationElapsed)
        {
            _cleavingBuffActive = false;
            _cleavingAdditionalTargets = 0;
            _cleavingHitsRemaining = 0;
            _cleavingBuffEndsAt = 0f;
            _cleavingBuffDuration = 0f;
        }
    }

    private void SyncCleavingStrikesHudBuff()
    {
        if (!buffController)
            return;

        if (!_cleavingBuffActive)
        {
            if (!float.IsNaN(_lastSyncedCleavingHudEnd) || _lastSyncedCleavingHudStacks != int.MinValue)
            {
                buffController.ClearHudAbilityBuff(CleavingStrikesId);
                _lastSyncedCleavingHudStacks = int.MinValue;
                _lastSyncedCleavingHudEnd = float.NaN;
            }

            return;
        }

        int displayStacks = Mathf.Max(1, _cleavingHitsRemaining);
        if (_lastSyncedCleavingHudStacks == displayStacks &&
            Mathf.Approximately(_lastSyncedCleavingHudEnd, _cleavingBuffEndsAt))
            return;

        _lastSyncedCleavingHudStacks = displayStacks;
        _lastSyncedCleavingHudEnd = _cleavingBuffEndsAt;
        buffController.SetHudAbilityBuff(CleavingStrikesId, displayStacks, _cleavingBuffEndsAt, _cleavingBuffDuration);
    }

    private void ActivateLumberFrenzyBuff()
    {
        _lumberFrenzyActive = true;
        _lumberFrenzyDuration = LumberFrenzyDurationSeconds;
        _lumberFrenzyEndsAt = Time.time + _lumberFrenzyDuration;
        _lastSyncedLumberFrenzyHudEnd = float.NaN;
        SyncLumberFrenzyHudBuff();
        stats?.NotifyStatsChanged();
    }

    private void CleanupLumberFrenzyIfExpired()
    {
        if (!_lumberFrenzyActive)
            return;
        if (Time.time < _lumberFrenzyEndsAt)
            return;

        _lumberFrenzyActive = false;
        _lumberFrenzyEndsAt = 0f;
        _lumberFrenzyDuration = 0f;

        // Cooldown begins now (not on cast) so the player gets a 60s
        // "downtime" after the 20s buff window finishes.
        if (_lumberFrenzyCooldownAbilityDef)
            StartCooldown(_lumberFrenzyCooldownAbilityDef);
        _lumberFrenzyCooldownAbilityDef = null;

        stats?.NotifyStatsChanged();
    }

    private void SyncLumberFrenzyHudBuff()
    {
        if (!buffController)
            return;

        if (!_lumberFrenzyActive)
        {
            if (!float.IsNaN(_lastSyncedLumberFrenzyHudEnd))
            {
                buffController.ClearHudAbilityBuff(LumberFrenzyId);
                _lastSyncedLumberFrenzyHudEnd = float.NaN;
            }

            return;
        }

        if (Mathf.Approximately(_lastSyncedLumberFrenzyHudEnd, _lumberFrenzyEndsAt))
            return;

        _lastSyncedLumberFrenzyHudEnd = _lumberFrenzyEndsAt;
        buffController.SetHudAbilityBuff(LumberFrenzyId, 1, _lumberFrenzyEndsAt, _lumberFrenzyDuration);
    }

    public bool IsLumberFrenzyActive
    {
        get
        {
            if (!_lumberFrenzyActive)
                return false;
            return Time.time < _lumberFrenzyEndsAt;
        }
    }

    /// <summary>Additive chopping speed bonus from active Lumber Frenzy buff (0 when inactive).</summary>
    public float GetLumberFrenzyChoppingSpeedBonus() =>
        IsLumberFrenzyActive ? LumberFrenzyChoppingSpeedBonus : 0f;

    /// <summary>Additive woodcutting grit chance bonus from active Lumber Frenzy buff, including Iron Grit enhancement (0 when inactive).</summary>
    public float GetLumberFrenzyGritChanceBonus()
    {
        if (!IsLumberFrenzyActive)
            return 0f;

        float bonus = LumberFrenzyGritChanceBonus;
        if (GetLumberFrenzySelectedChoice() == LumberFrenzyExtraGritEnhancementChoiceIndex)
            bonus += LumberFrenzyExtraGritEnhancementBonus;
        return bonus;
    }

    /// <summary>Additive woodcutting stamina efficiency bonus from active Lumber Frenzy buff, only when Sturdy Grip enhancement is selected (0 otherwise).</summary>
    public float GetLumberFrenzyStaminaEfficiencyBonus()
    {
        if (!IsLumberFrenzyActive)
            return 0f;
        return GetLumberFrenzySelectedChoice() == LumberFrenzyStaminaEnhancementChoiceIndex
            ? LumberFrenzyStaminaEfficiencyEnhancementBonus
            : 0f;
    }

    private int GetLumberFrenzySelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, LumberFrenzyChoiceSourceLevel, -1);
    }

    private void ActivateCleavingChopBuff()
    {
        _cleavingChopActive = true;
        _cleavingChopDuration = CleavingChopBaseDurationSeconds + GetCleavingChopProlongedBonusSeconds();
        _cleavingChopEndsAt = Time.time + _cleavingChopDuration;
        _lastSyncedCleavingChopHudEnd = float.NaN;
        SyncCleavingChopHudBuff();
    }

    private void CleanupCleavingChopIfExpired()
    {
        if (!_cleavingChopActive)
            return;
        if (Time.time < _cleavingChopEndsAt)
            return;

        _cleavingChopActive = false;
        _cleavingChopEndsAt = 0f;
        _cleavingChopDuration = 0f;

        // Deferred cooldown: 60s timer starts when the buff window closes so the
        // player gets a real downtime gap rather than ticking down during the buff.
        if (_cleavingChopCooldownAbilityDef)
            StartCooldown(_cleavingChopCooldownAbilityDef);
        _cleavingChopCooldownAbilityDef = null;
    }

    private void SyncCleavingChopHudBuff()
    {
        if (!buffController)
            return;

        if (!_cleavingChopActive)
        {
            if (!float.IsNaN(_lastSyncedCleavingChopHudEnd))
            {
                buffController.ClearHudAbilityBuff(CleavingChopId);
                _lastSyncedCleavingChopHudEnd = float.NaN;
            }

            return;
        }

        if (Mathf.Approximately(_lastSyncedCleavingChopHudEnd, _cleavingChopEndsAt))
            return;

        _lastSyncedCleavingChopHudEnd = _cleavingChopEndsAt;
        buffController.SetHudAbilityBuff(CleavingChopId, 1, _cleavingChopEndsAt, _cleavingChopDuration);
    }

    /// <summary>True while the Cleaving Chop buff window is open (drives the secondary tree gather logic).</summary>
    public bool IsCleavingChopActive
    {
        get
        {
            if (!_cleavingChopActive)
                return false;
            return Time.time < _cleavingChopEndsAt;
        }
    }

    /// <summary>Search radius (world units) for secondary cleaving strikes, including Extended Reach. 0 when inactive.</summary>
    public float GetCleavingChopRange()
    {
        if (!IsCleavingChopActive)
            return 0f;

        float range = CleavingChopBaseRange;
        if (GetCleavingChopSelectedChoice() == CleavingChopExtendedReachChoiceIndex)
            range += CleavingChopExtendedReachRangeBonus;
        return range;
    }

    /// <summary>Yield multiplier applied to each secondary tree's main roll while the buff is active. 0 when inactive.</summary>
    public float GetCleavingChopSecondaryYieldEfficiency() =>
        IsCleavingChopActive ? CleavingChopSecondaryYieldEfficiency : 0f;

    private float GetCleavingChopProlongedBonusSeconds() =>
        GetCleavingChopSelectedChoice() == CleavingChopProlongedCleaveChoiceIndex
            ? CleavingChopProlongedDurationBonusSeconds
            : 0f;

    private int GetCleavingChopSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        // Spine-keyed: Cleaving Chop is the slot-0 ability at Woodcutting Lv25, sharing the row with
        // Spectral Axe (slot 1). Using the legacy int "25" would silently return Spectral Axe's choice
        // when Cleaving Chop has no enhancement picked but Spectral Axe does.
        return skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv25_0", -1);
    }

    /// <summary>
    /// True when the player's "Show screen overlay visuals" setting is ON. Drives whether ability range
    /// gizmos (Cleaving Chop circle, Spectral Axe area, …) render. Gameplay is never gated on this.
    /// </summary>
    private static bool AreAbilityRangeIndicatorsEnabled()
    {
        return !ToggleSettingsStore.Get(ToggleSettingId.DisableScreenOverlayVisuals);
    }

    /// <summary>
    /// Shows/hides and rescales the in-world range circle that follows the player while Cleaving Chop is active.
    /// The indicator is lazily spawned on first activation so no manual prefab wiring is required.
    /// </summary>
    private void UpdateCleavingChopRangeIndicator()
    {
        // "Show screen overlay visuals" master toggle (Settings) suppresses every in-world ability range
        // indicator. The buff itself keeps running — only the visualizer disappears.
        if (!cleavingChopShowRangeIndicator || !AreAbilityRangeIndicatorsEnabled())
        {
            if (_cleavingChopIndicatorRoot != null && _cleavingChopIndicatorRoot.activeSelf)
                _cleavingChopIndicatorRoot.SetActive(false);
            return;
        }

        bool active = IsCleavingChopActive;
        if (!active)
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

        float radius = Mathf.Max(0f, GetCleavingChopRange());
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
        // View alignment guarantees the ribbon faces the camera in both top-down and side-view setups so the
        // circle is never edge-on (invisible) regardless of the player's local Z.
        lr.alignment = LineAlignment.View;
        lr.startWidth = cleavingChopIndicatorLineWidth;
        lr.endWidth = cleavingChopIndicatorLineWidth;
        lr.startColor = cleavingChopIndicatorColor;
        lr.endColor = cleavingChopIndicatorColor;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 0;
        lr.sortingOrder = cleavingChopIndicatorSortingOrder;
        if (!string.IsNullOrWhiteSpace(cleavingChopIndicatorSortingLayer))
            lr.sortingLayerName = cleavingChopIndicatorSortingLayer;

        // Freshly-added LineRenderers come with the legacy "Default-Line" material which renders pink (or
        // invisible) under URP. Always replace with the project-wide Sprites/Default so vertex colors apply.
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

    // -------- Spectral Axe (level 25 row, multi-choice with Cleaving Chop) ----------------------------
    //
    // Cast contract:
    //   • Spawn a blue-tinted clone of the equipped axe sprite at the player.
    //   • Travel forward (player.FacingDirectionX × SpectralAxeProjectDistance), spinning the whole time.
    //   • At the parked destination, scan the axe's own SpectralAxeAreaRadius circle for Woodcutting
    //     trees (collider-edge distance), pick the CLOSEST single tree, and chop it at that tree's own
    //     rate. Only logs are gathered.
    //   • If NO tree is inside that circle on landing, log "No trees for spectral axe to gather from",
    //     despawn the axe immediately, and apply SpectralAxeMissedCastCooldownSeconds instead of the
    //     full ability cooldown.
    //   • Phantom Harvest (choice 0): also rolls bonus + hidden drops via PreviewDrops using the
    //     player's gather state (mirrors the primary tick's bonus rules).
    //   • Cleaving Flight (choice 1): the first tree the axe passes within
    //     SpectralAxeTravelCollisionRadius yields one guaranteed log on the outbound leg and again on
    //     the return leg.
    //   • Cooldown is deferred — it begins when the projectile finishes its return + despawn.

    private bool TryActivateSpectralAxe(AbilityDefinition def)
    {
        if (_spectralAxeActive)
            return false;
        if (!player || !inventory)
            return false;

        // Spectral Axe is the toolbelt axe's spectral twin — no axe in toolbelt means there is
        // nothing to project. Surface a clear activity-log line and bail before any state is mutated
        // so neither the buff timer nor the deferred cooldown start.
        if (!HasAxeInToolbelt())
        {
            GameLog.Add("Must have an axe in toolbelt to use this ability", GameLog.CannotMessageColor);
            return false;
        }

        _spectralAxeActive = true;
        _spectralAxeMissedCast = false;
        _spectralAxeDuration = SpectralAxeBaseDurationSeconds;
        _spectralAxeEndsAt = Time.time + _spectralAxeDuration;
        _spectralAxeGatherAccum = 0f;
        _spectralAxeGatherNextInterval = 0f;
        _spectralAxeGatherTarget = null;
        _lastSyncedSpectralAxeHudEnd = float.NaN;
        SyncSpectralAxeHudBuff();

        if (_spectralAxeRoutine != null)
            StopCoroutine(_spectralAxeRoutine);
        _spectralAxeRoutine = StartCoroutine(RunSpectralAxeRoutine(def));
        return true;
    }

    /// <summary>Spawns the projectile, runs out → spin/chop → return → despawn, then starts the deferred cooldown.</summary>
    private IEnumerator RunSpectralAxeRoutine(AbilityDefinition def)
    {
        Vector3 origin = transform.position;
        float facing = player ? Mathf.Sign(player.FacingDirectionX == 0f ? 1f : player.FacingDirectionX) : 1f;
        if (Mathf.Approximately(facing, 0f)) facing = 1f;

        Vector3 destination = origin + new Vector3(facing * SpectralAxeProjectDistance, 0f, 0f);
        // Visual lift: the projectile spins higher than the player's pivot so it reads as a chest-height
        // axe, but gather/target queries still use the ground-level destination so collider checks against
        // tree trunks remain accurate.
        Vector3 visualLift = new Vector3(0f, spectralAxeVisualLift, 0f);

        GameObject projectile = BuildSpectralAxeProjectile(origin + visualLift, facing);
        if (projectile == null)
        {
            _spectralAxeActive = false;
            FinishSpectralAxeAfterDespawn();
            yield break;
        }
        _spectralAxeProjectile = projectile;

        Transform projTr = projectile.transform;
        float spinDeg = 0f;
        // In Unity 2D, positive Z rotation reads as counter-clockwise on screen, so flipping sign
        // when spectralAxeSpinClockwise is true gives an intuitive clockwise spin.
        float spinSign = spectralAxeSpinClockwise ? -1f : 1f;
        int outboundChoice = GetSpectralAxeSelectedChoice();
        bool cleavingFlight = outboundChoice == SpectralAxeCleavingFlightChoiceIndex;
        bool outboundLogAwarded = false;
        bool inboundLogAwarded = false;

        // Outbound travel
        float travelDist = Mathf.Max(0.1f, SpectralAxeProjectDistance);
        float travelTime = travelDist / Mathf.Max(0.1f, spectralAxeTravelSpeedUnitsPerSecond);
        float t = 0f;
        while (t < travelTime)
        {
            if (projectile == null) yield break;
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / travelTime);
            Vector3 groundPos = Vector3.Lerp(origin, destination, u);
            projTr.position = groundPos + visualLift;
            spinDeg += spinSign * spectralAxeSpinDegreesPerSecond * Time.deltaTime;
            projTr.rotation = Quaternion.Euler(0f, 0f, spinDeg);

            if (cleavingFlight && !outboundLogAwarded)
                outboundLogAwarded = TrySpectralAxeAwardTravelCollisionLog(groundPos);

            yield return null;
        }

        // Snap to destination (visually lifted), then do the axe's OWN area check around its rotating
        // point: a SpectralAxeAreaRadius circle in world space. We measure the area at the axe's
        // lifted center so the green circle visualizer and the gather check are exactly the same
        // region. Only the single CLOSEST overlapping tree is used.
        Vector3 axeCenter = destination + visualLift;
        projTr.position = axeCenter;

        EnsureSpectralAxeAreaIndicatorBuilt();
        UpdateSpectralAxeAreaIndicator(axeCenter);

        _spectralAxeGatherTarget = FindClosestWoodcuttingNodeInAxeArea(axeCenter);

        if (_spectralAxeGatherTarget == null)
        {
            // No tree under the axe → log, despawn, and apply the short missed-cast cooldown.
            GameLog.Add("No trees for spectral axe to gather from", GameLog.CannotMessageColor);
            _spectralAxeMissedCast = true;

            DestroySpectralAxeAreaIndicator();
            if (projectile != null)
                Destroy(projectile);
            _spectralAxeProjectile = null;

            FinishSpectralAxeAfterDespawn();
            yield break;
        }

        while (_spectralAxeActive && Time.time < _spectralAxeEndsAt)
        {
            if (projectile == null) yield break;

            // Re-acquire only if the locked-in target depleted or got destroyed. We don't widen the
            // search — the axe is fixed at this destination, so we only ever consider trees still
            // inside its area.
            if (_spectralAxeGatherTarget == null ||
                _spectralAxeGatherTarget.IsDepleted ||
                _spectralAxeGatherTarget.Definition == null)
            {
                _spectralAxeGatherTarget = FindClosestWoodcuttingNodeInAxeArea(axeCenter);
                _spectralAxeGatherAccum = 0f;
                _spectralAxeGatherNextInterval = 0f;
            }

            spinDeg += spinSign * spectralAxeSpinDegreesPerSecond * Time.deltaTime;
            projTr.rotation = Quaternion.Euler(0f, 0f, spinDeg);
            UpdateSpectralAxeAreaIndicator(axeCenter);

            AdvanceSpectralAxeGatherTimer(Time.deltaTime);
            yield return null;
        }

        DestroySpectralAxeAreaIndicator();

        // Return leg — fly back to the player's current position (chases if the player moved).
        Vector3 returnStart = projTr.position - visualLift; // strip lift so the lerp tracks ground positions
        float returnTime = travelDist / Mathf.Max(0.1f, spectralAxeTravelSpeedUnitsPerSecond);
        float rt = 0f;
        while (rt < returnTime)
        {
            if (projectile == null) yield break;
            rt += Time.deltaTime;
            float u = Mathf.Clamp01(rt / returnTime);
            Vector3 playerNow = transform.position;
            Vector3 groundPos = Vector3.Lerp(returnStart, playerNow, u);
            projTr.position = groundPos + visualLift;
            spinDeg += spinSign * spectralAxeSpinDegreesPerSecond * Time.deltaTime;
            projTr.rotation = Quaternion.Euler(0f, 0f, spinDeg);

            if (cleavingFlight && !inboundLogAwarded)
                inboundLogAwarded = TrySpectralAxeAwardTravelCollisionLog(groundPos);

            yield return null;
        }

        if (projectile != null)
            Destroy(projectile);
        _spectralAxeProjectile = null;

        FinishSpectralAxeAfterDespawn();
    }

    private void FinishSpectralAxeAfterDespawn()
    {
        _spectralAxeActive = false;
        _spectralAxeEndsAt = 0f;
        _spectralAxeDuration = 0f;
        _spectralAxeGatherTarget = null;
        _spectralAxeGatherAccum = 0f;
        _spectralAxeGatherNextInterval = 0f;
        _spectralAxeRoutine = null;

        DestroySpectralAxeAreaIndicator();
        SyncSpectralAxeHudBuff();

        if (_spectralAxeCooldownAbilityDef)
        {
            if (_spectralAxeMissedCast)
            {
                // Missed cast (no tree under the axe) → short fixed cooldown so the player isn't punished
                // with the full 90s but can't spam the cast across an empty field either.
                float cd = Mathf.Max(0f, SpectralAxeMissedCastCooldownSeconds);
                if (cd > 0f)
                {
                    float end = Time.time + cd;
                    _cooldownEndsById[_spectralAxeCooldownAbilityDef.abilityId] = end;
                    string rowKey = BuildAbilityRowKey(_spectralAxeCooldownAbilityDef);
                    if (rowKey != null)
                        _cooldownEndsByRowKey[rowKey] = end;
                }
            }
            else
            {
                StartCooldown(_spectralAxeCooldownAbilityDef);
            }
        }
        _spectralAxeCooldownAbilityDef = null;
        _spectralAxeMissedCast = false;
    }

    private GameObject BuildSpectralAxeProjectile(Vector3 origin, float facing)
    {
        Sprite axeSprite = ResolveEquippedAxeSprite();
        var go = new GameObject("SpectralAxeProjectile");
        go.transform.position = origin;
        go.transform.localScale = new Vector3(facing < 0f ? -1f : 1f, 1f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = axeSprite;
        sr.color = spectralAxeTint;

        // Borrow the player's sorting layer so the axe draws above the lane like a regular held tool.
        SpriteRenderer playerSr = player ? player.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (playerSr != null)
        {
            sr.sortingLayerID = playerSr.sortingLayerID;
            sr.sortingOrder = playerSr.sortingOrder + 8;
        }
        else
        {
            sr.sortingOrder = 50;
        }

        AttachSpectralAxeBlueTrail(go, sr, axeSprite);

        return go;
    }

    /// <summary>
    /// Spawns a child TrailRenderer pinned to the axe's top-back edge. Because the trail's transform is a
    /// child of the spinning projectile, the trail sweeps a swooping arc as the axe rotates — visually
    /// reinforcing its motion. Tinted blue with a fading alpha gradient so it reads as a spectral wake.
    /// </summary>
    private void AttachSpectralAxeBlueTrail(GameObject projectile, SpriteRenderer axeSr, Sprite axeSprite)
    {
        if (projectile == null || axeSr == null)
            return;

        // Anchor the trail roughly at the top-back of the axe head. We bias toward the upper-left of the
        // (un-rotated) sprite so the visual matches the reference screenshot. Falls back to a small fixed
        // offset when the sprite has no bounds info.
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

        // Sprites/Default lets the gradient's vertex colors actually show under URP — the legacy
        // Default-Line material renders pink/invisible.
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

        // Render the trail just under the axe so the wake reads as "behind" the chopping head.
        trail.sortingLayerID = axeSr.sortingLayerID;
        trail.sortingOrder = axeSr.sortingOrder - 1;
    }

    /// <summary>
    /// Returns true when at least one toolbelt slot is bound to an item whose <see cref="ItemDefinition.handVisualKey"/>
    /// is <see cref="ToolKey.Axe"/>. Used as the activation gate for Spectral Axe — the ability projects
    /// a copy of the toolbelt axe, so without one there is nothing to project.
    /// </summary>
    private bool HasAxeInToolbelt()
    {
        if (toolbelt == null && player != null)
            toolbelt = player.GetComponent<ToolbeltManager>();
        if (toolbelt == null || inventory == null)
            return false;

        for (int i = 0; i < ToolbeltManager.SlotCount; i++)
        {
            string id = toolbelt.GetToolItemId(i);
            if (string.IsNullOrWhiteSpace(id))
                continue;
            ItemDefinition def = inventory.GetItemDef(id);
            if (def != null && def.handVisualKey == ToolKey.Axe)
                return true;
        }
        return false;
    }

    private Sprite ResolveEquippedAxeSprite()
    {
        // Prefer the player's toolbelt axe (the actual woodcutting tool) so Spectral Axe
        // mirrors the woodcutting visual even when a weapon (e.g. bow) is the worn main-hand.
        if (toolbelt == null && player != null)
            toolbelt = player.GetComponent<ToolbeltManager>();

        if (toolbelt != null && inventory != null)
        {
            for (int i = 0; i < ToolbeltManager.SlotCount; i++)
            {
                string id = toolbelt.GetToolItemId(i);
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                ItemDefinition def = inventory.GetItemDef(id);
                if (def == null)
                    continue;
                if (def.handVisualKey != ToolKey.Axe)
                    continue;
                if (def.icon != null)
                    return def.icon;
            }
        }

        // Fallback: the worn main-hand if it happens to be an axe (matches old behavior
        // when no toolbelt is wired up).
        if (equipment != null && inventory != null)
        {
            string visualId = equipment.VisualMainHandItemId;
            if (!string.IsNullOrWhiteSpace(visualId))
            {
                ItemDefinition def = inventory.GetItemDef(visualId);
                if (def != null && def.handVisualKey == ToolKey.Axe && def.icon != null)
                    return def.icon;
            }
        }

        // Final fallback: scan ToolSocket children for a SpriteRenderer (matches ToolSocketEquipper.axe binding).
        if (player != null)
        {
            Transform[] children = player.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform t = children[i];
                if (t == null) continue;
                string n = t.name;
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (n.IndexOf("Axe", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var sr = t.GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null && sr.sprite != null)
                    return sr.sprite;
            }
        }

        return null;
    }

    private ResourceNode FindClosestWoodcuttingNodeNear(Vector3 worldPos, float radius)
    {
        ResourceNode[] all = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        ResourceNode best = null;
        float bestSqr = radius * radius;
        for (int i = 0; i < all.Length; i++)
        {
            ResourceNode node = all[i];
            if (!node || node.ActionType != NodeAction.Woodcutting || node.IsDepleted)
                continue;
            if (node.Definition == null || !node.Definition.HasMainYield)
                continue;

            // Measure against the tree's collider edge (Bounds.ClosestPoint) so the test matches the
            // visible trunk silhouette. Falls back to the transform pivot when no Collider2D is present.
            Vector3 measureFrom = ResolveResourceNodeMeasurePoint(node, worldPos);
            float dx = measureFrom.x - worldPos.x;
            float dy = measureFrom.y - worldPos.y;
            float d = dx * dx + dy * dy;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = node;
            }
        }

        return best;
    }

    /// <summary>Closest point on the node's first <see cref="Collider2D"/> to <paramref name="worldPos"/>; pivot fallback when no collider exists.</summary>
    private static Vector3 ResolveResourceNodeMeasurePoint(ResourceNode node, Vector3 worldPos)
    {
        if (node == null)
            return worldPos;

        Collider2D col = node.GetComponent<Collider2D>();
        if (col == null)
            col = node.GetComponentInChildren<Collider2D>();
        if (col != null && col.enabled)
        {
            Vector2 cp = col.bounds.ClosestPoint(new Vector2(worldPos.x, worldPos.y));
            return new Vector3(cp.x, cp.y, node.transform.position.z);
        }

        return node.transform.position;
    }

    /// <summary>
    /// Spectral Axe's OWN area scan, used in place of <see cref="FindClosestWoodcuttingNodeNear"/> once
    /// the axe parks. Considers every Woodcutting tree whose collider edge sits within
    /// <see cref="SpectralAxeAreaRadius"/> of <paramref name="axeCenter"/> (matching the green circle
    /// visualizer), picks the closest one, and returns it. Returns null when no tree overlaps the circle.
    /// </summary>
    private ResourceNode FindClosestWoodcuttingNodeInAxeArea(Vector3 axeCenter)
    {
        float radius = SpectralAxeAreaRadius;
        float radiusSqr = radius * radius;

        ResourceNode[] all = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        ResourceNode best = null;
        float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < all.Length; i++)
        {
            ResourceNode node = all[i];
            if (!node || node.ActionType != NodeAction.Woodcutting || node.IsDepleted)
                continue;
            if (node.Definition == null || !node.Definition.HasMainYield)
                continue;

            // Collider-edge distance: the tree counts as in-area when any point on its trunk collider is
            // within the radius (off-pivot trees still register), falling back to the transform pivot
            // when no Collider2D is present.
            Vector3 measureFrom = ResolveResourceNodeMeasurePoint(node, axeCenter);
            float dx = measureFrom.x - axeCenter.x;
            float dy = measureFrom.y - axeCenter.y;
            float d = dx * dx + dy * dy;
            if (d > radiusSqr)
                continue;

            if (d < bestSqr)
            {
                bestSqr = d;
                best = node;
            }
        }

        return best;
    }

    private void EnsureSpectralAxeAreaIndicatorBuilt()
    {
        // Mirrors the Cleaving Chop gate — disabling "Show screen overlay visuals" hides this gizmo too,
        // without affecting the spectral axe's targeting / gather logic which run from world data, not visuals.
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
        lr.sortingOrder = spectralAxeAreaIndicatorSortingOrder;
        if (!string.IsNullOrWhiteSpace(spectralAxeAreaIndicatorSortingLayer))
            lr.sortingLayerName = spectralAxeAreaIndicatorSortingLayer;

        // Replace the legacy Default-Line material with Sprites/Default so vertex colors apply under URP.
        Shader spritesDefault = Shader.Find("Sprites/Default");
        if (spritesDefault != null)
            lr.material = new Material(spritesDefault) { color = Color.white };
    }

    private void UpdateSpectralAxeAreaIndicator(Vector3 axeCenter)
    {
        if (_spectralAxeAreaIndicatorLine == null)
            return;

        // Draw a closed circle of radius SpectralAxeAreaRadius centered on the axe. Same shape pattern
        // as Cleaving Chop's range indicator.
        int segs = Mathf.Clamp(spectralAxeAreaIndicatorSegments, 8, 256);
        if (_spectralAxeAreaIndicatorLine.positionCount != segs)
            _spectralAxeAreaIndicatorLine.positionCount = segs;

        float radius = SpectralAxeAreaRadius;
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

    private void DestroySpectralAxeAreaIndicator()
    {
        if (_spectralAxeAreaIndicatorRoot != null)
        {
            Destroy(_spectralAxeAreaIndicatorRoot);
            _spectralAxeAreaIndicatorRoot = null;
        }
        _spectralAxeAreaIndicatorLine = null;
    }

    /// <summary>
    /// Cleaving Flight: scans for the first nearby Woodcutting tree and awards 1 guaranteed log from it.
    /// Mirrors how secondary cleave drops are added (inventory + overflow → DropManager).
    /// </summary>
    private bool TrySpectralAxeAwardTravelCollisionLog(Vector3 axePos)
    {
        ResourceNode hit = FindClosestWoodcuttingNodeNear(axePos, SpectralAxeTravelCollisionRadius);
        if (hit == null || hit.Definition == null || !hit.Definition.HasMainYield)
            return false;

        AddSpectralAxeLootToInventory(hit, 1, hit.transform.position);
        return true;
    }

    /// <summary>Runs at the player's axe-speed × destination tree's own interval; mirrors Cleaving Chop's per-tree timer.</summary>
    private void AdvanceSpectralAxeGatherTimer(float deltaSeconds)
    {
        if (_spectralAxeGatherTarget == null || _spectralAxeGatherTarget.Definition == null)
            return;
        if (inventory == null)
            return;

        var nodeDef = _spectralAxeGatherTarget.Definition;
        float speedMult = stats ? Mathf.Max(0.01f, stats.AxeSpeedMult) : 1f;

        if (nodeDef.useRandomInterval)
        {
            if (_spectralAxeGatherNextInterval <= 0f)
                _spectralAxeGatherNextInterval = _spectralAxeGatherTarget.GetNextInterval();

            _spectralAxeGatherAccum += deltaSeconds * speedMult;
            while (_spectralAxeGatherAccum >= _spectralAxeGatherNextInterval && _spectralAxeGatherNextInterval > 0f)
            {
                _spectralAxeGatherAccum -= _spectralAxeGatherNextInterval;
                DoOneSpectralAxeGather(_spectralAxeGatherTarget);
                _spectralAxeGatherNextInterval = _spectralAxeGatherTarget.GetNextInterval();
            }
        }
        else
        {
            float rate = nodeDef.ratePerSecond;
            if (rate <= 0f)
                return;

            _spectralAxeGatherAccum += rate * deltaSeconds * speedMult;
            int gained = Mathf.FloorToInt(_spectralAxeGatherAccum);
            if (gained > 0)
            {
                _spectralAxeGatherAccum -= gained;
                for (int g = 0; g < gained; g++)
                    DoOneSpectralAxeGather(_spectralAxeGatherTarget);
            }
        }
    }

    private void DoOneSpectralAxeGather(ResourceNode node)
    {
        if (!node || node.Definition == null)
            return;
        if (inventory == null)
            return;

        var nodeDef = node.Definition;

        // Spectral Axe counts toward the destination tree's depletion (same contract as Cleaving Chop).
        node.NotifyGatherTickBeforeBonuses(countTowardDepletionCap: true);

        int mainAmt = nodeDef.RollMainYieldAmount();
        if (node.ApplyDepletedYieldPenaltyThisTick)
            mainAmt = SpectralAxeRollDepletedYield(mainAmt, nodeDef.depletedYieldMultiplier);

        // Match Cleaving Chop: spectral gathers yield at 60% efficiency (stochastic so small rolls
        // like 1 log don't get stuck at 1; they average out to the efficiency over many ticks).
        mainAmt = RollSpectralAxeEfficiencyYield(mainAmt, SpectralAxeYieldEfficiency);

        if (mainAmt > 0)
            AddSpectralAxeLootToInventory(node, mainAmt, node.transform.position);

        // Phantom Harvest: also roll bonus + hidden drops using the player's bonus find chance,
        // mirroring the bonus pass in PlayerController.DoOneGatherTick. Logs already paid out above.
        if (GetSpectralAxeSelectedChoice() == SpectralAxePhantomHarvestChoiceIndex)
            RollSpectralAxeBonusDrops(node, nodeDef);

        node.NotifyGatherTickFinishedDepletionCheck();
    }

    /// <summary>
    /// Phantom Harvest: scan the destination tree's drop table with the player's bonus find chance,
    /// then deposit any non-main yields (bonus / hidden items). Subject to the same 60% efficiency
    /// roll and depleted-yield penalty as the main log gather.
    /// </summary>
    private void RollSpectralAxeBonusDrops(ResourceNode node, NodeDefinition nodeDef)
    {
        if (node == null || nodeDef == null || inventory == null)
            return;

        float bonusFind = stats ? Mathf.Max(0f, stats.AxeBonusFindChance) : 0f;
        var drops = new List<Drop>(8);
        nodeDef.PreviewDrops(drops, bonusFind);

        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            if (string.IsNullOrWhiteSpace(d.itemId) || d.amount <= 0)
                continue;
            // Skip the main yield — already handled by DoOneSpectralAxeGather.
            if (string.Equals(d.itemId, nodeDef.YieldItemId, StringComparison.Ordinal))
                continue;

            int bonusAmt = node.ApplyDepletedYieldPenaltyThisTick
                ? SpectralAxeRollDepletedYield(d.amount, nodeDef.depletedYieldMultiplier)
                : d.amount;
            // Same 60% efficiency applies to bonus drops so the whole ability is consistently
            // rate-limited like Cleaving Chop's secondaries.
            bonusAmt = RollSpectralAxeEfficiencyYield(bonusAmt, SpectralAxeYieldEfficiency);
            if (bonusAmt <= 0)
                continue;

            AddSpectralAxeLootToInventory(node, bonusAmt, node.transform.position, d.itemId);
        }
    }

    /// <summary>
    /// Stochastically reduce <paramref name="amount"/> by <paramref name="efficiency"/> (0..1). Matches the
    /// shape of <c>PlayerController.RollCleavingChopSecondaryYield</c> so single-log rolls average to the
    /// configured efficiency instead of clamping to 1.
    /// </summary>
    private static int RollSpectralAxeEfficiencyYield(int amount, float efficiency)
    {
        if (amount <= 0)
            return 0;
        float m = Mathf.Clamp01(efficiency);
        if (m >= 1f)
            return amount;
        int sum = 0;
        for (int i = 0; i < amount; i++)
        {
            if (UnityEngine.Random.value < m)
                sum++;
        }
        return sum;
    }

    private void AddSpectralAxeLootToInventory(ResourceNode node, int amount, Vector3 worldDropPos, string overrideItemId = null)
    {
        if (amount <= 0 || node == null || node.Definition == null || inventory == null)
            return;

        string itemId = string.IsNullOrWhiteSpace(overrideItemId) ? node.Definition.YieldItemId : overrideItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        int added = inventory.AddPartial(itemId, amount);
        int overflow = amount - added;

        if (added > 0)
            SessionTrackerData.EnsureInstance().RegisterLootGain(node.Definition.displayName, itemId, added);

        if (overflow > 0 && DropManager.Instance != null)
        {
            var itemDef = inventory.GetItemDef(itemId);
            Sprite icon = itemDef ? itemDef.icon : null;
            DropManager.Instance.Spawn(itemId, overflow, icon, node.Definition.displayName);
        }
    }

    private static int SpectralAxeRollDepletedYield(int amount, float multiplier)
    {
        if (amount <= 0) return 0;
        float m = Mathf.Clamp01(multiplier);
        if (m >= 1f) return amount;
        int sum = 0;
        for (int i = 0; i < amount; i++)
        {
            if (UnityEngine.Random.value < m)
                sum++;
        }
        return sum;
    }

    private void SyncSpectralAxeHudBuff()
    {
        if (!buffController)
            return;

        if (!_spectralAxeActive)
        {
            if (!float.IsNaN(_lastSyncedSpectralAxeHudEnd))
            {
                buffController.ClearHudAbilityBuff(SpectralAxeId);
                _lastSyncedSpectralAxeHudEnd = float.NaN;
            }
            return;
        }

        if (Mathf.Approximately(_lastSyncedSpectralAxeHudEnd, _spectralAxeEndsAt))
            return;

        _lastSyncedSpectralAxeHudEnd = _spectralAxeEndsAt;
        buffController.SetHudAbilityBuff(SpectralAxeId, 1, _spectralAxeEndsAt, _spectralAxeDuration);
    }

    private int GetSpectralAxeSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        // Spine-keyed: Spectral Axe is the slot-1 ability at Woodcutting Lv25, sharing the row with
        // Cleaving Chop (slot 0).
        return skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv25_1", -1);
    }

    /// <summary>True while the buff window is open. Surface for tooltips / status checks.</summary>
    public bool IsSpectralAxeActive => _spectralAxeActive && Time.time < _spectralAxeEndsAt;

    /// <summary>
    /// Called on successful primary hit release. Returns whether cleaving is active and how many extra targets to attempt.
    /// Hit charges decrement until zero; after that, cleave continues until duration ends.
    /// </summary>
    public bool TryConsumeCleavingExtraTargetsOnSuccessfulHit(out int additionalTargets)
    {
        additionalTargets = 0;
        CleanupCleavingStrikesIfExpired();
        if (!_cleavingBuffActive)
            return false;

        additionalTargets = Mathf.Max(0, _cleavingAdditionalTargets);
        if (additionalTargets <= 0)
            return false;

        if (_cleavingHitsRemaining > 0)
            _cleavingHitsRemaining--;

        CleanupCleavingStrikesIfExpired();
        return true;
    }

    /// <summary>Action bar stack-text helper for ability-specific counters.</summary>
    public int GetAbilityStackCountDisplay(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return 0;

        if (string.Equals(abilityId, CleavingStrikesId, StringComparison.OrdinalIgnoreCase))
        {
            CleanupCleavingStrikesIfExpired();
            if (!_cleavingBuffActive)
                return 0;
            // After hit budget is spent, show1 so the slot still reads as active during the time-only tail.
            return _cleavingHitsRemaining > 0 ? _cleavingHitsRemaining : 1;
        }

        return 0;
    }

    /// <summary>
    /// Cleaving Strikes: bonus targets take a flat fraction of the same rolled weapon split (no ability/AP scaling).
    /// Primary target damage is unchanged.
    /// </summary>
    public SplitDamage BuildCleavingSecondarySplit(SplitDamage baseRolled)
    {
        float m = AbilityCombatPower.CleavingStrikesSecondaryHitWeaponDamageFraction;
        return new SplitDamage(
            Mathf.Max(0f, baseRolled.physical * m),
            Mathf.Max(0f, baseRolled.magic * m),
            Mathf.Max(0f, baseRolled.corruptionDamage * m));
    }

    private int GetCleavingStrikesSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, "Lv15_1", -1);
    }

    private int GetCrescentSlashSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, "Lv15_2", -1);
    }

    private int GetRendSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        // Rend enhancement selection is keyed on the level-5 ability row.
        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
    }

    private int GetEnvenomSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        // Envenom enhancement selection is keyed on the level-5 ability row.
        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
    }

    public bool IsAbilityPrimed(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (string.Equals(abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
            return _powerSlashQueued;
        if (string.Equals(abilityId, RendId, StringComparison.OrdinalIgnoreCase))
            return _rendQueued;
        if (string.Equals(abilityId, EnvenomId, StringComparison.OrdinalIgnoreCase))
            return _envenomQueued;
        if (string.Equals(abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase))
            return _crescentSlashQueued;

        return false;
    }

    public bool CanUseAbilityWithCurrentWeapon(string abilityId)
    {
        AbilityDefinition def = GetAbilityDefinition(abilityId);
        if (!def)
            return false;
        return CanUseWithEquippedWeapon(def);
    }

    private void StartCooldown(AbilityDefinition def)
    {
        if (!def) return;
        float cd = Mathf.Max(0f, def.cooldown - GetPowerSlashCooldownReduction(def));
        if (cd <= 0f) return;
        float end = Time.time + cd;
        _cooldownEndsById[def.abilityId] = end;

        string rowKey = BuildAbilityRowKey(def);
        if (rowKey != null)
            _cooldownEndsByRowKey[rowKey] = end;
    }

    private float GetPowerSlashAnyTypeMultiplierBonus()
    {
        if (!skillsManager) skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return 0f;

        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
        return selected == 0 ? 0.25f : 0f;
    }

    private float GetPowerSlashCooldownReduction(AbilityDefinition def)
    {
        if (def == null || !string.Equals(def.abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
            return 0f;
        if (!skillsManager) skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return 0f;

        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
        return selected == 1 ? 3f : 0f;
    }

    private void SpawnPowerSlashTrail()
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

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.95f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f)
        );
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
            }
        );
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

    /// <summary>Resolves the assigned database or Resources default (same as runtime ability lookup).</summary>
    public AbilityDatabase GetDatabaseOrDefault()
    {
        if (!abilityDatabase)
            abilityDatabase = AbilityDatabase.LoadDefault();
        return abilityDatabase;
    }

    private bool IsAbilityAllowedBySkillProgress(AbilityDefinition def)
    {
        if (!def)
            return false;
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillDatabase)
            skillDatabase = SkillDatabase.LoadDefault();
        SkillDefinition skill = skillDatabase ? skillDatabase.Get(def.sourceSkill) : null;
        return SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skill, def, skillsManager);
    }

    private AbilityDefinition GetAbilityDefinition(string abilityId)
    {
        AbilityDatabase db = GetDatabaseOrDefault();
        return db ? db.Get(abilityId) : null;
    }

    private bool CanUseWithEquippedWeapon(AbilityDefinition def)
    {
        if (!stats)
            stats = GetComponent<CharacterStats>();
        if (stats)
            return stats.IsAbilityUsableWithEquippedWeapon(def);

        if (!def || def.requiredWeaponType == AbilityWeaponRequirement.Any)
            return true;

        if (!equipment)
            equipment = GetComponent<EquipmentManager>();
        if (!inventory)
            inventory = GetComponent<Inventory>();
        if (!equipment || !inventory || string.IsNullOrWhiteSpace(equipment.MainHandItemId))
            return false;

        ItemDefinition mainHand = inventory.GetItemDef(equipment.MainHandItemId);
        if (!mainHand || !mainHand.IsWeapon)
            return false;

        AttackSkill skill = mainHand.weaponStats.attackSkill;
        return def.requiredWeaponType switch
        {
            AbilityWeaponRequirement.Melee => skill == AttackSkill.Melee,
            AbilityWeaponRequirement.Ranged => skill == AttackSkill.Ranged,
            AbilityWeaponRequirement.Magic => skill == AttackSkill.Magic,
            AbilityWeaponRequirement.MeleeOrRanged =>
                skill == AttackSkill.Melee || skill == AttackSkill.Ranged,
            _ => true
        };
    }

    private bool TrySpawnSoulforgedWeaponMinion(AbilityDefinition def)
    {
        MinionDefinition md = def.minionSpawnDefinition;
        if (!md || !md.runtimePrefab || !_ownerStats || !player)
            return false;

        Transform anchor = player.SoulforgedWeaponSpawnPoint;
        Transform attacker = _ownerTransform ? _ownerTransform : _ownerStats.transform;
        Sprite weaponSprite = ResolveSoulforgedWeaponVisualSprite(md, def);
        int selectedChoice = GetSoulforgedWeaponSelectedChoice();
        bool swarm = selectedChoice == SoulforgedWeaponSwarmChoiceIndex;
        bool indefinite = selectedChoice == SoulforgedWeaponIndefiniteChoiceIndex;
        int spawnCount = swarm ? SoulforgedWeaponSwarmCount : 1;
        _activeSoulforgedWeaponIsPersistent = indefinite;
        CleanupSoulforgedWeaponList();

        var occupiedTargets = new HashSet<int>();

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 homeOffset = swarm ? GetSoulforgedSwarmHomeOffset(i) : Vector3.zero;
            Vector3 attachOffset = swarm ? GetSoulforgedSwarmAttachOffset(i) : Vector3.zero;
            GameObject go = Instantiate(md.runtimePrefab, anchor.position + homeOffset, Quaternion.identity);
            SoulforgedWeaponMinion minion = go.GetComponent<SoulforgedWeaponMinion>();
            if (!minion)
            {
                Destroy(go);
                CleanupSoulforgedWeaponSummonsWithoutCooldown();
                return false;
            }

            if (!minion.Initialize(
                    _ownerStats,
                    md,
                    soulforgedWeaponMinionPresentation,
                    anchor,
                    weaponSprite,
                    attacker,
                    HandleSoulforgedWeaponReleased,
                    swarm ? SoulforgedWeaponSwarmDurationSeconds : -1f,
                    indefinite,
                    swarm ? SoulforgedWeaponSwarmDamageMultiplier : 1f,
                    homeOffset,
                    attachOffset))
            {
                Destroy(go);
                CleanupSoulforgedWeaponSummonsWithoutCooldown();
                return false;
            }

            minion.PersistAcrossSceneLoads();
            _activeSoulforgedWeaponMinions.Add(minion);
            if (swarm)
            {
                minion.TryRecastRetargetOrReturn(occupiedTargets);
                RememberSoulforgedTarget(occupiedTargets, minion.CurrentTarget);
            }
        }

        _soulforgedWeaponCooldownAbilityDef = def;

        // Mirror the swarm/indefinite split in the HUD buff bar. Swarm gets a real timer so the
        // 20s overlay sweeps down; indefinite parks endsAt slightly in the past with duration=0 so
        // BuffIconUI hides both the timer text and the radial overlay (the icon just persists).
        if (swarm)
        {
            _soulforgedHudBuffDuration = SoulforgedWeaponSwarmDurationSeconds;
            _soulforgedHudBuffEndsAt = Time.time + SoulforgedWeaponSwarmDurationSeconds;
        }
        else
        {
            _soulforgedHudBuffDuration = 0f;
            _soulforgedHudBuffEndsAt = Time.time - 1f;
        }
        _lastSyncedSoulforgedHudEnd = float.NaN;
        _lastSyncedSoulforgedHudStacks = int.MinValue;
        SyncSoulforgedWeaponHudBuff();

        return true;
    }

    private void SyncSoulforgedWeaponHudBuff()
    {
        if (!buffController)
            buffController = GetComponent<PlayerBuffController>();
        if (!buffController)
            return;

        int liveCount = 0;
        for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
        {
            if (_activeSoulforgedWeaponMinions[i])
                liveCount++;
        }

        if (liveCount <= 0)
        {
            if (!float.IsNaN(_lastSyncedSoulforgedHudEnd) || _lastSyncedSoulforgedHudStacks != int.MinValue)
            {
                buffController.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWeaponAbilityId);
                _lastSyncedSoulforgedHudEnd = float.NaN;
                _lastSyncedSoulforgedHudStacks = int.MinValue;
            }
            return;
        }

        // Only push to the controller when something visible actually changed — keeps the HUD from
        // rebuilding every frame while the swarm timer ticks (the icon polls RemainingSeconds itself).
        if (Mathf.Approximately(_lastSyncedSoulforgedHudEnd, _soulforgedHudBuffEndsAt) &&
            _lastSyncedSoulforgedHudStacks == liveCount)
            return;

        _lastSyncedSoulforgedHudEnd = _soulforgedHudBuffEndsAt;
        _lastSyncedSoulforgedHudStacks = liveCount;
        buffController.SetHudAbilityBuff(
            AbilityCombatPower.SoulforgedWeaponAbilityId,
            liveCount,
            _soulforgedHudBuffEndsAt,
            _soulforgedHudBuffDuration);
    }

    private int GetSoulforgedWeaponSelectedChoice()
    {
        if (!skillsManager) skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, SoulforgedWeaponChoiceSourceLevel, -1);
    }

    private static Vector3 GetSoulforgedSwarmHomeOffset(int index)
    {
        return index switch
        {
            1 => new Vector3(-0.28f, 0.18f, 0f),
            2 => new Vector3(0.28f, -0.12f, 0f),
            _ => Vector3.zero
        };
    }

    private static Vector3 GetSoulforgedSwarmAttachOffset(int index)
    {
        return index switch
        {
            1 => new Vector3(-0.22f, 0.16f, 0f),
            2 => new Vector3(0.22f, -0.12f, 0f),
            _ => Vector3.zero
        };
    }

    private void HandleSoulforgedWeaponReleased(SoulforgedWeaponMinion m)
    {
        _activeSoulforgedWeaponMinions.Remove(m);
        CleanupSoulforgedWeaponList();

        if (_activeSoulforgedWeaponMinions.Count == 0 && _soulforgedWeaponCooldownAbilityDef)
        {
            StartCooldown(_soulforgedWeaponCooldownAbilityDef);
            _soulforgedWeaponCooldownAbilityDef = null;
            _activeSoulforgedWeaponIsPersistent = false;
        }
    }

    private void RecastActiveSoulforgedWeapons()
    {
        CleanupSoulforgedWeaponList();
        bool swarm = GetSoulforgedWeaponSelectedChoice() == SoulforgedWeaponSwarmChoiceIndex;
        EnemyBaseController collapseTarget = swarm && combat != null ? combat.CurrentTarget : null;
        if (swarm && collapseTarget && !collapseTarget.IsDead)
        {
            for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
                _activeSoulforgedWeaponMinions[i]?.ForceTarget(collapseTarget);
            return;
        }

        var occupiedTargets = swarm ? new HashSet<int>() : null;
        for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
        {
            SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
            if (!minion)
                continue;
            minion.TryRecastRetargetOrReturn(occupiedTargets);
            if (swarm)
                RememberSoulforgedTarget(occupiedTargets, minion.CurrentTarget);
        }
    }

    private static void RememberSoulforgedTarget(HashSet<int> occupiedTargets, EnemyBaseController target)
    {
        if (occupiedTargets != null && target && !target.IsDead)
            occupiedTargets.Add(target.GetInstanceID());
    }

    private void CleanupSoulforgedWeaponList()
    {
        for (int i = _activeSoulforgedWeaponMinions.Count - 1; i >= 0; i--)
        {
            if (!_activeSoulforgedWeaponMinions[i])
                _activeSoulforgedWeaponMinions.RemoveAt(i);
        }
    }

    private void CleanupSoulforgedWeaponSummonsWithoutCooldown()
    {
        for (int i = _activeSoulforgedWeaponMinions.Count - 1; i >= 0; i--)
        {
            SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
            if (minion)
                minion.CancelAndDestroy();
        }
        _activeSoulforgedWeaponMinions.Clear();
        _soulforgedWeaponCooldownAbilityDef = null;
        _activeSoulforgedWeaponIsPersistent = false;
    }

    private void CleanupSoulforgedWeaponIfUnavailable()
    {
        if (!_activeSoulforgedWeaponIsPersistent || _activeSoulforgedWeaponMinions.Count == 0)
            return;

        if (Time.time < _soulforgedAvailabilityCheckPausedUntil)
            return;

        AbilityDefinition def = _soulforgedWeaponCooldownAbilityDef;
        if (!def)
            return;

        bool stillAvailable =
            GetSoulforgedWeaponSelectedChoice() == SoulforgedWeaponIndefiniteChoiceIndex &&
            IsAbilityAllowedBySkillProgress(def) &&
            IsAbilityAssignedToActionBar(def.abilityId);
        if (stillAvailable)
            return;

        EndSoulforgedAndStartCooldown();
    }

    private bool IsAbilityAssignedToActionBar(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (!actionBar)
            return true;

        foreach (ActionBarSlotUI slot in actionBar.GetSlots())
        {
            ActionBarAssignment action = slot != null ? slot.AssignedAction : null;
            if (action != null &&
                action.IsAbility &&
                string.Equals(action.id, abilityId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void EndSoulforgedAndStartCooldown()
    {
        AbilityDefinition cooldownDef = _soulforgedWeaponCooldownAbilityDef;
        bool hadActiveMinion = _activeSoulforgedWeaponMinions.Count > 0;

        for (int i = _activeSoulforgedWeaponMinions.Count - 1; i >= 0; i--)
        {
            SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
            if (minion)
                minion.CancelAndDestroy();
        }

        _activeSoulforgedWeaponMinions.Clear();

        if (cooldownDef != null && hadActiveMinion)
            StartCooldown(cooldownDef);

        _soulforgedWeaponCooldownAbilityDef = null;
        _activeSoulforgedWeaponIsPersistent = false;
    }

    /// <summary>
    /// Owner death: immediately end active Soulforged Weapon summon and force ability cooldown.
    /// Also tears down any deployed Spectral Axe so it doesn't keep gathering after the player dies.
    /// </summary>
    public void EndSoulforgedOnOwnerDeath()
    {
        AbilityDefinition cooldownDef = _soulforgedWeaponCooldownAbilityDef;
        bool hadActiveMinion = _activeSoulforgedWeaponMinions.Count > 0;

        for (int i = _activeSoulforgedWeaponMinions.Count - 1; i >= 0; i--)
        {
            SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
            if (minion)
                minion.CancelAndDestroy();
        }
        _activeSoulforgedWeaponMinions.Clear();

        if (cooldownDef != null && hadActiveMinion)
            StartCooldown(cooldownDef);

        _soulforgedWeaponCooldownAbilityDef = null;
        _activeSoulforgedWeaponIsPersistent = false;

        AbortSpectralAxe(awardCooldown: true);
    }

    /// <summary>
    /// Force-cancel the current Spectral Axe deployment (kills coroutine + projectile). When
    /// <paramref name="awardCooldown"/> is true the deferred cooldown still starts, mirroring how
    /// Soulforged Weapon's cooldown sticks even if its owner dies mid-cast.
    /// </summary>
    private void AbortSpectralAxe(bool awardCooldown)
    {
        if (!_spectralAxeActive && _spectralAxeRoutine == null && _spectralAxeProjectile == null)
            return;

        if (_spectralAxeRoutine != null)
            StopCoroutine(_spectralAxeRoutine);
        _spectralAxeRoutine = null;

        if (_spectralAxeProjectile != null)
        {
            Destroy(_spectralAxeProjectile);
            _spectralAxeProjectile = null;
        }

        bool wasActive = _spectralAxeActive;
        AbilityDefinition deferred = _spectralAxeCooldownAbilityDef;

        _spectralAxeActive = false;
        _spectralAxeEndsAt = 0f;
        _spectralAxeDuration = 0f;
        _spectralAxeGatherTarget = null;
        _spectralAxeGatherAccum = 0f;
        _spectralAxeGatherNextInterval = 0f;
        _spectralAxeCooldownAbilityDef = null;
        _spectralAxeMissedCast = false;
        DestroySpectralAxeAreaIndicator();
        SyncSpectralAxeHudBuff();

        if (awardCooldown && wasActive && deferred != null)
            StartCooldown(deferred);
    }

    /// <summary>
    /// Visual only: held → equipped → item icon → <see cref="AbilityDefinition.icon"/> → minion placeholder. Does not copy weapon combat stats.
    /// </summary>
    private Sprite ResolveSoulforgedWeaponVisualSprite(MinionDefinition md, AbilityDefinition abilityDef)
    {
        Sprite FallbackAbilityOrPlaceholder()
        {
            if (abilityDef && abilityDef.icon)
                return abilityDef.icon;
            if (soulforgedWeaponMinionPresentation.placeholderWeaponSprite)
                return soulforgedWeaponMinionPresentation.placeholderWeaponSprite;
            return null;
        }

        if (!equipment || !inventory)
            return FallbackAbilityOrPlaceholder();

        string id = equipment.MainHandItemId;
        if (string.IsNullOrWhiteSpace(id))
            return FallbackAbilityOrPlaceholder();

        ItemDefinition itemDef = inventory.GetItemDef(id);
        if (!itemDef)
            return FallbackAbilityOrPlaceholder();

        if (itemDef.HeldSprite)
            return itemDef.HeldSprite;
        if (itemDef.EquippedSprite)
            return itemDef.EquippedSprite;
        if (itemDef.icon)
            return itemDef.icon;

        return FallbackAbilityOrPlaceholder();
    }

    private void OnDestroy()
    {
        if (_activeSoulforgedWeaponMinions.Count > 0 && _soulforgedWeaponCooldownAbilityDef)
            s_pendingSoulforgedRestoreAbilityId = _soulforgedWeaponCooldownAbilityDef.abilityId;

        for (int i = _activeSoulforgedWeaponMinions.Count - 1; i >= 0; i--)
        {
            SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
            if (minion)
                minion.CancelAndDestroy();
        }
        _activeSoulforgedWeaponMinions.Clear();
    }
}

