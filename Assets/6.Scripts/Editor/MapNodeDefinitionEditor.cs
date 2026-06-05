using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapNodeDefinition))]
public sealed class MapNodeDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "m_Script")
                continue;

            if (iterator.name == "mapCombatScalingEnabled")
            {
                DrawMapScalingSection(iterator);
                continue;
            }

            if (iterator.name == "mapCombatScalingSpecialDropsByLevel")
                continue;

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMapScalingSection(SerializedProperty scalingEnabledProp)
    {
        SerializedProperty nodeType = serializedObject.FindProperty("nodeType");
        SerializedProperty specialDrops = serializedObject.FindProperty("mapCombatScalingSpecialDropsByLevel");
        bool isCombat = nodeType != null && nodeType.enumValueIndex == (int)MapNodeType.Combat;

        using (new EditorGUI.DisabledScope(!isCombat))
        {
            EditorGUILayout.PropertyField(scalingEnabledProp, new GUIContent("Map Combat Scaling Enabled"));

            if (isCombat && scalingEnabledProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Level 1 = current base stats. Unlocks: 200 / 500 / 1k / 2k / 5k / 10k kills on this map.\n" +
                    "Per level (from base): HP +100%, XP +10%, loot chance +25%, gold +25%. Special drops use fixed chances.",
                    MessageType.Info);

                if (specialDrops != null)
                    EditorGUILayout.PropertyField(specialDrops, new GUIContent("Special Drops By Scaling Level"), true);
            }
            else if (!isCombat && scalingEnabledProp.boolValue)
            {
                EditorGUILayout.HelpBox("Map scaling applies to Combat node type only.", MessageType.Warning);
            }
        }
    }
}
