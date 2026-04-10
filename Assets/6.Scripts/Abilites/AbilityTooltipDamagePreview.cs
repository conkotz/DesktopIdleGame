using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Ability tooltip damage preview: average weapon split × multipliers and AP × mult, before crit.
/// Crit still applies when the hit resolves; values here match additive scaling from stats, not expected DPS.
/// </summary>
public static class AbilityTooltipDamagePreview
{
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

    /// <summary>AP contribution is shown as phys — matches runtime (added to physical on most abilities).</summary>
    public static string FormatAbilityPowerSuffix(CharacterStats stats, float abilityPowerMultiplier)
    {
        if (stats == null)
            return "";

        int n = Mathf.RoundToInt(Mathf.Max(0f, stats.AbilityPower) * abilityPowerMultiplier);
        if (n == 0)
            return "";

        return $" ({n} phys)";
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

    private static int GetMeleeSkillRow15Choice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 15, -1);
    }

    /// <summary>
    /// Compact tooltip: Effects (damage or non-damage), Scaling (% + raw contribution), Energy · CD.
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

        float physMult = def.physicalDamageMultiplier;
        float cooldown = Mathf.Max(0f, def.cooldown);
        AbilityTooltipAdjustments.ApplySkillTreeChoices(def, skillsManager, ref physMult, ref cooldown);

        float magMult = def.magicalDamageMultiplier;
        float apMult = def.abilityPowerMultiplier;

        var body = new StringBuilder();
        body.AppendLine(O("Effects:"));

        if (IsRend(def))
        {
            body.AppendLine(O("100% Bleed on next hit (+3s duration)"));
        }
        else if (IsVenomJab(def))
        {
            body.AppendLine(O("100% Poison on next hit (when True Damage is dealt)"));
        }
        else if (IsCleavingStrikes(def))
        {
            int cleaveSel = GetMeleeSkillRow15Choice(skillsManager);
            if (cleaveSel == 0)
                body.AppendLine(O("+3 nearby enemies per strike (8s, 4 hits); reduced cleave damage."));
            else if (cleaveSel == 1)
                body.AppendLine(O("+1 nearby enemy per strike (14s, 7 hits); reduced cleave damage."));
            else
                body.AppendLine(O("+1 nearby enemy per strike (10s, 5 hits); reduced cleave damage."));
        }
        else if (IsCrescentSlash(def))
        {
            int crescentSel = GetMeleeSkillRow15Choice(skillsManager);

            float avgPhys = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f
                : 0f;
            float avgMag = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.magical) + Mathf.Max(0f, stats.MaxSplitDamage.magical)) * 0.5f
                : 0f;
            float ap = stats ? Mathf.Max(0f, stats.AbilityPower) : 0f;

            float scaledPhysical = avgPhys * physMult;
            float scaledMagical = avgMag * magMult;
            float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
            float apBonus = ap * apMult;
            float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
            float physPart = scaledPhysical + apBonus;
            float magPart = scaledMagical + elementBonus + ailmentBonus;

            if (crescentSel == 0)
            {
                float ph = Mathf.Max(0f, physPart);
                float move = ph * 0.5f;
                physPart = ph - move;
                magPart += move;
            }

            int p = Mathf.RoundToInt(physPart);
            int m = Mathf.RoundToInt(magPart);

            string dmgSuffix = DamageTimingSuffix();
            if (p != 0)
                body.AppendLine(O(FormatSignedDamageLine(p, "Physical", dmgSuffix)));
            if (m != 0)
                body.AppendLine(O(FormatSignedDamageLine(m, "Magical", dmgSuffix)));
            if (p == 0 && m == 0)
                body.AppendLine(O("(No direct damage — see description)"));

            if (crescentSel == 1)
                body.AppendLine(O("Hits all enemies."));
            else
                body.AppendLine(O("Hits 3 enemies."));
        }
        else
        {
            float avgPhys = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f
                : 0f;
            float avgMag = stats
                ? (Mathf.Max(0f, stats.MinSplitDamage.magical) + Mathf.Max(0f, stats.MaxSplitDamage.magical)) * 0.5f
                : 0f;
            float ap = stats ? Mathf.Max(0f, stats.AbilityPower) : 0f;

            float physPart;
            float magPart;

            if (IsPowerSlash(def))
            {
                float bonusPhys = avgPhys * physMult;
                float bonusMag = avgMag * magMult;
                float apBonus = ap * apMult;
                float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
                float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
                physPart = bonusPhys + apBonus + ailmentBonus;
                magPart = bonusMag + elementBonus;
            }
            else
            {
                float scaledPhysical = avgPhys * physMult;
                float scaledMagical = avgMag * magMult;
                float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
                float apBonus = ap * apMult;
                float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
                physPart = scaledPhysical + apBonus;
                magPart = scaledMagical + elementBonus + ailmentBonus;
            }

            int p = Mathf.RoundToInt(physPart);
            int m = Mathf.RoundToInt(magPart);

            string dmgSuffix = DamageTimingSuffix();
            if (p != 0)
                body.AppendLine(O(FormatSignedDamageLine(p, "Physical", dmgSuffix)));
            if (m != 0)
                body.AppendLine(O(FormatSignedDamageLine(m, "Magical", dmgSuffix)));
            if (p == 0 && m == 0)
                body.AppendLine(O("(No direct damage — see description)"));
        }

        const float epsilon = 0.0001f;
        // 0 = omit; positive/negative coefficients show as ±%.
        bool showPhysScaling = Mathf.Abs(physMult) > epsilon;
        bool showMagScaling = Mathf.Abs(magMult) > epsilon;
        bool hasApScaling = Mathf.Abs(apMult) > epsilon;

        float tipAvgPhys = stats
            ? (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f
            : 0f;
        float tipAvgMag = stats
            ? (Mathf.Max(0f, stats.MinSplitDamage.magical) + Mathf.Max(0f, stats.MaxSplitDamage.magical)) * 0.5f
            : 0f;
        float tipAp = stats ? Mathf.Max(0f, stats.AbilityPower) : 0f;
        int physScalingContrib = Mathf.RoundToInt(tipAvgPhys * physMult);
        int magScalingContrib = Mathf.RoundToInt(tipAvgMag * magMult);
        int apScalingContrib = Mathf.RoundToInt(tipAp * apMult);

        if (showPhysScaling || showMagScaling || hasApScaling)
        {
            body.AppendLine(O("Scaling:"));
            if (showPhysScaling)
                body.AppendLine(O($"{physMult * 100f:0.#}% Physical Damage ({physScalingContrib})"));
            if (showMagScaling)
                body.AppendLine(O($"{magMult * 100f:0.#}% Magic Damage ({magScalingContrib})"));
            if (hasApScaling)
                body.AppendLine(O($"{apMult * 100f:0.#}% Ability Power ({apScalingContrib})"));
        }

        body.AppendLine(string.Empty);
        body.AppendLine(O($"{def.energyCost:0.#} Energy · {cooldown:0.#}s cooldown"));

        return body.ToString().TrimEnd();
    }

    private static string FormatSignedDamageLine(int amount, string kind, string suffix)
    {
        if (amount > 0)
            return $"+{amount} {kind} Damage{suffix}";
        return $"{amount} {kind} Damage{suffix}";
    }

    private static string DamageTimingSuffix() => " on hit";

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
