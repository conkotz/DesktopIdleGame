using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpawnPrefabCount))]
public sealed class SpawnPrefabCountDrawer : PropertyDrawer
{
    private const float RemoveButtonWidth = 72f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float total = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return total;

        float vsp = EditorGUIUtility.standardVerticalSpacing;
        total += vsp + LineHeight(property, "spawnPointGroupId");
        total += vsp + LineHeight(property, "spawnPointName");
        total += vsp + SectionHeight(property, includeContent: true);
        total += vsp + LineHeight(property, "count");

        bool overridesExpanded = SessionState.GetBool(
            property.propertyPath + "._overridesFoldout",
            HasOverrides(property));
        total += vsp + EditorGUIUtility.singleLineHeight;
        if (overridesExpanded)
            total += GetOverridesInnerHeight(property);

        if (CanRemoveRow(property))
            total += vsp + EditorGUIUtility.singleLineHeight;

        total += vsp;
        return total;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;
        Rect row = new Rect(position.x, position.y, position.width, line);

        bool canRemove = CanRemoveRow(property);
        float foldoutWidth = canRemove ? row.width - RemoveButtonWidth - 4f : row.width;
        Rect foldoutRect = new Rect(row.x, row.y, foldoutWidth, line);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (canRemove && GUI.Button(new Rect(row.x + row.width - RemoveButtonWidth, row.y, RemoveButtonWidth, line), "Remove"))
        {
            SpawnEditorArrayUtility.ScheduleRemoveElement(property);
            GUIUtility.ExitGUI();
        }

        if (!property.isExpanded)
            return;

        row.y += line + vsp;
        Rect content = new Rect(position.x, row.y, position.width, position.height - (row.y - position.y));

        DrawFullWidthField(ref content, property, "spawnPointGroupId", vsp);
        DrawFullWidthField(ref content, property, "spawnPointName", vsp);
        DrawContentSection(ref content, property, vsp);
        DrawFullWidthField(ref content, property, "count", vsp);
        DrawOverridesSection(ref content, property, vsp);

