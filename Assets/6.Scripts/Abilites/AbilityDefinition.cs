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
    [Tooltip("Physical coefficient: 1 = 100%, −0.5 = −50% (reduction), 0 = omit in tooltip. Power Slash: adds hit Physical × this.")]
    public float physicalDamageMultiplier = 0f;

    [Tooltip("Magic coefficient: 1 = 100%, negative values reduce. Power Slash: adds hit Magic × this. 0 = omit in tooltip.")]
    [FormerlySerializedAs("magicalDamageMultiplier")]
    public float magicDamageMultiplier = 0f;

    [Tooltip("Ability Power coefficient: damage × (1 + AP × this / 100). 1 = +1% damage per Ability Power (100 AP = +100%). 0 = no AP scaling.")]
    public float abilityPowerMultiplier = 0f;

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
}