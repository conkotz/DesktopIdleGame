using UnityEngine;

public partial class PlayerCombatController
{
    public static void NotifyEnchantedQuiverKillFromEnemyDeath()
    {
        CharacterStats stats = AbilityTooltipDamagePreview.FindLocalPlayerStats();
        stats?.TryProcEnchantedQuiverArrowsOnKill();
    }
}
