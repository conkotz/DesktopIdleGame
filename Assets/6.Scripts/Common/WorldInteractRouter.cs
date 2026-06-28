using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared world-interact routing for mouse clicks and the Interact hotkey.
/// </summary>
public static class WorldInteractRouter
{
    public const float InteractHotkeyHalfRangeX = 2f;

    private static readonly Collider2D[] s_overlapScratch = new Collider2D[64];

    /// <summary>
    /// True when the player's X is within ±<see cref="InteractHotkeyHalfRangeX"/> of the interactable's routable center.
    /// Used to open dialogue/shop immediately instead of walking to the collider edge.
    /// </summary>
    public static bool IsPlayerWithinImmediateInteractRange(PlayerController player, Collider2D interactCol)
    {
        if (!player || !interactCol)
            return false;

        float playerX = player.transform.position.x;
        float targetX = GetRoutableCenterX(interactCol);
        return Mathf.Abs(targetX - playerX) <= InteractHotkeyHalfRangeX + 0.0001f;
    }

    /// <summary>
    /// Picks the routable collider whose X is closest to <paramref name="playerX"/> within ±<paramref name="halfRangeX"/>.
    /// </summary>
    public static bool TryFindClosestRoutableCollider(
        float playerX,
        float playerY,
        LayerMask mask,
        float halfRangeX,
        out Collider2D winner)
    {
        winner = null;
        float bestDx = float.PositiveInfinity;

        Vector2 center = new(playerX, playerY);
        Vector2 size = new(halfRangeX * 2f, 12f);
        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = true
        };

        int count = Physics2D.OverlapBox(center, size, 0f, filter, s_overlapScratch);
        if (count <= 0)
            return false;

        if (count >= s_overlapScratch.Length)
            Debug.LogWarning("[WorldInteractRouter] Overlap buffer full; increase s_overlapScratch.");

        for (int i = 0; i < count; i++)
        {
            Collider2D c = s_overlapScratch[i];
            if (!c || !IsRoutableCollider(c))
                continue;

            float targetX = GetRoutableCenterX(c);
            float dx = Mathf.Abs(targetX - playerX);
            if (dx > halfRangeX + 0.0001f)
                continue;

            if (dx < bestDx - 0.0001f)
            {
                bestDx = dx;
                winner = c;
            }
            else if (Mathf.Approximately(dx, bestDx) && winner != null)
            {
                // Tie-break: prefer visually topmost (same rules as mouse pick at target X).
                Collider2D alt = WorldClickPicker2D.PickTopmostAtPoint(new Vector2(targetX, playerY), mask);
                if (alt != null)
                    winner = alt;
            }
        }

