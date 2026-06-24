using System;
using System.Collections.Generic;

/// <summary>
/// Maps minor passive unlocks to aggregate passive-list line labels for yellow "active" highlighting.
/// </summary>
public static class PassiveUnlocksLineHighlight
{
    public const string ActiveEffectColor = "#FFEB3B";

    public static HashSet<string> CollectActiveLineLabels(SkillDefinition skill, int currentLevel)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (skill?.unlocks == null)
            return labels;

        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = skill.unlocks[i];
            if (unlock == null || unlock.unlockType != SkillUnlockType.MinorPassive)
                continue;
            if (currentLevel < unlock.requiredLevel)
                continue;
            if (TryGetLineLabel(skill, unlock, out string key) && !string.IsNullOrEmpty(key))
                labels.Add(key);
        }

        if (currentLevel > CharacterStats.SkillPostCapThresholdLevel)
        {
            string postCap = GetPostCapLabel(skill.skillType);
            if (!string.IsNullOrEmpty(postCap))
                labels.Add(postCap);
        }

        return labels;
    }

    public static bool TryGetLineLabel(SkillDefinition skill, SkillUnlockDefinition unlock, out string key)
    {
        key = null;
        if (skill == null || unlock == null || unlock.unlockType != SkillUnlockType.MinorPassive)
            return false;

        switch (skill.skillType)
        {
            case SkillType.Melee:
                return TryGetMeleeKey(unlock.meleeMinorStatOption, out key);
            case SkillType.Ranged:
                return TryGetRangedKey(unlock.rangedMinorStatOption, out key);
            case SkillType.Magic:
                return TryGetMagicKey(unlock.magicMinorStatOption, out key);
            case SkillType.Endurance:
                return TryGetEnduranceKey(unlock.enduranceMinorStatOption, out key);
            case SkillType.Woodcutting:
                return TryGetWoodcuttingKey(unlock.woodcuttingMinorStatOption, out key);
            case SkillType.Mining:
                return TryGetMiningKey(unlock.miningMinorStatOption, out key);
            case SkillType.Fishing:
                return TryGetFishingKey(unlock.fishingMinorStatOption, out key);
            default:
                return false;
        }
    }

    private static string GetPostCapLabel(SkillType type) => type switch
    {
        SkillType.Melee => "Melee Damage",
        SkillType.Ranged => "Ranged Damage",
        SkillType.Magic => "Magic Damage",
        SkillType.Endurance => "Max HP",
        SkillType.Woodcutting => "Woodcutting Speed",
        SkillType.Mining => "Mining Speed",
        SkillType.Fishing => "Fishing Speed",
        _ => null
    };

    private static bool TryGetMeleeKey(MeleeMinorNodeStatOption opt, out string key)
    {
        key = opt switch
        {
            MeleeMinorNodeStatOption.MinMeleeDamageFlat2 => "Min Melee Damage",
            MeleeMinorNodeStatOption.MaxMeleeDamageFlat2 => "Max Melee Damage",
            MeleeMinorNodeStatOption.MeleeAttackSpeedPercent3 => "Melee Attack Speed",
            MeleeMinorNodeStatOption.MeleeDamagePercent3 => "Melee Damage",
            MeleeMinorNodeStatOption.MeleeCritChancePercent2 => "Melee Crit Chance",
            MeleeMinorNodeStatOption.MeleeDamageVsLowHpPercent10 => $"Melee Damage to Low HP Enemies {CharacterStats.MeleeLowHpDisplaySuffix}",
            MeleeMinorNodeStatOption.MeleeBleedChancePercent5 => "Melee Bleed Chance",
            MeleeMinorNodeStatOption.MeleeBleedDamagePercent10 => "Melee Bleed Multiplier",
            MeleeMinorNodeStatOption.MeleeMoveSpeedPercent2 or MeleeMinorNodeStatOption.MeleeMoveSpeedPercent5 => "Melee Move Speed",
            MeleeMinorNodeStatOption.MeleeCritDamagePercent8 => "Melee Crit Damage",
            MeleeMinorNodeStatOption.MeleePoisonChancePercent5 => "Melee Poison Chance",
            MeleeMinorNodeStatOption.MeleePoisonDurationPercent10 => "Melee Poison Duration",
            MeleeMinorNodeStatOption.MeleeAilmentDamagePercent4 => "Melee Bleed, Poison, Burn Multipliers",
            MeleeMinorNodeStatOption.MeleeDamageVsPoisonedPercent10 => "Melee Damage to Poisoned Enemies",
            MeleeMinorNodeStatOption.MeleeShockChancePercent5 => "Melee Shock Chance",
            MeleeMinorNodeStatOption.MeleeDamageVsShockedPercent10 => "Melee Damage to Shocked Enemies",
            MeleeMinorNodeStatOption.MeleeBurnChancePercent5 => "Melee Burn Chance",
            MeleeMinorNodeStatOption.MeleeDamageVsBurningPercent10 => "Melee Damage to Burning Enemies",
            MeleeMinorNodeStatOption.MeleeLifeStealPercent1 or MeleeMinorNodeStatOption.MeleeLifeStealPercent2 => "Melee Lifesteal",
            MeleeMinorNodeStatOption.MeleeDamageVsBleedingPercent10 => "Melee Damage to Bleeding Enemies",
            _ => null
        };
        return key != null;
    }

    private static bool TryGetRangedKey(RangedMinorNodeStatOption opt, out string key)
    {
        key = opt switch
        {
            RangedMinorNodeStatOption.RangedDamagePercent3 => "Ranged Damage",
            RangedMinorNodeStatOption.RangedAttackSpeedPercent3 => "Ranged Attack Speed",
            RangedMinorNodeStatOption.RangedCritChancePercent2 => "Ranged Crit Chance",
            RangedMinorNodeStatOption.RangedMoveSpeedPercent5 or RangedMinorNodeStatOption.RangedMoveSpeedPercent10 => "Ranged Move Speed",
            RangedMinorNodeStatOption.MinRangedDamageFlat2 => "Min Ranged Damage",
            RangedMinorNodeStatOption.MaxRangedDamageFlat2 => "Max Ranged Damage",
            RangedMinorNodeStatOption.RangedDamageVsLowHpPercent10 => $"Ranged Damage to Low HP Enemies {CharacterStats.RangedLowHpDisplaySuffix}",
            RangedMinorNodeStatOption.RangedShockChancePercent5 => "Ranged Shock Chance",
            RangedMinorNodeStatOption.RangedLightningDamagePercent10 or RangedMinorNodeStatOption.RangedLightningDamagePercent4 => "Lightning Damage",
            RangedMinorNodeStatOption.RangedDamageVsShockedPercent10 => "Ranged Damage to Shocked Enemies",
            RangedMinorNodeStatOption.RangedCritDamagePercent8 => "Ranged Crit Damage",
            RangedMinorNodeStatOption.MinionDamagePercent5 or RangedMinorNodeStatOption.MinionDamagePercent8 => "Minion Damage",
            RangedMinorNodeStatOption.MinionMaxLifePercent10 => "Minion Max HP",
            RangedMinorNodeStatOption.RangedDamageWithMinionActivePercent10 => "Ranged Damage while a Minion is Active",
            RangedMinorNodeStatOption.RangedDamageVsDistantPercent5 or RangedMinorNodeStatOption.RangedDamageVsDistantPercent10 => "Ranged Damage against Distant Enemies",
            RangedMinorNodeStatOption.RangedDamageWhenNoNearbyEnemyPercent5 or RangedMinorNodeStatOption.RangedDamageWhenNoNearbyEnemyPercent10 => "Ranged Damage when no Enemy is within 3m",
            RangedMinorNodeStatOption.RangedDamageVsFullHpPercent6 => "Ranged Damage to Full HP Enemies",
            _ => null
        };
        return key != null;
    }

    private static bool TryGetMagicKey(MagicMinorNodeStatOption opt, out string key)
    {
        key = opt switch
        {
            MagicMinorNodeStatOption.MagicDamagePercent3 => "Magic Damage",
            MagicMinorNodeStatOption.MagicAttackSpeedPercent3 => "Cast Speed",
            MagicMinorNodeStatOption.MagicCritChancePercent2 => "Crit Chance",
            MagicMinorNodeStatOption.MagicCritDamagePercent8 => "Crit Damage",
            _ => null
        };
        return key != null;
    }

    private static bool TryGetEnduranceKey(EnduranceMinorNodeStatOption opt, out string key)
    {
        key = opt switch
        {
            EnduranceMinorNodeStatOption.EnduranceHealthFlat10 => "Max HP",
            EnduranceMinorNodeStatOption.EnduranceMaxHealthPercent2
                or EnduranceMinorNodeStatOption.EnduranceHeavyArmourMasteryMaxHealthPercent5 => "Max HP",
            EnduranceMinorNodeStatOption.EnduranceArmourFlat10
                or EnduranceMinorNodeStatOption.EnduranceHeavyArmourMasteryArmourFlat10 => "Armour",
            EnduranceMinorNodeStatOption.EnduranceMagicResistFlat10
                or EnduranceMinorNodeStatOption.EnduranceMediumArmourMasteryMagicResistFlat5
                or EnduranceMinorNodeStatOption.EnduranceLightArmourMasteryMagicResistFlat10 => "Magic Resist",
            EnduranceMinorNodeStatOption.EnduranceCorruptionResistFlat10
                or EnduranceMinorNodeStatOption.EnduranceMediumArmourMasteryCorruptionResistFlat5 => "Corruption Resist",
            EnduranceMinorNodeStatOption.EnduranceEnergyEfficiencyPercent2 => "Energy Efficiency",
            EnduranceMinorNodeStatOption.EnduranceLifeRegenFlat1 => "HP Regen",
            EnduranceMinorNodeStatOption.EnduranceThornsDamagePercent5 => "Thorns Damage",
            EnduranceMinorNodeStatOption.EnduranceMaxGuardPercent5
                or EnduranceMinorNodeStatOption.EnduranceMaxGuardPercent10 => "Max Guard",
            EnduranceMinorNodeStatOption.EnduranceGuardGainPercent5 => "Increased Guard",
            EnduranceMinorNodeStatOption.EnduranceBastionDrPercent5WhileGuardActive => "Damage Reduction while Guard is Active",
            EnduranceMinorNodeStatOption.EnduranceShieldBlockChancePercent5 => "Block Chance while a Shield is Equipped",
            EnduranceMinorNodeStatOption.EnduranceShieldBlockMitigationPercent5 => "Block Mitigation while a Shield is Equipped",
            EnduranceMinorNodeStatOption.EnduranceParryChancePercent2_5 => "Parry Chance",
            EnduranceMinorNodeStatOption.EnduranceParryMitigationPercent5 => "Parry Mitigation",
            EnduranceMinorNodeStatOption.EnduranceSurvivorDrPercent5BelowHalfHp => "Damage Reduction while below 50% HP",
            EnduranceMinorNodeStatOption.EnduranceMediumArmourMasteryMoveSpeedPercent10 => "Move Speed while wearing only Medium Armour",
            EnduranceMinorNodeStatOption.EnduranceLightArmourMasteryManaFlat20 => "Mana while wearing only Light Armour",
            EnduranceMinorNodeStatOption.EnduranceLightArmourMasteryManaRegenFlat3 => "Mana Regeneration while wearing only Light Armour",
            _ => null
        };
        return key != null;
    }

    private static bool TryGetMiningKey(MiningMinorNodeStatOption opt, out string key)
    {
        key = opt switch
        {
            MiningMinorNodeStatOption.MiningGatherSpeedFlat01 => "Gathering Speed",
            MiningMinorNodeStatOption.MiningGritPercent2 => "Grit",
            MiningMinorNodeStatOption.MiningEnergyEfficiencyPercent2 => "Energy Efficiency",
            MiningMinorNodeStatOption.MiningBonusItemChancePercent2 => "Bonus Item Chance",
            _ => null
        };
        return key != null;
    }

    private static bool TryGetFishingKey(FishingMinorNodeStatOption opt, out string key)
    {
        key = opt switch
        {
            FishingMinorNodeStatOption.FishingGatherSpeedFlat01
                or FishingMinorNodeStatOption.FishingSpeedPercent2
                or FishingMinorNodeStatOption.FishingSpeedPercent3
                or FishingMinorNodeStatOption.FishingSpeedPercent4 => "Fishing Speed",
            FishingMinorNodeStatOption.FishingGritPercent1
                or FishingMinorNodeStatOption.FishingGritPercent2
                or FishingMinorNodeStatOption.FishingGritPercent3 => "Fishing Grit Chance",
            FishingMinorNodeStatOption.FishingEnergyEfficiencyPercent2
                or FishingMinorNodeStatOption.FishingStaminaEfficiencyPercent1
                or FishingMinorNodeStatOption.FishingStaminaEfficiencyPercent3 => "Fishing Stamina Efficiency",
            FishingMinorNodeStatOption.FishingBonusItemChancePercent2
                or FishingMinorNodeStatOption.FishingBonusFindPercent1
                or FishingMinorNodeStatOption.FishingBonusFindPercent3
                or FishingMinorNodeStatOption.FishingBonusFindPercent5 => "Fishing Bonus Find Chance",
            FishingMinorNodeStatOption.FishingGritRestoreStaminaFlat10 => "Fishing Grit catches restore",
            FishingMinorNodeStatOption.FishingDoubleXpChancePercent3 => "chance to gain double Fishing XP",
            FishingMinorNodeStatOption.FishingNoStaminaSwingChancePercent3 => "chance for Fishing casts to cost no stamina",
            FishingMinorNodeStatOption.FishingFrenzyAfterGritSpeedPercent5Duration7s => "After a Fishing Grit catch:",
            FishingMinorNodeStatOption.FishingCalmWatersContinuousSpeedPercent3EfficiencyPercent3 => "While continuously fishing:",
            FishingMinorNodeStatOption.FishingBaitConservationChancePercent10 => "chance to not consume bait durability",
            FishingMinorNodeStatOption.FishingAutoCookChancePercent2
                or FishingMinorNodeStatOption.FishingAutoCookChancePercent4 => "chance for caught fish to be automatically cooked",
            FishingMinorNodeStatOption.FishingTreasureCatchChanceSmall => "catch treasure while fishing",
            _ => null
        };
        return key != null;
    }

    private static bool TryGetWoodcuttingKey(WoodcuttingMinorNodeStatOption opt, out string key)
    {
        key = null;
        switch (opt)
        {
            case WoodcuttingMinorNodeStatOption.WoodcuttingGatherSpeedFlat01:
            case WoodcuttingMinorNodeStatOption.WoodcuttingSpeedPercent2:
            case WoodcuttingMinorNodeStatOption.WoodcuttingSpeedPercent3:
            case WoodcuttingMinorNodeStatOption.WoodcuttingSpeedPercent4:
                key = "Woodcutting Speed";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingGritPercent1:
            case WoodcuttingMinorNodeStatOption.WoodcuttingGritPercent2:
            case WoodcuttingMinorNodeStatOption.WoodcuttingGritPercent3:
            case WoodcuttingMinorNodeStatOption.WoodcuttingGritPercent4:
                key = "Woodcutting Grit Chance";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingEnergyEfficiencyPercent2:
            case WoodcuttingMinorNodeStatOption.WoodcuttingStaminaEfficiencyPercent2:
            case WoodcuttingMinorNodeStatOption.WoodcuttingStaminaEfficiencyPercent3:
                key = "Woodcutting Stamina Efficiency";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingBonusItemChancePercent2:
            case WoodcuttingMinorNodeStatOption.WoodcuttingBonusFindPercent1:
            case WoodcuttingMinorNodeStatOption.WoodcuttingBonusFindPercent3:
            case WoodcuttingMinorNodeStatOption.WoodcuttingBonusFindPercent6:
                key = "Woodcutting Bonus Find Chance";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingExtraLogChancePercent2:
                key = "Woodcutting Chance for +1 Extra Main Resource";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingYieldPercent2:
            case WoodcuttingMinorNodeStatOption.WoodcuttingYieldPercent3:
                key = "Woodcutting Base Resource Yield";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingCritRestoreEnergy10OnGrit:
            case WoodcuttingMinorNodeStatOption.WoodcuttingGritProcRestoresMaxStaminaPercent7:
            case WoodcuttingMinorNodeStatOption.WoodcuttingGritProcRestoresMaxStaminaPercent8:
                key = "Woodcutting Grit procs restore";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingBonusXpChancePercent2:
                key = "double XP gained from Woodcutting";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingNoStaminaSwingChancePercent3:
                key = "Woodcutting No-Stamina Swing Chance";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingChanceNotToCountTowardTreeDepletionPercent10:
                key = "Woodcutting chance not to count toward tree depletion";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingFrenzyAfterGritSpeedPercent5Duration7s:
                key = "After a Woodcutting Grit proc:";
                return true;

            case WoodcuttingMinorNodeStatOption.WoodcuttingForestFlowContinuousSpeedPercent3RecoveryPercent3:
                key = "While continuously woodcutting (after 15 seconds):";
                return true;

            default:
                return false;
        }
    }
}
