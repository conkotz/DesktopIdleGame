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
        return line + gap + line;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.BeginProperty(position, label, property);

        Rect rowLabel = new Rect(position.x, position.y, position.width, line);
        Rect row1 = new Rect(position.x, rowLabel.yMax + gap, position.width, line);

        EditorGUI.LabelField(rowLabel, label);

        bool interact =
            ((HelperDismissMode)property.intValue & HelperDismissMode.InteractWhitelistDismiss) != 0;

        EditorGUI.indentLevel++;

        EditorGUI.BeginChangeCheck();
        interact = EditorGUI.ToggleLeft(row1,
            new GUIContent(
                "Interact whitelist dismiss",
                "World targets (collider whitelist) and matching Helper Whitelist UI Interact Target clicks. Overlay X still closes when Show Close Button is on."),
            interact);

        if (EditorGUI.EndChangeCheck())
        {
            property.intValue =
                interact ? (int)HelperDismissMode.InteractWhitelistDismiss : 0;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
#endif
