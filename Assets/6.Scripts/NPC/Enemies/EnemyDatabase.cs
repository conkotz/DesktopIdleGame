using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/Enemy Database", fileName = "EnemyDatabase")]
public class EnemyDatabase : ScriptableObject
{
    [SerializeField] private List<EnemyDefinition> enemies = new();

    private Dictionary<string, EnemyDefinition> _map;

    private void OnEnable() => Build();

#if UNITY_EDITOR
    private void OnValidate() => Build();
#endif

    private void Build()
    {
        if (_map == null)
            _map = new Dictionary<string, EnemyDefinition>(64, StringComparer.Ordinal);
        else
            _map.Clear();

        foreach (EnemyDefinition e in enemies)
        {
            if (!e || string.IsNullOrWhiteSpace(e.enemyId))
                continue;
            string key = e.enemyId.Trim();
            if (_map.TryGetValue(key, out EnemyDefinition existing) && existing != e)
            {
                Debug.LogError($"[EnemyDatabase] Duplicate enemyId '{key}'.");
                continue;
            }

            _map[key] = e;
        }
    }

    public EnemyDefinition Get(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return null;
        if (_map == null || _map.Count == 0)
            Build();
        string key = enemyId.Trim();
        return _map.TryGetValue(key, out EnemyDefinition d) ? d : null;
    }

    /// <summary>All authored enemies, sorted by display name.</summary>
    public List<EnemyDefinition> GetAllSortedByDisplayName()
    {
        var result = new List<EnemyDefinition>(enemies != null ? enemies.Count : 0);
        if (enemies == null)
            return result;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyDefinition enemy = enemies[i];
            if (enemy != null)
                result.Add(enemy);
        }

        result.Sort((a, b) => string.Compare(
            a != null ? a.displayName : string.Empty,
            b != null ? b.displayName : string.Empty,
            StringComparison.OrdinalIgnoreCase));
        return result;
    }
}
