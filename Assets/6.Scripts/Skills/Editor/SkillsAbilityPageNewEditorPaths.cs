#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Resolves hierarchy paths under SkillsAbilityPageNEW for editor setup tools.</summary>
public static class SkillsAbilityPageNewEditorPaths
{
    public const string GamePlayScenePath = "Assets/1.Scenes/GamePlay.unity";

    /// <summary>RectTransform parent for <see cref="SkillNodeDetailsPanelUI"/> (newest names first).</summary>
    public static readonly string[] DetailsContentLeafNames =
    {
        "ViewDetailsContent",
        "CurrentSelectionContent",
    };

    private static readonly string[] DetailsContentPaths =
    {
        "BottomPanelBar/DetailsPanel/ViewDetailsContent",
        "BottomPanelBar/DetailsPanel/CurrentSelectionContent",
        "BottomPanelBar/CurrentSelectionsPanel/CurrentSelectionContent",
        "BottomPanelBar/CurrentSelectionsPanel/ViewDetailsContent",
        "BottomPanelBar/CurrentSelectionPanel/CurrentSelectionContent",
        "DetailsPanel/ViewDetailsContent",
        "DetailsPanel/CurrentSelectionContent",
        "CurrentSelectionsPanel/CurrentSelectionContent",
        "CurrentSelectionsPanel/ViewDetailsContent",
        "CurrentSelectionPanel/CurrentSelectionContent",
        "BottomPanelBar/ViewDetailsContent",
        "BottomPanelBar/CurrentSelectionContent",
    };

    public static SkillsAbilityPageNewUI FindPage(bool preferActiveScene = true)
    {
        SkillsAbilityPageNewUI[] pages = Object.FindObjectsByType<SkillsAbilityPageNewUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (pages != null && pages.Length > 0)
        {
            if (preferActiveScene)
            {
                Scene active = SceneManager.GetActiveScene();
                for (int i = 0; i < pages.Length; i++)
                {
                    if (pages[i] != null && pages[i].gameObject.scene == active)
                        return pages[i];
                }
            }

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null)
                    return pages[i];
            }
        }

        Transform root = FindTransformInLoadedScenes("SkillsAbilityPageNEW");
        return root != null ? root.GetComponent<SkillsAbilityPageNewUI>() : null;
    }

    public static Transform FindSkillsAbilityPageRoot()
    {
        SkillsAbilityPageNewUI page = FindPage(preferActiveScene: false);
        if (page != null)
            return page.transform;

        return FindTransformInLoadedScenes("SkillsAbilityPageNEW");
    }

    /// <summary>Host rect for the node details panel (ViewDetailsContent / legacy CurrentSelectionContent).</summary>
    public static Transform FindCurrentSelectionContent(SkillsAbilityPageNewUI page = null)
    {
        Transform pageRoot = page != null ? page.transform : FindSkillsAbilityPageRoot();
        if (pageRoot != null)
        {
            Transform underPage = FindUnderTransform(pageRoot, DetailsContentPaths, DetailsContentLeafNames);
            if (underPage != null)
                return underPage;
        }

        Transform fromSelection = FindFromEditorSelection();
        if (fromSelection != null)
            return fromSelection;

        for (int i = 0; i < DetailsContentLeafNames.Length; i++)
        {
            Transform underPageName = FindTransformInLoadedScenes(
                DetailsContentLeafNames[i],
                requireAncestorNamed: "SkillsAbilityPageNEW");
            if (underPageName != null)
                return underPageName;
        }

        for (int i = 0; i < DetailsContentLeafNames.Length; i++)
        {
            Transform any = FindTransformInLoadedScenes(DetailsContentLeafNames[i]);
            if (any != null)
                return any;
        }

        return null;
    }

    public static bool EnsureGamePlaySceneLoaded(bool setActive = true)
    {
        Scene scene = EditorSceneManager.GetSceneByPath(GamePlayScenePath);
        if (scene.IsValid() && scene.isLoaded)
            return true;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        EditorSceneManager.OpenScene(GamePlayScenePath, OpenSceneMode.Single);
        return EditorSceneManager.GetSceneByPath(GamePlayScenePath).isLoaded;
    }

    public static Transform FindTimelineContainer(SkillsAbilityPageNewUI page = null)
    {
        Transform pageRoot = page != null ? page.transform : FindSkillsAbilityPageRoot();
        if (pageRoot == null)
            return null;

        Transform direct = pageRoot.Find("TimelineContainer");
        return direct != null ? direct : FindChildByName(pageRoot, "TimelineContainer");
    }

    public static Transform FindTimelineContent(SkillsAbilityPageNewUI page = null)
    {
        Transform container = FindTimelineContainer(page);
        if (container == null)
            return null;

        Transform content = container.Find("TimelineViewport/TimelineContent");
        if (content != null)
            return content;

        Transform[] children = container.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "TimelineContent")
                return children[i];
        }

        return null;
    }

    private static Transform FindFromEditorSelection()
    {
        if (Selection.activeTransform == null)
            return null;

        Transform t = Selection.activeTransform;
        while (t != null)
        {
            if (IsDetailsContentHost(t.name))
                return t;
            t = t.parent;
        }

        return null;
    }

    public static bool IsDetailsContentHost(string objectName)
    {
        for (int i = 0; i < DetailsContentLeafNames.Length; i++)
        {
            if (DetailsContentLeafNames[i] == objectName)
                return true;
        }

        return false;
    }

    private static Transform FindUnderTransform(Transform root, string[] paths, string[] leafNames)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            Transform found = root.Find(paths[i]);
            if (found != null)
                return found;
        }

        for (int i = 0; i < leafNames.Length; i++)
        {
            Transform found = FindChildByName(root, leafNames[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string leafName)
    {
        if (root.name == leafName)
            return root;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == leafName)
                return all[i];
        }

        return null;
    }

    private static Transform GetPrefabStageRootTransform()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null)
            return null;

        GameObject root = prefabStage.prefabContentsRoot;
        return root != null ? root.transform : null;
    }

    private static Transform FindTransformInLoadedScenes(string objectName, string requireAncestorNamed = null)
    {
        Transform prefabRoot = GetPrefabStageRootTransform();
        if (prefabRoot != null)
        {
            Transform inPrefab = FindChildByName(prefabRoot, objectName);
            if (inPrefab != null && AncestorNamed(inPrefab, requireAncestorNamed))
                return inPrefab;
        }

        List<Transform> matches = new();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] children = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].name != objectName)
                        continue;

                    if (!AncestorNamed(children[i], requireAncestorNamed))
                        continue;

                    matches.Add(children[i]);
                }
            }
        }

        if (matches.Count == 0)
            return null;

        Scene activeScene = SceneManager.GetActiveScene();
        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i].gameObject.scene == activeScene)
                return matches[i];
        }

        return matches[0];
    }

    private static bool AncestorNamed(Transform t, string ancestorName)
    {
        if (string.IsNullOrEmpty(ancestorName))
            return true;

        while (t != null)
        {
            if (t.name == ancestorName)
                return true;
            t = t.parent;
        }

        return false;
    }
}
#endif
