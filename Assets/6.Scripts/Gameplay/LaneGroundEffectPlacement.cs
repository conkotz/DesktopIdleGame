using UnityEngine;

/// <summary>
/// Places world ground VFX on the playable lane floor (UILaneAlignment / Lane / Floor) so effects
/// track UI zoom and floor alignment the same way spawns and drops do.
/// </summary>
public static class LaneGroundEffectPlacement
{
    private const string LaneChildName = "Lane";
    private const string GroundEffectsChildName = "GroundEffects";

    public static Transform ResolveLaneFloorTransform()
    {
        WorldFloorToUIEdge edge = WorldFloorToUIEdge.Active;
        if (edge != null && edge.FloorCollider != null)
            return edge.FloorCollider.transform;

        Transform content = edge != null ? edge.WorldContentRoot : null;
        if (content != null)
        {
            Transform lane = content.Find(LaneChildName);
            if (lane != null)
                return lane;
        }

        GameObject laneGo = GameObject.Find(LaneChildName);
        return laneGo != null ? laneGo.transform : null;
    }

    public static Transform ResolveGroundEffectsRoot()
    {
        Transform lane = ResolveLaneFloorTransform();
        if (lane == null)
            return null;

        Transform existing = lane.Find(GroundEffectsChildName);
        if (existing != null)
            return existing;

        var root = new GameObject(GroundEffectsChildName);
        root.transform.SetParent(lane, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root.transform;
    }

    public static float GetLaneFloorTopWorldY()
    {
        WorldFloorToUIEdge edge = WorldFloorToUIEdge.Active;
        if (edge != null)
        {
            float top = edge.FloorTopWorldY;
            if (!float.IsNaN(top))
                return top;
        }

        Transform floor = ResolveLaneFloorTransform();
        if (floor != null)
        {
            Collider2D col = floor.GetComponent<Collider2D>();
            if (col != null)
                return col.bounds.max.y;
        }

        return 0f;
    }

    public static Vector3 SnapWorldPointToLaneFloor(Vector3 worldPoint, float yOffset = 0.08f)
    {
        worldPoint.y = GetLaneFloorTopWorldY() + yOffset;
        worldPoint.z = 0f;
        return worldPoint;
    }

    /// <summary>Parents under Lane/GroundEffects and snaps Y to the canonical floor top.</summary>
    public static void PlaceOnLaneFloor(Transform effectTransform, Vector3 worldPoint, float yOffset = 0.08f)
    {
        if (!effectTransform)
            return;

        Vector3 snapped = SnapWorldPointToLaneFloor(worldPoint, yOffset);
        Transform parent = ResolveGroundEffectsRoot() ?? ResolveLaneFloorTransform();
        if (parent != null)
            effectTransform.SetParent(parent, true);

        effectTransform.position = snapped;
    }
}
