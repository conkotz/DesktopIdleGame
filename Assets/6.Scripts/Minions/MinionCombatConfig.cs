using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Per-minion definition data (inspector, ScriptableObject field, or prefab). Drives <see cref="MinionRuntimeStatsCalculator"/>.
/// </summary>
[System.Serializable]
public struct MinionCombatConfig
{
    [Header("Damage source")]
    [Tooltip("Inherit: owner weapon hit split + weapon APS/crit/ailments (each scaled below). Pure minion source: minion-only split, APS, crit, and ailments below (not owner weapon hit stats).")]
    public MinionDamageSourceMode damageSourceMode;

    [Header("Inherit — scale owner weapon (InheritOwnerHitSplit only)")]
    [Tooltip("Multiplier on owner Min/Max hit split snapshot (before FinalMinionDamagePercent).")]
    [Min(0f)]
    public float inheritDamageCoefficient;

    [Tooltip("Multiplier on owner AttacksPerSecond (before FinalMinionAttackSpeedPercent). 0 = treat as 1 (C# 9 / legacy assets).")]
    [Min(0f)]
    public float inheritAttackSpeedCoefficient;

    [Tooltip("Multiplier on owner CritChance (0–1), then add FinalMinionCritChance. 0 = treat as 1.")]
    [Min(0f)]
    public float inheritCritChanceCoefficient;

    [Tooltip("Scales crit multiplier excess above 1×: 1 + (ownerCritMult − 1) × this. 0 = treat as 1.")]
    [Min(0f)]
    public float inheritCritMultiplierCoefficient;

    [Tooltip("Multiplier on owner bleed/poison/burn/shock/magic-ailment apply chances. 0 = treat as 1.")]
    [Min(0f)]
    public float inheritAilmentChanceCoefficient;

    [Header("Pure minion source damage — hit split (PureMinionSourceDamage mode only)")]
    [Tooltip("Minion-only min/max per damage type; rolls each hit (like player basics). Ignored when inheriting owner weapon.")]
    [FormerlySerializedAs("baseDamageSplitRange")]
    public SplitDamageRange pureMinionDamageSplitRange;

    [SerializeField, HideInInspector, FormerlySerializedAs("baseDamageSplit")]
    private SplitDamage _legacyBaseDamageSplit;

    /// <summary>Maps pre-range assets: single split → min=max; clears legacy payload.</summary>
    public static MinionCombatConfig AfterDeserialize(MinionCombatConfig c)
    {
        if (!c._legacyBaseDamageSplit.IsEmpty && c.pureMinionDamageSplitRange.IsUnset)
            c.pureMinionDamageSplitRange = SplitDamageRange.Uniform(c._legacyBaseDamageSplit);
        c._legacyBaseDamageSplit = default;
        return c;
    }

    [Header("Pure minion source damage — attack speed & crit (PureMinionSourceDamage mode only)")]
    [Tooltip("Minion APS before owner FinalMinionAttackSpeedPercent. Ignored when inheriting owner weapon.")]
    [Min(0f)]
    [FormerlySerializedAs("baseAttackSpeed")]
    public float pureMinionAttackSpeed;

    [Tooltip("0–1 minion crit chance; owner FinalMinionCritChance is added in the runtime calculator.")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("baseCritChance")]
    public float pureMinionCritChance;

    [Header("Pure minion source damage — ailments (PureMinionSourceDamage mode only)")]
    [Tooltip("On-hit ailment rolls when using pure minion source damage (not owner weapon chances).")]
    public MinionAilmentChances ailmentChances;
}
