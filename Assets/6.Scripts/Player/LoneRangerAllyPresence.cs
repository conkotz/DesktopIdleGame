using UnityEngine;

/// <summary>
/// Counts player-owned allies (minions / companions) sharing the same enabled play area as the owner.
/// Called from combat events only — no Update polling.
/// </summary>
public static class LoneRangerAllyPresence
{
    /// <summary>Allies in the same enabled side play area (or main lane when none applies).</summary>
    public static int CountAlliesInSameArea(PlayerCombatController owner)
    {
        if (!owner)
            return 0;

        string ownerAreaKey = PlayAreaBounds.GetPlayAreaKey(owner.transform.position.x);
        int count = 0;

        var combatTargets = MinionCombatTarget.ActiveTargets;
        for (int i = 0; i < combatTargets.Count; i++)
        {
            MinionCombatTarget minion = combatTargets[i];
            if (minion == null || !minion.IsAlive || minion.OwnerCombat != owner)
                continue;

            Transform minionTransform = minion.transform;
            if (!minionTransform)
                continue;

            if (PlayAreaBounds.GetPlayAreaKey(minionTransform.position.x) == ownerAreaKey)
                count++;
        }

        var hawkCompanions = HawkCompanionMinion.ActiveCompanions;
        for (int i = 0; i < hawkCompanions.Count; i++)
        {
            HawkCompanionMinion hawk = hawkCompanions[i];
            if (hawk == null || !hawk.IsActiveAlly || hawk.ResolveOwnerCombat() != owner)
                continue;

            if (PlayAreaBounds.GetPlayAreaKey(hawk.transform.position.x) == ownerAreaKey)
                count++;
        }

        var soulforgedWeapons = SoulforgedWeaponMinion.ActiveCompanions;
        for (int i = 0; i < soulforgedWeapons.Count; i++)
        {
            SoulforgedWeaponMinion weapon = soulforgedWeapons[i];
            if (weapon == null || !weapon.IsActiveAlly || weapon.ResolveOwnerCombat() != owner)
                continue;

            if (PlayAreaBounds.GetPlayAreaKey(weapon.transform.position.x) == ownerAreaKey)
                count++;
        }

        return count;
    }
}
