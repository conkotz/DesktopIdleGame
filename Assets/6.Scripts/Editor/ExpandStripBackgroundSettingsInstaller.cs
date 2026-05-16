#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds the Expand background toggle row to Settings ▸ Misc (SettingsRightContent) in GamePlay.
/// </summary>
public static class ExpandStripBackgroundSettingsInstaller
{
    const string ToggleRowPrefabPath = "Assets/2.Prefabs/UI/SettingsToggleRow.prefab";
    const string SettingsContentName = "SettingsRightContent";
    const string InstalledRowName = "SettingsToggleRowExpandBackground";

    [MenuItem("Desktop Idle/Settings/Add Expand Background Toggle Row")]
    public static void AddExpandBackgroundToggleRow()
    {
        GameObject content = FindSettingsRightContent();
        if (!content)
        {
            Debug.LogError(
                $"[ExpandStripBackgroundSettingsInstaller] Could not find '{SettingsContentName}' in open scenes.");
            return;
        }

        if (content.transform.Find(InstalledRowName) != null)
        {
            Debug.Log($"[ExpandStripBackgroundSettingsInstaller] '{InstalledRowName}' already exists under {content.name}.");
            Selection.activeGameObject = content.transform.Find(InstalledRowName).gameObject;
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToggleRowPrefabPath);
        if (!prefab)
        {
            Debug.LogError($"[ExpandStripBackgroundSettingsInstaller] Missing prefab: {ToggleRowPrefabPath}");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, content.transform);
        instance.name = InstalledRowName;

        ToggleSettingsRowUI row = instance.GetComponent<ToggleSettingsRowUI>();
        if (row)
        {
            SerializedObject so = new SerializedObject(row);
            so.FindProperty("settingId").enumValueIndex = (int)ToggleSettingId.ExpandStripBackground;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Toggle toggle = instance.GetComponentInChildren<Toggle>(true);
        if (toggle)
        {
            SerializedObject toggleSo = new SerializedObject(toggle);
            toggleSo.FindProperty("m_IsOn").boolValue =
                ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground);
            toggleSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(content);
        if (content.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(content.scene);

        Selection.activeGameObject = instance;
        Debug.Log($"[ExpandStripBackgroundSettingsInstaller] Added '{InstalledRowName}' under {content.name}. Save the scene.");
    }

    static GameObject FindSettingsRightContent()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid())
            return null;

        GameObject[] roots = active.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == SettingsContentName)
                    return all[i].gameObject;
            }
        }

        return null;
    }
}
#endif
