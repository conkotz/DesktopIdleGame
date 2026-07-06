using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a spawned world interactable as player-movable along the lane X axis (1-unit grid snap).
/// Add manually or let <see cref="LevelSpawnDirector"/> bind spawned interactables automatically.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Desktop Idle Game/Gameplay/World Object Movable")]
public sealed class WorldObjectMovable : MonoBehaviour
{
    public const float GridSnapSize = 1f;

    [SerializeField] private bool enableMove = true;

    [Tooltip("When empty, movable on any map where this instance is spawned.")]
    [SerializeField] private string[] allowedMapNodeIds;

    private static readonly List<WorldObjectMovable> Registry = new(32);

    private readonly List<SpriteRenderer> _spriteRenderers = new(8);
    private readonly List<Color> _defaultSpriteColors = new(8);

    public string PositionKey { get; private set; } = string.Empty;
    public string MapNodeId { get; private set; } = string.Empty;
    public string SpawnPointName { get; private set; } = string.Empty;
    public Vector3 DefaultSpawnPosition { get; private set; }
    public bool IsBound { get; private set; }

    public bool CanShowMoveMenu => enableMove && IsBound && IsAllowedOnMap(MapNodeId);

    public static IReadOnlyList<WorldObjectMovable> AllBound => Registry;

    public static bool TryGetFromCollider(Collider2D col, out WorldObjectMovable movable)
    {
        movable = col != null ? col.GetComponentInParent<WorldObjectMovable>() : null;
        return movable != null && movable.CanShowMoveMenu;
    }

    public static void TryBindSpawnedInstance(
        GameObject instance,
        string mapNodeId,
        string spawnPointName,
        Vector3 spawnPosition)
    {
        if (!instance || string.IsNullOrWhiteSpace(mapNodeId) || string.IsNullOrWhiteSpace(spawnPointName))
            return;

        if (IsExcludedSpawn(instance))
            return;

        WorldObjectMovable movable = instance.GetComponent<WorldObjectMovable>();
        if (movable == null)
        {
            if (!HasAutoMovableInteractable(instance))
                return;

            movable = instance.AddComponent<WorldObjectMovable>();
        }

        if (!movable.enableMove)
            return;

        movable.BindSpawn(mapNodeId, spawnPointName, spawnPosition);
        movable.ApplySavedPositionIfAny();
    }

    private static bool IsExcludedSpawn(GameObject instance)
    {
        if (instance.GetComponentInChildren<MapNodePortalTeleporter>(true))
            return true;
        if (instance.GetComponentInChildren<InMapTeleporter>(true))
            return true;
        if (instance.GetComponentInChildren<EnemyBaseController>(true))
            return true;
        if (instance.GetComponentInChildren<EnemyClick>(true))
            return true;
        return false;
    }

    private static bool HasAutoMovableInteractable(GameObject instance)
    {
        if (instance.GetComponentInChildren<MerchantClick>(true))
            return true;
        if (instance.GetComponentInChildren<StorageClick>(true))
            return true;
        if (instance.GetComponentInChildren<FurnaceClick>(true))
            return true;
        if (instance.GetComponentInChildren<CookingClick>(true))
            return true;
        if (instance.GetComponentInChildren<BlacksmithingClick>(true))
            return true;
        if (instance.GetComponentInChildren<NPCInteractionSettings>(true))
            return true;
        if (instance.GetComponentInChildren<QuestGiver>(true))
            return true;
        return false;
    }

    private void OnEnable()
    {
        if (!Registry.Contains(this))
            Registry.Add(this);
    }

    private void OnDisable()
    {
        Registry.Remove(this);
    }

    public void BindSpawn(string mapNodeId, string spawnPointName, Vector3 defaultSpawnPosition)
    {
        MapNodeId = mapNodeId?.Trim() ?? string.Empty;
        SpawnPointName = spawnPointName?.Trim() ?? string.Empty;
        DefaultSpawnPosition = defaultSpawnPosition;
        PositionKey = WorldObjectPositionStore.BuildKey(MapNodeId, SpawnPointName);
        IsBound = !string.IsNullOrWhiteSpace(PositionKey);
        CacheSpriteRenderers();
    }

    public void ApplySavedPositionIfAny()
    {
        if (!IsBound)
            return;

        if (!WorldObjectPositionStore.TryGetSavedWorldX(PositionKey, out float savedX))
            return;

        SetWorldX(savedX);
    }

    public void ResetToDefaultSpawnPosition()
    {
        if (!IsBound)
            return;

        SetWorldX(DefaultSpawnPosition.x);
    }

