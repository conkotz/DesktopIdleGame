using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple pulsing rectangle glow overlay (UI-only), tuned to match the helper whitelist glow feel.
/// Attach at runtime to any RectTransform and call <see cref="Show"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIPulseGlowOverlay : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform target;

    [Header("Pulse")]
    [SerializeField] private Color glowTint = new Color(1f, 0.93f, 0.42f, 1f);
    [SerializeField, Range(0f, 1f)] private float pulseAlphaMin = 0f;
    [SerializeField, Range(0f, 1f)] private float pulseAlphaMax = 0.74f;
    [SerializeField] private float pulseSpeed = 2.8f;

    [Header("Halo")]
    [SerializeField] private float haloPadding = 0f;
    [SerializeField, Range(1f, 1.25f)] private float haloUniformScale = 1f;
    [SerializeField, Range(0.1f, 1f)] private float haloAlphaScale = 0.72f;

    private RectTransform _rootRt;
    private Image _core;
    private Image _halo;

    private static Sprite s_fallbackSprite;

    public static UIPulseGlowOverlay Show(RectTransform targetRect)
    {
        if (!targetRect)
            return null;

        UIPulseGlowOverlay existing = targetRect.GetComponent<UIPulseGlowOverlay>();
        if (!existing)
            existing = targetRect.gameObject.AddComponent<UIPulseGlowOverlay>();

        existing.target = targetRect;
        existing.EnsureOverlay();
        existing.enabled = true;
        return existing;
    }

    public void Clear()
    {
        if (_rootRt)
            Destroy(_rootRt.gameObject);
        _rootRt = null;
        _core = null;
        _halo = null;
        enabled = false;
    }

    private void OnDisable()
    {
        // Keep overlay if disabled by parent; caller should Clear() explicitly when done.
        if (_core) _core.enabled = false;
        if (_halo) _halo.enabled = false;
    }

    private void OnEnable()
    {
        EnsureOverlay();
        if (_core) _core.enabled = true;
        if (_halo) _halo.enabled = true;
    }

    private void LateUpdate()
    {
        if (!_core)
            return;

        float t = Time.unscaledTime * Mathf.Max(0.01f, pulseSpeed);
        float s = (Mathf.Sin(t) + 1f) * 0.5f;
        float a = Mathf.Lerp(pulseAlphaMin, pulseAlphaMax, s);

        Color c = glowTint;
        c.a = a;
        _core.color = c;

        if (_halo)
        {
            Color hc = c;
            hc.a *= haloAlphaScale;
            _halo.color = hc;
        }
    }

    private void EnsureOverlay()
    {
        if (!target)
            target = transform as RectTransform;
        if (!target)
            return;

        if (_rootRt)
            return;

        // Root overlay as last child so it draws on top, but doesn't block raycasts.
        GameObject root = new GameObject("PulseGlowOverlay", typeof(RectTransform));
        _rootRt = root.GetComponent<RectTransform>();
        _rootRt.SetParent(target, false);
        _rootRt.anchorMin = Vector2.zero;
        _rootRt.anchorMax = Vector2.one;
        _rootRt.pivot = new Vector2(0.5f, 0.5f);
        _rootRt.anchoredPosition = Vector2.zero;
        _rootRt.sizeDelta = Vector2.zero;
        _rootRt.SetAsLastSibling();
        RectMask2D clip = _rootRt.gameObject.AddComponent<RectMask2D>();
        clip.padding = Vector4.zero;

        // Halo (optional)
        if (haloPadding > 0f)
        {
            GameObject haloGo = new GameObject("Halo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform haloRt = haloGo.GetComponent<RectTransform>();
            haloRt.SetParent(_rootRt, false);
            haloRt.anchorMin = Vector2.zero;
            haloRt.anchorMax = Vector2.one;
            haloRt.pivot = new Vector2(0.5f, 0.5f);
            haloRt.anchoredPosition = Vector2.zero;
            haloRt.sizeDelta = Vector2.one * (Mathf.Max(0f, haloPadding) * 2f);
            haloRt.localScale = Vector3.one * Mathf.Max(1f, haloUniformScale);
            _halo = haloGo.GetComponent<Image>();
            _halo.sprite = GetFallbackSprite();
            _halo.type = Image.Type.Simple;
            _halo.preserveAspect = false;
            _halo.raycastTarget = false;
        }

        // Core
        GameObject coreGo = new GameObject("Core", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform coreRt = coreGo.GetComponent<RectTransform>();
        coreRt.SetParent(_rootRt, false);
        coreRt.anchorMin = Vector2.zero;
        coreRt.anchorMax = Vector2.one;
        coreRt.pivot = new Vector2(0.5f, 0.5f);
        coreRt.anchoredPosition = Vector2.zero;
        coreRt.sizeDelta = Vector2.zero;

        _core = coreGo.GetComponent<Image>();
        _core.sprite = GetFallbackSprite();
        _core.type = Image.Type.Simple;
        _core.preserveAspect = false;
        _core.raycastTarget = false;
    }

    private static Sprite GetFallbackSprite()
    {
        if (s_fallbackSprite)
            return s_fallbackSprite;

        Texture2D t = Texture2D.whiteTexture;
        float w = Mathf.Max(1f, t.width);
        float h = Mathf.Max(1f, t.height);
        s_fallbackSprite = Sprite.Create(
            t,
            new Rect(0f, 0f, w, h),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        return s_fallbackSprite;
    }
}

