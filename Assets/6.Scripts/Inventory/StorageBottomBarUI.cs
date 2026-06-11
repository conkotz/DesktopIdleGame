using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Bottom bar for the storage window: "Store all" into <see cref="PlayerStorage"/>, used/total slot label, and the
/// total combined value of all stored items. Also drives the StoreAll button's visibility — it lives in the
/// inventory window now but should only be shown while the storage window (this component) is open.
/// </summary>
public class StorageBottomBarUI : MonoBehaviour
{
    [SerializeField] private Button storeAllButton;
    [Tooltip("If empty, a sibling named StorageSpaceText under the storage window is used (same pattern as StorageTotalValue).")]
    [SerializeField] private TMP_Text spaceText;
    [Tooltip("Suffix before used/total counts. Tab name is prefixed automatically, e.g. \"Main Space: 12/88\".")]
    [FormerlySerializedAs("spaceCountPrefix")]
    [SerializeField] private string spaceCountSuffix = "Space:";
    [Tooltip(
        "Optional. Renders the combined value of all items in storage. If empty, a sibling named StorageTotalValue " +
        "under the same storage window (parent of BottomBarOfStorage) is used.")]
    [SerializeField] private TMP_Text totalValueText;
    [SerializeField] private PlayerStorage storage;
    [SerializeField] private StorageGridUI gridUi;

    [Tooltip("Numeric format passed to int.ToString. Default groups thousands.")]
    [SerializeField] private string totalValueNumberFormat = "N0";
    [Tooltip("Suffix appended after the formatted total (e.g. 'g' for gold).")]
    [SerializeField] private string totalValueSuffix = "g";

    private Inventory _inventory;
    private StorageTabBarUI _tabBar;
    private string _totalValuePrefix;
    private bool _labelsDirty;

