using UnityEngine;

public class DebugGiveItems : MonoBehaviour
{
    [SerializeField] private Inventory inventory;

    [Header("Item Definitions to Test")]
    [SerializeField] private ItemDefinition swordDef;
    [SerializeField] private ItemDefinition shieldDef;
    [SerializeField] private ItemDefinition axeDef;
    [SerializeField] private ItemDefinition rodDef;
    [SerializeField] private ItemDefinition pickaxeDef;
    [SerializeField] private ItemDefinition spearDef;
    [SerializeField] private ItemDefinition polearmDef;
    [SerializeField] private ItemDefinition daggerDef;
    [SerializeField] private ItemDefinition maceDef;
    [SerializeField] private ItemDefinition critRingDef;
    [SerializeField] private ItemDefinition vampRingDef;
    [SerializeField] private ItemDefinition bootsDef;

    [Header("Amount")]
    [SerializeField] private int amount = 1;

    private void Awake()
    {
        if (!inventory)
            inventory = GetComponent<Inventory>();

        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Spawn(axeDef);
        if (Input.GetKeyDown(KeyCode.Alpha1)) Spawn(rodDef);
        if (Input.GetKeyDown(KeyCode.Alpha1)) Spawn(pickaxeDef);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Spawn(spearDef);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Spawn(polearmDef);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Spawn(daggerDef);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Spawn(swordDef);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Spawn(shieldDef);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Spawn(maceDef);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Spawn(critRingDef);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Spawn(vampRingDef);
        if (Input.GetKeyDown(KeyCode.Alpha4)) Spawn(bootsDef);


    }

    private void Spawn(ItemDefinition def)
    {
        if (!def)
        {
            Debug.LogWarning("[DebugGiveItems] ItemDefinition missing.");
            return;
        }

        if (DropManager.Instance == null)
        {
            Debug.LogWarning("[DebugGiveItems] No DropManager found.");
            return;
        }

        DropManager.Instance.Spawn(def.itemId, amount, def.icon);

        Debug.Log($"[DebugGiveItems] Spawned {amount} of '{def.itemId}' to world.");
    }
}