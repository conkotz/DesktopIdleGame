public enum EnhancementTier
{
    Basic = 0,
    Intermediate = 1,
    Advanced = 2,
}

public enum EnhancementTrack
{
    Standard = 0,
    Corruption = 1,
    Special = 2,
}

public enum EnhancementPaymentKind
{
    None = 0,
    Scroll = 1,
    Materials = 2,
}

public enum GearUpgradeMaterialFamily
{
    Unknown = 0,
    Stone = 1,
    Leather = 2,
    Linen = 3,
    Wood = 4,
}

public readonly struct GearUpgradeMaterialRequirement
{
    public string ItemId { get; }
    public int Amount { get; }

    public GearUpgradeMaterialRequirement(string itemId, int amount)
    {
        ItemId = itemId;
        Amount = amount;
    }
}
