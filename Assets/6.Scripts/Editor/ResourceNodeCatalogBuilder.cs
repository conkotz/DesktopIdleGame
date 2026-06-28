#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ResourceNodeCatalogBuilder
{
    private const string CatalogResourcePath = "Assets/Resources/Databases/ResourceNodeCatalog.asset";
    private const string NodeSearchRoot = "Assets/3.ScriptableObjects/ResourceNodeDefinitions";

    [MenuItem("Desktop Idle/Build Resource Node Catalog")]
    public static void BuildCatalog()
    {
        ResourceNodeCatalog catalog = AssetDatabase.LoadAssetAtPath<ResourceNodeCatalog>(CatalogResourcePath);
        if (!catalog)
        {
            EnsureResourcesFolder();
            catalog = ScriptableObject.CreateInstance<ResourceNodeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogResourcePath);
        }

        string[] guids = AssetDatabase.FindAssets("t:NodeDefinition", new[] { NodeSearchRoot });
        var nodes = new List<NodeDefinition>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (AssetDatabase.LoadAssetAtPath<NodeDefinition>(path) is NodeDefinition node)
                nodes.Add(node);
        }

        nodes.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty list = so.FindProperty("nodes");
        list.ClearArray();
        for (int i = 0; i < nodes.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ResourceNodeCatalogBuilder] Updated {CatalogResourcePath} with {nodes.Count} node definition(s).");
    }

    private static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Databases"))
            AssetDatabase.CreateFolder("Assets/Resources", "Databases");
    }
}
#endif
