using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(EquipmentManager))]
public class EquipmentManagerEditor : Editor
{
    private const string ResourcesPath = "Items/ItemsDefinitions";

    private SerializedProperty _inventory;
    private SerializedProperty _handEquipper;
    private SerializedProperty _mainHandItemId;

    private void OnEnable()
    {
        _inventory = serializedObject.FindProperty("inventory");
        _handEquipper = serializedObject.FindProperty("handEquipper");
        _mainHandItemId = serializedObject.FindProperty("mainHandItemId");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Refs", EditorStyles.boldLabel);
        if (_inventory != null) EditorGUILayout.PropertyField(_inventory);
        if (_handEquipper != null) EditorGUILayout.PropertyField(_handEquipper);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("State", EditorStyles.boldLabel);

        DrawMainHandDropdownFromResources();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMainHandDropdownFromResources()
    {
        if (_mainHandItemId == null)
        {
            EditorGUILayout.HelpBox("Could not find serialized field 'mainHandItemId' on EquipmentManager.", MessageType.Error);
            return;
        }

        ItemDefinition[] all = Resources.LoadAll<ItemDefinition>(ResourcesPath);

        // Build dropdown lists
        List<string> ids = new List<string> { "" };
        List<string> labels = new List<string> { "(None)" };

        if (all != null)
        {
            foreach (var def in all)
            {
                if (!def) continue;

                // Only main-hand equippables
                if (def.equipSlot != EquipSlot.MainHand) continue;

                if (string.IsNullOrWhiteSpace(def.itemId)) continue;

                ids.Add(def.itemId);
                labels.Add($"{def.displayName} ({def.itemId})");
            }
        }

        // Current selection
        string current = _mainHandItemId.stringValue ?? "";
        int currentIndex = 0;
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == current) { currentIndex = i; break; }
        }

        // UI
        int newIndex = EditorGUILayout.Popup("Main Hand Item Id", currentIndex, labels.ToArray());
        _mainHandItemId.stringValue = ids[newIndex];

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            $"Dropdown source: Resources/{ResourcesPath}\n" +
            $"Items found: {(all == null ? 0 : all.Length)} (filtered to MainHand)",
            MessageType.None
        );
    }
}