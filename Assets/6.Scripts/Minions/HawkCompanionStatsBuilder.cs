using UnityEngine;

/// <summary>
/// Pure-minion combat numbers for <see cref="HawkCompanionMinion"/> including ranged-level scaling and enhancements.
/// </summary>
public static class HawkCompanionStatsBuilder
{
    public static SplitDamageRange BuildBaseDamageRange(int rangedLevel)
    {
        float bonus = AbilityCombatPower.ComputeRangedLevelBonusAfterUnlock(
            rangedLevel,
            AbilityCombatPower.HawkCompanionEnhancementSourceLevel,
            AbilityCombatPower.HawkCompanionPhysicalPerTwoRangedLevels);
        return new SplitDamageRange
        {
            min = new SplitDamage
            {
                physical = AbilityCombatPower.HawkCompanionBaseMinPhysical + bonus,
                magic = 0f,
                corruptionDamage = 0f
            },
            max = new SplitDamage
            {
                physical = AbilityCombatPower.HawkCompanionBaseMaxPhysical + bonus,
                magic = 0f,
                corruptionDamage = 0f
            }
        };
    }

    public static MinionRuntimeCombatStats Compute(
        CharacterStats owner,
        MinionCombatConfig baseConfig,
        int rangedLevel,
        int enhancementChoice)
    {
        if (!owner)
            return MinionRuntimeCombatStats.Zero;

        MinionCombatConfig cfg = MinionCombatConfig.AfterDeserialize(baseConfig);
        cfg.pureMinionDamageSplitRange = BuildBaseDamageRange(rangedLevel);
        cfg.pureMinionAttackSpeed = AbilityCombatPower.HawkCompanionBaseAttackSpeed;

        if (enhancementChoice == AbilityCombatPower.HawkCompanionWeakspotsChoiceIndex)
        {
            cfg.pureMinionCritChance = Mathf.Clamp01(
                cfg.pureMinionCritChance + AbilityCombatPower.HawkCompanionWeakspotsCritChanceBonus);
        }

        return MinionRuntimeStatsCalculator.Compute(owner, cfg, default);
    }
}
