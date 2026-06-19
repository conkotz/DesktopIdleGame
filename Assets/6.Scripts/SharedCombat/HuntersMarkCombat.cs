using UnityEngine;

/// <summary>
/// Applies and reads Hunter's Mark from player auto attacks and non-minion ability hits.
/// </summary>
public static class HuntersMarkCombat
{
    public static void TryApplyFromPlayerHit(EnemyBaseController target, CharacterStats attackerStats, float totalDealt)
    {
        if (!target || target.IsDead || totalDealt <= 0f || attackerStats == null)
            return;

        if (!attackerStats.IsHuntersMarkMajorPassiveActive())
            return;

        EnemyHuntersMark mark = target.GetComponent<EnemyHuntersMark>();
        if (!mark)
            mark = target.gameObject.AddComponent<EnemyHuntersMark>();

        mark.ApplyMark(attackerStats);
        UnitOverheadUI.RefreshHuntersMarkForEnemy(target);
    }
}
