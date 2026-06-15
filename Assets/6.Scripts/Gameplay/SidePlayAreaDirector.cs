using UnityEngine;

/// <summary>
/// Enables side-play-area bounds for the active <see cref="MapNodeDefinition"/> on level start.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay/Side Play Area Director")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
public sealed class SidePlayAreaDirector : MonoBehaviour
{
    private void OnEnable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted += HandleLevelStarted;
    }

    private void OnDisable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= HandleLevelStarted;

        PlayAreaBounds.ClearEnabledAreas();
    }

    private void Start()
    {
        if (GameplayLevelBootstrapper.Instance != null &&
            GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
        {
            HandleLevelStarted(GameplayLevelBootstrapper.Instance.ActiveDefinition);
        }
    }

    private static void HandleLevelStarted(MapNodeDefinition node)
    {
        if (node == null || node.enabledSidePlayAreaIds == null || node.enabledSidePlayAreaIds.Count == 0)
        {
            PlayAreaBounds.ClearEnabledAreas();
            return;
        }

        PlayAreaBounds.SetEnabledAreasForMap(node.enabledSidePlayAreaIds);
    }
}
