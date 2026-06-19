using System.Collections.Generic;
using UnityEngine;

public partial class PlayerAbilityController
{
    private bool _lightningRodActive;
    private float _lightningRodEndsAt;
    private float _lightningRodDuration;
    private Vector3 _lightningRodAnchor;
    private float _lightningRodNextPeriodicArcAt;
    private float _lightningRodNextSurgeAt;
    private float _lastSyncedLightningRodHudEnd = float.NaN;
    private AbilityDefinition _lightningRodCooldownAbilityDef;
    private readonly List<EnemyBaseController> _lightningRodTargetScratch = new();
    private readonly HashSet<EnemyBaseController> _lightningRodHitScratch = new();

    private static bool IsLightningRodAbilityId(string abilityId) =>
        string.Equals(abilityId, AbilityCombatPower.LightningRodAbilityId, System.StringComparison.OrdinalIgnoreCase);

    public bool IsLightningRodActive => _lightningRodActive && Time.time < _lightningRodEndsAt;

    public float EstimateLightningDamagePortion(float physicalDealt, float magicDealt)
    {
        if (magicDealt <= 0f)
            return 0f;

        if (_staticArrowsAppliedThisHit)
            return magicDealt;

        if (stats != null
            && stats.CurrentAttackSkill == AttackSkill.Magic
            && stats.CurrentMagicAttackType == MagicAttackType.Lightning)
        {
            return magicDealt;
        }

        if (stats == null)
            return 0f;

        float frac = stats.GetMeleeMagicLightningFraction();
        if (frac <= 0f)
            return 0f;

        return magicDealt * frac;
    }

    public void TryLightningRodSurgeOnLightningHit(EnemyBaseController enemy, float lightningDealt)
    {
        if (!IsLightningRodActive || enemy == null || enemy.IsDead || lightningDealt <= 0f)
            return;

        if (!IsEnemyWithinLightningRodRange(enemy))
            return;

        if (Time.time < _lightningRodNextSurgeAt)
            return;

        _lightningRodNextSurgeAt = Time.time + AbilityCombatPower.LightningRodSurgeCooldownSeconds;
        FireLightningRodArcToEnemy(enemy, GetLightningRodVfxCenter(), applyShock: false);
    }

    private void BeginLightningRodCast(AbilityDefinition def)
    {
        _lightningRodCooldownAbilityDef = def;

        if (IsLightningRodActive)
            ForceEndLightningRodEarly(applyCooldown: false, awardDeferredCooldown: false, runExpiryChain: false);

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        float anchorX = transform.position.x;
        EnemyBaseController attackTarget = combat != null ? combat.CurrentTarget : null;
        if (attackTarget != null && !attackTarget.IsDead && attackTarget.gameObject.activeInHierarchy)
            anchorX = attackTarget.transform.position.x;

        Vector3 anchor = LaneGroundEffectPlacement.SnapWorldPointToLaneFloor(
            new Vector3(anchorX, 0f, 0f),
            0.08f);

        abilityVfx?.AnchorLightningRodAt(anchor);
        ActivateLightningRodAt(anchor);
    }

    private void ActivateLightningRodAt(Vector3 anchor)
    {
        _lightningRodActive = true;
        _lightningRodAnchor = anchor;
        _lightningRodDuration = AbilityCombatPower.LightningRodBaseDurationSeconds;
        _lightningRodEndsAt = Time.time + _lightningRodDuration;
        _lightningRodNextPeriodicArcAt = Time.time + GetLightningRodPeriodicInterval();
        _lightningRodNextSurgeAt = 0f;
        _lastSyncedLightningRodHudEnd = float.NaN;

        FireLightningRodPeriodicArcs();
        SyncLightningRodHudBuff();
    }

