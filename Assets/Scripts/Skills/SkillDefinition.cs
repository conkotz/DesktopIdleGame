using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDefinition_", menuName = "Game/Skills/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    [Header("Identity")]
    public SkillType skillType;
    public string displayName;
    [TextArea] public string description;

    [Header("Visuals")]
    public Sprite icon;
    public Color themeColor = Color.white;

    [Header("Category")]
    public SkillCategory category = SkillCategory.Gathering;

    [Header("Progression")]
    public List<SkillUnlockDefinition> unlocks = new();
}