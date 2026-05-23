using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-time spawn for <see cref="QuestDefinition.respawnEnemyOnQuestAccepted"/> when a quest is accepted
/// (or when the player later enters the configured map). Clears permanent-death slots so
/// <see cref="EnemyDefinition.cannotRespawn"/> map spawns can be filled once; death after that uses normal rules.
/// </summary>
public static class QuestAcceptedEnemyRespawnService
{
    private static readonly HashSet<string> HandledQuestIds = new(StringComparer.Ordinal);
    private static bool _lifecycleBound;

    public static void BindLifecycle()
    {
        if (_lifecycleBound)
            return;

        GameplayLevelBootstrapper bootstrapper = GameplayLevelBootstrapper.Instance ??
            UnityEngine.Object.FindFirstObjectByType<GameplayLevelBootstrapper>(FindObjectsInactive.Include);
        if (bootstrapper == null)
            return;

        bootstrapper.OnLevelStarted -= OnLevelStarted;
        bootstrapper.OnLevelStarted += OnLevelStarted;
        _lifecycleBound = true;
    }

    public static void UnbindLifecycle()
    {
        if (!_lifecycleBound)
            return;

        GameplayLevelBootstrapper bootstrapper = GameplayLevelBootstrapper.Instance ??
            UnityEngine.Object.FindFirstObjectByType<GameplayLevelBootstrapper>(FindObjectsInactive.Include);
        if (bootstrapper != null)
            bootstrapper.OnLevelStarted -= OnLevelStarted;

        _lifecycleBound = false;
    }

    public static void LoadFromSave(SaveData data)
    {
        HandledQuestIds.Clear();
        if (data?.questAcceptedEnemyRespawnHandledIds == null)
            return;

        for (int i = 0; i < data.questAcceptedEnemyRespawnHandledIds.Count; i++)
        {
            string id = data.questAcceptedEnemyRespawnHandledIds[i];
            if (!string.IsNullOrWhiteSpace(id))
                HandledQuestIds.Add(id.Trim());
        }
    }

    public static void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        data.questAcceptedEnemyRespawnHandledIds ??= new List<string>();
        data.questAcceptedEnemyRespawnHandledIds.Clear();

        if (HandledQuestIds.Count == 0)
            return;

