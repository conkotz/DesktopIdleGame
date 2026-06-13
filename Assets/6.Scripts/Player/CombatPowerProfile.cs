using System.Collections.Generic;
using System.Text;
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
    public float EffectiveHpVsMagic { get; }
    public float EffectiveHpVsCorruption { get; }
    public int Armor { get; }
    public int MagicResist { get; }
    public int CorruptionResist { get; }
    public int MaxHP { get; }
    /// <summary>Max HP used for Tank identity (definition base for enemies; excludes map combat scaling).</summary>
    public int IdentityMaxHp { get; }
    /// <summary>Same value as CP mobility input (<see cref="CharacterStats.GetMoveSpeedForCombatPower"/>).</summary>
    public float FinalMoveSpeed { get; }

    public CombatProfileDefenseHints(
        float effectiveHpVsPhysical,
        float effectiveHpVsMagic,
        float effectiveHpVsCorruption,
        int armor,
        int magicResist,
        int corruptionResist,
        int maxHp,
        int identityMaxHp,
        float finalMoveSpeed)
    {
        EffectiveHpVsPhysical = effectiveHpVsPhysical;
        EffectiveHpVsMagic = effectiveHpVsMagic;
        EffectiveHpVsCorruption = effectiveHpVsCorruption;
        Armor = armor;
        MagicResist = magicResist;
        CorruptionResist = corruptionResist;
        MaxHP = maxHp;
        IdentityMaxHp = identityMaxHp;
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
    public const string Shrouded = "Shrouded";
    public const string Tank = "Tank";
    public const string Sustaining = "Sustaining";
    public const string Nimble = "Nimble";
    public const string Bruiser = "Bruiser";
    public const string Balanced = "Balanced";
}

/// <summary>Ailment tags appended to combat profiles when apply chance is at or above 1%.</summary>
public static class CombatProfileAilmentLabel
{
    public const float MinApplyChance = 0.01f;

    public const string Poisonous = "Poisonous";
    public const string Sanguine = "Sanguine";
    public const string Electrified = "Electrified";
    public const string Fiery = "Fiery";
    public const string Frosted = "Frosted";
}

/// <summary>Builds display labels like "Deadly - Poisonous" for UI and database rows.</summary>
public static class CombatProfileDisplay
{
    private static readonly List<string> AilmentSuffixScratch = new List<string>(5);

    public static string FormatWithAilments(string baseProfile, IReadOnlyList<string> ailmentSuffixes)
    {
        if (string.IsNullOrWhiteSpace(baseProfile))
            return string.Empty;

        if (ailmentSuffixes == null || ailmentSuffixes.Count == 0)
            return baseProfile.Trim();

        var parts = new System.Collections.Generic.List<string>(1 + ailmentSuffixes.Count) { baseProfile.Trim() };
        for (int i = 0; i < ailmentSuffixes.Count; i++)
        {
            string suffix = ailmentSuffixes[i];
            if (!string.IsNullOrWhiteSpace(suffix))
                parts.Add(suffix.Trim());
        }

        return string.Join(" - ", parts);
    }

    public static string BuildLabel(CharacterStats stats)
    {
        if (stats == null)
            return string.Empty;

        string baseProfile = CombatProfileClassifier.Classify(
            stats.GetCombatProfileBreakdown(),
            stats.GetCombatProfileDefenseHints());

        AilmentSuffixScratch.Clear();
        CollectAilmentSuffixes(stats, AilmentSuffixScratch);
        return FormatWithAilments(baseProfile, AilmentSuffixScratch);
    }

    /// <summary>Overhead UI: base profile and each ailment suffix use their own TMP color tags.</summary>
    public static string BuildRichTextLabel(CharacterStats stats)
    {
        if (stats == null)
            return string.Empty;

        string baseProfile = CombatProfileClassifier.Classify(
            stats.GetCombatProfileBreakdown(),
            stats.GetCombatProfileDefenseHints());

        AilmentSuffixScratch.Clear();
        CollectAilmentSuffixes(stats, AilmentSuffixScratch);

        if (AilmentSuffixScratch.Count == 0)
            return WrapRichTextSegment(baseProfile, CombatProfileClassifier.GetColorForLabel(baseProfile));

        var sb = new StringBuilder(baseProfile.Length + AilmentSuffixScratch.Count * 32);
        sb.Append(WrapRichTextSegment(baseProfile, CombatProfileClassifier.GetColorForLabel(baseProfile)));
        for (int i = 0; i < AilmentSuffixScratch.Count; i++)
        {
            string suffix = AilmentSuffixScratch[i];
            sb.Append(" - ");
            sb.Append(WrapRichTextSegment(suffix, CombatProfileClassifier.GetColorForLabel(suffix)));
        }

        return sb.ToString();
    }

    private static string WrapRichTextSegment(string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string hex = ColorUtility.ToHtmlStringRGB(color);
        return $"<color=#{hex}>{text}</color>";
    }

    public static Color GetColorForStats(CharacterStats stats) =>
        GetColorForDisplayLabel(BuildLabel(stats));

