using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional branch off the main lane with its own horizontal walk bounds.
/// The main <see cref="WorldBounds"/> floor collider is unchanged; entities use <see cref="PlayAreaBounds"/>
/// to pick main lane vs side-area clamps based on world X and the active map.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay/Side Play Area")]
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class SidePlayArea : MonoBehaviour
{
    [Tooltip("Stable id referenced by MapNodeDefinition.enabledSidePlayAreaIds.")]
    [SerializeField] private string areaId = "SideArea_1";

    [Tooltip("Optional floor collider child. If empty, uses a BoxCollider2D on this object.")]
    [SerializeField] private BoxCollider2D boundsCollider;

    [Tooltip("SpawnPointGroup.groupId spawned on maps that enable this area.")]
    [SerializeField] private string linkedSpawnGroupId = "CombatPractice";

    [Header("Lane Center Line Visual")]
    [SerializeField] private bool showCenterLaneLine = true;
    [SerializeField] private Color centerLaneLineColor = new Color(0.45f, 0.84f, 0.86f, 1f);
    [SerializeField, Min(0.001f)] private float centerLaneLineWidth = 0.05f;
    [SerializeField] private float centerLaneLineZ = -0.1f;
    [SerializeField] private string centerLaneLineChildName = "LaneCenterLine";

    private LineRenderer _centerLaneLine;
    private SpawnPointGroup _cachedSpawnGroup;
    private Vector3 _lastLineRefreshRootPos;
    private Bounds _lastLineRefreshBounds;
    private float _cachedLaneAnchorY = float.NaN;
    private bool _hasLineRefreshState;

    private static readonly Dictionary<string, SidePlayArea> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    public string AreaId => string.IsNullOrWhiteSpace(areaId) ? name : areaId.Trim();
    public string LinkedSpawnGroupId => linkedSpawnGroupId?.Trim() ?? string.Empty;

    public float Left => RefreshBounds().min.x;
    public float Right => RefreshBounds().max.x;

    private void OnEnable()
    {
        Register();
        CacheLinkedSpawnGroup();
        EnsureCenterLaneLineVisual();
        ForceRefreshCenterLaneLineVisual();
    }
    private void OnDisable() => Unregister();

    private void Awake()
    {
        if (!boundsCollider)
            boundsCollider = GetComponentInChildren<BoxCollider2D>(true);
        CacheLinkedSpawnGroup();
        EnsureCenterLaneLineVisual();
        ForceRefreshCenterLaneLineVisual();
    }

    private void OnValidate()
    {
        if (!boundsCollider)
            boundsCollider = GetComponentInChildren<BoxCollider2D>(true);
        _cachedSpawnGroup = null;
        _cachedLaneAnchorY = float.NaN;
        CacheLinkedSpawnGroup();
        EnsureCenterLaneLineVisual();
        ForceRefreshCenterLaneLineVisual();
    }

    private void LateUpdate()
    {
        if (!showCenterLaneLine)
            return;

        EnsureCenterLaneLineVisual();
        if (_centerLaneLine == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            RefreshCenterLaneLineIfDirty();
            return;
        }
#endif

        // Play mode: skip bounds work when this side area hasn't moved since last refresh.
        if (_hasLineRefreshState &&
            (transform.position - _lastLineRefreshRootPos).sqrMagnitude <= 0.000001f)
            return;

        RefreshCenterLaneLineIfDirty();
    }

    public Bounds RefreshBounds()
    {
        if (!boundsCollider)
            boundsCollider = GetComponentInChildren<BoxCollider2D>(true);
        return boundsCollider ? boundsCollider.bounds : new Bounds(transform.position, Vector3.zero);
    }

    public bool ContainsWorldX(float worldX, float slack = 0.05f)
    {
        Bounds b = RefreshBounds();
        if (b.size.sqrMagnitude < 0.0001f)
            return false;
        return worldX >= b.min.x - slack && worldX <= b.max.x + slack;
    }

    public void GetClampX(float padding, out float minX, out float maxX)
    {
        Bounds b = RefreshBounds();
        minX = b.min.x + padding;
        maxX = b.max.x - padding;
        if (minX > maxX)
        {
            float mid = (b.min.x + b.max.x) * 0.5f;
            minX = maxX = mid;
        }
    }

    public void GetCameraClampX(float halfViewportWidth, out float minX, out float maxX)
    {
        GetClampX(halfViewportWidth, out minX, out maxX);
    }

    private void Register()
    {
        string key = AreaId;
        if (string.IsNullOrEmpty(key))
            return;

        if (Registry.TryGetValue(key, out SidePlayArea existing) && existing && existing != this)
        {
            Debug.LogWarning(
                $"[SidePlayArea] Duplicate area id '{key}'. Keeping '{existing.name}', ignoring '{name}'.",
                this);
            return;
        }

        Registry[key] = this;
    }

    private void Unregister()
    {
        string key = AreaId;
        if (string.IsNullOrEmpty(key))
            return;

        if (Registry.TryGetValue(key, out SidePlayArea existing) && existing == this)
            Registry.Remove(key);
    }

    public static bool TryGet(string areaId, out SidePlayArea area)
    {
        area = null;
        if (string.IsNullOrWhiteSpace(areaId))
            return false;
        return Registry.TryGetValue(areaId.Trim(), out area) && area;
    }

    public static IReadOnlyDictionary<string, SidePlayArea> All => Registry;

    private void EnsureCenterLaneLineVisual()
    {
        if (!showCenterLaneLine)
        {
            if (_centerLaneLine != null)
                _centerLaneLine.gameObject.SetActive(false);
            return;
        }

        if (_centerLaneLine == null)
        {
            Transform existing = transform.Find(centerLaneLineChildName);
            if (existing != null)
                _centerLaneLine = existing.GetComponent<LineRenderer>();
        }

        if (_centerLaneLine == null)
        {
            var go = new GameObject(centerLaneLineChildName);
            go.transform.SetParent(transform, false);
            _centerLaneLine = go.AddComponent<LineRenderer>();
            _centerLaneLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _centerLaneLine.receiveShadows = false;
            _centerLaneLine.textureMode = LineTextureMode.Stretch;
            _centerLaneLine.alignment = LineAlignment.View;
            _centerLaneLine.numCapVertices = 0;
            _centerLaneLine.numCornerVertices = 0;
            _centerLaneLine.positionCount = 2;
            _centerLaneLine.useWorldSpace = true;
            _centerLaneLine.sortingLayerName = "Default";
            _centerLaneLine.sortingOrder = 50;
            Shader spritesShader = Shader.Find("Sprites/Default");
            if (spritesShader != null)
                _centerLaneLine.sharedMaterial = new Material(spritesShader);
        }

        if (_centerLaneLine != null)
            _centerLaneLine.gameObject.SetActive(true);
    }

    private void ForceRefreshCenterLaneLineVisual()
    {
        _hasLineRefreshState = false;
        RefreshCenterLaneLineIfDirty();
    }

    private void RefreshCenterLaneLineIfDirty()
    {
        if (_centerLaneLine == null || !showCenterLaneLine)
            return;

        Bounds b = RefreshBounds();
        if (b.size.sqrMagnitude < 0.0001f)
            return;

        Vector3 rootPos = transform.position;
        if (_hasLineRefreshState &&
            (rootPos - _lastLineRefreshRootPos).sqrMagnitude <= 0.000001f &&
            b.center == _lastLineRefreshBounds.center &&
            b.extents == _lastLineRefreshBounds.extents &&
            !float.IsNaN(_cachedLaneAnchorY))
        {
            return;
        }

        _lastLineRefreshRootPos = rootPos;
        _lastLineRefreshBounds = b;
        _cachedLaneAnchorY = ResolveLaneAnchorY(b);
        _hasLineRefreshState = true;

        float y = _cachedLaneAnchorY;
        Vector3 start = new Vector3(b.min.x, y, centerLaneLineZ);
        Vector3 end = new Vector3(b.max.x, y, centerLaneLineZ);

        _centerLaneLine.startWidth = centerLaneLineWidth;
        _centerLaneLine.endWidth = centerLaneLineWidth;
        _centerLaneLine.startColor = centerLaneLineColor;
        _centerLaneLine.endColor = centerLaneLineColor;
        _centerLaneLine.SetPosition(0, start);
        _centerLaneLine.SetPosition(1, end);
    }

    private void CacheLinkedSpawnGroup()
    {
        _cachedSpawnGroup = null;
        if (string.IsNullOrWhiteSpace(linkedSpawnGroupId))
            return;

        string wantId = linkedSpawnGroupId.Trim();
        SpawnPointGroup onSelf = GetComponent<SpawnPointGroup>();
        if (onSelf != null && string.Equals(onSelf.groupId?.Trim(), wantId, StringComparison.OrdinalIgnoreCase))
        {
            _cachedSpawnGroup = onSelf;
            return;
        }

        SpawnPointGroup[] groups = FindObjectsByType<SpawnPointGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < groups.Length; i++)
        {
            SpawnPointGroup g = groups[i];
            if (!g || !string.Equals(g.groupId?.Trim(), wantId, StringComparison.OrdinalIgnoreCase))
                continue;

            _cachedSpawnGroup = g;
            return;
        }
    }

    private float ResolveLaneAnchorY(Bounds fallbackBounds)
    {
        if (_cachedSpawnGroup == null)
            CacheLinkedSpawnGroup();

        SpawnPointGroup group = _cachedSpawnGroup;
        if (group != null)
        {
            IReadOnlyList<Transform> points = group.Points;
            float sumY = 0f;
            int count = 0;
            for (int p = 0; p < points.Count; p++)
            {
                Transform t = points[p];
                if (!t)
                    continue;
                sumY += t.position.y;
                count++;
            }

            if (count > 0)
                return sumY / count;
        }

        return fallbackBounds.center.y;
    }
}
