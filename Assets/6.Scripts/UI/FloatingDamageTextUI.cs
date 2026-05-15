using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class FloatingDamageTextUI : MonoBehaviour
{
    public enum PopupDamageKind
    {
        Physical,
        Magic,
        Corruption,
        Typless,
        Bleed,
        Poison,
        Blocked,
        Immune
    }

    [Header("Refs")]
    [SerializeField] private TMP_Text text;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform rect;

    [Header("Motion")]
    [SerializeField] private float distance = 65f;
    [SerializeField] private float arcHeight = 25f;
    [SerializeField] private float maxAngleOffset = 35f;

    [Header("Timing")]
    [SerializeField] private float fadeInSeconds = 0.08f;
    [SerializeField] private float visibleSeconds = 0.35f;
    [SerializeField] private float fadeOutSeconds = 0.25f;

    [Header("Crit")]
    [SerializeField] private float critSizeMultiplier = 1.85f;
    [SerializeField] private float critExtraLifetime = 1f;
    [SerializeField, Range(1f, 2f)] private float critBrightnessMultiplier = 1.15f;

    [Header("DOT (bleed / poison / burn ticks)")]
    [SerializeField, Tooltip("TMP font size points removed after base size (about two default inspector steps).")]
    private float dotFontSizeSubtractPoints = 4f;
    [SerializeField, Tooltip("Applied after subtract; keep at 1 to size DoTs only via subtract.")]
    private float dotSizeMultiplier = 1f;

    [Header("Colours")]
    [SerializeField] private Color physicalColor = new Color32(220, 40, 40, 255);
    [FormerlySerializedAs("magicalColor")]
    [SerializeField] private Color magicColor = new Color32(80, 170, 255, 255);
    [FormerlySerializedAs("trueColor")]
    [SerializeField] private Color corruptionColor = new Color32(112, 64, 192, 255);
    [SerializeField] private Color typlessColor = Color.white;
    [SerializeField] private Color bleedColor = new Color32(170, 35, 35, 255);
    [SerializeField] private Color poisonColor = new Color32(85, 200, 90, 255);
    [SerializeField] private Color blockColor = new Color32(80, 170, 255, 255);

    [Header("Ailment presentation (HP tint + first-apply status popups)")]
    [SerializeField] private Color burnPresentationColor = new Color32(255, 140, 40, 255);
    [SerializeField] private Color shockPresentationColor = new Color32(255, 190, 70, 255);
    [SerializeField] private Color chillPresentationColor = new Color32(90, 160, 255, 255);

    private float _baseFontSize;
    private Coroutine _run;

    /// <summary>Set by <see cref="DamagePopupSystem"/> so popups follow world hits while the strip camera pans.</summary>
    private Vector3 _worldAnchor;
    private Vector2 _spawnJitter;
    private Camera _worldCam;
    private RectTransform _parentRect;
    private Camera _overlayEventCam;

    private bool HasWorldFollow => _parentRect != null && _worldCam != null;

    public Color PoisonDamageColor => poisonColor;
    public Color BleedDamageColor => bleedColor;
    public Color BurnPresentationColor => burnPresentationColor;
    public Color ShockPresentationColor => shockPresentationColor;
    public Color ChillPresentationColor => chillPresentationColor;

    private void Awake()
    {
        if (!text) text = GetComponentInChildren<TMP_Text>(true);
        if (!group) group = GetComponent<CanvasGroup>();
        if (!rect) rect = GetComponent<RectTransform>();
        if (!group) group = gameObject.AddComponent<CanvasGroup>();

        if (text != null)
            _baseFontSize = text.fontSize;
    }

    /// <summary>
    /// Call immediately after instantiate (before <see cref="Init"/>). Keeps the label pinned to the world hit
    /// while the gameplay camera moves; still drawn on the high-sort damage overlay canvas.
    /// </summary>
    public void BeginWorldAnchorFollow(Vector3 worldAnchor, Vector2 spawnJitter, Camera worldCam, RectTransform parentRect, Camera overlayEventCam)
    {
        _worldAnchor = worldAnchor;
        _spawnJitter = spawnJitter;
        _worldCam = worldCam;
        _parentRect = parentRect;
        _overlayEventCam = overlayEventCam;
    }

    public void Init(int amount, PopupDamageKind kind, bool isCrit, bool isDot, Vector3 worldDirection)
    {
        if (!text) return;

        // DOTs should never crit visually
        if (isDot)
            isCrit = false;

        text.text = amount.ToString();
        text.color = GetDisplayColor(kind, isCrit);
        text.fontSize = GetDisplayFontSize(isCrit, isDot);

        float totalVisible = visibleSeconds + fadeOutSeconds;
        if (isCrit)
            totalVisible += critExtraLifetime;

        Vector2 dir = BuildDirection(worldDirection);

        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Run(dir, totalVisible));
    }

    public void InitBlocked(Vector3 worldDirection)
    {
        if (!text) return;

        text.text = "Blocked";
        text.color = blockColor;
        text.fontSize = _baseFontSize;

        Vector2 dir = BuildDirection(worldDirection);

        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Run(dir, visibleSeconds + fadeOutSeconds));
    }

    public void InitImmune(Vector3 worldDirection)
    {
        if (!text) return;

        text.text = "Immune";
        text.color = blockColor;
        text.fontSize = _baseFontSize;

        Vector2 dir = BuildDirection(worldDirection);

        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Run(dir, visibleSeconds + fadeOutSeconds));
    }

    /// <summary>Short floating label (e.g. first application of an ailment). Uses the same motion as blocked/immune.</summary>
    public void InitAilmentStatus(string message, Color color, Vector3 worldDirection)
    {
        if (!text) return;

        text.text = message;
        text.color = color;
        text.fontSize = _baseFontSize;

        Vector2 dir = BuildDirection(worldDirection);

        if (_run != null) StopCoroutine(_run);
        float life = visibleSeconds + fadeOutSeconds;
        _run = StartCoroutine(Run(dir, life));
    }

    private Color GetDisplayColor(PopupDamageKind kind, bool isCrit)
    {
        Color c = kind switch
        {
            PopupDamageKind.Physical => physicalColor,
            PopupDamageKind.Magic => magicColor,
            PopupDamageKind.Corruption => corruptionColor,
            PopupDamageKind.Typless => typlessColor,
            PopupDamageKind.Bleed => bleedColor,
            PopupDamageKind.Poison => poisonColor,
            PopupDamageKind.Blocked => blockColor,
            PopupDamageKind.Immune => blockColor,
            _ => physicalColor
        };

        if (isCrit)
            c = Brighten(c, critBrightnessMultiplier);

        return c;
    }

    private float GetDisplayFontSize(bool isCrit, bool isDot)
    {
        float size = _baseFontSize;

        if (isDot)
        {
            size = Mathf.Max(8f, size - Mathf.Max(0f, dotFontSizeSubtractPoints));
            size *= dotSizeMultiplier;
        }

        if (isCrit)
            size *= critSizeMultiplier;

        return size;
    }

    private static Color Brighten(Color c, float mult)
    {
        return new Color(
            Mathf.Clamp01(c.r * mult),
            Mathf.Clamp01(c.g * mult),
            Mathf.Clamp01(c.b * mult),
            c.a
        );
    }

    private Vector2 BuildDirection(Vector3 worldDirection)
    {
        Vector2 dir = new Vector2(worldDirection.x, worldDirection.y).normalized;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.up;

        dir.y = Mathf.Abs(dir.y) + 0.5f;
        dir.Normalize();

        float randomAngle = Random.Range(-maxAngleOffset, maxAngleOffset);
        dir = Rotate(dir, randomAngle);

        return dir;
    }

    private IEnumerator Run(Vector2 direction, float lifeTime)
    {
        Vector2 travel = direction * distance;
        Vector2 legacyStart = rect.anchoredPosition;
        Vector2 legacyEnd = legacyStart + travel;

        group.alpha = 0f;

        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeInSeconds));
            if (HasWorldFollow)
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter;
            yield return null;
        }

        group.alpha = 1f;

        float elapsed = 0f;

        while (elapsed < lifeTime)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, lifeTime));

            if (HasWorldFollow)
            {
                Vector2 anim = Vector2.Lerp(Vector2.zero, travel, p);
                float arc = Mathf.Sin(p * Mathf.PI) * arcHeight;
                anim.y += arc;
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter + anim;
            }
            else
            {
                Vector2 pos = Vector2.Lerp(legacyStart, legacyEnd, p);
                float arc = Mathf.Sin(p * Mathf.PI) * arcHeight;
                pos.y += arc;
                rect.anchoredPosition = pos;
            }

            if (elapsed > lifeTime - fadeOutSeconds)
            {
                float fadeP = (elapsed - (lifeTime - fadeOutSeconds)) / Mathf.Max(0.0001f, fadeOutSeconds);
                group.alpha = 1f - fadeP;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private Vector2 GetAnchorLocal()
    {
        Vector3 screen = _worldCam.WorldToScreenPoint(_worldAnchor);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect, screen, _overlayEventCam, out Vector2 local))
            return local;

        return rect.anchoredPosition - _spawnJitter;
    }

    private Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }
}