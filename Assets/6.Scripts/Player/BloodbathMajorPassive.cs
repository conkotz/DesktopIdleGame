using UnityEngine;

public partial class PlayerCombatController
{
    private int _bloodbathStacks;
    private float _bloodbathEndsAt = -1f;
    private int _lastSyncedBloodbathHudStacks = int.MinValue;

    public void NotifyBloodbathStackFromBleedApplication()
    {
        if (stats == null || !stats.IsBloodbathUnlocked() || !stats.AreMeleeMajorPassiveEffectsEnabled())
            return;

        _bloodbathStacks = Mathf.Min(
            AbilityCombatPower.BloodbathMaxStacks,
            _bloodbathStacks + 1);
        _bloodbathEndsAt = Time.time + AbilityCombatPower.BloodbathStackDurationSeconds;
        _lastSyncedBloodbathHudStacks = int.MinValue;
        stats.NotifyStatsChanged();
        SyncBloodbathHudBuff();
    }

    public int GetBloodbathStacks() =>
        IsBloodbathActive() ? _bloodbathStacks : 0;

    public float GetBloodbathPhysicalDamagePercent() =>
        GetBloodbathStacks() * AbilityCombatPower.BloodbathPhysicalDamagePerStack;

    public float GetBloodbathBleedMultiplierBonus() =>
        stats != null && stats.GetBloodbathEnhancementPick() == 0
            ? GetBloodbathStacks() * AbilityCombatPower.BloodbathCarnageBleedMultiplierPerStack
            : 0f;

    public float GetBloodbathBleedChanceBonus() =>
        stats != null && stats.GetBloodbathEnhancementPick() == 1
            ? GetBloodbathStacks() * AbilityCombatPower.BloodbathButcheryBleedChancePerStack
            : 0f;

    private bool IsBloodbathActive() =>
        stats != null
        && stats.IsBloodbathUnlocked()
        && stats.AreMeleeMajorPassiveEffectsEnabled()
        && _bloodbathStacks > 0
        && Time.time < _bloodbathEndsAt;

    private void TickBloodbathStacks()
    {
        if (stats == null || !stats.IsBloodbathUnlocked() || !stats.AreMeleeMajorPassiveEffectsEnabled())
        {
            ClearBloodbathStacksIfAny();
            return;
        }

        if (_bloodbathStacks > 0 && Time.time >= _bloodbathEndsAt)
            ClearBloodbathStacksIfAny();
        else
            SyncBloodbathHudBuff();
    }

    private void SyncBloodbathHudBuff()
    {
        PlayerBuffController buffController = player != null ? player.GetComponent<PlayerBuffController>() : null;
        if (!buffController)
            return;

        if (!IsBloodbathActive())
        {
            if (_lastSyncedBloodbathHudStacks != 0)
            {
                buffController.ClearHudAbilityBuff(AbilityCombatPower.BloodbathHudBuffId);
                _lastSyncedBloodbathHudStacks = 0;
            }

            return;
        }

        if (_lastSyncedBloodbathHudStacks == _bloodbathStacks)
            return;

        _lastSyncedBloodbathHudStacks = _bloodbathStacks;
        buffController.SetHudAbilityBuff(
            AbilityCombatPower.BloodbathHudBuffId,
            _bloodbathStacks,
            _bloodbathEndsAt,
            AbilityCombatPower.BloodbathStackDurationSeconds);
    }

    private void ClearBloodbathStacksIfAny()
    {
        bool hadState = _bloodbathStacks > 0 || _bloodbathEndsAt >= 0f || _lastSyncedBloodbathHudStacks != int.MinValue;
        _bloodbathStacks = 0;
        _bloodbathEndsAt = -1f;
        _lastSyncedBloodbathHudStacks = int.MinValue;

        if (!hadState)
            return;

        stats?.NotifyStatsChanged();
        player?.GetComponent<PlayerBuffController>()?.ClearHudAbilityBuff(AbilityCombatPower.BloodbathHudBuffId);
    }
}
