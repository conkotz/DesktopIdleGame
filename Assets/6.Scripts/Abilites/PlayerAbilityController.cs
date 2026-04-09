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

    [Header("Whirling Blade VFX")]
    [SerializeField] private Color whirlingBladeColor = new Color(1f, 0.88f, 0.22f, 0.95f);
    [SerializeField, Min(0.01f)] private float whirlingBladeDuration = 0.22f;
    [SerializeField, Min(90f)] private float whirlingBladeSpinDegrees = 720f;
    [SerializeField, Min(0.01f)] private float whirlingBladeLineWidth = 0.14f;
    [SerializeField] private Vector3 whirlingBladeCenterOffset = new Vector3(0f, 0.65f, 0f);
    [SerializeField, Min(0f)] private float whirlingBladeUpwardDrift = 0.14f;
    [SerializeField, Min(0f)] private float whirlingBladeVerticalWave = 0.06f;

    private readonly Dictionary<string, float> _cooldownEndsById = new(StringComparer.OrdinalIgnoreCase);
    private const string PowerSlashId = "power_slash";
    private const string WhirlingBladeId = "whirling_blade";
    private const int WhirlingBladeChoiceSourceLevel = 15;
    private const float WhirlingBladeBaseRadius = 2.5f;
    private const float WhirlingBladeDamageMultiplier = 1.2f;
    private const float WhirlingBladeSecondHitMultiplier = 0.2f;
    private const float WhirlingBladeTwinCycloneSecondHitDelay = 0.5f;
    private const float WhirlingBladeRadiusBonus = 3f;
    private bool _powerSlashQueued;
    private float _queuedPowerSlashPhysicalMultiplier = 1f;
    private float _queuedPowerSlashMagicalMultiplier = 1f;
    private float _queuedPowerSlashAbilityPowerMultiplier;

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
            _queuedPowerSlashPhysicalMultiplier = Mathf.Max(0f, def.physicalDamageMultiplier + powerSlashPhysicalBonus);
            _queuedPowerSlashMagicalMultiplier = Mathf.Max(0f, def.magicalDamageMultiplier);
            _queuedPowerSlashAbilityPowerMultiplier = Mathf.Max(0f, def.abilityPowerMultiplier);
            if (globalCooldownSeconds > 0f)
                _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
            return true;
        }

        if (string.Equals(def.abilityId, WhirlingBladeId, StringComparison.OrdinalIgnoreCase))
        {
            bool usedWhirl = TryUseWhirlingBlade(def);
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

        // Instant-cast damage model: physical + magical weapon averages, optional element lines, AP, small ailment hook.
        float basePhysical =
            (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        float baseMagical =
            (Mathf.Max(0f, stats.MinSplitDamage.magical) + Mathf.Max(0f, stats.MaxSplitDamage.magical)) * 0.5f;

        float scaledPhysical = basePhysical * Mathf.Max(0f, def.physicalDamageMultiplier);
        float scaledMagical = baseMagical * Mathf.Max(0f, def.magicalDamageMultiplier);
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float apBonus = Mathf.Max(0f, stats.AbilityPower * Mathf.Max(0f, def.abilityPowerMultiplier));
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float physPart = scaledPhysical + apBonus;
        float magPart = scaledMagical + elementBonus + ailmentBonus;
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
            dealt += target.TakeDamage(mag, DamageType.Magical, wasCrit, transform);

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
        public float magical;
        public float trueDamage;
        public float Total => physical + magical + trueDamage;
    }

    private bool TryUseWhirlingBlade(AbilityDefinition def)
    {
        if (stats == null)
            return false;

        int selectedChoice = GetWhirlingBladeSelectedChoice();
        // First unlock acts as default branch until player explicitly chooses the other option.
        bool twinCyclone = selectedChoice == 0 || selectedChoice < 0;
        bool expansiveWhirl = selectedChoice == 1;

        float baseWeaponRange = GetWhirlingBaseRange();
        float radius = baseWeaponRange + (expansiveWhirl ? WhirlingBladeRadiusBonus : 0f);

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
        SpawnWhirlingBladeVfx(radius);

        if (targets.Count <= 0)
            return true; // ability cast still consumes resources/cooldown.

        SplitDamage rolled = stats.RollSplitAttackDamage(out bool wasCrit);
        SplitDamage rolledNonCrit = rolled;
        if (wasCrit)
        {
            float critMult = Mathf.Max(1f, stats.CritMultiplier);
            if (critMult > 1f)
            {
                rolledNonCrit.physical /= critMult;
                rolledNonCrit.magical /= critMult;
            }
        }

        SplitDamage firstHit = rolled * WhirlingBladeDamageMultiplier;
        SplitDamage secondHitBase = rolledNonCrit * WhirlingBladeDamageMultiplier * WhirlingBladeSecondHitMultiplier;

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyBaseController target = targets[i];
            if (!target || target.IsDead)
                continue;

            DealtHit dealt = ApplySplitDamageToEnemy(target, firstHit, wasCrit);
            ApplyOnHitEffects(target, dealt);
        }

        if (twinCyclone)
            StartCoroutine(ApplyTwinCycloneSecondWave(new List<EnemyBaseController>(targets), secondHitBase, radius));

        return true;
    }

    private IEnumerator ApplyTwinCycloneSecondWave(List<EnemyBaseController> targets, SplitDamage secondHitBase, float radius)
    {
        yield return new WaitForSeconds(WhirlingBladeTwinCycloneSecondHitDelay);

        // Replay only the Whirling VFX; do not retrigger the attack animation on second wave.
        SpawnWhirlingBladeVfx(radius);

        if (targets == null || targets.Count == 0)
            yield break;

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyBaseController target = targets[i];
            if (!target || target.IsDead)
                continue;

            SplitDamage secondHit = secondHitBase;
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

    private float GetWhirlingBaseRange()
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

    private int GetWhirlingBladeSelectedChoice()
    {
        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
        if (skillsManager == null)
            return -1;

        // Primary key: source unlock level (Lv15).
        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, WhirlingBladeChoiceSourceLevel, -1);
        if (selected >= 0)
            return selected;

        // Compatibility fallback: some earlier data/UI setups may key by choice unlock row (Lv18).
        selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, WhirlingBladeChoiceSourceLevel + 3, -1);
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
        float mag = Mathf.Max(0f, hit.magical * cond);
        float tru = Mathf.Max(0f, hit.trueDamage * cond);

        if (phys > 0f)
            result.physical = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(phys), DamageType.Physical, wasCrit, transform));
        if (mag > 0f)
            result.magical = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(mag), DamageType.Magical, wasCrit, transform));
        if (tru > 0f)
            result.trueDamage = Mathf.Max(0f, target.TakeDamage(Mathf.RoundToInt(tru), DamageType.True, wasCrit, transform));

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
        if (hit.physical <= 0f && hit.magical <= 0f)
            return false;

        float critChance = Mathf.Clamp01(stats.CritChance);
        if (UnityEngine.Random.value > critChance)
            return false;

        float critMult = Mathf.Max(1f, stats.CritMultiplier);
        hit.physical *= critMult;
        hit.magical *= critMult;
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

        if (dealt.trueDamage > 0f && stats.PoisonChance > 0f && stats.PoisonMultiplier >= 0f && UnityEngine.Random.value <= stats.PoisonChance)
        {
            float totalPoisonDamage = dealt.trueDamage * (1f + stats.PoisonMultiplier);
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

    private void SpawnWhirlingBladeVfx(float radius)
    {
        Transform anchor = ResolvePowerSlashAnchor();
        Transform center = player != null ? player.transform : transform;
        if (center == null)
            return;

        if (anchor == null)
            anchor = center;

        GameObject orbitGO = new GameObject("WhirlingBladeTrailEmitter");
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

        StartCoroutine(AnimateWhirlingBladeTrail(orbitGO.transform, trail, center, radius, startDir));
    }

    private IEnumerator AnimateWhirlingBladeTrail(Transform emitter, TrailRenderer trail, Transform center, float radius, Vector2 startDir)
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
        if (!_powerSlashQueued || rolled.IsEmpty)
            return false;

        _powerSlashQueued = false;

        float physicalScaleBonus = Mathf.Max(0f, _queuedPowerSlashPhysicalMultiplier - 1f);
        float physicalBonus = Mathf.Max(0f, rolled.physical * physicalScaleBonus);
        float magScaleBonus = Mathf.Max(0f, _queuedPowerSlashMagicalMultiplier - 1f);
        float magicalBonus = Mathf.Max(0f, rolled.magical * magScaleBonus);
        float apBonus = Mathf.Max(0f, stats != null ? stats.AbilityPower * _queuedPowerSlashAbilityPowerMultiplier : 0f);
        AbilityDefinition slashDef = GetAbilityDefinition(PowerSlashId);
        float elementBonus = slashDef != null && stats != null ? AbilityElementScaling.GetElementDamageBonus(slashDef, stats) : 0f;
        float ailmentBonus = slashDef != null && stats != null ? AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(slashDef, stats) : 0f;
        float totalBonus = physicalBonus + magicalBonus + apBonus + elementBonus + ailmentBonus;

        if (totalBonus > 0f)
        {
            rolled.physical += physicalBonus + apBonus + ailmentBonus;
            rolled.magical += magicalBonus + elementBonus;
        }

        AbilityDefinition def = GetAbilityDefinition(PowerSlashId);
        if (def)
            StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;

        SpawnPowerSlashTrail();
        return true;
    }

    public bool IsAbilityPrimed(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (string.Equals(abilityId, PowerSlashId, StringComparison.OrdinalIgnoreCase))
            return _powerSlashQueued;

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

