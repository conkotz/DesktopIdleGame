using System;
using System.Collections;
using UnityEngine;

public partial class PlayerAbilityController
{
    private bool _snipeCharging;
    private bool _snipeActionBarHeld;
    private bool _snipeAutoBattleCharging;
    private bool _snipeResourceCommitted;
    private float _snipeChargeStartedAt;
    private AbilityDefinition _snipeChargeDef;
    private EnemyBaseController _snipeChargeTarget;

    public static bool IsSnipeAbilityId(string abilityId) =>
        string.Equals(abilityId, AbilityCombatPower.SnipeAbilityId, StringComparison.OrdinalIgnoreCase);

    public void SetSnipeActionBarHeld(bool held)
    {
        _snipeActionBarHeld = held;
        if (!held && _snipeCharging && !_snipeAutoBattleCharging)
            TryReleaseSnipeCharge();
    }

    public bool TryGetSnipeChargeBarOverlay(string abilityId, out float normalizedRemaining, out float secondsRemaining)
    {
        normalizedRemaining = 0f;
        secondsRemaining = 0f;
        if (!_snipeCharging || !IsSnipeAbilityId(abilityId))
            return false;

        float duration = GetSnipeChargeDurationSeconds();
        if (duration <= 0.0001f)
            return false;

        float elapsed = Time.time - _snipeChargeStartedAt;
        secondsRemaining = Mathf.Max(0f, duration - elapsed);
        normalizedRemaining = Mathf.Clamp01(secondsRemaining / duration);
        return true;
    }

