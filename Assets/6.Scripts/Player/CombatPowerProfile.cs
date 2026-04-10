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

/// <summary>Mitigation / pool stats used to split defense profiles (Armoured vs Warded vs Tank).</summary>
public readonly struct CombatProfileDefenseHints
{
    public float EffectiveHpVsPhysical { get; }
    public float EffectiveHpVsMagical { get; }
    public float EffectiveHpVsCorruption { get; }
    public int Armor { get; }
    public int MagicResist { get; }
    public int MaxHP { get; }
    /// <summary>Same value as CP mobility input (<see cref="CharacterStats.GetMoveSpeedForCombatPower"/>).</summary>
    public float FinalMoveSpeed { get; }

    public CombatProfileDefenseHints(
        float effectiveHpVsPhysical,
        float effectiveHpVsMagical,
        float effectiveHpVsCorruption,
        int armor,
        int magicResist,
        int maxHp,
        float finalMoveSpeed)
    {
        EffectiveHpVsPhysical = effectiveHpVsPhysical;
        EffectiveHpVsMagical = effectiveHpVsMagical;
        EffectiveHpVsCorruption = effectiveHpVsCorruption;
        Armor = armor;
        MagicResist = magicResist;
        MaxHP = maxHp;
        FinalMoveSpeed = finalMoveSpeed;
    }
}

/// <summary>Display strings for combat profile labels (HUD / tooltips).</summary>
public static class CombatProfileLabel
{
    public const string GlassCannon = "Glass Cannon";
    public const string Deadly = "Deadly";
    public const string Relentless = "Relentless";
    public const string Armoured = "Armoured";
    public const string Warded = "Warded";
    public const string Tank = "Tank";
    public const string Sustaining = "Sustaining";
    public const string Nimble = "Nimble";
    public const string Bruiser = "Bruiser";
    public const string Balanced = "Balanced";
}

/// <summary>Tunable thresholds for <see cref="CombatProfileClassifier.Classify"/>.</summary>
public static class CombatProfileThresholds
{
    public const float GlassCannonOffenseMin = 0.48f;
    public const float GlassCannonDefenseMax = 0.22f;
    public const float GlassCannonSustainMax = 0.20f;

    public const float DeadlyOffenseMin = 0.36f;
    public const float DeadlyDefenseMax = 0.33f;

    public const float RelentlessOffenseMin = 0.30f;
    public const float RelentlessMobilityMin = 0.14f;
    public const float RelentlessOffenseMobilitySumMin = 0.48f;

    /// <summary>Required move speed (enemy <see cref="EnemyBaseController.MoveSpeed"/> or player <see cref="CharacterStats.FinalMoveSpeed"/>) for any Relentless result.</summary>
    public const float RelentlessMinFinalMoveSpeed = 3f;
    /// <summary>When <see cref="RelentlessMinFinalMoveSpeed"/> is met, allow slightly lower offense CP %.</summary>
    public const float RelentlessOffenseMinWhenFast = 0.26f;

    /// <summary>Minimum defense CP share before we classify into Armoured / Warded / Tank.</summary>
    public const float DefenseProfileMin = 0.26f;

    public const int ArmouredMinArmor = 10;
    public const float ArmouredPhysEhpOverCorruptionMin = 1.12f;

    public const int WardedMinMagicResist = 10;
    public const float WardedMagEhpOverCorruptionMin = 1.12f;

    public const int TankMinMaxHp = 35;

    /// <summary>
    /// Max ratio of physical (or magical) effective HP vs corruption EHP for "low mitigation" Tank identity.
    /// ~10% phys block alone yields ~1.11; keep above that so block-only dummies still qualify as Tank.
    /// </summary>
    public const float TankMitigationEhpOverCorruptionMax = 1.15f;

    public const float SustainingMin = 0.32f;

    public const float NimbleMin = 0.28f;

    public const float BruiserOffenseMin = 0.24f;
    public const float BruiserDefenseMin = 0.24f;
    public const float BruiserCombinedMin = 0.54f;
}

