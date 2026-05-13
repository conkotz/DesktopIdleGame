public enum WoodcuttingMinorNodeStatOption
{
    None = 0,
    WoodcuttingGatherSpeedFlat01 = 1,
    WoodcuttingGritPercent2 = 2,
    WoodcuttingEnergyEfficiencyPercent2 = 3,
    WoodcuttingBonusItemChancePercent2 = 4,

    WoodcuttingSpeedPercent2 = 10,
    WoodcuttingSpeedPercent3 = 11,
    WoodcuttingSpeedPercent4 = 12,
    WoodcuttingStaminaEfficiencyPercent2 = 13,
    WoodcuttingGritPercent1 = 14,
    WoodcuttingGritPercent3 = 15,
    WoodcuttingBonusFindPercent1 = 16,
    WoodcuttingBonusFindPercent3 = 17,
    WoodcuttingExtraLogChancePercent2 = 18,

    // Retained for serialized data compatibility; not used by current woodcutting skill asset.
    WoodcuttingSteadySwingSpeedPercent2Above80Energy = 19,
    WoodcuttingCritRestoreEnergy1 = 20,
    WoodcuttingMomentumAfter3SuccessSpeedPercent2For5s = 21,
    WoodcuttingRareFindUncommonChancePercent2 = 22,
    WoodcuttingYieldPercent2 = 23,
    WoodcuttingYieldPercent3 = 24,
    WoodcuttingRecoveryPercent5 = 25,
    WoodcuttingCriticalYieldPercent5 = 26,
    WoodcuttingHighTierStaminaDrainReduction = 27,
    WoodcuttingDuplicateRareSmallChance = 28,
    WoodcuttingTirelessEvery12thSwingFree = 29,
    WoodcuttingBonusXpSmallChance = 30,
    WoodcuttingMomentumContinuous15sSpeedPercent2BonusFindPercent2 = 31,
    WoodcuttingHeavyHitCritRollMainYieldTwiceKeepHigher = 32,
    WoodcuttingMaterialFindSmallChance = 33,
    WoodcuttingMomentumConsecutiveSpeedStacks = 34,
    WoodcuttingRareFindImprovedRarityRolls = 35,
    WoodcuttingFrenzyAfterCritSpeedPercent5For3s = 36,
    WoodcuttingDoubleSwingSmallChance = 37,
    WoodcuttingTreasureFindRareIncreased = 38,
    WoodcuttingBonusRewardBonusFindsPlusOneChance15 = 39,
    WoodcuttingTirelessSmallChanceNoStaminaCost = 40,
    WoodcuttingForestFlowContinuousSpeedPercent3RecoveryPercent3 = 41,
    WoodcuttingMasterGathererSmallChanceDoubleResources = 42,

    WoodcuttingStaminaEfficiencyPercent3 = 43,
    WoodcuttingGritPercent4 = 44,
    WoodcuttingBonusFindPercent6 = 45,
    /// <summary>Legacy: treated as +7% max stamina restored on grit proc.</summary>
    WoodcuttingCritRestoreEnergy10OnGrit = 46,
    /// <summary>+4% chance (per node) to roll duplicate Woodcutting XP on a gather tick.</summary>
    WoodcuttingBonusXpChancePercent2 = 47,
    WoodcuttingNoStaminaSwingChancePercent3 = 48,
    WoodcuttingFrenzyAfterGritSpeedPercent5Duration7s = 49,
    /// <summary>+10% chance this woodcutting tick does not count toward the tree depletion cap.</summary>
    WoodcuttingChanceNotToCountTowardTreeDepletionPercent10 = 50,
    WoodcuttingGritProcRestoresMaxStaminaPercent7 = 51,
    WoodcuttingGritProcRestoresMaxStaminaPercent8 = 52
}

public enum MiningMinorNodeStatOption
{
    None = 0,
    MiningGatherSpeedFlat01 = 1,
    MiningGritPercent2 = 2,
    MiningEnergyEfficiencyPercent2 = 3,
    MiningBonusItemChancePercent2 = 4
}

public enum FishingMinorNodeStatOption
{
    None = 0,
    FishingGatherSpeedFlat01 = 1,
    FishingGritPercent2 = 2,
    FishingEnergyEfficiencyPercent2 = 3,
    FishingBonusItemChancePercent2 = 4,

    FishingSpeedPercent2 = 10,
    FishingSpeedPercent3 = 11,
    FishingSpeedPercent4 = 12,
    FishingStaminaEfficiencyPercent1 = 13,
    FishingStaminaEfficiencyPercent3 = 14,
    FishingGritPercent1 = 15,
    FishingGritPercent3 = 16,
    FishingBonusFindPercent1 = 17,
    FishingBonusFindPercent3 = 18,
    FishingBonusFindPercent5 = 19,

    FishingGritRestoreStaminaFlat10 = 30,
    FishingDoubleXpChancePercent3 = 31,
    FishingNoStaminaSwingChancePercent3 = 32,
    FishingFrenzyAfterGritSpeedPercent5Duration7s = 33,
    FishingCalmWatersContinuousSpeedPercent3EfficiencyPercent3 = 34,
    FishingBaitConservationChancePercent10 = 35,
    FishingAutoCookChancePercent2 = 36,
    FishingAutoCookChancePercent4 = 37,
    FishingTreasureCatchChanceSmall = 38
}
