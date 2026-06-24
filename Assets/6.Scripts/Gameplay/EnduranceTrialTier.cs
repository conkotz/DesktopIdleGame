using UnityEngine;

/// <summary>
/// Endurance trial difficulty Tier I–V: per-tier enemy scaling and UI helpers.
/// Default formula: Tier I baseline; each step adds +50% health and +25% damage / armour / MR from Tier I values.
/// Override with a serialized <see cref="EnduranceTrialDifficultyScaling"/> asset via <see cref="SetDifficultyScaling"/> or <see cref="EnduranceTrialDifficultyBootstrap"/>.
/// </summary>
public static class EnduranceTrialTier
{
    public const int MinTier = 1;
    public const int MaxTier = 5;

    private static EnduranceTrialDifficultyScaling _activeScaling;

    /// <summary>Currently applied scaling asset, if any.</summary>
    public static EnduranceTrialDifficultyScaling ActiveScaling => _activeScaling;

    /// <summary>Use null to fall back to the built-in formula.</summary>
    public static void SetDifficultyScaling(EnduranceTrialDifficultyScaling scaling)
    {
        _activeScaling = scaling;
    }

    /// <summary>0-based index (0 = Tier I, 4 = Tier V).</summary>
    public static int ToTierIndex(int tier1Based)
    {
        return Mathf.Clamp(tier1Based, MinTier, MaxTier) - 1;
    }

    /// <summary>Health multiplier vs Tier I (Tier I = 1, Tier V = 3 with default formula).</summary>
    public static float GetHealthMultiplier(int tier1Based)
    {
        if (_activeScaling != null && _activeScaling.TryGetHealthMultiplier(tier1Based, out float m))
            return m;
        return DefaultHealthMultiplier(tier1Based);
    }

    /// <summary>Outgoing damage multiplier vs Tier I.</summary>
    public static float GetDamageMultiplier(int tier1Based)
    {
        if (_activeScaling != null && _activeScaling.TryGetDamageMultiplier(tier1Based, out float m))
            return m;
        return DefaultDamageMultiplier(tier1Based);
    }

    /// <summary>Armour and magic resist multiplier vs Tier I.</summary>
    public static float GetArmourAndResistMultiplier(int tier1Based)
    {
        if (_activeScaling != null && _activeScaling.TryGetArmourAndResistMultiplier(tier1Based, out float m))
            return m;
        return DefaultArmourAndResistMultiplier(tier1Based);
    }

    private static float DefaultHealthMultiplier(int tier1Based) => 1f + 0.5f * ToTierIndex(tier1Based);

    private static float DefaultDamageMultiplier(int tier1Based) => 1f + 0.25f * ToTierIndex(tier1Based);

    private static float DefaultArmourAndResistMultiplier(int tier1Based) => 1f + 0.25f * ToTierIndex(tier1Based);

    /// <summary>Feeds <see cref="RecommendedCombatPower.Options.TierMultiplier"/> for recommended CP display.</summary>
    public static float GetRecommendedCpTierMultiplier(int tier1Based)
    {
        float h = GetHealthMultiplier(tier1Based);
        float d = GetDamageMultiplier(tier1Based);
        float a = GetArmourAndResistMultiplier(tier1Based);
        return (h + d + a) / 3f;
    }

    public static string ToRomanNumeral(int tier1Based)
    {
        return Mathf.Clamp(tier1Based, MinTier, MaxTier) switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => "I"
        };
    }

    public static RecommendedCombatPower.Options BuildRecommendedCpOptions(int tier1Based)
    {
        RecommendedCombatPower.Options d = RecommendedCombatPower.Options.Default;
        float tm = GetRecommendedCpTierMultiplier(tier1Based);
        return new RecommendedCombatPower.Options(d.WaveStressPerExtraWave, d.EnemyStressPerExtraInstance, tm, d.MinimumRecommended);
    }
}

/// <summary>
/// Tier chosen in the pre-trial UI; read when <see cref="EnduranceTrialDirector.ConfirmBeginTrial"/> runs.
/// </summary>
public static class EnduranceTrialPendingTier
{
    public static int Tier { get; set; } = 1;
}
