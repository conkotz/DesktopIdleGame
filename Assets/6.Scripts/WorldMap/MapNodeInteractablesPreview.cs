using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a human-readable summary of interactable-style content configured on a <see cref="MapNodeDefinition"/>
/// (merchants, storage, notice boards, quest givers). Uses prefabs and spawn rows from the asset — not runtime
/// spawned instances — so it works from the level-select map screen.
/// </summary>
public static class MapNodeInteractablesPreview
{
    private const string NoticeBoardTag = "NoticeBoard";

    /// <summary>
    /// Comma-separated labels (e.g. <c>Fletcher, Chef, Storage, Notice Board</c>). Duplicate display names aggregate as <c>Rogue x2</c>.
    /// </summary>
    public static string BuildSummary(MapNodeDefinition node)
    {
        if (node == null)
            return "";

        var tallies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var prefabsUsedInSpawnPlans = new HashSet<GameObject>();

        void Add(string label, int amount)
        {
            if (string.IsNullOrWhiteSpace(label) || amount <= 0)
                return;
            string k = label.Trim();
            tallies.TryGetValue(k, out int c);
            tallies[k] = c + amount;
        }

        void RegisterSpawnPrefab(GameObject pfb)
        {
            if (pfb)
                prefabsUsedInSpawnPlans.Add(pfb);
        }

        void AddSpawnList(List<SpawnPrefabCount> spawns)
        {
            if (spawns == null)
                return;

            for (int i = 0; i < spawns.Count; i++)
            {
                SpawnPrefabCount row = spawns[i];
                if (row == null || row.count < 1)
                    continue;
                if (row.enemyDefinition != null || row.itemDefinition)
                    continue;

                if (!row.TryResolveSpawnPrefab(out GameObject pfb, out _, node, logWarnings: false) || !pfb)
                    continue;

                RegisterSpawnPrefab(pfb);
                AddLabelsFromPrefab(pfb, Add, row.count);
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

        if (node.prefabGroups != null)
        {
            for (int g = 0; g < node.prefabGroups.Count; g++)
            {
                EncounterPrefabGroup group = node.prefabGroups[g];
                if (group?.prefabs == null)
                    continue;

                for (int i = 0; i < group.prefabs.Count; i++)
                {
                    GameObject pfb = group.prefabs[i];
                    if (!pfb || prefabsUsedInSpawnPlans.Contains(pfb))
                        continue;
                    if (!PrefabLooksLikeInteractableFeature(pfb))
                        continue;
                    AddLabelsFromPrefab(pfb, Add, 1);
                }
            }
        }

        if (tallies.Count == 0)
            return "";

        var keys = new List<string>(tallies.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);

        var parts = new List<string>(keys.Count);
        for (int i = 0; i < keys.Count; i++)
        {
            string k = keys[i];
            int n = tallies[k];
            parts.Add(n > 1 ? $"{k} x{n}" : k);
        }

        return string.Join(", ", parts);
    }

    private static bool PrefabLooksLikeInteractableFeature(GameObject prefab)
    {
        if (!prefab)
            return false;
        if (prefab.GetComponentInChildren<Merchant>(true))
            return true;
        if (prefab.GetComponentInChildren<StorageClick>(true))
            return true;
        if (HasNoticeBoardInHierarchy(prefab.transform))
            return true;
        if (HasQuestGiverWithoutMerchant(prefab))
            return true;
        return false;
    }

    private static bool HasQuestGiverWithoutMerchant(GameObject prefab)
    {
        QuestGiver[] qgs = prefab.GetComponentsInChildren<QuestGiver>(true);
        for (int i = 0; i < qgs.Length; i++)
        {
            QuestGiver q = qgs[i];
            if (q && q.GetComponentInParent<Merchant>(true) == null)
                return true;
        }

        return false;
    }

    private static bool HasNoticeBoardInHierarchy(Transform root)
    {
        if (!root)
            return false;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t || !t.gameObject)
                continue;
            try
            {
                if (t.CompareTag(NoticeBoardTag))
                    return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        return false;
    }

    private static void AddLabelsFromPrefab(GameObject prefab, Action<string, int> add, int weight)
    {
        if (!prefab || weight <= 0)
            return;

        Merchant[] merchants = prefab.GetComponentsInChildren<Merchant>(true);
        for (int i = 0; i < merchants.Length; i++)
        {
            Merchant m = merchants[i];
            if (!m)
                continue;
            string label = ResolveInteractableDisplayLabel(m.gameObject, preferMerchantPersonName: true, m);
            if (string.IsNullOrWhiteSpace(label))
                label = HumanizeUnityObjectName(m.gameObject.name);
            add(label, weight);
        }

        StorageClick[] storages = prefab.GetComponentsInChildren<StorageClick>(true);
        for (int i = 0; i < storages.Length; i++)
        {
            StorageClick s = storages[i];
            if (!s || s.GetComponentInParent<Merchant>(true) != null)
                continue;
            add("Storage", weight);
        }

        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (!t || !t.gameObject)
                continue;
            try
            {
                if (t.CompareTag(NoticeBoardTag))
                    add("Notice Board", weight);
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
            string label = ResolveInteractableDisplayLabel(q.gameObject, preferMerchantPersonName: false, merchantForPersonName: null);
            if (string.IsNullOrWhiteSpace(label))
                label = HumanizeUnityObjectName(q.gameObject.name);
            add(label, weight);
        }
    }

    /// <summary>
    /// Matches <see cref="UnitOverheadUI"/> name resolution (enemy display name, else <see cref="CharacterStats.UnitDisplayName"/>).
    /// Optionally uses a merchant's person name (inspector "Name") when present so the map preview matches world name labels.
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
        return string.IsNullOrWhiteSpace(s) ? "NPC" : s.Trim();
    }
}
