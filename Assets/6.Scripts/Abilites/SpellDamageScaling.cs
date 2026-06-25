using UnityEngine;

/// <summary>
/// Spell hits use fixed base damage scaled by spell-specific stats — not weapon split damage.
/// Multipliers: Spell Damage %, Magic Damage %, element % (Fire/Ice/Lightning). Ability Power excluded.
/// </summary>
public static class SpellDamageScaling
{
    /// <summary>
    /// Combined multiplier for spell lightning damage: Spell damage %, Magic damage %, Lightning damage %.
    /// </summary>
    public static float GetSpellDamageMultiplier(CharacterStats stats) =>
        GetSpellDamageMultiplierForElement(stats, MagicAttackType.Lightning);

    /// <summary>Spell hits use fixed base damage scaled by spell stats — not weapon split damage or Ability Power.</summary>
    public static float GetSpellDamageMultiplierForElement(CharacterStats stats, MagicAttackType element)
    {
        if (stats == null)
            return 1f;

        float spellMult = 1f + stats.SpellDamageTotalScalingPercentPoints / 100f;
        float magicMult = 1f + stats.SpellMagicDamageScalingPercentPoints / 100f;
        float elementMult = GetElementSkillDamageMultiplier(stats, element);
        return spellMult * magicMult * elementMult;
    }

    private static float GetElementSkillDamageMultiplier(CharacterStats stats, MagicAttackType element)
    {
        return element switch
        {
            MagicAttackType.Fire => 1f + stats.FireSkillDamageTotalScalingPercentPoints / 100f,
            MagicAttackType.Ice => 1f + stats.IceSkillDamageTotalScalingPercentPoints / 100f,
            _ => 1f + stats.LightningSkillDamageTotalScalingPercentPoints / 100f
        };
    }

    public static void ScaleElementBounds(
        CharacterStats stats,
        MagicAttackType element,
        float baseMin,
        float baseMax,
        out float minDamage,
        out float maxDamage)
    {
        float mult = GetSpellDamageMultiplierForElement(stats, element);
        float flatMin = 0f;
        float flatMax = 0f;
        if (stats != null && stats.TryGetEquippedRuneElementFlatBounds(element, out float runeMin, out float runeMax))
        {
            flatMin = runeMin;
            flatMax = runeMax;
        }

        minDamage = Mathf.Max(0f, (baseMin + flatMin) * mult);
        maxDamage = Mathf.Max(minDamage, (baseMax + flatMax) * mult);
    }

    public static void ScaleBaseLightningBounds(
        CharacterStats stats,
        float baseMin,
        float baseMax,
        out float minDamage,
        out float maxDamage) =>
        ScaleElementBounds(stats, MagicAttackType.Lightning, baseMin, baseMax, out minDamage, out maxDamage);

    public static float RollScaledLightningDamage(CharacterStats stats, float baseMin, float baseMax)
    {
        ScaleBaseLightningBounds(stats, baseMin, baseMax, out float min, out float max);
        return RollElementDamage(min, max, stats, MagicAttackType.Lightning);
    }

    public static float RollScaledElementDamage(
        CharacterStats stats,
        MagicAttackType element,
        float baseMin,
        float baseMax)
    {
        ScaleElementBounds(stats, element, baseMin, baseMax, out float min, out float max);
        return RollElementDamage(min, max, stats, element);
    }

    private static float RollElementDamage(float min, float max, CharacterStats stats, MagicAttackType element)
    {
        float luckyChance = element == MagicAttackType.Lightning && stats != null
            ? stats.LightningLuckyChanceFraction
            : 0f;
        return RollWithOptionalLucky(min, max, luckyChance);
    }

    /// <summary>Roll damage; when lucky procs, roll again and keep the higher value.</summary>
    public static float RollWithOptionalLucky(float min, float max, float luckyChance)
    {
        float roll = RollFromScaledBounds(min, max);
        if (luckyChance > 0f && max > min + 0.001f && Random.value < Mathf.Clamp01(luckyChance))
            roll = Mathf.Max(roll, RollFromScaledBounds(min, max));
        return roll;
    }

    private static float RollFromScaledBounds(float min, float max)
    {
        if (max <= min + 0.001f)
            return Mathf.Max(1f, min);

        return Mathf.Max(1f, Random.Range(min, max));
    }
}
