using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Which skill level-up can fire a <see cref="HelperActivationTrigger.SkillLevelReached"/> helper. Values 0–6 match <see cref="SkillType"/>; <see cref="AnySkill"/> matches any tracked skill.
/// </summary>
public enum HelperSkillLevelTriggerOption
{
    Mining = 0,
    Woodcutting = 1,
    Fishing = 2,
    Melee = 3,
    Ranged = 4,
    Magic = 5,
    Endurance = 6,
    AnySkill = 7,
}

/// <summary>
/// One whitelisted interact id for a helper, with optional per-target UI glow behavior.
/// </summary>
[Serializable]
public sealed class HelperWhitelistInteractEntry
{
    [Tooltip("Case-insensitive; must match HelperWhitelistUiInteractTarget / HelperWhitelistInteractTarget id.")]
    public string interactionId = "";

    [Tooltip(
        "When on, moving the pointer over this whitelisted UI target clears only that target's pulsing glow overlay " +
        "(click can still run whitelist dismiss).")]
    public bool clearGlowOnPointerEnter;
}

/// <summary>
/// Data for a single helper tip. Dismissal is tracked per-character in <see cref="SaveData.dismissedHelperIds"/> (cleared automatically on New Game).
/// Create assets under <b>Assets → Create → Desktop Idle Game → Helper Popup Definition</b>:
/// <b>Empty</b> for a blank SO (creates next to whichever folder you have selected — pick <c>Assets/3.ScriptableObjects/HelperDefinitions</c> first), or <b>Game Start</b> for a sample early-game / first-visit preset (edit map node id, copy, and a <b>new</b> unique <c>helperId</c> per popup).
/// </summary>
[CreateAssetMenu(fileName = "HelperPopup", menuName = "Desktop Idle Game/Helper Popup Definition/Empty", order = 52)]
public sealed class HelperPopupDefinition : ScriptableObject
{
    [Tooltip("Unique stable id saved to dismissal list (same id on two definitions will collide). Use lowercase_snake_case, e.g. first_item_hint.")]
    public string helperId = "unnamed_helper";

    [Tooltip("Shown above body; leave empty to hide the title row. Use {playerName} for the character name from CharacterStats.")]
    public string title = "Help";

    [TextArea(4, 18)]
    [Tooltip("Supports {playerName} — replaced at runtime with CharacterStats.UnitDisplayName (fallback: Adventurer).")]
    public string bodyText = "";

    public HelperActivationTrigger activationTrigger = HelperActivationTrigger.FirstVisitMapNode;

    [Tooltip("When FirstVisitMapNode or Inventory Item Count Reached (non-empty): only run when active level nodeId matches.")]
    public string requiredMapNodeId = "";

    [Tooltip("When InventoryItemCountReached: item id summed across bag slots.")]
    public string inventoryTriggerItemId = "";

    [Tooltip("When InventoryItemCountReached: fire once when inventory total reaches this threshold.")]
    [Min(1)] public int inventoryTriggerItemCount = 3;

    [Tooltip(
        "When QuestGatherObjectiveReady: QuestDefinition.questId. GatherItem quests only — fires when the quest is accepted, " +
        "reward is not claimed yet, and live gather count (inventory + storage, same as tracker) >= target count.")]
    public string questGatherTriggerQuestId = "";

    [Tooltip(
        "When QuestRewardClaimed: QuestDefinition.questId. Fires once after the player completes the quest — i.e. reward is claimed (Complete clicked).")]
    public string questRewardClaimedTriggerQuestId = "";

    [Tooltip(
        "When QuestAccepted: QuestDefinition.questId (e.g. tutorial_basic_combat). Fires once when the player accepts the quest.")]
    public string questAcceptedTriggerQuestId = "";

    [Tooltip(
        "When SkillLevelReached: which skill's level-up fires this helper (from SkillsManager.OnLevelUp), or Any Skill for whichever skill reaches the minimum level first.")]
    public HelperSkillLevelTriggerOption skillLevelTriggerSkill = HelperSkillLevelTriggerOption.Woodcutting;

    [Tooltip(
        "When SkillLevelReached: minimum new level from OnLevelUp (default 2 = first rise above starter level 1). With Any Skill, any tracked skill reaching at least this level triggers once.")]
    [Min(2)] public int skillLevelTriggerMinimumNewLevel = 2;

    [Tooltip("Lower runs first when several helpers could activate the same frame.")]
    public int priority = 0;

    [Tooltip("When off, hides the overlay X button (whitelist dismiss flow only — X closes when on).")]
    public bool showCloseButton = true;

