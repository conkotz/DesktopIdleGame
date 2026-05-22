#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Creates milestone choice group prefab under Assets/2.Prefabs/UI/SkillsAbilityNew/.</summary>
public static class SkillChoiceGroupPrefabCreator
{
    private const string PrefabPath = "Assets/2.Prefabs/UI/SkillsAbilityNew/SkillChoiceGroupUI.prefab";
    private const string MilestonePrefabPath = "Assets/2.Prefabs/UI/SkillsAbilityNew/MilestoneGroupUI.prefab";

    [MenuItem("Assets/Create/Skills/Skill Choice Group UI Prefab")]
    public static void CreatePrefab() => SavePrefab("SkillChoiceGroupUI", typeof(SkillChoiceGroupUI), PrefabPath);

    [MenuItem("Assets/Create/Skills/Milestone Group UI Prefab")]
    public static void CreateMilestonePrefab() => SavePrefab("MilestoneGroupUI", typeof(MilestoneGroupUI), MilestonePrefabPath);

    private static void SavePrefab(string rootName, System.Type componentType, string path)
    {
        var root = new GameObject(rootName, typeof(RectTransform), componentType);
        ((SkillChoiceGroupUI)root.GetComponent(componentType)).EnsurePrefabHierarchy();

        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(280f, 120f);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Selection.activeObject = prefab;
        Debug.Log($"[SkillChoiceGroupPrefabCreator] Saved {path}");
    }
}
#endif
