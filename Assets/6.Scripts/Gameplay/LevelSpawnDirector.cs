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

    [Header("Logging")]
    [SerializeField] private bool logSpawns = true;

    private bool _hasSpawnedForCurrentLevel;
    private readonly HashSet<Vector2Int> _reservedSpawnCells = new();
    private readonly List<PendingRespawn> _pendingRespawns = new();
    private Coroutine _respawnQueueCoroutine;

    private struct PendingRespawn
    {
        public string NodeId;
        public string SpawnPointGroupId;
        public bool ShuffleSpawnPointsFromPlan;
        public GameObject PrefabAsset;
        public EnemyDefinition EnemyDefinition;
    }

    private enum RespawnAttemptOutcome
    {
        Spawned,
        NoFreePoint,
        AbortedInvalidContext,
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

        StopRespawnQueueCoroutineOnly();
    }

    private void Start()
    {
        // If we enabled after the bootstrapper fired, handle it once.
        if (_hasSpawnedForCurrentLevel)
            return;

        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            SpawnFor(GameplayLevelBootstrapper.Instance.ActiveDefinition);
        else if (ActiveLevelContext.Current != null)
            SpawnFor(ActiveLevelContext.Current);
    }

    private void OnLevelStarted(MapNodeDefinition def)
    {
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
            return;
        }

        if (def.spawnGroupPlans == null || def.spawnGroupPlans.Count == 0)
        {
            return;
        }

        _hasSpawnedForCurrentLevel = true;
        _reservedSpawnCells.Clear();
        ClearPendingRespawnsForNewLevel();

        var groups = FindAllSpawnPointGroups();
        Transform parent = ResolveSpawnParent();

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

            SpawnGroup(plan, groups, parent, null, def);
        }
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

        for (int i = 0; i < plans.Count; i++)
        {
            LevelSpawnGroupPlan plan = plans[i];
            if (plan == null || plan.spawns == null || plan.spawns.Count == 0)
                continue;

            if (!SpawnPlanHasGroupSource(plan))
                continue;

            SpawnGroup(plan, groups, parent, spawnedRoots, null);
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

    private void SpawnGroup(LevelSpawnGroupPlan plan, Dictionary<string, SpawnPointGroup> groupsById, Transform parent, List<GameObject> collectRoots, MapNodeDefinition levelDefForRespawn)
    {
        if (plan.spawns == null)
            return;

        var cursors = new Dictionary<string, GroupSpawnCursor>(StringComparer.OrdinalIgnoreCase);
        int totalSpawned = 0;

        foreach (SpawnPrefabCount entry in plan.spawns)
        {
            if (entry == null || entry.count <= 0)
                continue;

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

            GroupSpawnCursor cursor = GetOrCreateGroupCursor(cursors, gid, pointGroup, plan.shuffleSpawnPoints);
            if (cursor.PointList.Count == 0)
            {
                if (logSpawns)
                    Debug.LogWarning($"[LevelSpawnDirector] SpawnPointGroup '{gid}' has no points.", pointGroup);
                continue;
            }

            if (!prefabAsset.activeSelf && logSpawns)
                Debug.LogWarning($"[LevelSpawnDirector] Prefab '{prefabAsset.name}' is inactive in the Project. Instances would be invisible unless activated.", prefabAsset);

            for (int c = 0; c < entry.count; c++)
            {
                Transform p = PickNextAvailablePoint(
                    cursor.PointList,
                    ref cursor.Cursor,
                    out bool hadToReuse,
                    gid,
                    pointGroup);
                if (!p)
                    continue;

                GameObject inst = SpawnEnemyInstanceAt(p, prefabAsset, defForInit, parent);
                if (!inst)
                    continue;

                collectRoots?.Add(inst);

                if (alignSpawnPointToColliderBottom)
                    AlignBottomOfColliderToPoint(inst.transform, p.position);

                if (preventOverlappingSpawns)
                    ReservePoint(p.position);

                totalSpawned++;

                if (levelDefForRespawn != null
                    && levelDefForRespawn.enemyRespawnEnabled
                    && levelDefForRespawn.enemyRespawnDelaySeconds >= 0.01f)
                {
                    EnemyBaseController ec = inst.GetComponent<EnemyBaseController>() ??
                                             inst.GetComponentInChildren<EnemyBaseController>(true);
                    if (ec != null)
                    {
                        var src = inst.AddComponent<EnemySpawnSource>();
                        src.Bind(this, levelDefForRespawn, gid, plan.shuffleSpawnPoints, prefabAsset, defForInit);
                    }
                }

                if (logSpawns)
                {
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
        EnemyDefinition enemyDefinition)
    {
        if (!mapNode || !mapNode.enemyRespawnEnabled || mapNode.enemyRespawnDelaySeconds < 0.01f)
            return;
        if (!prefabAsset)
            return;

        StartCoroutine(CoRespawnAfterDelay(mapNode, spawnPointGroupId, shuffleSpawnPointsFromPlan, prefabAsset, enemyDefinition));
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
        EnemyDefinition enemyDefinition)
    {
        _pendingRespawns.Add(new PendingRespawn
        {
            NodeId = nodeId,
            SpawnPointGroupId = spawnPointGroupId,
            ShuffleSpawnPointsFromPlan = shuffleSpawnPointsFromPlan,
            PrefabAsset = prefabAsset,
            EnemyDefinition = enemyDefinition,
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
        bool logWhenNoFreePoint)
    {
        MapNodeDefinition active = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;

        if (active == null || active.nodeId != expectedNodeId || !active.enemyRespawnEnabled)
            return RespawnAttemptOutcome.AbortedInvalidContext;

        if (!prefabAsset)
            return RespawnAttemptOutcome.AbortedInvalidContext;

        var groupsById = FindAllSpawnPointGroups();
        if (!groupsById.TryGetValue(spawnPointGroupId, out SpawnPointGroup pointGroup) || pointGroup == null)
            return RespawnAttemptOutcome.AbortedInvalidContext;

        Transform p = PickSpawnPointForRespawn(pointGroup, shuffleSpawnPointsFromPlan);
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
        GameObject inst = SpawnEnemyInstanceAt(p, prefabAsset, enemyDefinition, parent);
        if (!inst)
            return RespawnAttemptOutcome.AbortedInvalidContext;

        if (alignSpawnPointToColliderBottom)
            AlignBottomOfColliderToPoint(inst.transform, p.position);

        if (preventOverlappingSpawns)
            ReservePoint(p.position);

        EnemyBaseController ec = inst.GetComponent<EnemyBaseController>() ??
                                 inst.GetComponentInChildren<EnemyBaseController>(true);
        if (ec != null && active.enemyRespawnEnabled && active.enemyRespawnDelaySeconds >= 0.01f)
        {
            var src = inst.AddComponent<EnemySpawnSource>();
            src.Bind(this, active, spawnPointGroupId, shuffleSpawnPointsFromPlan, prefabAsset, enemyDefinition);
        }

        return RespawnAttemptOutcome.Spawned;
    }

    private IEnumerator CoRespawnAfterDelay(
        MapNodeDefinition mapNode,
        string spawnPointGroupId,
        bool shuffleSpawnPointsFromPlan,
        GameObject prefabAsset,
        EnemyDefinition enemyDefinition)
    {
        string nodeId = mapNode.nodeId;
        float delay = mapNode.enemyRespawnDelaySeconds;
        yield return new WaitForSeconds(delay);

        if (!this)
            yield break;

        RespawnAttemptOutcome outcome = TrySpawnRespawnAfterDelay(
            nodeId,
            spawnPointGroupId,
            shuffleSpawnPointsFromPlan,
            prefabAsset,
            enemyDefinition,
            logWhenNoFreePoint: true);

        if (outcome == RespawnAttemptOutcome.NoFreePoint)
        {
            EnqueuePendingRespawn(nodeId, spawnPointGroupId, shuffleSpawnPointsFromPlan, prefabAsset, enemyDefinition);
        }
    }

    private Transform PickSpawnPointForRespawn(SpawnPointGroup group, bool shuffleSpawnPointsFromPlan)
    {
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

    private GameObject SpawnEnemyInstanceAt(Transform spawnPoint, GameObject prefabAsset, EnemyDefinition defForInit, Transform parent)
    {
        GameObject inst = Instantiate(prefabAsset, spawnPoint.position, spawnPoint.rotation, parent);
        if (!inst.activeSelf)
            inst.SetActive(true);

        if (defForInit != null)
            ApplyEnemyDefinitionAfterSpawn(inst, defForInit);

        return inst;
    }

    private void ApplyEnemyDefinitionAfterSpawn(GameObject instance, EnemyDefinition def)
    {
        if (!def || !instance)
            return;

        EnemyBaseController ec = instance.GetComponent<EnemyBaseController>() ??
                                  instance.GetComponentInChildren<EnemyBaseController>(true);
        if (ec != null)
        {
            ec.InitializeFromDefinition(def);
            return;
        }

        if (logSpawns)
        {
            Debug.LogWarning(
                $"[LevelSpawnDirector] EnemyDefinition '{def.name}' was used but instance '{instance.name}' has no EnemyBaseController (root or children).",
                instance);
        }
    }

    private static string ResolveSpawnGroupId(LevelSpawnGroupPlan plan, SpawnPrefabCount entry)
    {
        if (entry != null && !string.IsNullOrWhiteSpace(entry.spawnPointGroupId))
            return entry.spawnPointGroupId.Trim();
        return plan.groupId != null ? plan.groupId.Trim() : string.Empty;
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

        bool PointIsStrictlyFree(Transform p)
        {
            if (!p)
                return false;
            if (IsSpawnPointOccupiedByEnemy(p.position))
                return false;
            if (preventOverlappingSpawns && IsReserved(p.position))
                return false;
            return true;
        }

        for (int tries = 0; tries < points.Count; tries++)
        {
            Transform p = points[cursor % points.Count];
            cursor++;
            if (PointIsStrictlyFree(p))
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

