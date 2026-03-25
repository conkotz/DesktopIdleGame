using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DesktopIdleGame/Merchant Stock", fileName = "MerchantStock")]
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
        [Tooltip("-1 = infinite stock")]
        public int quantity = -1;
    }

    [SerializeField] private List<Entry> items = new();

    public IReadOnlyList<Entry> Items => items;

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

            if (entry.quantity < -1)
                entry.quantity = -1;

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