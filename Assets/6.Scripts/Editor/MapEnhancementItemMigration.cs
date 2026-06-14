#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Moves legacy Consumable + MapEnhancement templates to <see cref="ItemKind.MapEnhancement"/>.</summary>
public static class MapEnhancementItemMigration
{
    [MenuItem("Tools/Items/Migrate Legacy Map Enhancement Items")]
    public static void MigrateAllInProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDefinition");
        int migrated = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null || !TryMigrate(def))
                continue;

            EditorUtility.SetDirty(def);
            migrated++;
        }

        if (migrated > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"[MapEnhancementItemMigration] Migrated {migrated} item definition(s) to ItemKind.MapEnhancement.");
    }

    public static bool TryMigrate(ItemDefinition def)
    {
        if (def == null || def.itemKind == ItemKind.MapEnhancement)
            return false;

        if (def.itemKind != ItemKind.Consumable ||
            def.consumableStats.consumableType != ConsumableType.MapEnhancement)
            return false;

        def.mapEnhancementStats = new MapEnhancementItemStats
        {
            tier = def.consumableStats.mapEnhancementTier,
            modRolls = def.consumableStats.mapEnhancementModRolls
        };

        def.consumableStats.consumableType = ConsumableType.None;
        def.itemKind = ItemKind.MapEnhancement;
        return true;
    }
}
#endif
