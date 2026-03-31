using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCombatState : MonoBehaviour
{
    private readonly HashSet<int> _engagers = new HashSet<int>();
    public bool InCombat => _engagers.Count > 0;

    public void SetEngaged(EnemyBaseController enemy, bool engaged)
    {
        if (!enemy) return;
        int id = enemy.GetInstanceID();

        if (engaged) _engagers.Add(id);
        else _engagers.Remove(id);
    }

    public void ClearAll() => _engagers.Clear();
}