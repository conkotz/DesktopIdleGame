using UnityEngine;

/// <summary>
/// Shared checks for whether minion command UI should be available.
/// </summary>
public static class MinionControlVisibility
{
    public static bool HasActiveSoulforgedWarrior()
    {
        SoulforgedWarriorMinion[] warriors = Object.FindObjectsByType<SoulforgedWarriorMinion>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < warriors.Length; i++)
        {
            SoulforgedWarriorMinion warrior = warriors[i];
            if (warrior != null && warrior.IsOperational)
                return true;
        }

        return false;
    }
}
