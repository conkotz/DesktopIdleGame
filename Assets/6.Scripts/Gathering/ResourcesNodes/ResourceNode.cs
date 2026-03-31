using UnityEngine;

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

    private void OnDrawGizmosSelected()
    {
        if (!workSpot) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(workSpot.position, interactRange);
    }
}