using System.Collections.Generic;
using UnityEngine;

public partial class PlayerAbilityController
{
    private readonly HashSet<EnemyBaseController> _chainLightningHitScratch = new();

    private static bool IsChainLightningAbilityId(string abilityId) =>
        string.Equals(abilityId, AbilityCombatPower.ChainLightningAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private bool CanHitAnyEnemyWithChainLightning()
    {
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        return combat != null && combat.FindClosestEnemyInAttackRange() != null;
    }

    private bool TryCastChainLightning(AbilityDefinition def, bool showLockedFeedback)
    {
        if (def == null || stats == null)
            return false;

        if (!CanHitAnyEnemyWithChainLightning())
        {
            if (showLockedFeedback)
                player?.ShowPopup("No targets in range.");
            return false;
        }

        if (!TryFindClosestEnemyInChainLightningSpellRange(out EnemyBaseController primary))
        {
            if (showLockedFeedback)
                player?.ShowPopup("No targets in range.");
            return false;
        }

        if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
            return false;

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (!abilityVfx)
            abilityVfx = GetComponent<PlayerAbilityVfxController>();

        if (combat != null && def.SetsTargetOnHit())
            combat.SetTargetIfNone(primary);

        RunChainLightningCast(primary);
        return true;
    }

    private bool TryFindClosestEnemyInChainLightningSpellRange(out EnemyBaseController target)
    {
        target = null;
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null)
            return false;

        EnemyBaseController engaged = combat.GetPrimaryEngagedEnemy();
        if (engaged != null && combat.IsEnemyWithinAttackRange(engaged))
        {
            target = engaged;
            return true;
        }

        target = combat.FindClosestEnemyInAttackRange();
        return target != null;
    }

    private float GetChainLightningChainRange()
    {
        float range = AbilityCombatPower.ChainLightningBaseChainRange;
        if (GetChainLightningSelectedChoice() == AbilityCombatPower.ChainLightningEnh1ExtraChainChoiceIndex)
            range += AbilityCombatPower.ChainLightningEnh1ChainRangeBonus;
        return range;
    }

    private int GetChainLightningMaxChainJumps()
    {
        int jumps = AbilityCombatPower.ChainLightningBaseMaxChainJumps;
        if (GetChainLightningSelectedChoice() == AbilityCombatPower.ChainLightningEnh1ExtraChainChoiceIndex)
            jumps++;
        return jumps;
    }

    /// <summary>
    /// Lightning Rod expiry-chain pattern: arc to primary, then each jump searches from the last enemy hit.
    /// </summary>
    private void RunChainLightningCast(EnemyBaseController primary)
    {
        if (primary == null || primary.IsDead)
            return;

        _chainLightningHitScratch.Clear();

        int maxJumps = GetChainLightningMaxChainJumps();
        float chainRange = GetChainLightningChainRange();
        bool tryShock =
            GetChainLightningSelectedChoice() == AbilityCombatPower.ChainLightningEnh2ShockChoiceIndex;
        float primaryDamageMultiplier = ComputeChainLightningPrimaryDamageMultiplier(primary, maxJumps, chainRange);

        FireChainLightningArcToEnemy(
            primary,
            GetChainLightningCastOrigin(),
            primaryDamageMultiplier,
            tryShock);

        EnemyBaseController current = primary;
        for (int jump = 0; jump < maxJumps; jump++)
        {
            EnemyBaseController next = FindNearestEnemyWithinRange(
                current.transform.position,
                chainRange,
                _chainLightningHitScratch);
            if (next == null)
                break;

            FireChainLightningArcToEnemy(
                next,
                GetEnemyVfxCenter(current),
                1f,
                tryShock);
            current = next;
        }
    }

    private float ComputeChainLightningPrimaryDamageMultiplier(
        EnemyBaseController primary,
        int maxJumps,
        float chainRange)
    {
        var scratch = new HashSet<EnemyBaseController> { primary };
        EnemyBaseController current = primary;
        int chainsPerformed = 0;

        for (int jump = 0; jump < maxJumps; jump++)
        {
            EnemyBaseController next = FindNearestEnemyWithinRange(
                current.transform.position,
                chainRange,
                scratch);
            if (next == null)
                break;

            scratch.Add(next);
            current = next;
            chainsPerformed++;
        }

        int unusedChains = Mathf.Max(0, maxJumps - chainsPerformed);
        return 1f + AbilityCombatPower.ChainLightningUnusedChainPrimaryDamageBonusPerJump * unusedChains;
    }

    private void FireChainLightningArcToEnemy(
        EnemyBaseController intendedTarget,
        Vector3 arcStart,
        float damageMultiplier,
        bool tryShock)
    {
        if (intendedTarget == null || intendedTarget.IsDead)
            return;

        Vector3 arcEnd = GetEnemyVfxCenter(intendedTarget);
        float rolled = SpellDamageScaling.RollScaledLightningDamage(
            stats,
            AbilityCombatPower.ChainLightningBaseMinDamage,
            AbilityCombatPower.ChainLightningBaseMaxDamage);
        rolled = Mathf.Max(1f, rolled * damageMultiplier);

        EnemyBaseController damageTarget = TornadoLightningRouter.RouteLightningArc(
            abilityVfx,
            arcStart,
            arcEnd,
            rolled,
            intendedTarget.transform,
            intendedTarget,
            _chainLightningHitScratch);

        if (damageTarget == null || damageTarget.IsDead)
            return;

        ApplyChainLightningHitDamage(damageTarget, rolled, tryShock);
        _chainLightningHitScratch.Add(damageTarget);
    }

    private Vector3 GetChainLightningCastOrigin()
    {
        return transform.position + new Vector3(0f, 0.55f, 0f);
    }

    private void ApplyChainLightningHitDamage(EnemyBaseController target, float baseDamage, bool tryShock)
    {
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null || target == null || target.IsDead || stats == null)
            return;

        bool wasCrit = false;
        float critMult = 1f;
        if (UnityEngine.Random.value < GetEffectiveAbilityCritChance(target))
        {
            wasCrit = true;
            critMult = Mathf.Max(1f, stats.CritMultiplier);
        }

        float damage = Mathf.Max(1f, baseDamage * critMult);
        SplitDamage rolled = new SplitDamage(0f, damage, 0f);
        combat.ApplyStaticArrowsCritArcDamage(
            target,
            rolled,
            wasCrit,
            AbilityCombatPower.ChainLightningOutgoingDamageSourceLabel);

        if (tryShock)
            TryApplyChainLightningShock(target);
    }

    private void TryApplyChainLightningShock(EnemyBaseController target)
    {
        if (target == null || target.IsDead || stats == null)
            return;
        if (UnityEngine.Random.value >= AbilityCombatPower.ChainLightningEnh2ShockChance)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        float shockMult = Mathf.Clamp01(
            stats.ShockDamageTakenMultiplier + AbilityCombatPower.ChainLightningEnh2ShockEffectBonus);
        ailments.ApplyShockFromHit(new ShockPayload(
            duration: stats.ShockDuration,
            damageTakenMultiplier: shockMult,
            source: transform));
    }

    private int GetChainLightningSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Magic,
            AbilityCombatPower.ChainLightningEnhancementParentSpineNodeId,
            -1);
    }
}
