#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Wires <see cref="DebugPerformanceToggles"/> onto WorldManager in the open scene.</summary>
public static class DebugPerformanceTogglesSceneSetup
{
    [MenuItem("Tools/Debug/Wire DebugPerformanceToggles on WorldManager")]
    public static void WireWorldManager()
    {
        GameObject worldManager = GameObject.Find("WorldManager");
        if (worldManager == null)
        {
            Debug.LogError("[DebugPerformanceTogglesSceneSetup] WorldManager not found in the active scene.");
            return;
        }

        DebugPerformanceToggles toggles = worldManager.GetComponent<DebugPerformanceToggles>();
        if (toggles == null)
            toggles = Undo.AddComponent<DebugPerformanceToggles>(worldManager);

        SerializedObject so = new SerializedObject(toggles);
        AssignIfEmpty(so, "actionBarUi", FindByName("ActionBarWindow"));
        AssignIfEmpty(so, "statsPanel", FindByName("RightPanel"));
        AssignIfEmpty(so, "activityLog", FindByName("GameActivityWindow"));
        AssignIfEmpty(so, "backgroundParallax", FindByName("BackgroundVisuals"));

        GameObject worldVisuals = FindByName("WorldVisuals");
        if (worldVisuals != null)
        {
            Transform floorVisuals = worldVisuals.transform.Find("FloorVisuals");
            AssignIfEmpty(so, "particlesParent", floorVisuals != null ? floorVisuals.gameObject : worldVisuals);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = worldManager;

        Debug.Log(
            "[DebugPerformanceTogglesSceneSetup] Wired DebugPerformanceToggles on WorldManager. " +
            "Overhead UI and floating combat text use runtime fallbacks unless you assign them manually.",
            worldManager);
    }

    private static void AssignIfEmpty(SerializedObject so, string propertyName, GameObject value)
    {
        if (value == null)
            return;

        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
            return;

        if (property.objectReferenceValue == null)
            property.objectReferenceValue = value;
    }

    private static GameObject FindByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t != null && t.name == objectName)
                return t.gameObject;
        }

        return null;
    }
}
#endif
