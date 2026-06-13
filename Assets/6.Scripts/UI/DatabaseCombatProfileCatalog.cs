using System.Collections.Generic;

/// <summary>Player-facing combat profile blurbs for the database general information page.</summary>
public static class DatabaseCombatProfileCatalog
{
    private static readonly (string Label, string Description)[] Entries =
    {
        (
            CombatProfileLabel.Relentless,
            "Fast enemies with strong offense. They close distance quickly and keep pressure on you."
        ),
        (
            CombatProfileLabel.GlassCannon,
            "Very high damage with little durability. They hit hard but fall fast when focused."
        ),
        (
            CombatProfileLabel.Deadly,
            "Offense-focused fighters with lighter defenses. Dangerous damage dealers that are still somewhat fragile."
        ),
        (
            CombatProfileLabel.Bruiser,
            "Solid offense and defense together. Tough in melee and harder to burst down quickly."
        ),
        (
            CombatProfileLabel.Armoured,
            "Heavy armour or strong physical mitigation. Weapon hits and physical damage are less effective."
        ),
        (
            CombatProfileLabel.Warded,
            "High magic resist. Spells and magic damage struggle to get through."
        ),
        (
            CombatProfileLabel.Shrouded,
            "High corruption resist or corruption durability. Corruption damage is a poor answer against them."
        ),
        (
            CombatProfileLabel.Tank,
            "A large health pool with little specialised mitigation. Raw HP makes them durable, tanky foes."
        ),
        (
            CombatProfileLabel.Sustaining,
            "Strong sustain in their stat profile. They recover or outlast you during longer fights."
        ),
        (
            CombatProfileLabel.Nimble,
            "High mobility. They reposition, kite, or dodge more than they stand and absorb hits."
        ),
        (
            CombatProfileLabel.Balanced,
            "No single extreme strength. Their stats are spread evenly without one sharp specialty."
        ),
        (
            CombatProfileAilmentLabel.Poisonous,
            "Applies poison on hit. Expect stacking poison damage over time if you let procs build up."
        ),
        (
            CombatProfileAilmentLabel.Sanguine,
            "Applies bleed on hit. Sustained bleed pressure adds up during longer trades."
        ),
        (
            CombatProfileAilmentLabel.Electrified,
            "Applies shock on hit. Shocked targets take increased damage from follow-up hits."
        ),
        (
            CombatProfileAilmentLabel.Fiery,
            "Applies burn on hit. Burn stacks build toward burst damage if left unchecked."
        ),
        (
            CombatProfileAilmentLabel.Frosted,
            "Applies chill on hit. Chill slows movement and attack speed as stacks build."
        ),
    };

    public static IReadOnlyList<(string Label, string Description)> GetAllEntries() => Entries;
}
