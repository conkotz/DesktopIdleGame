using UnityEngine;

/// <summary>
/// When the player voluntarily leaves a map (teleport / travel), combat enemy loot on the ground is recovered
/// into inventory first, then storage (affinity tab then Main). Overflow is queued in
/// <see cref="PendingLootRecoveryStore"/>. Player inventory drops and level-placed map pickups are skipped.
/// Death respawn does not call this.
/// </summary>
public static class MapExitGroundLootCollector
{
    public static void CollectAllToInventoryAndMainStorage()
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (player == null || player.IsDead)
            return;

        Inventory inventory = player.GetComponent<Inventory>();
        if (inventory == null)
            inventory = Inventory.ResolvePlayer();

        // Storage often lives on a separate DDOL/chest object — do not require it on PlayerController.
        PlayerStorage storage = player.GetComponent<PlayerStorage>();
        if (storage == null)
            storage = Object.FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);

        // ItemDrop.CollectForVoluntaryMapExit already handles null inventory and/or storage
        // (pending-loot fallback). Only skip when neither destination exists.
        if (inventory == null && storage == null)
            return;

        ItemDrop[] drops = Object.FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < drops.Length; i++)
        {
            ItemDrop drop = drops[i];
            if (drop != null && drop.SweepOnMapExit)
                drop.CollectForVoluntaryMapExit(inventory, storage);
        }
    }
}
