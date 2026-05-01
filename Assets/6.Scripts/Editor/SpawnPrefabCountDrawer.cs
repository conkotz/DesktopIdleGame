using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpawnPrefabCount))]
public sealed class SpawnPrefabCountDrawer : PropertyDrawer
{
    private static readonly string[] FieldOrder =
    {
        "spawnPointGroupId",
        "spawnPointName",
        "enemyDefinition",
        "prefab",
        "itemDefinition",
        "itemAmount",
        "respawnUntilSimpleWavesStart",
        "levelOneShotPickupKey",
        "count",
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float total = EditorGUIUtility.singleLineHeight;
        float vsp = EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded)
            return total;

        bool showRespawnUntilWaves = ShouldShowRespawnUntilSimpleWavesStart(property);
        for (int i = 0; i < FieldOrder.Length; i++)
        {
            string fieldName = FieldOrder[i];
            if (!showRespawnUntilWaves && fieldName == "respawnUntilSimpleWavesStart")
                continue;

            SerializedProperty child = property.FindPropertyRelative(fieldName);
            if (child == null)
                continue;

            total += vsp;
            total += EditorGUI.GetPropertyHeight(child, true);
        }

        return total;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
        if (!property.isExpanded)
            return;

        bool showRespawnUntilWaves = ShouldShowRespawnUntilSimpleWavesStart(property);
        float vsp = EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.indentLevel++;

        for (int i = 0; i < FieldOrder.Length; i++)
        {
            string fieldName = FieldOrder[i];
            if (!showRespawnUntilWaves && fieldName == "respawnUntilSimpleWavesStart")
                continue;

            SerializedProperty child = property.FindPropertyRelative(fieldName);
            if (child == null)
                continue;

            row.y += row.height + vsp;
            row.height = EditorGUI.GetPropertyHeight(child, true);
            EditorGUI.PropertyField(row, child, includeChildren: true);
        }

        EditorGUI.indentLevel--;
    }

    private static bool ShouldShowRespawnUntilSimpleWavesStart(SerializedProperty property)
    {
        // Hide this row-level respawn override only in the simple wave sequence section.
        // Keep it visible for spawnGroupPlans where it's meant to be configured.
        string path = property.propertyPath;
        return path.IndexOf("simpleCombatWaves.Array", System.StringComparison.Ordinal) < 0;
    }
}
