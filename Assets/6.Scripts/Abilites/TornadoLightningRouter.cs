using System.Collections.Generic;
using UnityEngine;

/// <summary>Routes lightning arcs through active tornados before they reach enemies.</summary>
public static class TornadoLightningRouter
{
    public static EnemyBaseController RouteLightningArc(
        PlayerAbilityVfxController vfx,
        Vector3 arcStart,
        Vector3 arcEnd,
        float arcLightningDamage,
        Transform sortingReference,
        EnemyBaseController defaultChainTarget,
        HashSet<EnemyBaseController> excludeChainTargets = null)
    {
        if (vfx == null || arcLightningDamage <= 0f)
            return defaultChainTarget;

        Vector3 current = arcStart;
        EnemyBaseController chainTarget = defaultChainTarget;
        if (TryFindAbsorbingTornado(arcStart, arcEnd, out TornadoInstance tornado))
        {
            Vector3 tornadoPoint = tornado.GetLightningArcAnchor();
            vfx.SpawnStaticArrowsCritLightningArc(current, tornadoPoint, sortingReference);
            tornado.AbsorbLightningArc(arcLightningDamage);
            current = tornadoPoint;

            EnemyBaseController nearestFromTornado = FindNearestEnemyTo(tornadoPoint, excludeChainTargets);
            if (nearestFromTornado != null)
            {
                chainTarget = nearestFromTornado;
                arcEnd = GetEnemyVfxCenter(nearestFromTornado);
            }
            else
            {
                chainTarget = null;
            }
        }

        if ((arcEnd - current).sqrMagnitude > 0.0001f)
            vfx.SpawnStaticArrowsCritLightningArc(current, arcEnd, sortingReference);

        return chainTarget;
    }

    /// <summary>Nearest absorb-capable tornado within <paramref name="range"/> of <paramref name="point"/>.</summary>
    public static bool TryFindTornadoNear(Vector3 point, float range, out TornadoInstance tornado, out Vector3 anchor)
    {
        tornado = null;
        anchor = Vector3.zero;

        float rangeSq = range * range;
        float bestDistSq = float.MaxValue;
        IReadOnlyList<TornadoInstance> active = TornadoCombatRegistry.ActiveInstances;

        for (int i = 0; i < active.Count; i++)
        {
            TornadoInstance candidate = active[i];
            if (candidate == null || !candidate.IsAlive || !candidate.CanAbsorbLightning)
                continue;

            Vector3 candidateAnchor = candidate.GetLightningArcAnchor();
            float distSq = (candidateAnchor - point).sqrMagnitude;
            if (distSq > rangeSq || distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            tornado = candidate;
            anchor = candidateAnchor;
        }

        return tornado != null;
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

    private static EnemyBaseController FindNearestEnemyTo(
        Vector3 origin,
        HashSet<EnemyBaseController> exclude = null)
    {
        float range = AbilityCombatPower.TornadoLightningAbsorbRange;
        float rangeSq = range * range;
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;
            if (exclude != null && exclude.Contains(enemy))
                continue;

            float distSq = (enemy.transform.position - origin).sqrMagnitude;
            if (distSq > rangeSq || distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = enemy;
        }

        return best;
    }

    private static Vector3 GetEnemyVfxCenter(EnemyBaseController enemy)
    {
        if (!enemy)
            return Vector3.zero;

        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        return enemy.transform.position;
    }
}
