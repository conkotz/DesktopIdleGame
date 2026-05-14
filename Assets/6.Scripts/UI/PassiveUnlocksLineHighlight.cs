using System;

/// <summary>
/// Maps a minor passive unlock to a substring/label key used by <see cref="SkillsAbilitiesPageUI"/> passive list lines
/// so the tree selection can bold the matching aggregate bullet.
/// </summary>
public static class PassiveUnlocksLineHighlight
{
    public static bool TryGetLineLabel(SkillDefinition skill, SkillUnlockDefinition unlock, out string key)
    {
        key = null;
        if (skill == null || unlock == null || unlock.unlockType != SkillUnlockType.MinorPassive)
            return false;

        if (skill.skillType == SkillType.Woodcutting)
            return TryGetWoodcuttingKey(unlock.woodcuttingMinorStatOption, out key);

        return false;
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