/// <summary>
/// Classifies a unit by CP distribution and defensive identity. Priority: Relentless → Glass Cannon → Deadly →
/// Tank (HP-forward) → Armoured → Warded → Tank → Sustaining → Nimble → Bruiser → Balanced.
/// </summary>
public static class CombatProfileClassifier
{
    public static string Classify(CombatPowerBreakdown b, CombatProfileDefenseHints d)
    {
        float t = b.TotalCombatPower;
        if (t <= 1e-4f)
            return CombatProfileLabel.Balanced;

        float pO = b.Offense / t;
        float pD = b.Defense / t;
        float pS = b.Sustain / t;
        float pMob = b.Mobility / t;

        float eCorr = Mathf.Max(1f, d.EffectiveHpVsCorruption);
        float physOverCorruption = d.EffectiveHpVsPhysical / eCorr;
        float magOverCorruption = d.EffectiveHpVsMagical / eCorr;

        // 1 Relentless — must meet min move speed (enemy inspector / player FinalMoveSpeed), then either
        //    strong offense+mobility CP split OR fast+decent offense. Checked before Glass Cannon so fast strikers
        //    are not labelled Glass Cannon solely from high offense share.
        bool relentlessSpeedOk = d.FinalMoveSpeed >= CombatProfileThresholds.RelentlessMinFinalMoveSpeed;
        bool relentlessByCpShares = relentlessSpeedOk
                                    && pO >= CombatProfileThresholds.RelentlessOffenseMin
                                    && pMob >= CombatProfileThresholds.RelentlessMobilityMin
                                    && (pO + pMob) >= CombatProfileThresholds.RelentlessOffenseMobilitySumMin;
        bool relentlessBySpeed = relentlessSpeedOk
                                 && pO >= CombatProfileThresholds.RelentlessOffenseMinWhenFast;
        if (relentlessByCpShares || relentlessBySpeed)
            return CombatProfileLabel.Relentless;

        // 2 Glass Cannon — high offense, low defense & sustain (slower units only reach here if not Relentless)
        if (pO >= CombatProfileThresholds.GlassCannonOffenseMin
            && pD <= CombatProfileThresholds.GlassCannonDefenseMax
            && pS <= CombatProfileThresholds.GlassCannonSustainMax)
            return CombatProfileLabel.GlassCannon;

        // 3 Deadly — offense-led, defense still relatively low
        if (pO >= CombatProfileThresholds.DeadlyOffenseMin
            && pD <= CombatProfileThresholds.DeadlyDefenseMax
            && pO > pD)
            return CombatProfileLabel.Deadly;

        // 3b Tank (HP pool) — large MaxHP with flat mitigation when offense CP dominates (pD below defense threshold).
        // Without this, high-HP training dummies / punch bags read as Balanced because defense share looks small vs DPS CP.
        if (pD < CombatProfileThresholds.DefenseProfileMin)
        {
            bool lowMitigation = physOverCorruption <= CombatProfileThresholds.TankMitigationEhpOverCorruptionMax
                                 && magOverCorruption <= CombatProfileThresholds.TankMitigationEhpOverCorruptionMax;
            if (lowMitigation
                && d.MaxHP >= CombatProfileThresholds.TankMinMaxHp
                && d.Armor < CombatProfileThresholds.ArmouredMinArmor
                && d.MagicResist < CombatProfileThresholds.WardedMinMagicResist)
                return CombatProfileLabel.Tank;
        }

        // 4–6 Defense identities (defense must matter in CP)
        if (pD >= CombatProfileThresholds.DefenseProfileMin)
        {
            // 4 Armoured — armour rating / physical mitigation drives durability
            bool armourDriven = d.Armor >= CombatProfileThresholds.ArmouredMinArmor
                              || physOverCorruption >= CombatProfileThresholds.ArmouredPhysEhpOverCorruptionMin;
            if (armourDriven && physOverCorruption >= magOverCorruption - 0.02f)
                return CombatProfileLabel.Armoured;

            // 5 Warded — magic resist drives durability
            bool wardDriven = d.MagicResist >= CombatProfileThresholds.WardedMinMagicResist
                              || magOverCorruption >= CombatProfileThresholds.WardedMagEhpOverCorruptionMin;
            if (wardDriven && magOverCorruption >= physOverCorruption - 0.02f)
                return CombatProfileLabel.Warded;

            // 6 Tank — large HP pool; mitigation from armour/MR is not the main story
            bool lowMitigation = physOverCorruption <= CombatProfileThresholds.TankMitigationEhpOverCorruptionMax
                                 && magOverCorruption <= CombatProfileThresholds.TankMitigationEhpOverCorruptionMax;
            if (lowMitigation && d.MaxHP >= CombatProfileThresholds.TankMinMaxHp)
                return CombatProfileLabel.Tank;
        }

        // 7 Sustaining
        if (pS >= CombatProfileThresholds.SustainingMin && pS >= pO && pS >= pD && pS >= pMob)
            return CombatProfileLabel.Sustaining;

        // 8 Nimble
        if (pMob >= CombatProfileThresholds.NimbleMin && pMob >= pO && pMob >= pD && pMob >= pS)
            return CombatProfileLabel.Nimble;

        // 9 Bruiser — both offense and defense substantial
        if (pO >= CombatProfileThresholds.BruiserOffenseMin
            && pD >= CombatProfileThresholds.BruiserDefenseMin
            && (pO + pD) >= CombatProfileThresholds.BruiserCombinedMin)
            return CombatProfileLabel.Bruiser;

        // 10 Balanced
        return CombatProfileLabel.Balanced;
    }

