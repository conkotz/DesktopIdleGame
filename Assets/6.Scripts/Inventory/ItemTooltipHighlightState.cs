using System.Collections.Generic;

/// <summary>Tracks which inventory/storage item instances should show bold bonus-stat tooltips.</summary>
public static class ItemTooltipHighlightState
{
    private static readonly HashSet<string> EnabledItemIds = new HashSet<string>();

    public static bool IsEnabled(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && EnabledItemIds.Contains(itemId);
    }

    public static void SetEnabled(string itemId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (enabled)
            EnabledItemIds.Add(itemId);
        else
            EnabledItemIds.Remove(itemId);
    }

    public static void Toggle(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (EnabledItemIds.Contains(itemId))
            EnabledItemIds.Remove(itemId);
        else
            EnabledItemIds.Add(itemId);
    }
}
