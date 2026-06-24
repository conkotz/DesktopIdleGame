using UnityEngine;

/// <summary>
/// Spell hits use fixed base damage scaled by spell-specific stats — not weapon split damage.
/// </summary>
public static class SpellDamageScaling
{
    /// <summary>
    /// Combined multiplier for spell lightning damage: Spell damage %, Magic damage %, Lightning damage %, Ability Power %.
    /// </summary>
    public static float GetSpellDamageMultiplier(CharacterStats stats) =>
        GetSpellDamageMultiplierForElement(stats, MagicAttackType.Lightning);

    /// <summary>
    /// Full spell multiplier including magic damage % (instant-cast spells).
    /// </summary>
    public static float GetSpellDamageMultiplierForElement(CharacterStats stats, MagicAttackType element)
    {
        if (stats == null)
            return 1f;

        float spellMult = 1f + stats.SpellDamageTotalScalingPercentPoints / 100f;
        float magicMult = 1f + stats.SpellMagicDamageScalingPercentPoints / 100f;
        float elementMult = GetElementSkillDamageMultiplier(stats, element);
        float apMult = stats.GetAbilityPowerDamageMultiplier();
        return spellMult * magicMult * elementMult * apMult;
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
        minDamage = Mathf.Max(0f, baseMin * mult);
        maxDamage = Mathf.Max(minDamage, baseMax * mult);
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
        return RollFromScaledBounds(min, max);
    }

    public static float RollScaledElementDamage(
        CharacterStats stats,
        MagicAttackType element,
        float baseMin,
        float baseMax)
    {
        ScaleElementBounds(stats, element, baseMin, baseMax, out float min, out float max);
        return RollFromScaledBounds(min, max);
    }

    private static float RollFromScaledBounds(float min, float max)
    {
        if (max <= min + 0.001f)
            return Mathf.Max(1f, min);

        return Mathf.Max(1f, Random.Range(min, max));
    }
}
