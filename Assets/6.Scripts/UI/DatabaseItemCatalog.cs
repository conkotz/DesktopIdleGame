using System;
using System.Collections.Generic;
using UnityEngine;

public enum DatabaseItemSubtab
{
    Resources = 0,
    Equipment = 1,
    Consumables = 2,
    Enhancement = 3,
}

/// <summary>Filters and sorts items for the database Items section.</summary>
public static class DatabaseItemCatalog
{
    private const string BasicScrollExampleId = "basic_weapon_physical_scroll";
    private const string IntermediateScrollExampleId = "intermediate_weapon_physical_scroll";
    private const string AdvancedScrollExampleId = "advanced_weapon_physical_scroll";

    private static readonly HashSet<string> RepresentativeScrollIds = new(StringComparer.OrdinalIgnoreCase)
    {
        BasicScrollExampleId,
        IntermediateScrollExampleId,
        AdvancedScrollExampleId,
        MapCombatScalingSpecialDropDefaults.SlotReductionScrollItemId,
        MapCombatScalingSpecialDropDefaults.MapEnhancementTier1ItemId,
        MapCombatScalingSpecialDropDefaults.MapEnhancementTier2ItemId,
    };

    public static bool ShouldListInDatabase(ItemDefinition item)
    {
        if (!item || string.IsNullOrWhiteSpace(item.itemId))
            return false;

        if (item.itemKind == ItemKind.Quest)
            return false;

        if (!item.IsEnhancementScroll)
            return true;

        return IsRepresentativeEnhancementScroll(item);
    }

    public static bool PassesSubtab(ItemDefinition item, DatabaseItemSubtab subtab)
    {
        if (!item)
            return false;

        return subtab switch
        {
            DatabaseItemSubtab.Resources => item.itemKind == ItemKind.Resource,
            DatabaseItemSubtab.Equipment => item.itemKind == ItemKind.Weapon ||
                                              item.itemKind == ItemKind.Armor ||
                                              item.itemKind == ItemKind.CombatSupport ||
                                              item.itemKind == ItemKind.Tool ||
                                              item.itemKind == ItemKind.Jewelry,
            DatabaseItemSubtab.Consumables => item.itemKind == ItemKind.Consumable && !item.IsMapEnhancement,
            DatabaseItemSubtab.Enhancement => item.IsEnhancementScroll || item.IsMapEnhancement,
            _ => false,
        };
    }

    public static List<ItemDefinition> CollectForSubtab(
        IReadOnlyList<ItemDefinition> allItems,
        DatabaseItemSubtab subtab)
    {
        var results = new List<ItemDefinition>();
        if (allItems == null)
            return results;

        for (int i = 0; i < allItems.Count; i++)
        {
            ItemDefinition item = allItems[i];
            if (!ShouldListInDatabase(item))
                continue;
            if (!PassesSubtab(item, subtab))
                continue;

            results.Add(item);
        }

        results.Sort(CompareByDisplayName);
        return results;
    }

    public static bool IsRepresentativeEnhancementScroll(ItemDefinition item)
    {
        if (!item || string.IsNullOrWhiteSpace(item.itemId))
            return false;

        if (item.IsMapEnhancement)
            return true;

        string id = item.itemId.Trim();
        return RepresentativeScrollIds.Contains(id);
    }

    private static int CompareByDisplayName(ItemDefinition a, ItemDefinition b)
    {
        string nameA = a != null ? a.displayName : string.Empty;
        string nameB = b != null ? b.displayName : string.Empty;
        return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
    }
}
