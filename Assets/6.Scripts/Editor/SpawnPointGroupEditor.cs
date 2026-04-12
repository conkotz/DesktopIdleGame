using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpawnPointGroup))]
public class SpawnPointGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Fill Points From Children"))
        {
            var group = (SpawnPointGroup)target;
            Transform root = group.transform;
            SerializedProperty pointsProp = serializedObject.FindProperty("points");

            Undo.RecordObject(group, "Fill Spawn Point Group From Children");

            pointsProp.ClearArray();
            pointsProp.arraySize = root.childCount;
            for (int i = 0; i < root.childCount; i++)
                pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = root.GetChild(i);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(group);
        }
    }
}
