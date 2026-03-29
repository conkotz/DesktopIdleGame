using UnityEngine;

/// <summary>
/// Holds the <see cref="MapNodeDefinition"/> for the upcoming or current GamePlay session.
/// Set before <c>SceneManager.LoadScene("GamePlay")</c>; survives the load because it is static.
/// Clear when returning to menu / Bootstrap if you do not want stale data.
/// </summary>
public static class ActiveLevelContext
{
    public static MapNodeDefinition Current { get; private set; }

    /// <summary>
    /// Call from level select (or tests) immediately before loading the gameplay scene.
    /// </summary>
    /// <param name="logToConsole">Set false when restoring from save to avoid noisy duplicate logs.</param>
    public static void SetPendingLevel(MapNodeDefinition node, bool logToConsole = true)
    {
        Current = node;
        if (!logToConsole)
            return;

        if (node)
            Debug.Log($"[ActiveLevelContext] Pending level set: '{node.nodeId}' ({node.displayName})");
        else
            Debug.Log("[ActiveLevelContext] Pending level cleared (null).");
    }

    public static void Clear()
    {
        Current = null;
    }

    public static bool HasPendingOrActiveLevel => Current != null;
}
