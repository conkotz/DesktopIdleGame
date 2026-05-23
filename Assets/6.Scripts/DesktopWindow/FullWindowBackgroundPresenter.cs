using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// When <see cref="ToggleSettingId.ExpandStripBackground"/> is on, the strip uses the scene
/// <see cref="FullSkyVisualName"/> with clouds hidden; <see cref="FullCamera"/> draws a clone with clouds only in
/// the viewport above the strip. Collapsed: full-window clear with alpha 0 for desktop transparency (UniWindow Alpha mode).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class FullWindowBackgroundPresenter : MonoBehaviour
{
    public const string BackgroundLayerName = "WindowBackground";
    private const string BackgroundVisualsName = "BackgroundVisuals";
    private const string FullSkyVisualName = "FullSkyVisual";
    private const string CloudsBackName = "CloudsBack";
    private const string CloudsFrontName = "CloudsFront";
    private const string FullSkyVisualCloneName = "FullSkyVisual_FullWindow";
    private const int BackgroundLayerFallbackIndex = 11;

    /// <summary>Clear color for empty framebuffer pixels. Alpha must be 0 for UniWindow Alpha transparency.</summary>
    private static readonly Color TransparentDesktopClearColor = new Color(0f, 0f, 0f, 0f);

    [SerializeField] private Camera fullCamera;
    [SerializeField] private Camera stripCamera;
    [SerializeField] private StripCameraController stripController;

    private int _backgroundLayer = -1;
    private int _backgroundLayerMask;
    private int _stripMaskWithBackground = -1;
    private int _stripMaskWithoutBackground = -1;
    private Transform _backgroundRoot;
    private Transform _fullWindowSkyClone;
    private Transform _cloneSourceSkyVisual;
    private bool _expanded;
    private bool _stripCloudVisibilityCached;
    private bool _stripCloudsBackWasActive;
    private bool _stripCloudsFrontWasActive;
    private UniversalAdditionalCameraData _fullUrp;
    private UniversalAdditionalCameraData _stripUrp;
    private Coroutine _urpStackRoutine;
    private Coroutine _expandSideEffectsRoutine;
    private bool _pendingUrpStackConfig;
    private bool _pendingUrpStackExpanded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePresenterExists()
    {
        if (FindFirstObjectByType<FullWindowBackgroundPresenter>(FindObjectsInactive.Include) != null)
            return;

        Camera fullCam = FindCameraByName("FullCamera");
        if (!fullCam)
            return;

        if (!fullCam.GetComponent<FullWindowBackgroundPresenter>())
            fullCam.gameObject.AddComponent<FullWindowBackgroundPresenter>();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheLayerMasks();
        EnsureUrpCameraData();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheLayerMasks();
        EnsureUrpCameraData();
        ToggleSettingsStore.Changed += HandleToggleChanged;
        ApplyExpandState(ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground));
        FlushPendingDeferredWork();
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= HandleToggleChanged;
        if (_urpStackRoutine != null)
        {
            StopCoroutine(_urpStackRoutine);
            _urpStackRoutine = null;
        }

        if (_expandSideEffectsRoutine != null)
        {
            StopCoroutine(_expandSideEffectsRoutine);
            _expandSideEffectsRoutine = null;
        }

        ApplyExpandState(false, updateStripLayoutLock: false);
        StripCameraController.SyncStripLockToExpandBackgroundSetting();
    }

    private void LateUpdate()
    {
        if (!_expanded || !fullCamera || !stripCamera)
            return;

        ApplyFullCameraViewportAboveStrip();
        SyncFullCameraToStrip();
        SyncFullWindowSkyCloneFromSource();
    }

    private void OnDestroy()
    {
        DestroyFullWindowSkyClone();
        ApplyStripSceneCloudVisibility(false);
    }

    private void HandleToggleChanged(ToggleSettingId id, bool value)
    {
        if (id != ToggleSettingId.ExpandStripBackground)
            return;

        ApplyExpandState(value);
    }

    public static bool IsExpandBackgroundEnabled =>
        ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground);

    public static void RefreshAllFromSettings()
    {
        bool expanded = IsExpandBackgroundEnabled;

        StripCameraController.SyncStripLockToExpandBackgroundSetting();

        FullWindowBackgroundPresenter[] list = FindObjectsByType<FullWindowBackgroundPresenter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (list.Length == 0)
        {
            GameplayScreenOverlayLayout.RefreshAllRegistered();
            return;
        }

        for (int i = 0; i < list.Length; i++)
        {
            if (list[i])
                list[i].ApplyExpandState(expanded);
        }
    }

    private void ApplyExpandState(bool expanded, bool updateStripLayoutLock = true)
    {
        ResolveReferences();
        CacheLayerMasks();
        EnsureUrpCameraData();

        if (_backgroundLayer < 0)
        {
            Debug.LogWarning(
                $"[FullWindowBackgroundPresenter] Layer '{BackgroundLayerName}' is missing. " +
                "Add it in Tags & Layers (index 11).");
            return;
        }

        _expanded = expanded;

        if (expanded)
            EnsureFullWindowSkyClone();
        else
            DestroyFullWindowSkyClone();

        ApplyStripSceneCloudVisibility(expanded);

        if (fullCamera)
        {
            fullCamera.enabled = true;
            fullCamera.clearFlags = CameraClearFlags.SolidColor;
            fullCamera.backgroundColor = TransparentDesktopClearColor;
            fullCamera.cullingMask = expanded ? _backgroundLayerMask : 0;
            fullCamera.depth = -50;

            if (expanded)
            {
                ApplyFullCameraViewportAboveStrip();
                SyncFullCameraToStrip();
            }
            else
                fullCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }

        if (stripCamera)
            stripCamera.cullingMask = expanded ? _stripMaskWithoutBackground : _stripMaskWithBackground;

        ScheduleUrpCameraStackConfiguration(expanded);

        if (updateStripLayoutLock)
            StripCameraController.SetExpandBackgroundStripLayoutLocked(expanded);

        ScheduleExpandSideEffects();
    }

    private void ScheduleUrpCameraStackConfiguration(bool expanded)
    {
        if (!Application.isPlaying)
        {
            ConfigureUrpCameraStack(expanded);
            return;
        }

        if (!isActiveAndEnabled)
        {
            _pendingUrpStackConfig = true;
            _pendingUrpStackExpanded = expanded;
            return;
        }

        if (_urpStackRoutine != null)
            StopCoroutine(_urpStackRoutine);

        _urpStackRoutine = StartCoroutine(CoConfigureUrpCameraStackNextFrame(expanded));
    }

    private void FlushPendingDeferredWork()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        if (_pendingUrpStackConfig)
        {
            bool expanded = _pendingUrpStackExpanded;
            _pendingUrpStackConfig = false;
            ScheduleUrpCameraStackConfiguration(expanded);
        }
    }

    private IEnumerator CoConfigureUrpCameraStackNextFrame(bool expanded)
    {
        yield return null;
        _urpStackRoutine = null;

        if (!this || !isActiveAndEnabled)
            yield break;

        EnsureUrpCameraData();
        ConfigureUrpCameraStack(expanded);
    }

    private void ScheduleExpandSideEffects()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            GameplayScreenOverlayLayout.RefreshAllRegistered();
            return;
        }

        if (_expandSideEffectsRoutine != null)
            StopCoroutine(_expandSideEffectsRoutine);

        _expandSideEffectsRoutine = StartCoroutine(CoExpandSideEffectsNextFrame());
    }

    private IEnumerator CoExpandSideEffectsNextFrame()
    {
        yield return null;
        _expandSideEffectsRoutine = null;

        if (!this)
            yield break;

        GameplayScreenOverlayLayout.RefreshAllRegistered();
    }

    private void ConfigureUrpCameraStack(bool expanded)
    {
        if (!fullCamera || !stripCamera)
            return;

        EnsureUrpCameraData();
        if (_fullUrp == null || _stripUrp == null)
            return;

        if (expanded)
        {
            ClearCameraStackIfBase(_fullUrp);
            ClearCameraStackIfBase(_stripUrp);

            _fullUrp.renderType = CameraRenderType.Base;
            _stripUrp.renderType = CameraRenderType.Overlay;

            if (TryGetBaseCameraStack(_fullUrp, out List<Camera> stack) && !stack.Contains(stripCamera))
                stack.Add(stripCamera);
        }
        else
        {
            RemoveFromCameraStackIfBase(_fullUrp, stripCamera);
            ClearCameraStackIfBase(_fullUrp);

            _fullUrp.renderType = CameraRenderType.Base;
            _stripUrp.renderType = CameraRenderType.Base;
        }
    }

    private static bool TryGetBaseCameraStack(UniversalAdditionalCameraData data, out List<Camera> stack)
    {
        stack = null;
        if (!data || data.renderType != CameraRenderType.Base)
            return false;

        Camera cam = data.GetComponent<Camera>();
        if (!cam)
            return false;

        try
        {
            stack = data.cameraStack;
        }
        catch
        {
            return false;
        }

        return stack != null;
    }

    private static void ClearCameraStackIfBase(UniversalAdditionalCameraData data)
    {
        if (!TryGetBaseCameraStack(data, out List<Camera> stack))
            return;

        stack.Clear();
    }

    private static void RemoveFromCameraStackIfBase(UniversalAdditionalCameraData data, Camera cam)
    {
        if (!cam || !TryGetBaseCameraStack(data, out List<Camera> stack))
            return;

        stack.Remove(cam);
    }

    private void ApplyFullCameraViewportAboveStrip()
    {
        if (!fullCamera || !stripCamera)
            return;

        Rect stripRect = stripCamera.rect;
        float topY = stripRect.y + stripRect.height;
        float topHeight = 1f - topY;
        if (topHeight > 0.001f)
            fullCamera.rect = new Rect(stripRect.x, topY, stripRect.width, topHeight);
    }

    private void SyncFullCameraToStrip()
    {
        if (!fullCamera || !stripCamera || !stripController)
            return;

        Transform stripTf = stripCamera.transform;
        fullCamera.transform.SetPositionAndRotation(stripTf.position, stripTf.rotation);

        if (!fullCamera.orthographic || !stripCamera.orthographic)
            return;

        float stripViewportHeight = Mathf.Max(0.1f, stripController.StripHeightPercent);
        float stripOrtho = Mathf.Max(0.01f, stripCamera.orthographicSize);
        fullCamera.orthographicSize = stripOrtho / stripViewportHeight;
    }

    private void ApplyStripSceneCloudVisibility(bool hideCloudsOnStrip)
    {
        if (!TryGetStripSceneFullSky(out Transform stripFullSky))
            return;

        Transform cloudsBack = FindChildRecursive(stripFullSky, CloudsBackName);
        Transform cloudsFront = FindChildRecursive(stripFullSky, CloudsFrontName);

        if (hideCloudsOnStrip)
        {
            if (!_stripCloudVisibilityCached)
            {
                _stripCloudsBackWasActive = cloudsBack && cloudsBack.gameObject.activeSelf;
                _stripCloudsFrontWasActive = cloudsFront && cloudsFront.gameObject.activeSelf;
                _stripCloudVisibilityCached = true;
            }

            if (cloudsBack)
                cloudsBack.gameObject.SetActive(false);
            if (cloudsFront)
                cloudsFront.gameObject.SetActive(false);
            return;
        }

        if (!_stripCloudVisibilityCached)
            return;

        if (cloudsBack)
            cloudsBack.gameObject.SetActive(_stripCloudsBackWasActive);
        if (cloudsFront)
            cloudsFront.gameObject.SetActive(_stripCloudsFrontWasActive);

        _stripCloudVisibilityCached = false;
    }

    private void EnsureFullWindowSkyClone()
    {
        if (!TryGetActiveSkyVisualRoot(out Transform source))
        {
            DestroyFullWindowSkyClone();
            return;
        }

        if (_fullWindowSkyClone != null &&
            (!_fullWindowSkyClone.gameObject.scene.IsValid() || _cloneSourceSkyVisual != source))
        {
            DestroyFullWindowSkyClone();
        }

        if (_fullWindowSkyClone == null)
        {
            if (!_stripCloudVisibilityCached)
            {
                Transform cloudsBack = FindChildRecursive(source, CloudsBackName);
                Transform cloudsFront = FindChildRecursive(source, CloudsFrontName);
                _stripCloudsBackWasActive = cloudsBack && cloudsBack.gameObject.activeSelf;
                _stripCloudsFrontWasActive = cloudsFront && cloudsFront.gameObject.activeSelf;
            }

            GameObject cloneObject = Instantiate(source.gameObject, source.parent);
            cloneObject.name = FullSkyVisualCloneName;
            _fullWindowSkyClone = cloneObject.transform;
            _cloneSourceSkyVisual = source;
            SetLayerRecursive(_fullWindowSkyClone, _backgroundLayer);
            ApplyFullWindowCloneCloudVisibility();
        }

        SyncFullWindowSkyCloneFromSource();
    }

    private void ApplyFullWindowCloneCloudVisibility()
    {
        if (!_fullWindowSkyClone)
            return;

        Transform cloudsBack = FindChildRecursive(_fullWindowSkyClone, CloudsBackName);
        Transform cloudsFront = FindChildRecursive(_fullWindowSkyClone, CloudsFrontName);

        if (cloudsBack)
            cloudsBack.gameObject.SetActive(_stripCloudsBackWasActive);
        if (cloudsFront)
            cloudsFront.gameObject.SetActive(_stripCloudsFrontWasActive);
    }

    private void SyncFullWindowSkyCloneFromSource()
    {
        if (!TryGetActiveSkyVisualRoot(out Transform source))
        {
            DestroyFullWindowSkyClone();
            return;
        }

        if (_cloneSourceSkyVisual != source)
        {
            DestroyFullWindowSkyClone();
            EnsureFullWindowSkyClone();
            return;
        }

        if (!_fullWindowSkyClone)
            return;

        _fullWindowSkyClone.SetPositionAndRotation(source.position, source.rotation);
        _fullWindowSkyClone.localScale = source.localScale;
        _fullWindowSkyClone.gameObject.SetActive(source.gameObject.activeInHierarchy);
        ApplyFullWindowCloneCloudVisibility();
    }

    private void DestroyFullWindowSkyClone()
    {
        if (!_fullWindowSkyClone)
        {
            _cloneSourceSkyVisual = null;
            return;
        }

        if (Application.isPlaying)
            Destroy(_fullWindowSkyClone.gameObject);
        else
            DestroyImmediate(_fullWindowSkyClone.gameObject);

        _fullWindowSkyClone = null;
        _cloneSourceSkyVisual = null;
    }

    private bool TryGetStripSceneFullSky(out Transform stripFullSky)
    {
        stripFullSky = null;
        if (!_backgroundRoot)
            return false;

        stripFullSky = FindChildRecursive(_backgroundRoot, FullSkyVisualName);
        return stripFullSky != null;
    }

    private bool TryGetActiveSkyVisualRoot(out Transform skyVisualRoot)
    {
        skyVisualRoot = null;
        if (!_backgroundRoot)
            return false;

        for (int i = 0; i < _backgroundRoot.childCount; i++)
        {
            Transform biomeRoot = _backgroundRoot.GetChild(i);
            if (!biomeRoot.gameObject.activeInHierarchy)
                continue;

            Transform fullSky = FindChildRecursive(biomeRoot, FullSkyVisualName);
            skyVisualRoot = fullSky != null ? fullSky : biomeRoot;
            return true;
        }

        return false;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (!root)
            return null;

        if (string.Equals(root.name, childName, System.StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found)
                return found;
        }

        return null;
    }

    private void EnsureUrpCameraData()
    {
        if (fullCamera)
        {
            _fullUrp = fullCamera.GetComponent<UniversalAdditionalCameraData>();
            if (_fullUrp == null)
                _fullUrp = fullCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        if (stripCamera)
        {
            _stripUrp = stripCamera.GetComponent<UniversalAdditionalCameraData>();
            if (_stripUrp == null)
                _stripUrp = stripCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }
    }

    private void ResolveReferences()
    {
        if (!fullCamera)
            fullCamera = GetComponent<Camera>();

        if (!stripCamera || !stripController)
        {
            StripCameraController ctrl =
                FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Include);
            if (ctrl)
            {
                stripController = ctrl;
                stripCamera = ctrl.GetComponent<Camera>();
            }
        }

        if (!_backgroundRoot)
            _backgroundRoot = FindBackgroundVisualsRoot();
    }

    private void CacheLayerMasks()
    {
        _backgroundLayer = LayerMask.NameToLayer(BackgroundLayerName);
        if (_backgroundLayer < 0)
            _backgroundLayer = BackgroundLayerFallbackIndex;

        if (_backgroundLayer < 0 || _backgroundLayer >= 32)
            return;

        _backgroundLayerMask = 1 << _backgroundLayer;

        if (stripCamera)
        {
            _stripMaskWithBackground = stripCamera.cullingMask;
            if (_stripMaskWithBackground == 0)
                _stripMaskWithBackground = ~0;

            _stripMaskWithoutBackground = _stripMaskWithBackground & ~_backgroundLayerMask;
        }
    }

    private static Camera FindCameraByName(string cameraName)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam && string.Equals(cam.gameObject.name, cameraName, System.StringComparison.Ordinal))
                return cam;
        }

        return null;
    }

    private static Transform FindBackgroundVisualsRoot()
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        Transform fallback = null;

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (!string.Equals(t.name, BackgroundVisualsName, System.StringComparison.Ordinal))
                continue;

            fallback = t;
            if (t.parent != null &&
                string.Equals(t.parent.name, "WorldVisuals", System.StringComparison.Ordinal))
                return t;
        }

        return fallback;
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (!root)
            return;

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }
}
