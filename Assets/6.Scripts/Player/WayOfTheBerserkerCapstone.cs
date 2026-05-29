using UnityEngine;

public partial class PlayerCombatController
{
    public const string WayOfTheBerserkerHudBuffId = "way_of_the_berserker";
    public const string WayOfTheBerserkerLeechHudBuffId = "way_of_the_berserker_leech";

    private int _wayOfTheBerserkerStacks;
    private float _wayOfTheBerserkerEndsAt = -1f;
    private float _wayOfTheBerserkerLeechEndsAt = -1f;
    private float _wayOfTheBerserkerLeechCooldownEndsAt = -1f;
    private int _lastSyncedWayOfTheBerserkerHudStacks = int.MinValue;
    private float _lastSyncedWayOfTheBerserkerHudEnd = float.NaN;
    private bool _lastSyncedWayOfTheBerserkerLeechActive;

    public int GetWayOfTheBerserkerStacks() =>
        IsWayOfTheBerserkerCapstoneActive() && IsWayOfTheBerserkerStackWindowActive()
            ? _wayOfTheBerserkerStacks
            : 0;

    public bool IsWayOfTheBerserkerSlowImmune() =>
        GetWayOfTheBerserkerStacks() >= AbilityCombatPower.WayOfTheBerserkerSlowImmunityMinStacks;

    public float GetWayOfTheBerserkerAttackSpeedBonusFraction()
    {
        int stacks = GetWayOfTheBerserkerStacks();
        return stacks > 0 ? stacks * AbilityCombatPower.WayOfTheBerserkerAttackSpeedPerStack : 0f;
    }

    public float GetWayOfTheBerserkerCritChanceBonusFraction()
    {
        int stacks = GetWayOfTheBerserkerStacks();
        return stacks > 0 ? stacks * AbilityCombatPower.WayOfTheBerserkerCritChancePerStack : 0f;
    }

    public float GetWayOfTheBerserkerMeleeDamageBonusFraction()
    {
        int stacks = GetWayOfTheBerserkerStacks();
        return stacks > 0 ? stacks * AbilityCombatPower.WayOfTheBerserkerMeleeDamagePerStack : 0f;
    }

    public float GetWayOfTheBerserkerMoveSpeedBonusFraction()
    {
        int stacks = GetWayOfTheBerserkerStacks();
        return stacks > 0 ? stacks * AbilityCombatPower.WayOfTheBerserkerMoveSpeedPerStack : 0f;
    }

    public float GetWayOfTheBerserkerIncomingDamageMultiplier()
    {
        int stacks = GetWayOfTheBerserkerStacks();
        return stacks > 0 ? 1f + stacks * AbilityCombatPower.WayOfTheBerserkerDamageTakenPerStack : 1f;
    }

    public float GetWayOfTheBerserkerLeechBonusFraction() =>
        IsWayOfTheBerserkerCapstoneActive() && Time.time < _wayOfTheBerserkerLeechEndsAt
            ? AbilityCombatPower.WayOfTheBerserkerLowHpLeechFraction
            : 0f;

    private bool IsWayOfTheBerserkerCapstoneActive() =>
        stats != null && stats.IsWayOfTheBerserkerCapstoneActive();

    private bool IsWayOfTheBerserkerStackWindowActive() =>
        _wayOfTheBerserkerStacks > 0 && Time.time < _wayOfTheBerserkerEndsAt;

    private void TickWayOfTheBerserkerCapstone()
    {
        if (!IsWayOfTheBerserkerCapstoneActive())
        {
            ClearWayOfTheBerserkerStateIfAny();
            return;
        }

        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
        {
            ClearWayOfTheBerserkerStateIfAny();
            return;
        }

        TickWayOfTheBerserkerStackExpiry();
        TickWayOfTheBerserkerLowHpLeechProc();
        SyncWayOfTheBerserkerHudBuffs();
    }

    private void TryAddWayOfTheBerserkerStackOnAutoAttack(SwingOutgoingAttribution swingAttribution)
    {
        if (!IsWayOfTheBerserkerCapstoneActive())
            return;

        if (!IsMeleeAutoAttackSwing(swingAttribution))
            return;

        _wayOfTheBerserkerStacks = Mathf.Min(
            AbilityCombatPower.WayOfTheBerserkerMaxStacks,
            _wayOfTheBerserkerStacks + 1);
        _wayOfTheBerserkerEndsAt = Time.time + AbilityCombatPower.WayOfTheBerserkerStackDurationSeconds;
        _lastSyncedWayOfTheBerserkerHudStacks = int.MinValue;
        stats?.NotifyStatsChanged();
    }

    private static bool IsMeleeAutoAttackSwing(SwingOutgoingAttribution swingAttribution) =>
        string.Equals(swingAttribution.primarySource, "Auto Attack", System.StringComparison.OrdinalIgnoreCase);

