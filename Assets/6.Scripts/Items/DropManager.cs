using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DropManager : MonoBehaviour
{
    public static DropManager Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private ItemDrop worldDropPrefab;

    [Tooltip("Anchor point on the player where drops spawn from. Leave null to auto-resolve.")]
    [SerializeField] private Transform dropAnchor;

    [Tooltip("Child name under the Player (any depth) to use if dropAnchor is not set.")]
    [SerializeField] private string dropAnchorChildName = "DropAnchor";

    [SerializeField] private float scatterRadius = 0.15f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // If this manager should persist too, uncomment:
        // DontDestroyOnLoad(gameObject);

        ResolveAnchor();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // Player may persist, but references can still end up null after scene loads / re-instantiation.
        dropAnchor = null;
        ResolveAnchor();
    }

    private void ResolveAnchor()
    {
        if (dropAnchor) return;

        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!player) return;

        // Best option (if you add this to PlayerController): player.DropAnchor
        // if (player.DropAnchor) { dropAnchor = player.DropAnchor; return; }

        // Otherwise: find by name anywhere under the player (works with nested hierarchy)
        var found = FindDeepChild(player.transform, dropAnchorChildName);
        dropAnchor = found ? found : player.transform; // safe fallback
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        // DFS search through all descendants
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.Equals(name, StringComparison.Ordinal))
                return child;

            var result = FindDeepChild(child, name);
            if (result) return result;
        }
        return null;
    }

    public void Spawn(string itemId, int amount, Sprite icon)
    {
        if (!dropAnchor) ResolveAnchor();

        if (!worldDropPrefab || !dropAnchor ||
            string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        float scatterX = UnityEngine.Random.Range(-scatterRadius, scatterRadius);
        Vector3 spawnPos = dropAnchor.position + new Vector3(scatterX, 0f, 0f);

        var drop = Instantiate(worldDropPrefab, spawnPos, Quaternion.identity);
        drop.Init(itemId, amount, icon);
    }
}