    private void ForceEndLightningRodEarly(
        bool applyCooldown,
        bool awardDeferredCooldown = true,
        bool runExpiryChain = true)
    {
        if (!_lightningRodActive)
        {
            abilityVfx?.StopLightningRodVfx();
            return;
        }

        AbilityDefinition defForCooldown = _lightningRodCooldownAbilityDef;
        _lightningRodCooldownAbilityDef = null;

        if (runExpiryChain)
            RunLightningRodExpiryChain();

        _lightningRodActive = false;
        _lightningRodEndsAt = 0f;
        _lightningRodDuration = 0f;
        abilityVfx?.StopLightningRodVfx();
        _lastSyncedLightningRodHudEnd = float.NaN;
        SyncLightningRodHudBuff();

        if (!awardDeferredCooldown)
            return;

        AbilityDefinition cooldownDef = applyCooldown
            ? defForCooldown ?? GetAbilityDefinition(AbilityCombatPower.LightningRodAbilityId)
            : defForCooldown;

        if (cooldownDef != null && cooldownDef.cooldown > 0f)
            StartCooldown(cooldownDef);
    }

    private void CleanupLightningRodIfExpired()
    {
        if (!_lightningRodActive)
            return;

        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
        {
            _lightningRodCooldownAbilityDef = null;
            ForceEndLightningRodEarly(applyCooldown: false, awardDeferredCooldown: false, runExpiryChain: false);
            return;
        }

        if (Time.time < _lightningRodEndsAt)
            return;

        ForceEndLightningRodEarly(applyCooldown: false, awardDeferredCooldown: true, runExpiryChain: true);
    }

    private void TickLightningRod()
    {
        if (!_lightningRodActive)
            return;

        abilityVfx?.MaintainLightningRodAt(_lightningRodAnchor);

        if (Time.time < _lightningRodNextPeriodicArcAt)
            return;

        _lightningRodNextPeriodicArcAt = Time.time + GetLightningRodPeriodicInterval();
        FireLightningRodPeriodicArcs();
    }

    private float GetLightningRodPeriodicInterval()
    {
        return GetLightningRodSelectedChoice() == AbilityCombatPower.LightningRodEnh1FasterArcsChoiceIndex
            ? AbilityCombatPower.LightningRodEnh1PeriodicArcIntervalSeconds
            : AbilityCombatPower.LightningRodPeriodicArcIntervalSeconds;
    }

    private void FireLightningRodPeriodicArcs()
    {
        CollectEnemiesNearLightningRod(
            _lightningRodTargetScratch,
            AbilityCombatPower.LightningRodArcRange,
            exclude: null,
            maxCount: AbilityCombatPower.LightningRodMaxPeriodicArcTargets);

        Vector3 from = GetLightningRodVfxCenter();
        for (int i = 0; i < _lightningRodTargetScratch.Count; i++)
        {
            EnemyBaseController enemy = _lightningRodTargetScratch[i];
            if (enemy == null || enemy.IsDead)
                continue;

            FireLightningRodArcToEnemy(enemy, from, applyShock: false);
        }
    }

    private void RunLightningRodExpiryChain()
    {
        if (GetLightningRodSelectedChoice() != AbilityCombatPower.LightningRodEnh2ExpiryChainChoiceIndex)
            return;

        _lightningRodHitScratch.Clear();
        EnemyBaseController current = FindNearestEnemyWithinRange(
            _lightningRodAnchor,
            AbilityCombatPower.LightningRodArcRange,
            _lightningRodHitScratch);
        if (current == null)
            return;

        FireLightningRodArcToEnemy(current, GetLightningRodVfxCenter(), applyShock: true);
        _lightningRodHitScratch.Add(current);

        while (true)
        {
            EnemyBaseController next = FindNearestEnemyWithinRange(
                current.transform.position,
                AbilityCombatPower.LightningRodArcRange,
                _lightningRodHitScratch);
            if (next == null)
                break;

            FireLightningRodArcToEnemy(next, GetEnemyVfxCenter(current), applyShock: true);
            _lightningRodHitScratch.Add(next);
            current = next;
        }
    }

    private void FireLightningRodArcToEnemy(
        EnemyBaseController enemy,
        Vector3 arcStart,
        bool applyShock)
    {
        if (enemy == null || enemy.IsDead)
            return;

        Vector3 arcEnd = GetEnemyVfxCenter(enemy);
        float arcDamage = ComputeLightningRodArcDamage();
        EnemyBaseController damageTarget = TornadoLightningRouter.RouteLightningArc(
            abilityVfx,
            arcStart,
            arcEnd,
            arcDamage,
            enemy.transform,
            enemy);

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null || damageTarget == null || damageTarget.IsDead)
            return;

