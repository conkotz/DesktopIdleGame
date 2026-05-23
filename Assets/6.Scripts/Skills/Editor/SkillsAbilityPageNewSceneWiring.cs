#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds timeline scaffold components to SkillsAbilityPageNEW and assigns prefab references.
/// </summary>
public static class SkillsAbilityPageNewSceneWiring
{
    private const string NodePrefabPath = "Assets/2.Prefabs/UI/SkillsAbilityNew/SkillTimelineNodeUI.prefab";
    private const string ChoiceGroupPrefabPath = "Assets/2.Prefabs/UI/SkillsAbilityNew/MilestoneGroupUI.prefab";

    [MenuItem("Tools/Skills/Wire Skills Ability Page NEW (Active Scene)")]
    public static void WireActiveScene()
    {
        SkillsAbilityPageNewUI page = Object.FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);
        if (page == null)
        {
            Debug.LogError("[SkillsAbilityPageNewSceneWiring] No SkillsAbilityPageNewUI found in the active scene.");
            return;
        }

        if (!WirePage(page))
            return;

        EditorUtility.SetDirty(page);
        EditorSceneManager.MarkSceneDirty(page.gameObject.scene);
        Debug.Log("[SkillsAbilityPageNewSceneWiring] Wired SkillsAbilityPageNEW in the active scene.", page);
    }

    public static bool WirePage(SkillsAbilityPageNewUI page)
    {
        if (page == null)
            return false;

        Transform timelineContainer = page.transform.Find("TimelineContainer");
        if (timelineContainer == null)
        {
            Debug.LogError("[SkillsAbilityPageNewSceneWiring] TimelineContainer child not found.", page);
            return false;
        }

        Transform timelineContent = timelineContainer.Find("TimelineViewport/TimelineContent");
        if (timelineContent == null)
        {
            Debug.LogError("[SkillsAbilityPageNewSceneWiring] TimelineViewport/TimelineContent not found.", page);
            return false;
        }

        SkillTimelineNodeUI nodePrefab = AssetDatabase.LoadAssetAtPath<SkillTimelineNodeUI>(NodePrefabPath);
        SkillChoiceGroupUI choiceGroupPrefab = AssetDatabase.LoadAssetAtPath<SkillChoiceGroupUI>(ChoiceGroupPrefabPath);
        if (nodePrefab == null || choiceGroupPrefab == null)
        {
            Debug.LogError("[SkillsAbilityPageNewSceneWiring] Timeline prefabs missing under SkillsAbilityNew.", page);
            return false;
        }

        SkillTimelineScaffoldUI scaffold = timelineContainer.GetComponent<SkillTimelineScaffoldUI>();
        if (scaffold == null)
            scaffold = timelineContainer.gameObject.AddComponent<SkillTimelineScaffoldUI>();

        SerializedObject scaffoldSo = new SerializedObject(scaffold);
        scaffoldSo.FindProperty("rebuildOnEnable").boolValue = true;
        scaffoldSo.FindProperty("buildPlaceholderNodes").boolValue = false;
        scaffoldSo.ApplyModifiedPropertiesWithoutUndo();

        HorizontalSkillTreeScaffoldUI horizontal = timelineContainer.GetComponent<HorizontalSkillTreeScaffoldUI>();
        if (horizontal == null)
            horizontal = timelineContainer.gameObject.AddComponent<HorizontalSkillTreeScaffoldUI>();

        SerializedObject horizontalSo = new SerializedObject(horizontal);
        horizontalSo.FindProperty("timelineContent").objectReferenceValue = timelineContent as RectTransform;
        horizontalSo.FindProperty("nodePrefab").objectReferenceValue = nodePrefab;
        horizontalSo.FindProperty("choiceGroupPrefab").objectReferenceValue = choiceGroupPrefab;
        horizontalSo.FindProperty("timelineScaffold").objectReferenceValue = scaffold;
        horizontalSo.FindProperty("generateOnStart").boolValue = false;
        horizontalSo.FindProperty("useTestTimelineFallback").boolValue = false;
        horizontalSo.ApplyModifiedPropertiesWithoutUndo();

        Transform tabBar = page.transform.Find("TopBar/SkillsTabBar");
        if (tabBar == null)
            tabBar = page.transform.Find("SkillsTabBar");

        SerializedObject pageSo = new SerializedObject(page);
        pageSo.FindProperty("horizontalSkillTimeline").objectReferenceValue = horizontal;
        if (tabBar != null)
            pageSo.FindProperty("skillTabBarRoot").objectReferenceValue = tabBar;

        pageSo.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
#endif
