using System;
using UnityEngine;

/// <summary>
/// Hunter's Mark debuff (Ranged Lv10 major passive) — marked enemies take increased damage from minions.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHuntersMark : MonoBehaviour
{
    public event Action OnMarkChanged;

    private bool _active;
    private CharacterStats _appliedBy;
    private float _expiresAt;

    public bool IsActive => _active && Time.time < _expiresAt;

    private void Update()
    {
        if (_active && Time.time >= _expiresAt)
            ClearMark();
    }

    public void ApplyMark(CharacterStats appliedBy)
    {
        if (appliedBy == null)
            return;

        bool wasActive = IsActive;
        bool sameOwner = _appliedBy == appliedBy;
        _active = true;
        _appliedBy = appliedBy;
        _expiresAt = Time.time + AbilityCombatPower.HuntersMarkDurationSeconds;

        if (!wasActive || !sameOwner)
            OnMarkChanged?.Invoke();
    }

    public float GetMinionIncomingDamageMultiplier()
    {
        if (!IsActive || _appliedBy == null || !_appliedBy.IsHuntersMarkMajorPassiveActive())
            return 1f;

        return 1f + _appliedBy.GetHuntersMarkMinionDamageBonusFraction();
    }

    public void ClearMark()
    {
        if (!_active)
            return;

        _active = false;
        _appliedBy = null;
        _expiresAt = 0f;
        OnMarkChanged?.Invoke();
    }

    private void OnDisable()
    {
        ClearMark();
    }
}
