using UnityEngine;

/// <summary>
/// Spell hits use fixed base damage scaled by spell-specific stats — not weapon split damage.
/// </summary>
public static class SpellDamageScaling
{
    /// <summary>
    /// Combined multiplier for spell lightning damage: Spell damage %, Magic damage %, Lightning damage %, Ability Power %.
    /// </summary>
    public static float GetSpellDamageMultiplier(CharacterStats stats)
    {
        if (stats == null)
            return 1f;

        float spellMult = 1f + stats.SpellDamageTotalScalingPercentPoints / 100f;
        float magicMult = 1f + stats.SpellMagicDamageScalingPercentPoints / 100f;
        float lightningMult = 1f + stats.LightningSkillDamageTotalScalingPercentPoints / 100f;
        float apMult = stats.GetAbilityPowerDamageMultiplier();
        return spellMult * magicMult * lightningMult * apMult;
    }

    public static void ScaleBaseLightningBounds(
        CharacterStats stats,
        float baseMin,
        float baseMax,
        out float minDamage,
        out float maxDamage)
    {
        float mult = GetSpellDamageMultiplier(stats);
        minDamage = Mathf.Max(0f, baseMin * mult);
        maxDamage = Mathf.Max(minDamage, baseMax * mult);
    }

    public static float RollScaledLightningDamage(CharacterStats stats, float baseMin, float baseMax)
    {
        ScaleBaseLightningBounds(stats, baseMin, baseMax, out float min, out float max);
        if (max <= min + 0.001f)
            return Mathf.Max(1f, min);

        return Mathf.Max(1f, Random.Range(min, max));
    }
}
