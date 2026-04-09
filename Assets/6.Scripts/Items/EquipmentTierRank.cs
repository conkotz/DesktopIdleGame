/// <summary>
/// Weapon/tool tier rank. Shown everywhere as "Tier 1" … "Tier 3"; item display names carry flavor (e.g. Splitwood).
/// Gated by skill level: Tier1 = 1, Tier2 = 20, Tier3 = 40 on the matching skill.
/// </summary>
public enum EquipmentTierRank
{
    Tier1 = 0,
    Tier2 = 1,
    Tier3 = 2,
}

/// <summary>
/// Tier display strings and level thresholds (same for all weapon/tool families).
/// </summary>
public static class EquipmentTierRules
{
    public static int GetRequiredSkillLevel(EquipmentTierRank rank)
    {
        return rank switch
        {
            EquipmentTierRank.Tier1 => 1,
            EquipmentTierRank.Tier2 => 20,
            EquipmentTierRank.Tier3 => 40,
            _ => 1
        };
    }

    public static string GetTierDisplayLabel(EquipmentTierRank rank)
    {
        return rank switch
        {
            EquipmentTierRank.Tier1 => "Tier 1",
            EquipmentTierRank.Tier2 => "Tier 2",
            EquipmentTierRank.Tier3 => "Tier 3",
            _ => "Tier 1"
        };
    }

    /// <summary>Optional hint under the skill tree (assign <see cref="SkillTreeViewUI"/> hint field).</summary>
    public static string BuildSkillTreeHint(SkillType skill)
    {
        switch (skill)
        {
            case SkillType.Melee:
            case SkillType.Ranged:
            case SkillType.Magic:
            case SkillType.Woodcutting:
            case SkillType.Mining:
            case SkillType.Fishing:
                return "Equipment tiers: Tier 1 (skill Lv1), Tier 2 (Lv20), Tier 3 (Lv40).";
            default:
                return "";
        }
    }
}
