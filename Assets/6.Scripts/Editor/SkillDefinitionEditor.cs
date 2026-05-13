#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(SkillDefinition))]
[CanEditMultipleObjects]
public class SkillDefinitionEditor : Editor
{
    private SerializedProperty _unlocksProp;
    private ReorderableList _unlocksList;

    private void OnEnable()
    {
        _unlocksProp = serializedObject.FindProperty("unlocks");
        _unlocksList = new ReorderableList(serializedObject, _unlocksProp, true, true, true, true)
        {
            drawHeaderCallback = DrawUnlocksHeader,
            drawElementCallback = DrawUnlockElement,
            elementHeightCallback = GetUnlockElementHeight
        };
    }

    private static void DrawUnlocksHeader(Rect rect)
    {
        EditorGUI.LabelField(rect, "Unlocks");
    }

    private void DrawUnlockElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = _unlocksProp.GetArrayElementAtIndex(index);
        rect.y += 2f;
        rect.height -= 4f;
        EditorGUI.PropertyField(rect, element, true);
    }

    private float GetUnlockElementHeight(int index)
    {
        SerializedProperty element = _unlocksProp.GetArrayElementAtIndex(index);
        return EditorGUI.GetPropertyHeight(element, true) + 6f;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (serializedObject.isEditingMultipleObjects)
        {
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawPropertiesExceptUnlocks();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Unlocks (reorder helpers)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginDisabledGroup(_unlocksList.index < 0 || _unlocksProp.arraySize == 0);
            if (GUILayout.Button("Move to Top", GUILayout.Height(22f)))
                MoveUnlockToTop();
            if (GUILayout.Button("Move Up", GUILayout.Height(22f)))
                MoveUnlockBy(-1);
            if (GUILayout.Button("Move Down", GUILayout.Height(22f)))
                MoveUnlockBy(1);
            if (GUILayout.Button("Move to Bottom", GUILayout.Height(22f)))
                MoveUnlockToBottom();
            EditorGUI.EndDisabledGroup();
        }

        EditorGUILayout.HelpBox(
            "Select a row in the list (click the row body), then use the buttons to reorder that unlock.",
            MessageType.None);

        _unlocksList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPropertiesExceptUnlocks()
    {
        SerializedProperty prop = serializedObject.GetIterator();
        for (bool enterChildren = true; prop.NextVisible(enterChildren); enterChildren = false)
        {
            if (prop.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(prop);
                continue;
            }

            if (prop.name == "unlocks")
                continue;

            EditorGUILayout.PropertyField(prop, true);
        }
    }

    private void MoveUnlockToTop()
    {
        int i = _unlocksList.index;
        if (i <= 0)
            return;
        _unlocksProp.MoveArrayElement(i, 0);
        _unlocksList.index = 0;
    }

    private void MoveUnlockToBottom()
    {
        int i = _unlocksList.index;
        int last = _unlocksProp.arraySize - 1;
        if (i < 0 || i >= last)
            return;
        _unlocksProp.MoveArrayElement(i, last);
        _unlocksList.index = last;
    }

    private void MoveUnlockBy(int delta)
    {
        int i = _unlocksList.index;
        int n = i + delta;
        if (i < 0 || n < 0 || n >= _unlocksProp.arraySize)
            return;
        _unlocksProp.MoveArrayElement(i, n);
        _unlocksList.index = n;
    }
}
#endif
