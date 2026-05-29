using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Builds tooltip stat lines with bold markers for rolled / enhancement bonuses vs a base item.</summary>
public static class ItemTooltipStatHighlight
{
    public static ItemDefinition ResolveBaseline(ItemDatabase db, string itemId)
    {
        if (!db || string.IsNullOrWhiteSpace(itemId))
            return null;

        if (!db.IsRuntimeEnhancedItem(itemId))
            return null;

        string baseId = db.GetBaseItemId(itemId);
        return string.IsNullOrWhiteSpace(baseId) ? null : db.Get(baseId);
    }

    public static string BuildMainStatsText(ItemDefinition current, ItemDefinition baseline)
    {
        if (!current || baseline == null || ReferenceEquals(current, baseline))
            return current != null ? current.BuildTooltipMainStatsText() : string.Empty;

        if (current.IsWeapon)
            return BuildWeaponMainStats(current, baseline);

        if (current.IsArmor || current.IsJewelry)
            return BuildArmorJewelryMainStats(current, baseline);

        if (current.IsTool)
            return BuildToolMainStats(current, baseline);

        return current.BuildTooltipMainStatsText();
    }

    private static string BuildWeaponMainStats(ItemDefinition current, ItemDefinition baseline)
    {
        float aps = current.weaponStats.attacksPerSecond > 0f ? current.weaponStats.attacksPerSecond : 1f;
        aps *= Mathf.Max(0.1f, 1f + current.bonusStats.attackSpeedPercent);

        float baseAps = baseline.weaponStats.attacksPerSecond > 0f ? baseline.weaponStats.attacksPerSecond : 1f;
        baseAps *= Mathf.Max(0.1f, 1f + baseline.bonusStats.attackSpeedPercent);

        float critChancePct = Mathf.Clamp01(current.weaponStats.critChance + current.bonusStats.critChanceBonus) * 100f;
        float baseCritChancePct = Mathf.Clamp01(baseline.weaponStats.critChance + baseline.bonusStats.critChanceBonus) * 100f;

        float critMultBonusPct =
            (Mathf.Max(0f, current.weaponStats.critMultiplier + current.bonusStats.critMultiplierBonus) - 1f) * 100f;
        float baseCritMultBonusPct =
            (Mathf.Max(0f, baseline.weaponStats.critMultiplier + baseline.bonusStats.critMultiplierBonus) - 1f) * 100f;

        float range = current.AttackRange;
        float baseRange = baseline.AttackRange;

        string dual = (current.weaponStats.handedness == Handedness.OneHanded && current.weaponStats.canEquipInOffHand)
            ? "\nDual Wield: Yes"
            : "";

        var s = new StringBuilder();

        if (current.HasPhysicalWeaponDamage)
        {
            s.Append(FormatWeaponPhysicalDamageLine(current, baseline));
            s.Append('\n');
        }

        if (current.weaponStats.minFireDamage > 0 || current.weaponStats.maxFireDamage > 0)
            s.Append(FormatIntRangeLine("Fire Damage", current.weaponStats.minFireDamage, current.weaponStats.maxFireDamage,
                baseline.weaponStats.minFireDamage, baseline.weaponStats.maxFireDamage)).Append('\n');

        if (current.weaponStats.minIceDamage > 0 || current.weaponStats.maxIceDamage > 0)
            s.Append(FormatIntRangeLine("Ice Damage", current.weaponStats.minIceDamage, current.weaponStats.maxIceDamage,
                baseline.weaponStats.minIceDamage, baseline.weaponStats.maxIceDamage)).Append('\n');

        if (current.weaponStats.minLightningDamage > 0 || current.weaponStats.maxLightningDamage > 0)
            s.Append(FormatIntRangeLine("Lightning Damage", current.weaponStats.minLightningDamage, current.weaponStats.maxLightningDamage,
                baseline.weaponStats.minLightningDamage, baseline.weaponStats.maxLightningDamage)).Append('\n');

        if (current.HasCorruptionWeaponDamage)
            s.Append(FormatIntRangeLine("Corruption Damage", current.weaponStats.minCorruptionDamage, current.weaponStats.maxCorruptionDamage,
                baseline.weaponStats.minCorruptionDamage, baseline.weaponStats.maxCorruptionDamage)).Append('\n');

        s.Append(FormatFloatLine("Speed", $"{aps:0.##} atk/s", aps, baseAps)).Append('\n');

        if (HasSignificantPercentPoints(critChancePct) || current.IsCorruptionOnlyWeapon)
            s.Append(FormatPercentLine("Crit Chance", critChancePct, baseCritChancePct, signed: true)).Append('\n');

        if (HasSignificantPercentPoints(critMultBonusPct) || current.IsCorruptionOnlyWeapon)
            s.Append(FormatPercentLine("Crit Multi", critMultBonusPct, baseCritMultBonusPct, signed: true)).Append('\n');

        string ailments = current.BuildWeaponAilmentsLineForTooltip();
        if (!string.IsNullOrWhiteSpace(ailments))
            s.Append(ailments).Append('\n');

        s.Append(FormatFloatLine("Range", $"{range:0.##}", range, baseRange)).Append(dual);

        AppendWeaponProcLine(s, "Phys Block", current.PhysBlockChance, baseline.PhysBlockChance, percent01: true);
        AppendWeaponProcLine(s, "Parry Chance", current.ParryChance, baseline.ParryChance, percent01: true);
        AppendWeaponProcLine(s, "Stun Chance", current.StunChance, baseline.StunChance, percent01: true);
        AppendWeaponProcLine(s, "Health", current.BonusHealth, baseline.BonusHealth, percent01: false, prefixPlus: true);

        if (current.weaponStats.attackSkill == AttackSkill.Magic)
            s.Append($"\nMana Cost: {current.ManaCostPerAttack:0.##}");

        if (current.RequiresOffhandSupport)
            s.Append($"\nRequires: {current.RequiredSupportType}");

        string extras = current.BuildBonusLinesForHighlight(baseline);
        if (!string.IsNullOrWhiteSpace(extras))
            s.Append('\n').Append(extras);

        string misc = current.BuildMiscTooltipLinesForTooltip();
        if (!string.IsNullOrWhiteSpace(misc))
            s.Append('\n').Append(misc);

        return s.ToString().TrimEnd('\n');
    }

