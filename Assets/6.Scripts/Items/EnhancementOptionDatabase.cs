using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/Enhancement Option Database", fileName = "EnhancementOptionDatabase")]
public sealed class EnhancementOptionDatabase : ScriptableObject
{
    private const string SchemaMarkerOptionId = "armour_intermediate";

    [Header("Scroll Icons By Tier")]
    [Tooltip("Used when creating or syncing enhancement scroll items from this database.")]
    public Sprite basicScrollIcon;
    public Sprite intermediateScrollIcon;
    public Sprite advancedScrollIcon;
    public Sprite chaosScrollIcon;

    [SerializeField] private List<EnhancementOptionEntry> options = new();

    public IReadOnlyList<EnhancementOptionEntry> Options => options;

    public EnhancementOptionEntry GetById(string optionId)
    {
        if (string.IsNullOrWhiteSpace(optionId))
            return null;

        for (int i = 0; i < options.Count; i++)
        {
            EnhancementOptionEntry entry = options[i];
            if (entry != null && string.Equals(entry.optionId, optionId, System.StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    public List<EnhancementOptionEntry> GetForTrack(EnhancementTrack track)
    {
        var result = new List<EnhancementOptionEntry>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            EnhancementOptionEntry entry = options[i];
            if (entry != null && entry.track == track)
                result.Add(entry);
        }

        return result;
    }

    public void EnsureDefaults()
    {
        options ??= new List<EnhancementOptionEntry>(128);
        MigrateLegacyChaosOptionIds();
        if (NeedsSchemaRefresh())
        {
            options.Clear();
            PopulateDefaultOptions();
            EnhancementOptionResolver.InvalidateCache();
            return;
        }

        if (options.Count == 0)
            PopulateDefaultOptions();
        else
        {
            EnsureSpecialOptionsPresent();
            EnsureSpellAndElementalPercentOptionsPresent();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Populate Default Options")]
    private void PopulateDefaultOptionsMenu()
    {
        options ??= new List<EnhancementOptionEntry>(128);
        options.Clear();
        PopulateDefaultOptions();
        EnhancementOptionResolver.InvalidateCache();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private bool NeedsSchemaRefresh()
    {
        EnhancementOptionEntry marker = GetById(SchemaMarkerOptionId);
        if (marker == null ||
            !string.Equals(marker.linkedScrollItemId, "intermediate_armour_scroll", System.StringComparison.OrdinalIgnoreCase))
            return true;

        EnhancementOptionEntry iceBasic = GetById("ice_basic");
        if (iceBasic == null || !iceBasic.usesWeaponWeightScaling)
            return true;

        if (GetById("attack_speed_basic") == null)
            return true;

        EnhancementOptionEntry manaBasic = GetById("mana_basic");
        if (manaBasic == null || (manaBasic.allowedGearTypes & EnhancementScrollGearMask.Jewelry) != 0)
            return true;

        EnhancementOptionEntry energyBasic = GetById("energy_efficiency_basic");
        if (energyBasic != null && energyBasic.modifierValue > 0.01f)
            return true;

        EnhancementOptionEntry fireBasic = GetById("fire_basic");
        if (fireBasic != null && fireBasic.allowedGearTypes == EnhancementScrollGearMask.MagicWeapon)
            return true;

        EnhancementOptionEntry critMultiBasic = GetById("crit_multi_basic");
        if (critMultiBasic == null || !critMultiBasic.usesWeaponWeightScaling)
            return true;

        return GetById("spell_damage_basic") == null;
    }

    private void PopulateDefaultOptions()
    {
        AddFlatTierLine("armour", "Armour", EnhancementScrollTargetStat.Armour, 5f,
            EnhancementScrollGearMask.AllArmourSlots, "basic_armour_scroll");
        AddFlatTierLine("health", "Health", EnhancementScrollTargetStat.Health, 5f,
            EnhancementScrollGearMask.AllArmourSlots, "basic_health_scroll");
        AddFlatTierLine("magic_resist", "Magic Res", EnhancementScrollTargetStat.MagicResist, 5f,
            EnhancementScrollGearMask.AllArmourSlots, "basic_magic_resist_scroll");
        AddFlatTierLine("corruption_resist", "Corruption Res", EnhancementScrollTargetStat.CorruptionResist, 5f,
            EnhancementScrollGearMask.AllArmourSlots, "basic_corruption_resist_scroll");
        AddPercentTierValues("energy_efficiency", "Energy Efficiency", EnhancementScrollTargetStat.EnergyEfficiency,
            0.005f, 0.007f, 0.01f, EnhancementScrollGearMask.AllArmourSlots, "basic_energy_efficiency_scroll");
        AddFlatTierLine("flat_guard", "Flat Guard", EnhancementScrollTargetStat.FlatGuard, 5f,
            EnhancementScrollGearMask.AllArmourSlots, "basic_flat_guard_scroll");
        AddFlatTierValues("mana", "Mana", EnhancementScrollTargetStat.Mana, 7f, 10f, 13f,
            EnhancementScrollGearMask.AllArmourSlots, "basic_mana_scroll");
        AddFlatTierValues("mana_regen", "Mana Regen", EnhancementScrollTargetStat.ManaRegen, 0.5f, 0.7f, 1f,
            EnhancementScrollGearMask.AllArmourSlots, "basic_mana_regen_scroll");
        AddPercentTierValues("move_speed", "Move Speed", EnhancementScrollTargetStat.MoveSpeed,
            0.04f, 0.06f, 0.08f, EnhancementScrollGearMask.Boots, "basic_movespeed_scroll");

        AddWeightScaledFlatTierLine("physical", "Physical Damage", EnhancementScrollTargetStat.PhysicalDamage, 2f,
            EnhancementScrollGearMask.MeleeOrRangedWeapon, "basic_weapon_physical_scroll");
        AddWeightScaledFlatTierLine("fire", "Fire Damage", EnhancementScrollTargetStat.FireDamage, 2f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_fire_scroll");
        AddWeightScaledFlatTierLine("ice", "Ice Damage", EnhancementScrollTargetStat.IceDamage, 2f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_ice_scroll");
        AddWeightScaledFlatTierLine("lightning", "Lightning Damage", EnhancementScrollTargetStat.LightningDamage, 2f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_lightning_scroll");
        AddWeightScaledFlatTierLine("corruption", "Corruption Damage", EnhancementScrollTargetStat.CorruptionDamage, 2f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_corruption_scroll");

        AddWeightScaledAttackSpeedLine();

        AddPercentTierValues("crit_chance", "Crit Chance", EnhancementScrollTargetStat.CritChance,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.Weapon, "basic_weapon_crit_chance_scroll");
        AddWeightScaledPercentTierValues("crit_multi", "Crit Multi", EnhancementScrollTargetStat.CritMultiplier,
            0.05f, 0.07f, 0.09f, EnhancementScrollGearMask.Weapon, "basic_weapon_crit_multi_scroll");
        AddPercentTierValues("poison_chance", "Poison Chance", EnhancementScrollTargetStat.PoisonChance,
            0.03f, 0.04f, 0.05f, EnhancementScrollGearMask.Weapon, "basic_weapon_poison_chance_scroll");
        AddWeightScaledPercentTierValues("poison_multi", "Poison Multi", EnhancementScrollTargetStat.PoisonMultiplier,
            0.05f, 0.07f, 0.09f, EnhancementScrollGearMask.Weapon, "basic_weapon_poison_multi_scroll");
        AddPercentTierValues("burn_chance", "Burn Chance", EnhancementScrollTargetStat.BurnChance,
            0.03f, 0.04f, 0.05f, EnhancementScrollGearMask.Weapon, "basic_weapon_burn_chance_scroll");
        AddPercentTierValues("chill_chance", "Chill Chance", EnhancementScrollTargetStat.ChillChance,
            0.03f, 0.04f, 0.05f, EnhancementScrollGearMask.Weapon, "basic_weapon_chill_chance_scroll");
        AddPercentTierValues("shock_chance", "Shock Chance", EnhancementScrollTargetStat.ShockChance,
            0.03f, 0.04f, 0.05f, EnhancementScrollGearMask.Weapon, "basic_weapon_shock_chance_scroll");
        AddWeightScaledPercentTierValues("burn_multi", "Burn Multi", EnhancementScrollTargetStat.BurnMultiplier,
            0.05f, 0.07f, 0.09f, EnhancementScrollGearMask.Weapon, "basic_weapon_burn_multi_scroll");

        AddPercentTierValues("spell_damage", "Spell Damage", EnhancementScrollTargetStat.SpellDamage,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.MagicWeapon, "basic_weapon_spell_damage_scroll");
        AddPercentTierValues("fire_percent", "Fire Damage %", EnhancementScrollTargetStat.FireDamagePercent,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.MagicWeapon, "basic_weapon_fire_percent_scroll");
        AddPercentTierValues("ice_percent", "Ice Damage %", EnhancementScrollTargetStat.IceDamagePercent,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.MagicWeapon, "basic_weapon_ice_percent_scroll");
        AddPercentTierValues("lightning_percent", "Lightning Damage %", EnhancementScrollTargetStat.LightningDamagePercent,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.MagicWeapon, "basic_weapon_lightning_percent_scroll");

        AddPercentTierValues("gather_speed", "Gather Speed", EnhancementScrollTargetStat.GatherSpeed,
            0.10f, 0.15f, 0.20f, EnhancementScrollGearMask.Tool, "basic_tool_gather_speed_scroll");
        AddPercentTierValues("gathering_grit", "Gathering Grit", EnhancementScrollTargetStat.GatheringGrit,
            0.04f, 0.06f, 0.08f, EnhancementScrollGearMask.Tool, "basic_tool_grit_scroll");
        AddPercentTierValues("stamina_efficiency", "Stamina Efficiency", EnhancementScrollTargetStat.StaminaEfficiency,
            0.06f, 0.08f, 0.10f, EnhancementScrollGearMask.Tool, "basic_tool_stamina_efficiency_scroll");

        AddWeightScaledChaosGamble("chaos_physical_gamble", "Chaos Physical Gamble",
            EnhancementScrollTargetStat.PhysicalDamage, 6f, EnhancementScrollGearMask.Weapon,
            "chaos_weapon_physical_scroll");
        AddWeightScaledChaosGamble("chaos_fire_gamble", "Chaos Fire Gamble",
            EnhancementScrollTargetStat.FireDamage, 6f, EnhancementScrollGearMask.Weapon,
            "chaos_weapon_magic_scroll");
        AddWeightScaledChaosGamble("chaos_corruption_gamble", "Chaos Corruption Gamble",
            EnhancementScrollTargetStat.CorruptionDamage, 6f, EnhancementScrollGearMask.Weapon,
            "chaos_weapon_corruption_scroll");
        AddChaosGamble("chaos_health_gamble", "Chaos Health Gamble",
            EnhancementScrollTargetStat.Health, 15f, EnhancementScrollGearMask.AllArmourSlots,
            "chaos_health_scroll");

        EnsureSpecialOptionsPresent();
    }

    private void EnsureSpecialOptionsPresent()
    {
        if (GetById("slot_reduction") == null)
            options.Add(CreateSlotReductionOption());
    }

    private void EnsureSpellAndElementalPercentOptionsPresent()
    {
        if (GetById("spell_damage_basic") != null)
            return;

        AddPercentTierValues("spell_damage", "Spell Damage", EnhancementScrollTargetStat.SpellDamage,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.MagicWeapon, "basic_weapon_spell_damage_scroll");
        AddPercentTierValues("fire_percent", "Fire Damage %", EnhancementScrollTargetStat.FireDamagePercent,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.MagicWeapon, "basic_weapon_fire_percent_scroll");
        AddPercentTierValues("ice_percent", "Ice Damage %", EnhancementScrollTargetStat.IceDamagePercent,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.MagicWeapon, "basic_weapon_ice_percent_scroll");
        AddPercentTierValues("lightning_percent", "Lightning Damage %", EnhancementScrollTargetStat.LightningDamagePercent,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.MagicWeapon, "basic_weapon_lightning_percent_scroll");
        EnhancementOptionResolver.InvalidateCache();
    }

    private static EnhancementOptionEntry CreateSlotReductionOption()
    {
        return new EnhancementOptionEntry
        {
            optionId = "slot_reduction",
            displayName = "Slot Reduction",
            track = EnhancementTrack.Special,
            tier = EnhancementTier.Basic,
            targetStat = EnhancementScrollTargetStat.UpgradeSlotReduction,
            modifierKind = EnhancementScrollModifierKind.Flat,
            modifierValue = 1f,
            allowedGearTypes = EnhancementScrollGearMask.AllGear,
            successChance = 0.5f,
            consumeSlotOnFailure = false,
            failureOutcome = EnhancementScrollFailureOutcome.Nothing,
            linkedScrollItemId = "slot_reduction_scroll",
        };
    }

    private void AddFlatTierLine(
        string idPrefix,
        string displayPrefix,
        EnhancementScrollTargetStat stat,
        float basicValue,
        EnhancementScrollGearMask mask,
        string basicScrollId)
    {
        AddFlatTierValues(idPrefix, displayPrefix, stat, basicValue, basicValue * 2f, basicValue * 3f, mask, basicScrollId);
    }

    private void AddFlatTierValues(
        string idPrefix,
        string displayPrefix,
        EnhancementScrollTargetStat stat,
        float basicValue,
        float intermediateValue,
        float advancedValue,
        EnhancementScrollGearMask mask,
        string basicScrollId)
    {
        AddStandard($"{idPrefix}_basic", $"{displayPrefix} Basic", EnhancementTier.Basic, stat, basicValue, mask, basicScrollId);
        AddStandard($"{idPrefix}_intermediate", $"{displayPrefix} Intermediate", EnhancementTier.Intermediate, stat,
            intermediateValue, mask, EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Intermediate));
        AddStandard($"{idPrefix}_advanced", $"{displayPrefix} Advanced", EnhancementTier.Advanced, stat,
            advancedValue, mask, EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Advanced));
    }

    private void AddWeightScaledAttackSpeedLine()
    {
        const string basicScrollId = "basic_weapon_attack_speed_scroll";
        AddWeightScaledStandard("attack_speed_basic", "Attack Speed Basic", EnhancementTier.Basic,
            EnhancementScrollTargetStat.AttackSpeed, 0.03f,
            EnhancementWeightValues.Explicit(0.02f, 0.03f, 0.04f),
            EnhancementScrollGearMask.Weapon, basicScrollId, EnhancementScrollModifierKind.Percent);
        AddWeightScaledStandard("attack_speed_intermediate", "Attack Speed Intermediate", EnhancementTier.Intermediate,
            EnhancementScrollTargetStat.AttackSpeed, 0.04f,
            EnhancementWeightValues.Explicit(0.03f, 0.04f, 0.05f),
            EnhancementScrollGearMask.Weapon,
            EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Intermediate),
            EnhancementScrollModifierKind.Percent);
        AddWeightScaledStandard("attack_speed_advanced", "Attack Speed Advanced", EnhancementTier.Advanced,
            EnhancementScrollTargetStat.AttackSpeed, 0.05f,
            EnhancementWeightValues.Explicit(0.04f, 0.05f, 0.06f),
            EnhancementScrollGearMask.Weapon,
            EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Advanced),
            EnhancementScrollModifierKind.Percent);
    }

    private void AddWeightScaledFlatTierLine(
        string idPrefix,
        string displayPrefix,
        EnhancementScrollTargetStat stat,
        float basicMediumValue,
        EnhancementScrollGearMask mask,
        string basicScrollId)
    {
        AddWeightScaledStandard($"{idPrefix}_basic", $"{displayPrefix} Basic", EnhancementTier.Basic, stat,
            basicMediumValue, EnhancementWeightValues.FlatDamageForTier(EnhancementTier.Basic, basicMediumValue), mask,
            basicScrollId);
        float intermediateMedium = basicMediumValue * 2f;
        AddWeightScaledStandard($"{idPrefix}_intermediate", $"{displayPrefix} Intermediate", EnhancementTier.Intermediate,
            stat, intermediateMedium, EnhancementWeightValues.FlatDamageForTier(EnhancementTier.Intermediate, intermediateMedium),
            mask, EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Intermediate));
        float advancedMedium = basicMediumValue * 3f;
        AddWeightScaledStandard($"{idPrefix}_advanced", $"{displayPrefix} Advanced", EnhancementTier.Advanced, stat,
            advancedMedium, EnhancementWeightValues.FlatDamageForTier(EnhancementTier.Advanced, advancedMedium), mask,
            EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Advanced));
    }

    private void AddWeightScaledPercentTierValues(
        string idPrefix,
        string displayPrefix,
        EnhancementScrollTargetStat stat,
        float basicMediumValue,
        float intermediateMediumValue,
        float advancedMediumValue,
        EnhancementScrollGearMask mask,
        string basicScrollId)
    {
        AddWeightScaledStandard($"{idPrefix}_basic", $"{displayPrefix} Basic", EnhancementTier.Basic, stat,
            basicMediumValue, EnhancementWeightValues.PercentStep(basicMediumValue), mask, basicScrollId,
            EnhancementScrollModifierKind.Percent);
        AddWeightScaledStandard($"{idPrefix}_intermediate", $"{displayPrefix} Intermediate", EnhancementTier.Intermediate,
            stat, intermediateMediumValue, EnhancementWeightValues.PercentStep(intermediateMediumValue), mask,
            EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Intermediate),
            EnhancementScrollModifierKind.Percent);
        AddWeightScaledStandard($"{idPrefix}_advanced", $"{displayPrefix} Advanced", EnhancementTier.Advanced, stat,
            advancedMediumValue, EnhancementWeightValues.PercentStep(advancedMediumValue), mask,
            EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Advanced),
            EnhancementScrollModifierKind.Percent);
    }

    private void AddPercentTierLine(
        string idPrefix,
        string displayPrefix,
        EnhancementScrollTargetStat stat,
        float basicValue,
        EnhancementScrollGearMask mask,
        string basicScrollId)
    {
        AddPercentTierValues(idPrefix, displayPrefix, stat, basicValue, basicValue * 2f, basicValue * 3f, mask, basicScrollId);
    }

    private void AddPercentTierValues(
        string idPrefix,
        string displayPrefix,
        EnhancementScrollTargetStat stat,
        float basicValue,
        float intermediateValue,
        float advancedValue,
        EnhancementScrollGearMask mask,
        string basicScrollId)
    {
        AddStandard($"{idPrefix}_basic", $"{displayPrefix} Basic", EnhancementTier.Basic, stat, basicValue, mask,
            basicScrollId, EnhancementScrollModifierKind.Percent);
        AddStandard($"{idPrefix}_intermediate", $"{displayPrefix} Intermediate", EnhancementTier.Intermediate, stat,
            intermediateValue, mask, EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Intermediate),
            EnhancementScrollModifierKind.Percent);
        AddStandard($"{idPrefix}_advanced", $"{displayPrefix} Advanced", EnhancementTier.Advanced, stat,
            advancedValue, mask, EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Advanced),
            EnhancementScrollModifierKind.Percent);
    }

