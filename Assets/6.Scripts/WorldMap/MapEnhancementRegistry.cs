using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime rolled map enhancement items (separate from gear enhancement clones).</summary>
public static class MapEnhancementRegistry
{
    public const string RuntimeSeparator = "__mapenh__";

    private static readonly Dictionary<string, MapEnhancementInstanceData> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, ItemDefinition> RuntimeDefinitions =
        new(StringComparer.OrdinalIgnoreCase);

    public static bool IsRuntimeItem(string itemId) =>
        !string.IsNullOrWhiteSpace(itemId)
        && itemId.IndexOf(RuntimeSeparator, StringComparison.Ordinal) >= 0;

    public static bool TryGetInstance(string itemId, out MapEnhancementInstanceData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        return Instances.TryGetValue(NormalizeKey(itemId), out data) && data != null;
    }

    public static MapEnhancementInstanceData GetInstanceOrNull(string itemId)
    {
        TryGetInstance(itemId, out MapEnhancementInstanceData data);
        return data;
    }

    public static ItemDefinition TryGetRuntimeDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        RuntimeDefinitions.TryGetValue(NormalizeKey(itemId), out ItemDefinition def);
        return def;
    }

    public static ItemDefinition RegisterRuntimeItem(ItemDefinition runtimeDef, MapEnhancementInstanceData data)
    {
        if (runtimeDef == null || data == null || string.IsNullOrWhiteSpace(data.itemId))
            return null;

        string key = NormalizeKey(data.itemId);
        data.itemId = key;
        runtimeDef.itemId = key;
        runtimeDef.maxStack = 1;
        runtimeDef.hideFlags = HideFlags.DontSave;

        Instances[key] = data;
        RuntimeDefinitions[key] = runtimeDef;
        return runtimeDef;
    }

    public static void ClearAll()
    {
        Instances.Clear();
        RuntimeDefinitions.Clear();
    }

    public static void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        data.mapEnhancementItems ??= new List<SaveData.MapEnhancementItemData>();
        data.mapEnhancementItems.Clear();

        foreach (var pair in Instances)
        {
            MapEnhancementInstanceData src = pair.Value;
            if (src == null || string.IsNullOrWhiteSpace(src.itemId))
                continue;

            var row = new SaveData.MapEnhancementItemData
            {
                itemId = src.itemId,
                baseItemId = src.baseItemId,
                displayName = src.displayName,
                sourceMapNodeId = src.sourceMapNodeId,
                tier = src.tier
            };

            if (src.mods != null)
            {
                for (int i = 0; i < src.mods.Count; i++)
                {
                    MapEnhancementMod mod = src.mods[i];
                    row.modTypes.Add((int)mod.modType);
                    row.modValues.Add(mod.value);
                    row.modExtraSpawnEnemyIds.Add(mod.extraSpawnEnemyId ?? string.Empty);
                }
            }

            data.mapEnhancementItems.Add(row);
        }
    }

    public static void LoadFrom(SaveData data, ItemDatabase itemDb)
    {
        ClearAll();
        if (data?.mapEnhancementItems == null || itemDb == null)
            return;

        for (int i = 0; i < data.mapEnhancementItems.Count; i++)
        {
            SaveData.MapEnhancementItemData saved = data.mapEnhancementItems[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.itemId) || string.IsNullOrWhiteSpace(saved.baseItemId))
                continue;

            ItemDefinition baseDef = itemDb.Get(saved.baseItemId);
            if (baseDef == null || !baseDef.IsMapEnhancement)
                continue;

            var instance = new MapEnhancementInstanceData
            {
                itemId = saved.itemId.Trim(),
                baseItemId = saved.baseItemId.Trim(),
                displayName = saved.displayName,
                sourceMapNodeId = saved.sourceMapNodeId,
                tier = saved.tier,
                mods = new List<MapEnhancementMod>()
            };

            int modCount = saved.modTypes != null ? saved.modTypes.Count : 0;
            for (int m = 0; m < modCount; m++)
            {
                instance.mods.Add(new MapEnhancementMod
                {
                    modType = (MapEnhancementModType)saved.modTypes[m],
                    value = m < saved.modValues.Count ? saved.modValues[m] : 0f,
                    extraSpawnEnemyId = m < saved.modExtraSpawnEnemyIds.Count
                        ? saved.modExtraSpawnEnemyIds[m]
                        : string.Empty
                });
            }

            ItemDefinition runtime = UnityEngine.Object.Instantiate(baseDef);
            runtime.name = instance.itemId;
            runtime.displayName = string.IsNullOrWhiteSpace(instance.displayName)
                ? baseDef.displayName
                : instance.displayName;
            runtime.description = MapEnhancementService.BuildDescription(instance);

            RegisterRuntimeItem(runtime, instance);
        }
    }

    private static string NormalizeKey(string itemId) => itemId.Trim();
}
