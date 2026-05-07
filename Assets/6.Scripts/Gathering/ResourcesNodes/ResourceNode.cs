using UnityEngine;
using System.Collections.Generic;

public enum NodeAction
{
    Mining,
    Woodcutting,
    Fishing
}

public class ResourceNode : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private NodeDefinition definition;
    public NodeDefinition Definition => definition;

    [Header("Interaction")]
    public Transform workSpot;
    public float interactRange = 0.1f;

    /// <summary>
    /// World X the player should face while gathering. Uses this node's position (visual center), not
    /// <see cref="workSpot"/> — the avatar stands at the work spot, so work-spot X matches the player and breaks flip logic.
    /// </summary>
    public float GatherFacingWorldX => transform.position.x;

    // Read-only views of the definition data
    public string DisplayName => definition ? definition.displayName : "Resource";
    public NodeAction ActionType => definition ? definition.actionType : NodeAction.Woodcutting;
    public int RequiredLevel => definition ? definition.requiredLevel : 1;
    public ItemDefinition YieldItem => definition ? definition.yieldItem : null;
    public string YieldItemId => definition ? definition.YieldItemId : string.Empty;

    public bool UseRandomInterval => definition && definition.useRandomInterval;
    public float RatePerSecond => definition ? definition.ratePerSecond : 0f;
    public float MinInterval => definition ? definition.minInterval : 0f;
    public float MaxInterval => definition ? definition.maxInterval : 0f;

    public float GetNextInterval() => definition ? definition.GetNextInterval() : float.MaxValue;
    public string GetActionText() => definition ? definition.GetActionText() : string.Empty;

    public bool RequiresTool => definition && definition.requiresTool && definition.requiredTool != ToolKey.None;
    public ToolKey RequiredTool => definition ? definition.requiredTool : ToolKey.None;
    public string MissingToolMessage => definition ? definition.missingToolMessage : "Put the required tool in your toolbelt.";
    public float EnergyCostPerSwing => definition ? Mathf.Max(0f, definition.energyCostPerSwing) : 0f;

    public bool UseLevelRequirement => definition && definition.useLevelRequirement;

    [Header("Name Label Visibility")]
    [Tooltip("Hide name label object(s) while player stands on this node.")]
    [SerializeField] private bool hideNameLabelsWhenPlayerOverlaps = true;
    [Tooltip("Optional explicit label roots; when empty, children containing 'NameLabel' in their name are auto-detected.")]
    [SerializeField] private List<GameObject> nameLabelRoots = new();

    private PlayerController _player;
    private Collider2D _nodeCollider;
    private bool _nameLabelsHidden;

    private void Awake()
    {
        _nodeCollider = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();
        if (nameLabelRoots == null)
            nameLabelRoots = new List<GameObject>();
        if (nameLabelRoots.Count == 0)
            AutoCollectNameLabelRoots();
    }

    private void Update()
    {
        if (!hideNameLabelsWhenPlayerOverlaps || nameLabelRoots == null || nameLabelRoots.Count == 0)
            return;

        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (_player == null)
            return;

        bool overlap = IsPlayerOverlappingNode();
        if (overlap == _nameLabelsHidden)
            return;

        _nameLabelsHidden = overlap;
        for (int i = 0; i < nameLabelRoots.Count; i++)
        {
            GameObject go = nameLabelRoots[i];
            if (go != null)
                go.SetActive(!overlap);
        }
    }

    private bool IsPlayerOverlappingNode()
    {
        if (_player == null)
            return false;

        if (_nodeCollider != null)
            return _nodeCollider.bounds.Contains(_player.transform.position);

        float dx = Mathf.Abs(_player.transform.position.x - transform.position.x);
        return dx <= interactRange;
    }

    private void AutoCollectNameLabelRoots()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (t == null || t == transform)
                continue;
            if (t.gameObject == null)
                continue;
            string n = t.name;
            if (string.IsNullOrWhiteSpace(n))
                continue;
            if (n.IndexOf("NameLabel", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (!nameLabelRoots.Contains(t.gameObject))
                nameLabelRoots.Add(t.gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!workSpot) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(workSpot.position, interactRange);
    }
}