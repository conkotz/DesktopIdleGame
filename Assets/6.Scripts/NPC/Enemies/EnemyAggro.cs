using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared enemy aggro rules for the local player team (player + summons).
/// Centralized here so future multiplayer can key threats per team member.
/// </summary>
public static class EnemyAggro
{
    public static float GetNearestPlayerTeamDistanceX(float enemyWorldX, Transform playerTransform)
    {
        float best = float.MaxValue;

        if (playerTransform)
            best = Mathf.Min(best, Mathf.Abs(playerTransform.position.x - enemyWorldX));

        IReadOnlyList<MinionCombatTarget> minions = MinionCombatTarget.ActiveTargets;
        for (int i = 0; i < minions.Count; i++)
        {
            MinionCombatTarget mct = minions[i];
            if (!mct || !mct.IsAlive)
                continue;

            Transform t = mct.transform;
            if (!t)
                continue;

            best = Mathf.Min(best, Mathf.Abs(t.position.x - enemyWorldX));
        }

        return best;
    }

    /// <summary>
    /// Whether this enemy should leave idle wander and enter combat AI this tick.
    /// </summary>
    public static bool ShouldEngage(
        LevelEnemyAggroMode mode,
        bool personallyProvoked,
        bool mapAggroLatched,
        float nearestPlayerTeamDistanceX,
        float aggroRange,
        bool ignoreAggroRange)
    {
        bool inRange = nearestPlayerTeamDistanceX <= aggroRange;

        if (ignoreAggroRange)
        {
            return mode switch
            {
                LevelEnemyAggroMode.Aggressive => true,
                LevelEnemyAggroMode.CalmUntilPlayerAggressive => personallyProvoked || mapAggroLatched,
                _ => personallyProvoked
            };
        }

        return mode switch
        {
            LevelEnemyAggroMode.Aggressive => inRange,
            LevelEnemyAggroMode.CalmUntilPlayerAggressive => personallyProvoked || (mapAggroLatched && inRange),
            _ => personallyProvoked
        };
    }

    public static bool IsDirectPlayerAttacker(Transform attacker)
    {
        if (!attacker)
            return false;

        if (attacker.GetComponent<MinionCombatTarget>() != null ||
            attacker.GetComponentInParent<MinionCombatTarget>() != null)
            return false;

        return attacker.CompareTag("Player") ||
            attacker.GetComponent<PlayerController>() != null ||
            attacker.GetComponentInParent<PlayerController>() != null ||
            attacker.GetComponent<PlayerCombatController>() != null ||
            attacker.GetComponentInParent<PlayerCombatController>() != null;
    }

    public static MinionCombatTarget GetMinionCombatTargetFrom(Transform attacker)
    {
        if (!attacker)
            return null;

        MinionCombatTarget mct = attacker.GetComponent<MinionCombatTarget>();
        if (!mct)
            mct = attacker.GetComponentInParent<MinionCombatTarget>();
        return mct;
    }

    public static void ResolveIncomingHitAggro(
        Transform attacker,
        bool playerDamagedThisEnemy,
        bool minionDamagedThisEnemy,
        bool retaliationMinionValid,
        bool minionActivelyStrikingThisEnemy,
        bool minionTauntLocked,
        MinionCombatTarget minionAttacker,
        ref Transform retaliationMinionTarget)
    {
        if (minionTauntLocked && retaliationMinionValid)
            return;

        if (IsDirectPlayerAttacker(attacker))
        {
            ResolvePlayerHitAggro(
                playerDamagedThisEnemy,
                minionDamagedThisEnemy,
                retaliationMinionValid,
                minionActivelyStrikingThisEnemy,
                ref retaliationMinionTarget);
            return;
        }

        if (minionAttacker != null && minionAttacker.IsAlive)
        {
            ResolveMinionHitAggro(
                playerDamagedThisEnemy,
                minionAttacker,
                ref retaliationMinionTarget);
        }
    }

    /// <summary>
    /// Player damage: engage the player unless the minion is actively striking this enemy,
    /// or already damaged this enemy first (peel blocked until a future taunt ability).
    /// </summary>
    private static void ResolvePlayerHitAggro(
        bool playerDamagedThisEnemy,
        bool minionDamagedThisEnemy,
        bool retaliationMinionValid,
        bool minionActivelyStrikingThisEnemy,
        ref Transform retaliationMinionTarget)
    {
        if (!playerDamagedThisEnemy)
            return;

        if (minionActivelyStrikingThisEnemy)
            return;

        if (minionDamagedThisEnemy && retaliationMinionValid)
            return;

        retaliationMinionTarget = null;
    }

    /// <summary>
    /// Minion damage: pull onto the minion unless the player has already damaged this enemy.
    /// </summary>
    private static void ResolveMinionHitAggro(
        bool playerDamagedThisEnemy,
        MinionCombatTarget minionAttacker,
        ref Transform retaliationMinionTarget)
    {
        if (playerDamagedThisEnemy)
            return;

        retaliationMinionTarget = minionAttacker.transform;
    }
}