    private static string BuildArmorJewelryMainStats(ItemDefinition current, ItemDefinition baseline)
    {
        var s = new StringBuilder();

        AppendIntStatLine(s, "Armour", current.ArmorValue, baseline.ArmorValue);
        AppendIntStatLine(s, "Magic Res", current.MagicResist, baseline.MagicResist);
        AppendIntStatLine(s, "Corruption Res", current.CorruptionResist, baseline.CorruptionResist);
        AppendIntStatLine(s, "Health", current.BonusHealth, baseline.BonusHealth, prefixPlus: true);
        AppendIntStatLine(s, "Energy", current.BonusEnergy, baseline.BonusEnergy, prefixPlus: true);
        AppendIntStatLine(s, "Mana", current.BonusMana, baseline.BonusMana, prefixPlus: true);

        if (current.CombatEnergyEfficiency > 0.0001f || baseline.CombatEnergyEfficiency > 0.0001f)
            AppendFloatStatLine(s, "Energy Efficiency", current.CombatEnergyEfficiency * 100f, baseline.CombatEnergyEfficiency * 100f, suffix: "%");

        if (current.PhysBlockChance > 0f || baseline.PhysBlockChance > 0f)
            AppendFloatStatLine(s, "Phys Block", current.PhysBlockChance * 100f, baseline.PhysBlockChance * 100f, suffix: "%");

        if (current.ArmorFlatGuard > 0 || baseline.ArmorFlatGuard > 0)
            AppendIntStatLine(s, "Guard", current.ArmorFlatGuard, baseline.ArmorFlatGuard, prefixPlus: true);

        if (current.ArmorMaxGuardPercent > 0.00001f || baseline.ArmorMaxGuardPercent > 0.00001f)
            AppendFloatStatLine(s, "Max Guard", current.ArmorMaxGuardPercent, baseline.ArmorMaxGuardPercent, suffix: "%", signed: true);

        string extras = current.BuildBonusLinesForHighlight(baseline);
        if (!string.IsNullOrWhiteSpace(extras))
        {
            if (s.Length > 0)
                s.Append('\n');
            s.Append(extras);
        }

        string misc = current.BuildMiscTooltipLinesForTooltip();
        if (!string.IsNullOrWhiteSpace(misc))
        {
            if (s.Length > 0)
                s.Append('\n');
            s.Append(misc);
        }

        return s.ToString().TrimEnd('\n');
    }

