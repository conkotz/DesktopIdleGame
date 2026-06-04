using UnityEngine;

public partial class PlayerCombatController
{
    public const string WayOfTheCrusaderHudBuffId = "way_of_the_crusader";
    public const string WayOfTheCrusaderHealingSourceLabel = "Way of the Crusader";
    public const string WayOfTheCrusaderExtraFireOutgoingSourceLabel = "Way of the Crusader";

    private int _wayOfTheCrusaderHolySeals;
    private float _wayOfTheCrusaderNextHolySealAt = -1f;
    private float _pendingWayOfTheCrusaderExtraFireDamage;
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
        _lastSyncedWayOfTheCrusaderHudSeals = int.MinValue;
        HealWayOfTheCrusaderFromMaxHpFraction();
    }

    public void TryPrepareWayOfTheCrusaderExtraFireOnAutoAttack(SplitDamage rolled, SwingOutgoingAttribution swingAttribution)
    {
        _pendingWayOfTheCrusaderExtraFireDamage = 0f;

        if (!IsWayOfTheCrusaderCapstoneActive() || !IsMeleeAutoAttackSwing(swingAttribution))
            return;

        float physOrFireBase = GetWayOfTheCrusaderExtraFireBaseFromRolledSwing(rolled);
        float bonusFire = physOrFireBase * AbilityCombatPower.WayOfTheCrusaderExtraFireDamageFraction;
        if (bonusFire <= 0f)
            return;

        _pendingWayOfTheCrusaderExtraFireDamage = bonusFire;
    }

    private float GetWayOfTheCrusaderExtraFireBaseFromRolledSwing(SplitDamage rolled)
    {
        float baseAmount = Mathf.Max(0f, rolled.physical);
        if (stats == null)
            return baseAmount;

        float fireFromMagic = Mathf.Max(0f, rolled.magic) * stats.GetWeaponMagicFireFraction();
        if (abilityController != null && abilityController.IsCrusaderStrikeFireBalanceBuffActive)
            fireFromMagic = Mathf.Max(fireFromMagic, Mathf.Max(0f, rolled.magic));

        return baseAmount + fireFromMagic;
    }

    public void TryApplyPendingWayOfTheCrusaderExtraFireDamage(EnemyBaseController target, bool wasCrit)
    {
        if (target == null || target.IsDead || _pendingWayOfTheCrusaderExtraFireDamage <= 0f || player == null)
            return;

        float bonus = _pendingWayOfTheCrusaderExtraFireDamage;
        _pendingWayOfTheCrusaderExtraFireDamage = 0f;

        float conditionalDamageMult = GetConditionalMeleeDamageMultiplier(target);
        if (wasCrit && stats != null)
            conditionalDamageMult *= stats.GetPredatorsInstinctExecutionerCritDamageFactor(target, true);

        int dealt = target.TakeDamage(
            Mathf.RoundToInt(bonus * conditionalDamageMult),
            DamageType.Magic,
            wasCrit,
            player.transform,
            stats != null ? stats.CurrentAttackSkill : null,
            outgoingDpsSourceLabel: WayOfTheCrusaderExtraFireOutgoingSourceLabel);

        if (dealt <= 0)
            return;

        player.ApplyLifeSteal(dealt);

        if (stats == null)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        TryApplyElementalMagicAilment(
            target,
            new DamageResult
            {
                magic = dealt,
                physical = 0f,
                corruptionDamage = 0f
            });
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
                        || _pendingWayOfTheCrusaderExtraFireDamage > 0f
                        || _wayOfTheCrusaderNextHolySealAt >= 0f
                        || _lastSyncedWayOfTheCrusaderHudSeals > 0;

        _wayOfTheCrusaderHolySeals = 0;
        _pendingWayOfTheCrusaderExtraFireDamage = 0f;
        _wayOfTheCrusaderNextHolySealAt = -1f;
        _lastSyncedWayOfTheCrusaderHudSeals = int.MinValue;

        PlayerBuffController buffController = player != null ? player.GetComponent<PlayerBuffController>() : null;
        buffController?.ClearHudAbilityBuff(WayOfTheCrusaderHudBuffId);

        if (hadState)
            stats?.NotifyStatsChanged();
    }
}
