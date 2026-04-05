using UnityEngine;

public class DebugGiveItems : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;
    [SerializeField] private LevelUpEffect levelUpEffect;
    [SerializeField] private GoldPopupSpawner popupSpawner;
    [SerializeField] private Transform popupAnchor;

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
    [SerializeField] private ItemDefinition stoneDef;
    [SerializeField] private ItemDefinition woodDef;
    [SerializeField] private ItemDefinition fishDef;

    [Header("Amount")]
    [SerializeField] private int amount = 1;

    [Header("L Debug Pack")]
    [SerializeField] private KeyCode grantPackKey = KeyCode.L;
    [SerializeField] private int grantGold = 50000;
    [SerializeField] private int grantResourceAmount = 100;
    [SerializeField] private Vector3 popupWorldOffset = new Vector3(0f, 1.6f, 0f);

    private void Awake()
    {
        if (!inventory)
            inventory = GetComponent<Inventory>();

        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (!wallet)
            wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

        if (!popupSpawner)
            popupSpawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);

        if (!popupAnchor)
        {
            var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            popupAnchor = player ? player.transform : transform;
        }

        if (!levelUpEffect)
            levelUpEffect = GetComponentInChildren<LevelUpEffect>(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(grantPackKey))
            GrantDebugPack();
        
        // Disabled for now: number-key spawning.
        // (We only want the L debug pack to apply.)
        /*
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
        */
    }

    private void GrantDebugPack()
    {
        if (wallet != null && grantGold > 0)
            wallet.AddGold(grantGold);

        AddToInventory(stoneDef, grantResourceAmount);
        AddToInventory(woodDef, grantResourceAmount);
        AddToInventory(fishDef, grantResourceAmount);

        if (levelUpEffect != null)
            levelUpEffect.PlayLevelUp();

        GrantAllSkillsPlusOneLevel();

        if (popupSpawner != null && popupAnchor != null)
            popupSpawner.ShowMessageAtWorld(popupAnchor.position + popupWorldOffset, "DEBUG LEVEL UP!", Color.yellow);

        Debug.Log($"[DebugGiveItems] Granted pack: +{grantGold} gold, +{grantResourceAmount} stone/wood/fish, +1 level all skills.");
    }

    /// <summary>Grants exactly one level per skill via XP (same rules as normal progression).</summary>
    private void GrantAllSkillsPlusOneLevel()
    {
        SkillsManager sm = ResolveSkillsManager();
        if (sm == null)
        {
            Debug.LogWarning("[DebugGiveItems] No SkillsManager — skill levels unchanged.");
            return;
        }

        foreach (SkillType t in System.Enum.GetValues(typeof(SkillType)))
        {
            int rem = sm.GetXpRemainingThisLevel(t);
            int need = rem > 0 ? rem : sm.XpToNextLevel(sm.GetLevel(t));
            if (need <= 0)
                continue;
            sm.AddXp(t, need, "debug_l");
        }
    }

    /// <summary>
    /// No inspector wire-up — bootstrap lives on a different object than the player / skills singleton.
    /// Resolved when L is pressed so <see cref="SkillsManager"/> exists after gameplay loads.
    /// </summary>
    private static SkillsManager ResolveSkillsManager()
    {
        if (SkillsManager.Instance != null)
            return SkillsManager.Instance;

        return FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }

    private void AddToInventory(ItemDefinition def, int qty)
    {
        if (inventory == null || def == null || qty <= 0) return;
        inventory.Add(def.itemId, qty);
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