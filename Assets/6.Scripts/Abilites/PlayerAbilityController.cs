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
public partial class PlayerAbilityController : MonoBehaviour
{
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
    [SerializeField] private PlayerAbilityVfxController abilityVfx;

    [Header("Global Cooldown")]
    [SerializeField, Min(0f)] private float globalCooldownSeconds = 0.3f;
    private float _globalCooldownEndsAt;

    private readonly Dictionary<string, float> _cooldownEndsById = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cooldown end time per skill-tree row (skillType:Lv{requiredLevel}). When the tree is reset
    /// and the player picks a different ability in the same row, the new ability inherits this
    /// remaining cooldown so swapping isn't a free reset.
    /// </summary>
    private readonly Dictionary<string, float> _cooldownEndsByRowKey = new(StringComparer.OrdinalIgnoreCase);

    private static PlayerAbilityController _instance;
    private bool _finalSeveranceChanneling;
    private Coroutine _finalSeveranceRoutine;
    private bool _bladestormChanneling;
    private Coroutine _bladestormRoutine;
    private float _bladestormSavedCombatDamageTakenMultiplier = 1f;
    private bool _bladestormCombatDamageTakenApplied;
    private Coroutine _executionersDescentRoutine;
    private bool _executionersDescentTargetDiedDuringDescent;

    /// <summary>True while a channeled combat ability blocks normal attacks (Final Severance, Bladestorm, Flame Charge).</summary>
    public static bool BlocksCombatActions =>
        _instance != null && (_instance._finalSeveranceChanneling || _instance._bladestormChanneling ||
                              _instance._whirlwindChanneling || _instance._bladestormRoutine != null ||
                              _instance._flameChargeRoutine != null || _instance._snipeCharging);

    /// <summary>True during the Flame Charge dash window; click-to-move keeps its destination (same as sprint dash).</summary>
    public static bool IsFlameChargeDashing { get; private set; }

    public static bool IsWhirlwindAutoChanneling =>
        _instance != null && _instance._whirlwindChanneling && _instance._whirlwindAutoChanneling;

    public static bool SuppressesIdleCombatTargeting => IsWhirlwindAutoChanneling;

