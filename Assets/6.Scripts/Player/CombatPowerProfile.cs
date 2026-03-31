using UnityEngine;

/// <summary>
/// Raw bucket values that sum to total combat power (same composition as <see cref="CharacterStats.CombatPower"/>).
/// </summary>
public readonly struct CombatPowerBreakdown
{
    public float Offense { get; }
    public float Defense { get; }
    public float Sustain { get; }
    public float Mobility { get; }
    public float TotalCombatPower { get; }

    public CombatPowerBreakdown(float offense, float defense, float sustain, float mobility)
    {
        Offense = offense;
        Defense = defense;
        Sustain = sustain;
        Mobility = mobility;
        TotalCombatPower = offense + defense + sustain + mobility;
    }

    public void GetPercentages(out float offensePct, out float defensePct, out float sustainPct, out float mobilityPct)
    {
        float t = TotalCombatPower;
        if (t <= 1e-6f)
        {
            offensePct = defensePct = sustainPct = mobilityPct = 0.25f;
            return;
        }

        offensePct = Offense / t;
        defensePct = Defense / t;
        sustainPct = Sustain / t;
        mobilityPct = Mobility / t;
    }
}

/// <summary>Display strings for combat profile labels (HUD / tooltips).</summary>
public static class CombatProfileLabel
{
    public const string GlassCannon = "Glass Cannon";
    public const string Deadly = "Deadly";
    public const string Armoured = "Armoured";
    public const string Sustaining = "Sustaining";
    public const string Nimble = "Nimble";
    public const string Bruiser = "Bruiser";
    public const string Balanced = "Balanced";
}

/// <summary>Tunable thresholds for <see cref="CombatProfileClassifier.Classify"/> (ratio of CP, 0–1).</summary>
public static class CombatProfileThresholds
{
    public const float GlassCannonOffenseMin = 0.48f;
    public const float GlassCannonDefenseMax = 0.22f;
    public const float GlassCannonSustainMax = 0.20f;

    public const float DeadlyOffenseMin = 0.36f;
    public const float DeadlyDefenseMax = 0.33f;

    public const float ArmouredDefenseMin = 0.36f;

    public const float SustainingMin = 0.32f;

    public const float NimbleMin = 0.28f;

    public const float BruiserOffenseMin = 0.24f;
    public const float BruiserDefenseMin = 0.24f;
    public const float BruiserCombinedMin = 0.54f;
}

/// <summary>
/// Classifies a unit by how CP is distributed. Evaluates rules in priority order (Glass Cannon → … → Balanced).
/// </summary>
public static class CombatProfileClassifier
{
    public static string Classify(CombatPowerBreakdown b)
    {
        float t = b.TotalCombatPower;
        if (t <= 1e-4f)
            return CombatProfileLabel.Balanced;

        float pO = b.Offense / t;
        float pD = b.Defense / t;
        float pS = b.Sustain / t;
        float pM = b.Mobility / t;

        // 1 Glass Cannon — high offense, low defense & sustain
        if (pO >= CombatProfileThresholds.GlassCannonOffenseMin
            && pD <= CombatProfileThresholds.GlassCannonDefenseMax
            && pS <= CombatProfileThresholds.GlassCannonSustainMax)
            return CombatProfileLabel.GlassCannon;

        // 2 Deadly — offense-led, defense still relatively low
        if (pO >= CombatProfileThresholds.DeadlyOffenseMin
            && pD <= CombatProfileThresholds.DeadlyDefenseMax
            && pO > pD)
            return CombatProfileLabel.Deadly;

        // 3 Armoured — defense is the largest share and above floor
        if (pD >= CombatProfileThresholds.ArmouredDefenseMin && pD >= pO && pD >= pS && pD >= pM)
            return CombatProfileLabel.Armoured;

        // 4 Sustaining
        if (pS >= CombatProfileThresholds.SustainingMin && pS >= pO && pS >= pD && pS >= pM)
            return CombatProfileLabel.Sustaining;

        // 5 Nimble
        if (pM >= CombatProfileThresholds.NimbleMin && pM >= pO && pM >= pD && pM >= pS)
            return CombatProfileLabel.Nimble;

        // 6 Bruiser — both offense and defense substantial
        if (pO >= CombatProfileThresholds.BruiserOffenseMin
            && pD >= CombatProfileThresholds.BruiserDefenseMin
            && (pO + pD) >= CombatProfileThresholds.BruiserCombinedMin)
            return CombatProfileLabel.Bruiser;

        // 7 Balanced
        return CombatProfileLabel.Balanced;
    }

    public static Color GetColorForLabel(string label)
    {
        if (label == CombatProfileLabel.Deadly)
            return new Color(0.95f, 0.22f, 0.22f);

        if (label == CombatProfileLabel.GlassCannon)
            return new Color(1f, 0.38f, 0.18f);

        if (label == CombatProfileLabel.Armoured)
            return new Color(0.58f, 0.58f, 0.62f);

        if (label == CombatProfileLabel.Sustaining)
            return new Color(0.38f, 0.82f, 0.42f);

        if (label == CombatProfileLabel.Nimble)
            return new Color(0.42f, 0.92f, 0.95f);

        if (label == CombatProfileLabel.Bruiser)
            return new Color(1f, 0.58f, 0.18f);

        return Color.white;
    }

    public static string BuildDebugSummary(CombatPowerBreakdown b, string label)
    {
        b.GetPercentages(out float pO, out float pD, out float pS, out float pM);
        return
            $"Combat profile: {label}\n" +
            $"Total CP: {b.TotalCombatPower:0.##}\n" +
            $"Offense: {b.Offense:0.##} ({pO * 100f:0.#}%)\n" +
            $"Defense: {b.Defense:0.##} ({pD * 100f:0.#}%)\n" +
            $"Sustain: {b.Sustain:0.##} ({pS * 100f:0.#}%)\n" +
            $"Mobility: {b.Mobility:0.##} ({pM * 100f:0.#}%)";
    }
}
