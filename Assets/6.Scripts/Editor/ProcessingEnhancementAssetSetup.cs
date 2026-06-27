#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProcessingEnhancementAssetSetup
{
    private const string ItemFolder = "Assets/3.ScriptableObjects/ItemsDefinitions/Resources/Cooking";
    private const string SpriteSheetPath = "Assets/5.Art/Sprites/Resources/Cooking/CookingItemsSpriteSheet.png";
    private const string ItemDatabasePath = "Assets/Resources/Databases/ItemDatabase.asset";

    private readonly struct EnhancementSpec
    {
        public readonly string FileName;
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly int Value;
        public readonly string SpriteName;
        public readonly float FlatSecondsReduction;
        public readonly float BurnChanceReductionPercent;

        public EnhancementSpec(
            string fileName,
            string itemId,
            string displayName,
            string description,
            int value,
            string spriteName,
            float flatSecondsReduction = 0f,
            float burnChanceReductionPercent = 0f)
        {
            FileName = fileName;
            ItemId = itemId;
            DisplayName = displayName;
            Description = description;
            Value = value;
            SpriteName = spriteName;
            FlatSecondsReduction = flatSecondsReduction;
            BurnChanceReductionPercent = burnChanceReductionPercent;
        }
    }

    private static readonly EnhancementSpec[] Specs =
    {
        new(
            "cooking_salt",
            "cooking_salt",
            "Salt",
            "Cooking enhancement. Trims 1 second from each cook attempt while loaded in the range. Consumes 1 per attempt.",
            50,
            "CookingItemsSpriteSheet_Salt",
            flatSecondsReduction: 1f),
        new(
            "cooking_spice",
            "cooking_spice",
            "Spice",
            "Cooking enhancement. Trims 2 seconds from each cook attempt while loaded in the range. Consumes 1 per attempt.",
            150,
            "CookingItemsSpriteSheet_Spice",
            flatSecondsReduction: 2f),
        new(
            "cooking_herbs",
            "cooking_herbs",
            "Herbs",
            "Cooking enhancement. Reduces burn chance by 10% on each cook attempt while loaded in the range. Consumes 1 per attempt.",
            100,
            "CookingItemsSpriteSheet_Herbs",
            burnChanceReductionPercent: 10f),
    };

    private static readonly SmeltingEnhancementSpec[] SmeltingSpecs =
    {
        new(
            "smelting_coal",
            "smelting_coal",
            "Coal",
            "Smelting enhancement. Trims 2 seconds from each bar while loaded in the furnace. Consumes 1 per bar.",
            100,
            "Assets/5.Art/Sprites/Resources/Smelting/CoalSprite.png",
            "CoalSprite_0",
            flatSecondsReduction: 2f),
    };

    private readonly struct SmeltingEnhancementSpec
    {
        public readonly string FileName;
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly int Value;
        public readonly string SpritePath;
        public readonly string SpriteName;
        public readonly float FlatSecondsReduction;

        public SmeltingEnhancementSpec(
            string fileName,
            string itemId,
            string displayName,
            string description,
            int value,
            string spritePath,
            string spriteName,
            float flatSecondsReduction = 0f)
        {
            FileName = fileName;
            ItemId = itemId;
            DisplayName = displayName;
            Description = description;
            Value = value;
            SpritePath = spritePath;
            SpriteName = spriteName;
            FlatSecondsReduction = flatSecondsReduction;
        }
    }

    [MenuItem("Tools/Cooking/Create Processing Enhancements")]
    public static void CreateProcessingEnhancements()
    {
        if (!AssetDatabase.IsValidFolder("Assets/3.ScriptableObjects/ItemsDefinitions/Resources"))
            AssetDatabase.CreateFolder("Assets/3.ScriptableObjects/ItemsDefinitions", "Resources");

        if (!AssetDatabase.IsValidFolder(ItemFolder))
            AssetDatabase.CreateFolder("Assets/3.ScriptableObjects/ItemsDefinitions/Resources", "Cooking");

        var created = new List<ItemDefinition>();
        foreach (EnhancementSpec spec in Specs)
        {
            string path = $"{ItemFolder}/{spec.FileName}.asset";
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (!item)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            ConfigureItem(item, spec);
            EditorUtility.SetDirty(item);
            created.Add(item);
        }

        RegisterInItemDatabase(created);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ProcessingEnhancementAssetSetup] Created/updated {Specs.Length} cooking enhancement items.");
    }

    [MenuItem("Tools/Smelting/Create Smelting Enhancements")]
    public static void CreateSmeltingEnhancements()
    {
        const string smeltingFolder = "Assets/3.ScriptableObjects/ItemsDefinitions/Resources/Smelting";

        if (!AssetDatabase.IsValidFolder("Assets/3.ScriptableObjects/ItemsDefinitions/Resources"))
            AssetDatabase.CreateFolder("Assets/3.ScriptableObjects/ItemsDefinitions", "Resources");

        if (!AssetDatabase.IsValidFolder(smeltingFolder))
            AssetDatabase.CreateFolder("Assets/3.ScriptableObjects/ItemsDefinitions/Resources", "Smelting");

        var created = new List<ItemDefinition>();
        foreach (SmeltingEnhancementSpec spec in SmeltingSpecs)
        {
            string path = $"{smeltingFolder}/{spec.FileName}.asset";
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (!item)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            ConfigureSmeltingItem(item, spec);
            EditorUtility.SetDirty(item);
            created.Add(item);
        }

        RegisterInItemDatabase(created);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ProcessingEnhancementAssetSetup] Created/updated {SmeltingSpecs.Length} smelting enhancement items.");
    }

    private static void ConfigureSmeltingItem(ItemDefinition item, SmeltingEnhancementSpec spec)
    {
        item.name = spec.FileName;
        item.itemKind = ItemKind.Consumable;
        item.maxStack = 1000;
        item.itemId = spec.ItemId;
        item.displayName = spec.DisplayName;
        item.description = spec.Description;
        item.rarity = ItemRarity.Common;
        item.value = spec.Value;
        item.equipSlot = EquipSlot.None;

        item.consumableStats.consumableType = ConsumableType.ProcessingSkillEnhancement;
        item.consumableStats.consumeOnUse = false;
        item.consumableStats.processingSkillTarget = ProcessingSkillTarget.Smelting;
        item.consumableStats.processingFlatSecondsReduction = spec.FlatSecondsReduction;
        item.consumableStats.processingBurnChanceReductionPercent = 0f;

        Sprite sprite = FindSpriteAtPath(spec.SpritePath, spec.SpriteName);
        if (sprite)
            item.icon = sprite;
    }

    private static Sprite FindSpriteAtPath(string assetPath, string spriteName)
    {
        Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] is Sprite sprite && sprite.name == spriteName)
                return sprite;
        }

        Debug.LogWarning($"[ProcessingEnhancementAssetSetup] Sprite not found: {spriteName} at {assetPath}");
        return null;
    }

    private static void ConfigureItem(ItemDefinition item, EnhancementSpec spec)
    {
        item.name = spec.FileName;
        item.itemKind = ItemKind.Consumable;
        item.maxStack = 1000;
        item.itemId = spec.ItemId;
        item.displayName = spec.DisplayName;
        item.description = spec.Description;
        item.rarity = ItemRarity.Common;
        item.value = spec.Value;
        item.equipSlot = EquipSlot.None;

        item.consumableStats.consumableType = ConsumableType.ProcessingSkillEnhancement;
        item.consumableStats.consumeOnUse = false;
        item.consumableStats.processingSkillTarget = ProcessingSkillTarget.Cooking;
        item.consumableStats.processingFlatSecondsReduction = spec.FlatSecondsReduction;
        item.consumableStats.processingBurnChanceReductionPercent = spec.BurnChanceReductionPercent;

        Sprite sprite = FindSprite(spec.SpriteName);
        if (sprite)
            item.icon = sprite;
    }

    private static Sprite FindSprite(string spriteName)
    {
        Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] is Sprite sprite && sprite.name == spriteName)
                return sprite;
        }

        Debug.LogWarning($"[ProcessingEnhancementAssetSetup] Sprite not found: {spriteName}");
        return null;
    }

    private static void RegisterInItemDatabase(List<ItemDefinition> items)
    {
        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (!db)
            return;

        SerializedObject so = new SerializedObject(db);
        SerializedProperty list = so.FindProperty("items");
        var existing = new HashSet<ItemDefinition>();
        for (int i = 0; i < list.arraySize; i++)
        {
            var element = list.GetArrayElementAtIndex(i).objectReferenceValue as ItemDefinition;
            if (element)
                existing.Add(element);
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && !existing.Contains(items[i]))
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = items[i];
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(db);
    }
}
#endif
