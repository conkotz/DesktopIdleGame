using UnityEngine;

/// <summary>Resolves map labels shown on the gameplay level-load black screen.</summary>
public static class GameplayLoadDisplayNames
{
    public static string ResolveActiveMapDisplayName()
    {
        MapNodeDefinition node = ResolveActiveMapNode();
        if (node == null)
            return string.Empty;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance;
        string display = MapCombatScaling.BuildLocationDisplayName(node, progress);
        if (!string.IsNullOrWhiteSpace(display))
            return display.Trim();

        if (!string.IsNullOrWhiteSpace(node.displayName))
            return node.displayName.Trim();

        return node.nodeId ?? string.Empty;
    }

    public static bool IsActiveTownMap()
    {
        MapNodeDefinition node = ResolveActiveMapNode();
        return node != null && node.nodeType == MapNodeType.Town;
    }

    private static MapNodeDefinition ResolveActiveMapNode()
    {
        if (ActiveLevelContext.Current != null)
            return ActiveLevelContext.Current;

        if (GameplayLevelBootstrapper.Instance != null &&
            GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition;

        return null;
    }
}
