using UnityEngine;

/// <summary>
/// Resolves minion combat numbers from <see cref="MinionCombatConfig"/> + owner <see cref="CharacterStats"/> minion bonuses.
/// </summary>
/// <remarks>
/// <b>Inherited-hit minions</b> only use the supplied <see cref="SplitDamage"/> snapshot (e.g. owner&apos;s current hit).
/// They do <b>not</b> inherit player crit chance, crit multiplier, attack speed, ailment chances, lifesteal, or on-hit procs
/// unless you add that explicitly in a future phase.
/// </remarks>
public static class MinionRuntimeStatsCalculator
{
    private const float MinAttacksPerSecond = 0.01f;

    /// <param name="owner">Must be non-null; otherwise returns <see cref="MinionRuntimeCombatStats.Zero"/>.</param>
    /// <param name="config">Minion definition.</param>
    /// <param name="ownerHitSplitForInherit">
    /// Owner hit split when <see cref="MinionCombatConfig.damageSourceMode"/> is <see cref="MinionDamageSourceMode.InheritOwnerHitSplit"/>.
    /// Typically <see cref="CharacterStats.MaxSplitDamage"/> or context-specific split from the ability building the hit.
    /// Ignored when mode is <see cref="MinionDamageSourceMode.UseBaseDamageSplit"/>.
    /// </param>
    public static MinionRuntimeCombatStats Compute(
        CharacterStats owner,
        MinionCombatConfig config,
        SplitDamage ownerHitSplitForInherit)
    {
        if (!owner)
            return MinionRuntimeCombatStats.Zero;

        SplitDamage preBonus = config.damageSourceMode switch
        {
            MinionDamageSourceMode.InheritOwnerHitSplit =>
                ownerHitSplitForInherit * config.inheritDamageCoefficient,
            MinionDamageSourceMode.UseBaseDamageSplit =>
                config.baseDamageSplit,
            _ => SplitDamage.Zero
        };

        float dmgMult = 1f + owner.FinalMinionDamagePercent;
        SplitDamage finalDamage = preBonus * dmgMult;

        float aps = config.baseAttackSpeed * (1f + owner.FinalMinionAttackSpeedPercent);
        if (aps < MinAttacksPerSecond)
            aps = MinAttacksPerSecond;

        float crit = Mathf.Clamp01(config.baseCritChance + owner.FinalMinionCritChance);

        return new MinionRuntimeCombatStats
        {
            FinalDamageSplit = finalDamage,
            AttacksPerSecond = aps,
            CritChance = crit,
            CritDamageMultiplier = CharacterStats.MinionCritDamageMultiplier,
            AilmentChances = config.ailmentChances
        };
    }
}
