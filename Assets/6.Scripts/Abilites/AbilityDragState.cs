using UnityEngine;

public static class AbilityDragState
{
    public static bool HasDrag { get; private set; }

    public static string AbilityId { get; private set; }
    public static Sprite AbilityIcon { get; private set; }
    public static string AbilityDisplayName { get; private set; }
    public static string AbilityDescription { get; private set; }
    /// <summary>When dragging from an action-bar slot, the slot the ability came from (for swap-on-drop).</summary>
    public static ActionBarSlotUI SourceActionBarSlot { get; private set; }

    /// <summary>True after a successful drop onto an action-bar slot this drag.</summary>
    public static bool DropWasHandled { get; private set; }

    public static void BeginDrag(
        string abilityId,
        Sprite abilityIcon = null,
        string displayName = null,
        string description = null,
        ActionBarSlotUI sourceActionBarSlot = null)
    {
        AbilityId = abilityId;
        AbilityIcon = abilityIcon;
        AbilityDisplayName = displayName;
        AbilityDescription = description;
        SourceActionBarSlot = sourceActionBarSlot;
        DropWasHandled = false;
        HasDrag = !string.IsNullOrWhiteSpace(abilityId);
    }

    public static void MarkDropHandled() => DropWasHandled = true;

    public static void EndDrag()
    {
        HasDrag = false;
        DropWasHandled = false;
        AbilityId = null;
        AbilityIcon = null;
        AbilityDisplayName = null;
        AbilityDescription = null;
        SourceActionBarSlot = null;
    }
}

