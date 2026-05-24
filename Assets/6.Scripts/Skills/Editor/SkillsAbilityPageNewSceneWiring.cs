#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        SkillsAbilityPageNewUI page = SkillsAbilityPageNewEditorPaths.FindPage();
        if (page == null)
        {
            Debug.LogError("[SkillsAbilityPageNewSceneWiring] No SkillsAbilityPageNewUI found. Open GamePlay.unity first.");
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

        Transform timelineContainer = SkillsAbilityPageNewEditorPaths.FindTimelineContainer(page);
        if (timelineContainer == null)
        {
            Debug.LogError("[SkillsAbilityPageNewSceneWiring] TimelineContainer child not found.", page);
            return false;
        }

        Transform timelineContent = SkillsAbilityPageNewEditorPaths.FindTimelineContent(page);
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
        horizontalSo.FindProperty("generateOnStart").boolValue = true;
        horizontalSo.FindProperty("useTestTimelineFallback").boolValue = false;
        horizontalSo.FindProperty("rebuildOnInspectorChange").boolValue = true;
        if (page.SkillDatabase != null)
            horizontalSo.FindProperty("skillDatabase").objectReferenceValue = page.SkillDatabase;

        SkillNodeDetailsPanelUI detailsPanel = page.GetComponentInChildren<SkillNodeDetailsPanelUI>(true);
        if (detailsPanel != null)
            horizontalSo.FindProperty("detailsPanel").objectReferenceValue = detailsPanel;

        TMP_Text skillLevelText = timelineContainer.Find("SkillLevelText")?.GetComponent<TMP_Text>();
        if (skillLevelText != null)
            horizontalSo.FindProperty("skillLevelText").objectReferenceValue = skillLevelText;

        AbilityEntryUI abilityEntryPrefab =
            AssetDatabase.LoadAssetAtPath<AbilityEntryUI>("Assets/2.Prefabs/UI/AbilityListEntryUI.prefab");
        Transform abilityList = page.transform.Find("BottomPanelBar/AbilityList");
        if (abilityList == null)
            abilityList = page.transform.Find("AbilityList");

        SkillsAbilityActiveAbilitiesListUI activeList = abilityList != null
            ? abilityList.GetComponent<SkillsAbilityActiveAbilitiesListUI>()
            : null;
        if (activeList == null && abilityList != null)
            activeList = abilityList.gameObject.AddComponent<SkillsAbilityActiveAbilitiesListUI>();

        SerializedObject activeListSo = activeList != null ? new SerializedObject(activeList) : null;
        if (activeListSo != null)
        {
            if (abilityEntryPrefab != null)
                activeListSo.FindProperty("entryPrefab").objectReferenceValue = abilityEntryPrefab;

            Transform scrollContent = abilityList != null ? abilityList.Find("ScrollView/Viewport/Content") : null;
            if (scrollContent != null)
                activeListSo.FindProperty("listContent").objectReferenceValue = scrollContent;
        }

        activeListSo?.ApplyModifiedPropertiesWithoutUndo();

        horizontalSo.ApplyModifiedPropertiesWithoutUndo();

        Transform combatTabBar = page.transform.Find("TopBar/CombatSkillsTabBar");
        if (combatTabBar == null)
            combatTabBar = page.transform.Find("TopBar/SkillsTabBar");
        if (combatTabBar == null)
            combatTabBar = page.transform.Find("SkillsTabBar");

        Transform gatheringTabBar = page.transform.Find("TopBar/GatheringSkillsTabBar");
        if (gatheringTabBar == null)
            SkillsAbilityPageNewGatheringTabBarSetup.EnsureGatheringTabBarInActiveScene();

        gatheringTabBar = page.transform.Find("TopBar/GatheringSkillsTabBar");

        Transform unlocks = page.transform.Find("BottomPanelBar/Unlocks");
        SkillsAbilityActiveBonusesPanelUI bonusesPanel = unlocks != null
            ? unlocks.GetComponent<SkillsAbilityActiveBonusesPanelUI>()
            : null;
        if (bonusesPanel == null && unlocks != null)
            bonusesPanel = unlocks.gameObject.AddComponent<SkillsAbilityActiveBonusesPanelUI>();

        Transform bottomPanelBar = page.transform.Find("BottomPanelBar");
        if (bottomPanelBar != null && bottomPanelBar.GetComponent<SkillsAbilityBottomPanelLayoutUI>() == null)
            bottomPanelBar.gameObject.AddComponent<SkillsAbilityBottomPanelLayoutUI>();

        Transform skillsProgress = page.transform.Find("BottomPanelBar/SkillsProgress");
        SkillsAbilitySkillsListPanelUI skillsList = skillsProgress != null
            ? skillsProgress.GetComponent<SkillsAbilitySkillsListPanelUI>()
            : null;
        if (skillsList == null && skillsProgress != null)
            skillsList = skillsProgress.gameObject.AddComponent<SkillsAbilitySkillsListPanelUI>();

        SkillListEntryUI skillEntryPrefab =
            AssetDatabase.LoadAssetAtPath<SkillListEntryUI>("Assets/2.Prefabs/UI/SkillsListEntryUI.prefab");
        MajorPassiveListEntryUI majorPassivePrefab =
            AssetDatabase.LoadAssetAtPath<MajorPassiveListEntryUI>("Assets/2.Prefabs/UI/MajorPassiveListEntryUI.prefab");

        SerializedObject pageSo = new SerializedObject(page);
        pageSo.FindProperty("horizontalSkillTimeline").objectReferenceValue = horizontal;
        if (combatTabBar != null)
            pageSo.FindProperty("combatSkillsTabBarRoot").objectReferenceValue = combatTabBar;
        if (gatheringTabBar != null)
            pageSo.FindProperty("gatheringSkillsTabBarRoot").objectReferenceValue = gatheringTabBar;

        Transform nodeToggle = page.transform.Find("TopBar/NodeToggleBar");
        if (nodeToggle != null)
        {
            pageSo.FindProperty("combatCategoryButton").objectReferenceValue =
                nodeToggle.Find("CombatSkills")?.GetComponent<Button>();
            pageSo.FindProperty("gatheringCategoryButton").objectReferenceValue =
                nodeToggle.Find("GatheringSkills")?.GetComponent<Button>();
        }

        if (skillLevelText != null)
            pageSo.FindProperty("selectedSkillTitleText").objectReferenceValue = skillLevelText;

        if (detailsPanel != null)
            pageSo.FindProperty("skillNodeDetailsPanel").objectReferenceValue = detailsPanel;

        Transform viewDetailsBar = page.transform.Find("BottomPanelBar/DetailsPanel/ViewDetailsBar");
        if (viewDetailsBar != null)
        {
            Button collapseBtn = viewDetailsBar.Find("CollapseDetailsButton")?.GetComponent<Button>()
                ?? viewDetailsBar.Find("CollapseDetails")?.GetComponent<Button>()
                ?? viewDetailsBar.Find("CollapseButton")?.GetComponent<Button>();
            if (collapseBtn != null)
            {
                collapseBtn.interactable = true;
                pageSo.FindProperty("collapseDetailsButton").objectReferenceValue = collapseBtn;
            }
        }

        if (activeList != null)
            pageSo.FindProperty("activeAbilitiesList").objectReferenceValue = activeList;
        if (abilityEntryPrefab != null)
            pageSo.FindProperty("abilityEntryPrefab").objectReferenceValue = abilityEntryPrefab;
        if (bonusesPanel != null)
            pageSo.FindProperty("activeBonusesPanel").objectReferenceValue = bonusesPanel;
        if (skillsList != null)
            pageSo.FindProperty("skillsListPanel").objectReferenceValue = skillsList;
        if (skillEntryPrefab != null)
            pageSo.FindProperty("skillEntryPrefab").objectReferenceValue = skillEntryPrefab;

        if (skillsList != null && skillEntryPrefab != null)
        {
            SerializedObject skillsListSo = new SerializedObject(skillsList);
            skillsListSo.FindProperty("entryPrefab").objectReferenceValue = skillEntryPrefab;
            skillsListSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (bonusesPanel != null && majorPassivePrefab != null)
        {
            SerializedObject bonusesSo = new SerializedObject(bonusesPanel);
            bonusesSo.FindProperty("majorPassiveEntryPrefab").objectReferenceValue = majorPassivePrefab;
            bonusesSo.ApplyModifiedPropertiesWithoutUndo();
        }

        pageSo.ApplyModifiedPropertiesWithoutUndo();

        if (detailsPanel != null)
        {
            SerializedObject detailsSo = new SerializedObject(detailsPanel);
            SerializedProperty collapseProp = pageSo.FindProperty("collapseDetailsButton");
            if (collapseProp != null && collapseProp.objectReferenceValue != null)
                detailsSo.FindProperty("collapseDetailsButton").objectReferenceValue = collapseProp.objectReferenceValue;
            detailsSo.ApplyModifiedPropertiesWithoutUndo();
        }

        return true;
    }
}
#endif