    [Tooltip(
        "When on (default): fullscreen dimmer blocks clicks behind the helper and movement is locked while this tip is expanded. "
        + "When off: the popup still appears but the player can keep interacting and moving.")]
    public bool darkenScreenAndLockGameplay = true;

    [Tooltip(
        "Scripted dismiss: whitelist interact and/or any player action (movement / world click). Overlay X still closes when Show Close Button is on. Scripted dismiss keeps the panel expanded.")]
    public HelperDismissMode dismissModes = HelperDismissMode.InteractWhitelistDismiss;

    [Header("Allowed interact targets while helper modal (whitelist ids)")]
    [Tooltip(
        "Per-id whitelist with optional clear-glow-on-hover on each row. Union with Whitelisted Interaction Ids — both lists apply at once.")]
    public HelperWhitelistInteractEntry[] whitelistInteractEntries;

    [Tooltip(
        "Click / interact whitelist ids (same matching rules as hover entries). Union with hover entries — both lists apply at once.")]
    public string[] whitelistedInteractionIds;

    [Tooltip(
        "If true, whitelisted world sprites and UI Graphics get a yellow pulsing glow clone above the dimmer (world tint fallback when Glow Above Dimmer is off — UI uses overlay glow only).")]
    public bool highlightWhitelistTargetsDuringHelper = true;

    [Tooltip(
        "Optional: bag / storage slots that currently hold this item use the same yellow tint as idle auto-battle new loot when the helper is shown " +
        "(e.g. First Bag Gain From Zero — assign the item the player just received). Leave empty to skip.")]
    public ItemDefinition highlightInventorySlotsForItem;

    /// <summary>
    /// True when at least one non-empty whitelist id is configured (structured entries and/or legacy strings).
    /// </summary>
    public bool HasConfiguredWhitelistInteractIds()
    {
        if (whitelistInteractEntries != null)
        {
            for (int i = 0; i < whitelistInteractEntries.Length; i++)
            {
                HelperWhitelistInteractEntry e = whitelistInteractEntries[i];
                if (e != null && !string.IsNullOrWhiteSpace(e.interactionId))
                    return true;
            }
        }

        if (whitelistedInteractionIds != null)
        {
            for (int i = 0; i < whitelistedInteractionIds.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(whitelistedInteractionIds[i]))
                    return true;
            }
        }

