using UnityEngine;

/// <summary>
/// Exposes lane (world) axis-aligned bounds from a <see cref="BoxCollider2D"/>.
/// Attach to the lane/floor GameObject. Bounds refresh every <see cref="LateUpdate"/> so they stay correct when
/// <see cref="WorldFloorToUIEdge"/> moves the lane (cached-once Awake bounds would drift and break camera/player clamps).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[DefaultExecutionOrder(101)]
public sealed class WorldBounds : MonoBehaviour
{
    private static Bounds _cachedBounds;

    /// <summary>Lane bounds instance (single scene lane; set in <see cref="Awake"/>).</summary>
    public static WorldBounds Instance { get; private set; }

    private BoxCollider2D _laneCollider;
    private Bounds _bounds;

    /// <summary>World-space minimum X from the lane collider.</summary>
    public float Left => _bounds.min.x;

    /// <summary>World-space maximum X from the lane collider.</summary>
    public float Right => _bounds.max.x;

    /// <summary>World-space maximum Y from the lane collider (last refresh).</summary>
    public static float Top => _cachedBounds.max.y;

    /// <summary>World-space minimum Y from the lane collider (last refresh).</summary>
    public static float Bottom => _cachedBounds.min.y;

    private void Awake()
    {
        _laneCollider = GetComponent<BoxCollider2D>();
        Instance = this;
        RefreshBounds();
    }

    private void LateUpdate()
    {
        RefreshBounds();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void RefreshBounds()
    {
        if (!_laneCollider)
            _laneCollider = GetComponent<BoxCollider2D>();
        if (!_laneCollider)
            return;

        _bounds = _laneCollider.bounds;
        _cachedBounds = _bounds;
    }
}
