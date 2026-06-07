using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/Enhancement Option Database", fileName = "EnhancementOptionDatabase")]
public sealed class EnhancementOptionDatabase : ScriptableObject
{
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
        options ??= new List<EnhancementOptionEntry>(96);
        if (options.Count == 0)
            PopulateDefaultOptions();
        else
            EnsureSpecialOptionsPresent();
    }

#if UNITY_EDITOR
    [ContextMenu("Populate Default Options")]
    private void PopulateDefaultOptionsMenu()
    {
        options ??= new List<EnhancementOptionEntry>(96);
        options.Clear();
        PopulateDefaultOptions();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void PopulateDefaultOptions()
    {
        AddFlatTierLine("armour", "Armour", EnhancementScrollTargetStat.Armor, 5f,
            EnhancementScrollGearMask.AllArmorSlots, "basic_armour_scroll");
        AddFlatTierLine("health", "Health", EnhancementScrollTargetStat.Health, 5f,
            EnhancementScrollGearMask.AllArmorSlots, "basic_health_scroll");
        AddPercentTierLine("move_speed", "Move Speed", EnhancementScrollTargetStat.MoveSpeed, 0.07f,
            EnhancementScrollGearMask.Boots, "basic_movespeed_scroll");

        AddFlatTierLine("physical", "Physical Damage", EnhancementScrollTargetStat.PhysicalDamage, 2f,
            EnhancementScrollGearMask.MeleeOrRangedWeapon, "basic_weapon_physical_scroll");
        AddFlatTierLine("magic", "Magic Damage", EnhancementScrollTargetStat.MagicDamage, 2f,
            EnhancementScrollGearMask.MagicWeapon, "basic_weapon_magic_scroll");
        AddFlatTierLine("corruption", "Corruption Damage", EnhancementScrollTargetStat.CorruptionDamage, 2f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_corruption_scroll");

        AddPercentTierLine("crit_chance", "Crit Chance", EnhancementScrollTargetStat.CritChance, 0.03f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_crit_chance_scroll");
        AddPercentTierLine("crit_multi", "Crit Multi", EnhancementScrollTargetStat.CritMultiplier, 0.05f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_crit_multi_scroll");
        AddPercentTierLine("poison_chance", "Poison Chance", EnhancementScrollTargetStat.PoisonChance, 0.03f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_poison_chance_scroll");
        AddPercentTierLine("poison_multi", "Poison Multi", EnhancementScrollTargetStat.PoisonMultiplier, 0.05f,
            EnhancementScrollGearMask.Weapon, "basic_weapon_poison_multi_scroll");

        AddPercentTierLine("gather_speed", "Gather Speed", EnhancementScrollTargetStat.GatherSpeed, 0.15f,
            EnhancementScrollGearMask.Tool, "basic_tool_gather_speed_scroll");
        AddPercentTierLine("gathering_grit", "Gathering Grit", EnhancementScrollTargetStat.GatheringGrit, 0.05f,
            EnhancementScrollGearMask.Tool, "basic_tool_grit_scroll");
        AddPercentTierLine("stamina_efficiency", "Stamina Efficiency", EnhancementScrollTargetStat.StaminaEfficiency, 0.06f,
            EnhancementScrollGearMask.Tool, "basic_tool_stamina_efficiency_scroll");

        AddCorruptionGamble("corruption_physical_gamble", "Corruption Physical Gamble",
            EnhancementScrollTargetStat.PhysicalDamage, 6f, EnhancementScrollGearMask.Weapon,
            "chaos_weapon_physical_scroll");
        AddCorruptionGamble("corruption_magic_gamble", "Corruption Magic Gamble",
            EnhancementScrollTargetStat.MagicDamage, 6f, EnhancementScrollGearMask.MagicWeapon,
            "chaos_weapon_magic_scroll");
        AddCorruptionGamble("corruption_damage_gamble", "Corruption Damage Gamble",
            EnhancementScrollTargetStat.CorruptionDamage, 6f, EnhancementScrollGearMask.Weapon,
            "chaos_weapon_corruption_scroll");
        AddCorruptionGamble("corruption_health_gamble", "Corruption Health Gamble",
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
            basicValue * 2f, mask, null);
        AddStandard($"{idPrefix}_advanced", $"{displayPrefix} Advanced", EnhancementTier.Advanced, stat,
            basicValue * 3f, mask, null);
    }

    private void AddPercentTierLine(
        string idPrefix,
        string displayPrefix,
        EnhancementScrollTargetStat stat,
        float basicValue,
        EnhancementScrollGearMask mask,
        string basicScrollId)
    {
        AddStandard($"{idPrefix}_basic", $"{displayPrefix} Basic", EnhancementTier.Basic, stat, basicValue, mask,
            basicScrollId, EnhancementScrollModifierKind.Percent);
        AddStandard($"{idPrefix}_intermediate", $"{displayPrefix} Intermediate", EnhancementTier.Intermediate, stat,
            basicValue * 2f, mask, null, EnhancementScrollModifierKind.Percent);
        AddStandard($"{idPrefix}_advanced", $"{displayPrefix} Advanced", EnhancementTier.Advanced, stat,
            basicValue * 3f, mask, null, EnhancementScrollModifierKind.Percent);
    }

    private void AddCorruptionGamble(
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
}
