using UnityEngine;

public class HoverPicker2D : MonoBehaviour
{
    [SerializeField] private LayerMask pickMask = ~0;

    private SimpleHoverHighlight2D _currentHovered;

    private static readonly Collider2D[] _hits = new Collider2D[32];

    private void Update()
    {
        var winner = PickTopmostUnderMouse();

        if (_currentHovered != winner)
        {
            if (_currentHovered) _currentHovered.SetHovered(false);
            _currentHovered = winner;
            if (_currentHovered) _currentHovered.SetHovered(true);
        }
    }

    public SimpleHoverHighlight2D PickTopmostUnderMouse()
    {
        var cam = Camera.main;
        if (!cam) return null;

        Vector3 wp3 = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 wp = new Vector2(wp3.x, wp3.y);

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = pickMask,
            useTriggers = true
        };

        int count = Physics2D.OverlapPoint(wp, filter, _hits);
        if (count <= 0) return null;

        SimpleHoverHighlight2D best = null;

        int bestLayerValue = int.MinValue;
        int bestOrder = int.MinValue;
        float bestZ = float.NegativeInfinity;
        int bestId = int.MinValue;

        for (int i = 0; i < count; i++)
        {
            var c = _hits[i];
            if (!c) continue;

            var h = c.GetComponentInParent<SimpleHoverHighlight2D>();
            if (!h) continue;

            var sr = h.GetComponent<SpriteRenderer>();
            if (!sr) continue;

            int layerValue = SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
            int order = sr.sortingOrder;
            float z = sr.transform.position.z;
            int id = sr.GetInstanceID();

            bool better =
                (layerValue > bestLayerValue) ||
                (layerValue == bestLayerValue && order > bestOrder) ||
                (layerValue == bestLayerValue && order == bestOrder && z > bestZ) ||
                (layerValue == bestLayerValue && order == bestOrder && Mathf.Approximately(z, bestZ) && id > bestId);

            if (better)
            {
                bestLayerValue = layerValue;
                bestOrder = order;
                bestZ = z;
                bestId = id;
                best = h;
            }
        }

        return best;
    }
}