        return false;
    }

    public bool MatchesWhitelistId(string markerId)
    {
        if (string.IsNullOrWhiteSpace(markerId))
            return false;

        string trimmed = markerId.Trim();
        if (MatchesWhitelistIdInEntries(trimmed))
            return true;

        return MatchesWhitelistIdInLegacyStrings(trimmed);
    }

    /// <summary>
    /// When <paramref name="markerId"/> matches a row in <see cref="whitelistInteractEntries"/>, returns true and that row's
    /// clear-glow-on-hover flag. Ids whitelisted only via <see cref="whitelistedInteractionIds"/> return false (use UI component default).
    /// </summary>
    public bool TryGetStructuredEntryClearGlowOnPointerEnter(string markerId, out bool clearGlowOnPointerEnter)
    {
        clearGlowOnPointerEnter = false;
        if (string.IsNullOrWhiteSpace(markerId) || whitelistInteractEntries == null)
            return false;

        string trimmed = markerId.Trim();

        for (int i = 0; i < whitelistInteractEntries.Length; i++)
        {
            HelperWhitelistInteractEntry e = whitelistInteractEntries[i];
            if (e == null || string.IsNullOrWhiteSpace(e.interactionId))
                continue;

            string rowTrim = e.interactionId.Trim();
            if (WhitelistIdRowMatches(trimmed, rowTrim))
            {
                clearGlowOnPointerEnter = e.clearGlowOnPointerEnter;
                return true;
            }
        }

        return false;
    }

    private static bool WhitelistIdRowMatches(string trimmedMarker, string rowTrim)
    {
        if (string.Equals(rowTrim, trimmedMarker, StringComparison.OrdinalIgnoreCase))
            return true;

        return HelperWhitelistUiInteractTarget.IsSkillsAbilityToolbarWhitelistMarker(trimmedMarker) &&
               HelperWhitelistUiInteractTarget.IsSkillsAbilityToolbarWhitelistMarker(rowTrim);
    }

    private bool MatchesWhitelistIdInEntries(string trimmed)
    {
        if (whitelistInteractEntries == null)
            return false;

        for (int i = 0; i < whitelistInteractEntries.Length; i++)
        {
            HelperWhitelistInteractEntry e = whitelistInteractEntries[i];
            if (e == null || string.IsNullOrWhiteSpace(e.interactionId))
                continue;

            if (WhitelistIdRowMatches(trimmed, e.interactionId.Trim()))
                return true;
        }

        return false;
    }

    private bool MatchesWhitelistIdInLegacyStrings(string trimmedMarker)
    {
        if (whitelistedInteractionIds == null)
            return false;

        for (int i = 0; i < whitelistedInteractionIds.Length; i++)
        {
            string row = whitelistedInteractionIds[i];
            if (string.IsNullOrWhiteSpace(row))
                continue;

            string rowTrim = row.Trim();
            if (WhitelistIdRowMatches(trimmedMarker, rowTrim))
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

    /// <summary>
    /// Replaces <c>{playerName}</c> (case-insensitive) with <see cref="CharacterStats.UnitDisplayName"/> when <paramref name="stats"/> is assigned.
    /// </summary>
    public static string ApplyRuntimeSubstitutions(string text, CharacterStats stats)
    {
        if (text == null)
            return string.Empty;

        string name = ResolvePlayerDisplayName(stats);

        try
        {
            return Regex.Replace(
                text,
                @"\{playerName\}",
                _ => name,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }
    }

    private static string ResolvePlayerDisplayName(CharacterStats stats)
    {
        if (stats != null && !string.IsNullOrWhiteSpace(stats.UnitDisplayName))
            return stats.UnitDisplayName.Trim();

        return "Adventurer";
    }
}

public enum HelperActivationTrigger
{
    /// <summary>Never auto-shows (reserved for future script triggers).</summary>
    None = 0,

    /// <summary>Show once the first time the player enters <see cref="HelperPopupDefinition.requiredMapNodeId"/>.</summary>
    FirstVisitMapNode = 1,

    /// <summary>Show once when total item quantity in inventory goes from 0 to &gt;0 after save load on an empty bag (skipped if save already had items).</summary>
    FirstBagGainFromZero = 2,

    /// <summary>
    /// Show once when bag total for <see cref="HelperPopupDefinition.inventoryTriggerItemId"/> reaches
    /// <see cref="HelperPopupDefinition.inventoryTriggerItemCount"/> (use <see cref="HelperPopupDefinition.requiredMapNodeId"/> for map filter).
    /// </summary>
    InventoryItemCountReached = 3,

    /// <summary>
    /// GatherItem quest: once when accepted and <see cref="QuestProgressManager.GetDisplayProgress"/> meets
    /// <see cref="QuestDefinition.targetCount"/> before rewards are claimed (matches on-screen tracker).
    /// </summary>
    QuestGatherObjectiveReady = 4,

    /// <summary>Fires once when <see cref="SkillsManager.OnLevelUp"/> reports a level-up matching <see cref="HelperPopupDefinition.skillLevelTriggerSkill"/> (or any skill when that option is <see cref="HelperSkillLevelTriggerOption.AnySkill"/>) at least <see cref="HelperPopupDefinition.skillLevelTriggerMinimumNewLevel"/>.</summary>
    SkillLevelReached = 5,

    /// <summary>Fires once when <see cref="QuestProgressManager.IsRewardClaimed"/> becomes true for <see cref="HelperPopupDefinition.questRewardClaimedTriggerQuestId"/>.</summary>
    QuestRewardClaimed = 6,

    /// <summary>Fires once when <see cref="QuestProgressManager.IsQuestAccepted"/> becomes true for <see cref="HelperPopupDefinition.questAcceptedTriggerQuestId"/>.</summary>
    QuestAccepted = 7,
}

[System.Flags]
public enum HelperDismissMode
{
    None = 0,

    /// <summary>Reserved (strip from assets). Closing with X is always allowed regardless of bitmask.</summary>
    CloseButton = 1 << 0,

    /// <summary>Obsolete in runtime; leftover bit from older helpers — stripped in Inspector.</summary>
    CharacterPageOpened = 1 << 1,

    /// <summary>
    /// Dismiss after a whitelist world interact routes (<see cref="WorldInputRouter2D"/>) or a matching toolbar
    /// <see cref="HelperWhitelistUiInteractTarget"/> click (<see cref="HelperGameplayController.NotifyWhitelistUiInteract"/>).
    /// </summary>
    InteractWhitelistDismiss = 1 << 2,

    /// <summary>
    /// Dismiss when the player uses movement (Horizontal/Vertical axes) or clicks the game world (mouse buttons, not over UI).
    /// Useful when there is no whitelist target. Overlay X still closes when <see cref="HelperPopupDefinition.showCloseButton"/> is on.
    /// </summary>
    AnyPlayerActionDismiss = 1 << 3,
}
