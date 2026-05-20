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

    [Tooltip(
        "Minor Passive: preset stat row. Minor Unlock: flavor only (e.g. new fish); use title + description; no stat options — gameplay is enforced in content (e.g. fishing nodes). " +
        "Unlock / Ability / Major / Capstone: progression as before.")]
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

    [Tooltip("Optional centralized copy for this unlock row (overrides title/description in UI when fields are set).")]
    public SkillsAndAbilityPresentationDefinition presentation;

    [Header("Choices (optional, data-driven)")]
    [Tooltip(
        "Enhancement branch options (up to 4). Empty = no choice nodes. " +
        "Skill tree layout: 2 = side by side; 3 = [3rd, 1st, 2nd] left→right; 4 = [3rd, 1st, 2nd, 4th]. " +
        "Selection index matches list order (0 = first entry).")]
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

    [Tooltip("Optional centralized copy for this choice node.")]
    public SkillsAndAbilityPresentationDefinition presentation;
}