    private int GetSnipeSelectedChoice()
    {
        if (skillsManager == null)
            return -1;

        int selected = skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged, AbilityCombatPower.SnipeEnhancementParentSpineNodeId, -1);
        if (selected < 0)
            selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
        return selected;
    }

    private float GetSnipeChargeDurationSeconds() =>
        AbilityCombatPower.GetSnipeChargeDurationSeconds(GetSnipeSelectedChoice());

    private void CancelSnipeCharge(bool refundResource, bool cancelAttackAnim = true)
    {
        if (!_snipeCharging)
            return;

        if (refundResource && _snipeResourceCommitted && _snipeChargeDef != null)
            RefundAbilityResourceCost(_snipeChargeDef);

        _snipeCharging = false;
        _snipeActionBarHeld = false;
        _snipeAutoBattleCharging = false;
        _snipeResourceCommitted = false;
        _snipeChargeDef = null;
        _snipeChargeTarget = null;
        _snipeChargeStartedAt = 0f;
        if (cancelAttackAnim)
            player?.CancelSnipeChargeAttackAnim();
        abilityVfx?.EndSnipeChargeVfx();
        SyncSnipeHudBuff();
    }

    public void CancelSnipeChargeFromPlayerStop()
    {
        CancelSnipeCharge(refundResource: true);
    }

    private bool TryBeginSnipeCharge(AbilityDefinition def, EnemyBaseController target, bool autoBattleFullCharge)
    {
        if (def == null || target == null || target.IsDead)
            return false;

        _snipeCharging = true;
        _snipeChargeDef = def;
        _snipeChargeTarget = target;
        _snipeChargeStartedAt = Time.time;
        _snipeAutoBattleCharging = autoBattleFullCharge;
        _snipeActionBarHeld = !autoBattleFullCharge;
        _snipeResourceCommitted = true;
        player?.BeginSnipeChargeAttackAnim();
        abilityVfx?.BeginSnipeChargeVfx(GetSnipeChargeDurationSeconds());
        SyncSnipeHudBuff();
        return true;
    }

    private void TickSnipeCharge()
    {
        if (!_snipeCharging)
            return;

        if (player == null || stats == null || player.IsDead || stats.IsDead)
        {
            CancelSnipeCharge(refundResource: true);
            return;
        }

        if (_snipeChargeDef == null || !IsAbilityAllowedBySkillProgress(_snipeChargeDef) || !CanUseWithEquippedWeapon(_snipeChargeDef))
        {
            CancelSnipeCharge(refundResource: true);
            return;
        }

        EnemyBaseController target = _snipeChargeTarget;
        if (target == null || target.IsDead)
        {
            CancelSnipeCharge(refundResource: true);
            return;
        }

        if (combat != null && _snipeChargeDef.RequiresKeyboardRangeCheckToActivate() && !combat.IsEnemyWithinAttackRange(target))
        {
            CancelSnipeCharge(refundResource: true);
            return;
        }

        float elapsed = Time.time - _snipeChargeStartedAt;
        float duration = GetSnipeChargeDurationSeconds();
        if (elapsed + 0.0001f >= duration)
        {
            ExecuteSnipeFire();
            return;
        }

        if (!_snipeAutoBattleCharging && !_snipeActionBarHeld)
            TryReleaseSnipeCharge();
    }

    private void TryReleaseSnipeCharge()
    {
        if (!_snipeCharging)
            return;

        ExecuteSnipeFire();
    }

    private void ExecuteSnipeFire()
    {
        if (!_snipeCharging || _snipeChargeDef == null)
            return;

        AbilityDefinition def = _snipeChargeDef;
        EnemyBaseController target = _snipeChargeTarget;
        float elapsed = Time.time - _snipeChargeStartedAt;
        float duration = GetSnipeChargeDurationSeconds();
        float chargeDamageMult = AbilityCombatPower.GetSnipeDamageMultiplierAtElapsed(elapsed, duration);
        bool forceBleed = GetSnipeSelectedChoice() == AbilityCombatPower.SnipeGuaranteedBleedChoiceIndex;

        CancelSnipeCharge(refundResource: false, cancelAttackAnim: false);

        if (target == null || target.IsDead)
        {
            player?.CancelSnipeChargeAttackAnim();
            return;
        }

        BuildSnipeHitSplit(def, target, chargeDamageMult, out SplitDamage hit, out bool wasCrit);
        if (hit.IsEmpty)
            return;

        PrepareBattleEngineAbilityHitSession(def);
        player?.ReleaseSnipeChargeAttackAnim();
        combat?.ApplyFullAutoAttackCooldown();
        combat?.TryConsumeOffHandSupportAmmoOnUse();
        StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        ApplyBattleEngineOnAbilityCommitEffects(def, beginHitSession: false);
        LogAbilityUsed(def);

        if (combat != null && combat.TryFireSnipeProjectile(target, out float travelTime))
            StartCoroutine(CoResolveSnipeHitAfterTravel(target, def, hit, wasCrit, travelTime, forceBleed));
        else
            ResolveSnipeHitNow(target, def, hit, wasCrit, forceBleed);
    }

    private void BuildSnipeHitSplit(
        AbilityDefinition def,
        EnemyBaseController target,
        float chargeDamageMultiplier,
        out SplitDamage hit,
        out bool wasCrit)
    {
        SplitDamage rolled = stats.RollSplitAttackDamage(out bool baseWasCrit);
        float critMult = Mathf.Max(1f, stats.CritMultiplier);
        if (baseWasCrit && critMult > 1f)
        {
            rolled.physical /= critMult;
            rolled.magic /= critMult;
        }

        float allM = def.GetEffectiveAllDamageMultiplier();
        float wM = def.GetWeaponHitScalingMultiplier() * Mathf.Max(0f, chargeDamageMultiplier);
        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apM = GetAbilityPowerDamageMultiplierForAbility(def);
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
        float overloadMult = GetBattleEngineOverloadDamageMultiplier();

        float physLine = rolled.physical * wM + ailmentBonus;
        float magLine = rolled.magic * wM * elemM + elementBonus * elemM;
        float physPart = physLine * allM * apM * overloadMult;
        float magPart = magLine * allM * apM * overloadMult;
        float corrPart = (rolled.corruptionDamage * wM) * allM * apM * overloadMult;

        SplitDamage preCritHit = new SplitDamage(physPart, magPart, corrPart);
        wasCrit = false;
        float appliedCritMult = 1f;
        if (preCritHit.CanCrit && UnityEngine.Random.value < GetEffectiveAbilityCritChance(target))
        {
            wasCrit = true;
            appliedCritMult = critMult;
        }

        hit = new SplitDamage(
            Mathf.Max(0f, physPart * appliedCritMult),
            Mathf.Max(0f, magPart * appliedCritMult),
            Mathf.Max(0f, corrPart * appliedCritMult));

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

        ApplyActiveDamageConversions(ref hit);
    }

    private IEnumerator CoResolveSnipeHitAfterTravel(
        EnemyBaseController targetAtFireTime,
        AbilityDefinition def,
        SplitDamage hit,
        bool wasCrit,
        float travelTime,
        bool forceBleed)
    {
        if (travelTime > 0f)
            yield return new WaitForSeconds(travelTime);

        ResolveSnipeHitNow(targetAtFireTime, def, hit, wasCrit, forceBleed);
    }

    private void ResolveSnipeHitNow(
        EnemyBaseController target,
        AbilityDefinition def,
        SplitDamage hit,
        bool wasCrit,
        bool forceBleed)
    {
        if (target == null || target.IsDead || hit.IsEmpty)
            return;

        string sourceLabel = GetAbilityOutgoingDamageSourceLabel(def.abilityId);
        int phys = Mathf.Max(0, Mathf.RoundToInt(hit.physical));
        int mag = Mathf.Max(0, Mathf.RoundToInt(hit.magic));
        int corr = Mathf.Max(0, Mathf.RoundToInt(hit.corruptionDamage));
        int dealt = 0;
        AttackSkill? skill = stats != null ? stats.CurrentAttackSkill : null;

        if (phys > 0)
            dealt += target.TakeDamage(phys, DamageType.Physical, wasCrit, transform, skill, outgoingDpsSourceLabel: sourceLabel);
        if (mag > 0)
            dealt += target.TakeDamage(mag, DamageType.Magic, wasCrit, transform, skill, outgoingDpsSourceLabel: sourceLabel);
        if (corr > 0)
            dealt += target.TakeDamage(corr, DamageType.Corruption, wasCrit, transform, skill, outgoingDpsSourceLabel: sourceLabel);

        TryGrantBattleEngineEnergyOnAbilityHit(def, dealt > 0);

        if (def.SetsTargetOnHit() && combat != null)
            combat.SetTarget(target);

        var dealtHit = new DealtHit
        {
            physical = phys,
            magic = mag,
            corruptionDamage = corr,
            meleeMagicLightningFraction = stats != null ? stats.GetMeleeMagicLightningFraction() : 0f
        };

        ApplyOnHitEffects(target, dealtHit);
        if (forceBleed)
            TryApplySnipeGuaranteedBleed(target, dealtHit);
    }

    private void TryApplySnipeGuaranteedBleed(EnemyBaseController target, DealtHit dealt)
    {
        if (target == null || stats == null || dealt.physical <= 0f)
            return;
        if (!stats.CanApplyOutgoingAilmentsOnHit())
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        float duration = Mathf.Max(1f, stats.BleedDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        float baseDuration = Mathf.Max(1f, stats.BleedBaseDuration);
        float bleedTickDamage = dealt.physical * (1f + stats.BleedMultiplier) / baseDuration;
        if (bleedTickDamage <= 0f)
            return;

        float totalBleedDamage = bleedTickDamage * ticks;
        ailments.ApplyBleedFromHit(new BleedPayload(
            totalBleedDamage,
            duration,
            ticks,
            transform,
            maxStacks: stats.BleedMaxStacks));
    }

    private void SyncSnipeHudBuff()
    {
        if (!buffController)
            return;

        string buffId = AbilityCombatPower.SnipeAbilityId;
        if (!_snipeCharging)
        {
            if (buffController.IsHudAbilityBuffActive(buffId))
                buffController.ClearHudAbilityBuff(buffId);
            return;
        }

        float duration = GetSnipeChargeDurationSeconds();
        float endsAt = _snipeChargeStartedAt + duration;
        buffController.SetHudAbilityBuff(buffId, 1, endsAt, duration);
    }
}
