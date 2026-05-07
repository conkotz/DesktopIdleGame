using UnityEngine;

/// <summary>
/// Click-to-enter portal that loads <c>GamePlay</c> with <see cref="ActiveLevelContext"/> set to the target <see cref="MapNodeDefinition"/>.
/// Click routes via <see cref="WorldInputRouter2D"/>: the player will path to this portal's collider, then teleport on arrival.
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
    private Collider2D _col;
    private PlayerController _pendingPlayer;
    private float _pendingArrivalX;
    private bool _pendingEnter;

    [Header("Click-to-enter")]
    [Tooltip("How close (world units) the player must be to the portal collider X before entering.")]
    [SerializeField, Min(0.01f)] private float arriveDistanceX = 0.08f;

#if UNITY_EDITOR
    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c)
            c.isTrigger = true;
    }
#endif

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col == null)
            Debug.LogError("[MapNodePortalTeleporter] Missing Collider2D.", this);
    }

    private void Update()
    {
        if (!_pendingEnter || _pendingPlayer == null || _pendingPlayer.IsDead)
        {
            _pendingEnter = false;
            _pendingPlayer = null;
            return;
        }

        if (Time.time < _nextAllowedTime)
            return;

        float dx = Mathf.Abs(_pendingPlayer.transform.position.x - _pendingArrivalX);
        if (dx > Mathf.Max(0.01f, arriveDistanceX))
            return;

        _pendingEnter = false;
        TryEnterNow();
    }

    /// <summary>
    /// Called from <see cref="WorldInputRouter2D"/> when this portal is clicked.
    /// The player paths to the collider edge first, then teleport triggers on arrival.
    /// </summary>
    public void OnClickedByPlayer(PlayerController player)
    {
        if (player == null || player.IsDead)
            return;

        MapNodeDefinition node = ResolveTarget();
        if (!node)
        {
            Debug.LogWarning("[MapNodePortalTeleporter] Assign Target Map or Target Map Node Id.", this);
            return;
        }

        if (_col == null)
            _col = GetComponent<Collider2D>();
        if (_col == null)
            return;

        // Walk up to the portal collider (closest x on its bounds).
        float px = player.transform.position.x;
        Bounds b = _col.bounds;
        float arrivalX = Mathf.Clamp(px, b.min.x, b.max.x);

        _pendingPlayer = player;
        _pendingArrivalX = arrivalX;
        _pendingEnter = true;

        player.MoveToPointX(arrivalX);
    }

    private void TryEnterNow()
    {
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
