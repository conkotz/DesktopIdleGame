using UnityEngine;

/// <summary>
/// Marks a <see cref="EnemyDefinition.cannotRespawn"/> map spawn as cleared in save when the enemy dies.
/// Added by <see cref="LevelSpawnDirector"/>; do not stack with <see cref="EnemySpawnSource"/> for the same enemy.
/// </summary>
[DisallowMultipleComponent]
public class EnemyPermanentDeathMarker : MonoBehaviour
{
    private string _saveKey;
    private EnemyBaseController _enemy;
    private bool _hooked;

    public void Initialize(string saveKey)
    {
        if (_hooked)
            return;

        _saveKey = saveKey;
        _enemy = GetComponent<EnemyBaseController>() ?? GetComponentInChildren<EnemyBaseController>(true);
        if (_enemy == null || string.IsNullOrWhiteSpace(_saveKey))
        {
            Destroy(this);
            return;
        }

        _enemy.OnDeath += OnOwnerDeath;
        _hooked = true;
    }

    private void OnDestroy()
    {
        if (_enemy != null)
            _enemy.OnDeath -= OnOwnerDeath;
    }

    private void OnOwnerDeath()
    {
        PermanentEnemyDeathSaveStore.MarkPermanentlyDead(_saveKey);
    }
}