        return winner != null;
    }

    public static bool IsEnterAreaCollider(Collider2D col) =>
        col != null &&
        (col.GetComponentInParent<MapNodePortalTeleporter>() != null ||
         col.GetComponentInParent<InMapTeleporter>() != null);

    /// <summary>
    /// Closest map portal / cave entrance / signpost teleporter within horizontal range (enter-area hotkey only).
    /// </summary>
    public static bool TryFindClosestEnterAreaCollider(
        float playerX,
        float playerY,
        LayerMask mask,
        float halfRangeX,
        out Collider2D winner)
    {
        winner = null;
        float bestDx = float.PositiveInfinity;

        Vector2 center = new(playerX, playerY);
        Vector2 size = new(halfRangeX * 2f, 12f);
        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = true
        };

        int count = Physics2D.OverlapBox(center, size, 0f, filter, s_overlapScratch);
        if (count <= 0)
            return false;

        for (int i = 0; i < count; i++)
        {
            Collider2D c = s_overlapScratch[i];
            if (!c || !IsEnterAreaCollider(c))
                continue;

            float targetX = GetRoutableCenterX(c);
            float dx = Mathf.Abs(targetX - playerX);
            if (dx > halfRangeX + 0.0001f)
                continue;

            if (dx < bestDx - 0.0001f)
            {
                bestDx = dx;
                winner = c;
            }
            else if (Mathf.Approximately(dx, bestDx) && winner != null)
            {
                Collider2D alt = WorldClickPicker2D.PickTopmostAtPoint(new Vector2(targetX, playerY), mask);
                if (alt != null && IsEnterAreaCollider(alt))
                    winner = alt;
            }
        }

        return winner != null;
    }

    /// <summary>
    /// Resolves the nearest enter-area collider (map portal or in-map teleporter) for the enter-area hotkey.
    /// Falls back to the player's interact focus when it is a portal/teleporter (e.g. after clicking one).
    /// </summary>
    public static bool TryResolveEnterAreaColliderForPlayer(
        PlayerController player,
        LayerMask mask,
        float halfRangeX,
        out Collider2D winner)
    {
        winner = null;
        if (!player)
            return false;

        Vector3 pos = player.transform.position;
        if (TryFindClosestEnterAreaCollider(pos.x, pos.y, mask, halfRangeX, out winner))
            return true;

        return TryGetEnterAreaColliderFromInteractFocus(player, out winner);
    }

    public static void RouteEnterArea(Collider2D winnerCol, PlayerController player) =>
        RouteContextPortalEnter(winnerCol, player);

    public static void RouteInteract(Collider2D winnerCol, PlayerController player)
    {
        if (!winnerCol || !player)
            return;

        StorageClick targetStorage = winnerCol.GetComponentInParent<StorageClick>();
        DismissPreviousInteractUiForNewTarget(winnerCol, targetStorage);
        PlayerWorldInteractFocus.TrySetFocusFromCollider(player, winnerCol);

        StorageClick storage = targetStorage;
        if (storage != null)
        {
            ApplyCombatTargetWhenInteractingNonEnemy(player);
            storage.Open();
            return;
        }

        FurnaceClick furnace = winnerCol.GetComponentInParent<FurnaceClick>();
        if (furnace != null)
        {
            ApplyCombatTargetWhenInteractingNonEnemy(player);
            NPCInteractionSettings furnaceNpc = winnerCol.GetComponentInParent<NPCInteractionSettings>();
            if (furnaceNpc != null)
                furnaceNpc.Interact();
            furnace.Open();
            return;
        }

        CookingClick cooking = winnerCol.GetComponentInParent<CookingClick>();
        if (cooking != null)
        {
            ApplyCombatTargetWhenInteractingNonEnemy(player);
            NPCInteractionSettings cookingNpc = winnerCol.GetComponentInParent<NPCInteractionSettings>();
            if (cookingNpc != null)
                cookingNpc.Interact();
            cooking.Open();
            return;
        }

        var portal = winnerCol.GetComponentInParent<MapNodePortalTeleporter>();
        if (portal != null)
        {
            portal.OnClickedByPlayer(player);
            return;
        }

        InMapTeleporter inMapTeleporter = winnerCol.GetComponentInParent<InMapTeleporter>();
        if (inMapTeleporter != null)
        {
            inMapTeleporter.OnClickedByPlayer(player);
            return;
        }

        var drop = winnerCol.GetComponentInParent<ItemDrop>();
        if (drop != null)
        {
            player.RequestPickup(drop);
            return;
        }

        var node = winnerCol.GetComponentInParent<ResourceNode>();
        if (node != null)
        {
            player.SelectNode(node);
            return;
        }

        NPCInteractionSettings npc = winnerCol.GetComponentInParent<NPCInteractionSettings>();
        MerchantClick merchant = winnerCol.GetComponentInParent<MerchantClick>();

        if (merchant != null || npc != null)
        {
            ApplyCombatTargetWhenInteractingNonEnemy(player);

            if (merchant != null)
            {
                if (npc != null)
                    npc.Interact();

                merchant.Open();
                return;
            }

            npc.Interact();
            return;
        }

        QuestGiver questGiver = winnerCol.GetComponentInParent<QuestGiver>();
        if (questGiver != null)
        {
            ApplyCombatTargetWhenInteractingNonEnemy(player);

            NPCInteractionSettings npcOnGiver = questGiver.GetComponent<NPCInteractionSettings>();
            if (npcOnGiver != null)
            {
                npcOnGiver.Interact();
                return;
            }

            if (questGiver.TryClaimFirstReadyQuestReward())
                return;
        }
    }

    /// <summary>
    /// Closes merchant / storage / dialogue from a previous interact target so the new target can open cleanly.
    /// Re-interacting the same open storage chest is left alone (re-pin only).
    /// </summary>
    private static void DismissPreviousInteractUiForNewTarget(Collider2D winnerCol, StorageClick targetStorage)
    {
        MerchantClick.ForceCloseMerchantMode();
        MerchantClick.CancelPendingOpen();
        FurnaceClick.CancelPendingOpen();
        CookingClick.CancelPendingOpen();
        NPCInteractionSettings.CancelPendingInteract();
        FurnaceClick.ForceClose();
        CookingClick.ForceClose();

        if (targetStorage == null || !StorageClick.IsActiveInstance(targetStorage))
            StorageClick.ForceCloseStorageMode();

        Transform newRoot = GetInteractDismissRoot(winnerCol);
        if (!NPCDialogueBoxUI.HasAnyActiveDialogue)
            return;

        if (newRoot == null || !NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(newRoot))
            NPCDialogueBoxUI.DismissAllActive();
    }

    /// <summary>World transform used for interact focus icon and dialogue dismiss grouping.</summary>
    public static Transform ResolveInteractFocusRoot(Collider2D col) => GetInteractDismissRoot(col);

    private static Transform GetInteractDismissRoot(Collider2D col)
    {
        if (!col)
            return null;

        StorageClick storage = col.GetComponentInParent<StorageClick>();
        if (storage)
            return storage.transform;

        NPCInteractionSettings npc = col.GetComponentInParent<NPCInteractionSettings>();
        if (npc)
            return npc.transform;

        MerchantClick merchant = col.GetComponentInParent<MerchantClick>();
        if (merchant)
            return merchant.transform;

        QuestGiver questGiver = col.GetComponentInParent<QuestGiver>();
        if (questGiver)
            return questGiver.transform;

        MapNodePortalTeleporter portal = col.GetComponentInParent<MapNodePortalTeleporter>();
        if (portal)
            return portal.transform;

        InMapTeleporter inMapTeleporter = col.GetComponentInParent<InMapTeleporter>();
        if (inMapTeleporter)
            return inMapTeleporter.transform;

        if (IsNoticeBoardCollider(col))
            return col.transform;

        return col.transform;
    }

    public static bool IsRoutableCollider(Collider2D col)
    {
        if (!col)
            return false;

        if (col.GetComponentInParent<StorageClick>()) return true;
        if (col.GetComponentInParent<FurnaceClick>()) return true;
        if (col.GetComponentInParent<CookingClick>()) return true;
        if (col.GetComponentInParent<ItemDrop>()) return true;
        if (col.GetComponentInParent<ResourceNode>()) return true;
        if (col.GetComponentInParent<NPCInteractionSettings>()) return true;
        if (col.GetComponentInParent<MerchantClick>()) return true;
        if (col.GetComponentInParent<MapNodePortalTeleporter>()) return true;
        if (col.GetComponentInParent<InMapTeleporter>()) return true;
        if (col.GetComponentInParent<QuestGiver>()) return true;
        if (IsNoticeBoardCollider(col)) return true;
        return false;
    }

    public static bool IsNoticeBoardCollider(Collider2D col) =>
        col != null &&
        col.CompareTag("NoticeBoard") &&
        (col.GetComponentInParent<NPCInteractionSettings>() != null ||
         col.GetComponentInParent<QuestGiver>() != null);

    /// <summary>Prepares focus + dismisses prior interact UI before a context-menu action.</summary>
    public static void PrepareForContextAction(Collider2D col, PlayerController player)
    {
        if (!col || !player)
            return;

        StorageClick targetStorage = col.GetComponentInParent<StorageClick>();
        DismissPreviousInteractUiForNewTarget(col, targetStorage);
        PlayerWorldInteractFocus.TrySetFocusFromCollider(player, col);
    }

    public static void RouteContextShop(Collider2D col, PlayerController player)
    {
        if (!col || !player)
            return;

        MerchantClick merchant = col.GetComponentInParent<MerchantClick>();
        if (!merchant)
            return;

        PrepareForContextAction(col, player);
        ApplyCombatTargetWhenInteractingNonEnemy(player);
        merchant.Open();
    }

    public static void RouteContextTalk(Collider2D col, PlayerController player)
    {
        if (!col || !player)
            return;

        PrepareForContextAction(col, player);
        ApplyCombatTargetWhenInteractingNonEnemy(player);

        FurnaceClick furnace = col.GetComponentInParent<FurnaceClick>();
        if (furnace != null)
        {
            furnace.Open();
            return;
        }

        CookingClick cooking = col.GetComponentInParent<CookingClick>();
        if (cooking != null)
        {
            cooking.Open();
            return;
        }

        NPCInteractionSettings npc = col.GetComponentInParent<NPCInteractionSettings>();
        if (npc != null)
        {
            npc.Interact();
            return;
        }

        QuestGiver questGiver = col.GetComponentInParent<QuestGiver>();
        if (questGiver != null && questGiver.TryClaimFirstReadyQuestReward())
            return;
    }

    public static void RouteContextRead(Collider2D col, PlayerController player) =>
        RouteContextTalk(col, player);

    public static void RouteContextStorageOpen(Collider2D col, PlayerController player)
    {
        if (!col || !player)
            return;

        StorageClick storage = col.GetComponentInParent<StorageClick>();
        if (!storage)
            return;

        PrepareForContextAction(col, player);
        ApplyCombatTargetWhenInteractingNonEnemy(player);
        storage.Open();
    }

    public static void RouteContextPortalEnter(Collider2D col, PlayerController player)
    {
        if (!col || !player)
            return;

        MapNodePortalTeleporter portal = col.GetComponentInParent<MapNodePortalTeleporter>();
        if (portal != null)
        {
            PrepareForContextAction(col, player);
            portal.OnClickedByPlayer(player);
            return;
        }

        InMapTeleporter inMapTeleporter = col.GetComponentInParent<InMapTeleporter>();
        if (inMapTeleporter == null)
            return;

        PrepareForContextAction(col, player);
        inMapTeleporter.OnClickedByPlayer(player);
    }

    public static void RouteContextAttack(Collider2D col, PlayerController player)
    {
        if (!col || !player)
            return;

        EnemyClick enemyClick = col.GetComponentInParent<EnemyClick>();
        if (!enemyClick)
            return;

        EnemyBaseController enemy = enemyClick.GetEnemy();
        if (!enemy)
            return;

        PrepareForContextAction(col, player);
        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        combat?.EngageTargetFromPlayerInput(enemy);
    }

    public static void RouteContextGather(Collider2D col, PlayerController player)
    {
        if (!col || !player)
            return;

        ResourceNode node = col.GetComponentInParent<ResourceNode>();
        if (!node)
            return;

        PrepareForContextAction(col, player);
        player.SelectNode(node);
    }

    /// <summary>Moves the player toward an interactable without triggering its primary action.</summary>
    public static void WalkPlayerToCollider(PlayerController player, Collider2D col)
    {
        if (!player || !col)
            return;

        MerchantClick.CancelPendingOpen();
        FurnaceClick.CancelPendingOpen();
        CookingClick.CancelPendingOpen();
        NPCInteractionSettings.CancelPendingInteract();
        MapNodePortalTeleporter.CancelPendingApproachForPlayer(player);
        InMapTeleporter.CancelPendingApproachForPlayer(player);

        ResourceNode node = col.GetComponentInParent<ResourceNode>();
        if (node)
        {
            node.ChooseClosestWorkSpot(player.transform.position);
            float targetX = node.workSpot ? node.workSpot.position.x : node.transform.position.x;
            player.MoveToPointX(targetX, fromPlayerInput: true);
            return;
        }

        MapNodePortalTeleporter portal = col.GetComponentInParent<MapNodePortalTeleporter>();
        if (portal)
        {
            Collider2D portalCol = col.GetComponent<Collider2D>() ?? col;
            Bounds b = portalCol.bounds;
            float arrivalX = Mathf.Clamp(player.transform.position.x, b.min.x, b.max.x);
            player.MoveToPointX(arrivalX, fromPlayerInput: true);
            return;
        }

        InMapTeleporter inMapTeleporter = col.GetComponentInParent<InMapTeleporter>();
        if (inMapTeleporter)
        {
            Collider2D teleporterCol = col.GetComponent<Collider2D>() ?? col;
            Bounds b = teleporterCol.bounds;
            float arrivalX = Mathf.Clamp(player.transform.position.x, b.min.x, b.max.x);
            player.MoveToPointX(arrivalX, fromPlayerInput: true);
            return;
        }

        EnemyClick enemyClick = col.GetComponentInParent<EnemyClick>();
        if (enemyClick != null)
        {
            EnemyBaseController enemy = enemyClick.GetEnemy();
            float targetX = enemy ? enemy.transform.position.x : col.bounds.center.x;
            player.MoveToPointX(targetX, fromPlayerInput: true);
            return;
        }

        player.MoveToPointX(ComputeWalkApproachTargetX(player, col), fromPlayerInput: true);
    }

    private static float ComputeWalkApproachTargetX(PlayerController player, Collider2D col)
    {
        if (!player || !col)
            return 0f;

        Bounds b = col.bounds;
        float playerX = player.transform.position.x;
        bool approachFromLeft = playerX <= b.center.x;
        float edgeX = approachFromLeft ? b.min.x : b.max.x;
        float sign = approachFromLeft ? -1f : 1f;

        Collider2D playerCol = player.GetComponent<Collider2D>() ??
                               player.GetComponentInChildren<Collider2D>(true);
        float playerHalfWidth = playerCol ? playerCol.bounds.extents.x : 0f;
        return edgeX + sign * playerHalfWidth;
    }

    /// <summary>
    /// Interact hotkey with nothing routable in range: engage closest enemy within ±<see cref="InteractHotkeyHalfRangeX"/>,
    /// or disengage the current target when it is outside that band.
    /// </summary>
    public static void ApplyCombatTargetForInteractHotkeyMiss(PlayerController player)
    {
        if (!player)
            return;

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        if (combat == null)
            return;

        float playerX = player.transform.position.x;

        EnemyBaseController closestInInteractRange = FindClosestEnemyInInteractRange(player);
        if (closestInInteractRange != null)
        {
            combat.SetTarget(closestInInteractRange);
            return;
        }

        EnemyBaseController current = combat.CurrentTarget;
        if (current == null || current.IsDead || !current.gameObject.activeInHierarchy)
            return;

        float dx = Mathf.Abs(current.transform.position.x - playerX);
        if (dx > InteractHotkeyHalfRangeX + 0.0001f)
            combat.ClearTarget();
    }

    /// <summary>
    /// Non-enemy interact (NPC, storage, notice board, etc.): stop fighting and focus the clicked interactable.
    /// </summary>
    private static void ApplyCombatTargetWhenInteractingNonEnemy(PlayerController player)
    {
        if (!player)
            return;

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        if (combat == null)
            return;

        PlayerAbilityController.EndAutoBattleWhirlwindChannelIfActive();
        combat.ClearTarget();
    }

    private static EnemyBaseController FindClosestEnemyInInteractRange(PlayerController player)
    {
        if (!player)
            return null;

        float playerX = player.transform.position.x;
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDx = float.MaxValue;
        float maxDx = InteractHotkeyHalfRangeX;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (!enemy || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            if (!PlayerCombatController.IsValidCombatTargetForPlayer(enemy, player))
                continue;

            float dx = Mathf.Abs(enemy.transform.position.x - playerX);
            if (dx > maxDx + 0.0001f)
                continue;

            if (dx < bestDx)
            {
                bestDx = dx;
                best = enemy;
            }
        }

        return best;
    }

    private static bool TryGetEnterAreaColliderFromInteractFocus(PlayerController player, out Collider2D winner)
    {
        winner = null;
        if (!player)
            return false;

        PlayerWorldInteractFocus focus = player.GetComponent<PlayerWorldInteractFocus>();
        Transform root = focus != null ? focus.CurrentFocusTransform : null;
        if (!root)
            return false;

        MapNodePortalTeleporter portal = root.GetComponentInParent<MapNodePortalTeleporter>();
        if (portal != null)
        {
            winner = portal.GetComponent<Collider2D>() ?? portal.GetComponentInChildren<Collider2D>(true);
            return winner != null;
        }

        InMapTeleporter teleporter = root.GetComponentInParent<InMapTeleporter>();
        if (teleporter != null)
        {
            winner = teleporter.GetComponent<Collider2D>() ?? teleporter.GetComponentInChildren<Collider2D>(true);
            return winner != null;
        }

        return false;
    }

    private static float GetRoutableCenterX(Collider2D col)
    {
        if (!col)
            return 0f;

        var enemy = col.GetComponentInParent<EnemyClick>();
        if (enemy)
            return enemy.transform.position.x;

        var drop = col.GetComponentInParent<ItemDrop>();
        if (drop)
            return drop.transform.position.x;

        var node = col.GetComponentInParent<ResourceNode>();
        if (node && node.workSpot)
            return node.workSpot.position.x;

        return col.bounds.center.x;
    }
}
