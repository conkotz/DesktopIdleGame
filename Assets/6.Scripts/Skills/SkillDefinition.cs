using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDefinition_", menuName = "Desktop Idle Game/Skills/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable id used by SkillsManager, XP routing, and save data.")]
    public SkillType skillType;

    [Tooltip("Display name shown in skills UI, headers, and summaries.")]
    public string displayName;

    [Tooltip("Long-form copy for detail panels, tooltips, and help text.")]
    [TextArea(3, 10)]
    public string description;

    [Header("Visuals")]
    [Tooltip("Icon for list rows, category headers, and compact UI.")]
    public Sprite icon;

    [Tooltip("Theme tint for selection frames, XP bars, category strips, etc.")]
    public Color themeColor = Color.white;

    [Header("Organization")]
    [Tooltip("High-level bucket for filtering and tabbed skills UI.")]
    public SkillCategory category = SkillCategory.Gathering;

    [Tooltip("Sort key within a category or list (lower = earlier). Independent of SkillType enum order.")]
    public int listSortOrder;

    [Header("Progression")]
    [Tooltip("Per-level unlock rows (data only; wire-up happens elsewhere).")]
    public List<SkillUnlockDefinition> unlocks = new();
}
