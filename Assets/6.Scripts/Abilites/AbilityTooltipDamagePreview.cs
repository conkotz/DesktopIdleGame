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
    /// TMP rich-text line for ability tooltips. Empty when <see cref="AbilityWeaponRequirement.Any"/>.
    /// Red when equipped weapon does not match; neutral or orange (action bar) when it does.
    /// </summary>
    public static string BuildWeaponRequirementRichLine(AbilityDefinition def, CharacterStats stats, bool orangeWhenOk = false)
    {
        if (def == null || def.requiredWeaponType == AbilityWeaponRequirement.Any)
            return "";

        string label = def.requiredWeaponType switch
        {
            AbilityWeaponRequirement.Melee => "Melee",
            AbilityWeaponRequirement.Ranged => "Ranged",
            AbilityWeaponRequirement.Magic => "Magic",
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

    private static bool IsVenomJab(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.VenomJabAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsCleavingStrikes(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.CleavingStrikesAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsCrescentSlash(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.CrescentSlashAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsWhirlwind(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.WhirlwindAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static int GetMeleeSkillRow15Choice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 15, -1);
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

        float tipAp = stats ? Mathf.Max(0f, stats.AbilityPower) : 0f;
        bool showApInEffects =
            !IsCleavingStrikes(def) && !IsRend(def) && !IsVenomJab(def);
        int tooltipApBonus = showApInEffects && stats != null
            ? ComputeTooltipApBonusDamage(def, stats, pEffTip, mEffTip, cEffTip, allM)
            : 0;

        var body = new StringBuilder();
        body.AppendLine(O("Effects:"));

        if (IsRend(def))
        {
            body.AppendLine(O("100% bleed on next hit if physical damage is dealt"));
            body.AppendLine(O("+3s duration"));
            if (GetMeleeSkillRow5Choice(skillsManager) == 1)
                body.AppendLine(O("Crimson Spread"));
        }
        else if (IsVenomJab(def))
        {
            body.AppendLine(O("100% poison on next hit if corruption damage is dealt."));
            body.AppendLine(O("Apply max poison stacks"));
            if (GetMeleeSkillRow5Choice(skillsManager) == 1)
                body.AppendLine(O("Contagion Burst"));
        }
        else if (IsCleavingStrikes(def))
        {
            int cleaveSel = GetMeleeSkillRow15Choice(skillsManager);
            if (cleaveSel == 0)
                body.AppendLine(O("+2 nearby enemies per strike (5s, 3 hits); 40% reduced damage on cleaved hits."));
            else if (cleaveSel == 1)
                body.AppendLine(O("+1 nearby enemy per strike (10s, 6 hits); 40% reduced damage on cleaved hits."));
            else
                body.AppendLine(O("+1 nearby enemy per strike (7s, 4 hits); 40% reduced damage on cleaved hits."));
        }
        else if (IsCrescentSlash(def))
        {
            int crescentSel = GetMeleeSkillRow15Choice(skillsManager);

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

            int wwEnhance = GetMeleeSkillRow15Choice(skillsManager);
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
                float magEff = def.GetMagicHitScalingMultiplier();
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
            if (showAllScaling)
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
        body.AppendLine(O($"{def.energyCost:0.#} Energy • {cooldown:0.#}s Cooldown"));

        return body.ToString().TrimEnd();
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
