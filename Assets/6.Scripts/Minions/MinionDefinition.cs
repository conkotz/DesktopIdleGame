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
    [Tooltip("Prefab must include a SoulforgedWeaponMinion (or future runtime) on the root or a child.")]
    public GameObject runtimePrefab;

    [Header("Lifetime")]
    [FormerlySerializedAs("summonDurationSeconds")]
    [Min(0.1f)]
    [Tooltip("Seconds before the summon despawns.")]
    public float summonDuration = 10f;

    [Header("Minion combat (Phase 2)")]
    public MinionCombatConfig combatConfig;

    [Header("Minion durability (future)")]
    [Tooltip(
        "Flat base max HP for this minion type. 0 = no health until minion combat is implemented. " +
        "Scaled at runtime by owner minion max life % (see CharacterStats.FinalMinionMaxLifePercent).")]
    [Min(0f)]
    public float baseMaxHealth = 0f;

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        combatConfig = MinionCombatConfig.AfterDeserialize(combatConfig);
    }
}
