using UnityEngine;

/// <summary>
/// Per-node-type vertical offsets for the horizontal skill timeline (relative to layout baseline).
/// Tune on <see cref="HorizontalSkillTreeScaffoldUI"/> — not on choice group prefabs.
/// </summary>
[System.Serializable]
public struct SkillTimelineChoiceGroupLayout
{
    [Tooltip("Ability milestone groups (baseline — usually 0).")]
    public float abilityNodeOffsetY;

    [Tooltip("Major passive choice groups.")]
    public float majorPassiveNodeOffsetY;

    [Tooltip("Capstone choice groups.")]
    public float capstoneNodeOffsetY;

    public static SkillTimelineChoiceGroupLayout Default => new()
    {
        abilityNodeOffsetY = 0f,
        majorPassiveNodeOffsetY = 8f,
        capstoneNodeOffsetY = 12f
    };

    public float GetNodeOffsetY(SkillTimelineNodeUI.SkillTimelineNodeType nodeType) =>
        nodeType switch
        {
            // Keep major passive choice rows on the exact same baseline as ability rows so
            // icon, title, and selection chrome line up across the tree.
            SkillTimelineNodeUI.SkillTimelineNodeType.MajorPassive => abilityNodeOffsetY,
            SkillTimelineNodeUI.SkillTimelineNodeType.Capstone => capstoneNodeOffsetY,
            SkillTimelineNodeUI.SkillTimelineNodeType.Ability => abilityNodeOffsetY,
            _ => 0f
        };
}
