using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

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
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;

    [Header("Global Cooldown")]
    [SerializeField, Min(0f)] private float globalCooldownSeconds = 0.15f;
    private float _globalCooldownEndsAt;

    [Header("Power Slash VFX")]
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

    [Header("Whirlwind VFX")]
    [SerializeField] private Color whirlingBladeColor = new Color(1f, 0.88f, 0.22f, 0.95f);
    [SerializeField, Min(0.01f)] private float whirlingBladeDuration = 0.22f;
    [SerializeField, Min(90f)] private float whirlingBladeSpinDegrees = 720f;
    [SerializeField, Min(0.01f)] private float whirlingBladeLineWidth = 0.14f;
    [SerializeField] private Vector3 whirlingBladeCenterOffset = new Vector3(0f, 0.65f, 0f);
    [SerializeField, Min(0f)] private float whirlingBladeUpwardDrift = 0.14f;
    [SerializeField, Min(0f)] private float whirlingBladeVerticalWave = 0.06f;

    [Header("Crescent Slash VFX")]
    [SerializeField] private Color crescentSlashColor = new Color(0.55f, 0.95f, 1f, 0.9f);
    [SerializeField, Min(0.05f)] private float crescentSlashVfxDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float crescentSlashLineWidth = 0.12f;
    [SerializeField] private Vector3 crescentSlashCenterOffset = new Vector3(0f, 0.65f, 0f);

    private readonly Dictionary<string, float> _cooldownEndsById = new(StringComparer.OrdinalIgnoreCase);
    private const string PowerSlashId = "power_slash";
    private const string WhirlwindId = "whirlwind";
    private const string RendId = "rend";
    private const string VenomJabId = "venom_jab";
    private const string CleavingStrikesId = "cleaving_strikes";
    private const string CrescentSlashId = "crescent_slash";
    private const int WhirlwindChoiceSourceLevel = 15;
    private const float WhirlwindBaseRadius = 2.5f;
    private const float WhirlwindDamageMultiplier = 1.2f;
    private static readonly float WhirlwindSecondHitMultiplier = AbilityCombatPower.WhirlwindTwinCycloneSecondHitFraction;
    private const float WhirlwindTwinCycloneSecondHitDelay = 0.5f;
    private const float WhirlwindRadiusBonus = 3f;
    private bool _powerSlashQueued;
    private bool _rendQueued;
    private bool _venomJabQueued;
    private bool _crescentSlashQueued;
    private int _cleavingHitsRemaining;
    private int _cleavingAdditionalTargets;
    private float _cleavingBuffEndsAt;
    private bool _cleavingBuffActive;
    private float _queuedPowerSlashPhysicalMultiplier = 1f;
    private float _queuedPowerSlashMagicMultiplier = 1f;
    private float _queuedPowerSlashAbilityPowerMultiplier;
    private QueuedHitEffect _queuedConsumedThisHit;
    private int _queuedConsumedFrame = -1;

    private enum QueuedHitEffect
    {
        None,
        PowerSlash,
        Rend,
        VenomJab,
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
        if (!combat) combat = GetComponent<PlayerCombatController>();
        if (!equipment) equipment = GetComponent<EquipmentManager>();
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!abilityDatabase) abilityDatabase = AbilityDatabase.LoadDefault();
        if (!skillDatabase) skillDatabase = SkillDatabase.LoadDefault();
        if (!skillsManager) skillsManager = SkillsManager.Instance;
    }

    private void Update()
    {
        TryAutoReleaseQueuedCrescentSlash();
    }

    public bool IsOnCooldown(string abilityId, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (!_cooldownEndsById.TryGetValue(abilityId, out float end))
            return false;

        remainingSeconds = Mathf.Max(0f, end - Time.time);
        return remainingSeconds > 0f;
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

    public bool TryUseAbility(string abilityId, bool showLockedFeedback = true)
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

        if (globalCooldownSeconds > 0f && Time.time < _globalCooldownEndsAt)
            return false;

        if (IsOnCooldown(def.abilityId, out _))
            return false;

        if (!player || !stats)
            return false;

        if (!CanUseWithEquippedWeapon(def))
        {
            player.ShowPopup("Ability cant be used with this weapon");
            return false;
        }

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
        if (string.Equals(def.abilityId, VenomJabId, StringComparison.OrdinalIgnoreCase))
        {
            if (_venomJabQueued)
                return false;
        }
        if (string.Equals(def.abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase))
        {
            if (_crescentSlashQueued)
                return false;
        }

        if (def.energyCost > 0f && !player.SpendEnergy(def.energyCost))
        {
            player.ShowPopup("Not enough energy.");
            return false;
        }

        if (string.Equals(def.abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
        {
            if (_powerSlashQueued)
                return false;

            _powerSlashQueued = true;
            float powerSlashPhysicalBonus = GetPowerSlashPhysicalMultiplierBonus();
            _queuedPowerSlashPhysicalMultiplier = def.physicalDamageMultiplier + powerSlashPhysicalBonus;
            _queuedPowerSlashMagicMultiplier = def.magicDamageMultiplier;
            _queuedPowerSlashAbilityPowerMultiplier = def.abilityPowerMultiplier;
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

        if (string.Equals(def.abilityId, VenomJabId, StringComparison.OrdinalIgnoreCase))
        {
            if (_venomJabQueued)
                return false;
            _venomJabQueued = true;
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
        if (string.Equals(def.abilityId, CrescentSlashId, StringComparison.OrdinalIgnoreCase))
        {
            bool castNow = combat != null && combat.TryConsumeAttackCycleForAbilityCast();
            if (castNow)
            {
                ExecuteCrescentSlashCast(def);
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

        float scaledPhysical = basePhysical * def.physicalDamageMultiplier;
        float scaledMagic = baseMagic * def.magicDamageMultiplier;
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        AbilityElementScaling.ScaleMagicAbilityContributions(scaledMagic, elementBonus, stats, out float magScaled, out float elemScaled);
        float apM = stats.GetAbilityPowerDamageMultiplier(def.abilityPowerMultiplier);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float physPart = scaledPhysical * apM;
        float magPart = (magScaled + elemScaled + ailmentBonus) * apM;
        float raw = Mathf.Max(0f, physPart + magPart);

        bool wasCrit = false;
        float critMult = 1f;
        if (raw > 0f && UnityEngine.Random.value < Mathf.Clamp01(stats.CritChance))
        {
            wasCrit = true;
            critMult = Mathf.Max(1f, stats.CritMultiplier);
        }

        int phys = Mathf.Max(0, Mathf.RoundToInt(physPart * critMult));
        int mag = Mathf.Max(0, Mathf.RoundToInt(magPart * critMult));
        int dealt = 0;
        if (phys > 0)
            dealt += target.TakeDamage(phys, DamageType.Physical, wasCrit, transform);
        if (mag > 0)
            dealt += target.TakeDamage(mag, DamageType.Magic, wasCrit, transform);

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
        public float Total => physical + magic + corruptionDamage;
    }

    /// <summary>Builds one independent ability hit roll (per target): attack roll + ability scaling + independent crit.</summary>
    private void BuildWhirlwindAbilityScaledSplit(AbilityDefinition def, out SplitDamage nonCritBase, out bool wasCrit)
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

        float scaledPhysical = basePhysical * def.physicalDamageMultiplier;
        float scaledMagic = baseMagic * def.magicDamageMultiplier;
        // Corruption scales like the physical multiplier so poison can still proc from ability hits.
        float scaledCorruption = baseCorruption * def.physicalDamageMultiplier;
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        AbilityElementScaling.ScaleMagicAbilityContributions(scaledMagic, elementBonus, stats, out float magScaled, out float elemScaled);
        float apM = stats.GetAbilityPowerDamageMultiplier(def.abilityPowerMultiplier);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);

        float physPart = scaledPhysical * apM;
        float magPart = (magScaled + elemScaled + ailmentBonus) * apM;
        float corruptionPart = scaledCorruption * apM;

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
        // First unlock acts as default branch until player explicitly chooses the other option.
        bool twinCyclone = selectedChoice == 0 || selectedChoice < 0;
        bool expansiveWhirl = selectedChoice == 1;

        float baseWeaponRange = GetWhirlwindBaseRange();
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

        var secondWaveTargets = new List<(EnemyBaseController target, SplitDamage secondHitBase)>(targets.Count);

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyBaseController target = targets[i];
            if (!target || target.IsDead)
                continue;

            BuildWhirlwindAbilityScaledSplit(def, out SplitDamage rolledNonCrit, out bool wasCrit);
            float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
            SplitDamage rolled = new SplitDamage(
                rolledNonCrit.physical * critMult,
                rolledNonCrit.magic * critMult,
                rolledNonCrit.corruptionDamage * critMult);
            SplitDamage firstHit = rolled * WhirlwindDamageMultiplier;
            SplitDamage secondHitBase = rolledNonCrit * WhirlwindDamageMultiplier * WhirlwindSecondHitMultiplier;

            DealtHit dealt = ApplySplitDamageToEnemy(target, firstHit, wasCrit);
            ApplyOnHitEffects(target, dealt);
            secondWaveTargets.Add((target, secondHitBase));
        }

        if (twinCyclone)
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

        forwardHits.Sort((a, b) => a.dist.CompareTo(b.dist));
        int cap = penetrating ? forwardHits.Count : Mathf.Min(3, forwardHits.Count);
        for (int i = 0; i < cap; i++)
        {
            EnemyBaseController target = forwardHits[i].enemy;
            if (!target || target.IsDead)
                continue;

            BuildWhirlwindAbilityScaledSplit(def, out SplitDamage rolledNonCrit, out bool wasCrit);
            float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
            SplitDamage hitForTarget = new SplitDamage(
                rolledNonCrit.physical * critMult,
                rolledNonCrit.magic * critMult,
                rolledNonCrit.corruptionDamage * critMult);
            CrescentConvertedElement convertedElement = CrescentConvertedElement.Lightning;
            float convertedDamage = 0f;
            bool hasConvertedDamage = elementalCrescent &&
                                      TryApplyElementalConversionForCrescent(ref hitForTarget, out convertedElement, out convertedDamage);

            DealtHit dealt = ApplySplitDamageToEnemy(target, hitForTarget, wasCrit);
            ApplyOnHitEffects(target, dealt);
            if (hasConvertedDamage)
                ApplyElementalAilmentForCrescent(target, convertedElement, convertedDamage);
        }
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

    private IEnumerator ApplyTwinCycloneSecondWave(List<(EnemyBaseController target, SplitDamage secondHitBase)> targets, float radius)
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
            bool secondWasCrit = TryRollIndependentCrit(ref secondHit);
            DealtHit dealtSecond = ApplySplitDamageToEnemy(target, secondHit, secondWasCrit);
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

        // Primary key: source unlock level (Lv15).
        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, WhirlwindChoiceSourceLevel, -1);
        if (selected >= 0)
            return selected;

        // Compatibility fallback: some earlier data/UI setups may key by choice unlock row (Lv18).
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

    private DealtHit ApplySplitDamageToEnemy(EnemyBaseController target, SplitDamage hit, bool wasCrit)
    {
        DealtHit result = default;
        if (target == null || target.IsDead)
            return result;

        float cond = GetConditionalMeleeDamageMultiplier(target);
        float phys = Mathf.Max(0f, hit.physical * cond);
        float mag = Mathf.Max(0f, hit.magic * cond);
        float corrRaw = Mathf.Max(0f, hit.corruptionDamage * cond);

        if (phys > 0f)
            result.physical = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(phys), DamageType.Physical, wasCrit, transform));
        if (mag > 0f)
            result.magic = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(mag), DamageType.Magic, wasCrit, transform));
        if (corrRaw > 0f)
            result.corruptionDamage = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(corrRaw), DamageType.Corruption, wasCrit, transform));

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

        if (dealt.Total > 0f && stats.MeleeShockChance > 0f && UnityEngine.Random.value <= stats.MeleeShockChance)
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

            float physicalBonus = rolled.physical * _queuedPowerSlashPhysicalMultiplier;
            float magicBonus = rolled.magic * _queuedPowerSlashMagicMultiplier;
            float apM = stats != null ? stats.GetAbilityPowerDamageMultiplier(_queuedPowerSlashAbilityPowerMultiplier) : 1f;
            AbilityDefinition slashDef = GetAbilityDefinition(PowerSlashId);
            float elementBonus = slashDef != null && stats != null ? AbilityElementScaling.GetElementDamageBonus(slashDef, stats) : 0f;
            float ailmentBonus = slashDef != null && stats != null ? AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(slashDef, stats) : 0f;

            rolled.physical += (physicalBonus + ailmentBonus) * apM;
            rolled.magic += (magicBonus + elementBonus) * apM;
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

        if (_venomJabQueued)
        {
            _venomJabQueued = false;
            _queuedConsumedThisHit = QueuedHitEffect.VenomJab;
            _queuedConsumedFrame = Time.frameCount;

            const float quickStrikeMultiplier = 0.75f;
            rolled.physical *= quickStrikeMultiplier;
            rolled.magic *= quickStrikeMultiplier;
            rolled.corruptionDamage *= quickStrikeMultiplier;
            return true;
        }

        return false;
    }

    private void TryAutoReleaseQueuedCrescentSlash()
    {
        if (!_crescentSlashQueued)
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

        ExecuteCrescentSlashCast(def);
    }

    /// <summary>
    /// Called from combat cadence; guarantees queued Crescent Slash gets priority over normal auto attacks.
    /// </summary>
    public bool TryAutoReleaseQueuedCrescentSlashFromCadence()
    {
        if (!_crescentSlashQueued)
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

        ExecuteCrescentSlashCast(def);
        return true;
    }

    private void ExecuteCrescentSlashCast(AbilityDefinition def)
    {
        _crescentSlashQueued = false;
        TryUseCrescentSlash(def);
        player?.TriggerAttackAnim();
        StartCooldown(def);
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

        // Only apply once, and only for the same frame that consumed the queued modifier.
        if (_queuedConsumedFrame != Time.frameCount)
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
        else if (_queuedConsumedThisHit == QueuedHitEffect.VenomJab)
        {
            result.suppressDefaultPoison = true;
            TryApplyVenomJabPoison(target, corruptionDealtPostMitigation);

            AbilityDefinition def = GetAbilityDefinition(VenomJabId);
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

        bool wasAlreadyBleeding = ailments.HasBleed;

        // Exclusive bleed: blocks other bleed applications while active; also does not override normal bleed.
        ailments.ApplyExclusiveBleedFromHit(new BleedPayload(totalDamage, duration, ticks, transform));

        if (selected == 1 && wasAlreadyBleeding)
        {
            // Upgrade 2: If target already bleeding, spread this bleed to 1 nearby enemy.
            EnemyBaseController spread = FindNearestLivingEnemyExcluding(target, range: 3f);
            if (spread != null)
            {
                AilmentController otherAilments = spread.GetComponent<AilmentController>();
                if (otherAilments != null)
                    otherAilments.ApplyExclusiveBleedFromHit(new BleedPayload(totalDamage, duration, ticks, transform));
            }
        }
    }

    private void TryApplyVenomJabPoison(EnemyBaseController target, float corruptionDealtPostMitigation)
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

        int selected = GetVenomJabSelectedChoice();

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
            // Upgrade 2: Poison spreads to 1 nearby enemy if it dies within 6 seconds.
            var marker = target.GetComponent<VenomJabSpreadOnDeathMarker>();
            if (marker == null)
                marker = target.gameObject.AddComponent<VenomJabSpreadOnDeathMarker>();
            marker.Arm(payload, expiresAt: Time.time + 6f, range: 3f);
        }
    }

    private EnemyBaseController FindNearestLivingEnemyExcluding(EnemyBaseController exclude, float range)
    {
        EnemyBaseController[] allEnemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        EnemyBaseController best = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 origin = exclude != null ? exclude.transform.position : transform.position;
        float r2 = Mathf.Max(0f, range) * Mathf.Max(0f, range);

        for (int i = 0; i < allEnemies.Length; i++)
        {
            EnemyBaseController e = allEnemies[i];
            if (e == null || e.IsDead || e == exclude)
                continue;
            float sqr = (e.transform.position - origin).sqrMagnitude;
            if (sqr <= r2 && sqr < bestSqr)
            {
                bestSqr = sqr;
                best = e;
            }
        }

        return best;
    }

    private void ActivateCleavingStrikesBuff()
    {
        int selected = GetCleavingStrikesSelectedChoice();
        _cleavingBuffActive = true;
        if (selected == 0)
        {
            // Greater Cleave: primary + 3 extra = up to 4 enemies per swing (cleave hits use reduced damage).
            _cleavingAdditionalTargets = 3;
            _cleavingHitsRemaining = 4;
            _cleavingBuffEndsAt = Time.time + 8f;
        }
        else if (selected == 1)
        {
            // Lasting Momentum
            _cleavingAdditionalTargets = 1;
            _cleavingHitsRemaining = 7;
            _cleavingBuffEndsAt = Time.time + 14f;
        }
        else
        {
            // Base
            _cleavingAdditionalTargets = 1;
            _cleavingHitsRemaining = 5;
            _cleavingBuffEndsAt = Time.time + 10f;
        }
    }

    private void CleanupCleavingStrikesIfExpired()
    {
        if (!_cleavingBuffActive)
            return;

        // Cleaving should only end after BOTH constraints are satisfied:
        // - required hit count has been consumed
        // - duration window has elapsed
        bool hitsConsumed = _cleavingHitsRemaining <= 0;
        bool durationElapsed = Time.time >= _cleavingBuffEndsAt;
        if (hitsConsumed && durationElapsed)
        {
            _cleavingBuffActive = false;
            _cleavingAdditionalTargets = 0;
            _cleavingHitsRemaining = 0;
            _cleavingBuffEndsAt = 0f;
        }
    }

    /// <summary>
    /// Called on successful primary hit release. Returns whether cleaving is active and how many extra targets to attempt.
    /// Hit count is consumed only when there was a valid successful release.
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

        _cleavingHitsRemaining = Mathf.Max(0, _cleavingHitsRemaining - 1);
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
            return _cleavingBuffActive ? Mathf.Max(0, _cleavingHitsRemaining) : 0;
        }

        return 0;
    }

    /// <summary>
    /// Scales Cleaving Strikes secondary hits from the base rolled attack split.
    /// Main hit remains unchanged; only bonus cleave hits use this scaled split.
    /// </summary>
    public SplitDamage BuildCleavingSecondarySplit(SplitDamage baseRolled)
    {
        AbilityDefinition def = GetAbilityDefinition(CleavingStrikesId);
        if (def == null)
            return baseRolled;

        float physMult = def.physicalDamageMultiplier;
        float magMult = def.magicDamageMultiplier;
        float apM = stats != null ? stats.GetAbilityPowerDamageMultiplier(def.abilityPowerMultiplier) : 1f;

        return new SplitDamage(
            baseRolled.physical * physMult * apM,
            baseRolled.magic * magMult * apM,
            baseRolled.corruptionDamage * physMult * apM
        );
    }

    private int GetCleavingStrikesSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 15, -1);
    }

    private int GetCrescentSlashSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 15, -1);
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

    private int GetVenomJabSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        // Venom Jab enhancement selection is keyed on the level-5 ability row.
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
        if (string.Equals(abilityId, VenomJabId, StringComparison.OrdinalIgnoreCase))
            return _venomJabQueued;
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
        _cooldownEndsById[def.abilityId] = Time.time + cd;
    }

    private float GetPowerSlashPhysicalMultiplierBonus()
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
        return selected == 1 ? 5f : 0f;
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
            _ => true
        };
    }
}

