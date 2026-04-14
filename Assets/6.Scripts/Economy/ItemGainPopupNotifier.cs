using UnityEngine;

/// <summary>
/// World-space popup when inventory gains resources (gathering, etc.). Uses <see cref="GoldPopupSpawner"/>.
/// </summary>
public static class ItemGainPopupNotifier
{
    private static readonly Color DefaultGainColor = new Color(0.55f, 0.92f, 0.58f, 1f);

    public static void Notify(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        itemId = itemId.Trim();

        ItemDatabase db = Object.FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        ItemDefinition def = db ? db.Get(itemId) : null;
        if (def != null && def.itemKind != ItemKind.Resource)
            return;

        GoldPopupSpawner spawner = Object.FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
        if (!spawner)
            return;

        Transform anchor = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include)?.transform;
        if (!anchor)
            return;

        string label = ResolveLabel(def, itemId, amount);
        string msg = $"+{amount} {label}";
        Vector3 worldPos = anchor.position + Vector3.up * 1.2f;
        spawner.ShowMessageAtWorld(worldPos, msg, DefaultGainColor);
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
