#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click setup for <see cref="WorldMapRegionAnchorGroup"/> / <see cref="WorldMapNodeAnchor"/> under MappingPoints.
/// Select the MappingPoints (or a region folder) root, then run the menu item.
/// </summary>
public static class WorldMapAnchorsSetupMenu
{
    private const string MenuRoot = "Tools/World Map/";

    [MenuItem(MenuRoot + "Setup region + node anchors (children of selection)", false, 1)]
    private static void SetupFromSelection()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("World Map Anchors", "Select MappingPoints (or a region folder), then run again.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Setup World Map Anchors");
        int group = Undo.GetCurrentGroup();

        foreach (GameObject root in Selection.gameObjects)
        {
            if (!root)
                continue;

            Transform rt = root.transform;
            if (GetRegionIdForFolderName(rt.name) != null)
            {
                ConfigureRegionFolder(rt);
                continue;
            }

            for (int i = 0; i < rt.childCount; i++)
                ConfigureRegionFolder(rt.GetChild(i));
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log("[WorldMapAnchors] Region + node anchor components updated for selection.");
    }

    [MenuItem(MenuRoot + "Add / refresh WorldMapNodeAnchor on selection only", false, 12)]
    private static void NodeAnchorsOnSelectionOnly()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            return;

        Undo.SetCurrentGroupName("World Map Node Anchors");
        int group = Undo.GetCurrentGroup();
        foreach (GameObject go in Selection.gameObjects)
        {
            if (!go || !go.GetComponent<RectTransform>())
                continue;
            EnsureNodeAnchor(go);
        }

        Undo.CollapseUndoOperations(group);
    }

    private static void ConfigureRegionFolder(Transform regionFolder)
    {
        if (!regionFolder)
            return;

        string regionId = GetRegionIdForFolderName(regionFolder.name);
        if (string.IsNullOrEmpty(regionId))
        {
            regionId = DefaultRegionIdFromFolderName(regionFolder.name);
            Debug.LogWarning(
                $"[WorldMapAnchors] Unrecognized region folder name '{regionFolder.name}'. Using regionId '{regionId}'. " +
                "Adjust WorldMapRegionAnchorGroup.regionId in the Inspector if needed.",
                regionFolder);
        }

        WorldMapRegionAnchorGroup groupComp = regionFolder.GetComponent<WorldMapRegionAnchorGroup>();
        if (!groupComp)
        {
            groupComp = Undo.AddComponent<WorldMapRegionAnchorGroup>(regionFolder.gameObject);
            Undo.RecordObject(groupComp, "Add WorldMapRegionAnchorGroup");
        }
        else
            Undo.RecordObject(groupComp, "Configure WorldMapRegionAnchorGroup");

        groupComp.regionId = regionId;

        var anchors = regionFolder.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < anchors.Length; i++)
        {
            RectTransform r = anchors[i];
            if (!r || r.gameObject == regionFolder.gameObject)
                continue;
            if (r.GetComponent<WorldMapRegionAnchorGroup>() != null)
                continue;

            EnsureNodeAnchor(r.gameObject);
        }
    }

    private static void EnsureNodeAnchor(GameObject go)
    {
        WorldMapNodeAnchor a = go.GetComponent<WorldMapNodeAnchor>();
        string inferred = InferMapNodeId(go.name);
        if (string.IsNullOrEmpty(inferred))
        {
            Debug.LogWarning($"[WorldMapAnchors] Could not infer mapNodeId for '{go.name}'. Set WorldMapNodeAnchor.mapNodeId manually.", go);
            if (!a)
                Undo.AddComponent<WorldMapNodeAnchor>(go);
            return;
        }

        if (!a)
        {
            a = Undo.AddComponent<WorldMapNodeAnchor>(go);
            Undo.RecordObject(a, "Add WorldMapNodeAnchor");
        }
        else
            Undo.RecordObject(a, "Configure WorldMapNodeAnchor");

        a.mapNodeId = inferred;
    }

    /// <summary>Known region folder names from the hierarchy → RegionDefinition.regionId.</summary>
    private static string GetRegionIdForFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return null;

        switch (folderName.Trim().ToLowerInvariant())
        {
            case "greenlands":
                return "greenlands";
            case "tutorial":
                return "tutorial";
            case "emberhollow":
                return "emberhollow";
            default:
                return null;
        }
    }

    private static string DefaultRegionIdFromFolderName(string folderName)
    {
        return Regex.Replace(folderName.Trim().ToLowerInvariant(), @"\s+", "_");
    }

    /// <summary>Anchor GameObject names from your layout → MapNodeDefinition.nodeId.</summary>
    private static string InferMapNodeId(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        string key = objectName.Trim();
        if (TryLookup(key, out string id))
            return id;

        string noAnchor = Regex.Replace(key, @"Anchor\s*$", "", RegexOptions.IgnoreCase).Trim();
        if (noAnchor != key && TryLookup(noAnchor + "Anchor", out id))
            return id;
        if (TryLookup(noAnchor, out id))
            return id;

        return CamelToSnakeNodeId(noAnchor);
    }

    private static bool TryLookup(string nameKey, out string nodeId)
    {
        nodeId = null;
        if (string.IsNullOrEmpty(nameKey))
            return false;

        foreach (KeyValuePair<string, string> kv in KnownAnchorNames)
        {
            if (string.Equals(kv.Key, nameKey, StringComparison.OrdinalIgnoreCase))
            {
                nodeId = kv.Value;
                return true;
            }
        }

        return false;
    }

    private static readonly Dictionary<string, string> KnownAnchorNames = BuildKnown();

    private static Dictionary<string, string> BuildKnown()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Greenlands (screenshot names)
            { "DuskwoodAnchor", "duskwood" },
            { "battlegroundsAnchor", "battlegrounds" },
            { "battlegrounds2Anchor", "battlegrounds_2" },
            { "HuntingGroundsAnchor", "hunting_grounds" },
            { "BearDenAnchor", "bear_den" },
            { "GreenGroveAnchor", "green_grove" },
            { "SpiderLairAnchor", "spider_lair" },
            { "EnduranceTrialAnchor", "endurance_trial_greenlands" },
            // Tutorial
            { "Tutorial1Anchor", "tutorial_1" },
            { "Tutorial2Anchor", "tutorial_2" },
            { "Tutorial3Anchor", "tutorial_3" },
        };
        return d;
    }

    private static string CamelToSnakeNodeId(string s)
    {
        if (string.IsNullOrEmpty(s))
            return null;

        string t = Regex.Replace(s.Trim(), @"([a-z])([A-Z])", "$1_$2");
        t = Regex.Replace(t, @"([A-Za-z])(\d)", "$1_$2");
        t = Regex.Replace(t, @"(\d)([A-Za-z])", "$1_$2");
        return t.ToLowerInvariant();
    }
}
#endif
