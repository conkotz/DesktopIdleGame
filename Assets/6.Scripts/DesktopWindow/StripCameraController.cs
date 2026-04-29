using System;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-200)]
public sealed class StripCameraController : MonoBehaviour
{
    [SerializeField] private Camera stripCamera;

    [Header("Viewport")]
    [Range(0.1f, 1f)]
    public float stripHeightPercent = 0.3f;

    [Tooltip("Bottom of the strip in normalized screen space. 0 = bottom, 1 = top.")]
    [Range(0f, 1f)]
    public float bottomNormalized = 0f;

    [Tooltip("Left edge of the strip in normalized screen space.")]
    [Range(0f, 1f)]
    public float leftNormalized = 0f;

    [Tooltip("Width of the strip in normalized screen space.")]
    [Range(0.1f, 1f)]
    public float widthNormalized = 1f;

    [Header("Orthographic Scaling")]
    [Min(0.01f)]
    public float baseOrthoSize = 4f;

    [Tooltip("Minimum orthographic half-height while playing (furthest zoom in).")]
    [SerializeField] private float minOrthoSize = 2.25f;

    [Tooltip("Fallback max ortho half-height only when lane WorldBounds are unavailable (e.g. loading). While playing with WorldBounds, max zoom-out is computed from lane width.")]
    [SerializeField, FormerlySerializedAs("maxOrthoSize")] private float maxOrthoSizeFallback = 9f;

    [Header("Keyboard zoom")]
    [Tooltip("Arrow Up zooms in; Arrow Down zooms out. Holds repeat every frame — use Zoom Speed × deltaTime while key is held.")]
    [SerializeField] private bool enableKeyboardZoom = true;

    [Tooltip("Ortho half-height change per second while Up/Down is held (world units/s).")]
    [SerializeField] private float orthoZoomSpeed = 5f;

    [Header("Behaviour")]
    public bool updateContinuously = false;

    [Header("Persistence")]
    [Tooltip("Save strip viewport (size, screen position, zoom) and restore after level loads / next play session.")]
    [SerializeField] private bool persistStripLayout = true;

    private const string StripLayoutPrefsKey = "DesktopStripLayout.v1";
    private const float StripPrefsWriteMinInterval = 0.12f;
    private static float _nextAllowStripPrefsWriteTime = -999f;

    [Serializable]
    private struct SavedStripLayout
    {
        public float stripHeightPercent;
        public float bottomNormalized;
        public float leftNormalized;
        public float widthNormalized;
        public float baseOrthoSize;
    }

    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private float _lastStripHeightPercent = float.NaN;
    private float _lastBottomNormalized = float.NaN;
    private float _lastLeftNormalized = float.NaN;
    private float _lastWidthNormalized = float.NaN;
    private float _lastBaseOrthoSize = float.NaN;

    public float StripHeightPercent => Mathf.Clamp(stripHeightPercent, 0.1f, 1f);
    public float BottomNormalized => bottomNormalized;
    public float LeftNormalized => leftNormalized;
    public float WidthNormalized => Mathf.Clamp(widthNormalized, 0.1f, 1f);

    private void OnEnable()
    {
        CacheCamera();
        if (Application.isPlaying && persistStripLayout)
            TryLoadSavedLayoutQuiet();
        Apply(force: true);
    }

    private void Start()
    {
        Apply(force: true);
    }

