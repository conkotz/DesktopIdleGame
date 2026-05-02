#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ShowWhenTrueAttribute))]
public sealed class ShowWhenTrueDrawer : PropertyDrawer
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

        ShowWhenTrueAttribute a = (ShowWhenTrueAttribute)attribute;
        if (string.IsNullOrEmpty(a.ConditionBoolFieldName))
            return true;

        SerializedProperty cond = property.serializedObject.FindProperty(a.ConditionBoolFieldName);
        return cond != null && cond.propertyType == SerializedPropertyType.Boolean && cond.boolValue;
    }
}
#endif
