using UnityEngine;

/// <summary>
/// Skill-tree choice tweaks and live combat modifiers mirrored in ability tooltips.
/// </summary>
public static class AbilityTooltipAdjustments
{
    public static void ApplySkillTreeChoices(
        AbilityDefinition def,
        SkillsManager skillsManager,
        ref float weaponDamageMultiplier,
        ref float cooldownSeconds)
    {
        if (!def || skillsManager == null)
            return;

        if (string.Equals(def.abilityId, "power_slash", System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
            if (selected == 0)
                weaponDamageMultiplier += 0.25f;
            else if (selected == 1)
                cooldownSeconds = Mathf.Max(0.01f, cooldownSeconds - 3f);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.AvatarOfTheForestAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Woodcutting, AbilityCombatPower.AvatarOfTheForestEnhancementParentSpineNodeId, -1);
            if (selected == 1)
                cooldownSeconds = Mathf.Max(0.01f, cooldownSeconds - 30f);
        }
    }

    /// <summary>
    /// Energy and cooldown shown in tooltips — mirrors <see cref="PlayerAbilityController"/> spend/cooldown rules.
    /// </summary>
    public static void ResolveTooltipEnergyAndCooldown(
        AbilityDefinition def,
        SkillsManager skillsManager,
        CharacterStats stats,
        PlayerAbilityController abilityController,
        out float energy,
        out float cooldownSeconds)
    {
        energy = 0f;
        cooldownSeconds = 0f;
        if (!def)
            return;

        float weaponMult = 0f;
        cooldownSeconds = Mathf.Max(0f, def.cooldown);
        ApplySkillTreeChoices(def, skillsManager, ref weaponMult, ref cooldownSeconds);

        energy = Mathf.Max(0f, def.energyCost);
        if (abilityController != null && energy > 0f)
        {
            float costMult = abilityController.GetTooltipAbilityEnergyCostMultiplier();
            energy = Mathf.Max(0f, Mathf.Round(energy * costMult));
        }

        if (stats != null)
            cooldownSeconds *= Mathf.Max(0.05f, 1f - stats.FinalAbilityCooldownReductionFraction);
    }

    public static float GetTooltipAbilityDamageMultiplier(PlayerAbilityController abilityController)
    {
        return abilityController != null ? abilityController.GetTooltipAbilityDamageMultiplier() : 1f;
    }
}
