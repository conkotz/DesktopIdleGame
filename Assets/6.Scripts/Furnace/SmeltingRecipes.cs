using System.Collections.Generic;
using UnityEngine;

public readonly struct SmeltingRecipe
{
    public readonly string OreItemId;
    public readonly string BarItemId;
    public readonly int OrePerBar;
    public readonly float SecondsPerBar;
    public readonly int RequiredSmeltingLevel;

    public SmeltingRecipe(
        string oreItemId,
        string barItemId,
        int orePerBar,
        float secondsPerBar,
        int requiredSmeltingLevel = 1)
    {
        OreItemId = oreItemId;
        BarItemId = barItemId;
        OrePerBar = Mathf.Max(1, orePerBar);
        SecondsPerBar = Mathf.Max(0.1f, secondsPerBar);
        RequiredSmeltingLevel = Mathf.Max(1, requiredSmeltingLevel);
    }
}

/// <summary>Authoritative ore → bar smelting timings for furnace stations.</summary>
public static class SmeltingRecipes
{
    public const int DefaultOrePerBar = 5;

    private static readonly SmeltingRecipe[] Recipes =
    {
        new("iron_ore", "iron_bar", DefaultOrePerBar, 10f, requiredSmeltingLevel: 1),
        new("mythril_ore", "mythril_bar", DefaultOrePerBar, 15f, requiredSmeltingLevel: 10),
        new("runite_ore", "runite_bar", DefaultOrePerBar, 20f, requiredSmeltingLevel: 20),
        new("celestium_ore", "celestium_bar", DefaultOrePerBar, 25f, requiredSmeltingLevel: 30),
    };

    private static readonly Dictionary<string, SmeltingRecipe> ByOre = BuildMap();

    public static IReadOnlyList<SmeltingRecipe> All => Recipes;

    public static bool TryGetForOre(string oreItemId, out SmeltingRecipe recipe)
    {
        recipe = default;
        if (string.IsNullOrWhiteSpace(oreItemId))
            return false;

        string key = oreItemId.Trim().ToLowerInvariant();
        return ByOre.TryGetValue(key, out recipe);
    }

    public static bool IsSmeltableOre(string oreItemId) => TryGetForOre(oreItemId, out _);

    public static int GetRequiredSmeltingLevel(string oreItemId)
    {
        if (!TryGetForOre(oreItemId, out SmeltingRecipe recipe))
            return 0;

        ItemDatabase db = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        ItemDefinition def = db != null ? db.Get(oreItemId) : null;
        if (def != null && def.smeltableStats.requiredSmeltingLevel > 0)
            return def.RequiredSmeltingLevel;

        return recipe.RequiredSmeltingLevel;
    }

    public static int GetRequiredSmeltingLevel(SmeltingRecipe recipe) =>
        GetRequiredSmeltingLevel(recipe.OreItemId);

    private static Dictionary<string, SmeltingRecipe> BuildMap()
    {
        var map = new Dictionary<string, SmeltingRecipe>(Recipes.Length);
        for (int i = 0; i < Recipes.Length; i++)
            map[Recipes[i].OreItemId] = Recipes[i];
        return map;
    }
}
