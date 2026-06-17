#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds <see cref="WorldNameLabelResolver"/> only to whitelisted world-object prefabs.
/// </summary>
public static class WorldNameLabelResolverPrefabTools
{
    private const string MenuRoot = "Tools/World Labels/";

    private static readonly string[] WhitelistedPrefabPaths =
    {
        "Assets/2.Prefabs/UI/LeaveAreaRope.prefab",
        "Assets/2.Prefabs/UI/SignPost.prefab",
        "Assets/2.Prefabs/UI/BearDenEntrance.prefab",
        "Assets/2.Prefabs/UI/SpiderLairEntrance.prefab",
        "Assets/2.Prefabs/ResourcesNodes/Trees/SplitwoodTree.prefab",
        "Assets/2.Prefabs/ResourcesNodes/Trees/HardwoodTree.prefab",
        "Assets/2.Prefabs/ResourcesNodes/Trees/WildwoodTree.prefab",
        "Assets/2.Prefabs/ResourcesNodes/StoneDeposit.prefab",
        "Assets/2.Prefabs/ResourcesNodes/SmallPond.prefab",
    };

    [MenuItem(MenuRoot + "Install Resolvers On Whitelisted Prefabs", false, 0)]
    public static void InstallWhitelistedResolversMenu()
    {
        int changed = InstallWhitelistedResolvers(logSummary: true);
        EditorUtility.DisplayDialog(
            "World Name Labels",
            changed > 0
                ? $"Installed or refreshed WorldNameLabelResolver on {changed} whitelisted prefab(s)."
                : "Whitelisted prefabs are already up to date.",
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

            if (!IsWhitelistedPath(path))
                continue;

            if (ProcessPrefabAtPath(path, forceRefreshResolver: true))
                changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WorldNameLabelResolver] Resolved NameLabels on {changed} selected prefab(s).");
    }

    public static int InstallWhitelistedResolvers(bool logSummary)
    {
        int changed = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < WhitelistedPrefabPaths.Length; i++)
            {
                if (ProcessPrefabAtPath(WhitelistedPrefabPaths[i], forceRefreshResolver: true))
                    changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        if (logSummary)
            Debug.Log($"[WorldNameLabelResolver] Updated {changed} whitelisted prefab(s).");

        return changed;
    }

    private static bool IsWhitelistedPath(string path)
    {
        for (int i = 0; i < WhitelistedPrefabPaths.Length; i++)
        {
            if (WhitelistedPrefabPaths[i] == path)
                return true;
        }

        return false;
    }

    private static bool ProcessPrefabAtPath(string path, bool forceRefreshResolver)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
        if (!prefabRoot)
            return false;

        try
        {
            Transform root = FindResolverRoot(prefabRoot.transform);
            if (!root || !WorldNameLabelResolver.HasResolvableWorldNameLabels(root, includeInactive: true))
                return false;

            RemoveMisplacedResolvers(prefabRoot.transform, root);

            WorldNameLabelResolver resolver = root.GetComponent<WorldNameLabelResolver>();
            bool addedResolver = false;
            if (!resolver)
            {
                resolver = root.gameObject.AddComponent<WorldNameLabelResolver>();
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

    private static Transform FindResolverRoot(Transform prefabRoot)
    {
        if (WorldNameLabelResolver.HasResolvableWorldNameLabels(prefabRoot, includeInactive: true))
            return prefabRoot;

        for (int i = 0; i < prefabRoot.childCount; i++)
        {
            Transform child = prefabRoot.GetChild(i);
            if (child && WorldNameLabelResolver.HasResolvableWorldNameLabels(child, includeInactive: true))
                return child;
        }

        return prefabRoot;
    }

    private static void RemoveMisplacedResolvers(Transform prefabRoot, Transform keepRoot)
    {
        WorldNameLabelResolver[] resolvers = prefabRoot.GetComponentsInChildren<WorldNameLabelResolver>(true);
        for (int i = 0; i < resolvers.Length; i++)
        {
            WorldNameLabelResolver resolver = resolvers[i];
            if (!resolver || resolver.transform == keepRoot)
                continue;

            Object.DestroyImmediate(resolver, true);
        }
    }
}
#endif
