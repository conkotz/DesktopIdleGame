using System.Collections.Generic;
using UnityEngine;

public static class WorldClickPicker2D
{
    private static readonly Collider2D[] _hits = new Collider2D[128];
    private static readonly Dictionary<int, CachedPickSort> _sortCache = new(256);

    private struct CachedPickSort
    {
        public Collider2D Collider;
        public int LayerValue;
        public int Order;
        public float Z;
        public int DropOrder;
        public bool HasRenderer;
    }

    public static Collider2D PickTopmostAtPoint(Vector2 point, LayerMask mask)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = true
        };

        int count = Physics2D.OverlapPoint(point, filter, _hits);
        if (count <= 0) return null;
        if (count >= _hits.Length)
            Debug.LogWarning($"[WorldClickPicker2D] OverlapPoint returned {count}+ colliders; increase buffer — pick may miss items.");

        Collider2D bestC = null;

        int bestLayerValue = int.MinValue;
        int bestOrder = int.MinValue;
        float bestZ = float.NegativeInfinity;
        int bestDropOrder = int.MinValue;
        int bestId = int.MinValue;
        float bestDistSq = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            var c = _hits[i];
            if (!c) continue;

            if (!TryGetPickSort(c, out int layerValue, out int order, out float z, out int dropOrder))
                continue;

            int id = c.GetInstanceID();
            Vector2 close = c.ClosestPoint(point);
            float distSq = (close - point).sqrMagnitude;

            bool better =
                (layerValue > bestLayerValue) ||
                (layerValue == bestLayerValue && order > bestOrder) ||
                (layerValue == bestLayerValue && order == bestOrder && z > bestZ) ||
                (layerValue == bestLayerValue && order == bestOrder && Mathf.Approximately(z, bestZ) && dropOrder > bestDropOrder) ||
                (layerValue == bestLayerValue && order == bestOrder && Mathf.Approximately(z, bestZ) && dropOrder == bestDropOrder && distSq < bestDistSq - 1e-8f) ||
                (layerValue == bestLayerValue && order == bestOrder && Mathf.Approximately(z, bestZ) && dropOrder == bestDropOrder && Mathf.Approximately(distSq, bestDistSq) && id > bestId);

            if (better)
            {
                bestLayerValue = layerValue;
                bestOrder = order;
                bestZ = z;
                bestDropOrder = dropOrder;
                bestId = id;
                bestDistSq = distSq;
                bestC = c;
            }
        }

        return bestC;
    }

    private static bool TryGetPickSort(
        Collider2D c,
        out int layerValue,
        out int order,
        out float z,
        out int dropOrder)
    {
        layerValue = 0;
        order = 0;
        z = 0f;
        dropOrder = int.MinValue;

        int id = c.GetInstanceID();
        if (_sortCache.TryGetValue(id, out CachedPickSort cached) && cached.Collider == c)
        {
            if (!cached.HasRenderer)
                return false;

            layerValue = cached.LayerValue;
            order = cached.Order;
            z = cached.Z;
            dropOrder = cached.DropOrder;
            return true;
        }

        SpriteRenderer r = c.GetComponentInParent<SpriteRenderer>();
        if (!r)
            r = c.GetComponentInChildren<SpriteRenderer>(true);

        if (!r)
        {
            _sortCache[id] = new CachedPickSort { Collider = c, HasRenderer = false };
            return false;
        }

        ItemDrop drop = c.GetComponentInParent<ItemDrop>();
        cached = new CachedPickSort
        {
            Collider = c,
            LayerValue = SortingLayer.GetLayerValueFromID(r.sortingLayerID),
            Order = r.sortingOrder,
            Z = r.transform.position.z,
            DropOrder = drop ? drop.DropOrder : int.MinValue,
            HasRenderer = true
        };
        _sortCache[id] = cached;

        layerValue = cached.LayerValue;
        order = cached.Order;
        z = cached.Z;
        dropOrder = cached.DropOrder;
        return true;
    }
}
