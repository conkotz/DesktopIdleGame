using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures the player prefab exists when starting from BootMenu.
/// Keeps changes minimal: if a PlayerController already exists in the loaded scenes, does nothing.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class PlayerBootstrapper : MonoBehaviour
{
    private static PlayerBootstrapper _instance;
    private static GameObject _cachedPlayerPrefab;
    [Header("Player Prefab")]
    [Tooltip("Assign your Player prefab (e.g. Assets/Prefabs/Characters/Player.prefab).")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("If true, the spawned player persists across scene loads.")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Tooltip("If set, moves the spawned player to the SpawnPoint_Player in newly loaded scenes (via PlayerSpawnController).")]
    [SerializeField] private bool requirePlayerSpawnController = true;

    private static GameObject _spawned;

    private void Awake()
    {
        _instance = this;
        if (playerPrefab != null)
            _cachedPlayerPrefab = playerPrefab;
        Debug.Log($"[PlayerBootstrapper] Awake. scene='{gameObject.scene.name}' activeScene='{SceneManager.GetActiveScene().name}' " +
                  $"playerPrefab={(playerPrefab ? playerPrefab.name : "<null>")} spawned={(_spawned ? _spawned.name : "<null>")}", this);

        TrySpawnPlayerNow("Awake");
    }

    public static bool EnsurePlayerExists(string reason = "Unknown")
    {
        if (FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null)
            return true;

        if (_instance == null)
            _instance = FindFirstObjectByType<PlayerBootstrapper>(FindObjectsInactive.Include);

        if (_instance == null && _cachedPlayerPrefab == null)
        {
            Debug.LogWarning($"[PlayerBootstrapper] EnsurePlayerExists('{reason}') failed: no PlayerBootstrapper instance found.");
            return false;
        }

        if (_instance != null)
            return _instance.TrySpawnPlayerNow($"EnsurePlayerExists:{reason}");

        // Bootstrapper instance was unloaded, but we still know the prefab from earlier bootstrap.
        if (_spawned == null)
        {
            _spawned = Instantiate(_cachedPlayerPrefab);
            _spawned.name = _cachedPlayerPrefab.name;
            DontDestroyOnLoad(_spawned);
            Debug.Log($"[PlayerBootstrapper] EnsurePlayerExists:{reason}: spawned player from cached prefab.");
        }

        return FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null;
    }

    private bool TrySpawnPlayerNow(string reason)
    {
        // If a player already exists (placed in scene or previously spawned), do nothing.
        if (FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null)
        {
            Debug.Log($"[PlayerBootstrapper] {reason}: PlayerController already exists. Skipping spawn.", this);
            return true;
        }

        if (_spawned != null)
        {
            Debug.Log($"[PlayerBootstrapper] {reason}: player already spawned previously. Skipping spawn.", this);
            return FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null;
        }

        if (!playerPrefab)
        {
            Debug.LogError($"[PlayerBootstrapper] {reason}: playerPrefab is not assigned. Player will not spawn.", this);
            return false;
        }

        _spawned = Instantiate(playerPrefab);
        _spawned.name = playerPrefab.name; // keep hierarchy tidy
        Debug.Log($"[PlayerBootstrapper] {reason}: Spawned player '{_spawned.name}'. dontDestroyOnLoad={dontDestroyOnLoad}", _spawned);

        if (requirePlayerSpawnController && _spawned.GetComponent<PlayerSpawnController>() == null)
        {
            Debug.LogWarning("[PlayerBootstrapper] Spawned player has no PlayerSpawnController. " +
                             "It may not snap to SpawnPoint_Player on scene load.", _spawned);
        }

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(_spawned);

        return FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}

