#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>One-shot helper to create/rename tier-1 gem jewelry assets. Run via Tools/Jewelry/Apply Tier 1 Gem Setup.</summary>
public static class JewelryTierOneAssetSetup
{
    private const string JewelryFolder = "Assets/3.ScriptableObjects/ItemsDefinitions/Jewelery";
    private const string SpriteSheetPath = "Assets/5.Art/Sprites/Gear/JewelerySpriteSheet.png";
    private const string SpriteSheet2Path = "Assets/5.Art/Sprites/Gear/JewelerySpriteSheet2.png";
    private const string ItemDatabasePath = "Assets/Resources/Databases/ItemDatabase.asset";

    private readonly struct JewelrySpec
    {
        public readonly string FileName;
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly EquipSlot Slot;
        public readonly JewelryGemType GemType;
        public readonly string SpriteName;
        public readonly bool UniquelyEquipped;
        public readonly bool RenameFrom;

        public JewelrySpec(
            string fileName,
            string itemId,
            string displayName,
            string description,
            EquipSlot slot,
            JewelryGemType gemType,
            string spriteName,
            bool uniquelyEquipped = true,
            bool renameFrom = false)
        {
            FileName = fileName;
            ItemId = itemId;
            DisplayName = displayName;
            Description = description;
            Slot = slot;
            GemType = gemType;
            SpriteName = spriteName;
            UniquelyEquipped = uniquelyEquipped;
            RenameFrom = renameFrom;
        }
    }

    private static readonly (string OldName, string NewName)[] Renames =
    {
        ("ability_power_pendant", "amethyst_pendant"),
        ("bone_ring", "citrine_ring"),
        ("crit_ring", "diamond_ring"),
        ("physical_damage_pendant", "ruby_pendant"),
        ("vamp_ring", "quartz_ring"),
    };

    private static readonly JewelrySpec[] Specs =
    {
        new("amethyst_pendant", "amethyst_pendant", "Amethyst Pendant", "Tier 1 amethyst pendant.", EquipSlot.Pendant, JewelryGemType.Amethyst, "JewelerySpriteSheet_17Amethyst_Pendant", uniquelyEquipped: false, renameFrom: true),
        new("citrine_ring", "citrine_ring", "Citrine Ring", "Tier 1 citrine ring.", EquipSlot.Ring, JewelryGemType.Citrine, "JewelerySpriteSheet_4CITRINE_Ring", renameFrom: true),
        new("diamond_ring", "diamond_ring", "Diamond Ring", "Tier 1 diamond ring.", EquipSlot.Ring, JewelryGemType.Diamond, "JewelerySpriteSheet_7", renameFrom: true),
        new("ruby_pendant", "ruby_pendant", "Ruby Pendant", "Tier 1 ruby pendant.", EquipSlot.Pendant, JewelryGemType.Ruby, "JewelerySpriteSheet_14RUBY_Pendant", uniquelyEquipped: false, renameFrom: true),
        new("quartz_ring", "quartz_ring", "Quartz Ring", "Tier 1 quartz ring.", EquipSlot.Ring, JewelryGemType.Quartz, "JewelerySpriteSheet_6", renameFrom: true),
        new("sapphire_trinket", "sapphire_trinket", "Trinket of respawning", "Reduces the time enemies in the current combat map take to respawn.", EquipSlot.Trinket, JewelryGemType.Sapphire, "JewelerySpriteSheet_16", uniquelyEquipped: false),

        new("ruby_ring", "ruby_ring", "Ruby Ring", "Tier 1 ruby ring.", EquipSlot.Ring, JewelryGemType.Ruby, "JewelerySpriteSheet_0RUBY_Ring"),
        new("emerald_ring", "emerald_ring", "Emerald Ring", "Tier 1 emerald ring.", EquipSlot.Ring, JewelryGemType.Emerald, "JewelerySpriteSheet_1EMERALD_Ring"),
        new("emerald_pendant", "emerald_pendant", "Emerald Pendant", "Tier 1 emerald pendant.", EquipSlot.Pendant, JewelryGemType.Emerald, "JewelerySpriteSheet_15Emerald_Pendant", uniquelyEquipped: false),
        new("sapphire_ring", "sapphire_ring", "Sapphire Ring", "Tier 1 sapphire ring.", EquipSlot.Ring, JewelryGemType.Sapphire, "JewelerySpriteSheet_2SAPPHIRE_Ring"),
        new("sapphire_pendant", "sapphire_pendant", "Sapphire Pendant", "Tier 1 sapphire pendant.", EquipSlot.Pendant, JewelryGemType.Sapphire, "JewelerySpriteSheet_16", uniquelyEquipped: false),
        new("citrine_pendant", "citrine_pendant", "Citrine Pendant", "Tier 1 citrine pendant.", EquipSlot.Pendant, JewelryGemType.Citrine, "JewelerySpriteSheet_3", uniquelyEquipped: false),
        new("quartz_pendant", "quartz_pendant", "Quartz Pendant", "Tier 1 quartz pendant.", EquipSlot.Pendant, JewelryGemType.Quartz, "JewelerySpriteSheet_11", uniquelyEquipped: false),
        new("diamond_pendant", "diamond_pendant", "Diamond Pendant", "Tier 1 diamond pendant.", EquipSlot.Pendant, JewelryGemType.Diamond, "JewelerySpriteSheet_19Diamond_Pendant", uniquelyEquipped: false),
        new("amethyst_ring", "amethyst_ring", "Amethyst Ring", "Tier 1 amethyst ring.", EquipSlot.Ring, JewelryGemType.Amethyst, "JewelerySpriteSheet_12"),
        new("topaz_ring", "topaz_ring", "Topaz Ring", "Tier 1 topaz ring.", EquipSlot.Ring, JewelryGemType.Topaz, "JewelerySpriteSheet2_TOPAZ_Ring"),
        new("topaz_pendant", "topaz_pendant", "Topaz Pendant", "Tier 1 topaz pendant.", EquipSlot.Pendant, JewelryGemType.Topaz, "JewelerySpriteSheet2_TOPAZ_Pendant", uniquelyEquipped: false),
    };

