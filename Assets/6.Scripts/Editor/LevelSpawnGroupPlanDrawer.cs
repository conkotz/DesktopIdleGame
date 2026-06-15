using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(LevelSpawnGroupPlan))]
public sealed class LevelSpawnGroupPlanDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, ReorderableList> SpawnsListsByPath = new();

    public static void InvalidateCachedLists() => SpawnsListsByPath.Clear();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return h;

        float vsp = EditorGUIUtility.standardVerticalSpacing;
        h += vsp + EditorGUIUtility.singleLineHeight * 2f + vsp; // group id + shuffle
        ReorderableList spawnsList = GetSpawnsList(property);
        if (spawnsList != null)
            h += vsp + spawnsList.GetHeight();
        h += vsp * 2f;
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, BuildGroupLabel(property), true);
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

        ReorderableList spawnsList = GetSpawnsList(property);
        if (spawnsList != null)
        {
            Rect listRect = new Rect(row.x, row.y, row.width, spawnsList.GetHeight());
            spawnsList.DoList(listRect);
        }
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

    private static ReorderableList GetSpawnsList(SerializedProperty groupProperty)
    {
        string key = groupProperty.propertyPath;
        SerializedProperty spawns = groupProperty.FindPropertyRelative("spawns");
        if (spawns == null)
            return null;

        if (SpawnsListsByPath.TryGetValue(key, out ReorderableList cached) &&
            cached.serializedProperty == spawns)
        {
            return cached;
        }

        var list = new ReorderableList(groupProperty.serializedObject, spawns, true, true, true, true)
        {
            drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Spawns", EditorStyles.boldLabel);
            },
            drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (!SpawnEditorArrayUtility.IsValidArrayIndex(spawns, index))
                    return;

                SerializedProperty element = spawns.GetArrayElementAtIndex(index);
                GUIContent rowLabel = SpawnPrefabCountDrawer.BuildRowLabel(element, index);
                rect.y += 2f;
                rect.height = EditorGUI.GetPropertyHeight(element, rowLabel, true);
                EditorGUI.PropertyField(rect, element, rowLabel, true);
            },
            elementHeightCallback = index =>
            {
                if (!SpawnEditorArrayUtility.IsValidArrayIndex(spawns, index))
                    return EditorGUIUtility.singleLineHeight + 6f;

                SerializedProperty element = spawns.GetArrayElementAtIndex(index);
                GUIContent rowLabel = SpawnPrefabCountDrawer.BuildRowLabel(element, index);
                return EditorGUI.GetPropertyHeight(element, rowLabel, true) + 6f;
            },
            onRemoveCallback = list =>
            {
                SpawnEditorArrayUtility.ScheduleRemoveAtIndex(spawns, list.index);
            },
        };

        SpawnsListsByPath[key] = list;
        return list;
    }
}
