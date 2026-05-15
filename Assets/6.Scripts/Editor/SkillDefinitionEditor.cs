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
        GUIContent rowLabel = BuildUnlockRowLabel(element);
        rect.y += 2f;
        rect.height -= 4f;
        EditorGUI.PropertyField(rect, element, rowLabel, true);
    }

    private float GetUnlockElementHeight(int index)
    {
        SerializedProperty element = _unlocksProp.GetArrayElementAtIndex(index);
        GUIContent rowLabel = BuildUnlockRowLabel(element);
        return EditorGUI.GetPropertyHeight(element, rowLabel, true) + 6f;
    }

    /// <summary>
    /// Foldout label for each unlock row (visible when the row is collapsed).
    /// </summary>
    private static GUIContent BuildUnlockRowLabel(SerializedProperty unlockElement)
    {
        if (unlockElement == null)
            return new GUIContent("(invalid unlock)");

        SerializedProperty typeProp = unlockElement.FindPropertyRelative("unlockType");
        SerializedProperty titleProp = unlockElement.FindPropertyRelative("title");
        SerializedProperty levelProp = unlockElement.FindPropertyRelative("requiredLevel");

        int level = 0;
        if (levelProp != null && levelProp.propertyType == SerializedPropertyType.Integer)
            level = levelProp.intValue;

        string typePart = "?";
        if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
        {
            string[] names = typeProp.enumDisplayNames;
            int idx = typeProp.enumValueIndex;
            if (names != null && idx >= 0 && idx < names.Length)
                typePart = names[idx];
            else if (typeProp.enumNames != null && idx >= 0 && idx < typeProp.enumNames.Length)
                typePart = ObjectNames.NicifyVariableName(typeProp.enumNames[idx]);
        }

        string titlePart = titleProp != null && titleProp.propertyType == SerializedPropertyType.String
            ? (titleProp.stringValue ?? string.Empty).Trim()
            : string.Empty;
        if (string.IsNullOrEmpty(titlePart))
            titlePart = "(no title)";

        return new GUIContent($"Lv{level} · [{typePart}] {titlePart}");
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