        if (canRemove)
        {
            Rect removeRow = new Rect(content.x, content.y, content.width, line);
            if (GUI.Button(removeRow, "Remove This Row"))
            {
                SpawnEditorArrayUtility.ScheduleRemoveElement(property);
                GUIUtility.ExitGUI();
            }
        }
    }

    public static GUIContent BuildRowLabel(SerializedProperty property, int index)
    {
        return new GUIContent($"#{index + 1}  {BuildSummaryText(property)}");
    }

    public static string BuildSummaryText(SerializedProperty property)
    {
        if (property == null)
            return "(invalid spawn)";

        string point = property.FindPropertyRelative("spawnPointName")?.stringValue?.Trim();
        if (string.IsNullOrEmpty(point))
            point = "any point";

        string content = ResolveContentLabel(property);
        int count = Mathf.Max(1, property.FindPropertyRelative("count")?.intValue ?? 1);
        return $"{point}  •  {content}  ×{count}";
    }

    private static string ResolveContentLabel(SerializedProperty property)
    {
        Object item = property.FindPropertyRelative("itemDefinition")?.objectReferenceValue;
        if (item)
            return item.name;

        Object enemy = property.FindPropertyRelative("enemyDefinition")?.objectReferenceValue;
        if (enemy)
            return enemy.name;

        Object prefab = property.FindPropertyRelative("prefab")?.objectReferenceValue;
        if (prefab)
            return prefab.name;

        return "(empty)";
    }

    private static void DrawFullWidthField(ref Rect content, SerializedProperty parent, string fieldName, float vsp)
    {
        SerializedProperty child = parent.FindPropertyRelative(fieldName);
        if (child == null)
            return;

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        try
        {
            content.height = EditorGUI.GetPropertyHeight(child, true);
            EditorGUI.PropertyField(content, child, includeChildren: true);
            content.y += content.height + vsp;
        }
        finally
        {
            EditorGUI.indentLevel = oldIndent;
        }
    }

    private static void DrawContentSection(ref Rect content, SerializedProperty property, float vsp)
    {
        float sectionH = SectionHeight(property, includeContent: true);
        Rect box = new Rect(content.x, content.y, content.width, sectionH);
        GUI.Box(box, GUIContent.none);

        Rect inner = new Rect(box.x + 8f, box.y + 4f, box.width - 16f, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(inner, "Content", EditorStyles.boldLabel);
        inner.y += inner.height + 2f;

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        try
        {
            SerializedProperty item = property.FindPropertyRelative("itemDefinition");
            SerializedProperty enemy = property.FindPropertyRelative("enemyDefinition");
            SerializedProperty prefab = property.FindPropertyRelative("prefab");

            if (enemy != null)
            {
                inner.width = box.width - 16f;
                inner.height = EditorGUI.GetPropertyHeight(enemy, true);
                EditorGUI.PropertyField(inner, enemy, includeChildren: true);
                inner.y += inner.height + 2f;
            }

            if (prefab != null && (enemy == null || enemy.objectReferenceValue == null))
            {
                inner.height = EditorGUI.GetPropertyHeight(prefab, true);
                EditorGUI.PropertyField(inner, prefab, includeChildren: true);
                inner.y += inner.height + 2f;
            }

            if (item != null)
            {
                inner.height = EditorGUI.GetPropertyHeight(item, true);
                EditorGUI.PropertyField(inner, item, includeChildren: true);
                inner.y += inner.height + 2f;

                if (item.objectReferenceValue != null)
                {
                    DrawOptionalField(ref inner, property, "itemAmount");
                    DrawOptionalField(ref inner, property, "itemRespawnTimer");
                    DrawOptionalField(ref inner, property, "levelOneShotPickupKey");
                }
            }

            if (ShouldShowRespawnUntilSimpleWavesStart(property))
                DrawOptionalField(ref inner, property, "respawnUntilSimpleWavesStart");
        }
        finally
        {
            EditorGUI.indentLevel = oldIndent;
        }

        content.y += box.height + vsp;
    }

    private static void DrawOverridesSection(ref Rect content, SerializedProperty property, float vsp)
    {
        bool hasValues = HasOverrides(property);
        string foldoutKey = property.propertyPath + "._overridesFoldout";
        bool expanded = SessionState.GetBool(foldoutKey, hasValues);

        Rect foldoutRect = new Rect(content.x, content.y, content.width, EditorGUIUtility.singleLineHeight);
        expanded = EditorGUI.Foldout(foldoutRect, expanded, "Per-spawn overrides (optional)", true);
        SessionState.SetBool(foldoutKey, expanded);
        content.y += EditorGUIUtility.singleLineHeight;

        if (!expanded)
        {
            content.y += vsp;
            return;
        }

        content.y += 2f;
        float innerH = GetOverridesInnerHeight(property);
        Rect box = new Rect(content.x, content.y, content.width, innerH);
        GUI.Box(box, GUIContent.none);

        Rect inner = new Rect(box.x + 8f, box.y + 4f, box.width - 16f, EditorGUIUtility.singleLineHeight);
        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        try
        {
            DrawOptionalField(ref inner, property, "portalTargetMapNodeId");
            DrawOptionalField(ref inner, property, "teleporterLinkId");
            DrawOptionalField(ref inner, property, "teleporterDestinationLabel");
        }
        finally
        {
            EditorGUI.indentLevel = oldIndent;
        }

        content.y += box.height + vsp;
    }

    private static void DrawOptionalField(ref Rect rect, SerializedProperty parent, string fieldName)
    {
        SerializedProperty child = parent.FindPropertyRelative(fieldName);
        if (child == null)
            return;

        rect.height = EditorGUI.GetPropertyHeight(child, true);
        EditorGUI.PropertyField(rect, child, includeChildren: true);
        rect.y += rect.height + 2f;
    }

    private static float LineHeight(SerializedProperty parent, string fieldName)
    {
        SerializedProperty child = parent.FindPropertyRelative(fieldName);
        return child != null ? EditorGUI.GetPropertyHeight(child, true) : 0f;
    }

    private static float SectionHeight(SerializedProperty property, bool includeContent)
    {
        if (!includeContent)
            return 0f;

        float h = 8f + EditorGUIUtility.singleLineHeight + 2f;

        SerializedProperty enemy = property.FindPropertyRelative("enemyDefinition");
        SerializedProperty prefab = property.FindPropertyRelative("prefab");
        SerializedProperty item = property.FindPropertyRelative("itemDefinition");

        if (enemy != null)
            h += EditorGUI.GetPropertyHeight(enemy, true) + 2f;

        if (prefab != null && (enemy == null || enemy.objectReferenceValue == null))
            h += EditorGUI.GetPropertyHeight(prefab, true) + 2f;

        if (item != null)
        {
            h += EditorGUI.GetPropertyHeight(item, true) + 2f;
            if (item.objectReferenceValue != null)
            {
                h += LineFor("itemAmount", property);
                h += LineFor("itemRespawnTimer", property);
                h += LineFor("levelOneShotPickupKey", property);
            }
        }

        if (ShouldShowRespawnUntilSimpleWavesStart(property))
            h += LineFor("respawnUntilSimpleWavesStart", property);

        return h + 4f;
    }

    private static float LineFor(string fieldName, SerializedProperty parent)
    {
        SerializedProperty child = parent.FindPropertyRelative(fieldName);
        return child != null ? EditorGUI.GetPropertyHeight(child, true) + 2f : 0f;
    }

    private static float GetOverridesInnerHeight(SerializedProperty property)
    {
        float h = 8f;
        h += LineFor("portalTargetMapNodeId", property);
        h += LineFor("teleporterLinkId", property);
        h += LineFor("teleporterDestinationLabel", property);
        return h + 4f;
    }

    private static bool HasOverrides(SerializedProperty property)
    {
        return !string.IsNullOrWhiteSpace(property.FindPropertyRelative("portalTargetMapNodeId")?.stringValue) ||
               !string.IsNullOrWhiteSpace(property.FindPropertyRelative("teleporterLinkId")?.stringValue) ||
               !string.IsNullOrWhiteSpace(property.FindPropertyRelative("teleporterDestinationLabel")?.stringValue);
    }

    private static bool ShouldShowRespawnUntilSimpleWavesStart(SerializedProperty property)
    {
        return property.propertyPath.IndexOf("simpleCombatWaves.Array", System.StringComparison.Ordinal) < 0;
    }

    private static bool CanRemoveRow(SerializedProperty property) =>
        SpawnEditorArrayUtility.TryGetParentArray(property, out _, out _);

    private static bool TryGetParentArray(SerializedProperty element, out SerializedProperty array, out int index) =>
        SpawnEditorArrayUtility.TryGetParentArray(element, out array, out index);
}
