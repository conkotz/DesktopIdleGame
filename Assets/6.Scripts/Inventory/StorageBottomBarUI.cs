using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom bar for the storage window: "Store all" into <see cref="PlayerStorage"/> and used/total slot label.
/// </summary>
public class StorageBottomBarUI : MonoBehaviour
{
    [SerializeField] private Button storeAllButton;
    [SerializeField] private TMP_Text spaceText;
    [SerializeField] private PlayerStorage storage;
    [SerializeField] private StorageGridUI gridUi;

    private Inventory _inventory;

    private void Awake()
    {
        if (!storeAllButton)
            storeAllButton = GetComponentInChildren<Button>(true);
        if (!spaceText)
            spaceText = GetComponentInChildren<TMP_Text>(true);

        if (storeAllButton != null)
            storeAllButton.onClick.AddListener(OnStoreAllClicked);
    }

    private void OnEnable()
    {
        ResolveStorage();
        if (_inventory == null)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (storage != null)
            storage.OnStorageChanged += RefreshSpaceLabel;
        if (_inventory != null)
            _inventory.OnInventoryChanged += RefreshSpaceLabel;

        RefreshSpaceLabel();
    }

    private void OnDisable()
    {
        if (storage != null)
            storage.OnStorageChanged -= RefreshSpaceLabel;
        if (_inventory != null)
            _inventory.OnInventoryChanged -= RefreshSpaceLabel;
    }

    private void ResolveStorage()
    {
        if (!gridUi)
            gridUi = FindFirstObjectByType<StorageGridUI>(FindObjectsInactive.Include);
        if (gridUi != null && gridUi.PlayerStorage != null)
            storage = gridUi.PlayerStorage;
        if (!storage)
            storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
    }

    private void OnStoreAllClicked()
    {
        ResolveStorage();
        if (_inventory == null)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (_inventory == null || storage == null) return;

        storage.TryDepositEntireInventory(_inventory);
        RefreshSpaceLabel();
    }

    private void RefreshSpaceLabel()
    {
        if (!spaceText) return;
        ResolveStorage();
        if (storage == null)
        {
            spaceText.text = "";
            return;
        }

        int used = 0;
        int n = storage.SlotCount;
        for (int i = 0; i < n; i++)
        {
            if (!storage.GetSlot(i).IsEmpty) used++;
        }

        spaceText.text = $"{used}/{n}";
    }
}
