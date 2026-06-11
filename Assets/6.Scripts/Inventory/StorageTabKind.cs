/// <summary>Logical storage pages — Main holds <see cref="PlayerStorage.MainSlotsPerTab"/>; others hold <see cref="PlayerStorage.NonMainSlotsPerTab"/>.</summary>
public enum StorageTabKind
{
    Main = 0,
    Resources = 1,
    Equips = 2,
    Consumables = 3,
    Enhance = 4
}

/// <summary>Item-type rules for storage tabs (mirrors inventory category filters).</summary>
public static class StorageTabFilters
{
    public static bool PassesTab(ItemDefinition def, StorageTabKind tab)
    {
        if (def == null)
            return false;

        return tab switch
        {
            StorageTabKind.Main => true,
            StorageTabKind.Resources => def.itemKind == ItemKind.Resource,
            StorageTabKind.Equips => def.itemKind == ItemKind.Weapon ||
                                     def.itemKind == ItemKind.Armor ||
                                     def.itemKind == ItemKind.CombatSupport ||
                                     def.itemKind == ItemKind.Tool ||
                                     def.itemKind == ItemKind.Jewelry,
            StorageTabKind.Consumables => def.itemKind == ItemKind.Consumable && !def.IsMapEnhancement,
            StorageTabKind.Enhance => def.IsEnhancementScroll || def.IsMapEnhancement,
            _ => false
        };
    }

    public static StorageTabKind InferTabForItem(ItemDefinition def)
    {
        if (def == null)
            return StorageTabKind.Main;

        if (PassesTab(def, StorageTabKind.Enhance)) return StorageTabKind.Enhance;
        if (PassesTab(def, StorageTabKind.Resources)) return StorageTabKind.Resources;
        if (PassesTab(def, StorageTabKind.Equips)) return StorageTabKind.Equips;
        if (PassesTab(def, StorageTabKind.Consumables)) return StorageTabKind.Consumables;
        return StorageTabKind.Main;
    }

    public static string GetDisplayName(StorageTabKind tab) =>
        tab switch
        {
            StorageTabKind.Main => "Main",
            StorageTabKind.Resources => "Resources",
            StorageTabKind.Equips => "Equips",
            StorageTabKind.Consumables => "Consumables",
            StorageTabKind.Enhance => "Enhance",
            _ => tab.ToString()
        };
}
