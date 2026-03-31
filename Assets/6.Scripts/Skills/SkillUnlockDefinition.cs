using UnityEngine;

[System.Serializable]
public class SkillUnlockDefinition
{
    public int requiredLevel = 1;
    public string title;
    [TextArea] public string description;
    public Sprite icon;

    public SkillUnlockType unlockType = SkillUnlockType.PassiveBonus;

    [Header("Optional refs")]
    public AbilityDefinition ability;
}