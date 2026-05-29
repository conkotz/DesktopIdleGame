using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Live <see cref="EnemyBaseController"/> instances for combat queries without per-cast scene scans.
/// </summary>
public static class CombatEnemyRegistry
{
    private static readonly List<EnemyBaseController> Live = new(32);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Live.Clear();
    }

    static CombatEnemyRegistry()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Live.Clear();
    }

    public static void Register(EnemyBaseController enemy)
    {
        if (!enemy || Live.Contains(enemy))
            return;

        if (enemy.GetComponent<PlayerController>() != null ||
            enemy.GetComponentInParent<PlayerController>() != null)
            return;

        Live.Add(enemy);
    }

    public static void Unregister(EnemyBaseController enemy)
    {
        if (!enemy)
            return;
        Live.Remove(enemy);
    }

    public static IReadOnlyList<EnemyBaseController> GetLiveEnemies()
    {
        PruneInvalidEntries();
        return Live;
    }

    private static void PruneInvalidEntries()
    {
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            EnemyBaseController enemy = Live[i];
            if (!enemy || enemy.IsDead)
                Live.RemoveAt(i);
        }
    }
}
