using System.Collections.Generic;
using UnityEngine;

public readonly struct SmeltingRecipe
{
    public readonly string OreItemId;
    public readonly string BarItemId;
    public readonly int OrePerBar;
    public readonly float SecondsPerBar;

    public SmeltingRecipe(string oreItemId, string barItemId, int orePerBar, float secondsPerBar)
    {
        OreItemId = oreItemId;
        BarItemId = barItemId;
        OrePerBar = Mathf.Max(1, orePerBar);
        SecondsPerBar = Mathf.Max(0.1f, secondsPerBar);
    }
}

/// <summary>Authoritative ore → bar smelting timings for furnace stations.</summary>
public static class SmeltingRecipes
{
    public const int DefaultOrePerBar = 5;

    private static readonly SmeltingRecipe[] Recipes =
    {
        new("iron_ore", "iron_bar", DefaultOrePerBar, 20f),
        new("mythril_ore", "mythril_bar", DefaultOrePerBar, 40f),
        new("runite_ore", "runite_bar", DefaultOrePerBar, 60f),
        new("celestium_ore", "celestium_bar", DefaultOrePerBar, 90f),
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

    private static Dictionary<string, SmeltingRecipe> BuildMap()
    {
        var map = new Dictionary<string, SmeltingRecipe>(Recipes.Length);
        for (int i = 0; i < Recipes.Length; i++)
            map[Recipes[i].OreItemId] = Recipes[i];
        return map;
    }
}
