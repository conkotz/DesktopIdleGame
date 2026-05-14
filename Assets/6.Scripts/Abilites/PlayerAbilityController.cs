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
    [SerializeField] private PlayerAbilityVfxController abilityVfx;

    [Header("Global Cooldown")]
    [SerializeField, Min(0f)] private float globalCooldownSeconds = 0.15f;
    private float _globalCooldownEndsAt;

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

    private bool _avatarOfForestActive;
    private float _avatarOfForestEndsAt;
    private float _avatarOfForestDuration;
    private float _lastSyncedAvatarOfForestHudEnd = float.NaN;
    /// <summary>When the Avatar buff expires, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _avatarOfForestCooldownAbilityDef;
    private float _avatarOfForestReplenishAccum;

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
        if (!abilityVfx) abilityVfx = GetComponent<PlayerAbilityVfxController>();
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
        abilityVfx?.DestroyLumberFrenzyOrbitVfx();
        abilityVfx?.DestroyAvatarOfTheForestGlowVfx();
    }

    private void Update()
    {
        TryAutoReleaseQueuedCrescentSlash();
        CleanupCleavingStrikesIfExpired();
        SyncCleavingStrikesHudBuff();
        CleanupLumberFrenzyIfExpired();
        abilityVfx?.UpdateLumberFrenzyOrbitVfx(IsLumberFrenzyActive);
        SyncLumberFrenzyHudBuff();
        CleanupCleavingChopIfExpired();
        SyncCleavingChopHudBuff();
        abilityVfx?.UpdateCleavingChopRangeIndicator(IsCleavingChopActive, GetCleavingChopRange());
        CleanupAvatarOfTheForestIfExpired();
        abilityVfx?.UpdateAvatarOfTheForestGlowVfx(IsAvatarOfTheForestActive);
        SyncAvatarOfTheForestHudBuff();
        TickAvatarOfTheForestNearbyReplenish(Time.deltaTime);
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
        if (string.Equals(abilityId, CleavingChopId, StringComparison.OrdinalIgnoreCase))
            return IsCleavingChopActive;
        if (string.Equals(abilityId, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase))
            return IsAvatarOfTheForestActive;
        if (string.Equals(abilityId, SpectralAxeId, StringComparison.OrdinalIgnoreCase))
            return IsSpectralAxeActive;
        if (string.Equals(abilityId, CleavingStrikesId, StringComparison.OrdinalIgnoreCase))
            return _cleavingBuffActive;
        if (string.Equals(abilityId, AbilityCombatPower.SoulforgedWeaponAbilityId, StringComparison.OrdinalIgnoreCase))
            return _activeSoulforgedWeaponMinions.Count > 0;

        return false;
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

        if (string.Equals(abilityId, LumberFrenzyId, StringComparison.OrdinalIgnoreCase))
        {
            ForceEndLumberFrenzyEarly();
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

        ForceEndGenericHudAbilityBuffWithCooldown(abilityId);
    }

    /// <summary>Skill tree active overlay: seconds left on the HUD buff timer when applicable.</summary>
    public bool TryGetAbilitySkillTreeActiveBuffTimer(string abilityId, out float remainingSecondsForDisplay)
    {
        remainingSecondsForDisplay = 0f;
        if (!IsAbilityBuffOrLingeringActive(abilityId))
            return false;

        if (buffController != null &&
            buffController.ShouldDisplayHudAbilityBuffCountdown(abilityId, out float rem))
        {
            remainingSecondsForDisplay = rem;
            return true;
        }

        remainingSecondsForDisplay = 0f;
        return true;
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

    private void ForceEndLumberFrenzyEarly()
    {
        if (!_lumberFrenzyActive)
            return;

        _lumberFrenzyActive = false;
        _lumberFrenzyEndsAt = 0f;
        _lumberFrenzyDuration = 0f;

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

    private void LogAbilityUsed(AbilityDefinition def)
    {
        if (!def)
            return;
        string label = string.IsNullOrWhiteSpace(def.displayName) ? def.abilityId : def.displayName.Trim();
        GameLog.Add($"Ability used: {label}", GameLog.AbilityUsedColor);
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
            LogAbilityUsed(def);
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

        if (string.Equals(def.abilityId, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase) && IsAvatarOfTheForestActive)
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
            LogAbilityUsed(def);
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
            LogAbilityUsed(def);
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
        LogAbilityUsed(def);
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
        abilityVfx?.SpawnWhirlwind(radius);

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
        abilityVfx?.SpawnWhirlwind(radius);

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

            abilityVfx?.SpawnPowerSlashTrail();
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

    private float GetCombatFacingSign() =>
        abilityVfx != null ? abilityVfx.GetCombatFacingSign() : (player != null ? Mathf.Sign(player.transform.localScale.x >= 0f ? 1f : -1f) : 1f);

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
            if (buffController.IsHudAbilityBuffActive(CleavingStrikesId))
                buffController.ClearHudAbilityBuff(CleavingStrikesId);
            _lastSyncedCleavingHudStacks = int.MinValue;
            _lastSyncedCleavingHudEnd = float.NaN;
            return;
        }

        if (IsOnCooldown(CleavingStrikesId, out _))
        {
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

    private void ActivateLumberFrenzyBuff()
    {
        _lumberFrenzyActive = true;
        _lumberFrenzyDuration = LumberFrenzyDurationSeconds;
        _lumberFrenzyEndsAt = Time.time + _lumberFrenzyDuration;
        _lastSyncedLumberFrenzyHudEnd = float.NaN;
        abilityVfx?.SpawnLumberFrenzyOrbitVfx();
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

    private void ActivateAvatarOfTheForestBuff()
    {
        _avatarOfForestActive = true;
        float dur = AvatarOfTheForestBaseDurationSeconds;
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

        return skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv50_0", -1);
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

        abilityVfx?.EnsureSpectralAxeAreaIndicatorBuilt();
        abilityVfx?.UpdateSpectralAxeAreaIndicator(axeCenter, SpectralAxeAreaRadius);

        _spectralAxeGatherTarget = FindClosestWoodcuttingNodeInAxeArea(axeCenter);

        if (_spectralAxeGatherTarget == null)
        {
            // No tree under the axe → log, despawn, and apply the short missed-cast cooldown.
            GameLog.Add("No trees for spectral axe to gather from", GameLog.CannotMessageColor);
            _spectralAxeMissedCast = true;

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

            spinDeg += spinSign * spinRate * Time.deltaTime;
            projTr.rotation = Quaternion.Euler(0f, 0f, spinDeg);
            abilityVfx?.UpdateSpectralAxeAreaIndicator(axeCenter, SpectralAxeAreaRadius);

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

        bool countTowardDepletion = !IsAvatarOfTheForestActive;
        node.NotifyGatherTickBeforeBonuses(countTowardDepletion);

        int mainAmt = nodeDef.RollMainYieldAmount();
        if (node.ApplyDepletedYieldPenaltyThisTick)
            mainAmt = SpectralAxeRollDepletedYield(mainAmt, nodeDef.depletedYieldMultiplier);

        // Match Cleaving Chop: spectral gathers yield at 60% efficiency (stochastic so small rolls
        // like 1 log don't get stuck at 1; they average out to the efficiency over many ticks).
        mainAmt = RollSpectralAxeEfficiencyYield(mainAmt, SpectralAxeYieldEfficiency);

        if (mainAmt > 0)
            AddSpectralAxeLootToInventory(node, mainAmt, node.transform.position);

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

        if (string.Equals(id, LumberFrenzyId, StringComparison.OrdinalIgnoreCase) && _lumberFrenzyActive)
        {
            _lumberFrenzyActive = false;
            _lumberFrenzyEndsAt = 0f;
            _lumberFrenzyDuration = 0f;
            abilityVfx?.DestroyLumberFrenzyOrbitVfx();
            _lumberFrenzyCooldownAbilityDef = null;
            _lastSyncedLumberFrenzyHudEnd = float.NaN;
            buffController?.ClearHudAbilityBuff(LumberFrenzyId);
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
            _activeSoulforgedWeaponIsPersistent = false;
            _lastSyncedSoulforgedHudEnd = float.NaN;
            _lastSyncedSoulforgedHudStacks = int.MinValue;
            buffController?.ClearHudAbilityBuff(AbilityCombatPower.SoulforgedWeaponAbilityId);
        }
    }

    private void StartCooldown(AbilityDefinition def)
    {
        if (!def) return;
        float cd = Mathf.Max(0f, def.cooldown - GetPowerSlashCooldownReduction(def) - GetAvatarOfTheForestCooldownReduction(def));
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

    private float GetAvatarOfTheForestCooldownReduction(AbilityDefinition def)
    {
        if (def == null || !string.Equals(def.abilityId, AvatarOfTheForestId, StringComparison.OrdinalIgnoreCase))
            return 0f;
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return 0f;

        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv50_0", -1);
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
                    abilityVfx != null ? abilityVfx.SoulforgedWeaponMinionPresentation : default,
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
        abilityVfx?.DestroySpectralAxeAreaIndicator();
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

