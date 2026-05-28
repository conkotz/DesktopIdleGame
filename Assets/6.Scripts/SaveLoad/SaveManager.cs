using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kirurobo;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SaveManager : MonoBehaviour
{
    public enum SaveRequestKind
    {
        Unknown = 0,
        AutosaveInterval = 1,
        DebouncedStripZoom = 2,
        InventoryChanged = 3,
        StorageChanged = 4,
        ShopStockChanged = 5,
        SceneTransition = 6,
        ReturnToBootstrap = 7,
        AppQuit = 8,
        Manual = 9,
        NewGameInit = 10,
        LoadFallbackRecovery = 11,
        DeathDialogueRecovery = 12
    }

    public static SaveManager Instance { get; private set; }
    public event Action OnSaveSystemReady;

    [SerializeField] private bool autosave = true;
    [SerializeField] private float autosaveIntervalSeconds = 30f;

    [Header("Debug")]
    [Tooltip("Logs non-critical save flow messages (staged load, ApplyToPlayer summary, slot metadata, force-apply). Warnings for real problems stay on.")]
    [SerializeField] private bool verboseInfoLogs;

#if UNITY_EDITOR
    private static bool _editorIsExitingPlayMode;

    [InitializeOnLoadMethod]
    private static void RegisterEditorPlayModeHooks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            _editorIsExitingPlayMode = state == PlayModeStateChange.ExitingPlayMode;
        };
    }
