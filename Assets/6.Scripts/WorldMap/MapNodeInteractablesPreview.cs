using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Builds a human-readable summary of interactable-style content configured on a <see cref="MapNodeDefinition"/>
/// (merchants, storage, notice boards, quest givers). Uses prefabs and spawn rows from the asset — not runtime
/// spawned instances — so it works from the level-select map screen.
/// </summary>
public static class MapNodeInteractablesPreview
{
    private const string NoticeBoardTag = "NoticeBoard";

    public readonly struct ContainsSummary
    {
        public readonly string npcMerchantsLine;
        public readonly string enemiesLine;
        public readonly string otherLine;

        public ContainsSummary(string npcMerchantsLine, string enemiesLine, string otherLine)
        {
            this.npcMerchantsLine = npcMerchantsLine ?? "";
            this.enemiesLine = enemiesLine ?? "";
            this.otherLine = otherLine ?? "";
        }
    }

    /// <summary>
    /// Comma-separated labels (e.g. <c>Fletcher, Chef, Storage, Notice Board</c>). Duplicate display names aggregate as <c>Rogue x2</c>.
    /// </summary>
    public static string BuildSummary(MapNodeDefinition node)
    {
        ContainsSummary split = BuildSplitSummary(node);
        return split.npcMerchantsLine;
    }

    public static ContainsSummary BuildSplitSummary(MapNodeDefinition node)
    {
        if (node == null)
            return new ContainsSummary("", "", "");

        var npcMerchantOrder = new OrderedTallyAccumulator();
        var enemyOrder = new OrderedTallyAccumulator();
        var otherOrder = new OrderedTallyAccumulator();

        void AddNpcMerchant(string label, int amount) => npcMerchantOrder.Add(label, amount);
        void AddEnemy(string label, int amount) => enemyOrder.Add(label, amount);
        void AddOther(string label, int amount) => otherOrder.Add(label, amount);

        void AddSpawnList(List<SpawnPrefabCount> spawns)
        {
            if (spawns == null)
                return;

            for (int i = 0; i < spawns.Count; i++)
            {
                SpawnPrefabCount row = spawns[i];
                if (row == null || row.count < 1)
                    continue;

                int weight = Mathf.Max(1, row.count);

                if (row.enemyDefinition != null)
                {
                    string enemyName = ResolveEnemyDefinitionName(row.enemyDefinition);
                    AddEnemy(enemyName, weight);
                    continue;
                }

                if (row.itemDefinition != null)
                {
                    string itemName = ResolveItemDefinitionName(row.itemDefinition);
                    AddOther(itemName, weight);
                    continue;
                }

                if (!row.TryResolveSpawnPrefab(out GameObject pfb, out _, node, logWarnings: false) || !pfb)
                    continue;

                bool hadCategorizedSignals = AddCategorizedLabelsFromPrefab(pfb, AddNpcMerchant, AddOther, weight);
                if (!hadCategorizedSignals)
                {
                    if (TryResolveEnemyLabelFromPrefab(pfb, out string enemyLabel))
                        AddEnemy(enemyLabel, weight);
                    else
                        AddOther(HumanizeUnityObjectName(pfb.name), weight);
                }
            }
        }

        if (node.spawnGroupPlans != null)
        {
            for (int p = 0; p < node.spawnGroupPlans.Count; p++)
            {
                LevelSpawnGroupPlan plan = node.spawnGroupPlans[p];
                AddSpawnList(plan?.spawns);
            }
        }

        if (node.enduranceWaves != null)
        {
            for (int w = 0; w < node.enduranceWaves.Count; w++)
            {
                EnduranceWavePlan wave = node.enduranceWaves[w];
                wave?.EnsureReady();
                AddSpawnList(wave?.spawns);
            }
        }

        if (node.simpleCombatWaves != null)
        {
            for (int w = 0; w < node.simpleCombatWaves.Count; w++)
            {
                EnduranceWavePlan wave = node.simpleCombatWaves[w];
                wave?.EnsureReady();
                AddSpawnList(wave?.spawns);
            }
        }

        return new ContainsSummary(
            npcMerchantOrder.BuildCommaSeparatedLine(),
            enemyOrder.BuildCommaSeparatedLine(),
            otherOrder.BuildCommaSeparatedLine());
    }

