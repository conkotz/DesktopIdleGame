using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Single source of truth for gathering major-passive and related HUD buff tooltip copy.
/// Gameplay code should reference these constants so tooltips stay aligned with behavior.
/// </summary>
public static class GatheringPassiveTooltipText
{
    // ---- Woodcutting Lv15 — Conservationist ----
    public const int WoodcuttingDepletionSkipBasePercent = 15;
    public const int WoodcuttingDepletionSkipDeepPercent = 10;
    public const int WoodcuttingDepletionSkipRestoreMaxEnergyPercent = 10;

    // ---- Woodcutting Lv15 — Heavy Swing ----
    public const int WoodcuttingGritExtraResourceBasePercent = 15;
    public const int WoodcuttingGritExtraResourceCrushingPercent = 5;
    public const int WoodcuttingGritBonusFindControlledPercent = 10;

    // ---- Woodcutting Lv15 — Flow State ----
    public const float WoodcuttingFlowContinuousSeconds = 15f;
    public const int WoodcuttingFlowSpeedPercent = 10;
    public const int WoodcuttingFlowStaminaEfficiencyPercent = 10;
    public const float WoodcuttingFlowLingerSeconds = 5f;
    public const int WoodcuttingFlowDeepFocusGritPercent = 10;

    // ---- Fishing Lv15 — Sustainable Catch ----
    public const int FishingDepletionSkipBasePercent = 15;
    public const int FishingDepletionSkipDeepPercent = 10;
    public const int FishingDepletionSkipRestoreMaxEnergyPercent = 10;

    // ---- Fishing Lv15 — Powered Reel ----
    public const int FishingGritExtraFishBasePercent = 15;
    public const int FishingGritExtraFishDoubleHaulPercent = 5;
    public const int FishingGritBonusFindTightLinePercent = 10;

    // ---- Fishing Lv15 — Calm Waters (major) ----
    public const float FishingCalmWatersMajorStackIntervalSeconds = 4f;
    public const int FishingCalmWatersMajorMaxStacksBase = 5;
    public const int FishingCalmWatersMajorMaxStacksDeepWaters = 7;
    public const int FishingCalmWatersMajorSpeedPerStackPercent = 2;
    public const int FishingCalmWatersMajorBonusFindPerStackPercent = 1;
    public const float FishingCalmWatersMajorLingerDecaySeconds = 2f;

    // ---- Fishing Lv5 ability — Fishing Frenzy (HUD + ability tooltips) ----
    public const float FishingFrenzyDurationSeconds = 20f;
    public const int FishingFrenzySpeedPercent = 20;
    public const int FishingFrenzyGritPercent = 10;
    public const int FishingFrenzyStaminaEfficiencyEnhancementPercent = 15;
    public const int FishingFrenzyExtraGritEnhancementPercent = 5;
    public const int FishingFrenzyChoiceSourceLevel = 5;
    public const int LumberFrenzyChoiceSourceLevel = 5;

    // ---- Woodcutting Lv5 ability — Lumber Frenzy ----
    public const int LumberFrenzySpeedPercent = 20;
    public const int LumberFrenzyGritPercent = 10;
    public const int LumberFrenzyStaminaEfficiencyEnhancementPercent = 15;
    public const int LumberFrenzyExtraGritEnhancementPercent = 5;

    public const string CalmWatersMajorTitle = "Calm Waters";
    public const string FlowStateTitle = "Flow State";

    public static int GetFishingCalmWatersMaxStacks(bool deepWatersEnhancement) =>
        deepWatersEnhancement ? FishingCalmWatersMajorMaxStacksDeepWaters : FishingCalmWatersMajorMaxStacksBase;

    public static int GetWoodcuttingDepletionSkipPercent(bool deepEnhancement) =>
        WoodcuttingDepletionSkipBasePercent + (deepEnhancement ? WoodcuttingDepletionSkipDeepPercent : 0);

    public static int GetFishingDepletionSkipPercent(bool deepEnhancement) =>
        FishingDepletionSkipBasePercent + (deepEnhancement ? FishingDepletionSkipDeepPercent : 0);

    public static int GetWoodcuttingGritExtraResourcePercent(bool crushingEnhancement) =>
        WoodcuttingGritExtraResourceBasePercent + (crushingEnhancement ? WoodcuttingGritExtraResourceCrushingPercent : 0);

    public static int GetFishingGritExtraFishPercent(bool doubleHaulEnhancement) =>
        FishingGritExtraFishBasePercent + (doubleHaulEnhancement ? FishingGritExtraFishDoubleHaulPercent : 0);

