using UnityEngine;

/// <summary>
/// Shared scaling for ability damage: elemental lines tied to <see cref="CharacterStats.CurrentMagicAttackType"/>
/// and light hooks for poison/bleed DPS (used by <see cref="PlayerAbilityController"/> and <see cref="AbilityCombatPower"/>).
/// </summary>
public static class AbilityElementScaling
{
    /// <summary>
    /// Average weapon magical hit used for element lines (matches instant ability model).
    /// </summary>
    private static float AverageMagicalHit(CharacterStats stats)
    {
        if (!stats)
            return 0f;
        return (Mathf.Max(0f, stats.MinSplitDamage.magical) + Mathf.Max(0f, stats.MaxSplitDamage.magical)) * 0.5f;
    }

    /// <summary>
    /// Extra damage from Fire/Ice/Lightning multipliers when the character's magic attack type matches.
    /// </summary>
    public static float GetElementDamageBonus(AbilityDefinition def, CharacterStats stats)
    {
        if (!def || !stats)
            return 0f;

        float avgMag = AverageMagicalHit(stats);
        if (avgMag <= 0f)
            return 0f;

        switch (stats.CurrentMagicAttackType)
        {
            case MagicAttackType.Fire:
                return avgMag * Mathf.Max(0f, def.fireDamageMultiplier);
            case MagicAttackType.Ice:
                return avgMag * Mathf.Max(0f, def.iceDamageMultiplier);
            case MagicAttackType.Lightning:
                return avgMag * Mathf.Max(0f, def.lightningDamageMultiplier);
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Small expected contribution from poison/bleed DPS scaled by ability multipliers (heuristic for balance).
    /// </summary>
    public static float GetPoisonBleedBonusForInstantAbility(AbilityDefinition def, CharacterStats stats)
    {
        if (!def || !stats)
            return 0f;

        const float weight = 0.05f;
        float p = stats.ExpectedPoisonDPS * Mathf.Max(0f, def.poisonDamageMultiplier) * weight;
        float b = stats.ExpectedBleedDPS * Mathf.Max(0f, def.bleedDamageMultiplier) * weight;
        return Mathf.Max(0f, p + b);
    }
}
