using UnityEngine;

/// <summary>
/// Display-only link between a horizontal timeline node and authored unlock data.
/// </summary>
public sealed class SkillTimelineNodeBinding
{
    public SkillDefinition Skill { get; set; }
    public SkillUnlockDefinition Unlock { get; set; }
    public SkillChoiceDefinition Choice { get; set; }
    public int ChoiceAssetIndex { get; set; } = -1;
    public int Level { get; set; } = 1;
    public int SlotAtLevel { get; set; }
    public SkillTimelineNodeUI.SkillTimelineNodeType TimelineNodeType { get; set; }
    public SkillTimelineNodeUI.SkillTimelineNodeState DisplayState { get; set; }

    public bool IsChoiceNode => Choice != null || ChoiceAssetIndex >= 0;

    public string ResolveSpineNodeId() =>
        Skill != null && Unlock != null
            ? SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock(Skill, Unlock)
            : null;
}
