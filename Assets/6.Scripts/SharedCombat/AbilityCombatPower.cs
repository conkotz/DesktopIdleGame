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
    public const string EnvenomAbilityId = "envenom";
    public const string CleavingStrikesAbilityId = "cleaving_strikes";
    public const string CrescentSlashAbilityId = "crescent_slash";
    public const string SoulforgedWeaponAbilityId = "soulforged_weapon";
    public const string LumberFrenzyAbilityId = "lumber_frenzy";
    public const string CleavingChopAbilityId = "cleaving_chop";
    public const string SpectralAxeAbilityId = "spectral_axe";
    public const string AvatarOfTheForestAbilityId = "avatar_of_the_forest";

    /// <summary>
    /// Flat add to the effective woodcutting speed multiplier while Avatar of the Forest is active (e.g. 1.52x → 1.62x).
    /// Not multiplied into <c>sheet × (1 + Forest Flow + …)</c>; keep in sync with ability tooltip copy (+10%).
    /// </summary>
    public const float AvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd = 0.10f;

    /// <summary>Second Twin Cyclone wave as a fraction of the first wave's scaled split (sync with Whirlwind runtime).</summary>
    public const float WhirlwindTwinCycloneSecondHitFraction = 0.2f;

    /// <summary>Cleaving Strikes secondary hits: fraction of rolled weapon split (sync with <see cref="PlayerAbilityController.BuildCleavingSecondarySplit"/>).</summary>
    public const float CleavingStrikesSecondaryHitWeaponDamageFraction = 0.6f;

    /// <summary>Combat-power heuristic: extra enemies credited for Crimson Spread / Contagion full radial payloads.</summary>
    private const float AilmentRadialSpreadAssumedExtraTargets = 2.5f;
    private const int SoulforgedWeaponChoiceSourceLevel = 35;
    private const int SoulforgedWeaponSwarmChoiceIndex = 0;
    private const int SoulforgedWeaponIndefiniteChoiceIndex = 1;
    private const float SoulforgedWeaponSwarmCount = 3f;
    private const float SoulforgedWeaponSwarmDamageMultiplier = 0.75f;
    private const float SoulforgedWeaponSwarmDurationSeconds = 20f;

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

        if (def.minionSpawnDefinition)
        {
            float mdps = MinionRuntimeStatsCalculator.EstimateMinionDamagePerSecond(
                stats,
                def.minionSpawnDefinition.combatConfig);
            float summonDur = Mathf.Max(0.1f, def.minionSpawnDefinition.summonDuration);
            float cooldown = Mathf.Max(0.01f, def.cooldown);

            if (string.Equals(def.abilityId, SoulforgedWeaponAbilityId, StringComparison.OrdinalIgnoreCase))
            {
                int selected = GetSoulforgedWeaponSelectedChoice();
                if (selected == SoulforgedWeaponSwarmChoiceIndex)
                {
                    mdps *= SoulforgedWeaponSwarmCount * SoulforgedWeaponSwarmDamageMultiplier;
                    summonDur = SoulforgedWeaponSwarmDurationSeconds;
                }
                else if (selected == SoulforgedWeaponIndefiniteChoiceIndex)
                {
                    return Mathf.Max(0f, mdps);
                }
            }

            // Summon cooldown starts after the summon expires, so the average cycle is active duration + cooldown.
            float cycleSeconds = summonDur + cooldown;
            return Mathf.Max(0f, mdps * (summonDur / cycleSeconds));
        }

        float physMult = def.physicalDamageMultiplier;
        float cd = Mathf.Max(0.01f, def.cooldown);
        ApplyPowerSlashChoiceAdjustments(def, ref physMult, ref cd);
        float extraHitFactor = 1f;
        ApplyWhirlwindChoiceAdjustments(def, ref cd, ref extraHitFactor);
        float critFactor = GetCritFactor(stats);

        float avgPhys = (stats.MinSplitDamage.physical + stats.MaxSplitDamage.physical) * 0.5f;
        float avgMag = (stats.MinSplitDamage.magic + stats.MaxSplitDamage.magic) * 0.5f;
        float avgCorruption = (stats.MinSplitDamage.corruptionDamage + stats.MaxSplitDamage.corruptionDamage) * 0.5f;

        // Power Slash: rolled hit × multipliers (+ extras), not a second additive copy of the roll.
        if (string.Equals(def.abilityId, PowerSlashAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float psAllM = def.GetEffectiveAllDamageMultiplier();
            float apM = stats.GetAbilityPowerDamageMultiplier(AbilityDefinition.StandardAbilityPowerCoefficient);
            float physEff = physMult <= 0f ? 1f : physMult;
            float magEff = physEff;
            float corrEff = physEff;
            float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
            float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
            float physExtra = (avgPhys * physEff + ailmentBonus) * apM * psAllM - avgPhys;
            float magExtra = (avgMag * magEff + elementBonus) * apM * psAllM - avgMag;
            float corrExtra = (avgCorruption * corrEff) * apM * psAllM - avgCorruption;
            float rawBonus = physExtra + magExtra + corrExtra;
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
                totalBleedDamage *= 1f + 0.12f * AilmentRadialSpreadAssumedExtraTargets; // Crimson Spread: all in radius (~2.5 targets).

            return Mathf.Max(0f, totalBleedDamage * procRate);
        }

        // Envenom: poison from corruption on the quick strike — marginal value drops if poison already applies often; stacks matter.
        if (string.Equals(def.abilityId, EnvenomAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);

            float poisonSourceCorruption = Mathf.Max(0f, avgCorruption);
            if (poisonSourceCorruption <= 0f)
                return 0f;

            int selected = GetEnvenomSelectedChoiceForCombatPower();
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
                totalPoisonDamage *= 1f + 0.12f * AilmentRadialSpreadAssumedExtraTargets; // Contagion: all in radius on death.

            return Mathf.Max(0f, totalPoisonDamage * procRate);
        }

        if (string.Equals(def.abilityId, CleavingStrikesAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            int extraTargets = 1;
            int empoweredHits = 4;
            float duration = 7f;
            int selected = GetCleavingStrikesSelectedChoiceForCombatPower();
            if (selected == 0)
            {
                extraTargets = 2;
                empoweredHits = 3;
                duration = 5f;
            }
            else if (selected == 1)
            {
                extraTargets = 1;
                empoweredHits = 6;
                duration = 10f;
            }

            float activeWindow = Mathf.Min(duration, aps > 0f ? (empoweredHits / aps) : duration);
            float uptime = activeWindow / Mathf.Max(0.01f, cd);
            uptime = Mathf.Clamp01(uptime);
            float cleaveExtraScale = extraTargets * CleavingStrikesSecondaryHitWeaponDamageFraction;
            float baseHit = Mathf.Max(0f, avgPhys + avgMag + avgCorruption) * critFactor;
            return baseHit * cleaveExtraScale * procRate * uptime;
        }

        // Default: instant cast (Whirlwind, Crescent Slash, etc.).
        float pEff = def.GetPhysicalHitScalingMultiplier();
        float mEff = def.GetMagicHitScalingMultiplier();
        float cEff = def.GetCorruptionHitScalingMultiplier();
        float allM = def.GetEffectiveAllDamageMultiplier();
        float elementBonusInstant = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonusInstant = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apMInstant = stats.GetAbilityPowerDamageMultiplier(AbilityDefinition.StandardAbilityPowerCoefficient);
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
        float raw =
            (avgPhys * pEff + ailmentBonusInstant) * allM * apMInstant
            + (avgMag * mEff * elemM + elementBonusInstant * elemM) * allM * apMInstant
            + (avgCorruption * cEff) * allM * apMInstant;
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

    private static int GetSoulforgedWeaponSelectedChoice()
    {
        SkillsManager skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, SoulforgedWeaponChoiceSourceLevel, -1);
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
            cooldownSeconds = Mathf.Max(0.01f, cooldownSeconds - 3f); // Relentless Flow
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

    /// <summary>Matches <see cref="PlayerAbilityController"/> choice keying (spine Lv15_0, then legacy keys).</summary>
    private static int GetWhirlwindSelectedChoiceForCombatPower()
    {
        const int whirlwindSourceLevel = 15;
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        int selected = sm.GetSkillChoiceSelection(SkillType.Melee, "Lv15_0", -1);
        if (selected >= 0)
            return selected;

        selected = sm.GetSkillChoiceSelection(SkillType.Melee, whirlwindSourceLevel, -1);
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

    private static int GetEnvenomSelectedChoiceForCombatPower()
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

        return sm.GetSkillChoiceSelection(SkillType.Melee, "Lv15_1", -1);
    }

    private static int GetCrescentSlashSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, "Lv15_2", -1);
    }
}