        float damage = ComputeLightningRodArcDamage();
        SplitDamage rolled = new SplitDamage(0f, damage, 0f);
        combat.ApplyStaticArrowsCritArcDamage(
            damageTarget,
            rolled,
            wasCrit: false,
            AbilityCombatPower.LightningRodOutgoingDamageSourceLabel);

        if (!applyShock || stats == null)
            return;

        AilmentController ailments = damageTarget.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        ailments.ApplyShockFromHit(new ShockPayload(
            duration: stats.ShockDuration,
            damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
            source: transform));
    }

    private float ComputeLightningRodArcDamage()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;

        int rangedLevel = skillsManager != null ? skillsManager.GetLevel(SkillType.Ranged) : 1;
        AbilityCombatPower.GetLightningRodArcDamageBounds(rangedLevel, stats, out float min, out float max);
        return Mathf.Max(1f, Random.Range(min, max));
    }

    private Vector3 GetLightningRodVfxCenter()
    {
        return _lightningRodAnchor + new Vector3(0f, 0.75f, 0f);
    }

    private bool IsEnemyWithinLightningRodRange(EnemyBaseController enemy)
    {
        if (enemy == null)
            return false;

        float range = AbilityCombatPower.LightningRodArcRange;
        return (enemy.transform.position - _lightningRodAnchor).sqrMagnitude <= range * range;
    }

    private void CollectEnemiesNearLightningRod(
        List<EnemyBaseController> dest,
        float range,
        HashSet<EnemyBaseController> exclude,
        int maxCount)
    {
        dest.Clear();
        float rangeSq = range * range;
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController candidate = enemies[i];
            if (candidate == null || candidate.IsDead || !candidate.gameObject.activeInHierarchy)
                continue;
            if (exclude != null && exclude.Contains(candidate))
                continue;

            float distSq = (candidate.transform.position - _lightningRodAnchor).sqrMagnitude;
            if (distSq > rangeSq)
                continue;

            dest.Add(candidate);
        }

        dest.Sort((a, b) =>
        {
            float da = (a.transform.position - _lightningRodAnchor).sqrMagnitude;
            float db = (b.transform.position - _lightningRodAnchor).sqrMagnitude;
            return da.CompareTo(db);
        });

        if (dest.Count > maxCount)
            dest.RemoveRange(maxCount, dest.Count - maxCount);
    }

    private EnemyBaseController FindNearestEnemyWithinRange(
        Vector3 origin,
        float range,
        HashSet<EnemyBaseController> exclude)
    {
        float rangeSq = range * range;
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController candidate = enemies[i];
            if (candidate == null || candidate.IsDead || !candidate.gameObject.activeInHierarchy)
                continue;
            if (exclude != null && exclude.Contains(candidate))
                continue;

            float distSq = (candidate.transform.position - origin).sqrMagnitude;
            if (distSq > rangeSq || distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = candidate;
        }

        return best;
    }

    private void SyncLightningRodHudBuff()
    {
        if (!buffController)
            return;

        string hudId = AbilityCombatPower.LightningRodAbilityId;
        if (!IsLightningRodActive)
        {
            if (buffController.IsHudAbilityBuffActive(hudId))
                buffController.ClearHudAbilityBuff(hudId);
            _lastSyncedLightningRodHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedLightningRodHudEnd, _lightningRodEndsAt)
            && buffController.IsHudAbilityBuffActive(hudId))
            return;

        _lastSyncedLightningRodHudEnd = _lightningRodEndsAt;
        buffController.SetHudAbilityBuff(hudId, 1, _lightningRodEndsAt, _lightningRodDuration);
    }

    private int GetLightningRodSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged,
            AbilityCombatPower.LightningRodEnhancementParentSpineNodeId,
            -1);
    }
}
