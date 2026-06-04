using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/Item Database", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    private const string RuntimeEnhancedSeparator = "__enh_";

    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

    private Dictionary<string, ItemDefinition> _map;
    private readonly Dictionary<string, ItemDefinition> _runtimeItems = new Dictionary<string, ItemDefinition>(32);
    private readonly Dictionary<string, string> _runtimeBaseIds = new Dictionary<string, string>(32);

    private void OnEnable()
    {
        Build();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keeps the map correct while editing itemIds in the inspector
        Build();
    }
#endif

    private static string Normalize(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? null
            : id.Trim().ToLowerInvariant().Replace(" ", "_");
    }

    private void Build()
    {
        if (_map == null) _map = new Dictionary<string, ItemDefinition>(256);
        else _map.Clear();

        foreach (var item in items)
        {
            if (!item) continue;

            string key = Normalize(item.itemId);
            if (string.IsNullOrWhiteSpace(key)) continue;

            // Detect duplicates clearly (optional but highly recommended)
            if (_map.TryGetValue(key, out var existing) && existing != item)
            {
                Debug.LogError($"[ItemDatabase] Duplicate itemId '{key}' (existing '{existing.name}', new '{item.name}').");
                continue;
            }

            _map[key] = item;
        }
    }
    /// <summary>
    /// List index used for inventory/storage sort order. Runtime enhanced items
    /// (<c>baseId__enh_...</c>) use the same index as their base <see cref="ItemDefinition"/> so
    /// variants stay grouped with the authored item order, regardless of display name.
    /// </summary>
    public int GetIndex(string itemId)
    {
        if (_map == null || _map.Count == 0)
            Build();

        string key = Normalize(itemId);
        if (string.IsNullOrEmpty(key))
            return int.MaxValue;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && Normalize(items[i].itemId) == key)
                return i;
        }

        if (_runtimeBaseIds.TryGetValue(key, out string baseKey) && !string.IsNullOrEmpty(baseKey))
            return GetIndexForBaseNormalized(baseKey);

        int marker = itemId.IndexOf(RuntimeEnhancedSeparator, System.StringComparison.Ordinal);
        if (marker > 0)
        {
            string parsedBase = Normalize(itemId.Substring(0, marker));
            if (!string.IsNullOrEmpty(parsedBase))
                return GetIndexForBaseNormalized(parsedBase);
        }

        return int.MaxValue;
    }

    private int GetIndexForBaseNormalized(string baseKeyNormalized)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && Normalize(items[i].itemId) == baseKeyNormalized)
                return i;
        }

        return int.MaxValue;
    }

    public ItemDefinition Get(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;

        // Rebuild if needed (runtime safety)
        if (_map == null || _map.Count == 0)
            Build();

        string key = Normalize(itemId);
        if (key != null && _runtimeItems.TryGetValue(key, out var runtimeDef))
        {
            runtimeDef.NormalizeEnhancementState();
            return runtimeDef;
        }
        if (key != null && _map.TryGetValue(key, out var def))
            return def;

        return TryRebuildRuntimeEnhancedFallback(itemId);
    }

    public List<ItemDefinition> GetAll() => items;

    public bool IsRuntimeEnhancedItem(string itemId)
    {
        string key = Normalize(itemId);
        return key != null && _runtimeItems.ContainsKey(key);
    }

    /// <summary>
    /// Authored item id for stock lookups. Runtime clones (<c>baseId__enh_...</c>) resolve to their base item.
    /// </summary>
    public string GetBaseItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return itemId;

        if (_map == null || _map.Count == 0)
            Build();

        string key = Normalize(itemId);
        if (!string.IsNullOrEmpty(key) && _runtimeBaseIds.TryGetValue(key, out string baseKeyNormalized))
        {
            if (_map.TryGetValue(baseKeyNormalized, out ItemDefinition baseDef) && baseDef)
                return baseDef.itemId;

            return itemId.Substring(0, itemId.IndexOf(RuntimeEnhancedSeparator, System.StringComparison.Ordinal));
        }

        int markerIndex = itemId.IndexOf(RuntimeEnhancedSeparator, System.StringComparison.Ordinal);
        if (markerIndex > 0)
            return itemId.Substring(0, markerIndex);

        return itemId;
    }

    public ItemDefinition CreateRuntimeEnhancedItem(ItemDefinition baseDef, string runtimeItemId = null)
    {
        if (!baseDef || string.IsNullOrWhiteSpace(baseDef.itemId))
            return null;

        string id = string.IsNullOrWhiteSpace(runtimeItemId)
            ? $"{baseDef.itemId}{RuntimeEnhancedSeparator}{System.Guid.NewGuid():N}"
            : runtimeItemId.Trim();

        ItemDefinition clone = Instantiate(baseDef);
        clone.name = id;
        clone.itemId = id;
        clone.maxStack = 1;
        clone.hideFlags = HideFlags.DontSave;
        RegisterRuntimeItem(clone, baseDef.itemId);
        return clone;
    }

    public void RegisterRuntimeItem(ItemDefinition def, string baseItemId)
    {
        if (!def || string.IsNullOrWhiteSpace(def.itemId) || string.IsNullOrWhiteSpace(baseItemId))
            return;

        string key = Normalize(def.itemId);
        if (string.IsNullOrWhiteSpace(key))
            return;

        _runtimeItems[key] = def;
        _runtimeBaseIds[key] = Normalize(baseItemId);
    }

    public void SaveRuntimeEnhancedItemsInto(SaveData data)
    {
        if (data == null)
            return;

        data.enhancedItems ??= new List<SaveData.EnhancedItemData>();
        data.enhancedItems.Clear();

        foreach (var pair in _runtimeItems)
        {
            ItemDefinition def = pair.Value;
            if (!def || !_runtimeBaseIds.TryGetValue(pair.Key, out string baseId))
                continue;

            data.enhancedItems.Add(new SaveData.EnhancedItemData
            {
                itemId = def.itemId,
                baseItemId = baseId,
                displayName = def.displayName,
                usedUpgradeSlots = def.usedUpgradeSlots,
                successfulEnhancements = def.successfulEnhancements,
                enhancementScrollHistory = CopyEnhancementScrollHistory(def.enhancementScrollHistory),
                weaponStats = def.weaponStats,
                armorStats = def.armorStats,
                bonusStats = def.bonusStats,
                combatSupportStats = def.combatSupportStats,
                toolStats = def.toolStats
            });
        }
    }

    public void LoadRuntimeEnhancedItemsFrom(SaveData data)
    {
        _runtimeItems.Clear();
        _runtimeBaseIds.Clear();

        if (data?.enhancedItems == null)
            return;

        for (int i = 0; i < data.enhancedItems.Count; i++)
        {
            SaveData.EnhancedItemData saved = data.enhancedItems[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.itemId) || string.IsNullOrWhiteSpace(saved.baseItemId))
                continue;

            ItemDefinition baseDef = Get(saved.baseItemId);
            if (!baseDef)
                continue;

            ItemDefinition clone = CreateRuntimeEnhancedItem(baseDef, saved.itemId);
            if (!clone)
                continue;

            clone.displayName = string.IsNullOrWhiteSpace(saved.displayName) ? baseDef.displayName : saved.displayName;
            clone.usedUpgradeSlots = Mathf.Max(0, saved.usedUpgradeSlots);
            clone.successfulEnhancements = Mathf.Max(0, saved.successfulEnhancements);
            clone.enhancementScrollHistory = CopyEnhancementScrollHistory(saved.enhancementScrollHistory);
            clone.weaponStats = saved.weaponStats;
            clone.armorStats = saved.armorStats;
            clone.bonusStats = saved.bonusStats;
            clone.combatSupportStats = saved.combatSupportStats;
            clone.toolStats = saved.toolStats;
            clone.NormalizeEnhancementState();
        }
    }

    private ItemDefinition TryRebuildRuntimeEnhancedFallback(string runtimeItemId)
    {
        if (string.IsNullOrWhiteSpace(runtimeItemId))
            return null;

        int markerIndex = runtimeItemId.IndexOf(RuntimeEnhancedSeparator, System.StringComparison.Ordinal);
        if (markerIndex <= 0)
            return null;

        string baseItemId = runtimeItemId.Substring(0, markerIndex);
        ItemDefinition baseDef = Get(baseItemId);
        if (!baseDef)
            return null;

        ItemDefinition clone = CreateRuntimeEnhancedItem(baseDef, runtimeItemId);
        return clone;
    }

    private static List<EnhancementScrollHistoryEntry> CopyEnhancementScrollHistory(
        List<EnhancementScrollHistoryEntry> source)
    {
        if (source == null || source.Count == 0)
            return new List<EnhancementScrollHistoryEntry>();

        var copy = new List<EnhancementScrollHistoryEntry>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            EnhancementScrollHistoryEntry entry = source[i];
            if (entry == null)
                continue;

            copy.Add(new EnhancementScrollHistoryEntry
            {
                scrollName = entry.scrollName,
                success = entry.success,
                effectSummary = entry.effectSummary
            });
        }

        return copy;
    }
}