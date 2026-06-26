using System;
using System.Collections.Generic;
using System.Linq;
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

    private readonly HashSet<int> _selectedIndices = new HashSet<int>();
    private int _selectionAnchor = -1;
    private int _moveToIndex;

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

        DrawBulkMoveToolbar();
        EditorGUILayout.Space(2f);
        DrawReorderableList();
        DrawSelectionHelp();

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

    private void DrawBulkMoveToolbar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Bulk Move", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_selectedIndices.Count == 0))
                {
                    if (GUILayout.Button("Move Up", GUILayout.Width(72f)))
                        MoveSelection(-1);
                    if (GUILayout.Button("Move Down", GUILayout.Width(84f)))
                        MoveSelection(1);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Select All", GUILayout.Width(72f)))
                    SelectAll();
                if (GUILayout.Button("Clear Sel.", GUILayout.Width(72f)))
                    ClearSelection();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int count = _selectedIndices.Count;
                EditorGUILayout.LabelField(count == 0 ? "No rows selected" : $"{count} selected", GUILayout.Width(96f));

                _moveToIndex = EditorGUILayout.IntField("Move to index", _moveToIndex);
                using (new EditorGUI.DisabledScope(_selectedIndices.Count == 0))
                {
                    if (GUILayout.Button("Go", GUILayout.Width(36f)))
                        MoveSelectionToIndex(_moveToIndex);
                }
            }
        }
    }

    private void DrawSelectionHelp()
    {
        EditorGUILayout.HelpBox(
            "Inventory/storage sort uses this list order (ItemDatabase.GetIndex).\n" +
            "Ctrl+click toggles a row. Shift+click selects a range. Drag handles still move one row at a time.",
            MessageType.Info);
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

        _list.onReorderCallback = _ =>
        {
            ClearSelection();
            serializedObject.ApplyModifiedProperties();
        };

        _list.onRemoveCallback = list =>
        {
            if (list.index >= 0 && list.index < _itemsProp.arraySize)
            {
                if (_selectedIndices.Count > 1 && _selectedIndices.Contains(list.index))
                {
                    var toRemove = _selectedIndices.OrderByDescending(i => i).ToList();
                    foreach (int index in toRemove)
                    {
                        if (index >= 0 && index < _itemsProp.arraySize)
                            _itemsProp.DeleteArrayElementAtIndex(index);
                    }

                    ClearSelection();
                    return;
                }

                _itemsProp.DeleteArrayElementAtIndex(list.index);
                PruneSelectionAfterArrayChange();
            }
        };

        _list.drawElementCallback = (rect, index, _, __) =>
        {
            if (_itemsProp == null || index < 0 || index >= _itemsProp.arraySize)
                return;

            SerializedProperty element = _itemsProp.GetArrayElementAtIndex(index);
            ItemDefinition def = element != null ? element.objectReferenceValue as ItemDefinition : null;

            bool match = IsMatch(index, (_search ?? "").Trim());
            bool selected = _selectedIndices.Contains(index);

            rect.y += 2f;
            float h = EditorGUIUtility.singleLineHeight;
            float pingW = 42f;
            float openW = 44f;
            float gap = 4f;

            Rect rowRect = new Rect(rect.x, rect.y - 2f, rect.width, h + 4f);
            Rect pingR = new Rect(rect.x, rect.y, pingW, h);
            Rect openR = new Rect(pingR.xMax + gap, rect.y, openW, h);
            Rect fieldR = new Rect(openR.xMax + gap, rect.y, rect.width - (pingW + openW + gap * 2f), h);

            HandleRowSelectionClick(rowRect, pingR, openR, fieldR, index);

            if (selected)
            {
                Color fill = EditorGUIUtility.isProSkin
                    ? new Color(0.24f, 0.45f, 0.85f, 0.35f)
                    : new Color(0.35f, 0.55f, 0.95f, 0.35f);
                EditorGUI.DrawRect(rowRect, fill);
            }

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

    private void HandleRowSelectionClick(Rect rowRect, Rect pingRect, Rect openRect, Rect fieldRect, int index)
    {
        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0)
            return;

        if (!rowRect.Contains(evt.mousePosition))
            return;

        if (pingRect.Contains(evt.mousePosition) || openRect.Contains(evt.mousePosition))
            return;

        if (fieldRect.Contains(evt.mousePosition) && evt.clickCount > 1)
            return;

        if (evt.control || evt.command)
        {
            ToggleSelection(index);
        }
        else if (evt.shift && _selectionAnchor >= 0)
        {
            SelectRange(_selectionAnchor, index);
        }
        else
        {
            SetSingleSelection(index);
        }

        _selectionAnchor = index;
        _list.index = index;
        evt.Use();
        Repaint();
    }

    private void DrawReorderableList()
    {
        if (_list == null)
            return;

        _list.DoLayoutList();
    }

    private void ToggleSelection(int index)
    {
        if (_selectedIndices.Contains(index))
            _selectedIndices.Remove(index);
        else
            _selectedIndices.Add(index);
    }

    private void SetSingleSelection(int index)
    {
        _selectedIndices.Clear();
        _selectedIndices.Add(index);
    }

    private void SelectRange(int anchor, int index)
    {
        _selectedIndices.Clear();
        int start = Mathf.Min(anchor, index);
        int end = Mathf.Max(anchor, index);
        for (int i = start; i <= end; i++)
            _selectedIndices.Add(i);
    }

    private void SelectAll()
    {
        _selectedIndices.Clear();
        if (_itemsProp == null)
            return;

        for (int i = 0; i < _itemsProp.arraySize; i++)
            _selectedIndices.Add(i);
    }

    private void ClearSelection()
    {
        _selectedIndices.Clear();
        _selectionAnchor = -1;
    }

    private void PruneSelectionAfterArrayChange()
    {
        if (_itemsProp == null || _selectedIndices.Count == 0)
            return;

        int size = _itemsProp.arraySize;
        _selectedIndices.RemoveWhere(i => i < 0 || i >= size);
    }

    private void MoveSelection(int delta)
    {
        if (_itemsProp == null || _selectedIndices.Count == 0 || delta == 0)
            return;

        List<int> ordered = _selectedIndices.OrderBy(i => i).ToList();
        int first = ordered[0];
        int last = ordered[^1];
        int size = _itemsProp.arraySize;

        if (delta < 0 && first <= 0)
            return;
        if (delta > 0 && last >= size - 1)
            return;

        var references = new List<UnityEngine.Object>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            SerializedProperty element = _itemsProp.GetArrayElementAtIndex(ordered[i]);
            references.Add(element != null ? element.objectReferenceValue : null);
        }

        for (int i = ordered.Count - 1; i >= 0; i--)
            _itemsProp.DeleteArrayElementAtIndex(ordered[i]);

        int insertAt = Mathf.Clamp(first + delta, 0, _itemsProp.arraySize);
        for (int i = 0; i < references.Count; i++)
        {
            _itemsProp.InsertArrayElementAtIndex(insertAt + i);
            _itemsProp.GetArrayElementAtIndex(insertAt + i).objectReferenceValue = references[i];
        }

        _selectedIndices.Clear();
        for (int i = 0; i < references.Count; i++)
            _selectedIndices.Add(insertAt + i);

        _selectionAnchor = insertAt;
        _list.index = insertAt;
        serializedObject.ApplyModifiedProperties();
        GUI.FocusControl(null);
        Repaint();
    }

    private void MoveSelectionToIndex(int targetIndex)
    {
        if (_itemsProp == null || _selectedIndices.Count == 0)
            return;

        int size = _itemsProp.arraySize;
        if (size <= 0)
            return;

        targetIndex = Mathf.Clamp(targetIndex, 0, size - 1);

        List<int> ordered = _selectedIndices.OrderBy(i => i).ToList();
        var references = new List<UnityEngine.Object>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            SerializedProperty element = _itemsProp.GetArrayElementAtIndex(ordered[i]);
            references.Add(element != null ? element.objectReferenceValue : null);
        }

        for (int i = ordered.Count - 1; i >= 0; i--)
            _itemsProp.DeleteArrayElementAtIndex(ordered[i]);

        int removedBeforeTarget = ordered.Count(i => i < targetIndex);
        int insertAt = Mathf.Clamp(targetIndex - removedBeforeTarget, 0, _itemsProp.arraySize);

        for (int i = 0; i < references.Count; i++)
        {
            _itemsProp.InsertArrayElementAtIndex(insertAt + i);
            _itemsProp.GetArrayElementAtIndex(insertAt + i).objectReferenceValue = references[i];
        }

        _selectedIndices.Clear();
        for (int i = 0; i < references.Count; i++)
            _selectedIndices.Add(insertAt + i);

        _selectionAnchor = insertAt;
        _list.index = insertAt;
        serializedObject.ApplyModifiedProperties();
        GUI.FocusControl(null);
        Repaint();
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

            SetSingleSelection(idx);
            _selectionAnchor = idx;
            _list.index = idx;
            GUI.FocusControl(null);
            Repaint();
            break;
        }
    }
}
