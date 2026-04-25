using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class GoldPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Optional second line under the gold amount (e.g. quest rewards). Leave empty in prefab to use single-line popups only.")]
    [SerializeField] private TMP_Text sourceLabel;
    [SerializeField] private float floatUpPx = 45f;
    [Header("Message popups (+ gold lingers / phased fade)")]
    [SerializeField] private float riseDuration = 1.1f;
    [FormerlySerializedAs("duration")]
    [SerializeField] private float lingerAtApexSeconds = 2f;
    [SerializeField] private float fadeDuration = 0.5f;
    [Header("Popup size")]
    [SerializeField, Range(0.1f, 1f)] private float messageScale = 0.35f;
    [SerializeField, Range(0.1f, 1f)] private float goldScale = 0.65f;

    [Header("Gold gain popup (+Ng)")]
    [Tooltip("Original style: float up and fade out together over this time (unscaled).")]
    [SerializeField] private float goldCombinedDuration = 2f;

    [Header("Default Colours")]
    [SerializeField] private Color goldColor = new Color(0.72f, 0.52f, 0.06f, 1f);
    [SerializeField] private Color messageColor = Color.white;
    [SerializeField] private Color levelUpColor = new Color(0.35f, 0.8f, 1f, 1f);
    [SerializeField] private Color sourceLineColor = new Color(0.74f, 0.77f, 0.82f, 1f);

    [Header("Gold readability")]
    [SerializeField] private float goldOutlineWidth = 0.28f;
    [SerializeField] private Color goldOutlineColor = new Color(0.12f, 0.08f, 0.02f, 1f);

    private RectTransform _rt;
    private Coroutine _co;
    private Color _sourceFadeBase = Color.white;
    private Action _onComplete;

    /// <summary>Wall-clock duration for message-style popups (unscaled).</summary>
    public float TotalPhasedAnimationDuration =>
        Mathf.Max(0f, riseDuration) + Mathf.Max(0f, lingerAtApexSeconds) + Mathf.Max(0f, fadeDuration);

    /// <summary>Wall-clock duration for +gold popups using the combined rise/fade (unscaled).</summary>
    public float TotalGoldAnimationDuration => Mathf.Max(0.0001f, goldCombinedDuration);

    private void Awake()
    {
        _rt = transform as RectTransform;
        if (!label) label = GetComponentInChildren<TMP_Text>(true);
    }

    public void PlayLocal(Vector2 startAnchoredPos, int amount)
    {
        PlayLocal(startAnchoredPos, amount, null, null);
    }

    public void PlayLocal(Vector2 startAnchoredPos, int amount, string sourceLine)
    {
        PlayLocal(startAnchoredPos, amount, sourceLine, null);
    }

    public void PlayLocal(Vector2 startAnchoredPos, int amount, string sourceLine, Action onComplete)
    {
        ConfigureSourceLine(sourceLine);
        PlayLocalTextCore(startAnchoredPos, $"+{amount:N0}g", goldColor, applyGoldStroke: true, onComplete, useGoldLegacyFade: true);
    }

    public void PlayLocalText(Vector2 startAnchoredPos, string text)
    {
        PlayLocalText(startAnchoredPos, text, messageColor, applyGoldStroke: false, null);
    }

    public void PlayLocalText(Vector2 startAnchoredPos, string text, Color color)
    {
        PlayLocalText(startAnchoredPos, text, color, applyGoldStroke: false, null);
    }

    public void PlayLocalText(Vector2 startAnchoredPos, string text, Color color, bool applyGoldStroke)
    {
        PlayLocalText(startAnchoredPos, text, color, applyGoldStroke, null);
    }

    public void PlayLocalText(Vector2 startAnchoredPos, string text, Color color, bool applyGoldStroke, Action onComplete)
    {
        ConfigureSourceLine(null);
        PlayLocalTextCore(startAnchoredPos, text, color, applyGoldStroke, onComplete, useGoldLegacyFade: false);
    }

    private void PlayLocalTextCore(Vector2 startAnchoredPos, string text, Color color, bool applyGoldStroke, Action onComplete, bool useGoldLegacyFade)
    {
        _onComplete = onComplete;
        gameObject.SetActive(true);

        if (label)
        {
            label.text = text;
            label.color = color;
            if (applyGoldStroke)
            {
                label.outlineWidth = goldOutlineWidth;
                label.outlineColor = goldOutlineColor;
            }
            else
            {
                label.outlineWidth = 0f;
            }
        }

        if (_rt != null)
        {
            _rt.anchoredPosition = startAnchoredPos;
            _rt.localScale = Vector3.one * (useGoldLegacyFade ? goldScale : messageScale);
        }

        if (_co != null) StopCoroutine(_co);
        _co = useGoldLegacyFade
            ? StartCoroutine(AnimAnchoredGoldLegacy(startAnchoredPos, color))
            : StartCoroutine(AnimAnchored(startAnchoredPos, color));
    }

    private void ConfigureSourceLine(string line)
    {
        if (!sourceLabel)
            return;

        bool show = !string.IsNullOrWhiteSpace(line);
        sourceLabel.gameObject.SetActive(show);
        if (!show)
            return;

        sourceLabel.text = line.Trim();
        sourceLabel.color = sourceLineColor;
        sourceLabel.outlineWidth = 0f;
        _sourceFadeBase = sourceLineColor;
    }

    public void PlayLocalLevelUp(Vector2 startAnchoredPos, string text)
    {
        PlayLocalText(startAnchoredPos, text, levelUpColor, applyGoldStroke: false, null);
    }

    private void ApplyAlpha(Color baseColor, float mainAlphaMul)
    {
        if (label)
        {
            var c = baseColor;
            c.a *= mainAlphaMul;
            label.color = c;
        }

        if (sourceLabel && sourceLabel.gameObject.activeSelf)
        {
            Color sc = _sourceFadeBase;
            sc.a = _sourceFadeBase.a * mainAlphaMul;
            sourceLabel.color = sc;
        }
    }

    private IEnumerator AnimAnchored(Vector2 start, Color baseColor)
    {
        Vector2 apex = start + Vector2.up * floatUpPx;
        float rise = Mathf.Max(0.0001f, riseDuration);
        float linger = Mathf.Max(0f, lingerAtApexSeconds);
        float fade = Mathf.Max(0.0001f, fadeDuration);

        float t = 0f;
        while (t < rise)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / rise);
            float ease = 1f - Mathf.Pow(1f - u, 2f);
            if (_rt != null)
                _rt.anchoredPosition = Vector2.LerpUnclamped(start, apex, ease);
            ApplyAlpha(baseColor, 1f);
            yield return null;
        }

        if (_rt != null)
            _rt.anchoredPosition = apex;

        t = 0f;
        while (t < linger)
        {
            t += Time.unscaledDeltaTime;
            ApplyAlpha(baseColor, 1f);
            yield return null;
        }

        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / fade);
            ApplyAlpha(baseColor, 1f - u);
            yield return null;
        }

        var complete = _onComplete;
        _onComplete = null;
        complete?.Invoke();
        gameObject.SetActive(false);
    }

    private IEnumerator AnimAnchoredGoldLegacy(Vector2 start, Color baseColor)
    {
        float dur = Mathf.Max(0.0001f, goldCombinedDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float ease = 1f - Mathf.Pow(1f - u, 2f);

            if (_rt != null)
                _rt.anchoredPosition = start + Vector2.up * (floatUpPx * ease);

            ApplyAlpha(baseColor, 1f - u);
            yield return null;
        }

        var complete = _onComplete;
        _onComplete = null;
        complete?.Invoke();
        gameObject.SetActive(false);
    }
}