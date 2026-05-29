using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds right-click context menu entries for world resource nodes.</summary>
public static class ResourceContextMenuBuilder
{
    public static List<ContextMenuEntry> Build(ResourceNode node, PlayerController player)
    {
        var entries = new List<ContextMenuEntry>(2);
        if (!node || !player)
            return entries;

        string gatherLabel = node.ActionType switch
        {
            NodeAction.Woodcutting => "Chop",
            NodeAction.Mining => "Mine",
            NodeAction.Fishing => "Fish",
            _ => "Gather"
        };

        entries.Add(new ContextMenuEntry(gatherLabel, () => WorldInteractRouter.RouteContextGather(
            node.GetComponent<Collider2D>() ?? node.GetComponentInChildren<Collider2D>(), player)));
        entries.Add(new ContextMenuEntry("Walk here", () =>
        {
            Collider2D col = node.GetComponent<Collider2D>() ?? node.GetComponentInChildren<Collider2D>();
            WorldInteractRouter.WalkPlayerToCollider(player, col);
        }));
        return entries;
    }
}
