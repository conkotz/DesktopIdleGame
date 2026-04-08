using System.Collections.Generic;
using UnityEngine;
using System;

[DisallowMultipleComponent]
public class PlayerCombatState : MonoBehaviour
{
    private readonly HashSet<int> _engagers = new HashSet<int>();
    public bool InCombat => _engagers.Count > 0;
    public event Action<bool> OnCombatStateChanged; // true=in combat, false=out of combat

    public void SetEngaged(EnemyBaseController enemy, bool engaged)
    {
        if (!enemy) return;
        bool wasInCombat = InCombat;
        int id = enemy.GetInstanceID();

        if (engaged) _engagers.Add(id);
        else _engagers.Remove(id);

        if (wasInCombat != InCombat)
            OnCombatStateChanged?.Invoke(InCombat);
    }

    public void ClearAll()
    {
        bool wasInCombat = InCombat;
        _engagers.Clear();
        if (wasInCombat)
            OnCombatStateChanged?.Invoke(false);
    }
}