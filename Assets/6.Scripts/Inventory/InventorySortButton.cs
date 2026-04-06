using UnityEngine;

/// <summary>
/// Wire a UI Button OnClick to <see cref="Sort"/>. Sorts either the player <see cref="Inventory"/>
/// or <see cref="PlayerStorage"/> (town chest) using the same database order as the inventory sort.
/// </summary>
public class InventorySortButton : MonoBehaviour
{
    public enum SortTarget
    {
        Inventory = 0,
        Storage = 1
    }

    [SerializeField] private SortTarget sortTarget = SortTarget.Inventory;

    [Tooltip("Optional override. If empty, the player's Inventory is found in the scene.")]
    [SerializeField] private Inventory inventory;

    [Tooltip("Optional override. If empty when Sort Target is Storage, uses PlayerController's PlayerStorage.")]
    [SerializeField] private PlayerStorage playerStorage;

    public void Sort()
    {
        if (sortTarget == SortTarget.Storage)
        {
            var ps = playerStorage != null ? playerStorage : ResolvePlayerStorage();
            if (ps == null)
            {
                Debug.LogWarning("[InventorySortButton] PlayerStorage not found — assign it or ensure the player has PlayerStorage.");
                return;
            }

            ps.SortByDatabaseOrder();
            return;
        }

        var inv = inventory != null ? inventory : FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inv == null)
        {
            Debug.LogWarning("[InventorySortButton] Inventory not found in scene.");
            return;
        }

        inv.SortByDatabaseOrder();
    }

    private static PlayerStorage ResolvePlayerStorage()
    {
        var pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc != null)
        {
            var ps = pc.GetComponent<PlayerStorage>();
            if (ps != null) return ps;
        }

        return FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
    }
}
