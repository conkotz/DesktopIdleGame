using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Skill-tree and HUD copy for Melee major passives (Lv10 / Lv20 / Lv30).
/// Gameplay constants live on <see cref="CharacterStats"/> and <see cref="AbilityCombatPower"/>.
/// </summary>
public static class MeleeMajorPassiveTooltipText
{
    public const int AilmentAttunementMajorPassiveLevel = 10;
    public const string BattleEngineOverloadTitle = "Overload";

    public static bool TryBuildSkillTreeBody(int majorPassiveLevel, int selectedChoice, out string body)
    {
        body = null;
        switch (majorPassiveLevel)
        {
            case AilmentAttunementMajorPassiveLevel:
                return TryBuildAilmentAttunementBody(selectedChoice, out body);
            case CharacterStats.PredatorsInstinctMajorPassiveLevel:
                return TryBuildPredatorsInstinctBody(selectedChoice, out body);
            case CharacterStats.BattleEngineMajorPassiveLevel:
                return TryBuildBattleEngineBody(selectedChoice, out body);
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
        AppendEnhancementLines(sb, selectedChoice, 10);
        body = sb.ToString();
        return true;
    }

    private static bool TryBuildPredatorsInstinctBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine("+5% Critical Chance");
        sb.AppendLine("+10% Critical Damage");
        AppendEnhancementLines(sb, selectedChoice, 20);
        body = sb.ToString();
        return true;
    }

    private static bool TryBuildBattleEngineBody(int selectedChoice, out string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Gain 5 Energy when abilities hit enemies (once per cast).");
        AppendEnhancementLines(sb, selectedChoice, 30);
        body = sb.ToString();
        return true;
    }

    private static void AppendEnhancementLines(StringBuilder sb, int selectedChoice, int majorLevel)
    {
        if (sb == null || selectedChoice < 0)
            return;

        if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
            sb.AppendLine();

        switch (majorLevel)
        {
            case AilmentAttunementMajorPassiveLevel:
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

            case CharacterStats.PredatorsInstinctMajorPassiveLevel:
                if (selectedChoice == 0)
                    sb.AppendLine("+30% Critical Damage vs enemies below 30% HP");
                else if (selectedChoice == 1)
                    sb.AppendLine("+10% Attack Speed for 7 seconds on critical hit");
                break;

            case CharacterStats.BattleEngineMajorPassiveLevel:
                if (selectedChoice == 0)
                    sb.AppendLine("Using an ability lowers your other cooldowns by 0.5 seconds.");
                else if (selectedChoice == 1)
                    sb.AppendLine("Using an ability adds +10% ability cost and +5% ability damage per stack (max 5 stacks, 10s).");
                break;
        }
    }
}
