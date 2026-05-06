using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

[System.Serializable]
public class SkillUnlockDefinition
{
    public int requiredLevel = 1;
    public string title;
    [TextArea] public string description;
    public Sprite icon;

    public SkillUnlockType unlockType = SkillUnlockType.MinorPassive;
    [Tooltip("Preset stat package for MinorPassive melee nodes. Ignored for non-MinorPassive rows.")]
    public MeleeMinorNodeStatOption meleeMinorStatOption = MeleeMinorNodeStatOption.None;
    [Tooltip("Preset stat package for MinorPassive ranged nodes. Ignored for non-MinorPassive rows.")]
    public RangedMinorNodeStatOption rangedMinorStatOption = RangedMinorNodeStatOption.None;
    [Tooltip("Preset stat package for MinorPassive woodcutting nodes. Ignored for non-MinorPassive rows.")]
    public WoodcuttingMinorNodeStatOption woodcuttingMinorStatOption = WoodcuttingMinorNodeStatOption.None;
    [Tooltip("Preset stat package for MinorPassive mining nodes. Ignored for non-MinorPassive rows.")]
    public MiningMinorNodeStatOption miningMinorStatOption = MiningMinorNodeStatOption.None;
    [Tooltip("Preset stat package for MinorPassive fishing nodes. Ignored for non-MinorPassive rows.")]
    public FishingMinorNodeStatOption fishingMinorStatOption = FishingMinorNodeStatOption.None;
    [Tooltip("Preset stat package for MinorPassive endurance nodes. Ignored for non-MinorPassive rows.")]
    public EnduranceMinorNodeStatOption enduranceMinorStatOption = EnduranceMinorNodeStatOption.None;
    [Tooltip("Preset stat package for MinorPassive magic nodes. Ignored for non-MinorPassive rows.")]
    public MagicMinorNodeStatOption magicMinorStatOption = MagicMinorNodeStatOption.None;
    [FormerlySerializedAs("skillMinorStatOption")]
    [HideInInspector] public SkillMinorStatOption legacySkillMinorStatOption = SkillMinorStatOption.None;

    [Header("Optional refs")]
    public AbilityDefinition ability;

    [Header("Choices (optional, data-driven)")]
    [Tooltip("If this unlock creates branch choices, define them here. Empty = no choice nodes for this unlock.")]
    public List<SkillChoiceDefinition> choices = new();
}

[System.Serializable]
public class SkillChoiceDefinition
{
    [Tooltip("Level where this choice node appears. 0 = same level as the parent unlock.")]
    public int requiredLevel = 0;

    [Tooltip("Display title for this choice.")]
    public string title;

    [TextArea]
    [Tooltip("Description shown in tooltip.")]
    public string description;

    [Tooltip("Optional icon for this choice (future use in node visuals/tooltip).")]
    public Sprite icon;

    [Header("Optional refs")]
    [Tooltip("Optional ability linked to this choice.")]
    public AbilityDefinition ability;
}