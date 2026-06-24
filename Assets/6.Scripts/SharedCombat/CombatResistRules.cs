using UnityEngine;

/// <summary>Shared rules for combat resist ratings (armour, MR, corruption resist, etc.).</summary>
public static class CombatResistRules
{
    /// <summary>Maximum damage reduction from a single resist rating (70%).</summary>
    public const float MaxDamageReductionFraction = 0.70f;

    /// <summary>
    /// Soft-scaling constant for <c>DR = max × rating / (rating + scale)</c>.
    /// Tuned for gentler low-rating DR (~14% at 50, ~50% at 500, approaching 70% at very high rating).
    /// </summary>
    public const float RatingMitigationScale = 200f;

    public static int ClampRating(int rating) => Mathf.Max(0, rating);

    public static float ClampRating(float rating) => Mathf.Max(0f, rating);

    /// <summary>Fraction of damage taken after resist rating (0.30 = 70% DR).</summary>
    public static float GetDamageTakenMultiplierFromRating(float rating)
    {
        rating = ClampRating(rating);
        if (rating <= 0f)
            return 1f;

        float reductionFraction = MaxDamageReductionFraction * rating / (rating + RatingMitigationScale);
        return 1f - reductionFraction;
    }

    /// <summary>Damage reduction percent points from rating (70 = 70% DR).</summary>
    public static float GetDamageReductionPercentFromRating(float rating)
    {
        rating = ClampRating(rating);
        if (rating <= 0f)
            return 0f;

        return MaxDamageReductionFraction * rating / (rating + RatingMitigationScale) * 100f;
    }

    /// <summary>Applies armour / MR / corruption resist mitigation to incoming damage.</summary>
    public static float ApplyRatingMitigation(float damage, float rating)
    {
        if (damage <= 0f)
            return 0f;

        return damage * GetDamageTakenMultiplierFromRating(rating);
    }

    /// <summary>Reduces a resist rating by a fraction of its current value; result is never below zero.</summary>
    public static int ApplyRatingPercentReduction(int rating, float reductionFraction01)
    {
        rating = ClampRating(rating);
        if (rating <= 0 || reductionFraction01 <= 0f)
            return rating;

        float next = rating * (1f - Mathf.Clamp01(reductionFraction01));
        return ClampRating(Mathf.FloorToInt(next));
    }
}
