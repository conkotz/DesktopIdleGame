using System.Collections.Generic;
using UnityEngine;

/// <summary>Routes lightning arcs through active tornados before they reach enemies.</summary>
public static class TornadoLightningRouter
{
    public static Vector3 RouteLightningArc(
        PlayerAbilityVfxController vfx,
        Vector3 arcStart,
        Vector3 arcEnd,
        float arcLightningDamage,
        Transform sortingReference)
    {
        if (vfx == null || arcLightningDamage <= 0f)
            return arcStart;

        Vector3 current = arcStart;
        if (TryFindAbsorbingTornado(arcStart, arcEnd, out TornadoInstance tornado))
        {
            Vector3 tornadoPoint = tornado.GetLightningArcAnchor();
            vfx.SpawnStaticArrowsCritLightningArc(current, tornadoPoint, sortingReference);
            tornado.AbsorbLightningArc(arcLightningDamage);
            current = tornadoPoint;
        }

        if ((arcEnd - current).sqrMagnitude > 0.0001f)
            vfx.SpawnStaticArrowsCritLightningArc(current, arcEnd, sortingReference);

        return current;
    }

    private static bool TryFindAbsorbingTornado(
        Vector3 arcStart,
        Vector3 arcEnd,
        out TornadoInstance tornado)
    {
        tornado = null;
        float bestDistSq = float.MaxValue;
        float range = AbilityCombatPower.TornadoLightningAbsorbRange;
        float rangeSq = range * range;

        IReadOnlyList<TornadoInstance> active = TornadoCombatRegistry.ActiveInstances;
        for (int i = 0; i < active.Count; i++)
        {
            TornadoInstance candidate = active[i];
            if (candidate == null || !candidate.IsAlive || !candidate.CanAbsorbLightning)
                continue;

            Vector3 anchor = candidate.GetLightningArcAnchor();
            float distSq = Mathf.Min(
                (anchor - arcStart).sqrMagnitude,
                (anchor - arcEnd).sqrMagnitude);
            if (distSq > rangeSq || distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            tornado = candidate;
        }

        return tornado != null;
    }
}
