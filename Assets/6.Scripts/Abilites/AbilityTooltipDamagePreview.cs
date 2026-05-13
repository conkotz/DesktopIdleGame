using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Ability tooltip damage preview: average weapon split × multipliers and ability power (× per-ability coefficient / 100), before crit.
/// Crit still applies when the hit resolves; values here match runtime scaling, not expected DPS.
/// </summary>
public static class AbilityTooltipDamagePreview
{
    /// <summary>Scaling lines in ability tooltips (TMP rich text). Cool accent vs orange (#FFB347) effects on the action bar.</summary>
    private const string TooltipScalingAccentColorOrangeMode = "#9DD4FF";

    /// <summary>Scaling lines when effects are default/white (e.g. skills ability list).</summary>
    private const string TooltipScalingAccentColorPlain = "#B0C8DD";

    /// <summary>Inherit-mode minions: no per-type damage numbers — one global rule (orange, like Power Slash primary effects).</summary>
    private const string InheritMinionDamageRuleLine =
        "This minion inherits a portion of the damage from your total damage";

    /// <summary>Blue scaling line for inherit Soulforged-style minions (matches Power Slash "Deals %..." accent).</summary>
    private const string InheritMinionDealsBonusScalingLine =
        "Deals bonus damage from minion damage scaling (reduced for inherited minions)";

    private const int SoulforgedWeaponChoiceSourceLevel = 35;
    private const int SoulforgedWeaponSwarmChoiceIndex = 0;
    private const int SoulforgedWeaponIndefiniteChoiceIndex = 1;
    private const float SoulforgedWeaponSwarmDurationSeconds = 20f;

    /// <summary>Rich-text tag line for ability category (prepend above description). Empty if not applicable.</summary>
    public static string BuildAbilityTooltipTagLine(AbilityDefinition def, bool orangeMarkup)
    {
        if (!def)
            return "";

        string tag = ResolveAbilityTagLabel(def);
        if (string.IsNullOrEmpty(tag))
            return "";

        return orangeMarkup
            ? $"<color=#FFB347>{tag}</color>"
            : $"<color=#B0C8DD>{tag}</color>";
    }

    /// <summary>
    /// Reads the asset-driven <see cref="AbilityDefinition.tag"/>. Untagged assets fall back to
    /// auto-detection so the historical "Minion" label keeps working until they are tagged manually.
    /// </summary>
    private static string ResolveAbilityTagLabel(AbilityDefinition def)
    {
        if (!def)
            return null;

        switch (def.tag)
        {
            case AbilityTag.Active:
                return "Active";
            case AbilityTag.Minion:
                return "Minion";
            case AbilityTag.Buff:
                return "Buff";
        }

        // Legacy fallback for assets that haven't been tagged in the inspector yet.
        if (def.SpawnsMinionOnCast)
            return "Minion";
        return null;
    }

    public static CharacterStats FindLocalPlayerStats()
    {
        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            var s = player.GetComponent<CharacterStats>();
            if (s != null)
                return s;
        }