    [MenuItem("Tools/Jewelry/Apply Tier 1 Gem Setup")]
    public static void ApplySetup()
    {
        ApplyRenames();
        var created = new List<ItemDefinition>();

        foreach (JewelrySpec spec in Specs)
        {
            string path = $"{JewelryFolder}/{spec.FileName}.asset";
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
        Debug.Log($"[JewelryTierOneAssetSetup] Updated {Specs.Length} jewelry assets.");
    }

    private static void ApplyRenames()
    {
        foreach ((string oldName, string newName) in Renames)
        {
            string oldPath = $"{JewelryFolder}/{oldName}.asset";
            string newPath = $"{JewelryFolder}/{newName}.asset";
            if (!File.Exists(oldPath) || File.Exists(newPath))
                continue;

            AssetDatabase.MoveAsset(oldPath, newPath);
        }
    }

    private static void ConfigureItem(ItemDefinition item, JewelrySpec spec)
    {
        item.name = spec.FileName;
        item.itemKind = ItemKind.Jewelry;
        item.maxStack = 1;
        item.itemId = spec.ItemId;
        item.displayName = spec.DisplayName;
        item.description = spec.Description;
        item.rarity = ItemRarity.Uncommon;
        item.value = 800;
        item.equipSlot = spec.Slot;
        item.bonusStats = default;
        item.bonusStats.bonusHealth = 5;
        item.miscEffects.enemyRespawnTimeReductionSeconds = 0f;

        SerializedObject so = new SerializedObject(item);
        so.FindProperty("uniquelyEquipped").boolValue = spec.UniquelyEquipped;
        so.FindProperty("jewelryEquipmentTier").enumValueIndex = (int)EquipmentTierRank.Tier1;
        so.FindProperty("jewelryGemType").enumValueIndex = (int)spec.GemType;
        so.FindProperty("useDefaultRandomStatPoolPackage").boolValue = true;
        so.FindProperty("extraRandomStatPoolPackages").intValue = 0;

        Sprite sprite = LoadSprite(spec.SpriteName);
        if (sprite)
            so.FindProperty("icon").objectReferenceValue = sprite;

        so.ApplyModifiedPropertiesWithoutUndo();

        if (spec.ItemId == "sapphire_trinket")
            item.miscEffects.enemyRespawnTimeReductionSeconds = 1f;

        ApplyRandomStatPool(item, ItemRandomStatRoller.BuildConfiguredDefaultPoolEntries(item));
    }

    private static Sprite LoadSprite(string spriteName)
    {
        Sprite sprite = FindSpriteInSheet(SpriteSheetPath, spriteName);
        if (sprite)
            return sprite;

        sprite = FindSpriteInSheet(SpriteSheet2Path, spriteName);
        if (sprite)
            return sprite;

        Debug.LogWarning($"[JewelryTierOneAssetSetup] Sprite not found: {spriteName}");
        return null;
    }

    private static Sprite FindSpriteInSheet(string sheetPath, string spriteName)
    {
        UnityEngine.Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] is Sprite s && s.name == spriteName)
                return s;
        }

        return null;
    }

    private static void RegisterInItemDatabase(List<ItemDefinition> items)
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
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
            if (items[i] && !existing.Contains(items[i]))
            {
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = items[i];
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(db);
    }

    private static void ApplyRandomStatPool(ItemDefinition item, IReadOnlyList<RandomStatPoolEntry> entries)
    {
        SerializedObject so = new SerializedObject(item);
        SerializedProperty randomStatPool = so.FindProperty("randomStatPool");
        if (randomStatPool == null)
            return;

        randomStatPool.ClearArray();
        for (int i = 0; i < entries.Count; i++)
        {
            randomStatPool.InsertArrayElementAtIndex(i);
            SerializedProperty element = randomStatPool.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SetInt(element, "stat", (int)entries[i].stat);
            SetFloat(element, "weight", entries[i].weight);
            SetFloat(element, "minValue", entries[i].minValue);
            SetFloat(element, "maxValue", entries[i].maxValue);
            SetInt(element, "valueKind", (int)entries[i].valueKind);
            SetBool(element, "rollSecondaryValue", entries[i].rollSecondaryValue);
            SetFloat(element, "secondaryMinValue", entries[i].secondaryMinValue);
            SetFloat(element, "secondaryMaxValue", entries[i].secondaryMaxValue);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(SerializedProperty parent, string name, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
                property.enumValueIndex = value;
            else
                property.intValue = value;
        }
    }

    private static void SetFloat(SerializedProperty parent, string name, float value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedProperty parent, string name, bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.boolValue = value;
    }
}
#endif
