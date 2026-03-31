using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DesktopIdleGame/Item Database", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

    private Dictionary<string, ItemDefinition> _map;

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
    public int GetIndex(string itemId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].itemId == itemId)
                return i;
        }

        return int.MaxValue; // unknown items go to bottom
    }

    public ItemDefinition Get(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;

        // Rebuild if needed (runtime safety)
        if (_map == null || _map.Count == 0)
            Build();

        string key = Normalize(itemId);
        return (key != null && _map.TryGetValue(key, out var def)) ? def : null;
    }

    public List<ItemDefinition> GetAll() => items;
}