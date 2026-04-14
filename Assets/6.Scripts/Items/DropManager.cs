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

    [Header("Spawn clamp (optional)")]
    [Tooltip("If set, drop positions are clamped in screen space so they stay inside this RectTransform (e.g. gameplay / inventory window frame).")]
    [SerializeField] private RectTransform dropFrameBounds;

    [Tooltip("Keeps spawns slightly inside the frame border (screen pixels).")]
    [SerializeField] private float dropFrameInsetPixels = 1f;

    [Tooltip("Used for screen-space clamping. Leave empty to use Camera.main.")]
    [SerializeField] private Camera dropClampCamera;

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

    private void ClampSpawnToDropFrame(ref Vector3 worldPos)
    {
        if (dropFrameBounds == null) return;

        Camera cam = dropClampCamera != null ? dropClampCamera : Camera.main;
        if (cam == null) return;

        if (!TryGetScreenRectScreenBounds(dropFrameBounds, cam, out float minX, out float maxX, out float minY, out float maxY))
            return;

        float inset = Mathf.Max(0f, dropFrameInsetPixels);
        minX += inset;
        maxX -= inset;
        minY += inset;
        maxY -= inset;
        if (minX > maxX || minY > maxY) return;

        Vector3 screen = cam.WorldToScreenPoint(worldPos);
        screen.x = Mathf.Clamp(screen.x, minX, maxX);
        screen.y = Mathf.Clamp(screen.y, minY, maxY);

        float zWorld = worldPos.z;
        worldPos = cam.ScreenToWorldPoint(screen);
        worldPos.z = zWorld;
    }

    private static bool TryGetScreenRectScreenBounds(RectTransform rect, Camera cam, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = maxX = minY = maxY = 0f;
        if (rect == null || cam == null) return false;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(corners[i]);
            minX = Mathf.Min(minX, sp.x);
            maxX = Mathf.Max(maxX, sp.x);
            minY = Mathf.Min(minY, sp.y);
            maxY = Mathf.Max(maxY, sp.y);
        }

        return true;
    }

    public void Spawn(string itemId, int amount, Sprite icon)
    {
        if (!dropAnchor) ResolveAnchor();

        if (!dropAnchor)
        {
            Debug.LogWarning("[DropManager] No drop anchor — cannot spawn.", this);
            return;
        }

        SpawnAtWorldPosition(itemId, amount, icon, dropAnchor.position, clampToDropFrame: true);
    }

    /// <summary>
    /// Spawn a world pickup at a position (e.g. enemy <c>DropAnchor</c>). Uses the same prefab and horizontal scatter as <see cref="Spawn"/>.
    /// Frame clamp is off by default so ground loot stays at the spawn point.
    /// </summary>
    public void SpawnAtWorldPosition(
        string itemId,
        int amount,
        Sprite icon,
        Vector3 worldPosition,
        bool clampToDropFrame = false)
    {
        if (!worldDropPrefab || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        float scatterX = UnityEngine.Random.Range(-scatterRadius, scatterRadius);
        Vector3 spawnPos = worldPosition + new Vector3(scatterX, 0f, 0f);
        if (clampToDropFrame)
            ClampSpawnToDropFrame(ref spawnPos);

        var drop = Instantiate(worldDropPrefab, spawnPos, Quaternion.identity);
        drop.Init(itemId, amount, icon);
    }
}