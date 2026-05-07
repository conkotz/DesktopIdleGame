using UnityEngine;

/// <summary>
/// Place on a GameObject with a <see cref="Collider2D"/> set as a trigger. When the player enters,
/// loads <c>GamePlay</c> with <see cref="ActiveLevelContext"/> set to the target <see cref="MapNodeDefinition"/>.
/// Respects the same gates as <see cref="MapNodeDefinition.CanEnter"/> (unlike the Locations menu, which also respects
/// <see cref="MapNodeDefinition.entranceOnlyAccess"/>).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class MapNodePortalTeleporter : MonoBehaviour
{
    [Tooltip("Map to load. Takes precedence over Target Map Node Id.")]
    [SerializeField] private MapNodeDefinition targetMap;

    [Tooltip("Used when Target Map is null. Must match MapNodeDefinition.nodeId on the world map.")]
    [SerializeField] private string targetMapNodeId = "";

    [Tooltip("Gameplay scene to load (same as Level Select).")]
    [SerializeField] private string gameplaySceneName = "GamePlay";

    [Tooltip("Minimum time before this portal can trigger again.")]
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.75f;

    [Tooltip("Activity log line when the player does not meet map entry requirements.")]
    [SerializeField] private bool logWhenBlocked = true;

    [SerializeField] private string blockedMessage = "You cannot enter this area yet.";

    private float _nextAllowedTime;

#if UNITY_EDITOR
    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c)
            c.isTrigger = true;
    }
#endif

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;
        if (!other.GetComponentInParent<PlayerController>())
            return;
        if (Time.time < _nextAllowedTime)
            return;

        MapNodeDefinition node = ResolveTarget();
        if (!node)
        {
            Debug.LogWarning("[MapNodePortalTeleporter] Assign Target Map or Target Map Node Id.", this);
            return;
        }

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        SkillsManager skills = SkillsManager.Instance ??
            FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);

        if (!node.CanEnter(progress, skills))
        {
            if (logWhenBlocked && !string.IsNullOrWhiteSpace(blockedMessage))
                GameLog.Add(blockedMessage.Trim(), GameLog.CannotMessageColor);
            return;
        }

        _nextAllowedTime = Time.time + Mathf.Max(0f, cooldownSeconds);
        ActiveLevelContext.SetPendingLevel(node, logToConsole: false);

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogError("[MapNodePortalTeleporter] Gameplay scene name is empty.", this);
            return;
        }

        PlayerLevelTransition.LoadSceneWithEffectOrImmediate(gameplaySceneName.Trim());
    }

    private MapNodeDefinition ResolveTarget()
    {
        if (targetMap)
            return targetMap;

        if (string.IsNullOrWhiteSpace(targetMapNodeId))
            return null;

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        WorldMapDefinition map = wmp ? wmp.WorldMap : null;
        if (!map)
            map = Resources.Load<WorldMapDefinition>("Databases/WorldMap_Main");
        if (!map)
            return null;

        return map.FindNodeById(targetMapNodeId.Trim());
    }
}
