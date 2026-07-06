using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns content for the active <see cref="MapNodeDefinition"/> by mapping
/// <see cref="MapNodeDefinition.spawnGroupPlans"/> to <see cref="SpawnPointGroup"/>s in the scene.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay/Level Spawn Director")]
[DisallowMultipleComponent]
public class LevelSpawnDirector : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("If set, spawned instances will be parented here. Otherwise GameplayLevelBootstrapper.ContentRoot is used when available.")]
    [SerializeField] private Transform spawnParentOverride;

    [Header("Placement")]
    [Tooltip("If true, moves spawned instances so the bottom of their collider sits on the spawn point.")]
    [SerializeField] private bool alignSpawnPointToColliderBottom = true;

    [Tooltip("If true, once any spawn point location is used, no other group can use the same location again.")]
    [SerializeField] private bool preventOverlappingSpawns = true;

    [Tooltip("World-space grid size used to treat two points as 'the same spot' for overlap prevention.")]
    [Min(0.001f)]
    [SerializeField] private float overlapGridSize = 0.05f;

    [Tooltip("Radius (world units) used to detect if another living enemy already occupies a spawn point.")]
    [Min(0.01f)]
    [SerializeField] private float enemySpawnOccupancyRadius = 0.4f;

    [Tooltip("When a respawn cannot find a free spawn point, retry after this many seconds until one opens.")]
    [Min(0.02f)]
    [SerializeField] private float respawnQueueRetryIntervalSec = 0.25f;

    [Header("In-map teleporters")]
    [Tooltip("Default prefab when an InMapTeleporterPlan leaves Teleporter Prefab empty.")]
    [SerializeField] private GameObject defaultInMapTeleporterPrefab;

    [Header("Logging")]
    [Tooltip("When enabled, logs successful spawns/respawns and existing warnings for missing groups, etc.")]
    [SerializeField] private bool logSpawns;

    private bool _hasSpawnedForCurrentLevel;
    private Coroutine _initialSpawnCoroutine;
    private readonly HashSet<Vector2Int> _reservedSpawnCells = new();
    private readonly List<PendingRespawn> _pendingRespawns = new();
    private Coroutine _respawnQueueCoroutine;

    private struct PendingRespawn
    {
        public string NodeId;
        public string SpawnPointGroupId;
        public bool ShuffleSpawnPointsFromPlan;
        public string SpawnPointName;
        public GameObject PrefabAsset;
        public EnemyDefinition EnemyDefinition;
        public bool RespawnUntilSimpleWavesStart;
    }

    private enum RespawnAttemptOutcome
    {
        Spawned,
        NoFreePoint,
        AbortedInvalidContext,
    }

    /// <summary>
    /// Base delay from the active <see cref="MapNodeDefinition.enemyRespawnDelaySeconds"/> minus equipped
    /// <see cref="ItemMiscEffects.enemyRespawnTimeReductionSeconds"/>, floored at 0.01s.
    /// </summary>
    public float GetEffectiveRespawnDelaySeconds()
    {
        MapNodeDefinition active = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;
        return GetEffectiveRespawnDelayForNode(active);
    }

    private float GetEffectiveRespawnDelayForNode(MapNodeDefinition node)
    {
        float baseDelay = Mathf.Max(0.01f, node != null ? node.enemyRespawnDelaySeconds : 30f);
        float reduction = 0f;
        CharacterStats stats = GetPlayerCharacterStats();
        if (stats != null)
            reduction = stats.GetTotalEquippedEnemyRespawnTimeReductionSeconds();

        if (node != null)
        {
            MapEnhancementAggregate mapEnh = MapEnhancementService.BuildAggregate(node);
            if (mapEnh != null)
                reduction += Mathf.Max(0f, mapEnh.respawnTimeReductionSeconds);
        }

        return Mathf.Max(0.01f, baseDelay - reduction);
    }

    /// <summary>Enemies also have CharacterStats; never use FindObjectOfType(CharacterStats) for equipment.</summary>
    private static CharacterStats GetPlayerCharacterStats()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        return pc != null ? pc.GetComponent<CharacterStats>() : null;
    }

    private void OnEnable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;

        if (_pendingRespawns.Count > 0 && _respawnQueueCoroutine == null)
            _respawnQueueCoroutine = StartCoroutine(CoProcessRespawnQueue());
    }

    private void OnDisable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= OnLevelStarted;

        if (_initialSpawnCoroutine != null)
        {
            StopCoroutine(_initialSpawnCoroutine);
            _initialSpawnCoroutine = null;
        }

        StopRespawnQueueCoroutineOnly();
    }

    private void Start()
    {
        // If we enabled after the bootstrapper fired, handle it once (deferred one frame).
        if (_hasSpawnedForCurrentLevel)
            return;

        MapNodeDefinition def = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;
        if (def != null)
            QueueInitialSpawn(def);
    }

    private void OnLevelStarted(MapNodeDefinition def)
    {
        QueueInitialSpawn(def);
    }

    private void QueueInitialSpawn(MapNodeDefinition def)
    {
        if (_hasSpawnedForCurrentLevel || !def)
            return;

        if (_initialSpawnCoroutine != null)
            return;

        _initialSpawnCoroutine = StartCoroutine(CoInitialSpawn(def));
    }

    private IEnumerator CoInitialSpawn(MapNodeDefinition def)
    {
        // Wait one frame so SpawnPointGroup lane points and bootstrapper state are fully ready.
        yield return null;

        _initialSpawnCoroutine = null;
        if (_hasSpawnedForCurrentLevel || !def)
            yield break;

        SpawnFor(def);
    }

    private void SpawnFor(MapNodeDefinition def)
    {
        if (_hasSpawnedForCurrentLevel)
            return;

        if (!def)
            return;

        if (def.nodeType == MapNodeType.EnduranceTrial)
        {
            _hasSpawnedForCurrentLevel = true;
            _reservedSpawnCells.Clear();
            ClearPendingRespawnsForNewLevel();
            SpawnInMapTeleporters(def, FindAllSpawnPointGroups(), ResolveSpawnParent());
            return;
        }

        _hasSpawnedForCurrentLevel = true;
        _reservedSpawnCells.Clear();
        ClearPendingRespawnsForNewLevel();

        var groups = FindAllSpawnPointGroups();
        Transform parent = ResolveSpawnParent();

        if (def.spawnGroupPlans != null && def.spawnGroupPlans.Count > 0)
        {
            if (logSpawns)
                Debug.Log($"[LevelSpawnDirector] Spawning level '{def.nodeId}' ({def.displayName}): {def.spawnGroupPlans.Count} spawn plan(s).", this);

            MapEnhancementAggregate mapEnhancements = MapEnhancementService.BuildAggregate(def);
            var extraSpawnBonusesApplied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int scalingSlider = def.UsesNormalMapCombatScaling()
                ? MapCombatScaling.GetEffectiveSliderValue(def, WorldMapProgressManager.Instance)
                : MapCombatScaling.SliderMin;
            Dictionary<string, int> scalingExtraSpawns = def.UsesNormalMapCombatScaling()
                ? MapCombatScaling.BuildScalingExtraSpawnsByEnemyId(def, scalingSlider)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Pass 1: fixed-point world prefabs (signposts, cave entrances, etc.) before shuffled enemies.
            SpawnAllPlans(
                def,
                groups,
                parent,
                IsFixedPointWorldPrefabRow,
                requireExactNamedPoint: true,
                mapEnhancements,
                extraSpawnBonusesApplied,
                scalingExtraSpawns);

            // Pass 2: enemies, items, and anything without a fixed named point.
            SpawnAllPlans(
                def,
                groups,
                parent,
                row => !IsFixedPointWorldPrefabRow(row),
                requireExactNamedPoint: false,
                mapEnhancements,
                extraSpawnBonusesApplied,
                scalingExtraSpawns);
        }

        SpawnInMapTeleporters(def, groups, parent);
    }

    private void SpawnAllPlans(
        MapNodeDefinition def,
        Dictionary<string, SpawnPointGroup> groups,
        Transform parent,
        Func<SpawnPrefabCount, bool> rowFilter,
        bool requireExactNamedPoint,
        MapEnhancementAggregate mapEnhancements,
        HashSet<string> extraSpawnBonusesApplied,
        Dictionary<string, int> scalingExtraSpawns)
    {
        for (int i = 0; i < def.spawnGroupPlans.Count; i++)
        {
            LevelSpawnGroupPlan plan = def.spawnGroupPlans[i];
            if (plan == null || plan.spawns == null || plan.spawns.Count == 0)
                continue;

            if (!SpawnPlanHasGroupSource(plan))
            {
                if (logSpawns)
                    Debug.LogWarning("[LevelSpawnDirector] Spawn plan has no Group Id and no per-row Spawn Point Group Id — skipped.", this);
                continue;
            }

            SpawnGroup(
                plan,
                groups,
                parent,
                collectRoots: null,
                levelDefForRespawn: def,
                allowEliteSpawnRoll: false,
                planIndexForSaveKeys: i,
                levelDefForOneShotKeys: def,
                rowFilter: rowFilter,
                requireExactNamedPoint: requireExactNamedPoint,
                mapEnhancements: mapEnhancements,
                extraSpawnBonusesApplied: extraSpawnBonusesApplied,
                scalingExtraSpawns: scalingExtraSpawns);
        }
    }

    /// <summary>
    /// Non-enemy, non-item prefab rows pinned to a specific spawn point (signposts, entrances, etc.).
    /// These must spawn before shuffled enemy rows so overlap/enemy placement cannot steal their point.
    /// </summary>
    private static bool IsFixedPointWorldPrefabRow(SpawnPrefabCount entry)
    {
        if (entry == null || entry.count <= 0)
            return false;
        if (entry.enemyDefinition != null || entry.itemDefinition != null)
            return false;
        if (entry.prefab == null)
            return false;
        return !string.IsNullOrWhiteSpace(entry.spawnPointName);
    }

    private static bool SpawnPlanHasGroupSource(LevelSpawnGroupPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.groupId))
            return true;
        if (plan.spawns == null)
            return false;
        for (int i = 0; i < plan.spawns.Count; i++)
        {
            SpawnPrefabCount e = plan.spawns[i];
            if (e != null && !string.IsNullOrWhiteSpace(e.spawnPointGroupId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Spawns one endurance wave: clears overlap reservation so later waves can reuse the same points.
    /// Returns spawned enemies (roots that have <see cref="EnemyBaseController"/>).
    /// </summary>
    public List<EnemyBaseController> SpawnWavePlans(IReadOnlyList<LevelSpawnGroupPlan> plans)
    {
        var enemies = new List<EnemyBaseController>();
        if (plans == null || plans.Count == 0)
            return enemies;

        _reservedSpawnCells.Clear();

        var groups = FindAllSpawnPointGroups();
        Transform parent = ResolveSpawnParent();
        var spawnedRoots = new List<GameObject>();

        MapNodeDefinition saveDef = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;

        for (int i = 0; i < plans.Count; i++)
        {
            LevelSpawnGroupPlan plan = plans[i];
            if (plan == null || plan.spawns == null || plan.spawns.Count == 0)
                continue;

            if (!SpawnPlanHasGroupSource(plan))
                continue;

            SpawnGroup(
                plan,
                groups,
                parent,
                spawnedRoots,
                levelDefForRespawn: null,
                allowEliteSpawnRoll: false,
                planIndexForSaveKeys: i,
                levelDefForOneShotKeys: saveDef);
        }

        for (int i = 0; i < spawnedRoots.Count; i++)
        {
            GameObject go = spawnedRoots[i];
            if (!go)
                continue;
            EnemyBaseController ec = go.GetComponent<EnemyBaseController>() ??
                                     go.GetComponentInChildren<EnemyBaseController>(true);
            if (ec)
                enemies.Add(ec);
        }

        return enemies;
    }

    /// <summary>
    /// Extra encounter mid-session (tutorial phases, scripted waves). Does not change initial spawn bookkeeping.
    /// </summary>
    public List<EnemyBaseController> SpawnAdditionalGroupPlan(LevelSpawnGroupPlan plan, MapNodeDefinition respawnRulesFrom = null)
    {
        var enemies = new List<EnemyBaseController>();
        if (plan == null || plan.spawns == null || plan.spawns.Count == 0)
            return enemies;
        if (!SpawnPlanHasGroupSource(plan))
            return enemies;

        var groups = FindAllSpawnPointGroups();
        Transform parent = ResolveSpawnParent();
        var spawnedRoots = new List<GameObject>();

        MapNodeDefinition saveDef = respawnRulesFrom;
        if (saveDef == null && GameplayLevelBootstrapper.Instance != null)
            saveDef = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        if (saveDef == null)
            saveDef = ActiveLevelContext.Current;

        SpawnGroup(
            plan,
            groups,
            parent,
            spawnedRoots,
            respawnRulesFrom,
            allowEliteSpawnRoll: false,
            planIndexForSaveKeys: -1,
            levelDefForOneShotKeys: saveDef);

        for (int i = 0; i < spawnedRoots.Count; i++)
        {
            GameObject go = spawnedRoots[i];
            if (!go)
                continue;
            EnemyBaseController ec = go.GetComponent<EnemyBaseController>() ??
                                     go.GetComponentInChildren<EnemyBaseController>(true);
            if (ec)
                enemies.Add(ec);
        }

        return enemies;
    }

    private Transform ResolveSpawnParent()
    {
        if (spawnParentOverride != null)
            return spawnParentOverride;
        if (GameplayLevelBootstrapper.Instance != null)
            return GameplayLevelBootstrapper.Instance.ContentRoot;
        return transform;
    }

    private static Dictionary<string, SpawnPointGroup> FindAllSpawnPointGroups()
    {
        var dict = new Dictionary<string, SpawnPointGroup>(StringComparer.OrdinalIgnoreCase);
        SpawnPointGroup[] all = FindObjectsByType<SpawnPointGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            SpawnPointGroup g = all[i];
            if (!g || string.IsNullOrWhiteSpace(g.groupId))
                continue;

            if (!dict.ContainsKey(g.groupId))
                dict.Add(g.groupId, g);
        }

        return dict;
    }

    private static string BuildLevelItemOneShotKey(
        MapNodeDefinition levelDef,
        int planIndex,
        int rowIndex,
        int instanceIndex,
        string itemId,
        string designerOverride)
    {
        if (!string.IsNullOrWhiteSpace(designerOverride))
            return designerOverride.Trim();

        string node = levelDef != null && !string.IsNullOrWhiteSpace(levelDef.nodeId)
            ? levelDef.nodeId.Trim()
            : "unknown_node";
        string iid = string.IsNullOrWhiteSpace(itemId) ? "unknown_item" : itemId.Trim();
        int pi = planIndex >= 0 ? planIndex : 0;
        return $"levelItem:{node}:p{pi}:r{rowIndex}:i{instanceIndex}:{iid}";
    }

    public static string BuildPermanentEnemyDeathKey(
        MapNodeDefinition levelDef,
        int planIndex,
        int rowIndex,
        int instanceIndex,
        string enemyId)
    {
        string node = levelDef != null && !string.IsNullOrWhiteSpace(levelDef.nodeId)
            ? levelDef.nodeId.Trim()
            : "unknown_node";
        string eid = string.IsNullOrWhiteSpace(enemyId) ? "unknown_enemy" : enemyId.Trim();
        string planTag = planIndex >= 0 ? $"p{planIndex}" : "pX";
        return $"permDeadEnemy:{node}:{planTag}:r{rowIndex}:i{instanceIndex}:{eid}";
    }

    private bool TryResolveOneSpawnPoint(
        SpawnPrefabCount entry,
        LevelSpawnGroupPlan plan,
        Dictionary<string, GroupSpawnCursor> cursors,
        string gid,
        SpawnPointGroup pointGroup,
        out Transform point,
        out bool hadToReuse,
        bool requireExactNamedPoint = false)
    {
        hadToReuse = false;
        point = null;

        GroupSpawnCursor cursor = GetOrCreateGroupCursor(cursors, gid, pointGroup, plan.shuffleSpawnPoints);
        if (cursor.PointList.Count == 0)
        {
            if (logSpawns)
                Debug.LogWarning($"[LevelSpawnDirector] SpawnPointGroup '{gid}' has no points.", pointGroup);
            return false;
        }

        Transform p = null;
        if (entry != null && !string.IsNullOrWhiteSpace(entry.spawnPointName))
        {
            string wantName = entry.spawnPointName.Trim();
            p = TryResolveSpawnPointByName(pointGroup, wantName);
            if (!p)
            {
                if (logSpawns)
                    Debug.LogWarning(
                        $"[LevelSpawnDirector] No spawn point named '{wantName}' in group '{gid}'.",
                        pointGroup);
                return false;
            }

            if (requireExactNamedPoint)
            {
                point = p;
                return true;
            }

            if (!IsSpawnPointStrictlyFree(p))
            {
                if (logSpawns)
                    Debug.LogWarning(
                        $"[LevelSpawnDirector] Named spawn point '{p.name}' is not free — using cursor order.",
                        pointGroup);
                p = null;
            }
        }

        if (!p)
        {
            p = PickNextAvailablePoint(
                cursor.PointList,
                ref cursor.Cursor,
                out hadToReuse,
                gid,
                pointGroup);
        }

        point = p;
        return p != null;
    }

    private void SpawnGroup(
        LevelSpawnGroupPlan plan,
        Dictionary<string, SpawnPointGroup> groupsById,
        Transform parent,
        List<GameObject> collectRoots,
        MapNodeDefinition levelDefForRespawn,
        bool allowEliteSpawnRoll,
        int planIndexForSaveKeys = -1,
        MapNodeDefinition levelDefForOneShotKeys = null,
        Func<SpawnPrefabCount, bool> rowFilter = null,
        bool requireExactNamedPoint = false,
        MapEnhancementAggregate mapEnhancements = null,
        HashSet<string> extraSpawnBonusesApplied = null,
        Dictionary<string, int> scalingExtraSpawns = null)
    {
        if (plan.spawns == null)
            return;

        MapNodeDefinition saveDef = levelDefForOneShotKeys
                                    ?? GameplayLevelBootstrapper.Instance?.ActiveDefinition
                                    ?? ActiveLevelContext.Current;

        var cursors = new Dictionary<string, GroupSpawnCursor>(StringComparer.OrdinalIgnoreCase);
        int totalSpawned = 0;

        for (int rowIdx = 0; rowIdx < plan.spawns.Count; rowIdx++)
        {
            SpawnPrefabCount entry = plan.spawns[rowIdx];
            if (entry == null || entry.count <= 0)
                continue;
            if (rowFilter != null && !rowFilter(entry))
                continue;

            if (entry.itemDefinition != null)
            {
                string itemId = entry.itemDefinition.itemId != null ? entry.itemDefinition.itemId.Trim() : string.Empty;
                if (string.IsNullOrEmpty(itemId))
                {
                    if (logSpawns)
                        Debug.LogWarning($"[LevelSpawnDirector] ItemDefinition '{entry.itemDefinition.name}' has no itemId — skipped.", entry.itemDefinition);
                    continue;
                }

                string itemGid = ResolveSpawnGroupId(plan, entry);
                if (string.IsNullOrWhiteSpace(itemGid))
                {
                    if (logSpawns)
                        Debug.LogWarning("[LevelSpawnDirector] Spawn row has no Group Id (set plan default or Spawn Point Group Id on the row).", this);
                    continue;
                }

                if (!groupsById.TryGetValue(itemGid, out SpawnPointGroup itemPointGroup) || itemPointGroup == null)
                {
                    if (logSpawns)
                        Debug.LogWarning($"[LevelSpawnDirector] Missing SpawnPointGroup for groupId='{itemGid}'", this);
                    continue;
                }

                int stack = Mathf.Max(1, entry.itemAmount);
                float itemRespawnSec = Mathf.Max(0f, entry.itemRespawnTimer);
                bool respawnsAfterPickup = itemRespawnSec >= 0.01f;

                for (int c = 0; c < entry.count; c++)
                {
                    string key = respawnsAfterPickup
                        ? null
                        : BuildLevelItemOneShotKey(saveDef, planIndexForSaveKeys, rowIdx, c, itemId, entry.levelOneShotPickupKey);
                    if (!respawnsAfterPickup && LevelItemPickupSaveStore.IsClaimed(key))
                        continue;

                    if (!TryResolveOneSpawnPoint(
                            entry,
                            plan,
                            cursors,
                            itemGid,
                            itemPointGroup,
                            out Transform p,
                            out bool hadToReuse,
                            requireExactNamedPoint))
                        continue;

                    DropManager dm = DropManager.Instance;
                    if (dm == null)
                    {
                        if (logSpawns)
                            Debug.LogWarning("[LevelSpawnDirector] DropManager missing — cannot spawn level item pickup.", this);
                        continue;
                    }

                    dm.SpawnPlacedLevelPickup(
                        entry.itemDefinition,
                        stack,
                        p.position,
                        parent,
                        alignSpawnPointToColliderBottom,
                        key,
                        itemRespawnSec);

                    if (preventOverlappingSpawns)
                        ReservePoint(p.position);

                    totalSpawned++;

                    if (logSpawns)
                    {
                        string keyLabel = key ?? "(none — respawns after pickup)";
                        Debug.Log(
                            $"[LevelSpawnDirector] Spawned item '{itemId}' x{stack} at group '{itemGid}' point '{p.name}' pos={p.position} (one-shot key '{keyLabel}', respawnTimer={itemRespawnSec:F1}s)",
                            this);
                        if (hadToReuse)
                            Debug.LogWarning($"[LevelSpawnDirector] Group '{itemGid}' ran out of free spawn points; reusing a location. Add more points to avoid overlaps.", itemPointGroup);
                    }
                }

                continue;
            }

            if (!entry.TryResolveSpawnPrefab(out GameObject prefabAsset, out EnemyDefinition defForInit, this, logSpawns))
                continue;

            string gid = ResolveSpawnGroupId(plan, entry);
            if (string.IsNullOrWhiteSpace(gid))
            {
                if (logSpawns)
                    Debug.LogWarning("[LevelSpawnDirector] Spawn row has no Group Id (set plan default or Spawn Point Group Id on the row).", this);
                continue;
            }

            if (!groupsById.TryGetValue(gid, out SpawnPointGroup pointGroup) || pointGroup == null)
            {
                if (logSpawns)
                    Debug.LogWarning($"[LevelSpawnDirector] Missing SpawnPointGroup for groupId='{gid}'", this);
                continue;
            }

            GroupSpawnCursor cursorProbe = GetOrCreateGroupCursor(cursors, gid, pointGroup, plan.shuffleSpawnPoints);
            if (cursorProbe.PointList.Count == 0)
            {
                if (logSpawns)
                    Debug.LogWarning($"[LevelSpawnDirector] SpawnPointGroup '{gid}' has no points.", pointGroup);
                continue;
            }

            if (!prefabAsset.activeSelf && logSpawns)
                Debug.LogWarning($"[LevelSpawnDirector] Prefab '{prefabAsset.name}' is inactive in the Project. Instances would be invisible unless activated.", prefabAsset);

            int spawnCount = GetEnemySpawnCountWithMapEnhancementExtras(
                entry,
                defForInit,
                mapEnhancements,
                scalingExtraSpawns,
                extraSpawnBonusesApplied);

            for (int c = 0; c < spawnCount; c++)
            {
                string permDeathKey = null;
                if (defForInit != null && defForInit.cannotRespawn && saveDef != null &&
                    !string.IsNullOrWhiteSpace(saveDef.nodeId))
                {
                    permDeathKey = BuildPermanentEnemyDeathKey(saveDef, planIndexForSaveKeys, rowIdx, c, defForInit.enemyId);
                    if (PermanentEnemyDeathSaveStore.IsPermanentlyDead(permDeathKey))
                        continue;
                }

                if (!TryResolveOneSpawnPoint(
                        entry,
                        plan,
                        cursors,
                        gid,
                        pointGroup,
                        out Transform p,
                        out bool hadToReuse,
                        requireExactNamedPoint))
                    continue;

                GameObject inst = SpawnEnemyInstanceAt(
                    p,
                    prefabAsset,
                    defForInit,
                    parent,
                    allowEliteSpawnRoll,
                    allowEliteSpawnRoll ? levelDefForRespawn : null,
                    out GameObject bonusElite);
                if (!inst)
                    continue;

                ApplySpawnRowOverrides(inst, entry);

                collectRoots?.Add(inst);

                if (alignSpawnPointToColliderBottom)
                    AlignBottomOfColliderToPoint(inst.transform, p.position);

                if (defForInit == null && saveDef != null && !string.IsNullOrWhiteSpace(entry.spawnPointName))
                    WorldObjectMovable.TryBindSpawnedInstance(inst, saveDef.nodeId, entry.spawnPointName, p.position);

                if (preventOverlappingSpawns)
                    ReservePoint(p.position);

                totalSpawned++;

                TryBindEnemyRespawn(
                    inst,
                    levelDefForRespawn,
                    gid,
                    plan.shuffleSpawnPoints,
                    prefabAsset,
                    defForInit,
                    entry.spawnPointName,
                    entry.respawnUntilSimpleWavesStart);

                if (bonusElite != null)
                {
                    collectRoots?.Add(bonusElite);

                    if (alignSpawnPointToColliderBottom)
                        AlignBottomOfColliderToPoint(bonusElite.transform, p.position);

                    TryBindEnemyRespawn(
                        bonusElite,
                        levelDefForRespawn,
                        gid,
                        plan.shuffleSpawnPoints,
                        prefabAsset,
                        defForInit,
                        entry.spawnPointName,
                        entry.respawnUntilSimpleWavesStart);
                }

                if (!string.IsNullOrEmpty(permDeathKey))
                {
                    var deathMarker = inst.AddComponent<EnemyPermanentDeathMarker>();
                    deathMarker.Initialize(permDeathKey);
                }

                if (logSpawns)
                {
                    Debug.Log(
                        $"[LevelSpawnDirector] Spawned '{prefabAsset.name}' -> '{inst.name}' at group '{gid}' point '{p.name}' pos={p.position}",
                        inst);
                    if (hadToReuse)
                        Debug.LogWarning($"[LevelSpawnDirector] Group '{gid}' ran out of free spawn points; reusing a location. Add more points to avoid overlaps.", pointGroup);
                }
            }
        }
    }

    /// <summary>
    /// Called by <see cref="EnemySpawnSource"/> when respawn is enabled on the active <see cref="MapNodeDefinition"/>.
    /// </summary>
    public void QueueEnemyRespawn(
        MapNodeDefinition mapNode,
        string spawnPointGroupId,
        bool shuffleSpawnPointsFromPlan,
        GameObject prefabAsset,
        EnemyDefinition enemyDefinition,
        string spawnPointName = null,
        bool respawnUntilSimpleWavesStart = false)
    {
        if (!mapNode || mapNode.enemyRespawnDelaySeconds < 0.01f)
            return;
        if (!prefabAsset)
            return;
        if (!ShouldAllowRespawnNow(mapNode, respawnUntilSimpleWavesStart))
            return;

        StartCoroutine(CoRespawnAfterDelay(
            mapNode,
            spawnPointGroupId,
            shuffleSpawnPointsFromPlan,
            prefabAsset,
            enemyDefinition,
            spawnPointName,
            respawnUntilSimpleWavesStart));
    }

    private void ClearPendingRespawnsForNewLevel()
    {
        _pendingRespawns.Clear();
        StopRespawnQueueCoroutineOnly();
    }

    private void StopRespawnQueueCoroutineOnly()
    {
        if (_respawnQueueCoroutine != null)
        {
            StopCoroutine(_respawnQueueCoroutine);
            _respawnQueueCoroutine = null;
        }
    }

    private void EnqueuePendingRespawn(
        string nodeId,
        string spawnPointGroupId,
        bool shuffleSpawnPointsFromPlan,
        GameObject prefabAsset,
        EnemyDefinition enemyDefinition,
        string spawnPointName,
        bool respawnUntilSimpleWavesStart)
    {
        _pendingRespawns.Add(new PendingRespawn
        {
            NodeId = nodeId,
            SpawnPointGroupId = spawnPointGroupId,
            ShuffleSpawnPointsFromPlan = shuffleSpawnPointsFromPlan,
            SpawnPointName = spawnPointName,
            PrefabAsset = prefabAsset,
            EnemyDefinition = enemyDefinition,
            RespawnUntilSimpleWavesStart = respawnUntilSimpleWavesStart,
        });

        if (_respawnQueueCoroutine == null && isActiveAndEnabled)
            _respawnQueueCoroutine = StartCoroutine(CoProcessRespawnQueue());
    }

    private IEnumerator CoProcessRespawnQueue()
    {
        try
        {
            while (_pendingRespawns.Count > 0)
            {
                bool spawnedAny = false;
                for (int i = 0; i < _pendingRespawns.Count;)
                {
                    PendingRespawn pr = _pendingRespawns[i];
                    RespawnAttemptOutcome outcome = TrySpawnRespawnAfterDelay(
                        pr.NodeId,
                        pr.SpawnPointGroupId,
                        pr.ShuffleSpawnPointsFromPlan,
                        pr.PrefabAsset,
                        pr.EnemyDefinition,
                        pr.SpawnPointName,
                        pr.RespawnUntilSimpleWavesStart,
                        logWhenNoFreePoint: false);

                    if (outcome == RespawnAttemptOutcome.Spawned)
                    {
                        _pendingRespawns.RemoveAt(i);
                        spawnedAny = true;
                    }
                    else if (outcome == RespawnAttemptOutcome.NoFreePoint)
                    {
                        i++;
                    }
                    else
                    {
                        _pendingRespawns.RemoveAt(i);
                    }
                }

                if (_pendingRespawns.Count == 0)
                    yield break;

                if (!spawnedAny)
                    yield return new WaitForSeconds(respawnQueueRetryIntervalSec);
            }
        }
        finally
        {
            _respawnQueueCoroutine = null;
        }
    }

    private RespawnAttemptOutcome TrySpawnRespawnAfterDelay(
        string expectedNodeId,
        string spawnPointGroupId,
        bool shuffleSpawnPointsFromPlan,
        GameObject prefabAsset,
        EnemyDefinition enemyDefinition,
        string spawnPointName,
        bool respawnUntilSimpleWavesStart,
        bool logWhenNoFreePoint)
    {
        MapNodeDefinition active = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;

        if (active == null || active.nodeId != expectedNodeId || !ShouldAllowRespawnNow(active, respawnUntilSimpleWavesStart))
            return RespawnAttemptOutcome.AbortedInvalidContext;

        if (!prefabAsset)
            return RespawnAttemptOutcome.AbortedInvalidContext;

        var groupsById = FindAllSpawnPointGroups();
        if (!groupsById.TryGetValue(spawnPointGroupId, out SpawnPointGroup pointGroup) || pointGroup == null)
            return RespawnAttemptOutcome.AbortedInvalidContext;

        Transform p = PickSpawnPointForRespawn(pointGroup, shuffleSpawnPointsFromPlan, spawnPointName);
        if (!p)
        {
            if (logWhenNoFreePoint && logSpawns)
            {
                Debug.LogWarning(
                    $"[LevelSpawnDirector] No free spawn point for respawn; all points in group '{pointGroup.groupId}' are occupied by enemies. Queued for retry.",
                    pointGroup);
            }

            return RespawnAttemptOutcome.NoFreePoint;
        }

        Transform parent = ResolveSpawnParent();
        GameObject inst = SpawnEnemyInstanceAt(
            p,
            prefabAsset,
            enemyDefinition,
            parent,
            allowEliteRoll: true,
            nodeForEliteChance: active,
            out GameObject bonusElite);
        if (!inst)
            return RespawnAttemptOutcome.AbortedInvalidContext;

        if (alignSpawnPointToColliderBottom)
            AlignBottomOfColliderToPoint(inst.transform, p.position);

        if (preventOverlappingSpawns)
            ReservePoint(p.position);

        TryBindEnemyRespawn(
            inst,
            active,
            spawnPointGroupId,
            shuffleSpawnPointsFromPlan,
            prefabAsset,
            enemyDefinition,
            spawnPointName,
            respawnUntilSimpleWavesStart);

        if (bonusElite != null)
        {
            if (alignSpawnPointToColliderBottom)
                AlignBottomOfColliderToPoint(bonusElite.transform, p.position);

            TryBindEnemyRespawn(
                bonusElite,
                active,
                spawnPointGroupId,
                shuffleSpawnPointsFromPlan,
                prefabAsset,
                enemyDefinition,
                spawnPointName,
                respawnUntilSimpleWavesStart);
        }

        if (logSpawns)
            Debug.Log(
                $"[LevelSpawnDirector] Respawned '{prefabAsset.name}' -> '{inst.name}' at group '{spawnPointGroupId}' point '{p.name}' pos={p.position}",
                inst);

        return RespawnAttemptOutcome.Spawned;
    }

    private IEnumerator CoRespawnAfterDelay(
        MapNodeDefinition mapNode,
        string spawnPointGroupId,
        bool shuffleSpawnPointsFromPlan,
        GameObject prefabAsset,
        EnemyDefinition enemyDefinition,
        string spawnPointName,
        bool respawnUntilSimpleWavesStart)
    {
        string nodeId = mapNode.nodeId;
        float delay = GetEffectiveRespawnDelayForNode(mapNode);
        yield return new WaitForSeconds(delay);

        if (!this)
            yield break;

        RespawnAttemptOutcome outcome = TrySpawnRespawnAfterDelay(
            nodeId,
            spawnPointGroupId,
            shuffleSpawnPointsFromPlan,
            prefabAsset,
            enemyDefinition,
            spawnPointName,
            respawnUntilSimpleWavesStart,
            logWhenNoFreePoint: true);

        if (outcome == RespawnAttemptOutcome.NoFreePoint)
        {
            EnqueuePendingRespawn(
                nodeId,
                spawnPointGroupId,
                shuffleSpawnPointsFromPlan,
                prefabAsset,
                enemyDefinition,
                spawnPointName,
                respawnUntilSimpleWavesStart);
        }
    }

    private static bool ShouldAllowRespawnBinding(MapNodeDefinition node, bool respawnUntilSimpleWavesStart)
    {
        if (node == null)
            return false;
        if (node.enemyRespawnEnabled)
            return true;
        if (!respawnUntilSimpleWavesStart)
            return false;
        if (node.simpleCombatWaves == null || node.simpleCombatWaves.Count == 0)
            return false;
        return !SimpleCombatWaveDirector.HaveSimpleWavesStartedFor(node);
    }

    private static bool ShouldAllowRespawnNow(MapNodeDefinition node, bool respawnUntilSimpleWavesStart)
    {
        if (node == null)
            return false;
        if (node.enemyRespawnEnabled)
            return true;
        if (!respawnUntilSimpleWavesStart)
            return false;
        if (node.simpleCombatWaves == null || node.simpleCombatWaves.Count == 0)
            return false;
        return !SimpleCombatWaveDirector.HaveSimpleWavesStartedFor(node);
    }

    private Transform PickSpawnPointForRespawn(SpawnPointGroup group, bool shuffleSpawnPointsFromPlan, string spawnPointName)
    {
        if (!string.IsNullOrWhiteSpace(spawnPointName))
        {
            Transform named = TryResolveSpawnPointByName(group, spawnPointName.Trim());
            if (named && !IsSpawnPointOccupiedByEnemy(named.position))
                return named;
        }

        IReadOnlyList<Transform> raw = group.Points;
        var list = new List<Transform>();
        for (int i = 0; i < raw.Count; i++)
        {
            if (raw[i])
                list.Add(raw[i]);
        }

        if (list.Count == 0)
            return null;

        if (shuffleSpawnPointsFromPlan)
            Shuffle(list);

        for (int i = 0; i < list.Count; i++)
        {
            Transform p = list[i];
            if (!p)
                continue;
            if (!IsSpawnPointOccupiedByEnemy(p.position))
                return p;
        }

        return null;
    }

    private static Transform TryResolveSpawnPointByName(SpawnPointGroup group, string name)
    {
        if (group == null || string.IsNullOrWhiteSpace(name))
            return null;

        IReadOnlyList<Transform> raw = group.Points;
        for (int i = 0; i < raw.Count; i++)
        {
            Transform t = raw[i];
            if (t && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private bool IsSpawnPointStrictlyFree(Transform p)
    {
        if (!p)
            return false;
        if (IsSpawnPointOccupiedByEnemy(p.position))
            return false;
        if (preventOverlappingSpawns && IsReserved(p.position))
            return false;
        return true;
    }

    private GameObject SpawnEnemyInstanceAt(
        Transform spawnPoint,
        GameObject prefabAsset,
        EnemyDefinition defForInit,
        Transform parent,
        bool allowEliteRoll,
        MapNodeDefinition nodeForEliteChance,
        out GameObject bonusEliteInstance)
    {
        bonusEliteInstance = null;

        GameObject inst = Instantiate(prefabAsset, spawnPoint.position, spawnPoint.rotation, parent);
        if (!inst.activeSelf)
            inst.SetActive(true);

        bool spawnedAsElite = false;
        if (defForInit != null)
            spawnedAsElite = ApplyEnemyDefinitionAfterSpawn(inst, defForInit, allowEliteRoll, nodeForEliteChance);

        if (spawnedAsElite && allowEliteRoll && nodeForEliteChance != null && defForInit != null)
            bonusEliteInstance = TrySpawnBonusEliteDouble(spawnPoint, prefabAsset, defForInit, parent, nodeForEliteChance);

        return inst;
    }

    private GameObject TrySpawnBonusEliteDouble(
        Transform spawnPoint,
        GameObject prefabAsset,
        EnemyDefinition defForInit,
        Transform parent,
        MapNodeDefinition nodeForEliteChance)
    {
        MapEnhancementAggregate aggregate = MapEnhancementService.BuildAggregate(nodeForEliteChance);
        float doubleChance = aggregate != null ? Mathf.Clamp01(aggregate.eliteDoubleSpawnChance) : 0f;
        if (doubleChance <= 0.001f || UnityEngine.Random.value >= doubleChance)
            return null;

        GameObject bonus = Instantiate(prefabAsset, spawnPoint.position, spawnPoint.rotation, parent);
        if (!bonus.activeSelf)
            bonus.SetActive(true);

        ApplyEnemyDefinitionAfterSpawn(bonus, defForInit, allowEliteRoll: false, nodeForEliteChance: null, forceElite: true);

        if (logSpawns)
        {
            Debug.Log(
                $"[LevelSpawnDirector] Map enhancement elite double spawn: '{defForInit.enemyId}' at '{spawnPoint.name}'.",
                bonus);
        }

        return bonus;
    }

    private bool ApplyEnemyDefinitionAfterSpawn(
        GameObject instance,
        EnemyDefinition def,
        bool allowEliteRoll,
        MapNodeDefinition nodeForEliteChance,
        bool forceElite = false)
    {
        if (!def || !instance)
            return false;

        bool spawnAsElite = forceElite;
        if (!spawnAsElite && allowEliteRoll && nodeForEliteChance != null && nodeForEliteChance.eliteSpawnChance > 0f)
        {
            MapEnhancementAggregate aggregate = MapEnhancementService.BuildAggregate(nodeForEliteChance);
            float effectiveChance = MapEnhancementService.GetEffectiveEliteSpawnChance(
                nodeForEliteChance.eliteSpawnChance,
                aggregate);
            spawnAsElite = UnityEngine.Random.value < effectiveChance;
        }

        EnemyBaseController ec = instance.GetComponent<EnemyBaseController>() ??
                                  instance.GetComponentInChildren<EnemyBaseController>(true);
        if (ec != null)
        {
            ec.InitializeFromDefinition(def, spawnAsElite);
            return spawnAsElite;
        }

        if (logSpawns)
        {
            Debug.LogWarning(
                $"[LevelSpawnDirector] EnemyDefinition '{def.name}' was used but instance '{instance.name}' has no EnemyBaseController (root or children).",
                instance);
        }

        return false;
    }

    /// <summary>
    /// Adds map-enhancement and scaling extra spawns to the first spawn row for each enemy id (AllSpawns / spawnGroupPlans).
    /// Extras use the same spawn, elite, and respawn rules as the base row count.
    /// </summary>
    private static int GetEnemySpawnCountWithMapEnhancementExtras(
        SpawnPrefabCount entry,
        EnemyDefinition defForInit,
        MapEnhancementAggregate aggregate,
        IReadOnlyDictionary<string, int> scalingExtraSpawns,
        HashSet<string> extraBonusesApplied)
    {
        int count = entry != null ? entry.count : 0;
        if (defForInit == null || string.IsNullOrWhiteSpace(defForInit.enemyId))
            return count;

        string enemyId = defForInit.enemyId.Trim();
        if (extraBonusesApplied != null && extraBonusesApplied.Contains(enemyId))
            return count;

        int extra = 0;
        if (aggregate != null &&
            aggregate.extraSpawnsByEnemyId.TryGetValue(enemyId, out int enhancementExtra))
            extra += enhancementExtra;

        if (scalingExtraSpawns != null &&
            scalingExtraSpawns.TryGetValue(enemyId, out int scalingExtra))
            extra += scalingExtra;

        if (extra <= 0)
            return count;

        extraBonusesApplied?.Add(enemyId);
        return count + extra;
    }

    private void TryBindEnemyRespawn(
        GameObject inst,
        MapNodeDefinition levelDefForRespawn,
        string gid,
        bool shuffleSpawnPoints,
        GameObject prefabAsset,
        EnemyDefinition defForInit,
        string spawnPointName,
        bool respawnUntilSimpleWavesStart)
    {
        if (!inst || levelDefForRespawn == null)
            return;

        bool allowRespawn =
            levelDefForRespawn.enemyRespawnDelaySeconds >= 0.01f &&
            ShouldAllowRespawnBinding(levelDefForRespawn, respawnUntilSimpleWavesStart) &&
            (defForInit == null || !defForInit.cannotRespawn);
        if (!allowRespawn)
            return;

        EnemyBaseController ec = inst.GetComponent<EnemyBaseController>() ??
                                 inst.GetComponentInChildren<EnemyBaseController>(true);
        if (ec == null)
            return;

        var src = inst.AddComponent<EnemySpawnSource>();
        src.Bind(
            this,
            levelDefForRespawn,
            gid,
            shuffleSpawnPoints,
            prefabAsset,
            defForInit,
            spawnPointName,
            respawnUntilSimpleWavesStart);
    }

    private static string ResolveSpawnGroupId(LevelSpawnGroupPlan plan, SpawnPrefabCount entry)
    {
        if (entry != null && !string.IsNullOrWhiteSpace(entry.spawnPointGroupId))
            return entry.spawnPointGroupId.Trim();
        return plan.groupId != null ? plan.groupId.Trim() : string.Empty;
    }

    private static void ApplySpawnRowOverrides(GameObject instance, SpawnPrefabCount entry)
    {
        if (instance == null || entry == null)
            return;

        if (!string.IsNullOrWhiteSpace(entry.portalTargetMapNodeId))
        {
            MapNodePortalTeleporter[] portals = instance.GetComponentsInChildren<MapNodePortalTeleporter>(true);
            string nodeId = entry.portalTargetMapNodeId.Trim();
            for (int i = 0; i < portals.Length; i++)
            {
                MapNodePortalTeleporter portal = portals[i];
                if (portal == null)
                    continue;
                portal.SetTargetMapNodeId(nodeId, clearTargetMapAsset: true);
            }
        }

        if (!string.IsNullOrWhiteSpace(entry.teleporterLinkId))
        {
            InMapTeleporter[] teleporters = instance.GetComponentsInChildren<InMapTeleporter>(true);
            string linkId = entry.teleporterLinkId.Trim();
            for (int i = 0; i < teleporters.Length; i++)
            {
                InMapTeleporter teleporter = teleporters[i];
                if (teleporter == null)
                    continue;
                teleporter.SetTeleporterLinkId(linkId);
                teleporter.SetDestinationLabel(entry.teleporterDestinationLabel);
            }
        }
    }

    private void SpawnInMapTeleporters(
        MapNodeDefinition def,
        Dictionary<string, SpawnPointGroup> groupsById,
        Transform parent)
    {
        if (def?.inMapTeleporters == null || def.inMapTeleporters.Count == 0)
            return;

        for (int i = 0; i < def.inMapTeleporters.Count; i++)
        {
            InMapTeleporterPlan plan = def.inMapTeleporters[i];
            if (plan == null || string.IsNullOrWhiteSpace(plan.teleporterLinkId) ||
                string.IsNullOrWhiteSpace(plan.spawnPointName))
            {
                continue;
            }

            GameObject prefab = plan.teleporterPrefab != null ? plan.teleporterPrefab : defaultInMapTeleporterPrefab;
            if (!prefab)
            {
                if (logSpawns)
                    Debug.LogWarning("[LevelSpawnDirector] In-map teleporter plan has no prefab and no default is assigned.", this);
                continue;
            }

            string groupId = plan.spawnPointGroupId?.Trim() ?? string.Empty;
            SpawnPointGroup pointGroup = null;
            if (!string.IsNullOrEmpty(groupId))
            {
                groupsById.TryGetValue(groupId, out pointGroup);
                if (!pointGroup && logSpawns)
                {
                    Debug.LogWarning(
                        $"[LevelSpawnDirector] In-map teleporter group '{groupId}' not found for link '{plan.teleporterLinkId}'.",
                        this);
                    continue;
                }
            }

            if (!pointGroup)
            {
                foreach (KeyValuePair<string, SpawnPointGroup> kv in groupsById)
                {
                    SpawnPointGroup candidate = kv.Value;
                    if (candidate == null)
                        continue;
                    if (TryResolveSpawnPointByName(candidate, plan.spawnPointName.Trim()) != null)
                    {
                        pointGroup = candidate;
                        break;
                    }
                }
            }

            if (!pointGroup)
            {
                if (logSpawns)
                    Debug.LogWarning(
                        $"[LevelSpawnDirector] No spawn group contains point '{plan.spawnPointName}' for teleporter '{plan.teleporterLinkId}'.",
                        this);
                continue;
            }

            Transform point = TryResolveSpawnPointByName(pointGroup, plan.spawnPointName.Trim());
            if (!point)
            {
                if (logSpawns)
                    Debug.LogWarning(
                        $"[LevelSpawnDirector] Spawn point '{plan.spawnPointName}' not found for teleporter '{plan.teleporterLinkId}'.",
                        this);
                continue;
            }

            GameObject instance = Instantiate(prefab, point.position, point.rotation, parent);
            if (alignSpawnPointToColliderBottom)
                AlignBottomOfColliderToPoint(instance.transform, point.position);

            InMapTeleporter[] teleporters = instance.GetComponentsInChildren<InMapTeleporter>(true);
            string linkId = plan.teleporterLinkId.Trim();
            for (int t = 0; t < teleporters.Length; t++)
            {
                if (teleporters[t] != null)
                {
                    teleporters[t].SetTeleporterLinkId(linkId);
                    teleporters[t].SetDestinationLabel(plan.destinationLabel);
                }
            }

            if (logSpawns)
                Debug.Log(
                    $"[LevelSpawnDirector] Spawned in-map teleporter '{linkId}' at '{plan.spawnPointName}'.",
                    instance);
        }
    }

    private GroupSpawnCursor GetOrCreateGroupCursor(
        Dictionary<string, GroupSpawnCursor> byId,
        string gid,
        SpawnPointGroup pointGroup,
        bool shuffle)
    {
        if (byId.TryGetValue(gid, out GroupSpawnCursor cur))
            return cur;

        cur = new GroupSpawnCursor();
        IReadOnlyList<Transform> rawPoints = pointGroup.Points;
        if (rawPoints != null)
        {
            for (int i = 0; i < rawPoints.Count; i++)
            {
                if (rawPoints[i])
                    cur.PointList.Add(rawPoints[i]);
            }
        }

        if (shuffle && cur.PointList.Count > 0)
            Shuffle(cur.PointList);

        byId[gid] = cur;
        return cur;
    }

    private sealed class GroupSpawnCursor
    {
        public readonly List<Transform> PointList = new();
        public int Cursor;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private Transform PickNextAvailablePoint(
        List<Transform> points,
        ref int cursor,
        out bool hadToReuse,
        string groupIdForLog,
        SpawnPointGroup pointGroupForLog)
    {
        hadToReuse = false;
        if (points == null || points.Count == 0)
            return null;

        int start = cursor;

        for (int tries = 0; tries < points.Count; tries++)
        {
            Transform p = points[cursor % points.Count];
            cursor++;
            if (IsSpawnPointStrictlyFree(p))
                return p;
        }

        if (preventOverlappingSpawns)
        {
            cursor = start;
            for (int tries = 0; tries < points.Count; tries++)
            {
                Transform p = points[cursor % points.Count];
                cursor++;
                if (!p)
                    continue;
                if (IsSpawnPointOccupiedByEnemy(p.position))
                    continue;
                hadToReuse = true;
                return p;
            }
        }

        if (logSpawns)
        {
            Debug.LogWarning(
                $"[LevelSpawnDirector] No free spawn point for group '{groupIdForLog}'; all points are occupied by enemies.",
                pointGroupForLog);
        }

        return null;
    }

    private bool IsSpawnPointOccupiedByEnemy(Vector3 worldPos)
    {
        float r = Mathf.Max(0.01f, enemySpawnOccupancyRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, r);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (!col || !col.enabled)
                continue;

            EnemyBaseController ec = col.GetComponentInParent<EnemyBaseController>();
            if (ec != null && !ec.IsDead)
                return true;
        }

        return false;
    }

    private bool IsReserved(Vector3 worldPos)
    {
        return _reservedSpawnCells.Contains(ToCell(worldPos));
    }

    private void ReservePoint(Vector3 worldPos)
    {
        _reservedSpawnCells.Add(ToCell(worldPos));
    }

    private Vector2Int ToCell(Vector3 worldPos)
    {
        float s = Mathf.Max(0.001f, overlapGridSize);
        int x = Mathf.RoundToInt(worldPos.x / s);
        int y = Mathf.RoundToInt(worldPos.y / s);
        return new Vector2Int(x, y);
    }

    public static void AlignBottomOfColliderToPointPublic(Transform root, Vector3 worldPoint) =>
        AlignBottomOfColliderToPoint(root, worldPoint);

    private static void AlignBottomOfColliderToPoint(Transform root, Vector3 worldPoint)
    {
        if (root == null)
            return;

        bool hasAny = false;
        float bottomY = float.PositiveInfinity;

        Collider2D[] c2 = root.GetComponentsInChildren<Collider2D>(includeInactive: true);
        for (int i = 0; i < c2.Length; i++)
        {
            if (!c2[i]) continue;
            hasAny = true;
            bottomY = Mathf.Min(bottomY, c2[i].bounds.min.y);
        }

        if (!hasAny)
        {
            Collider[] c3 = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < c3.Length; i++)
            {
                if (!c3[i]) continue;
                hasAny = true;
                bottomY = Mathf.Min(bottomY, c3[i].bounds.min.y);
            }
        }

        if (!hasAny || float.IsInfinity(bottomY))
            return;

        float dy = worldPoint.y - bottomY;
        if (Mathf.Abs(dy) < 0.0001f)
            return;

        Vector3 pos = root.position;
        pos.y += dy;
        root.position = pos;
    }
}

