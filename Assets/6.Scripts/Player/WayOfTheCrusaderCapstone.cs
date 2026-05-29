using UnityEngine;

public partial class PlayerCombatController
{
    public const string WayOfTheCrusaderHudBuffId = "way_of_the_crusader";
    public const string WayOfTheCrusaderHealingSourceLabel = "Way of the Crusader";
    public const string HolySealFireStrikeOutgoingSourceLabel = "Way of the Crusader";

    private int _wayOfTheCrusaderHolySeals;
    private bool _wayOfTheCrusaderPendingFireStrike;
    private float _wayOfTheCrusaderNextHolySealAt = -1f;
    private float _pendingHolySealFireStrikeBonus;
    private int _lastSyncedWayOfTheCrusaderHudSeals = int.MinValue;

    public int GetWayOfTheCrusaderHolySeals() =>
        IsWayOfTheCrusaderCapstoneActive() ? _wayOfTheCrusaderHolySeals : 0;

    private bool IsWayOfTheCrusaderCapstoneActive() =>
        stats != null && stats.IsWayOfTheCrusaderCapstoneActive();

    private void TickWayOfTheCrusaderCapstone()
    {
        if (!IsWayOfTheCrusaderCapstoneActive())
        {
            ClearWayOfTheCrusaderStateIfAny();
            return;
        }

        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
        {
            ClearWayOfTheCrusaderStateIfAny();
            return;
        }

        TickWayOfTheCrusaderHolySealGain();
        SyncWayOfTheCrusaderHudBuff();
    }

    private void TickWayOfTheCrusaderHolySealGain()
    {
        if (_wayOfTheCrusaderHolySeals >= AbilityCombatPower.WayOfTheCrusaderMaxHolySeals)
            return;

        if (_wayOfTheCrusaderNextHolySealAt < 0f)
        {
            _wayOfTheCrusaderNextHolySealAt =
                Time.time + AbilityCombatPower.WayOfTheCrusaderHolySealGainIntervalSeconds;
        }

        while (_wayOfTheCrusaderHolySeals < AbilityCombatPower.WayOfTheCrusaderMaxHolySeals
               && Time.time >= _wayOfTheCrusaderNextHolySealAt)
        {
            _wayOfTheCrusaderHolySeals++;
            _wayOfTheCrusaderNextHolySealAt +=
                AbilityCombatPower.WayOfTheCrusaderHolySealGainIntervalSeconds;
            _lastSyncedWayOfTheCrusaderHudSeals = int.MinValue;
        }
    }

    public void TryProcessWayOfTheCrusaderOnIncomingHit(bool blocked, float totalToVitals, float hpDamageDealt)
    {
        if (!IsWayOfTheCrusaderCapstoneActive() || stats == null || stats.IsDead)
            return;

        if (blocked)
        {
            HealWayOfTheCrusaderFromMaxHpFraction();
            return;
        }

        if (totalToVitals <= 0f && hpDamageDealt <= 0f)
            return;

        if (IsAtFullHealthForHolySeal())
            return;

        if (_wayOfTheCrusaderHolySeals <= 0)
            return;

        _wayOfTheCrusaderHolySeals--;
        _wayOfTheCrusaderPendingFireStrike = true;
        _lastSyncedWayOfTheCrusaderHudSeals = int.MinValue;
        HealWayOfTheCrusaderFromMaxHpFraction();
    }

    public void TryPrepareWayOfTheCrusaderFireStrikeBonus(SplitDamage rolled, SwingOutgoingAttribution swingAttribution)
    {
        _pendingHolySealFireStrikeBonus = 0f;

        if (!_wayOfTheCrusaderPendingFireStrike || !IsWayOfTheCrusaderCapstoneActive())
            return;

        if (!IsMeleeAutoAttackSwing(swingAttribution))
            return;

        _wayOfTheCrusaderPendingFireStrike = false;

        float weaponTotal = rolled.physical + rolled.magic + rolled.corruptionDamage;
        float bonusFire = weaponTotal * AbilityCombatPower.WayOfTheCrusaderFireStrikeWeaponDamageFraction;
        if (bonusFire <= 0f)
            return;

        _pendingHolySealFireStrikeBonus = bonusFire;
    }