    private void OnApplicationQuit()
    {
        if (Application.isPlaying && persistStripLayout)
            SaveLayoutToPrefs(forceImmediate: true);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && persistStripLayout)
            SaveLayoutToPrefs(forceImmediate: true);
    }

    private void OnValidate()
    {
        CacheCamera();
        ClampInspectorValues();
        Apply(force: true);
    }

    private void Update()
    {
        if (Application.isPlaying)
            ClampInspectorValues();

        if (Application.isPlaying && enableKeyboardZoom)
            ApplyKeyboardOrthoZoom();

        if (updateContinuously || HasChanged())
            Apply(force: false);
    }

    /// <summary>Hold Up = zoom in (− ortho half-height); Hold Down = zoom out (+).</summary>
    private void ApplyKeyboardOrthoZoom()
    {
        CacheCamera();

        if (!stripCamera || !stripCamera.orthographic)
            return;

        float change = orthoZoomSpeed * Time.deltaTime;
        int zoomInput = 0;
        if (Input.GetKey(KeyCode.DownArrow)) zoomInput++;
        if (Input.GetKey(KeyCode.UpArrow)) zoomInput--;
        if (zoomInput == 0)
            return;

        baseOrthoSize += change * zoomInput;

        ClampInspectorValues();

        Apply(force: true);
    }

    /// <summary>
    /// Max zoom-out = ortho half-height such that visible world width matches lane width
    /// (<c>2 × ortho × aspect</c> = lane width from <see cref="WorldBounds"/>).
    /// </summary>
    private float GetEffectiveMaxOrthoSize()
    {
        if (!Application.isPlaying)
            return maxOrthoSizeFallback;

        CacheCamera();

        if (!stripCamera || !stripCamera.orthographic || !stripCamera.isActiveAndEnabled)
            return Mathf.Max(minOrthoSize, maxOrthoSizeFallback);

        WorldBounds wb = WorldBounds.Instance;
        if (wb == null)
            return Mathf.Max(minOrthoSize, maxOrthoSizeFallback);

        float laneW = wb.Right - wb.Left;
        if (laneW <= 1e-4f)
            return Mathf.Max(minOrthoSize, maxOrthoSizeFallback);

        float aspect = Mathf.Max(0.001f, stripCamera.aspect);
        return Mathf.Max(minOrthoSize, laneW / (2f * aspect));
    }

    public void SetBottomNormalized(float value)
    {
        bottomNormalized = value;
        Apply(force: true);
    }

    public void SetLeftNormalized(float value)
    {
        leftNormalized = value;
        Apply(force: true);
    }

    public void SetWidthNormalized(float value)
    {
        widthNormalized = value;
        Apply(force: true);
    }

    public void SetStripHeightPercent(float value)
    {
        stripHeightPercent = value;
        Apply(force: true);
    }

    private void CacheCamera()
    {
        if (!stripCamera)
            stripCamera = GetComponent<Camera>();
    }

    private bool HasChanged()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            return true;

        return !Mathf.Approximately(stripHeightPercent, _lastStripHeightPercent) ||
               !Mathf.Approximately(bottomNormalized, _lastBottomNormalized) ||
               !Mathf.Approximately(leftNormalized, _lastLeftNormalized) ||
               !Mathf.Approximately(widthNormalized, _lastWidthNormalized) ||
               !Mathf.Approximately(baseOrthoSize, _lastBaseOrthoSize);
    }

    private void Apply(bool force)
    {
        CacheCamera();

        if (!stripCamera)
            return;

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        if (!force && !HasChanged())
            return;

        ClampInspectorValues();

        float height = StripHeightPercent;
        float width = WidthNormalized;
        float x = Mathf.Clamp(leftNormalized, 0f, 1f - width);
        float y = Mathf.Clamp(bottomNormalized, 0f, 1f - height);

        stripCamera.rect = new Rect(x, y, width, height);

        if (stripCamera.orthographic)
            stripCamera.orthographicSize = baseOrthoSize;

        RememberCurrentState();
    }

    private void TryLoadSavedLayoutQuiet()
    {
        if (!PlayerPrefs.HasKey(StripLayoutPrefsKey))
            return;

        try
        {
            string json = PlayerPrefs.GetString(StripLayoutPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            SavedStripLayout s = JsonUtility.FromJson<SavedStripLayout>(json);
            stripHeightPercent = s.stripHeightPercent;
            bottomNormalized = s.bottomNormalized;
            leftNormalized = s.leftNormalized;
            widthNormalized = s.widthNormalized;
            baseOrthoSize = Mathf.Max(0.01f, s.baseOrthoSize);
        }
        catch (Exception)
        {
            // Corrupt or incompatible saved data — keep scene defaults.
        }
    }

    private void SaveLayoutToPrefs(bool forceImmediate)
    {
        if (!forceImmediate && Time.unscaledTime < _nextAllowStripPrefsWriteTime)
            return;

        _nextAllowStripPrefsWriteTime = Time.unscaledTime + StripPrefsWriteMinInterval;

        var s = new SavedStripLayout
        {
            stripHeightPercent = stripHeightPercent,
            bottomNormalized = bottomNormalized,
            leftNormalized = leftNormalized,
            widthNormalized = widthNormalized,
            baseOrthoSize = baseOrthoSize
        };

        PlayerPrefs.SetString(StripLayoutPrefsKey, JsonUtility.ToJson(s));
    }

    private void ClampInspectorValues()
    {
        stripHeightPercent = Mathf.Clamp(stripHeightPercent, 0.1f, 1f);
        bottomNormalized = Mathf.Clamp01(bottomNormalized);
        leftNormalized = Mathf.Clamp01(leftNormalized);
        widthNormalized = Mathf.Clamp(widthNormalized, 0.1f, 1f);

        minOrthoSize = Mathf.Max(0.01f, minOrthoSize);

        float hi = GetEffectiveMaxOrthoSize();

        baseOrthoSize = Mathf.Clamp(Mathf.Max(0.01f, baseOrthoSize), minOrthoSize, hi);
    }

    private void RememberCurrentState()
    {
        bool layoutChanged =
            !Mathf.Approximately(stripHeightPercent, _lastStripHeightPercent) ||
            !Mathf.Approximately(bottomNormalized, _lastBottomNormalized) ||
            !Mathf.Approximately(leftNormalized, _lastLeftNormalized) ||
            !Mathf.Approximately(widthNormalized, _lastWidthNormalized) ||
            !Mathf.Approximately(baseOrthoSize, _lastBaseOrthoSize) ||
            Screen.width != _lastScreenWidth ||
            Screen.height != _lastScreenHeight;

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastStripHeightPercent = stripHeightPercent;
        _lastBottomNormalized = bottomNormalized;
        _lastLeftNormalized = leftNormalized;
        _lastWidthNormalized = widthNormalized;
        _lastBaseOrthoSize = baseOrthoSize;

        if (Application.isPlaying && persistStripLayout && layoutChanged)
            SaveLayoutToPrefs(forceImmediate: false);
    }
}