    /// <summary>Skill-tree / passive panel body for a committed major passive row.</summary>
    public static bool TryBuildSkillTreeMajorPassiveBody(
        SkillType skillType,
        string majorTitle,
        string enhancementTitle,
        out string body)
    {
        body = null;
        if (string.IsNullOrWhiteSpace(majorTitle))
            return false;

        if (skillType == SkillType.Woodcutting)
            return TryBuildWoodcuttingSkillTreeMajorBody(majorTitle, enhancementTitle, out body);

        if (skillType == SkillType.Fishing)
            return TryBuildFishingSkillTreeMajorBody(majorTitle, enhancementTitle, out body);

        return false;
    }

    /// <summary>Right-hand abilities panel effect bullets for a committed major + enhancement.</summary>
    public static void AppendMajorPassiveEffectLines(StringBuilder sb, SkillType skillType, string majorTitle, string enhancementTitle)
    {
        if (sb == null || string.IsNullOrWhiteSpace(majorTitle))
            return;

        if (skillType == SkillType.Woodcutting)
            AppendWoodcuttingMajorEffectLines(sb, majorTitle, enhancementTitle);
        else if (skillType == SkillType.Fishing)
            AppendFishingMajorEffectLines(sb, majorTitle, enhancementTitle);
    }

    /// <summary>
    /// HUD buff strip for major-passive rows that are NOT ability ids
    /// (Flow State, Calm Waters). Ability buffs use <see cref="AbilityTooltipDamagePreview.TryBuildHudBuffTooltip"/>.
    /// </summary>
    public static bool TryGetHudBuffTooltip(
        string buffId,
        int displayStacks,
        SkillsManager skillsManager,
        out string title,
        out string body)
    {
        title = null;
        body = null;
        if (string.IsNullOrWhiteSpace(buffId))
            return false;

        if (string.Equals(buffId, PlayerController.WoodcuttingFlowStateHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            title = FlowStateTitle;
            body = BuildFlowStateHudBody();
            return true;
        }

        if (string.Equals(buffId, PlayerController.FishingCalmWatersMajorHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            title = CalmWatersMajorTitle;
            body = BuildCalmWatersHudBody(displayStacks, skillsManager);
            return true;
        }

        return false;
    }

    public static string BuildCalmWatersHudBody(int currentStacks, SkillsManager skillsManager)
    {
        bool deepWaters = HasFishingLv15Enhancement(skillsManager, 2, 1);
        bool lastingWaters = HasFishingLv15Enhancement(skillsManager, 2, 0);
        int maxStacks = GetFishingCalmWatersMaxStacks(deepWaters);

        var sb = new StringBuilder();
        if (currentStacks > 0)
        {
            sb.Append("Calm stacks: ");
            sb.Append(currentStacks);
            sb.Append('/');
            sb.AppendLine(maxStacks.ToString());
            sb.AppendLine();
            int speedPct = currentStacks * FishingCalmWatersMajorSpeedPerStackPercent;
            int findPct = currentStacks * FishingCalmWatersMajorBonusFindPerStackPercent;
            sb.Append('+');
            sb.Append(speedPct);
            sb.Append("% Fishing Speed");
            sb.AppendLine();
            sb.Append('+');
            sb.Append(findPct);
            sb.AppendLine("% Bonus Find Chance");
            sb.AppendLine();
        }

        sb.Append("Each stack: +");
        sb.Append(FishingCalmWatersMajorSpeedPerStackPercent);
        sb.Append("% Fishing Speed, +");
        sb.Append(FishingCalmWatersMajorBonusFindPerStackPercent);
        sb.AppendLine("% Bonus Find Chance");
        sb.Append("(Gain 1 stack every ");
        sb.Append(FishingCalmWatersMajorStackIntervalSeconds.ToString("0.#"));
        sb.Append("s while fishing, max ");
        sb.Append(maxStacks);
        sb.Append(')');
        if (lastingWaters)
        {
            sb.AppendLine();
            sb.Append("After fishing stops: lose 1 stack every ");
            sb.Append(FishingCalmWatersMajorLingerDecaySeconds.ToString("0.#"));
            sb.Append('s');
        }

        return sb.ToString();
    }

    public static string BuildFlowStateHudBody()
    {
        var sb = new StringBuilder();
        sb.Append('+');
        sb.Append(WoodcuttingFlowSpeedPercent);
        sb.Append("% Chopping Speed");
        sb.AppendLine();
        sb.Append('+');
        sb.Append(WoodcuttingFlowStaminaEfficiencyPercent);
        sb.Append("% Stamina Efficiency");
        return sb.ToString();
    }

    public static string BuildFishingFrenzyHudBody(SkillsManager skillsManager)
    {
        var sb = new StringBuilder();
        AppendFishingFrenzyEffectLines(sb, skillsManager);
        sb.AppendLine();
        sb.Append("Duration: ");
        sb.Append(FishingFrenzyDurationSeconds.ToString("0.#"));
        sb.Append('s');
        return sb.ToString();
    }

    public static string BuildLumberFrenzyHudBody(SkillsManager skillsManager)
    {
        var sb = new StringBuilder();
        AppendLumberFrenzyEffectLines(sb, skillsManager);
        return sb.ToString();
    }

    public static void AppendFishingFrenzyEffectLines(StringBuilder sb, SkillsManager skillsManager)
    {
        int choice = GetFishingFrenzyChoice(skillsManager);
        float gritTotal = FishingFrenzyGritPercent + (choice == 1 ? FishingFrenzyExtraGritEnhancementPercent : 0);

        sb.Append('+');
        sb.Append(FishingFrenzySpeedPercent);
        sb.AppendLine("% Fishing Speed");
        sb.Append('+');
        sb.Append(gritTotal.ToString("0.#"));
        sb.AppendLine("% Fishing Grit Chance");
        if (choice == 0)
        {
            sb.Append('+');
            sb.Append(FishingFrenzyStaminaEfficiencyEnhancementPercent);
            sb.AppendLine("% Fishing Stamina Efficiency");
        }
    }

    public static void AppendLumberFrenzyEffectLines(StringBuilder sb, SkillsManager skillsManager)
    {
        int choice = GetLumberFrenzyChoice(skillsManager);
        float gritTotal = LumberFrenzyGritPercent + (choice == 1 ? LumberFrenzyExtraGritEnhancementPercent : 0);

        sb.Append('+');
        sb.Append(LumberFrenzySpeedPercent);
        sb.AppendLine("% Woodcutting Speed");
        sb.Append('+');
        sb.Append(gritTotal.ToString("0.#"));
        sb.AppendLine("% Woodcutting Grit Chance");
        if (choice == 0)
        {
            sb.Append('+');
            sb.Append(LumberFrenzyStaminaEfficiencyEnhancementPercent);
            sb.AppendLine("% Woodcutting Stamina Efficiency");
        }
    }

    private static bool TryBuildWoodcuttingSkillTreeMajorBody(string majorTitle, string enhancementTitle, out string body)
    {
        body = null;
        if (string.Equals(majorTitle, "Conservationist", StringComparison.OrdinalIgnoreCase))
        {
            bool sustainable = Eq(enhancementTitle, "Sustainable Harvest");
            bool ancient = Eq(enhancementTitle, "Ancient Preservation");
            var sb = new StringBuilder();
            sb.AppendLine("Successful chops have a chance to not count toward tree depletion.");
            sb.AppendLine();
            sb.Append('+');
            sb.Append(GetWoodcuttingDepletionSkipPercent(ancient));
            sb.Append("% Tree Depletion Skip Chance");
            if (sustainable)
            {
                sb.AppendLine();
                sb.Append('+');
                sb.Append(WoodcuttingDepletionSkipRestoreMaxEnergyPercent);
                sb.Append("% Max Stamina restored when tree depletion is skipped");
            }
            body = sb.ToString();
            return true;
        }

        if (string.Equals(majorTitle, "Heavy Swing", StringComparison.OrdinalIgnoreCase))
        {
            bool controlled = Eq(enhancementTitle, "Controlled Force");
            bool crushing = Eq(enhancementTitle, "Crushing Swing");
            var sb = new StringBuilder();
            sb.AppendLine("When Woodcutting Grit procs:");
            sb.AppendLine();
            sb.Append('+');
            sb.Append(GetWoodcuttingGritExtraResourcePercent(crushing));
            sb.Append("% Extra Resource Chance");
            if (controlled)
            {
                sb.AppendLine();
                sb.Append('+');
                sb.Append(WoodcuttingGritBonusFindControlledPercent);
                sb.Append("% Bonus Find Chance when Woodcutting Grit procs");
            }
            body = sb.ToString();
            return true;
        }

        if (string.Equals(majorTitle, FlowStateTitle, StringComparison.OrdinalIgnoreCase))
        {
            bool lasting = Eq(enhancementTitle, "Lasting Focus");
            bool deep = Eq(enhancementTitle, "Deep Focus");
            var sb = new StringBuilder();
            sb.Append("After ");
            sb.Append(WoodcuttingFlowContinuousSeconds.ToString("0.#"));
            sb.AppendLine(" seconds of continuous woodcutting on the same tree, you enter Flow State.");
            sb.AppendLine();
            sb.Append('+');
            sb.Append(WoodcuttingFlowSpeedPercent);
            sb.AppendLine("% Chopping Speed while Flow is active");
            sb.Append('+');
            sb.Append(WoodcuttingFlowStaminaEfficiencyPercent);
            sb.Append("% Stamina Efficiency while Flow is active");
            if (lasting)
            {
                sb.AppendLine();
                sb.Append("Flow lasts ");
                sb.Append(WoodcuttingFlowLingerSeconds.ToString("0.#"));
                sb.Append(" seconds after you stop gathering");
            }
            if (deep)
            {
                sb.AppendLine();
                sb.Append('+');
                sb.Append(WoodcuttingFlowDeepFocusGritPercent);
                sb.Append("% Woodcutting Grit Chance while Flow is active");
            }
            body = sb.ToString();
            return true;
        }

        return false;
    }

    private static bool TryBuildFishingSkillTreeMajorBody(string majorTitle, string enhancementTitle, out string body)
    {
        body = null;
        if (string.Equals(majorTitle, "Sustainable Catch", StringComparison.OrdinalIgnoreCase))
        {
            bool tidal = Eq(enhancementTitle, "Tidal Recovery");
            bool deepRuns = Eq(enhancementTitle, "Deep Runs");
            var sb = new StringBuilder();
            sb.AppendLine("Successful catches have a chance to not count toward spot depletion.");
            sb.AppendLine();
            sb.Append('+');
            sb.Append(GetFishingDepletionSkipPercent(deepRuns));
            sb.Append("% Spot Depletion Skip Chance");
            if (tidal)
            {
                sb.AppendLine();
                sb.Append('+');
                sb.Append(FishingDepletionSkipRestoreMaxEnergyPercent);
                sb.Append("% Max Stamina restored when a spot depletion skip triggers");
            }
            body = sb.ToString();
            return true;
        }

        if (string.Equals(majorTitle, "Powered Reel", StringComparison.OrdinalIgnoreCase))
        {
            bool tightLine = Eq(enhancementTitle, "Tight Line");
            bool doubleHaul = Eq(enhancementTitle, "Double Haul");
            var sb = new StringBuilder();
            sb.AppendLine("When Fishing Grit procs:");
            sb.AppendLine();
            sb.Append('+');
            sb.Append(GetFishingGritExtraFishPercent(doubleHaul));
            sb.Append("% Extra Fish Chance");
            if (tightLine)
            {
                sb.AppendLine();
                sb.Append('+');
                sb.Append(FishingGritBonusFindTightLinePercent);
                sb.Append("% Bonus Find Chance when Fishing Grit procs");
            }
            body = sb.ToString();
            return true;
        }

        if (string.Equals(majorTitle, CalmWatersMajorTitle, StringComparison.OrdinalIgnoreCase))
        {
            bool lasting = Eq(enhancementTitle, "Lasting Waters");
            bool deep = Eq(enhancementTitle, "Deep Waters");
            int maxStacks = GetFishingCalmWatersMaxStacks(deep);
            var sb = new StringBuilder();
            sb.Append("Gain 1 Calm stack every ");
            sb.Append(FishingCalmWatersMajorStackIntervalSeconds.ToString("0.#"));
            sb.Append(" seconds while fishing the same spot (up to ");
            sb.Append(maxStacks);
            sb.AppendLine(").");
            sb.AppendLine();
            sb.Append('+');
            sb.Append(FishingCalmWatersMajorSpeedPerStackPercent);
            sb.AppendLine("% Fishing Speed per Calm stack");
            sb.Append('+');
            sb.Append(FishingCalmWatersMajorBonusFindPerStackPercent);
            sb.Append("% Bonus Find Chance per Calm stack");
            if (lasting)
            {
                sb.AppendLine();
                sb.Append("Lasting Waters: lose 1 Calm stack every ");
                sb.Append(FishingCalmWatersMajorLingerDecaySeconds.ToString("0.#"));
                sb.Append(" seconds after you stop fishing");
            }
            body = sb.ToString();
            return true;
        }

        return false;
    }

    private static void AppendWoodcuttingMajorEffectLines(StringBuilder sb, string majorTitle, string enhancementTitle)
    {
        if (string.Equals(majorTitle, "Conservationist", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("     +");
            sb.Append(GetWoodcuttingDepletionSkipPercent(Eq(enhancementTitle, "Ancient Preservation")));
            sb.AppendLine("% Tree Depletion Skip Chance");
            if (Eq(enhancementTitle, "Sustainable Harvest"))
                sb.AppendLine($"     +{WoodcuttingDepletionSkipRestoreMaxEnergyPercent}% Max Stamina restored when tree depletion is skipped");
            return;
        }

        if (string.Equals(majorTitle, "Heavy Swing", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("     +");
            sb.Append(GetWoodcuttingGritExtraResourcePercent(Eq(enhancementTitle, "Crushing Swing")));
            sb.AppendLine("% Extra Resource Chance when Woodcutting Grit procs");
            if (Eq(enhancementTitle, "Controlled Force"))
                sb.AppendLine($"     +{WoodcuttingGritBonusFindControlledPercent}% Bonus Find Chance when Woodcutting Grit procs");
            return;
        }

        if (string.Equals(majorTitle, FlowStateTitle, StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"     +{WoodcuttingFlowSpeedPercent}% Chopping Speed while Flow is active");
            sb.AppendLine($"     +{WoodcuttingFlowStaminaEfficiencyPercent}% Stamina Efficiency while Flow is active");
            if (Eq(enhancementTitle, "Lasting Focus"))
            {
                sb.Append("     Flow lasts ");
                sb.Append(WoodcuttingFlowLingerSeconds.ToString("0.#"));
                sb.AppendLine(" seconds after you stop gathering");
            }
            else if (Eq(enhancementTitle, "Deep Focus"))
                sb.AppendLine($"     +{WoodcuttingFlowDeepFocusGritPercent}% Woodcutting Grit Chance while Flow is active");
        }
    }

    private static void AppendFishingMajorEffectLines(StringBuilder sb, string majorTitle, string enhancementTitle)
    {
        if (string.Equals(majorTitle, "Sustainable Catch", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("     +");
            sb.Append(GetFishingDepletionSkipPercent(Eq(enhancementTitle, "Deep Runs")));
            sb.AppendLine("% Spot Depletion Skip Chance");
            if (Eq(enhancementTitle, "Tidal Recovery"))
                sb.AppendLine($"     +{FishingDepletionSkipRestoreMaxEnergyPercent}% Max Stamina restored when a spot depletion skip triggers");
            return;
        }

        if (string.Equals(majorTitle, "Powered Reel", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("     +");
            sb.Append(GetFishingGritExtraFishPercent(Eq(enhancementTitle, "Double Haul")));
            sb.AppendLine("% Extra Fish Chance when Fishing Grit procs");
            if (Eq(enhancementTitle, "Tight Line"))
                sb.AppendLine($"     +{FishingGritBonusFindTightLinePercent}% Bonus Find Chance when Fishing Grit procs");
            return;
        }

        if (string.Equals(majorTitle, CalmWatersMajorTitle, StringComparison.OrdinalIgnoreCase))
        {
            int maxStacks = GetFishingCalmWatersMaxStacks(Eq(enhancementTitle, "Deep Waters"));
            sb.Append("     Gain Calm stacks every ");
            sb.Append(FishingCalmWatersMajorStackIntervalSeconds.ToString("0.#"));
            sb.Append("s while fishing (max ");
            sb.Append(maxStacks);
            sb.AppendLine(")");
            sb.AppendLine($"     +{FishingCalmWatersMajorSpeedPerStackPercent}% Fishing Speed per Calm stack");
            sb.AppendLine($"     +{FishingCalmWatersMajorBonusFindPerStackPercent}% Bonus Find Chance per Calm stack");
            if (Eq(enhancementTitle, "Lasting Waters"))
            {
                sb.Append("     Lose 1 Calm stack every ");
                sb.Append(FishingCalmWatersMajorLingerDecaySeconds.ToString("0.#"));
                sb.AppendLine("s after you stop fishing");
            }
        }
    }

    private static int GetFishingFrenzyChoice(SkillsManager sm) =>
        sm != null ? sm.GetSkillChoiceSelection(SkillType.Fishing, FishingFrenzyChoiceSourceLevel, -1) : -1;

    private static int GetLumberFrenzyChoice(SkillsManager sm) =>
        sm != null ? sm.GetSkillChoiceSelection(SkillType.Woodcutting, LumberFrenzyChoiceSourceLevel, -1) : -1;

    private static bool HasFishingLv15Enhancement(SkillsManager sm, int rowPick, int choiceIndex)
    {
        if (sm == null)
            return false;
        if (sm.GetSkillAbilityRowPick(SkillType.Fishing, PlayerController.FishingMajorPassiveSourceLevel, -1) != rowPick)
            return false;
        return sm.GetSkillChoiceSelection(SkillType.Fishing, PlayerController.FishingLevel15ChoiceSpineId(rowPick), -1) == choiceIndex;
    }

    private static bool Eq(string a, string b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
