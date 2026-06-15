using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(InMapTeleporterPlan))]
public sealed class InMapTeleporterPlanDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float vsp = EditorGUIUtility.standardVerticalSpacing;
        float h = EditorGUIUtility.singleLineHeight;
        h += vsp + LineFor(property, "teleporterLinkId");
        h += vsp + LineFor(property, "spawnPointGroupId");
        h += vsp + LineFor(property, "spawnPointName");
        h += vsp + LineFor(property, "teleporterPrefab");
        h += vsp + LineFor(property, "destinationLabel");
        h += vsp;
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;
        Rect row = new Rect(position.x, position.y, position.width, line);

        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, BuildLabel(property), true);
        if (!property.isExpanded)
            return;

        row.y += line + vsp;
        Rect content = new Rect(position.x, row.y, position.width, position.height - (row.y - position.y));

        DrawField(ref content, property, "teleporterLinkId", vsp);
        DrawField(ref content, property, "spawnPointGroupId", vsp);
        DrawField(ref content, property, "spawnPointName", vsp);
        DrawField(ref content, property, "teleporterPrefab", vsp);
        DrawField(ref content, property, "destinationLabel", vsp);
    }

    public static string BuildLabel(SerializedProperty property)
    {
        if (property == null)
            return "(invalid teleporter)";

        string link = property.FindPropertyRelative("teleporterLinkId")?.stringValue?.Trim();
        string point = property.FindPropertyRelative("spawnPointName")?.stringValue?.Trim();
        string dest = property.FindPropertyRelative("destinationLabel")?.stringValue?.Trim();

        if (string.IsNullOrEmpty(link))
            link = "(no link id)";
        if (string.IsNullOrEmpty(point))
            point = "any point";

        string destPart = string.IsNullOrEmpty(dest) ? string.Empty : $"  ·  {dest}";
        return $"{link} @ {point}{destPart}";
    }

    private static void DrawField(ref Rect content, SerializedProperty parent, string fieldName, float vsp)
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

    private static float LineFor(SerializedProperty parent, string fieldName)
    {
        SerializedProperty child = parent.FindPropertyRelative(fieldName);
        return child != null ? EditorGUI.GetPropertyHeight(child, true) : 0f;
    }
}
