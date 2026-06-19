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
    public const string TooltipScalingAccentColorPlain = "#B0C8DD";

    /// <summary>Wraps a details-panel scaling line in the same blue accent used for ability scaling.</summary>
    public static string WrapDetailsScalingAccentLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return string.Empty;

        return $"<color={TooltipScalingAccentColorPlain}>{line.Trim()}</color>";
    }

    /// <summary>Inherit-mode minions: no per-type damage numbers — one global rule (orange, like Power Slash primary effects).</summary>
    private const string InheritMinionDamageRuleLine =
        "This minion inherits a portion of the damage from your total damage";

    private const int SoulforgedWeaponChoiceSourceLevel = AbilityCombatPower.SoulforgedWeaponEnhancementSourceLevel;
    private const int SoulforgedWeaponSwarmChoiceIndex = AbilityCombatPower.SoulforgedWeaponSwarmChoiceIndex;
    private const int SoulforgedWeaponExtendedDurationChoiceIndex = AbilityCombatPower.SoulforgedWeaponExtendedDurationChoiceIndex;
    private const int SoulforgedWeaponSwarmCount = AbilityCombatPower.SoulforgedWeaponSwarmCount;
    private const float SoulforgedWeaponSwarmDamageMultiplier = AbilityCombatPower.SoulforgedWeaponSwarmDamageMultiplier;
    private const float SoulforgedWeaponSwarmDurationSeconds = AbilityCombatPower.SoulforgedWeaponSwarmDurationSeconds;
    private const float SoulforgedWeaponExtendedDurationSeconds = AbilityCombatPower.SoulforgedWeaponExtendedDurationSeconds;

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
    public static string BuildAbilityTooltipEffectsSection(
        AbilityDefinition def,
        SkillsManager skillsManager,
        int enhancementChoiceOverride = -1)
    {
        return BuildCompactEffectsBody(
            def, skillsManager, includeDuration: true, displayStacks: 0, includeEnhancementEffects: true,
            enhancementChoiceOverride);
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

        if (IsSoulforgedWeapon(def) || IsSoulforgedWarrior(def))
            return "Minion (Inherited)";
        if (IsHawkCompanion(def))
            return "Minion (Pure)";

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
            CharacterStats s = player.GetComponent<CharacterStats>();
            if (s != null && !IsMinionCharacterStats(s))
                return s;

            CharacterStats[] children = player.GetComponentsInChildren<CharacterStats>(true);
            for (int i = 0; i < children.Length; i++)
            {
                CharacterStats candidate = children[i];
                if (!candidate || IsMinionCharacterStats(candidate))
                    continue;
                return candidate;
            }
        }

        CharacterStats[] all = UnityEngine.Object.FindObjectsByType<CharacterStats>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            CharacterStats candidate = all[i];
            if (!candidate || IsMinionCharacterStats(candidate))
                continue;
            if (candidate.GetComponent<PlayerController>() != null ||
                candidate.GetComponentInParent<PlayerController>() != null)
                return candidate;
        }

        for (int i = 0; i < all.Length; i++)
        {
            CharacterStats candidate = all[i];
            if (!candidate || IsMinionCharacterStats(candidate))
                continue;
            return candidate;
        }

        return null;
    }

    private static bool IsMinionCharacterStats(CharacterStats stats) =>
        stats.GetComponent<MinionCombatTarget>() != null ||
        stats.GetComponentInParent<MinionCombatTarget>() != null;

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

    private static bool IsTripleShot(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.TripleShotAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsStaticArrows(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.StaticArrowsAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsSnipe(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.SnipeAbilityId, System.StringComparison.OrdinalIgnoreCase);

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
        if (IsCleavingChop(def) || IsSpectralAxe(def) || IsPowerSlash(def) || IsTripleShot(def) || IsStaticArrows(def) || IsSnipe(def))
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

    private static bool IsWarBanner(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.WarBannerAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsLightningRod(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.LightningRodAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsHuntersSwiftness(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.HuntersSwiftnessAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsTornado(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.TornadoAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsHammerTempest(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.HammerTempestAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsFlameCharge(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.FlameChargeAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsSoulforgedWeapon(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.SoulforgedWeaponAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsSoulforgedWarrior(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.SoulforgedWarriorAbilityId, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsHawkCompanion(AbilityDefinition def) =>
        def && string.Equals(def.abilityId, AbilityCombatPower.HawkCompanionAbilityId, System.StringComparison.OrdinalIgnoreCase);

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
        if (c == 1)
            return AbilityCombatPower.CleavingStrikesLastingMomentumDurationSeconds;
        return AbilityCombatPower.CleavingStrikesBaseDurationSeconds;
    }

    private static void AppendCleavingStrikesEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager,
        bool includeEnhancementEffects = true)
    {
        int sel = includeEnhancementEffects ? GetMeleeLv15BranchChoice(skillsManager, 1) : -1;
        int extraTargets = AbilityCombatPower.CleavingStrikesBaseExtraTargets;
        int empoweredHits = AbilityCombatPower.CleavingStrikesBaseEmpoweredHits;
        float durationSeconds = AbilityCombatPower.CleavingStrikesBaseDurationSeconds;
        if (sel == 0)
            extraTargets += AbilityCombatPower.CleavingStrikesGreaterCleaveBonusTargets;
        else if (sel == 1)
        {
            empoweredHits = AbilityCombatPower.CleavingStrikesLastingMomentumEmpoweredHits;
            durationSeconds = AbilityCombatPower.CleavingStrikesLastingMomentumDurationSeconds;
        }

        AppendDetailsEffectParagraph(body, O($"+{extraTargets} nearby enemies per strike"));
        AppendDetailsEffectParagraph(body, O("40% reduced damage on cleaved hits."));
        AppendDetailsEffectParagraph(body, O(
            $"Duration {empoweredHits} hits or {durationSeconds:0.#}s if hits are used up first."));
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
        else if (sel == SoulforgedWeaponExtendedDurationChoiceIndex)
            body.AppendLine(O($"Duration: {SoulforgedWeaponExtendedDurationSeconds:0.#}s"));
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

    private static int GetWarBannerBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee, AbilityCombatPower.WarBannerEnhancementParentSpineNodeId, -1);
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
        float strikesCodeBase = AbilityCombatPower.CleavingStrikesBaseDurationSeconds;
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

            if (IsHawkCompanion(def))
                AppendHawkCompanionRangedLevelScalingLine(scaling, S, skillsManager);

            return scaling.ToString().TrimEnd();
        }

        if (IsFlameCharge(def))
        {
            if (stats != null)
            {
                float firePct = stats.FireSkillDamageTotalScalingPercentPoints;
                if (firePct > 0.05f)
                    scaling.AppendLine(S($"+{firePct:0.#}% scaling from {OffenseBonusDisplayNames.FireDamagePercent}"));
                else
                    scaling.AppendLine(S($"Scales with {OffenseBonusDisplayNames.FireDamagePercent}"));
            }
            else
                scaling.AppendLine(S($"Scales with {OffenseBonusDisplayNames.FireDamagePercent}"));

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
            if (stats != null)
            {
                float apBonusPct = Mathf.Max(0f, (stats.GetAbilityPowerDamageMultiplier() - 1f) * 100f);
                if (apBonusPct > 0.05f)
                    scaling.AppendLine(S($"+{apBonusPct:0.#}% damage from Ability Power"));
            }
            return scaling.ToString().TrimEnd();
        }

        if (IsStaticArrows(def))
        {
            float effectiveWeaponMult = def.weaponDamageMultiplier > 0f
                ? def.weaponDamageMultiplier
                : AbilityCombatPower.StaticArrowsWeaponDamageMultiplier;
            int selected = GetStaticArrowsSelectedChoice(skillsManager);
            if (selected == AbilityCombatPower.StaticArrowsFullyChargedChoiceIndex)
                effectiveWeaponMult += AbilityCombatPower.StaticArrowsFullyChargedDamageBonus;

            scaling.AppendLine(S(
                $"{effectiveWeaponMult * 100f:0.#}% of your weapon physical or Lightning damage"));
            if (stats != null)
            {
                float apBonusPct = Mathf.Max(0f, (stats.GetAbilityPowerDamageMultiplier() - 1f) * 100f);
                if (apBonusPct > 0.05f)
                    scaling.AppendLine(S($"+{apBonusPct:0.#}% damage from Ability Power"));
            }
            return scaling.ToString().TrimEnd();
        }

        if (IsLightningRod(def))
        {
            AppendLightningRodRangedLevelScalingLine(scaling, S);
            if (stats != null)
            {
                scaling.AppendLine(S(
                    $"+{stats.LightningSkillDamageTotalScalingPercentPoints:0.#}% lightning damage"));
                AppendAbilityPowerScalingLine(scaling, S, stats);
            }
            return scaling.ToString().TrimEnd();
        }

        if (IsTornado(def))
        {
            AppendTornadoTooltipScaling(scaling, S, stats, skillsManager);
            AppendAbilityPowerScalingLine(scaling, S, stats);
            return scaling.ToString().TrimEnd();
        }

        if (IsWhirlwind(def))
        {
            scaling.AppendLine(S($"Deals {weaponMult * 100f:0.#}% of your weapon damage"));
            scaling.AppendLine(S("Attack speed determines hit frequency"));
            AppendAbilityTooltipBonusScalerLines(
                scaling, S, def, stats, weaponMult, def.GetEffectiveAllDamageMultiplier());
            return scaling.ToString().TrimEnd();
        }

        if (IsSnipe(def))
        {
            scaling.AppendLine(S(
                $"Deals {AbilityCombatPower.SnipeMinChargeDamageMultiplier * 100f:0.#}%-{AbilityCombatPower.SnipeMaxChargeDamageMultiplier * 100f:0.#}% of your weapon damage"));
            AppendAbilityTooltipBonusScalerLines(
                scaling, S, def, stats, weaponMult, def.GetEffectiveAllDamageMultiplier());
            return scaling.ToString().TrimEnd();
        }

        if (IsCleavingStrikes(def) || weaponMult <= scalingEpsilon)
            return string.Empty;

        float allDamageMult = def.GetEffectiveAllDamageMultiplier();
        scaling.AppendLine(S($"Deals {weaponMult * 100f:0.#}% of your weapon damage"));
        if (IsHammerTempest(def) && stats != null)
        {
            int maxBonusPct = Mathf.RoundToInt(AbilityCombatPower.HammerTempestWeaponSpeedMaxHitDamageBonusFraction * 100f);
            int speedBonusPct = AbilityCombatPower.GetHammerTempestWeaponSpeedHitDamageBonusPercent(stats.AttacksPerSecond);
            if (speedBonusPct > 0)
            {
                scaling.AppendLine(S(
                    $"+{speedBonusPct}% hit damage with your weapon ({stats.AttacksPerSecond:0.##} atk/s, max +{maxBonusPct}%)"));
            }
        }

        AppendAbilityTooltipBonusScalerLines(scaling, S, def, stats, weaponMult, allDamageMult);
        return scaling.ToString().TrimEnd();
    }

    private static void AppendAbilityPowerScalingLine(
        StringBuilder scaling,
        System.Func<string, string> S,
        CharacterStats stats)
    {
        if (scaling == null || stats == null)
            return;

        float apBonusPct = Mathf.Max(0f, (stats.GetAbilityPowerDamageMultiplier() - 1f) * 100f);
        if (apBonusPct > 0.05f)
            scaling.AppendLine(S($"+{apBonusPct:0.#}% damage from Ability Power"));
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

        float apBonusPct = Mathf.Max(0f, (apM - 1f) * 100f);
        if (apBonusPct > 0.05f)
            scaling.AppendLine(S($"+{apBonusPct:0.#}% damage from Ability Power"));

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
        bool orangeMarkup,
        bool includeEnhancementEffects = true,
        int enhancementChoiceOverride = -1)
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
            int enhance = includeEnhancementEffects ? GetFlameChargeBranchChoice(skillsManager) : -1;
            body.AppendLine(O("Dash forward leaving a trail of fire on the ground."));
            body.AppendLine(O(
                $"Trail: {AbilityCombatPower.FlameChargeTrailTotalFlatFireDamage:0.#} fire damage over {AbilityCombatPower.FlameChargeTrailDurationSeconds:0.#}s (trails don't overlap)."));
            if (includeEnhancementEffects)
            {
                if (enhance == 0)
                    body.AppendLine(O("2 charges."));
                else if (enhance == 1)
                    body.AppendLine(O(
                        $"{AbilityCombatPower.FlameChargeVolcanicExplosionFlatFireDamage:0.#} Fire explosion at dash end ({AbilityCombatPower.FlameChargeVolcanicExplosionRadius:0.#} radius). Applies Burn."));
            }
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"Duration: {AbilityCombatPower.FlameChargeTrailDurationSeconds:0.#}s (trail)"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsEnergyInfusion(def))
        {
            body.AppendLine(O(
                $"{AbilityCombatPower.EnergyInfusionBaseManaCostFraction * 100f:0.#}% of energy cost replaced as mana"));
            if (includeEnhancementEffects)
            {
                int enhance = GetEnergyInfusionBranchChoice(skillsManager);
                if (enhance == 0)
                {
                    body.AppendLine(O(
                        $"+{AbilityCombatPower.EnergyInfusionEfficientConversionAdditionalManaCostFraction * 100f:0.#}% additional Mana instead of Energy."));
                    body.AppendLine(O(
                        $"+{AbilityCombatPower.EnergyInfusionEfficientConversionFlatManaRegenPerSecond:0.#} Mana per second while active."));
                }
                else if (enhance == 1)
                    body.AppendLine(O(
                        $"Uses {AbilityCombatPower.EnergyInfusionOverchargedManaCostFraction * 100f:0.#}% Mana instead of {AbilityCombatPower.EnergyInfusionBaseManaCostFraction * 100f:0.#}%, and if Mana is used that ability gains +{AbilityCombatPower.EnergyInfusionOverchargedAbilityPowerPercentBonus:0.#}% ability power."));
            }
            body.AppendLine(string.Empty);
            body.AppendLine(O("Duration: Toggle"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsWarBanner(def))
        {
            if (includeEnhancementEffects)
                AppendWarBannerTooltipEffects(body, O, skillsManager);
            else
                AppendWarBannerBaseTooltipEffects(body, O);
            float dur = GetWarBannerTooltipDurationSeconds(def, skillsManager);
            body.AppendLine(string.Empty);
            body.AppendLine(O($"Duration: {dur:0.#}s"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsLightningRod(def))
        {
            AppendLightningRodTooltipEffects(body, O, skillsManager, stats, includeEnhancementEffects);
            AppendDetailsEffectParagraph(body, O($"Duration: {AbilityCombatPower.LightningRodBaseDurationSeconds:0.#}s"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsHuntersSwiftness(def))
        {
            AppendHuntersSwiftnessTooltipEffects(body, O, skillsManager, includeEnhancementEffects);
            AppendDetailsEffectParagraph(body, O($"Duration: {AbilityCombatPower.HuntersSwiftnessBaseDurationSeconds:0.#}s"));
            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsTornado(def))
        {
            AppendTornadoTooltipEffects(body, O, skillsManager, stats, includeEnhancementEffects);
            AppendDetailsEffectParagraph(body, O($"Duration: {AbilityCombatPower.TornadoBaseDurationSeconds:0.#}s"));
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
            if (IsHawkCompanion(def))
            {
                if (stats != null)
                    AppendHawkCompanionTooltipEffectLines(body, O, def, stats, skillsManager, includeEnhancementEffects);
                else
                    AppendHawkCompanionTooltipEffectLinesNoStats(body, O);

                AppendHawkCompanionDurationLine(body, O, def);
            }
            else
            {
                if (stats != null)
                    AppendMinionSpawnTooltipEffectLines(body, O, def, stats, skillsManager);
                else
                    AppendMinionSpawnTooltipEffectLinesNoStats(body, O, def);
            }

            if (IsSoulforgedWeapon(def))
            {
                AppendSoulforgedEnhancementEffectLines(body, O, def, skillsManager);
                AppendSoulforgedWeaponDurationLine(body, O, def, skillsManager);
            }

            if (IsSoulforgedWarrior(def))
                AppendSoulforgedWarriorEffectLines(body, O, def, skillsManager);

            body.AppendLine(string.Empty);
            AppendTooltipEnergyCooldownFooter(body, O, def, skillsManager, stats, abilityController);
            return body.ToString().TrimEnd();
        }

        if (IsRend(def))
        {
            body.AppendLine(O("100% bleed on next hit if physical damage is dealt"));
            body.AppendLine(O("+3s duration"));
            int rendChoice = GetMeleeSkillRow5Choice(skillsManager);
            if (rendChoice == 0)
                body.AppendLine(O("Deals full damage in half duration"));
            else if (rendChoice == 1)
                body.AppendLine(O("Bleed spreads"));
        }
        else if (IsEnvenom(def))
        {
            body.AppendLine(O("100% poison on next hit if corruption damage is dealt"));
            int envenomChoice = GetMeleeSkillRow5Choice(skillsManager);
            if (envenomChoice == 0)
            {
                int stackCount = ResolveEnvenomTooltipPoisonStacks(stats, skillsManager);
                body.AppendLine(O($"Applies {stackCount} stacks of poison"));
            }
            else if (envenomChoice == 1)
                body.AppendLine(O("Poison spreads"));
            else
            {
                int stackCount = stats != null ? stats.PoisonMaxStacks : 3;
                body.AppendLine(O($"Applies {stackCount} stacks of poison"));
            }
        }
        else if (IsCleavingStrikes(def))
        {
            AppendCleavingStrikesEffectLines(body, O, skillsManager, includeEnhancementEffects);
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
            body.AppendLine(O($"Range: {AbilityCombatPower.CrescentSlashReach:0.#}"));
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

            if (includeEnhancementEffects && fsEnhance == 0)
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
                $"Dashes behind the closest enemy ({AbilityCombatPower.ShadowStrikeForwardReach:0.#} units)."));

            if (includeEnhancementEffects)
            {
                if (enhance == 0)
                    body.AppendLine(O(
                        $"Marks the target — the next critical hit deals +{AbilityCombatPower.ShadowStrikeLethalCritBonusFraction * 100f:0.#}% critical damage, then the mark expires."));
                else if (enhance == 1)
                    body.AppendLine(O(
                        $"Marks the target on hit — when they die, Shadow Strike cooldown is reduced by {AbilityCombatPower.ShadowStrikeExecutionCooldownRefundSeconds:0.#}s."));
            }
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
                $"{channelSeconds:0.#}s channel — {strikeCount} strikes at {AbilityCombatPower.BladestormAttackSpeedMultiplier * 100f:0.#}% attack speed"));
            body.AppendLine(O($"Each strike: {perStrikeTotal} total damage{dmgSuffix} (50% weapon damage)"));
            body.AppendLine(O("Locks onto a single enemy in front of you for the duration."));
            body.AppendLine(O(
                $"Take {(1f - AbilityCombatPower.BladestormChannelDamageTakenMultiplier) * 100f:0.#}% reduced damage while channeling."));

            if (includeEnhancementEffects)
            {
                if (enhance == 0)
                {
                    ComputeAverageAbilityHitSplit(def, stats, finaleMult, allM, out float finPhys, out float finMag, out float finCorr, liveDamageMultiplier);
                    int finaleTotal = Mathf.RoundToInt(finPhys + finMag + finCorr);
                    body.AppendLine(O(
                        $"Additional strike after the combo — {finaleTotal} total damage{dmgSuffix} (150% weapon damage)"));
                }
                else if (enhance == 1)
                    body.AppendLine(O(
                        "If the target dies during the combo, remaining strikes hit the nearest enemy in range (auto-paths while channeling)."));
            }
        }
        else if (IsExecutionersDescent(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            int enhance = GetExecutionersDescentBranchChoice(skillsManager);

            if (includeEnhancementEffects && enhance == 2)
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
                    $"Axe anchors at impact height for {AbilityCombatPower.ExecutionersDescentContinuumDurationSeconds:0.#}s — no crash descent"));
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
                    $"{shockTotal} damage to enemies within {AbilityCombatPower.ExecutionersDescentShockwaveRadius:0.#} units of the target"));
                body.AppendLine(O(
                    $"{AbilityCombatPower.ExecutionersDescentDescentSeconds:0.#}s — locks onto a target, then impacts at their position"));

                if (includeEnhancementEffects)
                {
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
        }
        else if (IsWhirlwind(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);

            int wwEnhance = includeEnhancementEffects ? GetMeleeLv15BranchChoice(skillsManager, 0) : -1;
            float movePenalty = AbilityCombatPower.WhirlwindBaseMoveSpeedPenaltyFraction;
            body.AppendLine(O($"Move speed is reduced by {movePenalty * 100f:0.#}% while channelling."));

            if (includeEnhancementEffects && wwEnhance == 0)
            {
                ComputeAverageAbilityHitSplit(
                    def,
                    stats,
                    weaponMult,
                    allM,
                    out float twPhys,
                    out float twMag,
                    out float twCorr,
                    liveDamageMultiplier * AbilityCombatPower.WhirlwindGaleforceTwisterDamageMultiplier);
                int twisterTotal = Mathf.RoundToInt(twPhys + twMag + twCorr);
                body.AppendLine(O($"Twisters deal {twisterTotal} damage on hit"));
            }

            body.AppendLine(O(
                $"(During auto battle, whirlwind only starts when energy is above {AbilityCombatPower.WhirlwindAutoBattleMinEnergyFraction * 100f:0.#}%)"));

            if (includeEnhancementEffects && wwEnhance == 1)
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
            body.AppendLine(O($"{AbilityCombatPower.GuardiansHammerForwardReach:0.#} range."));

            if (includeEnhancementEffects)
            {
                int hammerEnhance = GetMeleeLv15BranchChoice(skillsManager, 3);
                if (hammerEnhance == 0 && stats != null)
                {
                    float perHitFraction =
                        AbilityCombatPower.GuardiansHammerProtectorResolveGuardPerHitFractionMaxHealth;
                    float perHitGuard = stats.MaxHP * perHitFraction;
                    int maxHits = AbilityCombatPower.GuardiansHammerProtectorResolveMaxEnemyHits;
                    body.AppendLine(O(
                        $"Gain {Mathf.RoundToInt(perHitGuard)} Guard ({perHitFraction * 100f:0.#}% max health) per enemy hit, up to {maxHits} ({AbilityCombatPower.GuardiansHammerProtectorResolveGuardDurationSeconds:0.#} seconds)."));
                    body.AppendLine(O(
                        $"Stun enemies on hit for {AbilityCombatPower.GuardiansHammerProtectorResolveStunDurationSeconds:0.#}s."));
                }
                else if (hammerEnhance == 1)
                {
                    body.AppendLine(O(
                        $"Hitting burning enemies causes burns to flare up, exploding in {AbilityCombatPower.GuardiansHammerBurningVerdictExplosionRadius:0.#} range for {AbilityCombatPower.GuardiansHammerBurningVerdictTicksWorth}x their current burn tick damage. This does not remove the burn."));
                }
            }
        }
        else if (IsPowerSlash(def))
        {
            AppendPowerSlashTooltipHitDamage(body, O, def, stats, weaponMult, allM, liveDamageMultiplier);
        }
        else if (IsTripleShot(def))
        {
            AppendTripleShotTooltipHitDamage(body, O, def, stats, weaponMult, allM, liveDamageMultiplier);
        }
        else if (IsStaticArrows(def))
        {
            AppendStaticArrowsTooltipHitDamage(body, O, def, stats, skillsManager, weaponMult, allM, liveDamageMultiplier, includeEnhancementEffects);
        }
        else if (IsSnipe(def))
        {
            AppendSnipeTooltipHitDamage(
                body, O, def, stats, skillsManager, weaponMult, allM, liveDamageMultiplier,
                includeEnhancementEffects, enhancementChoiceOverride);
        }
        else if (IsHammerTempest(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
            float hammerHitMult = stats != null
                ? AbilityCombatPower.GetHammerTempestWeaponSpeedHitDamageMultiplier(stats.AttacksPerSecond)
                : 1f;
            int hammerEnhance = GetHammerTempestBranchChoice(skillsManager);
            if (hammerEnhance == AbilityCombatPower.HammerTempestSacredArsenalChoiceIndex)
                hammerHitMult *= AbilityCombatPower.HammerTempestSacredArsenalDamageMultiplier;

            physHit *= hammerHitMult;
            magHit *= hammerHitMult;
            corrHit *= hammerHitMult;

            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);
        }
        else if (UsesCombinedTotalHitDamageTooltip(def))
        {
            string dmgSuffix = DamageTimingSuffix();
            ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
            AppendAbilityTotalHitDamageEffects(body, O, physHit, magHit, corrHit, dmgSuffix, stats);
        }

        if (IsHammerTempest(def))
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

        if (string.Equals(buffId, AbilityCombatPower.WarBannerAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            AbilityDefinition warBannerDef = abilityDatabase.Get(buffId);
            if (warBannerDef == null)
                return false;

            title = SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(warBannerDef);
            if (string.IsNullOrWhiteSpace(title))
                title = buffId;

            body = CombineShortDescriptionWithBody(
                warBannerDef,
                BuildWarBannerHudActiveEffectsBody(displayStacks, skillsManager));
            return !string.IsNullOrWhiteSpace(body);
        }

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
               IsSoulforgedWeapon(def) || IsSoulforgedWarrior(def) || IsHawkCompanion(def);
    }

    private static string BuildCompactEffectsBody(
        AbilityDefinition def,
        SkillsManager skillsManager,
        bool includeDuration,
        int displayStacks,
        bool includeEnhancementEffects = true,
        int enhancementChoiceOverride = -1)
    {
        if (def == null)
            return string.Empty;

        if (IsWarBanner(def))
            return BuildWarBannerCompactEffectsBody(def, skillsManager, includeDuration, displayStacks, includeEnhancementEffects);

        if (IsLightningRod(def))
            return BuildLightningRodCompactEffectsBody(def, skillsManager, includeDuration, includeEnhancementEffects);

        if (IsHuntersSwiftness(def))
            return BuildHuntersSwiftnessCompactEffectsBody(def, skillsManager, includeDuration, includeEnhancementEffects);

        if (IsTornado(def))
            return BuildTornadoCompactEffectsBody(def, skillsManager, includeDuration, includeEnhancementEffects);

        CharacterStats stats = FindLocalPlayerStats();
        string full = BuildAbilityTooltipStatsSection(
            def, stats, skillsManager, orangeMarkup: false, includeEnhancementEffects, enhancementChoiceOverride);
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

        var result = new StringBuilder();
        bool pendingParagraphGap = false;

        foreach (string raw in statsSection.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                if (result.Length > 0)
                    pendingParagraphGap = true;
                continue;
            }

            if (string.Equals(StripRichText(line), "Effects:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.Contains(" Cooldown", StringComparison.Ordinal) && line.Contains("•", StringComparison.Ordinal))
                break;

            if (!includeDuration && StripRichText(line).StartsWith("Duration:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (pendingParagraphGap)
            {
                result.Append(DetailsEffectParagraphGap);
                pendingParagraphGap = false;
            }
            else if (result.Length > 0)
            {
                result.Append('\n');
            }

            result.Append(line);
        }

        return result.Length == 0 ? string.Empty : result.ToString();
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

    private static float GetWarBannerTooltipDurationSeconds(AbilityDefinition def, SkillsManager skillsManager)
    {
        float dur = GetTooltipBuffMinionDisplayDurationSeconds(def, AbilityCombatPower.WarBannerBaseDurationSeconds, 0f);
        if (GetWarBannerBranchChoice(skillsManager) == 2)
        {
            dur += AbilityCombatPower.WarBannerEnh3MaxKillProcs
                   * AbilityCombatPower.WarBannerEnh3KillDurationExtensionSeconds;
        }

        return dur;
    }

    private static float GetWarBannerTotalStatPercent(int stacks)
    {
        return (AbilityCombatPower.WarBannerBaseAttackSpeedBonus
                + stacks * AbilityCombatPower.WarBannerStackBonusPerStat) * 100f;
    }

    private static void AppendWarBannerBaseStatLines(StringBuilder body, System.Func<string, string> O, int stacks)
    {
        float totalPct = stacks > 0
            ? GetWarBannerTotalStatPercent(stacks)
            : AbilityCombatPower.WarBannerBaseAttackSpeedBonus * 100f;

        body.AppendLine(O($"+{totalPct:0.#}% attack speed"));
        body.AppendLine(O($"+{totalPct:0.#}% damage reduction"));
        body.AppendLine(O($"+{totalPct:0.#}% global physical damage"));
    }

    private static string BuildWarBannerHudActiveEffectsBody(int displayStacks, SkillsManager skillsManager)
    {
        var body = new StringBuilder();
        System.Func<string, string> O = s => s;
        AppendWarBannerBaseStatLines(body, O, displayStacks);

        int enhance = GetWarBannerBranchChoice(skillsManager);
        if (enhance == 0)
        {
            body.AppendLine(string.Empty);
            body.AppendLine(O($"+{AbilityCombatPower.WarBannerEnh1CooldownReduction * 100f:0.#}% cooldown reduction"));
        }
        else if (enhance == 1)
        {
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"{AbilityCombatPower.WarBannerEnh2MaxStacksHealPerSecondFraction * 100f:0.#}% HP regen per second after {AbilityCombatPower.WarBannerBaseMaxStacks} stacks."));
        }

        return body.ToString().TrimEnd();
    }

    private static string BuildWarBannerCompactEffectsBody(
        AbilityDefinition def,
        SkillsManager skillsManager,
        bool includeDuration,
        int displayStacks,
        bool includeEnhancementEffects)
    {
        var body = new StringBuilder();
        System.Func<string, string> O = s => s;

        if (displayStacks > 0)
        {
            AppendWarBannerBaseStatLines(body, O, displayStacks);
        }
        else
        {
            AppendWarBannerSkillTreeEffectLines(body, O, skillsManager, includeEnhancementEffects);
        }

        if (includeDuration)
        {
            body.AppendLine(string.Empty);
            body.AppendLine(O($"Duration: {GetWarBannerTooltipDurationSeconds(def, skillsManager):0.#}s"));
        }

        return body.ToString().TrimEnd();
    }

    private static void AppendWarBannerSkillTreeEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager,
        bool includeEnhancementEffects)
    {
        float basePct = AbilityCombatPower.WarBannerBaseAttackSpeedBonus * 100f;
        body.AppendLine(O($"+{basePct:0.#}% attack speed"));
        body.AppendLine(O($"+{AbilityCombatPower.WarBannerBaseDamageReductionFraction * 100f:0.#}% damage reduction"));
        body.AppendLine(O($"+{AbilityCombatPower.WarBannerBaseGlobalPhysicalDamageBonus * 100f:0.#}% global physical damage"));
        body.AppendLine(string.Empty);
        body.AppendLine(O(
            $"Every {AbilityCombatPower.WarBannerStackIntervalSeconds:0.#}s gain a stack (max {AbilityCombatPower.WarBannerBaseMaxStacks} stacks)."));

        if (!includeEnhancementEffects)
            return;

        int enhance = GetWarBannerBranchChoice(skillsManager);
        if (enhance == 0)
        {
            body.AppendLine(string.Empty);
            body.AppendLine(O($"+{AbilityCombatPower.WarBannerEnh1CooldownReduction * 100f:0.#}% cooldown reduction"));
        }
        else if (enhance == 1)
        {
            body.AppendLine(string.Empty);
            body.AppendLine(O(
                $"At {AbilityCombatPower.WarBannerBaseMaxStacks} stacks, all allies regenerate {AbilityCombatPower.WarBannerEnh2MaxStacksHealPerSecondFraction * 100f:0.#}% max health per second and gain {AbilityCombatPower.WarBannerEnh2MoveSpeedBonus * 100f:0.#}% movement speed while banner persists."));
        }
    }

    private static void AppendWarBannerTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        AppendWarBannerSkillTreeEffectLines(body, O, skillsManager, includeEnhancementEffects: true);
    }

    private static void AppendWarBannerBaseTooltipEffects(StringBuilder body, System.Func<string, string> O)
    {
        AppendWarBannerSkillTreeEffectLines(body, O, skillsManager: null, includeEnhancementEffects: false);
    }

    private static int GetLightningRodBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged,
            AbilityCombatPower.LightningRodEnhancementParentSpineNodeId,
            -1);
    }

    private static void AppendLightningRodRangedLevelScalingLine(
        StringBuilder body,
        System.Func<string, string> S)
    {
        body.AppendLine(S(
            $"+{AbilityCombatPower.LightningRodLightningPerTwoRangedLevels:0.#} lightning damage every 2 ranged levels"));
    }

    private static float GetLightningRodPeriodicArcIntervalForTooltip(
        SkillsManager skillsManager,
        bool includeEnhancementEffects)
    {
        if (includeEnhancementEffects
            && GetLightningRodBranchChoice(skillsManager) == AbilityCombatPower.LightningRodEnh1FasterArcsChoiceIndex)
        {
            return AbilityCombatPower.LightningRodEnh1PeriodicArcIntervalSeconds;
        }

        return AbilityCombatPower.LightningRodPeriodicArcIntervalSeconds;
    }

    private static void AppendLightningRodTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager,
        CharacterStats stats,
        bool includeEnhancementEffects)
    {
        int rangedLevel = skillsManager != null ? skillsManager.GetLevel(SkillType.Ranged) : 0;
        if (stats != null)
        {
            AbilityCombatPower.GetLightningRodArcDamageBounds(rangedLevel, stats, out float minD, out float maxD);
            AppendDetailsEffectParagraph(body, O(
                $"{Mathf.RoundToInt(minD)}–{Mathf.RoundToInt(maxD)} lightning damage on arc hits"));
        }
        else
        {
            AppendDetailsEffectParagraph(body, O(
                $"{AbilityCombatPower.LightningRodBaseMinLightningDamage:0.#}–{AbilityCombatPower.LightningRodBaseMaxLightningDamage:0.#} lightning damage on arc hits"));
        }

        float periodicInterval = GetLightningRodPeriodicArcIntervalForTooltip(skillsManager, includeEnhancementEffects);
        AppendDetailsEffectParagraph(body, O(
            $"Lightning arcs up to {AbilityCombatPower.LightningRodMaxPeriodicArcTargets} enemies every {periodicInterval:0.#}s (one burst on spawn)"));
        AppendDetailsEffectParagraph(body, O(
            $"Lightning damage dealt to enemies within {AbilityCombatPower.LightningRodArcRange:0.#} range can surge back through the rod (max every {AbilityCombatPower.LightningRodSurgeCooldownSeconds:0.#}s)"));

        if (!includeEnhancementEffects)
            return;

        int enhance = GetLightningRodBranchChoice(skillsManager);
        if (enhance == AbilityCombatPower.LightningRodEnh2ExpiryChainChoiceIndex)
        {
            AppendDetailsEffectParagraph(body, O(
                "On expiry, chains lightning to nearby enemies and applies shock"));
        }
    }

    private static string BuildLightningRodCompactEffectsBody(
        AbilityDefinition def,
        SkillsManager skillsManager,
        bool includeDuration,
        bool includeEnhancementEffects)
    {
        var body = new StringBuilder();
        System.Func<string, string> O = s => s;
        CharacterStats stats = FindLocalPlayerStats();
        AppendLightningRodTooltipEffects(body, O, skillsManager, stats, includeEnhancementEffects);
        if (includeDuration)
            AppendDetailsEffectParagraph(body, O($"Duration: {AbilityCombatPower.LightningRodBaseDurationSeconds:0.#}s"));
        return body.ToString().TrimEnd();
    }

    private static int GetHuntersSwiftnessBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged,
            AbilityCombatPower.HuntersSwiftnessEnhancementParentSpineNodeId,
            -1);
    }

    private static void AppendHuntersSwiftnessTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager,
        bool includeEnhancementEffects)
    {
        int enhance = includeEnhancementEffects ? GetHuntersSwiftnessBranchChoice(skillsManager) : -1;
        bool nimbleHunter = enhance == AbilityCombatPower.HuntersSwiftnessEnh2NimbleHunterChoiceIndex;

        float moveSpeed = (AbilityCombatPower.HuntersSwiftnessBaseMoveSpeedBonus
            + (nimbleHunter ? AbilityCombatPower.HuntersSwiftnessEnh2MoveSpeedBonus : 0f)) * 100f;
        float nearbyMoveSpeed = (AbilityCombatPower.HuntersSwiftnessNearbyEnemyMoveSpeedBonus
            + (nimbleHunter ? AbilityCombatPower.HuntersSwiftnessEnh2MoveSpeedBonus : 0f)) * 100f;
        float evade = (AbilityCombatPower.HuntersSwiftnessBaseEvadeChance
            + (nimbleHunter ? AbilityCombatPower.HuntersSwiftnessEnh2EvadeChanceBonus : 0f)) * 100f;

        var coreLines = new List<string>
        {
            O($"+{moveSpeed:0.#}% increased movement speed")
        };
        if (nimbleHunter)
        {
            coreLines.Add(O(
                $"+{nearbyMoveSpeed:0.#}% increased movement speed if an enemy is within {AbilityCombatPower.HuntersSwiftnessNimbleHunterNearbySideRange:0.#} range either side of you"));
        }
        else
        {
            coreLines.Add(O(
                $"+{nearbyMoveSpeed:0.#}% increased movement speed if an enemy is within {AbilityCombatPower.HuntersSwiftnessNearbyEnemyRange:0.#} range"));
        }

        coreLines.Add(O($"+{evade:0.#}% chance to evade"));
        coreLines.Add(O(AbilityCombatPower.HuntersSwiftnessEvadeTooltipNote));
        AppendEffectLineGroup(body, coreLines);

        if (!includeEnhancementEffects)
            return;

        if (enhance == AbilityCombatPower.HuntersSwiftnessEnh1HunterTrapsChoiceIndex)
        {
            AppendDetailsEffectParagraph(body, O(
                $"Every {AbilityCombatPower.HuntersSwiftnessTrapDropIntervalSeconds:0.#}s drop a trap below you. Enemies that step on a trap are stunned for {AbilityCombatPower.HuntersSwiftnessTrapStunDurationSeconds:0.#}s and take {AbilityCombatPower.HuntersSwiftnessTrapWeaponDamageFraction * 100f:0.#}% weapon damage. Traps expire after {AbilityCombatPower.HuntersSwiftnessTrapLifetimeSeconds:0.#}s; each enemy can only be affected once per cast."));
        }
    }

    private static string BuildHuntersSwiftnessCompactEffectsBody(
        AbilityDefinition def,
        SkillsManager skillsManager,
        bool includeDuration,
        bool includeEnhancementEffects)
    {
        var body = new StringBuilder();
        System.Func<string, string> O = s => s;
        AppendHuntersSwiftnessTooltipEffects(body, O, skillsManager, includeEnhancementEffects);
        if (includeDuration)
            AppendDetailsEffectParagraph(body, O($"Duration: {AbilityCombatPower.HuntersSwiftnessBaseDurationSeconds:0.#}s"));
        return body.ToString().TrimEnd();
    }

    private static int GetTornadoBranchChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged,
            AbilityCombatPower.TornadoEnhancementParentSpineNodeId,
            -1);
    }

    private static void AppendTornadoTooltipScaling(
        StringBuilder scaling,
        System.Func<string, string> S,
        CharacterStats stats,
        SkillsManager skillsManager)
    {
        if (scaling == null)
            return;

        int enhance = GetTornadoBranchChoice(skillsManager);
        if (enhance == AbilityCombatPower.TornadoEnh1LightningTornadoChoiceIndex)
        {
            scaling.AppendLine(S(
                "Lightning arcs absorbed by the tornado, add 40% of the hits damage per second"));
        }
        else if (enhance == AbilityCombatPower.TornadoEnh2BowInfusedChoiceIndex)
        {
            scaling.AppendLine(S("Adds 40% of your weapons physical damage"));
        }
    }

    private static void GetTornadoTickDamageBounds(
        CharacterStats stats,
        int enhanceChoice,
        out int tickMin,
        out int tickMax)
    {
        float apMult = stats != null ? stats.GetAbilityPowerDamageMultiplier() : 1f;
        float min = AbilityCombatPower.TornadoBaseMinDamagePerSecond * apMult;
        float max = AbilityCombatPower.TornadoBaseMaxDamagePerSecond * apMult;

        if (enhanceChoice == AbilityCombatPower.TornadoEnh2BowInfusedChoiceIndex && stats != null)
        {
            min += stats.MinSplitDamage.physical * AbilityCombatPower.TornadoEnh2BowDamageFraction * apMult;
            max += stats.MaxSplitDamage.physical * AbilityCombatPower.TornadoEnh2BowDamageFraction * apMult;
        }

        tickMin = Mathf.RoundToInt(min);
        tickMax = Mathf.RoundToInt(max);
    }

    private const string TornadoSeekEffectText =
        "Tornado seeks out closest enemies. Reactivating tornado will cause it to seek a new target.";

    private static void AppendTornadoTooltipEffects(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager,
        CharacterStats stats,
        bool includeEnhancementEffects)
    {
        int enhance = includeEnhancementEffects ? GetTornadoBranchChoice(skillsManager) : -1;
        GetTornadoTickDamageBounds(stats, enhance, out int tickMin, out int tickMax);
        AppendDetailsEffectParagraph(body, O($"{tickMin}–{tickMax} physical damage per second"));

        AppendDetailsEffectParagraph(body, O(TornadoSeekEffectText));

        if (!includeEnhancementEffects)
            return;

        if (enhance == AbilityCombatPower.TornadoEnh1LightningTornadoChoiceIndex)
        {
            AppendDetailsEffectParagraph(body, O(
                "Lightning arcs can be absorbed by the tornado. the arc then chains to a nearby enemy. " +
                "Bonus lightning damage is added per second for its duration and it grows in size hitting enemies in a wider radius."));
        }
    }

    private static string BuildTornadoCompactEffectsBody(
        AbilityDefinition def,
        SkillsManager skillsManager,
        bool includeDuration,
        bool includeEnhancementEffects)
    {
        var body = new StringBuilder();
        System.Func<string, string> O = s => s;
        CharacterStats stats = FindLocalPlayerStats();
        AppendTornadoTooltipEffects(body, O, skillsManager, stats, includeEnhancementEffects);
        if (includeDuration)
            AppendDetailsEffectParagraph(body, O($"Duration: {AbilityCombatPower.TornadoBaseDurationSeconds:0.#}s"));
        return body.ToString().TrimEnd();
    }

    /// <summary>Legacy hook — ability combat stats belong in the details panel Effect column.</summary>
    public static bool TryBuildSkillTreeAbilityEffectsAppendix(
        AbilityDefinition def,
        SkillsManager skillsManager,
        out string appendix)
    {
        appendix = null;
        return false;
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
        string dmgSuffix = DamageTimingSuffix();
        body.AppendLine(O($"+0 damage from Minion Damage{dmgSuffix}"));

        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit && !IsSoulforgedWarrior(def))
            body.AppendLine(O(InheritMinionDamageRuleLine));
    }

    private static void AppendMinionSpawnTooltipScalingLinesNoStats(
        StringBuilder body,
        System.Func<string, string> S,
        AbilityDefinition def)
    {
        AppendMinionOwnerScalingPercentLines(body, S, def, null);
    }

    private static void AppendMinionSpawnTooltipEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        SkillsManager skillsManager)
    {
        MinionCombatConfig cfg = def.minionSpawnDefinition.combatConfig;
        string dmgSuffix = DamageTimingSuffix();
        int mdFlat = ComputeTooltipMinionDamageFlatBonus(def, stats);
        body.AppendLine(O($"+{mdFlat} damage from Minion Damage{dmgSuffix}"));

        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit && !IsSoulforgedWarrior(def))
            body.AppendLine(O(InheritMinionDamageRuleLine));
    }

    private static void AppendMinionSpawnTooltipScalingLines(
        StringBuilder body,
        System.Func<string, string> S,
        AbilityDefinition def,
        CharacterStats stats)
    {
        AppendMinionOwnerScalingPercentLines(body, S, def, stats);
    }

    private static void AppendMinionOwnerScalingPercentLines(
        StringBuilder body,
        System.Func<string, string> S,
        AbilityDefinition def,
        CharacterStats stats)
    {
        MinionDefinition minionDef = def != null ? def.minionSpawnDefinition : null;
        MinionCombatConfig cfg = minionDef != null
            ? MinionCombatConfig.AfterDeserialize(minionDef.combatConfig)
            : default;

        float ownerBonusScale = cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit
            ? MinionRuntimeStatsCalculator.InheritMinionOwnerBonusScale
            : 1f;

        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            float inheritPct = Mathf.Max(0f, cfg.inheritDamageCoefficient) * 100f;
            body.AppendLine(S($"{inheritPct:0.#}% inherited weapon damage"));
        }

        float dmgPct = stats != null ? stats.FinalMinionDamagePercentPoints : 0f;
        float apsPct = stats != null ? stats.FinalMinionAttackSpeedPercentPoints : 0f;
        float critPct = stats != null ? stats.FinalMinionCritChancePercentPoints : 0f;
        float hpPct = stats != null ? stats.FinalMinionMaxLifePercentPoints : 0f;

        body.AppendLine(S(FormatMinionBoostStatLine(dmgPct, ownerBonusScale, "minion damage")));
        body.AppendLine(S(FormatMinionBoostStatLine(apsPct, ownerBonusScale, "minion attack speed")));
        body.AppendLine(S(FormatMinionBoostStatLine(critPct, ownerBonusScale, "minion crit chance")));
        if (MinionDefinitionHasHealth(minionDef))
            body.AppendLine(S(FormatMinionBoostStatLine(hpPct, ownerBonusScale, "minion max HP")));
    }

    private static bool MinionDefinitionHasHealth(MinionDefinition minionDef) =>
        minionDef && (minionDef.ownerMaxHealthFraction > 0f || minionDef.baseMaxHealth > 0f);

    private static string FormatMinionBoostStatLine(float ownerPercentPoints, float ownerBonusScale, string label)
    {
        if (Mathf.Approximately(ownerBonusScale, 1f))
            return $"{FormatSignedPercentPointsForTooltip(ownerPercentPoints)} {label}";

        float effective = ownerPercentPoints * ownerBonusScale;
        return
            $"{FormatSignedPercentPointsForTooltip(effective)} ({FormatSignedPercentPointsForTooltip(ownerPercentPoints)}) {label}";
    }

    /// <summary>Flat damage from owner Minion Damage % on one hit (matches runtime × pre-hit base; inherit uses half scaling).</summary>
    private static int ComputeTooltipMinionDamageFlatBonus(AbilityDefinition def, CharacterStats stats)
    {
        if (!def || def.minionSpawnDefinition == null || !stats)
            return 0;

        MinionCombatConfig cfg = MinionCombatConfig.AfterDeserialize(def.minionSpawnDefinition.combatConfig);
        float scale = cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit
            ? MinionRuntimeStatsCalculator.InheritMinionOwnerBonusScale
            : 1f;
        float effectivePct = Mathf.Max(0f, stats.FinalMinionDamagePercent) * scale;

        if (cfg.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit)
        {
            SplitDamageRange inheritedRange = new SplitDamageRange
            {
                min = stats.MinSplitDamage,
                max = stats.MaxSplitDamage
            };
            MinionRuntimeCombatStats runtime =
                MinionRuntimeStatsCalculator.Compute(stats, cfg, inheritedRange);
            float avgTotal = AverageSplitRangeTotal(runtime.FinalDamageSplitRange);
            if (effectivePct <= 0f || avgTotal <= 0f)
                return 0;

            float preTotal = avgTotal / (1f + effectivePct);
            return Mathf.RoundToInt(preTotal * effectivePct);
        }

        SplitDamageRange basePre = cfg.pureMinionDamageSplitRange;
        float p = (basePre.min.physical + basePre.max.physical) * 0.5f;
        float m = (basePre.min.magic + basePre.max.magic) * 0.5f;
        float c = (basePre.min.corruptionDamage + basePre.max.corruptionDamage) * 0.5f;
        float preTotalPure = p + m + c;
        return Mathf.RoundToInt(preTotalPure * Mathf.Max(0f, stats.FinalMinionDamagePercent));
    }

    private static float AverageSplitRangeTotal(SplitDamageRange range)
    {
        float avgPhys = (range.min.physical + range.max.physical) * 0.5f;
        float avgMag = (range.min.magic + range.max.magic) * 0.5f;
        float avgCorr = (range.min.corruptionDamage + range.max.corruptionDamage) * 0.5f;
        return avgPhys + avgMag + avgCorr;
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

    private static string FormatMinionDefenseFraction(float fraction) =>
        $"{Mathf.Clamp(fraction, 0f, 2f) * 100f:0.#}%";

    private static void AppendSoulforgedWarriorEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        SkillsManager skillsManager)
    {
        body.AppendLine(O("This minion inherits a portion of your weapons stats."));
        body.AppendLine(O("Inherits your maximum health and basic defences (armour, magic resist and corruption resist)."));
        body.AppendLine(O("Summons a soulforged clone."));
        body.AppendLine(O(
            $"Releases a warcry every {AbilityCombatPower.SoulforgedWarriorWarcryIntervalSeconds:0.#} seconds (the first at {AbilityCombatPower.SoulforgedWarriorWarcryFirstDelaySeconds:0.#}s), granting allies +{AbilityCombatPower.SoulforgedWarriorWarcryPhysicalDamageBonus * 100f:0.#}% physical damage for {AbilityCombatPower.SoulforgedWarriorWarcryBuffDurationSeconds:0.#}s."));
        AppendSoulforgedWarriorEnhancementLines(body, O, skillsManager);
        float dur = def?.minionSpawnDefinition != null
            ? Mathf.Max(0.1f, def.minionSpawnDefinition.summonDuration)
            : 30f;
        if (def != null && def.tooltipBuffMinionDurationSeconds > 0.01f)
            dur = def.tooltipBuffMinionDurationSeconds;
        body.AppendLine(O($"Duration: {dur:0.#}s"));
    }

    private static void AppendSoulforgedWarriorEnhancementLines(
        StringBuilder body,
        System.Func<string, string> O,
        SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return;

        int sel = skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.SoulforgedWarriorEnhancementParentSpineNodeId,
            -1);
        if (sel == AbilityCombatPower.SoulforgedWarriorFuriousSlamChoiceIndex)
        {
            body.AppendLine(O(
                $"Furious Slam: ground slam in front ({AbilityCombatPower.SoulforgedWarriorFuriousSlamRange:0.#} range) for {AbilityCombatPower.SoulforgedWarriorFuriousSlamDamageMultiplier * 100f:0.#}% minion strike damage."));
        }
        else if (sel == AbilityCombatPower.SoulforgedWarriorTauntingShoutChoiceIndex)
        {
            body.AppendLine(O(
                $"Taunting Shout: warcry also taunts enemies within {AbilityCombatPower.SoulforgedWarriorTauntRange:0.#} range. Taunted enemies deal {AbilityCombatPower.SoulforgedWarriorTauntingShoutOutgoingDamageReduction * 100f:0.#}% reduced damage for {AbilityCombatPower.SoulforgedWarriorTauntingShoutDebuffDurationSeconds:0.#}s."));
        }
    }

    private static void AppendHawkCompanionDurationLine(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def)
    {
        body.AppendLine(string.Empty);
        float dur = GetTooltipBuffMinionDisplayDurationSeconds(
            def, AbilityCombatPower.HawkCompanionDurationSeconds, 0f);
        body.AppendLine(O($"Duration: {dur:0.#}s"));
    }

    private static void AppendHawkCompanionTooltipEffectLinesNoStats(
        StringBuilder body,
        System.Func<string, string> O)
    {
        body.AppendLine(O($"{AbilityCombatPower.HawkCompanionBaseMinPhysical:0.#} - {AbilityCombatPower.HawkCompanionBaseMaxPhysical:0.#} damage on hit"));
        body.AppendLine(O($"{AbilityCombatPower.HawkCompanionBaseAttackSpeed} attacks per second"));
    }

    private static void AppendHawkCompanionRangedLevelScalingLine(
        StringBuilder body,
        System.Func<string, string> S,
        SkillsManager skillsManager)
    {
        body.AppendLine(S(
            $"+{AbilityCombatPower.HawkCompanionPhysicalPerTwoRangedLevels:0.#} physical damage every 2 ranged levels"));
    }

    private static void AppendHawkCompanionTooltipEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        SkillsManager skillsManager,
        bool includeEnhancementEffects)
    {
        int rangedLevel = skillsManager != null ? skillsManager.GetLevel(SkillType.Ranged) : 0;
        SplitDamageRange range = HawkCompanionStatsBuilder.BuildBaseDamageRange(rangedLevel);
        float dmgMult = 1f + stats.FinalMinionDamagePercent;
        int minD = Mathf.RoundToInt(range.min.physical * dmgMult);
        int maxD = Mathf.RoundToInt(range.max.physical * dmgMult);
        string dmgSuffix = DamageTimingSuffix();
        int mdFlat = ComputeTooltipMinionDamageFlatBonus(def, stats);

        body.AppendLine(O($"+{mdFlat} damage from Minion Damage{dmgSuffix}"));
        body.AppendLine(O($"{minD} - {maxD} damage on hit"));
        body.AppendLine(O($"{AbilityCombatPower.HawkCompanionBaseAttackSpeed} attacks per second"));

        if (!includeEnhancementEffects || skillsManager == null)
            return;

        int sel = skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged, AbilityCombatPower.HawkCompanionEnhancementParentSpineNodeId, -1);
        if (sel < 0)
            sel = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);

        if (sel == AbilityCombatPower.HawkCompanionLightningInfusedChoiceIndex)
        {
            body.AppendLine(O("Converts all its physical damage to lightning and gains 50% chance to shock."));
            body.AppendLine(O($"This shock applied uses your shock effect ({stats.ShockDamageTakenMultiplier * 100f:0.#}%)."));
        }
        else if (sel == AbilityCombatPower.HawkCompanionWeakspotsChoiceIndex)
        {
            body.AppendLine(O($"{AbilityCombatPower.HawkCompanionWeakspotsCritChanceBonus * 100f:0.#}% critical strike chance."));
        }
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
            body.AppendLine(O("Summons 3 soulforged weapons."));
            body.AppendLine(O("Each weapon 15% less damage."));
            body.AppendLine(O("Recast to collapse all weapons onto your current target, or find new targets if you don't have one."));
            return;
        }

        if (sel == SoulforgedWeaponExtendedDurationChoiceIndex)
        {
            body.AppendLine(O($"Soulforged Weapon now lasts {SoulforgedWeaponExtendedDurationSeconds:0.#}s."));
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
            body.AppendLine(O("+0 damage"));
            return;
        }

        ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
        DistributeMagicLaneDamage(stats, magHit, out float fireHit, out float iceHit, out float lightningHit, out float untypedMagicHit);
        AppendElementAwareDamageLines(body, O, physHit, fireHit, iceHit, lightningHit, untypedMagicHit, corrHit, suffix);
    }

    private static void AppendTripleShotTooltipHitDamage(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM,
        float liveDamageMultiplier)
    {
        if (!stats)
        {
            body.AppendLine(O("Arrow damage"));
            return;
        }

        var arrowDamageLines = new List<string>();
        CollectAbilityHitDamageRangeLines(
            arrowDamageLines,
            O,
            def,
            stats,
            weaponMult,
            allM,
            liveDamageMultiplier,
            " per arrow on hit");
        if (arrowDamageLines.Count > 0)
        {
            if (body.Length > 0)
                body.Append(DetailsEffectParagraphGap);
            AppendEffectLineGroup(body, arrowDamageLines);
        }

        AppendDetailsEffectParagraph(body, O(
            $"Fires {AbilityCombatPower.TripleShotArrowCount} arrows ({AbilityCombatPower.TripleShotPhantomArrowIntervalSeconds:0.#}s apart). Phantom arrows do not consume ammo."));
    }

    private static int GetStaticArrowsSelectedChoice(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return -1;

        int selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
        if (selected < 0)
            selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Ranged,
                AbilityCombatPower.StaticArrowsEnhancementParentSpineNodeId,
                -1);
        return selected;
    }

    private static void AppendStaticArrowsTooltipHitDamage(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        SkillsManager skillsManager,
        float weaponMult,
        float allM,
        float liveDamageMultiplier,
        bool includeEnhancementEffects = true)
    {
        string suffix = " on hit";
        int selected = GetStaticArrowsSelectedChoice(skillsManager);
        float conversionFrac = selected == AbilityCombatPower.StaticArrowsFullyChargedChoiceIndex
            ? AbilityCombatPower.StaticArrowsFullyChargedConversionFraction
            : AbilityCombatPower.StaticArrowsPhysicalToLightningConversionFraction;

        if (!stats)
        {
            AppendEffectLineGroup(body, new List<string> { O("+0 damage on hit") });
            AppendDetailsEffectParagraph(body, O(FormatStaticArrowsDurationEffectLine()));
            return;
        }

        ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out float physHit, out float magHit, out float corrHit, liveDamageMultiplier);
        DistributeMagicLaneDamage(stats, magHit, out float fireHit, out float iceHit, out float lightningHit, out float untypedMagicHit);

        float convertedPhys = physHit * conversionFrac;
        float displayPhys = Mathf.Max(0f, physHit - convertedPhys);
        float displayLightning = lightningHit + convertedPhys + untypedMagicHit;

        var damageLines = new List<string>();
        CollectElementAwareDamageLines(
            damageLines,
            O,
            displayPhys,
            fireHit,
            iceHit,
            displayLightning,
            0f,
            corrHit,
            suffix);
        AppendEffectLineGroup(body, damageLines);

        int conversionPct = Mathf.RoundToInt(conversionFrac * 100f);
        AppendDetailsEffectParagraph(body, O(FormatStaticArrowsDurationEffectLine()));
        AppendDetailsEffectParagraph(body, O(
            $"Converts {conversionPct}% of physical damage to lightning."));

        if (!includeEnhancementEffects || selected < 0)
            return;

        if (selected == AbilityCombatPower.StaticArrowsChainLightningChoiceIndex)
        {
            AppendDetailsEffectParagraph(body, O(
                $"On crit, arcs lightning to a nearby enemy within {AbilityCombatPower.StaticArrowsCritArcRange:0.#} range for {AbilityCombatPower.StaticArrowsCritArcDamageFraction * 100f:0.#}% of the hit's damage."));
        }
    }

    private const string DetailsEffectParagraphGap = "\n\n";

    private static void AppendDetailsEffectParagraph(StringBuilder body, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (body.Length > 0)
            body.Append(DetailsEffectParagraphGap);

        body.Append(line.Trim());
    }

    private static void AppendSnipeTooltipHitDamage(
        StringBuilder body,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        SkillsManager skillsManager,
        float weaponMult,
        float allM,
        float liveDamageMultiplier,
        bool includeEnhancementEffects = true,
        int enhancementChoiceOverride = -1)
    {
        int selected = ResolveSnipeEnhancementChoice(skillsManager, enhancementChoiceOverride);
        float duration = AbilityCombatPower.GetSnipeChargeDurationSeconds(selected);

        if (!stats)
        {
            var fallbackDamageLines = new List<string>
            {
                O("+0 damage at no charge"),
                O("+0 damage at full charge")
            };
            AppendSnipeTooltipChargeDamageGroup(body, fallbackDamageLines);

            AppendDetailsEffectParagraph(body, O($"Charge time: {duration:0.#}s"));
            AppendSnipeEnhancementEffectLines(body, O, selected, includeEnhancementEffects);
            return;
        }

        float initialMult = AbilityCombatPower.GetSnipeDamageMultiplierAtElapsed(0f, duration);
        float fullMult = AbilityCombatPower.SnipeMaxChargeDamageMultiplier;

        var snipeDamageLines = new List<string>();
        AppendSnipeTooltipChargeDamageLine(snipeDamageLines, O, def, stats, weaponMult * initialMult, allM, liveDamageMultiplier, " at no charge");
        AppendSnipeTooltipChargeDamageLine(snipeDamageLines, O, def, stats, weaponMult * fullMult, allM, liveDamageMultiplier, " at full charge");
        AppendSnipeTooltipChargeDamageGroup(body, snipeDamageLines);

        AppendDetailsEffectParagraph(body, O($"Charge time: {duration:0.#}s"));
        AppendSnipeEnhancementEffectLines(body, O, selected, includeEnhancementEffects);
    }

    private static void AppendSnipeTooltipChargeDamageGroup(StringBuilder body, List<string> lines)
        => AppendEffectLineGroup(body, lines);

    private static void AppendEffectLineGroup(StringBuilder body, List<string> lines)
    {
        if (lines == null || lines.Count == 0)
            return;

        if (body.Length > 0)
            body.Append(DetailsEffectParagraphGap);

        body.Append(string.Join("\n", lines));
    }

    private static string FormatStaticArrowsDurationEffectLine()
    {
        return
            $"Duration {AbilityCombatPower.StaticArrowsAutoAttackCount} hits or {AbilityCombatPower.StaticArrowsBaseDurationSeconds:0.#}s if hits are used up first.";
    }

    private static int ResolveSnipeEnhancementChoice(SkillsManager skillsManager, int enhancementChoiceOverride)
    {
        if (enhancementChoiceOverride >= 0)
            return enhancementChoiceOverride;

        int selected = skillsManager != null
            ? skillsManager.GetSkillChoiceSelection(SkillType.Ranged, AbilityCombatPower.SnipeEnhancementParentSpineNodeId, -1)
            : -1;
        if (selected < 0 && skillsManager != null)
            selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
        return selected;
    }

    private static void AppendSnipeEnhancementEffectLines(
        StringBuilder body,
        System.Func<string, string> O,
        int selected,
        bool includeEnhancementEffects)
    {
        if (!includeEnhancementEffects || selected < 0)
            return;

        if (selected == AbilityCombatPower.SnipeGuaranteedBleedChoiceIndex)
            AppendDetailsEffectParagraph(body, O("Gains 100% chance to bleed"));
    }

    private static void AppendSnipeTooltipChargeDamageLine(
        List<string> lines,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM,
        float liveDamageMultiplier,
        string suffix)
    {
        var lineBuilder = new List<string>();
        CollectAbilityHitDamageRangeLines(
            lineBuilder,
            O,
            def,
            stats,
            weaponMult,
            allM,
            liveDamageMultiplier,
            suffix);

        foreach (string line in lineBuilder)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line.Trim());
        }
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
        var lines = new List<string>();
        CollectElementAwareDamageLines(
            lines, O, physHit, fireHit, iceHit, lightningHit, magicHit, corrHit, suffix);
        foreach (string line in lines)
            body.AppendLine(line);
    }

    private static void CollectElementAwareDamageLines(
        List<string> lines,
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
            lines.Add(O($"{p} Physical damage{suffix}"));
        if (f > 0)
            lines.Add(O($"{f} Fire damage{suffix}"));
        if (i > 0)
            lines.Add(O($"{i} Ice damage{suffix}"));
        if (l > 0)
            lines.Add(O($"{l} Lightning damage{suffix}"));
        if (m > 0)
            lines.Add(O($"{m} Magic damage{suffix}"));
        if (c > 0)
            lines.Add(O($"{c} Corruption damage{suffix}"));

        if (p == 0 && f == 0 && i == 0 && l == 0 && m == 0 && c == 0)
            lines.Add(O($"+0 damage{suffix}"));
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

    private static void CollectAbilityHitDamageRangeLines(
        List<string> lines,
        System.Func<string, string> O,
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM,
        float liveDamageMultiplier,
        string suffix)
    {
        if (!def || !stats)
        {
            lines.Add(O($"+0 damage{suffix}"));
            return;
        }

        ComputeAbilityHitSplitBounds(
            def,
            stats,
            weaponMult,
            allM,
            out float physMin,
            out float physMax,
            out float magMin,
            out float magMax,
            out float corrMin,
            out float corrMax,
            liveDamageMultiplier);

        DistributeMagicLaneDamage(stats, magMin, out float fireMin, out float iceMin, out float lightningMin, out float magicMin);
        DistributeMagicLaneDamage(stats, magMax, out float fireMax, out float iceMax, out float lightningMax, out float magicMax);

        AppendDamageRangeLine(lines, O, physMin, physMax, "Physical damage", suffix);
        AppendDamageRangeLine(lines, O, fireMin, fireMax, "Fire damage", suffix);
        AppendDamageRangeLine(lines, O, iceMin, iceMax, "Ice damage", suffix);
        AppendDamageRangeLine(lines, O, lightningMin, lightningMax, "Lightning damage", suffix);
        AppendDamageRangeLine(lines, O, magicMin, magicMax, "Magic damage", suffix);
        AppendDamageRangeLine(lines, O, corrMin, corrMax, "Corruption damage", suffix);

        if (lines.Count == 0)
            lines.Add(O($"+0 damage{suffix}"));
    }

    private static void AppendDamageRangeLine(
        List<string> lines,
        System.Func<string, string> O,
        float minHit,
        float maxHit,
        string label,
        string suffix)
    {
        int min = Mathf.RoundToInt(Mathf.Max(0f, minHit));
        int max = Mathf.RoundToInt(Mathf.Max(0f, maxHit));
        if (min <= 0 && max <= 0)
            return;

        if (min == max)
            lines.Add(O($"{min} {label}{suffix}"));
        else
            lines.Add(O($"{min}–{max} {label}{suffix}"));
    }

    private static void ComputeAbilityHitSplitBounds(
        AbilityDefinition def,
        CharacterStats stats,
        float weaponMult,
        float allM,
        out float physHitMin,
        out float physHitMax,
        out float magHitMin,
        out float magHitMax,
        out float corrHitMin,
        out float corrHitMax,
        float liveDamageMultiplier = 1f)
    {
        physHitMin = physHitMax = magHitMin = magHitMax = corrHitMin = corrHitMax = 0f;
        if (!def || !stats)
            return;

        float wEff = weaponMult <= 0f ? 1f : weaponMult;
        float minPhys = Mathf.Max(0f, stats.MinSplitDamage.physical);
        float maxPhys = Mathf.Max(0f, stats.MaxSplitDamage.physical);
        float minMag = Mathf.Max(0f, stats.MinSplitDamage.magic);
        float maxMag = Mathf.Max(0f, stats.MaxSplitDamage.magic);
        float minCorr = Mathf.Max(0f, stats.MinSplitDamage.corruptionDamage);
        float maxCorr = Mathf.Max(0f, stats.MaxSplitDamage.corruptionDamage);

        float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apM = stats.GetAbilityPowerDamageMultiplier();
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
        float scale = Mathf.Max(0f, allM) * apM * Mathf.Max(0f, liveDamageMultiplier);

        physHitMin = (minPhys * wEff + ailmentBonus) * scale;
        physHitMax = (maxPhys * wEff + ailmentBonus) * scale;
        magHitMin = (minMag * wEff * elemM + elementBonus * elemM) * scale;
        magHitMax = (maxMag * wEff * elemM + elementBonus * elemM) * scale;
        corrHitMin = minCorr * wEff * scale;
        corrHitMax = maxCorr * wEff * scale;
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
        float apM = stats.GetAbilityPowerDamageMultiplier();

        if (finalStrike)
        {
            fireHit = (avgPhys + avgFire) * wMult * apM;
            return;
        }

        physHit = avgPhys * wMult * apM;
        fireHit = avgFire * wMult * apM;
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
