using UnityEngine;

public static class AbilityDragState
{
    public static bool HasDrag { get; private set; }

    public static string AbilityId { get; private set; }
    public static Sprite AbilityIcon { get; private set; }
    public static string AbilityDisplayName { get; private set; }
    public static string AbilityDescription { get; private set; }

    public static void BeginDrag(string abilityId, Sprite abilityIcon = null, string displayName = null, string description = null)
    {
        AbilityId = abilityId;
        AbilityIcon = abilityIcon;
        AbilityDisplayName = displayName;
        AbilityDescription = description;
        HasDrag = !string.IsNullOrWhiteSpace(abilityId);
    }

    public static void EndDrag()
    {
        HasDrag = false;
        AbilityId = null;
        AbilityIcon = null;
        AbilityDisplayName = null;
        AbilityDescription = null;
    }
}

