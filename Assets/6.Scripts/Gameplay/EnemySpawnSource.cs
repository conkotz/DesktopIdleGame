using UnityEngine;

/// <summary>
/// Marks an enemy spawned by <see cref="LevelSpawnDirector"/> so it can respawn per <see cref="MapNodeDefinition"/>.
/// </summary>
[DisallowMultipleComponent]
public class EnemySpawnSource : MonoBehaviour
{
    private LevelSpawnDirector _director;
    private MapNodeDefinition _mapNode;
    private string _spawnPointGroupId;
    private bool _shuffleSpawnPointsFromPlan;
    private string _spawnPointName;
    private GameObject _prefabAsset;
    private EnemyDefinition _enemyDefinition;

    private EnemyBaseController _enemy;

    public void Bind(
        LevelSpawnDirector director,
        MapNodeDefinition mapNode,
        string spawnPointGroupId,
        bool shuffleSpawnPointsFromPlan,
        GameObject prefabAsset,
        EnemyDefinition enemyDefinition,
        string spawnPointName = null)
    {
        if (_enemy != null)
            _enemy.OnDeath -= HandleDeath;

        _director = director;
        _mapNode = mapNode;
        _spawnPointGroupId = spawnPointGroupId;
        _shuffleSpawnPointsFromPlan = shuffleSpawnPointsFromPlan;
        _spawnPointName = spawnPointName;
        _prefabAsset = prefabAsset;
        _enemyDefinition = enemyDefinition;

        _enemy = GetComponent<EnemyBaseController>() ?? GetComponentInChildren<EnemyBaseController>(true);
        if (_enemy != null)
            _enemy.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (_enemy != null)
            _enemy.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (_director == null || _mapNode == null || string.IsNullOrWhiteSpace(_spawnPointGroupId))
            return;

        _director.QueueEnemyRespawn(
            _mapNode,
            _spawnPointGroupId,
            _shuffleSpawnPointsFromPlan,
            _prefabAsset,
            _enemyDefinition,
            _spawnPointName);
    }
}
