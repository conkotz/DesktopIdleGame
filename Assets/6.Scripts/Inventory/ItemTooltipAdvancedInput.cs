using UnityEngine;

/// <summary>Hold Alt while hovering an item to show advanced tooltip details (replaces context-menu toggle).</summary>
public static class ItemTooltipAdvancedInput
{
    public static bool IsHeld =>
        Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
}
