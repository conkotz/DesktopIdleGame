/// <summary>
/// Weapon / armor / tool tier rank. Shown as "Tier 1" … "Tier 5"; item display names carry flavor (e.g. Splitwood).
/// Gated by skill level on the matching skill: Tier1 = Lv1, Tier2 = Lv10, Tier3 = Lv20, Tier4 = Lv30, Tier5 = Lv50.
/// </summary>
public enum EquipmentTierRank
{
    Tier1 = 0,
    Tier2 = 1,
    Tier3 = 2,
    Tier4 = 3,
    Tier5 = 4,
}

/// <summary>
/// Tier display strings and level thresholds (same for all weapon / armor / tool families that use <see cref="EquipmentTierRank"/>).
/// </summary>
public static class EquipmentTierRules
{
    public static int GetRequiredSkillLevel(EquipmentTierRank rank)
    {
        return rank switch
        {
            EquipmentTierRank.Tier1 => 1,
            EquipmentTierRank.Tier2 => 10,
            EquipmentTierRank.Tier3 => 20,
            EquipmentTierRank.Tier4 => 30,
            EquipmentTierRank.Tier5 => 50,
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
            EquipmentTierRank.Tier4 => "Tier 4",
            EquipmentTierRank.Tier5 => "Tier 5",
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
                return "Equipment tiers: Tier 1 (Lv1), Tier 2 (Lv10), Tier 3 (Lv20), Tier 4 (Lv30), Tier 5 (Lv50).";
            default:
                return "";
        }
    }
}
