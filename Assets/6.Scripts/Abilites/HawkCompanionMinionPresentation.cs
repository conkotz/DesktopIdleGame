using System;
using UnityEngine;

/// <summary>
/// Flight orbit, dive swoop, and wander tuning for <see cref="HawkCompanionMinion"/>.
/// Serialized on <see cref="PlayerAbilityVfxController"/>.
/// </summary>
[Serializable]
public struct HawkCompanionMinionPresentation
{
    [Header("Targeting")]
    [Min(0.5f)]
    public float attackRange;

    [Header("Flight band")]
    [Tooltip("World units above the owner's position while circling.")]
    [Min(0.5f)]
    public float flightHeightAbovePlayer;

    [Tooltip("Horizontal move speed while patrolling.")]
    [Min(0.01f)]
    public float horizontalFlightSpeed;

    [Header("Idle wander (Soulforged Warrior style)")]
    [Min(0.01f)]
    public float wanderDistanceMin;

    [Min(0.01f)]
    public float wanderDistanceMax;

    [Tooltip("Max horizontal distance from the owner while wandering.")]
    [Min(0.5f)]
    public float maxWanderRadiusFromPlayer;

    [Tooltip("When horizontal distance from the owner exceeds this, the hawk flies back toward them.")]
    [Min(0.5f)]
    public float followStopDistance;

    [Tooltip("Teleport back to the owner when farther than this (matches Soulforged Warrior leash).")]
    [Min(1f)]
    public float maxLeashDistanceFromPlayer;

    [Min(0f)]
    public float idleWanderDelaySeconds;

    [Min(0.05f)]
    public float wanderArrivalDistance;

    [Header("Dive & return")]
    [Min(0.01f)]
    public float diveSpeed;

    [Min(0.01f)]
    public float returnSpeed;

    [Min(0.05f)]
    public float diveStrikeArrivalDistance;

    [Tooltip("World units the hawk carries upward along the dive arc after striking.")]
    [Min(0.1f)]
    public float swoopArcCarryDistance;

    [Tooltip("World units above the enemy root at the strike point.")]
    [Min(0f)]
    public float strikeHeightAboveEnemy;

    [Header("Idle bob")]
    public float wobbleAmplitudeX;
    public float wobbleAmplitudeY;

    [Min(0.01f)]
    public float wobbleFrequency;

    [Min(0f)]
    public float rotationWobbleDegrees;

    [Header("Visual")]
    [Min(0.05f)]
    public float visualWorldScale;

    public int spriteSortingOrder;

    public static HawkCompanionMinionPresentation Default => new HawkCompanionMinionPresentation
    {
        attackRange = 12f,
        flightHeightAbovePlayer = 2.35f,
        horizontalFlightSpeed = 3.375f,
        wanderDistanceMin = 1f,
        wanderDistanceMax = 3f,
        maxWanderRadiusFromPlayer = 4f,
        followStopDistance = 2.5f,
        maxLeashDistanceFromPlayer = AbilityCombatPower.SoulforgedWarriorMaxLeashDistance,
        idleWanderDelaySeconds = 0.1f,
        wanderArrivalDistance = 0.12f,
        diveSpeed = 18f,
        returnSpeed = 14f,
        diveStrikeArrivalDistance = 0.35f,
        swoopArcCarryDistance = 3f,
        strikeHeightAboveEnemy = 0.2f,
        wobbleAmplitudeX = 0.05f,
        wobbleAmplitudeY = 0.08f,
        wobbleFrequency = 1.6f,
        rotationWobbleDegrees = 4f,
        visualWorldScale = 1f,
        spriteSortingOrder = 6
    };

    public static HawkCompanionMinionPresentation Resolve(HawkCompanionMinionPresentation configured) =>
        configured.attackRange >= 0.5f ? configured : Default;
}
