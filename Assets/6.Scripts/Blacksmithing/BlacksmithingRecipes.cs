using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BlacksmithingIngredient
{
    public readonly string ItemId;
    public readonly int Amount;

    public BlacksmithingIngredient(string itemId, int amount)
    {
        ItemId = itemId ?? "";
        Amount = Mathf.Max(1, amount);
    }
}

public readonly struct BlacksmithingRecipe
{
    public readonly string OutputItemId;
    public readonly string Category;
    public readonly int RequiredLevel;
    public readonly float CraftSeconds;
    public readonly BlacksmithingIngredient[] Ingredients;

    public BlacksmithingRecipe(
        string outputItemId,
        string category,
        int requiredLevel,
        float craftSeconds,
        params BlacksmithingIngredient[] ingredients)
    {
        OutputItemId = outputItemId ?? "";
        Category = category ?? "Other";
        RequiredLevel = Mathf.Max(1, requiredLevel);
        CraftSeconds = Mathf.Max(0.1f, craftSeconds);
        Ingredients = ingredients ?? Array.Empty<BlacksmithingIngredient>();
    }
}

/// <summary>
/// Player-crafted gear at the anvil. Item costs mirror <c>blacksmith_merchant.asset</c> (gold excluded).
/// </summary>
public static class BlacksmithingRecipes
{
    public const float DefaultCraftSeconds = 15f;

    public static readonly string[] CategoryOrder =
    {
        "Stone",
        "Iron",
        "Mythril",
        "Runite",
        "Celestium",
        "Special",
    };

    private static readonly BlacksmithingRecipe[] Recipes =
    {
        new("stone_sword", "Stone", 1, DefaultCraftSeconds, new BlacksmithingIngredient("stone_chunk", 10)),
        new("stone_dagger", "Stone", 1, DefaultCraftSeconds, new BlacksmithingIngredient("stone_chunk", 8)),
        new("stone_shield", "Stone", 1, DefaultCraftSeconds, new BlacksmithingIngredient("stone_chunk", 40)),
        new("stone_platebody", "Stone", 1, DefaultCraftSeconds, new BlacksmithingIngredient("stone_chunk", 50)),
        new("stone_helmet", "Stone", 1, DefaultCraftSeconds, new BlacksmithingIngredient("stone_chunk", 30)),
        new("stone_spear", "Iron", 5, DefaultCraftSeconds, new BlacksmithingIngredient("iron_bar", 30)),
        new("poison_dagger", "Special", 5, DefaultCraftSeconds,
            new BlacksmithingIngredient("stone_chunk", 1),
            new BlacksmithingIngredient("vial_poison", 25)),
        new("knights_polearm", "Runite", 20, DefaultCraftSeconds,
            new BlacksmithingIngredient("mythril_bar", 20),
            new BlacksmithingIngredient("runite_bar", 30)),
        new("ghorrocks_mace", "Celestium", 30, DefaultCraftSeconds, new BlacksmithingIngredient("celestium_bar", 80)),
    };

    private static readonly Dictionary<string, BlacksmithingRecipe> ByOutput = BuildMap();

    public static IReadOnlyList<BlacksmithingRecipe> All => Recipes;

    public static bool TryGetForOutput(string outputItemId, out BlacksmithingRecipe recipe)
    {
        recipe = default;
        if (string.IsNullOrWhiteSpace(outputItemId))
            return false;

        return ByOutput.TryGetValue(outputItemId.Trim().ToLowerInvariant(), out recipe);
    }

    public static IReadOnlyList<BlacksmithingRecipe> GetRecipesForCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Array.Empty<BlacksmithingRecipe>();

        string key = category.Trim();
        var list = new List<BlacksmithingRecipe>(8);
        for (int i = 0; i < Recipes.Length; i++)
        {
            if (string.Equals(Recipes[i].Category, key, StringComparison.OrdinalIgnoreCase))
                list.Add(Recipes[i]);
        }

        return list;
    }

    public static int GetRequiredLevel(string outputItemId)
    {
        return TryGetForOutput(outputItemId, out BlacksmithingRecipe recipe) ? recipe.RequiredLevel : 1;
    }

    public static int GetEffectiveIngredientAmount(int baseAmount, float resourceCostReductionPercent)
    {
        if (baseAmount <= 0)
            return 0;

        float mul = 1f - Mathf.Clamp(resourceCostReductionPercent, 0f, 50f) / 100f;
        return Mathf.Max(1, Mathf.FloorToInt(baseAmount * mul));
    }

    private static Dictionary<string, BlacksmithingRecipe> BuildMap()
    {
        var map = new Dictionary<string, BlacksmithingRecipe>(Recipes.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Recipes.Length; i++)
            map[Recipes[i].OutputItemId] = Recipes[i];
        return map;
    }
}
