using System;
using UnityEngine;

/// <summary>
/// Shadow Strike enhancement marks on enemies (lethal crit amp or execution cooldown refund).
/// </summary>
[DisallowMultipleComponent]
public class EnemyShadowStrikeMarks : MonoBehaviour
{
    public enum MarkKind
    {
        None,
        LethalCrit,
        Execution
    }

    public event Action OnMarksChanged;

    private EnemyBaseController _enemy;
    private bool _lethalCritMarkActive;
    private bool _executionMarkActive;
    private float _executionMarkExpiresAt = -1f;
    private PlayerAbilityController _executionMarkOwner;
    private AbilityDefinition _executionMarkAbilityDef;

    public MarkKind ActiveMarkKind
    {
        get
        {
            if (_lethalCritMarkActive)
                return MarkKind.LethalCrit;
            if (_executionMarkActive && Time.time < _executionMarkExpiresAt)
                return MarkKind.Execution;
            return MarkKind.None;
        }
    }

    public bool HasLethalCritMark => _lethalCritMarkActive;
    public bool HasExecutionMark => _executionMarkActive && Time.time < _executionMarkExpiresAt;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBaseController>();
    }

    private void Update()
    {
        if (_executionMarkActive && Time.time >= _executionMarkExpiresAt)
        {
            _executionMarkActive = false;
            _executionMarkOwner = null;
            _executionMarkAbilityDef = null;
            OnMarksChanged?.Invoke();
        }
    }

    public void ApplyMark(MarkKind kind, PlayerAbilityController owner, AbilityDefinition abilityDef)
    {
        ClearMarks();

        switch (kind)
        {
            case MarkKind.LethalCrit:
                _lethalCritMarkActive = true;
                break;
            case MarkKind.Execution:
                _executionMarkActive = true;
                _executionMarkExpiresAt = Time.time + AbilityCombatPower.ShadowStrikeExecutionMarkSeconds;
                _executionMarkOwner = owner;
                _executionMarkAbilityDef = abilityDef;
                break;
        }

        OnMarksChanged?.Invoke();
    }

    /// <summary>
    /// +80% critical damage on the next successful crit; returns extra multiplier (1.8× total crit damage).
    /// </summary>
    public float TryConsumeLethalCritDamageMultiplier()
    {
        if (!_lethalCritMarkActive)
            return 1f;

        _lethalCritMarkActive = false;
        OnMarksChanged?.Invoke();
        return 1f + AbilityCombatPower.ShadowStrikeLethalCritBonusFraction;
    }

    public void NotifyEnemyDied()
    {
        if (_executionMarkActive && Time.time < _executionMarkExpiresAt &&
            _executionMarkOwner != null && _executionMarkAbilityDef != null)
        {
            _executionMarkOwner.ReduceAbilityCooldownBySeconds(
                _executionMarkAbilityDef,
                AbilityCombatPower.ShadowStrikeExecutionCooldownRefundSeconds);
        }

        ClearMarks();
    }

    public void ClearMarks()
    {
        bool changed = _lethalCritMarkActive || _executionMarkActive;
        _lethalCritMarkActive = false;
        _executionMarkActive = false;
        _executionMarkExpiresAt = -1f;
        _executionMarkOwner = null;
        _executionMarkAbilityDef = null;

        if (changed)
            OnMarksChanged?.Invoke();
    }

    private void OnDisable()
    {
        ClearMarks();
    }
}
