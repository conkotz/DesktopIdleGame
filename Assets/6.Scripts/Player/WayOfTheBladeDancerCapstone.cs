using UnityEngine;

public partial class PlayerCombatController
{
    private float _bladeDancerKillCritEndsAt = -1f;
    private float _nextBladeDancerDashTime = -1f;
    private int _lastSyncedBladeDancerKillCritHud = int.MinValue;

    public float GetWayOfTheBladeDancerKillCritBonusFraction() =>
        stats != null && stats.IsWayOfTheBladeDancerCapstoneActive() && Time.time < _bladeDancerKillCritEndsAt
            ? AbilityCombatPower.WayOfTheBladeDancerKillCritChanceBonus
            : 0f;

    private bool IsWayOfTheBladeDancerCapstoneActive() =>
        stats != null && stats.IsWayOfTheBladeDancerCapstoneActive();

    private void TickWayOfTheBladeDancerCapstone()
    {
        if (!IsWayOfTheBladeDancerCapstoneActive())
        {
            ClearWayOfTheBladeDancerStateIfAny();
            return;
        }

        if (!stats.IsBladeDancerDualWieldingMatchingWeapons())
        {
            ClearWayOfTheBladeDancerStateIfAny();
            return;
        }

        SyncWayOfTheBladeDancerKillCritHudBuff();
    }

    /// <summary>After a kill, dash once toward the nearest enemy if a single dash can reach melee.</summary>
    private void TryBladeDancerDashToNearbyEnemyAfterKill()
    {
        EnemyBaseController dashTarget = FindClosestLivingEnemy();
        if (dashTarget == null || dashTarget.IsDead)
            return;

        TryBladeDancerDashTowardEnemy(dashTarget);
    }

    /// <summary>Idle auto-battle: while pathing to the current target (not yet in melee range), dash when in dash range.</summary>
    private bool TryBladeDancerDashDuringIdleAutoBattlePathing(EnemyBaseController pathTarget)
    {
        if (!idleCombatEnabled || pathTarget == null || pathTarget.IsDead)
            return false;

        return TryBladeDancerDashTowardEnemy(pathTarget);
    }

    private bool TryBladeDancerDashTowardEnemy(EnemyBaseController dashTarget)
    {
        if (!IsWayOfTheBladeDancerCapstoneActive() || !stats.IsBladeDancerDualWieldingMatchingWeapons())
            return false;

        if (Time.time < _nextBladeDancerDashTime || player == null)
            return false;

        if (dashTarget == null || dashTarget.IsDead || !dashTarget.gameObject.activeInHierarchy)
            return false;

        if (IsEnemyWithinAttackRange(dashTarget))
            return false;

        if (!TryGetEdgeGapToEnemy(dashTarget, out float gap))
            return false;

        if (!CanBladeDancerDashReachMeleeRange(gap, out float dash))
            return false;

        float myX = transform.position.x;
        float enemyX = dashTarget.transform.position.x;
        float dir = Mathf.Sign(enemyX - myX);
        if (Mathf.Abs(dir) < 0.001f)
            return false;

        float newX = player.ClampWorldX(myX + dir * dash);
        player.SetHorizontalPositionForScriptedMove(newX, dir);
        _nextBladeDancerDashTime = Time.time + AbilityCombatPower.WayOfTheBladeDancerDashCooldownSeconds;
        return true;
    }

    private bool CanBladeDancerDashReachMeleeRange(float gap, out float dashAmount)
    {
        dashAmount = 0f;
        float closeEnoughToSwing = GetEffectiveMeleeReach() + stopSlack;
        if (gap <= closeEnoughToSwing)
            return false;

        float maxDash = AbilityCombatPower.WayOfTheBladeDancerDashMaxDistance;
        dashAmount = Mathf.Min(maxDash, Mathf.Max(0f, gap - 0.5f));
        if (dashAmount <= 0.05f)
            return false;

        return gap - dashAmount <= closeEnoughToSwing + 0.01f;
    }

    private bool TryGetEdgeGapToEnemy(EnemyBaseController enemy, out float gap)
    {
        gap = float.PositiveInfinity;
        if (enemy == null || stats == null)
            return false;

        float myX = transform.position.x;
        float myHalf = HalfWidthX(playerCol);

        Collider2D enemyCol = enemy.GetComponent<Collider2D>();
        if (!enemyCol)
            enemyCol = enemy.GetComponentInChildren<Collider2D>();

        float enemyHalf = HalfWidthX(enemyCol);
        gap = EdgeGapX(myX, enemy.transform.position.x, myHalf, enemyHalf);
        return true;
    }

