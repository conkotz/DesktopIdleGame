public enum SkillUnlockType
{
    // Keep these first four in the same ordinal order as legacy values
    // to preserve existing asset data mapping:
    // PassiveBonus -> MinorPassive
    // AbilityUnlock -> Ability
    // ResourceUnlock -> Unlock
    // SpecialUnlock -> MajorPassive
    MinorPassive,
    Ability,
    Unlock,
    MajorPassive,
    CapstonePassive,

    /// <summary>
    /// Flavor / world unlock (e.g. new fish). No stat package; gameplay is driven elsewhere. Serialized as last value so existing assets keep their unlock types.
    /// </summary>
    MinorUnlock
}