    public static void EndAutoBattleWhirlwindChannelIfActive()
    {
        if (_instance == null || !_instance._whirlwindAutoChanneling)
            return;

        _instance.ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: true);
    }
    private const string PowerSlashId = "power_slash";
    private const string TripleShotId = "triple_shot";
    private const string StaticArrowsId = "static_arrows";
    private const string CrusaderStrikeId = "crusader_strike";
    public const string CrusaderStrikeFireBalanceHudBuffId = "crusader_strike_fire_balance";
    private const string WhirlwindId = "whirlwind";
    private const string RendId = "rend";
    private const string EnvenomId = "envenom";
    private const string CleavingStrikesId = "cleaving_strikes";
    private const string CrescentSlashId = "crescent_slash";
    private const string GuardiansHammerId = "guardians_hammer";
    private const string FinalSeveranceId = "final_severance";
    private const string ExecutionersDescentId = "executioners_descent";
    private const string BladestormId = "bladestorm";
    private const string ShadowStrikeId = "shadow_strike";
    private const string EnergyInfusionId = "energy_infusion";
    private const string HammerTempestId = "hammer_tempest";
    private const int HammerTempestSacredArsenalChoiceIndex = AbilityCombatPower.HammerTempestSacredArsenalChoiceIndex;
    private const int HammerTempestCrushingMomentumChoiceIndex = AbilityCombatPower.HammerTempestCrushingMomentumChoiceIndex;
    private const string FlameChargeId = "flame_charge";
    private const string LumberFrenzyId = "lumber_frenzy";
    private const float LumberFrenzyDurationSeconds = 20f;
    private const float LumberFrenzyChoppingSpeedBonus = 0.20f;
    private const float LumberFrenzyGritChanceBonus = 0.10f;
    private const float LumberFrenzyStaminaEfficiencyEnhancementBonus = 0.15f;
    private const float LumberFrenzyExtraGritEnhancementBonus = 0.05f;
    private const int LumberFrenzyChoiceSourceLevel = 5;
    private const int LumberFrenzyStaminaEnhancementChoiceIndex = 0;
    private const int LumberFrenzyExtraGritEnhancementChoiceIndex = 1;

    private const string FishingFrenzyId = "fishing_frenzy";
    private const float FishingFrenzyDurationSeconds = 20f;
    private const float FishingFrenzySpeedBonus = 0.20f;
    private const float FishingFrenzyGritChanceBonus = 0.10f;
    private const float FishingFrenzyStaminaEfficiencyEnhancementBonus = 0.15f;
    private const float FishingFrenzyExtraGritEnhancementBonus = 0.05f;
    private const int FishingFrenzyChoiceSourceLevel = 5;
    private const int FishingFrenzyStaminaEnhancementChoiceIndex = 0;
    private const int FishingFrenzyExtraGritEnhancementChoiceIndex = 1;

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

    private const string AvatarOfTheForestId = "avatar_of_the_forest";
    private const float AvatarOfTheForestBaseDurationSeconds = 90f;
    private const float AvatarOfTheForestDurationEnhancementBonusSeconds = 30f;
    private const float AvatarOfTheForestCooldownEnhancementReductionSeconds = 30f;
    private const float AvatarOfTheForestReplenishIntervalSeconds = 5f;
    private const int AvatarOfTheForestDurationEnhancementChoiceIndex = 0;
    private const int AvatarOfTheForestCooldownEnhancementChoiceIndex = 1;

    private const int WhirlwindChoiceSourceLevel = 15;
    private const int GuardiansHammerProtectorResolveChoiceIndex = 0;
    private float _guardiansHammerProtectorResolveGuardExpiresAt;
    private const int GuardiansHammerBurningVerdictChoiceIndex = 1;
    private const string GuardiansHammerBurningVerdictOutgoingSourceLabel = "Burning Verdict";
    private const int SoulforgedWeaponChoiceSourceLevel = AbilityCombatPower.SoulforgedWeaponEnhancementSourceLevel;
    private const int SoulforgedWeaponSwarmChoiceIndex = AbilityCombatPower.SoulforgedWeaponSwarmChoiceIndex;
    private const int SoulforgedWeaponExtendedDurationChoiceIndex = AbilityCombatPower.SoulforgedWeaponExtendedDurationChoiceIndex;
    private const int SoulforgedWeaponSwarmCount = AbilityCombatPower.SoulforgedWeaponSwarmCount;
    private const float SoulforgedWeaponSwarmDamageMultiplier = AbilityCombatPower.SoulforgedWeaponSwarmDamageMultiplier;
    private const float SoulforgedWeaponSwarmDurationSeconds = AbilityCombatPower.SoulforgedWeaponSwarmDurationSeconds;
    private const float SoulforgedWeaponExtendedDurationSeconds = AbilityCombatPower.SoulforgedWeaponExtendedDurationSeconds;
    private const float SoulforgedWeaponSceneLoadActionBarGraceSeconds = 2f;
    private const int WhirlwindMaxChannelStacks = 5;
    private const int CrusaderStrikeFinalComboStep = 3;
    public const int CrusaderStrikeFireBalanceChoiceIndex = 0;
    public const int CrusaderStrikeSacredRestorationChoiceIndex = 1;
    private const float CrusaderStrikeFirstHitWeaponMultiplier = AbilityCombatPower.CrusaderStrikeFirstHitWeaponMultiplier;
    private const float CrusaderStrikeSecondHitWeaponMultiplier = AbilityCombatPower.CrusaderStrikeSecondHitWeaponMultiplier;
    private const float CrusaderStrikeFinalHitWeaponMultiplier = AbilityCombatPower.CrusaderStrikeFinalHitWeaponMultiplier;
    private const float CrusaderStrikeComboTimeoutSeconds = 7f;
    private bool _whirlwindChanneling;
    private bool _whirlwindAutoChanneling;
    private EnemyBaseController _whirlwindSavedCombatTarget;
    private bool _whirlwindActionBarHeld;
    private float _whirlwindChannelStartedAt;
    private float _whirlwindLastTickAt;
    private float _whirlwindNextTickAt;
    private float _whirlwindNextGaleforceTwisterAt;
    private readonly List<GaleforceTwisterInstance> _galeforceTwisterDamageBuffer = new();
    private readonly Dictionary<int, float> _whirlwindLastHitTimeByEnemyId = new();
    private readonly HashSet<int> _whirlwindEnemiesInContactThisFrame = new();
    private readonly List<int> _whirlwindContactRemovalBuffer = new();
    private int _lastSyncedWhirlwindHudStacks = int.MinValue;
    private Camera _whirlwindCachedCamera;
    private bool _whirlwindScreenBoundsValid;
    private float _whirlwindScreenWorldMinX;
    private float _whirlwindScreenWorldMaxX;
    private float _whirlwindScreenWorldMinY;
    private float _whirlwindScreenWorldMaxY;
    private float _nextWhirlwindScreenBoundsRefreshAt;
    private float _nextWhirlwindAutoBattlePresenceCheckAt;
    private bool _whirlwindAutoBattleAnyOnScreen;
    private bool _whirlwindAutoBattleAnyWithinStopDistance;
    private const float WhirlwindAutoBattlePresenceRecheckInterval = 0.15f;
    private float _nextWhirlwindAdvanceRecalcAt;
    private float _cachedWhirlwindAdvanceX;
    private bool _hasCachedWhirlwindAdvanceX;
    private float _nextIdleAbilityMaintenanceAt;
    private const float IdleAbilityMaintenanceInterval = 1f;
    private const float IdleLingeringActionBarCheckInterval = 1f;
    private int _crusaderStrikeComboStep;
    private int _crusaderStrikePrimedStage;
    private bool _crusaderStrikeQueued;
    private int _queuedCrusaderStrikeConsumedStage;
    private DpsDamageBucket? _crusaderStrikeAttributionBucketPending;
    private float _crusaderStrikeComboTimeoutAt;
    private int _lastSyncedCrusaderStrikeHudStacks = int.MinValue;
    private float _crusaderStrikeFireBalanceBuffEndsAt;
    private float _crusaderStrikeFireBalanceBuffDuration;
    private float _lastSyncedCrusaderStrikeFireBalanceHudEnd = float.NaN;
    private bool _powerSlashQueued;
    private bool _tripleShotQueued;
    private Coroutine _tripleShotPhantomVolleyRoutine;
    private float _queuedTripleShotWeaponMultiplier = 1f;
    private float _queuedTripleShotAllDamageMultiplier = 1f;
    private bool _queuedTripleShotUsedEnergyInfusionMana;
    private string _pendingMeleeApproachAbilityId;
    private bool _rendQueued;
    private bool _envenomQueued;
    private bool _crescentSlashQueued;
    /// <summary>True after energy is spent to prime Crescent Slash (matches Power Slash priming contract).</summary>
    private bool _crescentSlashEnergyCommitted;
    private int _cleavingHitsRemaining;
    private int _cleavingAdditionalTargets;
    private float _cleavingBuffEndsAt;
    private float _cleavingBuffDuration;
    private bool _cleavingBuffActive;

    private bool _staticArrowsBuffActive;
    private int _staticArrowsHitsRemaining;
    private float _staticArrowsBuffEndsAt;
    private float _staticArrowsBuffDuration;
    private bool _staticArrowsAppliedThisHit;
    private int _lastSyncedStaticArrowsHudStacks = int.MinValue;
    private float _lastSyncedStaticArrowsHudEnd = float.NaN;
    /// <summary>When Static Arrows charges are spent (or the buff is cleared), this ability gets <see cref="StartCooldown"/>.</summary>
    private AbilityDefinition _staticArrowsCooldownAbilityDef;
    private int _lastSyncedCleavingHudStacks = int.MinValue;
    private float _lastSyncedCleavingHudEnd = float.NaN;
    private bool _lumberFrenzyActive;
    private float _lumberFrenzyEndsAt;
    private float _lumberFrenzyDuration;
    private float _lastSyncedLumberFrenzyHudEnd = float.NaN;
    /// <summary>When the Lumber Frenzy buff expires, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _lumberFrenzyCooldownAbilityDef;

    private bool _fishingFrenzyActive;
    private float _fishingFrenzyEndsAt;
    private float _fishingFrenzyDuration;
    private float _lastSyncedFishingFrenzyHudEnd = float.NaN;
    /// <summary>When the Fishing Frenzy buff expires, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _fishingFrenzyCooldownAbilityDef;

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
    /// <summary>World center of the parked Spectral Axe gather circle (lifted visual position).</summary>
    private Vector3 _spectralAxeAreaCenterWorld;
    private bool _spectralAxeAreaCenterValid;
    /// <summary>True when the axe parked but found no tree in its area → cooldown is overridden to <see cref="SpectralAxeMissedCastCooldownSeconds"/>.</summary>
    private bool _spectralAxeMissedCast;

    private bool _avatarOfForestActive;
    private bool _energyInfusionActive;
    private int _lastAbilityResourceSpendHealth;
    private int _lastAbilityResourceSpendMana;
    private int _lastAbilityResourceSpendEnergy;
    private string _lastAbilityResourceSpendAbilityId = "";
    private bool _lastAbilityResourceSpendUsedEnergyInfusionMana;
    private bool _queuedPowerSlashUsedEnergyInfusionMana;
    private bool _crescentSlashUsedEnergyInfusionMana;
    private bool _guardiansHammerUsedEnergyInfusionMana;
    private bool _whirlwindUsedEnergyInfusionMana;

    private bool _hammerTempestActive;
    private float _hammerTempestEndsAt;
    private float _hammerTempestDuration;
    private float _hammerTempestNextTickAt;
    private float _lastSyncedHammerTempestHudEnd = float.NaN;
    private readonly Dictionary<int, float> _hammerTempestLastHitTimeByEnemyId = new();
    private readonly Dictionary<int, HammerTempestMomentumState> _hammerTempestMomentumByEnemyId = new();

    private struct HammerTempestMomentumState
    {
        public int stacks;
    }

    private int _battleEngineCastSessionId;
    private int _battleEngineEnergyGrantedSessionId = -1;
    private string _battleEngineEnergyPendingAbilityId = "";
    private int _battleEngineOverloadStacks;
    private float _battleEngineOverloadEndsAt = -1f;
    private int _lastSyncedOverloadHudStacks = int.MinValue;
    private float _lastSyncedOverloadHudEnd = float.NaN;

    private float _phoenixSoulBurnRegenAccum;
    private float _phoenixAshenRebirthCooldownEndsAt = -1f;
    private Coroutine _phoenixAshenRebirthImmunityRoutine;

    /// <summary>Skill tree cooldown overlay id for Phoenix Soul — Ashen Rebirth (not an <see cref="AbilityDefinition"/>).</summary>
    public const string PhoenixAshenRebirthSkillTreeCooldownId = "phoenix_soul_ashen_rebirth_cooldown";

    private Coroutine _flameChargeRoutine;
    private float[] _flameChargePerChargeCooldownEnds = Array.Empty<float>();
    private int _flameChargeChargesMax;
    private bool _flameChargeChargesInitialized;
    private int _flameChargeLastKnownMaxCharges;
    private int _flameChargeCastCounter;
    /// <summary>One trail damage tick per enemy per interval, even when multiple ground segments overlap.</summary>
    private readonly Dictionary<EnemyBaseController, float> _flameChargeTrailEnemyNextTickAt = new();
    private readonly List<EnemyBaseController> _flameChargeTrailScratchEnemies = new();
    private float _avatarOfForestEndsAt;
    private float _avatarOfForestDuration;
    private float _lastSyncedAvatarOfForestHudEnd = float.NaN;
    /// <summary>When the Avatar buff expires, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _avatarOfForestCooldownAbilityDef;
    private float _avatarOfForestReplenishAccum;

    private float _queuedPowerSlashWeaponMultiplier = 1f;
    private float _queuedPowerSlashAllDamageMultiplier = 1f;
    private QueuedHitEffect _queuedConsumedThisHit;
    private int _queuedConsumedFrame = -1;

    /// <summary>Same object as <see cref="stats"/>; cached for summon spawn clarity.</summary>
    private CharacterStats _ownerStats;

    /// <summary>Player root transform (this component lives on the player).</summary>
    private Transform _ownerTransform;

    private readonly List<SoulforgedWeaponMinion> _activeSoulforgedWeaponMinions = new();
    private readonly List<SoulforgedWarriorMinion> _activeSoulforgedWarriorMinions = new();
    private readonly List<HawkCompanionMinion> _activeHawkCompanionMinions = new();
    private float _soulforgedAvailabilityCheckPausedUntil;
    private float _nextLingeringActionBarCheckAt;
    private float _nextSoulforgedOrphanScanAt;

    /// <summary>When the Soulforged Weapon summon despawns, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _soulforgedWeaponCooldownAbilityDef;
    private AbilityDefinition _soulforgedWarriorCooldownAbilityDef;
    private AbilityDefinition _hawkCompanionCooldownAbilityDef;
    private float _soulforgedWarriorHudBuffEndsAt;
    private float _soulforgedWarriorHudBuffDuration;

    /// <summary>
    /// HUD buff bookkeeping for Soulforged Weapon (swarm/timed countdown or indefinite full overlay).
    /// </summary>
    private float _soulforgedHudBuffEndsAt;
    private float _soulforgedHudBuffDuration;
    private bool _soulforgedHudPersistOverlay;
    private float _lastSyncedSoulforgedHudEnd = float.NaN;
    private int _lastSyncedSoulforgedHudStacks = int.MinValue;
    private float _hawkCompanionHudBuffEndsAt;
    private float _hawkCompanionHudBuffDuration;
    private float _lastSyncedHawkHudEnd = float.NaN;

    private static string s_pendingSoulforgedRestoreAbilityId;
    private static string s_persistedSoulforgedWeaponCooldownAbilityId;
    private static string s_persistedSoulforgedWarriorCooldownAbilityId;
    private static string s_persistedHawkCompanionCooldownAbilityId;

    private enum QueuedHitEffect
    {
        None,
        PowerSlash,
        TripleShot,
        Rend,
        Envenom,
        CrescentSlash,
        CrusaderStrike
    }

    public struct QueuedHitEffectResult
    {
        public bool suppressDefaultBleed;
        public bool suppressDefaultPoison;
        public bool suppressDefaultElementalMagicAilment;
        public bool triggerCrescentSlash;
        public bool crescentAppliesElemental;
        public bool crescentPenetrating;
    }

    /// <summary>Legacy fallback label for weapon swings (use <see cref="BuildSwingOutgoingAttribution"/> for DPS splits).</summary>
    public string GetBasicAttackOutgoingDamageSourceLabel() => "Auto Attack";

    /// <summary>
    /// Captured at attack start (after queued modifiers). When Power Slash fires, the entire swing's dealt
    /// damage is credited to Power Slash; otherwise the swing is Auto Attack only.
    /// </summary>
    public PlayerCombatController.SwingOutgoingAttribution BuildSwingOutgoingAttribution(
        SplitDamage preModifier,
        SplitDamage postModifier)
    {
        if (_queuedConsumedThisHit == QueuedHitEffect.PowerSlash)
        {
            // Power Slash has no post-hit ConsumeQueuedHitEffects work; with melee impact delay the hit
            // lands on a later frame so ConsumeQueuedHitEffects never clears this — clear now so the
            // next swing is not still credited to Power Slash.
            _queuedConsumedThisHit = QueuedHitEffect.None;
            _queuedConsumedFrame = -1;

            return new PlayerCombatController.SwingOutgoingAttribution(
                "Auto Attack",
                GetAbilityOutgoingDamageSourceLabel(PowerSlashId),
                1f);
        }

        if (_queuedConsumedThisHit == QueuedHitEffect.TripleShot)
        {
            _queuedConsumedThisHit = QueuedHitEffect.None;
            _queuedConsumedFrame = -1;

            return new PlayerCombatController.SwingOutgoingAttribution(
                "Auto Attack",
                GetAbilityOutgoingDamageSourceLabel(TripleShotId),
                1f);
        }

        if (_staticArrowsAppliedThisHit)
        {
            float preTotal = Mathf.Max(0f, preModifier.Total);
            float postTotal = Mathf.Max(0f, postModifier.Total);
            if (postTotal <= 0f)
                return PlayerCombatController.SwingOutgoingAttribution.AutoAttackOnly;

            float bonus = Mathf.Max(0f, postTotal - preTotal);
            float bonusFraction = Mathf.Clamp01(bonus / postTotal);
            if (bonusFraction <= 0f)
                return PlayerCombatController.SwingOutgoingAttribution.AutoAttackOnly;

            return new PlayerCombatController.SwingOutgoingAttribution(
                "Auto Attack",
                GetAbilityOutgoingDamageSourceLabel(StaticArrowsId),
                bonusFraction);
        }

        if (_crusaderStrikeAttributionBucketPending.HasValue)
        {
            DpsDamageBucket bucket = _crusaderStrikeAttributionBucketPending ?? DpsDamageBucket.Physical;
            _crusaderStrikeAttributionBucketPending = null;
            return new PlayerCombatController.SwingOutgoingAttribution(
                "Auto Attack",
                GetAbilityOutgoingDamageSourceLabel(CrusaderStrikeId),
                1f,
                bucket);
        }

        return PlayerCombatController.SwingOutgoingAttribution.AutoAttackOnly;
    }

    public string GetAbilityOutgoingDamageSourceLabel(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return "Ability";

        AbilityDefinition def = GetAbilityDefinition(abilityId);
        if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
            return def.displayName.Trim();

        return abilityId.Replace('_', ' ');
    }

    private void Awake()
    {
        _instance = this;
        if (!player) player = GetComponent<PlayerController>();
        if (!stats) stats = GetComponent<CharacterStats>();
        _ownerStats = stats;
        _ownerTransform = transform;
        if (!combat) combat = GetComponent<PlayerCombatController>();
        if (!equipment) equipment = GetComponent<EquipmentManager>();
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!toolbelt) toolbelt = GetComponent<ToolbeltManager>();
        if (!buffController) buffController = GetComponent<PlayerBuffController>();
        if (!abilityVfx) abilityVfx = GetComponent<PlayerAbilityVfxController>();
        if (!abilityDatabase) abilityDatabase = AbilityDatabase.LoadDefault();
        if (!skillDatabase) skillDatabase = SkillDatabase.LoadDefault();
        if (!skillsManager) skillsManager = SkillsManager.Instance;
        if (!actionBar) ResolveActionBarReference();
        ReclaimPersistedSoulforgedMinionsFromScene();
    }

    private void ResolveActionBarReference()
    {
        ActionBarUI resolved = ActionBarUI.FindForCharacterStats(stats);
        if (resolved)
            actionBar = resolved;
        else if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
    }

    private bool IsActionBarReadyForLingeringAbilityChecks()
    {
        ResolveActionBarReference();
        return actionBar != null && !actionBar.IsSavedStateApplyPending;
    }

    private static bool IsSoulforgedMinionAbilityId(string abilityId) =>
        string.Equals(abilityId, AbilityCombatPower.SoulforgedWeaponAbilityId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(abilityId, AbilityCombatPower.SoulforgedWarriorAbilityId, StringComparison.OrdinalIgnoreCase);

    private static int CountSoulforgedWeaponMinionsInScene()
    {
        SoulforgedWeaponMinion[] found = FindObjectsByType<SoulforgedWeaponMinion>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i])
                count++;
        }

        return count;
    }

    private static int CountSoulforgedWarriorMinionsInScene()
    {
        SoulforgedWarriorMinion[] found = FindObjectsByType<SoulforgedWarriorMinion>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i])
                count++;
        }

        return count;
    }

    private static int CountHawkCompanionMinionsInScene()
    {
        HawkCompanionMinion[] found = FindObjectsByType<HawkCompanionMinion>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i])
                count++;
        }

        return count;
    }

    private void MaybeReclaimOrphanedSoulforgedMinions()
    {
        if (Time.time >= _soulforgedAvailabilityCheckPausedUntil)
            return;
        if (Time.time < _nextSoulforgedOrphanScanAt)
            return;

        _nextSoulforgedOrphanScanAt = Time.time + 0.5f;
        if (CountSoulforgedWeaponMinionsInScene() > _activeSoulforgedWeaponMinions.Count
            || CountSoulforgedWarriorMinionsInScene() > _activeSoulforgedWarriorMinions.Count
            || CountHawkCompanionMinionsInScene() > _activeHawkCompanionMinions.Count)
            ReclaimPersistedSoulforgedMinionsFromScene();
    }

    private void TickLingeringActionBarRemovalIfDue()
    {
        if (!HasLingeringAbilityRuntimeState())
            return;

        float interval = RequiresPerFrameAbilityRuntimeWork()
            ? 0.25f
            : IdleLingeringActionBarCheckInterval;
        if (Time.time < _nextLingeringActionBarCheckAt)
            return;

        _nextLingeringActionBarCheckAt = Time.time + interval;
        MaybeReclaimOrphanedSoulforgedMinions();
        EndActiveLingeringAbilitiesNotOnActionBar();
    }

    private void ReclaimPersistedSoulforgedMinionsFromScene()
    {
        ResolveActionBarReference();

        float graceEnd = Time.time + SoulforgedWeaponSceneLoadActionBarGraceSeconds;
        if (actionBar != null && actionBar.IsSavedStateApplyPending)
            graceEnd = Mathf.Max(graceEnd, Time.unscaledTime + 10f);

        _soulforgedAvailabilityCheckPausedUntil = graceEnd;

        if (!string.IsNullOrWhiteSpace(s_persistedSoulforgedWeaponCooldownAbilityId))
        {
            _soulforgedWeaponCooldownAbilityDef =
                GetAbilityDefinition(s_persistedSoulforgedWeaponCooldownAbilityId);
            s_persistedSoulforgedWeaponCooldownAbilityId = null;
        }

        if (!string.IsNullOrWhiteSpace(s_persistedSoulforgedWarriorCooldownAbilityId))
        {
            _soulforgedWarriorCooldownAbilityDef =
                GetAbilityDefinition(s_persistedSoulforgedWarriorCooldownAbilityId);
            s_persistedSoulforgedWarriorCooldownAbilityId = null;
        }

        if (!string.IsNullOrWhiteSpace(s_persistedHawkCompanionCooldownAbilityId))
        {
            _hawkCompanionCooldownAbilityDef =
                GetAbilityDefinition(s_persistedHawkCompanionCooldownAbilityId);
            s_persistedHawkCompanionCooldownAbilityId = null;
        }

        ReclaimPersistedSoulforgedWeapons();
        ReclaimPersistedSoulforgedWarriors();
        ReclaimPersistedHawkCompanions();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(RestorePendingSoulforgedAfterSceneLoad());
    }

    private void OnDisable()
    {
        if (_instance == this)
            _instance = null;

        if (_finalSeveranceRoutine != null)
        {
            StopCoroutine(_finalSeveranceRoutine);
            _finalSeveranceRoutine = null;
        }

        if (_executionersDescentRoutine != null)
        {
            StopCoroutine(_executionersDescentRoutine);
            EndExecutionersDescentInstanceState();
        }
        else
            abilityVfx?.StopExecutionersDescentVfx();
        GameplayScreenOverlay.Hide(GameplayScreenOverlay.FinalSeveranceChannelId);
        _finalSeveranceChanneling = false;
        if (_bladestormRoutine != null)
        {
            StopCoroutine(_bladestormRoutine);
            _bladestormRoutine = null;
        }

        EndBladestormInstanceState();
        ForceEndHammerTempestEarly(applyCooldown: false);
        ForceEndWhirlwindChannel(clearHeldState: true, applyCooldown: false, lingerGaleforceTwisters: false);
        CancelSnipeCharge(refundResource: true);
        ForceEndCrusaderStrikeCombo(applyCooldown: false);
        ClearCrusaderStrikeFireBalanceBuff();
        ClearPendingMeleeApproachAbility();
        player?.SetTeleportDamageImmune(false);
        player?.SetAbilityChannelLock(false);
        if (_phoenixAshenRebirthImmunityRoutine != null)
        {
            StopCoroutine(_phoenixAshenRebirthImmunityRoutine);
            _phoenixAshenRebirthImmunityRoutine = null;
        }

        buffController?.ClearHudAbilityBuff(CharacterStats.PhoenixSoulAshenRebirthImmunityHudBuffId);

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        abilityVfx?.DestroyLumberFrenzyOrbitVfx();
        abilityVfx?.DestroyAvatarOfTheForestGlowVfx();
        abilityVfx?.DestroyEnergyInfusionGlowVfx();
        abilityVfx?.StopWarBannerVfx();
        abilityVfx?.StopLightningRodVfx();
        ClearAllHuntersSwiftnessTraps();
        ForceEndTornadoEarly(applyCooldown: false, awardDeferredCooldown: false);
        abilityVfx?.DestroyHammerTempestOrbitVfx();
    }

    private void Update()
    {
        if (!RequiresPerFrameAbilityRuntimeWork())
        {
            if (Time.time >= _nextIdleAbilityMaintenanceAt)
            {
                _nextIdleAbilityMaintenanceAt = Time.time + IdleAbilityMaintenanceInterval;
                RunIdleAbilityMaintenance();
            }

            return;
        }

        RunActiveAbilityUpdate();
    }

    private bool RequiresPerFrameAbilityRuntimeWork()
    {
        if (_whirlwindChanneling || _finalSeveranceChanneling || _bladestormChanneling || _snipeCharging)
            return true;
        if (_bladestormRoutine != null || _flameChargeRoutine != null || _executionersDescentRoutine != null)
            return true;
        if (_warBannerActive && _warBannerDeployed)
            return true;
        if (_lightningRodActive)
            return true;
        if (_huntersSwiftnessActive || HasActiveHuntersSwiftnessTraps)
            return true;
        if (_tornadoActive)
            return true;
        if (IsHammerTempestActive)
            return true;
        if (_energyInfusionActive || _lumberFrenzyActive || _fishingFrenzyActive)
            return true;
        if (_avatarOfForestActive || _cleavingChopActive || _spectralAxeActive)
            return true;
        if (_cleavingBuffActive)
            return true;
        if (_crusaderStrikeComboStep > 0 && _crusaderStrikeComboStep < CrusaderStrikeFinalComboStep)
            return true;
        if (_crusaderStrikeQueued || _powerSlashQueued || _tripleShotQueued || _rendQueued || _envenomQueued || _crescentSlashQueued)
            return true;
        if (!string.IsNullOrEmpty(_pendingMeleeApproachAbilityId))
            return true;
        if (abilityVfx != null && abilityVfx.HasActiveGaleforceTwisters)
            return true;
        return false;
    }

    private bool HasLingeringAbilityRuntimeState()
    {
        if (_cleavingBuffActive || _lumberFrenzyActive || _fishingFrenzyActive)
            return true;
        if (_cleavingChopActive || _spectralAxeActive || _avatarOfForestActive)
            return true;
        if (_energyInfusionActive || _warBannerActive || _lightningRodActive || IsHammerTempestActive)
            return true;
        if (_huntersSwiftnessActive || HasActiveHuntersSwiftnessTraps)
            return true;
        if (_tornadoActive)
            return true;
        if (_whirlwindChanneling)
            return true;
        if (_crusaderStrikeComboStep > 0)
            return true;
        if (_battleEngineOverloadStacks > 0)
            return true;
        if (_activeSoulforgedWeaponMinions.Count > 0 || _activeSoulforgedWarriorMinions.Count > 0
            || _activeHawkCompanionMinions.Count > 0)
            return true;
        return false;
    }

    private void RunIdleAbilityMaintenance()
    {
        TickLingeringActionBarRemovalIfDue();
        CleanupCrusaderStrikeIfExpired();
        SyncCrusaderStrikeHudBuff();
        CleanupCrusaderStrikeFireBalanceIfExpired();
        SyncCrusaderStrikeFireBalanceHudBuff();
        CleanupCleavingStrikesIfExpired();
        SyncCleavingStrikesHudBuff();
        CleanupStaticArrowsIfExpired();
        SyncStaticArrowsHudBuff();
        CleanupLumberFrenzyIfExpired();
        CleanupFishingFrenzyIfExpired();
        SyncLumberFrenzyHudBuff();
        SyncFishingFrenzyHudBuff();
        CleanupCleavingChopIfExpired();
        SyncCleavingChopHudBuff();
        CleanupAvatarOfTheForestIfExpired();
        SyncAvatarOfTheForestHudBuff();
        SyncSpectralAxeHudBuff();
        CleanupWarBannerIfExpired();
        SyncWarBannerHudBuff();
        CleanupLightningRodIfExpired();
        SyncLightningRodHudBuff();
        CleanupHuntersSwiftnessIfExpired();
        SyncHuntersSwiftnessHudBuff();
        TickHuntersSwiftnessTraps();
        CleanupTornadoIfExpired();
        SyncTornadoHudBuff();
        CleanupHammerTempestIfExpired();
        SyncHammerTempestHudBuff();
        TickBattleEngineOverloadExpiry();
        TickGuardiansHammerProtectorResolveGuardExpiry();
        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
            ClearBattleEngineOverloadStacksIfAny();
        TickPhoenixSoulBurnRegen(Time.deltaTime);
        SyncSoulforgedWeaponHudBuff();
        SyncHawkCompanionHudBuff();
        SyncSoulforgedWarriorHudBuff();
    }

    private void RunActiveAbilityUpdate()
    {
        TryAutoReleaseQueuedCrescentSlash();
        TickPendingMeleeApproachAbility();
        TickWhirlwindChannel();
        TickSnipeCharge();
        TickGaleforceTwisterDamage();
        SyncWhirlwindHudBuff();
        CleanupCrusaderStrikeIfExpired();
        SyncCrusaderStrikeHudBuff();
        CleanupCrusaderStrikeFireBalanceIfExpired();
        SyncCrusaderStrikeFireBalanceHudBuff();
        CleanupCleavingStrikesIfExpired();
        SyncCleavingStrikesHudBuff();
        CleanupStaticArrowsIfExpired();
        SyncStaticArrowsHudBuff();
        CleanupLumberFrenzyIfExpired();
        CleanupFishingFrenzyIfExpired();
        abilityVfx?.UpdateLumberFrenzyOrbitVfx(_lumberFrenzyActive, _fishingFrenzyActive);
        SyncLumberFrenzyHudBuff();
        SyncFishingFrenzyHudBuff();
        CleanupCleavingChopIfExpired();
        SyncCleavingChopHudBuff();
        ResourceNode cleavingOriginNode = player != null ? player.CurrentTarget : null;
        Vector3? cleavingIndicatorOrigin = null;
        if (IsCleavingChopActive &&
            cleavingOriginNode != null &&
            cleavingOriginNode.ActionType == NodeAction.Woodcutting)
        {
            cleavingIndicatorOrigin = cleavingOriginNode.transform.position;
        }

        abilityVfx?.UpdateCleavingChopRangeIndicator(IsCleavingChopActive, GetCleavingChopRange(), cleavingIndicatorOrigin);
        abilityVfx?.UpdateWoodcuttingTreeRangeOutlines(this);
        if (TryGetSpectralAxeGatherArea(out Vector3 spectralAxeCenter, out float spectralAxeRadius))
        {
            Transform spectralFollow = _spectralAxeProjectile != null ? _spectralAxeProjectile.transform : null;
            abilityVfx?.EnsureSpectralAxeAreaIndicatorBuilt(spectralFollow);
            abilityVfx?.UpdateSpectralAxeAreaIndicator(spectralAxeCenter, spectralAxeRadius);
        }
        CleanupAvatarOfTheForestIfExpired();
        abilityVfx?.UpdateAvatarOfTheForestGlowVfx(IsAvatarOfTheForestActive);
        SyncAvatarOfTheForestHudBuff();
        TickAvatarOfTheForestNearbyReplenish(Time.deltaTime);
        SyncSpectralAxeHudBuff();
        TickLingeringActionBarRemovalIfDue();
        CleanupWarBannerIfExpired();
        TickWarBanner();
        SyncWarBannerHudBuff();
        CleanupLightningRodIfExpired();
        TickLightningRod();
        SyncLightningRodHudBuff();
        CleanupHuntersSwiftnessIfExpired();
        TickHuntersSwiftness();
        SyncHuntersSwiftnessHudBuff();
        CleanupTornadoIfExpired();
        SyncTornadoHudBuff();
        TickHammerTempest();
        CleanupHammerTempestIfExpired();
        SyncHammerTempestHudBuff();
        TickBattleEngineOverloadExpiry();
        TickGuardiansHammerProtectorResolveGuardExpiry();
        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
            ClearBattleEngineOverloadStacksIfAny();
        TickPhoenixSoulBurnRegen(Time.deltaTime);
        SyncSoulforgedWeaponHudBuff();
        SyncHawkCompanionHudBuff();
        SyncSoulforgedWarriorHudBuff();
        // Energy Infusion glow is static once spawned; avoid per-frame transform dirtying.
    }

    private void LateUpdate()
    {
        // After PlayerController.TickRegen so mana regen is converted in the same frame.
        TickEnergyInfusion(Time.deltaTime);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveActionBarReference();

        ReclaimPersistedSoulforgedMinionsFromScene();
        _nextSoulforgedOrphanScanAt = 0f;
        _nextLingeringActionBarCheckAt = 0f;
        StartCoroutine(RefreshPersistedSoulforgedMinionsAfterSceneLoad());

        _lastSyncedSoulforgedHudEnd = float.NaN;
        _lastSyncedSoulforgedHudStacks = int.MinValue;
    }

    private IEnumerator RefreshPersistedSoulforgedMinionsAfterSceneLoad()
    {
        for (int i = 0; i < 10; i++)
            yield return null;

        if (!_ownerStats)
            _ownerStats = stats;
        if (!player)
            player = GetComponent<PlayerController>();
        if (!_ownerStats || !player)
            yield break;

        Transform ownerRoot = _ownerStats.transform;
        Transform rangeOrigin = _ownerTransform ? _ownerTransform : ownerRoot;
        Transform weaponHome = player.SoulforgedWeaponSpawnPoint;

        for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
        {
            SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
            if (!minion)
                continue;

            minion.RefreshAfterSceneLoad(_ownerStats, weaponHome, rangeOrigin, HandleSoulforgedWeaponReleased);
        }

        for (int i = 0; i < _activeSoulforgedWarriorMinions.Count; i++)
        {
            SoulforgedWarriorMinion minion = _activeSoulforgedWarriorMinions[i];
            if (!minion)
                continue;

            minion.RefreshAfterSceneLoad(_ownerStats, ownerRoot, rangeOrigin);
        }

        for (int i = 0; i < _activeHawkCompanionMinions.Count; i++)
        {
            HawkCompanionMinion minion = _activeHawkCompanionMinions[i];
            if (!minion)
                continue;

            minion.RefreshAfterSceneLoad(_ownerStats, ownerRoot, rangeOrigin, HandleHawkCompanionReleased);
        }

        SyncSoulforgedWeaponHudBuff();
        SyncHawkCompanionHudBuff();
        SyncSoulforgedWarriorHudBuff();
    }

    private void ReclaimPersistedSoulforgedWeapons()
    {
        CleanupSoulforgedWeaponList();
        SoulforgedWeaponMinion[] found = FindObjectsByType<SoulforgedWeaponMinion>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < found.Length; i++)
        {
            SoulforgedWeaponMinion minion = found[i];
            if (!minion)
                continue;

            if (!_activeSoulforgedWeaponMinions.Contains(minion))
            {
                _activeSoulforgedWeaponMinions.Add(minion);
                minion.BindReleasedCallback(HandleSoulforgedWeaponReleased);
            }
        }
    }

    private void ReclaimPersistedSoulforgedWarriors()
    {
        CleanupSoulforgedWarriorList();
        SoulforgedWarriorMinion[] found = FindObjectsByType<SoulforgedWarriorMinion>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < found.Length; i++)
        {
            SoulforgedWarriorMinion minion = found[i];
            if (!minion)
                continue;

            if (!_activeSoulforgedWarriorMinions.Contains(minion))
            {
                _activeSoulforgedWarriorMinions.Add(minion);
                minion.BindReleasedCallback(HandleSoulforgedWarriorReleased);
            }
        }
    }

    private void ReclaimPersistedHawkCompanions()
    {
        CleanupHawkCompanionList();
        HawkCompanionMinion[] found = FindObjectsByType<HawkCompanionMinion>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < found.Length; i++)
        {
            HawkCompanionMinion minion = found[i];
            if (!minion)
                continue;

            if (!_activeHawkCompanionMinions.Contains(minion))
            {
                _activeHawkCompanionMinions.Add(minion);
                minion.BindReleasedCallback(HandleHawkCompanionReleased);
            }
        }
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

        TrySpawnSoulforgedWeaponMinion(def, recordDamageMeterSummonUse: false);
    }

    /// <summary>Dev testing — clears action-bar ability cooldowns, GCD, and Flame Charge charge timers.</summary>
    public void DevTesting_ClearAllAbilityCooldowns()
    {
        _cooldownEndsById.Clear();
        _cooldownEndsByRowKey.Clear();
        _globalCooldownEndsAt = 0f;

        if (_flameChargePerChargeCooldownEnds != null)
        {
            for (int i = 0; i < _flameChargePerChargeCooldownEnds.Length; i++)
                _flameChargePerChargeCooldownEnds[i] = 0f;
        }
    }

    public bool IsOnCooldown(string abilityId, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (string.Equals(abilityId, PhoenixAshenRebirthSkillTreeCooldownId, StringComparison.OrdinalIgnoreCase))
        {
            if (Time.time >= _phoenixAshenRebirthCooldownEndsAt)
                return false;

            remainingSeconds = _phoenixAshenRebirthCooldownEndsAt - Time.time;
            return remainingSeconds > 0f;
        }

        if (string.Equals(abilityId, FlameChargeId, StringComparison.OrdinalIgnoreCase))
        {
            RefreshFlameChargeChargesFromSkillTree();
            if (GetFlameChargeReadyChargeCount() > 0)
                return false;

            remainingSeconds = GetFlameChargeSoonestRechargingChargeRemaining();
            return remainingSeconds > 0f;
        }

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

    /// <summary>
    /// True when the ability has an active HUD buff row and/or a known lingering runtime state (used by the skill tree).
    /// </summary>
    public bool IsAbilityBuffOrLingeringActive(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (IsOnCooldown(abilityId, out _))
            return false;

        if (buffController && buffController.IsHudAbilityBuffActive(abilityId))
            return true;

        if (string.Equals(abilityId, LumberFrenzyId, StringComparison.OrdinalIgnoreCase))
            return IsLumberFrenzyActive;
        if (string.Equals(abilityId, FishingFrenzyId, StringComparison.OrdinalIgnoreCase))
            return IsFishingFrenzyActive;
        if (string.Equals(abilityId, CleavingChopId, StringComparison.OrdinalIgnoreCase))
            return IsCleavingChopActive;
        if (string.Equals(abilityId, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase))
            return IsAvatarOfTheForestActive;
        if (string.Equals(abilityId, SpectralAxeId, StringComparison.OrdinalIgnoreCase))
            return IsSpectralAxeActive;
        if (string.Equals(abilityId, CleavingStrikesId, StringComparison.OrdinalIgnoreCase))
            return _cleavingBuffActive;
        if (string.Equals(abilityId, StaticArrowsId, StringComparison.OrdinalIgnoreCase))
            return _staticArrowsBuffActive;
        if (string.Equals(abilityId, EnergyInfusionId, StringComparison.OrdinalIgnoreCase))
            return _energyInfusionActive;
        if (IsWarBannerAbilityId(abilityId))
            return IsWarBannerActive;
        if (IsLightningRodAbilityId(abilityId))
            return IsLightningRodActive;
        if (IsPenetratingShotAbilityId(abilityId))
            return IsPenetratingShotInFlight;
        if (IsHuntersSwiftnessAbilityId(abilityId))
            return IsHuntersSwiftnessActive;
        if (IsTornadoAbilityId(abilityId))
            return IsTornadoActive;
        if (string.Equals(abilityId, HammerTempestId, StringComparison.OrdinalIgnoreCase))
            return IsHammerTempestActive;
        if (string.Equals(abilityId, AbilityCombatPower.SoulforgedWeaponAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            if (_activeSoulforgedWeaponMinions.Count > 0)
                return true;
            return Time.time < _soulforgedAvailabilityCheckPausedUntil
                   && CountSoulforgedWeaponMinionsInScene() > 0;
        }

        if (string.Equals(abilityId, AbilityCombatPower.SoulforgedWarriorAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            if (_activeSoulforgedWarriorMinions.Count > 0)
                return true;
            return Time.time < _soulforgedAvailabilityCheckPausedUntil
                   && CountSoulforgedWarriorMinionsInScene() > 0;
        }

        return false;
    }

    private static readonly HashSet<string> HudBuffIdsNotDismissableFromPanel =
        new(StringComparer.OrdinalIgnoreCase)
        {
            CrusaderStrikeId,
            CrusaderStrikeFireBalanceHudBuffId,
            CharacterStats.TacticianDualityHudBuffId,
            CharacterStats.ShadowHunterHudBuffId,
            CharacterStats.BattleEngineOverloadHudBuffId,
            PlayerController.WoodcuttingFlowStateHudBuffId,
            PlayerController.FishingCalmWatersMajorHudBuffId,
            AbilityCombatPower.WayOfTheBladeDancerKillCritHudBuffId,
            PlayerCombatController.WayOfTheBerserkerHudBuffId,
            PlayerCombatController.WayOfTheBerserkerLeechHudBuffId,
            PlayerCombatController.WayOfTheCrusaderHudBuffId,
            AbilityCombatPower.BloodbathHudBuffId,
            CharacterStats.PhoenixSoulAshenRebirthImmunityHudBuffId,
            PlayerSprintInput.SprintHudBuffId,
        };

    /// <summary>
    /// HUD strip rows driven by passives/stats/capstones — not lingering abilities that should end when
    /// removed from the action bar. Excluded from <see cref="EndActiveLingeringAbilitiesNotOnActionBar"/>.
    /// </summary>
    private static bool IsPassiveOrStatHudBuffStripRow(string hudBuffId) =>
        !string.IsNullOrWhiteSpace(hudBuffId) && HudBuffIdsNotDismissableFromPanel.Contains(hudBuffId);

    /// <summary>Right-click dismiss on the buff strip — ends lingering abilities/minions; not combo-phase trackers.</summary>
    public bool TryDismissHudBuffFromPanel(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId) || HudBuffIdsNotDismissableFromPanel.Contains(abilityId))
            return false;

        if (!buffController)
            buffController = GetComponent<PlayerBuffController>();
        if (buffController == null || !buffController.IsHudAbilityBuffActive(abilityId))
            return false;

        if (string.Equals(abilityId, WhirlwindId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndWhirlwindChannel(clearHeldState: true, applyCooldown: true);
            return true;
        }

        ForceEndLingeringAbilityForSkillTreeReset(abilityId);
        return true;
    }

    /// <summary>
    /// When the skill tree row is reset while this ability is active, ends buff/lingering state and applies cooldown
    /// the same way natural expiry would (Cleaving Strikes only clears its window — it already cooled down on cast).
    /// </summary>
    public void ForceEndLingeringAbilityForSkillTreeReset(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return;

        if (string.Equals(abilityId, CleavingStrikesId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndCleavingStrikesBuffEarly();
            return;
        }
        if (string.Equals(abilityId, StaticArrowsId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndStaticArrowsBuffEarly();
            return;
        }

        if (string.Equals(abilityId, LumberFrenzyId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndLumberFrenzyEarly();
            return;
        }

        if (string.Equals(abilityId, FishingFrenzyId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndFishingFrenzyEarly();
            return;
        }

        if (string.Equals(abilityId, CleavingChopId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndCleavingChopEarly();
            return;
        }

        if (string.Equals(abilityId, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndAvatarOfTheForestEarly();
            return;
        }

        if (string.Equals(abilityId, EnergyInfusionId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndEnergyInfusionEarly(applyCooldown: true);
            return;
        }

        if (IsWarBannerAbilityId(abilityId))
        {
            ForceEndWarBannerEarly(applyCooldown: true);
            return;
        }

        if (IsLightningRodAbilityId(abilityId))
        {
            ForceEndLightningRodEarly(applyCooldown: true, runExpiryChain: false);
            return;
        }

        if (IsPenetratingShotAbilityId(abilityId))
        {
            AbortPenetratingShotInFlight();
            return;
        }

        if (IsHuntersSwiftnessAbilityId(abilityId))
        {
            ForceEndHuntersSwiftnessEarly(applyCooldown: true, clearTraps: false, awardDeferredCooldown: true);
            return;
        }

        if (IsTornadoAbilityId(abilityId))
        {
            ForceEndTornadoEarly(applyCooldown: true, awardDeferredCooldown: true);
            return;
        }

        if (string.Equals(abilityId, HammerTempestId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndHammerTempestEarly(applyCooldown: true);
            return;
        }

        if (string.Equals(abilityId, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndCrusaderStrikeCombo(applyCooldown: false);
            return;
        }

        if (string.Equals(abilityId, FlameChargeId, StringComparison.OrdinalIgnoreCase))
        {
            if (_flameChargeRoutine != null)
            {
                StopCoroutine(_flameChargeRoutine);
                _flameChargeRoutine = null;
            }

            abilityVfx?.EndFlameChargePlayerGlow();
            player?.SetTeleportDamageImmune(false);
            IsFlameChargeDashing = false;
            _flameChargeChargesInitialized = false;
            _flameChargePerChargeCooldownEnds = Array.Empty<float>();
            return;
        }

        if (string.Equals(abilityId, SpectralAxeId, StringComparison.OrdinalIgnoreCase))
        {
            if (_spectralAxeActive || _spectralAxeRoutine != null || _spectralAxeProjectile != null)
                AbortSpectralAxe(awardCooldown: true);
            return;
        }

        if (string.Equals(abilityId, AbilityCombatPower.SoulforgedWeaponAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            if (_activeSoulforgedWeaponMinions.Count > 0)
                EndSoulforgedAndStartCooldown();
            return;
        }

        if (string.Equals(abilityId, AbilityCombatPower.SoulforgedWarriorAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            if (_activeSoulforgedWarriorMinions.Count > 0)
                EndSoulforgedWarriorAndStartCooldown();
            return;
        }

        if (string.Equals(abilityId, AbilityCombatPower.HawkCompanionAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            if (_activeHawkCompanionMinions.Count > 0)
                EndHawkCompanionAndStartCooldown();
            return;
        }

        ForceEndGenericHudAbilityBuffWithCooldown(abilityId);
    }

    /// <summary>
    /// Ends active minion summons, channeled abilities, and timed buffs when their ability is no longer
    /// assigned to any loadout slot (combat set 1/2 or any gathering strip). Set swaps alone do not count
    /// as removal. Called on slot assignment changes and each frame as a safety net.
    /// </summary>
    public void HandleActionBarAssignmentsChanged()
    {
        _nextLingeringActionBarCheckAt = 0f;
        _nextIdleAbilityMaintenanceAt = 0f;
        MaybeReclaimOrphanedSoulforgedMinions();
        EndActiveLingeringAbilitiesNotOnActionBar();
    }

    private void EndActiveLingeringAbilitiesNotOnActionBar()
    {
        if (!HasLingeringAbilityRuntimeState())
            return;

        ResolveActionBarReference();
        if (!actionBar)
            return;

        TryEndLingeringIfRemovedFromActionBar(CleavingStrikesId);
        TryEndLingeringIfRemovedFromActionBar(StaticArrowsId);
        TryEndLingeringIfRemovedFromActionBar(LumberFrenzyId);
        TryEndLingeringIfRemovedFromActionBar(FishingFrenzyId);
        TryEndLingeringIfRemovedFromActionBar(CleavingChopId);
        TryEndLingeringIfRemovedFromActionBar(AvatarOfTheForestId);
        TryEndLingeringIfRemovedFromActionBar(EnergyInfusionId);
        TryEndLingeringIfRemovedFromActionBar(AbilityCombatPower.WarBannerAbilityId);
        TryEndLingeringIfRemovedFromActionBar(AbilityCombatPower.LightningRodAbilityId);
        TryEndLingeringIfRemovedFromActionBar(AbilityCombatPower.PenetratingShotAbilityId);
        TryEndLingeringIfRemovedFromActionBar(AbilityCombatPower.HuntersSwiftnessAbilityId);
        TryEndLingeringIfRemovedFromActionBar(AbilityCombatPower.TornadoAbilityId);
        TryEndLingeringIfRemovedFromActionBar(HammerTempestId);
        TryEndLingeringIfRemovedFromActionBar(CrusaderStrikeId);
        TryEndLingeringIfRemovedFromActionBar(FlameChargeId);
        TryEndLingeringIfRemovedFromActionBar(SpectralAxeId);
        TryEndLingeringIfRemovedFromActionBar(AbilityCombatPower.SoulforgedWeaponAbilityId);
        TryEndLingeringIfRemovedFromActionBar(AbilityCombatPower.SoulforgedWarriorAbilityId);
        TryEndLingeringIfRemovedFromActionBar(AbilityCombatPower.HawkCompanionAbilityId);

        if (_whirlwindChanneling && !actionBar.HasAbilityOnLoadout(WhirlwindId))
            ForceEndWhirlwindChannel(clearHeldState: true, applyCooldown: true);

        if (!buffController)
            buffController = GetComponent<PlayerBuffController>();
        if (buffController == null)
            return;

        IReadOnlyList<PlayerBuffController.ActiveBuff> hudBuffs = buffController.ActiveBuffs;
        for (int i = 0; i < hudBuffs.Count; i++)
        {
            PlayerBuffController.ActiveBuff buff = hudBuffs[i];
            if (buff.type != ConsumableEffectType.HudAbilityBuff || string.IsNullOrWhiteSpace(buff.id))
                continue;
            if (IsPassiveOrStatHudBuffStripRow(buff.id))
                continue;
            if (ShouldSkipActionBarRemovalForAbility(buff.id))
                continue;
            if (IsWarBannerAbilityId(buff.id) && IsWarBannerOnActionBar())
                continue;
            if (actionBar.HasAbilityOnLoadout(buff.id))
                continue;

            ForceEndLingeringAbilityForSkillTreeReset(buff.id);
        }
    }

    private void TryEndLingeringIfRemovedFromActionBar(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId) || ShouldSkipActionBarRemovalForAbility(abilityId))
            return;
        if (IsWarBannerAbilityId(abilityId))
        {
            if (IsWarBannerOnActionBar())
                return;
        }
        else if (actionBar.HasAbilityOnLoadout(abilityId))
        {
            return;
        }

        if (!IsAbilityBuffOrLingeringActive(abilityId))
            return;

        ForceEndLingeringAbilityForSkillTreeReset(abilityId);
    }

    private bool ShouldSkipActionBarRemovalForAbility(string abilityId)
    {
        if (!IsSoulforgedMinionAbilityId(abilityId))
            return false;

        if (Time.time < _soulforgedAvailabilityCheckPausedUntil)
            return true;

        if (!IsActionBarReadyForLingeringAbilityChecks())
            return true;

        return false;
    }

    /// <summary>Skill tree active overlay: only for finite-duration HUD buffs (not indefinite minion / until-dismissed).</summary>
    public bool TryGetAbilitySkillTreeActiveBuffTimer(string abilityId, out float remainingSecondsForDisplay)
    {
        remainingSecondsForDisplay = 0f;
        if (buffController == null)
            return false;

        return buffController.ShouldDisplayHudAbilityBuffTimedPresentation(abilityId, out remainingSecondsForDisplay);
    }

    public bool TryGetForcedAutoBattleAbilityId(out string abilityId)
    {
        abilityId = null;
        return false;
    }

    private bool IsCrusaderStrikeBusyThisAttack()
    {
        return _crusaderStrikeQueued ||
               _queuedConsumedThisHit == QueuedHitEffect.CrusaderStrike ||
               _queuedCrusaderStrikeConsumedStage > 0;
    }

    private bool IsCrusaderStrikeComboInProgress()
    {
        if (IsCrusaderStrikeBusyThisAttack())
            return true;

        return _crusaderStrikeComboStep > 0 && _crusaderStrikeComboStep < CrusaderStrikeFinalComboStep;
    }

    private void ForceEndCrusaderStrikeCombo(bool applyCooldown)
    {
        AbilityDefinition def = applyCooldown ? GetAbilityDefinition(CrusaderStrikeId) : null;
        _crusaderStrikeComboStep = 0;
        _crusaderStrikePrimedStage = 0;
        _crusaderStrikeQueued = false;
        _queuedCrusaderStrikeConsumedStage = 0;
        _crusaderStrikeAttributionBucketPending = null;
        _crusaderStrikeComboTimeoutAt = 0f;
        _lastSyncedCrusaderStrikeHudStacks = int.MinValue;
        if (buffController != null && buffController.IsHudAbilityBuffActive(CrusaderStrikeId))
            buffController.ClearHudAbilityBuff(CrusaderStrikeId);
        if (applyCooldown && def != null)
            StartCooldown(def);
    }

    private void RefreshCrusaderStrikeComboTimeout()
    {
        _crusaderStrikeComboTimeoutAt = Time.time + CrusaderStrikeComboTimeoutSeconds;
    }

    private void CleanupCrusaderStrikeIfExpired()
    {
        bool comboActive = IsCrusaderStrikeComboInProgress();
        if (!comboActive)
        {
            _crusaderStrikeComboTimeoutAt = 0f;
            return;
        }

        if (_crusaderStrikeComboTimeoutAt <= 0f || Time.time < _crusaderStrikeComboTimeoutAt)
            return;

        ForceEndCrusaderStrikeCombo(applyCooldown: true);
    }

    private void SyncCrusaderStrikeHudBuff()
    {
        if (!buffController)
            return;

        if (_crusaderStrikeComboStep <= 0 || _crusaderStrikeComboStep >= CrusaderStrikeFinalComboStep)
        {
            if (buffController.IsHudAbilityBuffActive(CrusaderStrikeId))
                buffController.ClearHudAbilityBuff(CrusaderStrikeId);
            _lastSyncedCrusaderStrikeHudStacks = int.MinValue;
            return;
        }

        int stacks = Mathf.Clamp(_crusaderStrikeComboStep, 1, CrusaderStrikeFinalComboStep - 1);
        if (_lastSyncedCrusaderStrikeHudStacks == stacks)
            return;

        _lastSyncedCrusaderStrikeHudStacks = stacks;
        buffController.SetHudAbilityBuff(CrusaderStrikeId, stacks, 0f, 0f, persistActiveOverlay: true);
    }

    private int GetCrusaderStrikeSelectedChoice()
    {
        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.CrusaderStrikeEnhancementParentSpineNodeId,
            -1);
    }

    private float GetCrusaderStrikeHealFraction() =>
        GetCrusaderStrikeSelectedChoice() == CrusaderStrikeSacredRestorationChoiceIndex
            ? AbilityCombatPower.CrusaderStrikeSacredRestorationHealFractionOfMaxHealth
            : AbilityCombatPower.CrusaderStrikeHealFractionOfMaxHealth;

    private bool IsCrusaderStrikeSecondCastFree()
    {
        if (GetCrusaderStrikeSelectedChoice() != CrusaderStrikeSacredRestorationChoiceIndex)
            return false;

        int nextCastStep = Mathf.Clamp(_crusaderStrikeComboStep + 1, 1, CrusaderStrikeFinalComboStep);
        return nextCastStep == 2;
    }

    private bool HasCrusaderStrikeFireBalanceBuff() =>
        _crusaderStrikeFireBalanceBuffEndsAt > Time.time &&
        GetCrusaderStrikeSelectedChoice() == CrusaderStrikeFireBalanceChoiceIndex;

    public bool IsCrusaderStrikeFireBalanceBuffActive => HasCrusaderStrikeFireBalanceBuff();

    private void ActivateCrusaderStrikeFireBalanceBuff()
    {
        _crusaderStrikeFireBalanceBuffDuration = AbilityCombatPower.CrusaderStrikeFireBalanceBuffDurationSeconds;
        _crusaderStrikeFireBalanceBuffEndsAt = Time.time + _crusaderStrikeFireBalanceBuffDuration;
        _lastSyncedCrusaderStrikeFireBalanceHudEnd = float.NaN;
        SyncCrusaderStrikeFireBalanceHudBuff();
    }

    private void ClearCrusaderStrikeFireBalanceBuff()
    {
        _crusaderStrikeFireBalanceBuffEndsAt = 0f;
        _crusaderStrikeFireBalanceBuffDuration = 0f;
        _lastSyncedCrusaderStrikeFireBalanceHudEnd = float.NaN;
        buffController?.ClearHudAbilityBuff(CrusaderStrikeFireBalanceHudBuffId);
    }

    private void CleanupCrusaderStrikeFireBalanceIfExpired()
    {
        if (_crusaderStrikeFireBalanceBuffEndsAt <= 0f)
            return;

        if ((player != null && player.IsDead) || (stats != null && stats.IsDead) || Time.time >= _crusaderStrikeFireBalanceBuffEndsAt)
            ClearCrusaderStrikeFireBalanceBuff();
    }

    private void SyncCrusaderStrikeFireBalanceHudBuff()
    {
        if (!buffController)
            return;

        if (_crusaderStrikeFireBalanceBuffEndsAt <= Time.time || _crusaderStrikeFireBalanceBuffDuration <= 0f)
        {
            if (buffController.IsHudAbilityBuffActive(CrusaderStrikeFireBalanceHudBuffId))
                buffController.ClearHudAbilityBuff(CrusaderStrikeFireBalanceHudBuffId);
            _lastSyncedCrusaderStrikeFireBalanceHudEnd = float.NaN;
            return;
        }

        if (Mathf.Abs(_lastSyncedCrusaderStrikeFireBalanceHudEnd - _crusaderStrikeFireBalanceBuffEndsAt) < 0.01f &&
            buffController.IsHudAbilityBuffActive(CrusaderStrikeFireBalanceHudBuffId))
            return;

        _lastSyncedCrusaderStrikeFireBalanceHudEnd = _crusaderStrikeFireBalanceBuffEndsAt;
        buffController.SetHudAbilityBuff(
            CrusaderStrikeFireBalanceHudBuffId,
            1,
            _crusaderStrikeFireBalanceBuffEndsAt,
            _crusaderStrikeFireBalanceBuffDuration);
    }

    public void ApplyActiveDamageConversions(ref SplitDamage hit)
    {
        if (!HasCrusaderStrikeFireBalanceBuff())
            return;

        float converted = Mathf.Max(0f, hit.physical) * AbilityCombatPower.CrusaderStrikeFireBalancePhysicalToFireFraction;
        if (converted <= 0f)
            return;

        hit = new SplitDamage(
            Mathf.Max(0f, hit.physical - converted),
            Mathf.Max(0f, hit.magic + converted),
            Mathf.Max(0f, hit.corruptionDamage));
    }

    private void ForceEndCleavingStrikesBuffEarly()
    {
        if (!_cleavingBuffActive)
            return;

        _cleavingBuffActive = false;
        _cleavingAdditionalTargets = 0;
        _cleavingHitsRemaining = 0;
        _cleavingBuffEndsAt = 0f;
        _cleavingBuffDuration = 0f;
        SyncCleavingStrikesHudBuff();
    }

    private void ForceEndStaticArrowsBuffEarly()
    {
        if (!_staticArrowsBuffActive)
            return;

        _staticArrowsHitsRemaining = 0;
        FinishStaticArrowsBuffAndStartCooldown();
    }

    private void ForceEndLumberFrenzyEarly()
    {
        if (!_lumberFrenzyActive)
            return;

        _lumberFrenzyActive = false;
        _lumberFrenzyEndsAt = 0f;
        _lumberFrenzyDuration = 0f;

        if (!_fishingFrenzyActive)
            abilityVfx?.DestroyLumberFrenzyOrbitVfx();

        if (_lumberFrenzyCooldownAbilityDef)
            StartCooldown(_lumberFrenzyCooldownAbilityDef);
        _lumberFrenzyCooldownAbilityDef = null;

        _lastSyncedLumberFrenzyHudEnd = float.NaN;
        SyncLumberFrenzyHudBuff();
        stats?.NotifyStatsChanged();
    }

    private void ForceEndCleavingChopEarly()
    {
        if (!IsCleavingChopActive)
            return;

        _cleavingChopActive = false;
        _cleavingChopEndsAt = 0f;
        _cleavingChopDuration = 0f;

        if (_cleavingChopCooldownAbilityDef)
            StartCooldown(_cleavingChopCooldownAbilityDef);
        _cleavingChopCooldownAbilityDef = null;

        _lastSyncedCleavingChopHudEnd = float.NaN;
        SyncCleavingChopHudBuff();
    }

    private bool TryHandleToggleAbilityUse(AbilityDefinition def, bool allowToggleOff = true)
    {
        if (def == null || def.tag != AbilityTag.ToggleBuff)
            return false;

        if (!string.Equals(def.abilityId, EnergyInfusionId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (_energyInfusionActive)
        {
            if (!allowToggleOff)
                return false;

            ForceEndEnergyInfusionEarly(applyCooldown: false);
            return true;
        }

        if (IsOnCooldown(def.abilityId, out _))
            return false;

        ActivateEnergyInfusion(def);
        StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        LogAbilityUsed(def);
        return true;
    }

    private void ActivateEnergyInfusion(AbilityDefinition def)
    {
        _energyInfusionActive = true;
        ApplyEnergyInfusionCombatModifiers();
        abilityVfx?.SpawnEnergyInfusionGlowVfx();
        SyncEnergyInfusionHudBuff();
        stats?.NotifyStatsChanged();
    }

    private void ForceEndEnergyInfusionEarly(bool applyCooldown)
    {
        if (!_energyInfusionActive)
            return;

        _energyInfusionActive = false;
        if (stats != null)
            stats.CombatFlatManaRegenPerSecond = 0f;
        _whirlwindUsedEnergyInfusionMana = false;

        abilityVfx?.DestroyEnergyInfusionGlowVfx();
        SyncEnergyInfusionHudBuff();

        if (applyCooldown)
        {
            AbilityDefinition def = GetAbilityDefinition(EnergyInfusionId);
            if (def != null && def.cooldown > 0f)
                StartCooldown(def);
        }

        stats?.NotifyStatsChanged();
    }

    public bool IsHammerTempestActive =>
        _hammerTempestActive && Time.time < _hammerTempestEndsAt;

    private void ActivateHammerTempest()
    {
        _hammerTempestActive = true;
        _hammerTempestDuration = AbilityCombatPower.HammerTempestBaseDurationSeconds;
        AbilityDefinition def = GetAbilityDefinition(HammerTempestId);
        if (def != null && def.tooltipBuffMinionDurationSeconds > 0.01f)
            _hammerTempestDuration = def.tooltipBuffMinionDurationSeconds;
        if (GetHammerTempestSelectedChoice() == HammerTempestSacredArsenalChoiceIndex)
            _hammerTempestDuration = Mathf.Max(
                0.1f,
                _hammerTempestDuration - AbilityCombatPower.HammerTempestSacredArsenalDurationPenaltySeconds);

        _hammerTempestEndsAt = Time.time + _hammerTempestDuration;
        _hammerTempestNextTickAt = Time.time;
        _hammerTempestLastHitTimeByEnemyId.Clear();
        _hammerTempestMomentumByEnemyId.Clear();
        _lastSyncedHammerTempestHudEnd = float.NaN;
        SyncHammerTempestHudBuff();
        abilityVfx?.SpawnHammerTempestOrbitVfx(_hammerTempestDuration, GetHammerTempestOrbitHammerCount());
        TickHammerTempest();
    }

    private void ForceEndHammerTempestEarly(bool applyCooldown)
    {
        if (!_hammerTempestActive && !IsHammerTempestActive)
            return;

        _hammerTempestActive = false;
        _hammerTempestEndsAt = 0f;
        _hammerTempestDuration = 0f;
        _hammerTempestNextTickAt = 0f;
        _hammerTempestLastHitTimeByEnemyId.Clear();
        _hammerTempestMomentumByEnemyId.Clear();
        abilityVfx?.DestroyHammerTempestOrbitVfx();
        _lastSyncedHammerTempestHudEnd = float.NaN;
        SyncHammerTempestHudBuff();

        if (applyCooldown)
        {
            AbilityDefinition def = GetAbilityDefinition(HammerTempestId);
            if (def != null && def.cooldown > 0f)
                StartCooldown(def);
        }
    }

    private void CleanupHammerTempestIfExpired()
    {
        if (!_hammerTempestActive)
            return;

        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
        {
            ForceEndHammerTempestEarly(applyCooldown: false);
            return;
        }

        if (Time.time < _hammerTempestEndsAt)
            return;

        ForceEndHammerTempestEarly(applyCooldown: false);
    }

    private void SyncHammerTempestHudBuff()
    {
        if (!buffController)
            return;

        if (!IsHammerTempestActive)
        {
            if (buffController.IsHudAbilityBuffActive(HammerTempestId))
                buffController.ClearHudAbilityBuff(HammerTempestId);
            _lastSyncedHammerTempestHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedHammerTempestHudEnd, _hammerTempestEndsAt))
            return;

        _lastSyncedHammerTempestHudEnd = _hammerTempestEndsAt;
        buffController.SetHudAbilityBuff(HammerTempestId, 1, _hammerTempestEndsAt, _hammerTempestDuration);
    }

    private void TickHammerTempest()
    {
        if (!IsHammerTempestActive)
            return;

        if (player == null || stats == null || player.IsDead || stats.IsDead)
        {
            ForceEndHammerTempestEarly(applyCooldown: false);
            return;
        }

        AbilityDefinition def = GetAbilityDefinition(HammerTempestId);
        if (!def || !IsAbilityAllowedBySkillProgress(def) || !CanUseWithEquippedWeapon(def))
        {
            ForceEndHammerTempestEarly(applyCooldown: false);
            return;
        }

        CleanupExpiredHammerTempestMomentum();

        float interval = GetHammerTempestHitIntervalSeconds();
        while (IsHammerTempestActive && Time.time + 0.0001f >= _hammerTempestNextTickAt)
        {
            float tickAt = _hammerTempestNextTickAt;
            TryHammerTempestDamageTick(def, tickAt, interval);
            _hammerTempestNextTickAt += interval;
        }
    }

    private void TryHammerTempestDamageTick(AbilityDefinition def, float tickAt, float intervalSeconds)
    {
        if (def == null || stats == null)
            return;

        intervalSeconds = Mathf.Max(0.01f, intervalSeconds);
        float radius = GetHammerTempestHitRadius();
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            if (!IsEnemyWithinHammerTempestRange(enemy, radius))
                continue;

            int enemyId = enemy.GetInstanceID();
            if (_hammerTempestLastHitTimeByEnemyId.TryGetValue(enemyId, out float lastHitAt) &&
                tickAt + 0.0001f < lastHitAt + intervalSeconds)
                continue;

            ApplyHammerTempestHitToTarget(enemy, def);
            _hammerTempestLastHitTimeByEnemyId[enemyId] = tickAt;
        }
    }

    private void ApplyHammerTempestHitToTarget(EnemyBaseController target, AbilityDefinition def)
    {
        if (target == null || target.IsDead || def == null || stats == null)
            return;

        float damageMultiplier = GetHammerTempestHitDamageMultiplier(target);
        ApplyWhirlwindHitToTarget(target, def, damageMultiplier);

        if (GetHammerTempestSelectedChoice() == HammerTempestCrushingMomentumChoiceIndex)
            AddHammerTempestMomentumStack(target);
    }

    private float GetHammerTempestHitDamageMultiplier(EnemyBaseController target)
    {
        float mult = 1f;
        if (stats != null)
        {
            mult *= AbilityCombatPower.GetHammerTempestWeaponSpeedHitDamageMultiplier(stats.AttacksPerSecond);
        }

        if (GetHammerTempestSelectedChoice() == HammerTempestSacredArsenalChoiceIndex)
            mult *= AbilityCombatPower.HammerTempestSacredArsenalDamageMultiplier;

        if (GetHammerTempestSelectedChoice() == HammerTempestCrushingMomentumChoiceIndex && target != null)
        {
            int stacks = GetHammerTempestMomentumStacks(target);
            mult *= 1f + stacks * AbilityCombatPower.HammerTempestCrushingMomentumDamagePerStack;
        }

        return mult;
    }

    private float GetHammerTempestHitIntervalSeconds()
    {
        int hammerCount = Mathf.Max(1, GetHammerTempestOrbitHammerCount());
        float hammerTempestDegreesPerHit = 360f / hammerCount;
        float orbitDegPerSecond = abilityVfx != null
            ? abilityVfx.GetHammerTempestMainOrbitDegreesPerSecond()
            : 360f;
        orbitDegPerSecond = Mathf.Max(1f, orbitDegPerSecond);
        float interval = hammerTempestDegreesPerHit / orbitDegPerSecond;
        return Mathf.Max(0.05f, interval);
    }

    private int GetHammerTempestOrbitHammerCount()
    {
        int count = AbilityCombatPower.HammerTempestBaseHammerCount;
        if (GetHammerTempestSelectedChoice() == HammerTempestSacredArsenalChoiceIndex)
            count += AbilityCombatPower.HammerTempestSacredArsenalBonusHammerCount;
        return Mathf.Clamp(count, 1, 12);
    }

    private int GetHammerTempestSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.HammerTempestEnhancementParentSpineNodeId,
            -1);
    }

    private int GetHammerTempestMomentumStacks(EnemyBaseController target)
    {
        if (target == null)
            return 0;

        int enemyId = target.GetInstanceID();
        if (!_hammerTempestMomentumByEnemyId.TryGetValue(enemyId, out HammerTempestMomentumState state))
            return 0;

        return Mathf.Clamp(state.stacks, 0, AbilityCombatPower.HammerTempestCrushingMomentumMaxStacks);
    }

    private void AddHammerTempestMomentumStack(EnemyBaseController target)
    {
        if (target == null)
            return;

        int enemyId = target.GetInstanceID();
        int stacks = 1;
        if (_hammerTempestMomentumByEnemyId.TryGetValue(enemyId, out HammerTempestMomentumState existing))
        {
            stacks = Mathf.Min(AbilityCombatPower.HammerTempestCrushingMomentumMaxStacks, existing.stacks + 1);
        }

        _hammerTempestMomentumByEnemyId[enemyId] = new HammerTempestMomentumState
        {
            stacks = stacks
        };
    }

    private void CleanupExpiredHammerTempestMomentum()
    {
        // Crushing Momentum stacks now persist for the entire Hammer Tempest cast.
    }

    private bool CanHitAnyEnemyWithHammerTempest()
    {
        if (stats == null)
            return false;

        float radius = GetHammerTempestHitRadius();
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;
            if (IsEnemyWithinHammerTempestRange(enemy, radius))
                return true;
        }

        return false;
    }

    private float GetHammerTempestHitRadius()
    {
        if (abilityVfx != null)
            return abilityVfx.GetHammerTempestOrbitRadiusWorld();
        return 1.35f;
    }

    private Vector3 GetHammerTempestOrbitPivotWorld()
    {
        Vector3 offset = abilityVfx != null
            ? abilityVfx.GetHammerTempestCenterOffsetWorld()
            : new Vector3(0f, 0.75f, 0f);
        return transform.position + offset;
    }

    private static float GetEnemyColliderReach(EnemyBaseController enemy)
    {
        if (enemy == null)
            return 0f;

        Collider2D enemyCol = enemy.GetComponent<Collider2D>();
        if (enemyCol == null)
            enemyCol = enemy.GetComponentInChildren<Collider2D>();
        if (enemyCol == null)
            return 0f;

        Vector3 ext = enemyCol.bounds.extents;
        return Mathf.Max(ext.x, ext.y);
    }

    private bool IsEnemyWithinHammerTempestRange(EnemyBaseController enemy, float radius)
    {
        if (enemy == null)
            return false;

        Vector3 pivot = GetHammerTempestOrbitPivotWorld();
        Vector2 enemyPos = enemy.transform.position;
        float centerDist = Vector2.Distance(new Vector2(pivot.x, pivot.y), enemyPos);
        float reach = GetEnemyColliderReach(enemy);
        return centerDist - reach <= Mathf.Max(0f, radius);
    }

    private void TickEnergyInfusion(float deltaTime)
    {
        if (!_energyInfusionActive || player == null || stats == null)
            return;

        if (player.IsDead || stats.IsDead)
        {
            ForceEndEnergyInfusionEarly(applyCooldown: false);
        }
    }

    private void ApplyEnergyInfusionCombatModifiers()
    {
        if (stats == null)
            return;

        int choice = GetEnergyInfusionSelectedChoice();
        stats.CombatFlatManaRegenPerSecond = choice == 0
            ? AbilityCombatPower.EnergyInfusionEfficientConversionFlatManaRegenPerSecond
            : 0f;
    }

    private void SyncEnergyInfusionHudBuff()
    {
        if (!buffController)
            return;

        if (!_energyInfusionActive)
        {
            if (buffController.IsHudAbilityBuffActive(EnergyInfusionId))
                buffController.ClearHudAbilityBuff(EnergyInfusionId);
            return;
        }

        buffController.SetHudAbilityBuff(EnergyInfusionId, 1, 0f, 0f, persistActiveOverlay: true);
    }

    private int GetEnergyInfusionSelectedChoice()
    {
        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            return -1;

        int selected = skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.EnergyInfusionEnhancementParentSpineNodeId,
            -1);
        return selected;
    }

    private float GetEnergyInfusionAbilityPowerPercentBonus(AbilityDefinition def)
    {
        if (def == null || GetEnergyInfusionSelectedChoice() != 1)
            return 0f;

        if (string.Equals(def.abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
            return _queuedPowerSlashUsedEnergyInfusionMana
                ? AbilityCombatPower.EnergyInfusionOverchargedAbilityPowerPercentBonus
                : 0f;

        if (string.Equals(def.abilityId, TripleShotId, StringComparison.OrdinalIgnoreCase))
            return _queuedTripleShotUsedEnergyInfusionMana
                ? AbilityCombatPower.EnergyInfusionOverchargedAbilityPowerPercentBonus
                : 0f;

        if (string.Equals(def.abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase))
            return _crescentSlashUsedEnergyInfusionMana
                ? AbilityCombatPower.EnergyInfusionOverchargedAbilityPowerPercentBonus
                : 0f;

        if (string.Equals(def.abilityId, WhirlwindId, StringComparison.OrdinalIgnoreCase))
            return _whirlwindUsedEnergyInfusionMana
                ? AbilityCombatPower.EnergyInfusionOverchargedAbilityPowerPercentBonus
                : 0f;

        return DidLastAbilitySpendUseEnergyInfusionMana(def)
            ? AbilityCombatPower.EnergyInfusionOverchargedAbilityPowerPercentBonus
            : 0f;
    }

    private float GetAbilityPowerDamageMultiplierForAbility(AbilityDefinition def)
    {
        if (stats == null)
            return 1f;

        return stats.GetAbilityPowerDamageMultiplier(
            bonusAbilityPowerPercent: GetEnergyInfusionAbilityPowerPercentBonus(def));
    }

    private void ForceEndAvatarOfTheForestEarly()
    {
        if (!IsAvatarOfTheForestActive)
            return;

        _avatarOfForestActive = false;
        _avatarOfForestEndsAt = 0f;
        _avatarOfForestDuration = 0f;
        _avatarOfForestReplenishAccum = 0f;

        abilityVfx?.DestroyAvatarOfTheForestGlowVfx();

        if (_avatarOfForestCooldownAbilityDef)
            StartCooldown(_avatarOfForestCooldownAbilityDef);
        _avatarOfForestCooldownAbilityDef = null;

        _lastSyncedAvatarOfForestHudEnd = float.NaN;
        SyncAvatarOfTheForestHudBuff();
        stats?.NotifyStatsChanged();
    }

    private void ForceEndGenericHudAbilityBuffWithCooldown(string abilityId)
    {
        if (buffController == null || !buffController.IsHudAbilityBuffActive(abilityId))
            return;

        buffController.ClearHudAbilityBuff(abilityId);

        AbilityDefinition def = GetAbilityDefinition(abilityId);
        if (def != null && def.cooldown > 0f)
            StartCooldown(def);
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
        if (!IsOnCooldown(abilityId, out float remaining))
            return 0f;

        if (string.Equals(abilityId, PhoenixAshenRebirthSkillTreeCooldownId, StringComparison.OrdinalIgnoreCase))
        {
            float total = AbilityCombatPower.PhoenixSoulAshenRebirthCooldownSeconds;
            return Mathf.Clamp01(remaining / Mathf.Max(0.01f, total));
        }

        return GetCooldownNormalizedFromRemaining(abilityId, remaining);
    }

    /// <summary>Normalized CD fill when remaining seconds are already known (avoids duplicate cooldown lookups).</summary>
    public float GetCooldownNormalizedFromRemaining(string abilityId, float remainingSeconds)
    {
        if (remainingSeconds <= 0f)
            return 0f;

        var def = GetAbilityDefinition(abilityId);
        if (!def)
            return 0f;

        if (string.Equals(abilityId, FlameChargeId, StringComparison.OrdinalIgnoreCase))
        {
            float cd = GetFlameChargeCooldownDuration(def);
            return Mathf.Clamp01(remainingSeconds / Mathf.Max(0.01f, cd));
        }

        if (def.cooldown <= 0f)
            return 0f;

        return Mathf.Clamp01(remainingSeconds / Mathf.Max(0.01f, def.cooldown));
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

    private void LogAbilityUsed(AbilityDefinition def)
    {
        bool resumesCombat = AbilityResumesCombatOnCast(def);

        if (def != null && !def.SpawnsMinionOnCast && !IsGatheringAbilitySkill(def.sourceSkill) && resumesCombat)
        {
            if (combat == null)
                combat = GetComponent<PlayerCombatController>();
            combat?.RecordOutgoingSourceUse(GetAbilityOutgoingDamageSourceLabel(def.abilityId));
            combat?.NotifyExplicitCombatEngage();
        }

        ApplyBattleEngineOnAbilityCommitEffects(def, beginHitSession: resumesCombat);
    }

    /// <summary>Self-buff / utility casts that should not resume paused combat or start a hit session.</summary>
    private static bool AbilityResumesCombatOnCast(AbilityDefinition def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.abilityId))
            return true;

        if (def.tag == AbilityTag.Buff || def.tag == AbilityTag.ToggleBuff)
            return false;

        string id = def.abilityId;
        return !string.Equals(id, StaticArrowsId, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(id, LumberFrenzyId, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(id, FishingFrenzyId, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(id, CleavingChopId, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(id, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGatheringAbilitySkill(SkillType skillType)
    {
        return skillType == SkillType.Woodcutting ||
               skillType == SkillType.Mining ||
               skillType == SkillType.Fishing;
    }

    private void ApplyBattleEngineOnAbilityCommitEffects(AbilityDefinition def, bool beginHitSession)
    {
        if (def == null || stats == null || !stats.IsBattleEngineUnlocked())
            return;

        if (beginHitSession)
            PrepareBattleEngineAbilityHitSession(def);

        int pick = stats.GetBattleEngineEnhancementPick();
        if (pick == 0)
        {
            ReduceOtherAbilityCooldownsBySeconds(
                def,
                AbilityCombatPower.BattleEngineRapidCastingCooldownReductionSeconds);
        }
        else if (pick == 1 && beginHitSession)
        {
            RefreshBattleEngineOverloadOnAbilityCast();
        }
    }

    private void PrepareBattleEngineAbilityHitSession(AbilityDefinition def)
    {
        if (def == null || stats == null || !stats.IsBattleEngineUnlocked())
            return;

        _battleEngineCastSessionId++;
        _battleEngineEnergyPendingAbilityId = def.abilityId ?? "";
    }

    private void TryGrantBattleEngineEnergyOnAbilityHit(AbilityDefinition def, bool dealtDamage)
    {
        if (!dealtDamage || def == null || player == null || stats == null || !stats.IsBattleEngineUnlocked())
            return;

        if (_battleEngineEnergyGrantedSessionId == _battleEngineCastSessionId)
            return;

        if (string.IsNullOrWhiteSpace(_battleEngineEnergyPendingAbilityId) ||
            !string.Equals(def.abilityId, _battleEngineEnergyPendingAbilityId, StringComparison.OrdinalIgnoreCase))
            return;

        player.AddEnergy(AbilityCombatPower.BattleEngineEnergyOnAbilityHit);
        _battleEngineEnergyGrantedSessionId = _battleEngineCastSessionId;
    }

    public void TryGrantBattleEngineEnergyForPendingAbilityHit()
    {
        if (string.IsNullOrWhiteSpace(_battleEngineEnergyPendingAbilityId))
            return;

        TryGrantBattleEngineEnergyOnAbilityHit(GetAbilityDefinition(_battleEngineEnergyPendingAbilityId), true);
    }

    private bool IsBattleEngineOverloadActive()
    {
        if (stats == null || stats.GetBattleEngineEnhancementPick() != 1)
            return false;

        return _battleEngineOverloadStacks > 0 && Time.time < _battleEngineOverloadEndsAt;
    }

    /// <summary>
    /// Ability tooltip damage for the next cast (Overload adds a stack before hits resolve).
    /// </summary>
    public float GetTooltipAbilityDamageMultiplier()
    {
        if (stats == null || stats.GetBattleEngineEnhancementPick() != 1)
            return 1f;

        int stacks = IsBattleEngineOverloadActive() ? _battleEngineOverloadStacks : 0;
        int stacksForDamage = Mathf.Min(
            AbilityCombatPower.BattleEngineOverloadMaxStacks,
            stacks + 1);
        return 1f + stacksForDamage * AbilityCombatPower.BattleEngineOverloadDamagePerStack;
    }

    /// <summary>Ability tooltip energy for the next cast (uses active Overload stacks before the new stack is added).</summary>
    public float GetTooltipAbilityEnergyCostMultiplier()
    {
        if (!IsBattleEngineOverloadActive())
            return 1f;

        return GetBattleEngineOverloadEnergyCostMultiplier();
    }

    public void SetWhirlwindActionBarHeld(bool held)
    {
        _whirlwindActionBarHeld = held;
        if (!held && _whirlwindChanneling && !_whirlwindAutoChanneling)
            ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: true);
    }

    private static bool IsWhirlwindMobilityAbility(string abilityId)
    {
        return string.Equals(abilityId, ShadowStrikeId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(abilityId, FlameChargeId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whirlwind can coexist with a small set of mobility actions without dropping the channel.</summary>
    private bool CanUseAbilityDuringWhirlwindChannel(string abilityId)
    {
        if (!_whirlwindChanneling)
            return true;

        if (string.Equals(abilityId, WhirlwindId, StringComparison.OrdinalIgnoreCase))
            return true;

        return IsWhirlwindMobilityAbility(abilityId);
    }

    private float GetBattleEngineOverloadDamageMultiplier()
    {
        if (!IsBattleEngineOverloadActive())
            return 1f;

        return 1f + _battleEngineOverloadStacks * AbilityCombatPower.BattleEngineOverloadDamagePerStack;
    }

    private float GetBattleEngineOverloadEnergyCostMultiplier()
    {
        if (!IsBattleEngineOverloadActive())
            return 1f;

        return 1f + _battleEngineOverloadStacks * AbilityCombatPower.BattleEngineOverloadCostPerStack;
    }

    private void RefreshBattleEngineOverloadOnAbilityCast()
    {
        if (stats == null || stats.GetBattleEngineEnhancementPick() != 1)
            return;

        _battleEngineOverloadStacks = Mathf.Min(
            AbilityCombatPower.BattleEngineOverloadMaxStacks,
            _battleEngineOverloadStacks + 1);
        _battleEngineOverloadEndsAt = Time.time + CharacterStats.BattleEngineOverloadDurationSeconds;
        SyncBattleEngineOverloadHudBuff();
    }

    private void TickBattleEngineOverloadExpiry()
    {
        if (_battleEngineOverloadStacks <= 0)
            return;

        if (Time.time < _battleEngineOverloadEndsAt)
            return;

        ClearBattleEngineOverloadStacksIfAny();
    }

    private void SyncBattleEngineOverloadHudBuff()
    {
        if (buffController == null)
            return;

        if (!IsBattleEngineOverloadActive())
        {
            if (_lastSyncedOverloadHudStacks != int.MinValue || !float.IsNaN(_lastSyncedOverloadHudEnd))
            {
                _lastSyncedOverloadHudStacks = int.MinValue;
                _lastSyncedOverloadHudEnd = float.NaN;
                buffController.ClearHudAbilityBuff(CharacterStats.BattleEngineOverloadHudBuffId);
            }

            return;
        }

        if (_lastSyncedOverloadHudStacks == _battleEngineOverloadStacks &&
            Mathf.Approximately(_lastSyncedOverloadHudEnd, _battleEngineOverloadEndsAt))
            return;

        _lastSyncedOverloadHudStacks = _battleEngineOverloadStacks;
        _lastSyncedOverloadHudEnd = _battleEngineOverloadEndsAt;
        buffController.SetHudAbilityBuff(
            CharacterStats.BattleEngineOverloadHudBuffId,
            _battleEngineOverloadStacks,
            _battleEngineOverloadEndsAt,
            CharacterStats.BattleEngineOverloadDurationSeconds);
    }

    private void ClearBattleEngineOverloadStacksIfAny()
    {
        if (_battleEngineOverloadStacks <= 0 && _battleEngineOverloadEndsAt < 0f)
            return;

        _battleEngineOverloadStacks = 0;
        _battleEngineOverloadEndsAt = -1f;
        _lastSyncedOverloadHudStacks = int.MinValue;
        _lastSyncedOverloadHudEnd = float.NaN;
        buffController?.ClearHudAbilityBuff(CharacterStats.BattleEngineOverloadHudBuffId);
    }

    public int GetPhoenixLivingInfernoNearbyBurningCount() =>
        stats != null && stats.GetPhoenixSoulEnhancementPick() == 1 ? CountBurningEnemiesNearPlayer() : 0;

    public float GetPhoenixLivingInfernoMeleeDamageBonusFraction()
    {
        if (stats == null
            || !stats.AreMeleeMajorPassiveEffectsEnabled()
            || stats.GetPhoenixSoulEnhancementPick() != 1)
            return 0f;

        int burningCount = CountBurningEnemiesNearPlayer();
        float bonus = burningCount * AbilityCombatPower.PhoenixSoulLivingInfernoMeleeDamagePerBurningEnemy;
        return Mathf.Min(bonus, AbilityCombatPower.PhoenixSoulLivingInfernoMaxMeleeDamageBonusFraction);
    }

    /// <summary>Phoenix Soul — Ashen Rebirth: intercept death before <see cref="PlayerController"/> runs full death flow.</summary>
    public bool TryTriggerPhoenixAshenRebirth()
    {
        if (stats == null
            || player == null
            || !stats.AreMeleeMajorPassiveEffectsEnabled()
            || stats.GetPhoenixSoulEnhancementPick() != 0)
            return false;

        if (Time.time < _phoenixAshenRebirthCooldownEndsAt)
            return false;

        if (!stats.TryReviveFromPhoenixSoul(AbilityCombatPower.PhoenixSoulAshenRebirthHealthFraction))
            return false;

        _phoenixAshenRebirthCooldownEndsAt = Time.time + AbilityCombatPower.PhoenixSoulAshenRebirthCooldownSeconds;

        SkillTreeViewUI skillTree = FindFirstObjectByType<SkillTreeViewUI>(FindObjectsInactive.Include);
        skillTree?.RefreshSkillTreePresentationNow();

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        combat?.UnpauseDpsTracker();

        if (_phoenixAshenRebirthImmunityRoutine != null)
            StopCoroutine(_phoenixAshenRebirthImmunityRoutine);
        _phoenixAshenRebirthImmunityRoutine = StartCoroutine(CoPhoenixAshenRebirthImmunity());

        abilityVfx?.SpawnAshenRebirthPhoenixVfx();
        abilityVfx?.SpawnFlameChargeVolcanicBurst(player.transform.position);
        PulseAshenRebirthExplosion();

        player.ShowPopup("Phoenix Soul — Ashen Rebirth");
        return true;
    }

    private void PulseAshenRebirthExplosion()
    {
        if (stats == null || player == null)
            return;

        Vector3 origin = player.transform.position;
        float radius = AbilityCombatPower.PhoenixSoulNearbyRadius;
        float flatFire = AbilityCombatPower.PhoenixSoulAshenRebirthExplosionFlatFireDamage;
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            if (!IsEnemyWithinPhoenixNearbyRadius(enemy, origin, radius))
                continue;

            SplitDamage fireHit = new SplitDamage(0f, flatFire, 0f);
            DealtHit dealt = ApplyAbilitySplitDamageToEnemy(enemy, null, fireHit, false, 0f);
            if (dealt.magic > 0f)
                TryApplyAshenRebirthBurnFromFireHit(enemy, dealt.magic);

            if (dealt.Total > 0f)
                player.ApplyLifeSteal(dealt.Total);
        }
    }

    private bool TryApplyAshenRebirthBurnFromFireHit(EnemyBaseController enemy, float fireDamageDealt)
    {
        if (!enemy || stats == null || fireDamageDealt <= 0f)
            return false;

        AilmentController ailments = enemy.GetComponent<AilmentController>();
        if (ailments == null)
            return false;

        return ailments.TryApplyBurnFromFireHit(
            fireDamageDealt,
            AbilityCombatPower.PhoenixSoulAshenRebirthExplosionBurnApplyChance,
            stats.BurnExplosionMultiplier,
            transform,
            AshenRebirthOutgoingDamageSourceLabel,
            burnTickIntervalSeconds: stats.BurnTickIntervalSeconds);
    }

    private const string AshenRebirthOutgoingDamageSourceLabel = "Phoenix Soul — Ashen Rebirth";

    private IEnumerator CoPhoenixAshenRebirthImmunity()
    {
        float duration = AbilityCombatPower.PhoenixSoulAshenRebirthImmunitySeconds;
        float endsAt = Time.time + duration;
        player?.SetTeleportDamageImmune(true);

        if (buffController)
        {
            buffController.SetHudAbilityBuff(
                CharacterStats.PhoenixSoulAshenRebirthImmunityHudBuffId,
                1,
                endsAt,
                duration,
                persistActiveOverlay: false);
        }

        yield return new WaitForSeconds(duration);

        player?.SetTeleportDamageImmune(false);
        buffController?.ClearHudAbilityBuff(CharacterStats.PhoenixSoulAshenRebirthImmunityHudBuffId);
        _phoenixAshenRebirthImmunityRoutine = null;
    }

    private void TickPhoenixSoulBurnRegen(float deltaSeconds)
    {
        if (stats == null || player == null || !stats.IsPhoenixSoulUnlocked() || !stats.AreMeleeMajorPassiveEffectsEnabled())
            return;

        if (player.IsDead || stats.IsDead)
            return;

        _phoenixSoulBurnRegenAccum += deltaSeconds;
        while (_phoenixSoulBurnRegenAccum >= AbilityCombatPower.PhoenixSoulBurnRegenIntervalSeconds)
        {
            _phoenixSoulBurnRegenAccum -= AbilityCombatPower.PhoenixSoulBurnRegenIntervalSeconds;
            PulsePhoenixSoulBurnRegen();
        }
    }

    private void PulsePhoenixSoulBurnRegen()
    {
        if (stats == null || player == null)
            return;

        int burningCount = CountBurningEnemiesNearPlayer();
        if (burningCount <= 0)
            return;

        float lifeTotal = burningCount * AbilityCombatPower.PhoenixSoulLifePerBurningEnemy;
        float energyTotal = burningCount * AbilityCombatPower.PhoenixSoulEnergyPerBurningEnemy;
        if (lifeTotal > 0f)
            stats.Heal(lifeTotal, PlayerCombatController.PhoenixSoulHealingSourceLabel);
        if (energyTotal > 0f)
            player.AddEnergy(energyTotal);
    }

    private int CountBurningEnemiesNearPlayer()
    {
        if (!player)
            return 0;

        Vector3 origin = player.transform.position;
        float radius = AbilityCombatPower.PhoenixSoulNearbyRadius;
        int maxCount = AbilityCombatPower.PhoenixSoulMaxNearbyBurningEnemies;
        int found = 0;

        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < enemies.Count && found < maxCount; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            if (!IsEnemyWithinPhoenixNearbyRadius(enemy, origin, radius))
                continue;

            AilmentController ailments = enemy.GetComponent<AilmentController>();
            if (ailments == null || !ailments.HasBurn)
                continue;

            found++;
        }

        return found;
    }

    private static bool IsEnemyWithinPhoenixNearbyRadius(EnemyBaseController enemy, Vector3 origin, float radius)
    {
        if (!enemy)
            return false;

        float dx = Mathf.Abs(enemy.transform.position.x - origin.x);
        float dy = Mathf.Abs(enemy.transform.position.y - origin.y);
        return dx <= radius && dy <= radius;
    }

    private void ReduceOtherAbilityCooldownsBySeconds(AbilityDefinition exceptDef, float seconds)
    {
        if (seconds <= 0f)
            return;

        string exceptId = exceptDef != null ? exceptDef.abilityId : null;
        var abilityIds = new List<string>(_cooldownEndsById.Keys);
        for (int i = 0; i < abilityIds.Count; i++)
        {
            string id = abilityIds[i];
            if (!string.IsNullOrEmpty(exceptId) &&
                string.Equals(id, exceptId, StringComparison.OrdinalIgnoreCase))
                continue;

            ReduceStoredAbilityCooldownBySeconds(id, seconds);
        }
    }

    private void ReduceStoredAbilityCooldownBySeconds(string abilityId, float seconds)
    {
        if (string.IsNullOrWhiteSpace(abilityId) || seconds <= 0f)
            return;

        if (!_cooldownEndsById.TryGetValue(abilityId, out float end))
            return;

        float remaining = end - Time.time;
        if (remaining <= 0f)
            return;

        float newEnd = Time.time + Mathf.Max(0f, remaining - seconds);
        _cooldownEndsById[abilityId] = newEnd;

        AbilityDefinition def = GetAbilityDefinition(abilityId);
        string rowKey = BuildAbilityRowKey(def);
        if (rowKey != null)
            _cooldownEndsByRowKey[rowKey] = newEnd;
    }

    /// <summary>Queued or target-gated abilities spend their resource cost when they actually fire, not when the bar button is pressed.</summary>
    private static bool AbilityDefersEnergyUntilActivated(AbilityDefinition def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.abilityId))
            return false;

        if (def.minionSpawnDefinition)
            return true;

        string id = def.abilityId;
        return string.Equals(id, PowerSlashId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, TripleShotId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, RendId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, EnvenomId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, CrescentSlashId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, WhirlwindId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, FinalSeveranceId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, ShadowStrikeId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, EnergyInfusionId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, FlameChargeId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, ExecutionersDescentId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, BladestormId, StringComparison.OrdinalIgnoreCase)
               || IsSnipeAbilityId(id);
    }

    public bool IsEnergyInfusionActive => _energyInfusionActive;

    /// <summary>Energy/s shown on the stats panel.</summary>
    public float GetDisplayedEnergyRegenPerSecond()
    {
        return stats != null ? stats.EnergyRegenPerSecond : 0f;
    }

    private bool TrySpendAbilityResourceCost(AbilityDefinition def, bool showInsufficientFeedback = true)
    {
        if (def == null || player == null || stats == null)
            return true;

        ResetLastAbilityResourceSpend();

        switch (def.GetResourceCostType())
        {
            case AbilityResourceCostType.None:
                return true;
            case AbilityResourceCostType.Health:
            {
                int hpCost = Mathf.Max(0, Mathf.RoundToInt(def.healthCost));
                if (hpCost <= 0)
                    return true;
                if (stats.HP < hpCost)
                {
                    if (showInsufficientFeedback)
                        player.ShowPopup("Not enough health.");
                    return false;
                }

                bool spentHealth = stats.SpendHealthForAbilityCost(hpCost);
                if (spentHealth)
                    RecordLastAbilityResourceSpend(def, hpCost, 0, 0, false);
                return spentHealth;
            }
            case AbilityResourceCostType.Mana:
            {
                int manaCost = Mathf.Max(0, Mathf.RoundToInt(def.manaCost));
                if (manaCost <= 0)
                    return true;
                if (stats.Mana < manaCost)
                {
                    if (showInsufficientFeedback)
                        player.ShowPopup("Not enough mana.");
                    return false;
                }

                bool spentMana = player.SpendMana(manaCost);
                if (spentMana)
                    RecordLastAbilityResourceSpend(def, 0, manaCost, 0, false);
                return spentMana;
            }
            default:
            {
                if (string.Equals(def.abilityId, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase) &&
                    IsCrusaderStrikeSecondCastFree())
                    return true;

                float costMultiplier = GetBattleEngineOverloadEnergyCostMultiplier();
                int energyCost = Mathf.Max(0, Mathf.RoundToInt(def.energyCost * costMultiplier));
                energyCost = stats.ApplyEnergyEfficiencyToAbilityEnergyCost(def, energyCost);
                if (energyCost <= 0)
                    return true;

                if (!TrySpendEnergyAbilityCost(def, energyCost, showInsufficientFeedback, out int energySpent, out int manaSpent))
                    return false;

                RecordLastAbilityResourceSpend(def, 0, manaSpent, energySpent, manaSpent > 0);
                return true;
            }
        }
    }

    private void RefundAbilityResourceCost(AbilityDefinition def)
    {
        if (def == null || player == null || stats == null)
            return;

        if (LastAbilityResourceSpendMatches(def))
        {
            if (_lastAbilityResourceSpendHealth > 0)
                stats.Heal(_lastAbilityResourceSpendHealth, PlayerCombatController.AbilityHealthRefundHealingSourceLabel);
            if (_lastAbilityResourceSpendMana > 0)
                player.AddMana(_lastAbilityResourceSpendMana);
            if (_lastAbilityResourceSpendEnergy > 0)
                player.AddEnergy(_lastAbilityResourceSpendEnergy);
            ResetLastAbilityResourceSpend();
            return;
        }

        switch (def.GetResourceCostType())
        {
            case AbilityResourceCostType.Health:
                stats.Heal(def.healthCost, PlayerCombatController.AbilityHealthRefundHealingSourceLabel);
                break;
            case AbilityResourceCostType.Mana:
                player.AddMana(def.manaCost);
                break;
            case AbilityResourceCostType.Energy:
                player.AddEnergy(def.energyCost);
                break;
        }
    }

    private bool TrySpendEnergyAbilityCost(
        AbilityDefinition def,
        int energyCost,
        bool showInsufficientFeedback,
        out int energySpent,
        out int manaSpent)
    {
        energySpent = 0;
        manaSpent = 0;
        if (energyCost <= 0)
            return true;

        int manaCost = GetEnergyInfusionConvertedManaCost(def, energyCost);
        if (manaCost > 0 && stats.Mana + 0.0001f >= manaCost)
        {
            int reducedEnergyCost = Mathf.Max(0, energyCost - manaCost);
            if (stats.Energy + 0.0001f < reducedEnergyCost)
            {
                if (showInsufficientFeedback)
                    player.ShowPopup("Not enough energy.");
                return false;
            }

            if (manaCost > 0 && !player.SpendMana(manaCost))
                return false;

            if (reducedEnergyCost > 0 && !player.SpendEnergy(reducedEnergyCost))
            {
                if (manaCost > 0)
                    player.AddMana(manaCost);
                return false;
            }

            manaSpent = manaCost;
            energySpent = reducedEnergyCost;
            return true;
        }

        if (stats.Energy + 0.0001f < energyCost)
        {
            if (showInsufficientFeedback)
                player.ShowPopup("Not enough energy.");
            return false;
        }

        if (!player.SpendEnergy(energyCost))
            return false;

        energySpent = energyCost;
        return true;
    }

    private int GetEnergyInfusionConvertedManaCost(AbilityDefinition def, int energyCost)
    {
        if (!ShouldUseEnergyInfusionForAbility(def) || energyCost <= 0)
            return 0;

        int manaCost = Mathf.RoundToInt(energyCost * GetEnergyInfusionManaCostFraction(def));
        return Mathf.Clamp(manaCost, 0, energyCost);
    }

    private float GetEnergyInfusionManaCostFraction(AbilityDefinition def)
    {
        if (!ShouldUseEnergyInfusionForAbility(def))
            return 0f;

        float manaFraction = AbilityCombatPower.EnergyInfusionBaseManaCostFraction;
        int selectedChoice = GetEnergyInfusionSelectedChoice();
        if (selectedChoice == 0)
            manaFraction += AbilityCombatPower.EnergyInfusionEfficientConversionAdditionalManaCostFraction;
        else if (selectedChoice == 1)
            manaFraction = AbilityCombatPower.EnergyInfusionOverchargedManaCostFraction;

        return Mathf.Clamp01(manaFraction);
    }

    private bool ShouldUseEnergyInfusionForAbility(AbilityDefinition def)
    {
        if (!_energyInfusionActive || def == null || def.sourceSkill != SkillType.Melee)
            return false;

        if (IsEnergyInfusionExcludedChannelingAbility(def))
            return false;

        return def.GetResourceCostType() == AbilityResourceCostType.Energy;
    }

    private static bool IsEnergyInfusionExcludedChannelingAbility(AbilityDefinition def) =>
        def != null && string.Equals(def.abilityId, WhirlwindId, StringComparison.OrdinalIgnoreCase);

    private void RecordLastAbilityResourceSpend(
        AbilityDefinition def,
        int healthSpent,
        int manaSpent,
        int energySpent,
        bool usedEnergyInfusionMana)
    {
        _lastAbilityResourceSpendAbilityId = def != null ? def.abilityId ?? "" : "";
        _lastAbilityResourceSpendHealth = Mathf.Max(0, healthSpent);
        _lastAbilityResourceSpendMana = Mathf.Max(0, manaSpent);
        _lastAbilityResourceSpendEnergy = Mathf.Max(0, energySpent);
        _lastAbilityResourceSpendUsedEnergyInfusionMana = usedEnergyInfusionMana;
    }

    private void ResetLastAbilityResourceSpend()
    {
        _lastAbilityResourceSpendAbilityId = "";
        _lastAbilityResourceSpendHealth = 0;
        _lastAbilityResourceSpendMana = 0;
        _lastAbilityResourceSpendEnergy = 0;
        _lastAbilityResourceSpendUsedEnergyInfusionMana = false;
    }

    private bool LastAbilityResourceSpendMatches(AbilityDefinition def)
    {
        return def != null &&
               !string.IsNullOrWhiteSpace(_lastAbilityResourceSpendAbilityId) &&
               string.Equals(_lastAbilityResourceSpendAbilityId, def.abilityId, StringComparison.OrdinalIgnoreCase);
    }

    private bool DidLastAbilitySpendUseEnergyInfusionMana(AbilityDefinition def)
    {
        return LastAbilityResourceSpendMatches(def) && _lastAbilityResourceSpendUsedEnergyInfusionMana;
    }

    /// <summary>
    /// Whether idle auto-battle may cast this ability (action bar border + <see cref="TryUseAbility"/> when feedback is off).
    /// Extend here when more abilities are manual-only.
    /// </summary>
    public static bool CanAbilityBeUsedByAutoBattle(AbilityDefinition def)
    {
        if (def == null)
            return false;

        if (CombatStarterAttackAbility.IsCombatStarterAttack(def))
            return false;

        if (def.tag == AbilityTag.ToggleBuff)
            return !IsToggleBuffActive(def);

        return true;
    }

    public static bool IsToggleBuffActive(AbilityDefinition def)
    {
        if (def == null || def.tag != AbilityTag.ToggleBuff)
            return false;

        if (_instance == null)
            return false;

        if (string.Equals(def.abilityId, EnergyInfusionId, StringComparison.OrdinalIgnoreCase))
            return _instance._energyInfusionActive;

        return false;
    }

    private const string NoTargetsInRangeLogMessage = "No targets in range";
    private const float NoTargetsInRangeLogCooldownSeconds = 3f;
    private static float _nextNoTargetsInRangeLogTime = -999f;
    private const string NoActiveTargetPrimedLogMessage = "No active target. Ability primed.";
    private const float NoActiveTargetPrimedLogCooldownSeconds = 3f;
    private static float _nextNoActiveTargetPrimedLogTime = -999f;
    private const string NoDamageWithCurrentWeaponLogMessage = "Ability does no damage with this weapon";
    private const float NoDamageWithCurrentWeaponLogCooldownSeconds = 3f;
    private static float _nextNoDamageWithCurrentWeaponLogTime = -999f;

    /// <summary>
    /// Pick the closest valid enemy for this ability, set combat target, or block the cast when range check applies.
    /// </summary>
    public bool TryPrepareKeyboardModeAbilityTarget(string abilityId)
    {
        AbilityDefinition def = GetAbilityDefinition(abilityId);
        if (!def || !def.RequiresKeyboardRangeCheckToActivate())
            return true;

        if (TryFindKeyboardModeAbilityTarget(def, out EnemyBaseController target))
        {
            if (combat == null)
                combat = GetComponent<PlayerCombatController>();
            if (UsesMeleeApproachOnActivate(def))
                combat?.EngageTargetFromPlayerInput(target);
            else if (def.SetsTargetOnHit())
                combat?.SetTargetIfNone(target);
            return true;
        }

        if (UsesMeleeApproachOnActivate(def))
        {
            LogNoTargetsInRangeThrottled();
            return false;
        }

        if (IsPrimingAbility(def))
        {
            LogNoActiveTargetPrimedThrottled();
            return true;
        }

        HandleNoTargetsInRangeForStarterAttack(def);
        return false;
    }

    /// <summary>Combat starter Attack: engage closest living enemy and chase into weapon range (no ability damage or cooldown).</summary>
    private bool TryUseCombatStarterAttack(AbilityDefinition def, bool showLockedFeedback)
    {
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null)
            return false;

        EnemyBaseController currentTarget = combat.CurrentTarget;
        bool hasLiveCurrentTarget =
            currentTarget != null &&
            !currentTarget.IsDead &&
            currentTarget.gameObject.activeInHierarchy;

        // Auto/idle battle should only use the slotted base Attack once to acquire a target.
        // After that, normal chase/attack flow should continue without re-engaging every scan.
        if (!showLockedFeedback && hasLiveCurrentTarget)
            return false;

        if (TryFindKeyboardModeAbilityTarget(def, out EnemyBaseController target))
        {
            if (!showLockedFeedback && target == currentTarget)
                return false;

            combat.EngageTargetFromPlayerInput(target);
            return true;
        }

        if (!showLockedFeedback)
            return false;

        HandleNoTargetsInRangeForStarterAttack(def);
        return false;
    }

    private void HandleNoTargetsInRangeForStarterAttack(AbilityDefinition def)
    {
        if (!CombatStarterAttackAbility.IsCombatStarterAttack(def))
        {
            LogNoTargetsInRangeThrottled();
            return;
        }

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        if (combat != null && combat.CurrentTarget != null)
            combat.ClearTarget();

        LogNoTargetsInRangeThrottled();
    }

    private static void LogNoTargetsInRangeThrottled()
    {
        if (Time.time < _nextNoTargetsInRangeLogTime)
            return;

        _nextNoTargetsInRangeLogTime = Time.time + NoTargetsInRangeLogCooldownSeconds;
        GameLog.Add(NoTargetsInRangeLogMessage, GameLog.CannotMessageColor);
    }

    private static void LogNoActiveTargetPrimedThrottled()
    {
        if (Time.time < _nextNoActiveTargetPrimedLogTime)
            return;

        _nextNoActiveTargetPrimedLogTime = Time.time + NoActiveTargetPrimedLogCooldownSeconds;
        GameLog.Add(NoActiveTargetPrimedLogMessage, GameLog.CannotMessageColor);
    }

    private static void LogNoDamageWithCurrentWeaponThrottled()
    {
        if (Time.time < _nextNoDamageWithCurrentWeaponLogTime)
            return;

        _nextNoDamageWithCurrentWeaponLogTime = Time.time + NoDamageWithCurrentWeaponLogCooldownSeconds;
        GameLog.Add(NoDamageWithCurrentWeaponLogMessage, GameLog.CannotMessageColor);
    }

    private static bool IsPrimingAbility(AbilityDefinition def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.abilityId))
            return false;

        string id = def.abilityId;
        return string.Equals(id, PowerSlashId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, TripleShotId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, RendId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, EnvenomId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, CrescentSlashId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesMeleeApproachOnActivate(AbilityDefinition def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.abilityId))
            return false;

        return string.Equals(def.abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(def.abilityId, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase);
    }

    public void ClearPendingMeleeApproachAbility() => _pendingMeleeApproachAbilityId = null;

    private void TickPendingMeleeApproachAbility()
    {
        if (string.IsNullOrEmpty(_pendingMeleeApproachAbilityId))
            return;

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null || player == null)
        {
            ClearPendingMeleeApproachAbility();
            return;
        }

        EnemyBaseController target = combat.CurrentTarget;
        if (target == null || target.IsDead || !target.gameObject.activeInHierarchy)
        {
            ClearPendingMeleeApproachAbility();
            return;
        }

        if (player.IsPlayerMovingAwayFromCombatTarget())
        {
            ClearPendingMeleeApproachAbility();
            return;
        }

        if (!combat.IsEnemyWithinApproachRange(target))
        {
            ClearPendingMeleeApproachAbility();
            return;
        }

        if (!combat.IsEnemyWithinAttackRange(target))
            return;

        AbilityDefinition def = GetAbilityDefinition(_pendingMeleeApproachAbilityId);
        ClearPendingMeleeApproachAbility();
        if (def == null)
            return;

        ExecuteMeleeApproachAbilityNow(def, showLockedFeedback: false);
    }

    private bool TryUseMeleeApproachAbility(AbilityDefinition def, bool showLockedFeedback)
    {
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null)
            return false;

        ClearPendingMeleeApproachAbility();

        EnemyBaseController target = combat.FindClosestEnemyWithinApproachRange();
        if (target == null)
        {
            if (showLockedFeedback)
                LogNoTargetsInRangeThrottled();
            return false;
        }

        combat.EngageTargetFromPlayerInput(target);

        if (!combat.IsEnemyWithinAttackRange(target))
        {
            if (!showLockedFeedback)
                return false;

            _pendingMeleeApproachAbilityId = def.abilityId;
            return true;
        }

        return ExecuteMeleeApproachAbilityNow(def, showLockedFeedback);
    }

    private bool ExecuteMeleeApproachAbilityNow(AbilityDefinition def, bool showLockedFeedback)
    {
        if (def == null)
            return false;

        if (string.Equals(def.abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
        {
            if (_powerSlashQueued)
                return false;

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            _powerSlashQueued = true;
            float powerSlashAnyTypeBonus = GetPowerSlashAnyTypeMultiplierBonus();
            float weaponCombo = def.weaponDamageMultiplier + powerSlashAnyTypeBonus;
            _queuedPowerSlashWeaponMultiplier = weaponCombo <= 0f ? 1f : weaponCombo;
            _queuedPowerSlashAllDamageMultiplier = def.GetEffectiveAllDamageMultiplier();
            _queuedPowerSlashUsedEnergyInfusionMana = DidLastAbilitySpendUseEnergyInfusionMana(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (string.Equals(def.abilityId, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase))
        {
            if (IsCrusaderStrikeComboInProgress())
                return false;

            if (!TryUseCrusaderStrike(def))
                return false;

            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        return false;
    }

    private EnemyBaseController GetPreferredCombatEngagedEnemy()
    {
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        return combat != null ? combat.GetPrimaryEngagedEnemy() : null;
    }

    private bool TryPreferEngagedEnemy(System.Func<EnemyBaseController, bool> isValid, out EnemyBaseController target)
    {
        target = GetPreferredCombatEngagedEnemy();
        if (target != null && isValid(target))
            return true;

        target = null;
        return false;
    }

    private bool TryFindKeyboardModeAbilityTarget(AbilityDefinition def, out EnemyBaseController target)
    {
        target = null;
        if (!def)
            return false;

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        string id = def.abilityId;

        if (string.Equals(id, WhirlwindId, StringComparison.OrdinalIgnoreCase))
            return TryFindClosestEnemyInWhirlwindRadius(out target);

        if (string.Equals(id, CrescentSlashId, StringComparison.OrdinalIgnoreCase))
            return TryFindClosestEnemyInCrescentSlashArc(out target);

        if (IsPenetratingShotAbilityId(id))
            return TryFindClosestEnemyInPenetratingShotLane(out target);

        if (string.Equals(id, GuardiansHammerId, StringComparison.OrdinalIgnoreCase))
            return TryFindClosestEnemyInGuardiansHammerZone(out target);

        if (string.Equals(id, ShadowStrikeId, StringComparison.OrdinalIgnoreCase))
            return TryFindClosestEnemyInShadowStrikeArc(out target);

        if (string.Equals(id, BladestormId, StringComparison.OrdinalIgnoreCase))
            return TryFindClosestBladestormTarget(out target);

        if (string.Equals(id, ExecutionersDescentId, StringComparison.OrdinalIgnoreCase))
        {
            target = ResolveExecutionersDescentTargetInCastRange();
            return target != null;
        }

        if (string.Equals(id, FinalSeveranceId, StringComparison.OrdinalIgnoreCase))
            return TryFindClosestEnemyInFinalSeveranceRange(out target);

        if (CombatStarterAttackAbility.IsCombatStarterAttack(def))
        {
            target = combat != null ? combat.ResolveManualStarterAttackTarget() : null;
            return target != null;
        }

        if (UsesMeleeApproachOnActivate(def))
        {
            target = combat != null ? combat.FindClosestEnemyWithinApproachRange() : null;
            return target != null;
        }

        if (combat != null)
        {
            EnemyBaseController engaged = combat.GetPrimaryEngagedEnemy();
            if (engaged != null)
            {
                if (combat.IsEnemyWithinAttackRange(engaged))
                {
                    target = engaged;
                    return true;
                }

                return false;
            }

            target = combat.FindClosestEnemyInAttackRange();
            if (target != null)
                return true;
        }

        return false;
    }

    private bool TryFindClosestEnemyInWhirlwindRadius(out EnemyBaseController target)
    {
        target = null;
        if (stats == null)
            return false;

        float radius = GetWhirlwindEffectiveRadius();
        float ownerX = transform.position.x;
        float ownerHalf = GetOwnerHalfWidthX();

        EnemyBaseController engaged = GetPreferredCombatEngagedEnemy();
        if (engaged != null &&
            IsEnemyWithinWhirlRange(engaged, radius, ownerX, ownerHalf, out _))
        {
            target = engaged;
            return true;
        }

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        float bestDist = float.MaxValue;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            if (!IsEnemyWithinWhirlRange(enemy, radius, ownerX, ownerHalf, out float edgeGap))
                continue;

            if (edgeGap < bestDist)
            {
                bestDist = edgeGap;
                target = enemy;
            }
        }

        return target != null;
    }

    private static float GetCrescentSlashReach() => AbilityCombatPower.CrescentSlashReach;

    private bool TryFindClosestEnemyInCrescentSlashArc(out EnemyBaseController target)
    {
        target = PickPreferredForwardArcEnemy(
            CollectCrescentSlashForwardHits(GetCrescentSlashReach()));
        return target != null;
    }

    private bool TryFindClosestEnemyInGuardiansHammerZone(out EnemyBaseController target)
    {
        target = PickPreferredForwardArcEnemy(
            CollectGuardiansHammerTargets(AbilityCombatPower.GuardiansHammerForwardReach));
        return target != null;
    }

    private bool TryFindClosestEnemyInShadowStrikeArc(out EnemyBaseController target)
    {
        target = PickPreferredForwardArcEnemy(
            CollectShadowStrikeForwardHits(AbilityCombatPower.ShadowStrikeForwardReach));
        return target != null;
    }

    private bool TryFindClosestBladestormTarget(out EnemyBaseController target)
    {
        if (TryPreferEngagedEnemy(IsEnemyValidBladestormTarget, out target))
            return true;

        target = null;
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        float ownerX = transform.position.x;
        EnemyBaseController best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!IsEnemyValidBladestormTarget(enemy))
                continue;

            float dist = Mathf.Abs(enemy.transform.position.x - ownerX);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        if (best != null)
        {
            target = best;
            return true;
        }

        target = PickPreferredForwardArcEnemy(
            CollectBladestormForwardHits(AbilityCombatPower.BladestormForwardReach));
        return target != null;
    }

    private bool TryFindClosestEnemyInFinalSeveranceRange(out EnemyBaseController target)
    {
        float ownerX = transform.position.x;
        float maxDist = AbilityCombatPower.FinalSeveranceHitRangeHalfWidth;

        EnemyBaseController engaged = GetPreferredCombatEngagedEnemy();
        if (engaged != null && !engaged.IsDead)
        {
            float engagedDist = Mathf.Abs(engaged.transform.position.x - ownerX);
            if (engagedDist <= maxDist)
            {
                target = engaged;
                return true;
            }
        }

        target = null;
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        float bestDist = float.MaxValue;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            float dist = Mathf.Abs(enemy.transform.position.x - ownerX);
            if (dist > maxDist)
                continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                target = enemy;
            }
        }

        return target != null;
    }

    private EnemyBaseController PickPreferredForwardArcEnemy(
        List<(EnemyBaseController enemy, float dist)> forwardHits)
    {
        if (forwardHits == null || forwardHits.Count == 0)
            return null;

        EnemyBaseController engaged = GetPreferredCombatEngagedEnemy();
        if (engaged != null)
        {
            for (int i = 0; i < forwardHits.Count; i++)
            {
                if (forwardHits[i].enemy == engaged)
                    return engaged;
            }
        }

        forwardHits.Sort((a, b) => a.dist.CompareTo(b.dist));
        return forwardHits[0].enemy;
    }

    /// <param name="allowSoulforgedRecastWhileActive">
    /// When false (e.g. idle auto-abilities), an active Soulforged Weapon minion does not receive recast/retarget — use fails so other bar abilities can run.
    /// Manual bar use keeps default true (player can recast while the summon is up).
    /// </param>
    public bool TryUseAbility(
        string abilityId,
        bool showLockedFeedback = true,
        bool allowSoulforgedRecastWhileActive = true,
        bool requireCrescentSlashTargetInFacingLane = false,
        bool requireWhirlwindTargetInRadius = false,
        bool requireGuardiansHammerTargetInFacingZone = false,
        bool snipeAutoBattleFullCharge = false)
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

        if (_finalSeveranceChanneling || _bladestormChanneling || _bladestormRoutine != null)
            return false;

        if (_snipeCharging)
        {
            if (!IsSnipeAbilityId(abilityId))
                return false;
            return !snipeAutoBattleFullCharge;
        }

        if (_whirlwindChanneling && !CanUseAbilityDuringWhirlwindChannel(abilityId))
            return false;

        if (player.IsDead || stats.IsDead)
            return false;

        if (!CanUseWithEquippedWeapon(def))
        {
            if (CombatStarterAttackAbility.IsCombatStarterAttack(def))
                GameLog.Add(CombatStarterAttackAbility.WrongWeaponEquippedLogMessage, GameLog.CannotMessageColor);
            else if (showLockedFeedback && player)
                player.ShowPopup("Ability cant be used with this weapon");
            return false;
        }

        if (WouldAbilityDealNoDamageWithCurrentWeapon(def))
        {
            if (showLockedFeedback)
                LogNoDamageWithCurrentWeaponThrottled();
            return false;
        }

        if (!showLockedFeedback && !CanAbilityBeUsedByAutoBattle(def))
            return false;

        if (TryHandleToggleAbilityUse(def, allowToggleOff: showLockedFeedback))
            return true;

        bool isWhirlwind = string.Equals(def.abilityId, WhirlwindId, StringComparison.OrdinalIgnoreCase);
        bool isSnipe = IsSnipeAbilityId(def.abilityId);

        if (CombatStarterAttackAbility.IsCombatStarterAttack(def))
            return TryUseCombatStarterAttack(def, showLockedFeedback);

        if (globalCooldownSeconds > 0f && Time.time < _globalCooldownEndsAt)
            return false;

        CleanupSoulforgedWeaponList();
        CleanupSoulforgedWarriorList();
        CleanupHawkCompanionList();
        if (def.minionSpawnDefinition && IsSoulforgedWarriorAbility(def) && _activeSoulforgedWarriorMinions.Count > 0)
        {
            if (!allowSoulforgedRecastWhileActive)
                return false;

            RecastActiveSoulforgedWarriors();
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (def.minionSpawnDefinition && IsHawkCompanionAbility(def) && _activeHawkCompanionMinions.Count > 0)
        {
            if (!allowSoulforgedRecastWhileActive)
                return false;

            RecastActiveHawkCompanions();
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (def.minionSpawnDefinition && IsSoulforgedWeaponAbility(def) && _activeSoulforgedWeaponMinions.Count > 0)
        {
            if (!allowSoulforgedRecastWhileActive)
                return false;

            RecastActiveSoulforgedWeapons();
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (IsOnCooldown(def.abilityId, out _))
            return false;

        // Lumber Frenzy: block recast while the buff is still active; cooldown
        // does not begin until the buff expires.
        if (string.Equals(def.abilityId, LumberFrenzyId, StringComparison.OrdinalIgnoreCase) && _lumberFrenzyActive)
            return false;

        if (string.Equals(def.abilityId, FishingFrenzyId, StringComparison.OrdinalIgnoreCase) && _fishingFrenzyActive)
            return false;

        // Cleaving Chop: same deferred-cooldown contract as Lumber Frenzy.
        if (string.Equals(def.abilityId, CleavingChopId, StringComparison.OrdinalIgnoreCase) && _cleavingChopActive)
            return false;

        if (string.Equals(def.abilityId, StaticArrowsId, StringComparison.OrdinalIgnoreCase) && _staticArrowsBuffActive)
            return false;

        if (string.Equals(def.abilityId, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase) && IsAvatarOfTheForestActive)
            return false;

        if (IsWarBannerAbilityId(def.abilityId) && (_warBannerCastRoutine != null || IsWarBannerActive))
            return false;

        if (IsLightningRodAbilityId(def.abilityId) && IsLightningRodActive)
            return false;

        if (IsPenetratingShotAbilityId(def.abilityId) && IsPenetratingShotInFlight)
            return false;

        if (IsHuntersSwiftnessAbilityId(def.abilityId) && IsHuntersSwiftnessActive)
            return false;

        if (IsTornadoAbilityId(def.abilityId) && IsTornadoActive)
        {
            if (!showLockedFeedback)
                return false;

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            RetargetActiveTornado();
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        // Spectral Axe: deferred cooldown starts when the projectile returns. Block recast while deployed.
        if (string.Equals(def.abilityId, SpectralAxeId, StringComparison.OrdinalIgnoreCase) && _spectralAxeActive)
            return false;

        if (string.Equals(def.abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
        {
            if (_powerSlashQueued)
                return false;
        }
        if (string.Equals(def.abilityId, TripleShotId, StringComparison.OrdinalIgnoreCase))
        {
            if (_tripleShotQueued)
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
        if (string.Equals(def.abilityId, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase))
        {
            if (IsCrusaderStrikeComboInProgress())
                return false;
        }
        if (string.Equals(def.abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase))
        {
            if (_crescentSlashQueued)
                return false;
        }
        bool isCrescentSlash = string.Equals(def.abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase);
        bool isGuardiansHammer = string.Equals(def.abilityId, GuardiansHammerId, StringComparison.OrdinalIgnoreCase);
        bool isFinalSeverance = string.Equals(def.abilityId, FinalSeveranceId, StringComparison.OrdinalIgnoreCase);
        bool isExecutionersDescent = string.Equals(def.abilityId, ExecutionersDescentId, StringComparison.OrdinalIgnoreCase);
        bool isBladestorm = string.Equals(def.abilityId, BladestormId, StringComparison.OrdinalIgnoreCase);
        bool isShadowStrike = string.Equals(def.abilityId, ShadowStrikeId, StringComparison.OrdinalIgnoreCase);
        bool isFlameCharge = string.Equals(def.abilityId, FlameChargeId, StringComparison.OrdinalIgnoreCase);
        bool isPenetratingShot = IsPenetratingShotAbilityId(def.abilityId);
        if (!isWhirlwind &&
            !isSnipe &&
            !isPenetratingShot &&
            !UsesMeleeApproachOnActivate(def) &&
            !isGuardiansHammer &&
            !AbilityDefersEnergyUntilActivated(def) &&
            !TrySpendAbilityResourceCost(def, showLockedFeedback))
            return false;

        // Summon abilities: no current-target requirement (unlike the generic instant-hit block below).
        if (def.minionSpawnDefinition)
        {
            if (!def.minionSpawnDefinition.runtimePrefab)
                return false;

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            if (!TrySpawnMinionForAbility(def))
            {
                RefundAbilityResourceCost(def);
                return false;
            }

            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (UsesMeleeApproachOnActivate(def))
            return TryUseMeleeApproachAbility(def, showLockedFeedback);

        if (string.Equals(def.abilityId, TripleShotId, StringComparison.OrdinalIgnoreCase))
        {
            if (_tripleShotQueued)
                return false;

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            _tripleShotQueued = true;
            _queuedTripleShotUsedEnergyInfusionMana = DidLastAbilitySpendUseEnergyInfusionMana(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (string.Equals(def.abilityId, RendId, StringComparison.OrdinalIgnoreCase))
        {
            if (_rendQueued)
                return false;
            _rendQueued = true;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (string.Equals(def.abilityId, EnvenomId, StringComparison.OrdinalIgnoreCase))
        {
            if (_envenomQueued)
                return false;
            _envenomQueued = true;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (string.Equals(def.abilityId, CleavingStrikesId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateCleavingStrikesBuff();
            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (string.Equals(def.abilityId, StaticArrowsId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateStaticArrowsBuff();
            _staticArrowsCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (IsWarBannerAbilityId(def.abilityId))
        {
            BeginWarBannerCast(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (IsLightningRodAbilityId(def.abilityId))
        {
            BeginLightningRodCast(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (IsPenetratingShotAbilityId(def.abilityId))
        {
            if (!CanHitAnyEnemyWithPenetratingShot())
                return false;

            if (combat == null)
                combat = GetComponent<PlayerCombatController>();

            if (combat != null && !combat.HasConsumableOffHandSupportAmmo())
            {
                if (showLockedFeedback)
                    player?.ShowPopup("Out of arrows.");
                return false;
            }

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            if (combat != null && !combat.TryConsumeOffHandSupportAmmoOnUse())
            {
                RefundAbilityResourceCost(def);
                if (showLockedFeedback)
                    player?.ShowPopup("Out of arrows.");
                return false;
            }

            BeginPenetratingShotCast(def);
            player.TriggerAttackAnim();
            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (IsHuntersSwiftnessAbilityId(def.abilityId))
        {
            BeginHuntersSwiftnessCast(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (IsTornadoAbilityId(def.abilityId))
        {
            BeginTornadoCast(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (string.Equals(def.abilityId, HammerTempestId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateHammerTempest();
            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (string.Equals(def.abilityId, LumberFrenzyId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateLumberFrenzyBuff();
            // Cooldown is deferred to start when the buff expires (see CleanupLumberFrenzyIfExpired).
            _lumberFrenzyCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (string.Equals(def.abilityId, FishingFrenzyId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateFishingFrenzyBuff();
            _fishingFrenzyCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (string.Equals(def.abilityId, CleavingChopId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateCleavingChopBuff();
            _cleavingChopCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (string.Equals(def.abilityId, SpectralAxeId, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryActivateSpectralAxe(def))
                return false;
            _spectralAxeCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (string.Equals(def.abilityId, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase))
        {
            ActivateAvatarOfTheForestBuff();
            _avatarOfForestCooldownAbilityDef = def;
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }
        if (isCrescentSlash)
        {
            if (requireCrescentSlashTargetInFacingLane && !CanHitAnyEnemyWithCrescentSlash())
                return false;

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            _crescentSlashUsedEnergyInfusionMana = DidLastAbilitySpendUseEnergyInfusionMana(def);

            if (combat == null)
                combat = GetComponent<PlayerCombatController>();

            bool castNow = combat != null && combat.CanConsumeAttackCycleNow();
            if (castNow && combat.TryConsumeAttackCycleForAbilityCast())
                FireCrescentSlashImpact(def);
            else
            {
                _crescentSlashQueued = true;
                _crescentSlashEnergyCommitted = true;
            }

            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (isGuardiansHammer)
        {
            if (requireGuardiansHammerTargetInFacingZone && !CanHitAnyEnemyWithGuardiansHammer())
                return false;

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            _guardiansHammerUsedEnergyInfusionMana = DidLastAbilitySpendUseEnergyInfusionMana(def);
            FireGuardiansHammerImpact(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (isWhirlwind)
        {
            if (_whirlwindChanneling)
                return true;

            if (requireWhirlwindTargetInRadius && !CanHitAnyEnemyWithWhirlwind())
                return false;

            if (requireWhirlwindTargetInRadius && !HasEnoughEnergyForAutoBattleWhirlwind())
                return false;

            if (!TryStartWhirlwindChannel(def, showLockedFeedback, requireWhirlwindTargetInRadius))
                return false;

            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (isSnipe)
        {
            EnemyBaseController snipeTarget = combat != null ? combat.CurrentTarget : null;
            if (snipeTarget == null || snipeTarget.IsDead)
                return false;

            if (def.RequiresKeyboardRangeCheckToActivate() && combat != null && !combat.IsEnemyWithinAttackRange(snipeTarget))
                return false;

            if (combat != null && !combat.HasConsumableOffHandSupportAmmo())
            {
                if (showLockedFeedback)
                    player?.ShowPopup("Out of arrows.");
                return false;
            }

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            if (!TryBeginSnipeCharge(def, snipeTarget, snipeAutoBattleFullCharge))
            {
                RefundAbilityResourceCost(def);
                return false;
            }

            return true;
        }

        if (isFinalSeverance)
        {
            if (_finalSeveranceChanneling || _finalSeveranceRoutine != null)
                return false;

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            _finalSeveranceRoutine = StartCoroutine(CoFinalSeverance(def));
            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (isBladestorm)
        {
            if (_bladestormRoutine != null)
            {
                if (showLockedFeedback)
                    player?.ShowPopup("Bladestorm is still in progress.");
                return false;
            }

            if (!TryResolveBladestormTarget(out EnemyBaseController bladestormTarget))
            {
                if (showLockedFeedback)
                    player?.ShowPopup("No enemy in front of you.");
                return false;
            }

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            _bladestormRoutine = StartCoroutine(CoBladestorm(def, bladestormTarget));
            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (isShadowStrike)
        {
            if (!TryResolveShadowStrikeTarget(out EnemyBaseController shadowTarget))
            {
                if (showLockedFeedback)
                    player.ShowPopup("No enemy in range.");
                return false;
            }

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            StartCooldown(def);
            ExecuteShadowStrike(def, shadowTarget);

            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (isFlameCharge)
        {
            if (_flameChargeRoutine != null)
                return false;

            RefreshFlameChargeChargesFromSkillTree();
            if (GetFlameChargeReadyChargeCount() <= 0)
                return false;

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            if (!TryStartFlameChargeChargeCooldown(def))
                return false;

            _flameChargeRoutine = StartCoroutine(CoFlameCharge(def));
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
            return true;
        }

        if (isExecutionersDescent)
        {
            if (_executionersDescentRoutine != null)
            {
                if (showLockedFeedback)
                    player?.ShowPopup("Executioner's Descent is still in progress.");
                return false;
            }

            EnemyBaseController descentTarget = ResolveExecutionersDescentTarget();
            if (descentTarget == null)
            {
                player?.ShowPopup("No valid target.");
                return false;
            }

            if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
                return false;

            _executionersDescentTargetDiedDuringDescent = false;
            int descentEnhance = GetExecutionersDescentSelectedChoice();
            _executionersDescentRoutine = descentEnhance == 2
                ? StartCoroutine(CoExecutionersDescentContinuum(def, descentTarget))
                : StartCoroutine(CoExecutionersDescent(def, descentTarget));
            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            LogAbilityUsed(def);
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
        float wM = def.GetWeaponHitScalingMultiplier();
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apM = GetAbilityPowerDamageMultiplierForAbility(def);
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
        float overloadMult = GetBattleEngineOverloadDamageMultiplier();
        float physLine = basePhysical * wM + ailmentBonus;
        float magLine = baseMagic * wM * elemM + elementBonus * elemM;
        float physPart = physLine * allM * apM * overloadMult;
        float magPart = magLine * allM * apM * overloadMult;
        float corrPart = (baseCorruption * wM) * allM * apM * overloadMult;

        PrepareBattleEngineAbilityHitSession(def);

        bool wasCrit = false;
        float critMult = 1f;
        SplitDamage preCritHit = new SplitDamage(physPart, magPart, corrPart);
        if (preCritHit.CanCrit && UnityEngine.Random.value < GetEffectiveAbilityCritChance(target))
        {
            wasCrit = true;
            critMult = Mathf.Max(1f, stats.CritMultiplier);
        }

        SplitDamage hit = new SplitDamage(
            Mathf.Max(0f, physPart * critMult),
            Mathf.Max(0f, magPart * critMult),
            Mathf.Max(0f, corrPart * critMult));
        ApplyActiveDamageConversions(ref hit);

        float cond = GetConditionalMeleeDamageMultiplier(target);
        if (wasCrit && stats != null)
        {
            cond *= stats.GetPredatorsInstinctExecutionerCritDamageFactor(target, true);
            cond *= stats.GetOpportunisticCritDamageFactor(target, true);
        }

        hit = new SplitDamage(
            Mathf.Max(0f, hit.physical * cond),
            Mathf.Max(0f, hit.magic * cond),
            Mathf.Max(0f, hit.corruptionDamage * cond));

        int phys = Mathf.Max(0, Mathf.RoundToInt(hit.physical));
        int mag = Mathf.Max(0, Mathf.RoundToInt(hit.magic));
        int corr = Mathf.Max(0, Mathf.RoundToInt(hit.corruptionDamage));
        string sourceLabel = GetAbilityOutgoingDamageSourceLabel(def.abilityId);
        float physDealt = 0f;
        float magDealt = 0f;
        int corrDealt = 0;
        if (phys > 0)
            physDealt = Mathf.Max(0f, target.TakeDamage(phys, DamageType.Physical, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null, outgoingDpsSourceLabel: sourceLabel));
        if (mag > 0)
            magDealt = Mathf.Max(0f, target.TakeDamage(mag, DamageType.Magic, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null, outgoingDpsSourceLabel: sourceLabel));
        if (corr > 0)
            corrDealt = target.TakeDamage(corr, DamageType.Corruption, wasCrit, transform, stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null, outgoingDpsSourceLabel: sourceLabel);

        TryNotifyLightningRodSurgeFromDealt(target, physDealt, magDealt, sourceLabel);
        int dealt = Mathf.RoundToInt(physDealt + magDealt + corrDealt);

        TryGrantBattleEngineEnergyOnAbilityHit(def, dealt > 0);

        // Fire the attack anim as feedback, but do not modify basic attack cooldown timing.
        player.TriggerAttackAnim();
        StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        ApplyBattleEngineOnAbilityCommitEffects(def, beginHitSession: false);
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
    private void BuildWhirlwindAbilityScaledSplit(
        AbilityDefinition def,
        EnemyBaseController critTarget,
        out SplitDamage nonCritBase,
        out bool wasCrit,
        out float lightningMagicNonCrit)
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
        float wM = def.GetWeaponHitScalingMultiplier();
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apM = GetAbilityPowerDamageMultiplierForAbility(def);
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);

        float physLine = basePhysical * wM + ailmentBonus;
        float magLine = baseMagic * wM * elemM + elementBonus * elemM;
        float physPart = physLine * allM * apM;
        float magPart = magLine * allM * apM;
        float corruptionPart = (baseCorruption * wM) * allM * apM;

        float fWeaponLightning = stats.GetMeleeMagicLightningFraction();
        float weaponLightMag = baseMagic * wM * elemM * allM * apM * fWeaponLightning;
        float elemLightMag = stats.CurrentMagicAttackType == MagicAttackType.Lightning ? elementBonus * elemM * allM * apM : 0f;
        lightningMagicNonCrit = weaponLightMag + elemLightMag;

        nonCritBase = new SplitDamage(physPart, magPart, corruptionPart);

        wasCrit = false;
        if (nonCritBase.CanCrit && UnityEngine.Random.value < GetEffectiveAbilityCritChance(critTarget))
            wasCrit = true;
    }

    private float GetEffectiveAbilityCritChance(EnemyBaseController target)
    {
        if (stats == null)
            return 0f;

        float critChance = stats.CritChance;
        critChance += stats.GetOpportunisticAbilityCritChanceBonus(target);
        return Mathf.Clamp01(critChance);
    }

    private bool TryUseWhirlwind(AbilityDefinition def)
    {
        if (stats == null || def == null)
            return false;

        _whirlwindEnemiesInContactThisFrame.Clear();
        float hitInterval = GetWhirlwindChannelHitIntervalSeconds();

        float channelSeconds = GetWhirlwindChannelElapsedSeconds();
        float radius = GetWhirlwindEffectiveRadius(channelSeconds);
        float damageMultiplier = GetWhirlwindChannelDamageMultiplier(channelSeconds);
        if (stats != null)
        {
            damageMultiplier *= AbilityCombatPower.GetWhirlwindWeaponSpeedHitDamageMultiplier(
                Mathf.Max(0.01f, stats.AttacksPerSecond));
        }

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        float ownerX = transform.position.x;
        float ownerHalf = GetOwnerHalfWidthX();
        bool hadAnyTargetInRange = false;
        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            int enemyId = enemy.GetInstanceID();
            bool inRange = IsEnemyWithinWhirlRange(enemy, radius, ownerX, ownerHalf, out _);
            if (!inRange)
                continue;

            hadAnyTargetInRange = true;
            _whirlwindEnemiesInContactThisFrame.Add(enemyId);

            if (_whirlwindLastHitTimeByEnemyId.TryGetValue(enemyId, out float lastHitAt) &&
                Time.time + 0.0001f < lastHitAt + hitInterval)
                continue;

            ApplyWhirlwindHitToTarget(enemy, def, damageMultiplier);
            _whirlwindLastHitTimeByEnemyId[enemyId] = Time.time;
        }

        CleanupWhirlwindContactCache();
        if (!hadAnyTargetInRange)
            return true;

        return true;
    }

    private void TrySpawnGaleforceTwister(AbilityDefinition def, float whirlwindRadius)
    {
        if (def == null || GetWhirlwindSelectedChoice() != 0)
            return;

        float twisterRadius = whirlwindRadius * AbilityCombatPower.WhirlwindGaleforceTwisterRadiusScale;
        float damageMultiplier =
            GetWhirlwindChannelDamageMultiplier(GetWhirlwindChannelElapsedSeconds())
            * AbilityCombatPower.WhirlwindGaleforceTwisterDamageMultiplier;
        float offsetX = UnityEngine.Random.Range(-whirlwindRadius * 0.45f, whirlwindRadius * 0.45f);

        abilityVfx?.SpawnGaleforceTwisterNearPlayer(offsetX, twisterRadius, damageMultiplier);
    }

    private void TickGaleforceTwisterDamage()
    {
        if (abilityVfx == null)
            abilityVfx = GetComponent<PlayerAbilityVfxController>();

        abilityVfx?.CollectActiveGaleforceTwisters(_galeforceTwisterDamageBuffer);
        if (_galeforceTwisterDamageBuffer.Count == 0)
            return;

        AbilityDefinition def = GetAbilityDefinition(WhirlwindId);
        if (def == null || stats == null || !IsAbilityAllowedBySkillProgress(def))
            return;

        float hitInterval = GetWhirlwindChannelHitIntervalSeconds();
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();

        for (int t = 0; t < _galeforceTwisterDamageBuffer.Count; t++)
        {
            GaleforceTwisterInstance twister = _galeforceTwisterDamageBuffer[t];
            if (twister == null)
                continue;

            float centerX = twister.transform.position.x;
            float radius = twister.HitRadius;
            float damageMultiplier = twister.DamageMultiplier;

            for (int i = 0; i < allEnemies.Count; i++)
            {
                EnemyBaseController enemy = allEnemies[i];
                if (enemy == null || enemy.IsDead)
                    continue;

                int enemyId = enemy.GetInstanceID();
                if (!IsEnemyWithinWhirlRange(enemy, radius, centerX, 0f, out _))
                    continue;

                if (!twister.CanHitEnemy(enemyId, hitInterval))
                    continue;

                ApplyWhirlwindHitToTarget(
                    enemy,
                    def,
                    damageMultiplier,
                    AbilityCombatPower.WhirlwindTwistersOutgoingDamageSourceLabel);
                twister.RecordHit(enemyId);
            }
        }
    }

    private void ApplyWhirlwindHitToTarget(
        EnemyBaseController target,
        AbilityDefinition def,
        float damageMultiplier,
        string outgoingDamageSourceLabel = null)
    {
        if (target == null || target.IsDead || def == null || stats == null)
            return;

        BuildWhirlwindAbilityScaledSplit(def, target, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        SplitDamage rolled = new SplitDamage(
            rolledNonCrit.physical * critMult,
            rolledNonCrit.magic * critMult,
            rolledNonCrit.corruptionDamage * critMult);
        SplitDamage firstHit = rolled * damageMultiplier;

        float lightningAfterCrit = lightningMagNonCrit * critMult * damageMultiplier;
        float firstFrac = firstHit.magic > 1e-8f ? Mathf.Clamp01(lightningAfterCrit / firstHit.magic) : 0f;

        DealtHit dealt = ApplyAbilitySplitDamageToEnemy(
            target, def, firstHit, wasCrit, firstFrac, outgoingDamageSourceLabelOverride: outgoingDamageSourceLabel);
        ApplyOnHitEffects(target, dealt);
    }

    private void CleanupWhirlwindContactCache()
    {
        if (_whirlwindLastHitTimeByEnemyId.Count == 0)
            return;

        _whirlwindContactRemovalBuffer.Clear();
        foreach (int enemyId in _whirlwindLastHitTimeByEnemyId.Keys)
        {
            if (!_whirlwindEnemiesInContactThisFrame.Contains(enemyId))
                _whirlwindContactRemovalBuffer.Add(enemyId);
        }

        for (int i = 0; i < _whirlwindContactRemovalBuffer.Count; i++)
            _whirlwindLastHitTimeByEnemyId.Remove(_whirlwindContactRemovalBuffer[i]);
    }

    private bool TryUseCrusaderStrike(AbilityDefinition def)
    {
        if (def == null || stats == null)
            return false;

        if (IsCrusaderStrikeComboInProgress())
            return false;

        if (!TrySpendAbilityResourceCost(def, showInsufficientFeedback: true))
            return false;

        _crusaderStrikeComboStep = 0;
        _crusaderStrikePrimedStage = 1;
        _crusaderStrikeQueued = true;
        RefreshCrusaderStrikeComboTimeout();
        return true;
    }

    private bool TryQueueNextCrusaderStrikeHit(int completedStage)
    {
        if (completedStage >= CrusaderStrikeFinalComboStep)
            return false;

        int nextStage = completedStage + 1;
        AbilityDefinition def = GetAbilityDefinition(CrusaderStrikeId);
        bool secondCastFree = nextStage == 2 && IsCrusaderStrikeSecondCastFree();

        if (!secondCastFree &&
            def != null &&
            !TrySpendAbilityResourceCost(def, showInsufficientFeedback: false))
        {
            ForceEndCrusaderStrikeCombo(applyCooldown: false);
            return false;
        }

        _crusaderStrikePrimedStage = nextStage;
        _crusaderStrikeQueued = true;
        RefreshCrusaderStrikeComboTimeout();
        return true;
    }

    private static float GetCrusaderStrikeWeaponMultiplier(int castStep)
    {
        return castStep switch
        {
            1 => CrusaderStrikeFirstHitWeaponMultiplier,
            2 => CrusaderStrikeSecondHitWeaponMultiplier,
            _ => CrusaderStrikeFinalHitWeaponMultiplier
        };
    }

    private float GetCrusaderStrikeFinalFireScaling(AbilityDefinition def)
    {
        if (stats == null || def == null)
            return 1f;

        float firePortionScale = Mathf.Max(0f, def.fireDamageMultiplier);
        float fireSkillBonus = Mathf.Max(0f, stats.ElementSkillDamageScalingFractionFor(MagicAttackType.Fire));
        float scale = firePortionScale > 0f
            ? 1f + fireSkillBonus * firePortionScale
            : 1f;

        if (GetCrusaderStrikeSelectedChoice() == CrusaderStrikeFireBalanceChoiceIndex)
            scale *= AbilityCombatPower.CrusaderStrikeFireBalanceFinalStrikeFireMultiplier;

        return scale;
    }

    private SplitDamage BuildCrusaderStrikeSplit(
        SplitDamage rolled,
        float weaponMultiplier,
        bool finalStrike,
        AbilityDefinition def)
    {
        if (stats == null || !stats.CurrentMeleeWeaponHasPhysicalOrFireDamage())
            return SplitDamage.Zero;

        float usablePhysical = Mathf.Max(0f, rolled.physical) * stats.GetMeleeWeaponPhysicalFraction();
        float usableFire = Mathf.Max(0f, rolled.magic) * stats.GetMeleeMagicFireFraction();
        float weaponMultClamped = Mathf.Max(0f, weaponMultiplier);
        float apM = GetAbilityPowerDamageMultiplierForAbility(def);

        if (finalStrike)
        {
            float totalWeaponDamage = (usablePhysical + usableFire) * weaponMultClamped;
            return new SplitDamage(0f, totalWeaponDamage * GetCrusaderStrikeFinalFireScaling(def) * apM, 0f);
        }

        return new SplitDamage(
            usablePhysical * weaponMultClamped * apM,
            usableFire * weaponMultClamped * apM,
            0f);
    }

    private IEnumerator CoFinalSeverance(AbilityDefinition def)
    {
        _finalSeveranceChanneling = true;
        player?.SetTeleportDamageImmune(true);
        GameplayScreenOverlay.Show(
            GameplayScreenOverlay.FinalSeveranceChannelId,
            GameplayScreenOverlay.FinalSeveranceChannelTint,
            GameplayScreenOverlay.Spec.DefaultAbilityChannel);

        try
        {
            float channelSeconds = AbilityCombatPower.FinalSeveranceChannelSeconds;
            float halfReach = AbilityCombatPower.FinalSeveranceHitRangeHalfWidth;
            float facing = GetCombatFacingSign();

            abilityVfx?.SpawnFinalSeveranceChannelWindup(halfReach, facing);

            if (player != null)
            {
                player.SetAbilityChannelLock(true, channelSeconds);
                player.TriggerAttackAnim();
            }

            yield return new WaitForSeconds(channelSeconds);

            if (player == null || stats == null || def == null)
                yield break;

            if (player.IsDead || stats.IsDead)
                yield break;

            abilityVfx?.SpawnFinalSeveranceStrike(halfReach, facing);
            player.TriggerAttackAnim();
            ApplyFinalSeveranceHits(def);
        }
        finally
        {
            GameplayScreenOverlay.Hide(GameplayScreenOverlay.FinalSeveranceChannelId);
            _finalSeveranceChanneling = false;
            _finalSeveranceRoutine = null;
            player?.SetAbilityChannelLock(false);
            player?.SetTeleportDamageImmune(false);
        }
    }

    private void ApplyFinalSeveranceHits(AbilityDefinition def)
    {
        if (stats == null || def == null)
            return;

        List<EnemyBaseController> targets = CollectFinalSeveranceTargets();
        if (targets.Count == 0)
            return;

        int selected = GetFinalSeveranceSelectedChoice();
        bool worldbreaker = selected == 0;
        bool thousandCuts = selected == 1;
        int hitCount = thousandCuts ? AbilityCombatPower.FinalSeveranceThousandCutsHitCount : 1;
        float hitFraction = thousandCuts ? AbilityCombatPower.FinalSeveranceThousandCutsHitFraction : 1f;

        for (int t = 0; t < targets.Count; t++)
        {
            EnemyBaseController target = targets[t];
            if (!target || target.IsDead)
                continue;

            BuildWhirlwindAbilityScaledSplit(def, target, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
            float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
            SplitDamage rolled = new SplitDamage(
                rolledNonCrit.physical * critMult,
                rolledNonCrit.magic * critMult,
                rolledNonCrit.corruptionDamage * critMult);
            float lightningAfterCrit = lightningMagNonCrit * critMult;

            if (worldbreaker && IsEnemyAtFullHealthForFinalSeveranceWorldbreaker(target))
            {
                rolled *= AbilityCombatPower.FinalSeveranceWorldbreakerBonusMultiplier;
                lightningAfterCrit *= AbilityCombatPower.FinalSeveranceWorldbreakerBonusMultiplier;
            }

            for (int h = 0; h < hitCount; h++)
            {
                if (!target || target.IsDead)
                    break;

                SplitDamage hit = rolled * hitFraction;
                float lightningHit = lightningAfterCrit * hitFraction;
                float frac = hit.magic > 1e-8f ? Mathf.Clamp01(lightningHit / hit.magic) : 0f;
                DealtHit dealt = ApplyAbilitySplitDamageToEnemy(target, def, hit, wasCrit, frac);
                ApplyOnHitEffects(target, dealt);
                if (player != null && dealt.Total > 0f)
                    player.ApplyLifeSteal(dealt.Total);
            }
        }
    }

    private static bool IsEnemyAtFullHealthForFinalSeveranceWorldbreaker(EnemyBaseController enemy)
    {
        if (enemy == null)
            return false;

        int maxHp = enemy.MaxHP;
        if (maxHp <= 0)
            return false;

        return (float)enemy.HP / maxHp >= AbilityCombatPower.FinalSeveranceWorldbreakerFullHealthThreshold01;
    }

    private IEnumerator CoBladestorm(AbilityDefinition def, EnemyBaseController initialTarget)
    {
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        int selected = GetBladestormSelectedChoice();
        bool bladestormFinale = selected == 0;
        bool endlessCarnage = selected == 1;
        EnemyBaseController lockedTarget = initialTarget;

        try
        {
            float approachEnd = Time.time + AbilityCombatPower.BladestormApproachTimeoutSeconds;
            while (Time.time < approachEnd)
            {
                if (player == null || stats == null || def == null)
                    yield break;

                if (player.IsDead || stats.IsDead)
                    yield break;

                if (lockedTarget == null || lockedTarget.IsDead || !lockedTarget.gameObject.activeInHierarchy)
                    yield break;

                if (combat != null && combat.IsEnemyWithinAttackRange(lockedTarget))
                    break;

                MaintainBladestormTargetLock(lockedTarget, allowPathing: true);
                yield return null;
            }

            if (lockedTarget == null || lockedTarget.IsDead ||
                combat == null || !combat.IsEnemyWithinAttackRange(lockedTarget))
            {
                player?.ShowPopup("Could not reach target.");
                yield break;
            }

            _bladestormChanneling = true;
            float channelSeconds = AbilityCombatPower.BladestormChannelSeconds;

            float attackRate = Mathf.Max(0.05f, stats.AttacksPerSecond * AbilityCombatPower.BladestormAttackSpeedMultiplier);
            int totalStrikes = Mathf.Max(1, Mathf.RoundToInt(attackRate * channelSeconds));
            float strikeInterval = channelSeconds / totalStrikes;
            int strikesRemaining = totalStrikes;

            if (player != null)
            {
                player.SetActionOverride(PlayerController.PlayerAction.Fighting);
                player.ExtendAttackLockUntil(Time.time + channelSeconds + 0.5f);
            }

            BeginBladestormCombatModifiers();

            float channelEnd = Time.time + channelSeconds;
            while (strikesRemaining > 0 && Time.time < channelEnd)
            {
                if (player == null || stats == null || def == null)
                    yield break;

                if (player.IsDead || stats.IsDead)
                    yield break;

                if (!TryKeepBladestormLockedOnTarget(ref lockedTarget, endlessCarnage))
                    yield break;

                ApplyBladestormChannelMovementLock(endlessCarnage, lockedTarget);
                MaintainBladestormTargetLock(lockedTarget, allowPathing: endlessCarnage);

                if (combat.IsEnemyWithinAttackRange(lockedTarget))
                {
                    ApplyBladestormHit(lockedTarget, def, AbilityCombatPower.BladestormNormalHitWeaponMultiplier);
                    player?.TriggerAttackAnim();
                }

                strikesRemaining--;
                if (strikesRemaining <= 0)
                    break;

                yield return new WaitForSeconds(strikeInterval);
            }

            if (bladestormFinale &&
                lockedTarget != null && !lockedTarget.IsDead && lockedTarget.gameObject.activeInHierarchy &&
                combat != null && combat.IsEnemyWithinAttackRange(lockedTarget))
            {
                ApplyBladestormChannelMovementLock(endlessCarnage, lockedTarget);
                MaintainBladestormTargetLock(lockedTarget, allowPathing: false);
                ApplyBladestormHit(lockedTarget, def, AbilityCombatPower.BladestormFinaleHitWeaponMultiplier, isFinale: true);
                player?.TriggerAttackAnim();
            }
        }
        finally
        {
            EndBladestormInstanceState();
        }
    }

    private void ApplyBladestormChannelMovementLock(bool endlessCarnage, EnemyBaseController target)
    {
        if (player == null || combat == null)
            return;

        if (!endlessCarnage)
        {
            player.SetMovementLocked(true);
            return;
        }

        bool inRange = target != null && !target.IsDead && combat.IsEnemyWithinAttackRange(target);
        player.SetMovementLocked(inRange);
    }

    private void BeginBladestormCombatModifiers()
    {
        if (stats == null || _bladestormCombatDamageTakenApplied)
            return;

        _bladestormSavedCombatDamageTakenMultiplier = stats.CombatDamageTakenMultiplier;
        stats.CombatDamageTakenMultiplier = AbilityCombatPower.BladestormChannelDamageTakenMultiplier;
        _bladestormCombatDamageTakenApplied = true;
    }

    private void EndBladestormCombatModifiers()
    {
        if (!_bladestormCombatDamageTakenApplied || stats == null)
            return;

        stats.CombatDamageTakenMultiplier = _bladestormSavedCombatDamageTakenMultiplier;
        _bladestormCombatDamageTakenApplied = false;
    }

    private void EndBladestormInstanceState()
    {
        _bladestormChanneling = false;
        _bladestormRoutine = null;
        EndBladestormCombatModifiers();
        player?.SetMovementLocked(false);
        player?.ClearActionOverride();
    }

    private bool TryKeepBladestormLockedOnTarget(ref EnemyBaseController lockedTarget, bool endlessCarnage)
    {
        if (lockedTarget != null && !lockedTarget.IsDead && lockedTarget.gameObject.activeInHierarchy &&
            IsEnemyValidBladestormTarget(lockedTarget))
            return true;

        if (!endlessCarnage)
            return false;

        EnemyBaseController replacement = ResolveNearestBladestormRetarget();
        if (replacement == null)
            return false;

        lockedTarget = replacement;
        return true;
    }

    private void MaintainBladestormTargetLock(EnemyBaseController target, bool allowPathing)
    {
        if (!target || player == null)
            return;

        float enemyX = target.transform.position.x;
        player.FaceTargetX(enemyX);

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null || stats == null)
            return;

        AbilityDefinition bladestormDef = GetAbilityDefinition(BladestormId);
        if (bladestormDef == null || bladestormDef.SetsTargetOnHit())
            combat.SetTargetIfNone(target);

        if (combat.IsEnemyWithinAttackRange(target))
        {
            player.StopMoveOnly();
            return;
        }

        if (!allowPathing)
            return;

        float myRange = Mathf.Max(0f, stats.Range) + combat.GetMeleeRangePadding();
        Collider2D playerCol = player.GetComponent<Collider2D>();
        Collider2D enemyCol = target.GetComponent<Collider2D>();
        if (!enemyCol)
            enemyCol = target.GetComponentInChildren<Collider2D>();

        float myHalf = HalfWidthX(playerCol);
        float enemyHalf = HalfWidthX(enemyCol);
        float desiredCenterDist = myRange + myHalf + enemyHalf;
        float myX = player.transform.position.x;
        float desiredX = myX < enemyX ? enemyX - desiredCenterDist : enemyX + desiredCenterDist;
        player.MoveToPointX_Combat(desiredX);
    }

    private static float HalfWidthX(Collider2D col) =>
        col != null ? col.bounds.extents.x : 0.25f;

    private bool TryResolveBladestormTarget(out EnemyBaseController target)
    {
        target = null;
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        EnemyBaseController engaged = combat != null ? combat.GetPrimaryEngagedEnemy() : null;
        if (IsEnemyValidBladestormTarget(engaged))
        {
            target = engaged;
            return true;
        }

        List<(EnemyBaseController enemy, float dist)> forwardHits =
            CollectBladestormForwardHits(AbilityCombatPower.BladestormForwardReach);
        if (forwardHits.Count == 0)
            return false;

        forwardHits.Sort((a, b) => a.dist.CompareTo(b.dist));
        target = forwardHits[0].enemy;
        return target != null;
    }

    private EnemyBaseController ResolveNearestBladestormRetarget()
    {
        EnemyBaseController engaged = GetPreferredCombatEngagedEnemy();
        if (IsEnemyValidBladestormTarget(engaged))
            return engaged;

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDist = float.MaxValue;
        Vector3 origin = transform.position;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!IsEnemyValidBladestormTarget(enemy))
                continue;

            float dist = Vector3.Distance(origin, enemy.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    private bool IsEnemyValidBladestormTarget(EnemyBaseController enemy)
    {
        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            return false;

        if (combat != null && combat.IsEnemyWithinAttackRange(enemy))
            return true;

        return IsEnemyInBladestormForwardArc(enemy, out _);
    }

    private bool IsEnemyInBladestormForwardArc(EnemyBaseController enemy, out float forwardDistance)
    {
        forwardDistance = float.MaxValue;
        if (!enemy || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            return false;

        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        float laneWidth = Mathf.Max(0.6f, AbilityCombatPower.BladestormForwardReach * 0.35f);
        Vector3 to = enemy.transform.position - origin;
        forwardDistance = to.x * facing;
        if (forwardDistance <= 0f || forwardDistance > AbilityCombatPower.BladestormForwardReach)
            return false;
        if (Mathf.Abs(to.y) > laneWidth)
            return false;
        return true;
    }

    private List<(EnemyBaseController enemy, float dist)> CollectBladestormForwardHits(float reach)
    {
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        var forwardHits = new List<(EnemyBaseController enemy, float dist)>(allEnemies.Count);
        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        float laneWidth = Mathf.Max(0.6f, reach * 0.35f);

        for (int i = 0; i < allEnemies.Count; i++)
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

    private void ApplyBladestormHit(
        EnemyBaseController target,
        AbilityDefinition def,
        float weaponDamageMultiplier,
        bool isFinale = false)
    {
        if (!target || target.IsDead || stats == null || def == null)
            return;

        BuildWhirlwindAbilityScaledSplit(def, target, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
        float scale = weaponDamageMultiplier / Mathf.Max(0.0001f, def.GetWeaponHitScalingMultiplier());
        rolledNonCrit *= scale;
        lightningMagNonCrit *= scale;

        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        SplitDamage rolled = new SplitDamage(
            rolledNonCrit.physical * critMult,
            rolledNonCrit.magic * critMult,
            rolledNonCrit.corruptionDamage * critMult);
        float lightningAfterCrit = lightningMagNonCrit * critMult;
        float frac = rolled.magic > 1e-8f ? Mathf.Clamp01(lightningAfterCrit / rolled.magic) : 0f;

        DealtHit dealt = ApplyAbilitySplitDamageToEnemy(target, def, rolled, wasCrit, frac);
        ApplyOnHitEffects(target, dealt);
        if (player != null && dealt.Total > 0f)
        {
            player.ApplyLifeSteal(dealt.Total);
            SpawnBladestormHitSlashVfx(target, isFinale);
        }
    }

    private void SpawnBladestormHitSlashVfx(EnemyBaseController target, bool isFinale)
    {
        if (abilityVfx == null || player == null || target == null)
            return;

        Vector3 playerPos = player.transform.position;
        Vector3 enemyPos = target.transform.position;
        if (isFinale)
            abilityVfx.SpawnBladestormFinaleSlash(playerPos, enemyPos);
        else
            abilityVfx.SpawnBladestormHitSlash(playerPos, enemyPos);
    }

    private int GetBladestormSelectedChoice()
    {
        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.BladestormEnhancementParentSpineNodeId,
            -1);
    }

    private List<EnemyBaseController> CollectFinalSeveranceTargets()
    {
        var result = new List<EnemyBaseController>(AbilityCombatPower.FinalSeveranceMaxTargets);
        float ownerX = transform.position.x;
        float maxDist = AbilityCombatPower.FinalSeveranceHitRangeHalfWidth;

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        var scratch = new List<(EnemyBaseController enemy, float dist)>(allEnemies.Count);
        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            float dist = Mathf.Abs(enemy.transform.position.x - ownerX);
            if (dist <= maxDist)
                scratch.Add((enemy, dist));
        }

        scratch.Sort((a, b) => a.dist.CompareTo(b.dist));
        int cap = Mathf.Min(AbilityCombatPower.FinalSeveranceMaxTargets, scratch.Count);
        for (int i = 0; i < cap; i++)
            result.Add(scratch[i].enemy);

        return result;
    }

    private int GetFinalSeveranceSelectedChoice()
    {
        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            return -1;

        int selected = skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.FinalSeveranceEnhancementParentSpineNodeId, -1);
        if (selected >= 0)
            return selected;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 45, -1);
    }

    private IEnumerator CoExecutionersDescentContinuum(AbilityDefinition def, EnemyBaseController initialTarget)
    {
        try
        {
            Vector3 targetWorld = initialTarget != null ? initialTarget.transform.position : transform.position;
            // Same lowest impact point as the end of a normal descent (not apex / spawn height).
            Vector3 impactPoint = abilityVfx != null
                ? abilityVfx.ResolveExecutionersDescentMinimumImpactWorldPosition(targetWorld)
                : targetWorld;

            abilityVfx?.BeginExecutionersDescentContinuum(impactPoint);

            float interval = AbilityCombatPower.ExecutionersDescentContinuumShockwaveIntervalSeconds;
            int pulseCount = AbilityCombatPower.GetExecutionersDescentContinuumShockwaveCount();
            float elapsed = 0f;

            for (int pulse = 0; pulse < pulseCount; pulse++)
            {
                float pulseAt = pulse * interval;
                while (elapsed < pulseAt)
                {
                    if (player == null || stats == null || def == null)
                        yield break;

                    if (player.IsDead || stats.IsDead)
                        yield break;

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                ApplyExecutionersDescentContinuumPulse(def, impactPoint);
            }
        }
        finally
        {
            EndExecutionersDescentInstanceState();
        }
    }

    private IEnumerator CoExecutionersDescent(AbilityDefinition def, EnemyBaseController initialTarget)
    {
        try
        {
            float descentSeconds = AbilityCombatPower.ExecutionersDescentDescentSeconds;
            Vector3 impactPoint = initialTarget != null ? initialTarget.transform.position : transform.position;
            EnemyBaseController trackedTarget = initialTarget;
            bool targetWasAliveAtCast = trackedTarget != null && !trackedTarget.IsDead;

            abilityVfx?.BeginExecutionersDescent(trackedTarget, impactPoint, descentSeconds);

            float elapsed = 0f;
            while (elapsed < descentSeconds)
            {
                if (player == null || stats == null || def == null)
                    yield break;

                if (player.IsDead || stats.IsDead)
                    yield break;

                bool trackedValid = trackedTarget != null && !trackedTarget.IsDead &&
                                    IsEnemyWithinExecutionersDescentCastRange(trackedTarget);

                if (!trackedValid)
                {
                    if (targetWasAliveAtCast && trackedTarget != null && trackedTarget.IsDead)
                    {
                        _executionersDescentTargetDiedDuringDescent = true;
                        impactPoint = trackedTarget.transform.position;
                        yield return CoExecutionersDescentRushToImpact(def, impactPoint, trackedTarget);
                        yield break;
                    }

                    if (trackedTarget != null && !trackedTarget.IsDead)
                    {
                        impactPoint = trackedTarget.transform.position;
                        abilityVfx?.UpdateExecutionersDescent(trackedTarget, impactPoint, elapsed);
                    }
                    else
                    {
                        EnemyBaseController replacement = ResolveExecutionersDescentTargetInCastRange(
                            combat != null ? combat.CurrentTarget : null);
                        if (replacement != null)
                        {
                            trackedTarget = replacement;
                            impactPoint = trackedTarget.transform.position;
                            abilityVfx?.UpdateExecutionersDescent(trackedTarget, impactPoint, elapsed);
                        }
                        else
                        {
                            ApplyExecutionersDescentEarlyDetonateNoTargetsInRange(def, impactPoint, trackedTarget);
                            yield break;
                        }
                    }
                }
                else
                {
                    impactPoint = trackedTarget.transform.position;
                    abilityVfx?.UpdateExecutionersDescent(trackedTarget, impactPoint, elapsed);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return FinishExecutionersDescentWithImpact(def, impactPoint, trackedTarget);
        }
        finally
        {
            EndExecutionersDescentInstanceState();
        }
    }

    private void EndExecutionersDescentInstanceState()
    {
        _executionersDescentRoutine = null;
        _executionersDescentTargetDiedDuringDescent = false;
        abilityVfx?.StopExecutionersDescentVfx();
    }

    private IEnumerator CoExecutionersDescentRushToImpact(
        AbilityDefinition def,
        Vector3 impactPoint,
        EnemyBaseController trackedTarget)
    {
        float rushSeconds = AbilityCombatPower.ExecutionersDescentTargetDiedRushSeconds;
        Vector3 endPos = abilityVfx != null
            ? abilityVfx.ResolveExecutionersDescentMinimumImpactWorldPosition(impactPoint)
            : impactPoint;
        Vector3 startPos = endPos + Vector3.up * AbilityCombatPower.ExecutionersDescentAxeSpawnHeight;
        if (abilityVfx != null && abilityVfx.TryGetExecutionersDescentAxeWorldPosition(out Vector3 axePos))
            startPos = axePos;

        abilityVfx?.HideExecutionersDescentMark();

        float elapsed = 0f;
        while (elapsed < rushSeconds)
        {
            if (player == null || stats == null || def == null)
                yield break;

            if (player.IsDead || stats.IsDead)
                yield break;

            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / rushSeconds));
            abilityVfx?.SetExecutionersDescentAxeWorldPosition(Vector3.Lerp(startPos, endPos, u));
            yield return null;
        }

        abilityVfx?.SetExecutionersDescentAxeWorldPosition(endPos);
        yield return FinishExecutionersDescentWithImpact(def, impactPoint, trackedTarget);
    }

    private IEnumerator FinishExecutionersDescentWithImpact(
        AbilityDefinition def,
        Vector3 impactPoint,
        EnemyBaseController trackedTarget)
    {
        if (player == null || stats == null || def == null)
            yield break;

        if (player.IsDead || stats.IsDead)
            yield break;

        ApplyExecutionersDescentImpact(def, impactPoint, trackedTarget);
    }

    private void ApplyExecutionersDescentContinuumPulse(AbilityDefinition def, Vector3 impactPoint)
    {
        abilityVfx?.SpawnExecutionersDescentImpactShockwave(impactPoint);
        ApplyExecutionersDescentShockwave(
            def,
            impactPoint,
            null,
            sunderingImpact: false,
            AbilityCombatPower.ExecutionersDescentContinuumShockwaveWeaponMultiplier);
    }

    private void ApplyExecutionersDescentImpact(
        AbilityDefinition def,
        Vector3 impactPoint,
        EnemyBaseController trackedTarget)
    {
        abilityVfx?.SpawnExecutionersDescentImpactShockwave(impactPoint);

        int selected = GetExecutionersDescentSelectedChoice();
        bool executionersClaim = selected == 0;
        bool sunderingImpact = selected == 1;

        bool primaryTargetAlive = trackedTarget != null && !trackedTarget.IsDead &&
                                  IsEnemyWithinExecutionersDescentCastRange(trackedTarget);
        if (primaryTargetAlive)
        {
            float armourMult = sunderingImpact ? 0f : 1f;
            float mrMult = sunderingImpact ? 0f : 1f;
            ApplyExecutionersDescentHit(
                trackedTarget,
                def,
                def.GetWeaponHitScalingMultiplier(),
                armourMult,
                mrMult);

            if (trackedTarget.IsDead)
                _executionersDescentTargetDiedDuringDescent = true;
        }

        ApplyExecutionersDescentShockwave(def, impactPoint, trackedTarget, sunderingImpact);

        if (executionersClaim && _executionersDescentTargetDiedDuringDescent)
            ReduceAbilityCooldown(def, AbilityCombatPower.ExecutionersDescentClaimCooldownReductionFraction);
    }

    private void ApplyExecutionersDescentEarlyDetonateNoTargetsInRange(
        AbilityDefinition def,
        Vector3 impactPoint,
        EnemyBaseController lastTrackedTarget)
    {
        if (player == null || stats == null || def == null)
            return;

        if (player.IsDead || stats.IsDead)
            return;

        GameLog.Add(
            "Executioner's Descent: no enemies in range — detonated early.",
            GameLog.CannotMessageColor);

        ApplyExecutionersDescentImpact(def, impactPoint, lastTrackedTarget);
        SetAbilityCooldownSeconds(def, AbilityCombatPower.ExecutionersDescentNoTargetInRangeCooldownSeconds);
    }

    private void ApplyExecutionersDescentHit(
        EnemyBaseController target,
        AbilityDefinition def,
        float weaponDamageMultiplier,
        float armourRatingMultiplier,
        float magicResistRatingMultiplier)
    {
        if (!target || target.IsDead || stats == null || def == null)
            return;

        BuildWhirlwindAbilityScaledSplit(def, target, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
        float scale = weaponDamageMultiplier / Mathf.Max(0.0001f, def.GetWeaponHitScalingMultiplier());
        rolledNonCrit *= scale;
        lightningMagNonCrit *= scale;

        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        SplitDamage rolled = new SplitDamage(
            rolledNonCrit.physical * critMult,
            rolledNonCrit.magic * critMult,
            rolledNonCrit.corruptionDamage * critMult);
        float lightningAfterCrit = lightningMagNonCrit * critMult;
        float frac = rolled.magic > 1e-8f ? Mathf.Clamp01(lightningAfterCrit / rolled.magic) : 0f;

        DealtHit dealt = ApplyAbilitySplitDamageToEnemy(
            target,
            def,
            rolled,
            wasCrit,
            frac,
            armourRatingMultiplier: armourRatingMultiplier,
            magicResistRatingMultiplier: magicResistRatingMultiplier);
        ApplyOnHitEffects(target, dealt);
        if (player != null && dealt.Total > 0f)
            player.ApplyLifeSteal(dealt.Total);
    }

    private void ApplyExecutionersDescentShockwave(
        AbilityDefinition def,
        Vector3 impactPoint,
        EnemyBaseController primaryTarget,
        bool sunderingImpact,
        float shockwaveWeaponMultiplier = -1f)
    {
        if (stats == null || def == null)
            return;

        if (shockwaveWeaponMultiplier < 0f)
            shockwaveWeaponMultiplier = AbilityCombatPower.ExecutionersDescentShockwaveWeaponMultiplier;

        float radius = AbilityCombatPower.ExecutionersDescentShockwaveRadius;
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            float dx = Mathf.Abs(enemy.transform.position.x - impactPoint.x);
            if (dx > radius)
                continue;

            ApplyExecutionersDescentHit(
                enemy,
                def,
                shockwaveWeaponMultiplier,
                armourRatingMultiplier: 1f,
                magicResistRatingMultiplier: 1f);

            if (sunderingImpact)
                ApplyExecutionersDescentSunderingDebuff(enemy);
        }
    }

    private static void ApplyExecutionersDescentSunderingDebuff(EnemyBaseController enemy)
    {
        if (!enemy)
            return;

        EnemyCombatMitigationModifiers mods = enemy.GetComponent<EnemyCombatMitigationModifiers>();
        if (!mods)
            mods = enemy.gameObject.AddComponent<EnemyCombatMitigationModifiers>();

        mods.ApplyArmourMrShred(
            AbilityCombatPower.ExecutionersDescentSunderingArmourMrMultiplier,
            AbilityCombatPower.ExecutionersDescentSunderingDebuffSeconds);
    }

    private void ExecuteShadowStrike(AbilityDefinition def, EnemyBaseController target)
    {
        if (!def || player == null || stats == null || target == null || target.IsDead)
            return;

        Vector3 departPosition = player.transform.position;
        TeleportPlayerBehindTarget(target, departPosition, AbilityCombatPower.ShadowStrikeLandBehindTargetDistance);
        if (combat != null && def.SetsTargetOnHit())
            combat.SetTargetIfNone(target);

        float enemyX = target.transform.position.x;
        player.FaceTargetX(enemyX);
        player.SyncSpriteFlipTrackingToPosition();

        player.TriggerAttackAnim();
        abilityVfx?.SpawnShadowStrikeDepartSmoke(departPosition);
        abilityVfx?.SpawnShadowStrikeBurst(target.transform.position);

        ApplyShadowStrikeMark(target, def);

        BuildWhirlwindAbilityScaledSplit(def, target, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        SplitDamage rolled = new SplitDamage(
            rolledNonCrit.physical * critMult,
            rolledNonCrit.magic * critMult,
            rolledNonCrit.corruptionDamage * critMult);
        float lightningAfterCrit = lightningMagNonCrit * critMult;
        float frac = rolled.magic > 1e-8f ? Mathf.Clamp01(lightningAfterCrit / rolled.magic) : 0f;

        DealtHit dealt = ApplyAbilitySplitDamageToEnemy(target, def, rolled, wasCrit, frac);
        ApplyOnHitEffects(target, dealt);
        if (player != null && dealt.Total > 0f)
            player.ApplyLifeSteal(dealt.Total);
    }

    private bool TryResolveShadowStrikeTarget(out EnemyBaseController target)
    {
        target = null;
        if (combat != null)
        {
            EnemyBaseController engaged = combat.GetPrimaryEngagedEnemy();
            if (engaged != null && IsEnemyInShadowStrikeForwardArc(engaged, out float engagedDist))
            {
                target = engaged;
                return true;
            }
        }

        List<(EnemyBaseController enemy, float dist)> forwardHits =
            CollectShadowStrikeForwardHits(AbilityCombatPower.ShadowStrikeForwardReach);
        if (forwardHits.Count == 0)
            return false;

        forwardHits.Sort((a, b) => a.dist.CompareTo(b.dist));
        target = forwardHits[0].enemy;
        return target != null;
    }

    private bool IsEnemyInShadowStrikeForwardArc(EnemyBaseController enemy, out float forwardDistance)
    {
        forwardDistance = float.MaxValue;
        if (!enemy || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            return false;

        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        float laneWidth = Mathf.Max(0.6f, AbilityCombatPower.ShadowStrikeForwardReach * 0.35f);
        Vector3 to = enemy.transform.position - origin;
        forwardDistance = to.x * facing;
        if (forwardDistance <= 0f || forwardDistance > AbilityCombatPower.ShadowStrikeForwardReach)
            return false;
        if (Mathf.Abs(to.y) > laneWidth)
            return false;
        return true;
    }

    private List<(EnemyBaseController enemy, float dist)> CollectShadowStrikeForwardHits(float reach)
    {
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        var forwardHits = new List<(EnemyBaseController enemy, float dist)>(allEnemies.Count);
        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        float laneWidth = Mathf.Max(0.6f, reach * 0.35f);

        for (int i = 0; i < allEnemies.Count; i++)
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

    private void TeleportPlayerBehindTarget(EnemyBaseController target, Vector3 approachFromPosition, float behindDistance)
    {
        if (!target || !player)
            return;

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        combat?.NotifyPlayerTeleported();

        float enemyX = target.transform.position.x;
        float approachSign = Mathf.Sign(enemyX - approachFromPosition.x);
        if (Mathf.Approximately(approachSign, 0f))
            approachSign = GetCombatFacingSign();
        if (Mathf.Approximately(approachSign, 0f))
            approachSign = 1f;

        float landX = enemyX + approachSign * Mathf.Max(0f, behindDistance);
        float laneRefX = player.transform.position.x;
        landX = player.ClampWorldXForLaneAt(laneRefX, landX);

        Vector3 pos = player.transform.position;
        pos.x = landX;
        player.transform.position = pos;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb)
            rb.position = pos;
    }

    private void TeleportPlayerToMeleeStrikePosition(EnemyBaseController target)
    {
        if (!target || !player || stats == null)
            return;

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        combat?.NotifyPlayerTeleported();

        float myRange = Mathf.Max(0f, stats.Range);
        if (combat != null)
            myRange += combat.GetMeleeRangePadding();

        Collider2D playerCol = player.GetComponent<Collider2D>();
        Collider2D enemyCol = target.GetComponent<Collider2D>();
        if (!enemyCol)
            enemyCol = target.GetComponentInChildren<Collider2D>();

        float myHalf = playerCol != null ? Mathf.Max(0f, playerCol.bounds.extents.x) : 0.25f;
        float enemyHalf = enemyCol != null ? Mathf.Max(0f, enemyCol.bounds.extents.x) : 0.25f;
        float desiredCenterDist = myRange + myHalf + enemyHalf;

        float enemyX = target.transform.position.x;
        float facing = player.FacingDirectionX;
        if (Mathf.Approximately(facing, 0f))
            facing = GetCombatFacingSign();
        if (Mathf.Approximately(facing, 0f))
            facing = 1f;

        // Land on the forward-arc side (same side you dashed from), then face the target.
        float laneRefX = player.transform.position.x;
        float desiredX = player.ClampWorldXForLaneAt(laneRefX, enemyX - facing * desiredCenterDist);

        Vector3 pos = player.transform.position;
        pos.x = desiredX;
        player.transform.position = pos;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb)
            rb.position = pos;
    }

    private void ApplyShadowStrikeMark(EnemyBaseController target, AbilityDefinition def)
    {
        if (!target || target.IsDead || def == null)
            return;

        int selected = GetShadowStrikeSelectedChoice();
        if (selected < 0)
            return;

        EnemyShadowStrikeMarks marks = target.GetComponent<EnemyShadowStrikeMarks>();
        if (!marks)
            marks = target.gameObject.AddComponent<EnemyShadowStrikeMarks>();

        EnemyShadowStrikeMarks.MarkKind kind = selected == 0
            ? EnemyShadowStrikeMarks.MarkKind.LethalCrit
            : EnemyShadowStrikeMarks.MarkKind.Execution;
        marks.ApplyMark(kind, this, def);
        UnitOverheadUI.RefreshShadowStrikeMarksForEnemy(target);
    }

    private int GetShadowStrikeSelectedChoice()
    {
        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.ShadowStrikeEnhancementParentSpineNodeId,
            -1);
    }

    public void ReduceAbilityCooldownBySeconds(AbilityDefinition def, float seconds)
    {
        if (!def || seconds <= 0f)
            return;

        ReduceStoredAbilityCooldownBySeconds(def.abilityId, seconds);
    }

    private EnemyBaseController ResolveExecutionersDescentTarget() =>
        ResolveExecutionersDescentTargetInCastRange();

    private static bool IsExecutionersDescentTargetAlive(EnemyBaseController enemy) =>
        enemy != null && !enemy.IsDead && enemy.gameObject.activeInHierarchy;

    private bool IsEnemyWithinExecutionersDescentCastRange(EnemyBaseController enemy)
    {
        if (!IsExecutionersDescentTargetAlive(enemy))
            return false;

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        if (combat != null && combat.IsEnemyWithinAttackRange(enemy))
            return true;

        float maxHorizontal = GetExecutionersDescentMaxCastHorizontalDistance();
        return Mathf.Abs(enemy.transform.position.x - transform.position.x) <= maxHorizontal;
    }

    private float GetExecutionersDescentMaxCastHorizontalDistance()
    {
        float meleeReach = stats != null ? Mathf.Max(0f, stats.Range) : 0f;
        return meleeReach + AbilityCombatPower.ExecutionersDescentCastRangeBeyondMelee;
    }

    private EnemyBaseController ResolveExecutionersDescentTargetInCastRange(EnemyBaseController preferredTarget = null)
    {
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        EnemyBaseController current = preferredTarget;
        if (current == null && combat != null)
            current = combat.CurrentTarget;

        if (IsEnemyWithinExecutionersDescentCastRange(current))
            return current;

        return FindClosestEnemyWithinExecutionersDescentCastRange(current);
    }

    private EnemyBaseController FindClosestEnemyWithinExecutionersDescentCastRange(
        EnemyBaseController preferredTarget = null)
    {
        if (preferredTarget != null && IsEnemyWithinExecutionersDescentCastRange(preferredTarget))
            return preferredTarget;

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDist = float.MaxValue;
        float ownerX = transform.position.x;
        float maxHorizontal = GetExecutionersDescentMaxCastHorizontalDistance();

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!IsExecutionersDescentTargetAlive(enemy))
                continue;

            float dist = Mathf.Abs(enemy.transform.position.x - ownerX);
            if (dist > maxHorizontal)
                continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    private void SetAbilityCooldownSeconds(AbilityDefinition def, float cooldownSeconds)
    {
        if (!def || cooldownSeconds <= 0f)
            return;

        TeardownLingeringAbilityStateBeforeCooldownWrite(def);

        float end = Time.time + cooldownSeconds;
        _cooldownEndsById[def.abilityId] = end;

        string rowKey = BuildAbilityRowKey(def);
        if (rowKey != null)
            _cooldownEndsByRowKey[rowKey] = end;
    }

    private EnemyBaseController FindClosestVisibleLivingEnemy()
    {
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDist = float.MaxValue;
        float ownerX = transform.position.x;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;
            if (!IsEnemyVisibleInGameplayCamera(enemy))
                continue;

            float dist = Mathf.Abs(enemy.transform.position.x - ownerX);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    private static bool IsEnemyVisibleInGameplayCamera(EnemyBaseController enemy)
    {
        if (!enemy)
            return false;

        Camera cam = Camera.main;
        if (!cam)
            return true;

        Collider2D col = enemy.GetComponent<Collider2D>();
        if (!col)
            col = enemy.GetComponentInChildren<Collider2D>();

        Bounds b = col != null ? col.bounds : new Bounds(enemy.transform.position, Vector3.one * 0.5f);
        Vector3 vp = cam.WorldToViewportPoint(b.center);
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }

    private int GetExecutionersDescentSelectedChoice()
    {
        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.ExecutionersDescentEnhancementParentSpineNodeId,
            -1);
    }

    private void ReduceAbilityCooldown(AbilityDefinition def, float reductionFraction)
    {
        if (!def || reductionFraction <= 0f)
            return;

        reductionFraction = Mathf.Clamp01(reductionFraction);
        if (!_cooldownEndsById.TryGetValue(def.abilityId, out float end))
            return;

        float remaining = end - Time.time;
        if (remaining <= 0f)
            return;

        float newEnd = Time.time + remaining * (1f - reductionFraction);
        _cooldownEndsById[def.abilityId] = newEnd;

        string rowKey = BuildAbilityRowKey(def);
        if (rowKey != null)
            _cooldownEndsByRowKey[rowKey] = newEnd;
    }

    private void TryUseCrescentSlash(AbilityDefinition def)
    {
        if (stats == null)
            return;

        int selected = GetCrescentSlashSelectedChoice();
        bool elementalCrescent = selected == 0;
        bool penetrating = selected == 1;

        float reach = GetCrescentSlashReach();
        abilityVfx?.SpawnCrescentSlash(reach, GetCombatFacingSign());

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
        float reach = GetCrescentSlashReach();
        List<(EnemyBaseController enemy, float dist)> forwardHits = CollectCrescentSlashForwardHits(reach);
        return forwardHits.Count > 0;
    }

    private bool CanHitAnyEnemyWithGuardiansHammer()
    {
        List<(EnemyBaseController enemy, float dist)> forwardHits =
            CollectGuardiansHammerTargets(AbilityCombatPower.GuardiansHammerForwardReach);
        return forwardHits.Count > 0;
    }

    /// <summary>True when at least one live enemy is within Whirlwind AoE (edge gap &lt;= hit radius).</summary>
    private bool CanHitAnyEnemyWithWhirlwind()
    {
        if (stats == null)
            return false;

        float radius = GetWhirlwindEffectiveRadius();
        float ownerX = transform.position.x;
        float ownerHalf = GetOwnerHalfWidthX();
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;
            if (IsEnemyWithinWhirlRange(enemy, radius, ownerX, ownerHalf, out _))
                return true;
        }

        return false;
    }

    private float GetWhirlwindEffectiveRadius(float channelSeconds = -1f)
    {
        int selectedChoice = GetWhirlwindSelectedChoice();
        if (selectedChoice != 1)
            return GetWhirlwindHitRadius();

        if (channelSeconds < 0f)
            channelSeconds = GetWhirlwindChannelElapsedSeconds();

        float expansiveStages = GetWhirlwindExpansiveStageCount(channelSeconds);
        return GetWhirlwindHitRadius() +
               expansiveStages * AbilityCombatPower.WhirlwindExpansiveRangePerStage;
    }

    private List<(EnemyBaseController enemy, float dist)> CollectCrescentSlashForwardHits(float reach)
    {
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        List<(EnemyBaseController enemy, float dist)> forwardHits = new List<(EnemyBaseController enemy, float dist)>(allEnemies.Count);
        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        float laneWidth = Mathf.Max(0.6f, reach * 0.35f);

        for (int i = 0; i < allEnemies.Count; i++)
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

    private List<(EnemyBaseController enemy, float dist)> CollectGuardiansHammerTargets(float reach)
    {
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        List<(EnemyBaseController enemy, float dist)> forwardHits = new List<(EnemyBaseController enemy, float dist)>(allEnemies.Count);
        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        float halfHeight = AbilityCombatPower.GuardiansHammerVerticalHalfHeight;
        float halfWidth = AbilityCombatPower.GuardiansHammerImpactWidth * 0.5f;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            Vector3 to = enemy.transform.position - origin;
            float forwardDist = to.x * facing;
            if (forwardDist <= 0f || forwardDist > reach)
                continue;
            if (Mathf.Abs(to.y) > halfHeight)
                continue;
            if (Mathf.Abs(forwardDist - reach * 0.5f) > reach * 0.5f + halfWidth)
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

        BuildWhirlwindAbilityScaledSplit(def, target, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        SplitDamage hitForTarget = new SplitDamage(
            rolledNonCrit.physical * critMult,
            rolledNonCrit.magic * critMult,
            rolledNonCrit.corruptionDamage * critMult);
        float convertedFireDamage = 0f;
        bool hasFireConversion = elementalCrescent &&
                                   TryApplyFireConversionForCrescent(ref hitForTarget, out convertedFireDamage);

        float crescentFrac = hitForTarget.magic > 1e-8f
            ? Mathf.Clamp01(lightningMagNonCrit * critMult / hitForTarget.magic)
            : 0f;

        DealtHit dealt = ApplyAbilitySplitDamageToEnemy(target, def, hitForTarget, wasCrit, crescentFrac);
        ApplyOnHitEffects(target, dealt);
        if (hasFireConversion)
            ApplyBurnForCrescent(target, convertedFireDamage);

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
    /// Convert all physical into magic as fire damage for this hit.
    /// </summary>
    private static bool TryApplyFireConversionForCrescent(ref SplitDamage hit, out float convertedFireDamage)
    {
        convertedFireDamage = 0f;

        float physical = Mathf.Max(0f, hit.physical);
        if (physical <= 0f)
            return false;

        convertedFireDamage = physical;
        hit.physical = 0f;
        hit.magic = Mathf.Max(0f, hit.magic) + convertedFireDamage;
        return true;
    }

    private void ApplyBurnForCrescent(EnemyBaseController target, float fireDamageDealt)
    {
        if (target == null || stats == null || fireDamageDealt <= 0f)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        ailments.TryApplyBurnFromFireHit(
            fireDamageDealt,
            1f,
            stats.BurnExplosionMultiplier,
            transform,
            burnTickIntervalSeconds: stats.BurnTickIntervalSeconds);
    }

    private void TryUseGuardiansHammer(AbilityDefinition def)
    {
        if (def == null || stats == null)
            return;

        float reach = AbilityCombatPower.GuardiansHammerForwardReach;
        float facing = GetCombatFacingSign();
        abilityVfx?.SpawnGuardiansHammerSlam(reach, facing);

        List<(EnemyBaseController enemy, float dist)> hits = CollectGuardiansHammerTargets(reach);
        bool grantProtectorResolve =
            GetGuardiansHammerSelectedChoice() == GuardiansHammerProtectorResolveChoiceIndex;
        int protectorResolveHitsGranted = 0;
        for (int i = 0; i < hits.Count; i++)
        {
            ApplyGuardiansHammerHit(hits[i].enemy, def);
            if (grantProtectorResolve &&
                protectorResolveHitsGranted < AbilityCombatPower.GuardiansHammerProtectorResolveMaxEnemyHits)
            {
                ApplyGuardiansHammerProtectorResolveGuardForHit();
                protectorResolveHitsGranted++;
            }
        }
    }

    private void ApplyGuardiansHammerHit(EnemyBaseController target, AbilityDefinition def)
    {
        if (target == null || target.IsDead || stats == null || def == null)
            return;

        BuildWhirlwindAbilityScaledSplit(def, target, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        SplitDamage hit = new SplitDamage(
            rolledNonCrit.physical * critMult,
            rolledNonCrit.magic * critMult,
            rolledNonCrit.corruptionDamage * critMult);
        float lightningFrac = hit.magic > 1e-8f ? Mathf.Clamp01(lightningMagNonCrit * critMult / hit.magic) : 0f;
        DealtHit dealt = ApplyAbilitySplitDamageToEnemy(target, def, hit, wasCrit, lightningFrac);
        ApplyOnHitEffects(target, dealt);
        if (player != null && dealt.Total > 0f)
            player.ApplyLifeSteal(dealt.Total);

        if (GetGuardiansHammerSelectedChoice() == GuardiansHammerBurningVerdictChoiceIndex)
            TryTriggerGuardiansHammerBurningVerdict(target);

        if (GetGuardiansHammerSelectedChoice() == GuardiansHammerProtectorResolveChoiceIndex)
            target.TryApplyStun(AbilityCombatPower.GuardiansHammerProtectorResolveStunDurationSeconds, 1f, transform);
    }

    private void TryTriggerGuardiansHammerBurningVerdict(EnemyBaseController sourceEnemy)
    {
        if (sourceEnemy == null || sourceEnemy.IsDead)
            return;

        AilmentController sourceAilments = sourceEnemy.GetComponent<AilmentController>();
        if (sourceAilments == null || !sourceAilments.HasBurn)
            return;

        if (!sourceAilments.TryBuildBurnFlarePayload(
                AbilityCombatPower.GuardiansHammerBurningVerdictTicksWorth,
                out int damage,
                out Transform source,
                out string dealerLabelForDps,
                out Vector3? dealerWorldPositionFallback,
                out _,
                out bool outgoingAttributeToMinion))
            return;

        abilityVfx?.SpawnGuardiansHammerBurnFlare(sourceEnemy.transform.position);

        float radius = AbilityCombatPower.GuardiansHammerBurningVerdictExplosionRadius;
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        bool recordedBurningVerdictUse = false;
        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            Vector3 delta = enemy.transform.position - sourceEnemy.transform.position;
            if (Mathf.Abs(delta.x) > radius || Mathf.Abs(delta.y) > radius)
                continue;

            AilmentController targetAilments = enemy.GetComponent<AilmentController>();
            if (targetAilments != null && targetAilments.ApplyExternalBurnDamage(
                damage,
                source,
                dealerLabelForDps,
                dealerWorldPositionFallback,
                GuardiansHammerBurningVerdictOutgoingSourceLabel,
                outgoingAttributeToMinion))
            {
                if (!recordedBurningVerdictUse)
                {
                    combat?.RecordOutgoingSourceUse(GuardiansHammerBurningVerdictOutgoingSourceLabel);
                    recordedBurningVerdictUse = true;
                }
            }
        }
    }

    private void ApplyGuardiansHammerProtectorResolveGuardForHit()
    {
        if (stats == null)
            return;

        float guardAmount =
            stats.MaxHP * AbilityCombatPower.GuardiansHammerProtectorResolveGuardPerHitFractionMaxHealth;
        if (guardAmount > 0.0001f)
        {
            stats.AddBonusGuard(guardAmount);
            _guardiansHammerProtectorResolveGuardExpiresAt =
                Time.time + AbilityCombatPower.GuardiansHammerProtectorResolveGuardDurationSeconds;
        }
    }

    private void TickGuardiansHammerProtectorResolveGuardExpiry()
    {
        if (_guardiansHammerProtectorResolveGuardExpiresAt <= 0f)
            return;

        if (Time.time < _guardiansHammerProtectorResolveGuardExpiresAt)
            return;

        _guardiansHammerProtectorResolveGuardExpiresAt = 0f;
        stats?.ClampGuardToNaturalCap();
    }

    private void FireGuardiansHammerImpact(AbilityDefinition def)
    {
        if (!def || player == null)
            return;

        TryUseGuardiansHammer(def);
        player.TriggerAttackAnim();
        StartCooldown(def);
    }

    private bool TryStartWhirlwindChannel(
        AbilityDefinition def,
        bool showInsufficientFeedback,
        bool autoBattleChannel)
    {
        if (def == null || player == null || stats == null)
            return false;

        _whirlwindChanneling = true;
        _whirlwindAutoChanneling = autoBattleChannel;
        _whirlwindSavedCombatTarget = autoBattleChannel && combat != null ? combat.CurrentTarget : null;
        _whirlwindChannelStartedAt = Time.time;
        _whirlwindLastTickAt = Time.time;
        _whirlwindNextTickAt = Time.time + GetWhirlwindChannelHitIntervalSeconds();
        _whirlwindNextGaleforceTwisterAt = GetWhirlwindSelectedChoice() == 0
            ? Time.time + AbilityCombatPower.WhirlwindGaleforceTwisterIntervalSeconds
            : 0f;
        _lastSyncedWhirlwindHudStacks = int.MinValue;
        if (autoBattleChannel)
        {
            _nextWhirlwindAutoBattlePresenceCheckAt = 0f;
            _nextWhirlwindAdvanceRecalcAt = 0f;
            RefreshWhirlwindScreenWorldBoundsIfDue();
            RefreshWhirlwindAutoBattlePresenceIfDue();
        }
        ApplyWhirlwindChannelMoveSpeedPenalty();

        float channelSeconds = GetWhirlwindChannelElapsedSeconds();
        float radius = GetWhirlwindEffectiveRadius(channelSeconds);
        player.BeginWhirlwindChannelAttackAnimLoop();
        abilityVfx?.BeginWhirlwindChannelVfx(radius);

        if (stats != null && stats.Energy <= 0.0001f)
        {
            if (showInsufficientFeedback)
                player.ShowPopup("Not enough energy.");
            ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: false);
            return false;
        }

        TryUseWhirlwind(def);
        return true;
    }

    private void TickWhirlwindChannel()
    {
        if (!_whirlwindChanneling)
            return;

        if (player == null || stats == null || player.IsDead || stats.IsDead)
        {
            ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: false);
            return;
        }

        AbilityDefinition def = GetAbilityDefinition(WhirlwindId);
        if (!def || !IsAbilityAllowedBySkillProgress(def) || !CanUseWithEquippedWeapon(def))
        {
            ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: false);
            return;
        }

        if (!_whirlwindAutoChanneling && !_whirlwindActionBarHeld)
        {
            ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: true);
            return;
        }

        if (_whirlwindAutoChanneling)
        {
            if (combat == null)
                combat = GetComponent<PlayerCombatController>();
            if (combat != null && !combat.IdleCombatEnabled)
            {
                ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: true);
                return;
            }
        }

        if (_whirlwindAutoChanneling)
        {
            RefreshWhirlwindAutoBattlePresenceIfDue();
            if (!_whirlwindAutoBattleAnyOnScreen)
            {
                ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: true);
                return;
            }

            if (!_whirlwindAutoBattleAnyWithinStopDistance)
            {
                ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: true);
                return;
            }
        }

        ApplyWhirlwindChannelMoveSpeedPenalty();
        if (!DrainWhirlwindChannelEnergy(def, showInsufficientFeedback: false))
        {
            ForceEndWhirlwindChannel(clearHeldState: false, applyCooldown: true);
            return;
        }

        float channelSeconds = GetWhirlwindChannelElapsedSeconds();
        float currentRadius = GetWhirlwindEffectiveRadius(channelSeconds);
        abilityVfx?.SetWhirlwindChannelRadius(currentRadius);

        if (_whirlwindChanneling && GetWhirlwindSelectedChoice() == 0 && Time.time >= _whirlwindNextGaleforceTwisterAt)
        {
            TrySpawnGaleforceTwister(def, currentRadius);
            _whirlwindNextGaleforceTwisterAt = Time.time + AbilityCombatPower.WhirlwindGaleforceTwisterIntervalSeconds;
        }

        if (_whirlwindChanneling && Time.time + 0.0001f >= _whirlwindNextTickAt)
        {
            TryUseWhirlwind(def);
            _whirlwindLastTickAt = Time.time;
            _whirlwindNextTickAt = _whirlwindLastTickAt + GetWhirlwindChannelHitIntervalSeconds();
        }

        if (_whirlwindAutoChanneling)
        {
            TickWhirlwindAutoBattleRetarget();
            TickWhirlwindAutoBattleAdvance();
        }
    }

    private void RefreshWhirlwindAutoBattlePresenceIfDue()
    {
        if (Time.time < _nextWhirlwindAutoBattlePresenceCheckAt)
            return;

        _nextWhirlwindAutoBattlePresenceCheckAt = Time.time + WhirlwindAutoBattlePresenceRecheckInterval;
        _whirlwindAutoBattleAnyOnScreen = false;
        _whirlwindAutoBattleAnyWithinStopDistance = false;

        RefreshWhirlwindScreenWorldBoundsIfDue();

        float stopDistance = AbilityCombatPower.WhirlwindAutoBattleStopIfNoEnemyWithinDistance;
        float ownerX = transform.position.x;
        float ownerHalf = GetOwnerHalfWidthX();
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            if (!_whirlwindAutoBattleAnyOnScreen && IsEnemyRoughlyOnScreen(enemy))
                _whirlwindAutoBattleAnyOnScreen = true;

            if (!_whirlwindAutoBattleAnyWithinStopDistance &&
                IsEnemyWithinWhirlRange(enemy, stopDistance, ownerX, ownerHalf, out _))
                _whirlwindAutoBattleAnyWithinStopDistance = true;

            if (_whirlwindAutoBattleAnyOnScreen && _whirlwindAutoBattleAnyWithinStopDistance)
                break;
        }
    }

    private void RefreshWhirlwindScreenWorldBoundsIfDue()
    {
        if (Time.time < _nextWhirlwindScreenBoundsRefreshAt && _whirlwindCachedCamera != null)
            return;

        _nextWhirlwindScreenBoundsRefreshAt = Time.time + 0.1f;
        _whirlwindCachedCamera = Camera.main;
        if (_whirlwindCachedCamera == null || !_whirlwindCachedCamera.orthographic)
        {
            _whirlwindScreenBoundsValid = false;
            return;
        }

        float halfH = _whirlwindCachedCamera.orthographicSize;
        float halfW = halfH * _whirlwindCachedCamera.aspect;
        Vector3 camPos = _whirlwindCachedCamera.transform.position;
        _whirlwindScreenWorldMinX = camPos.x - halfW - halfW * 0.05f;
        _whirlwindScreenWorldMaxX = camPos.x + halfW + halfW * 0.05f;
        _whirlwindScreenWorldMinY = camPos.y - halfH - halfH * 0.15f;
        _whirlwindScreenWorldMaxY = camPos.y + halfH + halfH * 0.15f;
        _whirlwindScreenBoundsValid = true;
    }

    private void TickWhirlwindAutoBattleRetarget()
    {
        if (!_whirlwindAutoChanneling || combat == null)
            return;

        EnemyBaseController current = combat.CurrentTarget;
        if (current != null && !current.IsDead)
            return;

        if (!TryFindClosestEnemyInWhirlwindRadius(out EnemyBaseController replacement))
            return;

        combat.SetTarget(replacement);
    }

    private void TickWhirlwindAutoBattleAdvance()
    {
        if (!_whirlwindAutoChanneling || player == null)
            return;

        if (Time.time >= _nextWhirlwindAdvanceRecalcAt)
        {
            _nextWhirlwindAdvanceRecalcAt = Time.time + 0.1f;
            RefreshWhirlwindScreenWorldBoundsIfDue();
            _hasCachedWhirlwindAdvanceX = TryFindWhirlwindAutoBattleAdvanceX(out _cachedWhirlwindAdvanceX);
        }

        if (!_hasCachedWhirlwindAdvanceX)
            return;

        player.MoveToPointX_Combat(_cachedWhirlwindAdvanceX);
    }

    private bool TryFindWhirlwindAutoBattleAdvanceX(out float moveToX)
    {
        moveToX = 0f;
        if (player == null)
            return false;

        float playerX = player.transform.position.x;
        float forwardSign = player.FacingDirectionX;
        if (Mathf.Approximately(forwardSign, 0f) && combat != null && combat.CurrentTarget != null && !combat.CurrentTarget.IsDead)
            forwardSign = Mathf.Sign(combat.CurrentTarget.transform.position.x - playerX);
        if (Mathf.Approximately(forwardSign, 0f))
            forwardSign = InferWhirlwindForwardSignFromEnemies(playerX);

        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController furthest = null;
        float bestForwardScore = float.NegativeInfinity;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;
            if (!IsEnemyRoughlyOnScreen(enemy))
                continue;

            float forwardScore = forwardSign * enemy.transform.position.x;
            if (forwardScore > bestForwardScore)
            {
                bestForwardScore = forwardScore;
                furthest = enemy;
            }
        }

        if (furthest == null)
            return false;

        moveToX = furthest.transform.position.x;
        return Mathf.Abs(moveToX - playerX) > 0.05f;
    }

    private float InferWhirlwindForwardSignFromEnemies(float playerX)
    {
        int rightCount = 0;
        int leftCount = 0;
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy || !IsEnemyRoughlyOnScreen(enemy))
                continue;

            if (enemy.transform.position.x >= playerX)
                rightCount++;
            else
                leftCount++;
        }

        if (rightCount == 0 && leftCount == 0)
            return 1f;

        return rightCount >= leftCount ? 1f : -1f;
    }

    private static bool IsEnemyRoughlyOnScreen(EnemyBaseController enemy)
    {
        if (enemy == null)
            return false;

        if (_instance != null && _instance._whirlwindScreenBoundsValid)
        {
            Vector3 pos = enemy.transform.position;
            return pos.x >= _instance._whirlwindScreenWorldMinX
                   && pos.x <= _instance._whirlwindScreenWorldMaxX
                   && pos.y >= _instance._whirlwindScreenWorldMinY
                   && pos.y <= _instance._whirlwindScreenWorldMaxY;
        }

        Camera cam = Camera.main;
        if (cam == null)
            return true;

        Vector3 viewport = cam.WorldToViewportPoint(enemy.transform.position);
        return viewport.z > 0f
               && viewport.x >= -0.05f
               && viewport.x <= 1.05f
               && viewport.y >= -0.15f
               && viewport.y <= 1.15f;
    }

    private void ApplyWhirlwindChannelMoveSpeedPenalty()
    {
        if (stats != null)
            stats.AbilityChannelMoveSpeedMultiplier = GetWhirlwindMoveSpeedMultiplier();
    }

    private bool HasEnoughEnergyForAutoBattleWhirlwind()
    {
        if (stats == null)
            return false;

        float maxEnergy = stats.MaxEnergy;
        if (maxEnergy <= 0f)
            return false;

        return stats.Energy > maxEnergy * AbilityCombatPower.WhirlwindAutoBattleMinEnergyFraction;
    }

    /// <summary>Drains whirlwind energy smoothly over time (per-second rate, FPS-independent via deltaTime).</summary>
    private bool DrainWhirlwindChannelEnergy(AbilityDefinition def, bool showInsufficientFeedback)
    {
        if (player == null || stats == null)
            return false;

        float perSecond = GetWhirlwindChannelEnergyPerSecond(def);
        if (perSecond <= 0f)
        {
            _whirlwindUsedEnergyInfusionMana = false;
            return true;
        }

        float deltaSeconds = Mathf.Max(0f, Time.deltaTime);
        if (deltaSeconds <= 0f)
            return true;

        float spendAmount = perSecond * deltaSeconds;
        if (spendAmount <= 0f)
            return true;

        return TrySpendWhirlwindChannelEnergyAmount(def, spendAmount, showInsufficientFeedback);
    }

    private bool TrySpendWhirlwindChannelEnergyAmount(
        AbilityDefinition def,
        float energyCost,
        bool showInsufficientFeedback)
    {
        if (player == null || stats == null)
            return false;

        if (energyCost <= 0f)
        {
            _whirlwindUsedEnergyInfusionMana = false;
            return true;
        }

        ResetLastAbilityResourceSpend();

        if (ShouldUseEnergyInfusionForAbility(def))
        {
            float manaFraction = GetEnergyInfusionManaCostFraction(def);
            float manaSpend = energyCost * manaFraction;
            float energySpend = energyCost - manaSpend;

            if (manaSpend > 0.0001f && stats.Mana + 0.0001f >= manaSpend)
            {
                if (stats.Energy + 0.0001f < energySpend)
                {
                    if (showInsufficientFeedback)
                        player.ShowPopup("Not enough energy.");
                    return false;
                }

                if (!player.SpendMana(manaSpend))
                    return false;

                if (energySpend > 0.0001f && !player.SpendEnergy(energySpend))
                {
                    player.AddMana(manaSpend);
                    return false;
                }

                RecordLastAbilityResourceSpend(
                    def,
                    0,
                    Mathf.RoundToInt(manaSpend),
                    Mathf.RoundToInt(energySpend),
                    true);
                _whirlwindUsedEnergyInfusionMana = true;
                return true;
            }
        }

        if (stats.Energy + 0.0001f < energyCost)
        {
            if (showInsufficientFeedback)
                player.ShowPopup("Not enough energy.");
            return false;
        }

        if (!player.SpendEnergy(energyCost))
            return false;

        RecordLastAbilityResourceSpend(def, 0, 0, Mathf.RoundToInt(energyCost), false);
        _whirlwindUsedEnergyInfusionMana = false;
        return true;
    }

    private float GetWhirlwindChannelHitIntervalSeconds()
    {
        return AbilityCombatPower.WhirlwindHitIntervalSeconds;
    }

    private float GetWhirlwindBaseChannelEnergyPerSecond(AbilityDefinition def)
    {
        if (def == null)
            return 0f;

        return AbilityCombatPower.GetWhirlwindBaseChannelEnergyPerSecond(
            Mathf.Max(0f, def.energyCost),
            GetWhirlwindSelectedChoice());
    }

    private float GetWhirlwindChannelEnergyPerSecond(AbilityDefinition def)
    {
        float baseCost = GetWhirlwindBaseChannelEnergyPerSecond(def);
        float scaled = Mathf.Max(0f, baseCost * GetBattleEngineOverloadEnergyCostMultiplier());
        return stats != null
            ? stats.ApplyEnergyEfficiencyToAbilityEnergyCost(def, scaled)
            : scaled;
    }

    private float GetWhirlwindChannelElapsedSeconds()
    {
        if (!_whirlwindChanneling)
            return 0f;

        return Mathf.Max(0f, Time.time - _whirlwindChannelStartedAt);
    }

    private float GetWhirlwindChannelDamageMultiplier(float channelSeconds)
    {
        if (GetWhirlwindSelectedChoice() != 1)
            return 1f;

        return 1f + GetWhirlwindExpansiveScalingSeconds(channelSeconds) * AbilityCombatPower.WhirlwindExpansiveDamagePerSecond;
    }

    private float GetWhirlwindMoveSpeedMultiplier()
    {
        float penalty = AbilityCombatPower.WhirlwindBaseMoveSpeedPenaltyFraction;
        return Mathf.Clamp01(1f - Mathf.Max(0f, penalty));
    }

    private static float GetWhirlwindExpansiveScalingSeconds(float channelSeconds)
    {
        return Mathf.Clamp(Mathf.Max(0f, channelSeconds), 0f, WhirlwindMaxChannelStacks - 1f);
    }

    private static int GetWhirlwindExpansiveStageCount(float channelSeconds)
    {
        return Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Max(0f, channelSeconds)), 1, WhirlwindMaxChannelStacks);
    }

    private void SyncWhirlwindHudBuff()
    {
        if (!buffController)
            return;

        if (!_whirlwindChanneling)
        {
            if (buffController.IsHudAbilityBuffActive(WhirlwindId))
                buffController.ClearHudAbilityBuff(WhirlwindId);
            _lastSyncedWhirlwindHudStacks = int.MinValue;
            return;
        }

        bool showScalingStacks = GetWhirlwindSelectedChoice() == 1;
        int stacks = showScalingStacks
            ? Mathf.Clamp(1 + Mathf.FloorToInt(GetWhirlwindChannelElapsedSeconds()), 1, WhirlwindMaxChannelStacks)
            : 1;
        if (_lastSyncedWhirlwindHudStacks == stacks)
            return;

        _lastSyncedWhirlwindHudStacks = stacks;
        buffController.SetHudAbilityBuff(WhirlwindId, stacks, 0f, 0f, persistActiveOverlay: true);
    }

    private void ForceEndWhirlwindChannel(
        bool clearHeldState,
        bool applyCooldown,
        bool lingerGaleforceTwisters = true)
    {
        AbilityDefinition def = applyCooldown ? GetAbilityDefinition(WhirlwindId) : null;
        EnemyBaseController resumeTarget = _whirlwindSavedCombatTarget;
        _whirlwindChanneling = false;
        _whirlwindAutoChanneling = false;
        _whirlwindSavedCombatTarget = null;
        _whirlwindChannelStartedAt = 0f;
        _whirlwindLastTickAt = 0f;
        _whirlwindNextTickAt = 0f;
        _whirlwindNextGaleforceTwisterAt = 0f;
        _whirlwindUsedEnergyInfusionMana = false;
        if (lingerGaleforceTwisters)
        {
            float lingerSeconds = abilityVfx != null
                ? abilityVfx.GaleforceTwisterLingerAfterChannelSeconds
                : AbilityCombatPower.WhirlwindGaleforceTwisterLingerAfterChannelSeconds;
            abilityVfx?.LingerGaleforceTwistersAfterChannelEnd(lingerSeconds);
        }
        else
        {
            abilityVfx?.EndAllGaleforceTwisterVfx();
        }

        abilityVfx?.EndWhirlwindChannelVfx();
        player?.EndWhirlwindChannelAttackAnimLoop();

        _whirlwindLastHitTimeByEnemyId.Clear();
        _whirlwindEnemiesInContactThisFrame.Clear();
        _whirlwindContactRemovalBuffer.Clear();
        _hasCachedWhirlwindAdvanceX = false;
        _nextWhirlwindAdvanceRecalcAt = 0f;
        if (stats != null)
            stats.AbilityChannelMoveSpeedMultiplier = 1f;
        player?.StopMoveOnly();
        if (clearHeldState)
            _whirlwindActionBarHeld = false;
        if (buffController != null && buffController.IsHudAbilityBuffActive(WhirlwindId))
            buffController.ClearHudAbilityBuff(WhirlwindId);
        if (applyCooldown && def != null)
            StartCooldown(def);

        if (resumeTarget != null && !resumeTarget.IsDead && combat != null)
            combat.SetTarget(resumeTarget);
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

    private DealtHit ApplySplitDamageToEnemy(
        EnemyBaseController target,
        SplitDamage hit,
        bool wasCrit,
        float meleeMagicLightningFraction = -1f,
        float armourRatingMultiplier = 1f,
        float magicResistRatingMultiplier = 1f,
        string outgoingDamageSourceLabel = null)
    {
        DealtHit result = default;
        if (target == null || target.IsDead)
            return result;

        if (stats != null)
            armourRatingMultiplier *= stats.GetTacticianOutgoingArmourRatingMultiplier();

        if (meleeMagicLightningFraction < 0f)
            meleeMagicLightningFraction = stats != null ? stats.GetMeleeMagicLightningFraction() : 0f;
        result.meleeMagicLightningFraction = Mathf.Clamp01(meleeMagicLightningFraction);

        float cond = GetConditionalMeleeDamageMultiplier(target);
        if (wasCrit && stats != null)
        {
            cond *= stats.GetPredatorsInstinctExecutionerCritDamageFactor(target, true);
            cond *= stats.GetOpportunisticCritDamageFactor(target, true);
            stats.OnPlayerCritLanded();
        }

        float phys = Mathf.Max(0f, hit.physical * cond);
        float mag = Mathf.Max(0f, hit.magic * cond);
        float corrRaw = Mathf.Max(0f, hit.corruptionDamage * cond);
        string sourceLabel = outgoingDamageSourceLabel;

        if (phys > 0f)
        {
            result.physical = Mathf.Max(0f, target.TakeDamage(
                Mathf.RoundToInt(phys),
                DamageType.Physical,
                wasCrit,
                transform,
                stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null,
                dpsBucketOverride: null,
                armourRatingMultiplier,
                magicResistRatingMultiplier,
                outgoingDpsSourceLabel: sourceLabel));
        }

        if (mag > 0f)
        {
            result.magic = Mathf.Max(0f, target.TakeDamage(
                Mathf.RoundToInt(mag),
                DamageType.Magic,
                wasCrit,
                transform,
                stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null,
                dpsBucketOverride: null,
                armourRatingMultiplier,
                magicResistRatingMultiplier,
                outgoingDpsSourceLabel: sourceLabel));

        }

        if (corrRaw > 0f)
        {
            result.corruptionDamage = Mathf.Max(0f, target.TakeDamage(
                Mathf.RoundToInt(corrRaw),
                DamageType.Corruption,
                wasCrit,
                transform,
                stats != null ? stats.CurrentAttackSkill : (AttackSkill?)null,
                outgoingDpsSourceLabel: sourceLabel));
        }

        if (stats != null && result.physical > 0f)
            stats.TryApplyTacticianStunOnEnemyHit(target);

        if (stats != null && result.Total > 0f && stats.CurrentAttackSkill == AttackSkill.Melee)
        {
            if (combat == null)
                combat = GetComponent<PlayerCombatController>();
            combat?.TryApplySecondarySpecialistDualWieldFollowUp(target, hit, wasCrit);
        }

        TryNotifyLightningRodSurgeFromDealt(target, result.physical, result.magic, sourceLabel);

        return result;
    }

    private void TryNotifyLightningRodSurgeFromDealt(
        EnemyBaseController target,
        float physicalDealt,
        float magicDealt,
        string outgoingDamageSourceLabel)
    {
        if (target == null || magicDealt <= 0f)
            return;

        if (string.Equals(
                outgoingDamageSourceLabel,
                AbilityCombatPower.LightningRodOutgoingDamageSourceLabel,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        float lightningDealt = EstimateLightningDamagePortion(physicalDealt, magicDealt);
        if (lightningDealt > 0f)
            TryLightningRodSurgeOnLightningHit(target, lightningDealt);
    }

    private DealtHit ApplyAbilitySplitDamageToEnemy(
        EnemyBaseController target,
        AbilityDefinition def,
        SplitDamage hit,
        bool wasCrit,
        float meleeMagicLightningFraction = -1f,
        float armourRatingMultiplier = 1f,
        float magicResistRatingMultiplier = 1f,
        string outgoingDamageSourceLabelOverride = null)
    {
        float overloadMult = GetBattleEngineOverloadDamageMultiplier();
        if (overloadMult > 1f)
        {
            hit = new SplitDamage(
                hit.physical * overloadMult,
                hit.magic * overloadMult,
                hit.corruptionDamage * overloadMult);
        }

        ApplyActiveDamageConversions(ref hit);

        DealtHit dealt = ApplySplitDamageToEnemy(
            target,
            hit,
            wasCrit,
            meleeMagicLightningFraction,
            armourRatingMultiplier,
            magicResistRatingMultiplier,
            outgoingDamageSourceLabelOverride
            ?? (def != null ? GetAbilityOutgoingDamageSourceLabel(def.abilityId) : null));

        TryGrantBattleEngineEnergyOnAbilityHit(def, dealt.Total > 0f);

        if (dealt.Total > 0f && stats != null)
            HuntersMarkCombat.TryApplyFromPlayerHit(target, stats, dealt.Total);

        return dealt;
    }

    private float GetConditionalMeleeDamageMultiplier(EnemyBaseController target)
    {
        if (stats == null || target == null)
            return 1f;

        if (stats.CurrentAttackSkill == AttackSkill.Ranged)
            return 1f + stats.GetRangedConditionalDamageBonusFraction(target, combat);

        float bonus = 0f;
        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments != null)
        {
            if (ailments.HasBleed) bonus += stats.MeleeDamageVsBleeding;
            if (ailments.HasPoison) bonus += stats.MeleeDamageVsPoisoned;
            if (ailments.HasShock) bonus += stats.MeleeDamageVsShocked;
            if (ailments.HasBurn) bonus += stats.MeleeDamageVsBurning;
            if (ailments.HasBleed || ailments.HasPoison || ailments.HasBurn)
                bonus += stats.MeleeDamageVsAilmented;
        }

        CharacterStats targetStats = target.GetComponent<CharacterStats>();
        if (targetStats != null && targetStats.MaxHP > 0f)
        {
            float hp01 = targetStats.HP / Mathf.Max(1f, targetStats.MaxHP);
            if (hp01 < stats.MeleeLowHpThreshold01)
                bonus += stats.MeleeDamageVsLowHp;
        }

        bonus += stats.GetOpportunisticAbilityDamageBonusFraction(target);

        return 1f + Mathf.Max(0f, bonus);
    }

    private bool TryRollIndependentCrit(ref SplitDamage hit)
    {
        if (stats == null)
            return false;
        if (!hit.CanCrit)
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
        if (target == null || stats == null || !stats.CanApplyOutgoingAilmentsOnHit())
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        float bleedProcChance = stats.GetEffectiveBleedChanceForProcs();
        if (dealt.physical > 0f && bleedProcChance > 0f && UnityEngine.Random.value <= bleedProcChance)
        {
            float duration = Mathf.Max(1f, stats.BleedDuration);
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
            float baseDuration = Mathf.Max(1f, stats.BleedBaseDuration);
            float bleedTickDamage = dealt.physical * (1f + stats.BleedMultiplier) / baseDuration;
            if (bleedTickDamage > 0f)
            {
                float totalBleedDamage = bleedTickDamage * ticks;
                ailments.ApplyBleedFromHit(new BleedPayload(
                    totalBleedDamage,
                    duration,
                    ticks,
                    transform,
                    maxStacks: stats.BleedMaxStacks));
            }
        }

        if (dealt.corruptionDamage > 0f && stats.PoisonChance > 0f && stats.PoisonMultiplier >= 0f && UnityEngine.Random.value <= stats.PoisonChance)
        {
            float totalPoisonDamage =
                dealt.corruptionDamage * stats.PoisonPoolFractionOfCorruptionDamage *
                (1f + stats.GetPoisonMultiplierAgainst(target != null ? target.Stats : null));
            if (totalPoisonDamage > 0f)
            {
                float duration = Mathf.Max(0.1f, stats.PoisonDuration);
                int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
                int maxStacks = Mathf.Max(1, stats.PoisonMaxStacks);
                ailments.ApplyPoisonFromHit(new PoisonPayload(
                    totalPoisonDamage, duration, ticks, maxStacks, transform, poisonMasteryOwner: transform));
            }
        }

        stats.TryApplyBurnFromDealtHit(ailments, dealt.magic, dealt.physical, transform);

        if (dealt.magic > 0f &&
            dealt.meleeMagicLightningFraction > 1e-5f)
        {
            float shockChance = stats.CurrentAttackSkill == AttackSkill.Ranged
                ? stats.RangedShockChance
                : stats.MeleeShockChance;
            if (shockChance > 0f && UnityEngine.Random.value <= shockChance)
            {
                ailments.ApplyShockFromHit(new ShockPayload(
                    duration: stats.ShockDuration,
                    damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
                    source: transform));
            }
        }
    }

    public bool TryConsumeQueuedAttackModifier(ref SplitDamage rolled)
    {
        if (rolled.IsEmpty)
            return false;

        if (_powerSlashQueued)
        {
            AbilityDefinition def = GetAbilityDefinition(PowerSlashId);

            _powerSlashQueued = false;
            _queuedConsumedThisHit = QueuedHitEffect.PowerSlash;
            _queuedConsumedFrame = Time.frameCount;

            float apM = stats != null
                ? GetAbilityPowerDamageMultiplierForAbility(def)
                : 1f;
            float allM = _queuedPowerSlashAllDamageMultiplier;
            AbilityDefinition slashDef = GetAbilityDefinition(PowerSlashId);
            float elementBonus = slashDef != null && stats != null ? AbilityElementScaling.GetElementDamageBonus(slashDef, stats) : 0f;
            float ailmentBonus = slashDef != null && stats != null ? AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(slashDef, stats) : 0f;
            float overloadMult = GetBattleEngineOverloadDamageMultiplier();

            // Mult scales the rolled basic hit (150% = 1.5× that swing), not an extra additive copy of it.
            rolled.physical = ((rolled.physical * _queuedPowerSlashWeaponMultiplier + ailmentBonus) * apM * allM) * overloadMult;
            rolled.magic = ((rolled.magic * _queuedPowerSlashWeaponMultiplier + elementBonus) * apM * allM) * overloadMult;
            rolled.corruptionDamage = ((rolled.corruptionDamage * _queuedPowerSlashWeaponMultiplier) * apM * allM) * overloadMult;
            rolled.physical = Mathf.Max(0f, rolled.physical);
            rolled.magic = Mathf.Max(0f, rolled.magic);
            _queuedPowerSlashUsedEnergyInfusionMana = false;

            if (def)
                StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;

            abilityVfx?.SpawnPowerSlashTrail();
            return true;
        }

        if (_tripleShotQueued)
        {
            if (stats == null || stats.CurrentAttackSkill != AttackSkill.Ranged)
                return false;

            AbilityDefinition def = GetAbilityDefinition(TripleShotId);
            if (def == null)
            {
                _tripleShotQueued = false;
                return false;
            }

            _tripleShotQueued = false;
            _queuedConsumedThisHit = QueuedHitEffect.TripleShot;
            _queuedConsumedFrame = Time.frameCount;

            float tripleShotAnyTypeBonus = GetTripleShotAnyTypeMultiplierBonus();
            float weaponCombo = def.weaponDamageMultiplier + tripleShotAnyTypeBonus;
            _queuedTripleShotWeaponMultiplier = weaponCombo <= 0f ? 1f : weaponCombo;
            _queuedTripleShotAllDamageMultiplier = def.GetEffectiveAllDamageMultiplier();

            float apM = GetAbilityPowerDamageMultiplierForAbility(def);
            float allM = _queuedTripleShotAllDamageMultiplier;
            float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
            float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
            float overloadMult = GetBattleEngineOverloadDamageMultiplier();

            rolled.physical = ((rolled.physical * _queuedTripleShotWeaponMultiplier + ailmentBonus) * apM * allM) * overloadMult;
            rolled.magic = ((rolled.magic * _queuedTripleShotWeaponMultiplier + elementBonus) * apM * allM) * overloadMult;
            rolled.corruptionDamage = ((rolled.corruptionDamage * _queuedTripleShotWeaponMultiplier) * apM * allM) * overloadMult;
            rolled.physical = Mathf.Max(0f, rolled.physical);
            rolled.magic = Mathf.Max(0f, rolled.magic);
            rolled.corruptionDamage = Mathf.Max(0f, rolled.corruptionDamage);
            _queuedTripleShotUsedEnergyInfusionMana = false;

            if (combat == null)
                combat = GetComponent<PlayerCombatController>();
            EnemyBaseController volleyTarget = combat != null ? combat.CurrentTarget : null;
            BeginTripleShotPhantomVolley(volleyTarget);

            StartCooldown(def);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }

        if (_rendQueued)
        {
            AbilityDefinition def = GetAbilityDefinition(RendId);
            if (def != null && !TrySpendAbilityResourceCost(def, showInsufficientFeedback: false))
                return false;

            _rendQueued = false;
            _queuedConsumedThisHit = QueuedHitEffect.Rend;
            _queuedConsumedFrame = Time.frameCount;
            if (def)
                StartCooldown(def);
            return true;
        }

        if (_envenomQueued)
        {
            AbilityDefinition def = GetAbilityDefinition(EnvenomId);
            if (def != null && !TrySpendAbilityResourceCost(def, showInsufficientFeedback: false))
                return false;

            _envenomQueued = false;
            _queuedConsumedThisHit = QueuedHitEffect.Envenom;
            _queuedConsumedFrame = Time.frameCount;
            if (def)
                StartCooldown(def);
            return true;
        }

        if (_crusaderStrikeQueued)
        {
            AbilityDefinition def = GetAbilityDefinition(CrusaderStrikeId);
            int castStep = Mathf.Clamp(_crusaderStrikePrimedStage, 1, CrusaderStrikeFinalComboStep);
            float weaponMultiplier = GetCrusaderStrikeWeaponMultiplier(castStep);
            bool finalStrike = castStep >= CrusaderStrikeFinalComboStep;

            _crusaderStrikeQueued = false;
            _crusaderStrikePrimedStage = 0;
            _queuedCrusaderStrikeConsumedStage = castStep;
            _queuedConsumedThisHit = QueuedHitEffect.CrusaderStrike;
            _queuedConsumedFrame = Time.frameCount;

            rolled = BuildCrusaderStrikeSplit(rolled, weaponMultiplier, finalStrike, def);
            _crusaderStrikeAttributionBucketPending = finalStrike || (rolled.physical <= 0f && rolled.magic > 0f)
                ? DpsDamageBucket.Magic
                : DpsDamageBucket.Physical;
            return true;
        }

        return false;
    }

    private void TryAutoReleaseQueuedCrescentSlash()
    {
        TryReleaseQueuedCrescentSlash(requireTargetInFacingLane: true);
    }

    /// <summary>
    /// Called from combat cadence; guarantees queued Crescent Slash gets priority over normal auto attacks.
    /// </summary>
    public bool TryAutoReleaseQueuedCrescentSlashFromCadence()
    {
        return TryReleaseQueuedCrescentSlash(requireTargetInFacingLane: true);
    }

    private bool TryReleaseQueuedCrescentSlash(bool requireTargetInFacingLane)
    {
        if (!_crescentSlashQueued)
            return false;
        if (requireTargetInFacingLane && !CanHitAnyEnemyWithCrescentSlash())
            return false;

        AbilityDefinition def = GetAbilityDefinition(CrescentSlashId);
        if (!def)
        {
            ClearCrescentSlashPrimeState();
            return false;
        }

        // Legacy queues from before pay-on-prime: spend now or drop the stale prime instead of eating attack cycles.
        if (!_crescentSlashEnergyCommitted)
        {
            if (!TrySpendAbilityResourceCost(def, showInsufficientFeedback: false))
            {
                ClearCrescentSlashPrimeState();
                return false;
            }

            _crescentSlashEnergyCommitted = true;
            _crescentSlashUsedEnergyInfusionMana = DidLastAbilitySpendUseEnergyInfusionMana(def);
        }

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null || !combat.TryConsumeAttackCycleForAbilityCast())
            return false;

        FireCrescentSlashImpact(def);
        return true;
    }

    private void FireCrescentSlashImpact(AbilityDefinition def)
    {
        if (!def || player == null)
            return;

        ClearCrescentSlashPrimeState();
        TryUseCrescentSlash(def);
        player.TriggerAttackAnim();
        StartCooldown(def);
    }

    private void ClearCrescentSlashPrimeState()
    {
        _crescentSlashQueued = false;
        _crescentSlashEnergyCommitted = false;
        _crescentSlashUsedEnergyInfusionMana = false;
    }

    private float GetCombatFacingSign() =>
        abilityVfx != null ? abilityVfx.GetCombatFacingSign() : (player != null ? Mathf.Sign(player.transform.localScale.x >= 0f ? 1f : -1f) : 1f);

    /// <summary>
    /// Called by <see cref="PlayerCombatController"/> after a hit lands, to apply queued on-hit logic that needs the target.
    /// Returns suppression flags for the default bleed/poison application.
    /// </summary>
    public QueuedHitEffectResult ConsumeQueuedHitEffects(
        EnemyBaseController target,
        float physicalDealt,
        float corruptionDealtPostMitigation,
        float magicDealtPostMitigation)
    {
        QueuedHitEffectResult result = default;
        if (target == null || target.IsDead)
            return result;

        // Melee resolves damage on the same frame as TryConsumeQueuedAttackModifier; ranged/projectile hits
        // often land later, so queued follow-up effects that need the landed hit must not require the same frame.
        bool requiresSameFrameAsConsume =
            _queuedConsumedThisHit != QueuedHitEffect.Rend &&
            _queuedConsumedThisHit != QueuedHitEffect.Envenom &&
            _queuedConsumedThisHit != QueuedHitEffect.CrusaderStrike;
        if (requiresSameFrameAsConsume && _queuedConsumedFrame != Time.frameCount)
            return result;

        if (_queuedConsumedThisHit == QueuedHitEffect.Rend)
        {
            result.suppressDefaultBleed = true;
            TryApplyRendBleed(target, physicalDealt);
            TryGrantBattleEngineEnergyOnAbilityHit(GetAbilityDefinition(RendId), physicalDealt > 0f);

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
            TryGrantBattleEngineEnergyOnAbilityHit(
                GetAbilityDefinition(EnvenomId),
                corruptionDealtPostMitigation > 0f);

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
        else if (_queuedConsumedThisHit == QueuedHitEffect.CrusaderStrike)
        {
            int consumedStage = _queuedCrusaderStrikeConsumedStage;
            if ((physicalDealt > 0f || magicDealtPostMitigation > 0f) && abilityVfx != null)
                abilityVfx.SpawnCrusaderStrikeBeam(target.transform.position, consumedStage >= CrusaderStrikeFinalComboStep);

            if (consumedStage == 1 || consumedStage == 2)
            {
                _crusaderStrikeComboStep = consumedStage;
                RefreshCrusaderStrikeComboTimeout();
                _lastSyncedCrusaderStrikeHudStacks = int.MinValue;
                SyncCrusaderStrikeHudBuff();

                if (consumedStage == 2 && (physicalDealt > 0f || magicDealtPostMitigation > 0f) && player != null && stats != null)
                    player.Heal(stats.MaxHP * GetCrusaderStrikeHealFraction(), PlayerCombatController.CrusaderStrikeHealingSourceLabel);

                TryQueueNextCrusaderStrikeHit(consumedStage);
            }
            else if (consumedStage >= CrusaderStrikeFinalComboStep)
            {
                result.suppressDefaultElementalMagicAilment = true;
                TryApplyCrusaderStrikeFinalBurn(target, magicDealtPostMitigation);
                _crusaderStrikeComboStep = 0;
                _lastSyncedCrusaderStrikeHudStacks = int.MinValue;
                SyncCrusaderStrikeHudBuff();
                if (GetCrusaderStrikeSelectedChoice() == CrusaderStrikeFireBalanceChoiceIndex)
                    ActivateCrusaderStrikeFireBalanceBuff();
                AbilityDefinition def = GetAbilityDefinition(CrusaderStrikeId);
                if (def)
                    StartCooldown(def);
            }

            _queuedCrusaderStrikeConsumedStage = 0;
        }

        _queuedConsumedThisHit = QueuedHitEffect.None;
        _queuedConsumedFrame = -1;
        return result;
    }

    private void TryApplyCrusaderStrikeFinalBurn(EnemyBaseController target, float fireDamageDealt)
    {
        if (target == null || stats == null || fireDamageDealt <= 0f)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        ailments.TryApplyBurnFromFireHit(
            fireDamageDealt,
            stats.BurnApplyChance,
            stats.BurnExplosionMultiplier,
            transform,
            burnTickIntervalSeconds: stats.BurnTickIntervalSeconds);
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
            (1f + stats.GetPoisonMultiplierAgainst(target.Stats));
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

        int effectiveMaxStacks = ailments.GetEffectivePoisonMaxStacks(baseMaxStacks);
        stacksToApply = effectiveMaxStacks;
        var payload = new PoisonPayload(
            perStackTotal,
            duration,
            ticks,
            effectiveMaxStacks,
            transform,
            poisonMasteryOwner: transform,
            splitTotalDamageAcrossStacks: true,
            replaceExistingPoisonStacks: true);
        ailments.ApplyPoisonStacksFromHit(payload, stacksToApply);

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
        float strikesCodeBaseSeconds = AbilityCombatPower.CleavingStrikesBaseDurationSeconds;
        int selected = GetCleavingStrikesSelectedChoice();
        _cleavingBuffActive = true;
        float fullChoiceDurationSeconds;
        if (selected == 0)
        {
            _cleavingAdditionalTargets = AbilityCombatPower.CleavingStrikesBaseExtraTargets
                                         + AbilityCombatPower.CleavingStrikesGreaterCleaveBonusTargets;
            _cleavingHitsRemaining = AbilityCombatPower.CleavingStrikesBaseEmpoweredHits;
            fullChoiceDurationSeconds = AbilityCombatPower.CleavingStrikesBaseDurationSeconds;
        }
        else if (selected == 1)
        {
            _cleavingAdditionalTargets = AbilityCombatPower.CleavingStrikesBaseExtraTargets;
            _cleavingHitsRemaining = AbilityCombatPower.CleavingStrikesLastingMomentumEmpoweredHits;
            fullChoiceDurationSeconds = AbilityCombatPower.CleavingStrikesLastingMomentumDurationSeconds;
        }
        else
        {
            _cleavingAdditionalTargets = AbilityCombatPower.CleavingStrikesBaseExtraTargets;
            _cleavingHitsRemaining = AbilityCombatPower.CleavingStrikesBaseEmpoweredHits;
            fullChoiceDurationSeconds = AbilityCombatPower.CleavingStrikesBaseDurationSeconds;
        }

        float baseDur = strikesCodeBaseSeconds;
        AbilityDefinition strikesDef = GetAbilityDefinition(CleavingStrikesId);
        if (strikesDef != null && strikesDef.tooltipBuffMinionDurationSeconds > 0.01f)
            baseDur = strikesDef.tooltipBuffMinionDurationSeconds;
        float additiveBonusSeconds = Mathf.Max(0f, fullChoiceDurationSeconds - strikesCodeBaseSeconds);
        _cleavingBuffDuration = baseDur + additiveBonusSeconds;
        _cleavingBuffEndsAt = Time.time + _cleavingBuffDuration;

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
            if (buffController.IsHudAbilityBuffActive(CleavingStrikesId))
                buffController.ClearHudAbilityBuff(CleavingStrikesId);
            _lastSyncedCleavingHudStacks = int.MinValue;
            _lastSyncedCleavingHudEnd = float.NaN;
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

    public bool IsStaticArrowsActive => _staticArrowsBuffActive;

    /// <summary>
    /// Scales the current auto attack while Static Arrows charges remain and converts physical to lightning.
    /// </summary>
    public void ApplyStaticArrowsAutoAttackScaling(ref SplitDamage rolled)
    {
        _staticArrowsAppliedThisHit = false;
        CleanupStaticArrowsIfExpired();
        if (!_staticArrowsBuffActive || rolled.IsEmpty)
            return;

        if (stats == null || stats.CurrentAttackSkill != AttackSkill.Ranged)
            return;

        AbilityDefinition def = GetAbilityDefinition(StaticArrowsId);
        if (def == null)
            return;

        float weaponMult = GetStaticArrowsWeaponDamageMultiplier(def);
        float apM = GetAbilityPowerDamageMultiplierForAbility(def);
        float allM = def.GetEffectiveAllDamageMultiplier();
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float overloadMult = GetBattleEngineOverloadDamageMultiplier();

        rolled.physical = ((rolled.physical * weaponMult + ailmentBonus) * apM * allM) * overloadMult;
        rolled.magic = ((rolled.magic * weaponMult + elementBonus) * apM * allM) * overloadMult;
        rolled.corruptionDamage = ((rolled.corruptionDamage * weaponMult) * apM * allM) * overloadMult;
        rolled.physical = Mathf.Max(0f, rolled.physical);
        rolled.magic = Mathf.Max(0f, rolled.magic);
        rolled.corruptionDamage = Mathf.Max(0f, rolled.corruptionDamage);

        ApplyStaticArrowsPhysicalToLightningConversion(ref rolled);
        _staticArrowsAppliedThisHit = true;
    }

    private void ApplyStaticArrowsPhysicalToLightningConversion(ref SplitDamage hit)
    {
        float frac = GetStaticArrowsPhysicalToLightningConversionFraction();
        if (frac <= 0f)
            return;

        float converted = Mathf.Max(0f, hit.physical) * frac;
        if (converted <= 0f)
            return;

        hit = new SplitDamage(
            Mathf.Max(0f, hit.physical - converted),
            Mathf.Max(0f, hit.magic + converted),
            Mathf.Max(0f, hit.corruptionDamage));
    }

    /// <summary>Called after a successful primary auto attack lands while Static Arrows is active.</summary>
    public bool TryConsumeStaticArrowsHitOnSuccessfulAttack()
    {
        CleanupStaticArrowsIfExpired();
        if (!_staticArrowsBuffActive || _staticArrowsHitsRemaining <= 0)
            return false;

        _staticArrowsHitsRemaining--;
        CleanupStaticArrowsIfExpired();
        SyncStaticArrowsHudBuff();
        return true;
    }

    public void ClearStaticArrowsPendingSwingFlag() => _staticArrowsAppliedThisHit = false;

    /// <summary>
    /// Enhancement: chance to arc lightning to a nearby enemy for a fraction of the hit damage dealt.
    /// </summary>
    public void TryStaticArrowsCritLightningArc(
        EnemyBaseController primaryTarget,
        float physicalDealt,
        float magicDealt,
        float corruptionDealt,
        bool wasCrit)
    {
        float totalDealt = Mathf.Max(0f, physicalDealt) + Mathf.Max(0f, magicDealt) + Mathf.Max(0f, corruptionDealt);
        if (primaryTarget == null || primaryTarget.IsDead || totalDealt <= 0f)
            return;
        if (!_staticArrowsAppliedThisHit)
            return;
        if (GetStaticArrowsSelectedChoice() != AbilityCombatPower.StaticArrowsChainLightningChoiceIndex)
            return;
        if (UnityEngine.Random.value >= AbilityCombatPower.StaticArrowsStaticArcChance)
            return;

        EnemyBaseController chainTarget = FindStaticArrowsStaticArcTarget(primaryTarget);
        Vector3 from = GetEnemyVfxCenter(primaryTarget);
        Vector3 to;
        Transform sortingReference = primaryTarget.transform;

        if (chainTarget != null)
        {
            to = GetEnemyVfxCenter(chainTarget);
        }
        else if (TornadoLightningRouter.TryFindTornadoNear(
                     from,
                     AbilityCombatPower.StaticArrowsStaticArcRange,
                     out TornadoInstance tornado,
                     out Vector3 tornadoAnchor))
        {
            sortingReference = tornado.transform;
            to = tornadoAnchor;
            chainTarget = null;
        }
        else
        {
            return;
        }

        float lightningDealt = EstimateLightningDamagePortion(physicalDealt, magicDealt);
        if (lightningDealt <= 0f)
            return;

        SplitDamage arcSplit = new SplitDamage(0f, lightningDealt, 0f);

        float arcLightning = arcSplit.magic;
        chainTarget = TornadoLightningRouter.RouteLightningArc(
            abilityVfx,
            from,
            to,
            arcLightning,
            sortingReference,
            chainTarget);

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null || chainTarget == null || chainTarget.IsDead)
            return;

        string label = AbilityCombatPower.StaticArrowsStaticArcOutgoingDamageSourceLabel;
        combat.ApplyStaticArrowsCritArcDamage(chainTarget, arcSplit, wasCrit: false, label);
    }

    private static Vector3 GetEnemyVfxCenter(EnemyBaseController enemy)
    {
        if (!enemy)
            return Vector3.zero;

        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        Collider2D best = null;
        float bestArea = 0f;
        Collider2D[] cols = enemy.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
        {
            Collider2D col = cols[i];
            if (!col || !col.enabled)
                continue;

            float area = col.bounds.size.x * col.bounds.size.y;
            if (area <= bestArea)
                continue;

            bestArea = area;
            best = col;
        }

        if (best != null)
            return best.bounds.center;

        return enemy.transform.position;
    }

    private EnemyBaseController FindStaticArrowsStaticArcTarget(EnemyBaseController origin)
    {
        if (!origin)
            return null;

        Vector3 originPos = origin.transform.position;
        float range = AbilityCombatPower.StaticArrowsStaticArcRange;
        float rangeSq = range * range;

        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController candidate = enemies[i];
            if (candidate == null || candidate.IsDead || !candidate.gameObject.activeInHierarchy || candidate == origin)
                continue;

            float distSq = (candidate.transform.position - originPos).sqrMagnitude;
            if (distSq > rangeSq || distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = candidate;
        }

        return best;
    }

    private void ActivateStaticArrowsBuff()
    {
        _staticArrowsBuffActive = true;
        _staticArrowsHitsRemaining = AbilityCombatPower.StaticArrowsAutoAttackCount;
        _staticArrowsAppliedThisHit = false;
        _staticArrowsBuffDuration = AbilityCombatPower.StaticArrowsBaseDurationSeconds;
        _staticArrowsBuffEndsAt = Time.time + _staticArrowsBuffDuration;
        _lastSyncedStaticArrowsHudStacks = int.MinValue;
        _lastSyncedStaticArrowsHudEnd = float.NaN;
        abilityVfx?.SetStaticArrowsBuffArrowVisual(true);
        SyncStaticArrowsHudBuff();
    }

    private void FinishStaticArrowsBuffAndStartCooldown()
    {
        if (!_staticArrowsBuffActive)
            return;

        _staticArrowsBuffActive = false;
        _staticArrowsHitsRemaining = 0;
        _staticArrowsAppliedThisHit = false;
        _staticArrowsBuffEndsAt = 0f;
        _staticArrowsBuffDuration = 0f;

        abilityVfx?.SetStaticArrowsBuffArrowVisual(false);

        if (_staticArrowsCooldownAbilityDef)
            StartCooldown(_staticArrowsCooldownAbilityDef);
        _staticArrowsCooldownAbilityDef = null;

        SyncStaticArrowsHudBuff();
    }

    public bool ShouldAttachStaticArrowsProjectileTrail => _staticArrowsAppliedThisHit;

    private void CleanupStaticArrowsIfExpired()
    {
        if (!_staticArrowsBuffActive)
            return;

        // End only when both duration and hit budget are satisfied: slow weapons can finish all swings after
        // the timer; fast weapons keep the buff until the timer after spending all hit charges.
        bool hitsConsumed = _staticArrowsHitsRemaining <= 0;
        bool durationElapsed = Time.time >= _staticArrowsBuffEndsAt;
        if (hitsConsumed && durationElapsed)
            FinishStaticArrowsBuffAndStartCooldown();
    }

    private void SyncStaticArrowsHudBuff()
    {
        if (!buffController)
            return;

        if (!_staticArrowsBuffActive)
        {
            if (buffController.IsHudAbilityBuffActive(StaticArrowsId))
                buffController.ClearHudAbilityBuff(StaticArrowsId);
            _lastSyncedStaticArrowsHudStacks = int.MinValue;
            _lastSyncedStaticArrowsHudEnd = float.NaN;
            return;
        }

        if (IsOnCooldown(StaticArrowsId, out _))
        {
            buffController.ClearHudAbilityBuff(StaticArrowsId);
            _lastSyncedStaticArrowsHudStacks = int.MinValue;
            _lastSyncedStaticArrowsHudEnd = float.NaN;
            return;
        }

        int displayStacks = _staticArrowsHitsRemaining > 0 ? _staticArrowsHitsRemaining : 1;
        if (_lastSyncedStaticArrowsHudStacks == displayStacks &&
            Mathf.Approximately(_lastSyncedStaticArrowsHudEnd, _staticArrowsBuffEndsAt))
            return;

        _lastSyncedStaticArrowsHudStacks = displayStacks;
        _lastSyncedStaticArrowsHudEnd = _staticArrowsBuffEndsAt;
        buffController.SetHudAbilityBuff(StaticArrowsId, displayStacks, _staticArrowsBuffEndsAt, _staticArrowsBuffDuration);
    }

    private float GetStaticArrowsWeaponDamageMultiplier(AbilityDefinition def)
    {
        float weaponMult = def != null && def.weaponDamageMultiplier > 0f
            ? def.weaponDamageMultiplier
            : AbilityCombatPower.StaticArrowsWeaponDamageMultiplier;
        if (GetStaticArrowsSelectedChoice() == AbilityCombatPower.StaticArrowsFullyChargedChoiceIndex)
            weaponMult += AbilityCombatPower.StaticArrowsFullyChargedDamageBonus;
        return weaponMult;
    }

    private float GetStaticArrowsPhysicalToLightningConversionFraction()
    {
        return GetStaticArrowsSelectedChoice() == AbilityCombatPower.StaticArrowsFullyChargedChoiceIndex
            ? AbilityCombatPower.StaticArrowsFullyChargedConversionFraction
            : AbilityCombatPower.StaticArrowsPhysicalToLightningConversionFraction;
    }

    private int GetStaticArrowsSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
        if (selected < 0)
            selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Ranged,
                AbilityCombatPower.StaticArrowsEnhancementParentSpineNodeId,
                -1);
        return selected;
    }

    private void ActivateLumberFrenzyBuff()
    {
        _lumberFrenzyActive = true;
        float dur = LumberFrenzyDurationSeconds;
        AbilityDefinition lumberDef = GetAbilityDefinition(LumberFrenzyId);
        if (lumberDef != null && lumberDef.tooltipBuffMinionDurationSeconds > 0.01f)
            dur = lumberDef.tooltipBuffMinionDurationSeconds;
        _lumberFrenzyDuration = dur;
        _lumberFrenzyEndsAt = Time.time + _lumberFrenzyDuration;
        _lastSyncedLumberFrenzyHudEnd = float.NaN;
        if (!_fishingFrenzyActive)
            abilityVfx?.SpawnLumberFrenzyOrbitVfx(isFishingFrenzy: false);
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

        if (!_fishingFrenzyActive)
            abilityVfx?.DestroyLumberFrenzyOrbitVfx();

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
            if (buffController.IsHudAbilityBuffActive(LumberFrenzyId))
                buffController.ClearHudAbilityBuff(LumberFrenzyId);
            _lastSyncedLumberFrenzyHudEnd = float.NaN;
            return;
        }

        if (IsOnCooldown(LumberFrenzyId, out _))
        {
            buffController.ClearHudAbilityBuff(LumberFrenzyId);
            _lastSyncedLumberFrenzyHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedLumberFrenzyHudEnd, _lumberFrenzyEndsAt))
            return;

        _lastSyncedLumberFrenzyHudEnd = _lumberFrenzyEndsAt;
        buffController.SetHudAbilityBuff(LumberFrenzyId, 1, _lumberFrenzyEndsAt, _lumberFrenzyDuration);
    }

    private void ActivateFishingFrenzyBuff()
    {
        _fishingFrenzyActive = true;
        float dur = FishingFrenzyDurationSeconds;
        AbilityDefinition fishingDef = GetAbilityDefinition(FishingFrenzyId);
        if (fishingDef != null && fishingDef.tooltipBuffMinionDurationSeconds > 0.01f)
            dur = fishingDef.tooltipBuffMinionDurationSeconds;
        _fishingFrenzyDuration = dur;
        _fishingFrenzyEndsAt = Time.time + _fishingFrenzyDuration;
        _lastSyncedFishingFrenzyHudEnd = float.NaN;
        if (!_lumberFrenzyActive)
            abilityVfx?.SpawnLumberFrenzyOrbitVfx(isFishingFrenzy: true);
        SyncFishingFrenzyHudBuff();
        stats?.NotifyStatsChanged();
    }

    private void CleanupFishingFrenzyIfExpired()
    {
        if (!_fishingFrenzyActive)
            return;
        if (Time.time < _fishingFrenzyEndsAt)
            return;

        _fishingFrenzyActive = false;
        _fishingFrenzyEndsAt = 0f;
        _fishingFrenzyDuration = 0f;

        if (!_lumberFrenzyActive)
            abilityVfx?.DestroyLumberFrenzyOrbitVfx();

        if (_fishingFrenzyCooldownAbilityDef)
            StartCooldown(_fishingFrenzyCooldownAbilityDef);
        _fishingFrenzyCooldownAbilityDef = null;

        stats?.NotifyStatsChanged();
    }

    private void SyncFishingFrenzyHudBuff()
    {
        if (!buffController)
            return;

        if (!_fishingFrenzyActive)
        {
            if (buffController.IsHudAbilityBuffActive(FishingFrenzyId))
                buffController.ClearHudAbilityBuff(FishingFrenzyId);
            _lastSyncedFishingFrenzyHudEnd = float.NaN;
            return;
        }

        if (IsOnCooldown(FishingFrenzyId, out _))
        {
            buffController.ClearHudAbilityBuff(FishingFrenzyId);
            _lastSyncedFishingFrenzyHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedFishingFrenzyHudEnd, _fishingFrenzyEndsAt))
            return;

        _lastSyncedFishingFrenzyHudEnd = _fishingFrenzyEndsAt;
        buffController.SetHudAbilityBuff(FishingFrenzyId, 1, _fishingFrenzyEndsAt, _fishingFrenzyDuration);
    }

    private void ForceEndFishingFrenzyEarly()
    {
        if (!_fishingFrenzyActive)
            return;

        _fishingFrenzyActive = false;
        _fishingFrenzyEndsAt = 0f;
        _fishingFrenzyDuration = 0f;

        if (!_lumberFrenzyActive)
            abilityVfx?.DestroyLumberFrenzyOrbitVfx();

        if (_fishingFrenzyCooldownAbilityDef)
            StartCooldown(_fishingFrenzyCooldownAbilityDef);
        _fishingFrenzyCooldownAbilityDef = null;

        _lastSyncedFishingFrenzyHudEnd = float.NaN;
        SyncFishingFrenzyHudBuff();
        stats?.NotifyStatsChanged();
    }

    private void ActivateAvatarOfTheForestBuff()
    {
        _avatarOfForestActive = true;
        float dur = AvatarOfTheForestBaseDurationSeconds;
        AbilityDefinition avatarDef = GetAbilityDefinition(AvatarOfTheForestId);
        if (avatarDef != null && avatarDef.tooltipBuffMinionDurationSeconds > 0.01f)
            dur = avatarDef.tooltipBuffMinionDurationSeconds;
        if (GetAvatarOfTheForestSelectedChoice() == AvatarOfTheForestDurationEnhancementChoiceIndex)
            dur += AvatarOfTheForestDurationEnhancementBonusSeconds;
        _avatarOfForestDuration = dur;
        _avatarOfForestEndsAt = Time.time + dur;
        _lastSyncedAvatarOfForestHudEnd = float.NaN;
        _avatarOfForestReplenishAccum = 0f;
        abilityVfx?.SpawnAvatarOfTheForestGlowVfx();
        SyncAvatarOfTheForestHudBuff();
        stats?.NotifyStatsChanged();
    }

    private void CleanupAvatarOfTheForestIfExpired()
    {
        if (!_avatarOfForestActive)
            return;
        if (Time.time < _avatarOfForestEndsAt)
            return;

        _avatarOfForestActive = false;
        _avatarOfForestEndsAt = 0f;
        _avatarOfForestDuration = 0f;
        _avatarOfForestReplenishAccum = 0f;

        abilityVfx?.DestroyAvatarOfTheForestGlowVfx();

        if (_avatarOfForestCooldownAbilityDef)
            StartCooldown(_avatarOfForestCooldownAbilityDef);
        _avatarOfForestCooldownAbilityDef = null;

        stats?.NotifyStatsChanged();
    }

    private void SyncAvatarOfTheForestHudBuff()
    {
        if (!buffController)
            return;

        if (!IsAvatarOfTheForestActive)
        {
            if (buffController.IsHudAbilityBuffActive(AvatarOfTheForestId))
                buffController.ClearHudAbilityBuff(AvatarOfTheForestId);
            _lastSyncedAvatarOfForestHudEnd = float.NaN;
            return;
        }

        if (IsOnCooldown(AvatarOfTheForestId, out _))
        {
            buffController.ClearHudAbilityBuff(AvatarOfTheForestId);
            _lastSyncedAvatarOfForestHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedAvatarOfForestHudEnd, _avatarOfForestEndsAt))
            return;

        _lastSyncedAvatarOfForestHudEnd = _avatarOfForestEndsAt;
        buffController.SetHudAbilityBuff(AvatarOfTheForestId, 1, _avatarOfForestEndsAt, _avatarOfForestDuration);
    }

    private void TickAvatarOfTheForestNearbyReplenish(float deltaSeconds)
    {
        if (!IsAvatarOfTheForestActive || !player)
            return;

        _avatarOfForestReplenishAccum += deltaSeconds;
        while (_avatarOfForestReplenishAccum >= AvatarOfTheForestReplenishIntervalSeconds)
        {
            _avatarOfForestReplenishAccum -= AvatarOfTheForestReplenishIntervalSeconds;
            PulseAvatarOfTheForestReplenish();
        }
    }

    private void PulseAvatarOfTheForestReplenish()
    {
        if (!player)
            return;

        Vector3 origin = player.transform.position;
        float r = GetAvatarOfTheForestReplenishRadiusWorld();
        float rSqr = r * r;
        ResourceNode[] all = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            ResourceNode node = all[i];
            if (!node || node.ActionType != NodeAction.Woodcutting || !node.UsesDepletion)
                continue;

            float dx = node.transform.position.x - origin.x;
            float dy = node.transform.position.y - origin.y;
            if (dx * dx + dy * dy > rSqr)
                continue;

            node.TryApplyAvatarOfForestReplenishOneStep();
        }
    }

    private int GetAvatarOfTheForestSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Woodcutting, AbilityCombatPower.AvatarOfTheForestEnhancementParentSpineNodeId, -1);
    }

    /// <summary>Active Avatar of the Forest buff window.</summary>
    public bool IsAvatarOfTheForestActive
    {
        get
        {
            if (!_avatarOfForestActive)
                return false;
            return Time.time < _avatarOfForestEndsAt;
        }
    }

    /// <summary>Final multiplier applied to woodcutting bonus-find rolls after all other bonuses (2 while active).</summary>
    public float GetAvatarOfTheForestBonusFindFinalMultiplier() => IsAvatarOfTheForestActive ? 2f : 1f;

    /// <summary>Flat add to effective woodcutting speed multiplier while active (see <see cref="AbilityCombatPower.AvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd"/>).</summary>
    public float GetAvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd() =>
        IsAvatarOfTheForestActive ? AbilityCombatPower.AvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd : 0f;

    /// <summary>Cleaving Chop range numbers (base + Extended Reach), even when Cleaving Chop is not active.</summary>
    public float GetAvatarOfTheForestReplenishRadiusWorld()
    {
        float range = CleavingChopBaseRange;
        if (GetCleavingChopSelectedChoice() == CleavingChopExtendedReachChoiceIndex)
            range += CleavingChopExtendedReachRangeBonus;
        return range;
    }

    /// <summary>Active Lumber Frenzy orbit VFX instance (null when the buff is off or VFX failed to spawn).</summary>
    public Transform LumberFrenzyOrbitVfxTransform => abilityVfx != null ? abilityVfx.LumberFrenzyOrbitVfxTransform : null;

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

    public bool IsFishingFrenzyActive
    {
        get
        {
            if (!_fishingFrenzyActive)
                return false;
            return Time.time < _fishingFrenzyEndsAt;
        }
    }

    /// <summary>Additive fishing speed bonus from active Fishing Frenzy buff (0 when inactive).</summary>
    public float GetFishingFrenzySpeedBonus() =>
        IsFishingFrenzyActive ? FishingFrenzySpeedBonus : 0f;

    /// <summary>Additive fishing grit chance bonus from active Fishing Frenzy buff, including Iron Grit enhancement (0 when inactive).</summary>
    public float GetFishingFrenzyGritChanceBonus()
    {
        if (!IsFishingFrenzyActive)
            return 0f;

        float bonus = FishingFrenzyGritChanceBonus;
        if (GetFishingFrenzySelectedChoice() == FishingFrenzyExtraGritEnhancementChoiceIndex)
            bonus += FishingFrenzyExtraGritEnhancementBonus;
        return bonus;
    }

    /// <summary>Additive fishing stamina efficiency bonus from active Fishing Frenzy buff, only when Sturdy Grip enhancement is selected (0 otherwise).</summary>
    public float GetFishingFrenzyStaminaEfficiencyBonus()
    {
        if (!IsFishingFrenzyActive)
            return 0f;
        return GetFishingFrenzySelectedChoice() == FishingFrenzyStaminaEnhancementChoiceIndex
            ? FishingFrenzyStaminaEfficiencyEnhancementBonus
            : 0f;
    }

    private int GetFishingFrenzySelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Fishing, FishingFrenzyChoiceSourceLevel, -1);
    }

    private void ActivateCleavingChopBuff()
    {
        _cleavingChopActive = true;
        float baseDur = CleavingChopBaseDurationSeconds;
        AbilityDefinition chopDef = GetAbilityDefinition(CleavingChopId);
        if (chopDef != null && chopDef.tooltipBuffMinionDurationSeconds > 0.01f)
            baseDur = chopDef.tooltipBuffMinionDurationSeconds;
        _cleavingChopDuration = baseDur + GetCleavingChopProlongedBonusSeconds();
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
            if (buffController.IsHudAbilityBuffActive(CleavingChopId))
                buffController.ClearHudAbilityBuff(CleavingChopId);
            _lastSyncedCleavingChopHudEnd = float.NaN;
            return;
        }

        if (IsOnCooldown(CleavingChopId, out _))
        {
            buffController.ClearHudAbilityBuff(CleavingChopId);
            _lastSyncedCleavingChopHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedCleavingChopHudEnd, _cleavingChopEndsAt))
            return;

        _lastSyncedCleavingChopHudEnd = _cleavingChopEndsAt;
        buffController.SetHudAbilityBuff(CleavingChopId, 1, _cleavingChopEndsAt, _cleavingChopDuration);
    }

    /// <summary>True while the Cleaving Strikes empowered-hit window is active.</summary>
    public bool IsCleavingStrikesActive => _cleavingBuffActive;

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
        if (def != null && def.tooltipBuffMinionDurationSeconds > 0.01f)
            _spectralAxeDuration = def.tooltipBuffMinionDurationSeconds;
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
        float vLift = abilityVfx != null ? abilityVfx.SpectralAxeVisualLift : 1.2f;
        float spinRate = abilityVfx != null ? abilityVfx.SpectralAxeSpinDegreesPerSecond : 720f;
        bool spinCw = abilityVfx != null && abilityVfx.SpectralAxeSpinClockwise;
        float travelSpeed = abilityVfx != null ? abilityVfx.SpectralAxeTravelSpeedUnitsPerSecond : 12f;
        Vector3 visualLift = new Vector3(0f, vLift, 0f);

        Sprite axeSprite = ResolveEquippedAxeSprite();
        GameObject projectile = abilityVfx != null ? abilityVfx.CreateSpectralAxeProjectile(origin + visualLift, facing, axeSprite) : null;
        if (projectile == null)
        {
            _spectralAxeActive = false;
            FinishSpectralAxeAfterDespawn();
            yield break;
        }
        _spectralAxeProjectile = projectile;

        Transform projTr = projectile.transform;
        float spinDeg = 0f;
        float spinSign = spinCw ? -1f : 1f;
        int outboundChoice = GetSpectralAxeSelectedChoice();
        bool cleavingFlight = outboundChoice == SpectralAxeCleavingFlightChoiceIndex;
        bool outboundLogAwarded = false;
        bool inboundLogAwarded = false;

        // Outbound travel
        float travelDist = Mathf.Max(0.1f, SpectralAxeProjectDistance);
        float travelTime = travelDist / Mathf.Max(0.1f, travelSpeed);
        float t = 0f;
        while (t < travelTime)
        {
            if (projectile == null) yield break;
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / travelTime);
            Vector3 groundPos = Vector3.Lerp(origin, destination, u);
            projTr.position = groundPos + visualLift;
            spinDeg += spinSign * spinRate * Time.deltaTime;
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
        _spectralAxeAreaCenterWorld = axeCenter;
        _spectralAxeAreaCenterValid = true;

        abilityVfx?.EnsureSpectralAxeAreaIndicatorBuilt(projTr);
        abilityVfx?.UpdateSpectralAxeAreaIndicator(SpectralAxeAreaRadius);

        _spectralAxeGatherTarget = FindClosestWoodcuttingNodeInAxeArea(axeCenter);

        if (_spectralAxeGatherTarget == null)
        {
            // No tree under the axe → log, despawn, and apply the short missed-cast cooldown.
            GameLog.Add("No trees for spectral axe to gather from", GameLog.CannotMessageColor);
            _spectralAxeMissedCast = true;
            _spectralAxeAreaCenterValid = false;

            abilityVfx?.DestroySpectralAxeAreaIndicator();
            if (projectile != null)
                Destroy(projectile);
            _spectralAxeProjectile = null;

            FinishSpectralAxeAfterDespawn();
            yield break;
        }

        while (_spectralAxeActive && Time.time < _spectralAxeEndsAt)
        {
            if (projectile == null) yield break;

            // Re-acquire only if the locked-in target was destroyed. Depleted trees keep gathering at the depleted rate.
            if (_spectralAxeGatherTarget == null ||
                _spectralAxeGatherTarget.Definition == null)
            {
                _spectralAxeGatherTarget = FindClosestWoodcuttingNodeInAxeArea(axeCenter);
                _spectralAxeGatherAccum = 0f;
                _spectralAxeGatherNextInterval = 0f;
            }

            spinDeg += spinSign * spinRate * Time.deltaTime;
            projTr.rotation = Quaternion.Euler(0f, 0f, spinDeg);
            _spectralAxeAreaCenterWorld = axeCenter;
            abilityVfx?.UpdateSpectralAxeAreaIndicator(SpectralAxeAreaRadius);

            AdvanceSpectralAxeGatherTimer(Time.deltaTime);
            yield return null;
        }

        abilityVfx?.DestroySpectralAxeAreaIndicator();

        // Return leg — fly back to the player's current position (chases if the player moved).
        Vector3 returnStart = projTr.position - visualLift; // strip lift so the lerp tracks ground positions
        float returnTime = travelDist / Mathf.Max(0.1f, travelSpeed);
        float rt = 0f;
        while (rt < returnTime)
        {
            if (projectile == null) yield break;
            rt += Time.deltaTime;
            float u = Mathf.Clamp01(rt / returnTime);
            Vector3 playerNow = transform.position;
            Vector3 groundPos = Vector3.Lerp(returnStart, playerNow, u);
            projTr.position = groundPos + visualLift;
            spinDeg += spinSign * spinRate * Time.deltaTime;
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
        _spectralAxeAreaCenterValid = false;
        _spectralAxeGatherTarget = null;
        _spectralAxeGatherAccum = 0f;
        _spectralAxeGatherNextInterval = 0f;
        _spectralAxeRoutine = null;

        abilityVfx?.DestroySpectralAxeAreaIndicator();
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

    /// <summary>
    /// Returns true when at least one toolbelt slot is bound to an item whose <see cref="ItemDefinition.handVisualKey"/>
    /// is <see cref="ToolKey.Axe"/>. Used as the activation gate for Spectral Axe — the ability projects
    /// a copy of the toolbelt axe, so without one there is nothing to project.
    /// </summary>
    public bool HasAxeInToolbelt()
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
            if (!CanGatherWoodcuttingNodeByLevel(node))
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
            if (!CanGatherWoodcuttingNodeByLevel(node))
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

    /// <summary>Level gate for woodcutting-only helper gathers (Spectral Axe / Cleaving Flight).</summary>
    private bool CanGatherWoodcuttingNodeByLevel(ResourceNode node)
    {
        if (node == null || !node.UseLevelRequirement)
            return true;

        SkillsManager sm = skillsManager != null ? skillsManager : SkillsManager.Instance;
        int lvl = sm != null ? sm.GetLevel(SkillType.Woodcutting) : 1;
        return lvl >= node.RequiredLevel;
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

    private void DoOneSpectralAxeGather(ResourceNode node)
    {
        if (!node || node.Definition == null)
            return;
        if (inventory == null)
            return;

        var nodeDef = node.Definition;

        bool countTowardDepletion = !IsAvatarOfTheForestActive;
        node.NotifyGatherTickBeforeBonuses(countTowardDepletion);

        int woodGatherLevel = SkillsManager.Instance != null
            ? Mathf.Max(1, SkillsManager.Instance.GetLevel(SkillType.Woodcutting))
            : 1;

        var mainScratch = new Dictionary<string, int>(4);
        nodeDef.RollMainYieldCounts(mainScratch, woodGatherLevel, null);
        int mainAmt = NodeDefinition.SumMainYieldCounts(mainScratch);
        if (node.ApplyDepletedYieldPenaltyThisTick)
            mainAmt = SpectralAxeRollDepletedYield(mainAmt, nodeDef.depletedYieldMultiplier);

        // Match Cleaving Chop: spectral gathers yield at 60% efficiency (stochastic so small rolls
        // like 1 log don't get stuck at 1; they average out to the efficiency over many ticks).
        mainAmt = RollSpectralAxeEfficiencyYield(mainAmt, SpectralAxeYieldEfficiency);

        if (mainAmt > 0)
            AddSpectralAxeLootToInventory(node, mainAmt, node.transform.position, nodeDef.GetPrimaryYieldItemIdForSkillLevel(woodGatherLevel));

        // Phantom Harvest: also roll bonus then hidden drops using the player's bonus find chance,
        // mirroring the bonus pass in PlayerController.DoOneGatherTick (hidden only after a bonus proc).
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
        if (IsAvatarOfTheForestActive)
            bonusFind *= 2f;
        int woodGatherLevel = SkillsManager.Instance != null
            ? Mathf.Max(1, SkillsManager.Instance.GetLevel(SkillType.Woodcutting))
            : 1;
        var drops = new List<Drop>(8);
        nodeDef.PreviewDrops(drops, bonusFind, default, woodGatherLevel);

        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            if (string.IsNullOrWhiteSpace(d.itemId) || d.amount <= 0)
                continue;
            // Skip the main yield — already handled by DoOneSpectralAxeGather.
            if (nodeDef.IsMainYieldPoolItem(d.itemId))
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

        string itemId = string.IsNullOrWhiteSpace(overrideItemId)
            ? node.Definition.GetPrimaryYieldItemIdForSkillLevel(
                SkillsManager.Instance != null
                    ? Mathf.Max(1, SkillsManager.Instance.GetLevel(SkillType.Woodcutting))
                    : 1)
            : overrideItemId;
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
            if (buffController.IsHudAbilityBuffActive(SpectralAxeId))
                buffController.ClearHudAbilityBuff(SpectralAxeId);
            _lastSyncedSpectralAxeHudEnd = float.NaN;
            return;
        }

        if (IsOnCooldown(SpectralAxeId, out _))
        {
            buffController.ClearHudAbilityBuff(SpectralAxeId);
            _lastSyncedSpectralAxeHudEnd = float.NaN;
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

    /// <summary>Spectral Axe gather circle radius in world units (matches the parked axe area scan).</summary>
    public float SpectralAxeGatherAreaRadius => SpectralAxeAreaRadius;

    /// <summary>When Spectral Axe is parked and chopping, returns the lifted visual center of its gather circle.</summary>
    public bool TryGetSpectralAxeGatherArea(out Vector3 centerWorld, out float radiusWorld)
    {
        if (!IsSpectralAxeActive || !_spectralAxeAreaCenterValid)
        {
            centerWorld = default;
            radiusWorld = 0f;
            return false;
        }

        centerWorld = _spectralAxeAreaCenterWorld;
        radiusWorld = SpectralAxeAreaRadius;
        return true;
    }

    /// <summary>Woodcutting tree the parked Spectral Axe is currently gathering from (null when inactive).</summary>
    public ResourceNode SpectralAxeGatherTarget => IsSpectralAxeActive ? _spectralAxeGatherTarget : null;

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

        if (string.Equals(abilityId, StaticArrowsId, StringComparison.OrdinalIgnoreCase))
        {
            CleanupStaticArrowsIfExpired();
            if (!_staticArrowsBuffActive)
                return 0;
            return _staticArrowsHitsRemaining > 0 ? _staticArrowsHitsRemaining : 1;
        }

        if (string.Equals(abilityId, FlameChargeId, StringComparison.OrdinalIgnoreCase))
        {
            RefreshFlameChargeChargesFromSkillTree();
            return GetFlameChargeReadyChargeCount();
        }

        if (string.Equals(abilityId, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase))
            return _crusaderStrikeComboStep >= CrusaderStrikeFinalComboStep ? 0 : Mathf.Max(0, _crusaderStrikeComboStep);

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

    private int GetGuardiansHammerSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.GuardiansHammerEnhancementParentSpineNodeId, -1);
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
        if (string.Equals(abilityId, TripleShotId, StringComparison.OrdinalIgnoreCase))
            return _tripleShotQueued;
        if (string.Equals(abilityId, RendId, StringComparison.OrdinalIgnoreCase))
            return _rendQueued;
        if (string.Equals(abilityId, EnvenomId, StringComparison.OrdinalIgnoreCase))
            return _envenomQueued;
        if (string.Equals(abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase))
            return _crescentSlashQueued;
        if (string.Equals(abilityId, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase))
            return IsCrusaderStrikeComboInProgress();
        if (string.Equals(abilityId, StaticArrowsId, StringComparison.OrdinalIgnoreCase))
            return _staticArrowsBuffActive;

        return false;
    }

    public bool CanUseAbilityWithCurrentWeapon(string abilityId)
    {
        AbilityDefinition def = GetAbilityDefinition(abilityId);
        if (!def)
            return false;
        return CanUseWithEquippedWeapon(def);
    }

    /// <summary>
    /// When a cooldown is applied, any overlapping timed / lingering window for that ability must end so HUD and
    /// gameplay cannot stay "active" while the ability is on cooldown (Cleaving Strikes is excluded: its hit window
    /// intentionally overlaps its cast cooldown).
    /// </summary>
    private void TeardownLingeringAbilityStateBeforeCooldownWrite(AbilityDefinition def)
    {
        if (!def || string.IsNullOrWhiteSpace(def.abilityId))
            return;

        string id = def.abilityId;
        if (string.Equals(id, CleavingStrikesId, StringComparison.OrdinalIgnoreCase))
            return;

        // Timed buff overlaps cast cooldown (same contract as Energy Infusion / Cleaving Strikes).
        if (IsWarBannerAbilityId(id))
            return;

        if (IsLightningRodAbilityId(id))
            return;

        if (IsHuntersSwiftnessAbilityId(id))
            return;

        if (IsTornadoAbilityId(id))
            return;

        if (string.Equals(id, LumberFrenzyId, StringComparison.OrdinalIgnoreCase) && _lumberFrenzyActive)
        {
            _lumberFrenzyActive = false;
            _lumberFrenzyEndsAt = 0f;
            _lumberFrenzyDuration = 0f;
            if (!_fishingFrenzyActive)
                abilityVfx?.DestroyLumberFrenzyOrbitVfx();
            _lumberFrenzyCooldownAbilityDef = null;
            _lastSyncedLumberFrenzyHudEnd = float.NaN;
            buffController?.ClearHudAbilityBuff(LumberFrenzyId);
            stats?.NotifyStatsChanged();
            return;
        }

        if (string.Equals(id, FishingFrenzyId, StringComparison.OrdinalIgnoreCase) && _fishingFrenzyActive)
        {
            _fishingFrenzyActive = false;
            _fishingFrenzyEndsAt = 0f;
            _fishingFrenzyDuration = 0f;
            if (!_lumberFrenzyActive)
                abilityVfx?.DestroyLumberFrenzyOrbitVfx();
            _fishingFrenzyCooldownAbilityDef = null;
            _lastSyncedFishingFrenzyHudEnd = float.NaN;
            buffController?.ClearHudAbilityBuff(FishingFrenzyId);
            stats?.NotifyStatsChanged();
            return;
        }

        if (string.Equals(id, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase) && IsAvatarOfTheForestActive)
        {
            _avatarOfForestActive = false;
            _avatarOfForestEndsAt = 0f;
            _avatarOfForestDuration = 0f;
            _avatarOfForestReplenishAccum = 0f;
            abilityVfx?.DestroyAvatarOfTheForestGlowVfx();
            _avatarOfForestCooldownAbilityDef = null;
            _lastSyncedAvatarOfForestHudEnd = float.NaN;
            buffController?.ClearHudAbilityBuff(AvatarOfTheForestId);
            stats?.NotifyStatsChanged();
            return;
        }

        if (string.Equals(id, CleavingChopId, StringComparison.OrdinalIgnoreCase) && IsCleavingChopActive)
        {
            _cleavingChopActive = false;
            _cleavingChopEndsAt = 0f;
            _cleavingChopDuration = 0f;
            _cleavingChopCooldownAbilityDef = null;
            _lastSyncedCleavingChopHudEnd = float.NaN;
            buffController?.ClearHudAbilityBuff(CleavingChopId);
            return;
        }

        if (string.Equals(id, WhirlwindId, StringComparison.OrdinalIgnoreCase) && _whirlwindChanneling)
        {
            ForceEndWhirlwindChannel(clearHeldState: true, applyCooldown: false);
            return;
        }

        if (string.Equals(id, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase) && IsCrusaderStrikeComboInProgress())
        {
            ForceEndCrusaderStrikeCombo(applyCooldown: false);
            return;
        }

        if (string.Equals(id, SpectralAxeId, StringComparison.OrdinalIgnoreCase) &&
            (_spectralAxeActive || _spectralAxeRoutine != null || _spectralAxeProjectile != null))
        {
            AbortSpectralAxe(awardCooldown: false);
            return;
        }

        if (string.Equals(id, AbilityCombatPower.SoulforgedWeaponAbilityId, StringComparison.OrdinalIgnoreCase) &&
            _activeSoulforgedWeaponMinions.Count > 0)
        {
            for (int i = _activeSoulforgedWeaponMinions.Count - 1; i >= 0; i--)
            {
                SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
                if (minion)
                    minion.CancelAndDestroy();
            }

            _activeSoulforgedWeaponMinions.Clear();
            _soulforgedWeaponCooldownAbilityDef = null;
            _lastSyncedSoulforgedHudEnd = float.NaN;
            _lastSyncedSoulforgedHudStacks = int.MinValue;
            buffController?.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWeaponAbilityId);
        }
    }

    private void StartCooldown(AbilityDefinition def)
    {
        if (!def) return;
        float cd = Mathf.Max(0f, def.cooldown - GetPowerSlashCooldownReduction(def) - GetTripleShotCooldownReduction(def) - GetAvatarOfTheForestCooldownReduction(def));
        if (stats != null)
            cd *= Mathf.Max(0.05f, 1f - stats.FinalAbilityCooldownReductionFraction);
        if (cd <= 0f) return;

        TeardownLingeringAbilityStateBeforeCooldownWrite(def);

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

    private float GetTripleShotAnyTypeMultiplierBonus()
    {
        if (!skillsManager) skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return 0f;

        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
        return selected == 0 ? AbilityCombatPower.TripleShotEnhancementDamageBonus : 0f;
    }

    private float GetTripleShotCooldownReduction(AbilityDefinition def)
    {
        if (def == null || !string.Equals(def.abilityId, TripleShotId, StringComparison.OrdinalIgnoreCase))
            return 0f;
        if (!skillsManager) skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return 0f;

        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
        return selected == 1 ? AbilityCombatPower.TripleShotEnhancementCooldownReductionSeconds : 0f;
    }

    private void BeginTripleShotPhantomVolley(EnemyBaseController target)
    {
        if (_tripleShotPhantomVolleyRoutine != null)
        {
            StopCoroutine(_tripleShotPhantomVolleyRoutine);
            _tripleShotPhantomVolleyRoutine = null;
        }

        if (target == null || target.IsDead)
            return;

        _tripleShotPhantomVolleyRoutine = StartCoroutine(CoTripleShotPhantomVolley(target));
    }

    private IEnumerator CoTripleShotPhantomVolley(EnemyBaseController initialTarget)
    {
        int phantomShots = AbilityCombatPower.TripleShotArrowCount - 1;
        float interval = AbilityCombatPower.TripleShotPhantomArrowIntervalSeconds;
        AbilityDefinition def = GetAbilityDefinition(TripleShotId);
        string sourceLabel = GetAbilityOutgoingDamageSourceLabel(TripleShotId);

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        for (int i = 0; i < phantomShots; i++)
        {
            yield return new WaitForSeconds(interval);

            if (player == null || stats == null || combat == null || stats.IsDead || player.IsDead)
                break;

            EnemyBaseController target = initialTarget;
            if (target == null || target.IsDead || !target.gameObject.activeInHierarchy)
            {
                target = combat.CurrentTarget;
                if (target == null || target.IsDead)
                    break;
            }

            SplitDamage rolled = stats.RollSplitAttackDamage(out bool wasCrit);
            ApplyTripleShotPhantomDamageScaling(ref rolled, def);
            ApplyActiveDamageConversions(ref rolled);
            if (rolled.IsEmpty)
                continue;

            var attribution = new PlayerCombatController.SwingOutgoingAttribution(
                "Auto Attack",
                sourceLabel,
                1f);
            combat.FireBonusRangedAttackShot(target, rolled, wasCrit, attribution, consumeAmmo: false);
        }

        _tripleShotPhantomVolleyRoutine = null;
        _queuedTripleShotUsedEnergyInfusionMana = false;
    }

    private void ApplyTripleShotPhantomDamageScaling(ref SplitDamage rolled, AbilityDefinition def)
    {
        if (rolled.IsEmpty || stats == null)
            return;

        float apM = GetAbilityPowerDamageMultiplierForAbility(def);
        float allM = _queuedTripleShotAllDamageMultiplier;
        float elementBonus = def != null ? AbilityElementScaling.GetElementDamageBonus(def, stats) : 0f;
        float ailmentBonus = def != null ? AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats) : 0f;
        float overloadMult = GetBattleEngineOverloadDamageMultiplier();

        rolled.physical = ((rolled.physical * _queuedTripleShotWeaponMultiplier + ailmentBonus) * apM * allM) * overloadMult;
        rolled.magic = ((rolled.magic * _queuedTripleShotWeaponMultiplier + elementBonus) * apM * allM) * overloadMult;
        rolled.corruptionDamage = ((rolled.corruptionDamage * _queuedTripleShotWeaponMultiplier) * apM * allM) * overloadMult;
        rolled.physical = Mathf.Max(0f, rolled.physical);
        rolled.magic = Mathf.Max(0f, rolled.magic);
        rolled.corruptionDamage = Mathf.Max(0f, rolled.corruptionDamage);
    }

    private float GetAvatarOfTheForestCooldownReduction(AbilityDefinition def)
    {
        if (def == null || !string.Equals(def.abilityId, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase))
            return 0f;
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return 0f;

        int selected = skillsManager.GetSkillChoiceSelection(
            SkillType.Woodcutting, AbilityCombatPower.AvatarOfTheForestEnhancementParentSpineNodeId, -1);
        return selected == AvatarOfTheForestCooldownEnhancementChoiceIndex
            ? AvatarOfTheForestCooldownEnhancementReductionSeconds
            : 0f;
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
        AbilityDefinition def = db ? db.Get(abilityId) : null;
        return def ? def : AbilityDatabase.FindDefinitionById(abilityId);
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

    private bool WouldAbilityDealNoDamageWithCurrentWeapon(AbilityDefinition def)
    {
        if (def == null || stats == null)
            return false;

        if (string.Equals(def.abilityId, CrusaderStrikeId, StringComparison.OrdinalIgnoreCase))
            return !stats.CurrentMeleeWeaponHasPhysicalOrFireDamage();

        if (string.Equals(def.abilityId, GuardiansHammerId, StringComparison.OrdinalIgnoreCase))
            return stats.AveragePhysicalHit + stats.AverageMagicHit + stats.AverageCorruptionHit <= 0.0001f;

        return false;
    }

    private bool TrySpawnSoulforgedWeaponMinion(AbilityDefinition def, bool recordDamageMeterSummonUse = true)
    {
        MinionDefinition md = def.minionSpawnDefinition;
        if (!md || !md.runtimePrefab || !_ownerStats || !player)
            return false;

        Transform anchor = player.SoulforgedWeaponSpawnPoint;
        Transform attacker = _ownerTransform ? _ownerTransform : _ownerStats.transform;
        Sprite weaponSprite = ResolveSoulforgedWeaponVisualSprite(md, def);
        int selectedChoice = GetSoulforgedWeaponSelectedChoice();
        bool swarm = selectedChoice == SoulforgedWeaponSwarmChoiceIndex;
        bool extendedDuration = selectedChoice == SoulforgedWeaponExtendedDurationChoiceIndex;
        int spawnCount = swarm ? SoulforgedWeaponSwarmCount : 1;
        CleanupSoulforgedWeaponList();

        float swarmDurationSeconds = SoulforgedWeaponSwarmDurationSeconds;
        if (def != null && def.tooltipBuffMinionDurationSeconds > 0.01f)
            swarmDurationSeconds = def.tooltipBuffMinionDurationSeconds;

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

            float durationOverride = -1f;
            if (swarm)
                durationOverride = swarmDurationSeconds;
            else if (extendedDuration)
                durationOverride = SoulforgedWeaponExtendedDurationSeconds;

            if (!minion.Initialize(
                    _ownerStats,
                    md,
                    abilityVfx != null ? abilityVfx.SoulforgedWeaponMinionPresentation : default,
                    anchor,
                    weaponSprite,
                    attacker,
                    HandleSoulforgedWeaponReleased,
                    durationOverride,
                    neverExpires: false,
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

        if (swarm)
        {
            _soulforgedHudBuffDuration = swarmDurationSeconds;
            _soulforgedHudBuffEndsAt = Time.time + swarmDurationSeconds;
            _soulforgedHudPersistOverlay = false;
        }
        else if (extendedDuration)
        {
            _soulforgedHudBuffDuration = SoulforgedWeaponExtendedDurationSeconds;
            _soulforgedHudBuffEndsAt = Time.time + SoulforgedWeaponExtendedDurationSeconds;
            _soulforgedHudPersistOverlay = false;
        }
        else
        {
            float dur = md != null ? Mathf.Max(0.1f, md.summonDuration) : SoulforgedWeaponSwarmDurationSeconds;
            if (def != null && def.tooltipBuffMinionDurationSeconds > 0.01f)
                dur = def.tooltipBuffMinionDurationSeconds;
            _soulforgedHudBuffDuration = dur;
            _soulforgedHudBuffEndsAt = Time.time + dur;
            _soulforgedHudPersistOverlay = false;
        }
        _lastSyncedSoulforgedHudEnd = float.NaN;
        _lastSyncedSoulforgedHudStacks = int.MinValue;
        SyncSoulforgedWeaponHudBuff();
        SyncHawkCompanionHudBuff();

        if (recordDamageMeterSummonUse)
        {
            if (combat == null)
                combat = GetComponent<PlayerCombatController>();
            combat?.RecordOutgoingSourceUse(PlayerCombatController.DefaultMinionOutgoingSourceLabel);
        }

        return true;
    }

    private static bool IsSoulforgedWeaponAbility(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.SoulforgedWeaponAbilityId, StringComparison.OrdinalIgnoreCase);

    private static bool IsSoulforgedWarriorAbility(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.SoulforgedWarriorAbilityId, StringComparison.OrdinalIgnoreCase);

    private bool TrySpawnMinionForAbility(AbilityDefinition def)
    {
        MinionDefinition md = def.minionSpawnDefinition;
        if (!md || !md.runtimePrefab)
            return false;

        if (md.runtimePrefab.GetComponent<HawkCompanionMinion>() ||
            md.runtimePrefab.GetComponentInChildren<HawkCompanionMinion>(true))
            return TrySpawnHawkCompanionMinion(def);

        if (md.runtimePrefab.GetComponent<SoulforgedWarriorMinion>() ||
            md.runtimePrefab.GetComponentInChildren<SoulforgedWarriorMinion>(true))
            return TrySpawnSoulforgedWarriorMinion(def);

        return TrySpawnSoulforgedWeaponMinion(def);
    }

    private static bool IsHawkCompanionAbility(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.HawkCompanionAbilityId, StringComparison.OrdinalIgnoreCase);

    private bool TrySpawnHawkCompanionMinion(AbilityDefinition def, bool recordDamageMeterSummonUse = true)
    {
        MinionDefinition md = def.minionSpawnDefinition;
        if (!md || !md.runtimePrefab || !_ownerStats || !player)
            return false;

        CleanupHawkCompanionSummonsWithoutCooldown();

        Transform anchor = _ownerStats.transform;
        Transform attacker = _ownerTransform ? _ownerTransform : anchor;
        GameObject go = Instantiate(md.runtimePrefab, anchor.position, Quaternion.identity);
        HawkCompanionMinion minion = go.GetComponent<HawkCompanionMinion>();
        if (!minion)
        {
            Destroy(go);
            return false;
        }

        float duration = md.summonDuration;
        if (def.tooltipBuffMinionDurationSeconds > 0.01f)
            duration = def.tooltipBuffMinionDurationSeconds;

        int choice = GetHawkCompanionSelectedChoice();
        if (!minion.Initialize(
                _ownerStats,
                md,
                abilityVfx != null ? abilityVfx.HawkCompanionMinionPresentation : default,
                anchor,
                attacker,
                HandleHawkCompanionReleased,
                skillsManager,
                choice,
                duration))
        {
            Destroy(go);
            return false;
        }

        minion.PersistAcrossSceneLoads();
        _activeHawkCompanionMinions.Add(minion);
        _hawkCompanionCooldownAbilityDef = def;
        _hawkCompanionHudBuffDuration = Mathf.Max(0.1f, duration);
        _hawkCompanionHudBuffEndsAt = Time.time + _hawkCompanionHudBuffDuration;
        _lastSyncedHawkHudEnd = float.NaN;
        SyncHawkCompanionHudBuff();
        NotifyActionBarMinionControlChanged();

        if (recordDamageMeterSummonUse)
        {
            if (combat == null)
                combat = GetComponent<PlayerCombatController>();
            combat?.RecordOutgoingSourceUse(AbilityCombatPower.HawkCompanionOutgoingSourceLabel);
        }

        return true;
    }

    private int GetHawkCompanionSelectedChoice()
    {
        if (skillsManager == null)
            return -1;

        int selected = skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged, AbilityCombatPower.HawkCompanionEnhancementParentSpineNodeId, -1);
        if (selected < 0)
            selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
        return selected;
    }

    private void HandleHawkCompanionReleased(HawkCompanionMinion m)
    {
        _activeHawkCompanionMinions.Remove(m);
        CleanupHawkCompanionList();

        if (_activeHawkCompanionMinions.Count == 0 && _hawkCompanionCooldownAbilityDef)
        {
            StartCooldown(_hawkCompanionCooldownAbilityDef);
            _hawkCompanionCooldownAbilityDef = null;
        }

        SyncHawkCompanionHudBuff();
        NotifyActionBarMinionControlChanged();
    }

    private void RecastActiveHawkCompanions()
    {
        CleanupHawkCompanionList();
        for (int i = 0; i < _activeHawkCompanionMinions.Count; i++)
            _activeHawkCompanionMinions[i]?.TryRecastRetargetOrReturn();
    }

    private void CleanupHawkCompanionList()
    {
        for (int i = _activeHawkCompanionMinions.Count - 1; i >= 0; i--)
        {
            if (!_activeHawkCompanionMinions[i])
                _activeHawkCompanionMinions.RemoveAt(i);
        }
    }

    private void CleanupHawkCompanionSummonsWithoutCooldown()
    {
        for (int i = _activeHawkCompanionMinions.Count - 1; i >= 0; i--)
        {
            HawkCompanionMinion minion = _activeHawkCompanionMinions[i];
            if (minion)
                minion.CancelAndDestroy();
        }

        _activeHawkCompanionMinions.Clear();
        _hawkCompanionCooldownAbilityDef = null;
        SyncHawkCompanionHudBuff();
        NotifyActionBarMinionControlChanged();
    }

    private void SyncHawkCompanionHudBuff()
    {
        if (!buffController)
            buffController = GetComponent<PlayerBuffController>();
        if (!buffController)
            return;

        int liveCount = 0;
        for (int i = 0; i < _activeHawkCompanionMinions.Count; i++)
        {
            if (_activeHawkCompanionMinions[i])
                liveCount++;
        }

        if (liveCount <= 0)
        {
            if (buffController.IsHudAbilityBuffActive(AbilityCombatPower.HawkCompanionAbilityId))
                buffController.ClearHudAbilityBuff(AbilityCombatPower.HawkCompanionAbilityId);
            _lastSyncedHawkHudEnd = float.NaN;
            return;
        }

        if (IsOnCooldown(AbilityCombatPower.HawkCompanionAbilityId, out _))
        {
            buffController.ClearHudAbilityBuff(AbilityCombatPower.HawkCompanionAbilityId);
            _lastSyncedHawkHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedHawkHudEnd, _hawkCompanionHudBuffEndsAt))
            return;

        _lastSyncedHawkHudEnd = _hawkCompanionHudBuffEndsAt;
        buffController.SetHudAbilityBuff(
            AbilityCombatPower.HawkCompanionAbilityId,
            1,
            _hawkCompanionHudBuffEndsAt,
            _hawkCompanionHudBuffDuration,
            persistActiveOverlay: false);
    }

    private bool TrySpawnSoulforgedWarriorMinion(AbilityDefinition def, bool recordDamageMeterSummonUse = true)
    {
        MinionDefinition md = def.minionSpawnDefinition;
        if (!md || !md.runtimePrefab || !_ownerStats || !player)
            return false;

        CleanupSoulforgedWarriorSummonsWithoutCooldown();

        Transform ownerRoot = _ownerStats.transform;
        Transform attacker = _ownerTransform ? _ownerTransform : ownerRoot;
        GameObject go = Instantiate(md.runtimePrefab, ownerRoot.position, Quaternion.identity);
        LaneGroundEffectPlacement.AttachUnitToLane(go.transform);
        SoulforgedWarriorMinion minion = go.GetComponent<SoulforgedWarriorMinion>();
        if (!minion)
        {
            Destroy(go);
            return false;
        }

        float duration = md.summonDuration;
        if (def.tooltipBuffMinionDurationSeconds > 0.01f)
            duration = def.tooltipBuffMinionDurationSeconds;

        if (!minion.Initialize(
                _ownerStats,
                md,
                abilityVfx != null ? abilityVfx.SoulforgedWeaponMinionPresentation : default,
                ownerRoot,
                attacker,
                HandleSoulforgedWarriorReleased,
                duration,
                skillsManager))
        {
            Destroy(go);
            return false;
        }

        minion.PersistAcrossSceneLoads();
        _activeSoulforgedWarriorMinions.Add(minion);
        _soulforgedWarriorCooldownAbilityDef = def;
        _soulforgedWarriorHudBuffDuration = Mathf.Max(0.1f, duration);
        _soulforgedWarriorHudBuffEndsAt = Time.time + _soulforgedWarriorHudBuffDuration;
        SyncSoulforgedWarriorHudBuff();

        if (recordDamageMeterSummonUse)
        {
            if (combat == null)
                combat = GetComponent<PlayerCombatController>();
            combat?.RecordOutgoingSourceUse(AbilityCombatPower.SoulforgedWarriorOutgoingSourceLabel);
        }

        NotifyActionBarMinionControlChanged();
        return true;
    }

    private static void NotifyActionBarMinionControlChanged()
    {
        ActionBarUI[] bars = FindObjectsByType<ActionBarUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < bars.Length; i++)
            bars[i]?.NotifyMinionControlBarChanged();
    }

    private void SyncSoulforgedWarriorHudBuff()
    {
        if (!buffController)
            buffController = GetComponent<PlayerBuffController>();
        if (!buffController)
            return;

        CleanupSoulforgedWarriorList();
        if (_activeSoulforgedWarriorMinions.Count <= 0)
        {
            if (buffController.IsHudAbilityBuffActive(AbilityCombatPower.SoulforgedWarriorAbilityId))
                buffController.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWarriorAbilityId);
            return;
        }

        if (IsOnCooldown(AbilityCombatPower.SoulforgedWarriorAbilityId, out _))
        {
            buffController.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWarriorAbilityId);
            return;
        }

        buffController.SetHudAbilityBuff(
            AbilityCombatPower.SoulforgedWarriorAbilityId,
            1,
            _soulforgedWarriorHudBuffEndsAt,
            _soulforgedWarriorHudBuffDuration,
            persistActiveOverlay: false);
    }

    private void HandleSoulforgedWarriorReleased(SoulforgedWarriorMinion m)
    {
        _activeSoulforgedWarriorMinions.Remove(m);
        CleanupSoulforgedWarriorList();

        if (_activeSoulforgedWarriorMinions.Count == 0 && _soulforgedWarriorCooldownAbilityDef)
        {
            StartCooldown(_soulforgedWarriorCooldownAbilityDef);
            _soulforgedWarriorCooldownAbilityDef = null;
            buffController?.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWarriorAbilityId);
        }

        NotifyActionBarMinionControlChanged();
    }

    private void RecastActiveSoulforgedWarriors()
    {
        CleanupSoulforgedWarriorList();
        for (int i = 0; i < _activeSoulforgedWarriorMinions.Count; i++)
            _activeSoulforgedWarriorMinions[i]?.TryRecastRetargetOrReturn();
    }

    private void CleanupSoulforgedWarriorList()
    {
        for (int i = _activeSoulforgedWarriorMinions.Count - 1; i >= 0; i--)
        {
            if (!_activeSoulforgedWarriorMinions[i])
                _activeSoulforgedWarriorMinions.RemoveAt(i);
        }
    }

    private void CleanupSoulforgedWarriorSummonsWithoutCooldown()
    {
        for (int i = _activeSoulforgedWarriorMinions.Count - 1; i >= 0; i--)
        {
            SoulforgedWarriorMinion minion = _activeSoulforgedWarriorMinions[i];
            if (minion)
                minion.CancelAndDestroy();
        }

        _activeSoulforgedWarriorMinions.Clear();
        _soulforgedWarriorCooldownAbilityDef = null;
    }

    private void EndSoulforgedWarriorAndStartCooldown()
    {
        AbilityDefinition cooldownDef = _soulforgedWarriorCooldownAbilityDef;
        bool hadActiveMinion = _activeSoulforgedWarriorMinions.Count > 0;

        CleanupSoulforgedWarriorSummonsWithoutCooldown();

        if (cooldownDef != null && hadActiveMinion)
            StartCooldown(cooldownDef);

        buffController?.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWarriorAbilityId);
        NotifyActionBarMinionControlChanged();
    }

    private void EndHawkCompanionAndStartCooldown()
    {
        AbilityDefinition cooldownDef = _hawkCompanionCooldownAbilityDef;
        bool hadActiveMinion = _activeHawkCompanionMinions.Count > 0;

        CleanupHawkCompanionSummonsWithoutCooldown();

        if (cooldownDef != null && hadActiveMinion)
            StartCooldown(cooldownDef);

        buffController?.ClearHudAbilityBuff(AbilityCombatPower.HawkCompanionAbilityId);
        NotifyActionBarMinionControlChanged();
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
            if (buffController.IsHudAbilityBuffActive(AbilityCombatPower.SoulforgedWeaponAbilityId))
                buffController.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWeaponAbilityId);
            _lastSyncedSoulforgedHudEnd = float.NaN;
            _lastSyncedSoulforgedHudStacks = int.MinValue;
            return;
        }

        if (IsOnCooldown(AbilityCombatPower.SoulforgedWeaponAbilityId, out _))
        {
            buffController.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWeaponAbilityId);
            _lastSyncedSoulforgedHudEnd = float.NaN;
            _lastSyncedSoulforgedHudStacks = int.MinValue;
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
            _soulforgedHudBuffDuration,
            _soulforgedHudPersistOverlay);
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
        }
    }

    private void RecastActiveSoulforgedWeapons()
    {
        CleanupSoulforgedWeaponList();
        MinionControlStance stance = MinionControlService.CurrentStance;
        if (stance == MinionControlStance.Passive)
        {
            for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
                _activeSoulforgedWeaponMinions[i]?.ForceTarget(null);
            return;
        }

        bool swarm = GetSoulforgedWeaponSelectedChoice() == SoulforgedWeaponSwarmChoiceIndex;
        EnemyBaseController playerTarget = combat != null ? combat.CurrentTarget : null;
        if (playerTarget && playerTarget.IsDead)
            playerTarget = null;

        if (swarm && stance == MinionControlStance.Assist)
        {
            if (playerTarget)
            {
                for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
                    _activeSoulforgedWeaponMinions[i]?.ForceTarget(playerTarget);
            }
            else
            {
                for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
                    _activeSoulforgedWeaponMinions[i]?.ForceTarget(null);
            }

            return;
        }

        if (swarm && playerTarget)
        {
            for (int i = 0; i < _activeSoulforgedWeaponMinions.Count; i++)
                _activeSoulforgedWeaponMinions[i]?.ForceTarget(playerTarget);
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
        abilityVfx?.DestroySpectralAxeAreaIndicator();
        SyncSpectralAxeHudBuff();

        if (awardCooldown && wasActive && deferred != null)
            StartCooldown(deferred);
    }

    /// <summary>
    /// Visual only: held → equipped → item icon → ability presentation icon → minion placeholder. Does not copy weapon combat stats.
    /// </summary>
    private Sprite ResolveSoulforgedWeaponVisualSprite(MinionDefinition md, AbilityDefinition abilityDef)
    {
        Sprite FallbackAbilityOrPlaceholder()
        {
            Sprite abIcon = abilityDef ? SkillsAbilityPresentationResolver.ResolveAbilityIcon(abilityDef) : null;
            if (abIcon)
                return abIcon;
            if (abilityVfx != null &&
                abilityVfx.SoulforgedWeaponMinionPresentation.placeholderWeaponSprite)
                return abilityVfx.SoulforgedWeaponMinionPresentation.placeholderWeaponSprite;
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

    private void RefreshFlameChargeChargesFromSkillTree()
    {
        int choice = GetFlameChargeSelectedChoice();
        int newMax = choice == 0 ? 2 : 1;

        if (!_flameChargeChargesInitialized)
        {
            _flameChargeChargesMax = newMax;
            _flameChargePerChargeCooldownEnds = CreateReadyFlameChargeCooldownArray(newMax);
            _flameChargeLastKnownMaxCharges = newMax;
            _flameChargeChargesInitialized = true;
            return;
        }

        if (newMax > _flameChargeLastKnownMaxCharges)
        {
            float[] expanded = CreateReadyFlameChargeCooldownArray(newMax);
            for (int i = 0; i < _flameChargePerChargeCooldownEnds.Length && i < expanded.Length; i++)
                expanded[i] = _flameChargePerChargeCooldownEnds[i];
            _flameChargePerChargeCooldownEnds = expanded;
        }
        else if (newMax < _flameChargePerChargeCooldownEnds.Length)
        {
            var trimmed = new float[newMax];
            for (int i = 0; i < newMax; i++)
                trimmed[i] = _flameChargePerChargeCooldownEnds[i];
            _flameChargePerChargeCooldownEnds = trimmed;
        }

        _flameChargeChargesMax = newMax;
        _flameChargeLastKnownMaxCharges = newMax;
    }

    private static float[] CreateReadyFlameChargeCooldownArray(int chargeCount)
    {
        if (chargeCount <= 0)
            return Array.Empty<float>();

        var slots = new float[chargeCount];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = 0f;
        return slots;
    }

    private int GetFlameChargeReadyChargeCount()
    {
        int ready = 0;
        for (int i = 0; i < _flameChargePerChargeCooldownEnds.Length; i++)
        {
            if (Time.time >= _flameChargePerChargeCooldownEnds[i])
                ready++;
        }

        return ready;
    }

    private float GetFlameChargeSoonestRechargingChargeRemaining()
    {
        float soonest = float.MaxValue;
        for (int i = 0; i < _flameChargePerChargeCooldownEnds.Length; i++)
        {
            float remaining = _flameChargePerChargeCooldownEnds[i] - Time.time;
            if (remaining > 0f && remaining < soonest)
                soonest = remaining;
        }

        return soonest == float.MaxValue ? 0f : soonest;
    }

    private float GetFlameChargeCooldownDuration(AbilityDefinition def)
    {
        if (!def)
            def = GetAbilityDefinition(FlameChargeId);
        if (!def)
            return 12f;

        return Mathf.Max(0.01f, def.cooldown - GetPowerSlashCooldownReduction(def) - GetAvatarOfTheForestCooldownReduction(def));
    }

    private bool TryStartFlameChargeChargeCooldown(AbilityDefinition def)
    {
        if (def == null)
            return false;

        float cd = GetFlameChargeCooldownDuration(def);
        if (cd <= 0f)
            return true;

        float end = Time.time + cd;
        for (int i = 0; i < _flameChargePerChargeCooldownEnds.Length; i++)
        {
            if (Time.time < _flameChargePerChargeCooldownEnds[i])
                continue;

            _flameChargePerChargeCooldownEnds[i] = end;
            return true;
        }

        return false;
    }

    private float GetScaledFlameChargeFlatFire(AbilityDefinition def, float flatAmount)
    {
        if (!def || !stats || flatAmount <= 0f)
            return Mathf.Max(0f, flatAmount);

        float scaled = flatAmount * def.GetEffectiveAllDamageMultiplier() * Mathf.Max(0f, def.fireDamageMultiplier);
        scaled *= AbilityElementScaling.GetFireSkillDamageMultiplier(stats);
        return scaled;
    }

    private float GetFlameChargeTrailFlatFirePerTick(AbilityDefinition def)
    {
        _ = def;
        float tickCount = AbilityCombatPower.FlameChargeTrailDurationSeconds /
                          Mathf.Max(0.1f, AbilityCombatPower.FlameChargeTrailTickIntervalSeconds);
        return AbilityCombatPower.FlameChargeTrailTotalFlatFireDamage / Mathf.Max(1f, tickCount);
    }

    private void PruneFlameChargeTrailEnemyTracking()
    {
        if (_flameChargeTrailEnemyNextTickAt.Count > 0)
        {
            _flameChargeTrailScratchEnemies.Clear();
            foreach (KeyValuePair<EnemyBaseController, float> kv in _flameChargeTrailEnemyNextTickAt)
            {
                if (!kv.Key || kv.Key.IsDead)
                    _flameChargeTrailScratchEnemies.Add(kv.Key);
            }

            for (int i = 0; i < _flameChargeTrailScratchEnemies.Count; i++)
                _flameChargeTrailEnemyNextTickAt.Remove(_flameChargeTrailScratchEnemies[i]);
        }

    }

    private bool TryConsumeFlameChargeTrailTickForEnemy(EnemyBaseController enemy, float tickIntervalSeconds)
    {
        if (!enemy)
            return false;

        float now = Time.time;
        if (_flameChargeTrailEnemyNextTickAt.TryGetValue(enemy, out float nextAllowed) && now < nextAllowed)
            return false;

        _flameChargeTrailEnemyNextTickAt[enemy] = now + Mathf.Max(0.1f, tickIntervalSeconds);
        return true;
    }

    private bool TryApplyFlameChargeBurnFromFireHit(
        EnemyBaseController enemy,
        float fireDamageDealt,
        float applyChance = -1f)
    {
        if (!enemy || stats == null || fireDamageDealt <= 0f)
            return false;

        float resolvedApplyChance = applyChance >= 0f ? Mathf.Clamp01(applyChance) : stats.BurnApplyChance;
        if (resolvedApplyChance <= 0f)
            return false;

        AilmentController ailments = enemy.GetComponent<AilmentController>();
        if (ailments == null)
            return false;

        return ailments.TryApplyBurnFromFireHit(
            fireDamageDealt,
            resolvedApplyChance,
            stats.BurnExplosionMultiplier,
            transform,
            burnTickIntervalSeconds: stats.BurnTickIntervalSeconds);
    }

    private int GetFlameChargeSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.FlameChargeEnhancementParentSpineNodeId,
            -1);
    }

    private IEnumerator CoFlameCharge(AbilityDefinition def)
    {
        if (!def || player == null || stats == null)
        {
            _flameChargeRoutine = null;
            yield break;
        }

        if (!abilityVfx)
            abilityVfx = GetComponent<PlayerAbilityVfxController>();

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        int castId = ++_flameChargeCastCounter;
        float facing = player.FacingDirectionX;
        if (Mathf.Approximately(facing, 0f))
            facing = GetCombatFacingSign();
        if (Mathf.Approximately(facing, 0f))
            facing = 1f;
        Vector3 start = player.transform.position;
        float dashDist = AbilityCombatPower.FlameChargeDashDistance;
        float dashDuration = Mathf.Max(0.05f, AbilityCombatPower.FlameChargeDashDurationSeconds);
        float laneRefX = start.x;
        Vector3 end = start + new Vector3(facing * dashDist, 0f, 0f);
        end.x = player.ClampWorldXForLaneAt(laneRefX, end.x);
        int enhance = GetFlameChargeSelectedChoice();
        bool volcanic = enhance == 1;
        bool blockOverlappingTrails = enhance == 0;

        player.InterruptForSprintDash();
        IsFlameChargeDashing = true;
        player.SetTeleportDamageImmune(true);
        player.TriggerAttackAnimVisualOnly();
        abilityVfx?.BeginFlameChargePlayerGlow();
        Vector3 trailStart = LaneGroundEffectPlacement.SnapWorldPointToLaneFloor(start, 0.1f);
        Vector3 trailEnd = LaneGroundEffectPlacement.SnapWorldPointToLaneFloor(end, 0.1f);
        abilityVfx?.SpawnFlameChargeDashTrailVisual(
            trailStart,
            trailEnd,
            AbilityCombatPower.FlameChargeTrailDurationSeconds);

        float traveled = 0f;
        float nextTrailAt = 0f;

        try
        {
            for (float t = 0f; t < dashDuration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / dashDuration);
                Vector3 pos = Vector3.Lerp(start, end, u);
                player.SetHorizontalPositionForScriptedMove(pos.x, facing, laneRefX);

                traveled = Vector3.Distance(start, pos);
                while (traveled >= nextTrailAt)
                {
                    TrySpawnFlameChargeTrailSegment(def, castId, start + new Vector3(facing * nextTrailAt, 0f, 0f), blockOverlappingTrails);
                    nextTrailAt += AbilityCombatPower.FlameChargeTrailSegmentSpacing;
                }

                yield return null;
            }

            player.SetHorizontalPositionForScriptedMove(end.x, facing, laneRefX);
            while (traveled >= nextTrailAt - 0.001f)
            {
                TrySpawnFlameChargeTrailSegment(def, castId, start + new Vector3(facing * nextTrailAt, 0f, 0f), blockOverlappingTrails);
                nextTrailAt += AbilityCombatPower.FlameChargeTrailSegmentSpacing;
            }

            if (volcanic)
                ApplyFlameChargeVolcanicExplosion(def, end);
        }
        finally
        {
            abilityVfx?.EndFlameChargePlayerGlow();
            player.SetTeleportDamageImmune(false);
            IsFlameChargeDashing = false;
            _flameChargeRoutine = null;
        }
    }

    private void TrySpawnFlameChargeTrailSegment(
        AbilityDefinition def,
        int castId,
        Vector3 worldPos,
        bool blockOverlappingTrailsFromOtherDashes)
    {
        if (def == null)
            return;

        float radius = AbilityCombatPower.FlameChargeTrailRadius;
        if (blockOverlappingTrailsFromOtherDashes && FlameChargeTrailSegment.WouldOverlapOtherDash(worldPos, radius, castId))
            return;

        float flatFirePerTick = GetFlameChargeTrailFlatFirePerTick(def);
        FlameChargeTrailSegment.Spawn(
            this,
            def,
            castId,
            worldPos,
            AbilityCombatPower.FlameChargeTrailDurationSeconds,
            radius,
            AbilityCombatPower.FlameChargeTrailTickIntervalSeconds,
            flatFirePerTick,
            visualRoot: null);
    }

    public void TickFlameChargeTrailSegmentDamage(
        AbilityDefinition def,
        Vector3 center,
        float radius,
        float flatFirePerTick)
    {
        if (def == null || stats == null || flatFirePerTick <= 0f)
            return;

        PruneFlameChargeTrailEnemyTracking();
        float tickInterval = AbilityCombatPower.FlameChargeTrailTickIntervalSeconds;
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            float dx = Mathf.Abs(enemy.transform.position.x - center.x);
            float dy = Mathf.Abs(enemy.transform.position.y - center.y);
            if (dx > radius || dy > radius * 0.85f)
                continue;

            if (!TryConsumeFlameChargeTrailTickForEnemy(enemy, tickInterval))
                continue;

            ApplyFlameChargeFlatFireHit(def, enemy, flatFirePerTick, tryBurn: true);
        }
    }

    private void ApplyFlameChargeVolcanicExplosion(AbilityDefinition def, Vector3 impactPoint)
    {
        if (def == null || stats == null)
            return;

        abilityVfx?.SpawnFlameChargeVolcanicBurst(impactPoint);

        float radius = AbilityCombatPower.FlameChargeVolcanicExplosionRadius;
        float flatFire = GetScaledFlameChargeFlatFire(def, AbilityCombatPower.FlameChargeVolcanicExplosionFlatFireDamage);
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            float dx = Mathf.Abs(enemy.transform.position.x - impactPoint.x);
            float dy = Mathf.Abs(enemy.transform.position.y - impactPoint.y);
            if (dx > radius || dy > radius)
                continue;

            SplitDamage fireHit = new SplitDamage(0f, flatFire, 0f);
            DealtHit dealt = ApplyAbilitySplitDamageToEnemy(enemy, def, fireHit, false, 0f);
            if (dealt.Total > 0f)
                TryApplyFlameChargeBurnFromFireHit(enemy, dealt.magic, 1f);
        }
    }

    private void ApplyFlameChargeFlatFireHit(
        AbilityDefinition def,
        EnemyBaseController enemy,
        float flatFireAmount,
        bool tryBurn)
    {
        if (!enemy || enemy.IsDead || def == null || stats == null || flatFireAmount <= 0f)
            return;

        SplitDamage fireHit = new SplitDamage(0f, flatFireAmount, 0f);
        DealtHit dealt = ApplyAbilitySplitDamageToEnemy(enemy, def, fireHit, false, 0f);
        if (tryBurn && dealt.magic > 0f)
            TryApplyFlameChargeBurnFromFireHit(enemy, dealt.magic);

        if (player != null && dealt.Total > 0f)
            player.ApplyLifeSteal(dealt.Total);
    }

    private void OnDestroy()
    {
        if (_flameChargeRoutine != null)
        {
            StopCoroutine(_flameChargeRoutine);
            _flameChargeRoutine = null;
        }

        IsFlameChargeDashing = false;

        if (_activeSoulforgedWeaponMinions.Count > 0)
        {
            if (_soulforgedWeaponCooldownAbilityDef)
                s_persistedSoulforgedWeaponCooldownAbilityId = _soulforgedWeaponCooldownAbilityDef.abilityId;

            for (int i = _activeSoulforgedWeaponMinions.Count - 1; i >= 0; i--)
            {
                SoulforgedWeaponMinion minion = _activeSoulforgedWeaponMinions[i];
                if (!minion)
                    continue;

                minion.BindReleasedCallback(null);
                minion.PersistAcrossSceneLoads();
            }

            _activeSoulforgedWeaponMinions.Clear();
        }

        if (_activeSoulforgedWarriorMinions.Count > 0)
        {
            if (_soulforgedWarriorCooldownAbilityDef)
                s_persistedSoulforgedWarriorCooldownAbilityId = _soulforgedWarriorCooldownAbilityDef.abilityId;

            for (int i = _activeSoulforgedWarriorMinions.Count - 1; i >= 0; i--)
            {
                SoulforgedWarriorMinion minion = _activeSoulforgedWarriorMinions[i];
                if (!minion)
                    continue;

                minion.BindReleasedCallback(null);
                minion.PersistAcrossSceneLoads();
            }

            _activeSoulforgedWarriorMinions.Clear();
        }

        if (_activeHawkCompanionMinions.Count > 0)
        {
            if (_hawkCompanionCooldownAbilityDef)
                s_persistedHawkCompanionCooldownAbilityId = _hawkCompanionCooldownAbilityDef.abilityId;

            for (int i = _activeHawkCompanionMinions.Count - 1; i >= 0; i--)
            {
                HawkCompanionMinion minion = _activeHawkCompanionMinions[i];
                if (!minion)
                    continue;

                minion.BindReleasedCallback(null);
                minion.PersistAcrossSceneLoads();
            }

            _activeHawkCompanionMinions.Clear();
        }
    }
}

