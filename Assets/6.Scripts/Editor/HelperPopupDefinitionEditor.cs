#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Hides inventory / quest trigger fields unless their activation mode is selected.</summary>
[CustomEditor(typeof(HelperPopupDefinition))]
public sealed class HelperPopupDefinitionEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        DrawProperty(nameof(HelperPopupDefinition.helperId));
        DrawProperty(nameof(HelperPopupDefinition.title));
        DrawProperty(nameof(HelperPopupDefinition.bodyText));

        SerializedProperty activation = serializedObject.FindProperty(nameof(HelperPopupDefinition.activationTrigger));
        EditorGUILayout.PropertyField(activation);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.requiredMapNodeId)));

        var triggerKind = (HelperActivationTrigger)Mathf.Clamp(
            activation.enumValueIndex,
            (int)HelperActivationTrigger.None,
            (int)HelperActivationTrigger.WorldItemDropped);

        if (triggerKind == HelperActivationTrigger.InventoryItemCountReached)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.inventoryTriggerItemId)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.inventoryTriggerItemCount)));
        }

        if (triggerKind == HelperActivationTrigger.WorldItemDropped)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.worldDropTriggerItem)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.worldDropTriggerItemId)));
        }

        if (triggerKind == HelperActivationTrigger.QuestGatherObjectiveReady)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.questGatherTriggerQuestId)));
        }

        if (triggerKind == HelperActivationTrigger.QuestRewardClaimed)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.questRewardClaimedTriggerQuestId)));
        }

        if (triggerKind == HelperActivationTrigger.QuestAccepted)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.questAcceptedTriggerQuestId)));
        }

        if (triggerKind == HelperActivationTrigger.SkillLevelReached)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.skillLevelTriggerSkill)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.skillLevelTriggerMinimumNewLevel)));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.priority)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.showCloseButton)));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(HelperPopupDefinition.darkenScreenAndLockGameplay)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(HelperPopupDefinition.dismissModes)));

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(HelperPopupDefinition.whitelistInteractEntries)),
            new GUIContent("Whitelist interact hover Id's"),
            includeChildren: true);

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(HelperPopupDefinition.whitelistedInteractionIds)),
            includeChildren: true);

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(HelperPopupDefinition.highlightWhitelistTargetsDuringHelper)));

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(HelperPopupDefinition.highlightInventorySlotsForItem)));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProperty(string propertyPath)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyPath), includeChildren: true);
    }
}
#endif
