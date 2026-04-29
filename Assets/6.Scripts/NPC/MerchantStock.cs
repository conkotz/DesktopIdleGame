using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Desktop Idle Game/Merchant Stock", fileName = "MerchantStock")]
public class MerchantStock : ScriptableObject
{
    public const int MaxCapacity = 21;

    public enum CostType
    {
        Gold,
        Item
    }

    [Serializable]
    public class Cost
    {
        public CostType type = CostType.Gold;
        public int amount = 1;

        [Tooltip("Only used when type = Item")]
        public string itemId;
    }

    [Serializable]
    public class Entry
    {
        [Header("Item Sold")]
        public string itemId;

        [Header("Cost")]
        public List<Cost> costs = new();

        [Header("Stock")]
        [Tooltip(
            "-1 = infinite. Starting quantity for a new game; current stock is stored in the player save and is not written back to this asset.")]
        [FormerlySerializedAs("quantity")]
        public int defaultQuantity = -1;
    }

    [SerializeField] private List<Entry> items = new();

    public IReadOnlyList<Entry> Items => items;

    /// <summary>
    /// Used in save data when <see cref="Merchant"/> has no explicit merchant id.
    /// Defaults to the asset name so the same shop stock persists across level scenes.
    /// </summary>
    public string StockSaveKey => string.IsNullOrWhiteSpace(stockSaveKey) ? name : stockSaveKey.Trim();

    [SerializeField, Tooltip("Optional. If empty, the asset file name is used. Set when two stock assets share the same name.")]
    private string stockSaveKey = "";

    public Entry GetEntry(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        for (int i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            if (entry != null && entry.itemId == itemId)
                return entry;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (items == null)
            items = new List<Entry>();

        if (items.Count > MaxCapacity)
        {
            Debug.LogWarning($"MerchantStock '{name}' exceeds max capacity ({MaxCapacity}). Extra items removed.");
            items.RemoveRange(MaxCapacity, items.Count - MaxCapacity);
        }

        for (int i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            if (entry == null) continue;

            if (entry.costs == null)
                entry.costs = new List<Cost>();

            if (entry.defaultQuantity < -1)
                entry.defaultQuantity = -1;

            for (int j = 0; j < entry.costs.Count; j++)
            {
                var cost = entry.costs[j];
                if (cost == null) continue;

                if (cost.amount < 0)
                    cost.amount = 0;
            }
        }
    }
#endif
}