    public void TryApplyPendingHolySealFireStrikeDamage(EnemyBaseController target, bool wasCrit)
    {
        if (target == null || target.IsDead || _pendingHolySealFireStrikeBonus <= 0f || player == null)
            return;

        float bonus = _pendingHolySealFireStrikeBonus;
        _pendingHolySealFireStrikeBonus = 0f;

        float conditionalDamageMult = GetConditionalMeleeDamageMultiplier(target);
        if (wasCrit && stats != null)
            conditionalDamageMult *= stats.GetPredatorsInstinctExecutionerCritDamageFactor(target, true);

        int dealt = target.TakeDamage(
            Mathf.RoundToInt(bonus * conditionalDamageMult),
            DamageType.Magic,
            wasCrit,
            player.transform,
            stats != null ? stats.CurrentAttackSkill : null,
            outgoingDpsSourceLabel: HolySealFireStrikeOutgoingSourceLabel);

        if (dealt <= 0)
            return;

        player.ApplyLifeSteal(dealt);

        if (stats == null)
            return;

        var ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        var dealtResult = new DamageResult
        {
            magic = dealt,
            physical = 0f,
            corruptionDamage = 0f
        };
        TryApplyElementalMagicAilment(target, dealtResult);
    }

    private static bool IsAtFullHealthForHolySeal(CharacterStats characterStats)
    {
        if (characterStats == null)
            return true;

        return characterStats.HP >= characterStats.MaxHP - 0.001f;
    }

    private bool IsAtFullHealthForHolySeal() => IsAtFullHealthForHolySeal(stats);

    private void HealWayOfTheCrusaderFromMaxHpFraction()
    {
        if (stats == null || stats.MaxHP <= 0f)
            return;

        float healAmount = stats.MaxHP * AbilityCombatPower.WayOfTheCrusaderHealMaxHpFraction;
        if (healAmount <= 0f)
            return;

        stats.Heal(healAmount, WayOfTheCrusaderHealingSourceLabel);
    }

    private void SyncWayOfTheCrusaderHudBuff()
    {
        PlayerBuffController buffController = player != null ? player.GetComponent<PlayerBuffController>() : null;
        if (buffController == null)
            return;

        if (_wayOfTheCrusaderHolySeals <= 0)
        {
            if (_lastSyncedWayOfTheCrusaderHudSeals != 0)
            {
                buffController.ClearHudAbilityBuff(WayOfTheCrusaderHudBuffId);
                _lastSyncedWayOfTheCrusaderHudSeals = 0;
            }

            return;
        }

        if (_lastSyncedWayOfTheCrusaderHudSeals == _wayOfTheCrusaderHolySeals)
            return;

        _lastSyncedWayOfTheCrusaderHudSeals = _wayOfTheCrusaderHolySeals;
        buffController.SetHudAbilityBuff(
            WayOfTheCrusaderHudBuffId,
            _wayOfTheCrusaderHolySeals,
            0f,
            0f,
            persistActiveOverlay: true);
    }

    private void ClearWayOfTheCrusaderStateIfAny()
    {
        bool hadState = _wayOfTheCrusaderHolySeals > 0
                        || _wayOfTheCrusaderPendingFireStrike
                        || _pendingHolySealFireStrikeBonus > 0f
                        || _wayOfTheCrusaderNextHolySealAt >= 0f
                        || _lastSyncedWayOfTheCrusaderHudSeals > 0;

        _wayOfTheCrusaderHolySeals = 0;
        _wayOfTheCrusaderPendingFireStrike = false;
        _pendingHolySealFireStrikeBonus = 0f;
        _wayOfTheCrusaderNextHolySealAt = -1f;
        _lastSyncedWayOfTheCrusaderHudSeals = int.MinValue;

        PlayerBuffController buffController = player != null ? player.GetComponent<PlayerBuffController>() : null;
        buffController?.ClearHudAbilityBuff(WayOfTheCrusaderHudBuffId);

        if (hadState)
            stats?.NotifyStatsChanged();
    }
}
