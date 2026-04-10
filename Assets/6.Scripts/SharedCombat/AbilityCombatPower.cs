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
    public const string WhirlwindAbilityId = "whirlwind";
    public const string RendAbilityId = "rend";
    public const string VenomJabAbilityId = "venom_jab";
    public const string CleavingStrikesAbilityId = "cleaving_strikes";
    public const string CrescentSlashAbilityId = "crescent_slash";

    /// <summary>Second Twin Cyclone wave as a fraction of the first wave's scaled split (sync with Whirlwind runtime).</summary>
    public const float WhirlwindTwinCycloneSecondHitFraction = 0.2f;

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

            SkillDatabase skillDb = SkillDatabase.LoadDefault();
            SkillDefinition skillDef = skillDb != null ? skillDb.Get(def.sourceSkill) : null;
            SkillsManager skillsMgr = SkillsManager.Instance;
            if (!SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skillDef, def, skillsMgr))
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: id='{a.id}' skipped (not committed / level-locked on skill tree).",
                        stats);
                continue;
            }

            if (!stats.IsAbilityUsableWithEquippedWeapon(def))
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] slot[{slotIndex}] index={slot.SlotIndex}: id='{a.id}' skipped (wrong equipped weapon for {def.requiredWeaponType}).",
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

        float physMult = def.physicalDamageMultiplier;
        float cd = Mathf.Max(0.01f, def.cooldown);
        ApplyPowerSlashChoiceAdjustments(def, ref physMult, ref cd);
        float extraHitFactor = 1f;
        ApplyWhirlwindChoiceAdjustments(def, ref cd, ref extraHitFactor);
        float critFactor = GetCritFactor(stats);

        float avgPhys = (stats.MinSplitDamage.physical + stats.MaxSplitDamage.physical) * 0.5f;
        float avgMag = (stats.MinSplitDamage.magic + stats.MaxSplitDamage.magic) * 0.5f;
        float avgCorruption = (stats.MinSplitDamage.corruptionDamage + stats.MaxSplitDamage.corruptionDamage) * 0.5f;
        float magMult = def.magicDamageMultiplier;
        float apMult = def.abilityPowerMultiplier;

        // Power Slash: bonus on top of a normal weapon hit; proc rate limited by attack speed and ability cooldown.
        if (string.Equals(def.abilityId, PowerSlashAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float bonusPhys = avgPhys * physMult;
            float bonusMag = avgMag * magMult;
            float apM = stats.GetAbilityPowerDamageMultiplier(apMult);
            float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
            float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
            float rawBonus = (bonusPhys + ailmentBonus) * apM + (bonusMag + elementBonus) * apM;
            float perEnhancedHit = rawBonus * critFactor;
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            return Mathf.Max(0f, perEnhancedHit * procRate);
        }

        // Rend: exclusive bleed on proc — value scales with how much "guaranteed bleed" improves over baseline chance.
        // Duration barely moves CP (longer DoT spreads the same pool in runtime; avoid inflating from +3s / stat duration).
        if (string.Equals(def.abilityId, RendAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            float baseDur = Mathf.Max(1f, stats.BleedBaseDuration);
            float duration = Mathf.Max(1f, stats.BleedDuration) + 3f;
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
            float durRatio = ticks / baseDur;
            durRatio = Mathf.Clamp(durRatio, 1f, 1.1f);

            float totalBleedDamage = Mathf.Max(0f, avgPhys * (1f + stats.BleedMultiplier) * durRatio);

            float marginalBleedProc = 1f - Mathf.Clamp01(stats.BleedChance);
            float reliabilityWeight = Mathf.Lerp(0.22f, 1f, marginalBleedProc);
            totalBleedDamage *= reliabilityWeight;

            int selected = GetRendSelectedChoiceForCombatPower();
            if (selected == 0)
                totalBleedDamage *= 1.05f; // Hemorrhaging Rush: same total, faster — small throughput bump.
            else if (selected == 1)
                totalBleedDamage *= 1.12f; // Crimson Spread: conditional spread — modest uplift.

            return Mathf.Max(0f, totalBleedDamage * procRate);
        }

        // Venom Jab: poison from corruption on the quick strike — marginal value drops if poison already applies often; stacks matter.
        if (string.Equals(def.abilityId, VenomJabAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            const float quickStrikeMultiplier = 0.75f;

            float poisonSourceCorruption = Mathf.Max(0f, avgCorruption * quickStrikeMultiplier);
            if (poisonSourceCorruption <= 0f)
                return 0f;

            int selected = GetVenomJabSelectedChoiceForCombatPower();
            int baseStacks = Mathf.Max(1, stats.PoisonMaxStacks);
            int stackCount = baseStacks;
            if (selected == 0)
                stackCount = baseStacks + 2; // Potent Venom: extra stacks are a large part of the upgrade.

            float poisonPerStackTotal =
                poisonSourceCorruption * stats.PoisonPoolFractionOfCorruptionDamage *
                (1f + Mathf.Max(0f, stats.PoisonMultiplier));
            float totalPoisonDamage = poisonPerStackTotal * stackCount;

            float marginalPoisonProc = 1f - Mathf.Clamp01(stats.PoisonChance);
            float reliabilityWeight = Mathf.Lerp(0.28f, 1f, marginalPoisonProc);
            totalPoisonDamage *= reliabilityWeight;

            if (selected == 0)
                totalPoisonDamage *= 1.12f; // Extra emphasis on stack-cap path beyond raw stackCount.
            else if (selected == 1)
                totalPoisonDamage *= 1.12f; // Contagion Burst: conditional spread.

            return Mathf.Max(0f, totalPoisonDamage * procRate);
        }

        if (string.Equals(def.abilityId, CleavingStrikesAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            int extraTargets = 1;
            int empoweredHits = 5;
            float duration = 10f;
            int selected = GetCleavingStrikesSelectedChoiceForCombatPower();
            if (selected == 0)
            {
                extraTargets = 3;
                empoweredHits = 4;
                duration = 8f;
            }
            else if (selected == 1)
            {
                extraTargets = 1;
                empoweredHits = 7;
                duration = 14f;
            }

            float activeWindow = Mathf.Min(duration, aps > 0f ? (empoweredHits / aps) : duration);
            float uptime = activeWindow / Mathf.Max(0.01f, cd);
            uptime = Mathf.Clamp01(uptime);
            float aoeScale = Mathf.Clamp(extraTargets * 0.45f, 0f, 1.1f);
            float baseHit = Mathf.Max(0f, avgPhys + avgMag + avgCorruption) * critFactor;
            return baseHit * aoeScale * procRate * uptime;
        }

        // Default: instant cast (Whirlwind, Crescent Slash, etc.) — matches scaled split + small AoE lift where relevant.
        float scaledPhys = avgPhys * physMult;
        float scaledMag = avgMag * magMult;
        float scaledCorruption = avgCorruption * physMult; // same rule as PlayerAbilityController.BuildWhirlwindAbilityScaledSplit
        float elementBonusInstant = AbilityElementScaling.GetElementDamageBonus(def, stats);
        AbilityElementScaling.ScaleMagicAbilityContributions(scaledMag, elementBonusInstant, stats, out float sm, out float se);
        float ailmentBonusInstant = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apMInstant = stats.GetAbilityPowerDamageMultiplier(apMult);
        float raw = (scaledPhys + scaledCorruption) * apMInstant + (sm + se + ailmentBonusInstant) * apMInstant;
        float perCast = raw * critFactor * Mathf.Max(1f, extraHitFactor);
        float dps = Mathf.Max(0f, perCast / cd);

        if (string.Equals(def.abilityId, CrescentSlashAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            int selected = GetCrescentSlashSelectedChoiceForCombatPower();
            float expectedExtraHits = selected == 1 ? 2.5f : 2f; // soft estimate of extra enemies over primary
            float aoeLift = 1f + 0.055f * expectedExtraHits;
            if (selected == 0)
                aoeLift *= 1.04f; // Elemental Crescent: minor utility.
            return dps * aoeLift;
        }

        if (string.Equals(def.abilityId, WhirlwindAbilityId, StringComparison.OrdinalIgnoreCase))
            return dps * 1.08f; // Slight AoE coverage on top of per-target scaled damage (Twin Cyclone already in extraHitFactor).

        return dps;
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

    private static void ApplyWhirlwindChoiceAdjustments(AbilityDefinition def, ref float cooldownSeconds, ref float extraHitFactor)
    {
        if (!def || !string.Equals(def.abilityId, WhirlwindAbilityId, StringComparison.OrdinalIgnoreCase))
            return;

        int selected = GetWhirlwindSelectedChoiceForCombatPower();
        if (selected == 0)
        {
            // Twin Cyclone: second wave damage fraction (matches runtime second hit).
            extraHitFactor += WhirlwindTwinCycloneSecondHitFraction;
        }
        else if (selected == 1)
        {
            // Expansive Whirl: larger radius (coverage utility). Keep single-target CP neutral.
        }
    }

    /// <summary>Matches <see cref="PlayerAbilityController"/> choice keying (Lv15 row, Lv18 fallback).</summary>
    private static int GetWhirlwindSelectedChoiceForCombatPower()
    {
        const int whirlwindSourceLevel = 15;
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        int selected = sm.GetSkillChoiceSelection(SkillType.Melee, whirlwindSourceLevel, -1);
        if (selected >= 0)
            return selected;

        return sm.GetSkillChoiceSelection(SkillType.Melee, whirlwindSourceLevel + 3, -1);
    }

    private static int GetRendSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
    }

    private static int GetVenomJabSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
    }

    private static int GetCleavingStrikesSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, 15, -1);
    }

    private static int GetCrescentSlashSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, 15, -1);
    }
}
