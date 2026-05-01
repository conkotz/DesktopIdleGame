using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Matches <see cref="HelperPopupDefinition.whitelistedInteractionIds"/> for toolbar / UI buttons while a helper is modal:
/// clicks invoke <see cref="HelperGameplayController.NotifyWhitelistUiInteract"/>.
/// Prefer the same ids as world whitelist rows (Character button can use id <c>UIButton_Character</c> etc.).
/// The controller temporarily raises this hierarchy's Canvas sort order above the helper panel so rays hit the button.
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Helper Whitelist UI Interact Target")]
[DisallowMultipleComponent]
public sealed class HelperWhitelistUiInteractTarget : MonoBehaviour, IPointerClickHandler
{
    /// <summary>
    /// Default id for toolbar Character; pairing with whitelist dismiss skips an accidental Close() when the menu is already on Character.
    /// </summary>
    public const string CharacterToolbarWhitelistId = "UIButton_Character";

    /// <summary>
    /// Default id for toolbar Quest; pairing with whitelist dismiss skips an accidental Close() when the menu is already on Quest.
    /// </summary>
    public const string QuestToolbarWhitelistId = "UIButton_Quest";

    /// <summary>Skills &amp; Abilities toolbar — same-frame whitelist dismiss keeps tab open.</summary>
    public const string SkillsAbilityToolbarWhitelistId = "UIButton_SkillsAbility";

    /// <summary>
    /// Some helper assets and buttons use this spelling; treat it as equivalent to <see cref="SkillsAbilityToolbarWhitelistId"/> for menu / stamp logic.
    /// </summary>
    public const string SkillsAbilityToolbarWhitelistIdLegacy = "UIButton_SkillsAbilities";

    /// <summary>Level select toolbar — same-frame whitelist dismiss keeps tab open.</summary>
    public const string LevelSelectToolbarWhitelistId = "UIButton_LevelSelect";

    [Tooltip("Case-insensitive. Must match Whitelisted Interaction Ids on the active helper.")]
    [SerializeField] private string interactionId;

    [Tooltip(
        "Glow clone source: prefer the inner icon Image on this button so the pulse matches the NPC sprite silhouette. " +
        "If empty, uses the Graphic on this object (often the full button frame — assign the child icon for a tighter glow).")]
    [SerializeField] private Graphic glowSourceGraphic;

    public string InteractionId => interactionId != null ? interactionId.Trim() : string.Empty;

    public Graphic GlowSourceGraphic => glowSourceGraphic ? glowSourceGraphic : GetComponent<Graphic>();

    /// <summary>True for canonical or legacy Skills toolbar whitelist ids.</summary>
    public static bool IsSkillsAbilityToolbarWhitelistMarker(string markerId)
    {
        if (string.IsNullOrWhiteSpace(markerId))
            return false;

        string m = markerId.Trim();
        return string.Equals(m, SkillsAbilityToolbarWhitelistId, System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(m, SkillsAbilityToolbarWhitelistIdLegacy, System.StringComparison.OrdinalIgnoreCase);
    }

    private void Reset()
    {
        glowSourceGraphic = GetComponent<Graphic>();
    }

    /// <summary>Wired to Unity <see cref="Button.onClick"/> if pointer events stay on the Button only.</summary>
    public void RelayWhitelistClickFromButton()
    {
        RaiseNotify();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        RaiseNotify();
    }

    private void RaiseNotify()
    {
        HelperGameplayController.NotifyWhitelistUiInteract(InteractionId);
    }
}
