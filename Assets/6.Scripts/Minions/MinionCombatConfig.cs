using UnityEngine;

/// <summary>
/// Per-minion definition data (inspector, ScriptableObject field, or prefab). Drives <see cref="MinionRuntimeStatsCalculator"/>.
/// </summary>
[System.Serializable]
public struct MinionCombatConfig
{
    [Header("Damage source")]
    [Tooltip("Inherit: use owner hit split × coefficient. Base: use baseDamageSplit only.")]
    public MinionDamageSourceMode damageSourceMode;

    [Tooltip("Applied to owner hit split when mode is InheritOwnerHitSplit (e.g. 0.5 = half of that hit). Ignored for UseBaseDamageSplit.")]
    [Min(0f)]
    public float inheritDamageCoefficient;

    [Tooltip("Internal per-hit damage when mode is UseBaseDamageSplit.")]
    public SplitDamage baseDamageSplit;

    [Header("Combat pacing")]
    [Tooltip("Attacks per second before owner FinalMinionAttackSpeedPercent.")]
    [Min(0f)]
    public float baseAttackSpeed;

    [Header("Crit (minion-local base)")]
    [Tooltip("0–1 additive; owner FinalMinionCritChance is added in the runtime calculator.")]
    [Range(0f, 1f)]
    public float baseCritChance;

    [Header("Future: minion-specific ailments")]
    [Tooltip("Not applied by the calculator yet; passed through on MinionRuntimeCombatStats for future hit logic.")]
    public MinionAilmentChances ailmentChances;
}
