using System.Collections.Generic;
using UnityEngine;

public readonly struct CookingRecipe
{
    public readonly string RawItemId;
    public readonly string CookedItemId;
    public readonly int RawPerCooked;
    public readonly float SecondsPerCooked;

    public CookingRecipe(string rawItemId, string cookedItemId, int rawPerCooked, float secondsPerCooked)
    {
        RawItemId = rawItemId;
        CookedItemId = cookedItemId;
        RawPerCooked = Mathf.Max(1, rawPerCooked);
        SecondsPerCooked = Mathf.Max(0.1f, secondsPerCooked);
    }
}

/// <summary>Authoritative raw fish → cooked food mappings for cooking ranges. Timing comes from item definitions when set.</summary>
public static class CookingRecipes
{
    public const int DefaultRawPerCooked = 1;

    private static readonly CookingRecipe[] Recipes =
    {
        new("raw_fish", "cooked_fish", DefaultRawPerCooked, 5f),
        new("raw_perch", "cooked_perch", DefaultRawPerCooked, 7f),
        new("raw_pike", "cooked_pike", DefaultRawPerCooked, 9f),
    };

    private static readonly Dictionary<string, CookingRecipe> ByRaw = BuildMap();

    public static IReadOnlyList<CookingRecipe> All => Recipes;

    public static bool TryGetForRaw(string rawItemId, out CookingRecipe recipe)
    {
        recipe = default;
        if (string.IsNullOrWhiteSpace(rawItemId))
            return false;

        string key = rawItemId.Trim().ToLowerInvariant();
        if (!ByRaw.TryGetValue(key, out CookingRecipe baseRecipe))
            return false;

        float seconds = ResolveCookingTimeSeconds(key, baseRecipe.SecondsPerCooked);
        recipe = new CookingRecipe(baseRecipe.RawItemId, baseRecipe.CookedItemId, baseRecipe.RawPerCooked, seconds);
        return true;
    }

    public static bool IsCookableRaw(string rawItemId) => TryGetForRaw(rawItemId, out _);

    private static float ResolveCookingTimeSeconds(string rawItemId, float fallbackSeconds)
    {
        ItemDefinition def = ResolveItemDefinition(rawItemId);
        if (def != null && def.CookingTimeSeconds > 0f)
            return def.CookingTimeSeconds;

        return fallbackSeconds;
    }

    private static ItemDefinition ResolveItemDefinition(string rawItemId)
    {
        Inventory inv = Inventory.ResolvePlayer();
        ItemDatabase db = inv != null ? inv.GetItemDatabase() : null;
        if (db == null)
            db = Resources.Load<ItemDatabase>("Databases/ItemDatabase");

        return db?.Get(rawItemId);
    }

    private static Dictionary<string, CookingRecipe> BuildMap()
    {
        var map = new Dictionary<string, CookingRecipe>(Recipes.Length);
        for (int i = 0; i < Recipes.Length; i++)
            map[Recipes[i].RawItemId] = Recipes[i];
        return map;
    }
}
