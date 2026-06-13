using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Bestiary hover copy for <see cref="DatabaseEnemyIconTooltipUI"/>.</summary>
public static class DatabaseEnemyStatsTooltipText
{
    private const string HeaderLine = "BASE Enemy stats (this may be scaled)";

    public static bool TryBuild(EnemyDefinition enemy, out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;
        if (enemy == null)
            return false;

        title = string.IsNullOrWhiteSpace(enemy.displayName)
            ? "Enemy"
            : enemy.displayName.Trim();

        var sb = new StringBuilder(512);
        sb.AppendLine(HeaderLine);
        sb.AppendLine();

        AppendOffenseSection(sb, enemy);
        sb.AppendLine();
        AppendAilmentsSection(sb, enemy);
        sb.AppendLine();
        AppendDefensesSection(sb, enemy);
        sb.AppendLine();
        sb.Append("Movement speed: ").Append(FormatNumber(enemy.moveSpeed));

        body = sb.ToString().TrimEnd();
        return true;
    }

    private static void AppendOffenseSection(StringBuilder sb, EnemyDefinition enemy)
    {
        sb.Append("Main damage: ").AppendLine(BuildDamageTypeLabel(enemy));
        sb.Append("Damage: ").AppendLine(BuildDamageRangeLabel(enemy));
        sb.Append("Attack speed: ").Append(FormatNumber(enemy.attackSpeed)).AppendLine("/s");
        sb.Append("Attack range: ").AppendLine(FormatNumber(enemy.attackRange));
        sb.Append("Crit chance: ").Append(FormatPercent01(enemy.critChance)).AppendLine();
        sb.Append("Crit multi: ").Append(FormatCritDamageBonusPercent(enemy.critMultiplier));
    }

    private static void AppendAilmentsSection(StringBuilder sb, EnemyDefinition enemy)
    {
        sb.AppendLine("Ailments:");

        var lines = new List<string>(5);
        TryAddAilmentLine(lines, enemy.bleedChance, enemy.bleedMultiplier, "Bleed");
        TryAddPoisonLine(lines, enemy);
        TryAddAilmentLine(lines, enemy.burnChance, enemy.burnExplosionMultiplier, "Burn");
        TryAddChillLine(lines, enemy);
        TryAddShockLine(lines, enemy);

        if (lines.Count == 0)
        {
            sb.AppendLine("None");
            return;
        }

        for (int i = 0; i < lines.Count; i++)
            sb.AppendLine(lines[i]);
    }

    private static void AppendDefensesSection(StringBuilder sb, EnemyDefinition enemy)
    {
        sb.AppendLine("Defences:");
        sb.Append("Health: ").AppendLine(Mathf.Max(1, enemy.maxHealth).ToString());

        if (enemy.lifeRegenPerSecond > 0f)
            sb.Append("HP regen: ").Append(FormatNumber(enemy.lifeRegenPerSecond)).AppendLine("/s");

        sb.Append("Armour: ").AppendLine(Mathf.Max(0, enemy.armor).ToString());

        if (enemy.physBlockChance > 0f)
            sb.Append("Block: ").Append(FormatPercent01(enemy.physBlockChance)).AppendLine();

        sb.Append("Magic resist: ").AppendLine(Mathf.Max(0, enemy.magicResist).ToString());
        sb.Append("Corruption resist: ").Append(Mathf.Max(0, enemy.corruptionResist));
    }

    private static void TryAddPoisonLine(List<string> lines, EnemyDefinition enemy)
    {
        if (enemy == null)
            return;

        float chance = enemy.poisonChance;
        float multiplier = enemy.poisonMultiplier;
        if (chance <= 0f && multiplier <= 0f)
            return;

        var line = new StringBuilder(64);
        line.Append("Poison: ").Append(FormatPercent01(chance)).Append(" chance, ")
            .Append(FormatAilmentMultiplier(multiplier)).Append(" multi");

        if (chance > 0.01f && enemy.poisonMaxStacks > 0)
            line.Append(", ").Append(enemy.poisonMaxStacks).Append(" max stacks");

        lines.Add(line.ToString());
    }

    private static void TryAddChillLine(List<string> lines, EnemyDefinition enemy)
    {
        if (enemy == null)
            return;

        float chance = enemy.chillChance;
        float slowPerStack = enemy.chillSlowPerStack;
        if (chance <= 0f && slowPerStack <= 0f)
            return;

        lines.Add(
            $"Chill: {FormatPercent01(chance)} chance, {slowPerStack * 100f:0.#}% chill effect");
    }

