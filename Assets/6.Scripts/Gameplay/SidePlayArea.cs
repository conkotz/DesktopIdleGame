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
public sealed class SidePlayArea : MonoBehaviour
{
    [Tooltip("Stable id referenced by MapNodeDefinition.enabledSidePlayAreaIds.")]
    [SerializeField] private string areaId = "SideArea_1";

    [Tooltip("Optional floor collider child. If empty, uses a BoxCollider2D on this object.")]
    [SerializeField] private BoxCollider2D boundsCollider;

    [Tooltip("SpawnPointGroup.groupId spawned on maps that enable this area.")]
    [SerializeField] private string linkedSpawnGroupId = "CombatPractice";

    private static readonly Dictionary<string, SidePlayArea> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    public string AreaId => string.IsNullOrWhiteSpace(areaId) ? name : areaId.Trim();
    public string LinkedSpawnGroupId => linkedSpawnGroupId?.Trim() ?? string.Empty;

    public float Left => RefreshBounds().min.x;
    public float Right => RefreshBounds().max.x;

    private void OnEnable() => Register();
    private void OnDisable() => Unregister();

    private void Awake()
    {
        if (!boundsCollider)
            boundsCollider = GetComponentInChildren<BoxCollider2D>(true);
    }

    private void OnValidate()
    {
        if (!boundsCollider)
            boundsCollider = GetComponentInChildren<BoxCollider2D>(true);
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
            Debug.LogWarning($"[SidePlayArea] Duplicate area id '{key}'. Keeping '{existing.name}', ignoring '{name}'.", this);

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
}