    /// <summary>
    /// At scene load BottomBarOfStorage is inactive (storage window starts closed) which means our OnEnable hasn't run
    /// yet, but the StoreAll button now lives under the inventory window and would otherwise show as soon as the
    /// inventory opens. This hook proactively hides the button until storage is actually opened.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HideStoreAllAtSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedHideStoreAll;
        SceneManager.sceneLoaded += OnSceneLoadedHideStoreAll;
        ApplyHideStoreAllForCurrentScene();
    }

    private static void OnSceneLoadedHideStoreAll(Scene scene, LoadSceneMode mode) =>
        ApplyHideStoreAllForCurrentScene();

    private static void ApplyHideStoreAllForCurrentScene()
    {
        StorageBottomBarUI[] all = FindObjectsByType<StorageBottomBarUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            StorageBottomBarUI bar = all[i];
            if (bar == null || bar.storeAllButton == null)
                continue;
            // If the storage window is currently open, leave the button alone — OnEnable already showed it.
            if (bar.gameObject.activeInHierarchy)
                continue;
            GameObject btn = bar.storeAllButton.gameObject;
            if (btn.activeSelf)
                btn.SetActive(false);
        }
    }

    private void Awake()
    {
        if (!storeAllButton)
            storeAllButton = GetComponentInChildren<Button>(true);
        TryBindSiblingNamedText(ref spaceText, "StorageSpaceText");
        if (!spaceText)
            spaceText = ResolveTextByNameContains("Space") ?? GetComponentInChildren<TMP_Text>(true);
        TryBindSiblingNamedText(ref totalValueText, "StorageTotalValue");

        if (totalValueText != null)
            _totalValuePrefix = totalValueText.text ?? string.Empty;

        if (storeAllButton != null)
            storeAllButton.onClick.AddListener(OnStoreAllClicked);
    }

    private void OnEnable()
    {
        TryBindSiblingNamedText(ref spaceText, "StorageSpaceText");
        TryBindSiblingNamedText(ref totalValueText, "StorageTotalValue");
        if (totalValueText != null && string.IsNullOrEmpty(_totalValuePrefix))
            _totalValuePrefix = totalValueText.text ?? string.Empty;

        ResolveStorage();
        if (_inventory == null)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (storage != null)
            storage.OnStorageChanged += HandleStorageOrInventoryChanged;
        if (_inventory != null)
            _inventory.OnInventoryChanged += HandleStorageOrInventoryChanged;

        if (_tabBar == null)
            _tabBar = FindFirstObjectByType<StorageTabBarUI>(FindObjectsInactive.Include);
        if (_tabBar != null)
            _tabBar.OnTabSelected += HandleTabSelected;

        SetStoreAllButtonVisible(true);
        _labelsDirty = false;
        RefreshSpaceLabel();
        RefreshTotalValueLabel();
    }

    private void LateUpdate()
    {
        if (!_labelsDirty)
            return;
        _labelsDirty = false;
        RefreshSpaceLabel();
        RefreshTotalValueLabel();
    }

    private void OnDisable()
    {
        if (storage != null)
            storage.OnStorageChanged -= HandleStorageOrInventoryChanged;
        if (_inventory != null)
            _inventory.OnInventoryChanged -= HandleStorageOrInventoryChanged;
        if (_tabBar != null)
            _tabBar.OnTabSelected -= HandleTabSelected;

        SetStoreAllButtonVisible(false);
        _labelsDirty = false;
    }

    /// <summary>The StoreAll button now lives in the inventory window; mirror this component's enabled state to it.</summary>
    private void SetStoreAllButtonVisible(bool visible)
    {
        if (storeAllButton == null)
            return;
        GameObject go = storeAllButton.gameObject;
        if (go.activeSelf != visible)
            go.SetActive(visible);
    }

    private void HandleStorageOrInventoryChanged()
    {
        _labelsDirty = true;
    }

    private void HandleTabSelected(StorageTabKind _)
    {
        _labelsDirty = true;
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
        _labelsDirty = true;
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

        if (!gridUi)
            gridUi = FindFirstObjectByType<StorageGridUI>(FindObjectsInactive.Include);

        StorageTabKind tab = gridUi != null ? gridUi.ActiveTab : StorageTabKind.Main;
        int used = storage.GetTabUsedSlotCount(tab);
        int capacity = storage.GetSlotsForTab(tab);

        string suffix = string.IsNullOrWhiteSpace(spaceCountSuffix) ? "Space" : spaceCountSuffix.Trim().TrimEnd(':');
        string label = $"{StorageTabFilters.GetDisplayName(tab)} {suffix}:";
        spaceText.text = $"{label} {used}/{capacity}";
    }

    /// <summary>Renders the prefix authored on the field plus the combined item value of every stored stack.</summary>
    private void RefreshTotalValueLabel()
    {
        if (!totalValueText)
            return;

        ResolveStorage();
        if (_inventory == null)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        int total = ComputeStorageTotalValue();
        string formatted = total.ToString(string.IsNullOrEmpty(totalValueNumberFormat) ? "N0" : totalValueNumberFormat);
        string body = formatted + (totalValueSuffix ?? string.Empty);

        string prefix = _totalValuePrefix ?? string.Empty;
        bool needsSpace = prefix.Length > 0 && !prefix.EndsWith(" ") && !prefix.EndsWith("\t");
        totalValueText.text = needsSpace ? prefix + " " + body : prefix + body;
    }

    private int ComputeStorageTotalValue()
    {
        if (storage == null)
            return 0;

        int total = 0;
        int n = storage.SlotCount;
        for (int i = 0; i < n; i++)
        {
            PlayerStorage.Slot s = storage.GetSlot(i);
            if (s.IsEmpty) continue;

            int unitValue = ResolveItemUnitValue(s.itemId);
            if (unitValue <= 0) continue;

            total += unitValue * Mathf.Max(0, s.amount);
        }

        return total;
    }

    /// <summary>
    /// Looks up the ItemDefinition.value for a stored item. Tries <see cref="PlayerStorage.GetItemDef"/> first
    /// (uses the storage's own ItemDatabase reference), then falls back to the player <see cref="Inventory"/> so the
    /// total never reads as zero just because one path's database reference happened to be null at the moment.
    /// </summary>
    private int ResolveItemUnitValue(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        if (storage != null)
        {
            ItemDefinition def = storage.GetItemDef(itemId);
            if (def != null)
                return Mathf.Max(0, def.value);
        }

        if (_inventory != null)
            return _inventory.GetItemValue(itemId);

        return 0;
    }

    private TMP_Text ResolveTextByNameContains(string namePart)
    {
        if (string.IsNullOrWhiteSpace(namePart))
            return null;

        TMP_Text[] all = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < all.Length; i++)
        {
            TMP_Text t = all[i];
            if (t == null) continue;
            if (t.gameObject.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }
        return null;
    }

    /// <summary>
    /// Resolves <c>StorageSpaceText</c> / <c>StorageTotalValue</c> when they are siblings of <c>BottomBarOfStorage</c>, not descendants of this bar.
    /// </summary>
    private void TryBindSiblingNamedText(ref TMP_Text field, string objectName)
    {
        if (field != null)
            return;
        if (string.IsNullOrWhiteSpace(objectName))
            return;

        for (Transform p = transform.parent; p != null; p = p.parent)
        {
            for (int i = 0; i < p.childCount; i++)
            {
                Transform ch = p.GetChild(i);
                if (ch == null || ch == transform || transform.IsChildOf(ch))
                    continue;

                Transform hit = FindDeepNamedChild(ch, objectName);
                if (hit == null)
                    continue;

                field = hit.GetComponent<TMP_Text>() ?? hit.GetComponentInChildren<TMP_Text>(true);
                if (field != null)
                    return;
            }
        }
    }

    private static Transform FindDeepNamedChild(Transform root, string wantedName)
    {
        if (root == null || string.IsNullOrEmpty(wantedName))
            return null;
        if (string.Equals(root.name, wantedName, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepNamedChild(root.GetChild(i), wantedName);
            if (found != null)
                return found;
        }

        return null;
    }
}
