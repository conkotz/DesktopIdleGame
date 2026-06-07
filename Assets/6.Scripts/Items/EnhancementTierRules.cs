public static class EnhancementTierRules
{
    public static int GetRequiredPlayerSkillLevel(EnhancementTier tier)
    {
        return tier switch
        {
            EnhancementTier.Basic => 1,
            EnhancementTier.Intermediate => 20,
            EnhancementTier.Advanced => 40,
            _ => 1,
        };
    }

    public static bool PlayerMeetsTierSkillRequirement(
        SkillsManager skills,
        SkillType gateSkill,
        EnhancementTier tier,
        bool bypassForCorruptionTrack)
    {
        if (bypassForCorruptionTrack)
            return true;

        if (skills == null)
            return true;

        return skills.IsLevelUnlocked(gateSkill, GetRequiredPlayerSkillLevel(tier));
    }

    public static EnhancementTier GetMaxEnhancementTierForGear(EquipmentTierRank gearTier)
    {
        return gearTier switch
        {
            EquipmentTierRank.Tier1 or EquipmentTierRank.Tier2 => EnhancementTier.Basic,
            EquipmentTierRank.Tier3 or EquipmentTierRank.Tier4 => EnhancementTier.Intermediate,
            EquipmentTierRank.Tier5 => EnhancementTier.Advanced,
            _ => EnhancementTier.Basic,
        };
    }

    public static bool GearAllowsEnhancementTier(EquipmentTierRank gearTier, EnhancementTier optionTier) =>
        (int)optionTier <= (int)GetMaxEnhancementTierForGear(gearTier);

    public static int GetMaterialCost(EnhancementTier tier)
    {
        return tier switch
        {
            EnhancementTier.Basic => 50,
            EnhancementTier.Intermediate => 100,
            EnhancementTier.Advanced => 150,
            _ => 50,
        };
    }

    public static string GetTierDisplayName(EnhancementTier tier)
    {
        return tier switch
        {
            EnhancementTier.Basic => "Basic",
            EnhancementTier.Intermediate => "Intermediate",
            EnhancementTier.Advanced => "Advanced",
            _ => tier.ToString(),
        };
    }

    public static string FormatAllowedGearTierUsage(EnhancementTier optionTier)
    {
        return optionTier switch
        {
            EnhancementTier.Basic => "Can be used on tier 1 and tier 2 items",
            EnhancementTier.Intermediate => "Can be used on tier 3 and tier 4 items",
            EnhancementTier.Advanced => "Can be used on tier 5 items",
            _ => string.Empty,
        };
    }

    public static int GetMinimumGearTierDisplayNumber(EnhancementTier optionTier)
    {
        return optionTier switch
        {
            EnhancementTier.Basic => 1,
            EnhancementTier.Intermediate => 3,
            EnhancementTier.Advanced => 5,
            _ => 1,
        };
    }
}
