using UnityEngine;

public static class SkillCurves
{
    // XP required to go from level L -> L+1
    // Simple curve you can tweak anytime.
    public static int XpToNextLevel(int currentLevel)
    {
        currentLevel = Mathf.Max(1, currentLevel);

        // Example curve:
        // L1->2: 50, L2->3: 75, grows gradually
        // Tweak these values later.
        return Mathf.RoundToInt(50f + (currentLevel - 1) * 25f + Mathf.Pow(currentLevel - 1, 1.35f) * 10f);
    }
}