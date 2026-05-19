using System;
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
    private const int SoulforgedWeaponSwarmCount = 3;

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

        string fromPresentation = SkillsAbilityPresentationResolver.ResolveAbilityTooltipCategoryLabelOrNull(def);
        if (!string.IsNullOrEmpty(fromPresentation))
            return fromPresentation;

        switch (def.tag)
        {
            case AbilityTag.Active:
                return "Active";
            case AbilityTag.Minion:
                return "Minion";
            case AbilityTag.Buff:
                return "Buff";
            case AbilityTag.ToggleBuff:
                return "Toggle Buff";
        }

        // Legacy fallback for assets that haven't been tagged in the inspector yet.
        if (def.SpawnsMinionOnCast)
            return "Minion";
        return null;
    }

    public static CharacterStats FindLocalPlayerStats()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            var s = player.GetComponent<CharacterStats>();
            if (s != null)
                return s;
        }

        return UnityEngine.Object.FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);
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
    /// Red when the requirement is not met; green when met and <paramref name="accentWhenOk"/> is true; otherwise plain text.
    /// </summary>
    public static string BuildWeaponRequirementRichLine(AbilityDefinition def, CharacterStats stats, bool accentWhenOk = false)
    {
        if (def == null)
            return "";

        const string okColor = "#55DD55";

        if (IsSpectralAxe(def))
        {
            var pac = UnityEngine.Object.FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);
            bool axeOk = stats == null || (pac != null && pac.HasAxeInToolbelt());
            const string axeRequirementText = "Required: Axe in toolbelt";
            if (!axeOk)
                return $"<color=#FF5C5C>{axeRequirementText}</color>";
            if (accentWhenOk)
                return $"<color={okColor}>{axeRequirementText}</color>";
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

        if (accentWhenOk)
            return $"<color={okColor}>{line}</color>";

        return line;
    }

    private static bool IsPowerSlash(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.PowerSlashAbilityId, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Combat abilities that show full hit totals in Effects (weapon × mult + AP + bonuses),
    /// not separate "+ bonus" damage lines over a basic attack.
    /// </summary>
    private static bool UsesCombinedTotalHitDamageTooltip(AbilityDefinition def)
    {
        if (!def)
            return false;
        if (def.SpawnsMinionOnCast && def.minionSpawnDefinition)
            return false;
        if (IsRend(def) || IsEnvenom(def) || IsCleavingStrikes(def))
            return false;
        if (IsLumberFrenzy(def) || IsFishingFrenzy(def) || IsAvatarOfTheForest(def))
            return false;
        if (IsCleavingChop(def) || IsSpectralAxe(def))
            return false;

        const float scalingEpsilon = 0.0001f;
        return def.weaponDamageMultiplier > scalingEpsilon;
    }

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

    private static bool IsFinalSeverance(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.FinalSeveranceAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutionersDescent(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.ExecutionersDescentAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsShadowStrike(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.ShadowStrikeAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsEnergyInfusion(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.EnergyInfusionAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsSoulforgedWeapon(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.SoulforgedWeaponAbilityId, System.StringComparison.OrdinalIgnoreCase);

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

    private static int GetFishingSkillRow5Choice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Fishing, "Lv5_0", -1);
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
        return skillsManager.GetSkillChoiceSelection(
            SkillType.Woodcutting, AbilityCombatPower.AvatarOfTheForestEnhancementParentSpineNodeId, -1);
    }

    private const float SpectralAxeTooltipDurationSeconds = 60f;

    private static float GetCleavingStrikesTooltipDurationSeconds(SkillsManager skillsManager)
    {
        int c = GetMeleeLv15BranchChoice(skillsManager, 1);
        if (c == 0)
            return 5f;
        if (c == 1)
            return 10f;
        return 7f;
    }

    private static void AppendSoulforgedWeaponDurationLine(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        SkillsManager skillsManager)
    {
        body.AppendLine(string.Empty);
        if (skillsManager == null)
        {
            float fallback = def != null && def.minionSpawnDefinition != null
                ? Mathf.Max(0.1f, def.minionSpawnDefinition.summonDuration)
                : SoulforgedWeaponSwarmDurationSeconds;
            float dur = GetTooltipBuffMinionDisplayDurationSeconds(def, fallback, 0f);
            body.AppendLine(O($"Duration: {dur:0.#}s"));
            return;
        }

        int sel = skillsManager.GetSkillChoiceSelection(SkillType.Melee, SoulforgedWeaponChoiceSourceLevel, -1);
        if (sel == SoulforgedWeaponSwarmChoiceIndex)
        {
            float dur = GetTooltipBuffMinionDisplayDurationSeconds(def, SoulforgedWeaponSwarmDurationSeconds, 0f);
            body.AppendLine(O($"Duration: {dur:0.#}s"));
        }
        else if (sel == SoulforgedWeaponIndefiniteChoiceIndex)
            body.AppendLine(O("Duration: Until dismissed"));
        else
        {
            float fallback = def != null && def.minionSpawnDefinition != null
                ? Mathf.Max(0.1f, def.minionSpawnDefinition.summonDuration)
                : SoulforgedWeaponSwarmDurationSeconds;
            float dur = GetTooltipBuffMinionDisplayDurationSeconds(def, fallback, 0f);
            body.AppendLine(O($"Duration: {dur:0.#}s"));
        }
    }

    private static int GetMeleeLv15BranchChoice(SkillsManager skillsManager, int slot012)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, $"Lv15_{Mathf.Clamp(slot012, 0, 7)}", -1);
    }

    private static int GetMeleeLv45BranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        int selected = skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.FinalSeveranceEnhancementParentSpineNodeId, -1);
        if (selected >= 0)
            return selected;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 45, -1);
    }

    private static int GetExecutionersDescentBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.ExecutionersDescentEnhancementParentSpineNodeId, -1);
    }

    private static int GetShadowStrikeBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.ShadowStrikeEnhancementParentSpineNodeId, -1);
    }

    private static int GetEnergyInfusionBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.EnergyInfusionEnhancementParentSpineNodeId, -1);
    }

    private static int GetMeleeSkillRow5Choice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;
        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
    }

    /// <summary>
    /// Inspector base duration for buff/minion tooltips (and Lumber runtime). When ~0, uses <paramref name="codeBaseSeconds"/>.
    /// </summary>
    private static float GetTooltipBuffMinionBaseDurationSeconds(AbilityDefinition def, float codeBaseSeconds)
    {
        if (def != null && def.tooltipBuffMinionDurationSeconds > 0.01f)
            return def.tooltipBuffMinionDurationSeconds;
        return codeBaseSeconds;
    }

    /// <summary>Displayed duration = max(0.1, base + additiveBonus) where base is asset or code default.</summary>
    private static float GetTooltipBuffMinionDisplayDurationSeconds(
        AbilityDefinition def,
        float codeBaseSeconds,
        float additiveBonusSeconds)
    {
        return Mathf.Max(0.1f, GetTooltipBuffMinionBaseDurationSeconds(def, codeBaseSeconds) + additiveBonusSeconds);
    }

    private static float GetAvatarOfTheForestDurationBonusSeconds(SkillsManager skillsManager)
    {
        const float durationEnhancementBonusSec = 30f;
        return GetAvatarOfTheForestChoice(skillsManager) == 0 ? durationEnhancementBonusSec : 0f;
    }

    private static float GetCleavingChopDurationBonusSeconds(SkillsManager skillsManager)
    {
        const float prolongedDurationBonusSec = 5f;
        return GetCleavingChopChoice(skillsManager) == 1 ? prolongedDurationBonusSec : 0f;
    }

    private static float GetCleavingStrikesDurationBonusSeconds(SkillsManager skillsManager)
    {
        const float strikesCodeBase = 5f;
        float full = GetCleavingStrikesTooltipDurationSeconds(skillsManager);
        return Mathf.Max(0f, full - strikesCodeBase);
    }

    private static bool IsLumberFrenzy(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.LumberFrenzyAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsFishingFrenzy(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.FishingFrenzyAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private const float LumberFrenzyBuffDurationSecondsTooltip = 20f;
    private static float FishingFrenzyBuffDurationSecondsTooltip => GatheringPassiveTooltipText.FishingFrenzyDurationSeconds;

    /// <summary>
    /// Blue "Deals X% of your hit damage" (and minion scaling) lines — placed after flavor description, before Effects.
    /// </summary>
    public static string BuildAbilityTooltipScalingSection(
        AbilityDefinition def,
        CharacterStats stats,
        SkillsManager skillsManager,
        bool orangeMarkup)
    {
        if (!def)
            return string.Empty;

        string S(string line) =>
            orangeMarkup
                ? $"<color={TooltipScalingAccentColorOrangeMode}>{line}</color>"
                : $"<color={TooltipScalingAccentColorPlain}>{line}</color>";

        float weaponMult = def.weaponDamageMultiplier;
        float cooldown = Mathf.Max(0f, def.cooldown);
        AbilityTooltipAdjustments.ApplySkillTreeChoices(def, skillsManager, ref weaponMult, ref cooldown);

        var scaling = new StringBuilder();

        if (def.SpawnsMinionOnCast && def.minionSpawnDefinition)
        {
            if (stats != null)
                AppendMinionSpawnTooltipScalingLines(scaling, S, def, stats);
            else
                AppendMinionSpawnTooltipScalingLinesNoStats(scaling, S, def);
            return scaling.ToString().TrimEnd();
        }

        const float scalingEpsilon = 0.0001f;
        if (IsCleavingStrikes(def) || weaponMult <= scalingEpsilon)
            return string.Empty;

        scaling.AppendLine(S($"Deals {weaponMult * 100f:0.#}% of your weapon damage"));
        return scaling.ToString().TrimEnd();
    }

    /// <summary>
    /// League ability list tooltip order: tag, required weapon, flavor, scaling, effects, active enhancement.
    /// </summary>
    public static string AssembleLeagueStyleAbilityTooltipBody(
        string tagLine,
        string weaponRequirementLine,
        string flavorDescription,
        string scalingSection,
        string statsSection,
        string activeEnhancementLine)
    {
        var parts = new List<string>(6);
        if (!string.IsNullOrWhiteSpace(tagLine))
            parts.Add(tagLine.Trim());
        if (!string.IsNullOrWhiteSpace(weaponRequirementLine))
            parts.Add(weaponRequirementLine.Trim());
        if (!string.IsNullOrWhiteSpace(flavorDescription))
            parts.Add(flavorDescription.Trim());
        if (!string.IsNullOrWhiteSpace(scalingSection))
            parts.Add(scalingSection.Trim());
        if (!string.IsNullOrWhiteSpace(statsSection))
            parts.Add(statsSection.Trim());
        if (!string.IsNullOrWhiteSpace(activeEnhancementLine))
            parts.Add(activeEnhancementLine.TrimStart('\n', '\r').Trim());

        return parts.Count == 0 ? string.Empty : string.Join("\n\n", parts);
    }

    /// <summary>
    /// Compact tooltip: Effects header, effect bullets, Duration when applicable, Energy • Cooldown.
    /// Blue scaling belongs in <see cref="BuildAbilityTooltipScalingSection"/> (before Effects in full tooltips).
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

        float weaponMult = def.weaponDamageMultiplier;
        float cooldown = Mathf.Max(0f, def.cooldown);
        AbilityTooltipAdjustments.ApplySkillTreeChoices(def, skillsManager, ref weaponMult, ref cooldown);

        float allM = def.GetEffectiveAllDamageMultiplier();
        float energy = Mathf.Max(0f, def.energyCost);

        var body = new StringBuilder();
        body.AppendLine(O("Effects:"));

        if (IsLumberFrenzy(def))
        {
            AppendLumberFrenzyTooltipEffects(body, O, skillsManager);
            body.AppendLine(string.Empty);
            float lumberDur = GetTooltipBuffMinionDisplayDurationSeconds(def, LumberFrenzyBuffDurationSecondsTooltip, 0f);
            body.AppendLine(O($"Duration: {lumberDur:0.#}s"));
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (IsFishingFrenzy(def))
        {
            AppendFishingFrenzyTooltipEffects(body, O, skillsManager);
            body.AppendLine(string.Empty);
            float fishingDur = GetTooltipBuffMinionDisplayDurationSeconds(def, FishingFrenzyBuffDurationSecondsTooltip, 0f);
            body.AppendLine(O($"Duration: {fishingDur:0.#}s"));
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (IsEnergyInfusion(def))
        {
            int enhance = GetEnergyInfusionBranchChoice(skillsManager);
            body.AppendLine(O(
                $"While active, drains Mana to restore Energy at {AbilityCombatPower.EnergyInfusionBaseManaDrainPerSecond:0.#} per second (1:1)."));
            body.AppendLine(O("Stays on until toggled off or Mana reaches 0."));
            if (enhance == 0)
                body.AppendLine(O(
                    $"Efficient Conversion: Mana drain reduced by {(1f - AbilityCombatPower.EnergyInfusionEfficientConversionManaMultiplier) * 100f:0.#}% (full Energy gain)."));
            else if (enhance == 1)
                body.AppendLine(O(
                    $"Overcharged: +{(AbilityCombatPower.EnergyInfusionOverchargedAbilityPowerMultiplier - 1f) * 100f:0.#}% ability power while active."));
            body.AppendLine(string.Empty);
            body.AppendLine(O("Duration: Toggle"));
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (IsAvatarOfTheForest(def))
        {
            AppendAvatarOfTheForestTooltipEffects(body, O, skillsManager);
            const float avatarCodeBaseSeconds = 90f;
            float avatarDur = GetTooltipBuffMinionDisplayDurationSeconds(
                def, avatarCodeBaseSeconds, GetAvatarOfTheForestDurationBonusSeconds(skillsManager));
            body.AppendLine(string.Empty);
            body.AppendLine(O($"Duration: {avatarDur:0.#}s"));
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (IsCleavingChop(def))
        {
            AppendCleavingChopTooltipEffects(body, O, skillsManager, def);
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"Duration: {GetTooltipBuffMinionDisplayDurationSeconds(def, 40f, GetCleavingChopDurationBonusSeconds(skillsManager)):0.#}s"));
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (IsSpectralAxe(def))
        {
            AppendSpectralAxeTooltipEffects(body, O, skillsManager, def);
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"Duration: {GetTooltipBuffMinionDisplayDurationSeconds(def, SpectralAxeTooltipDurationSeconds, 0f):0.#}s"));
            body.AppendLine(string.Empty);
            body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));
            return body.ToString().TrimEnd();
        }

        if (def.SpawnsMinionOnCast && def.minionSpawnDefinition)
        {
            if (stats != null)
                AppendMinionSpawnTooltipEffectLines(body, O, def, stats, skillsManager);
            else
                AppendMinionSpawnTooltipEffectLinesNoStats(body, O, def);

            if (IsSoulforgedWeapon(def))
            {
                AppendSoulforgedEnhancementEffectLines(body, O, def, skillsManager);
                AppendSoulforgedWeaponDurationLine(body, O, def, skillsManager);
            }

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
            string dmgSuffix = DamageTimingSuffix();
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit);

            if (crescentSel == 0)
            {
                float ph = Mathf.Max(0f, physHit);
                float move = ph * 0.5f;
                physHit = ph - move;
                magHit += move;
            }

            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix);

            if (crescentSel == 1)
                body.AppendLine(O("Hits all enemies."));
            else
                body.AppendLine(O("Hits 3 enemies."));
        }
        else if (IsFinalSeverance(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            int fsEnhance = GetMeleeLv45BranchChoice(skillsManager);
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit);

            if (fsEnhance == 1)
            {
                float frac = AbilityCombatPower.FinalSeveranceThousandCutsHitFraction;
                int hits = AbilityCombatPower.FinalSeveranceThousandCutsHitCount;
                AppendPerHitDamageEffectLines(body, O, physHit * frac, magHit * frac, corrHit * frac, hits, dmgSuffix);
            }
            else
                AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix);

            body.AppendLine(O($"Channel: {AbilityCombatPower.FinalSeveranceChannelSeconds:0.#}s"));
            body.AppendLine(O(
                $"Wide arc — up to {AbilityCombatPower.FinalSeveranceMaxTargets} enemies hit."));

            if (fsEnhance == 0)
            {
                float bonusMult = AbilityCombatPower.FinalSeveranceWorldbreakerBonusMultiplier - 1f;
                int bonus = Mathf.RoundToInt((physHit + magHit + corrHit) * bonusMult);
                body.AppendLine(O($"50% extra damage to low health enemies (+{bonus})"));
            }
        }
        else if (IsShadowStrike(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            int enhance = GetShadowStrikeBranchChoice(skillsManager);
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix);

            body.AppendLine(O(
                $"Teleports to the closest enemy up to {AbilityCombatPower.ShadowStrikeForwardReach:0.#} units ahead in your facing arc."));
            body.AppendLine(O("Does not consume your auto-attack swing timer."));

            if (enhance == 0)
                body.AppendLine(O(
                    $"Marks the target — the next critical hit deals +{AbilityCombatPower.ShadowStrikeLethalCritBonusFraction * 100f:0.#}% critical damage, then the mark expires."));
            else if (enhance == 1)
                body.AppendLine(O(
                    $"Marks the target for {AbilityCombatPower.ShadowStrikeExecutionMarkSeconds:0.#}s — if they die while marked, cooldown is reduced by {AbilityCombatPower.ShadowStrikeExecutionCooldownRefundSeconds:0.#}s."));
        }
        else if (IsExecutionersDescent(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            int enhance = GetExecutionersDescentBranchChoice(skillsManager);
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix);

            ComputeAverageAbilityHitSplit(
                def,
                stats,
                AbilityCombatPower.ExecutionersDescentShockwaveWeaponMultiplier,
                allM,
                out float shockPhys,
                out float shockMag,
                out float shockCorr);
            int shockTotal = Mathf.RoundToInt(shockPhys + shockMag + shockCorr);
            body.AppendLine(O(
                $"Shockwave: {shockTotal} damage to enemies within {AbilityCombatPower.ExecutionersDescentShockwaveRadius:0.#} units of the target"));
            body.AppendLine(O(
                $"Descent: {AbilityCombatPower.ExecutionersDescentDescentSeconds:0.#}s — locks onto a target, then impacts at their position"));

            if (enhance == 0)
                body.AppendLine(O("If the target dies during descent or from the impact, cooldown is reduced by 50%."));
            else if (enhance == 1)
            {
                body.AppendLine(O("Main hit ignores armour and magic resist."));
                body.AppendLine(O(
                    $"Shockwave victims lose 50% armour and magic resist for {AbilityCombatPower.ExecutionersDescentSunderingDebuffSeconds:0.#}s."));
            }
        }
        else if (IsWhirlwind(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix);

            int wwEnhance = GetMeleeLv15BranchChoice(skillsManager, 0);
            if (wwEnhance == 0)
            {
                body.AppendLine(O(
                    $"Hits each enemy a second time for {AbilityCombatPower.WhirlwindTwinCycloneSecondHitFraction * 100f:0.#}% of the first wave."));
            }
        }
        else if (UsesCombinedTotalHitDamageTooltip(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix);
        }

        if (IsCleavingStrikes(def))
        {
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"Duration: {GetTooltipBuffMinionDisplayDurationSeconds(def, 5f, GetCleavingStrikesDurationBonusSeconds(skillsManager)):0.#}s"));
        }

        body.AppendLine(string.Empty);
        body.AppendLine(O($"{energy:0.#} Energy • {cooldown:0.#}s Cooldown"));

        return body.ToString().TrimEnd();
    }

    /// <summary>Presentation shortDescription / league intro (flavor line above numeric effects).</summary>
    public static string ResolveAbilityShortDescription(AbilityDefinition def)
    {
        if (def == null)
            return string.Empty;

        string intro = SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def);
        if (string.IsNullOrWhiteSpace(intro) || intro == "No description.")
            return string.Empty;

        return intro.Trim();
    }

    /// <summary>Short description, then a blank line, then effect lines (action bar / HUD).</summary>
    public static string CombineShortDescriptionWithBody(AbilityDefinition def, string effectsBody)
    {
        return CombineShortDescriptionScalingAndEffects(def, string.Empty, effectsBody);
    }

    /// <summary>Flavor intro, optional blue scaling, then effect lines (action bar / HUD).</summary>
    public static string CombineShortDescriptionScalingAndEffects(
        AbilityDefinition def,
        string scalingSection,
        string effectsBody)
    {
        string intro = ResolveAbilityShortDescription(def);
        string scaling = scalingSection?.Trim() ?? string.Empty;
        string effects = effectsBody?.Trim() ?? string.Empty;

        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(intro))
            parts.Add(intro);
        if (!string.IsNullOrEmpty(scaling))
            parts.Add(scaling);
        if (!string.IsNullOrEmpty(effects))
            parts.Add(effects);

        return parts.Count == 0 ? string.Empty : string.Join("\n\n", parts);
    }

    /// <summary>
    /// HUD buff strip + action bar: short description + numeric effects from
    /// <see cref="BuildAbilityTooltipStatsSection"/>.
    /// Major-passive HUD ids (Flow State, Calm Waters) use <see cref="GatheringPassiveTooltipText"/>.
    /// </summary>
    public static bool TryBuildHudBuffTooltip(
        string buffId,
        int displayStacks,
        SkillsManager skillsManager,
        AbilityDatabase abilityDatabase,
        out string title,
        out string body)
    {
        title = null;
        body = null;
        if (string.IsNullOrWhiteSpace(buffId))
            return false;

        if (GatheringPassiveTooltipText.TryGetHudBuffTooltip(buffId, displayStacks, skillsManager, out title, out body))
            return true;

        if (abilityDatabase == null)
            return false;

        AbilityDefinition def = abilityDatabase.Get(buffId);
        if (def == null)
            return false;

        CharacterStats stats = FindLocalPlayerStats();
        string scaling = BuildAbilityTooltipScalingSection(def, stats, skillsManager, orangeMarkup: false);
        string effects = BuildCompactEffectsBody(def, skillsManager, includeDuration: false, displayStacks);
        body = CombineShortDescriptionScalingAndEffects(def, scaling, effects);
        if (string.IsNullOrWhiteSpace(body))
            return false;

        title = SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def);
        if (string.IsNullOrWhiteSpace(title))
            title = buffId;
        return true;
    }

    /// <summary>Action-bar hover: short description + effects block when available.</summary>
    public static bool TryBuildActionBarCompactBody(AbilityDefinition def, SkillsManager skillsManager, out string body)
    {
        body = null;
        if (def == null)
            return false;

        bool includeDuration = ShouldShowDurationInCompactUi(def);
        CharacterStats stats = FindLocalPlayerStats();
        string scaling = BuildAbilityTooltipScalingSection(def, stats, skillsManager, orangeMarkup: false);
        string effects = BuildCompactEffectsBody(def, skillsManager, includeDuration, displayStacks: 0);
        body = CombineShortDescriptionScalingAndEffects(def, scaling, effects);
        return !string.IsNullOrWhiteSpace(body);
    }

    private static bool ShouldShowDurationInCompactUi(AbilityDefinition def)
    {
        if (!def)
            return false;
        if (def.tag == AbilityTag.Buff)
            return true;
        if (def.tag == AbilityTag.ToggleBuff)
            return false;
        return IsLumberFrenzy(def) || IsFishingFrenzy(def) || IsAvatarOfTheForest(def) ||
               IsCleavingChop(def) || IsSpectralAxe(def) || IsCleavingStrikes(def) ||
               IsSoulforgedWeapon(def);
    }

    private static string BuildCompactEffectsBody(
        AbilityDefinition def,
        SkillsManager skillsManager,
        bool includeDuration,
        int displayStacks)
    {
        if (def == null)
            return string.Empty;

        CharacterStats stats = FindLocalPlayerStats();
        string full = BuildAbilityTooltipStatsSection(def, stats, skillsManager, orangeMarkup: false);
        if (string.IsNullOrWhiteSpace(full))
            return string.Empty;

        string extracted = ExtractEffectLinesFromStatsSection(full, includeDuration);
        if (string.IsNullOrWhiteSpace(extracted))
            return string.Empty;

        if (displayStacks > 1 &&
            string.Equals(def.abilityId, AbilityCombatPower.CleavingStrikesAbilityId, StringComparison.OrdinalIgnoreCase))
            return extracted + $"\n\nSwing charges: {displayStacks}";

        return extracted;
    }

    /// <summary>Strips the Effects header and Energy/Cooldown footer from a stats-section string.</summary>
    private static string ExtractEffectLinesFromStatsSection(string statsSection, bool includeDuration)
    {
        if (string.IsNullOrWhiteSpace(statsSection))
            return string.Empty;

        var lines = new List<string>();
        foreach (string raw in statsSection.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (string.Equals(StripRichText(line), "Effects:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.Contains("Energy •", StringComparison.Ordinal))
                break;

            if (!includeDuration && StripRichText(line).StartsWith("Duration:", StringComparison.OrdinalIgnoreCase))
                continue;

            lines.Add(line);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
            lines.RemoveAt(lines.Count - 1);

        return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
    }

    private static string StripRichText(string line)
    {
        if (string.IsNullOrEmpty(line))
            return line;
        return line
            .Replace("<color=#FFB347>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<color=#B0C8DD>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<color=#9DD4FF>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</color>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static void AppendLumberFrenzyTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        var scratch = new StringBuilder();
        GatheringPassiveTooltipText.AppendLumberFrenzyEffectLines(scratch, skillsManager);
        foreach (string line in scratch.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            body.AppendLine(O(line));
    }

    private static void AppendFishingFrenzyTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        var scratch = new StringBuilder();
        GatheringPassiveTooltipText.AppendFishingFrenzyEffectLines(scratch, skillsManager);
        foreach (string line in scratch.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            body.AppendLine(O(line));
    }

    private static void AppendCleavingChopTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager,
        AbilityDefinition def)
    {
        // Numbers mirror PlayerAbilityController.CleavingChop* constants so the tooltip stays truthful.
        const float baseRange = 10f;
        const float extendedReachRangeBonus = 4f;
        const float secondaryYieldPct = 60f;

        int choice = GetCleavingChopChoice(skillsManager);
        float duration = GetTooltipBuffMinionDisplayDurationSeconds(def, 40f, GetCleavingChopDurationBonusSeconds(skillsManager));
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
        const float baseRange = 10f;
        const float extendedReachRangeBonus = 4f;
        float woodcuttingSpeedBonusPct = AbilityCombatPower.AvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd * 100f;

        int cleaveEnh = GetCleavingChopChoice(skillsManager);
        float range = baseRange + (cleaveEnh == 0 ? extendedReachRangeBonus : 0f);

        body.AppendLine(O("Woodcutting does not count toward tree depletion"));
        body.AppendLine(O($"Every 5s, woodcutting trees within {range:0.#} units regain 1 depletion"));
        body.AppendLine(O("Bonus find chance is doubled (applied after all other bonuses)"));
        body.AppendLine(O("Woodcutting stamina does not drain on gather swings"));
        body.AppendLine(O($"+{woodcuttingSpeedBonusPct:0.#}% Woodcutting Speed"));
    }

    private static void AppendSpectralAxeTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager,
        AbilityDefinition def)
    {
        // Mirrors the constants in PlayerAbilityController.SpectralAxe* so tooltip stays truthful.
        const float projectDistance = 5f;
        const float yieldEfficiencyPct = 60f; // SpectralAxeYieldEfficiency * 100
        const float areaRadius = 1.5f;        // SpectralAxeAreaRadius

        // Phantom Harvest opens up bonus / hidden item drops on the same gather tick, so the
        // "logs only" restriction no longer applies for that enhancement choice.
        bool phantomHarvest = GetSpectralAxeChoice(skillsManager) == 0;

        float spectralDur = GetTooltipBuffMinionDisplayDurationSeconds(def, SpectralAxeTooltipDurationSeconds, 0f);

        body.AppendLine(O($"Throws your axe {projectDistance:0.#} units forward"));
        body.AppendLine(O($"Chops the closest tree within {areaRadius:0.#} units for {spectralDur:0.#}s, then returns"));
        body.AppendLine(O($"Gathers logs at {yieldEfficiencyPct:0.#}% efficiency"));
        if (!phantomHarvest)
            body.AppendLine(O("Only logs are gathered from that tree"));

        // Enhancement-specific lines are emitted via the trailing "Active Enhancement: X (description)"
        // pattern in AbilityEntryUI / FormatActiveEnhancementLine, matching Power Slash etc.
    }

    private static void AppendMinionSpawnTooltipEffectLinesNoStats(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def)
    {
        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
            body.AppendLine(O(InheritMinionDamageRuleLine));
        else
            body.AppendLine(O("Minion source damage"));
    }

    private static void AppendMinionSpawnTooltipScalingLinesNoStats(
        StringBuilder body,
        System.Func<string, string> S,
        AbilityDefinition def)
    {
        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        string dmgSuffix = DamageTimingSuffix();
        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            body.AppendLine(S("+0 damage from Minion Damage" + dmgSuffix));
            body.AppendLine(S(InheritMinionDealsBonusScalingLine));
        }
        else
        {
            body.AppendLine(S("+0 damage from Minion Damage" + dmgSuffix));
        }
    }

    private static void AppendMinionSpawnTooltipEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        SkillsManager skillsManager)
    {
        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        const float scalerEps = 0.05f;
        string dmgSuffix = DamageTimingSuffix();

        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
            body.AppendLine(O(InheritMinionDamageRuleLine));
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
                AppendSignedDamageScalerEffects(
                    body,
                    O,
                    dmgSuffix,
                    Mathf.RoundToInt(physScaler),
                    Mathf.RoundToInt(magScaler),
                    Mathf.RoundToInt(corrScaler));
            }
            else
            {
                body.AppendLine(O("Minion source damage"));
            }
        }
    }

    private static void AppendMinionSpawnTooltipScalingLines(
        StringBuilder body,
        System.Func<string, string> S,
        AbilityDefinition def,
        CharacterStats stats)
    {
        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        string dmgSuffix = DamageTimingSuffix();
        int mdFlat = ComputeTooltipMinionDamageFlatBonus(def, stats);

        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            body.AppendLine(S($"+{mdFlat} damage from Minion Damage{dmgSuffix}"));
            body.AppendLine(S(InheritMinionDealsBonusScalingLine));
        }
        else
        {
            body.AppendLine(S($"+{mdFlat} damage from Minion Damage{dmgSuffix}"));
        }

        AppendMinionModifierStatLines(body, S, stats, cfg.damageSourceMode);
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

    private const string ReducedBaseDamageEffectLine = "Reduced base damage on hit";

    private readonly struct WeaponScaledHitScalerPreview
    {
        public readonly float Phys;
        public readonly float Mag;
        public readonly float Corruption;

        public WeaponScaledHitScalerPreview(float phys, float mag, float corruption)
        {
            Phys = phys;
            Mag = mag;
            Corruption = corruption;
        }
    }

    /// <summary>Per-type bonus from weapon multiplier (matches runtime phys/magic/corruption split scaling).</summary>
    private static WeaponScaledHitScalerPreview ComputeWeaponScaledHitScalers(
        CharacterStats stats,
        AbilityDefinition def,
        float wEffTip,
        float allM)
    {
        float avgPhys = stats
            ? (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f
            : 0f;
        float avgMag = stats
            ? (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f
            : 0f;
        float avgCorr = stats
            ? (Mathf.Max(0f, stats.MinSplitDamage.corruptionDamage) + Mathf.Max(0f, stats.MaxSplitDamage.corruptionDamage)) *
              0.5f
            : 0f;

        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float elemM = stats != null ? AbilityElementScaling.GetElementSkillDamageMultiplier(stats) : 1f;

        float physTotalNoAp = (avgPhys * wEffTip + ailmentBonus) * allM;
        float magTotalNoAp = (avgMag * wEffTip * elemM + elementBonus * elemM) * allM;
        float corrTotalNoAp = avgCorr * wEffTip * allM;

        return new WeaponScaledHitScalerPreview(
            physTotalNoAp - avgPhys,
            magTotalNoAp - avgMag,
            corrTotalNoAp - avgCorr);
    }

    private static void AppendWeaponScaledHitScalerEffects(
        StringBuilder body,
        System.Func<string, string> O,
        in WeaponScaledHitScalerPreview scalers,
        string dmgSuffix,
        bool splitApInEffects,
        int tooltipApBonus)
    {
        const float eps = 0.05f;
        if (splitApInEffects)
        {
            AppendDecimalScalerEffectLines(body, O, scalers.Phys, scalers.Mag, scalers.Corruption, dmgSuffix);
            if (Mathf.Abs(scalers.Phys) < eps && Mathf.Abs(scalers.Mag) < eps && Mathf.Abs(scalers.Corruption) < eps)
                body.AppendLine(O("Base hit damage"));
            body.AppendLine(O($"+{tooltipApBonus} damage from Ability Power{dmgSuffix}"));
            return;
        }

        int p = Mathf.RoundToInt(scalers.Phys);
        int m = Mathf.RoundToInt(scalers.Mag);
        int c = Mathf.RoundToInt(scalers.Corruption);
        AppendSignedDamageScalerEffects(body, O, dmgSuffix, p, m, c);
        if (p == 0 && m == 0 && c == 0)
            body.AppendLine(O("Base hit damage"));
    }

    private static void AppendDecimalScalerEffectLines(
        StringBuilder body,
        System.Func<string, string> wrapLine,
        float physScaler,
        float magScaler,
        float corrScaler,
        string dmgSuffix)
    {
        AppendSignedDamageScalerEffects(
            body,
            wrapLine,
            dmgSuffix,
            Mathf.RoundToInt(physScaler),
            Mathf.RoundToInt(magScaler),
            Mathf.RoundToInt(corrScaler));
    }

    private static void AppendSignedDamageScalerEffects(
        StringBuilder body,
        System.Func<string, string> wrapLine,
        string dmgSuffix,
        int physAmount,
        int magAmount,
        int corruptionAmount = 0)
    {
        bool reducedWritten = false;
        AppendSignedDamageScalerLine(body, wrapLine, physAmount, "Physical", dmgSuffix, ref reducedWritten);
        AppendSignedDamageScalerLine(body, wrapLine, magAmount, "Magic", dmgSuffix, ref reducedWritten);
        AppendSignedDamageScalerLine(body, wrapLine, corruptionAmount, "Corruption", dmgSuffix, ref reducedWritten);
    }

    private static void AppendSignedDamageScalerLine(
        StringBuilder body,
        System.Func<string, string> wrapLine,
        int amount,
        string kind,
        string suffix,
        ref bool reducedBaseDamageLineWritten)
    {
        if (amount == 0)
            return;

        if (amount < 0)
        {
            if (reducedBaseDamageLineWritten)
                return;

            reducedBaseDamageLineWritten = true;
            body.AppendLine(wrapLine(ReducedBaseDamageEffectLine));
            return;
        }

        body.AppendLine(wrapLine(FormatSignedDamageLine(amount, kind, suffix)));
    }

    private static string FormatSignedDamageLine(int amount, string kind, string suffix)
    {
        if (amount < 0)
            return ReducedBaseDamageEffectLine;

        if (amount > 0)
            return $"+{amount} {kind} Damage{suffix}";

        return $"0 {kind} Damage{suffix}";
    }

    private static string DamageTimingSuffix() => " on hit";

    /// <summary>Pre-AP scaled hit total (Phys+Magic+Corr in preview) × AP% / 100, rounded — matches runtime AP multiplier on that base.</summary>
    private static int ComputeTooltipApBonusDamage(
        AbilityDefinition def,
        CharacterStats stats,
        float wEffTip,
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
        float tipElemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
        float linearWeaponScaled = Mathf.Max(0f,
            tipAvgPhys * wEffTip * allM
            + tipAvgMag * wEffTip * allM * tipElemM
            + tipElemBonus * allM * tipElemM
            + tipAvgCorr * wEffTip * allM);

        return Mathf.RoundToInt(
            linearWeaponScaled * bonusPctNow / CharacterStats.AbilityPowerDamagePercentDivisor);
    }

    /// <summary>Trailing green Active Enhancement line (skill-tree choice name + description).</summary>
    public static string FormatActiveEnhancementLine(AbilityDefinition def, int selectedIndex)
    {
        if (def == null || selectedIndex < 0)
            return string.Empty;

        SkillChoiceDefinition selected = ResolveSkillChoice(def, selectedIndex);
        if (selected == null)
            return string.Empty;

        string title = !string.IsNullOrWhiteSpace(selected.title)
            ? selected.title.Trim()
            : $"Enhancement {selectedIndex + 1}";
        string choiceDesc = ResolveSkillChoiceDescription(def, selectedIndex);

        return string.IsNullOrEmpty(choiceDesc)
            ? $"\nActive Enhancement: <color=#33CC66>{title}</color>"
            : $"\nActive Enhancement: <color=#33CC66>{title} ({choiceDesc})</color>";
    }

    private static string ResolveSkillChoiceDescription(AbilityDefinition def, int selectedIndex)
    {
        SkillChoiceDefinition choice = ResolveSkillChoice(def, selectedIndex);
        if (choice == null)
            return string.Empty;

        string fromPresentation = SkillsAbilityPresentationResolver.ResolveChoiceDescription(choice);
        if (!string.IsNullOrWhiteSpace(fromPresentation))
            return fromPresentation.Trim();

        return !string.IsNullOrWhiteSpace(choice.description) ? choice.description.Trim() : string.Empty;
    }

    private static SkillChoiceDefinition ResolveSkillChoice(AbilityDefinition def, int selectedIndex)
    {
        if (def == null || selectedIndex < 0)
            return null;

        SkillDatabase skillDb = SkillDatabase.LoadDefault();
        SkillDefinition skill = skillDb != null ? skillDb.Get(def.sourceSkill) : null;
        SkillUnlockDefinition unlock = SkillAbilityCommitRules.FindAbilityUnlockOnSkill(skill, def);
        if (unlock == null || unlock.choices == null)
            return null;

        var nonNullChoices = new List<SkillChoiceDefinition>(unlock.choices.Count);
        for (int i = 0; i < unlock.choices.Count; i++)
        {
            SkillChoiceDefinition c = unlock.choices[i];
            if (c != null)
                nonNullChoices.Add(c);
        }

        if (selectedIndex < 0 || selectedIndex >= nonNullChoices.Count)
            return null;

        return nonNullChoices[selectedIndex];
    }

    private static void AppendSoulforgedEnhancementEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return;

        int sel = skillsManager.GetSkillChoiceSelection(SkillType.Melee, SoulforgedWeaponChoiceSourceLevel, -1);
        if (sel == SoulforgedWeaponSwarmChoiceIndex)
        {
            body.AppendLine(O($"Summons {SoulforgedWeaponSwarmCount} soulforged weapons."));
            return;
        }

        if (sel >= 0)
        {
            string desc = ResolveSkillChoiceDescription(def, sel);
            if (!string.IsNullOrEmpty(desc))
                body.AppendLine(O(desc));
        }
    }

    /// <summary>Full ability hit totals (not "+ bonus" over weapon average) for standalone casts like Final Severance.</summary>
    private static void AppendAbilityTotalHitDamageEffects(
        StringBuilder body,
        System.Func<string, string> O,
        float physHit,
        float magHit,
        float corrHit,
        string suffix)
    {
        int p = Mathf.RoundToInt(Mathf.Max(0f, physHit));
        int m = Mathf.RoundToInt(Mathf.Max(0f, magHit));
        int c = Mathf.RoundToInt(Mathf.Max(0f, corrHit));

        if (p > 0)
            body.AppendLine(O($"{p} Physical damage{suffix}"));
        if (m > 0)
            body.AppendLine(O($"{m} Magic damage{suffix}"));
        if (c > 0)
            body.AppendLine(O($"{c} Corruption damage{suffix}"));

        if (p == 0 && m == 0 && c == 0)
            body.AppendLine(O("Base hit damage"));
    }

    private static void ComputeAverageAbilityHitSplit(
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM,
        out float physHit,
        out float magHit,
        out float corrHit)
    {
        physHit = 0f;
        magHit = 0f;
        corrHit = 0f;
        if (!def || !stats)
            return;

        float wEff = weaponMult <= 0f ? 1f : weaponMult;
        float avgPhys = (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        float avgMag = (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f;
        float avgCorr =
            (Mathf.Max(0f, stats.MinSplitDamage.corruptionDamage) + Mathf.Max(0f, stats.MaxSplitDamage.corruptionDamage)) *
            0.5f;

        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apM = stats.GetAbilityPowerDamageMultiplier(AbilityDefinition.StandardAbilityPowerCoefficient);
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);

        float physLine = avgPhys * wEff + ailmentBonus;
        float magLine = avgMag * wEff * elemM + elementBonus * elemM;
        float corrLine = avgCorr * wEff;

        physHit = physLine * allM * apM;
        magHit = magLine * allM * apM;
        corrHit = corrLine * allM * apM;
    }

    private static void AppendPerHitDamageEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        float physPerHit,
        float magPerHit,
        float corrPerHit,
        int hitCount,
        string suffix)
    {
        int p = Mathf.RoundToInt(Mathf.Max(0f, physPerHit));
        int m = Mathf.RoundToInt(Mathf.Max(0f, magPerHit));
        int c = Mathf.RoundToInt(Mathf.Max(0f, corrPerHit));
        string mult = hitCount > 1 ? $" ×{hitCount}" : string.Empty;

        if (p > 0)
            body.AppendLine(O($"{p} Physical damage{suffix}{mult}"));
        if (m > 0)
            body.AppendLine(O($"{m} Magic damage{suffix}{mult}"));
        if (c > 0)
            body.AppendLine(O($"{c} Corruption damage{suffix}{mult}"));

        if (p == 0 && m == 0 && c == 0)
            body.AppendLine(O($"Reduced base damage{suffix}{mult}"));
    }
}
