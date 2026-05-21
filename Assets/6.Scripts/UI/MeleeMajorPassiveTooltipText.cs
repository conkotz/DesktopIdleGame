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

    /// <summary>Skill-tree body from parent spine id (e.g. Lv40_0, Lv40_1).</summary>
    public static bool TryBuildSkillTreeBody(string parentSpineNodeId, int selectedChoice, out string body)
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
                return TryBuildTacticianBody(selectedChoice, out body);
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

        if (!string.Equals(buffId, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase))
            return false;

        title = BattleEngineOverloadTitle;
        body = BuildOverloadHudBody(Mathf.Max(0, displayStacks));
        return true;
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
            if (choiceIndex == 0)
            {
                body =
                    "While wielding a two-handed weapon (replaces one-handed bonuses):\n" +
                    "+15% bleed multiplier\n" +
                    "25% armour penetration\n" +
                    "25% chance to stun enemies\n" +
                    "10% chance to block attacks";
                return true;
            }

            if (choiceIndex == 1)
            {
                body =
                    "While a shield is equipped in the off hand (adds):\n" +
                    "+5% block chance\n" +
                    "+10 armour, magic resist, and corruption resist";
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

    private static bool TryBuildTacticianBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine("While wielding a one-handed weapon:");
        sb.Append("+");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.TacticianOneHandedAttackSpeedPercent * 100f));
        sb.AppendLine("% attack speed");
        sb.Append("+");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.TacticianOneHandedPoisonChance * 100f));
        sb.Append(" poison and ");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.TacticianOneHandedBurnChance * 100f));
        sb.AppendLine(" burn chance");
        sb.Append("+");
        sb.Append(Mathf.RoundToInt(AbilityCombatPower.TacticianOneHandedCritChance * 100f));
        sb.AppendLine("% critical strike chance");
        AppendEnhancementLines(sb, selectedChoice, AbilityCombatPower.TacticianMajorPassiveSpineNodeId);
        body = sb.ToString();
        return true;
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
                if (selectedChoice == 0)
                {
                    sb.AppendLine("Two-Handed: +15% bleed multiplier, 25% armour penetration, 25% stun chance, 10% block.");
                }
                else if (selectedChoice == 1)
                {
                    sb.AppendLine("Shield: +5% block, +10 armour, magic resist, and corruption resist.");
                }
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
