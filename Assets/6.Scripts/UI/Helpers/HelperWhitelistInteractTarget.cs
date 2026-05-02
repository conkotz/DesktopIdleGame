using UnityEngine;

/// <summary>
/// Stable id matched against <see cref="HelperPopupDefinition.whitelistInteractEntries"/> (when set) or legacy
/// <see cref="HelperPopupDefinition.whitelistedInteractionIds"/> while a helper blocks world input,
/// allowing that NPC / merchant collider subtree to remain clickable for dismiss + routing.
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Helper Whitelist Interact Target")]
[DisallowMultipleComponent]
public sealed class HelperWhitelistInteractTarget : MonoBehaviour
{
    [Tooltip("Case-insensitive. Example: NPC_Tutorial_1. Must match a whitelist row on the active Helper Popup Definition.")]
    [SerializeField] private string interactionId;

    /// <inheritdoc cref="interactionId"/>
    public string InteractionId => interactionId != null ? interactionId.Trim() : string.Empty;
}