    public static string GetBaseProfileLabel(string displayLabel)
    {
        if (string.IsNullOrWhiteSpace(displayLabel))
            return string.Empty;

        int separator = displayLabel.IndexOf(" - ", System.StringComparison.Ordinal);
        return separator < 0 ? displayLabel.Trim() : displayLabel.Substring(0, separator).Trim();
    }

    public static Color GetColorForDisplayLabel(string displayLabel) =>
        CombatProfileClassifier.GetColorForLabel(GetBaseProfileLabel(displayLabel));

    public static void CollectAilmentSuffixes(CharacterStats stats, System.Collections.Generic.List<string> dest)
    {
        if (stats == null || dest == null)
            return;

        if (stats.BleedChance >= CombatProfileAilmentLabel.MinApplyChance)
            dest.Add(CombatProfileAilmentLabel.Sanguine);
        if (stats.PoisonChance >= CombatProfileAilmentLabel.MinApplyChance)
            dest.Add(CombatProfileAilmentLabel.Poisonous);
        if (GetBurnApplyChanceForProfile(stats) >= CombatProfileAilmentLabel.MinApplyChance)
            dest.Add(CombatProfileAilmentLabel.Fiery);
        if (GetShockApplyChanceForProfile(stats) >= CombatProfileAilmentLabel.MinApplyChance)
            dest.Add(CombatProfileAilmentLabel.Electrified);
        if (GetChillApplyChanceForProfile(stats) >= CombatProfileAilmentLabel.MinApplyChance)
            dest.Add(CombatProfileAilmentLabel.Frosted);
    }

    private static float GetShockApplyChanceForProfile(CharacterStats stats)
    {
        if (stats.MaxSplitDamage.magic > 0f
            && stats.CurrentMagicAttackType == MagicAttackType.Lightning)
            return stats.MagicAilmentApplyChance;

        return stats.MeleeShockChance;
    }

    private static float GetChillApplyChanceForProfile(CharacterStats stats)
    {
        if (stats.MaxSplitDamage.magic > 0f
            && stats.CurrentMagicAttackType == MagicAttackType.Ice)
            return stats.MagicAilmentApplyChance;

        return 0f;
    }

    private static float GetBurnApplyChanceForProfile(CharacterStats stats)
    {
        if (stats.MaxSplitDamage.magic > 0f
            && stats.CurrentMagicAttackType == MagicAttackType.Fire)
            return stats.MagicAilmentApplyChance;

        // Enemies only roll burn on fire magic; BurnApplyChance returns MagicAilmentApplyChance for all enemy magic types.
        if (stats.GetComponent<EnemyBaseController>() != null
            || stats.GetComponentInParent<EnemyBaseController>() != null)
            return 0f;

        return stats.BurnApplyChance;
    }
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

    /// <summary>Corruption resist at or above this (same spirit as armour/MR gates) qualifies for Shrouded when EHP ratios agree.</summary>
    public const int ShroudedMinCorruptionResist = 25;

    /// <summary>Corruption effective HP must exceed physical and magic EHP by this factor (mirror <see cref="ArmouredPhysEhpOverCorruptionMin"/>).</summary>
    public const float ShroudedCorruptionEhpOverOtherMin = 1.12f;

    public const int TankMinMaxHp = 35;

    /// <summary>
    /// Minimum Max HP per point of offense CP before a unit is treated as an HP-pool Tank.
    /// Prevents ailment-heavy strikers with modest HP from reading as Tank when offense CP share is low.
    /// </summary>
    public const float TankMinMaxHpPerOffensePoint = 22f;

    /// <summary>
    /// Max ratio of physical (or magic) effective HP vs corruption EHP for "low mitigation" Tank identity.
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
/// Shrouded or Tank when defense CP is low → Armoured → Warded → Shrouded → Tank (defense-heavy) →
/// Sustaining → Nimble → Bruiser → Balanced.
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
        float ePhys = Mathf.Max(1f, d.EffectiveHpVsPhysical);
        float eMag = Mathf.Max(1f, d.EffectiveHpVsMagic);
        float physOverCorruption = d.EffectiveHpVsPhysical / eCorr;
        float magOverCorruption = d.EffectiveHpVsMagic / eCorr;
        float corruptionOverPhys = eCorr / ePhys;
        float corruptionOverMag = eCorr / eMag;

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

        // 3b Shrouded (low defense CP share) — very high corruption resist or corruption EHP clearly above phys/mag.
        // Runs before HP-only Tank so corruption-heavy dummies are not misread as Tank when pD is small.
        if (pD < CombatProfileThresholds.DefenseProfileMin)
        {
            bool shroudDrivenLowPd = d.CorruptionResist >= CombatProfileThresholds.ShroudedMinCorruptionResist
                                     || (corruptionOverPhys >= CombatProfileThresholds.ShroudedCorruptionEhpOverOtherMin
                                         && corruptionOverMag >= CombatProfileThresholds.ShroudedCorruptionEhpOverOtherMin);
            if (shroudDrivenLowPd && corruptionOverPhys >= corruptionOverMag - 0.02f)
                return CombatProfileLabel.Shrouded;
        }

