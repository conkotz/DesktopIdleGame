using UnityEngine;

public static class MagicProjectileTargeting
{
    public static Vector3 ResolveDestination(Transform target, Vector3 fallbackTargetPosition)
    {
        if (target == null)
            return fallbackTargetPosition;

        if (target.TryGetComponent<Collider2D>(out var col) && col != null)
            return col.bounds.center;

        var childCol = target.GetComponentInChildren<Collider2D>();
        if (childCol != null)
            return childCol.bounds.center;

        var sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        return target.position;
    }
}
