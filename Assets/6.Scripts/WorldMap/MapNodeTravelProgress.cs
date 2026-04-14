using UnityEngine;

/// <summary>
/// Marks the active map node completed when the player opens meta-progression UI (level select / quests),
/// if the <see cref="MapNodeDefinition.markCompletedWhenReturningToMenu"/> flag is set.
/// </summary>
public static class MapNodeTravelProgress
{
    public static void TryMarkCurrentNodeIfConfigured()
    {
        MapNodeDefinition def = null;
        if (GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        if (def == null)
            def = ActiveLevelContext.Current;
        if (def == null || !def.markCompletedWhenReturningToMenu)
            return;

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            Object.FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (wmp == null || string.IsNullOrEmpty(def.nodeId))
            return;

        wmp.SetNodeCompleted(def.nodeId, true);
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }
}
