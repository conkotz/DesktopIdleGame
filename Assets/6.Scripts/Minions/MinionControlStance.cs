/// <summary>
/// Player-directed minion engagement rules for controllable summons (e.g. Soulforged Warrior).
/// </summary>
public enum MinionControlStance
{
    /// <summary>Attack the nearest valid enemy in range (default).</summary>
    Aggressive = 0,
    /// <summary>Only attack the player's current combat target; idle near the player otherwise.</summary>
    Assist = 1,
    /// <summary>Never auto-engage; retaliate only after being struck by an enemy.</summary>
    Passive = 2,
}
