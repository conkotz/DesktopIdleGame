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

        string tag = ResolveAbilityCategoryTagLabel(def);
        if (string.IsNullOrEmpty(tag))
            return "";

        return orangeMarkup
            ? $"<color=#FFB347>{tag}</color>"
            : $"<color=#B0C8DD>{tag}</color>";
    }

    /// <summary>Effect bullets for the skill details panel (no Effects header, no cost/cooldown footer).</summary>
    public static string BuildAbilityTooltipEffectsSection(AbilityDefinition def, SkillsManager skillsManager)
    {
        return BuildCompactEffectsBody(def, skillsManager, includeDuration: true, displayStacks: 0);
    }

    /// <summary>Separate cost and cooldown lines for the details panel middle column.</summary>
    public static bool TryGetDetailsPanelResourceLines(
        AbilityDefinition def,
        SkillsManager skillsManager,
        out string costLine,
        out string cooldownLine)
    {
        costLine = string.Empty;
        cooldownLine = string.Empty;
        if (!def || CombatStarterAttackAbility.IsCombatStarterAttack(def))
            return false;

        CharacterStats stats = FindLocalPlayerStats();
        PlayerAbilityController abilityController = FindLocalPlayerAbilityController();
        AbilityTooltipAdjustments.ResolveTooltipResourceAndCooldown(
            def, skillsManager, stats, abilityController, out float resourceCost, out string resourceLabel, out float cooldown);

        costLine = $"{resourceCost:0.#} {resourceLabel}";
        cooldownLine = $"{cooldown:0.#}s";
        return true;
    }

    /// <summary>
    /// Reads the asset-driven <see cref="AbilityDefinition.tag"/>. Untagged assets fall back to
    /// auto-detection so the historical "Minion" label keeps working until they are tagged manually.
    /// </summary>
    public static string ResolveAbilityCategoryTagLabel(AbilityDefinition def)
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

    public static PlayerAbilityController FindLocalPlayerAbilityController()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            var pac = player.GetComponent<PlayerAbilityController>();
            if (pac != null)
                return pac;
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);
    }

    private static void AppendTooltipEnergyCooldownFooter(
        StringBuilder body,
        System.Func<string, string> wrapLine,
        AbilityDefinition def,
        SkillsManager skillsManager,
        CharacterStats stats,
        PlayerAbilityController abilityController)
    {
        AbilityTooltipAdjustments.ResolveTooltipResourceAndCooldown(
            def, skillsManager, stats, abilityController, out float resourceCost, out string resourceLabel, out float cooldown);
        body.AppendLine(wrapLine($"{resourceCost:0.#} {resourceLabel} • {cooldown:0.#}s Cooldown"));
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

    /// <summary>Optional suffix: total % bonus from ability power at current stats.</summary>
    public static string FormatAbilityPowerSuffix(CharacterStats stats)
    {
        if (stats == null)
            return "";

        float bonusPct = Mathf.Max(0f, stats.AbilityPower);
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

        if (CombatStarterAttackAbility.IsMeleeStarterAttack(def))
        {
            const string meleeLine = "Required: Melee weapon or unarmed";
            bool meleeOk = stats == null || CombatStarterAttackAbility.IsUsableWithEquippedWeapon(def, stats);
            if (!meleeOk)
                return $"<color=#FF5C5C>{meleeLine}</color>";
            if (accentWhenOk)
                return $"<color={okColor}>{meleeLine}</color>";
            return meleeLine;
        }

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

    /// <summary>
    /// Weapon requirement plus ability-specific hit-type rules (Rend, Envenom, Crusader Strike).
    /// Each line is green when met, red when not.
    /// </summary>
    public static string BuildAbilityRequirementsRichText(AbilityDefinition def, CharacterStats stats, bool accentWhenOk = false)
    {
        if (def == null)
            return string.Empty;

        var lines = new List<string>();

        string weaponLine = BuildWeaponRequirementRichLine(def, stats, accentWhenOk);
        if (!string.IsNullOrWhiteSpace(weaponLine))
            lines.Add(weaponLine);

        if (IsRend(def))
        {
            bool ok = stats == null || stats.GetAverageWeaponPhysicalDamagePerHit() > 0.0001f;
            lines.Add(FormatConditionalRequirementLine("Hit MUST be physical.", ok, accentWhenOk));
        }
        else if (IsEnvenom(def))
        {
            bool ok = stats == null || stats.AverageCorruptionHit > 0.0001f;
            lines.Add(FormatConditionalRequirementLine("Hit MUST be corruption.", ok, accentWhenOk));
        }
        else if (IsCrusaderStrike(def))
        {
            bool ok = stats == null || stats.CurrentMeleeWeaponHasPhysicalOrFireDamage();
            lines.Add(FormatConditionalRequirementLine("Weapon must have physical or fire damage.", ok, accentWhenOk));
        }

        return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
    }

    private static string FormatConditionalRequirementLine(string text, bool ok, bool accentWhenOk)
    {
        if (!ok)
            return $"<color=#FF5C5C>{text}</color>";

        if (accentWhenOk)
            return $"<color=#55DD55>{text}</color>";

        return text;
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
        if (IsCleavingChop(def) || IsSpectralAxe(def) || IsPowerSlash(def))
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

    private static bool IsGuardiansHammer(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.GuardiansHammerAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsCrusaderStrike(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.CrusaderStrikeAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsWhirlwind(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.WhirlwindAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsFinalSeverance(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.FinalSeveranceAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutionersDescent(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.ExecutionersDescentAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsBladestorm(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.BladestormAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsShadowStrike(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.ShadowStrikeAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsEnergyInfusion(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.EnergyInfusionAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsBattleTrance(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.BattleTranceAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsHammerTempest(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.HammerTempestAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsFlameCharge(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.FlameChargeAbilityId, System.StringComparison.OrdinalIgnoreCase);

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

    private static int GetBladestormBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.BladestormEnhancementParentSpineNodeId, -1);
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

    private static int GetHammerTempestBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.HammerTempestEnhancementParentSpineNodeId, -1);
    }

    private static float GetHammerTempestDurationBonusSeconds(SkillsManager skillsManager)
    {
        int choice = GetHammerTempestBranchChoice(skillsManager);
        if (choice == AbilityCombatPower.HammerTempestSacredArsenalChoiceIndex)
            return -AbilityCombatPower.HammerTempestSacredArsenalDurationPenaltySeconds;
        return 0f;
    }

    private static int GetBattleTranceBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.BattleTranceEnhancementParentSpineNodeId, -1);
    }

    private static int GetFlameChargeBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.FlameChargeEnhancementParentSpineNodeId, -1);
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

        if (IsFlameCharge(def))
        {
            scaling.AppendLine(S("Scales with Fire damage"));
            return scaling.ToString().TrimEnd();
        }

        const float scalingEpsilon = 0.0001f;

        if (IsExecutionersDescent(def) && GetExecutionersDescentBranchChoice(skillsManager) == 2)
        {
            float shockMult = AbilityCombatPower.ExecutionersDescentContinuumShockwaveWeaponMultiplier;
            float allM = def.GetEffectiveAllDamageMultiplier();
            scaling.AppendLine(S($"Deals {shockMult * 100f:0.#}% of your weapon damage per shockwave"));
            AppendAbilityTooltipBonusScalerLines(scaling, S, def, stats, shockMult, allM);
            return scaling.ToString().TrimEnd();
        }

        if (IsCrusaderStrike(def))
        {
            scaling.AppendLine(S($"Cast 1: {AbilityCombatPower.CrusaderStrikeFirstHitWeaponMultiplier * 100f:0.#}% of your weapon physical or Fire damage"));
            scaling.AppendLine(S($"Cast 2: {AbilityCombatPower.CrusaderStrikeSecondHitWeaponMultiplier * 100f:0.#}% of your weapon physical or Fire damage"));
            scaling.AppendLine(S($"Cast 3: {AbilityCombatPower.CrusaderStrikeFinalHitWeaponMultiplier * 100f:0.#}% of your weapon physical or Fire damage"));
            return scaling.ToString().TrimEnd();
        }

        if (IsCleavingStrikes(def) || weaponMult <= scalingEpsilon)
            return string.Empty;

        float allDamageMult = def.GetEffectiveAllDamageMultiplier();
        scaling.AppendLine(S($"Deals {weaponMult * 100f:0.#}% of your weapon damage"));
        AppendAbilityTooltipBonusScalerLines(scaling, S, def, stats, weaponMult, allDamageMult);
        return scaling.ToString().TrimEnd();
    }

    /// <summary>
    /// Blue lines under weapon-% scaling: ability power and elemental bonuses (hidden when zero).
    /// </summary>
    private static void AppendAbilityTooltipBonusScalerLines(
        StringBuilder scaling,
        Func<string, string> S,
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM)
    {
        if (!def || !stats)
            return;

        const float eps = 0.05f;
        float wEff = weaponMult <= 0.0001f ? 1f : weaponMult;
        float apM = stats.GetAbilityPowerDamageMultiplier();
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);

        float tipAvgPhys =
            (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        float tipAvgMag =
            (Mathf.Max(0f, stats.MinSplitDamage.magic) + Mathf.Max(0f, stats.MaxSplitDamage.magic)) * 0.5f;
        float tipAvgCorr =
            (Mathf.Max(0f, stats.MinSplitDamage.corruptionDamage) + Mathf.Max(0f, stats.MaxSplitDamage.corruptionDamage)) *
            0.5f;

        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);

        float linearWeaponScaled = Mathf.Max(0f,
            tipAvgPhys * wEff * allM
            + tipAvgMag * wEff * allM * elemM
            + elementBonus * allM * elemM
            + tipAvgCorr * wEff * allM
            + ailmentBonus * allM);

        int apBonusPct = Mathf.RoundToInt(Mathf.Max(0f, (apM - 1f) * 100f));
        if (apBonusPct > 0)
            scaling.AppendLine(S($"+{apBonusPct}% damage from Ability Power"));

        AppendElementBonusScalerLine(scaling, S, def, stats, allM, apM, eps);
    }

    private static void AppendElementBonusScalerLine(
        StringBuilder scaling,
        Func<string, string> S,
        AbilityDefinition def,
        CharacterStats stats,
        float allM,
        float apM,
        float eps)
    {
        if (def.fireDamageMultiplier > eps && stats.CurrentMagicAttackType == MagicAttackType.Fire)
        {
            float amount = AbilityElementScaling.GetElementDamageBonus(def, stats)
                * AbilityElementScaling.GetElementSkillDamageMultiplier(stats) * allM * apM;
            if (amount >= eps)
                scaling.AppendLine(S($"+{Mathf.RoundToInt(amount)} Fire damage"));
            return;
        }

        if (def.iceDamageMultiplier > eps && stats.CurrentMagicAttackType == MagicAttackType.Ice)
        {
            float amount = AbilityElementScaling.GetElementDamageBonus(def, stats)
                * AbilityElementScaling.GetElementSkillDamageMultiplier(stats) * allM * apM;
            if (amount >= eps)
                scaling.AppendLine(S($"+{Mathf.RoundToInt(amount)} Ice damage"));
            return;
        }

        if (def.lightningDamageMultiplier > eps && stats.CurrentMagicAttackType == MagicAttackType.Lightning)
        {
            float amount = AbilityElementScaling.GetElementDamageBonus(def, stats)
                * AbilityElementScaling.GetElementSkillDamageMultiplier(stats) * allM * apM;
            if (amount >= eps)
                scaling.AppendLine(S($"+{Mathf.RoundToInt(amount)} Lightning damage"));
        }
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

        if (CombatStarterAttackAbility.IsCombatStarterAttack(def))
            return string.Empty;

        string O(string line) => orangeMarkup ? $"<color=#FFB347>{line}</color>" : line;

        PlayerAbilityController abilityController = FindLocalPlayerAbilityController();
        float liveDamageMultiplier = AbilityTooltipAdjustments.GetTooltipAbilityDamageMultiplier(abilityController);

        float weaponMult = def.weaponDamageMultiplier;
        float cooldown = Mathf.Max(0f, def.cooldown);
        AbilityTooltipAdjustments.ApplySkillTreeChoices(def, skillsManager, ref weaponMult, ref cooldown);

        float allM = def.GetEffectiveAllDamageMultiplier();

        var body = new StringBuilder();
        body.AppendLine(O("Effects:"));

        if (IsLumberFrenzy(def))
        {
            AppendLumberFrenzyTooltipEffects(body, O, skillsManager);
            body.AppendLine(string.Empty);
            float lumberDur = GetTooltipBuffMinionDisplayDurationSeconds(def, LumberFrenzyBuffDurationSecondsTooltip, 0f);
            body.AppendLine(O($"Duration: {lumberDur:0.#}s"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsFishingFrenzy(def))
        {
            AppendFishingFrenzyTooltipEffects(body, O, skillsManager);
            body.AppendLine(string.Empty);
            float fishingDur = GetTooltipBuffMinionDisplayDurationSeconds(def, FishingFrenzyBuffDurationSecondsTooltip, 0f);
            body.AppendLine(O($"Duration: {fishingDur:0.#}s"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsFlameCharge(def))
        {
            int enhance = GetFlameChargeBranchChoice(skillsManager);
            int charges = enhance == 0 ? 2 : 1;
            body.AppendLine(O(
                $"Charge forward {AbilityCombatPower.FlameChargeDashDistance:0.#} units (no dash damage). Leaves fire on the ground for {AbilityCombatPower.FlameChargeTrailDurationSeconds:0.#}s."));
            body.AppendLine(O(
                $"Trail: {AbilityCombatPower.FlameChargeTrailTotalFlatFireDamage:0.#} Fire damage over {AbilityCombatPower.FlameChargeTrailDurationSeconds:0.#}s to enemies inside (one tick per enemy)."));
            if (enhance == 0)
                body.AppendLine(O($"Double Ignition: {charges} charges (trail segments cannot overlap)."));
            else if (enhance == 1)
                body.AppendLine(O(
                    $"Volcanic Rush: +{AbilityCombatPower.FlameChargeVolcanicExplosionFlatFireDamage:0.#} Fire explosion at dash end ({AbilityCombatPower.FlameChargeVolcanicExplosionRadius:0.#} radius, scales with Fire damage)."));
            body.AppendLine(string.Empty);
            body.AppendLine(O($"Duration: {GetTooltipBuffMinionDisplayDurationSeconds(def, AbilityCombatPower.FlameChargeTrailDurationSeconds, 0f):0.#}s (trail)"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsEnergyInfusion(def))
        {
            int enhance = GetEnergyInfusionBranchChoice(skillsManager);
            body.AppendLine(O(
                $"While active, melee abilities that use Energy instead spend {AbilityCombatPower.EnergyInfusionBaseManaCostFraction * 100f:0.#}% Mana and {(1f - AbilityCombatPower.EnergyInfusionBaseManaCostFraction) * 100f:0.#}% Energy."));
            body.AppendLine(O("If you do not have enough Mana for the converted portion, that ability uses its full Energy cost instead."));
            if (enhance == 0)
                body.AppendLine(O(
                    $"Efficient Conversion: use +{AbilityCombatPower.EnergyInfusionEfficientConversionAdditionalManaCostFraction * 100f:0.#}% additional Mana instead of Energy and gain +{AbilityCombatPower.EnergyInfusionEfficientConversionFlatManaRegenPerSecond:0.#} Mana per second while active."));
            else if (enhance == 1)
                body.AppendLine(O(
                    $"Overcharged: abilities use {AbilityCombatPower.EnergyInfusionOverchargedManaCostFraction * 100f:0.#}% Mana instead of {AbilityCombatPower.EnergyInfusionBaseManaCostFraction * 100f:0.#}%, and if Mana is used that ability gains +{AbilityCombatPower.EnergyInfusionOverchargedAbilityPowerPercentBonus:0.#}% ability power."));
            body.AppendLine(string.Empty);
            body.AppendLine(O("Duration: Toggle"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsBattleTrance(def))
        {
            AppendBattleTranceTooltipEffects(body, O, skillsManager);
            float dur = GetTooltipBuffMinionDisplayDurationSeconds(def, AbilityCombatPower.BattleTranceBaseDurationSeconds, 0f);
            body.AppendLine(string.Empty);
            body.AppendLine(O($"Duration: {dur:0.#}s"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
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
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsCleavingChop(def))
        {
            AppendCleavingChopTooltipEffects(body, O, skillsManager, def);
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"Duration: {GetTooltipBuffMinionDisplayDurationSeconds(def, 40f, GetCleavingChopDurationBonusSeconds(skillsManager)):0.#}s"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsSpectralAxe(def))
        {
            AppendSpectralAxeTooltipEffects(body, O, skillsManager, def);
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"Duration: {GetTooltipBuffMinionDisplayDurationSeconds(def, SpectralAxeTooltipDurationSeconds, 0f):0.#}s"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
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
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
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
            int stackCount = ResolveEnvenomTooltipPoisonStacks(stats, skillsManager);
            body.AppendLine(O($"Applies {stackCount} stacks of poison"));
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
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);

            if (crescentSel == 0)
            {
                float ph = Mathf.Max(0f, physHit);
                physHit = 0f;
                magHit += ph;
                body.AppendLine(O("Converts 100% Physical to Fire and applies Burn."));
            }

            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);

            if (crescentSel == 1)
                body.AppendLine(O("Hits all enemies."));
            else
                body.AppendLine(O("Hits 3 enemies."));
        }
        else if (IsCrusaderStrike(def))
        {
            int crusaderChoice = GetCrusaderStrikeSelectedChoice(skillsManager);
            ComputeAverageCrusaderStrikeCastSplit(
                stats,
                AbilityCombatPower.CrusaderStrikeFirstHitWeaponMultiplier,
                1f,
                finalStrike: false,
                out float cast1Phys,
                out float cast1Fire);
            ComputeAverageCrusaderStrikeCastSplit(
                stats,
                AbilityCombatPower.CrusaderStrikeSecondHitWeaponMultiplier,
                1f,
                finalStrike: false,
                out float cast2Phys,
                out float cast2Fire);
            ComputeAverageCrusaderStrikeCastSplit(
                stats,
                AbilityCombatPower.CrusaderStrikeFinalHitWeaponMultiplier,
                GetCrusaderStrikeFinalFireTooltipScale(def, stats, crusaderChoice),
                finalStrike: true,
                out float cast3Phys,
                out float cast3Fire);
            float healAmount = stats != null
                ? stats.MaxHP * GetCrusaderStrikeHealFraction(crusaderChoice)
                : 0f;
            int healPercent = Mathf.RoundToInt(GetCrusaderStrikeHealFraction(crusaderChoice) * 100f);

            body.AppendLine(O($"Cast 1 - {FormatCrusaderStrikeDamageLabel(cast1Phys, cast1Fire)}"));
            body.AppendLine(O(
                $"Cast 2 - {FormatCrusaderStrikeDamageLabel(cast2Phys, cast2Fire)}, Heal {Mathf.RoundToInt(healAmount)}Hp ({healPercent}% max health)"));
            body.AppendLine(O($"Cast 3 - {FormatCrusaderStrikeDamageLabel(cast3Phys, cast3Fire)} (all weapon damage as fire)"));
        }
        else if (IsFinalSeverance(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            int fsEnhance = GetMeleeLv45BranchChoice(skillsManager);
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);

            if (fsEnhance == 1)
            {
                float frac = AbilityCombatPower.FinalSeveranceThousandCutsHitFraction;
                int hits = AbilityCombatPower.FinalSeveranceThousandCutsHitCount;
                AppendPerHitDamageEffectLines(body, O, physHit * frac, magHit * frac, corrHit * frac, hits, dmgSuffix);
            }
            else
                AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);

            body.AppendLine(O($"Channel: {AbilityCombatPower.FinalSeveranceChannelSeconds:0.#}s"));
            body.AppendLine(O(
                $"Wide arc — up to {AbilityCombatPower.FinalSeveranceMaxTargets} enemies hit."));

            if (fsEnhance == 0)
            {
                float bonusMult = AbilityCombatPower.FinalSeveranceWorldbreakerBonusMultiplier - 1f;
                int bonus = Mathf.RoundToInt((physHit + magHit + corrHit) * bonusMult);
                body.AppendLine(O($"20% bonus damage to enemies on full life (+{bonus})"));
            }
        }
        else if (IsShadowStrike(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            int enhance = GetShadowStrikeBranchChoice(skillsManager);
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);

            body.AppendLine(O(
                $"Teleports to the closest enemy up to {AbilityCombatPower.ShadowStrikeForwardReach:0.#} units ahead in your facing arc."));
            body.AppendLine(O("Does not consume your auto-attack swing timer."));

            if (enhance == 0)
                body.AppendLine(O(
                    $"Marks the target — the next critical hit deals +{AbilityCombatPower.ShadowStrikeLethalCritBonusFraction * 100f:0.#}% critical damage, then the mark expires."));
            else if (enhance == 1)
                body.AppendLine(O(
                    $"Marks the target on hit — when they die, Shadow Strike cooldown is reduced by {AbilityCombatPower.ShadowStrikeExecutionCooldownRefundSeconds:0.#}s."));
        }
        else if (IsBladestorm(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            int enhance = GetBladestormBranchChoice(skillsManager);
            float channelSeconds = AbilityCombatPower.BladestormChannelSeconds;
            float strikeRate = stats != null
                ? Mathf.Max(0.05f, stats.AttacksPerSecond * AbilityCombatPower.BladestormAttackSpeedMultiplier)
                : AbilityCombatPower.BladestormAttackSpeedMultiplier;
            int strikeCount = Mathf.Max(1, Mathf.RoundToInt(strikeRate * channelSeconds));
            float normalMult = AbilityCombatPower.BladestormNormalHitWeaponMultiplier;
            float finaleMult = AbilityCombatPower.BladestormFinaleHitWeaponMultiplier;

            ComputeAverageAbilityHitSplit(def, stats, normalMult, allM, out float normPhys, out float normMag, out float normCorr, liveDamageMultiplier);
            int perStrikeTotal = Mathf.RoundToInt(normPhys + normMag + normCorr);
            body.AppendLine(O(
                $"Relentless Execution: {channelSeconds:0.#}s channel — {strikeCount} strikes at {AbilityCombatPower.BladestormAttackSpeedMultiplier * 100f:0.#}% attack speed"));
            body.AppendLine(O($"Each strike: {perStrikeTotal} total damage{dmgSuffix} (50% weapon damage)"));
            body.AppendLine(O("Locks onto a single enemy in front of you for the duration."));
            body.AppendLine(O(
                $"Take {(1f - AbilityCombatPower.BladestormChannelDamageTakenMultiplier) * 100f:0.#}% reduced damage during Relentless Execution."));

            if (enhance == 0)
            {
                ComputeAverageAbilityHitSplit(def, stats, finaleMult, allM, out float finPhys, out float finMag, out float finCorr, liveDamageMultiplier);
                int finaleTotal = Mathf.RoundToInt(finPhys + finMag + finCorr);
                body.AppendLine(O(
                    $"Finale: additional strike after the combo — {finaleTotal} total damage{dmgSuffix} (150% weapon damage)"));
            }
            else if (enhance == 1)
                body.AppendLine(O(
                    "If the target dies during the combo, remaining strikes hit the nearest enemy in range (auto-paths while channeling)."));
        }
        else if (IsExecutionersDescent(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            int enhance = GetExecutionersDescentBranchChoice(skillsManager);

            if (enhance == 2)
            {
                float continuumMult = AbilityCombatPower.ExecutionersDescentContinuumShockwaveWeaponMultiplier;
                ComputeAverageAbilityHitSplit(
                    def,
                    stats,
                    continuumMult,
                    allM,
                    out float shockPhys,
                    out float shockMag,
                    out float shockCorr,
                    liveDamageMultiplier);

                AppendAbilityTotalHitDamageEffects(body, O, shockPhys, shockMag, shockCorr, $"{dmgSuffix} per pulse", stats);
                body.AppendLine(O(
                    $"Hits enemies within {AbilityCombatPower.ExecutionersDescentShockwaveRadius:0.#} units of the anchor"));
                body.AppendLine(O(
                    $"Continuum: axe anchors at impact height for {AbilityCombatPower.ExecutionersDescentContinuumDurationSeconds:0.#}s — no crash descent"));
                body.AppendLine(O(
                    $"Releases {AbilityCombatPower.GetExecutionersDescentContinuumShockwaveCount()} shockwaves every {AbilityCombatPower.ExecutionersDescentContinuumShockwaveIntervalSeconds:0.#}s"));
            }
            else
            {
                ComputeAverageAbilityHitSplit(
                    def,
                    stats,
                    AbilityCombatPower.ExecutionersDescentShockwaveWeaponMultiplier,
                    allM,
                    out float shockPhys,
                    out float shockMag,
                    out float shockCorr,
                    liveDamageMultiplier);
                int shockTotal = Mathf.RoundToInt(shockPhys + shockMag + shockCorr);

                ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
                AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);

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
        }
        else if (IsWhirlwind(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);
            body.AppendLine(O("Holding the hotkey channels Whirlwind continuously while energy remains."));
            body.AppendLine(O(
                $"Move speed is reduced by {AbilityCombatPower.WhirlwindBaseMoveSpeedPenaltyFraction * 100f:0.#}% while channelling."));

            int wwEnhance = GetMeleeLv15BranchChoice(skillsManager, 0);
            if (wwEnhance == 0)
            {
                float reducedCost = Mathf.Max(
                    0f,
                    Mathf.Max(0f, def.energyCost) - AbilityCombatPower.WhirlwindTwinCycloneChannelCostReductionPerSecond);
                body.AppendLine(O(
                    $"Channel cost is reduced to {reducedCost:0.#} Energy / s."));
                body.AppendLine(O("Move speed penalty is halved while using Whirlwind."));
            }
            else if (wwEnhance == 1)
            {
                body.AppendLine(O(
                    $"+{AbilityCombatPower.WhirlwindExpansiveRangePerStage:0.#} range per stage while channeling (up to 5 stacks)."));
                body.AppendLine(O(
                    $"+{AbilityCombatPower.WhirlwindExpansiveDamagePerSecond * 100f:0.#}% damage per second while channeling (up to 5 stacks)."));
            }
        }
        else if (IsGuardiansHammer(def))
        {
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, DamageTimingSuffix(), stats);
            body.AppendLine(O($"Wide frontal slam - up to {AbilityCombatPower.GuardiansHammerForwardReach:0.#} range."));

            int hammerEnhance = GetMeleeLv15BranchChoice(skillsManager, 3);
            if (hammerEnhance == 0 && stats != null)
            {
                float guardAmount = stats.MaxHP * AbilityCombatPower.GuardiansHammerProtectorResolveGuardFractionMaxHealth;
                body.AppendLine(O(
                    $"Gain {Mathf.RoundToInt(guardAmount)} Guard ({AbilityCombatPower.GuardiansHammerProtectorResolveGuardFractionMaxHealth * 100f:0.#}% max health) for {AbilityCombatPower.GuardiansHammerProtectorResolveDurationSeconds:0.#}s."));
            }
            else if (hammerEnhance == 1)
            {
                body.AppendLine(O(
                    $"Burning enemies hit explode in {AbilityCombatPower.GuardiansHammerBurningVerdictExplosionRadius:0.#} range for {AbilityCombatPower.GuardiansHammerBurningVerdictTicksWorth}x current burn tick damage without removing Burn."));
            }
        }
        else if (IsPowerSlash(def))
        {
            AppendPowerSlashTooltipHitDamage(body, O, def, stats, weaponMult, allM, liveDamageMultiplier);
        }
        else if (UsesCombinedTotalHitDamageTooltip(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);
        }

        if (IsCleavingStrikes(def))
        {
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"Duration: {GetTooltipBuffMinionDisplayDurationSeconds(def, 5f, GetCleavingStrikesDurationBonusSeconds(skillsManager)):0.#}s"));
        }
        else if (IsHammerTempest(def))
        {
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"Duration: {GetTooltipBuffMinionDisplayDurationSeconds(def, AbilityCombatPower.HammerTempestBaseDurationSeconds, GetHammerTempestDurationBonusSeconds(skillsManager)):0.#}s"));
        }

        body.AppendLine(string.Empty);
        AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);

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

        if (string.Equals(
                buffId,
                PlayerAbilityController.CrusaderStrikeFireBalanceHudBuffId,
                StringComparison.OrdinalIgnoreCase))
        {
            title = "Fire Balance";
            body =
                "30% of your physical damage is converted to fire.\n" +
                "Final Strike gains +20% fire damage.";
            return true;
        }

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

        if (!CombatStarterAttackAbility.IsCombatStarterAttack(def))
        {
            string costLine = BuildAbilityEnergyCooldownLine(def, skillsManager, orangeMarkup: false);
            if (!string.IsNullOrWhiteSpace(costLine))
                body = string.IsNullOrWhiteSpace(body) ? costLine : $"{body}\n\n{costLine}";
        }

        return !string.IsNullOrWhiteSpace(body);
    }

    /// <summary>Footer line shared by ability list and action-bar tooltips.</summary>
    public static string BuildAbilityEnergyCooldownLine(
        AbilityDefinition def,
        SkillsManager skillsManager,
        bool orangeMarkup)
    {
        if (!def || CombatStarterAttackAbility.IsCombatStarterAttack(def))
            return string.Empty;

        string O(string line) => orangeMarkup ? $"<color=#FFB347>{line}</color>" : line;

        CharacterStats stats = FindLocalPlayerStats();
        PlayerAbilityController abilityController = FindLocalPlayerAbilityController();
        AbilityTooltipAdjustments.ResolveTooltipResourceAndCooldown(
            def, skillsManager, stats, abilityController, out float resourceCost, out string resourceLabel, out float cooldown);
        return O($"{resourceCost:0.#} {resourceLabel} • {cooldown:0.#}s Cooldown");
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

            if (line.Contains(" Cooldown", StringComparison.Ordinal) && line.Contains("•", StringComparison.Ordinal))
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

    private static void AppendBattleTranceTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        int enhance = GetBattleTranceBranchChoice(skillsManager);

        float atkSpeed = AbilityCombatPower.BattleTranceBaseAttackSpeedBonus;
        float cdr = AbilityCombatPower.BattleTranceBaseAbilityCooldownReduction;
        float damageTaken = AbilityCombatPower.BattleTranceBaseDamageTakenMultiplier;
        float moveSpeed = 0f;

        if (enhance == 0)
        {
            atkSpeed += AbilityCombatPower.BattleTranceUnrelentingAttackSpeedBonus;
            cdr += AbilityCombatPower.BattleTranceUnrelentingCooldownReductionBonus;
            damageTaken = AbilityCombatPower.BattleTranceUnrelentingDamageTakenMultiplier;
        }
        else if (enhance == 1)
        {
            damageTaken = AbilityCombatPower.BattleTranceControlledDamageTakenMultiplier;
            moveSpeed = AbilityCombatPower.BattleTranceControlledMoveSpeedBonus;
        }

        AppendBattleTranceEffectLines(body, O, cdr, atkSpeed, damageTaken, moveSpeed, enhance);
    }

    private static void AppendBattleTranceBaseTooltipEffects(StringBuilder body, System.Func<string, string> O)
    {
        AppendBattleTranceEffectLines(
            body,
            O,
            AbilityCombatPower.BattleTranceBaseAbilityCooldownReduction,
            AbilityCombatPower.BattleTranceBaseAttackSpeedBonus,
            AbilityCombatPower.BattleTranceBaseDamageTakenMultiplier,
            0f,
            enhancePick: -1);
    }

    private static void AppendBattleTranceEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        float cdr,
        float atkSpeed,
        float damageTakenMultiplier,
        float moveSpeed,
        int enhancePick)
    {
        body.AppendLine(O("While active:"));
        body.AppendLine(O($"+{cdr * 100f:0.#}% ability cooldown reduction"));
        body.AppendLine(O($"+{atkSpeed * 100f:0.#}% attack speed"));
        body.AppendLine(O(
            $"+{(AbilityCombatPower.BattleTranceBaseMeleeDamageMultiplier - 1f) * 100f:0.#}% melee damage"));
        body.AppendLine(O($"+{(damageTakenMultiplier - 1f) * 100f:0.#}% damage taken"));

        if (moveSpeed > 0.001f)
            body.AppendLine(O($"+{moveSpeed * 100f:0.#}% movement speed"));

        if (enhancePick == 2)
        {
            body.AppendLine(O(
                $"Killing an enemy extends duration by {AbilityCombatPower.BattleTranceEndlessAssaultKillExtensionSeconds:0.#}s (up to +{AbilityCombatPower.BattleTranceEndlessAssaultMaxBonusDurationSeconds:0.#}s)."));
        }
    }

    /// <summary>Skill tree spine rows: flavor line plus base combat effects (no enhancement pick).</summary>
    public static bool TryBuildSkillTreeAbilityEffectsAppendix(
        AbilityDefinition def,
        SkillsManager skillsManager,
        out string appendix)
    {
        appendix = null;
        if (!IsBattleTrance(def))
            return false;

        var body = new StringBuilder();
        AppendBattleTranceBaseTooltipEffects(body, s => s);
        appendix = body.ToString().TrimEnd();
        return !string.IsNullOrWhiteSpace(appendix);
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
        float tooltipApBonusPercent)
    {
        const float eps = 0.05f;
        if (splitApInEffects)
        {
            AppendDecimalScalerEffectLines(body, O, scalers.Phys, scalers.Mag, scalers.Corruption, dmgSuffix);
            if (Mathf.Abs(scalers.Phys) < eps && Mathf.Abs(scalers.Mag) < eps && Mathf.Abs(scalers.Corruption) < eps)
                body.AppendLine(O("Base hit damage"));
            if (tooltipApBonusPercent > eps)
                body.AppendLine(O($"+{tooltipApBonusPercent:0.#}% damage from Ability Power{dmgSuffix}"));
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

    /// <summary>Ability-power percent bonus shown in tooltips (matches runtime AP multiplier on the scaled hit base).</summary>
    private static float ComputeTooltipApBonusPercent(
        AbilityDefinition def,
        CharacterStats stats,
        float wEffTip,
        float allM)
    {
        if (!def || !stats)
            return 0f;

        float apM = stats.GetAbilityPowerDamageMultiplier();
        return Mathf.Max(0f, (apM - 1f) * 100f);
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

    private static int ResolveEnvenomTooltipPoisonStacks(CharacterStats stats, SkillsManager skillsManager)
    {
        int baseStacks = stats != null ? stats.PoisonMaxStacks : 3;
        if (GetMeleeSkillRow5Choice(skillsManager) == 0)
            return baseStacks + 2;
        return baseStacks;
    }

    private static void AppendPowerSlashTooltipHitDamage(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM,
        float liveDamageMultiplier)
    {
        string suffix = DamageTimingSuffix();
        if (!stats)
        {
            body.AppendLine(O("Base hit damage"));
            return;
        }

        ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
        DistributeMagicLaneDamage(stats, magHit, out float fireHit, out float iceHit, out float lightningHit, out float untypedMagicHit);
        AppendElementAwareDamageLines(body, O, physHit, fireHit, iceHit, lightningHit, untypedMagicHit, corrHit, suffix);
    }

    /// <summary>
    /// Splits scaled magic-lane damage into weapon element portions plus any untyped magic (flat gear magic).
    /// </summary>
    private static void DistributeMagicLaneDamage(
        CharacterStats stats,
        float totalMagicHit,
        out float fireHit,
        out float iceHit,
        out float lightningHit,
        out float untypedMagicHit)
    {
        fireHit = 0f;
        iceHit = 0f;
        lightningHit = 0f;
        untypedMagicHit = Mathf.Max(0f, totalMagicHit);

        if (!stats || totalMagicHit <= 0f)
            return;

        if (stats.TryGetWeaponElementDamageProfile(out bool hasFire, out bool hasIce, out bool hasLightning))
        {
            int weaponElementCount = (hasFire ? 1 : 0) + (hasIce ? 1 : 0) + (hasLightning ? 1 : 0);
            if (weaponElementCount == 1)
            {
                if (hasFire)
                    fireHit = totalMagicHit;
                else if (hasIce)
                    iceHit = totalMagicHit;
                else
                    lightningHit = totalMagicHit;
                untypedMagicHit = 0f;
                return;
            }
        }

        float avgMagic = stats.AverageMagicHit;
        if (avgMagic <= 0.0001f)
            return;

        float scale = totalMagicHit / avgMagic;
        fireHit = stats.GetAverageWeaponFireDamagePerHit() * scale;
        iceHit = stats.GetAverageWeaponIceDamagePerHit() * scale;
        lightningHit = stats.GetAverageWeaponLightningDamagePerHit() * scale;
        untypedMagicHit = Mathf.Max(0f, totalMagicHit - fireHit - iceHit - lightningHit);
    }

    /// <summary>Full ability hit totals (not "+ bonus" over weapon average) for standalone casts like Final Severance.</summary>
    private static void AppendAbilityTotalHitDamageEffects(
        StringBuilder body,
        System.Func<string, string> O,
        float physHit,
        float magHit,
        float corrHit,
        string suffix,
        CharacterStats stats = null)
    {
        SplitHitDamageForTooltipDisplay(stats, physHit, magHit, corrHit,
            out int phys, out int fire, out int ice, out int lightning, out int magic, out int corruption);

        AppendElementAwareDamageLines(body, O, phys, fire, ice, lightning, magic, corruption, suffix);
    }

    private static void AppendElementAwareDamageLines(
        StringBuilder body,
        System.Func<string, string> O,
        float physHit,
        float fireHit,
        float iceHit,
        float lightningHit,
        float magicHit,
        float corrHit,
        string suffix)
    {
        int p = Mathf.RoundToInt(Mathf.Max(0f, physHit));
        int f = Mathf.RoundToInt(Mathf.Max(0f, fireHit));
        int i = Mathf.RoundToInt(Mathf.Max(0f, iceHit));
        int l = Mathf.RoundToInt(Mathf.Max(0f, lightningHit));
        int m = Mathf.RoundToInt(Mathf.Max(0f, magicHit));
        int c = Mathf.RoundToInt(Mathf.Max(0f, corrHit));

        if (p > 0)
            body.AppendLine(O($"{p} Physical damage{suffix}"));
        if (f > 0)
            body.AppendLine(O($"{f} Fire damage{suffix}"));
        if (i > 0)
            body.AppendLine(O($"{i} Ice damage{suffix}"));
        if (l > 0)
            body.AppendLine(O($"{l} Lightning damage{suffix}"));
        if (m > 0)
            body.AppendLine(O($"{m} Magic damage{suffix}"));
        if (c > 0)
            body.AppendLine(O($"{c} Corruption damage{suffix}"));

        if (p == 0 && f == 0 && i == 0 && l == 0 && m == 0 && c == 0)
            body.AppendLine(O("Base hit damage"));
    }

    private static void SplitHitDamageForTooltipDisplay(
        CharacterStats stats,
        float physHit,
        float magHit,
        float corrHit,
        out int phys,
        out int fire,
        out int ice,
        out int lightning,
        out int magic,
        out int corruption)
    {
        phys = Mathf.RoundToInt(Mathf.Max(0f, physHit));
        corruption = Mathf.RoundToInt(Mathf.Max(0f, corrHit));
        fire = 0;
        ice = 0;
        lightning = 0;
        magic = 0;

        DistributeMagicLaneDamage(stats, magHit, out float fireHit, out float iceHit, out float lightningHit, out float untypedMagicHit);
        fire = Mathf.RoundToInt(Mathf.Max(0f, fireHit));
        ice = Mathf.RoundToInt(Mathf.Max(0f, iceHit));
        lightning = Mathf.RoundToInt(Mathf.Max(0f, lightningHit));
        magic = Mathf.RoundToInt(Mathf.Max(0f, untypedMagicHit));
    }

    private static void ComputeAverageAbilityHitSplit(
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM,
        out float physHit,
        out float magHit,
        out float corrHit,
        float liveDamageMultiplier = 1f)
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
        float apM = stats.GetAbilityPowerDamageMultiplier();
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
        float dmgMult = Mathf.Max(0f, liveDamageMultiplier);

        float physLine = avgPhys * wEff + ailmentBonus;
        float magLine = avgMag * wEff * elemM + elementBonus * elemM;
        float corrLine = avgCorr * wEff;

        physHit = physLine * allM * apM * dmgMult;
        magHit = magLine * allM * apM * dmgMult;
        corrHit = corrLine * allM * apM * dmgMult;
    }

    private static float ComputeAverageCrusaderStrikeWeaponHit(CharacterStats stats, float weaponMult)
        => ComputeAverageCrusaderStrikeWeaponHit(stats, weaponMult, 1f);

    private static float ComputeAveragePhysicalOnlyAbilityHit(
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM,
        float liveDamageMultiplier = 1f)
    {
        if (!def || !stats)
            return 0f;

        float avgPhys =
            (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        float apM = stats.GetAbilityPowerDamageMultiplier();
        return Mathf.Max(0f, avgPhys * Mathf.Max(0f, weaponMult) * Mathf.Max(0f, allM) * apM * Mathf.Max(0f, liveDamageMultiplier));
    }

    private static float ComputeAverageCrusaderStrikeWeaponHit(CharacterStats stats, float weaponMult, float extraScale)
    {
        if (!stats)
            return 0f;

        if (!stats.CurrentMeleeWeaponHasPhysicalOrFireDamage())
            return 0f;

        float avgUsableWeaponHit = stats.GetMeleeAverageWeaponPhysicalOrFireDamagePerHit();
        return Mathf.Max(0f, avgUsableWeaponHit * Mathf.Max(0f, weaponMult) * Mathf.Max(0f, extraScale));
    }

    private static void ComputeAverageCrusaderStrikeCastSplit(
        CharacterStats stats,
        float weaponMult,
        float extraScale,
        bool finalStrike,
        out float physHit,
        out float fireHit)
    {
        physHit = 0f;
        fireHit = 0f;
        if (!stats || !stats.CurrentMeleeWeaponHasPhysicalOrFireDamage())
            return;

        float wMult = Mathf.Max(0f, weaponMult) * Mathf.Max(0f, extraScale);
        float avgPhys = stats.GetAverageWeaponPhysicalDamagePerHit();
        float avgFire = stats.GetAverageWeaponFireDamagePerHit();

        if (finalStrike)
        {
            fireHit = (avgPhys + avgFire) * wMult;
            return;
        }

        physHit = avgPhys * wMult;
        fireHit = avgFire * wMult;
    }

    private static string FormatCrusaderStrikeDamageLabel(float physHit, float fireHit)
    {
        int p = Mathf.RoundToInt(Mathf.Max(0f, physHit));
        int f = Mathf.RoundToInt(Mathf.Max(0f, fireHit));

        if (p > 0 && f > 0)
            return $"{p} Physical + {f} Fire damage on hit";
        if (f > 0)
            return $"{f} Fire damage on hit";
        if (p > 0)
            return $"{p} Physical damage on hit";
        return "0 damage on hit";
    }

    private static float GetCrusaderStrikeFinalFireTooltipScale(
        AbilityDefinition def,
        CharacterStats stats,
        int selectedChoice)
    {
        if (def == null || stats == null)
            return 1f;

        float firePortionScale = Mathf.Max(0f, def.fireDamageMultiplier);
        float fireSkillBonus = Mathf.Max(0f, stats.ElementSkillDamageScalingFractionFor(MagicAttackType.Fire));
        float scale = firePortionScale > 0f
            ? 1f + fireSkillBonus * firePortionScale
            : 1f;

        if (selectedChoice == PlayerAbilityController.CrusaderStrikeFireBalanceChoiceIndex)
            scale *= AbilityCombatPower.CrusaderStrikeFireBalanceFinalStrikeFireMultiplier;

        return scale;
    }

    private static int GetCrusaderStrikeSelectedChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.CrusaderStrikeEnhancementParentSpineNodeId,
            -1);
    }

    private static float GetCrusaderStrikeHealFraction(int selectedChoice) =>
        selectedChoice == PlayerAbilityController.CrusaderStrikeSacredRestorationChoiceIndex
            ? AbilityCombatPower.CrusaderStrikeSacredRestorationHealFractionOfMaxHealth
            : AbilityCombatPower.CrusaderStrikeHealFractionOfMaxHealth;

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
