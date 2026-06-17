using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves horizontal movement/camera clamps for the main lane and optional <see cref="SidePlayArea"/> branches.
/// Default behaviour matches legacy <see cref="WorldBounds"/> when no side area contains the queried world X.
/// </summary>
public static class PlayAreaBounds
{
    private static readonly HashSet<string> EnabledAreaIds =
        new(StringComparer.OrdinalIgnoreCase);

    public static void SetEnabledAreasForMap(IEnumerable<string> areaIds)
    {
        EnabledAreaIds.Clear();
        if (areaIds == null)
            return;

        foreach (string raw in areaIds)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            EnabledAreaIds.Add(raw.Trim());
        }
    }

    public static void ClearEnabledAreas() => EnabledAreaIds.Clear();

    public static bool IsAreaEnabled(SidePlayArea area)
    {
        if (!area)
            return false;
        if (EnabledAreaIds.Count == 0)
            return false;
        return EnabledAreaIds.Contains(area.AreaId);
    }

    public static bool TryGetClampXForWorldX(float worldX, float padding, out float minX, out float maxX)
    {
        if (TryGetEnabledSideAreaContainingWorldX(worldX, out SidePlayArea sideArea))
        {
            sideArea.GetClampX(padding, out minX, out maxX);
            return true;
        }

        if (WorldBounds.Instance != null)
        {
            minX = WorldBounds.Instance.Left + padding;
            maxX = WorldBounds.Instance.Right - padding;
            return true;
        }

        minX = maxX = worldX;
        return false;
    }

    /// <summary>
    /// Clamps a desired X using the lane/side-area bounds for <paramref name="laneReferenceWorldX"/>,
    /// not the destination X. Use when resolving dash/teleport travel so crossing a lane gap does not
    /// re-resolve to main-lane bounds mid-move.
    /// </summary>
    public static float ClampWorldXForLaneAt(float laneReferenceWorldX, float desiredWorldX, float padding)
    {
        if (TryGetClampXForWorldX(laneReferenceWorldX, padding, out float minX, out float maxX))
            return Mathf.Clamp(desiredWorldX, minX, maxX);

        return desiredWorldX;
    }

    public static bool TryGetCameraClampXForWorldX(float worldX, float halfViewportWidth, out float minX, out float maxX)
    {
        if (TryGetEnabledSideAreaContainingWorldX(worldX, out SidePlayArea sideArea))
        {
            sideArea.GetCameraClampX(halfViewportWidth, out minX, out maxX);
            return true;
        }

        if (WorldBounds.Instance != null)
        {
            minX = WorldBounds.Instance.Left + halfViewportWidth;
            maxX = WorldBounds.Instance.Right - halfViewportWidth;
            if (minX > maxX)
            {
                float mid = (WorldBounds.Instance.Left + WorldBounds.Instance.Right) * 0.5f;
                minX = maxX = mid;
            }

            return true;
        }

        minX = maxX = worldX;
        return false;
    }

    /// <summary>
    /// Resolves the canonical lane anchor Y for a world X position.
    /// Uses the enabled side-area floor when inside one; otherwise falls back to the main lane floor.
    /// Anchor is the floor collider center Y (matches spawn-point centerline placement).
    /// </summary>
    public static bool TryGetFloorTopYForWorldX(float worldX, out float floorTopY)
    {
        if (TryGetEnabledSideAreaContainingWorldX(worldX, out SidePlayArea sideArea) && sideArea != null)
        {
            if (TryGetSpawnGroupAnchorY(sideArea.LinkedSpawnGroupId, out floorTopY))
                return true;

            floorTopY = sideArea.RefreshBounds().center.y;
            return true;
        }

        if (TryGetMainLaneAnchorY(out floorTopY))
            return true;

        WorldFloorToUIEdge edge = WorldFloorToUIEdge.Active;
        if (edge != null && edge.FloorCollider != null)
        {
            floorTopY = edge.FloorCollider.bounds.center.y;
            return true;
        }

        Transform laneFloor = LaneGroundEffectPlacement.ResolveLaneFloorTransform();
        if (laneFloor != null)
        {
            Collider2D col = laneFloor.GetComponent<Collider2D>();
            if (col != null)
            {
                floorTopY = col.bounds.center.y;
                return true;
            }
        }

        floorTopY = LaneGroundEffectPlacement.GetLaneFloorTopWorldY();
        return true;
    }

    private static bool TryGetMainLaneAnchorY(out float y)
    {
        y = 0f;

        WorldFloorToUIEdge edge = WorldFloorToUIEdge.Active;
        if (edge != null && edge.FloorCollider != null)
        {
            y = edge.FloorCollider.bounds.center.y;
            return true;
        }

        if (TryGetSpawnGroupAnchorY("AllSpawns", out y))
            return true;

        GameObject playerSpawn = GameObject.Find("SpawnPoint_Player");
        if (playerSpawn != null)
        {
            y = playerSpawn.transform.position.y;
            return true;
        }

        return false;
    }

    private static bool TryGetSpawnGroupAnchorY(string groupId, out float y)
    {
        y = 0f;
        if (string.IsNullOrWhiteSpace(groupId))
            return false;

        SpawnPointGroup[] groups = UnityEngine.Object.FindObjectsByType<SpawnPointGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < groups.Length; i++)
        {
            SpawnPointGroup g = groups[i];
            if (!g || !string.Equals(g.groupId?.Trim(), groupId.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            IReadOnlyList<Transform> points = g.Points;
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
            {
                y = sumY / count;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetEnabledSideAreaContainingWorldX(float worldX, out SidePlayArea area)
    {
        area = null;
        if (EnabledAreaIds.Count == 0)
            return false;

        IReadOnlyDictionary<string, SidePlayArea> all = SidePlayArea.All;
        foreach (KeyValuePair<string, SidePlayArea> kv in all)
        {
            SidePlayArea candidate = kv.Value;
            if (!candidate || !EnabledAreaIds.Contains(candidate.AreaId))
                continue;
            if (!candidate.ContainsWorldX(worldX))
                continue;

            area = candidate;
            return true;
        }

        return false;
    }
}
