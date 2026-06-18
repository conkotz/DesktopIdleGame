using UnityEngine;

/// <summary>
/// When the player voluntarily leaves a map (teleport / travel), unclaimed ground loot is recovered
/// into inventory first, then the Main storage tab. Death respawn does not call this.
/// </summary>
public static class MapExitGroundLootCollector
{
    public static void CollectAllToInventoryAndMainStorage()
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (player == null || player.IsDead)
            return;

        Inventory inventory = player.GetComponent<Inventory>();
        PlayerStorage storage = player.GetComponent<PlayerStorage>();
        if (inventory == null || storage == null)
            return;

        ItemDrop[] drops = Object.FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < drops.Length; i++)
        {
            ItemDrop drop = drops[i];
            if (drop != null)
                drop.CollectForVoluntaryMapExit(inventory, storage);
        }
    }
}