    private void MigrateLegacyChaosOptionIds()
    {
        MigrateOptionId("corruption_physical_gamble", "chaos_physical_gamble");
        MigrateOptionId("corruption_fire_gamble", "chaos_fire_gamble");
        MigrateOptionId("corruption_magic_gamble", "chaos_fire_gamble");
        MigrateOptionId("corruption_damage_gamble", "chaos_corruption_gamble");
        MigrateOptionId("corruption_health_gamble", "chaos_health_gamble");
    }

    private void MigrateOptionId(string oldId, string newId)
    {
        for (int i = 0; i < options.Count; i++)
        {
            EnhancementOptionEntry entry = options[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.optionId))
                continue;

            if (string.Equals(entry.optionId, oldId, System.StringComparison.OrdinalIgnoreCase))
                entry.optionId = newId;
        }
    }

    private void AddChaosGamble(
        string id,
        string displayName,
        EnhancementScrollTargetStat stat,
        float value,
        EnhancementScrollGearMask mask,
        string linkedScrollId)
    {
        options.Add(new EnhancementOptionEntry
        {
            optionId = id,
            displayName = displayName,
            track = EnhancementTrack.Corruption,
            tier = EnhancementTier.Intermediate,
            targetStat = stat,
            modifierKind = EnhancementScrollModifierKind.Flat,
            modifierValue = value,
            allowedGearTypes = mask,
            successChance = 0.35f,
            consumeSlotOnFailure = true,
            failureOutcome = EnhancementScrollFailureOutcome.DestroyItem,
            destroyChanceOnFailure = 0.5f,
            linkedScrollItemId = linkedScrollId,
        });
    }

    private void AddWeightScaledChaosGamble(
        string id,
        string displayName,
        EnhancementScrollTargetStat stat,
        float mediumValue,
        EnhancementScrollGearMask mask,
        string linkedScrollId)
    {
        options.Add(new EnhancementOptionEntry
        {
            optionId = id,
            displayName = displayName,
            track = EnhancementTrack.Corruption,
            tier = EnhancementTier.Intermediate,
            targetStat = stat,
            modifierKind = EnhancementScrollModifierKind.Flat,
            modifierValue = mediumValue,
            usesWeaponWeightScaling = true,
            weightValues = EnhancementWeightValues.ChaosFlatDamage(mediumValue),
            allowedGearTypes = mask,
            successChance = 0.35f,
            consumeSlotOnFailure = true,
            failureOutcome = EnhancementScrollFailureOutcome.DestroyItem,
            destroyChanceOnFailure = 0.5f,
            linkedScrollItemId = linkedScrollId,
        });
    }

    private void AddStandard(
        string id,
        string displayName,
        EnhancementTier tier,
        EnhancementScrollTargetStat stat,
        float value,
        EnhancementScrollGearMask mask,
        string linkedScrollId,
        EnhancementScrollModifierKind kind = EnhancementScrollModifierKind.Flat)
    {
        options.Add(new EnhancementOptionEntry
        {
            optionId = id,
            displayName = displayName,
            track = EnhancementTrack.Standard,
            tier = tier,
            targetStat = stat,
            modifierKind = kind,
            modifierValue = value,
            allowedGearTypes = mask,
            successChance = 0.5f,
            consumeSlotOnFailure = true,
            failureOutcome = EnhancementScrollFailureOutcome.Nothing,
            linkedScrollItemId = linkedScrollId,
        });
    }

    private void AddWeightScaledStandard(
        string id,
        string displayName,
        EnhancementTier tier,
        EnhancementScrollTargetStat stat,
        float mediumValue,
        EnhancementWeightValues weightValues,
        EnhancementScrollGearMask mask,
        string linkedScrollId,
        EnhancementScrollModifierKind kind = EnhancementScrollModifierKind.Flat)
    {
        options.Add(new EnhancementOptionEntry
        {
            optionId = id,
            displayName = displayName,
            track = EnhancementTrack.Standard,
            tier = tier,
            targetStat = stat,
            modifierKind = kind,
            modifierValue = mediumValue,
            usesWeaponWeightScaling = true,
            weightValues = weightValues,
            allowedGearTypes = mask,
            successChance = 0.5f,
            consumeSlotOnFailure = true,
            failureOutcome = EnhancementScrollFailureOutcome.Nothing,
            linkedScrollItemId = linkedScrollId,
        });
    }

    public Sprite GetScrollIconForOption(EnhancementOptionEntry option)
    {
        if (option == null)
            return null;

        if (option.track == EnhancementTrack.Corruption)
            return chaosScrollIcon != null ? chaosScrollIcon : basicScrollIcon;

        return option.tier switch
        {
            EnhancementTier.Intermediate => intermediateScrollIcon != null ? intermediateScrollIcon : basicScrollIcon,
            EnhancementTier.Advanced => advancedScrollIcon != null ? advancedScrollIcon : basicScrollIcon,
            _ => basicScrollIcon,
        };
    }
}
