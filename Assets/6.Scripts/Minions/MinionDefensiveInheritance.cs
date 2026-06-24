using System;
using UnityEngine;

/// <summary>
/// Scales owner total defenses onto a summoned minion at spawn time.
/// </summary>
[Serializable]
public struct MinionDefensiveInheritance
{
    [Tooltip("When enabled, copies scaled owner Armour, Magic Resist, and Corruption Resist onto the minion.")]
    public bool inheritOwnerDefenses;

    [Tooltip("Fraction of owner total Armour rating applied to the minion.")]
    [Range(0f, 2f)]
    public float ownerArmourFraction;

    [Tooltip("Fraction of owner total Magic Resist rating applied to the minion.")]
    [Range(0f, 2f)]
    public float ownerMagicResistFraction;

    [Tooltip("Fraction of owner total Corruption Resist rating applied to the minion.")]
    [Range(0f, 2f)]
    public float ownerCorruptionResistFraction;

    public static MinionDefensiveInheritance InheritFullOwner => new MinionDefensiveInheritance
    {
        inheritOwnerDefenses = true,
        ownerArmourFraction = 1f,
        ownerMagicResistFraction = 1f,
        ownerCorruptionResistFraction = 1f
    };
}
