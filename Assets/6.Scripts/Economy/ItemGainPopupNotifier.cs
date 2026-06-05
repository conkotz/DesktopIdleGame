using System;
using UnityEngine;

/// <summary>
/// Runtime log entry when inventory gains resources (gathering, etc.).
/// </summary>
public static class ItemGainPopupNotifier
{
    private const string RuntimeEnhancedSeparator = "__enh_";

    private static ItemDatabase s_itemDatabase;

    public static void Notify(string itemId, int amount, bool purchased = false)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        itemId = itemId.Trim();

        string label = ResolveDisplayLabel(itemId, amount);
        if (purchased)
            GameLog.ItemPurchased(label, amount);
        else
            GameLog.ItemGained(label, amount);
    }

    public static void NotifyLost(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        itemId = itemId.Trim();

        string label = ResolveDisplayLabel(itemId, amount);
        GameLog.ItemLost(label, amount);
    }

    /// <summary>
    /// Player-facing item name for activity logs and popups.
    /// Runtime item ids (<c>baseId__enh_...</c>) show the base item name.
    /// Scroll enhancement level is shown as <c>+N</c>; random stat rolls do not add a suffix.
    /// </summary>
    public static string ResolveDisplayLabel(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "item";

        if (amount <= 0)
            amount = 1;

        itemId = itemId.Trim();

        if (MapEnhancementRegistry.IsRuntimeItem(itemId))
        {
            ItemDatabase mapEnhDb = ResolveItemDatabase();
            ItemDefinition rolledDef = mapEnhDb != null ? mapEnhDb.Get(itemId) : null;
            if (rolledDef != null && !string.IsNullOrWhiteSpace(rolledDef.displayName))
                return rolledDef.displayName.Trim();

            if (MapEnhancementRegistry.TryGetInstance(itemId, out MapEnhancementInstanceData mapData)
                && !string.IsNullOrWhiteSpace(mapData.displayName))
                return mapData.displayName.Trim();
        }

        ItemDatabase db = ResolveItemDatabase();

        ItemDefinition instanceDef = db != null ? db.Get(itemId) : null;
        string baseItemId = db != null ? db.GetBaseItemId(itemId) : StripRuntimeEnhancedSuffix(itemId);
        ItemDefinition baseDef = ResolveAuthoredDefinition(db, baseItemId, instanceDef);

        if (baseDef != null)
        {
            string label = amount == 1 && !string.IsNullOrEmpty(baseDef.displayNameSingular)
                ? baseDef.displayNameSingular
                : baseDef.displayName;

            label = StripScrollEnhancementSuffix(label);

            int enhancementLevel = instanceDef != null ? instanceDef.SuccessfulEnhancements : 0;
            if (enhancementLevel > 0)
                label = $"{label} +{enhancementLevel}";

            return label;
        }

        return FormatItemIdFallback(baseItemId);
    }

    private static ItemDatabase ResolveItemDatabase()
    {
        if (s_itemDatabase)
            return s_itemDatabase;

        s_itemDatabase = UnityEngine.Object.FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        return s_itemDatabase;
    }

    private static ItemDefinition ResolveAuthoredDefinition(ItemDatabase db, string baseItemId, ItemDefinition instanceDef)
    {
        if (db != null && !string.IsNullOrWhiteSpace(baseItemId))
        {
            ItemDefinition fromBaseId = db.Get(baseItemId);
            if (fromBaseId != null && !db.IsRuntimeEnhancedItem(fromBaseId.itemId))
                return fromBaseId;
        }

        if (instanceDef == null)
            return null;

        if (db == null || !db.IsRuntimeEnhancedItem(instanceDef.itemId))
            return instanceDef;

        return instanceDef;
    }

    private static string StripRuntimeEnhancedSuffix(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return itemId;

        int markerIndex = itemId.IndexOf(RuntimeEnhancedSeparator, StringComparison.Ordinal);
        return markerIndex > 0 ? itemId.Substring(0, markerIndex) : itemId;
    }

    private static string StripScrollEnhancementSuffix(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "Item";

        string trimmed = displayName.Trim();
        int marker = trimmed.LastIndexOf(" +", StringComparison.Ordinal);
        if (marker < 0)
            return trimmed;

        string suffix = trimmed.Substring(marker + 2);
        return int.TryParse(suffix, out _) ? trimmed.Substring(0, marker) : trimmed;
    }

    private static string FormatItemIdFallback(string raw)
    {
        raw = StripRuntimeEnhancedSuffix(raw);
        if (string.IsNullOrEmpty(raw))
            return "items";

        string[] parts = raw.Split('_');
        var words = new System.Collections.Generic.List<string>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (p.Length == 0)
                continue;

            words.Add(char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1).ToLowerInvariant() : ""));
        }

        return words.Count > 0 ? string.Join(" ", words) : "items";
    }
}
