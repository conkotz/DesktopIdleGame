public static class EnhancementWeightScalingRules
{
    public static bool IsWeightScaledStat(EnhancementScrollTargetStat stat)
    {
        return stat switch
        {
            EnhancementScrollTargetStat.PhysicalDamage => true,
            EnhancementScrollTargetStat.FireDamage => true,
            EnhancementScrollTargetStat.IceDamage => true,
            EnhancementScrollTargetStat.LightningDamage => true,
            EnhancementScrollTargetStat.CorruptionDamage => true,
            EnhancementScrollTargetStat.PoisonMultiplier => true,
            EnhancementScrollTargetStat.BurnMultiplier => true,
            EnhancementScrollTargetStat.CritMultiplier => true,
            EnhancementScrollTargetStat.AttackSpeed => true,
            _ => false,
        };
    }
}
