using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Reusable runtime config for summons/minions: prefab, lifetime, combat rules, and optional base durability.
/// Motion / VFX for Soulforged Weapon are configured on <see cref="PlayerAbilityController"/> (minion presentation block).
/// </summary>
[CreateAssetMenu(fileName = "MinionDefinition_", menuName = "Desktop Idle Game/Minions/Minion Definition")]
public class MinionDefinition : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("Prefab")]
    [Tooltip("Prefab must include SoulforgedWeaponMinion, or a MinionUnit-based puppet such as SoulforgedWarriorMinion.")]
    public GameObject runtimePrefab;

    [Header("Lifetime")]
    [FormerlySerializedAs("summonDurationSeconds")]
    [Min(0.1f)]
    [Tooltip("Seconds before the summon despawns.")]
    public float summonDuration = 10f;

    [Header("Minion combat (Phase 2)")]
    public MinionCombatConfig combatConfig;

    [Header("Minion durability")]
    [Tooltip(
        "When > 0, max HP = owner MaxHP × this fraction (+ owner minion max life %). " +
        "When 0, uses flat baseMaxHealth instead (0 = no health).")]
    [Range(0f, 2f)]
    public float ownerMaxHealthFraction = 0f;

    [Tooltip(
        "Flat base max HP when ownerMaxHealthFraction is 0. " +
        "Scaled at runtime by owner minion max life % (see CharacterStats.FinalMinionMaxLifePercent).")]
    [Min(0f)]
    public float baseMaxHealth = 0f;

    [Header("Inherit — owner defenses")]
    public MinionDefensiveInheritance defensiveInheritance = MinionDefensiveInheritance.InheritFullOwner;

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        combatConfig = MinionCombatConfig.AfterDeserialize(combatConfig);
    }
}
