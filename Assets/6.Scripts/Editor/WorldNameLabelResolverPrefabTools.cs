#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds <see cref="WorldNameLabelResolver"/> to prefab roots that contain embedded world <c>NameLabel</c> children.
/// </summary>
public static class WorldNameLabelResolverPrefabTools
{
    private const string MenuRoot = "Tools/World Labels/";
    private const string ResolverPrefabRootName = "NameLabel";

    [MenuItem(MenuRoot + "Add Resolvers To Prefabs With Embedded NameLabels", false, 0)]
    public static void AddResolversToPrefabsMenu()
    {
        int added = AddResolversToAllPrefabs(logSummary: true);
        EditorUtility.DisplayDialog(
            "World Name Labels",
            added > 0
                ? $"Added or refreshed WorldNameLabelResolver on {added} prefab(s)."
                : "No prefabs needed changes.",
            "OK");
    }

    [MenuItem(MenuRoot + "Resolve NameLabels On Selected Prefabs", false, 1)]
    public static void ResolveSelectedPrefabsMenu()
    {
        int changed = 0;
        Object[] selection = Selection.objects;
        for (int i = 0; i < selection.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selection[i]);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                continue;

            if (ProcessPrefabAtPath(path, forceRefreshResolver: true))
                changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WorldNameLabelResolver] Resolved NameLabels on {changed} selected prefab(s).");
    }

    public static int AddResolversToAllPrefabs(bool logSummary)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int changed = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (ShouldSkipPrefabPath(path))
                    continue;

                if (ProcessPrefabAtPath(path, forceRefreshResolver: false))
                    changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        if (logSummary)
            Debug.Log($"[WorldNameLabelResolver] Updated {changed} prefab(s).");

        return changed;
    }

    private static bool ShouldSkipPrefabPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        return string.Equals(fileName, ResolverPrefabRootName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool ProcessPrefabAtPath(string path, bool forceRefreshResolver)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
        if (!prefabRoot)
            return false;

        try
        {
            if (!WorldNameLabelResolver.HasResolvableWorldNameLabels(prefabRoot.transform, includeInactive: true))
                return false;

            WorldNameLabelResolver resolver = prefabRoot.GetComponent<WorldNameLabelResolver>();
            bool addedResolver = false;
            if (!resolver)
            {
                resolver = prefabRoot.AddComponent<WorldNameLabelResolver>();
                addedResolver = true;
            }
            else if (!forceRefreshResolver && !addedResolver)
            {
                return false;
            }

            resolver.ResolveNameLabels();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
#endif
