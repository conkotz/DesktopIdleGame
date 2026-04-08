using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estimates how much combat power action-bar abilities add, using the same building blocks as
/// <see cref="PlayerAbilityController"/> (weapon-average physical hit, ability multipliers, AP, crit, cooldown).
/// Only abilities currently assigned to the <see cref="ActionBarUI"/> count; duplicate slots with the same
/// ability id share one cooldown, so each unique id is counted once.
/// </summary>
public static class AbilityCombatPower
{
    /// <summary>Matches <see cref="PlayerAbilityController"/> Power Slash id — attack-queued bonus, not raw / cooldown.</summary>
    public const string PowerSlashAbilityId = "power_slash";

    /// <summary>Expected sustained DPS from all uniquely slotted abilities (0 if not the player or no bar).</summary>
    public static float EstimateTotalSlottedAbilityDps(CharacterStats stats, bool logDiagnostics = false)
    {
        if (!stats)
            return 0f;

        bool log = logDiagnostics;

        ActionBarUI bar = UnityEngine.Object.FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (!bar)
        {
            if (log)
                Debug.LogWarning(
                    "[AbilityCombatPower] No ActionBarUI in loaded scenes (FindFirstObjectByType returned null). Ability DPS = 0.",
                    stats);
            return 0f;
        }

        PlayerAbilityController abilityController =
            stats.GetComponent<PlayerAbilityController>() ?? stats.GetComponentInParent<PlayerAbilityController>();

        AbilityDatabase barDb = bar.GetAbilityDatabaseOrDefault();
        AbilityDatabase playerDb = abilityController != null ? abilityController.GetDatabaseOrDefault() : null;

        if (log)
        {
            Debug.Log(
                "[AbilityCombatPower] --- begin ---" +
                $"\n  stats: {stats.gameObject.name} path={BuildHierarchyPath(stats.transform)}" +
                $"\n  actionBar: {bar.gameObject.name} path={BuildHierarchyPath(bar.transform)}" +
                $"\n  playerAbilityController: {(abilityController ? abilityController.gameObject.name : "NULL")}" +
                $"\n  barDb: {DescribeDb(barDb)}  playerDb: {DescribeDb(playerDb)}",
                stats);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        float total = 0f;
        int slotIndex = -1;

        foreach (ActionBarSlotUI slot in bar.GetSlots())
        {
            slotIndex++;
            if (!slot)
            {
                if (log)
                    Debug.Log($"[AbilityCombatPower] slot[{slotIndex}]: (null ActionBarSlotUI)", stats);
                continue;
            }

            ActionBarAssignment a = slot.AssignedAction;
            if (a == null)
            {
                if (log)
                    Debug.Log($"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: AssignedAction is null", stats);
                continue;
            }

            if (!a.IsAssigned)
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: empty (kind={a.kind}, id='{a.id}')",
                        stats);
                continue;
            }

            if (!a.IsAbility)
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: not an ability (kind={a.kind}, id='{a.id}')",
                        stats);
                continue;
            }

            if (string.IsNullOrWhiteSpace(a.id))
            {
                if (log)
                    Debug.LogWarning(
                        $"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: IsAbility but id is blank",
                        stats);
                continue;
            }

            if (!seen.Add(a.id))
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: duplicate id '{a.id}' (skipped — already counted)",
                        stats);
                continue;
            }

            AbilityDefinition def = ResolveAbilityDefinition(a.id, barDb, playerDb, out string resolution);
            if (!def)
            {
                if (log)
                    Debug.LogWarning(
                        $"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: could not resolve AbilityDefinition for id='{a.id}' (tried bar → player → FindDefinitionById).",
                        stats);
                continue;
            }

            float dps = EstimateAbilityDps(def, stats);
            total += dps;

            if (log)
            {
                Debug.Log(
                    $"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: id='{a.id}' → def={def.displayName} ({def.abilityId}) " +
                    $"resolve={resolution}  estDps={dps:F4}",
                    stats);
            }
        }

        if (log)
            Debug.Log($"[AbilityCombatPower] --- end --- totalAbilityDps={total:F4}", stats);

        return Mathf.Max(0f, total);
    }

    private static string DescribeDb(AbilityDatabase db)
    {
        if (!db)
            return "null";
        return $"{db.name} ({db.GetInstanceID()})";
    }

    private static string BuildHierarchyPath(Transform t)
    {
        if (!t)
            return "";
        var stack = new System.Collections.Generic.List<string>(8);
        Transform walk = t;
        while (walk != null)
        {
            stack.Add(walk.name);
            walk = walk.parent;
        }
        stack.Reverse();
        return string.Join("/", stack);
    }

    private static AbilityDefinition ResolveAbilityDefinition(
        string abilityId,
        AbilityDatabase barDb,
        AbilityDatabase playerDb,
        out string resolutionSource)
    {
        resolutionSource = "none";
        AbilityDefinition def = barDb ? barDb.Get(abilityId) : null;
        if (def)
        {
            resolutionSource = "barDb";
            return def;
        }

        def = playerDb ? playerDb.Get(abilityId) : null;
        if (def)
        {
            resolutionSource = "playerDb";
            return def;
        }

        def = AbilityDatabase.FindDefinitionById(abilityId);
        if (def)
        {
            resolutionSource = "FindDefinitionById";
            return def;
        }

        return null;
    }

    /// <summary>
    /// Single-ability DPS estimate (for tooling / UI). Uses crit on the full hit like instant abilities in <see cref="PlayerAbilityController.TryUseAbility"/>.
    /// </summary>
    public static float EstimateAbilityDps(AbilityDefinition def, CharacterStats stats)
    {
        if (!def || !stats)
            return 0f;

        float physMult = Mathf.Max(0f, def.physicalDamageMultiplier);
        float cd = Mathf.Max(0.01f, def.cooldown);
        ApplyPowerSlashChoiceAdjustments(def, ref physMult, ref cd);
        float critFactor = GetCritFactor(stats);

        float avgPhys = (stats.MinSplitDamage.physical + stats.MaxSplitDamage.physical) * 0.5f;
        float avgMag = (stats.MinSplitDamage.magical + stats.MaxSplitDamage.magical) * 0.5f;
        float magMult = Mathf.Max(0f, def.magicalDamageMultiplier);
        float apMult = Mathf.Max(0f, def.abilityPowerMultiplier);
        float ap = stats.AbilityPower;

        // Power Slash: bonus on top of a normal weapon hit; proc rate limited by attack speed and ability cooldown.
        if (string.Equals(def.abilityId, PowerSlashAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float bonusPhys = avgPhys * Mathf.Max(0f, physMult - 1f);
            float bonusMag = avgMag * Mathf.Max(0f, magMult - 1f);
            float apBonus = ap * apMult;
            float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
            float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
            float rawBonus = Mathf.Max(0f, bonusPhys + bonusMag + apBonus + elementBonus + ailmentBonus);
            float perEnhancedHit = rawBonus * critFactor;
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            return perEnhancedHit * procRate;
        }

        // Default: instant cast nuke (same structure as PlayerAbilityController instant branch).
        float scaledPhys = avgPhys * physMult;
        float scaledMag = avgMag * magMult;
        float elementBonusInstant = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonusInstant = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apBonusInstant = ap * apMult;
        float raw = Mathf.Max(0f, scaledPhys + scaledMag + elementBonusInstant + apBonusInstant + ailmentBonusInstant);
        float perCast = raw * critFactor;
        return perCast / cd;
    }

    private static float GetCritFactor(CharacterStats stats)
    {
        float cc = Mathf.Clamp01(stats.CritChance);
        float cm = Mathf.Max(1f, stats.CritMultiplier);
        return 1f + cc * (cm - 1f);
    }

    private static void ApplyPowerSlashChoiceAdjustments(AbilityDefinition def, ref float physicalMultiplier, ref float cooldownSeconds)
    {
        if (!def || !string.Equals(def.abilityId, PowerSlashAbilityId, StringComparison.OrdinalIgnoreCase))
            return;

        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return;

        int selected = sm.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
        if (selected == 0)
        {
            physicalMultiplier += 0.25f; // Brutal Cut
        }
        else if (selected == 1)
        {
            cooldownSeconds = Mathf.Max(0.01f, cooldownSeconds - 5f); // Relentless Flow
        }
    }
}
