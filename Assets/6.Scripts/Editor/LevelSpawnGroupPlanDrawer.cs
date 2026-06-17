using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LevelSpawnGroupPlan))]
public sealed class LevelSpawnGroupPlanDrawer : PropertyDrawer
{
    public static void InvalidateCachedLists() { }

    public static float GetGroupInspectorHeight(SerializedProperty property, GUIContent label)
    {
        if (property == null)
            return EditorGUIUtility.singleLineHeight + 6f;

        float h = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return h + 6f;

        float vsp = EditorGUIUtility.standardVerticalSpacing;
        h += vsp + EditorGUIUtility.singleLineHeight * 2f + vsp;
        h += vsp + GetSpawnsListHeight(property.FindPropertyRelative("spawns"));
        h += vsp * 2f + 6f;
        return h;
    }

    public static void DrawGroupInspector(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null)
            return;

        Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
        if (!property.isExpanded)
            return;

        float vsp = EditorGUIUtility.standardVerticalSpacing;
        row.y += row.height + vsp;

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        try
        {
            SerializedProperty groupId = property.FindPropertyRelative("groupId");
            if (groupId != null)
            {
                EditorGUI.PropertyField(row, groupId, new GUIContent("Group Id"));
                row.y += EditorGUIUtility.singleLineHeight + vsp;
            }

            SerializedProperty shuffle = property.FindPropertyRelative("shuffleSpawnPoints");
            if (shuffle != null)
            {
                EditorGUI.PropertyField(row, shuffle, new GUIContent("Shuffle Spawn Points"));
                row.y += EditorGUIUtility.singleLineHeight + vsp;
            }
        }
        finally
        {
            EditorGUI.indentLevel = oldIndent;
        }

        SerializedProperty spawns = property.FindPropertyRelative("spawns");
        if (spawns != null)
        {
            Rect listRect = new Rect(row.x, row.y, row.width, GetSpawnsListHeight(spawns));
            DrawSpawnsList(listRect, spawns);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return GetGroupInspectorHeight(property, label) - 6f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        DrawGroupInspector(position, property, label);
    }

    public static string BuildGroupLabel(SerializedProperty property)
    {
        if (property == null)
            return "(invalid group)";

        string groupId = property.FindPropertyRelative("groupId")?.stringValue?.Trim();
        if (string.IsNullOrEmpty(groupId))
            groupId = "(no group id)";

        SerializedProperty spawns = property.FindPropertyRelative("spawns");
        int count = spawns != null ? spawns.arraySize : 0;
        return $"{groupId}  ·  {count} spawn{(count == 1 ? string.Empty : "s")}";
    }

    private static float GetSpawnsListHeight(SerializedProperty spawns)
    {
        if (spawns == null || !spawns.isArray)
            return EditorGUIUtility.singleLineHeight;

        float line = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;
        float h = line + vsp;

        for (int i = 0; i < spawns.arraySize; i++)
        {
            SerializedProperty element = spawns.GetArrayElementAtIndex(i);
            GUIContent rowLabel = SpawnPrefabCountDrawer.BuildRowLabel(element, i);
            h += SpawnPrefabCountDrawer.GetInspectorHeight(element, rowLabel) + vsp;
        }

        h += line + vsp;
        return h;
    }

    private static void DrawSpawnsList(Rect listRect, SerializedProperty spawns)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;

        Rect header = new Rect(listRect.x, listRect.y, listRect.width, line);
        EditorGUI.LabelField(header, "Spawns", EditorStyles.boldLabel);

        float y = header.yMax + vsp;
        for (int i = 0; i < spawns.arraySize; i++)
        {
            SerializedProperty element = spawns.GetArrayElementAtIndex(i);
            GUIContent rowLabel = SpawnPrefabCountDrawer.BuildRowLabel(element, i);
            float rowHeight = SpawnPrefabCountDrawer.GetInspectorHeight(element, rowLabel);
            Rect rowRect = new Rect(listRect.x, y, listRect.width, rowHeight);
            SpawnPrefabCountDrawer.DrawInspector(rowRect, element, rowLabel);
            y += rowHeight + vsp;
        }

        Rect footer = new Rect(listRect.x, y, listRect.width, line);
        Rect addButton = new Rect(footer.xMax - 44f, footer.y, 20f, line);
        Rect removeButton = new Rect(footer.xMax - 20f, footer.y, 20f, line);

        if (GUI.Button(addButton, "+"))
        {
            spawns.InsertArrayElementAtIndex(spawns.arraySize);
            spawns.serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        using (new EditorGUI.DisabledScope(spawns.arraySize <= 0))
        {
            if (GUI.Button(removeButton, "-") && spawns.arraySize > 0)
            {
                SpawnEditorArrayUtility.ScheduleRemoveAtIndex(spawns, spawns.arraySize - 1);
                GUIUtility.ExitGUI();
            }
        }
    }
}
