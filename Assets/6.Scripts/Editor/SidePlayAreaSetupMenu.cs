#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SidePlayAreaSetupMenu
{
    private const string SideAreaRootName = "SideArea_1";
    private const string LegacyRootName = "SpawnPoints_LeftArea";
    private const string AreaId = "SideArea_1";
    private const string SpawnGroupId = "CombatPractice";
    private const string FloorChildName = "SideAreaFloor";
    private const string SpawnNamePrefix = "SideArea_Spawn";
    private const float SpawnY = -2.927f;
    private const int SpawnPointCount = 30;
    private const float SpawnSpacing = 1f;
    private const float SpawnCenterX = -60f;

    [MenuItem("Tools/Gameplay/Setup Side Area 1 Combat Practice")]
    public static void SetupSideArea1CombatPractice()
    {
        GameObject root = GameObject.Find(SideAreaRootName) ?? GameObject.Find(LegacyRootName);
        if (!root)
        {
            Debug.LogError(
                $"[SidePlayArea] Could not find '{SideAreaRootName}' or legacy '{LegacyRootName}' in the open scene.");
            return;
        }

        if (root.name != SideAreaRootName)
            root.name = SideAreaRootName;

        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Side Area 1 Combat Practice");

        SidePlayArea sideArea = GetOrAdd<SidePlayArea>(root);
        SerializedObject sideAreaSo = new SerializedObject(sideArea);
        sideAreaSo.FindProperty("areaId").stringValue = AreaId;
        sideAreaSo.FindProperty("linkedSpawnGroupId").stringValue = SpawnGroupId;
        sideAreaSo.ApplyModifiedPropertiesWithoutUndo();

        SpawnPointGroup spawnGroup = GetOrAdd<SpawnPointGroup>(root);
        SerializedObject groupSo = new SerializedObject(spawnGroup);
        groupSo.FindProperty("groupId").stringValue = SpawnGroupId;
        groupSo.ApplyModifiedPropertiesWithoutUndo();

        float halfSpan = (SpawnPointCount - 1) * SpawnSpacing * 0.5f;
        float startX = SpawnCenterX - halfSpan;
        float floorWidth = Mathf.Max(16f, (SpawnPointCount - 1) * SpawnSpacing + 2f);

        Transform floor = EnsureChild(root.transform, FloorChildName);
        BoxCollider2D floorCollider = GetOrAdd<BoxCollider2D>(floor.gameObject);
        floorCollider.isTrigger = true;
        floorCollider.size = new Vector2(floorWidth, 0.25f);
        floor.localPosition = new Vector3(SpawnCenterX, -3.12f, 0f);

        SerializedObject sideAreaSo2 = new SerializedObject(sideArea);
        sideAreaSo2.FindProperty("boundsCollider").objectReferenceValue = floorCollider;
        sideAreaSo2.ApplyModifiedPropertiesWithoutUndo();

        PruneExtraSpawnPointChildren(root.transform, floor);

        var spawnPoints = new List<Transform>(SpawnPointCount);
        for (int i = 0; i < SpawnPointCount; i++)
        {
            string pointName = $"{SpawnNamePrefix}{i + 1}";
            Transform point = EnsureChild(root.transform, pointName);
            point.localPosition = new Vector3(startX + i * SpawnSpacing, SpawnY, 0f);
            spawnPoints.Add(point);
        }

        spawnPoints.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));

        SerializedProperty pointsProp = groupSo.FindProperty("points");
        pointsProp.ClearArray();
        pointsProp.arraySize = spawnPoints.Count;
        for (int i = 0; i < spawnPoints.Count; i++)
            pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];

        groupSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EnsureSidePlayAreaDirector();

        Debug.Log(
            $"[SidePlayArea] Configured '{SideAreaRootName}' with {SpawnPointCount} spawn points " +
            $"(x {startX:0.##} → {startX + (SpawnPointCount - 1) * SpawnSpacing:0.##}, {SpawnSpacing}u spacing).");
    }

    [MenuItem("Tools/Gameplay/Setup Left Area Combat Practice")]
    public static void SetupLeftAreaCombatPractice() => SetupSideArea1CombatPractice();

    private static void PruneExtraSpawnPointChildren(Transform root, Transform floor)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (!child || child == floor)
                continue;

            if (!child.name.StartsWith(SpawnNamePrefix, System.StringComparison.Ordinal) &&
                !child.name.StartsWith("LeftArea_", System.StringComparison.Ordinal))
            {
                continue;
            }

            string suffix = child.name.StartsWith(SpawnNamePrefix, System.StringComparison.Ordinal)
                ? child.name.Substring(SpawnNamePrefix.Length)
                : child.name.Substring("LeftArea_".Length);

            if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ||
                index < 1 || index > SpawnPointCount)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static void EnsureSidePlayAreaDirector()
    {
        SidePlayAreaDirector existing = Object.FindFirstObjectByType<SidePlayAreaDirector>(FindObjectsInactive.Include);
        if (existing)
            return;

        GameObject host = GameObject.Find("LevelSpawnManager") ?? new GameObject("SidePlayAreaDirector");
        if (!host.TryGetComponent(out SidePlayAreaDirector _))
            Undo.AddComponent<SidePlayAreaDirector>(host);
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child && child.name == childName)
                return child;
        }

        var go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, "Create Side Area Child");
        Transform t = go.transform;
        t.SetParent(parent, false);
        return t;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        if (!go.TryGetComponent(out T component))
            component = Undo.AddComponent<T>(go);
        return component;
    }
}
#endif
