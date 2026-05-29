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

        var enemyClick = winnerCol.GetComponentInParent<EnemyClick>();
        if (enemyClick != null)
        {
            EnemyBaseController clickedEnemy = enemyClick.GetEnemy();
            if (clickedEnemy != null)
            {
                PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
                if (combat != null)
                    combat.EngageTargetFromPlayerInput(clickedEnemy);
            }

            return;
        }

        var portal = winnerCol.GetComponentInParent<MapNodePortalTeleporter>();
        if (portal != null)
        {
            portal.OnClickedByPlayer(player);
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
        NPCInteractionSettings.CancelPendingInteract();

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

        if (IsNoticeBoardCollider(col))
            return col.transform;

        return col.transform;
    }

    public static bool IsRoutableCollider(Collider2D col)
    {
        if (!col)
            return false;

        if (col.GetComponentInParent<StorageClick>()) return true;
        if (col.GetComponentInParent<EnemyClick>()) return true;
        if (col.GetComponentInParent<ItemDrop>()) return true;
        if (col.GetComponentInParent<ResourceNode>()) return true;
        if (col.GetComponentInParent<NPCInteractionSettings>()) return true;
        if (col.GetComponentInParent<MerchantClick>()) return true;
        if (col.GetComponentInParent<MapNodePortalTeleporter>()) return true;
        if (col.GetComponentInParent<QuestGiver>()) return true;
        if (IsNoticeBoardCollider(col)) return true;
        return false;
    }

    private static bool IsNoticeBoardCollider(Collider2D col) =>
        col != null &&
        col.CompareTag("NoticeBoard") &&
        (col.GetComponentInParent<NPCInteractionSettings>() != null ||
         col.GetComponentInParent<QuestGiver>() != null);

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
    /// Non-enemy interact (NPC, storage, notice board, etc.): drop a far-away combat target, keep one still in weapon range,
    /// or retarget the closest enemy within interact range (±<see cref="InteractHotkeyHalfRangeX"/>).
    /// </summary>
    private static void ApplyCombatTargetWhenInteractingNonEnemy(PlayerController player)
    {
        if (!player)
            return;

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        if (combat == null)
            return;

        EnemyBaseController current = combat.CurrentTarget;
        if (current != null && !current.IsDead && current.gameObject.activeInHierarchy &&
            combat.IsEnemyWithinAttackRange(current))
            return;

        EnemyBaseController closestInInteractRange = FindClosestEnemyInInteractRange(player);
        if (closestInInteractRange != null)
        {
            combat.SetTarget(closestInInteractRange);
            return;
        }

        if (current != null && !current.IsDead && current.gameObject.activeInHierarchy)
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