        return Object.FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);
    }

    public static string FormatPhysSuffix(CharacterStats stats, float physicalMultiplier)
    {
        if (stats == null)
            return "";

        float avgPhys = (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        int n = Mathf.RoundToInt(avgPhys * Mathf.Max(0f, physicalMultiplier));
        if (n <= 0)
            return "";

        return $" ({n} phys)";
    }

    /// <summary>Optional suffix: total % bonus from ability power at current stats (default coef = <see cref="AbilityDefinition.StandardAbilityPowerCoefficient"/>).</summary>
    public static string FormatAbilityPowerSuffix(CharacterStats stats, float abilityPowerCoefficient = AbilityDefinition.StandardAbilityPowerCoefficient)
    {
        if (stats == null)
            return "";

        float bonusPct = Mathf.Max(0f, stats.AbilityPower) * Mathf.Max(0f, abilityPowerCoefficient);
        if (bonusPct <= 0f)
            return "";

        return $" (+{bonusPct:0.#}% from AP)";
    }

    /// <summary>
    /// TMP rich-text line for ability tooltips. Empty when <see cref="AbilityWeaponRequirement.Any"/>
    /// (Spectral Axe is an exception: it always shows a toolbelt-axe requirement line).
    /// Red when the requirement is not met; neutral or orange (action bar) when it is.
    /// </summary>
    public static string BuildWeaponRequirementRichLine(AbilityDefinition def, CharacterStats stats, bool orangeWhenOk = false)
    {
        if (def == null)
            return "";

        if (IsSpectralAxe(def))
        {
            var pac = Object.FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);
            bool axeOk = stats == null || (pac != null && pac.HasAxeInToolbelt());
            const string axeRequirementText = "Required: Axe in toolbelt";
            if (!axeOk)
                return $"<color=#FF5C5C>{axeRequirementText}</color>";
            if (orangeWhenOk)
                return $"<color=#FFB347>{axeRequirementText}</color>";
            return axeRequirementText;
        }

        if (def.requiredWeaponType == AbilityWeaponRequirement.Any)
            return "";

        string label = def.requiredWeaponType switch
        {
            AbilityWeaponRequirement.Melee => "Melee",
            AbilityWeaponRequirement.Ranged => "Ranged",
            AbilityWeaponRequirement.Magic => "Magic",
            AbilityWeaponRequirement.MeleeOrRanged => "Melee or Ranged",
            _ => "Any"
        };

        string line = $"Required: {label} weapon";
        bool ok = stats == null || stats.IsAbilityUsableWithEquippedWeapon(def);
        if (!ok)
            return $"<color=#FF5C5C>{line}</color>";

        if (orangeWhenOk)
            return $"<color=#FFB347>{line}</color>";

        return line;
    }

    private static bool IsPowerSlash(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.PowerSlashAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsRend(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.RendAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsEnvenom(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.EnvenomAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsCleavingStrikes(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.CleavingStrikesAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsCrescentSlash(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.CrescentSlashAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsWhirlwind(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.WhirlwindAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsSoulforgedWeapon(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.SoulforgedWeaponAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsLumberFrenzy(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.LumberFrenzyAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsCleavingChop(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.CleavingChopAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsSpectralAxe(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.SpectralAxeAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsAvatarOfTheForest(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.AvatarOfTheForestAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static int GetWoodcuttingSkillRow5Choice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        // Lumber Frenzy is the only ability at Lv5 (slot 0). The spine-keyed read also falls back to
        // the legacy "5" key so older saves keep working.
        return skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv5_0", -1);
    }

    /// <summary>Returns the enhancement choice for Cleaving Chop (Woodcutting Lv25 slot 0).</summary>
    private static int GetCleavingChopChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv25_0", -1);
    }

    /// <summary>Returns the enhancement choice for Spectral Axe (Woodcutting Lv25 slot 1).</summary>
    private static int GetSpectralAxeChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv25_1", -1);
    }

    private static int GetAvatarOfTheForestChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv50_0", -1);
    }

    private static int GetMeleeLv15BranchChoice(SkillsManager skillsManager, int slot012)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, $"Lv15_{Mathf.Clamp(slot012, 0, 7)}", -1);
    }

    private static int GetMeleeSkillRow5Choice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
    }

    /// <summary>
    /// Compact tooltip: Effects, then optional Deals/Ability Power scaling lines, then Energy • Cooldown.
    /// Damage numbers are pre-crit; crit multiplies them in combat.
    /// </summary>
    public static string BuildAbilityTooltipStatsSection(
        AbilityDefinition def,
        CharacterStats stats,
        SkillsManager skillsManager,
        bool orangeMarkup)
    {
        if (!def)
            return "";

        string O(string line) => orangeMarkup ? $"<color=#FFB347>{line}</color>" : line;
        string S(string line) =>
            orangeMarkup
                ? $"<color={TooltipScalingAccentColorOrangeMode}>{line}</color>"
                : $"<color={TooltipScalingAccentColorPlain}>{line}</color>";

        float physMult = def.physicalDamageMultiplier;
        float cooldown = Mathf.Max(0f, def.cooldown);
        AbilityTooltipAdjustments.ApplySkillTreeChoices(def, skillsManager, ref physMult, ref cooldown);

        float magMult = def.magicDamageMultiplier;
        float allM = def.GetEffectiveAllDamageMultiplier();
        const float scalingEpsilon = 0.0001f;
        float pEffTip = physMult > scalingEpsilon ? physMult : def.GetPhysicalHitScalingMultiplier();
        float mEffTip = magMult > scalingEpsilon ? magMult : def.GetMagicHitScalingMultiplier();
        float cEffTip = def.GetCorruptionHitScalingMultiplier();
        if (IsPowerSlash(def))
        {
            mEffTip = pEffTip;
            cEffTip = pEffTip;
        }

        float tipAp = stats ? Mathf.Max(0f, stats.AbilityPower) : 0f;
        float energy = Mathf.Max(0f, def.energyCost);
        bool showApInEffects =
            !IsCleavingStrikes(def) && !IsRend(def) && !IsEnvenom(def) && !def.SpawnsMinionOnCast;
        int tooltipApBonus = showApInEffects && stats != null
            ? ComputeTooltipApBonusDamage(def, stats, pEffTip, mEffTip, cEffTip, allM)
            : 0;

        var body = new StringBuilder();
        body.AppendLine(O("Effects:"));

        if (IsLumberFrenzy(def))
        {
            AppendLumberFrenzyTooltipEffects(body, O, skillsManager);
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (IsAvatarOfTheForest(def))
        {
            AppendAvatarOfTheForestTooltipEffects(body, O, skillsManager);
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown (begins after the buff ends)"));
            return body.ToString().TrimEnd();
        }

        if (IsCleavingChop(def))
        {
            AppendCleavingChopTooltipEffects(body, O, skillsManager);
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (IsSpectralAxe(def))
        {
            AppendSpectralAxeTooltipEffects(body, O, skillsManager);
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (def.SpawnsMinionOnCast && def.minionSpawnDefinition)
        {
            if (stats != null)
                AppendMinionSpawnTooltipEffects(body, O, S, def, stats, skillsManager);
            else
                AppendMinionSpawnTooltipEffectsNoStats(body, O, S, def, skillsManager);

            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (IsRend(def))
        {
            body.AppendLine(O("100% bleed on next hit if physical damage is dealt"));
            body.AppendLine(O("+3s duration"));
            if (GetMeleeSkillRow5Choice(skillsManager) == 1)
                body.AppendLine(O("Crimson Spread"));
        }
        else if (IsEnvenom(def))
        {
            body.AppendLine(O("100% poison on next hit if corruption damage is dealt."));
            body.AppendLine(O("Apply max poison stacks"));
            if (GetMeleeSkillRow5Choice(skillsManager) == 1)
                body.AppendLine(O("Contagion Burst"));
        }
        else if (IsCleavingStrikes(def))
        {
            int cleaveSel = GetMeleeLv15BranchChoice(skillsManager, 1);
            if (cleaveSel == 0)
                body.AppendLine(O("+2 nearby enemies per strike (5s, 3 hits); 40% reduced damage on cleaved hits."));
            else if (cleaveSel == 1)
                body.AppendLine(O("+1 nearby enemy per strike (10s, 6 hits); 40% reduced damage on cleaved hits."));
            else
                body.AppendLine(O("+1 nearby enemy per strike (7s, 4 hits); 40% reduced damage on cleaved hits."));
        }
        else if (IsCrescentSlash(def))
        {
            int crescentSel = GetMeleeLv15BranchChoice(skillsManager, 2);

            float avgPhys = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f
                : 0f;
            float avgMag = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f
                : 0f;
            float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
            float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
            float elemM = stats != null ? AbilityElementScaling.GetElementSkillDamageMultiplier(stats) : 1f;
            float physEff = def.GetPhysicalHitScalingMultiplier();
            float magEff = def.GetMagicHitScalingMultiplier();
            float physTotalNoAp = (avgPhys * physEff + ailmentBonus) * allM;
            float magTotalNoAp = (avgMag * magEff * elemM + elementBonus * elemM) * allM;
            float physScaler = physTotalNoAp - avgPhys;
            float magScaler = magTotalNoAp - avgMag;

            if (crescentSel == 0)
            {
                float ph = Mathf.Max(0f, physScaler);
                float move = ph * 0.5f;
                physScaler = ph - move;
                magScaler += move;
            }

            string dmgSuffix = DamageTimingSuffix();
            bool splitApInEffects = showApInEffects && tipAp > 0f;
            if (splitApInEffects)
            {
                AppendDecimalScalerEffectLines(body, O, physScaler, magScaler, dmgSuffix);
                if (Mathf.Abs(physScaler) < 0.05f && Mathf.Abs(magScaler) < 0.05f)
                    body.AppendLine(O("Base hit damage"));
                body.AppendLine(O($"+{tooltipApBonus} damage from Ability Power{dmgSuffix}"));
            }
            else
            {
                int p = Mathf.RoundToInt(physScaler);
                int m = Mathf.RoundToInt(magScaler);
                if (p != 0)
                    body.AppendLine(O(FormatSignedDamageLine(p, "Physical", dmgSuffix)));
                if (m != 0)
                    body.AppendLine(O(FormatSignedDamageLine(m, "Magic", dmgSuffix)));
                if (p == 0 && m == 0)
                    body.AppendLine(O("Base hit damage"));
            }

            if (crescentSel == 1)
                body.AppendLine(O("Hits all enemies."));
            else
                body.AppendLine(O("Hits 3 enemies."));
        }
        else if (IsWhirlwind(def))
        {
            float avgPhys = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f
                : 0f;
            float avgMag = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f
                : 0f;

            float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
            float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
            float elemM = stats != null ? AbilityElementScaling.GetElementSkillDamageMultiplier(stats) : 1f;
            float physTotalNoAp = (avgPhys * pEffTip + ailmentBonus) * allM;
            float magTotalNoAp = (avgMag * mEffTip * elemM + elementBonus * elemM) * allM;
            float physScaler = physTotalNoAp - avgPhys;
            float magScaler = magTotalNoAp - avgMag;

            string dmgSuffix = DamageTimingSuffix();
            bool splitApInEffects = showApInEffects && tipAp > 0f;
            if (splitApInEffects)
            {
                AppendDecimalScalerEffectLines(body, O, physScaler, magScaler, dmgSuffix);
                if (Mathf.Abs(physScaler) < 0.05f && Mathf.Abs(magScaler) < 0.05f)
                    body.AppendLine(O("Base hit damage"));
                body.AppendLine(O($"+{tooltipApBonus} damage from Ability Power{dmgSuffix}"));
            }
            else
            {
                int p = Mathf.RoundToInt(physScaler);
                int m = Mathf.RoundToInt(magScaler);
                if (p != 0)
                    body.AppendLine(O(FormatSignedDamageLine(p, "Physical", dmgSuffix)));
                if (m != 0)
                    body.AppendLine(O(FormatSignedDamageLine(m, "Magic", dmgSuffix)));
                if (p == 0 && m == 0)
                    body.AppendLine(O("Base hit damage"));
            }

            int wwEnhance = GetMeleeLv15BranchChoice(skillsManager, 0);
            if (wwEnhance == 0)
                body.AppendLine(O("Twin Cyclone: hits each enemy a second time for 20% of the first wave."));
        }
        else
        {
            float avgPhys = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f
                : 0f;
            float avgMag = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f
                : 0f;
            float physScaler;
            float magScaler;

            if (IsPowerSlash(def))
            {
                float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
                float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
                float physEff = physMult <= 0f ? 1f : physMult;
                float magEff = physEff;
                float physTotalNoAp = (avgPhys * physEff + ailmentBonus) * allM;
                float magTotalNoAp = (avgMag * magEff + elementBonus) * allM;
                physScaler = physTotalNoAp - avgPhys;
                magScaler = magTotalNoAp - avgMag;
            }
            else
            {
                float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
                float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
                float elemM = stats != null ? AbilityElementScaling.GetElementSkillDamageMultiplier(stats) : 1f;
                float physTotalNoAp = (avgPhys * pEffTip + ailmentBonus) * allM;
                float magTotalNoAp = (avgMag * mEffTip * elemM + elementBonus * elemM) * allM;
                physScaler = physTotalNoAp - avgPhys;
                magScaler = magTotalNoAp - avgMag;
            }

            string dmgSuffix = DamageTimingSuffix();
            bool splitApInEffects = showApInEffects && tipAp > 0f;
            if (splitApInEffects)
            {
                AppendDecimalScalerEffectLines(body, O, physScaler, magScaler, dmgSuffix);
                if (Mathf.Abs(physScaler) < 0.05f && Mathf.Abs(magScaler) < 0.05f)
                    body.AppendLine(O("Base hit damage"));
                body.AppendLine(O($"+{tooltipApBonus} damage from Ability Power{dmgSuffix}"));
            }
            else
            {
                int p = Mathf.RoundToInt(physScaler);
                int m = Mathf.RoundToInt(magScaler);
                if (p != 0)
                    body.AppendLine(O(FormatSignedDamageLine(p, "Physical", dmgSuffix)));
                if (m != 0)
                    body.AppendLine(O(FormatSignedDamageLine(m, "Magic", dmgSuffix)));
                if (p == 0 && m == 0)
                    body.AppendLine(O("Base hit damage"));
            }
        }

        // 0 = omit pass-through (100% of that split); >0 shows an explicit % line.
        bool showPhysScaling = physMult > scalingEpsilon;
        bool showMagScaling = magMult > scalingEpsilon;
        bool showCorrScaling = def.corruptionDamageMultiplier > scalingEpsilon;
        bool showAllScaling = def.allDamageMultiplier > 0f && Mathf.Abs(allM - 1f) > scalingEpsilon;

        bool scalingAllowed = !IsCleavingStrikes(def);
        bool hasWeaponScalingLines = scalingAllowed &&
            (showPhysScaling || showMagScaling || showCorrScaling || showAllScaling);
        if (hasWeaponScalingLines)
            body.AppendLine(string.Empty);

        if (hasWeaponScalingLines)
        {
            if (IsPowerSlash(def))
            {
                body.AppendLine(S(
                    $"Deals {pEffTip * 100f:0.#}% of your hit damage"));
            }
            else if (showAllScaling)
            {
                body.AppendLine(S(
                    $"Deals {allM * 100f:0.#}% of your hit damage"));
            }
            else
            {
                if (showPhysScaling)
                {
                    body.AppendLine(S(
                        $"Deals {pEffTip * 100f:0.#}% of your Physical hit damage"));
                }
                if (showMagScaling)
                {
                    body.AppendLine(S(
                        $"Deals {mEffTip * 100f:0.#}% of your Magic hit damage"));
                }
                if (showCorrScaling)
                {
                    body.AppendLine(S(
                        $"Deals {cEffTip * 100f:0.#}% of your Corruption hit damage"));
                }
            }
        }

        body.AppendLine(string.Empty);
        body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));

        return body.ToString().TrimEnd();
    }

    private static void AppendLumberFrenzyTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        const float baseChoppingSpeedPct = 20f;
        const float baseGritChancePct = 10f;
        const float sturdyGripStaminaEffPct = 15f;
        const float ironGritExtraGritPct = 5f;

        int choice = GetWoodcuttingSkillRow5Choice(skillsManager);
        float gritTotal = baseGritChancePct + (choice == 1 ? ironGritExtraGritPct : 0f);

        body.AppendLine(O($"+{baseChoppingSpeedPct:0.#}% Woodcutting Speed"));
        body.AppendLine(O($"+{gritTotal:0.#}% Woodcutting Grit Chance"));
        if (choice == 0)
            body.AppendLine(O($"+{sturdyGripStaminaEffPct:0.#}% Woodcutting Stamina Efficiency"));
    }

    private static void AppendCleavingChopTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        // Numbers mirror PlayerAbilityController.CleavingChop* constants so the tooltip stays truthful.
        const float baseDurationSec = 40f;
        const float prolongedDurationBonusSec = 5f;
        const float baseRange = 10f;
        const float extendedReachRangeBonus = 4f;
        const float secondaryYieldPct = 60f;

        int choice = GetCleavingChopChoice(skillsManager);
        float duration = baseDurationSec + (choice == 1 ? prolongedDurationBonusSec : 0f);
        float range = baseRange + (choice == 0 ? extendedReachRangeBonus : 0f);

        body.AppendLine(O($"For {duration:0.#}s, chops strike nearby trees"));
        body.AppendLine(O($"Cleave range: {range:0.#}"));
        body.AppendLine(O($"Secondary trees gather at {secondaryYieldPct:0.#}% efficiency"));
        body.AppendLine(O("Only logs are gathered from nearby trees"));
    }

    private static void AppendAvatarOfTheForestTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        const float baseDurationSec = 90f;
        const float durationEnhancementBonusSec = 30f;
        const float baseRange = 10f;
        const float extendedReachRangeBonus = 4f;
        const float woodcuttingSpeedBonusPct = 10f;

        int choice = GetAvatarOfTheForestChoice(skillsManager);
        float duration = baseDurationSec + (choice == 0 ? durationEnhancementBonusSec : 0f);
        int cleaveEnh = GetCleavingChopChoice(skillsManager);
        float range = baseRange + (cleaveEnh == 0 ? extendedReachRangeBonus : 0f);

        body.AppendLine(O($"For {duration:0.#}s:"));
        body.AppendLine(O("Woodcutting does not count toward tree depletion"));
        body.AppendLine(O($"Every 5s, woodcutting trees within {range:0.#} units regain 1 depletion"));
        body.AppendLine(O("Bonus find chance is doubled (applied after all other bonuses)"));
        body.AppendLine(O("Woodcutting stamina does not drain on gather swings"));
        body.AppendLine(O($"+{woodcuttingSpeedBonusPct:0.#}% Woodcutting Speed"));
    }

    private static void AppendSpectralAxeTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        // Mirrors the constants in PlayerAbilityController.SpectralAxe* so tooltip stays truthful.
        const float durationSec = 60f;
        const float projectDistance = 5f;
        const float yieldEfficiencyPct = 60f; // SpectralAxeYieldEfficiency * 100
        const float areaRadius = 1.5f;        // SpectralAxeAreaRadius

        // Phantom Harvest opens up bonus / hidden item drops on the same gather tick, so the
        // "logs only" restriction no longer applies for that enhancement choice.
        bool phantomHarvest = GetSpectralAxeChoice(skillsManager) == 0;

        body.AppendLine(O($"Throws your axe {projectDistance:0.#} units forward"));
        body.AppendLine(O($"Chops the closest tree within {areaRadius:0.#} units for {durationSec:0.#}s, then returns"));
        body.AppendLine(O($"Gathers logs at {yieldEfficiencyPct:0.#}% efficiency"));
        if (!phantomHarvest)
            body.AppendLine(O("Only logs are gathered from that tree"));

        // Enhancement-specific lines are emitted via the trailing "Active Enhancement: X (description)"
        // pattern in AbilityEntryUI / FormatActiveEnhancementLine, matching Power Slash etc.
    }

    private static void AppendMinionSpawnTooltipEffectsNoStats(
        StringBuilder body,
        System.Func<string, string> O,
        System.Func<string, string> S,
        AbilityDefinition def,
        SkillsManager skillsManager)
    {
        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            body.AppendLine(O(InheritMinionDamageRuleLine));
            string lingerLine = BuildMinionLingerLine(def, skillsManager);
            if (!string.IsNullOrEmpty(lingerLine))
                body.AppendLine(O(lingerLine));
            body.AppendLine(string.Empty);
            body.AppendLine(S("+0 damage from Minion Damage" + DamageTimingSuffix()));
            body.AppendLine(S(InheritMinionDealsBonusScalingLine));
        }
        else
        {
            body.AppendLine(O("Minion source damage"));
            body.AppendLine(O("+0 damage from Minion Damage" + DamageTimingSuffix()));
        }
    }

    private static void AppendMinionSpawnTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        System.Func<string, string> S,
        AbilityDefinition def,
        CharacterStats stats,
        SkillsManager skillsManager)
    {
        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        const float scalerEps = 0.05f;
        string dmgSuffix = DamageTimingSuffix();

        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            body.AppendLine(O(InheritMinionDamageRuleLine));
            string lingerLine = BuildMinionLingerLine(def, skillsManager);
            if (!string.IsNullOrEmpty(lingerLine))
                body.AppendLine(O(lingerLine));
        }
        else
        {
            SplitDamageRange basePre = cfg.pureMinionDamageSplitRange;
            float dmgMult = 1f + stats.FinalMinionDamagePercent;

            float avgPhys = (basePre.min.physical + basePre.max.physical) * 0.5f;
            float avgMag = (basePre.min.magic + basePre.max.magic) * 0.5f;
            float avgCorr = (basePre.min.corruptionDamage + basePre.max.corruptionDamage) * 0.5f;

            float finalPhys = avgPhys * dmgMult;
            float finalMag = avgMag * dmgMult;
            float finalCorr = avgCorr * dmgMult;

            float physScaler = finalPhys - avgPhys;
            float magScaler = finalMag - avgMag;
            float corrScaler = finalCorr - avgCorr;

            bool anySignificant =
                Mathf.Abs(physScaler) >= scalerEps || Mathf.Abs(magScaler) >= scalerEps ||
                Mathf.Abs(corrScaler) >= scalerEps;

            if (anySignificant)
            {
                if (Mathf.Abs(physScaler) >= scalerEps)
                    body.AppendLine(O(FormatSignedDamageLine(Mathf.RoundToInt(physScaler), "Physical", dmgSuffix)));
                if (Mathf.Abs(magScaler) >= scalerEps)
                    body.AppendLine(O(FormatSignedDamageLine(Mathf.RoundToInt(magScaler), "Magic", dmgSuffix)));
                if (Mathf.Abs(corrScaler) >= scalerEps)
                    body.AppendLine(O(FormatSignedDamageLine(Mathf.RoundToInt(corrScaler), "Corruption", dmgSuffix)));
            }
            else
            {
                body.AppendLine(O("Minion source damage"));
            }
        }

        int mdFlat = ComputeTooltipMinionDamageFlatBonus(def, stats);

        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            body.AppendLine(string.Empty);
            body.AppendLine(S($"+{mdFlat} damage from Minion Damage{dmgSuffix}"));
            body.AppendLine(S(InheritMinionDealsBonusScalingLine));
        }
        else
        {
            body.AppendLine(O($"+{mdFlat} damage from Minion Damage{dmgSuffix}"));
        }

        AppendMinionModifierStatLines(body, S, stats, cfg.damageSourceMode);
    }

    private static string BuildMinionLingerLine(AbilityDefinition def, SkillsManager skillsManager)
    {
        float seconds = def != null && def.minionSpawnDefinition != null
            ? Mathf.Max(0.1f, def.minionSpawnDefinition.summonDuration)
            : 0f;

        if (IsSoulforgedWeapon(def) && skillsManager != null)
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, SoulforgedWeaponChoiceSourceLevel, -1);
            if (selected == SoulforgedWeaponIndefiniteChoiceIndex)
                return string.Empty;
            if (selected == SoulforgedWeaponSwarmChoiceIndex)
                seconds = SoulforgedWeaponSwarmDurationSeconds;
        }

        return $"This minion lingers for {seconds:0.#} seconds";
    }

    /// <summary>Flat damage from owner Minion Damage % on one hit (matches runtime × pre-hit base; inherit uses half scaling).</summary>
    private static int ComputeTooltipMinionDamageFlatBonus(AbilityDefinition def, CharacterStats stats)
    {
        if (!def || def.minionSpawnDefinition == null || !stats)
            return 0;

        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        float scale = cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit
            ? MinionRuntimeStatsCalculator.InheritMinionOwnerBonusScale
            : 1f;
        float effectivePct = Mathf.Max(0f, stats.FinalMinionDamagePercent) * scale;

        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            float coeff = Mathf.Max(0f, cfg.inheritDamageCoefficient);
            SplitDamageRange preBonus = new SplitDamageRange
            {
                min = stats.MinSplitDamage * coeff,
                max = stats.MaxSplitDamage * coeff
            };
            float avgPhys = (preBonus.min.physical + preBonus.max.physical) * 0.5f;
            float avgMag = (preBonus.min.magic + preBonus.max.magic) * 0.5f;
            float avgCorr = (preBonus.min.corruptionDamage + preBonus.max.corruptionDamage) * 0.5f;
            float preTotal = avgPhys + avgMag + avgCorr;
            return Mathf.RoundToInt(preTotal * effectivePct);
        }

        SplitDamageRange basePre = cfg.pureMinionDamageSplitRange;
        float p = (basePre.min.physical + basePre.max.physical) * 0.5f;
        float m = (basePre.min.magic + basePre.max.magic) * 0.5f;
        float c = (basePre.min.corruptionDamage + basePre.max.corruptionDamage) * 0.5f;
        float preTotalPure = p + m + c;
        return Mathf.RoundToInt(preTotalPure * Mathf.Max(0f, stats.FinalMinionDamagePercent));
    }

    private static void AppendMinionModifierStatLines(
        StringBuilder body,
        System.Func<string, string> S,
        CharacterStats stats,
        MinionDamageSourceMode mode)
    {
        const float eps = 0.0001f;
        float scale = mode == MinionDamageSourceMode.InheritOwnerHitSplit
            ? MinionRuntimeStatsCalculator.InheritMinionOwnerBonusScale
            : 1f;

        if (Mathf.Abs(stats.FinalMinionAttackSpeedPercent * scale) > eps)
        {
            body.AppendLine(S(
                $"{FormatSignedPercentPointsForTooltip(stats.FinalMinionAttackSpeedPercentPoints * scale)} attack speed from minion attack speed"));
        }

        if (Mathf.Abs(stats.FinalMinionCritChance * scale) > eps)
        {
            body.AppendLine(S(
                $"{FormatSignedPercentPointsForTooltip(stats.FinalMinionCritChancePercentPoints * scale)} crit chance from minion crit chance"));
        }

        if (Mathf.Abs(stats.FinalMinionMaxLifePercent * scale) > eps)
        {
            body.AppendLine(S(
                $"{FormatSignedPercentPointsForTooltip(stats.FinalMinionMaxLifePercentPoints * scale)} max life from minion max life"));
        }
    }

    private static string FormatSignedPercentPointsForTooltip(float percentPoints)
    {
        if (percentPoints > 0f)
            return $"+{percentPoints:0.#}%";
        if (percentPoints < 0f)
            return $"{percentPoints:0.#}%";
        return "0%";
    }

    private static void AppendDecimalScalerEffectLines(
        StringBuilder body,
        System.Func<string, string> wrapLine,
        float physScaler,
        float magScaler,
        string dmgSuffix)
    {
        if (Mathf.Abs(physScaler) >= 0.05f)
        {
            string line = physScaler > 0f
                ? $"+{physScaler:0.#} Physical{dmgSuffix}"
                : $"{physScaler:0.#} Physical{dmgSuffix}";
            body.AppendLine(wrapLine(line));
        }

        if (Mathf.Abs(magScaler) >= 0.05f)
        {
            string line = magScaler > 0f
                ? $"+{magScaler:0.#} Magic{dmgSuffix}"
                : $"{magScaler:0.#} Magic{dmgSuffix}";
            body.AppendLine(wrapLine(line));
        }
    }

    private static string FormatSignedDamageLine(int amount, string kind, string suffix)
    {
        if (amount > 0)
            return $"+{amount} {kind} Damage{suffix}";
        return $"{amount} {kind} Damage{suffix}";
    }

    private static string DamageTimingSuffix() => " on hit";

    /// <summary>Pre-AP scaled hit total (Phys+Magic+Corr in preview) × AP% / 100, rounded — matches runtime AP multiplier on that base.</summary>
    private static int ComputeTooltipApBonusDamage(
        AbilityDefinition def,
        CharacterStats stats,
        float pEffTip,
        float mEffTip,
        float cEffTip,
        float allM)
    {
        if (!def || !stats)
            return 0;

        float tipAvgPhys =
            (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        float tipAvgMag =
            (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f;
        float tipAvgCorr =
            (Mathf.Max(0f, stats.MinSplitDamage.corruptionDamage) + Mathf.Max(0f, stats.MaxSplitDamage.corruptionDamage)) *
            0.5f;
        float bonusPctNow = Mathf.Max(0f, stats.AbilityPower) * AbilityDefinition.StandardAbilityPowerCoefficient;
        float tipElemBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float linearWeaponScaled;
        if (IsPowerSlash(def))
        {
            linearWeaponScaled = Mathf.Max(0f,
                tipAvgPhys * pEffTip * allM
                + (tipAvgMag * mEffTip + tipElemBonus) * allM
                + tipAvgCorr * cEffTip * allM);
        }
        else
        {
            float tipElemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
            linearWeaponScaled = Mathf.Max(0f,
                tipAvgPhys * pEffTip * allM
                + tipAvgMag * mEffTip * allM * tipElemM
                + tipElemBonus * allM * tipElemM
                + tipAvgCorr * cEffTip * allM);
        }

        return Mathf.RoundToInt(
            linearWeaponScaled * bonusPctNow / CharacterStats.AbilityPowerDamagePercentDivisor);
    }

    /// <summary>Trailing rich-text line for skill-tree enhancement choice (same format as skills UI).</summary>
    public static string FormatActiveEnhancementLine(AbilityDefinition def, int selectedIndex)
    {
        if (def == null || selectedIndex < 0)
            return string.Empty;

        SkillDatabase skillDb = SkillDatabase.LoadDefault();
        SkillDefinition skill = skillDb != null ? skillDb.Get(def.sourceSkill) : null;
        SkillUnlockDefinition unlock = SkillAbilityCommitRules.FindAbilityUnlockOnSkill(skill, def);
        if (unlock == null || unlock.choices == null)
            return string.Empty;

        var nonNullChoices = new List<SkillChoiceDefinition>(unlock.choices.Count);
        for (int i = 0; i < unlock.choices.Count; i++)
        {
            SkillChoiceDefinition c = unlock.choices[i];
            if (c != null)
                nonNullChoices.Add(c);
        }

        if (selectedIndex < 0 || selectedIndex >= nonNullChoices.Count)
            return string.Empty;

        SkillChoiceDefinition selected = nonNullChoices[selectedIndex];
        string title = !string.IsNullOrWhiteSpace(selected.title) ? selected.title.Trim() : $"Enhancement {selectedIndex + 1}";
        string choiceDesc = !string.IsNullOrWhiteSpace(selected.description) ? selected.description.Trim() : string.Empty;
        return string.IsNullOrEmpty(choiceDesc)
            ? $"\nActive Enhancement: <color=#33CC66>{title}</color>"
            : $"\nActive Enhancement: <color=#33CC66>{title} ({choiceDesc})</color>";
    }
}
