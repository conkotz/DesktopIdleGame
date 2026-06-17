using System.Collections.Generic;
using UnityEngine;
using System;

[DisallowMultipleComponent]
public class PlayerCombatState : MonoBehaviour
{
    private readonly HashSet<int> _engagers = new HashSet<int>();
    /// <summary>Enemy in attack/engage range (excludes soft combat from recent hits).</summary>
    public bool HasEnemyProximityEngagement => _engagers.Count > 0;

    /// <summary>Enemy proximity engagement OR recent damage dealt/taken (see <see cref="NotifySoftCombat"/>).</summary>
    public bool InCombat => HasEnemyProximityEngagement || Time.time < _softCombatUntil;
    public event Action<bool> OnCombatStateChanged; // true=in combat, false=out of combat

    private float _softCombatUntil = -999f;

    /// <summary>Extends "in combat" for gathering/UI until <paramref name="durationSeconds"/> elapses (stacked with max end time).</summary>
    public void NotifySoftCombat(float durationSeconds)
    {
        if (durationSeconds <= 0f) return;
        bool was = InCombat;
        _softCombatUntil = Mathf.Max(_softCombatUntil, Time.time + durationSeconds);
        bool now = InCombat;
        if (was != now)
            OnCombatStateChanged?.Invoke(now);
    }

    public bool IsEngagedWith(EnemyBaseController enemy)
    {
        if (!enemy)
            return false;

        return _engagers.Contains(enemy.GetInstanceID());
    }

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
        if (wasInCombat != InCombat)
            OnCombatStateChanged?.Invoke(InCombat);
    }
}