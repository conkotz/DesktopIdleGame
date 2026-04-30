using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows <c>Default</c> when strip ortho matches the prefab baseline, otherwise <c>Zoom N%</c> (100% = baseline).
/// Fades out after <see cref="idleSecondsBeforeFade"/> of no ortho change; zooming again restores full opacity.
/// </summary>
[DisallowMultipleComponent]
public sealed class StripZoomHudText : MonoBehaviour
{
    [SerializeField] private StripCameraController stripCamera;
    [SerializeField] private TMP_Text label;

    [SerializeField] private string defaultLabel = "Default";

    [Tooltip("Treat ortho as default when within this ratio of baseline (e.g. 0.005 ≈ half a percent).")]
    [SerializeField] private float defaultMatchRatioTolerance = 0.005f;

    [SerializeField] private float idleSecondsBeforeFade = 5f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    private CanvasGroup _canvasGroup;
    private float _lastSeenOrtho = float.NaN;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (!_canvasGroup)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        if (!label)
            label = GetComponent<TMP_Text>();

        ResolveStripIfNeeded();
    }

    private void OnEnable()
    {
        _lastSeenOrtho = float.NaN;
        if (_canvasGroup)
            _canvasGroup.alpha = 1f;

        StopFadeRoutine();
        if (Application.isPlaying && label && stripCamera)
            _fadeRoutine = StartCoroutine(FadeIdleRoutine());
    }

    private void OnDisable()
    {
        StopFadeRoutine();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !stripCamera || !label)
            return;

        float o = stripCamera.baseOrthoSize;
        if (Mathf.Approximately(o, _lastSeenOrtho))
            return;

        _lastSeenOrtho = o;
        RefreshContent();
        RestartVisibilityTimer();
    }

    private void RefreshContent()
    {
        float baseline = stripCamera.DefaultOrthoBaseline;
        if (baseline < 1e-4f)
        {
            label.text = defaultLabel;
            return;
        }

        float ratio = stripCamera.baseOrthoSize / baseline;
        if (Mathf.Abs(ratio - 1f) <= defaultMatchRatioTolerance)
            label.text = defaultLabel;
        else
            label.text = $"Zoom {Mathf.RoundToInt(ratio * 100f)}%";
    }

    private void ResolveStripIfNeeded()
    {
        if (stripCamera)
            return;

        stripCamera = FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Exclude);
    }

    private void RestartVisibilityTimer()
    {
        if (!isActiveAndEnabled)
            return;

        StopFadeRoutine();
        if (_canvasGroup)
            _canvasGroup.alpha = 1f;
        _fadeRoutine = StartCoroutine(FadeIdleRoutine());
    }

    private void StopFadeRoutine()
    {
        if (_fadeRoutine == null)
            return;

        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }

    private IEnumerator FadeIdleRoutine()
    {
        if (fadeOutDuration < 1e-4f)
            fadeOutDuration = 0.01f;

        yield return new WaitForSecondsRealtime(idleSecondsBeforeFade);

        float t = 0f;
        float startAlpha = _canvasGroup ? _canvasGroup.alpha : 1f;

        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / fadeOutDuration));
            if (_canvasGroup)
                _canvasGroup.alpha = a;
            yield return null;
        }

        if (_canvasGroup)
            _canvasGroup.alpha = 0f;

        _fadeRoutine = null;
    }
}
