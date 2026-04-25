using UnityEngine;

/// <summary>
/// Runtime log entry when inventory gains resources (gathering, etc.).
/// </summary>
public static class ItemGainPopupNotifier
{
    public static void Notify(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        itemId = itemId.Trim();

        ItemDatabase db = Object.FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        ItemDefinition def = db ? db.Get(itemId) : null;

        string label = ResolveLabel(def, itemId, amount);
        GameLog.ItemGained(label, amount);
    }

    public static void NotifyLost(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        itemId = itemId.Trim();

        ItemDatabase db = Object.FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        ItemDefinition def = db ? db.Get(itemId) : null;

        string label = ResolveLabel(def, itemId, amount);
        GameLog.ItemLost(label, amount);
    }

    private static string ResolveLabel(ItemDefinition def, string itemId, int amount)
    {
        if (def)
        {
            if (amount == 1 && !string.IsNullOrEmpty(def.displayNameSingular))
                return def.displayNameSingular;
            return def.displayName;
        }

        return FormatItemIdFallback(itemId);
    }

    private static string FormatItemIdFallback(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "items";
        string[] parts = raw.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (p.Length == 0)
                continue;
            parts[i] = char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1).ToLowerInvariant() : "");
        }

        return string.Join(" ", parts);
    }
}
