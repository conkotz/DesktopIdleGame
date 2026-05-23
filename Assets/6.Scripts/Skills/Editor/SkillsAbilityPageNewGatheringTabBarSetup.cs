#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates TopBar/GatheringSkillsTabBar (Woodcutting, Mining, Fishing) for SkillsAbilityPageNEW.
/// </summary>
public static class SkillsAbilityPageNewGatheringTabBarSetup
{
    private static readonly SkillType[] GatheringSkillTypes =
    {
        SkillType.Woodcutting,
        SkillType.Mining,
        SkillType.Fishing
    };

    [MenuItem("Tools/Skills/Ensure Gathering Skills Tab Bar (Active Scene)")]
    public static void EnsureGatheringTabBarInActiveScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[GatheringTabBarSetup] Exit Play Mode first.");
            return;
        }

        SkillsAbilityPageNewUI page = SkillsAbilityPageNewEditorPaths.FindPage();
        if (page == null)
        {
            Debug.LogError("[GatheringTabBarSetup] SkillsAbilityPageNEW not found.");
            return;
        }

        Transform topBar = page.transform.Find("TopBar");
        if (topBar == null)
        {
            Debug.LogError("[GatheringTabBarSetup] TopBar not found.", page);
            return;
        }

        Transform combatTabBar = topBar.Find("CombatSkillsTabBar");
        if (combatTabBar == null)
            combatTabBar = topBar.Find("SkillsTabBar");
        if (combatTabBar == null)
        {
            Debug.LogError("[GatheringTabBarSetup] CombatSkillsTabBar not found.", page);
            return;
        }

        Transform gatheringTabBar = topBar.Find("GatheringSkillsTabBar");
        if (gatheringTabBar == null)
        {
            GameObject barGo = new GameObject("GatheringSkillsTabBar", typeof(RectTransform));
            gatheringTabBar = barGo.transform;
            gatheringTabBar.SetParent(topBar, false);

            RectTransform combatRt = combatTabBar as RectTransform;
            RectTransform gatherRt = (RectTransform)gatheringTabBar;
            if (combatRt != null && gatherRt != null)
            {
                gatherRt.anchorMin = combatRt.anchorMin;
                gatherRt.anchorMax = combatRt.anchorMax;
                gatherRt.pivot = combatRt.pivot;
                gatherRt.anchoredPosition = combatRt.anchoredPosition;
                gatherRt.sizeDelta = new Vector2(520f, combatRt.sizeDelta.y);
            }

            HorizontalLayoutGroup hlg = barGo.AddComponent<HorizontalLayoutGroup>();
            HorizontalLayoutGroup combatHlg = combatTabBar.GetComponent<HorizontalLayoutGroup>();
            if (combatHlg != null)
            {
                hlg.padding = combatHlg.padding;
                hlg.spacing = combatHlg.spacing;
                hlg.childAlignment = combatHlg.childAlignment;
                hlg.childControlWidth = combatHlg.childControlWidth;
                hlg.childControlHeight = combatHlg.childControlHeight;
                hlg.childForceExpandWidth = combatHlg.childForceExpandWidth;
                hlg.childForceExpandHeight = combatHlg.childForceExpandHeight;
            }
            else
            {
                hlg.spacing = 8;
                hlg.childAlignment = TextAnchor.MiddleLeft;
            }
        }

        Button templateButton = null;
        for (int i = 0; i < combatTabBar.childCount; i++)
        {
            templateButton = combatTabBar.GetChild(i).GetComponent<Button>();
            if (templateButton != null)
                break;
        }

        if (templateButton == null)
        {
            Debug.LogError("[GatheringTabBarSetup] No combat tab button template found.", page);
            return;
        }

        for (int i = 0; i < GatheringSkillTypes.Length; i++)
        {
            SkillType skillType = GatheringSkillTypes[i];
            Transform existing = gatheringTabBar.Find(skillType.ToString());
            if (existing != null)
                continue;

            GameObject clone = Object.Instantiate(templateButton.gameObject, gatheringTabBar);
            clone.name = skillType.ToString();

            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = skillType.ToString();
        }

        gatheringTabBar.gameObject.SetActive(false);

        SerializedObject pageSo = new SerializedObject(page);
        pageSo.FindProperty("gatheringSkillsTabBarRoot").objectReferenceValue = gatheringTabBar;
        pageSo.FindProperty("combatSkillsTabBarRoot").objectReferenceValue = combatTabBar;
        pageSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(page);
        EditorUtility.SetDirty(gatheringTabBar.gameObject);
        EditorSceneManager.MarkSceneDirty(page.gameObject.scene);
        Debug.Log("[GatheringTabBarSetup] GatheringSkillsTabBar ready (Woodcutting, Mining, Fishing).", page);
    }
}
#endif
