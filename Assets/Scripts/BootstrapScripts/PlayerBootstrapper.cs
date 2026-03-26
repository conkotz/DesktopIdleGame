using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures the player prefab exists when starting from BootMenu.
/// Keeps changes minimal: if a PlayerController already exists in the loaded scenes, does nothing.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class PlayerBootstrapper : MonoBehaviour
{
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
        Debug.Log($"[PlayerBootstrapper] Awake. scene='{gameObject.scene.name}' activeScene='{SceneManager.GetActiveScene().name}' " +
                  $"playerPrefab={(playerPrefab ? playerPrefab.name : "<null>")} spawned={(_spawned ? _spawned.name : "<null>")}", this);

        // If a player already exists (placed in scene or previously spawned), do nothing.
        if (FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("[PlayerBootstrapper] PlayerController already exists in loaded scenes. Skipping spawn.", this);
            return;
        }

        if (_spawned != null)
        {
            Debug.Log("[PlayerBootstrapper] Player already spawned previously. Skipping spawn.", this);
            return;
        }

        if (!playerPrefab)
        {
            Debug.LogError("[PlayerBootstrapper] playerPrefab is not assigned. Player will not spawn.", this);
            return;
        }

        _spawned = Instantiate(playerPrefab);
        _spawned.name = playerPrefab.name; // keep hierarchy tidy
        Debug.Log($"[PlayerBootstrapper] Spawned player '{_spawned.name}'. dontDestroyOnLoad={dontDestroyOnLoad}", _spawned);

        if (requirePlayerSpawnController && _spawned.GetComponent<PlayerSpawnController>() == null)
        {
            Debug.LogWarning("[PlayerBootstrapper] Spawned player has no PlayerSpawnController. " +
                             "It may not snap to SpawnPoint_Player on scene load.", _spawned);
        }

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(_spawned);
    }
}

