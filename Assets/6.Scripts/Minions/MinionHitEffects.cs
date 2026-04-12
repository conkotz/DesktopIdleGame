using UnityEngine;

/// <summary>
/// On-hit ailments for minions that mirror the owner's weapon rules (chances from <see cref="MinionRuntimeCombatStats"/>,
/// durations and multipliers from summon-time <see cref="MinionOwnerWeaponSnapshot"/>).
/// </summary>
public static class MinionHitEffects
{
    public static void ApplyAilmentsFromOwnerWeapon(
        EnemyBaseController target,
        in MinionOwnerWeaponSnapshot ownerSnap,
        in MinionRuntimeCombatStats stats,
        float physicalDealt,
        float magicDealt,
        float corruptionDealt,
        Transform source)
    {
        if (!target || target.IsDead)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (!ailments)
            return;

        Transform src = source ? source : target.transform;

        TryBleed(ailments, ownerSnap, stats, physicalDealt, src);
        TryPoison(ailments, ownerSnap, stats, corruptionDealt, src);

        float totalDealt = physicalDealt + magicDealt + corruptionDealt;
        if (ownerSnap.currentAttackAppliesAsFireForBurn && totalDealt > 0f && stats.AilmentChances.burnChance > 0f)
        {
            ailments.TryApplyBurnFromFireHit(
                totalDealt,
                stats.AilmentChances.burnChance,
                ownerSnap.burnExplosionMultiplier,
                src);
        }

        if (ownerSnap.currentAttackSkill == AttackSkill.Magic && magicDealt > 0f && stats.MagicAilmentApplyChance > 0f)
        {
            if (Random.value <= stats.MagicAilmentApplyChance)
            {
                switch (ownerSnap.currentMagicAttackType)
                {
                    case MagicAttackType.Ice:
                        ailments.ApplyChillFromHit(new ChillPayload(
                            duration: ownerSnap.chillDuration,
                            maxStacks: ownerSnap.chillMaxStacks,
                            slowPerStack: ownerSnap.chillSlowPerStack,
                            source: src));
                        break;
                    case MagicAttackType.Lightning:
                    default:
                        ailments.ApplyShockFromHit(new ShockPayload(
                            duration: ownerSnap.shockDuration,
                            damageTakenMultiplier: ownerSnap.shockDamageTakenMultiplier,
                            source: src));
                        break;
                }
            }
        }
        else if (ownerSnap.currentAttackSkill != AttackSkill.Magic &&
                 magicDealt > 0f &&
                 ownerSnap.meleeMagicLightningFraction > 1e-5f &&
                 stats.AilmentChances.shockChance > 0f)
        {
            if (Random.value <= stats.AilmentChances.shockChance)
            {
                ailments.ApplyShockFromHit(new ShockPayload(
                    duration: ownerSnap.shockDuration,
                    damageTakenMultiplier: ownerSnap.shockDamageTakenMultiplier,
                    source: src));
            }
        }
    }

    private static void TryBleed(
        AilmentController ailments,
        in MinionOwnerWeaponSnapshot ownerSnap,
        in MinionRuntimeCombatStats stats,
        float physicalDealt,
        Transform src)
    {
        if (physicalDealt <= 0f || stats.AilmentChances.bleedChance <= 0f)
            return;

        if (Random.value > stats.AilmentChances.bleedChance)
            return;

        float duration = Mathf.Max(1f, ownerSnap.bleedDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        float baseDuration = Mathf.Max(1f, ownerSnap.bleedBaseDuration);
        float bleedTickDamage = physicalDealt * (1f + ownerSnap.bleedMultiplier) / baseDuration;
        if (bleedTickDamage <= 0f) return;

        float totalBleedDamage = bleedTickDamage * ticks;
        ailments.ApplyBleedFromHit(new BleedPayload(totalBleedDamage, duration, ticks, src));
    }

    private static void TryPoison(
        AilmentController ailments,
        in MinionOwnerWeaponSnapshot ownerSnap,
        in MinionRuntimeCombatStats stats,
        float corruptionDealt,
        Transform src)
    {
        if (corruptionDealt <= 0f || stats.AilmentChances.poisonChance <= 0f)
            return;
        if (ownerSnap.poisonMultiplier < 0f) return;

        if (Random.value > stats.AilmentChances.poisonChance)
            return;

        float totalPoisonDamage =
            corruptionDealt * ownerSnap.poisonPoolFractionOfCorruptionDamage * (1f + ownerSnap.poisonMultiplier);
        if (totalPoisonDamage <= 0f) return;

        float duration = Mathf.Max(0.1f, ownerSnap.poisonDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        int maxStacks = Mathf.Max(1, ownerSnap.poisonMaxStacks);

        ailments.ApplyPoisonFromHit(new PoisonPayload(
            totalPoisonDamage,
            duration,
            ticks,
            maxStacks,
            src));
    }
}
