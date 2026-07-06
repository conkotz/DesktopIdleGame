/// <summary>
/// Display copy for processing skills shown in the skills list (Cooking, Smelting, placeholders).
/// Edit descriptions here or wire assets later.
/// </summary>
public static class ProcessingSkillDisplayCatalog
{
    public enum Id
    {
        Cooking = 0,
        Smelting = 1,
        Blacksmithing = 2,
        MagicCrafting = 3,
        RangedCrafting = 4,
        Alchemy = 5,
        JewelCrafting = 6
    }

    public static string GetDisplayName(Id id) =>
        id switch
        {
            Id.Cooking => "Cooking",
            Id.Smelting => "Smelting",
            Id.Blacksmithing => "Blacksmithing",
            Id.MagicCrafting => "Magic Crafting",
            Id.RangedCrafting => "Ranged Crafting",
            Id.Alchemy => "Alchemy",
            Id.JewelCrafting => "Jewel Crafting",
            _ => id.ToString()
        };

    /// <summary>Long-form copy for the Details panel DESCRIPTION section.</summary>
    public static string GetDescription(Id id) =>
        id switch
        {
            Id.Cooking =>
                "Cooking turns raw fish and ingredients into meals that restore health, "
                + "and may grant temporary buffs. Higher proficiency unlocks faster cook times, reduced burn rates and the capability to cook higher-tier food.",
            Id.Smelting =>
                "Smelting converts ore into metal bars at furnaces. Bars feed blacksmithing and other crafting. "
                + "Train smelting to improve throughput and unlock higher-tier metals.",
            Id.Blacksmithing =>
                "Blacksmithing forges heavy armour, melee weapons, and metal tools from smelted bars. "
                + "Invest in this skill to craft stronger gear and unlock advanced smithing recipes.",
            Id.MagicCrafting =>
                "Magic Crafting imbues staves, wands, and arcane gear with spell power. "
                + "Progress to craft equipment that amplifies your magic combat abilities.",
            Id.RangedCrafting =>
                "Ranged Crafting produces bows, arrows, and ranged armour from wood, leather, and metal. "
                + "Higher levels unlock precision gear suited to bow and ranged combat.",
            Id.Alchemy =>
                "Alchemy brews potions, elixirs, and reagents from herbs, gems, and monster parts. "
                + "Useful for healing, buffs, and enhancing other crafting processes.",
            Id.JewelCrafting =>
                "Jewel Crafting cuts gems into rings, pendants, and trinkets that grant stat bonuses. "
                + "Pair with mining to turn raw gemstones into powerful accessories.",
            _ => string.Empty
        };

    public static bool HasProficiency(Id id) =>
        id == Id.Cooking || id == Id.Smelting || id == Id.Blacksmithing;

    public static bool TryGetProficiencyType(Id id, out ProcessingSkillType type)
    {
        switch (id)
        {
            case Id.Cooking:
                type = ProcessingSkillType.Cooking;
                return true;
            case Id.Smelting:
                type = ProcessingSkillType.Smelting;
                return true;
            case Id.Blacksmithing:
                type = ProcessingSkillType.Blacksmithing;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
