using System;
using System.Collections;
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

    [Header("Ground alignment")]
    [SerializeField] private bool alignPlayerDropsToGround = true;

    [Tooltip(
        "Optional canonical floor reference (e.g. the Player root, or a FloorAnchor child in the level). " +
        "When assigned, its world Y is used as the ground line for all drops so loot lands exactly on the same " +
        "line the player and NPCs stand on. Leave empty to fall back to the physics raycast below.")]
    [SerializeField] private Transform floorReference;

    [Tooltip(
        "Child name to search under PlayerController for the canonical floor anchor when Floor Reference is empty " +
        "(any depth, exact match). If found, its world Y is used as the ground line. Leave empty to disable the lookup.")]
    [SerializeField] private string floorReferenceChildName = "FloorAnchor";

    [Tooltip(
        "Extra adjustment applied to the resolved ground Y (negative pushes drops down, positive lifts them up). " +
        "Tweak by ~ -0.05 to -0.20 if drops look slightly too high above the visual floor.")]
    [SerializeField] private float floorYOffset = -0.15f;

    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCastDistance = 10f;
    [SerializeField] private float groundSkin = 0.01f;
    [SerializeField] private float launchDuration = 0.35f;
    [SerializeField] private float launchArcHeight = 1.25f;
    [SerializeField] private float landingJitterX = 0.5f;

    private PlayerController _player;
    private Transform _floorReferenceAutoCache;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
        _floorReferenceAutoCache = null;
        ResolveAnchor();
    }

    private void ResolveAnchor(bool forceRefresh = false)
    {
        if (dropAnchor && !forceRefresh) return;

        _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!_player) return;

        // Best option (if you add this to PlayerController): player.DropAnchor
        // if (player.DropAnchor) { dropAnchor = player.DropAnchor; return; }

        // Otherwise: find by name anywhere under the player (works with nested hierarchy)
        var found = FindDeepChild(_player.transform, dropAnchorChildName);
        dropAnchor = found ? found : _player.transform; // safe fallback
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

    public void Spawn(string itemId, int amount, Sprite icon, string sourceName = null)
    {
        ResolveAnchor(forceRefresh: true);

        if (!dropAnchor)
        {
            Debug.LogWarning("[DropManager] No drop anchor — cannot spawn.", this);
            return;
        }

        SpawnAtWorldPosition(
            itemId,
            amount,
            icon,
            dropAnchor.position,
            alignToGround: alignPlayerDropsToGround,
            sourceName: sourceName);
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
        bool clampToDropFrame = false,
        bool alignToGround = false,
        string sourceName = null,
        bool sweepOnMapExit = false)
    {
        if (!worldDropPrefab || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        float scatterX = GetOutwardScatterX(worldPosition, alignToGround);
        Vector3 spawnPos = worldPosition + new Vector3(scatterX, 0f, 0f);
        if (clampToDropFrame)
            ClampSpawnToDropFrame(ref spawnPos);

        float groundY = 0f;
        bool hasGround = alignToGround && TryFindGroundY(spawnPos, out groundY);

        Vector3 instantiatePosition = hasGround ? worldPosition : spawnPos;
        var drop = Instantiate(worldDropPrefab, instantiatePosition, Quaternion.identity);
        drop.Init(itemId, amount, icon);
        if (!string.IsNullOrWhiteSpace(sourceName))
            drop.SetSourceName(sourceName);
        if (sweepOnMapExit)
            drop.MarkSweepOnMapExit();

        if (hasGround)
        {
            drop.LaunchToGround(
                worldPosition,
                spawnPos,
                groundY,
                launchDuration,
                launchArcHeight,
                groundSkin);
        }
    }

    /// <summary>
    /// Spawns a pickup at an exact world position (level spawn points). No horizontal scatter, no drop-frame clamp.
    /// </summary>
    /// <param name="itemRespawnTimerSeconds">
    /// When &gt;= ~0.01s, fully picking up this drop schedules another spawn at the same spot after this delay (no one-shot save claim).
    /// </param>
    public void SpawnPlacedLevelPickup(
        ItemDefinition itemDef,
        int amount,
        Vector3 worldPosition,
        Transform parent,
        bool alignToGround,
        string levelOneShotSaveKey,
        float itemRespawnTimerSeconds = 0f)
    {
        if (!itemDef || amount <= 0)
            return;

        if (!worldDropPrefab)
        {
            Debug.LogWarning("[DropManager] World Drop Prefab is not assigned — cannot spawn level item pickup.", this);
            return;
        }

        string itemId = string.IsNullOrWhiteSpace(itemDef.itemId) ? null : itemDef.itemId.Trim();
        if (string.IsNullOrEmpty(itemId))
            return;

        bool respawns = itemRespawnTimerSeconds >= 0.01f;
        string keyToUse = respawns ? null : levelOneShotSaveKey;

        var drop = Instantiate(worldDropPrefab, worldPosition, Quaternion.identity, parent != null ? parent : null);
        drop.Init(itemId, amount, itemDef.icon, disableAutoDespawn: true);
        if (!string.IsNullOrWhiteSpace(keyToUse))
            drop.SetLevelOneShotPickupClaimKey(keyToUse);
        if (respawns)
            drop.ConfigurePlacedLevelRespawn(itemRespawnTimerSeconds, itemDef, amount, parent, alignToGround);

        if (alignToGround && TryFindGroundY(worldPosition, out float groundY))
            drop.SnapVisualBottomToWorldY(groundY, groundSkin);
    }

    internal void SchedulePlacedLevelPickupRespawn(
        ItemDefinition itemDef,
        int amount,
        Vector3 worldPosition,
        Transform parent,
        bool alignToGround,
        float delaySeconds)
    {
        if (!itemDef || amount <= 0 || delaySeconds < 0.01f)
            return;
        if (!isActiveAndEnabled)
            return;
        StartCoroutine(CoPlacedLevelPickupRespawn(itemDef, amount, worldPosition, parent, alignToGround, delaySeconds));
    }

    private IEnumerator CoPlacedLevelPickupRespawn(
        ItemDefinition itemDef,
        int amount,
        Vector3 worldPosition,
        Transform parent,
        bool alignToGround,
        float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        if (!itemDef || amount <= 0)
            yield break;
        SpawnPlacedLevelPickup(itemDef, amount, worldPosition, parent, alignToGround, null, delaySeconds);
    }

    /// <summary>
    /// Resolves the canonical floor Y used for drop landing. Priority:
    /// 1. <see cref="floorReference"/> (explicit, matches the player/NPC line exactly),
    /// 2. <see cref="WorldFloorToUIEdge.Active"/>'s configured floor collider top — same line the
    ///    lane alignment system uses, so loot snaps to the exact floor the player walks on,
    /// 3. A child named <see cref="floorReferenceChildName"/> under the active player,
    /// 4. A downward Physics2D raycast against <see cref="groundMask"/>.
    /// <see cref="floorYOffset"/> is added to whichever source succeeds.
    /// </summary>
    private bool TryFindGroundY(Vector3 origin, out float groundY)
    {
        groundY = 0f;

        if (TryResolveFloorReferenceY(out float referenceY))
        {
            groundY = referenceY + floorYOffset;
            return true;
        }

        int mask = groundMask.value != 0 ? groundMask.value : Physics2D.AllLayers;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, Mathf.Max(0.1f, groundCastDistance), mask);
        if (!hit.collider)
            return false;

        groundY = hit.point.y + floorYOffset;
        return true;
    }

    private bool TryResolveFloorReferenceY(out float y)
    {
        if (floorReference != null)
        {
            y = floorReference.position.y;
            return true;
        }

        // Lane-alignment system already knows the canonical floor (the Floor box collider under
        // UILaneAlignment/Lane). Match its world top so drops sit on the same line the player walks on.
        WorldFloorToUIEdge laneFloor = WorldFloorToUIEdge.Active;
        if (laneFloor != null)
        {
            float laneTop = laneFloor.FloorTopWorldY;
            if (!float.IsNaN(laneTop))
            {
                y = laneTop;
                return true;
            }
        }

        if (_floorReferenceAutoCache != null)
        {
            y = _floorReferenceAutoCache.position.y;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(floorReferenceChildName))
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

            if (_player != null)
            {
                Transform anchor = FindDeepChild(_player.transform, floorReferenceChildName.Trim());
                if (anchor != null)
                {
                    // Cache so subsequent drops avoid the recursive lookup. Stored separately from the
                    // inspector field so we never overwrite a designer-assigned reference.
                    _floorReferenceAutoCache = anchor;
                    y = anchor.position.y;
                    return true;
                }
            }
        }

        y = 0f;
        return false;
    }

    private float GetOutwardScatterX(Vector3 worldPosition, bool usePlayerDirection)
    {
        float distance = UnityEngine.Random.Range(scatterRadius * 0.35f, scatterRadius);
        distance += UnityEngine.Random.Range(-landingJitterX, landingJitterX);
        distance = Mathf.Max(0.05f, distance);

        if (!usePlayerDirection || !dropAnchor)
            return UnityEngine.Random.value < 0.5f ? -distance : distance;

        if (!_player)
            _player = dropAnchor.GetComponentInParent<PlayerController>();

        float awaySign = _player ? Mathf.Sign(_player.FacingDirectionX) : 1f;
        if (Mathf.Approximately(awaySign, 0f))
            awaySign = 1f;

        return awaySign * distance;
    }
}