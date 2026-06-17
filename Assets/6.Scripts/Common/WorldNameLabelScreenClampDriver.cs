using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single driver for all world <c>NameLabel</c> screen clamps. One layout-change hook, no per-label LateUpdate.
/// </summary>
public static class WorldNameLabelScreenClampDriver
{
    public struct StripViewState
    {
        public Camera Camera;
        public float OrthoSize;
        public float CameraY;
        public Rect PixelRect;
        public float MaxLabelTopScreenY;
    }

    private static readonly List<WorldNameLabelScreenClamp> s_labels = new(24);

    private static bool s_eventsHooked;
    private static bool s_levelHooked;
    private static float s_lastOrtho = float.NaN;
    private static float s_lastCameraY = float.NaN;
    private static Rect s_lastPixelRect;
    private static Coroutine s_bindRoutine;

    private sealed class Runner : MonoBehaviour { }

    private static Runner s_runner;

    public static void Register(WorldNameLabelScreenClamp label)
    {
        if (!label || s_labels.Contains(label))
            return;

        s_labels.Add(label);
        EnsureRunner();
        EnsureEventHooks();

        if (TryBuildViewState(out StripViewState view))
            label.UpdateForView(view, forceMeasure: true);
        else if (s_bindRoutine == null)
            ScheduleBindAndRefresh();
    }

    public static void Unregister(WorldNameLabelScreenClamp label)
    {
        if (!label)
            return;

        s_labels.Remove(label);
    }

    public static void RequestRefresh(WorldNameLabelScreenClamp label)
    {
        if (!label)
            return;

        if (TryBuildViewState(out StripViewState view))
            label.UpdateForView(view, forceMeasure: true);
    }

    private static void EnsureRunner()
    {
        if (s_runner)
            return;

        var go = new GameObject(nameof(WorldNameLabelScreenClampDriver));
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<Runner>();
    }

    private static void EnsureEventHooks()
    {
        if (s_eventsHooked)
            return;

        s_eventsHooked = true;
        StripCameraController.StripLayoutChanged += HandleLayoutChanged;
        GameplayScreenOverlayLayout.RegisterCoverageRefresh(HandleLayoutChanged);

        if (!s_levelHooked && GameplayLevelBootstrapper.Instance != null)
        {
            GameplayLevelBootstrapper.Instance.OnLevelStarted += HandleLevelStarted;
            s_levelHooked = true;
        }
        else if (!s_levelHooked && s_runner)
        {
            s_runner.StartCoroutine(CoWaitForBootstrapper());
        }
    }

    private static IEnumerator CoWaitForBootstrapper()
    {
        const int maxFrames = 120;
        for (int i = 0; i < maxFrames; i++)
        {
            if (GameplayLevelBootstrapper.Instance != null)
            {
                GameplayLevelBootstrapper.Instance.OnLevelStarted += HandleLevelStarted;
                s_levelHooked = true;
                yield break;
            }

            yield return null;
        }
    }

    private static void HandleLevelStarted(MapNodeDefinition _)
    {
        s_lastOrtho = float.NaN;
        if (s_runner)
            s_runner.StartCoroutine(CoRefreshAfterSpawn());
    }

    private static IEnumerator CoRefreshAfterSpawn()
    {
        yield return null;
        yield return null;
        HandleLayoutChanged();
    }

    private static void ScheduleBindAndRefresh()
    {
        if (!s_runner)
            return;

        if (s_bindRoutine != null)
            s_runner.StopCoroutine(s_bindRoutine);

        s_bindRoutine = s_runner.StartCoroutine(CoBindThenRefresh());
    }

    private static IEnumerator CoBindThenRefresh()
    {
        const int maxFrames = 180;
        for (int i = 0; i < maxFrames; i++)
        {
            if (TryBuildViewState(out StripViewState view))
            {
                ApplyAllForView(view);
                s_bindRoutine = null;
                yield break;
            }

            yield return null;
        }

        s_bindRoutine = null;
    }

    private static void HandleLayoutChanged()
    {
        if (!TryBuildViewState(out StripViewState view))
            return;

        bool viewChanged = HasViewChanged(view);
        if (!viewChanged && !AnyLabelNeedsUpdate(view))
            return;

        if (viewChanged)
        {
            s_lastOrtho = view.OrthoSize;
            s_lastCameraY = view.CameraY;
            s_lastPixelRect = view.PixelRect;
        }

        ApplyAllForView(view);
    }

    private static bool AnyLabelNeedsUpdate(StripViewState view)
    {
        for (int i = s_labels.Count - 1; i >= 0; i--)
        {
            WorldNameLabelScreenClamp label = s_labels[i];
            if (!label)
            {
                s_labels.RemoveAt(i);
                continue;
            }

            if (label.NeedsUpdateForView(view))
                return true;
        }

        return false;
    }

    private static void ApplyAllForView(StripViewState view)
    {
        bool anyNeedsUpdate = false;
        for (int i = s_labels.Count - 1; i >= 0; i--)
        {
            WorldNameLabelScreenClamp label = s_labels[i];
            if (!label)
            {
                s_labels.RemoveAt(i);
                continue;
            }

            if (label.NeedsUpdateForView(view))
                anyNeedsUpdate = true;
        }

        if (!anyNeedsUpdate)
            return;

        for (int i = s_labels.Count - 1; i >= 0; i--)
        {
            WorldNameLabelScreenClamp label = s_labels[i];
            if (!label)
                continue;

            if (label.NeedsUpdateForView(view))
                label.UpdateForView(view, forceMeasure: false);
            else
                label.ReleaseToRest();
        }
    }

    private static bool HasViewChanged(StripViewState view)
    {
        return float.IsNaN(s_lastOrtho)
            || !Mathf.Approximately(view.OrthoSize, s_lastOrtho)
            || view.PixelRect != s_lastPixelRect
            || !Mathf.Approximately(view.CameraY, s_lastCameraY);
    }

    private static bool TryBuildViewState(out StripViewState view)
    {
        view = default;
        Camera cam = GameplayScreenOverlayLayout.TryResolveStripCamera();
        if (!cam || !cam.isActiveAndEnabled)
            return false;

        Rect pr = cam.pixelRect;
        if (pr.width <= 1f || pr.height <= 1f)
            return false;

        view.Camera = cam;
        view.OrthoSize = cam.orthographicSize;
        view.CameraY = cam.transform.position.y;
        view.PixelRect = pr;
        view.MaxLabelTopScreenY = pr.yMax;
        return true;
    }
}
