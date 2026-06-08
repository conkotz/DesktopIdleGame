using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnhancementOptionDatabase))]
public sealed class EnhancementOptionDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("basicScrollIcon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("intermediateScrollIcon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("advancedScrollIcon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chaosScrollIcon"));

        if (GUILayout.Button("Assign Default Scroll Icons"))
            CreateEnhancementScrollItemsMenu.AssignDefaultScrollIcons((EnhancementOptionDatabase)target);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("options"), true);

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Populate Default Options"))
            {
                var db = (EnhancementOptionDatabase)target;
                db.EnsureDefaults();
                EditorUtility.SetDirty(db);
            }

            if (GUILayout.Button("Create Missing Scroll Items"))
            {
                var db = (EnhancementOptionDatabase)target;
                int created = CreateEnhancementScrollItemsMenu.CreateMissingScrollItemsForDatabase(db);
                CreateEnhancementScrollItemsMenu.RegisterItemsInDatabasePublic();
                AssetDatabase.SaveAssets();
                Debug.Log(created == 0
                    ? "[EnhancementOptionDatabase] All scroll items already exist."
                    : $"[EnhancementOptionDatabase] Created or updated {created} scroll item(s).");
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
