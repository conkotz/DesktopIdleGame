using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared helpers for world right-click context menus.</summary>
public static class WorldContextMenuEntries
{
    public static void AddMoveObjectIfAllowed(
        List<ContextMenuEntry> entries,
        Collider2D col,
        PlayerController player)
    {
        if (!col || !player)
            return;

        if (!WorldObjectMovable.TryGetFromCollider(col, out _))
            return;

        entries.Add(new ContextMenuEntry(
            "Move Object",
            () => WorldInteractRouter.RouteContextMoveObject(col, player)));
    }
}