#endif

    private int GetSafeActiveSlot()
    {
        int slot = SaveSlotManager.ActiveSlotIndex;
        if (slot < 0)
        {
            slot = 0;
        }

        return slot;
    }

    private string ActiveSavePath => SaveSlotManager.GetSavePath(GetSafeActiveSlot());
    private string ActiveSaveBackupPath => ActiveSavePath + ".bak";
    private float _autosaveTimer;
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
    private SaveSlotManager.SlotStartMode _lastStartMode = SaveSlotManager.SlotStartMode.None;
    private bool _hasPendingLoad;
    private bool _didFinalApplyForCurrentLoad;
    private Coroutine _forceApplyAfterSceneLoadRoutine;

    /// <summary>True after gameplay scene stabilizes (player + inventory present). Debounced saves wait for this.</summary>
    public bool IsGameFullyLoaded { get; private set; }

    private Coroutine _gameplayReadyRoutine;
    private float _autosaveHoldUntilUnscaled = -1f;
    private bool _saveRequestPending;
    private SaveRequestKind _pendingSaveKind = SaveRequestKind.Unknown;
    private string InstanceLogTag => $"id={GetInstanceID()} scene='{gameObject.scene.name}'";
    private readonly bool[] _slotHasSaveCache = new bool[SaveSlotManager.MaxSlots];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyImmediate(gameObject);
            return;
        }
        Instance = this;
        // Must target a scene root; SaveManager may live under a child (e.g. _GameSystems on Bootstrap).
        DontDestroyOnLoad(transform.root.gameObject);
        LoadAllSaveMetadata();
        FireSaveSystemReady("Awake");
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
        bool nonBootstrap = scene.IsValid() && scene.isLoaded &&
                            !scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase);
        if (nonBootstrap)
        {
            IsGameFullyLoaded = false;
            _autosaveHoldUntilUnscaled = Time.unscaledTime + 0.85f;
            if (_gameplayReadyRoutine != null)
            {
                StopCoroutine(_gameplayReadyRoutine);
                _gameplayReadyRoutine = null;
            }

            _gameplayReadyRoutine = StartCoroutine(CoMarkGameplayReadyWhenStable());

            if (_forceApplyAfterSceneLoadRoutine != null)
            {
                StopCoroutine(_forceApplyAfterSceneLoadRoutine);
                _forceApplyAfterSceneLoadRoutine = null;
            }
            _forceApplyAfterSceneLoadRoutine = StartCoroutine(CoForceApplyPendingLoadAfterSceneEntry());
        }

        // Rebind inventory for the new scene (Inventory likely lives in scene)
        TryBindInventory();
        TryBindPlayerStorage();

        if (scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            HelperGameplayController.ForceHidePersistentOverlayForMenuNavigation();
            RepairBootstrapUiAfterReturningFromGameplay();
            SaveSlotManager.ResetGameplaySpawnSessionFlags();
            RefreshSaveSlots();
            StartCoroutine(CoRefreshSaveSlotMenusAfterBootstrapLoad());
        }

        if (IsGameplaySceneForHudWiring(scene))
            StartCoroutine(CoRewireReturnToLoginButtonsAfterGameplayScene());

        // Bootstrap is intentionally "save-less". We initialize when entering gameplay.
        if (scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
            return;

        int slot = GetSafeActiveSlot();
        SaveSlotManager.SlotStartMode pendingMode = SaveSlotManager.ConsumePendingStartMode();
        bool hasExplicitStartRequest = pendingMode != SaveSlotManager.SlotStartMode.None;
        _lastStartMode = pendingMode;

        // Merchants reset runtime stock in Awake() from ScriptableObject defaults.
        // After the first session init, reload merchant quantities from disk when entering gameplay scene.
        if (_didInitialLoadOrCreate)
        {
            RehydrateMerchantStocksFromSave();
            ScheduleMerchantRehydrateFrames(2);
            RehydrateNpcDialogueStoresFromDiskPreferFile();

            // Critical: if bootstrap explicitly requested resume/new game, do not skip init.
            // Previously this early-return swallowed pending start intent and load/apply never ran.
            if (!hasExplicitStartRequest)
            {
                // MapTravelSession sets disposition before LoadScene; do not overwrite RestoreMapExit / RestoreSaved.
                SaveSlotManager.GameplaySpawnDisposition pendingSpawn =
                    SaveSlotManager.PeekPendingGameplaySpawnDisposition();
                bool preserveTravelSpawn =
                    pendingSpawn == SaveSlotManager.GameplaySpawnDisposition.RestoreMapExitPositionIfAvailable ||
                    pendingSpawn == SaveSlotManager.GameplaySpawnDisposition.RestoreSavedWorldPositionIfAvailable ||
                    pendingSpawn == SaveSlotManager.GameplaySpawnDisposition.RestoreLinkedPortalSpawnIfAvailable;

                if (!preserveTravelSpawn)
                {
                    SaveSlotManager.SetPendingGameplaySpawnDisposition(
                        SaveSlotManager.GameplaySpawnDisposition.DefaultSpawnPoint);
                    SaveSlotManager.MarkSkipApplySavedWorldPositionFromSaveOnce();
                }

                return;
            }
        }

        // Safe fallback if something loads gameplay without going through the Bootstrap UI buttons.
        if (pendingMode == SaveSlotManager.SlotStartMode.None)
            pendingMode = HasSave() ? SaveSlotManager.SlotStartMode.LoadGame : SaveSlotManager.SlotStartMode.NewGame;

        HelperProgressStore.ResetHydrationForNewSession();

        bool firstGameplayInitThisSession = !_didInitialLoadOrCreate;
        _didInitialLoadOrCreate = true;

        // Requirement: when entering gameplay from login/new session, start at Default Zoom (100%) even if save had another zoom.
        // Do NOT re-apply this on subsequent level transitions; those should keep current player zoom.
        if (firstGameplayInitThisSession)
            StripCameraController.IgnoreSavedZoomOnceOnNextGameplayLoad();

        if (pendingMode == SaveSlotManager.SlotStartMode.NewGame)
        {
            SaveSlotManager.SetPendingGameplaySpawnDisposition(SaveSlotManager.GameplaySpawnDisposition.DefaultSpawnPoint);
            _hasPendingLoad = false;
            _didFinalApplyForCurrentLoad = true;
            ResetAllSaveablesToDefaults();
            ApplyPendingNewGamePlayerName();
            RequestSave(SaveRequestKind.NewGameInit, immediate: true);
            HelperGameplayController.ResetHelperWindowLayoutForNewGame();
        }
        else // LoadGame
        {
            if (HasSave())
            {
                Load();
                RestoreActiveMapFromSaveData(_lastLoadedData);
                SaveSlotManager.SetPendingGameplaySpawnDisposition(
                    SaveSlotManager.GameplaySpawnDisposition.RestoreSavedWorldPositionIfAvailable);
            }
            else
            {
                SaveSlotManager.SetPendingGameplaySpawnDisposition(SaveSlotManager.GameplaySpawnDisposition.DefaultSpawnPoint);
                _hasPendingLoad = false;
                _didFinalApplyForCurrentLoad = true;
                ResetAllSaveablesToDefaults();
                RequestSave(SaveRequestKind.LoadFallbackRecovery, immediate: true);
            }
        }

        HelperProgressStore.ApplyFromSaveData(_lastLoadedData);
        LevelItemPickupSaveStore.ApplyFromSaveData(_lastLoadedData);
        PermanentEnemyDeathSaveStore.ApplyFromSaveData(_lastLoadedData);
        NpcPostDeathRespawnDialogueStore.ApplyFromSaveData(_lastLoadedData);
        NpcOneWayDialogueQueueStore.ApplyFromSaveData(_lastLoadedData);
        UIWindowLockStore.ApplyFromSaveData(_lastLoadedData);

        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        _ = player;
    }

    private IEnumerator CoMarkGameplayReadyWhenStable()
    {
        yield return null;
        yield return null;

        const float timeout = 5f;
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < timeout)
        {
            if (FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null &&
                FindFirstObjectByType<Inventory>(FindObjectsInactive.Include) != null)
                break;
            yield return null;
        }

        IsGameFullyLoaded = true;
        _gameplayReadyRoutine = null;

        OnGameplayReady();
    }

    /// <summary>
    /// Secondary deterministic safety apply. Resume can enter gameplay while some saveables still initialize;
    /// retry a few times so staged payload always lands even if first-ready timing drifts.
    /// </summary>
    private IEnumerator CoForceApplyPendingLoadAfterSceneEntry()
    {
        // Let scene Awake/Start and first layout/input/system ticks settle.
        yield return null;
        yield return null;

        const int maxAttempts = 180;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (!_hasPendingLoad || _didFinalApplyForCurrentLoad)
                break;

            PlayerBootstrapper.EnsurePlayerExists("CoForceApplyPendingLoadAfterSceneEntry");

            if (ApplyToPlayer(null))
            {
                StartCoroutine(DeferredApplyPlayerStorageLoad());
                _hasPendingLoad = false;
                _didFinalApplyForCurrentLoad = true;
                if (verboseInfoLogs)
                    Debug.Log($"[SaveManager] Force-apply succeeded on attempt {i + 1}.");
                break;
            }

            yield return null;
        }

        if (_hasPendingLoad && !_didFinalApplyForCurrentLoad)
            Debug.LogWarning("[SaveManager] Force-apply exhausted attempts; pending load still not applied.");

        _forceApplyAfterSceneLoadRoutine = null;
    }

    /// <summary>
    /// <see cref="SaveSlotMenuUI"/> is commonly on the same DontDestroyOnLoad root as SaveManager, so returning to
    /// Bootstrap does not re-fire <see cref="SaveSlotMenuUI.OnEnable"/>. Refresh after a frame so TMP/layout and disk headers exist.
    /// </summary>
    private static bool IsGameplaySceneForHudWiring(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;
        if (scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
            return false;
        return scene.name.Equals("GamePlay", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerator CoRewireReturnToLoginButtonsAfterGameplayScene()
    {
        yield return null;
        yield return null;
        ReturnToLoginMenuButton.RewireAll();
    }

    private IEnumerator CoRefreshSaveSlotMenusAfterBootstrapLoad()
    {
        const int maxWaitFrames = 30;
        int waited = 0;
        while (!SaveSlotMenuUI.TryGetLoadedBootstrapScene(out _) && waited < maxWaitFrames)
        {
            yield return null;
            waited++;
        }

        yield return null;

        SaveSlotMenuUI[] menus = FindObjectsByType<SaveSlotMenuUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool haveDdolMenu = false;
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] && menus[i].gameObject.scene.name == "DontDestroyOnLoad")
            {
                haveDdolMenu = true;
                break;
            }
        }

        for (int i = 0; i < menus.Length; i++)
        {
            if (!menus[i])
                continue;
            // Normal flow: menu is on DDOL with SaveManager. Editor-only bootstrap-only play has no DDOL menu — refresh all.
            if (haveDdolMenu && menus[i].gameObject.scene.name != "DontDestroyOnLoad")
                continue;

            menus[i].RefreshSlotsFromDisk();
        }

        yield return null;
        RepairBootstrapUiAfterReturningFromGameplay();
    }

    private void ResetAllSaveablesToDefaults()
    {
        var data = new SaveData
        {
            version = 4,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            // Class default is -1 ("omit"); 0 avoids a bogus integrity warning before PlayerSave applies real HP.
            playerCurrentHP = 0f,
        };

        _lastLoadedData = data;

        NormalizeSaveDataLists(data);
        AlignNewGameTemplateSlotCountsFromRuntime(data);
        SeedEmptyInventoryAndStorageRowsForNewGame(data);
        CombatStarterAttackAbility.SeedDefaultActionBarAssignments(data);
        SaveDataIntegrity.RepairAfterJsonLoad(data, "NewGameTemplate");

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

        HelperProgressStore.ApplyFromSaveData(data);
        LevelItemPickupSaveStore.ApplyFromSaveData(data);
        PermanentEnemyDeathSaveStore.ApplyFromSaveData(data);
        NpcPostDeathRespawnDialogueStore.ApplyFromSaveData(data);
        NpcOneWayDialogueQueueStore.ApplyFromSaveData(data);
        UIWindowLockStore.ApplyFromSaveData(data);
    }

    /// <summary>
    /// <see cref="SaveData"/> still carries legacy default slot counts (32/28) for JsonUtility; new game should match
    /// the live <see cref="Inventory"/> / <see cref="PlayerStorage"/> grid so integrity repair does not log noise.
    /// </summary>
    private static void AlignNewGameTemplateSlotCountsFromRuntime(SaveData data)
    {
        if (data == null)
            return;

        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inv != null && inv.SlotCount > 0)
            data.inventorySlotCount = inv.SlotCount;

        PlayerStorage ps = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (ps != null && ps.SlotCount > 0)
            data.storageSlotCount = ps.SlotCount;
    }

    private static void SeedEmptyInventoryAndStorageRowsForNewGame(SaveData data)
    {
        if (data == null)
            return;

        data.inventorySlots ??= new List<SaveData.InventorySlotData>();
        data.storageSlots ??= new List<SaveData.InventorySlotData>();

        int invWant = Mathf.Clamp(data.inventorySlotCount > 0 ? data.inventorySlotCount : 1, 1, 512);
        int stWant = Mathf.Clamp(data.storageSlotCount > 0 ? data.storageSlotCount : 1, 1, 512);

        data.inventorySlots.Clear();
        for (int i = 0; i < invWant; i++)
            data.inventorySlots.Add(default);

        data.storageSlots.Clear();
        for (int i = 0; i < stWant; i++)
            data.storageSlots.Add(default);
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

    private static void EnsureInventoryInSaveData(SaveData data)
    {
        if (data == null)
            return;

        Inventory inv = PickCanonicalInventoryForSave();
        if (inv != null)
            inv.SaveInto(data);
    }

    private static void EnsurePlayerStorageInSaveData(SaveData data)
    {
        if (data == null) return;

        PlayerStorage ps = PickCanonicalPlayerStorageForSave();
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
        if (_stripZoomSaveDueUnscaled >= 0f && Time.unscaledTime >= _stripZoomSaveDueUnscaled)
        {
            _stripZoomSaveDueUnscaled = -1f;
            RequestSave(SaveRequestKind.DebouncedStripZoom);
        }

        if (!autosave) return;

        _autosaveTimer += Time.unscaledDeltaTime;
        if (_autosaveTimer >= autosaveIntervalSeconds)
        {
            _autosaveTimer = 0f;
            RequestSave(SaveRequestKind.AutosaveInterval);
        }

        if (_saveRequestPending)
        {
            SaveRequestKind kind = _pendingSaveKind;
            _saveRequestPending = false;
            _pendingSaveKind = SaveRequestKind.Unknown;
            ExecuteSave(kind);
        }
    }

    private void OnApplicationQuit()
    {
#if UNITY_EDITOR
        // Stopping Play Mode fires OnApplicationQuit and would overwrite persistent slot JSON.
        if (_editorIsExitingPlayMode)
            return;
#endif
        RequestSave(SaveRequestKind.AppQuit, immediate: true);
    }

    public bool HasSave() => File.Exists(ActiveSavePath);

    /// <summary>UI-facing slot existence query from last metadata refresh.</summary>
    public bool SaveExists(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotHasSaveCache.Length)
            return false;
        return _slotHasSaveCache[slotIndex];
    }

    /// <summary>Reload slot metadata from disk and notify listeners (Bootstrap UI).</summary>
    public void RefreshSaveSlots()
    {
        LoadAllSaveMetadata();
        FireSaveSystemReady("RefreshSaveSlots");
    }

    private void LoadAllSaveMetadata()
    {
        for (int i = 0; i < SaveSlotManager.MaxSlots; i++)
            _slotHasSaveCache[i] = SaveSlotManager.HasSave(i);
        if (verboseInfoLogs)
            Debug.Log($"[SaveManager] Loaded slot metadata: slot0={_slotHasSaveCache[0]}, slot1={_slotHasSaveCache[1]}");
    }

    private void FireSaveSystemReady(string source)
    {
        if (verboseInfoLogs)
            Debug.Log($"[SaveManager] OnSaveSystemReady fired ({source}).");
        OnSaveSystemReady?.Invoke();
    }

    /// <summary>Active map node id from the last in-memory save payload (fallback when gameplay context is missing).</summary>
    public string GetLastWrittenActiveMapNodeId()
    {
        if (_lastLoadedData == null || string.IsNullOrWhiteSpace(_lastLoadedData.activeMapNodeId))
            return "";
        return _lastLoadedData.activeMapNodeId.Trim();
    }

    /// <summary>
    /// Merges only <see cref="NpcPostDeathRespawnDialogueStore"/> fields into the active save file.
    /// Use when <see cref="Save"/> was skipped (<see cref="_isApplyingSaveData"/>) so death/respawn dialogue flags are not lost.
    /// </summary>
    public void FlushNpcPostDeathDialogueToDisk()
    {
        if (!HasSave())
            return;
#if UNITY_EDITOR
        if (_editorIsExitingPlayMode)
            return;
#endif

        try
        {
            string json = File.ReadAllText(ActiveSavePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
                return;

            NormalizeSaveDataLists(data);
            SaveDataIntegrity.RepairAfterJsonLoad(data, "FlushNpcPostDeath");
            NpcPostDeathRespawnDialogueStore.WriteInto(data);

            if (_lastLoadedData != null)
            {
                _lastLoadedData.npcPostDeathRespawnDialoguePending = data.npcPostDeathRespawnDialoguePending;
                _lastLoadedData.npcPostDeathRespawnDialogueDeathNodeId = data.npcPostDeathRespawnDialogueDeathNodeId ?? "";
            }

            SaveDataIntegrity.SanitizeBeforeWrite(data, "FlushNpcPostDeath");
            File.WriteAllText(ActiveSavePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception ex)
        {
            _ = ex;
        }
    }

    public void Save()
    {
        RequestSave(SaveRequestKind.Manual, immediate: true);
    }

    public void RequestSave(SaveRequestKind kind, bool immediate = false)
    {
        if (_isApplyingSaveData)
            return;

        if (immediate)
        {
            ExecuteSave(kind);
            return;
        }

        if (!_saveRequestPending)
        {
            _saveRequestPending = true;
            _pendingSaveKind = kind;
            return;
        }

        // Coalesce bursty requests by keeping the highest-priority pending reason.
        if (GetSaveRequestPriority(kind) > GetSaveRequestPriority(_pendingSaveKind))
            _pendingSaveKind = kind;
    }

    private static int GetSaveRequestPriority(SaveRequestKind kind)
    {
        return kind switch
        {
            SaveRequestKind.AppQuit => 400,
            SaveRequestKind.SceneTransition => 350,
            SaveRequestKind.ReturnToBootstrap => 340,
            SaveRequestKind.NewGameInit => 300,
            SaveRequestKind.LoadFallbackRecovery => 290,
            SaveRequestKind.Manual => 250,
            SaveRequestKind.DeathDialogueRecovery => 220,
            SaveRequestKind.ShopStockChanged => 180,
            SaveRequestKind.InventoryChanged => 140,
            SaveRequestKind.StorageChanged => 130,
            SaveRequestKind.DebouncedStripZoom => 90,
            SaveRequestKind.AutosaveInterval => 50,
            _ => 0
        };
    }

    private void ExecuteSave(SaveRequestKind kind)
    {
        if (_isApplyingSaveData) return;
#if UNITY_EDITOR
        if (_editorIsExitingPlayMode)
        {
            if (verboseInfoLogs)
                Debug.Log($"[SaveManager] Skipping disk save ({kind}) — Editor is exiting Play Mode.");
            return;
        }
#endif
        if (!IsRuntimeReadyForSave(out string readinessReason))
            return;

        SaveData previousSnapshot = _lastLoadedData;

        var data = new SaveData
        {
            version = 4,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        NormalizeSaveDataLists(data);

        // Merchants only exist in the gameplay scene. Autosave / menu / world-map saves used to build an empty
        // merchantStocks list and wipe every vendor on disk. Seed from the last snapshot, then in-scene merchants overwrite.
        SeedMerchantStocksFromSnapshot(data, _lastLoadedData);
        PlayerMapExitPositionStore.CopyFromSnapshot(data, _lastLoadedData);

        ISaveable[] saveablesRaw = FindSaveables();
        List<ISaveable> saveables = DedupeActionBarSaveables(saveablesRaw);
        saveables = DedupeInventorySaveables(saveables);
        foreach (var s in saveables)
            s.SaveInto(data);

        SeedActionBarFromSnapshot(data, _lastLoadedData);

        EnsureInventoryInSaveData(data);
        EnsurePlayerStorageInSaveData(data);

        if (data.inventorySlotCount > 0 && (data.inventorySlots == null || data.inventorySlots.Count == 0))
        {
            Debug.LogError(
                $"[SaveManager] Save ({kind}) aborted snapshot: inventory rows missing after SaveInto (count={data.inventorySlotCount}).");
        }

        HelperProgressStore.WriteDismissedInto(data);
        HelperProgressStore.WriteNewBadgeSuppressedInto(data);
        LevelItemPickupSaveStore.WriteInto(data);
        PermanentEnemyDeathSaveStore.WriteInto(data);
        NpcPostDeathRespawnDialogueStore.WriteInto(data);
        NpcOneWayDialogueQueueStore.WriteInto(data);
        UIWindowLockStore.WriteInto(data);

        if (kind == SaveRequestKind.SceneTransition || kind == SaveRequestKind.ReturnToBootstrap)
            TryRecordGameplayMapExitPosition(data);

        ApplyActiveMapToSaveData(data);

        SaveDataIntegrity.SanitizeBeforeWrite(data, "Save");
        if (!IsCriticalSnapshotValid(data, out string snapshotReason))
            return;
        if (IsSuspiciousProgressWipe(data, previousSnapshot, kind, out string suspiciousReason))
        {
            Debug.LogWarning($"[SaveManager] Skipping save ({kind}) because {suspiciousReason}");
            return;
        }

        if (File.Exists(ActiveSavePath))
            File.Copy(ActiveSavePath, ActiveSaveBackupPath, overwrite: true);

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
        _lastLoadedData = data;
    }

    private bool IsRuntimeReadyForSave(out string reason)
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded)
        {
            reason = "active scene is invalid or not loaded";
            return false;
        }

        if (active.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            reason = "active scene is Bootstrap";
            return false;
        }

        if (FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) == null)
        {
            reason = "PlayerController missing";
            return false;
        }

        if (FindFirstObjectByType<Inventory>(FindObjectsInactive.Include) == null)
        {
            reason = "Inventory missing";
            return false;
        }

        reason = "";
        return true;
    }

    private static bool IsCriticalSnapshotValid(SaveData data, out string reason)
    {
        if (data == null)
        {
            reason = "SaveData is null";
            return false;
        }

        if (float.IsNaN(data.playerCurrentHP) || float.IsInfinity(data.playerCurrentHP) || data.playerCurrentHP < 0f)
        {
            reason = $"playerCurrentHP={data.playerCurrentHP}";
            return false;
        }

        if (data.inventorySlots == null)
        {
            reason = "inventorySlots is null";
            return false;
        }

        if (data.storageSlots == null)
        {
            reason = "storageSlots is null";
            return false;
        }

        if (data.inventorySlotCount > 0 && data.inventorySlots.Count == 0)
        {
            reason = $"inventorySlots empty while inventorySlotCount={data.inventorySlotCount}";
            return false;
        }

        if (data.storageSlotCount > 0 && data.storageSlots.Count == 0)
        {
            reason = $"storageSlots empty while storageSlotCount={data.storageSlotCount}";
            return false;
        }

        reason = "";
        return true;
    }

    private static bool IsSuspiciousProgressWipe(
        SaveData current,
        SaveData previous,
        SaveRequestKind kind,
        out string reason)
    {
        // Allow expected sparse snapshots for bootstrap/new-game flows.
        if (kind == SaveRequestKind.NewGameInit || kind == SaveRequestKind.LoadFallbackRecovery)
        {
            reason = "";
            return false;
        }

        if (current == null || previous == null)
        {
            reason = "";
            return false;
        }

        int currentInvFilled = CountFilledSlots(current.inventorySlots);
        int currentStorageFilled = CountFilledSlots(current.storageSlots);
        int currentEquipFilled = CountFilledEquipIds(current);
        int currentToolbeltFilled = CountFilledIds(current.toolbeltItemIds);
        int currentActionBarFilled = CountFilledActionBarAssignments(current);

        int prevInvFilled = CountFilledSlots(previous.inventorySlots);
        int prevStorageFilled = CountFilledSlots(previous.storageSlots);
        int prevEquipFilled = CountFilledEquipIds(previous);
        int prevToolbeltFilled = CountFilledIds(previous.toolbeltItemIds);
        int prevActionBarFilled = CountFilledActionBarAssignments(previous);

        bool previousHadProgress =
            prevInvFilled > 0 ||
            prevStorageFilled > 0 ||
            prevEquipFilled > 0 ||
            prevToolbeltFilled > 0 ||
            prevActionBarFilled > 0;

        bool currentWiped =
            currentInvFilled == 0 &&
            currentStorageFilled == 0 &&
            currentEquipFilled == 0 &&
            currentToolbeltFilled == 0 &&
            currentActionBarFilled == 0;

        if (previousHadProgress && currentWiped)
        {
            reason =
                $"suspicious wipe detected. prev(inv={prevInvFilled},storage={prevStorageFilled},equip={prevEquipFilled},toolbelt={prevToolbeltFilled},bar={prevActionBarFilled}) -> " +
                $"current(inv={currentInvFilled},storage={currentStorageFilled},equip={currentEquipFilled},toolbelt={currentToolbeltFilled},bar={currentActionBarFilled})";
            return true;
        }

        reason = "";
        return false;
    }

    private static int CountFilledSlots(List<SaveData.InventorySlotData> slots)
    {
        if (slots == null)
            return 0;

        int count = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(slots[i].itemId) && slots[i].amount > 0)
                count++;
        }
        return count;
    }

    private static int CountFilledIds(List<string> ids)
    {
        if (ids == null)
            return 0;

        int count = 0;
        for (int i = 0; i < ids.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]))
                count++;
        }
        return count;
    }

    private static int CountFilledActionBarItems(SaveData data) =>
        CountFilledActionBarAssignments(data, itemsOnly: true);

    private static int CountFilledActionBarAssignments(SaveData data, bool itemsOnly = false)
    {
        if (data == null || data.actionBarKinds == null || data.actionBarIds == null)
            return 0;

        int n = Mathf.Min(data.actionBarKinds.Count, data.actionBarIds.Count);
        int count = 0;
        for (int i = 0; i < n; i++)
        {
            if (itemsOnly && data.actionBarKinds[i] != (int)ActionBarAssignmentKind.Item)
                continue;
            if (!string.IsNullOrWhiteSpace(data.actionBarIds[i]))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Scene transitions can serialize a duplicate empty <see cref="ActionBarUI"/> before the live bar is ready.
    /// Keep the last good bar snapshot when the current payload has no assignments.
    /// </summary>
    private static void SeedActionBarFromSnapshot(SaveData dest, SaveData source)
    {
        if (dest == null || source == null)
            return;
        if (CountFilledActionBarAssignments(dest) > 0)
            return;
        if (CountFilledActionBarAssignments(source) <= 0)
            return;

        CopyActionBarLists(dest, source);
    }

    private static void CopyActionBarLists(SaveData dest, SaveData source)
    {
        dest.actionBarSlotIndexes = CloneIntList(source.actionBarSlotIndexes);
        dest.actionBarKinds = CloneIntList(source.actionBarKinds);
        dest.actionBarIds = CloneStringList(source.actionBarIds);
        dest.actionBarItemAmounts = CloneIntList(source.actionBarItemAmounts);
        dest.actionBarSecondarySlotIndexes = CloneIntList(source.actionBarSecondarySlotIndexes);
        dest.actionBarSecondaryKinds = CloneIntList(source.actionBarSecondaryKinds);
        dest.actionBarSecondaryIds = CloneStringList(source.actionBarSecondaryIds);
        dest.actionBarSecondaryItemAmounts = CloneIntList(source.actionBarSecondaryItemAmounts);
        dest.actionBarGatherWoodcutting = CloneGatheringActionBarBlock(source.actionBarGatherWoodcutting);
        dest.actionBarGatherMining = CloneGatheringActionBarBlock(source.actionBarGatherMining);
        dest.actionBarGatherFishing = CloneGatheringActionBarBlock(source.actionBarGatherFishing);
    }

    private static List<int> CloneIntList(List<int> src) =>
        src != null ? new List<int>(src) : new List<int>();

    private static List<string> CloneStringList(List<string> src) =>
        src != null ? new List<string>(src) : new List<string>();

    private static SaveData.GatheringActionBarSaveBlock CloneGatheringActionBarBlock(SaveData.GatheringActionBarSaveBlock src)
    {
        if (src == null)
            return new SaveData.GatheringActionBarSaveBlock();

        return new SaveData.GatheringActionBarSaveBlock
        {
            slotIndexes = CloneIntList(src.slotIndexes),
            kinds = CloneIntList(src.kinds),
            ids = CloneStringList(src.ids),
            itemAmounts = CloneIntList(src.itemAmounts)
        };
    }

    private static int CountFilledEquipIds(SaveData data)
    {
        if (data == null)
            return 0;

        int count = 0;
        if (!string.IsNullOrWhiteSpace(data.equippedMainHand1ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedOffHand1ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedMainHand2ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedOffHand2ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedHelmetItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedBodyItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedBootsItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedTrinketItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedPendantItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedRing1ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedRing2ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedHelmet2ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedBody2ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedBoots2ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedTrinket2ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedPendant2ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedRing12ItemId)) count++;
        if (!string.IsNullOrWhiteSpace(data.equippedRing22ItemId)) count++;
        return count;
    }

    /// <summary>
    /// Call after strip ortho / layout changes so <see cref="SaveData.stripCameraZoomMultiplier"/> catches up before scene reloads (debounced).
    /// </summary>
    public void NotifyStripZoomChangedDebounced()
    {
        if (_isApplyingSaveData)
            return;

        _stripZoomSaveDueUnscaled = Time.unscaledTime + ShopStockSaveDebounceSeconds;
    }

    /// <summary>
    /// Call after shop stock changes. Persists immediately so a quick scene reload cannot skip the debounced write.
    /// </summary>
    public void NotifyShopStockChanged()
    {
        RequestSave(SaveRequestKind.ShopStockChanged, immediate: true);
    }

    /// <summary>
    /// Reloads post-death / one-way NPC dialogue static stores from the active save <b>file</b> (then aligns
    /// <see cref="_lastLoadedData"/> flags). Use after gameplay scene loads so respawn always matches what was written
    /// before <c>LoadScene</c>, even if statics or the in-memory snapshot drifted.
    /// </summary>
    public void RehydrateNpcDialogueStoresFromDiskPreferFile()
    {
        if (!_didInitialLoadOrCreate || _isApplyingSaveData)
            return;

        bool memPending = NpcPostDeathRespawnDialogueStore.IsPending;
        string memDeathNode = NpcPostDeathRespawnDialogueStore.DeathOccurredOnMapNodeId;

        if (!HasSave())
        {
            if (_lastLoadedData != null)
            {
                NpcPostDeathRespawnDialogueStore.ApplyFromSaveData(_lastLoadedData);
                NpcOneWayDialogueQueueStore.ApplyFromSaveData(_lastLoadedData);
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(ActiveSavePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
                return;

            NormalizeSaveDataLists(data);
            SaveDataIntegrity.RepairAfterJsonLoad(data, "RehydrateNpcDialogue");
            NpcPostDeathRespawnDialogueStore.ApplyFromSaveData(data);
            NpcOneWayDialogueQueueStore.ApplyFromSaveData(data);

            if (_lastLoadedData != null)
            {
                _lastLoadedData.npcPostDeathRespawnDialoguePending = data.npcPostDeathRespawnDialoguePending;
                _lastLoadedData.npcPostDeathRespawnDialogueDeathNodeId = data.npcPostDeathRespawnDialogueDeathNodeId ?? "";
            }
        }
        catch (Exception ex)
        {
            _ = ex;
            if (_lastLoadedData != null)
            {
                NpcPostDeathRespawnDialogueStore.ApplyFromSaveData(_lastLoadedData);
                NpcOneWayDialogueQueueStore.ApplyFromSaveData(_lastLoadedData);
            }
        }

        // Disk can still have pending=false if full Save() was skipped when death was recorded; restore session truth.
        if (memPending && !NpcPostDeathRespawnDialogueStore.IsPending)
        {
            NpcPostDeathRespawnDialogueStore.RestorePendingState(memDeathNode);
            RequestSave(SaveRequestKind.DeathDialogueRecovery, immediate: true);
            FlushNpcPostDeathDialogueToDisk();
        }
    }

    /// <summary>
    /// Writes the active slot to disk before unloading gameplay (merchants, inventory, etc.).
    /// </summary>
    public void SaveBeforeSceneTransition()
    {
        RequestSave(SaveRequestKind.SceneTransition, immediate: true);
    }

    /// <summary>
    /// Persists the active slot, tears down the persisted player (same idea as stopping Play in the editor),
    /// clears gameplay session init so the next load runs <see cref="OnSceneLoaded"/> again, and loads the
    /// bootstrap / save-slot scene so the player can pick Continue or New Game.
    /// </summary>
    /// <param name="bootstrapSceneName">Must match the scene in Build Settings (default <c>Bootstrap</c>).</param>
    public void ReturnToSaveSlotSelectAfterSaving(string bootstrapSceneName = "Bootstrap")
    {
        if (!_isApplyingSaveData)
        {
            RequestSave(SaveRequestKind.ReturnToBootstrap, immediate: true);
            IsGameFullyLoaded = false;
            int slot = SaveSlotManager.ActiveSlotIndex;
            if (slot < 0)
                slot = 0;
            SaveSlotManager.MarkSlotAsLastPlayed(slot);
        }

        SaveSlotManager.SetPendingStartMode(SaveSlotManager.SlotStartMode.None);

        // Keep the persistent player alive across Bootstrap round-trips.
        // Destroying here made resume depend on PlayerBootstrapper being present in every flow.
        // With DDOL-based architecture, preserving the player gives deterministic resume behavior.
        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc != null)
            DontDestroyOnLoad(pc.transform.root != null ? pc.transform.root.gameObject : pc.gameObject);

        _didInitialLoadOrCreate = false;

        if (string.IsNullOrWhiteSpace(bootstrapSceneName))
            bootstrapSceneName = "Bootstrap";

        string scene = bootstrapSceneName.Trim();
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            return;
        }

        // Death-respawn can leave the DDOL helper overlay + PlayerPrefs "keep overlay" flag; Bootstrap load would then
        // skip tearing it down in HelperGameplayController.OnDestroy and clicks hit the dimmer instead of save-slot UI.
        HelperGameplayController.ForceHidePersistentOverlayForMenuNavigation();
        MainMenuWindowUI.CancelPersistedOpenRestore();
        DestroyDeathRespawnFullScreenFaderIfAny();

        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }

    private static void DestroyDeathRespawnFullScreenFaderIfAny()
    {
        // Legacy: older builds used a dedicated full-screen "DeathRespawnFader". Death now shares strip/overlay fade from PlayerSpawnController.
        GameObject fader = GameObject.Find("DeathRespawnFader");
        if (fader != null)
            UnityEngine.Object.Destroy(fader);
    }

    /// <summary>
    /// Windows: UniWindow click-through can stay enabled if UI raycasts briefly fail after a scene swap.
    /// Also collapses duplicate <see cref="EventSystem"/> instances so Bootstrap receives pointer events.
    /// </summary>
    private static void RepairBootstrapUiAfterReturningFromGameplay()
    {
        HotkeySettingsRowUI.EnsureUiInputModulesEnabled();

        UniWindowController[] uniWins =
            FindObjectsByType<UniWindowController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < uniWins.Length; i++)
        {
            if (uniWins[i])
                uniWins[i].SetClickThrough(false);
        }

        EventSystem[] systems =
            FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (systems.Length <= 1)
            return;

        EventSystem keep = null;
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem es = systems[i];
            if (!es)
                continue;
            Scene s = es.gameObject.scene;
            if (s.IsValid() && s.isLoaded && s.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                keep = es;
                break;
            }
        }

        if (keep == null)
            keep = EventSystem.current;
        if (keep == null && systems.Length > 0)
            keep = systems[0];

        if (keep != null)
        {
            keep.enabled = true;
            if (EventSystem.current != keep)
                EventSystem.current = keep;
            BaseInputModule[] keepModules = keep.GetComponents<BaseInputModule>();
            for (int m = 0; m < keepModules.Length; m++)
            {
                if (keepModules[m] != null)
                    keepModules[m].enabled = true;
            }
        }

        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem es = systems[i];
            if (!es || es == keep)
                continue;
            UnityEngine.Object.Destroy(es.gameObject);
        }

        // Bootstrap UI can become non-clickable if GraphicRaycaster is disabled on scene canvases after transitions.
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (!c || !c.gameObject.scene.IsValid() || !c.gameObject.scene.isLoaded)
                continue;
            if (!c.gameObject.scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
                continue;

            GraphicRaycaster ray = c.GetComponent<GraphicRaycaster>();
            if (ray != null)
                ray.enabled = true;
        }
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
        // During scene transition ActiveLevelContext already holds the destination; bootstrapper is still the map being left.
        if (ActiveLevelContext.Current != null)
            def = ActiveLevelContext.Current;
        else if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;

        if (def == null)
            return;

        data.activeMapNodeId = def.nodeId ?? "";
        data.activeMapDisplayName = def.displayName ?? "";
    }

    /// <summary>
    /// Call when the player starts map-UI travel so the map being left keeps its standing position in memory
    /// (written to disk on the next <see cref="SaveBeforeSceneTransition"/>).
    /// </summary>
    public void StageLeavingMapExitPosition()
    {
        if (_lastLoadedData == null)
            return;

        TryRecordGameplayMapExitPosition(_lastLoadedData);
    }

    private static void TryRecordGameplayMapExitPosition(SaveData data)
    {
        if (data == null)
            return;

        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.name.Equals("GamePlay", StringComparison.OrdinalIgnoreCase))
            return;

        MapNodeDefinition leaving = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : null;
        if (leaving == null || string.IsNullOrWhiteSpace(leaving.nodeId))
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
            return;

        Vector3 pos = player.transform.position;
        if (!IsValidGameplayWorldPosition(pos, out bool outOfBounds) || outOfBounds)
            return;

        PlayerMapExitPositionStore.RecordExitPosition(data, leaving.nodeId, pos);
    }

    private static bool IsValidGameplayWorldPosition(Vector3 pos, out bool outOfGameplayBounds)
    {
        outOfGameplayBounds = false;
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
            float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z))
            return false;

        if (WorldBounds.Instance == null)
            return true;

        float left = WorldBounds.Instance.Left;
        float right = WorldBounds.Instance.Right;
        outOfGameplayBounds = pos.x < left - 1f || pos.x > right + 1f;
        return !outOfGameplayBounds;
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

        SaveData data = ReadSaveDataFromPath(ActiveSavePath);
        SaveData backup = ReadSaveDataFromPath(ActiveSaveBackupPath);
        if (ShouldPreferBackup(data, backup))
        {
            Debug.LogWarning(
                $"[SaveManager] Active save looked wiped; restoring from backup '{ActiveSaveBackupPath}'.");
            data = backup;
        }
        if (data == null)
            return;

        NormalizeSaveDataLists(data);
        SaveDataIntegrity.RepairAfterJsonLoad(data, "Load");

        _lastLoadedData = data;
        _hasPendingLoad = true;
        _didFinalApplyForCurrentLoad = false;

        int filledInv = CountFilledSlots(data.inventorySlots);
        int filledStorage = CountFilledSlots(data.storageSlots);
        int filledEquip = CountFilledEquipIds(data);
        int filledBarItems = CountFilledActionBarItems(data);

        if (verboseInfoLogs)
        {
            Debug.Log(
                $"[SaveManager] Load staged slot={GetSafeActiveSlot()} inv={data.inventorySlots?.Count ?? -1}/{data.inventorySlotCount} filledInv={filledInv} " +
                $"storage={data.storageSlots?.Count ?? -1}/{data.storageSlotCount} filledStorage={filledStorage} equipFilled={filledEquip} barItemSlots={filledBarItems} " +
                $"gold={data.gold} hp={data.playerCurrentHP}");
        }
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
        if (data.helperNewBadgeSuppressedHelperIds == null)
            data.helperNewBadgeSuppressedHelperIds = new List<string>();
        if (data.levelItemPickupOnceClaimedKeys == null)
            data.levelItemPickupOnceClaimedKeys = new List<string>();
        if (data.permanentDeadEnemySpawnKeys == null)
            data.permanentDeadEnemySpawnKeys = new List<string>();
        if (data.npcOneWayConditionalDialogueConsumedKeys == null)
            data.npcOneWayConditionalDialogueConsumedKeys = new List<string>();
        if (data.npcOneWayDialogueChainProgressRows == null)
            data.npcOneWayDialogueChainProgressRows = new List<NpcOneWayDialogueChainProgressRow>();
        if (data.uiWindowLockKeys == null)
            data.uiWindowLockKeys = new List<string>();
        if (data.uiWindowLockLocked == null)
            data.uiWindowLockLocked = new List<int>();

        if (data.questProgressIds == null)
            data.questProgressIds = new List<string>();
        if (data.questProgressAmounts == null)
            data.questProgressAmounts = new List<int>();
        if (data.acceptedQuestIds == null)
            data.acceptedQuestIds = new List<string>();
        if (data.trackedQuestIds == null)
            data.trackedQuestIds = new List<string>();

        if (data.enduranceTrialNodeIds == null)
            data.enduranceTrialNodeIds = new List<string>();
        if (data.enduranceTrialMaxSelectableTier == null)
            data.enduranceTrialMaxSelectableTier = new List<int>();

        if (data.skills == null)
            data.skills = new List<SaveData.SkillSave>();
        if (data.skillChoiceSelectionKeys == null)
            data.skillChoiceSelectionKeys = new List<string>();
        if (data.skillChoiceSelectionValues == null)
            data.skillChoiceSelectionValues = new List<int>();
        if (data.skillAbilityRowPickKeys == null)
            data.skillAbilityRowPickKeys = new List<string>();
        if (data.skillAbilityRowPickValues == null)
            data.skillAbilityRowPickValues = new List<int>();

        if (data.toolbeltItemIds == null)
            data.toolbeltItemIds = new List<string>();

        PlayerMapExitPositionStore.EnsureLists(data);

        if (data.actionBarSlotIndexes == null)
            data.actionBarSlotIndexes = new List<int>();
        if (data.actionBarKinds == null)
            data.actionBarKinds = new List<int>();
        if (data.actionBarIds == null)
            data.actionBarIds = new List<string>();
        if (data.actionBarItemAmounts == null)
            data.actionBarItemAmounts = new List<int>();

        if (data.actionBarGatherWoodcutting == null)
            data.actionBarGatherWoodcutting = new SaveData.GatheringActionBarSaveBlock();
        if (data.actionBarGatherMining == null)
            data.actionBarGatherMining = new SaveData.GatheringActionBarSaveBlock();
        if (data.actionBarGatherFishing == null)
            data.actionBarGatherFishing = new SaveData.GatheringActionBarSaveBlock();

        MigrateLegacyWorldMapEnteredNodeIdsIfNeeded(data);
    }

    /// <summary>
    /// Older saves never persisted <see cref="SaveData.worldMapEnteredNodeIds"/>, so it stayed empty and every load of a
    /// map was treated as a first visit — replaying <see cref="HelperActivationTrigger.FirstVisitMapNode"/> helpers
    /// like the Welcome tip on <c>tutorial_1</c>. When the slot is clearly not a fresh start, infer nodes already entered.
    /// </summary>
    private static void MigrateLegacyWorldMapEnteredNodeIdsIfNeeded(SaveData data)
    {
        if (data == null || data.worldMapEnteredNodeIds == null || data.worldMapEnteredNodeIds.Count > 0)
            return;

        bool progressed =
            (data.worldMapCompletedNodeIds != null && data.worldMapCompletedNodeIds.Count > 0) ||
            (data.questRewardClaimedIds != null && data.questRewardClaimedIds.Count > 0) ||
            (data.acceptedQuestIds != null && data.acceptedQuestIds.Count > 0) ||
            (data.levelItemPickupOnceClaimedKeys != null && data.levelItemPickupOnceClaimedKeys.Count > 0) ||
            (data.permanentDeadEnemySpawnKeys != null && data.permanentDeadEnemySpawnKeys.Count > 0) ||
            data.playerLevel > 1 ||
            data.xp > 0 ||
            data.gold > 0 ||
            (data.worldMapUnlockedNodeIds != null && data.worldMapUnlockedNodeIds.Count > 1);

        if (!progressed)
            return;

        var set = new HashSet<string>(StringComparer.Ordinal);
        void AddId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;
            set.Add(id.Trim());
        }

        if (data.worldMapUnlockedNodeIds != null)
        {
            for (int i = 0; i < data.worldMapUnlockedNodeIds.Count; i++)
                AddId(data.worldMapUnlockedNodeIds[i]);
        }

        if (data.worldMapCompletedNodeIds != null)
        {
            for (int i = 0; i < data.worldMapCompletedNodeIds.Count; i++)
                AddId(data.worldMapCompletedNodeIds[i]);
        }

        AddId(data.activeMapNodeId);

        data.worldMapEnteredNodeIds.AddRange(set);
        data.worldMapEnteredNodeIds.Sort(string.CompareOrdinal);
    }

    public bool IsLevelItemPickupOnceClaimed(string key) => LevelItemPickupSaveStore.IsClaimed(key);

    public void MarkLevelItemPickupOnceClaimed(string key) => LevelItemPickupSaveStore.MarkClaimed(key);

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
                SaveDataIntegrity.RepairAfterJsonLoad(data, "MerchantRehydrate");
            }
            catch (Exception ex)
            {
                _ = ex;
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
        if (_isApplyingSaveData) return;

        if (Time.unscaledTime - _lastInventoryImmediateSave < MinSaveGap)
            return;

        _lastInventoryImmediateSave = Time.unscaledTime;
        RequestSave(SaveRequestKind.InventoryChanged);
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
        if (_isApplyingSaveData) return;

        // Separate debounce from inventory so a recent inv save cannot block persisting storage.
        if (Time.unscaledTime - _lastStorageImmediateSave < MinSaveGap)
            return;

        _lastStorageImmediateSave = Time.unscaledTime;
        RequestSave(SaveRequestKind.StorageChanged);
    }

    private ISaveable[] FindSaveables()
    {
        var behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        return behaviours.OfType<ISaveable>().ToArray();
    }

    /// <summary>
    /// Multiple <see cref="ActionBarUI"/> (e.g. persistent player shell + scene Canvas) would each call
    /// <see cref="ISaveable.SaveInto"/> and overwrite the same lists; arbitrary order could persist an empty bar
    /// and delete food after level loads.
    /// </summary>
    private static List<ISaveable> DedupeActionBarSaveables(ISaveable[] raw)
    {
        var list = new List<ISaveable>(raw);
        var bars = list.OfType<ActionBarUI>().ToList();
        if (bars.Count <= 1)
            return list;

        ActionBarUI keep = PickCanonicalActionBarForSave(bars);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] is ActionBarUI ab && ab != keep)
                list.RemoveAt(i);
        }

        return list;
    }

    /// <summary>
    /// Multiple <see cref="Inventory"/> components (shell + scene) can overwrite with an empty grid and wipe items on disk.
    /// </summary>
    private static List<ISaveable> DedupeInventorySaveables(List<ISaveable> list)
    {
        if (list == null)
            return list;

        var inventories = list.OfType<Inventory>().ToList();
        if (inventories.Count <= 1)
            return list;

        Inventory keep = PickCanonicalInventoryForSave(inventories);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] is Inventory inv && inv != keep)
                list.RemoveAt(i);
        }

        return list;
    }

    private static Inventory PickCanonicalInventoryForSave(List<Inventory> inventories = null)
    {
        if (inventories == null || inventories.Count == 0)
        {
            inventories = FindObjectsByType<Inventory>(FindObjectsInactive.Include, FindObjectsSortMode.None)?.ToList();
            if (inventories == null || inventories.Count == 0)
                return null;
        }

        Scene active = SceneManager.GetActiveScene();
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        Transform playerRoot = player != null ? player.transform : null;

        Inventory best = null;
        int bestScore = -1;

        for (int i = 0; i < inventories.Count; i++)
        {
            Inventory inv = inventories[i];
            if (!inv)
                continue;

            int score = Mathf.Max(0, inv.SlotCount);
            if (playerRoot != null && inv.transform.IsChildOf(playerRoot))
                score += 10000;
            if (inv.gameObject.scene == active)
                score += 1000;

            if (score > bestScore)
            {
                bestScore = score;
                best = inv;
            }
        }

        return best != null ? best : inventories[0];
    }

    private static PlayerStorage PickCanonicalPlayerStorageForSave()
    {
        PlayerStorage[] all = FindObjectsByType<PlayerStorage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
            return null;
        if (all.Length == 1)
            return all[0];

        Scene active = SceneManager.GetActiveScene();
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        Transform playerRoot = player != null ? player.transform : null;

        PlayerStorage best = null;
        int bestScore = -1;

        for (int i = 0; i < all.Length; i++)
        {
            PlayerStorage ps = all[i];
            if (!ps)
                continue;

            int score = Mathf.Max(0, ps.SlotCount);
            if (playerRoot != null && ps.transform.IsChildOf(playerRoot))
                score += 10000;
            if (ps.gameObject.scene == active)
                score += 1000;

            if (score > bestScore)
            {
                bestScore = score;
                best = ps;
            }
        }

        return best != null ? best : all[0];
    }

    private static ActionBarUI PickCanonicalActionBarForSave(List<ActionBarUI> bars)
    {
        if (bars == null || bars.Count == 0)
            return null;

        Scene active = SceneManager.GetActiveScene();
        ActionBarUI best = null;
        int bestScore = -1;

        for (int i = 0; i < bars.Count; i++)
        {
            ActionBarUI b = bars[i];
            if (!b)
                continue;

            int s = b.ComputeSavePriorityScore();
            bool replace = best == null || s > bestScore;
            if (!replace && s == bestScore)
            {
                bool bActive = b.gameObject.scene == active;
                bool bestActive = best.gameObject.scene == active;
                if (bActive && !bestActive)
                    replace = true;
            }

            if (replace)
            {
                bestScore = s;
                best = b;
            }
        }

        return best != null ? best : bars[0];
    }

    private static void SeedMerchantStocksFromSnapshot(SaveData dest, SaveData source)
    {
        if (dest == null || source == null) return;
        if (source.merchantStocks == null || source.merchantStocks.Count == 0) return;

        dest.merchantStocks ??= new List<SaveData.MerchantStockSave>();
        dest.merchantStocks.Clear();

        for (int i = 0; i < source.merchantStocks.Count; i++)
        {
            SaveData.MerchantStockSave row = source.merchantStocks[i];
            if (row == null || string.IsNullOrWhiteSpace(row.merchantId))
                continue;
            dest.merchantStocks.Add(CloneMerchantStockRow(row));
        }
    }

    private static SaveData.MerchantStockSave CloneMerchantStockRow(SaveData.MerchantStockSave row)
    {
        return new SaveData.MerchantStockSave
        {
            merchantId = row.merchantId,
            quantities = row.quantities != null ? (int[])row.quantities.Clone() : null
        };
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

    /// <summary>
    /// Explicit post-scene-load save application hook for resume flows.
    /// Re-applies the last loaded payload after runtime objects (player/UI/systems) are fully initialized.
    /// </summary>
    public bool ApplyToPlayer(PlayerController player)
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        bool hasPlayer = player != null;

        if (_isApplyingSaveData)
        {
            Debug.LogWarning("[SaveManager] ApplyToPlayer skipped: already applying save data.");
            return false;
        }

        if (_lastLoadedData == null)
        {
            if (!HasSave())
            {
                Debug.LogWarning("[SaveManager] ApplyToPlayer skipped: no save exists and no staged payload.");
                return false;
            }
            Load();
            if (_lastLoadedData == null)
            {
                Debug.LogWarning("[SaveManager] ApplyToPlayer skipped: Load() produced null payload.");
                return false;
            }
        }
        _isApplyingSaveData = true;
        try
        {
            ApplyRuntimeEnhancedItemsToDatabase(_lastLoadedData);

            ISaveable[] saveables = FindSaveables();
            if (saveables == null || saveables.Length == 0)
            {
                Debug.LogWarning("[SaveManager] ApplyToPlayer skipped: no ISaveable components found yet.");
                return false;
            }
            for (int i = 0; i < saveables.Length; i++)
            {
                ISaveable s = saveables[i];
                if (s != null)
                    s.LoadFrom(_lastLoadedData);
            }

            EnsurePlayerStorageLoadedFromData(_lastLoadedData);
            RestoreActiveMapFromSaveData(_lastLoadedData);

            HelperProgressStore.ApplyFromSaveData(_lastLoadedData);
            LevelItemPickupSaveStore.ApplyFromSaveData(_lastLoadedData);
            PermanentEnemyDeathSaveStore.ApplyFromSaveData(_lastLoadedData);
            NpcPostDeathRespawnDialogueStore.ApplyFromSaveData(_lastLoadedData);
            NpcOneWayDialogueQueueStore.ApplyFromSaveData(_lastLoadedData);
            UIWindowLockStore.ApplyFromSaveData(_lastLoadedData);
        }
        finally
        {
            _isApplyingSaveData = false;
        }
        if (verboseInfoLogs)
        {
            Debug.Log(
                $"[SaveManager] ApplyToPlayer complete slot={GetSafeActiveSlot()} inv={_lastLoadedData.inventorySlots?.Count ?? -1}/{_lastLoadedData.inventorySlotCount} " +
                $"storage={_lastLoadedData.storageSlots?.Count ?? -1}/{_lastLoadedData.storageSlotCount} gold={_lastLoadedData.gold} hp={_lastLoadedData.playerCurrentHP}");
        }

        // IMPORTANT: in Bootstrap/DDOL architectures, PlayerController can be transient during scene swaps.
        // Treat a successful ISaveable apply as final and stop pending-load retries immediately.
        _hasPendingLoad = false;
        _didFinalApplyForCurrentLoad = true;

        if (!hasPlayer && verboseInfoLogs)
            Debug.Log("[SaveManager] ApplyToPlayer succeeded without PlayerController present (Bootstrap/DDOL flow).");

        return true;
    }

    private static SaveData ReadSaveDataFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
                return null;

            NormalizeSaveDataLists(data);
            SaveDataIntegrity.RepairAfterJsonLoad(data, $"Load:{Path.GetFileName(path)}");
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Failed reading save path '{path}': {ex.Message}");
            return null;
        }
    }

    private static bool ShouldPreferBackup(SaveData active, SaveData backup)
    {
        if (active == null || backup == null)
            return false;

        int activeScore = ScoreSaveProgress(active);
        int backupScore = ScoreSaveProgress(backup);

        // Prefer backup only when it is materially richer and active looks near-empty.
        return activeScore <= 1 && backupScore >= 5;
    }

    private static int ScoreSaveProgress(SaveData data)
    {
        if (data == null)
            return 0;

        int score = 0;
        score += CountFilledSlots(data.inventorySlots);
        score += CountFilledSlots(data.storageSlots);
        score += CountFilledEquipIds(data);
        score += CountFilledIds(data.toolbeltItemIds);
        score += CountFilledActionBarItems(data);
        return score;
    }

    /// <summary>
    /// Called when gameplay runtime is ready (after scene object initialization).
    /// Applies staged load payload once, late, to avoid Awake/Start default resets overwriting loaded data.
    /// </summary>
    public void OnGameplayReady()
    {
        IsGameFullyLoaded = true;

        if (!_hasPendingLoad || _didFinalApplyForCurrentLoad)
            return;

        PlayerBootstrapper.EnsurePlayerExists("OnGameplayReady");

        if (!ApplyToPlayer(null))
            return;

        // Player / ItemDatabase can still be a frame behind on some loads; keep this late-pass too.
        StartCoroutine(DeferredApplyPlayerStorageLoad());

        _hasPendingLoad = false;
        _didFinalApplyForCurrentLoad = true;
    }

    /// <summary>
    /// Resume/New Game bootstrap entry-point safety reset.
    /// Forces gameplay scene re-entry to run init/load logic even if stale runtime flags survived.
    /// </summary>
    public void PrepareForBootstrapResumeLoad(string reason)
    {
        _ = reason;

        _didInitialLoadOrCreate = false;
        _isApplyingSaveData = false;
        IsGameFullyLoaded = false;
        _hasPendingLoad = false;
        _didFinalApplyForCurrentLoad = false;
        _autosaveTimer = 0f;
        _stripZoomSaveDueUnscaled = -1f;
        _autosaveHoldUntilUnscaled = -1f;

        if (_gameplayReadyRoutine != null)
        {
            StopCoroutine(_gameplayReadyRoutine);
            _gameplayReadyRoutine = null;
        }

        if (_forceApplyAfterSceneLoadRoutine != null)
        {
            StopCoroutine(_forceApplyAfterSceneLoadRoutine);
            _forceApplyAfterSceneLoadRoutine = null;
        }
    }
}