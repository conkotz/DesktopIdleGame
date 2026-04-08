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
    CapstonePassive
}