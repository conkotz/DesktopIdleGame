using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerCombatController
{
    private readonly Queue<(EnemyBaseController target, int shotCount, bool allowChainOnHit)> _seekerArrowVolleyQueue = new();
    private Coroutine _seekerArrowVolleyRoutine;

    public void TryProcSeekerArrowsFromAutoAttack(EnemyBaseController target)
    {
        if (!CanUseSeekerArrows() || target == null || target.IsDead)
            return;

        if (UnityEngine.Random.value >= stats.GetSeekerArrowProcChanceFraction())
            return;

        EnqueueSeekerArrowVolley(target, RollSeekerArrowVolleyCount(), allowChainOnHit: true);
    }

    private void TryProcSeekerArrowsFromSeekerHit(EnemyBaseController target)
    {
        if (!CanUseSeekerArrows() || !stats.CanSeekerArrowsChainOnHit() || target == null || target.IsDead)
            return;

        if (UnityEngine.Random.value >= stats.GetSeekerArrowProcChanceFraction())
            return;

        EnqueueSeekerArrowVolley(target, RollSeekerArrowVolleyCount(), allowChainOnHit: true);
    }

    private void EnqueueSeekerArrowVolley(EnemyBaseController target, int shotCount, bool allowChainOnHit)
    {
        if (target == null || target.IsDead || shotCount <= 0)
            return;

        _seekerArrowVolleyQueue.Enqueue((target, shotCount, allowChainOnHit));
        if (_seekerArrowVolleyRoutine == null)
            _seekerArrowVolleyRoutine = StartCoroutine(CoProcessSeekerArrowVolleyQueue());
    }

    private IEnumerator CoProcessSeekerArrowVolleyQueue()
    {
        while (_seekerArrowVolleyQueue.Count > 0)
        {
            (EnemyBaseController target, int shotCount, bool allowChainOnHit) request = _seekerArrowVolleyQueue.Dequeue();
            yield return CoSeekerArrowVolley(request.target, request.shotCount, request.allowChainOnHit);
        }

        _seekerArrowVolleyRoutine = null;
    }

    private IEnumerator CoSeekerArrowVolley(EnemyBaseController initialTarget, int shotCount, bool allowChainOnHit)
    {
        if (shotCount > 1)
        {
            float launchInterval = AbilityCombatPower.GetSeekerArrowVolleyLaunchIntervalSeconds();
            for (int i = 0; i < shotCount; i++)
            {
                if (i > 0)
                    yield return new WaitForSeconds(launchInterval);

                if (!CanUseSeekerArrows())
                    break;

                EnemyBaseController target = ResolveSeekerArrowTarget(initialTarget);
                if (target == null)
                    break;

                StartCoroutine(CoResolveSeekerArrowHit(target, allowChainOnHit));
            }

            yield break;
        }

        EnemyBaseController singleTarget = ResolveSeekerArrowTarget(initialTarget);
        if (singleTarget == null)
            yield break;

        yield return CoResolveSeekerArrowHit(singleTarget, allowChainOnHit);
    }

    private IEnumerator CoResolveSeekerArrowHit(EnemyBaseController target, bool allowChainOnHit)
    {
        if (!CanUseSeekerArrows() || target == null || target.IsDead)
            yield break;

        if (!TryFireSeekerArrowProjectile(target, out float travelTime))
            yield break;

        if (travelTime > 0f)
            yield return new WaitForSeconds(travelTime);

        target = ResolveSeekerArrowTarget(target);
        if (target == null || target.IsDead)
            yield break;

        if (!ApplySeekerArrowDamage(target, out _))
            yield break;

        if (allowChainOnHit)
            TryProcSeekerArrowsFromSeekerHit(target);
    }

    private EnemyBaseController ResolveSeekerArrowTarget(EnemyBaseController preferred)
    {
        if (preferred != null && !preferred.IsDead && preferred.gameObject.activeInHierarchy)
            return preferred;

        return _target != null && !_target.IsDead && _target.gameObject.activeInHierarchy ? _target : null;
    }

    private bool CanUseSeekerArrows() =>
        stats != null
        && player != null
        && !player.IsDead
        && !stats.IsDead
        && stats.IsSeekerArrowsMajorPassiveActive()
        && IsRangedAttack();

    private int RollSeekerArrowVolleyCount()
    {
        if (stats.GetSeekerArrowsEnhancementPick() == AbilityCombatPower.SeekerArrowsEnhancementVolleyChoiceIndex
            && UnityEngine.Random.value < AbilityCombatPower.SeekerArrowVolleyProcChance)
        {
            return AbilityCombatPower.SeekerArrowVolleyCount;
        }

        return 1;
    }

    private bool TryFireSeekerArrowProjectile(EnemyBaseController targetAtFireTime, out float travelTime)
    {
        travelTime = 0f;
        if (rangedProjectilePrefab == null || targetAtFireTime == null)
            return false;

        Vector3 start = GetSeekerArrowSpawnPosition();
        Vector3 targetCenter = GetTargetCenterMass(targetAtFireTime);

        ProjectileVisual proj = Instantiate(rangedProjectilePrefab, start, Quaternion.identity);
        PlayerAbilityVfxController vfx = ResolveAbilityVfx();
        SnipeLingeringTrailFollower.TrailSettings trailSettings = vfx != null
            ? vfx.GetSeekerArrowTrailSettings()
            : SnipeLingeringTrailFollower.TrailSettings.Default;
        SnipeLingeringTrailFollower.Create(proj.transform, trailSettings);
        proj.Launch(
            start,
            targetAtFireTime.transform,
            targetCenter,
            rangedProjectileSpeed,
            rangedProjectileRotationOffset,
            ProjectileVisual.FlightPathMode.Straight);

        travelTime = Mathf.Max(0f, proj.EstimatedTravelTime);
        return true;
    }

    private Vector3 GetSeekerArrowSpawnPosition()
    {
        Transform spawn = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        Vector3 pos;
        if (player == null)
        {
            pos = spawn.position;
        }
        else
        {
            Transform playerRoot = player.transform;
            Vector3 local = playerRoot.InverseTransformPoint(spawn.position);
            local.x = -local.x;
            pos = playerRoot.TransformPoint(local);
        }

        pos.y += SampleSeekerArrowSpawnVerticalOffset();
        return pos;
    }

    private float SampleSeekerArrowSpawnVerticalOffset()
    {
        float refHeight = 1f;
        if (playerCol != null)
            refHeight = Mathf.Max(0.25f, playerCol.bounds.size.y);
        else if (player != null && player.TryGetComponent<Collider2D>(out Collider2D col) && col != null)
            refHeight = Mathf.Max(0.25f, col.bounds.size.y);

        float variance = refHeight * 0.05f;
        return UnityEngine.Random.Range(-variance, variance);
    }

    private bool ApplySeekerArrowDamage(EnemyBaseController target, out bool wasCrit)
    {
        wasCrit = false;
        if (!CanUseSeekerArrows() || target == null || target.IsDead || stats == null)
            return false;

        SplitDamage rolled = stats.RollSplitAttackDamage(out wasCrit);
        rolled *= AbilityCombatPower.SeekerArrowWeaponDamageFraction;
        if (rolled.IsEmpty)
            return false;

        var attribution = new SwingOutgoingAttribution(
            AbilityCombatPower.SeekerArrowOutgoingSourceLabel,
            null,
            0f);
        DamageResult dealt = ApplySplitDamageToTarget(
            target,
            rolled,
            wasCrit,
            AbilityCombatPower.SeekerArrowOutgoingSourceLabel,
            attribution);
        return dealt.Total > 0f;
    }
}
