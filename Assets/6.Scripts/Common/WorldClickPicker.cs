using UnityEngine;

public static class WorldClickPicker2D
{
    private static readonly Collider2D[] _hits = new Collider2D[32];

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

        Collider2D bestC = null;

        int bestLayerValue = int.MinValue;
        int bestOrder = int.MinValue;
        float bestZ = float.NegativeInfinity;
        int bestDropOrder = int.MinValue;
        int bestId = int.MinValue;

        for (int i = 0; i < count; i++)
        {
            var c = _hits[i];
            if (!c) continue;

            var r = c.GetComponentInParent<SpriteRenderer>();
            if (!r) continue;

            int layerValue = SortingLayer.GetLayerValueFromID(r.sortingLayerID);
            int order = r.sortingOrder;
            float z = r.transform.position.z;

            var drop = c.GetComponentInParent<ItemDrop>();
            int dropOrder = drop ? drop.DropOrder : int.MinValue;

            int id = r.GetInstanceID();

            bool better =
                (layerValue > bestLayerValue) ||
                (layerValue == bestLayerValue && order > bestOrder) ||
                (layerValue == bestLayerValue && order == bestOrder && z > bestZ) ||
                (layerValue == bestLayerValue && order == bestOrder && Mathf.Approximately(z, bestZ) && dropOrder > bestDropOrder) ||
                (layerValue == bestLayerValue && order == bestOrder && Mathf.Approximately(z, bestZ) && dropOrder == bestDropOrder && id > bestId);

            if (better)
            {
                bestLayerValue = layerValue;
                bestOrder = order;
                bestZ = z;
                bestDropOrder = dropOrder;
                bestId = id;
                bestC = c;
            }
        }

        return bestC;
    }
}