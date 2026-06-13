using System;

/// <summary>
/// Local player minion command stance (Aggressive / Assist / Passive).
/// </summary>
public static class MinionControlService
{
    public static event Action<MinionControlStance> OnStanceChanged;

    public static MinionControlStance CurrentStance { get; private set; } = MinionControlStance.Aggressive;

    public static void SetStance(MinionControlStance stance)
    {
        if (CurrentStance == stance)
            return;

        CurrentStance = stance;
        OnStanceChanged?.Invoke(CurrentStance);
    }
}
