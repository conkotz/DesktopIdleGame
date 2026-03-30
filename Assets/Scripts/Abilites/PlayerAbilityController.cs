using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles ability cooldowns + executing ability effects.
/// Minimal implementation for "Power Slash" style abilities.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAbilityController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CharacterStats stats;
    [SerializeField] private PlayerCombatController combat;

    [Header("Global Cooldown")]
    [SerializeField, Min(0f)] private float globalCooldownSeconds = 0.15f;
    private float _globalCooldownEndsAt;

    private readonly Dictionary<string, float> _cooldownEndsById = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (!player) player = GetComponent<PlayerController>();
        if (!stats) stats = GetComponent<CharacterStats>();
        if (!combat) combat = GetComponent<PlayerCombatController>();
    }

    public bool IsOnCooldown(string abilityId, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (!_cooldownEndsById.TryGetValue(abilityId, out float end))
            return false;

        remainingSeconds = Mathf.Max(0f, end - Time.time);
        return remainingSeconds > 0f;
    }

    public float GetCooldownNormalized(string abilityId)
    {
        var def = AbilityLibrary.Get(abilityId);
        if (!def || def.cooldown <= 0f)
            return 0f;

        if (!IsOnCooldown(abilityId, out float remaining))
            return 0f;

        return Mathf.Clamp01(remaining / Mathf.Max(0.01f, def.cooldown));
    }

    public bool IsOnGlobalCooldown(out float remainingSeconds)
    {
        remainingSeconds = Mathf.Max(0f, _globalCooldownEndsAt - Time.time);
        return remainingSeconds > 0f;
    }

    public float GetGlobalCooldownNormalized()
    {
        if (globalCooldownSeconds <= 0f)
            return 0f;

        if (!IsOnGlobalCooldown(out float remaining))
            return 0f;

        return Mathf.Clamp01(remaining / Mathf.Max(0.01f, globalCooldownSeconds));
    }

    public bool TryUseAbility(string abilityId)
    {
        AbilityDefinition def = AbilityLibrary.Get(abilityId);
        if (!def)
            return false;

        if (globalCooldownSeconds > 0f && Time.time < _globalCooldownEndsAt)
            return false;

        if (IsOnCooldown(def.abilityId, out _))
            return false;

        if (!player || !stats)
            return false;

        if (def.energyCost > 0f && !player.SpendEnergy(def.energyCost))
        {
            player.ShowPopup("Not enough energy.");
            return false;
        }

        EnemyBaseController target = combat != null ? combat.CurrentTarget : null;
        if (target == null || target.IsDead)
        {
            player.ShowPopup("No target.");
            return false;
        }

        // Instant-cast damage model:
        // - base physical = average weapon physical hit (already includes buffs/gear via Min/Max split damage)
        // - apply physical multiplier to that base hit
        // - add ability power scaling on top
        float basePhysical =
            (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;

        float scaledPhysical = basePhysical * Mathf.Max(0f, def.physicalDamageMultiplier);
        float physicalBonus = Mathf.Max(0f, scaledPhysical - basePhysical);
        float apBonus = Mathf.Max(0f, stats.AbilityPower * Mathf.Max(0f, def.abilityPowerMultiplier));
        float raw = Mathf.Max(0f, scaledPhysical + apBonus);

        bool wasCrit = false;
        if (raw > 0f && UnityEngine.Random.value < Mathf.Clamp01(stats.CritChance))
        {
            wasCrit = true;
            raw *= Mathf.Max(1f, stats.CritMultiplier);
        }

        int final = Mathf.Max(0, Mathf.RoundToInt(raw));
        int dealt = 0;
        if (final > 0)
            dealt = target.TakeDamage(final, DamageType.Physical, wasCrit, transform);

        if (string.Equals(def.abilityId, "power_slash", StringComparison.OrdinalIgnoreCase))
        {
            player.ShowPopup("power slash used");
            float totalBonus = Mathf.Max(0f, physicalBonus + apBonus);
            Debug.Log(
                $"[Ability] Power Slash instant cast. " +
                $"BaseHit={basePhysical:0.##}, " +
                $"PhysicalBonus={physicalBonus:0.##}, " +
                $"ApBonus={apBonus:0.##}, " +
                $"TotalAbilityBonus={totalBonus:0.##}, " +
                $"FinalHitPreMitigation={raw:0.##}, " +
                $"Dealt={dealt}, crit={wasCrit}");
        }

        // Fire the attack anim as feedback, but do not modify basic attack cooldown timing.
        player.TriggerAttackAnim();
        StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        return true;
    }

    private void StartCooldown(AbilityDefinition def)
    {
        if (!def) return;
        float cd = Mathf.Max(0f, def.cooldown);
        if (cd <= 0f) return;
        _cooldownEndsById[def.abilityId] = Time.time + cd;
    }
}

