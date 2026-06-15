using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MapNodeDefinition))]
public sealed class MapNodeDefinitionEditor : Editor
{
    private bool _showDefaultScrollDrops = true;
    private SerializedProperty _spawnGroupPlansProp;
    private ReorderableList _spawnGroupPlansList;
    private SerializedProperty _inMapTeleportersProp;
    private ReorderableList _inMapTeleportersList;

    private void OnEnable()
    {
        _spawnGroupPlansProp = serializedObject.FindProperty("spawnGroupPlans");
        if (_spawnGroupPlansProp != null)
        {
            _spawnGroupPlansList = new ReorderableList(serializedObject, _spawnGroupPlansProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Spawn Group Plans", EditorStyles.boldLabel);
                },
                drawElementCallback = DrawSpawnGroupPlanElement,
                elementHeightCallback = GetSpawnGroupPlanElementHeight,
                onRemoveCallback = list =>
                {
                    SpawnEditorArrayUtility.ScheduleRemoveAtIndex(_spawnGroupPlansProp, list.index);
                },
            };
        }

        _inMapTeleportersProp = serializedObject.FindProperty("inMapTeleporters");
        if (_inMapTeleportersProp != null)
        {
            _inMapTeleportersList = new ReorderableList(serializedObject, _inMapTeleportersProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "In Map Teleporters", EditorStyles.boldLabel);
                },
                drawElementCallback = DrawInMapTeleporterElement,
                elementHeightCallback = GetInMapTeleporterElementHeight,
                onRemoveCallback = list =>
                {
                    SpawnEditorArrayUtility.ScheduleRemoveAtIndex(_inMapTeleportersProp, list.index);
                },
            };
        }
    }

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

            if (iterator.name == "spawnGroupPlans")
            {
                DrawSpawnGroupPlansSection();
                continue;
            }

            if (iterator.name == "inMapTeleporters")
            {
                DrawInMapTeleportersSection();
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSpawnGroupPlansSection()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Spawn plans (GamePlay scene)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each group targets a scene SpawnPointGroup. Expand a group to edit spawns. " +
            "Remove a spawn with the header Remove button, Remove This Row at the bottom, or the list X.",
            MessageType.None);

        if (_spawnGroupPlansList != null)
            _spawnGroupPlansList.DoLayoutList();
        else
            EditorGUILayout.PropertyField(_spawnGroupPlansProp, true);

        EditorGUILayout.Space(4f);
    }

    private void DrawSpawnGroupPlanElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (!SpawnEditorArrayUtility.IsValidArrayIndex(_spawnGroupPlansProp, index))
            return;

        SerializedProperty element = _spawnGroupPlansProp.GetArrayElementAtIndex(index);
        GUIContent label = new GUIContent(LevelSpawnGroupPlanDrawer.BuildGroupLabel(element));
        rect.y += 2f;
        rect.height = EditorGUI.GetPropertyHeight(element, label, true);
        EditorGUI.PropertyField(rect, element, label, true);
    }

    private float GetSpawnGroupPlanElementHeight(int index)
    {
        if (!SpawnEditorArrayUtility.IsValidArrayIndex(_spawnGroupPlansProp, index))
            return EditorGUIUtility.singleLineHeight + 6f;

        SerializedProperty element = _spawnGroupPlansProp.GetArrayElementAtIndex(index);
        GUIContent label = new GUIContent(LevelSpawnGroupPlanDrawer.BuildGroupLabel(element));
        return EditorGUI.GetPropertyHeight(element, label, true) + 6f;
    }

    private void DrawInMapTeleportersSection()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Same-map teleporter pairs. Set Destination Label to the second line under \"Teleporter\" " +
            "(e.g. \"To combat testing area\"). Use the X on each row to remove that teleporter entry.",
            MessageType.None);

        if (_inMapTeleportersList != null)
            _inMapTeleportersList.DoLayoutList();
        else
            EditorGUILayout.PropertyField(_inMapTeleportersProp, true);

        EditorGUILayout.Space(4f);
    }

    private void DrawInMapTeleporterElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (!SpawnEditorArrayUtility.IsValidArrayIndex(_inMapTeleportersProp, index))
            return;

        SerializedProperty element = _inMapTeleportersProp.GetArrayElementAtIndex(index);
        GUIContent label = new GUIContent(InMapTeleporterPlanDrawer.BuildLabel(element));
        rect.y += 2f;
        rect.height = EditorGUI.GetPropertyHeight(element, label, true);
        EditorGUI.PropertyField(rect, element, label, true);
    }

    private float GetInMapTeleporterElementHeight(int index)
    {
        if (!SpawnEditorArrayUtility.IsValidArrayIndex(_inMapTeleportersProp, index))
            return EditorGUIUtility.singleLineHeight + 6f;

        SerializedProperty element = _inMapTeleportersProp.GetArrayElementAtIndex(index);
        GUIContent label = new GUIContent(InMapTeleporterPlanDrawer.BuildLabel(element));
        return EditorGUI.GetPropertyHeight(element, label, true) + 6f;
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
                    "Level 1 = current base stats. Unlocks: 200 / 500 / 1k / 2k / 5k / 10k / 20k kills on this map.\n" +
                    "Per level (from base): HP +100%, XP +10%, loot chance +25%, gold +25%. Special drops use fixed chances.\n\n" +
                    "Enhancement scroll, map enhancement, and slot reduction scroll drops are shared defaults (see below) and are not stored in this map's list. " +
                    "Use the list below only for map-specific extras you add manually.",
                    MessageType.Info);

                DrawDefaultScrollDropPreview();

                if (specialDrops != null)
                    EditorGUILayout.PropertyField(specialDrops, new GUIContent("Map-Specific Special Drops By Scaling Level"), true);
            }
            else if (!isCombat && scalingEnabledProp.boolValue)
            {
                EditorGUILayout.HelpBox("Map scaling applies to Combat node type only.", MessageType.Warning);
            }
        }
    }

    private void DrawDefaultScrollDropPreview()
    {
        _showDefaultScrollDrops = EditorGUILayout.Foldout(
            _showDefaultScrollDrops,
            "Default Scaling Special Drops (automatic)",
            true);

        if (!_showDefaultScrollDrops)
            return;

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.HelpBox(
                "Applied at runtime to every combat map with scaling enabled. Each tier uses these total rates only (one roll per scroll category per kill, then a random scroll from that pool; map enhancements and slot reduction scroll roll individually).",
                MessageType.None);

            EditorGUILayout.TextArea(
                MapCombatScalingSpecialDropDefaults.BuildEditorPreviewText(),
                GUILayout.MinHeight(140f));
        }
    }
}
