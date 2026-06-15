#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-view-only visibility toggles for strip gameplay editing.
/// Does not modify the scene file — uses <see cref="SceneVisibilityManager"/>.
/// </summary>
public static class GameplayLayoutEditorTools
{
    private const string StripUiCanvasName = "StripUICanvas";
    private const string FullWindowCanvasName = "FullWindowCanvas";
    private const string WorldVisualsName = "WorldVisuals";

    private const string PrefHideStripUi = "GameplayLayoutEditorTools.HideStripUICanvas";
    private const string PrefHideFullWindow = "GameplayLayoutEditorTools.HideFullWindowCanvas";
    private const string PrefHideWorldVisuals = "GameplayLayoutEditorTools.HideWorldVisuals";

    [InitializeOnLoadMethod]
    private static void ReapplyOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
                return;

            ApplySavedVisibility();
        };
    }

    [MenuItem("Tools/Layout/Toggle Hide Strip UI Canvas (Scene View)", false, 100)]
    public static void ToggleHideStripUiCanvas()
    {
        bool hide = !EditorPrefs.GetBool(PrefHideStripUi, false);
        EditorPrefs.SetBool(PrefHideStripUi, hide);
        SetHidden(FindSceneRoot(StripUiCanvasName), hide);
        LogState();
    }

    [MenuItem("Tools/Layout/Toggle Hide Strip UI Canvas (Scene View)", true)]
    public static bool ToggleHideStripUiCanvasValidate()
    {
        Menu.SetChecked("Tools/Layout/Toggle Hide Strip UI Canvas (Scene View)", EditorPrefs.GetBool(PrefHideStripUi, false));
        return true;
    }

    [MenuItem("Tools/Layout/Toggle Hide Full Window Canvas (Scene View)", false, 101)]
    public static void ToggleHideFullWindowCanvas()
    {
        bool hide = !EditorPrefs.GetBool(PrefHideFullWindow, false);
        EditorPrefs.SetBool(PrefHideFullWindow, hide);
        SetHidden(FindSceneRoot(FullWindowCanvasName), hide);
        LogState();
    }

    [MenuItem("Tools/Layout/Toggle Hide Full Window Canvas (Scene View)", true)]
    public static bool ToggleHideFullWindowCanvasValidate()
    {
        Menu.SetChecked("Tools/Layout/Toggle Hide Full Window Canvas (Scene View)", EditorPrefs.GetBool(PrefHideFullWindow, false));
        return true;
    }

    [MenuItem("Tools/Layout/Toggle Hide World Visuals Backdrop (Scene View)", false, 102)]
    public static void ToggleHideWorldVisuals()
    {
        bool hide = !EditorPrefs.GetBool(PrefHideWorldVisuals, false);
        EditorPrefs.SetBool(PrefHideWorldVisuals, hide);
        SetHidden(FindSceneRoot(WorldVisualsName), hide);
        LogState();
    }

    [MenuItem("Tools/Layout/Toggle Hide World Visuals Backdrop (Scene View)", true)]
    public static bool ToggleHideWorldVisualsValidate()
    {
        Menu.SetChecked("Tools/Layout/Toggle Hide World Visuals Backdrop (Scene View)", EditorPrefs.GetBool(PrefHideWorldVisuals, false));
        return true;
    }

    [MenuItem("Tools/Layout/Hide All Strip Editor Overlays (Scene View)", false, 120)]
    public static void HideAllOverlays()
    {
        EditorPrefs.SetBool(PrefHideStripUi, true);
        EditorPrefs.SetBool(PrefHideFullWindow, true);
        EditorPrefs.SetBool(PrefHideWorldVisuals, true);
        ApplySavedVisibility();
        LogState();
    }

    [MenuItem("Tools/Layout/Show All Strip Editor Overlays (Scene View)", false, 121)]
    public static void ShowAllOverlays()
    {
        EditorPrefs.SetBool(PrefHideStripUi, false);
        EditorPrefs.SetBool(PrefHideFullWindow, false);
        EditorPrefs.SetBool(PrefHideWorldVisuals, false);
        ApplySavedVisibility();
        LogState();
    }

    private static void ApplySavedVisibility()
    {
        SetHidden(FindSceneRoot(StripUiCanvasName), EditorPrefs.GetBool(PrefHideStripUi, false));
        SetHidden(FindSceneRoot(FullWindowCanvasName), EditorPrefs.GetBool(PrefHideFullWindow, false));
        SetHidden(FindSceneRoot(WorldVisualsName), EditorPrefs.GetBool(PrefHideWorldVisuals, false));
    }

    private static void SetHidden(GameObject go, bool hidden)
    {
        if (!go)
            return;

        SceneVisibilityManager.instance.Hide(go, hidden);
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
    }

    private static GameObject FindSceneRoot(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && root.name == objectName)
                return root;
        }

        return null;
    }

    private static void LogState()
    {
        bool strip = EditorPrefs.GetBool(PrefHideStripUi, false);
        bool full = EditorPrefs.GetBool(PrefHideFullWindow, false);
        bool world = EditorPrefs.GetBool(PrefHideWorldVisuals, false);

        Debug.Log(
            "[GameplayLayoutEditorTools] Scene visibility — " +
            $"StripUICanvas: {(strip ? "HIDDEN" : "visible")}, " +
            $"FullWindowCanvas: {(full ? "HIDDEN" : "visible")}, " +
            $"WorldVisuals: {(world ? "HIDDEN" : "visible")}. " +
            "Scene view only; Game view / Play Mode unchanged.");
    }
}
#endif
