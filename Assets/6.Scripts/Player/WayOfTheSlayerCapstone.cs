using UnityEngine;

public partial class PlayerCombatController
{
    private bool TryWayOfTheSlayerExecute(EnemyBaseController target, out DamageResult result)
    {
        result = default;
        if (target == null || target.IsDead || stats == null || !stats.IsWayOfTheSlayerCapstoneActive())
            return false;

        CharacterStats enemyStats = target.GetComponent<CharacterStats>();
        if (enemyStats == null || enemyStats.IsDead || enemyStats.MaxHP <= 0f)
            return false;

        float hp01 = enemyStats.HP / Mathf.Max(1f, enemyStats.MaxHP);
        if (hp01 > AbilityCombatPower.WayOfTheSlayerExecuteHpThreshold01)
            return false;

        int dealt = target.TakeExecuteDamage(
            player.transform,
            AttackSkill.Melee,
            AbilityCombatPower.WayOfTheSlayerExecuteOutgoingSourceLabel);

        result.physical = Mathf.Max(0f, dealt);
        return dealt > 0;
    }
}
