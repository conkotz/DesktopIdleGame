using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds authored definition assets to their registry ScriptableObjects when the asset exists in the
/// project but is missing from the database list (appended at the end). Skips duplicates by
/// reference and by stable id where the runtime database requires uniqueness.
/// </summary>
public static class PopulateDatabasesMenu
{
    private const string MenuPath = "Tools/populate databases";
    private const string LogNothingAdded = "[Populate databases] Nothing added to databases.";

    [MenuItem(MenuPath)]
    private static void Populate()
    {
        Undo.SetCurrentGroupName("Populate Databases");
        int undoGroup = Undo.GetCurrentGroup();

        int added = 0;
        added += PopulateEnemyDatabases();
        added += PopulateItemDatabases();
        added += PopulateQuestDatabases();
        added += PopulateAbilityDatabases();
        added += PopulateSkillDatabases();
        added += PopulateWorldMapRegions();

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();

        if (added == 0)
            Debug.Log(LogNothingAdded);

        string msg = added == 0
            ? "No missing entries found; databases are already up to date."
            : $"Added {added} missing entr{(added == 1 ? "y" : "ies")} across databases (see Console for details).";
        EditorUtility.DisplayDialog("Populate databases", msg, "OK");
    }

    private static int PopulateEnemyDatabases()
    {
        int added = 0;
        foreach (string dbGuid in AssetDatabase.FindAssets("t:EnemyDatabase"))
        {
            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuid);
            var db = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(dbPath);
            if (!db)
                continue;

            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("enemies");
            if (list == null || !list.isArray)
                continue;

            var refs = new HashSet<EnemyDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            CollectEnemyListKeys(list, refs, ids);

            bool changed = false;
            foreach (string defGuid in AssetDatabase.FindAssets("t:EnemyDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(defGuid);
                var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
                if (!def || refs.Contains(def))
                    continue;

                if (!string.IsNullOrWhiteSpace(def.enemyId))
                {
                    string key = def.enemyId.Trim();
                    if (ids.Contains(key))
                    {
                        Debug.LogWarning(
                            $"[Populate databases] Skipping '{path}': enemyId '{key}' is already listed in '{dbPath}'.",
                            def);
                        continue;
                    }
                }

                if (!changed)
                {
                    Undo.RecordObject(db, "Populate Databases");
                    changed = true;
                }

                AppendReference(list, def);
                refs.Add(def);
                if (!string.IsNullOrWhiteSpace(def.enemyId))
                    ids.Add(def.enemyId.Trim());
                added++;
                LogAddedToDatabase(db, nameof(EnemyDatabase), path, def);
            }

            if (changed)
                so.ApplyModifiedProperties();
        }

        return added;
    }

