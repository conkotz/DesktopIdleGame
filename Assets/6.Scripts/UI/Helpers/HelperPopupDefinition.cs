using UnityEngine;

/// <summary>
/// Data for a single helper tip. Dismissal is tracked per-character in <see cref="SaveData.dismissedHelperIds"/> (cleared automatically on New Game).
/// Create assets under <b>Assets → Create → Desktop Idle Game → Helper Popup Definition</b>:
/// <b>Empty</b> for a blank SO (creates next to whichever folder you have selected — pick <c>Assets/3.ScriptableObjects/HelperDefinitions</c> first), or <b>Game Start</b> for a sample early-game / first-visit preset (edit map node id, copy, and <c>helperId</c> per area).
/// </summary>
[CreateAssetMenu(fileName = "HelperPopup", menuName = "Desktop Idle Game/Helper Popup Definition/Empty", order = 52)]
public sealed class HelperPopupDefinition : ScriptableObject
{
    [Tooltip("Stable id for PlayerPrefs (e.g. game_start_tutorial).")]
    public string helperId = "unnamed_helper";

    [Tooltip("Shown above body; leave empty to hide the title row.")]
    public string title = "Help";

    [TextArea(4, 18)]
    public string bodyText = "";

    public HelperActivationTrigger activationTrigger = HelperActivationTrigger.FirstVisitMapNode;

    [Tooltip("When FirstVisitMapNode: only show when this map node loads (e.g. tutorial_1).")]
    public string requiredMapNodeId = "";

    [Tooltip("Lower runs first when several helpers could activate the same frame.")]
    public int priority = 0;

    [Tooltip("How the player can dismiss this helper while it is showing.")]
    public HelperDismissMode dismissModes = HelperDismissMode.CloseButton | HelperDismissMode.CharacterPageOpened;

    [Header("World allowed targets (while helper blocks input)")]
    [Tooltip("Ids on HelperWhitelistInteractTarget on the NPC/merchant (same GameObject or parent of the click collider). Case-insensitive.")]
    public string[] whitelistedInteractionIds;

    [Tooltip("If true, whitelisted worlds targets get a yellow pulsing glow silhouette above the dimmer plus optional world tint when glow is off (see HelperGameplayController).")]
    public bool highlightWhitelistTargetsDuringHelper = true;

    public bool MatchesWhitelistId(string markerId)
    {
        if (string.IsNullOrWhiteSpace(markerId) || whitelistedInteractionIds == null)
            return false;

        string trimmed = markerId.Trim();
        for (int i = 0; i < whitelistedInteractionIds.Length; i++)
        {
            string row = whitelistedInteractionIds[i];
            if (string.IsNullOrWhiteSpace(row))
                continue;
            if (string.Equals(row.Trim(), trimmed, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool IsWhitelistedCollider(Collider2D winnerCol)
    {
        if (!winnerCol)
            return false;

        HelperWhitelistInteractTarget marker =
            winnerCol.GetComponentInParent<HelperWhitelistInteractTarget>(true);
        if (!marker)
            return false;

        return MatchesWhitelistId(marker.InteractionId);
    }
}

public enum HelperActivationTrigger
{
    /// <summary>Never auto-shows (reserved for future script triggers).</summary>
    None = 0,

    /// <summary>Show once the first time the player enters <see cref="HelperPopupDefinition.requiredMapNodeId"/>.</summary>
    FirstVisitMapNode = 1,
}

[System.Flags]
public enum HelperDismissMode
{
    None = 0,
    CloseButton = 1 << 0,

    /// <summary>Dismiss when <see cref="MainMenuWindowUI"/> opens the Character page (inventory/equipment).</summary>
    CharacterPageOpened = 1 << 1,

    /// <summary>
    /// When the player uses a whitelist world target (<see cref="HelperWhitelistInteractTarget"/>),
    /// dismiss after that interact is routed (<see cref="WorldInputRouter2D"/>). Requires whitelist ids populated.
    /// </summary>
    InteractWhitelistDismiss = 1 << 2,
}
