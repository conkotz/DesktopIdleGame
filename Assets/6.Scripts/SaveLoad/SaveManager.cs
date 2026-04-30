using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField] private bool autosave = true;
    [SerializeField] private float autosaveIntervalSeconds = 30f;

    private int GetSafeActiveSlot()
    {
        int slot = SaveSlotManager.ActiveSlotIndex;
        if (slot < 0)
        {
            Debug.LogWarning("[SaveManager] ActiveSlotIndex < 0. Defaulting to slot 0.");
            slot = 0;
        }

        return slot;
    }

    private string ActiveSavePath => SaveSlotManager.GetSavePath(GetSafeActiveSlot());
    private float _autosaveTimer;
    private float _shopStockSaveDueUnscaled = -1f;
    private float _stripZoomSaveDueUnscaled = -1f;
    private const float ShopStockSaveDebounceSeconds = 0.12f;

    private bool _didInitialLoadOrCreate;

    private Inventory _inventory;
    private PlayerStorage _playerStorage;

    private float _lastInventoryImmediateSave;
    private float _lastStorageImmediateSave;
    private const float MinSaveGap = 1f;

    private SaveData _lastLoadedData;
    private bool _isApplyingSaveData;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        // Must target a scene root; SaveManager may live under a child (e.g. _GameSystems on Bootstrap).
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryBindInventory();
        TryBindPlayerStorage();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindInventory();
        UnbindPlayerStorage();
    }

    // ✅ Key change: do NOT Load/Save in Start() anymore (Bootstrap has no saveables yet)
    private void Start()
    {
        // Intentionally empty.
        // Initial load/create happens in OnSceneLoaded when gameplay scene is present.
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Rebind inventory for the new scene (Inventory likely lives in scene)
        TryBindInventory();
        TryBindPlayerStorage();

        // Merchants reset runtime stock in Awake() from ScriptableObject defaults.
        // After the first session init, reload merchant quantities from disk when entering any gameplay scene.
        if (_didInitialLoadOrCreate &&
            !scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            RehydrateMerchantStocksFromSave();
            ScheduleMerchantRehydrateFrames(2);
        }

        // Only initialize once per app run.
        if (_didInitialLoadOrCreate) return;

        // Bootstrap is intentionally "save-less". We initialize when entering gameplay.
        if (scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
            return;

        int slot = GetSafeActiveSlot();
        var pendingMode = SaveSlotManager.ConsumePendingStartMode();

        // Safe fallback if something loads gameplay without going through the Bootstrap UI buttons.
        if (pendingMode == SaveSlotManager.SlotStartMode.None)
            pendingMode = HasSave() ? SaveSlotManager.SlotStartMode.LoadGame : SaveSlotManager.SlotStartMode.NewGame;

        HelperProgressStore.ResetHydrationForNewSession();

        _didInitialLoadOrCreate = true;

        if (pendingMode == SaveSlotManager.SlotStartMode.NewGame)
        {
            ResetAllSaveablesToDefaults();
            ApplyPendingNewGamePlayerName();
            Save();
        }
        else // LoadGame
        {
            if (HasSave())
            {
                Load();
            }
            else
            {
                Debug.LogWarning($"[SaveManager] Continue/Load requested but no save exists for slot {slot}. Starting a fresh game instead.");
                ResetAllSaveablesToDefaults();
                Save();
            }
        }

        HelperProgressStore.ApplyFromSaveData(_lastLoadedData);

        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
            Debug.LogWarning("[SaveManager] No PlayerController found after gameplay init. If you start from Bootstrap, ensure a player exists in the gameplay scene or is spawned by a bootstrapper.");
    }

    private void ResetAllSaveablesToDefaults()
    {
        var data = new SaveData
        {
            version = 4,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _lastLoadedData = data;

        _isApplyingSaveData = true;
        try
        {
            ApplyRuntimeEnhancedItemsToDatabase(data);

            var saveables = FindSaveables();
            foreach (var s in saveables)
                s.LoadFrom(data);

            EnsurePlayerStorageLoadedFromData(data);
            SeedActiveLevelFromWorldMapIfNeeded(data);
        }
        finally
        {
            _isApplyingSaveData = false;
        }
    }

    private static void SeedActiveLevelFromWorldMapIfNeeded(SaveData data)
    {
        if (data != null && !string.IsNullOrWhiteSpace(data.activeMapNodeId))
            return;

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (wmp == null || wmp.WorldMap == null)
            return;

        string startId = wmp.WorldMap.startingNodeId;
        if (string.IsNullOrWhiteSpace(startId))
            return;

        MapNodeDefinition node = wmp.WorldMap.FindNodeById(startId.Trim());
        if (node != null)
            ActiveLevelContext.SetPendingLevel(node, logToConsole: false);
    }

    private static void EnsurePlayerStorageInSaveData(SaveData data)
    {
        if (data == null) return;

        var ps = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (ps != null)
            ps.SaveInto(data);
    }

    private static void EnsurePlayerStorageLoadedFromData(SaveData data)
    {
        if (data == null) return;

        var ps = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (ps != null)
            ps.LoadFrom(data);
    }

    private static void ApplyRuntimeEnhancedItemsToDatabase(SaveData data)
    {
        ItemDatabase db = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        if (db != null)
            db.LoadRuntimeEnhancedItemsFrom(data);
    }

    private void ApplyPendingNewGamePlayerName()
    {
        string pendingName = SaveSlotManager.ConsumePendingNewGamePlayerName();
        if (string.IsNullOrWhiteSpace(pendingName))
            return;

        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
            return;

        player.SetDisplayName(pendingName.Trim());
    }

    private void Update()
    {
        if (!_didInitialLoadOrCreate) return;   // ✅ ADD THIS

        if (_shopStockSaveDueUnscaled >= 0f && Time.unscaledTime >= _shopStockSaveDueUnscaled)
        {
            _shopStockSaveDueUnscaled = -1f;
            if (!_isApplyingSaveData)
                Save();
        }

        if (_stripZoomSaveDueUnscaled >= 0f && Time.unscaledTime >= _stripZoomSaveDueUnscaled)
        {
            _stripZoomSaveDueUnscaled = -1f;
            if (!_isApplyingSaveData)
                Save();
        }

        if (!autosave) return;

        _autosaveTimer += Time.unscaledDeltaTime;
        if (_autosaveTimer >= autosaveIntervalSeconds)
        {
            _autosaveTimer = 0f;
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        if (!_didInitialLoadOrCreate) return;   // ✅ ADD THIS
        Save();
    }

    public bool HasSave() => File.Exists(ActiveSavePath);

    public void Save()
    {
        if (_isApplyingSaveData) return;

        var data = new SaveData
        {
            version = 4,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var saveables = FindSaveables();
        foreach (var s in saveables)
            s.SaveInto(data);

        EnsurePlayerStorageInSaveData(data);

        HelperProgressStore.WriteDismissedInto(data);

        ApplyActiveMapToSaveData(data);

        _lastLoadedData = data;

        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(ActiveSavePath, json);

        // Write a small meta/header file for the slot select UI.
        int slot = GetSafeActiveSlot();
        int combatPower = 0;
        PlayerController playerForHeader = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        CharacterStats playerStats = playerForHeader ? playerForHeader.GetComponent<CharacterStats>() : null;
        if (playerStats != null)
            combatPower = playerStats.CombatPowerRounded;

        var header = SaveSlotManager.BuildHeaderFromSaveData(
            slot,
            data,
            SceneManager.GetActiveScene().name,
            DateTime.UtcNow,
            combatPower
        );
        SaveSlotManager.WriteHeader(header);
    }

    /// <summary>
    /// Call after strip ortho / layout changes so <see cref="SaveData.stripCameraZoomMultiplier"/> catches up before scene reloads (debounced).
    /// </summary>
    public void NotifyStripZoomChangedDebounced()
    {
        if (!_didInitialLoadOrCreate || _isApplyingSaveData)
            return;

        _stripZoomSaveDueUnscaled = Time.unscaledTime + ShopStockSaveDebounceSeconds;
    }

    /// <summary>
    /// Call after shop stock changes without an inventory delta (debounced full save).
    /// </summary>
    public void NotifyShopStockChanged()
    {
        if (!_didInitialLoadOrCreate || _isApplyingSaveData)
            return;

        _shopStockSaveDueUnscaled = Time.unscaledTime + ShopStockSaveDebounceSeconds;
    }

    /// <summary>
    /// Reload merchant runtime quantities from disk or last loaded data. Safe after NPCs spawn mid-frame.
    /// </summary>
    public void RehydrateMerchantStocksFromSave() => RehydrateMerchantStocksFromSaveCore();

    /// <summary>
    /// Waits <paramref name="frames"/> frames then rehydrates (catches merchants spawned after scene load).
    /// </summary>
    public void ScheduleMerchantRehydrateFrames(int frames)
    {
        if (frames <= 0)
        {
            RehydrateMerchantStocksFromSaveCore();
            return;
        }

        StartCoroutine(CoRehydrateMerchantsAfterFrames(frames));
    }

    private IEnumerator CoRehydrateMerchantsAfterFrames(int frames)
    {
        for (int i = 0; i < frames; i++)
            yield return null;

        RehydrateMerchantStocksFromSaveCore();
    }

    private static void ApplyActiveMapToSaveData(SaveData data)
    {
        if (data == null)
            return;

        MapNodeDefinition def = null;
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        else if (ActiveLevelContext.Current != null)
            def = ActiveLevelContext.Current;

        if (def == null)
            return;

        data.activeMapNodeId = def.nodeId ?? "";
        data.activeMapDisplayName = def.displayName ?? "";
    }

    private static void RestoreActiveMapFromSaveData(SaveData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.activeMapNodeId))
            return;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance;
        if (progress == null || progress.WorldMap == null)
            return;

        MapNodeDefinition node = progress.WorldMap.FindNodeById(data.activeMapNodeId.Trim());
        if (node == null)
            return;

        ActiveLevelContext.SetPendingLevel(node, logToConsole: false);
    }

    public void Load()
    {
        if (!HasSave()) return;

        var json = File.ReadAllText(ActiveSavePath);
        var data = JsonUtility.FromJson<SaveData>(json);

        NormalizeSaveDataLists(data);

        _lastLoadedData = data;

        _isApplyingSaveData = true;
        try
        {
            ApplyRuntimeEnhancedItemsToDatabase(data);

            var saveables = FindSaveables();
            foreach (var s in saveables)
                s.LoadFrom(data);

            EnsurePlayerStorageLoadedFromData(data);

            RestoreActiveMapFromSaveData(data);
        }
        finally
        {
            _isApplyingSaveData = false;
        }

        // Player / ItemDatabase can be a frame behind scene setup; re-apply chest so load never misses.
        StartCoroutine(DeferredApplyPlayerStorageLoad());
    }

    private static void NormalizeSaveDataLists(SaveData data)
    {
        if (data == null) return;

        if (data.inventorySlots == null)
            data.inventorySlots = new List<SaveData.InventorySlotData>();
        if (data.enhancedItems == null)
            data.enhancedItems = new List<SaveData.EnhancedItemData>();
        if (data.storageSlots == null)
            data.storageSlots = new List<SaveData.InventorySlotData>();
        if (data.questRewardClaimedIds == null)
            data.questRewardClaimedIds = new List<string>();
        if (data.worldMapUnlockedNodeIds == null)
            data.worldMapUnlockedNodeIds = new List<string>();
        if (data.worldMapCompletedNodeIds == null)
            data.worldMapCompletedNodeIds = new List<string>();
        if (data.worldMapEnteredNodeIds == null)
            data.worldMapEnteredNodeIds = new List<string>();
        if (data.merchantStocks == null)
            data.merchantStocks = new List<SaveData.MerchantStockSave>();
        if (data.dismissedHelperIds == null)
            data.dismissedHelperIds = new List<string>();
    }

    /// <summary>
    /// After scene reload, <see cref="Merchant"/> Awake resets stock from assets. Rehydrate from the active save file.
    /// </summary>
    private void RehydrateMerchantStocksFromSaveCore()
    {
        if (_isApplyingSaveData)
            return;

        SaveData data = null;
        if (HasSave())
        {
            try
            {
                string json = File.ReadAllText(ActiveSavePath);
                data = JsonUtility.FromJson<SaveData>(json);
                NormalizeSaveDataLists(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Could not read save for merchant stock: {ex.Message}");
            }
        }

        if (data == null || data.merchantStocks == null || data.merchantStocks.Count == 0)
            data = _lastLoadedData;

        if (data == null || data.merchantStocks == null || data.merchantStocks.Count == 0)
            return;

        var merchants = FindObjectsByType<Merchant>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (merchants == null || merchants.Length == 0)
            return;

        _isApplyingSaveData = true;
        try
        {
            for (int i = 0; i < merchants.Length; i++)
            {
                if (merchants[i] != null)
                    merchants[i].LoadFrom(data);
            }
        }
        finally
        {
            _isApplyingSaveData = false;
        }
    }

    private IEnumerator DeferredApplyPlayerStorageLoad()
    {
        yield return null;
        if (_lastLoadedData == null) yield break;

        var ps = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (ps == null) yield break;

        _isApplyingSaveData = true;
        try
        {
            ps.LoadFrom(_lastLoadedData);
        }
        finally
        {
            _isApplyingSaveData = false;
        }
    }

    private void HandleInventoryChanged()
    {
        if (!_didInitialLoadOrCreate) return;   // ✅ ADD THIS
        if (_isApplyingSaveData) return;

        if (Time.unscaledTime - _lastInventoryImmediateSave < MinSaveGap)
            return;

        _lastInventoryImmediateSave = Time.unscaledTime;
        Save();
    }

    private void TryBindInventory()
    {
        // Find inventory even if inactive (Unity 6 API)
        var inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        // If it's the same one already bound, do nothing
        if (inv == _inventory) return;

        // Otherwise, rebind
        UnbindInventory();
        _inventory = inv;

        if (_inventory != null)
            _inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    private void UnbindInventory()
    {
        if (_inventory != null)
            _inventory.OnInventoryChanged -= HandleInventoryChanged;

        _inventory = null;
    }

    private void TryBindPlayerStorage()
    {
        var st = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (st == _playerStorage) return;

        UnbindPlayerStorage();
        _playerStorage = st;

        if (_playerStorage != null)
            _playerStorage.OnStorageChanged += HandleStorageChanged;
    }

    private void UnbindPlayerStorage()
    {
        if (_playerStorage != null)
            _playerStorage.OnStorageChanged -= HandleStorageChanged;

        _playerStorage = null;
    }

    private void HandleStorageChanged()
    {
        if (!_didInitialLoadOrCreate) return;
        if (_isApplyingSaveData) return;

        // Separate debounce from inventory so a recent inv save cannot block persisting storage.
        if (Time.unscaledTime - _lastStorageImmediateSave < MinSaveGap)
            return;

        _lastStorageImmediateSave = Time.unscaledTime;
        Save();
    }

    private ISaveable[] FindSaveables()
    {
        var behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        return behaviours.OfType<ISaveable>().ToArray();
    }

    public void DeleteSave()
    {
        if (!HasSave()) return;
        File.Delete(ActiveSavePath);
        _didInitialLoadOrCreate = false; // allow re-init on next scene load
    }
    public bool TryGetLastLoadedData(out SaveData data)
    {
        data = _lastLoadedData;
        return data != null;
    }
}