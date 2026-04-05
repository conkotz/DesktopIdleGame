using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Shared helpers for endurance trial HUD / begin popup (active map resolution, loot text, last enemy).
/// </summary>
public static class EnduranceTrialUIHelpers
{
    /// <summary>Active playable map if it is an endurance trial; otherwise null.</summary>
    public static MapNodeDefinition TryGetActiveEnduranceMapNode()
    {
        MapNodeDefinition def = ActiveLevelContext.Current;
        if (def == null && GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;

        if (def == null || def.nodeType != MapNodeType.EnduranceTrial)
            return null;

        return def;
    }

    /// <summary>
    /// When <paramref name="assignedTrial"/> is null, any endurance map matches (single-trial scenes).
    /// When set, only matches when <paramref name="active"/> has the same <see cref="MapNodeDefinition.nodeId"/>.
    /// </summary>
    public static bool MatchesAssignedTrial(MapNodeDefinition assignedTrial, MapNodeDefinition active)
    {
        if (assignedTrial == null)
            return true;
        if (active == null)
            return false;
        return string.Equals(assignedTrial.nodeId, active.nodeId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Last non-null <see cref="SpawnPrefabCount.enemyDefinition"/> when iterating waves in order, then rows in order.
    /// </summary>
    public static EnemyDefinition GetLastEnemyDefinitionInEnduranceTrial(MapNodeDefinition def)
    {
        if (def?.enduranceWaves == null || def.enduranceWaves.Count == 0)
            return null;

        EnemyDefinition last = null;
        for (int w = 0; w < def.enduranceWaves.Count; w++)
        {
            EnduranceWavePlan wave = def.enduranceWaves[w];
            if (wave?.spawns == null)
                continue;

            for (int i = 0; i < wave.spawns.Count; i++)
            {
                SpawnPrefabCount row = wave.spawns[i];
                if (row?.enemyDefinition != null)
                    last = row.enemyDefinition;
            }
        }

        return last;
    }

    /// <summary>
    /// Rounded combat power for an <see cref="EnemyDefinition"/>, using the same <see cref="CharacterStats"/> math as spawned enemies.
    /// </summary>
    public static int GetEnemyCombatPowerRounded(EnemyDefinition def)
    {
        if (def == null)
            return 0;

        var go = new GameObject("TempEnemyCombatPower");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterStats>();
        go.AddComponent<Animator>();
        var enemy = go.AddComponent<EnemyBaseController>();
        enemy.InitializeFromDefinition(def);
        var stats = go.GetComponent<CharacterStats>();
        int cp = stats != null ? stats.CombatPowerRounded : 0;
        UnityEngine.Object.Destroy(go);
        return cp;
    }

    /// <summary>Human-readable lines for completion loot (for UI).</summary>
    public static string BuildEnduranceCompletionLootSummary(MapNodeDefinition def)
    {
        if (def?.enduranceCompletionLoot == null || def.enduranceCompletionLoot.Count == 0)
            return "—";

        var sb = new StringBuilder();
        for (int i = 0; i < def.enduranceCompletionLoot.Count; i++)
        {
            EnduranceTrialLootEntry entry = def.enduranceCompletionLoot[i];
            if (entry == null || entry.item == null)
                continue;

            string itemName = string.IsNullOrWhiteSpace(entry.item.displayName)
                ? entry.item.itemId
                : entry.item.displayName.Trim();

            string amt;
            if (entry.amountMin == entry.amountMax)
                amt = entry.amountMin.ToString();
            else
                amt = $"{entry.amountMin}–{entry.amountMax}";

            string line = $"{itemName} ×{amt}";
            if (entry.dropChance < 1f)
                line += $" ({entry.dropChance * 100f:0.#}%)";

            sb.AppendLine(line);
        }

        string result = sb.ToString().TrimEnd();
        if (string.IsNullOrEmpty(result))
            return "—";
        return "Potential rewards:\n" + result;
    }
}
