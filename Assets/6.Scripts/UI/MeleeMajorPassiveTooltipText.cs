using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Skill-tree and HUD copy for Melee major passives (Lv10 / Lv20 / Lv30 / Lv40).
/// Gameplay constants live on <see cref="CharacterStats"/> and <see cref="AbilityCombatPower"/>.
/// </summary>
public static class MeleeMajorPassiveTooltipText
{
    public const int AilmentAttunementMajorPassiveLevel = 10;
    public const string BattleEngineOverloadTitle = "Overload";
    public const string TacticianDualityHudBuffTitle = "Duality";

    /// <summary>Yellow accent for weapon bonuses currently applied (equipped loadout).</summary>
    private const string TacticianActiveColor = "#FFEB3B";
    /// <summary>Muted body text for weapon rows that are not currently active.</summary>
    private const string TacticianInactiveColor = "#EDE6D655";

    public static string BuildTacticianPerfectFormDescription() => "All Tactician bonuses are doubled.";

    public static string BuildTacticianSecondarySpecialistDescription()
    {
        return
            "While using a one-handed weapon with a shield, also gain the one-handed bonuses plus:\n" +
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianShieldBlockChanceBonus * 100f)}% block chance\n" +
            $"+{AbilityCombatPower.TacticianShieldFlatResistBonus} armour, magic resist, and corruption resist\n" +
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianShieldBlockMitigationBonus * 100f)}% block mitigation\n" +
            "While dual wielding (a one-handed weapon in the off-hand slot), every fifth hit hits a second time for 50% damage.";
    }

    public static string BuildTacticianDualityDescription() =>
        "All bonuses from Tactician are tripled for 8 seconds after swapping weapons.";

    public static string BuildTacticianDualityHudBody() => BuildTacticianDualityDescription();

    public static bool TryBuildCapstoneBody(out string body)
    {
        body = MeleeCapstoneEffectDescription;
        return true;
    }

    public static bool TryBuildCapstoneChoiceBody(int choiceIndex, out string body)
    {
        body = null;
        if (choiceIndex == AbilityCombatPower.MeleeCapstoneWayOfTheBerserkerChoiceIndex)
        {
            body = BuildWayOfTheBerserkerChoiceEffectBody();
            return true;
        }

        if (choiceIndex == AbilityCombatPower.MeleeCapstoneWayOfTheCrusaderChoiceIndex)
        {
            body = BuildWayOfTheCrusaderChoiceEffectBody();
            return true;
        }

        return false;
    }

    public static string BuildMeleeCapstoneRequirementsRichText(CharacterStats stats)
    {
        bool ok = stats != null && stats.HasMeleeWeaponEquippedForCapstonePassive();
        if (!ok)
            return $"<color=#FF5C5C>{MeleeCapstoneWeaponRequirementLine}</color>";

        return $"<color=#55DD55>{MeleeCapstoneWeaponRequirementLine}</color>";
    }

    public static string BuildWayOfTheBerserkerChoiceEffectBody()
    {
        var sb = new StringBuilder();
        sb.Append("Gain ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.WayOfTheBerserkerAttackSpeedPerStack * 100f));
        sb.Append("% attack speed, ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.WayOfTheBerserkerCritChancePerStack * 100f));
        sb.Append("% crit chance, ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.WayOfTheBerserkerMeleeDamagePerStack * 100f));
        sb.Append("% melee damage, and ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.WayOfTheBerserkerMoveSpeedPerStack * 100f));
        sb.Append("% move speed per auto attack (max ");
        sb.Append(AbilityCombatPower.WayOfTheBerserkerMaxStacks);
        sb.Append(" stacks, ");
        sb.Append(AbilityCombatPower.WayOfTheBerserkerStackDurationSeconds.ToString("0.#"));
        sb.AppendLine(" seconds).");
        sb.AppendLine();
        sb.Append("Damage taken increased by ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.WayOfTheBerserkerDamageTakenPerStack * 100f));
        sb.AppendLine("% per stack.");
        sb.AppendLine();
        sb.Append("While below 30% HP, gain ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.WayOfTheBerserkerLowHpLeechFraction * 100f));
        sb.Append("% life steal for ");
        sb.Append(AbilityCombatPower.WayOfTheBerserkerLowHpLeechDurationSeconds.ToString("0.#"));
        sb.Append(" seconds (");
        sb.Append(AbilityCombatPower.WayOfTheBerserkerLowHpLeechCooldownSeconds.ToString("0.#"));
        sb.AppendLine(" second cooldown).");
        sb.AppendLine();
        sb.Append("At ");
        sb.Append(AbilityCombatPower.WayOfTheBerserkerSlowImmunityMinStacks);
        sb.Append("+ stacks, chill and other slows cannot reduce move speed below your normal move speed.");
        return sb.ToString();
    }

    public static string BuildWayOfTheCrusaderChoiceEffectBody()
    {
        int healPct = Mathf.RoundToInt(AbilityCombatPower.WayOfTheCrusaderHealMaxHpFraction * 100f);
        int firePct = Mathf.RoundToInt(AbilityCombatPower.WayOfTheCrusaderFireStrikeWeaponDamageFraction * 100f);

        var sb = new StringBuilder();
        sb.Append("Gain 1 holy seal per ");
        sb.Append(AbilityCombatPower.WayOfTheCrusaderHolySealGainIntervalSeconds.ToString("0.#"));
        sb.Append(" seconds (max ");
        sb.Append(AbilityCombatPower.WayOfTheCrusaderMaxHolySeals);
        sb.AppendLine(").");
        sb.AppendLine();
        sb.Append("Upon taking damage while below full HP, consume a seal to heal for ");
        sb.Append(healPct);
        sb.AppendLine("% max HP.");
        sb.AppendLine();
        sb.Append("Blocking a hit heals for ");
        sb.Append(healPct);
        sb.AppendLine("% max HP and does not consume a seal.");
        sb.AppendLine();
        sb.Append("When a seal is consumed, your next auto attack deals an additional ");
        sb.Append(firePct);
        sb.AppendLine("% weapon damage as extra fire.");
        sb.AppendLine();
        sb.Append("Every time a burn is applied, the enemy is also shocked.");
        return sb.ToString();
    }

    public static string BuildWayOfTheCrusaderHudBody(int currentSeals)
    {
        int maxSeals = AbilityCombatPower.WayOfTheCrusaderMaxHolySeals;
        int healPct = Mathf.RoundToInt(AbilityCombatPower.WayOfTheCrusaderHealMaxHpFraction * 100f);
        int firePct = Mathf.RoundToInt(AbilityCombatPower.WayOfTheCrusaderFireStrikeWeaponDamageFraction * 100f);

        var sb = new StringBuilder();
        sb.Append("Holy seals: ");
        sb.Append(currentSeals);
        sb.Append('/');
        sb.AppendLine(maxSeals.ToString());
        sb.AppendLine();
        sb.Append("Gain 1 seal per ");
        sb.Append(AbilityCombatPower.WayOfTheCrusaderHolySealGainIntervalSeconds.ToString("0.#"));
        sb.Append(" seconds. While below full HP, taking damage consumes a seal to heal ");
        sb.Append(healPct);
        sb.AppendLine("% max HP.");
        sb.Append("Blocking heals ");
        sb.Append(healPct);
        sb.AppendLine("% max HP without consuming a seal.");
        sb.Append("Consuming a seal primes your next auto attack for +");
        sb.Append(firePct);
        sb.Append("% extra weapon damage as fire.");
        return sb.ToString();
    }

    public static string BuildWayOfTheBerserkerHudBody(int currentStacks)
    {
        int maxStacks = AbilityCombatPower.WayOfTheBerserkerMaxStacks;
        var sb = new StringBuilder();
        sb.Append("Stacks: ");
        sb.Append(currentStacks);
        sb.Append('/');
        sb.AppendLine(maxStacks.ToString());
        if (currentStacks > 0)
        {
            sb.Append('+');
            sb.Append(Mathf.RoundToInt(currentStacks * AbilityCombatPower.WayOfTheBerserkerAttackSpeedPerStack * 100f));
            sb.AppendLine("% attack speed");
            sb.Append('+');
            sb.Append(Mathf.RoundToInt(currentStacks * AbilityCombatPower.WayOfTheBerserkerCritChancePerStack * 100f));
            sb.AppendLine("% crit chance");
            sb.Append('+');
            sb.Append(Mathf.RoundToInt(currentStacks * AbilityCombatPower.WayOfTheBerserkerMeleeDamagePerStack * 100f));
            sb.AppendLine("% melee damage");
            sb.Append('+');
            sb.Append(Mathf.RoundToInt(currentStacks * AbilityCombatPower.WayOfTheBerserkerMoveSpeedPerStack * 100f));
            sb.AppendLine("% move speed");
            sb.Append('+');
            sb.Append(Mathf.RoundToInt(currentStacks * AbilityCombatPower.WayOfTheBerserkerDamageTakenPerStack * 100f));
            sb.AppendLine("% damage taken");
        }

        sb.Append("Refreshes for ");
        sb.Append(AbilityCombatPower.WayOfTheBerserkerStackDurationSeconds.ToString("0.#"));
        sb.Append("s on melee auto attack.");
        if (currentStacks >= AbilityCombatPower.WayOfTheBerserkerSlowImmunityMinStacks)
            sb.AppendLine("\nSlow immunity active.");

        return sb.ToString();
    }

    public static string BuildWayOfTheBerserkerLeechHudBody() =>
        $"+{Mathf.RoundToInt(AbilityCombatPower.WayOfTheBerserkerLowHpLeechFraction * 100f)}% life steal while below 30% HP.";

    public const string AilmentAttunementFlavorDescription = "Increase your proficiency with ailments";
    public const string ParryFlavorDescription =
        "Increase your chance to parry enemies in melee range. When you parry, mitigate a portion of the hit and deal it back to the attacker.";
    public const string PredatorsInstinctFlavorDescription = "Aim for the enemies weakspots";
    public const string BattleEngineFlavorDescription = "Turn successful ability hits into combat energy";
    public const string TacticianFlavorDescription = "Adapt your fighting style to the weapon you wield";
    public const string PhoenixSoulFlavorDescription =
        "Draw life and energy from the heat of nearby burning foes.";
    public const string MasterOfVenomsFlavorDescription =
        "Master deadly poisons that can critically strike your enemies.";
    public const string MeleeCapstoneFlavorDescription = "Enhance into a pure form";
    public const string MeleeCapstoneEffectDescription =
        "Select an enhancement below to evolve your capstone passive.";
    public const string WayOfTheBerserkerTitle = "Way of the Berserker";
    public const string WayOfTheBerserkerLeechTitle = "Berserker's Thirst";
    public const string MeleeCapstoneWeaponRequirementLine = "Required: Melee weapon";
    public const string WayOfTheCrusaderTitle = "Way of the Crusader";

    /// <summary>Short flavor copy for the details panel description column.</summary>
    public static bool TryBuildFlavorDescription(string parentSpineNodeId, out string flavor)
    {
        flavor = null;
        if (string.IsNullOrWhiteSpace(parentSpineNodeId))
            return false;

        if (string.Equals(parentSpineNodeId, "Lv10_0", StringComparison.Ordinal))
        {
            flavor = AilmentAttunementFlavorDescription;
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.ParryMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            flavor = ParryFlavorDescription;
            return true;
        }

        if (string.Equals(parentSpineNodeId, "Lv20_0", StringComparison.Ordinal))
        {
            flavor = PredatorsInstinctFlavorDescription;
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.BattleEngineEnhancementParentSpineNodeId, StringComparison.Ordinal))
        {
            flavor = BattleEngineFlavorDescription;
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.TacticianMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            flavor = TacticianFlavorDescription;
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.PhoenixSoulEnhancementParentSpineNodeId, StringComparison.Ordinal))
        {
            flavor = PhoenixSoulFlavorDescription;
            return true;
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.MasterOfVenomsEnhancementParentSpineNodeId, StringComparison.Ordinal))
        {
            flavor = MasterOfVenomsFlavorDescription;
            return true;
        }

        return false;
    }

    /// <summary>Skill-tree body from parent spine id (e.g. Lv40_0, Lv40_1).</summary>
    public static bool TryBuildSkillTreeBody(string parentSpineNodeId, int selectedChoice, out string body) =>
        TryBuildSkillTreeBody(parentSpineNodeId, selectedChoice, ResolvePlayerStatsForTooltip(), out body);

    public static bool TryBuildSkillTreeBody(
        string parentSpineNodeId,
        int selectedChoice,
        CharacterStats stats,
        out string body)
    {
        body = null;
        if (string.IsNullOrWhiteSpace(parentSpineNodeId))
            return false;

        switch (parentSpineNodeId)
        {
            case "Lv10_0":
                return TryBuildAilmentAttunementBody(selectedChoice, out body);
            case AbilityCombatPower.ParryMajorPassiveSpineNodeId:
                return TryBuildParryBody(selectedChoice, out body);
            case "Lv20_0":
                return TryBuildPredatorsInstinctBody(selectedChoice, out body);
            case "Lv30_0":
                return TryBuildBattleEngineBody(selectedChoice, out body);
            case AbilityCombatPower.TacticianMajorPassiveSpineNodeId:
                return TryBuildTacticianBody(selectedChoice, stats, out body);
            case AbilityCombatPower.PhoenixSoulEnhancementParentSpineNodeId:
                return TryBuildPhoenixSoulBody(selectedChoice, out body);
            case AbilityCombatPower.MasterOfVenomsEnhancementParentSpineNodeId:
                return TryBuildMasterOfVenomsBody(selectedChoice, out body);
            default:
                return false;
        }
    }

    public static bool TryGetHudBuffTooltip(
        string buffId,
        int displayStacks,
        out string title,
        out string body)
    {
        title = null;
        body = null;
        if (string.Equals(buffId, CharacterStats.PhoenixSoulAshenRebirthImmunityHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            title = "Ashen Rebirth";
            body =
                $"Immune to damage for {AbilityCombatPower.PhoenixSoulAshenRebirthImmunitySeconds:0.#} seconds " +
                $"(remaining time shown on icon).";
            return true;
        }

        if (string.Equals(buffId, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            title = BattleEngineOverloadTitle;
            body = BuildOverloadHudBody(Mathf.Max(0, displayStacks));
            return true;
        }

        if (string.Equals(buffId, CharacterStats.TacticianDualityHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            title = TacticianDualityHudBuffTitle;
            body = BuildTacticianDualityHudBody();
            return true;
        }

        if (string.Equals(buffId, PlayerCombatController.WayOfTheBerserkerHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            title = WayOfTheBerserkerTitle;
            body = BuildWayOfTheBerserkerHudBody(Mathf.Max(0, displayStacks));
            return true;
        }

        if (string.Equals(buffId, PlayerCombatController.WayOfTheBerserkerLeechHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            title = WayOfTheBerserkerLeechTitle;
            body = BuildWayOfTheBerserkerLeechHudBody();
            return true;
        }

        if (string.Equals(buffId, PlayerCombatController.WayOfTheCrusaderHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            title = WayOfTheCrusaderTitle;
            body = BuildWayOfTheCrusaderHudBody(Mathf.Max(0, displayStacks));
            return true;
        }

        return false;
    }

    public static string BuildOverloadHudBody(int currentStacks)
    {
        int maxStacks = AbilityCombatPower.BattleEngineOverloadMaxStacks;
        int costPct = Mathf.RoundToInt(currentStacks * AbilityCombatPower.BattleEngineOverloadCostPerStack * 100f);
        int dmgPct = Mathf.RoundToInt(currentStacks * AbilityCombatPower.BattleEngineOverloadDamagePerStack * 100f);

        var sb = new StringBuilder();
        if (currentStacks > 0)
        {
            sb.Append("Overload stacks: ");
            sb.Append(currentStacks);
            sb.Append('/');
            sb.AppendLine(maxStacks.ToString());
            sb.AppendLine();
            sb.Append('+');
            sb.Append(costPct);
            sb.AppendLine("% Ability Energy Cost");
            sb.Append('+');
            sb.Append(dmgPct);
            sb.AppendLine("% Ability Damage");
            sb.AppendLine();
        }

        sb.Append("Each ability cast adds +");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.BattleEngineOverloadCostPerStack * 100f));
        sb.Append("% cost and +");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.BattleEngineOverloadDamagePerStack * 100f));
        sb.AppendLine("% damage (max ");
        sb.Append(maxStacks);
        sb.Append(" stacks).");
        sb.Append("Refreshes for ");
        sb.Append(CharacterStats.BattleEngineOverloadDurationSeconds.ToString("0.#"));
        sb.Append("s when you cast an ability; expires if you stop casting.");
        return sb.ToString();
    }

    /// <summary>Enhancement choice node tooltip (e.g. Lv43 Ashen Rebirth).</summary>
    public static bool TryBuildChoiceTooltipBody(string parentSpineNodeId, int choiceIndex, out string body)
    {
        body = null;
        if (string.IsNullOrWhiteSpace(parentSpineNodeId))
            return false;

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.MeleeCapstoneSpineNodeId, StringComparison.Ordinal))
            return TryBuildCapstoneChoiceBody(choiceIndex, out body);

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.PhoenixSoulEnhancementParentSpineNodeId, StringComparison.Ordinal))
        {
            if (choiceIndex == 0)
            {
                var sb = new StringBuilder();
                AppendAshenRebirthEffectLines(sb);
                body = sb.ToString();
                return true;
            }

            if (choiceIndex == 1)
            {
                var sb = new StringBuilder();
                AppendLivingInfernoEffectLine(sb);
                body = sb.ToString();
                return true;
            }
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.TacticianMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            if (choiceIndex == AbilityCombatPower.TacticianEnhancementPerfectForm)
            {
                body = BuildTacticianPerfectFormDescription();
                return true;
            }

            if (choiceIndex == AbilityCombatPower.TacticianEnhancementBulwark)
            {
                body = BuildTacticianSecondarySpecialistDescription();
                return true;
            }

            if (choiceIndex == AbilityCombatPower.TacticianEnhancementDuality)
            {
                body = BuildTacticianDualityDescription();
                return true;
            }
        }

        if (string.Equals(parentSpineNodeId, AbilityCombatPower.ParryMajorPassiveSpineNodeId, StringComparison.Ordinal))
        {
            if (choiceIndex == 0)
            {
                body =
                    "Parries no longer reduce incoming damage.\n" +
                    "On a successful parry, perform a free melee auto attack (no swing delay).\n" +
                    $"{AbilityCombatPower.ParryRiposteCooldownSeconds:0.#} second cooldown.";
                return true;
            }

            if (choiceIndex == 1)
            {
                body = "+5% additional parry chance.\n+15% parry mitigation.";
                return true;
            }
        }

        return false;
    }

    public static string FormatOverloadValueLabel(int stacks)
    {
        if (stacks <= 0)
            return "";

        int dmgPct = Mathf.RoundToInt(stacks * AbilityCombatPower.BattleEngineOverloadDamagePerStack * 100f);
        return $"+{dmgPct}%";
    }

    private static bool TryBuildAilmentAttunementBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine("+5% Bleed, Poison, Burn Ailment Chance");
        sb.AppendLine("+5% Melee Damage to enemies affected by an ailment");
        AppendEnhancementLines(sb, selectedChoice, "Lv10_0");
        body = sb.ToString();
        return true;
    }

    private static bool TryBuildParryBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        if (selectedChoice == 1)
        {
            int chance = Mathf.RoundToInt(
                (AbilityCombatPower.ParryBaseChance + AbilityCombatPower.ParryImprovedParryChanceBonus) * 100f);
            int mitigation = Mathf.RoundToInt(AbilityCombatPower.ParryImprovedMitigationBonus * 100f);
            sb.Append("Gain ");
            sb.Append(chance);
            sb.Append("% parry chance and ");
            sb.Append(mitigation);
            sb.AppendLine("% parry mitigation.");
        }
        else
        {
            sb.Append("Gain ");
            sb.Append(Mathf.RoundToInt(AbilityCombatPower.ParryBaseChance * 100f));
            sb.AppendLine("% parry chance.");
            AppendEnhancementLines(sb, selectedChoice, AbilityCombatPower.ParryMajorPassiveSpineNodeId);
        }

        body = sb.ToString();
        return true;
    }

    private static bool TryBuildPredatorsInstinctBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine("+5% Critical Chance");
        sb.AppendLine("+10% Critical Damage");
        AppendEnhancementLines(sb, selectedChoice, "Lv20_0");
        body = sb.ToString();
        return true;
    }

    private static bool TryBuildBattleEngineBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Gain 5 Energy when abilities hit enemies (once per cast).");
        AppendEnhancementLines(sb, selectedChoice, AbilityCombatPower.BattleEngineEnhancementParentSpineNodeId);
        body = sb.ToString();
        return true;
    }

    private static CharacterStats ResolvePlayerStatsForTooltip()
    {
        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        return player != null ? player.GetComponent<CharacterStats>() : null;
    }

    private static void AppendTacticianSecondarySpecialistLines(
        StringBuilder sb,
        bool shieldActive,
        bool dualWieldActive,
        float bonusMultiplier)
    {
        AppendTacticianSectionSpacer(sb);
        AppendTacticianColoredLine(
            sb,
            "While using a one-handed weapon with a shield, also gain the one-handed bonuses plus:",
            shieldActive);
        AppendTacticianColoredLine(
            sb,
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianShieldBlockChanceBonus, bonusMultiplier)}% block chance",
            shieldActive);
        AppendTacticianColoredLine(
            sb,
            $"+{AbilityCombatPower.TacticianShieldFlatResistBonus} armour, magic resist, and corruption resist",
            shieldActive);
        AppendTacticianColoredLine(
            sb,
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianShieldBlockMitigationBonus * 100f)}% block mitigation",
            shieldActive);
        AppendTacticianColoredLine(
            sb,
            "While dual wielding (a one-handed weapon in the off-hand slot), every fifth hit hits a second time for 50% damage.",
            dualWieldActive);
    }

    private static void AppendTacticianSectionSpacer(StringBuilder sb) => sb.AppendLine();

    private static float ResolveTacticianTooltipBonusMultiplier(int selectedChoice, bool dualityWindowActive)
    {
        if (selectedChoice == AbilityCombatPower.TacticianEnhancementPerfectForm)
            return 2f;

        if (selectedChoice == AbilityCombatPower.TacticianEnhancementDuality && dualityWindowActive)
            return 3f;

        return 1f;
    }

    private static int ScaleTacticianPercentDisplay(float baseFraction, float multiplier) =>
        Mathf.RoundToInt(baseFraction * multiplier * 100f);

    private static void AppendTacticianColoredLine(
        StringBuilder sb,
        string text,
        bool highlightActive,
        bool fadeInactive = true)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (highlightActive)
            sb.Append("<color=").Append(TacticianActiveColor).Append(">");
        else if (fadeInactive)
            sb.Append("<color=").Append(TacticianInactiveColor).Append(">");

        sb.Append(text);

        if (highlightActive || fadeInactive)
            sb.Append("</color>");

        sb.AppendLine();
    }

    private static bool TryBuildTacticianBody(int selectedChoice, CharacterStats stats, out string body)
    {
        bool oneHandActive = stats != null && stats.IsMainHandOneHandedWeaponEquipped();
        bool twoHandActive = stats != null && stats.IsMainHandTwoHandedMeleeWeaponEquipped();
        bool dualityWindowActive = selectedChoice == AbilityCombatPower.TacticianEnhancementDuality &&
                                   stats != null &&
                                   stats.IsWithinRecentWeaponSwapWindow();
        float bonusMultiplier = ResolveTacticianTooltipBonusMultiplier(selectedChoice, dualityWindowActive);

        var sb = new StringBuilder();
        AppendTacticianColoredLine(sb, "When wielding certain melee weapons, gain:", false, fadeInactive: false);
        AppendTacticianColoredLine(sb, "One-handed:", oneHandActive);
        AppendTacticianColoredLine(
            sb,
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianOneHandedAttackSpeedPercent, bonusMultiplier)}% attack speed",
            oneHandActive);
        AppendTacticianColoredLine(
            sb,
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianOneHandedPoisonChance, bonusMultiplier)}% poison and " +
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianOneHandedBurnChance, bonusMultiplier)}% burn chance",
            oneHandActive);
        AppendTacticianColoredLine(
            sb,
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianOneHandedCritChance, bonusMultiplier)}% critical strike chance",
            oneHandActive);
        AppendTacticianSectionSpacer(sb);
        AppendTacticianColoredLine(sb, "Two-handed:", twoHandActive);
        AppendTacticianColoredLine(
            sb,
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianTwoHandedBleedMultiplierBonus, bonusMultiplier)}% bleed multiplier",
            twoHandActive);
        AppendTacticianColoredLine(
            sb,
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianTwoHandedArmorPenetration, bonusMultiplier)}% armour penetration",
            twoHandActive);
        AppendTacticianColoredLine(
            sb,
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianTwoHandedStunChance, bonusMultiplier)}% chance to stun",
            twoHandActive);
        AppendTacticianColoredLine(
            sb,
            $"+{ScaleTacticianPercentDisplay(AbilityCombatPower.TacticianTwoHandedBlockChance, bonusMultiplier)}% block chance",
            twoHandActive);
        AppendTacticianEnhancementLines(sb, selectedChoice, stats, bonusMultiplier);
        body = sb.ToString();
        return true;
    }

    private static void AppendTacticianEnhancementLines(
        StringBuilder sb,
        int selectedChoice,
        CharacterStats stats,
        float bonusMultiplier)
    {
        if (sb == null || selectedChoice < 0)
            return;

        if (selectedChoice != AbilityCombatPower.TacticianEnhancementBulwark)
            return;

        bool specialistActive = stats != null &&
                                stats.HasShieldEquipped() &&
                                stats.IsMainHandOneHandedWeaponEquipped();
        bool dualWieldActive = stats != null && stats.IsDualWieldingOneHandedWeapons();
        AppendTacticianSecondarySpecialistLines(sb, specialistActive, dualWieldActive, bonusMultiplier);
    }

    private static bool TryBuildPhoenixSoulBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        sb.Append("Burning nearby enemies restore ");
        sb.Append(AbilityCombatPower.PhoenixSoulLifePerBurningEnemy.ToString("0.#"));
        sb.Append(" Life and ");
        sb.Append(AbilityCombatPower.PhoenixSoulEnergyPerBurningEnemy.ToString("0.#"));
        sb.Append(" Energy every ");
        sb.Append(AbilityCombatPower.PhoenixSoulBurnRegenIntervalSeconds.ToString("0.#"));
        sb.Append(" seconds (max ");
        sb.Append(AbilityCombatPower.PhoenixSoulMaxNearbyBurningEnemies);
        sb.AppendLine(" enemies).");
        sb.Append("+");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.PhoenixSoulBurnChanceBonus * 100f));
        sb.AppendLine("% burn chance.");
        AppendEnhancementLines(sb, selectedChoice, AbilityCombatPower.PhoenixSoulEnhancementParentSpineNodeId);
        body = sb.ToString();
        return true;
    }

    private static bool TryBuildMasterOfVenomsBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        sb.Append("Poison can critically strike (dealing ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.MasterOfVenomsPoisonCritFractionOfCritDamage * 100f));
        sb.AppendLine("% of your critical damage).");
        sb.Append("+");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.MasterOfVenomsPoisonChanceBonus * 100f));
        sb.AppendLine("% poison chance.");
        AppendEnhancementLines(sb, selectedChoice, AbilityCombatPower.MasterOfVenomsEnhancementParentSpineNodeId);
        body = sb.ToString();
        return true;
    }

    private static void AppendEnhancementLines(StringBuilder sb, int selectedChoice, string parentSpineNodeId)
    {
        if (sb == null || selectedChoice < 0 || string.IsNullOrWhiteSpace(parentSpineNodeId))
            return;

        if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
            sb.AppendLine();

        switch (parentSpineNodeId)
        {
            case "Lv10_0":
                switch (selectedChoice)
                {
                    case 0:
                        sb.AppendLine("+10% Melee Bleed Multiplier");
                        sb.AppendLine("+2 Poison Max Stacks");
                        break;
                    case 1:
                        sb.AppendLine("+10% Melee Bleed Multiplier");
                        sb.AppendLine("+1s Melee Bleed Duration");
                        break;
                    case 2:
                        sb.AppendLine("+10% Burn Damage Multiplier");
                        sb.AppendLine("-0.5s Burn Tick Rate");
                        break;
                }
                break;

            case AbilityCombatPower.ParryMajorPassiveSpineNodeId:
                if (selectedChoice == 0)
                    sb.AppendLine(
                        $"Riposte: free melee auto attack on parry (no damage reduction, {AbilityCombatPower.ParryRiposteCooldownSeconds:0.#}s cooldown).");
                else if (selectedChoice == 1)
                {
                    // Combined in TryBuildParryBody when Improved Parry is committed.
                }
                break;

            case "Lv20_0":
                if (selectedChoice == 0)
                    sb.AppendLine("+30% Critical Damage vs enemies below 30% HP");
                else if (selectedChoice == 1)
                    sb.AppendLine("+10% Attack Speed for 7 seconds on critical hit");
                break;

            case AbilityCombatPower.BattleEngineEnhancementParentSpineNodeId:
                if (selectedChoice == 0)
                    sb.AppendLine("Using an ability lowers your other cooldowns by 0.5 seconds.");
                else if (selectedChoice == 1)
                    sb.AppendLine("Using an ability adds +10% ability cost and +5% ability damage per stack (max 5 stacks, 10s).");
                break;

            case AbilityCombatPower.TacticianMajorPassiveSpineNodeId:
                if (selectedChoice == AbilityCombatPower.TacticianEnhancementBulwark)
                    sb.AppendLine(BuildTacticianSecondarySpecialistDescription());
                break;

            case AbilityCombatPower.PhoenixSoulEnhancementParentSpineNodeId:
                if (selectedChoice == 0)
                    AppendAshenRebirthEffectLines(sb);
                else if (selectedChoice == 1)
                    AppendLivingInfernoEffectLine(sb);
                break;

            case AbilityCombatPower.MasterOfVenomsEnhancementParentSpineNodeId:
                if (selectedChoice == 0)
                {
                    sb.Append("Poisoned enemies deal ");
                    sb.Append(Mathf.RoundToInt(AbilityCombatPower.MasterOfVenomsNeurotoxinOutgoingDamageReduction * 100f));
                    sb.AppendLine("% less damage.");
                    sb.Append("+");
                    sb.Append(Mathf.RoundToInt(AbilityCombatPower.MasterOfVenomsNeurotoxinMoveSlowPerPoisonStack * 100f));
                    sb.AppendLine("% movement speed per poison stack.");
                }
                else if (selectedChoice == 1)
                {
                    sb.Append("Poisons have ");
                    sb.Append(AbilityCombatPower.MasterOfVenomsLethalCompoundDurationReductionPerStackSeconds.ToString("0.#"));
                    sb.Append("s less duration per stack, +");
                    sb.Append(AbilityCombatPower.MasterOfVenomsLethalCompoundMaxStacksBonus);
                    sb.AppendLine(" max poison stacks.");
                }
                break;
        }
    }

    private static void AppendAshenRebirthEffectLines(StringBuilder sb)
    {
        if (sb == null)
            return;

        sb.Append("On death, revive at ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.PhoenixSoulAshenRebirthHealthFraction * 100f));
        sb.Append("% health and become immune to damage for ");
        sb.Append(AbilityCombatPower.PhoenixSoulAshenRebirthImmunitySeconds.ToString("0.#"));
        sb.Append(" seconds (");
        sb.Append(AbilityCombatPower.PhoenixSoulAshenRebirthCooldownSeconds.ToString("0.#"));
        sb.AppendLine(" second cooldown).");
        sb.Append("On proc: ");
        sb.Append(AbilityCombatPower.PhoenixSoulAshenRebirthExplosionFlatFireDamage.ToString("0.#"));
        sb.AppendLine(" fire damage to nearby enemies and burn them.");
    }

    private static void AppendLivingInfernoEffectLine(StringBuilder sb)
    {
        if (sb == null)
            return;

        sb.Append("+");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.PhoenixSoulLivingInfernoMeleeDamagePerBurningEnemy * 100f));
        sb.Append("% melee damage per burning enemy nearby (up to ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.PhoenixSoulLivingInfernoMaxMeleeDamageBonusFraction * 100f));
        sb.AppendLine("% at 5 enemies).");
    }
}
