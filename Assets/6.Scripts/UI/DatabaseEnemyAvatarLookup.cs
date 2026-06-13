using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Resolves database enemy portrait sprites from <c>Enemy Avatars_*</c> textures in the project.
/// Matches enemy ids / display names to the avatar filename suffix (e.g. bear → Enemy Avatars_bear).
/// </summary>
public static class DatabaseEnemyAvatarLookup
{
    private const string AvatarTexturePrefix = "Enemy Avatars_";

    private static readonly string[] AvatarProjectFolders =
    {
        "Assets/ImportedPackages/Enemy Pack/Enemy Avatars",
        "Assets/ImportedPackages/Tiny Swords - Enemy Pack/Enemy Avatars",
    };

    private static Dictionary<string, Sprite> _byAvatarKey;
    private static bool _cacheBuilt;

    private static readonly Dictionary<string, string> AvatarKeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["baby_bear"] = "bear",
        ["babybear"] = "bear",
        ["dev_testing_enemy_knight"] = "knight",
        ["devknight"] = "knight",
    };

    public static Sprite ResolveAvatarSprite(EnemyDefinition enemy)
    {
        if (enemy == null)
            return null;

        EnsureCacheBuilt();

        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in EnumerateCandidateAvatarKeys(enemy))
        {
            if (!tried.Add(key))
                continue;
            if (TryGetAvatarSprite(key, out Sprite sprite))
                return sprite;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateAvatarKeys(EnemyDefinition enemy)
    {
        if (!string.IsNullOrWhiteSpace(enemy.enemyId))
        {
            string id = enemy.enemyId.Trim();
            yield return NormalizeAvatarKey(id);

            if (id.StartsWith("enemy_", StringComparison.OrdinalIgnoreCase))
                yield return NormalizeAvatarKey(id.Substring("enemy_".Length));

            if (id.StartsWith("tutorial_", StringComparison.OrdinalIgnoreCase))
                yield return NormalizeAvatarKey(id.Substring("tutorial_".Length));

            if (id.StartsWith("dev_testing_enemy_", StringComparison.OrdinalIgnoreCase))
                yield return NormalizeAvatarKey(id.Substring("dev_testing_enemy_".Length));
        }

        if (!string.IsNullOrWhiteSpace(enemy.displayName))
            yield return NormalizeAvatarKey(enemy.displayName);
    }

    private static string NormalizeAvatarKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string underscored = raw.Trim().ToLowerInvariant().Replace(' ', '_');
        if (AvatarKeyAliases.TryGetValue(underscored, out string aliased))
            return aliased;

        string compact = underscored.Replace("_", string.Empty);
        if (AvatarKeyAliases.TryGetValue(compact, out aliased))
            return aliased;

        return underscored;
    }

    private static bool TryGetAvatarSprite(string key, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        EnsureCacheBuilt();
        if (_byAvatarKey.TryGetValue(key, out sprite))
            return true;

        string compact = key.Replace("_", string.Empty);
        return _byAvatarKey.TryGetValue(compact, out sprite);
    }

    private static void EnsureCacheBuilt()
    {
        if (_cacheBuilt)
            return;

        _cacheBuilt = true;
        _byAvatarKey = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

#if UNITY_EDITOR
        LoadAvatarsFromProjectFolders();
#endif
        LoadAvatarsFromLoadedSprites();
    }

#if UNITY_EDITOR
    private static void LoadAvatarsFromProjectFolders()
    {
        for (int i = 0; i < AvatarProjectFolders.Length; i++)
        {
            string folder = AvatarProjectFolders[i];
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            for (int g = 0; g < guids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                TryRegisterAvatarAssetAtPath(path);
            }
        }
    }

    private static void TryRegisterAvatarAssetAtPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return;
        if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return;

        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.StartsWith(AvatarTexturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string suffix = fileName.Substring(AvatarTexturePrefix.Length);
        if (string.IsNullOrWhiteSpace(suffix))
            return;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Sprite subSprite)
                {
                    sprite = subSprite;
                    break;
                }
            }
        }

        if (sprite != null)
            RegisterAvatarKey(suffix, sprite);
    }
#endif

    private static void LoadAvatarsFromLoadedSprites()
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null || sprite.texture == null)
                continue;

            string textureName = sprite.texture.name;
            if (!textureName.StartsWith(AvatarTexturePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string suffix = textureName.Substring(AvatarTexturePrefix.Length);
            if (string.IsNullOrWhiteSpace(suffix))
                continue;

            RegisterAvatarKey(suffix, sprite);
        }
    }

    private static void RegisterAvatarKey(string key, Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(key) || sprite == null)
            return;

        _byAvatarKey.TryAdd(key, sprite);

        string normalized = key.Trim().ToLowerInvariant().Replace(' ', '_');
        _byAvatarKey.TryAdd(normalized, sprite);
        _byAvatarKey.TryAdd(normalized.Replace("_", string.Empty), sprite);
    }
}