    private static void CollectEnemyListKeys(SerializedProperty list, HashSet<EnemyDefinition> refs, HashSet<string> ids)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            var e = list.GetArrayElementAtIndex(i).objectReferenceValue as EnemyDefinition;
            if (!e)
                continue;
            refs.Add(e);
            if (!string.IsNullOrWhiteSpace(e.enemyId))
                ids.Add(e.enemyId.Trim());
        }
    }

    private static int PopulateItemDatabases()
    {
        int added = 0;
        foreach (string dbGuid in AssetDatabase.FindAssets("t:ItemDatabase"))
        {
            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuid);
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
            if (!db)
                continue;

            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("items");
            if (list == null || !list.isArray)
                continue;

            var refs = new HashSet<ItemDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i).objectReferenceValue as ItemDefinition;
                if (!item)
                    continue;
                refs.Add(item);
                string k = NormalizeItemId(item.itemId);
                if (!string.IsNullOrEmpty(k))
                    ids.Add(k);
            }

            bool changed = false;
            foreach (string defGuid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(defGuid);
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (!def || refs.Contains(def))
                    continue;

                string idKey = NormalizeItemId(def.itemId);
                if (!string.IsNullOrEmpty(idKey) && ids.Contains(idKey))
                {
                    Debug.LogWarning(
                        $"[Populate databases] Skipping '{path}': itemId '{def.itemId}' (normalized '{idKey}') already listed in '{dbPath}'.",
                        def);
                    continue;
                }

                if (!changed)
                {
                    Undo.RecordObject(db, "Populate Databases");
                    changed = true;
                }

                AppendReference(list, def);
                refs.Add(def);
                if (!string.IsNullOrEmpty(idKey))
                    ids.Add(idKey);
                added++;
                LogAddedToDatabase(db, nameof(ItemDatabase), path, def);
            }

            if (changed)
                so.ApplyModifiedProperties();
        }

        return added;
    }

    private static string NormalizeItemId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        return id.Trim().ToLowerInvariant().Replace(" ", "_");
    }

    private static int PopulateQuestDatabases()
    {
        int added = 0;
        foreach (string dbGuid in AssetDatabase.FindAssets("t:QuestDatabase"))
        {
            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuid);
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(dbPath);
            if (!db)
                continue;

            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("quests");
            if (list == null || !list.isArray)
                continue;

            var refs = new HashSet<QuestDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.arraySize; i++)
            {
                var q = list.GetArrayElementAtIndex(i).objectReferenceValue as QuestDefinition;
                if (!q)
                    continue;
                refs.Add(q);
                if (!string.IsNullOrWhiteSpace(q.questId))
                    ids.Add(q.questId.Trim());
            }

            bool changed = false;
            foreach (string defGuid in AssetDatabase.FindAssets("t:QuestDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(defGuid);
                var def = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (!def || refs.Contains(def))
                    continue;

                if (!string.IsNullOrWhiteSpace(def.questId))
                {
                    string key = def.questId.Trim();
                    if (ids.Contains(key))
                    {
                        Debug.LogWarning(
                            $"[Populate databases] Skipping '{path}': questId '{key}' already listed in '{dbPath}'.",
                            def);
                        continue;
                    }
                }

                if (!changed)
                {
                    Undo.RecordObject(db, "Populate Databases");
                    changed = true;
                }

                AppendReference(list, def);
                refs.Add(def);
                if (!string.IsNullOrWhiteSpace(def.questId))
                    ids.Add(def.questId.Trim());
                added++;
                LogAddedToDatabase(db, nameof(QuestDatabase), path, def);
            }

            if (changed)
                so.ApplyModifiedProperties();
        }

        return added;
    }

    private static int PopulateAbilityDatabases()
    {
        int added = 0;
        foreach (string dbGuid in AssetDatabase.FindAssets("t:AbilityDatabase"))
        {
            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuid);
            var db = AssetDatabase.LoadAssetAtPath<AbilityDatabase>(dbPath);
            if (!db)
                continue;

            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("abilities");
            if (list == null || !list.isArray)
                continue;

            var refs = new HashSet<AbilityDefinition>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.arraySize; i++)
            {
                var a = list.GetArrayElementAtIndex(i).objectReferenceValue as AbilityDefinition;
                if (!a)
                    continue;
                refs.Add(a);
                if (!string.IsNullOrWhiteSpace(a.abilityId))
                    ids.Add(a.abilityId.Trim());
            }

            bool changed = false;
            foreach (string defGuid in AssetDatabase.FindAssets("t:AbilityDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(defGuid);
                var def = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                if (!def || refs.Contains(def))
                    continue;

                if (!string.IsNullOrWhiteSpace(def.abilityId))
                {
                    string key = def.abilityId.Trim();
                    if (ids.Contains(key))
                    {
                        Debug.LogWarning(
                            $"[Populate databases] Skipping '{path}': abilityId '{key}' already listed in '{dbPath}'.",
                            def);
                        continue;
                    }
                }

                if (!changed)
                {
                    Undo.RecordObject(db, "Populate Databases");
                    changed = true;
                }

                AppendReference(list, def);
                refs.Add(def);
                if (!string.IsNullOrWhiteSpace(def.abilityId))
                    ids.Add(def.abilityId.Trim());
                added++;
                LogAddedToDatabase(db, nameof(AbilityDatabase), path, def);
            }

            if (changed)
                so.ApplyModifiedProperties();
        }

        return added;
    }

    private static int PopulateSkillDatabases()
    {
        int added = 0;
        foreach (string dbGuid in AssetDatabase.FindAssets("t:SkillDatabase"))
        {
            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuid);
            var db = AssetDatabase.LoadAssetAtPath<SkillDatabase>(dbPath);
            if (!db)
                continue;

            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("skills");
            if (list == null || !list.isArray)
                continue;

            var refs = new HashSet<SkillDefinition>();
            var types = new HashSet<SkillType>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var s = list.GetArrayElementAtIndex(i).objectReferenceValue as SkillDefinition;
                if (!s)
                    continue;
                refs.Add(s);
                types.Add(s.skillType);
            }

            bool changed = false;
            foreach (string defGuid in AssetDatabase.FindAssets("t:SkillDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(defGuid);
                var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                if (!def || refs.Contains(def))
                    continue;

                if (types.Contains(def.skillType))
                {
                    Debug.LogWarning(
                        $"[Populate databases] Skipping '{path}': skill type '{def.skillType}' already listed in '{dbPath}'.",
                        def);
                    continue;
                }

                if (!changed)
                {
                    Undo.RecordObject(db, "Populate Databases");
                    changed = true;
                }

                AppendReference(list, def);
                refs.Add(def);
                types.Add(def.skillType);
                added++;
                LogAddedToDatabase(db, nameof(SkillDatabase), path, def);
            }

            if (changed)
                so.ApplyModifiedProperties();
        }

        return added;
    }

    private static int PopulateWorldMapRegions()
    {
        int added = 0;
        foreach (string dbGuid in AssetDatabase.FindAssets("t:WorldMapDefinition"))
        {
            string dbPath = AssetDatabase.GUIDToAssetPath(dbGuid);
            var map = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(dbPath);
            if (!map)
                continue;

            var so = new SerializedObject(map);
            SerializedProperty list = so.FindProperty("regions");
            if (list == null || !list.isArray)
                continue;

            var refs = new HashSet<RegionDefinition>();
            var regionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.arraySize; i++)
            {
                var r = list.GetArrayElementAtIndex(i).objectReferenceValue as RegionDefinition;
                if (!r)
                    continue;
                refs.Add(r);
                if (!string.IsNullOrWhiteSpace(r.regionId))
                    regionIds.Add(r.regionId.Trim());
            }

            bool changed = false;
            foreach (string defGuid in AssetDatabase.FindAssets("t:RegionDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(defGuid);
                var def = AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
                if (!def || refs.Contains(def))
                    continue;

                if (!string.IsNullOrWhiteSpace(def.regionId))
                {
                    string key = def.regionId.Trim();
                    if (regionIds.Contains(key))
                    {
                        Debug.LogWarning(
                            $"[Populate databases] Skipping '{path}': regionId '{key}' already listed in '{dbPath}'.",
                            def);
                        continue;
                    }
                }

                if (!changed)
                {
                    Undo.RecordObject(map, "Populate Databases");
                    changed = true;
                }

                AppendReference(list, def);
                refs.Add(def);
                if (!string.IsNullOrWhiteSpace(def.regionId))
                    regionIds.Add(def.regionId.Trim());
                added++;
                LogAddedToDatabase(map, nameof(WorldMapDefinition), path, def);
            }

            if (changed)
                so.ApplyModifiedProperties();
        }

        return added;
    }

    /// <summary>Writes a clear Console line: which definition was appended and which database asset received it.</summary>
    private static void LogAddedToDatabase(UnityEngine.Object database, string databaseTypeName, string definitionAssetPath, UnityEngine.Object definition)
    {
        if (!database)
        {
            Debug.Log($"[Populate databases] Added '{definitionAssetPath}' to {databaseTypeName} (database reference missing).", definition);
            return;
        }

        string dbPath = AssetDatabase.GetAssetPath(database);
        Debug.Log(
            $"[Populate databases] Added '{definitionAssetPath}' → {databaseTypeName} '{database.name}' at '{dbPath}'.",
            definition ? definition : database);
    }

    private static void AppendReference(SerializedProperty arrayProp, UnityEngine.Object reference)
    {
        int newIndex = arrayProp.arraySize;
        arrayProp.InsertArrayElementAtIndex(newIndex);
        arrayProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = reference;
    }
}
