using UnityEngine;

public partial class PlayerAbilityController
{
    private bool TryCastMagicStarterSpell(
        AbilityDefinition def,
        bool showLockedFeedback,
        out bool spellExecuted)
    {
        spellExecuted = false;
        if (def == null || stats == null)
            return false;

        if (!MagicStarterSpellRules.IsMagicStarterSpellId(def.abilityId))
            return false;

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        if (combat == null)
            return false;

        EnemyBaseController target = combat.GetPrimaryEngagedEnemy();
        if (target != null && !combat.IsEnemyWithinAttackRange(target))
            target = null;

        if (target == null)
            target = combat.FindClosestEnemyInAttackRange();

        if (target == null || target.IsDead)
        {
            if (showLockedFeedback)
                LogNoTargetsInRangeThrottled();
            return false;
        }

        if (def.SetsTargetOnHit())
            combat.SetTargetIfNone(target);

        if (!combat.HasRequiredSpellRunesForAbility(def))
        {
            if (showLockedFeedback)
                player?.ShowPopup(combat.ResolveMissingSpellRunesMessage(def));
            return false;
        }

        if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
            return false;

        if (!combat.TryConsumeSpellRunesForAbility(def))
        {
            RefundAbilityResourceCost(def);
            if (showLockedFeedback)
                player?.ShowPopup(combat.ResolveMissingSpellRunesMessage(def));
            return false;
        }

        if (!MagicStarterSpellRules.TryGetBaseDamageBounds(def.abilityId, out float baseMin, out float baseMax))
        {
            RefundAbilityResourceCost(def);
            return false;
        }

        MagicAttackType element = MagicStarterSpellRules.GetMagicAttackTypeForAbilityId(def.abilityId);
        float rolled = SpellDamageScaling.RollScaledElementDamage(stats, element, baseMin, baseMax);

        bool wasCrit = UnityEngine.Random.value < GetEffectiveAbilityCritChance(target);
        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        float damage = Mathf.Max(1f, rolled * critMult);
        SplitDamage split = new SplitDamage(0f, damage, 0f);
        string sourceLabel = GetAbilityOutgoingDamageSourceLabel(def.abilityId);

        player?.TriggerAttackAnim();
        StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        LogAbilityUsed(def);

        combat.LaunchMagicStarterSpellAtTarget(target, element, () =>
        {
            ApplyMagicStarterSpellHitDamage(target, def, element, split, wasCrit, sourceLabel);
        });

        spellExecuted = true;
        return true;
    }

    /// <summary>
    /// Magic weapon auto-attack cadence: repeatedly casts the committed Lv1 starter spell.
    /// </summary>
    public bool TryPerformMagicAutoAttack(bool showLockedFeedback, out bool spellExecuted)
    {
        spellExecuted = false;
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;

        if (!MagicStarterSpellRules.TryGetCommittedStarterSpellAbilityId(skillsManager, out string spellId))
        {
            LogNoPrimarySpellSelectedThrottled();
            return false;
        }

        AbilityDefinition def = GetAbilityDefinition(spellId);
        if (def == null)
            return false;

        return TryCastMagicStarterSpell(def, showLockedFeedback, out spellExecuted);
    }

    private void LogNoPrimarySpellSelectedThrottled()
    {
        if (Time.time < _nextNoPrimarySpellSelectedLogTime)
            return;

        _nextNoPrimarySpellSelectedLogTime = Time.time + NoPrimarySpellSelectedLogCooldownSeconds;
        GameLog.Add(CombatStarterAttackAbility.NoPrimarySpellSelectedLogMessage, GameLog.CannotMessageColor);
    }

    private void ApplyMagicStarterSpellHitDamage(
        EnemyBaseController target,
        AbilityDefinition def,
        MagicAttackType element,
        SplitDamage rolled,
        bool wasCrit,
        string sourceLabel)
    {
        if (combat == null || target == null || target.IsDead || stats == null || def == null)
            return;

        combat.ApplySpellArcDamage(target, rolled, wasCrit, element, sourceLabel);
    }
}
