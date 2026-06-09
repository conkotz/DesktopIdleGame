using System;
using UnityEngine;

[Serializable]
public struct EnhancementWeightValues
{
    [Tooltip("Value applied to light weapons (daggers, swiftbows, wands).")]
    public float light;

    [Tooltip("Value applied to medium weapons (swords, spears, maces). Also used as the default scroll display value.")]
    public float medium;

    [Tooltip("Value applied to heavy weapons (polearms, longbows).")]
    public float heavy;

    public float Resolve(WeaponWeight weight)
    {
        return weight switch
        {
            WeaponWeight.Light => light,
            WeaponWeight.Heavy => heavy,
            WeaponWeight.Medium => medium,
            _ => medium,
        };
    }

    public static EnhancementWeightValues FlatDamageForTier(EnhancementTier tier, float medium)
    {
        float offset = tier switch
        {
            EnhancementTier.Intermediate => 2f,
            EnhancementTier.Advanced => 3f,
            _ => 1f,
        };

        return new EnhancementWeightValues
        {
            light = medium - offset,
            medium = medium,
            heavy = medium + offset,
        };
    }

    public static EnhancementWeightValues PercentStep(float medium, float step = 0.01f)
    {
        return new EnhancementWeightValues
        {
            light = medium - step,
            medium = medium,
            heavy = medium + step,
        };
    }

    public static EnhancementWeightValues ChaosFlatDamage(float medium)
    {
        return FlatDamageForTier(EnhancementTier.Advanced, medium);
    }

    public static EnhancementWeightValues Explicit(float light, float medium, float heavy)
    {
        return new EnhancementWeightValues
        {
            light = light,
            medium = medium,
            heavy = heavy,
        };
    }
}
