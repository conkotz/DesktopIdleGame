using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Shared helpers for endurance trial HUD / begin popup (active map resolution, loot text, last enemy).
/// </summary>
public static class EnduranceTrialUIHelpers
{
    private static readonly Dictionary<int, string> CombatProfileLabelCache = new();

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

        if (!TryCreateTempEnemyStats(def, applyActiveMapModifiers: true, out GameObject go, out CharacterStats stats))
            return 0;

        int cp = stats.CombatPowerRounded;
        UnityEngine.Object.Destroy(go);
        return cp;
    }

    /// <summary>
    /// Combat profile label for an <see cref="EnemyDefinition"/> (same rules as overhead UI / live enemies).
    /// </summary>
    public static string GetEnemyCombatProfileLabel(EnemyDefinition def)
    {
        if (def == null)
            return string.Empty;

        int id = def.GetInstanceID();
        if (CombatProfileLabelCache.TryGetValue(id, out string cached))
            return cached;

        if (!TryCreateTempEnemyStats(def, applyActiveMapModifiers: false, out GameObject go, out CharacterStats stats))
        {
            cached = string.Empty;
        }
        else
        {
            cached = stats.GetCombatProfileLabel();
            UnityEngine.Object.Destroy(go);
        }

        CombatProfileLabelCache[id] = cached;
        return cached;
    }

    private static bool TryCreateTempEnemyStats(
        EnemyDefinition def,
        bool applyActiveMapModifiers,
        out GameObject go,
        out CharacterStats stats)
    {
        go = null;
        stats = null;
        if (def == null)
            return false;

        EnemyBaseController enemy = null;

        if (def.prefab != null)
        {
            go = UnityEngine.Object.Instantiate(def.prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);

            enemy = go.GetComponent<EnemyBaseController>()
                    ?? go.GetComponentInChildren<EnemyBaseController>(true);
            stats = enemy != null ? enemy.Stats : go.GetComponent<CharacterStats>();
        }

        if (enemy == null || stats == null)
        {
            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
                go = null;
            }

            go = new GameObject("TempEnemyStats");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<Rigidbody2D>();
            stats = go.AddComponent<CharacterStats>();
            go.AddComponent<Animator>();
            enemy = go.AddComponent<EnemyBaseController>();
        }

        enemy.InitializeFromDefinition(def, spawnAsElite: false, applyActiveMapModifiers: applyActiveMapModifiers);
        stats = enemy.Stats ?? stats;
        return stats != null;
    }

    /// <summary>
    /// Loot table for a trial completion at <paramref name="tier1Based"/>.
    /// Replace rows (Append off): only that tier's entries. Append rows stack: base + each lower tier's append + this tier's append.
    /// </summary>
    public static IReadOnlyList<EnduranceTrialLootEntry> GetResolvedEnduranceCompletionLoot(MapNodeDefinition def, int tier1Based)
    {
        int tier = Mathf.Clamp(tier1Based, EnduranceTrialTier.MinTier, EnduranceTrialTier.MaxTier);
        if (def == null)
            return Array.Empty<EnduranceTrialLootEntry>();

        List<EnduranceTrialLootEntry> baseList = def.enduranceCompletionLoot;
        EnduranceTrialLootByTier rowSelected = FindEnduranceLootTierRow(def, tier);

        // Replace mode: this tier's list is the entire reward (no base, no cumulative appends from lower tiers).
        if (rowSelected != null && rowSelected.entries != null && rowSelected.entries.Count > 0 && !rowSelected.appendToBaseLoot)
            return rowSelected.entries;

        // Cumulative append: base + every tier from I..selected that has Append on and non-empty entries.
        var combined = new List<EnduranceTrialLootEntry>();
        if (baseList != null)
            combined.AddRange(baseList);

        for (int t = EnduranceTrialTier.MinTier; t <= tier; t++)
        {
            EnduranceTrialLootByTier row = FindEnduranceLootTierRow(def, t);
            if (row == null || row.entries == null || row.entries.Count == 0)
                continue;
            if (!row.appendToBaseLoot)
                continue;
            combined.AddRange(row.entries);
        }

        if (combined.Count == 0)
            return Array.Empty<EnduranceTrialLootEntry>();

        return combined;
    }

    private static EnduranceTrialLootByTier FindEnduranceLootTierRow(MapNodeDefinition def, int tier1Based)
    {
        if (def?.enduranceCompletionLootByTier == null)
            return null;

        for (int i = 0; i < def.enduranceCompletionLootByTier.Count; i++)
        {
            EnduranceTrialLootByTier row = def.enduranceCompletionLootByTier[i];
            if (row != null && row.tier == tier1Based)
                return row;
        }

        return null;
    }

    /// <summary>Human-readable lines for completion loot (for UI). Uses tier-specific loot when configured.</summary>
    public static string BuildEnduranceCompletionLootSummary(MapNodeDefinition def, int tier1Based = 1)
    {
        IReadOnlyList<EnduranceTrialLootEntry> loot = GetResolvedEnduranceCompletionLoot(def, tier1Based);
        if (loot == null || loot.Count == 0)
            return "—";

        var sb = new StringBuilder();
        for (int i = 0; i < loot.Count; i++)
        {
            EnduranceTrialLootEntry entry = loot[i];
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
    public static List<EnduranceTrialLootGrant> RollEnduranceCompletionLoot(MapNodeDefinition def, ItemDatabase db, int tier1Based)
    {
        var list = new List<EnduranceTrialLootGrant>();
        IReadOnlyList<EnduranceTrialLootEntry> entries = GetResolvedEnduranceCompletionLoot(def, tier1Based);
        if (entries == null || entries.Count == 0)
            return list;

        for (int i = 0; i < entries.Count; i++)
        {
            EnduranceTrialLootEntry entry = entries[i];
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
