using UnityEngine;

public enum TownServiceSpawnMode
{
    [Tooltip("Spawn only after the town service has been unlocked (town merchant / station).")]
    UnlockedOnly = 0,

    [Tooltip("Spawn only while the town service is still locked (field outpost NPC).")]
    LockedOnly = 1,
}

/// <summary>
/// Gates <see cref="LevelSpawnDirector"/> prefab rows by <see cref="TownServiceUnlockStore"/>.
/// Attach to town bundles (merchant + stations) with <see cref="UnlockedOnly"/>,
/// and to outpost quest givers with <see cref="LockedOnly"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownServiceSpawnGate : MonoBehaviour
{
    [Tooltip("Town service id (e.g. blacksmith).")]
    public string serviceId = TownServiceIds.Blacksmith;

    public TownServiceSpawnMode spawnWhen = TownServiceSpawnMode.UnlockedOnly;

    public bool ShouldSpawn()
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return true;

        bool unlocked = TownServiceUnlockStore.IsUnlocked(serviceId);
        return spawnWhen == TownServiceSpawnMode.UnlockedOnly ? unlocked : !unlocked;
    }

    public static bool ShouldSpawnPrefab(GameObject prefabAsset)
    {
        if (!prefabAsset)
            return true;

        TownServiceSpawnGate gate = prefabAsset.GetComponent<TownServiceSpawnGate>();
        return gate == null || gate.ShouldSpawn();
    }
}
