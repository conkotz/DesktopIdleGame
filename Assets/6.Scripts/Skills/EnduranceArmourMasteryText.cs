/// <summary>Shared copy for endurance armour-mastery minors (skill tree + passive unlocks panel).</summary>
public static class EnduranceArmourMasteryText
{
    public static string FormatArmourTypeLabel(ArmourType armourType) =>
        armourType switch
        {
            ArmourType.Heavy => "Heavy",
            ArmourType.Medium => "Medium",
            ArmourType.Light => "Light",
            _ => armourType.ToString()
        };

    public static string FormatHeadAndBodySlotsSuffix(ArmourType armourType) =>
        $"while wearing {FormatArmourTypeLabel(armourType)} armour in head and body slots";
}