    private static string BuildToolMainStats(ItemDefinition current, ItemDefinition baseline)
    {
        var s = new StringBuilder();
        s.Append(FormatFloatLine("Gather Speed", $"{current.GatherSpeedMultiplier:0.##}x", current.GatherSpeedMultiplier, baseline.GatherSpeedMultiplier));
        s.Append('\n');
        s.Append(FormatFloatLine("Gather Grit", $"{current.GatheringGrit * 100f:0.#}%", current.GatheringGrit, baseline.GatheringGrit));
        s.Append('\n');
        s.Append(FormatFloatLine("Bonus Find", $"+{current.BonusResourceFindChance * 100f:0.#}%", current.BonusResourceFindChance, baseline.BonusResourceFindChance));
        s.Append('\n');
        s.Append(FormatFloatLine("Stamina Efficiency", $"+{current.StaminaEfficiency * 100f:0.#}%", current.StaminaEfficiency, baseline.StaminaEfficiency));

        string extras = current.BuildBonusLinesForHighlight(baseline);
        if (!string.IsNullOrWhiteSpace(extras))
            s.Append('\n').Append(extras);

        string misc = current.BuildMiscTooltipLinesForTooltip();
        if (!string.IsNullOrWhiteSpace(misc))
            s.Append('\n').Append(misc);

        return s.ToString().TrimEnd('\n');
    }

    private static string FormatWeaponPhysicalDamageLine(ItemDefinition current, ItemDefinition baseline)
    {
        int minCur = current.weaponStats.minPhysicalDamage;
        int maxCur = current.weaponStats.maxPhysicalDamage;
        int minBase = baseline.weaponStats.minPhysicalDamage;
        int maxBase = baseline.weaponStats.maxPhysicalDamage;

        string line = $"Physical Damage: {minCur}-{maxCur}";
        if (minCur == minBase && maxCur == maxBase)
            return line;

        var notes = new List<string>(2);
        if (minCur != minBase)
            notes.Add($"+{minCur - minBase} min physical");
        if (maxCur != maxBase)
            notes.Add($"+{maxCur - maxBase} max physical");

        return BoldWithNote(line, notes);
    }

    private static string FormatIntRangeLine(string label, int minCur, int maxCur, int minBase, int maxBase)
    {
        string line = $"{label}: {minCur}-{maxCur}";
        if (minCur == minBase && maxCur == maxBase)
            return line;

        var notes = new List<string>(2);
        if (minCur != minBase)
            notes.Add($"+{minCur - minBase} min");
        if (maxCur != maxBase)
            notes.Add($"+{maxCur - maxBase} max");

        return BoldWithNote(line, notes);
    }

    private static string FormatFloatLine(string label, string display, float current, float baseline)
    {
        if (Mathf.Approximately(current, baseline))
            return $"{label}: {display}";

        float delta = current - baseline;
        string sign = delta >= 0f ? "+" : "";
        return BoldWithNote($"{label}: {display}", new[] { $"{sign}{delta:0.##}" });
    }

