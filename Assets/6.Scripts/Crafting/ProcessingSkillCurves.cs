using UnityEngine;

/// <summary>XP pacing for processing proficiencies — uses the same level curve as gather/combat skills.</summary>
public static class ProcessingSkillCurves
{
    public const int MaxLevel = 50;

    public static int XpToNextLevel(int currentLevel) =>
        currentLevel >= MaxLevel ? 0 : SkillCurves.XpToNextLevel(Mathf.Max(1, currentLevel));

    /// <summary>Smelting proficiency XP granted when a bar finishes (tune per tier here).</summary>
    public static int GetSmeltingBarXp(SmeltingRecipe recipe)
    {
        string barId = recipe.BarItemId;
        if (string.IsNullOrWhiteSpace(barId))
            return 10;

        return barId.ToLowerInvariant() switch
        {
            "iron_bar" => 10,
            "mythril_bar" => 15,
            "runite_bar" => 20,
            "celestium_bar" => 25,
            _ => 10
        };
    }

    /// <summary>Cooking proficiency XP granted when a portion finishes.</summary>
    public static int GetCookingFishXp(CookingRecipe recipe)
    {
        string rawId = recipe.RawItemId;
        if (string.IsNullOrWhiteSpace(rawId))
            return 10;

        return rawId.ToLowerInvariant() switch
        {
            "raw_fish" => 10,
            "raw_perch" => 12,
            "raw_pike" => 15,
            _ => 10
        };
    }

    public static int GetBlacksmithingCraftXp(BlacksmithingRecipe recipe)
    {
        string outputId = recipe.OutputItemId;
        if (string.IsNullOrWhiteSpace(outputId))
            return 15;

        return outputId.ToLowerInvariant() switch
        {
            "stone_sword" or "stone_dagger" or "stone_shield" or "stone_helmet" or "stone_platebody" => 12,
            "stone_spear" or "poison_dagger" => 18,
            "knights_polearm" => 28,
            "ghorrocks_mace" => 35,
            _ => 15
        };
    }
}