    /// <summary>
    /// Preserves first-seen order from spawn list iteration. Merges counts only when the same label repeats
    /// consecutively (case-insensitive), matching row order in <see cref="MapNodeDefinition"/> plans.
    /// </summary>
    private sealed class OrderedTallyAccumulator
    {
        private readonly List<(string label, int count)> _segments = new();

        public void Add(string label, int amount)
        {
            if (string.IsNullOrWhiteSpace(label) || amount <= 0)
                return;

            string k = label.Trim();
            if (_segments.Count > 0 &&
                string.Equals(_segments[_segments.Count - 1].label, k, StringComparison.OrdinalIgnoreCase))
            {
                int i = _segments.Count - 1;
                _segments[i] = (_segments[i].label, _segments[i].count + amount);
                return;
            }

            _segments.Add((k, amount));
        }

        public string BuildCommaSeparatedLine()
        {
            if (_segments.Count == 0)
                return "";

            var parts = new List<string>(_segments.Count);
            for (int i = 0; i < _segments.Count; i++)
            {
                (string label, int n) = _segments[i];
                parts.Add(n > 1 ? $"{label} x{n}" : label);
            }

            return string.Join(", ", parts);
        }
    }

    private static string ResolveEnemyDefinitionName(EnemyDefinition enemyDefinition)
    {
        if (enemyDefinition == null)
            return "";
        if (!string.IsNullOrWhiteSpace(enemyDefinition.displayName))
            return enemyDefinition.displayName.Trim();
        return HumanizeUnityObjectName(enemyDefinition.name);
    }

    private static string ResolveItemDefinitionName(ItemDefinition itemDefinition)
    {
        if (itemDefinition == null)
            return "";
        if (!string.IsNullOrWhiteSpace(itemDefinition.displayName))
            return itemDefinition.displayName.Trim();
        return HumanizeUnityObjectName(itemDefinition.name);
    }

    private static bool TryResolveEnemyLabelFromPrefab(GameObject prefab, out string label)
    {
        label = "";
        if (!prefab)
            return false;

        EnemyBaseController enemy = prefab.GetComponentInChildren<EnemyBaseController>(true);
        if (enemy != null && !string.IsNullOrWhiteSpace(enemy.DisplayName))
        {
            label = enemy.DisplayName.Trim();
            return true;
        }

        CharacterStats stats = prefab.GetComponentInChildren<CharacterStats>(true);
        if (stats != null && !string.IsNullOrWhiteSpace(stats.UnitDisplayName))
        {
            label = stats.UnitDisplayName.Trim();
            return true;
        }

        return false;
    }

    private static bool AddCategorizedLabelsFromPrefab(
        GameObject prefab,
        Action<string, int> addNpcMerchant,
        Action<string, int> addOther,
        int weight)
    {
        if (!prefab || weight <= 0)
            return false;

        bool addedAny = false;
        void AddNpcMerchantLabel(string label, int amount)
        {
            if (string.IsNullOrWhiteSpace(label) || amount <= 0)
                return;
            addNpcMerchant(label, amount);
            addedAny = true;
        }
        void AddOtherLabel(string label, int amount)
        {
            if (string.IsNullOrWhiteSpace(label) || amount <= 0)
                return;
            addOther(label, amount);
            addedAny = true;
        }

        Merchant[] merchants = prefab.GetComponentsInChildren<Merchant>(true);
        for (int i = 0; i < merchants.Length; i++)
        {
            Merchant m = merchants[i];
            if (!m)
                continue;
            string label = ResolveInteractableDisplayLabel(m.gameObject, preferMerchantPersonName: true, m);
            if (string.IsNullOrWhiteSpace(label))
                label = HumanizeUnityObjectName(m.gameObject.name);
            AddNpcMerchantLabel(label, weight);
        }

        StorageClick[] storages = prefab.GetComponentsInChildren<StorageClick>(true);
        for (int i = 0; i < storages.Length; i++)
        {
            StorageClick s = storages[i];
            if (!s || s.GetComponentInParent<Merchant>(true) != null)
                continue;
            AddOtherLabel("Storage", weight);
        }

        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (!t || !t.gameObject)
                continue;
            try
            {
                if (t.CompareTag(NoticeBoardTag))
                    AddOtherLabel("Notice Board", weight);
            }
            catch (UnityException)
            {
                break;
            }
        }