    private static string FormatPercentLine(string label, float currentPct, float baselinePct, bool signed)
    {
        if (Mathf.Approximately(currentPct, baselinePct))
            return $"{label}: {FormatSignedPercent100WithPlus(currentPct)}";

        float delta = currentPct - baselinePct;
        string sign = delta >= 0f ? "+" : "";
        string display = signed ? FormatSignedPercent100WithPlus(currentPct) : $"{currentPct:0.#}%";
        return BoldWithNote($"{label}: {display}", new[] { $"{sign}{delta:0.#}%" });
    }

    private static void AppendWeaponProcLine(StringBuilder s, string label, float current, float baseline, bool percent01, bool prefixPlus = false)
    {
        if (percent01)
        {
            if (current <= 0f && baseline <= 0f)
                return;

            if (Mathf.Approximately(current, baseline))
            {
                if (current > 0f)
                    s.Append($"\n{label}: {FormatSignedPercent01(current)}");
                return;
            }

            string display = FormatSignedPercent01(current);
            float delta = (current - baseline) * 100f;
            s.Append('\n').Append(BoldWithNote($"{label}: {display}", new[] { $"{delta:+0.#;-0.#;0}%" }));
            return;
        }

        int curInt = Mathf.RoundToInt(current);
        int baseInt = Mathf.RoundToInt(baseline);
        if (curInt <= 0 && baseInt <= 0)
            return;

        if (curInt == baseInt)
        {
            if (curInt > 0)
                s.Append($"\n{label}: {(prefixPlus ? "+" : "")}{curInt}");
            return;
        }

        string valueText = prefixPlus ? $"+{curInt}" : curInt.ToString();
        s.Append('\n').Append(BoldWithNote($"{label}: {valueText}", new[] { $"+{curInt - baseInt}" }));
    }

    private static void AppendIntStatLine(StringBuilder s, string label, int current, int baseline, bool prefixPlus = false)
    {
        if (current == 0 && baseline == 0)
            return;

        if (s.Length > 0)
            s.Append('\n');

        if (current == baseline)
        {
            s.Append(prefixPlus ? $"{label}: +{current}" : $"{label}: {current}");
            return;
        }

        string valueText = prefixPlus ? $"+{current}" : current.ToString();
        s.Append(BoldWithNote($"{label}: {valueText}", new[] { $"+{current - baseline}" }));
    }

    private static void AppendFloatStatLine(StringBuilder s, string label, float current, float baseline, string suffix = "", bool signed = false)
    {
        if (Mathf.Approximately(current, 0f) && Mathf.Approximately(baseline, 0f))
            return;

        if (s.Length > 0)
            s.Append('\n');

        if (Mathf.Approximately(current, baseline))
        {
            s.Append(signed ? $"{label}: {FormatSignedPercent100WithPlus(current)}" : $"{label}: {current:0.#}{suffix}");
            return;
        }

        string display = signed ? FormatSignedPercent100WithPlus(current) : $"{current:0.#}{suffix}";
        float delta = current - baseline;
        string sign = delta >= 0f ? "+" : "";
        s.Append(BoldWithNote($"{label}: {display}", new[] { $"{sign}{delta:0.#}{suffix}" }));
    }

    private static string BoldWithNote(string line, IReadOnlyList<string> notes)
    {
        if (notes == null || notes.Count == 0)
            return $"<b>{line}</b>";

        var noteText = new StringBuilder();
        for (int i = 0; i < notes.Count; i++)
        {
            if (i > 0)
                noteText.Append(", ");
            noteText.Append(notes[i]);
        }

        return $"<b>{line} ({noteText})</b>";
    }

    private static bool HasSignificantPercentPoints(float percentPoints) =>
        Mathf.Abs(percentPoints) > 0.001f;

    private static string FormatSignedPercent01(float value01) =>
        $"{value01 * 100f:+0.#;-0.#;0}%";

    private static string FormatSignedPercent100WithPlus(float value) =>
        $"{value:+0.#;-0.#;0}%";
}
