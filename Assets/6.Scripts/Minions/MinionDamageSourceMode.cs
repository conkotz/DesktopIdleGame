using UnityEngine;

/// <summary>
/// How a minion resolves combat stats: inherit owner weapon vs minion-only numbers (see <see cref="MinionCombatConfig"/>).
/// </summary>
public enum MinionDamageSourceMode
{
    /// <summary>
    /// Owner weapon: hit split, APS, crit, crit mult, and ailment chances (each scaled by inherit coefficients), plus owner durations/multipliers on apply.
    /// </summary>
    [InspectorName("Inherit owner weapon hit")]
    InheritOwnerHitSplit = 0,

    /// <summary>
    /// Minion-only damage, APS, crit, and ailments from <see cref="MinionCombatConfig"/> — not owner weapon hit/APS/crit (still gets owner % bonuses like FinalMinionDamagePercent).
    /// </summary>
    [InspectorName("Pure minion source damage")]
    PureMinionSourceDamage = 1,
}
