using UnityEngine;

public class DebugGiveItems : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private LevelUpEffect levelUpEffect;
    [SerializeField] private GoldPopupSpawner popupSpawner;
    [SerializeField] private Transform popupAnchor;

    [Header("F1")]
    [SerializeField] private ItemDefinition fishDef;
    [SerializeField] private ItemDefinition logsDef;
    [SerializeField] private ItemDefinition stoneChunkDef;
    [SerializeField] private int grantResourceAmount = 100;

    [Header("F2 / F3 feedback")]
    [SerializeField] private Vector3 popupWorldOffset = new Vector3(0f, 1.6f, 0f);

    [Header("F4")]
    [SerializeField] private ItemDefinition devDestroyerMaceDef;

    private void Awake()
    {
        if (!inventory)
            inventory = GetComponent<Inventory>();

        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

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
        if (Input.GetKeyDown(KeyCode.F1))
            GrantResourcePack();

        if (Input.GetKeyDown(KeyCode.F2))
            GrantAllSkillsPlusOneLevelWithFeedback();

        if (Input.GetKeyDown(KeyCode.F3))
            DecreaseAllSkillsOneLevelWithFeedback();

        if (Input.GetKeyDown(KeyCode.F4))
            GrantDebugMace();
    }

    private void GrantResourcePack()
    {
        int n = Mathf.Max(1, grantResourceAmount);
        AddToInventory(fishDef, n);
        AddToInventory(logsDef, n);
        AddToInventory(stoneChunkDef, n);

        Debug.Log($"[DebugGiveItems] F1: +{n} fish, +{n} logs, +{n} stone (null defs skipped).");
    }

    private void DecreaseAllSkillsOneLevelWithFeedback()
    {
        SkillsManager sm = ResolveSkillsManager();
        if (sm == null)
        {
            Debug.LogWarning("[DebugGiveItems] F3: no SkillsManager — cannot decrease skill levels.");
            return;
        }

        sm.DebugDecreaseAllSkillsOneLevel();

        if (popupSpawner != null && popupAnchor != null)
            popupSpawner.ShowMessageAtWorld(popupAnchor.position + popupWorldOffset, "DEBUG -1 ALL SKILLS", new Color(0.85f, 0.55f, 0.35f));

        Debug.Log("[DebugGiveItems] F3: -1 level on all tracked skills (min 1).");
    }

    private void GrantDebugMace()
    {
        AddToInventory(devDestroyerMaceDef, 1);
        Debug.Log("[DebugGiveItems] F4: +1 dev_destroyer_mace.");
    }

    private void GrantAllSkillsPlusOneLevelWithFeedback()
    {
        SkillsManager sm = ResolveSkillsManager();
        if (sm == null)
        {
            Debug.LogWarning("[DebugGiveItems] F2: no SkillsManager — cannot grant levels.");
            return;
        }

        sm.DebugIncreaseAllSkillsOneLevel();

        if (levelUpEffect != null)
            levelUpEffect.PlayLevelUp();

        if (popupSpawner != null && popupAnchor != null)
            popupSpawner.ShowMessageAtWorld(popupAnchor.position + popupWorldOffset, "DEBUG +1 ALL SKILLS", Color.yellow);

        Debug.Log("[DebugGiveItems] F2: +1 level on all tracked skills (direct debug grant).");
    }

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
}
