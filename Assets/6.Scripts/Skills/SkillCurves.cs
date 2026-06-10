using UnityEngine;

/// <summary>
/// XP required to advance from level L to L+1. Tuned for long high-level grinds:
/// L1–10 similar to the legacy curve, steep ramp toward ~250k at 49→50, harsher past 50.
/// </summary>
public static class SkillCurves
{
    private const int LowLevelCap = 10;
    private const int PreFiftyCap = 49;
    private const int XpAt49To50 = 250_000;
    private const int PostFiftyBaseXp = 300_000;
    private const float PostFiftyGrowthPerLevel = 1.20f;

    public static int XpToNextLevel(int currentLevel)
    {
        currentLevel = Mathf.Max(1, currentLevel);

        if (currentLevel <= LowLevelCap)
            return LowLevelXpToNext(currentLevel);

        if (currentLevel < PreFiftyCap)
            return MidLevelXpToNext(currentLevel);

        return PostFiftyXpToNext(currentLevel);
    }

    /// <summary>Legacy-style curve kept for levels 1–10 (~50 XP at L1→2, ~416 at L10→11).</summary>
    private static int LowLevelXpToNext(int currentLevel) =>
        Mathf.RoundToInt(50f + (currentLevel - 1) * 30f + Mathf.Pow(currentLevel - 1, 1.35f) * 12f);

    /// <summary>Exponential ramp from the L10 cost up to <see cref="XpAt49To50"/> at 49→50.</summary>
    private static int MidLevelXpToNext(int currentLevel)
    {
        int anchorXp = LowLevelXpToNext(LowLevelCap);
        float span = PreFiftyCap - LowLevelCap;
        float t = (currentLevel - LowLevelCap) / span;
        float ratio = XpAt49To50 / (float)Mathf.Max(1, anchorXp);
        return Mathf.RoundToInt(anchorXp * Mathf.Pow(ratio, t));
    }

    /// <summary>Post-50: base step above 49→50, then +20% XP cost per level.</summary>
    private static int PostFiftyXpToNext(int currentLevel)
    {
        int levelsPastFifty = currentLevel - 50;
        return Mathf.RoundToInt(PostFiftyBaseXp * Mathf.Pow(PostFiftyGrowthPerLevel, levelsPastFifty));
    }
}
