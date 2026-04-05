using System;
using System.Collections.Generic;
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

    /// <summary>One rolled row of completion loot (matches what <see cref="DropManager"/> will spawn).</summary>
    public struct EnduranceTrialLootGrant
    {
        public string itemId;
        public int amount;
        public Sprite icon;
        public string displayName;
    }

    /// <summary>Same roll logic as <see cref="EnduranceTrialDirector"/> completion drops — call once and reuse for UI + spawning.</summary>
    public static List<EnduranceTrialLootGrant> RollEnduranceCompletionLoot(MapNodeDefinition def, ItemDatabase db)
    {
        var list = new List<EnduranceTrialLootGrant>();
        if (def?.enduranceCompletionLoot == null || def.enduranceCompletionLoot.Count == 0)
            return list;

        for (int i = 0; i < def.enduranceCompletionLoot.Count; i++)
        {
            EnduranceTrialLootEntry entry = def.enduranceCompletionLoot[i];
            if (entry == null || !entry.item)
                continue;
            if (entry.dropChance <= 0f)
                continue;
            if (entry.dropChance < 1f && UnityEngine.Random.value > entry.dropChance)
                continue;

            string id = entry.item.itemId;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            int minAmt = Mathf.Max(1, entry.amountMin);
            int maxAmt = Mathf.Max(minAmt, entry.amountMax);
            int amt = UnityEngine.Random.Range(minAmt, maxAmt + 1);
            Sprite icon = entry.item.icon;
            if (!icon && db)
            {
                ItemDefinition resolved = db.Get(id);
                if (resolved)
                    icon = resolved.icon;
            }

            string itemName = string.IsNullOrWhiteSpace(entry.item.displayName)
                ? entry.item.itemId
                : entry.item.displayName.Trim();

            list.Add(new EnduranceTrialLootGrant
            {
                itemId = id,
                amount = amt,
                icon = icon,
                displayName = itemName
            });
        }

        return list;
    }

    /// <summary>Post-trial summary: what was actually rolled/obtained.</summary>
    public static string BuildObtainedLootSummary(IReadOnlyList<EnduranceTrialLootGrant> grants)
    {
        if (grants == null || grants.Count == 0)
            return "No bonus loot this run.";

        var sb = new StringBuilder();
        for (int i = 0; i < grants.Count; i++)
        {
            EnduranceTrialLootGrant g = grants[i];
            sb.AppendLine($"{g.displayName} ×{g.amount}");
        }

        return "Rewards obtained:\n" + sb.ToString().TrimEnd();
    }
}
