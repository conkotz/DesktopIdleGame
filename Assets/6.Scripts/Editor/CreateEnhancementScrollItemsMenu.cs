using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateEnhancementScrollItemsMenu
{
    private const string MenuCreateAll = "Tools/Create Enhancement Scroll Items";
    private const string ScrollFolder = "Assets/3.ScriptableObjects/ItemsDefinitions/EnhancementScrolls";
    private const string BasicSpritePath = "Assets/5.Art/Sprites/Resources/EnhancementScroll2.png";
    private const string IntermediateSpritePath = "Assets/5.Art/Sprites/Resources/EnhancementScrollIntermediate.png";
    private const string AdvancedSpritePath = "Assets/5.Art/Sprites/Resources/EnhancementScrollAdvanced.png";

    [MenuItem(MenuCreateAll)]
    private static void CreateAllMissingScrollItems()
    {
        EnhancementOptionDatabase db = LoadEnhancementOptionDatabase();
        if (db == null)
        {
            EditorUtility.DisplayDialog("Create Enhancement Scroll Items", "EnhancementOptionDatabase not found.", "OK");
            return;
        }

        db.EnsureDefaults();
        AssignDefaultScrollIcons(db);
        int created = CreateMissingScrollItemsForDatabase(db);
        RegisterItemsInDatabase();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Create Enhancement Scroll Items",
            created == 0
                ? "All scroll items already exist and are synced."
                : $"Created or updated {created} enhancement scroll item(s). Run Tools/populate databases if any are still missing from ItemDatabase.",
            "OK");
    }

    public static int CreateMissingScrollItemsForDatabase(EnhancementOptionDatabase db)
    {
        if (db == null)
            return 0;

        db.EnsureDefaults();
        AssignDefaultScrollIcons(db);

        int changed = 0;
        IReadOnlyList<EnhancementOptionEntry> options = db.Options;
        for (int i = 0; i < options.Count; i++)
        {
            EnhancementOptionEntry option = options[i];
            if (option == null || string.IsNullOrWhiteSpace(option.linkedScrollItemId))
                continue;

            if (CreateOrUpdateScrollItem(db, option, out bool createdOrUpdated) && createdOrUpdated)
                changed++;
        }

        return changed;
    }

    public static void AssignDefaultScrollIcons(EnhancementOptionDatabase db)
    {
        if (db == null)
            return;

        bool changed = false;
        if (db.basicScrollIcon == null)
        {
            db.basicScrollIcon = LoadSpriteFromTexture(BasicSpritePath, "EnhancementScroll2_0");
            changed = true;
        }

        if (db.intermediateScrollIcon == null)
        {
            db.intermediateScrollIcon = LoadSpriteFromTexture(IntermediateSpritePath, "EnhancementScrollIntermediate_0");
            changed = true;
        }

        if (db.advancedScrollIcon == null)
        {
            db.advancedScrollIcon = LoadSpriteFromTexture(AdvancedSpritePath, "EnhancementScrollAdvanced_0");
            changed = true;
        }

        if (db.chaosScrollIcon == null)
        {
            ItemDefinition chaosDef = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/3.ScriptableObjects/ItemsDefinitions/EnhancementScrolls/chaos_weapon_physical_scroll.asset");
            if (chaosDef != null && chaosDef.icon != null)
            {
                db.chaosScrollIcon = chaosDef.icon;
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(db);
    }

    private static bool CreateOrUpdateScrollItem(
        EnhancementOptionDatabase db,
        EnhancementOptionEntry option,
        out bool createdOrUpdated)
    {
        createdOrUpdated = false;
        if (option == null || string.IsNullOrWhiteSpace(option.linkedScrollItemId))
            return false;

        string scrollId = option.linkedScrollItemId.Trim();
        string assetPath = $"{ScrollFolder}/{scrollId}.asset";
        EnsureFolderExists(ScrollFolder);

        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
        bool isNew = item == null;
        if (isNew)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, assetPath);
            createdOrUpdated = true;
        }

        EnhancementScrollStats stats = option.ToScrollStats();
        string displayName = BuildScrollDisplayName(option);
        Sprite icon = db.GetScrollIconForOption(option);

        ItemRarity rarity = GetRarityForOption(option);

        bool dirty =
            item.itemKind != ItemKind.EnhancementScroll ||
            !string.Equals(item.itemId, scrollId, StringComparison.Ordinal) ||
            !string.Equals(item.enhancementOptionId, option.optionId, StringComparison.Ordinal) ||
            !string.Equals(item.displayName, displayName, StringComparison.Ordinal) ||
            item.icon != icon ||
            item.rarity != rarity ||
            !ScrollStatsEqual(item.enhancementScrollStats, stats);

        item.itemKind = ItemKind.EnhancementScroll;
        item.itemId = scrollId;
        item.enhancementOptionId = option.optionId;
        item.displayName = displayName;
        item.maxStack = 5;
        item.rarity = rarity;
        item.value = option.track == EnhancementTrack.Corruption ? 100 : 20;
        item.description = BuildScrollDescription(option);
        if (icon != null)
            item.icon = icon;
        item.enhancementScrollStats = stats;

        if (dirty || isNew)
        {
            EditorUtility.SetDirty(item);
            createdOrUpdated = true;
        }

        return true;
    }

    private static EnhancementOptionDatabase LoadEnhancementOptionDatabase()
    {
        EnhancementOptionDatabase db = AssetDatabase.LoadAssetAtPath<EnhancementOptionDatabase>(
            "Assets/Resources/Databases/EnhancementOptionDatabase.asset");
        if (db != null)
            return db;

        string[] guids = AssetDatabase.FindAssets("t:EnhancementOptionDatabase");
        if (guids.Length == 0)
            return null;

        return AssetDatabase.LoadAssetAtPath<EnhancementOptionDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static ItemRarity GetRarityForOption(EnhancementOptionEntry option)
    {
        if (option == null)
            return ItemRarity.Uncommon;

        if (option.track == EnhancementTrack.Corruption)
            return ItemRarity.Rare;

        if (option.track == EnhancementTrack.Special)
            return ItemRarity.Epic;

        return option.tier switch
        {
            EnhancementTier.Intermediate => ItemRarity.Rare,
            EnhancementTier.Advanced => ItemRarity.Epic,
            _ => ItemRarity.Uncommon,
        };
    }

    private static string BuildScrollDisplayName(EnhancementOptionEntry option)
    {
        if (option == null)
            return "Enhancement Scroll";

        if (option.track == EnhancementTrack.Corruption)
            return option.displayName.Replace(" Gamble", " Scroll", StringComparison.Ordinal);

        return $"{option.displayName} Scroll";
    }

    private static string BuildScrollDescription(EnhancementOptionEntry option)
    {
        if (option == null)
            return "An enhancement scroll.";

        string statName = ItemDefinition.GetEnhancementScrollTargetStatDisplayName(option.targetStat).ToLowerInvariant();
        if (option.track == EnhancementTrack.Corruption)
            return $"A chaotic gamble scroll that can add {statName} to eligible gear.";

        return $"An enhancement scroll that can add {statName} to eligible gear.";
    }

    private static bool ScrollStatsEqual(EnhancementScrollStats a, EnhancementScrollStats b)
    {
        return a.targetStat == b.targetStat &&
               a.modifierKind == b.modifierKind &&
               Mathf.Approximately(a.modifierValue, b.modifierValue) &&
               a.consumeSlotOnFailure == b.consumeSlotOnFailure &&
               a.failureOutcome == b.failureOutcome &&
               Mathf.Approximately(a.destroyChanceOnFailure, b.destroyChanceOnFailure) &&
               a.cursed == b.cursed &&
               a.allowedGearTypes == b.allowedGearTypes;
    }

    private static Sprite LoadSpriteFromTexture(string texturePath, string spriteName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && string.Equals(sprite.name, spriteName, StringComparison.Ordinal))
                return sprite;
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string leaf = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);

        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            AssetDatabase.CreateFolder(parent, leaf);
    }

    public static void RegisterItemsInDatabasePublic() => RegisterItemsInDatabase();

    private static void RegisterItemsInDatabase()
    {
        EditorApplication.ExecuteMenuItem("Tools/populate databases");
    }
}
