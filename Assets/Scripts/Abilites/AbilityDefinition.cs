using UnityEngine;

[CreateAssetMenu(fileName = "Ability_", menuName = "Game/Skills/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    public string abilityId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    public float cooldown = 1f;
    public SkillType sourceSkill;
    public int unlockLevel = 1;
}