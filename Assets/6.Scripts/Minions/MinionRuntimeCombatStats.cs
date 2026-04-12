/// <summary>
/// Per-tick or per-attack resolved values for a minion after owner bonuses.
/// </summary>
public struct MinionRuntimeCombatStats
{
    public SplitDamage FinalDamageSplit;
    public float AttacksPerSecond;
    /// <summary>0–1, suitable for <c>Random.value &lt; CritChance</c>; matches player-style clamp.</summary>
    public float CritChance;
    /// <summary>Always <see cref="CharacterStats.MinionCritDamageMultiplier"/> (not scalable).</summary>
    public float CritDamageMultiplier;
    /// <summary>Copied from config for now; Phase 3+ may apply buffs or scaling here.</summary>
    public MinionAilmentChances AilmentChances;

    public static MinionRuntimeCombatStats Zero => new MinionRuntimeCombatStats
    {
        FinalDamageSplit = SplitDamage.Zero,
        AttacksPerSecond = 0f,
        CritChance = 0f,
        CritDamageMultiplier = CharacterStats.MinionCritDamageMultiplier,
        AilmentChances = MinionAilmentChances.Zero
    };
}
