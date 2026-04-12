using UnityEngine;
using UnityEngine.Serialization;

public enum AbilityWeaponRequirement
{
    Any,
    Melee,
    Ranged,
    Magic
}

[CreateAssetMenu(fileName = "Ability_", menuName = "Desktop Idle Game/Skills/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    public string abilityId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    public float cooldown = 1f;
    [Min(0f)] public float energyCost = 0f;

    [Header("Scaling")]
    [Tooltip("Physical: >0 scales that portion of the hit (1.5 = 150% of rolled Physical). ≤0 leaves Physical at 100% of the hit.")]
    public float physicalDamageMultiplier = 0f;

    [Tooltip("Magic: >0 scales rolled Magic. ≤0 leaves Magic at 100% of the hit.")]
    [FormerlySerializedAs("magicalDamageMultiplier")]
    public float magicDamageMultiplier = 0f;

    [Tooltip("Corruption: >0 scales rolled Corruption. ≤0 leaves Corruption at 100% of the hit (no longer tied to Physical).")]
    public float corruptionDamageMultiplier = 0f;

    [Tooltip("Applied after per-type scaling: entire hit (all types combined) × this. 1 = no change. ≤0 treated as 1.")]
    public float allDamageMultiplier = 1f;

    /// <summary>All abilities use the same AP curve: ×(1 + AP×coef/100) with this coefficient (0.5 = +0.5% damage per AP).</summary>
    public const float StandardAbilityPowerCoefficient = 0.5f;

    [Tooltip("Extra scaling on magic damage when the character's magic attack type is Fire (see CharacterStats CurrentMagicAttackType). 0 = ignore.")]
    [Min(0f)] public float fireDamageMultiplier = 0f;

    [Tooltip("Extra scaling when magic attack type is Ice. 0 = ignore.")]
    [Min(0f)] public float iceDamageMultiplier = 0f;

    [Tooltip("Extra scaling when magic attack type is Lightning. 0 = ignore.")]
    [Min(0f)] public float lightningDamageMultiplier = 0f;

    [Tooltip("CP / scaling hook: weights expected poison DPS contribution from this ability (0 = none).")]
    [Min(0f)] public float poisonDamageMultiplier = 0f;

    [Tooltip("CP / scaling hook: weights expected bleed DPS contribution from this ability (0 = none).")]
    [Min(0f)] public float bleedDamageMultiplier = 0f;

    [Header("Weapon Requirement")]
    [Tooltip("Restricts this ability to a matching equipped main-hand weapon type.")]
    public AbilityWeaponRequirement requiredWeaponType = AbilityWeaponRequirement.Any;

    public SkillType sourceSkill;
    public int unlockLevel = 1;

    [Header("Summon (optional)")]
    [Tooltip(
        "When set, this ability uses the summon path in PlayerAbilityController: energy + cooldown apply, then SpectralWeaponMinion is spawned. " +
        "Scaling fields above are not used for that path (damage comes from MinionDefinition / MinionCombatConfig + owner minion stats).")]
    public MinionDefinition minionSpawnDefinition;

    /// <summary>True when casting should spawn a minion instead of the generic instant-hit damage pipeline.</summary>
    public bool SpawnsMinionOnCast => minionSpawnDefinition != null;

    /// <summary>≤0 uses1 (legacy / Inspector0): no extra all-damage pass; phys/mag/corruption coeffs only.</summary>
    public float GetEffectiveAllDamageMultiplier() =>
        allDamageMultiplier <= 0f ? 1f : allDamageMultiplier;

    /// <summary>&gt;0 scales rolled Physical; ≤0 keeps 100% of rolled Physical.</summary>
    public float GetPhysicalHitScalingMultiplier() =>
        physicalDamageMultiplier <= 0f ? 1f : physicalDamageMultiplier;

    /// <summary>&gt;0 scales rolled Magic; ≤0 keeps 100% of rolled Magic.</summary>
    public float GetMagicHitScalingMultiplier() =>
        magicDamageMultiplier <= 0f ? 1f : magicDamageMultiplier;

    /// <summary>&gt;0 scales rolled Corruption; ≤0 keeps 100% of rolled Corruption.</summary>
    public float GetCorruptionHitScalingMultiplier() =>
        corruptionDamageMultiplier <= 0f ? 1f : corruptionDamageMultiplier;
}
