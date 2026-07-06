using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds right-click context menu entries for world interactables.</summary>
public static class WorldContextMenuBuilder
{
    public static List<ContextMenuEntry> Build(Collider2D col, PlayerController player)
    {
        var entries = new List<ContextMenuEntry>(3);
        if (!col || !player)
            return entries;

        EnemyClick enemyClick = col.GetComponentInParent<EnemyClick>();
        if (enemyClick != null && enemyClick.GetEnemy() != null)
        {
            AddWalkHere(entries, col, player);
            entries.Insert(0, new ContextMenuEntry("Attack", () => WorldInteractRouter.RouteContextAttack(col, player)));
            return entries;
        }

        StorageClick storage = col.GetComponentInParent<StorageClick>();
        if (storage != null)
        {
            entries.Add(new ContextMenuEntry("Open", () => WorldInteractRouter.RouteContextStorageOpen(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
            return entries;
        }

        MapNodePortalTeleporter portal = col.GetComponentInParent<MapNodePortalTeleporter>();
        if (portal != null)
        {
            entries.Add(new ContextMenuEntry("Enter", () => WorldInteractRouter.RouteContextPortalEnter(col, player)));
            AddWalkHere(entries, col, player);
            return entries;
        }

        InMapTeleporter inMapTeleporter = col.GetComponentInParent<InMapTeleporter>();
        if (inMapTeleporter != null)
        {
            entries.Add(new ContextMenuEntry("Teleport", () => WorldInteractRouter.RouteContextPortalEnter(col, player)));
            AddWalkHere(entries, col, player);
            return entries;
        }

        ResourceNode node = col.GetComponentInParent<ResourceNode>();
        if (node != null)
            return ResourceContextMenuBuilder.Build(node, player);

        BlacksmithingClick blacksmithing = col.GetComponentInParent<BlacksmithingClick>();
        if (blacksmithing != null)
        {
            entries.Add(new ContextMenuEntry("Smith", () => WorldInteractRouter.RouteContextTalk(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
            return entries;
        }

        FurnaceClick furnace = col.GetComponentInParent<FurnaceClick>();
        if (furnace != null)
        {
            entries.Add(new ContextMenuEntry("Smelt", () => WorldInteractRouter.RouteContextTalk(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
            return entries;
        }

        CookingClick cooking = col.GetComponentInParent<CookingClick>();
        if (cooking != null)
        {
            entries.Add(new ContextMenuEntry("Cook", () => WorldInteractRouter.RouteContextTalk(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
            return entries;
        }

        if (WorldInteractRouter.IsNoticeBoardCollider(col))
        {
            entries.Add(new ContextMenuEntry("Read", () => WorldInteractRouter.RouteContextRead(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
            return entries;
        }

        MerchantClick merchant = col.GetComponentInParent<MerchantClick>();
        NPCInteractionSettings npc = col.GetComponentInParent<NPCInteractionSettings>();

        if (merchant != null && npc != null)
        {
            entries.Add(new ContextMenuEntry("Shop", () => WorldInteractRouter.RouteContextShop(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
            return entries;
        }

        if (merchant != null)
        {
            entries.Add(new ContextMenuEntry("Shop", () => WorldInteractRouter.RouteContextShop(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
            return entries;
        }

        if (npc != null)
        {
            entries.Add(new ContextMenuEntry("Talk", () => WorldInteractRouter.RouteContextTalk(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
            return entries;
        }

        QuestGiver questGiver = col.GetComponentInParent<QuestGiver>();
        if (questGiver != null)
        {
            entries.Add(new ContextMenuEntry("Talk", () => WorldInteractRouter.RouteContextTalk(col, player)));
            WorldContextMenuEntries.AddMoveObjectIfAllowed(entries, col, player);
            AddWalkHere(entries, col, player);
        }

        return entries;
    }

    private static void AddWalkHere(List<ContextMenuEntry> entries, Collider2D col, PlayerController player)
    {
        entries.Add(new ContextMenuEntry("Walk here", () => WorldInteractRouter.WalkPlayerToCollider(player, col)));
    }
}
