using UnityEngine;

/// <summary>
/// Skill-tree choice tweaks mirrored in ability tooltips and <see cref="AbilityCombatPower"/> estimates.
/// </summary>
public static class AbilityTooltipAdjustments
{
    public static void ApplySkillTreeChoices(
        AbilityDefinition def,
        SkillsManager skillsManager,
        ref float physicalMultiplier,
        ref float cooldownSeconds)
    {
        if (!def || skillsManager == null)
            return;

        if (string.Equals(def.abilityId, "power_slash", System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
            if (selected == 0)
                physicalMultiplier += 0.25f;
            else if (selected == 1)
                cooldownSeconds = Mathf.Max(0.01f, cooldownSeconds - 5f);
        }
    }
}
