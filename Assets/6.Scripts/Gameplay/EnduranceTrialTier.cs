using UnityEngine;

/// <summary>
/// Endurance trial difficulty Tier I–V: per-tier enemy scaling and UI helpers.
/// Tier I baseline; each step adds +50% health and +25% damage / armor / MR from Tier I values.
/// </summary>
public static class EnduranceTrialTier
{
    public const int MinTier = 1;
    public const int MaxTier = 5;

    /// <summary>0-based index (0 = Tier I, 4 = Tier V).</summary>
    public static int ToTierIndex(int tier1Based)
    {
        return Mathf.Clamp(tier1Based, MinTier, MaxTier) - 1;
    }

    /// <summary>Health multiplier vs Tier I (Tier I = 1, Tier V = 3).</summary>
    public static float GetHealthMultiplier(int tier1Based)
    {
        return 1f + 0.5f * ToTierIndex(tier1Based);
    }

    /// <summary>Outgoing damage multiplier vs Tier I.</summary>
    public static float GetDamageMultiplier(int tier1Based)
    {
        return 1f + 0.25f * ToTierIndex(tier1Based);
    }

    /// <summary>Armor and magic resist multiplier vs Tier I.</summary>
    public static float GetArmorAndResistMultiplier(int tier1Based)
    {
        return 1f + 0.25f * ToTierIndex(tier1Based);
    }

    /// <summary>Feeds <see cref="RecommendedCombatPower.Options.TierMultiplier"/> for recommended CP display.</summary>
    public static float GetRecommendedCpTierMultiplier(int tier1Based)
    {
        float h = GetHealthMultiplier(tier1Based);
        float d = GetDamageMultiplier(tier1Based);
        float a = GetArmorAndResistMultiplier(tier1Based);
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
