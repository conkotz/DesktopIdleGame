using UnityEngine;

[CreateAssetMenu(fileName = "Ability_", menuName = "Game/Skills/Ability Definition")]
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

    [Tooltip("Multiplier applied to Ability Power (e.g. 0.25 = 25%).")]
    [Min(0f)] public float abilityPowerMultiplier = 0f;

    public SkillType sourceSkill;
    public int unlockLevel = 1;
}