        var sorted = new List<string>(HandledQuestIds);
        sorted.Sort(StringComparer.Ordinal);
        for (int i = 0; i < sorted.Count; i++)
            data.questAcceptedEnemyRespawnHandledIds.Add(sorted[i]);
    }

    public static void OnQuestAccepted(QuestDefinition quest)
    {
        if (!TryGetRespawnConfig(quest, out EnemyDefinition enemyDef, out string mapNodeId, out string spawnPointName, out string spawnGroupId))
            return;

        if (HandledQuestIds.Contains(quest.questId.Trim()))
            return;

        string enemyId = enemyDef.enemyId.Trim();
        if (IsLivingEnemyPresent(enemyId))
        {
            MarkHandled(quest.questId);
            return;
        }

        MapNodeDefinition active = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;
        if (!IsActiveMap(active, mapNodeId))
            return;

        if (TrySpawnOnMap(quest, enemyDef, mapNodeId, spawnPointName, spawnGroupId))
            MarkHandled(quest.questId);
    }

    private static void OnLevelStarted(MapNodeDefinition map)
    {
        QuestProgressManager quests = QuestProgressManager.Instance ??
            UnityEngine.Object.FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (quests == null || map == null || string.IsNullOrWhiteSpace(map.nodeId))
            return;

        string activeNodeId = map.nodeId.Trim();
        QuestDatabase database = quests.QuestDatabase;
        IReadOnlyList<QuestDefinition> all = database != null ? database.All : null;
        if (all == null)
            return;

        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition quest = all[i];
            if (!quest || !quests.IsQuestAccepted(quest))
                continue;
            if (!TryGetRespawnConfig(quest, out EnemyDefinition enemyDef, out string mapNodeId, out string spawnPointName, out string spawnGroupId))
                continue;
            if (!string.Equals(mapNodeId, activeNodeId, StringComparison.Ordinal))
                continue;
            if (HandledQuestIds.Contains(quest.questId.Trim()))
                continue;

            string enemyId = enemyDef.enemyId.Trim();
            if (IsLivingEnemyPresent(enemyId))
            {
                MarkHandled(quest.questId);
                continue;
            }

            if (TrySpawnOnMap(quest, enemyDef, mapNodeId, spawnPointName, spawnGroupId))
                MarkHandled(quest.questId);
        }
    }

    private static bool TryGetRespawnConfig(
        QuestDefinition quest,
        out EnemyDefinition enemyDef,
        out string mapNodeId,
        out string spawnPointName,
        out string spawnGroupId)
    {
        enemyDef = null;
        mapNodeId = "";
        spawnPointName = "";
        spawnGroupId = "AllSpawns";

        if (!quest || !quest.respawnEnemyOnQuestAccepted || !quest.respawnEnemyOnQuestAcceptedDefinition)
            return false;

        enemyDef = quest.respawnEnemyOnQuestAcceptedDefinition;
        if (string.IsNullOrWhiteSpace(enemyDef.enemyId))
            return false;

        mapNodeId = !string.IsNullOrWhiteSpace(quest.respawnEnemyOnQuestAcceptedMapNodeId)
            ? quest.respawnEnemyOnQuestAcceptedMapNodeId.Trim()
            : !string.IsNullOrWhiteSpace(quest.progressMapNodeId)
                ? quest.progressMapNodeId.Trim()
                : "";

        if (string.IsNullOrWhiteSpace(mapNodeId))
            return false;

        spawnPointName = quest.respawnEnemyOnQuestAcceptedSpawnPointName != null
            ? quest.respawnEnemyOnQuestAcceptedSpawnPointName.Trim()
            : "";

        spawnGroupId = string.IsNullOrWhiteSpace(quest.respawnEnemyOnQuestAcceptedSpawnGroupId)
            ? "AllSpawns"
            : quest.respawnEnemyOnQuestAcceptedSpawnGroupId.Trim();

        return true;
    }

    private static bool IsActiveMap(MapNodeDefinition active, string mapNodeId)
    {
        if (active == null || string.IsNullOrWhiteSpace(active.nodeId) || string.IsNullOrWhiteSpace(mapNodeId))
            return false;
        return string.Equals(active.nodeId.Trim(), mapNodeId.Trim(), StringComparison.Ordinal);
    }

    private static bool TrySpawnOnMap(
        QuestDefinition quest,
        EnemyDefinition enemyDef,
        string mapNodeId,
        string spawnPointName,
        string spawnGroupId)
    {
        MapNodeDefinition map = ResolveMapNode(mapNodeId);
        if (map == null)
        {
            Debug.LogWarning(
                $"[QuestAcceptedEnemyRespawn] Map node '{mapNodeId}' not found for quest '{quest.questId}'.",
                quest);
            return false;
        }

        ClearPermanentDeathSlotsForEnemy(map, enemyDef.enemyId);

        LevelSpawnDirector director = UnityEngine.Object.FindFirstObjectByType<LevelSpawnDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            Debug.LogWarning(
                $"[QuestAcceptedEnemyRespawn] No LevelSpawnDirector in scene for quest '{quest.questId}'.",
                quest);
            return false;
        }

        var plan = new LevelSpawnGroupPlan
        {
            groupId = spawnGroupId,
            shuffleSpawnPoints = false,
            spawns = new List<SpawnPrefabCount>
            {
                new SpawnPrefabCount
                {
                    spawnPointGroupId = "",
                    spawnPointName = spawnPointName,
                    enemyDefinition = enemyDef,
                    count = 1
                }
            }
        };

        List<EnemyBaseController> spawned = director.SpawnAdditionalGroupPlan(plan, map);
        if (spawned == null || spawned.Count == 0)
        {
            Debug.LogWarning(
                $"[QuestAcceptedEnemyRespawn] Failed to spawn '{enemyDef.enemyId}' for quest '{quest.questId}' on '{mapNodeId}'.",
                quest);
            return false;
        }

        GameLog.Add($"Spawned {enemyDef.displayName} for quest: {quest.displayName}");
        return true;
    }

    private static MapNodeDefinition ResolveMapNode(string mapNodeId)
    {
        if (string.IsNullOrWhiteSpace(mapNodeId))
            return null;

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            UnityEngine.Object.FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (wmp?.WorldMap == null)
            return null;

        return wmp.WorldMap.FindNodeById(mapNodeId.Trim());
    }

    private static void ClearPermanentDeathSlotsForEnemy(MapNodeDefinition map, string enemyId)
    {
        if (map?.spawnGroupPlans == null || string.IsNullOrWhiteSpace(enemyId))
            return;

        string eid = enemyId.Trim();
        for (int planIndex = 0; planIndex < map.spawnGroupPlans.Count; planIndex++)
        {
            LevelSpawnGroupPlan plan = map.spawnGroupPlans[planIndex];
            if (plan?.spawns == null)
                continue;

            for (int rowIndex = 0; rowIndex < plan.spawns.Count; rowIndex++)
            {
                SpawnPrefabCount row = plan.spawns[rowIndex];
                if (row?.enemyDefinition == null ||
                    !string.Equals(row.enemyDefinition.enemyId, eid, StringComparison.OrdinalIgnoreCase))
                    continue;

                int count = Mathf.Max(1, row.count);
                for (int instanceIndex = 0; instanceIndex < count; instanceIndex++)
                {
                    string key = LevelSpawnDirector.BuildPermanentEnemyDeathKey(
                        map,
                        planIndex,
                        rowIndex,
                        instanceIndex,
                        eid);
                    PermanentEnemyDeathSaveStore.ClearPermanentlyDead(key);
                }
            }
        }
    }

    private static bool IsLivingEnemyPresent(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return false;

        string want = enemyId.Trim();
        EnemyBaseController[] all =
            UnityEngine.Object.FindObjectsByType<EnemyBaseController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            EnemyBaseController ec = all[i];
            if (!ec || ec.IsDead)
                continue;
            if (string.Equals(ec.EnemyId, want, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void MarkHandled(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        string id = questId.Trim();
        if (!HandledQuestIds.Add(id))
            return;

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }
}
