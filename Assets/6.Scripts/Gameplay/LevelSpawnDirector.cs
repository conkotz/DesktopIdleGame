using System;
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

    [Header("Logging")]
    [SerializeField] private bool logSpawns = true;

    private bool _hasSpawnedForCurrentLevel;
    private readonly HashSet<Vector2Int> _reservedSpawnCells = new();

    private void OnEnable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;
    }

    private void OnDisable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= OnLevelStarted;
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
            if (logSpawns)
                Debug.Log("[LevelSpawnDirector] EnduranceTrial — one-shot spawn is skipped; waves are driven by EnduranceTrialDirector.", this);
            return;
        }

        if (def.spawnGroupPlans == null || def.spawnGroupPlans.Count == 0)
        {
            if (logSpawns)
                Debug.Log("[LevelSpawnDirector] No spawnGroupPlans on MapNodeDefinition (nothing to spawn).", this);
            return;
        }

        _hasSpawnedForCurrentLevel = true;
        _reservedSpawnCells.Clear();

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

            SpawnGroup(plan, groups, parent, null);
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

            SpawnGroup(plan, groups, parent, spawnedRoots);
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

    private void SpawnGroup(LevelSpawnGroupPlan plan, Dictionary<string, SpawnPointGroup> groupsById, Transform parent, List<GameObject> collectRoots)
    {
        if (plan.spawns == null)
            return;

        var cursors = new Dictionary<string, GroupSpawnCursor>(StringComparer.OrdinalIgnoreCase);
        int totalSpawned = 0;

        foreach (SpawnPrefabCount entry in plan.spawns)
        {
            if (entry == null || !entry.prefab || entry.count <= 0)
                continue;

            string gid = ResolveSpawnGroupId(plan, entry);
            if (string.IsNullOrWhiteSpace(gid))
            {
                if (logSpawns)
                    Debug.LogWarning("[LevelSpawnDirector] Spawn row has no Group Id (set plan default or Spawn Point Group Id on the row).", entry.prefab);
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

            if (!entry.prefab.activeSelf && logSpawns)
                Debug.LogWarning($"[LevelSpawnDirector] Prefab '{entry.prefab.name}' is inactive in the Project. Instances would be invisible unless activated.", entry.prefab);

            for (int c = 0; c < entry.count; c++)
            {
                Transform p = PickNextAvailablePoint(cursor.PointList, ref cursor.Cursor, out bool hadToReuse);
                if (!p)
                    return;

                GameObject inst = Instantiate(entry.prefab, p.position, p.rotation, parent);
                if (!inst.activeSelf)
                    inst.SetActive(true);

                collectRoots?.Add(inst);

                if (alignSpawnPointToColliderBottom)
                    AlignBottomOfColliderToPoint(inst.transform, p.position);

                if (preventOverlappingSpawns)
                    ReservePoint(p.position);

                totalSpawned++;

                if (logSpawns)
                {
                    if (hadToReuse)
                        Debug.LogWarning($"[LevelSpawnDirector] Group '{gid}' ran out of free spawn points; reusing a location. Add more points to avoid overlaps.", pointGroup);
                    Debug.Log($"[LevelSpawnDirector] Spawned group='{gid}' prefab='{entry.prefab.name}' -> '{inst.name}' at '{p.name}'", inst);
                }
            }
        }

        if (logSpawns)
            Debug.Log($"[LevelSpawnDirector] Plan spawned {totalSpawned} instance(s).", this);
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

    private Transform PickNextAvailablePoint(List<Transform> points, ref int cursor, out bool hadToReuse)
    {
        hadToReuse = false;
        if (points == null || points.Count == 0)
            return null;

        if (!preventOverlappingSpawns)
        {
            Transform p = points[cursor % points.Count];
            cursor++;
            return p;
        }

        int start = cursor;
        for (int tries = 0; tries < points.Count; tries++)
        {
            Transform p = points[cursor % points.Count];
            cursor++;
            if (!p) continue;

            if (!IsReserved(p.position))
                return p;
        }

        // All points appear reserved; fall back to deterministic reuse so we still spawn.
        cursor = start + 1;
        hadToReuse = true;
        return points[start % points.Count];
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

