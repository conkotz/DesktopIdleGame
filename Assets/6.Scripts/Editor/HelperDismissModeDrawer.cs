#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="HelperDismissMode.CloseButton"/> is intentionally hidden: closing with X is always allowed on the overlay.
/// The CloseButton and legacy CharacterPageOpened bits are stripped whenever this drawer draws.
/// </summary>
[CustomPropertyDrawer(typeof(HelperDismissMode))]
public sealed class HelperDismissModeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;
        return line + gap + line + gap + line;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.BeginProperty(position, label, property);

        Rect rowLabel = new Rect(position.x, position.y, position.width, line);
        Rect row1 = new Rect(position.x, rowLabel.yMax + gap, position.width, line);
        Rect row2 = new Rect(position.x, row1.yMax + gap, position.width, line);

        EditorGUI.LabelField(rowLabel, label);

        var mode = (HelperDismissMode)property.intValue;
        bool interact = (mode & HelperDismissMode.InteractWhitelistDismiss) != 0;
        bool anyPlayer = (mode & HelperDismissMode.AnyPlayerActionDismiss) != 0;

        EditorGUI.indentLevel++;

        EditorGUI.BeginChangeCheck();
        interact = EditorGUI.ToggleLeft(row1,
            new GUIContent(
                "Interact whitelist dismiss",
                "World targets (collider whitelist) and matching Helper Whitelist UI Interact Target clicks. Scripted dismiss keeps the panel expanded; use the chrome control to minimize."),
            interact);

        anyPlayer = EditorGUI.ToggleLeft(row2,
            new GUIContent(
                "Any player action",
                "Dismiss when the player moves (Horizontal/Vertical) or clicks the game world (not over UI). Combine with whitelist dismiss if you want either path."),
            anyPlayer);

        if (EditorGUI.EndChangeCheck())
        {
            HelperDismissMode rebuilt = HelperDismissMode.None;
            if (interact)
                rebuilt |= HelperDismissMode.InteractWhitelistDismiss;
            if (anyPlayer)
                rebuilt |= HelperDismissMode.AnyPlayerActionDismiss;
            property.intValue = (int)rebuilt;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
#endif
