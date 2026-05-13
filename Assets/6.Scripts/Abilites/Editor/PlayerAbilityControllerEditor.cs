#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Foldout groups for ability refs + global cooldown. VFX lives on <see cref="PlayerAbilityVfxController"/>.
/// </summary>
[CustomEditor(typeof(PlayerAbilityController))]
[CanEditMultipleObjects]
public class PlayerAbilityControllerEditor : Editor
{
    private static readonly string[] RefFieldNames =
    {
        "player",
        "stats",
        "combat",
        "abilityDatabase",
        "skillDatabase",
        "skillsManager",
        "actionBar",
        "equipment",
        "inventory",
        "toolbelt",
        "buffController",
        "abilityVfx"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty scriptProp = serializedObject.FindProperty("m_Script");
        if (scriptProp != null)
            EditorGUILayout.PropertyField(scriptProp);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Refs", EditorStyles.boldLabel);
        foreach (string name in RefFieldNames)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null)
                EditorGUILayout.PropertyField(p);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Global Cooldown", EditorStyles.boldLabel);
        SerializedProperty gcd = serializedObject.FindProperty("globalCooldownSeconds");
        if (gcd != null)
            EditorGUILayout.PropertyField(gcd);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
