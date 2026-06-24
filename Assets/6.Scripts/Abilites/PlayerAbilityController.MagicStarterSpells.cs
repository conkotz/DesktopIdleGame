using UnityEngine;

public partial class PlayerAbilityController
{
    private bool TryCastMagicStarterSpell(AbilityDefinition def, bool showLockedFeedback)
    {
        if (def == null || stats == null)
            return false;

        if (!MagicStarterSpellRules.IsMagicStarterSpellId(def.abilityId))
            return false;

        if (combat == null)
            combat = GetComponent<PlayerCombatController>();

        EnemyBaseController target = combat != null ? combat.CurrentTarget : null;
        if (target == null || target.IsDead)
        {
            if (showLockedFeedback)
                player?.ShowPopup("No valid target.");
            return false;
        }

        if (!TrySpendAbilityResourceCost(def, showLockedFeedback))
            return false;

        if (!MagicStarterSpellRules.TryGetBaseDamageBounds(def.abilityId, out float baseMin, out float baseMax))
            return false;

        MagicAttackType element = MagicStarterSpellRules.GetMagicAttackTypeForAbilityId(def.abilityId);
        float rolled = SpellDamageScaling.RollScaledElementDamage(stats, element, baseMin, baseMax);
        ApplyMagicStarterSpellHitDamage(target, def, element, rolled);

        player?.TriggerAttackAnim();
        StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        LogAbilityUsed(def);
        return true;
    }

    private void ApplyMagicStarterSpellHitDamage(
        EnemyBaseController target,
        AbilityDefinition def,
        MagicAttackType element,
        float baseDamage)
    {
        if (combat == null || target == null || target.IsDead || stats == null || def == null)
            return;

        bool wasCrit = false;
        float critMult = 1f;
        if (UnityEngine.Random.value < GetEffectiveAbilityCritChance(target))
        {
            wasCrit = true;
            critMult = Mathf.Max(1f, stats.CritMultiplier);
        }

        float damage = Mathf.Max(1f, baseDamage * critMult);
        SplitDamage rolled = new SplitDamage(0f, damage, 0f);
        string sourceLabel = GetAbilityOutgoingDamageSourceLabel(def.abilityId);
        combat.ApplyStaticArrowsCritArcDamage(target, rolled, wasCrit, sourceLabel);
        TryApplyMagicStarterSpellElementalAilment(target, element, damage);
    }

    private void TryApplyMagicStarterSpellElementalAilment(
        EnemyBaseController target,
        MagicAttackType element,
        float magicDealt)
    {
        if (target == null || stats == null || magicDealt <= 0f)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        switch (element)
        {
            case MagicAttackType.Fire:
                stats.TryApplyBurnFromDealtHit(ailments, magicDealt, 0f, transform);
                break;
            case MagicAttackType.Ice:
                if (UnityEngine.Random.value <= stats.MagicAilmentApplyChance)
                {
                    ailments.ApplyChillFromHit(new ChillPayload(
                        duration: stats.ChillDuration,
                        maxStacks: stats.ChillMaxStacks,
                        slowPerStack: stats.ChillSlowPerStack,
                        source: transform));
                }

                break;
            default:
                if (UnityEngine.Random.value <= stats.MagicAilmentApplyChance)
                {
                    ailments.ApplyShockFromHit(new ShockPayload(
                        duration: stats.ShockDuration,
                        damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
                        source: transform));
                }

                break;
        }
    }
}
