using UnityEngine;

/// <summary>
/// Resolves minion combat numbers from <see cref="MinionCombatConfig"/> + owner <see cref="CharacterStats"/>.
/// </summary>
public static class MinionRuntimeStatsCalculator
{
    private const float MinAttacksPerSecond = 0.01f;

    /// <summary>Serialized0 means "unset" for assets saved before inherit scale fields existed.</summary>
    private static float InheritScaleOrDefault(float v) => v > 0f ? v : 1f;

    /// <param name="owner">Must be non-null; otherwise returns <see cref="MinionRuntimeCombatStats.Zero"/>.</param>
    /// <param name="ownerHitRangeForInherit">
    /// Owner min/max hit split when mode is <see cref="MinionDamageSourceMode.InheritOwnerHitSplit"/> (rolls per hit like basics).
    /// Ignored for <see cref="MinionDamageSourceMode.PureMinionSourceDamage"/>.
    /// </param>
    public static MinionRuntimeCombatStats Compute(
        CharacterStats owner,
        MinionCombatConfig config,
        SplitDamageRange ownerHitRangeForInherit)
    {
        if (!owner)
            return MinionRuntimeCombatStats.Zero;

        float dmgMult = 1f + owner.FinalMinionDamagePercent;

        if (config.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            float coeff = Mathf.Max(0f, config.inheritDamageCoefficient);
            SplitDamageRange preBonus = new SplitDamageRange
            {
                min = ownerHitRangeForInherit.min * coeff,
                max = ownerHitRangeForInherit.max * coeff
            };
            SplitDamageRange finalDamage = new SplitDamageRange
            {
                min = preBonus.min * dmgMult,
                max = preBonus.max * dmgMult
            };

            float iAps = InheritScaleOrDefault(config.inheritAttackSpeedCoefficient);
            float iCrit = InheritScaleOrDefault(config.inheritCritChanceCoefficient);
            float iCritMult = InheritScaleOrDefault(config.inheritCritMultiplierCoefficient);
            float ac = InheritScaleOrDefault(config.inheritAilmentChanceCoefficient);

            float aps = owner.AttacksPerSecond * iAps;
            aps *= 1f + owner.FinalMinionAttackSpeedPercent;
            if (aps < MinAttacksPerSecond)
                aps = MinAttacksPerSecond;

            float crit = Mathf.Clamp01(owner.CritChance * iCrit + owner.FinalMinionCritChance);

            float critEx = Mathf.Max(0f, owner.CritMultiplier - 1f);
            float critMult = 1f + critEx * iCritMult;
            critMult = Mathf.Max(1f, critMult);
            var ailments = new MinionAilmentChances
            {
                poisonChance = Mathf.Clamp01(owner.PoisonChance * ac),
                bleedChance = Mathf.Clamp01(owner.BleedChance * ac),
                burnChance = Mathf.Clamp01(owner.BurnApplyChance * ac),
                shockChance = Mathf.Clamp01(owner.MeleeShockChance * ac)
            };

            float magicAil = Mathf.Clamp01(owner.MagicAilmentApplyChance * ac);

            return new MinionRuntimeCombatStats
            {
                FinalDamageSplitRange = finalDamage,
                AttacksPerSecond = aps,
                CritChance = crit,
                CritDamageMultiplier = critMult,
                AilmentChances = ailments,
                MagicAilmentApplyChance = magicAil
            };
        }

        // Pure minion source damage: minion-only numbers; not owner's weapon APS/crit/ailment chances.
        SplitDamageRange basePre = config.pureMinionDamageSplitRange;
        SplitDamageRange baseFinal = new SplitDamageRange
        {
            min = basePre.min * dmgMult,
            max = basePre.max * dmgMult
        };

        float baseAps = Mathf.Max(0f, config.pureMinionAttackSpeed) * (1f + owner.FinalMinionAttackSpeedPercent);
        if (baseAps < MinAttacksPerSecond)
            baseAps = MinAttacksPerSecond;

        float baseCrit = Mathf.Clamp01(config.pureMinionCritChance + owner.FinalMinionCritChance);

        return new MinionRuntimeCombatStats
        {
            FinalDamageSplitRange = baseFinal,
            AttacksPerSecond = baseAps,
            CritChance = baseCrit,
            CritDamageMultiplier = CharacterStats.MinionCritDamageMultiplier,
            AilmentChances = config.ailmentChances,
            MagicAilmentApplyChance = 0f
        };
    }
}