    private void TickWayOfTheBerserkerStackExpiry()
    {
        if (_wayOfTheBerserkerStacks <= 0)
            return;

        if (Time.time < _wayOfTheBerserkerEndsAt)
            return;

        _wayOfTheBerserkerStacks = 0;
        _wayOfTheBerserkerEndsAt = -1f;
        _lastSyncedWayOfTheBerserkerHudStacks = int.MinValue;
        stats?.NotifyStatsChanged();
    }

    private void TickWayOfTheBerserkerLowHpLeechProc()
    {
        if (stats == null || stats.MaxHP <= 0f)
            return;

        if (Time.time < _wayOfTheBerserkerLeechEndsAt)
            return;

        if (Time.time < _wayOfTheBerserkerLeechCooldownEndsAt)
            return;

        float hpFraction = stats.HP / stats.MaxHP;
        if (hpFraction >= stats.MeleeLowHpThreshold01)
            return;

        _wayOfTheBerserkerLeechEndsAt = Time.time + AbilityCombatPower.WayOfTheBerserkerLowHpLeechDurationSeconds;
        _wayOfTheBerserkerLeechCooldownEndsAt =
            Time.time + AbilityCombatPower.WayOfTheBerserkerLowHpLeechCooldownSeconds;
        _lastSyncedWayOfTheBerserkerLeechActive = false;
        stats.NotifyStatsChanged();
    }

    private void SyncWayOfTheBerserkerHudBuffs()
    {
        PlayerBuffController buffController = player != null ? player.GetComponent<PlayerBuffController>() : null;
        if (buffController == null)
            return;

        bool stackActive = IsWayOfTheBerserkerStackWindowActive();
        if (!stackActive)
        {
            if (_lastSyncedWayOfTheBerserkerHudStacks != 0)
            {
                buffController.ClearHudAbilityBuff(WayOfTheBerserkerHudBuffId);
                _lastSyncedWayOfTheBerserkerHudStacks = 0;
                _lastSyncedWayOfTheBerserkerHudEnd = float.NaN;
            }
        }
        else if (_lastSyncedWayOfTheBerserkerHudStacks != _wayOfTheBerserkerStacks
                 || !Mathf.Approximately(_lastSyncedWayOfTheBerserkerHudEnd, _wayOfTheBerserkerEndsAt))
        {
            _lastSyncedWayOfTheBerserkerHudStacks = _wayOfTheBerserkerStacks;
            _lastSyncedWayOfTheBerserkerHudEnd = _wayOfTheBerserkerEndsAt;
            buffController.SetHudAbilityBuff(
                WayOfTheBerserkerHudBuffId,
                _wayOfTheBerserkerStacks,
                _wayOfTheBerserkerEndsAt,
                AbilityCombatPower.WayOfTheBerserkerStackDurationSeconds);
        }

        bool leechActive = Time.time < _wayOfTheBerserkerLeechEndsAt;
        if (!leechActive)
        {
            if (_lastSyncedWayOfTheBerserkerLeechActive)
            {
                buffController.ClearHudAbilityBuff(WayOfTheBerserkerLeechHudBuffId);
                _lastSyncedWayOfTheBerserkerLeechActive = false;
            }

            return;
        }

        if (_lastSyncedWayOfTheBerserkerLeechActive)
            return;

        _lastSyncedWayOfTheBerserkerLeechActive = true;
        buffController.SetHudAbilityBuff(
            WayOfTheBerserkerLeechHudBuffId,
            1,
            _wayOfTheBerserkerLeechEndsAt,
            AbilityCombatPower.WayOfTheBerserkerLowHpLeechDurationSeconds);
    }

    private void ClearWayOfTheBerserkerStateIfAny()
    {
        bool hadStacks = _wayOfTheBerserkerStacks > 0 || _wayOfTheBerserkerEndsAt >= 0f;
        bool hadLeech = _wayOfTheBerserkerLeechEndsAt >= 0f || _lastSyncedWayOfTheBerserkerLeechActive;

        _wayOfTheBerserkerStacks = 0;
        _wayOfTheBerserkerEndsAt = -1f;
        _wayOfTheBerserkerLeechEndsAt = -1f;
        _wayOfTheBerserkerLeechCooldownEndsAt = -1f;
        _lastSyncedWayOfTheBerserkerHudStacks = int.MinValue;
        _lastSyncedWayOfTheBerserkerHudEnd = float.NaN;

        PlayerBuffController buffController = player != null ? player.GetComponent<PlayerBuffController>() : null;
        if (buffController != null)
        {
            buffController.ClearHudAbilityBuff(WayOfTheBerserkerHudBuffId);
            buffController.ClearHudAbilityBuff(WayOfTheBerserkerLeechHudBuffId);
        }

        if (hadStacks || hadLeech)
            stats?.NotifyStatsChanged();

        _lastSyncedWayOfTheBerserkerLeechActive = false;
    }
}
