using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(ItemDatabase))]
public sealed class ItemDatabaseEditor : Editor
{
    private SerializedProperty _itemsProp;
    private string _search = "";
    private int _lastMatchIndex = -1;
    private ReorderableList _list;

    private void OnEnable()
    {
        _itemsProp = serializedObject.FindProperty("items");
        if (_itemsProp != null)
            BuildList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSearchBar();
        EditorGUILayout.Space(4f);

        if (_itemsProp == null || !_itemsProp.isArray)
        {
            EditorGUILayout.HelpBox("Could not find 'items' list on ItemDatabase.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (_list == null || _list.serializedProperty != _itemsProp)
            BuildList();

        DrawReorderableList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSearchBar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Search", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                string next = EditorGUILayout.TextField(_search);
                if (!string.Equals(next, _search, StringComparison.Ordinal))
                {
                    _search = next ?? "";
                    _lastMatchIndex = -1;
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(56f)))
                    {
                        _search = "";
                        _lastMatchIndex = -1;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                {
                    if (GUILayout.Button("Prev", GUILayout.Width(48f)))
                        JumpToMatch(direction: -1);
                    if (GUILayout.Button("Next", GUILayout.Width(48f)))
                        JumpToMatch(direction: 1);
                }
            }
        }
    }

    private void BuildList()
    {
        _list = new ReorderableList(serializedObject, _itemsProp, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);

        _list.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
        _list.footerHeight = 16f;

        _list.drawHeaderCallback = rect =>
        {
            int total = _itemsProp != null ? _itemsProp.arraySize : 0;
            string q = (_search ?? "").Trim();
            bool hasQuery = !string.IsNullOrEmpty(q);
            int matches = hasQuery ? CountMatches(q) : 0;
            string right = hasQuery ? $"{matches} match{(matches == 1 ? "" : "es")}" : "";
            EditorGUI.LabelField(rect, $"Items ({total})", right);
        };

        _list.drawElementCallback = (rect, index, _, __) =>
        {
            if (_itemsProp == null || index < 0 || index >= _itemsProp.arraySize)
                return;

            SerializedProperty element = _itemsProp.GetArrayElementAtIndex(index);
            ItemDefinition def = element != null ? element.objectReferenceValue as ItemDefinition : null;

            bool match = IsMatch(index, (_search ?? "").Trim());

            rect.y += 2f;
            float h = EditorGUIUtility.singleLineHeight;
            float pingW = 42f;
            float openW = 44f;
            float gap = 4f;

            Rect pingR = new Rect(rect.x, rect.y, pingW, h);
            Rect openR = new Rect(pingR.xMax + gap, rect.y, openW, h);
            Rect fieldR = new Rect(openR.xMax + gap, rect.y, rect.width - (pingW + openW + gap * 2f), h);

            using (new EditorGUI.DisabledScope(def == null))
            {
                if (GUI.Button(pingR, "Ping"))
                    EditorGUIUtility.PingObject(def);
                if (GUI.Button(openR, "Open"))
                    Selection.activeObject = def;
            }

            string label = def != null
                ? $"{index}: {(!string.IsNullOrWhiteSpace(def.itemId) ? def.itemId : def.name)}"
                : $"{index}: (None)";

            Color prev = GUI.color;
            if (!string.IsNullOrWhiteSpace((_search ?? "").Trim()) && match)
                GUI.color = new Color(1f, 0.95f, 0.6f, 1f);

            EditorGUI.PropertyField(fieldR, element, new GUIContent(label), includeChildren: false);
            GUI.color = prev;
        };
    }

    private void DrawReorderableList()
    {
        if (_list == null)
            return;

        // Let the list fill the inspector height (no fixed MinHeight).
        _list.DoLayoutList();
    }

    private int CountMatches(string query)
    {
        if (_itemsProp == null || _itemsProp.arraySize <= 0)
            return 0;

        int count = 0;
        for (int i = 0; i < _itemsProp.arraySize; i++)
        {
            if (IsMatch(i, query))
                count++;
        }

        return count;
    }

    private bool IsMatch(int index, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        if (_itemsProp == null || index < 0 || index >= _itemsProp.arraySize)
            return false;

        SerializedProperty element = _itemsProp.GetArrayElementAtIndex(index);
        ItemDefinition def = element != null ? element.objectReferenceValue as ItemDefinition : null;
        if (def == null)
            return false;

        string q = query.Trim();
        if (q.Length == 0)
            return true;

        string id = def.itemId ?? "";
        string name = def.displayName ?? "";
        string assetName = def.name ?? "";

        return id.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
               assetName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void JumpToMatch(int direction)
    {
        if (_itemsProp == null)
            return;

        string q = (_search ?? "").Trim();
        if (string.IsNullOrEmpty(q))
            return;

        int n = _itemsProp.arraySize;
        if (n <= 0)
            return;

        int start = _lastMatchIndex;
        if (start < 0 || start >= n)
            start = direction > 0 ? -1 : n;

        for (int step = 1; step <= n; step++)
        {
            int idx = (start + direction * step) % n;
            if (idx < 0) idx += n;

            if (!IsMatch(idx, q))
                continue;

            _lastMatchIndex = idx;

            SerializedProperty element = _itemsProp.GetArrayElementAtIndex(idx);
            UnityEngine.Object obj = element != null ? element.objectReferenceValue : null;
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }

            // Ensure the row is visible.
            _list.index = idx;
            GUI.FocusControl(null);
            Repaint();
            break;
        }
    }
}

