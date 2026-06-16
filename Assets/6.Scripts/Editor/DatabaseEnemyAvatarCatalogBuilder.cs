#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps <see cref="DatabaseEnemyAvatarCatalog"/> in Resources in sync with Enemy Avatars textures.
/// </summary>
public static class DatabaseEnemyAvatarCatalogBuilder
{
    private const string CatalogAssetPath = "Assets/Resources/Databases/DatabaseEnemyAvatarCatalog.asset";
    private const string CombatProfileRowSourcePath = "Assets/2.Prefabs/UI/EnemyCombatProfileRow.prefab";
    private const string CombatProfileRowResourcesPath = "Assets/Resources/UI/EnemyCombatProfileRow.prefab";
    private const string AvatarTexturePrefix = "Enemy Avatars_";

    private static readonly string[] AvatarProjectFolders =
    {
        "Assets/ImportedPackages/Enemy Pack/Enemy Avatars",
        "Assets/ImportedPackages/Tiny Swords - Enemy Pack/Enemy Avatars",
    };

    [MenuItem("Tools/Database/Rebuild Enemy Avatar Catalog")]
    public static void RebuildFromMenu()
    {
        RebuildCatalogAsset();
        SyncCombatProfileRowPrefabForBuild();
        AssetDatabase.SaveAssets();
        Debug.Log("[DatabaseEnemyAvatarCatalog] Rebuilt enemy avatar catalog for builds.");
    }

    public static void SyncCombatProfileRowPrefabForBuild()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(CombatProfileRowSourcePath))
            return;

        EnsureParentFolderExists(CombatProfileRowResourcesPath);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(CombatProfileRowResourcesPath) != null)
            AssetDatabase.DeleteAsset(CombatProfileRowResourcesPath);

        if (!AssetDatabase.CopyAsset(CombatProfileRowSourcePath, CombatProfileRowResourcesPath))
        {
            Debug.LogWarning(
                "[DatabaseEnemyAvatarCatalog] Failed to copy EnemyCombatProfileRow prefab into Resources/UI for builds.");
            return;
        }

        EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<GameObject>(CombatProfileRowResourcesPath));
    }

    public static void RebuildCatalogAsset()
    {
        var sprites = new List<Sprite>();
        var seen = new HashSet<Sprite>();

        for (int i = 0; i < AvatarProjectFolders.Length; i++)
        {
            string folder = AvatarProjectFolders[i];
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            for (int g = 0; g < guids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                TryCollectSpriteAtPath(path, sprites, seen);
            }
        }

        sprites.Sort(static (a, b) =>
            string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty,
                System.StringComparison.OrdinalIgnoreCase));

        DatabaseEnemyAvatarCatalog catalog = AssetDatabase.LoadAssetAtPath<DatabaseEnemyAvatarCatalog>(CatalogAssetPath);
        if (!catalog)
        {
            EnsureParentFolderExists(CatalogAssetPath);
            catalog = ScriptableObject.CreateInstance<DatabaseEnemyAvatarCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.SetAvatarsForEditor(sprites);
        EditorUtility.SetDirty(catalog);
    }

    private static void TryCollectSpriteAtPath(string assetPath, List<Sprite> sprites, HashSet<Sprite> seen)
    {
        if (string.IsNullOrWhiteSpace(assetPath) ||
            !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.StartsWith(AvatarTexturePrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (!sprite)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite subSprite)
                {
                    sprite = subSprite;
                    break;
                }
            }
        }

        if (sprite && seen.Add(sprite))
            sprites.Add(sprite);
    }

    private static void EnsureParentFolderExists(string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir))
            return;

        string[] parts = dir.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

public sealed class DatabaseEnemyAvatarCatalogBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        DatabaseEnemyAvatarCatalogBuilder.RebuildCatalogAsset();
        DatabaseEnemyAvatarCatalogBuilder.SyncCombatProfileRowPrefabForBuild();
    }
}
#endif
