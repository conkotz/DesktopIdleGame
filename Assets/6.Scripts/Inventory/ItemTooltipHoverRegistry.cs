using System.Collections.Generic;
using UnityEngine;

/// <summary>Tracks hovered item slots so Alt-key advanced tooltip mode can refresh them in place.</summary>
public static class ItemTooltipHoverRegistry
{
    private static readonly HashSet<IItemTooltipHoverSource> Hovered = new HashSet<IItemTooltipHoverSource>();

    public static void SetHovered(IItemTooltipHoverSource source, bool hovered)
    {
        if (source == null)
            return;

        if (hovered)
            Hovered.Add(source);
        else
            Hovered.Remove(source);
    }

    public static void RefreshAllHovered()
    {
        if (Hovered.Count == 0)
            return;

        // Prune destroyed Unity objects first (C# refs can remain non-null after Destroy).
        Hovered.RemoveWhere(IsDestroyedOrNull);

        if (Hovered.Count == 0)
            return;

        var snapshot = new IItemTooltipHoverSource[Hovered.Count];
        Hovered.CopyTo(snapshot);
        for (int i = 0; i < snapshot.Length; i++)
            snapshot[i]?.RefreshTooltipIfHovered();
    }

    private static bool IsDestroyedOrNull(IItemTooltipHoverSource source)
    {
        if (source == null)
            return true;
        if (source is Object unityObj && !unityObj)
            return true;
        return false;
    }
}