        // 3c Tank (HP pool) — large MaxHP with flat mitigation when offense CP dominates (pD below defense threshold).
        // Without this, high-HP training dummies / punch bags read as Balanced because defense share looks small vs DPS CP.
        if (pD < CombatProfileThresholds.DefenseProfileMin)
        {
            bool lowMitigation = physOverCorruption <= CombatProfileThresholds.TankMitigationEhpOverCorruptionMax
                                 && magOverCorruption <= CombatProfileThresholds.TankMitigationEhpOverCorruptionMax;
            if (lowMitigation
                && QualifiesAsHpPoolTank(b, d)
                && d.Armor < CombatProfileThresholds.ArmouredMinArmor
                && d.MagicResist < CombatProfileThresholds.WardedMinMagicResist
                && d.CorruptionResist < CombatProfileThresholds.ShroudedMinCorruptionResist
                && !(corruptionOverPhys >= CombatProfileThresholds.ShroudedCorruptionEhpOverOtherMin
                     && corruptionOverMag >= CombatProfileThresholds.ShroudedCorruptionEhpOverOtherMin))
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

            // 5b Shrouded — corruption resist / corruption EHP dominates physical and magic EHP
            bool shroudDriven = d.CorruptionResist >= CombatProfileThresholds.ShroudedMinCorruptionResist
                                || (corruptionOverPhys >= CombatProfileThresholds.ShroudedCorruptionEhpOverOtherMin
                                    && corruptionOverMag >= CombatProfileThresholds.ShroudedCorruptionEhpOverOtherMin);
            if (shroudDriven && corruptionOverPhys >= corruptionOverMag - 0.02f)
                return CombatProfileLabel.Shrouded;

            // 6 Tank — large HP pool; mitigation from armour/MR is not the main story
            bool lowMitigation = physOverCorruption <= CombatProfileThresholds.TankMitigationEhpOverCorruptionMax
                                 && magOverCorruption <= CombatProfileThresholds.TankMitigationEhpOverCorruptionMax;
            if (lowMitigation
                && QualifiesAsHpPoolTank(b, d)
                && d.CorruptionResist < CombatProfileThresholds.ShroudedMinCorruptionResist
                && !(corruptionOverPhys >= CombatProfileThresholds.ShroudedCorruptionEhpOverOtherMin
                     && corruptionOverMag >= CombatProfileThresholds.ShroudedCorruptionEhpOverOtherMin))
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

    private static bool QualifiesAsHpPoolTank(CombatPowerBreakdown b, CombatProfileDefenseHints d)
    {
        if (d.IdentityMaxHp < CombatProfileThresholds.TankMinMaxHp)
            return false;

        float offense = Mathf.Max(b.Offense, 0.75f);
        return d.IdentityMaxHp / offense >= CombatProfileThresholds.TankMinMaxHpPerOffensePoint;
    }

    public static Color GetColorForLabel(string label)
    {
        label = CombatProfileDisplay.GetBaseProfileLabel(label);
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

        if (label == CombatProfileLabel.Shrouded)
            return new Color(112f / 255f, 64f / 255f, 192f / 255f);

        if (label == CombatProfileLabel.Tank)
            return new Color(0.45f, 0.72f, 0.48f);

        if (label == CombatProfileLabel.Sustaining)
            return new Color(0.38f, 0.82f, 0.42f);

        if (label == CombatProfileLabel.Nimble)
            return new Color(0.42f, 0.92f, 0.95f);

        if (label == CombatProfileLabel.Bruiser)
            return new Color(1f, 0.58f, 0.18f);

        if (label == CombatProfileAilmentLabel.Poisonous)
            return new Color(0.42f, 0.82f, 0.36f);

        if (label == CombatProfileAilmentLabel.Sanguine)
            return new Color(0.82f, 0.18f, 0.22f);

        if (label == CombatProfileAilmentLabel.Electrified)
            return new Color(0.95f, 0.86f, 0.28f);

        if (label == CombatProfileAilmentLabel.Fiery)
            return new Color(1f, 0.45f, 0.12f);

        if (label == CombatProfileAilmentLabel.Frosted)
            return new Color(0.55f, 0.82f, 0.98f);

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
            $"Armor: {d.Armor} | MR: {d.MagicResist} | CorruptionResist: {d.CorruptionResist} | MaxHP: {d.MaxHP} | Move: {d.FinalMoveSpeed:0.##}\n" +
            $"EHP phys/corr: {d.EffectiveHpVsPhysical / eCorr:0.##} | mag/corr: {d.EffectiveHpVsMagic / eCorr:0.##}\n" +
            $"EHP corr/phys: {eCorr / Mathf.Max(1f, d.EffectiveHpVsPhysical):0.##} | corr/mag: {eCorr / Mathf.Max(1f, d.EffectiveHpVsMagic):0.##}";
    }
}
