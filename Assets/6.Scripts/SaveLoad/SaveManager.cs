using System;
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

    private bool _didInitialLoadOrCreate;

    private Inventory _inventory;

    private float _lastImmediateSave;
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
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindInventory();
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

        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
            Debug.LogWarning("[SaveManager] No PlayerController found after gameplay init. If you start from Bootstrap, ensure a player exists in the gameplay scene or is spawned by a bootstrapper.");
    }

    private void ResetAllSaveablesToDefaults()
    {
        var data = new SaveData
        {
            version = 2,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _lastLoadedData = data;

        _isApplyingSaveData = true;
        try
        {
            var saveables = FindSaveables();
            foreach (var s in saveables)
                s.LoadFrom(data);
        }
        finally
        {
            _isApplyingSaveData = false;
        }
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
            version = 2,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var saveables = FindSaveables();
        foreach (var s in saveables)
            s.SaveInto(data);

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

        _lastLoadedData = data;

        _isApplyingSaveData = true;
        try
        {
            var saveables = FindSaveables();
            foreach (var s in saveables)
                s.LoadFrom(data);

            RestoreActiveMapFromSaveData(data);
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

        if (Time.unscaledTime - _lastImmediateSave < MinSaveGap)
            return;

        _lastImmediateSave = Time.unscaledTime;
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