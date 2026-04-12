/// <summary>
/// How a minion resolves its pre-bonus hit <see cref="SplitDamage"/> before owner <see cref="CharacterStats.FinalMinionDamagePercent"/> is applied.
/// Does not imply inheritance of crit, APS, ailments, or on-hit procs from the player.
/// </summary>
public enum MinionDamageSourceMode
{
    /// <summary>
    /// Use a caller-supplied slice of the owner&apos;s current hit split (weapon/ability context).
    /// Only <see cref="SplitDamage"/> is taken from the owner; see <see cref="MinionRuntimeStatsCalculator"/> remarks.
    /// </summary>
    InheritOwnerHitSplit = 0,

    /// <summary>
    /// Use <see cref="MinionCombatConfig.baseDamageSplit"/> defined on the minion (wolf, spirit, etc.).
    /// </summary>
    UseBaseDamageSplit = 1,
}
