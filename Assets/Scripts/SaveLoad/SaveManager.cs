using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField] private string fileName = "save.json";
    [SerializeField] private bool autosave = true;
    [SerializeField] private float autosaveIntervalSeconds = 30f;

    private string FilePath => Path.Combine(Application.persistentDataPath, fileName);
    private float _autosaveTimer;

    private bool _didInitialLoadOrCreate;

    private Inventory _inventory;

    private float _lastImmediateSave;
    private const float MinSaveGap = 1f;

    private SaveData _lastLoadedData;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
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

        // Do initial load/create once, after the first gameplay scene is loaded
        if (_didInitialLoadOrCreate) return;
        _didInitialLoadOrCreate = true;

        if (HasSave())
            Load();
        else
            Save();
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

    public bool HasSave() => File.Exists(FilePath);

    public void Save()
    {
        var data = new SaveData
        {
            version = 2,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var saveables = FindSaveables();
        foreach (var s in saveables)
            s.SaveInto(data);

        _lastLoadedData = data;

        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json);
    }

    public void Load()
    {
        if (!HasSave()) return;

        var json = File.ReadAllText(FilePath);
        var data = JsonUtility.FromJson<SaveData>(json);

        _lastLoadedData = data;

        var saveables = FindSaveables();
        foreach (var s in saveables)
            s.LoadFrom(data);
    }

    private void HandleInventoryChanged()
    {
        if (!_didInitialLoadOrCreate) return;   // ✅ ADD THIS

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
        File.Delete(FilePath);
        _didInitialLoadOrCreate = false; // allow re-init on next scene load
    }
    public bool TryGetLastLoadedData(out SaveData data)
    {
        data = _lastLoadedData;
        return data != null;
    }
}