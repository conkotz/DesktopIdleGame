#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ShowWhenEnumAttribute))]
public sealed class ShowWhenEnumDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property))
            return;

        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldShow(property))
            return 0f;

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    private bool ShouldShow(SerializedProperty property)
    {
        if (property == null)
            return false;

        ShowWhenEnumAttribute a = (ShowWhenEnumAttribute)attribute;
        if (string.IsNullOrEmpty(a.EnumFieldName))
            return true;

        SerializedProperty enumProp = FindSiblingProperty(property, a.EnumFieldName);
        if (enumProp == null)
            return true;

        return enumProp.propertyType == SerializedPropertyType.Enum
            ? enumProp.intValue == a.EnumValue
            : enumProp.intValue == a.EnumValue;
    }

    private static SerializedProperty FindSiblingProperty(SerializedProperty property, string siblingName)
    {
        string path = property.propertyPath;
        int lastDot = path.LastIndexOf('.');
        if (lastDot < 0)
            return property.serializedObject.FindProperty(siblingName);

        string parentPath = path.Substring(0, lastDot);
        return property.serializedObject.FindProperty(parentPath + "." + siblingName);
    }
}
#endif
