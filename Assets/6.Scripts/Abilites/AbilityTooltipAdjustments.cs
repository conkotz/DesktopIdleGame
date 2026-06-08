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
                weaponDamageMultiplier += 0.30f;
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
    /// Resource cost and cooldown shown in tooltips — mirrors <see cref="PlayerAbilityController"/> spend/cooldown rules.
    /// </summary>
    public static void ResolveTooltipResourceAndCooldown(
        AbilityDefinition def,
        SkillsManager skillsManager,
        CharacterStats stats,
        PlayerAbilityController abilityController,
        out float resourceCost,
        out string resourceLabel,
        out float cooldownSeconds)
    {
        resourceCost = 0f;
        resourceLabel = "Energy";
        cooldownSeconds = 0f;
        if (!def)
            return;

        float weaponMult = 0f;
        cooldownSeconds = Mathf.Max(0f, def.cooldown);
        ApplySkillTreeChoices(def, skillsManager, ref weaponMult, ref cooldownSeconds);

        if (string.Equals(def.abilityId, AbilityCombatPower.WhirlwindAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager != null
                ? skillsManager.GetSkillChoiceSelection(SkillType.Melee, "Lv15_0", -1)
                : -1;
            if (selected < 0 && skillsManager != null)
                selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 15, -1);
            if (selected < 0 && skillsManager != null)
                selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 18, -1);

            resourceLabel = "Energy / s";
            resourceCost = AbilityCombatPower.GetWhirlwindBaseChannelEnergyPerSecond(
                Mathf.Max(0f, def.energyCost),
                selected);
            if (abilityController != null && resourceCost > 0f)
            {
                float costMult = abilityController.GetTooltipAbilityEnergyCostMultiplier();
                resourceCost = Mathf.Max(0f, resourceCost * costMult);
            }
            if (stats != null && resourceCost > 0f)
                resourceCost = stats.ApplyEnergyEfficiencyToAbilityEnergyCost(def, resourceCost);

            return;
        }

        switch (def.GetResourceCostType())
        {
            case AbilityResourceCostType.Health:
                resourceLabel = "Health";
                resourceCost = Mathf.Max(0f, def.healthCost);
                break;
            case AbilityResourceCostType.Mana:
                resourceLabel = "Mana";
                resourceCost = Mathf.Max(0f, def.manaCost);
                break;
            case AbilityResourceCostType.Energy:
                resourceLabel = "Energy";
                resourceCost = Mathf.Max(0f, def.energyCost);
                if (abilityController != null && resourceCost > 0f)
                {
                    float costMult = abilityController.GetTooltipAbilityEnergyCostMultiplier();
                    resourceCost = Mathf.Max(0f, Mathf.Round(resourceCost * costMult));
                }
                if (stats != null && resourceCost > 0f)
                    resourceCost = stats.ApplyEnergyEfficiencyToAbilityEnergyCost(def, resourceCost);
                break;
        }

        if (stats != null)
            cooldownSeconds *= Mathf.Max(0.05f, 1f - stats.FinalAbilityCooldownReductionFraction);
    }

    /// <summary>Legacy name — <paramref name="energy"/> is the resolved resource amount (energy, mana, or health).</summary>
    public static void ResolveTooltipEnergyAndCooldown(
        AbilityDefinition def,
        SkillsManager skillsManager,
        CharacterStats stats,
        PlayerAbilityController abilityController,
        out float energy,
        out float cooldownSeconds)
    {
        ResolveTooltipResourceAndCooldown(
            def, skillsManager, stats, abilityController, out energy, out _, out cooldownSeconds);
    }

    public static float GetTooltipAbilityDamageMultiplier(PlayerAbilityController abilityController)
    {
        return abilityController != null ? abilityController.GetTooltipAbilityDamageMultiplier() : 1f;
    }
}
