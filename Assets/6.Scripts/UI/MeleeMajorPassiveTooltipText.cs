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

    public static string BuildTacticianPerfectFormDescription() => "All Tactician bonuses are doubled.";

    public static string BuildTacticianSecondarySpecialistDescription()
    {
        return
            "While using a one-handed weapon with a shield, also gain the one-handed bonuses plus:\n" +
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianShieldBlockChanceBonus * 100f)}% block chance\n" +
            $"+{AbilityCombatPower.TacticianShieldFlatResistBonus} armour, magic resist, and corruption resist\n" +
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianShieldBlockMitigationBonus * 100f)}% block mitigation\n" +
            "While dual wielding (a one-handed weapon in the off-hand slot), every fifth hit hits twice.";
    }

    public static string BuildTacticianDualityDescription() =>
        "All bonuses from Tactician are tripled for 8 seconds after swapping weapons.";

    public static string BuildTacticianDualityHudBody() => BuildTacticianDualityDescription();

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
                    "On a successful parry, perform a free melee auto attack (no swing delay; ailments cannot be applied).";
                return true;
            }

            if (choiceIndex == 1)
            {
                body = "Increase parry chance to 20%.";
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
        sb.Append("When struck by a melee-range enemy (within ");
        sb.Append(AbilityCombatPower.ParryMeleeRange.ToString("0.#"));
        sb.Append(" units), ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.ParryBaseChance * 100f));
        sb.AppendLine("% chance to parry:");
        sb.Append("Reduce that hit by ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.ParryDamageReductionFraction * 100f));
        sb.AppendLine("% and deal that damage back to the attacker.");
        AppendEnhancementLines(sb, selectedChoice, AbilityCombatPower.ParryMajorPassiveSpineNodeId);
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

    private static void AppendTacticianSecondarySpecialistLines(StringBuilder sb, bool specialistActive)
    {
        foreach (string line in BuildTacticianSecondarySpecialistDescription().Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
                AppendTacticianColoredLine(sb, line.Trim(), specialistActive);
        }
    }

    private static void AppendTacticianColoredLine(StringBuilder sb, string text, bool highlightActive)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (highlightActive)
            sb.Append("<color=").Append(TacticianActiveColor).Append(">");

        sb.Append(text);

        if (highlightActive)
            sb.Append("</color>");

        sb.AppendLine();
    }

    private static bool TryBuildTacticianBody(int selectedChoice, CharacterStats stats, out string body)
    {
        bool oneHandActive = stats != null && stats.IsTacticianApplyingOneHandedWeaponBonuses;
        bool twoHandActive = stats != null && stats.IsTacticianApplyingTwoHandedWeaponBonuses;

        var sb = new StringBuilder();
        AppendTacticianColoredLine(sb, "When wielding certain melee weapons, gain:", false);
        AppendTacticianColoredLine(sb, "One-handed:", oneHandActive);
        AppendTacticianColoredLine(sb,
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianOneHandedAttackSpeedPercent * 100f)}% attack speed",
            oneHandActive);
        AppendTacticianColoredLine(sb,
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianOneHandedPoisonChance * 100f)} poison and " +
            $"{Mathf.RoundToInt(AbilityCombatPower.TacticianOneHandedBurnChance * 100f)} burn chance",
            oneHandActive);
        AppendTacticianColoredLine(sb,
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianOneHandedCritChance * 100f)}% critical strike chance",
            oneHandActive);
        AppendTacticianColoredLine(sb, "Two-handed:", twoHandActive);
        AppendTacticianColoredLine(sb,
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianTwoHandedBleedMultiplierBonus * 100f)}% bleed multiplier",
            twoHandActive);
        AppendTacticianColoredLine(sb,
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianTwoHandedArmorPenetration * 100f)}% armour penetration",
            twoHandActive);
        AppendTacticianColoredLine(sb,
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianTwoHandedStunChance * 100f)}% chance to stun",
            twoHandActive);
        AppendTacticianColoredLine(sb,
            $"+{Mathf.RoundToInt(AbilityCombatPower.TacticianTwoHandedBlockChance * 100f)}% block chance",
            twoHandActive);
        AppendTacticianEnhancementLines(sb, selectedChoice, stats);
        body = sb.ToString();
        return true;
    }

    private static void AppendTacticianEnhancementLines(StringBuilder sb, int selectedChoice, CharacterStats stats)
    {
        if (sb == null || selectedChoice < 0)
            return;

        if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
            sb.AppendLine();

        bool specialistActive = stats != null && stats.IsTacticianApplyingSecondarySpecialistShieldBonuses;
        bool dualWieldActive = stats != null && stats.IsTacticianApplyingSecondarySpecialistDualWieldBonus;
        bool dualityActive = stats != null && stats.IsRecentWeaponSwapForTacticianDuality();

        switch (selectedChoice)
        {
            case AbilityCombatPower.TacticianEnhancementPerfectForm:
                AppendTacticianColoredLine(sb, BuildTacticianPerfectFormDescription(), false);
                break;
            case AbilityCombatPower.TacticianEnhancementBulwark:
                AppendTacticianSecondarySpecialistLines(sb, specialistActive || dualWieldActive);
                break;
            case AbilityCombatPower.TacticianEnhancementDuality:
                AppendTacticianColoredLine(sb, BuildTacticianDualityDescription(), dualityActive);
                break;
        }
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
                        sb.AppendLine("+10% Melee Poison Chance");
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
                    sb.AppendLine("Riposte: free melee auto attack on parry (no damage reduction).");
                else if (selectedChoice == 1)
                {
                    sb.Append("Parry chance increased to ");
                    sb.Append(Mathf.RoundToInt(AbilityCombatPower.ParryImprovedParryChance * 100f));
                    sb.AppendLine("%.");
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
                if (selectedChoice == AbilityCombatPower.TacticianEnhancementPerfectForm)
                    sb.AppendLine(BuildTacticianPerfectFormDescription());
                else if (selectedChoice == AbilityCombatPower.TacticianEnhancementBulwark)
                    sb.AppendLine(BuildTacticianSecondarySpecialistDescription());
                else if (selectedChoice == AbilityCombatPower.TacticianEnhancementDuality)
                    sb.AppendLine(BuildTacticianDualityDescription());
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
