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

        if (subtab == DatabaseItemSubtab.Enhancement)
            results.Sort(CompareEnhancementDisplayOrder);
        else
            results.Sort(CompareByDisplayName);

        return results;
    }

    private static int CompareEnhancementDisplayOrder(ItemDefinition a, ItemDefinition b)
    {
        int orderA = GetEnhancementDisplaySortOrder(a);
        int orderB = GetEnhancementDisplaySortOrder(b);
        if (orderA != orderB)
            return orderA.CompareTo(orderB);

        return CompareByDisplayName(a, b);
    }

    private static int GetEnhancementDisplaySortOrder(ItemDefinition item)
    {
        if (!item || string.IsNullOrWhiteSpace(item.itemId))
            return 99;

        string id = item.itemId.Trim();
        if (string.Equals(id, BasicScrollExampleId, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (string.Equals(id, IntermediateScrollExampleId, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (string.Equals(id, AdvancedScrollExampleId, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (string.Equals(id, MapCombatScalingSpecialDropDefaults.SlotReductionScrollItemId, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (string.Equals(id, MapCombatScalingSpecialDropDefaults.MapEnhancementTier1ItemId, StringComparison.OrdinalIgnoreCase))
            return 4;
        if (string.Equals(id, MapCombatScalingSpecialDropDefaults.MapEnhancementTier2ItemId, StringComparison.OrdinalIgnoreCase))
            return 5;

        return 50;
    }

    public const string EnhancementScaledMapsLocationText =
        "All scaled maps -> view scaling in each combat map area";

    public static string FormatDatabaseDisplayName(ItemDefinition item)
    {
        if (!item || string.IsNullOrWhiteSpace(item.itemId))
            return string.Empty;

        string id = item.itemId.Trim();
        if (string.Equals(id, BasicScrollExampleId, StringComparison.OrdinalIgnoreCase))
            return "Basic Scroll";
        if (string.Equals(id, IntermediateScrollExampleId, StringComparison.OrdinalIgnoreCase))
            return "Intermediate Scroll";
        if (string.Equals(id, AdvancedScrollExampleId, StringComparison.OrdinalIgnoreCase))
            return "Advanced Scroll";

        return item.displayName ?? string.Empty;
    }

    public static string FormatDatabaseObtainLocations(ItemDefinition item, DatabaseItemSubtab subtab)
    {
        if (!item)
            return string.Empty;

        if (subtab == DatabaseItemSubtab.Enhancement &&
            (item.IsEnhancementScroll || item.IsMapEnhancement))
        {
            return EnhancementScaledMapsLocationText;
        }

        return DatabaseItemSourceCatalog.FormatObtainLocations(item);
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

    /// <summary>Resolves the authored database entry for an inventory/storage item id (including runtime clones).</summary>
    public static bool TryResolveDatabaseLookup(
        ItemDatabase db,
        string itemId,
        out ItemDefinition listItem,
        out DatabaseItemSubtab subtab)
    {
        listItem = null;
        subtab = DatabaseItemSubtab.Resources;

        if (!db || string.IsNullOrWhiteSpace(itemId))
            return false;

        string baseId = db.GetBaseItemId(itemId);
        ItemDefinition def = db.Get(baseId);
        if (!def || !ShouldListInDatabase(def))
            return false;

        if (PassesSubtab(def, DatabaseItemSubtab.Resources))
        {
            listItem = def;
            subtab = DatabaseItemSubtab.Resources;
            return true;
        }

        if (PassesSubtab(def, DatabaseItemSubtab.Equipment))
        {
            listItem = def;
            subtab = DatabaseItemSubtab.Equipment;
            return true;
        }

        if (PassesSubtab(def, DatabaseItemSubtab.Consumables))
        {
            listItem = def;
            subtab = DatabaseItemSubtab.Consumables;
            return true;
        }

        if (PassesSubtab(def, DatabaseItemSubtab.Enhancement))
        {
            listItem = def;
            subtab = DatabaseItemSubtab.Enhancement;
            return true;
        }

        return false;
    }

    private static int CompareByDisplayName(ItemDefinition a, ItemDefinition b)
    {
        string nameA = a != null ? a.displayName : string.Empty;
        string nameB = b != null ? b.displayName : string.Empty;
        return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
    }
}