    private void TryApplyBladeDancerTripleHitFollowUp(
        EnemyBaseController targetToHit,
        SplitDamage rolled,
        bool wasCrit,
        SwingOutgoingAttribution swingAttribution,
        bool suppressOnHitAilments,
        bool suppressBleed,
        bool suppressPoison,
        bool suppressElementalMagicAilment)
    {
        if (targetToHit == null || stats == null || rolled.IsEmpty)
            return;

        if (!stats.TryConsumeBladeDancerTripleHitFollowUp())
            return;

        SplitDamage followUpHit = rolled * AbilityCombatPower.WayOfTheBladeDancerTripleHitDamageFraction;
        var followUpAttribution = new SwingOutgoingAttribution(
            AbilityCombatPower.WayOfTheBladeDancerTripleHitSourceLabel,
            null,
            0f);

        DamageResult doubleDealt = ApplySplitDamageToTarget(
            targetToHit,
            followUpHit,
            wasCrit,
            AbilityCombatPower.WayOfTheBladeDancerTripleHitSourceLabel,
            followUpAttribution);

        if (doubleDealt.Total > 0f)
        {
            RecordOutgoingSourceUse(AbilityCombatPower.WayOfTheBladeDancerTripleHitSourceLabel);
            player.ApplyLifeSteal(doubleDealt.Total);
        }

        if (targetToHit.IsDead)
            NotifyBladeDancerKillCritBuff();

        if (!suppressOnHitAilments && stats.CanApplyOutgoingAilmentsOnHit())
        {
            if (!suppressBleed)
                TryApplyBleed(targetToHit, doubleDealt);
            if (!suppressPoison)
                TryApplyPoison(targetToHit, doubleDealt);
            if (!suppressElementalMagicAilment)
                TryApplyElementalMagicAilment(targetToHit, doubleDealt);
            TryApplyMeleeShock(targetToHit, doubleDealt);
        }

        stats.TryApplyTacticianStunOnEnemyHit(targetToHit);
    }

    private void NotifyBladeDancerKillCritBuff()
    {
        if (!IsWayOfTheBladeDancerCapstoneActive())
            return;

        _bladeDancerKillCritEndsAt = Time.time + AbilityCombatPower.WayOfTheBladeDancerKillCritDurationSeconds;
        _lastSyncedBladeDancerKillCritHud = int.MinValue;
        stats?.NotifyStatsChanged();
        TryBladeDancerDashToNearbyEnemyAfterKill();
    }

    private void SyncWayOfTheBladeDancerKillCritHudBuff()
    {
        PlayerBuffController buffController = player != null ? player.GetComponent<PlayerBuffController>() : null;
        if (!buffController)
            return;

        bool active = Time.time < _bladeDancerKillCritEndsAt;
        if (!active)
        {
            if (_lastSyncedBladeDancerKillCritHud != 0)
            {
                buffController.ClearHudAbilityBuff(AbilityCombatPower.WayOfTheBladeDancerKillCritHudBuffId);
                _lastSyncedBladeDancerKillCritHud = 0;
            }

            return;
        }

        if (_lastSyncedBladeDancerKillCritHud == 1)
            return;

        _lastSyncedBladeDancerKillCritHud = 1;
        buffController.SetHudAbilityBuff(
            AbilityCombatPower.WayOfTheBladeDancerKillCritHudBuffId,
            1,
            _bladeDancerKillCritEndsAt,
            AbilityCombatPower.WayOfTheBladeDancerKillCritDurationSeconds);
    }

    private void ClearWayOfTheBladeDancerStateIfAny()
    {
        bool hadState = _bladeDancerKillCritEndsAt >= 0f
                        || _lastSyncedBladeDancerKillCritHud != int.MinValue;

        _bladeDancerKillCritEndsAt = -1f;
        _lastSyncedBladeDancerKillCritHud = int.MinValue;

        if (!hadState)
            return;

        stats?.ResetBladeDancerTripleHitCounter();
        stats?.NotifyStatsChanged();
        PlayerBuffController buffController = player != null ? player.GetComponent<PlayerBuffController>() : null;
        buffController?.ClearHudAbilityBuff(AbilityCombatPower.WayOfTheBladeDancerKillCritHudBuffId);
    }
}