    public static void ResetSpawnPositionsForMap(string mapNodeId)
    {
        if (string.IsNullOrWhiteSpace(mapNodeId))
            return;

        for (int i = 0; i < Registry.Count; i++)
        {
            WorldObjectMovable movable = Registry[i];
            if (!movable || !movable.IsBound)
                continue;
            if (!string.Equals(movable.MapNodeId, mapNodeId.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            movable.ResetToDefaultSpawnPosition();
        }
    }

    public static float SnapWorldX(float worldX)
    {
        return Mathf.Round(worldX / GridSnapSize) * GridSnapSize;
    }

    public bool WouldOverlapAtX(float snappedWorldX, WorldObjectMovable ignore = null)
    {
        if (!IsBound)
            return false;

        Bounds proposed = GetColliderBoundsAtX(snappedWorldX);
        if (proposed.size.sqrMagnitude <= 0.0001f)
        {
            for (int i = 0; i < Registry.Count; i++)
            {
                WorldObjectMovable other = Registry[i];
                if (!other || other == this || other == ignore || !other.IsBound)
                    continue;
                if (!string.Equals(other.MapNodeId, MapNodeId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (Mathf.Abs(SnapWorldX(other.transform.position.x) - snappedWorldX) < GridSnapSize * 0.5f)
                    return true;
            }

            return false;
        }

        for (int i = 0; i < Registry.Count; i++)
        {
            WorldObjectMovable other = Registry[i];
            if (!other || other == this || other == ignore || !other.IsBound)
                continue;
            if (!string.Equals(other.MapNodeId, MapNodeId, StringComparison.OrdinalIgnoreCase))
                continue;

            Bounds otherBounds = other.GetColliderBoundsAtX(other.transform.position.x);
            if (otherBounds.size.sqrMagnitude <= 0.0001f)
            {
                if (Mathf.Abs(SnapWorldX(other.transform.position.x) - snappedWorldX) < GridSnapSize * 0.5f)
                    return true;
                continue;
            }

            if (proposed.Intersects(otherBounds))
                return true;
        }

        return false;
    }

    public void SetWorldX(float worldX)
    {
        Vector3 pos = transform.position;
        pos.x = SnapWorldX(worldX);
        transform.position = pos;
    }

    public Bounds GetColliderBoundsAtX(float worldX)
    {
        float dx = worldX - transform.position.x;
        Collider2D[] cols = GetComponentsInChildren<Collider2D>(includeInactive: true);
        bool hasAny = false;
        Bounds combined = default;

        for (int i = 0; i < cols.Length; i++)
        {
            Collider2D col = cols[i];
            if (!col || !col.enabled)
                continue;

            Bounds b = col.bounds;
            b.center += new Vector3(dx, 0f, 0f);
            if (!hasAny)
            {
                combined = b;
                hasAny = true;
            }
            else
            {
                combined.Encapsulate(b.min);
                combined.Encapsulate(b.max);
            }
        }

        return combined;
    }

    public void CacheSpriteRenderers()
    {
        _spriteRenderers.Clear();
        _defaultSpriteColors.Clear();
        GetComponentsInChildren(true, _spriteRenderers);
        for (int i = 0; i < _spriteRenderers.Count; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (!sr)
                continue;
            _defaultSpriteColors.Add(sr.color);
        }
    }

    public void SetSpriteTint(Color tint)
    {
        EnsureSpriteCache();
        for (int i = 0; i < _spriteRenderers.Count; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (!sr)
                continue;
            Color baseColor = i < _defaultSpriteColors.Count ? _defaultSpriteColors[i] : sr.color;
            sr.color = new Color(
                baseColor.r * tint.r,
                baseColor.g * tint.g,
                baseColor.b * tint.b,
                baseColor.a * tint.a);
        }
    }

    public void RestoreSpriteColors()
    {
        EnsureSpriteCache();
        for (int i = 0; i < _spriteRenderers.Count; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (!sr)
                continue;
            sr.color = i < _defaultSpriteColors.Count ? _defaultSpriteColors[i] : Color.white;
        }
    }

    private void EnsureSpriteCache()
    {
        if (_spriteRenderers.Count == 0)
            CacheSpriteRenderers();
    }

    private bool IsAllowedOnMap(string mapNodeId)
    {
        if (allowedMapNodeIds == null || allowedMapNodeIds.Length == 0)
            return true;

        if (string.IsNullOrWhiteSpace(mapNodeId))
            return false;

        for (int i = 0; i < allowedMapNodeIds.Length; i++)
        {
            string allowed = allowedMapNodeIds[i];
            if (string.IsNullOrWhiteSpace(allowed))
                continue;
            if (string.Equals(allowed.Trim(), mapNodeId.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
