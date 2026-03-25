using UnityEngine;

public class InventorySortButton : MonoBehaviour
{
    [SerializeField] private Inventory inventory;

    private void Awake()
    {
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
    }

    public void Sort()
    {
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (!inventory)
        {
            Debug.LogWarning("[InventorySortButton] Inventory not found in scene.");
            return;
        }

        inventory.SortByDatabaseOrder();
    }
}