    private static void TryAddShockLine(List<string> lines, EnemyDefinition enemy)
    {
        if (enemy == null)
            return;

        float chance = enemy.shockChance;
        float damageTakenMultiplier = enemy.shockDamageTakenMultiplier;
        if (chance <= 0f && damageTakenMultiplier <= 0f)
            return;

        lines.Add(
            $"Shock: {FormatPercent01(chance)} chance, {damageTakenMultiplier * 100f:0.#}% Shocked increase damage taken");
    }

    private static void TryAddAilmentLine(List<string> lines, float chance, float multiplier, string label)
    {
        if (chance <= 0f && multiplier <= 0f)
            return;

        lines.Add($"{label}: {FormatPercent01(chance)} chance, {FormatAilmentMultiplier(multiplier)} multi");
    }

    private static string BuildDamageTypeLabel(EnemyDefinition enemy)
    {
        bool hasPhys = enemy.maxPhysicalDamage > 0;
        bool hasMag = enemy.maxMagicDamage > 0f;
        bool hasCorruption = enemy.maxCorruptionDamage > 0f;

        var parts = new List<string>(3);
        if (hasPhys)
            parts.Add("Physical");
        if (hasMag)
            parts.Add(FormatMagicElementLabel(enemy.magicAttackType));
        if (hasCorruption)
            parts.Add("Corruption");

        if (parts.Count == 0)
            return "-";
        if (parts.Count == 1)
            return parts[0];
        if (parts.Count == 2)
            return $"{parts[0]} + {parts[1]}";
        return "Hybrid";
    }

    private static string BuildDamageRangeLabel(EnemyDefinition enemy)
    {
        bool hasPhys = enemy.maxPhysicalDamage > 0;
        bool hasMag = enemy.maxMagicDamage > 0f;
        bool hasCorruption = enemy.maxCorruptionDamage > 0f;

        int activeCount = (hasPhys ? 1 : 0) + (hasMag ? 1 : 0) + (hasCorruption ? 1 : 0);
        if (activeCount == 0)
            return "-";

        if (activeCount == 1)
        {
            if (hasPhys)
                return FormatIntRange(enemy.minPhysicalDamage, enemy.maxPhysicalDamage);
            if (hasMag)
                return FormatFloatRange(enemy.minMagicDamage, enemy.maxMagicDamage);
            return FormatFloatRange(enemy.minCorruptionDamage, enemy.maxCorruptionDamage);
        }

        var sb = new StringBuilder(64);
        if (hasPhys)
            sb.Append(FormatIntRange(enemy.minPhysicalDamage, enemy.maxPhysicalDamage)).Append(" (Physical)");
        if (hasMag)
        {
            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append(FormatFloatRange(enemy.minMagicDamage, enemy.maxMagicDamage))
                .Append(" (")
                .Append(FormatMagicElementLabel(enemy.magicAttackType))
                .Append(')');
        }

        if (hasCorruption)
        {
            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append(FormatFloatRange(enemy.minCorruptionDamage, enemy.maxCorruptionDamage))
                .Append(" (Corruption)");
        }

        return sb.ToString();
    }

    private static string FormatMagicElementLabel(MagicAttackType attackType)
    {
        switch (attackType)
        {
            case MagicAttackType.Fire:
                return "Fire";
            case MagicAttackType.Ice:
                return "Ice";
            case MagicAttackType.Lightning:
            default:
                return "Lightning";
        }
    }

    private static string FormatIntRange(int min, int max)
    {
        max = Mathf.Max(min, max);
        return min == max ? min.ToString() : $"{min}-{max}";
    }

    private static string FormatFloatRange(float min, float max)
    {
        max = Mathf.Max(min, max);
        if (Mathf.Approximately(min, max))
            return FormatNumber(min);
        return $"{FormatNumber(min)}-{FormatNumber(max)}";
    }

    private static string FormatPercent01(float value01) =>
        $"{Mathf.Clamp01(value01) * 100f:0.#}%";

    private static string FormatAilmentMultiplier(float multiplierFraction) =>
        $"{multiplierFraction * 100f:+0.#;-0.#;0}%";

    private static string FormatCritDamageBonusPercent(float multiplier) =>
        $"{(Mathf.Max(1f, multiplier) - 1f) * 100f:+0.#;-0.#;0}%";

    private static string FormatNumber(float value)
    {
        if (Mathf.Approximately(value, Mathf.Round(value)))
            return Mathf.Round(value).ToString("0");
        return value.ToString("0.##");
    }
}
