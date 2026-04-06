using UnityEngine;

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
    [Tooltip("Multiplier applied to Physical damage component (e.g. 1.25 = 125%).")]
    [Min(0f)] public float physicalDamageMultiplier = 0f;

    [Tooltip("Multiplier on average weapon Magical split damage. 0 = no extra magic scaling from this ability.")]
    [Min(0f)] public float magicalDamageMultiplier = 0f;

    [Tooltip("Multiplier applied to Ability Power (e.g. 0.25 = 25%).")]
    [Min(0f)] public float abilityPowerMultiplier = 0f;

    [Tooltip("Extra scaling on magical damage when the character's magic attack type is Fire (see CharacterStats CurrentMagicAttackType). 0 = ignore.")]
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
}