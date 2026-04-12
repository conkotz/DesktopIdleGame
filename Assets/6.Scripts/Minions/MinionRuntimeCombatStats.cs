/// <summary>
/// Per-tick or per-attack resolved values for a minion after owner bonuses.
/// </summary>
public struct MinionRuntimeCombatStats
{
    /// <summary>Resolved after owner bonuses; each hit rolls in this range (inherit mode uses min=max).</summary>
    public SplitDamageRange FinalDamageSplitRange;
    public float AttacksPerSecond;
    /// <summary>0–1, suitable for <c>Random.value &lt; CritChance</c>; matches player-style clamp.</summary>
    public float CritChance;
    /// <summary>Resolved crit multiplier (owner weapon when inheriting, or minion default for pure minion source damage).</summary>
    public float CritDamageMultiplier;
    /// <summary>Copied from config for now; Phase 3+ may apply buffs or scaling here.</summary>
    public MinionAilmentChances AilmentChances;
    /// <summary>Magic-weapon chill / shock roll when <see cref="MinionDamageSourceMode.InheritOwnerHitSplit"/> (scaled owner chance).</summary>
    public float MagicAilmentApplyChance;

    public static MinionRuntimeCombatStats Zero => new MinionRuntimeCombatStats
    {
        FinalDamageSplitRange = SplitDamageRange.Uniform(SplitDamage.Zero),
        AttacksPerSecond = 0f,
        CritChance = 0f,
        CritDamageMultiplier = CharacterStats.MinionCritDamageMultiplier,
        AilmentChances = MinionAilmentChances.Zero,
        MagicAilmentApplyChance = 0f
    };
}