    public static Color GetColorForLabel(string label)
    {
        if (label == CombatProfileLabel.Deadly)
            return new Color(0.95f, 0.22f, 0.22f);

        if (label == CombatProfileLabel.GlassCannon)
            return new Color(1f, 0.38f, 0.18f);

        if (label == CombatProfileLabel.Relentless)
            return new Color(0.92f, 0.28f, 0.55f);

        if (label == CombatProfileLabel.Armoured)
            return new Color(0.58f, 0.58f, 0.62f);

        if (label == CombatProfileLabel.Warded)
            return new Color(0.55f, 0.45f, 0.85f);

        if (label == CombatProfileLabel.Tank)
            return new Color(0.45f, 0.72f, 0.48f);

        if (label == CombatProfileLabel.Sustaining)
            return new Color(0.38f, 0.82f, 0.42f);

        if (label == CombatProfileLabel.Nimble)
            return new Color(0.42f, 0.92f, 0.95f);

        if (label == CombatProfileLabel.Bruiser)
            return new Color(1f, 0.58f, 0.18f);

        return Color.white;
    }

    public static string BuildDebugSummary(CombatPowerBreakdown b, string label, CombatProfileDefenseHints d)
    {
        b.GetPercentages(out float pO, out float pD, out float pS, out float pMob);
        float eCorr = Mathf.Max(1f, d.EffectiveHpVsCorruption);
        return
            $"Combat profile: {label}\n" +
            $"Total CP: {b.TotalCombatPower:0.##}\n" +
            $"Offense: {b.Offense:0.##} ({pO * 100f:0.#}%)\n" +
            $"Defense: {b.Defense:0.##} ({pD * 100f:0.#}%)\n" +
            $"Sustain: {b.Sustain:0.##} ({pS * 100f:0.#}%)\n" +
            $"Mobility: {b.Mobility:0.##} ({pMob * 100f:0.#}%)\n" +
            $"Armor: {d.Armor} | MR: {d.MagicResist} | MaxHP: {d.MaxHP} | Move: {d.FinalMoveSpeed:0.##}\n" +
            $"EHP phys/corr: {d.EffectiveHpVsPhysical / eCorr:0.##} | mag/corr: {d.EffectiveHpVsMagical / eCorr:0.##}";
    }
}
