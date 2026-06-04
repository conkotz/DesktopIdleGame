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
        PoisonCrit,
        Blocked,
        Immune,
        Healing
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

    [Header("Status labels (Blocked / Parry / ailments)")]
    [SerializeField, Tooltip("TMP font size reduction for lingering status labels.")]
    private float statusFontSizeSubtractPoints = 2f;
    [SerializeField, Min(0.1f)] private float lingeringStatusLifetimeSeconds = 1.5f;
    [SerializeField, Min(0.01f)] private float lingeringStatusFadeInSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float lingeringStatusFadeOutSeconds = 0.2f;

    [Header("Healing")]
    [SerializeField, Tooltip("How far healing popups rise straight upward before fading.")]
    private float healingRiseDistance = 42f;

    [Header("Colours")]
    [SerializeField] private Color physicalColor = new Color32(220, 40, 40, 255);
    [FormerlySerializedAs("magicalColor")]
    [SerializeField] private Color magicColor = new Color32(80, 170, 255, 255);
    [FormerlySerializedAs("trueColor")]
    [SerializeField] private Color corruptionColor = new Color32(112, 64, 192, 255);
    [SerializeField] private Color typlessColor = Color.white;
    [SerializeField] private Color bleedColor = new Color32(170, 35, 35, 255);
    [SerializeField] private Color poisonColor = new Color32(85, 200, 90, 255);
    [SerializeField] private Color poisonCritColor = new Color32(130, 245, 145, 255);
    [SerializeField, Min(1f)] private float poisonCritDotSizeMultiplier = 1.14f;
    [SerializeField] private Color blockColor = new Color32(80, 170, 255, 255);
    [SerializeField] private Color parryColor = new Color32(255, 210, 40, 255);
    [SerializeField] private Color healingColor = new Color32(100, 255, 140, 255);

    [Header("Ailment presentation (HP tint + first-apply status popups)")]
    [SerializeField] private Color burnPresentationColor = new Color32(255, 140, 40, 255);
    [SerializeField] private Color shockPresentationColor = new Color32(255, 190, 70, 255);
    [SerializeField] private Color chillPresentationColor = new Color32(90, 160, 255, 255);
    [SerializeField] private Color stunPresentationColor = new Color32(38, 22, 12, 255);
    [SerializeField] private Color stunPresentationOutlineColor = new Color32(255, 232, 200, 255);
    [SerializeField, Range(0f, 0.5f)] private float stunPresentationOutlineWidth = 0.28f;
    [SerializeField] private Color executePresentationColor = new Color32(140, 18, 28, 255);
    [SerializeField] private Color executePresentationOutlineColor = new Color32(255, 220, 200, 255);
    [SerializeField, Range(0f, 0.5f)] private float executePresentationOutlineWidth = 0.28f;

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
    public Color StunPresentationColor => stunPresentationColor;
    public Color ExecutePresentationColor => executePresentationColor;

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

        bool isPoisonCritDot = isDot && kind == PopupDamageKind.PoisonCrit;
        if (isDot && !isPoisonCritDot)
            isCrit = false;

        text.text = amount.ToString();
        text.color = GetDisplayColor(kind, isCrit);
        text.fontSize = GetDisplayFontSize(kind, isCrit, isDot);

        float totalVisible = visibleSeconds + fadeOutSeconds;
        if (isCrit)
            totalVisible += critExtraLifetime;

        Vector2 dir = BuildDirection(worldDirection);

        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Run(dir, totalVisible));
    }

    public void InitBlocked(Vector3 worldDirection = default)
    {
        InitLingeringStatus("Blocked", blockColor);
    }

    public void InitParry(Vector3 worldDirection = default, string label = "Parry")
    {
        string textLabel = string.IsNullOrWhiteSpace(label) ? "Parry" : label.Trim();
        InitLingeringStatus(textLabel, parryColor);
    }

    public void InitImmune(Vector3 worldDirection = default)
    {
        InitLingeringStatus("Immune", blockColor);
    }

    public void InitHealing(int amount)
    {
        if (!text)
            return;

        text.text = $"+{Mathf.Max(0, amount)}";
        text.color = healingColor;
        text.fontSize = _baseFontSize;
        text.outlineWidth = 0f;

        if (_run != null)
            StopCoroutine(_run);

        float totalVisible = visibleSeconds + fadeOutSeconds;
        _run = StartCoroutine(RunVertical(Mathf.Max(8f, healingRiseDistance), totalVisible));
    }

    /// <summary>Lingering label (ailments, Blocked, Parry) — pinned to anchor, no travel arc.</summary>
    public void InitAilmentStatus(string message, Color color, Vector3 worldDirection = default)
    {
        InitLingeringStatus(message, color);
    }

    public void InitLingeringStatus(string message, Color color)
    {
        if (!text) return;

        text.text = message;
        text.color = color;
        text.fontSize = GetStatusFontSize();
        ApplyStunStatusOutlineIfNeeded(message);

        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(RunLingering(lingeringStatusLifetimeSeconds));
    }

    private void ApplyStunStatusOutlineIfNeeded(string message)
    {
        if (!text)
            return;

        bool isStun = string.Equals(message, "Stunned", System.StringComparison.OrdinalIgnoreCase);
        if (isStun)
        {
            text.outlineWidth = stunPresentationOutlineWidth;
            text.outlineColor = stunPresentationOutlineColor;
            return;
        }

        bool isExecute = string.Equals(
            message,
            AbilityCombatPower.WayOfTheSlayerExecuteStatusPopupLabel,
            System.StringComparison.OrdinalIgnoreCase);
        if (isExecute)
        {
            text.outlineWidth = executePresentationOutlineWidth;
            text.outlineColor = executePresentationOutlineColor;
            return;
        }

        text.outlineWidth = 0f;
    }

    private float GetStatusFontSize() =>
        Mathf.Max(8f, _baseFontSize - Mathf.Max(0f, statusFontSizeSubtractPoints));

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
            PopupDamageKind.PoisonCrit => poisonCritColor,
            PopupDamageKind.Blocked => blockColor,
            PopupDamageKind.Immune => blockColor,
            PopupDamageKind.Healing => healingColor,
            _ => physicalColor
        };

        if (isCrit)
            c = Brighten(c, critBrightnessMultiplier);

        return c;
    }

    private float GetDisplayFontSize(PopupDamageKind kind, bool isCrit, bool isDot)
    {
        float size = _baseFontSize;

        if (isDot)
        {
            size = Mathf.Max(8f, size - Mathf.Max(0f, dotFontSizeSubtractPoints));
            size *= dotSizeMultiplier;
        }

        if (kind == PopupDamageKind.PoisonCrit)
            size *= poisonCritDotSizeMultiplier;
        else if (isCrit)
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

    private IEnumerator RunLingering(float lifeTime)
    {
        lifeTime = Mathf.Max(0.1f, lifeTime);
        float fadeIn = Mathf.Clamp(lingeringStatusFadeInSeconds, 0.01f, lifeTime * 0.4f);
        float fadeOut = Mathf.Clamp(lingeringStatusFadeOutSeconds, 0.01f, lifeTime * 0.4f);
        float hold = Mathf.Max(0.05f, lifeTime - fadeIn - fadeOut);

        group.alpha = 0f;

        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Clamp01(t / fadeIn);
            if (HasWorldFollow)
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter;
            yield return null;
        }

        group.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < hold)
        {
            elapsed += Time.deltaTime;
            if (HasWorldFollow)
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter;
            yield return null;
        }

        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            if (HasWorldFollow)
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter;
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator RunVertical(float riseDistance, float lifeTime)
    {
        riseDistance = Mathf.Max(0f, riseDistance);
        lifeTime = Mathf.Max(0.1f, lifeTime);
        Vector2 legacyStart = rect.anchoredPosition;
        Vector2 legacyEnd = legacyStart + Vector2.up * riseDistance;
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
            Vector2 anim = Vector2.up * Mathf.Lerp(0f, riseDistance, p);

            if (HasWorldFollow)
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter + anim;
            else
                rect.anchoredPosition = Vector2.Lerp(legacyStart, legacyEnd, p);

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