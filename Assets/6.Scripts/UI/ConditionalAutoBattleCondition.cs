/// <summary>
/// When to automatically activate combat loadout set 1 or set 2.
/// Stored as int index in <see cref="ConditionalAutoBattleSettingsStore"/>.
/// </summary>
public enum ConditionalAutoBattleCondition
{
    DoNothing = 0,
    TargetInMeleeRange = 1,
    NoMeleeTarget = 2,
    LowHealth = 3,
    HighHealth = 4,
}
