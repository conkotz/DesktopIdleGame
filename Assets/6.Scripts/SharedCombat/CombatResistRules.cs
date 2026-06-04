using UnityEngine;

/// <summary>Shared rules for combat resist ratings (armor, MR, corruption resist, etc.).</summary>
public static class CombatResistRules
{
    public static int ClampRating(int rating) => Mathf.Max(0, rating);

    public static float ClampRating(float rating) => Mathf.Max(0f, rating);

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
