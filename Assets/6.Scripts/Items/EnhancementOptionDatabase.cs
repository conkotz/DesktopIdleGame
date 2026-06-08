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
            EnsureSpecialOptionsPresent();
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
        return marker == null ||
               !string.Equals(marker.linkedScrollItemId, "intermediate_armour_scroll", System.StringComparison.OrdinalIgnoreCase);
    }

    private void PopulateDefaultOptions()
    {
        AddFlatTierLine("armour", "Armour", EnhancementScrollTargetStat.Armor, 5f,
            EnhancementScrollGearMask.AllArmorSlots, "basic_armour_scroll");
        AddFlatTierLine("health", "Health", EnhancementScrollTargetStat.Health, 5f,
            EnhancementScrollGearMask.AllArmorSlots, "basic_health_scroll");
        AddPercentTierValues("move_speed", "Move Speed", EnhancementScrollTargetStat.MoveSpeed,
            0.04f, 0.06f, 0.08f, EnhancementScrollGearMask.Boots, "basic_movespeed_scroll");

        AddFlatTierLine("physical", "Physical Damage", EnhancementScrollTargetStat.PhysicalDamage, 2f,
            EnhancementScrollGearMask.MeleeOrRangedWeapon, "basic_weapon_physical_scroll");
        AddFlatTierLine("fire", "Fire Damage", EnhancementScrollTargetStat.FireDamage, 2f,
            EnhancementScrollGearMask.MagicWeapon, "basic_weapon_fire_scroll");
        AddFlatTierLine("ice", "Ice Damage", EnhancementScrollTargetStat.IceDamage, 2f,
            EnhancementScrollGearMask.MagicWeapon, "basic_weapon_ice_scroll");
        AddFlatTierLine("lightning", "Lightning Damage", EnhancementScrollTargetStat.LightningDamage, 2f,
            EnhancementScrollGearMask.MagicWeapon, "basic_weapon_lightning_scroll");
        AddFlatTierLine("corruption", "Corruption Damage", EnhancementScrollTargetStat.CorruptionDamage, 2f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_corruption_scroll");

        AddPercentTierValues("crit_chance", "Crit Chance", EnhancementScrollTargetStat.CritChance,
            0.02f, 0.03f, 0.04f, EnhancementScrollGearMask.Weapon, "basic_weapon_crit_chance_scroll");
        AddPercentTierValues("crit_multi", "Crit Multi", EnhancementScrollTargetStat.CritMultiplier,
            0.04f, 0.06f, 0.08f, EnhancementScrollGearMask.Weapon, "basic_weapon_crit_multi_scroll");
        AddPercentTierValues("poison_chance", "Poison Chance", EnhancementScrollTargetStat.PoisonChance,
            0.03f, 0.04f, 0.05f, EnhancementScrollGearMask.Weapon, "basic_weapon_poison_chance_scroll");
        AddPercentTierValues("poison_multi", "Poison Multi", EnhancementScrollTargetStat.PoisonMultiplier,
            0.05f, 0.07f, 0.09f, EnhancementScrollGearMask.Weapon, "basic_weapon_poison_multi_scroll");
        AddPercentTierValues("burn_chance", "Burn Chance", EnhancementScrollTargetStat.BurnChance,
            0.03f, 0.04f, 0.05f, EnhancementScrollGearMask.Weapon, "basic_weapon_burn_chance_scroll");
        AddPercentTierValues("chill_chance", "Chill Chance", EnhancementScrollTargetStat.ChillChance,
            0.03f, 0.04f, 0.05f, EnhancementScrollGearMask.Weapon, "basic_weapon_chill_chance_scroll");
        AddPercentTierValues("shock_chance", "Shock Chance", EnhancementScrollTargetStat.ShockChance,
            0.03f, 0.04f, 0.05f, EnhancementScrollGearMask.Weapon, "basic_weapon_shock_chance_scroll");
        AddPercentTierValues("burn_multi", "Burn Multi", EnhancementScrollTargetStat.BurnMultiplier,
            0.05f, 0.07f, 0.09f, EnhancementScrollGearMask.Weapon, "basic_weapon_burn_multi_scroll");

        AddPercentTierValues("gather_speed", "Gather Speed", EnhancementScrollTargetStat.GatherSpeed,
            0.10f, 0.15f, 0.20f, EnhancementScrollGearMask.Tool, "basic_tool_gather_speed_scroll");
        AddPercentTierValues("gathering_grit", "Gathering Grit", EnhancementScrollTargetStat.GatheringGrit,
            0.04f, 0.06f, 0.08f, EnhancementScrollGearMask.Tool, "basic_tool_grit_scroll");
        AddPercentTierValues("stamina_efficiency", "Stamina Efficiency", EnhancementScrollTargetStat.StaminaEfficiency,
            0.06f, 0.08f, 0.10f, EnhancementScrollGearMask.Tool, "basic_tool_stamina_efficiency_scroll");

        AddChaosGamble("chaos_physical_gamble", "Chaos Physical Gamble",
            EnhancementScrollTargetStat.PhysicalDamage, 6f, EnhancementScrollGearMask.Weapon,
            "chaos_weapon_physical_scroll");
        AddChaosGamble("chaos_fire_gamble", "Chaos Fire Gamble",
            EnhancementScrollTargetStat.FireDamage, 6f, EnhancementScrollGearMask.MagicWeapon,
            "chaos_weapon_magic_scroll");
        AddChaosGamble("chaos_corruption_gamble", "Chaos Corruption Gamble",
            EnhancementScrollTargetStat.CorruptionDamage, 6f, EnhancementScrollGearMask.Weapon,
            "chaos_weapon_corruption_scroll");
        AddChaosGamble("chaos_health_gamble", "Chaos Health Gamble",
            EnhancementScrollTargetStat.Health, 15f, EnhancementScrollGearMask.AllArmorSlots,
            "chaos_health_scroll");

        EnsureSpecialOptionsPresent();
    }

    private void EnsureSpecialOptionsPresent()
    {
        if (GetById("slot_reduction") != null)
            return;

        options.Add(CreateSlotReductionOption());
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
        AddStandard($"{idPrefix}_basic", $"{displayPrefix} Basic", EnhancementTier.Basic, stat, basicValue, mask, basicScrollId);
        AddStandard($"{idPrefix}_intermediate", $"{displayPrefix} Intermediate", EnhancementTier.Intermediate, stat,
            basicValue * 2f, mask, EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Intermediate));
        AddStandard($"{idPrefix}_advanced", $"{displayPrefix} Advanced", EnhancementTier.Advanced, stat,
            basicValue * 3f, mask, EnhancementOptionScrollIds.TierScrollId(basicScrollId, EnhancementTier.Advanced));
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
