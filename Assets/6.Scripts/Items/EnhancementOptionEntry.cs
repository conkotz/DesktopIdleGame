using System;
using UnityEngine;

[Serializable]
public sealed class EnhancementOptionEntry
{
    [Tooltip("Stable id for saves/UI (e.g. armour_basic).")]
    public string optionId;

    [Tooltip("Shown in the Type column (e.g. Armour Basic).")]
    public string displayName;

    public EnhancementTrack track = EnhancementTrack.Standard;
    public EnhancementTier tier = EnhancementTier.Basic;

    public EnhancementScrollTargetStat targetStat = EnhancementScrollTargetStat.Armor;
    public EnhancementScrollModifierKind modifierKind = EnhancementScrollModifierKind.Flat;
    public float modifierValue = 5f;

    [Header("Weapon Weight Scaling")]
    [Tooltip("When enabled, modifier value is resolved from weight values based on target gear weight.")]
    public bool usesWeaponWeightScaling;

    public EnhancementWeightValues weightValues;

    public EnhancementScrollGearMask allowedGearTypes = EnhancementScrollGearMask.Armor;

    [Range(0f, 1f)]
    public float successChance = 0.5f;
    public bool consumeSlotOnFailure = true;
    public EnhancementScrollFailureOutcome failureOutcome = EnhancementScrollFailureOutcome.Nothing;
    [Range(0f, 1f)]
    public float destroyChanceOnFailure;

    [Tooltip("Optional scroll item id. If the player owns this scroll, it can be spent instead of materials.")]
    public string linkedScrollItemId;

    public bool UsesWeightScalingEffective() =>
        usesWeaponWeightScaling || EnhancementWeightScalingRules.IsWeightScaledStat(targetStat);

    public EnhancementWeightValues ResolveWeightValues()
    {
        if (Mathf.Abs(weightValues.medium) > 0.0001f ||
            Mathf.Abs(weightValues.light) > 0.0001f ||
            Mathf.Abs(weightValues.heavy) > 0.0001f)
            return weightValues;

        if (targetStat == EnhancementScrollTargetStat.AttackSpeed)
        {
            return tier switch
            {
                EnhancementTier.Intermediate => EnhancementWeightValues.Explicit(0.03f, 0.04f, 0.05f),
                EnhancementTier.Advanced => EnhancementWeightValues.Explicit(0.04f, 0.05f, 0.06f),
                _ => EnhancementWeightValues.Explicit(0.02f, 0.03f, 0.04f),
            };
        }

        if (modifierKind == EnhancementScrollModifierKind.Percent)
            return EnhancementWeightValues.PercentStep(modifierValue);

        if (track == EnhancementTrack.Corruption)
            return EnhancementWeightValues.ChaosFlatDamage(modifierValue);

        return EnhancementWeightValues.FlatDamageForTier(tier, modifierValue);
    }

    public float ResolveModifierValue(ItemDefinition gear = null)
    {
        if (!UsesWeightScalingEffective())
            return modifierValue;

        WeaponWeight weight = gear != null
            ? WeaponWeightRules.GetEffectiveWeight(gear)
            : WeaponWeight.Medium;

        return ResolveWeightValues().Resolve(weight);
    }

    public EnhancementScrollStats ToScrollStats()
    {
        return new EnhancementScrollStats
        {
            successChance = successChance,
            targetStat = targetStat,
            modifierKind = modifierKind,
            modifierValue = ResolveModifierValue(),
            consumeSlotOnFailure = consumeSlotOnFailure,
            failureOutcome = failureOutcome,
            destroyChanceOnFailure = destroyChanceOnFailure,
            cursed = track == EnhancementTrack.Corruption,
            allowedGearTypes = allowedGearTypes,
        };
    }

    public EnhancementScrollStats ToScrollStats(ItemDefinition gear)
    {
        EnhancementScrollStats stats = ToScrollStats();
        stats.modifierValue = ResolveModifierValue(gear);
        stats.successChance = EnhancementSuccessChanceRules.GetSuccessChance(gear, this);
        return stats;
    }

    public bool TargetsGear(ItemDefinition gear)
    {
        if (gear == null)
            return false;

        return EnhancementScrollGearRules.MaskAllowsGear(allowedGearTypes, gear);
    }

    public SkillType ResolveGateSkill(ItemDefinition gear)
    {
        if (gear != null)
            return gear.GetEquipmentTierGateSkill();

        if (EnhancementScrollGearRules.MaskTargetsArmorSlots(allowedGearTypes))
            return SkillType.Endurance;

        if ((allowedGearTypes & EnhancementScrollGearMask.MagicWeapon) != 0)
            return SkillType.Magic;

        if ((allowedGearTypes & EnhancementScrollGearMask.RangedWeapon) != 0 &&
            (allowedGearTypes & EnhancementScrollGearMask.MeleeWeapon) == 0)
            return SkillType.Ranged;

        if ((allowedGearTypes & EnhancementScrollGearMask.MeleeWeapon) != 0)
            return SkillType.Melee;

        if ((allowedGearTypes & EnhancementScrollGearMask.Tool) != 0)
            return SkillType.Woodcutting;

        return SkillType.Melee;
    }

    public bool PlayerMeetsTierSkillRequirement(SkillsManager skills, ItemDefinition gear = null)
    {
        return EnhancementTierRules.PlayerMeetsTierSkillRequirement(
            skills,
            ResolveGateSkill(gear),
            tier,
            track == EnhancementTrack.Corruption);
    }

    public bool IsUnavailableForSelection(ItemDefinition gear, SkillsManager skills)
    {
        if (!PlayerMeetsTierSkillRequirement(skills, gear))
            return true;

        if (gear == null)
            return false;

        if (!TargetsGear(gear))
            return true;

        if (!EnhancementTierRules.GearAllowsEnhancementTier(gear.GetEquipmentTierRank(), tier))
            return true;

        return !GearMeetsRequiredStat(gear);
    }

    public bool GearMeetsRequiredStat(ItemDefinition gear)
    {
        if (gear == null)
            return true;

        EnhancementScrollStats stats = ToScrollStats();
        if (stats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return gear.UsedUpgradeSlots > 0;

        return gear.HasBaseStatForEnhancementScroll(stats.targetStat);
    }

    public bool CanApplyToGear(ItemDefinition gear, SkillsManager skills = null)
    {
        if (!TargetsGear(gear) || !gear.HasUpgradeSlots)
            return false;

        if (!PlayerMeetsTierSkillRequirement(skills, gear))
            return false;

        if (!EnhancementTierRules.GearAllowsEnhancementTier(gear.GetEquipmentTierRank(), tier))
            return false;

        EnhancementScrollStats stats = ToScrollStats();
        if (stats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return gear.UsedUpgradeSlots > 0;

        if (gear.HasReachedEnhancementCap)
            return false;

        if (!gear.HasBaseStatForEnhancementScroll(stats.targetStat))
            return false;

        return gear.HasAvailableUpgradeSlot;
    }

}