        QuestGiver[] questGivers = prefab.GetComponentsInChildren<QuestGiver>(true);
        for (int i = 0; i < questGivers.Length; i++)
        {
            QuestGiver q = questGivers[i];
            if (!q || q.GetComponentInParent<Merchant>(true) != null)
                continue;
            // Notice boards use QuestGiver but are already counted via NoticeBoard tag ("Notice Board").
            if (IsNoticeBoardObject(q.gameObject))
                continue;
            string label = ResolveInteractableDisplayLabel(q.gameObject, preferMerchantPersonName: false, merchantForPersonName: null);
            if (string.IsNullOrWhiteSpace(label))
                label = HumanizeUnityObjectName(q.gameObject.name);
            AddNpcMerchantLabel(label, weight);
        }

        return addedAny;
    }

    /// <summary>
    /// Matches <see cref="UnitOverheadUI"/> name resolution (enemy display name, else <see cref="CharacterStats.UnitDisplayName"/>).
    /// Uses <see cref="NpcIdentity"/> when present. Optionally uses a merchant's person name when present so the map preview matches world labels.
    /// </summary>
    private static string ResolveInteractableDisplayLabel(GameObject anchor, bool preferMerchantPersonName, Merchant merchantForPersonName)
    {
        if (!anchor)
            return null;

        if (preferMerchantPersonName && merchantForPersonName != null)
        {
            string person = merchantForPersonName.CharacterDisplayName;
            if (!string.IsNullOrWhiteSpace(person))
                return person.Trim();
            string role = merchantForPersonName.MerchantName;
            if (!string.IsNullOrWhiteSpace(role))
                return role.Trim();
            return null;
        }

        NpcIdentity npc = anchor.GetComponentInParent<NpcIdentity>(true);
        if (!npc)
            npc = anchor.GetComponentInChildren<NpcIdentity>(true);
        if (npc != null)
        {
            string person = npc.CharacterDisplayName;
            if (!string.IsNullOrWhiteSpace(person))
                return person.Trim();
            string r = npc.RoleDisplayLabel;
            if (!string.IsNullOrWhiteSpace(r))
                return r.Trim();
        }

        EnemyBaseController enemy = anchor.GetComponentInParent<EnemyBaseController>(true);
        if (enemy != null && !string.IsNullOrWhiteSpace(enemy.DisplayName))
            return enemy.DisplayName.Trim();

        CharacterStats stats = anchor.GetComponentInParent<CharacterStats>(true);
        if (!stats)
            stats = anchor.GetComponentInChildren<CharacterStats>(true);
        if (stats != null && !string.IsNullOrWhiteSpace(stats.UnitDisplayName))
            return stats.UnitDisplayName.Trim();

        return null;
    }

    private static string HumanizeUnityObjectName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "NPC";
        string s = raw.Trim();
        if (s.EndsWith("(Clone)", StringComparison.Ordinal))
            s = s.Substring(0, s.Length - "(Clone)".Length).Trim();
        s = s.Replace('_', ' ');

        // Split camel/pascal case and number transitions.
        s = Regex.Replace(s, "([a-z])([A-Z])", "$1 $2");
        s = Regex.Replace(s, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        s = Regex.Replace(s, "([A-Za-z])(\\d)", "$1 $2");
        s = Regex.Replace(s, "(\\d)([A-Za-z])", "$1 $2");
        s = Regex.Replace(s, "\\s+", " ").Trim();

        // Canonical cleanups for common interactable names.
        s = s.Replace(" Entrace", " Entrance", StringComparison.OrdinalIgnoreCase);
        s = s.Replace("Sign Post", "Signpost", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(s))
            return "NPC";

        // Keep readable title casing for object-name fallbacks.
        string[] words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            string w = words[i];
            if (w.Length <= 1)
            {
                words[i] = w.ToUpperInvariant();
                continue;
            }
            words[i] = char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant();
        }

        return string.Join(" ", words);
    }

    private static bool IsNoticeBoardObject(GameObject go)
    {
        if (!go)
            return false;
        try
        {
            return go.CompareTag(NoticeBoardTag);
        }
        catch (UnityException)
        {
            return false;
        }
    }
}
