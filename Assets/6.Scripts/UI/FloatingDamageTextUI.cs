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
        Evaded,
        Immune,
        Healing
    }

    [Header("Refs")]
    [SerializeField] private TMP_Text text;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform rect;

    [Header("Motion")]
    [SerializeField] private float distance = 70f;
    [SerializeField] private float arcHeight = 5f;
    [SerializeField] private float maxAngleOffset = 10f;
    [SerializeField] private float critAngleOffsetBonus = 4f;
    [SerializeField] private float critDistanceMultiplier = 1.12f;
    [Tooltip("Horizontal weight for drift away from the dealer (higher = steeper diagonal, less vertical).")]
    [SerializeField, Range(0.2f, 1.2f)] private float horizontalDriftWeight = 0.72f;
    [Tooltip("Minimum upward component so numbers still rise even when dealer is directly above/below.")]
    [SerializeField, Range(0.2f, 1f)] private float minUpwardDrift = 0.5f;

    [Header("Timing")]
    [SerializeField] private float fadeInSeconds = 0.08f;
    [SerializeField] private float visibleSeconds = 0.35f;
    [SerializeField] private float fadeOutSeconds = 0.25f;
    [Tooltip("Movement speed multiplier (lower = slower drift). Fade timings are unchanged.")]
    [SerializeField, Range(0.1f, 2f)] private float floatTravelSpeed = 0.5f;

    [Header("Crit")]
    [SerializeField] private float normalHitSizeMultiplier = 0.88f;
    [SerializeField] private float critSizeMultiplier = 1.3f;
    [SerializeField, Range(1f, 2f)] private float critBrightnessMultiplier = 1.15f;
    [SerializeField] private Color compactCritHighlightColor = new Color32(255, 244, 196, 255);
    [SerializeField, Range(0f, 1f)] private float compactCritTintBlend = 0.38f;

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
    [SerializeField] private Color poisonCritColor = new Color32(130, 245, 145, 255);
    [SerializeField, Min(1f)] private float poisonCritDotSizeMultiplier = 1.14f;
    [SerializeField] private Color healingColor = new Color32(100, 255, 140, 255);
    [SerializeField] private Color compactDamageColor = new Color32(220, 40, 40, 255);
    [SerializeField, Min(0.01f)] private float compactPulseSeconds = 0.085f;
    [SerializeField, Min(1f)] private float compactPulseMultiplier = 1.2f;
    [SerializeField, Min(1f)] private float compactCritPulseMultiplier = 1.5f;

    [Header("Status Presentations (HP tint + first-apply status popups)")]
    [SerializeField] private Color bleedColor = new Color32(170, 35, 35, 255);
    [SerializeField] private Color poisonColor = new Color32(85, 200, 90, 255);
    [SerializeField] private Color blockColor = new Color32(80, 170, 255, 255);
    [SerializeField] private Color evadeColor = new Color32(95, 220, 130, 255);
    [SerializeField] private Color parryColor = new Color32(255, 210, 40, 255);
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
    private bool _isNumericDamage;
    private bool _isDotNumeric;
    private float _spawnedAt;

    /// <summary>Set by <see cref="DamagePopupSystem"/> so popups follow world hits while the strip camera pans.</summary>
    private Vector3 _worldAnchor;
    private Vector2 _spawnJitter;
    private Camera _worldCam;
    private RectTransform _parentRect;
    private Camera _overlayEventCam;
    private Vector2 _cachedAnchorLocal;
    private bool _hasCachedAnchorLocal;

    private bool HasWorldFollow => _parentRect != null && _worldCam != null;

    public Color PoisonDamageColor => poisonColor;
    public Color BleedDamageColor => bleedColor;
    public Color BurnPresentationColor => burnPresentationColor;
    public Color ShockPresentationColor => shockPresentationColor;
    public Color ChillPresentationColor => chillPresentationColor;
    public Color StunPresentationColor => stunPresentationColor;
    public Color ExecutePresentationColor => executePresentationColor;
    public Color BlockPresentationColor => blockColor;
    public Color EvadePresentationColor => evadeColor;
    public Color ParryPresentationColor => parryColor;

    /// <summary>Colour for lingering status labels (Bleeding, Stunned, Blocked, Parry, etc.).</summary>
    public bool TryGetStatusPresentationColor(string message, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        switch (message.Trim())
        {
            case "Bleeding":
            case "bleeding":
                color = bleedColor;
                return true;
            case "Poisoned":
                color = poisonColor;
                return true;
            case "Burnt":
                color = burnPresentationColor;
                return true;
            case "Shocked":
                color = shockPresentationColor;
                return true;
            case "Chilled":
                color = chillPresentationColor;
                return true;
            case "Stunned":
                color = stunPresentationColor;
                return true;
            case "Blocked":
                color = blockColor;
                return true;
            case "Evaded":
                color = evadeColor;
                return true;
            case "Immune":
                color = blockColor;
                return true;
            case "Parry":
            case "Riposte":
                color = parryColor;
                return true;
            case "ENRAGED":
                color = new Color32(255, 70, 40, 255);
                return true;
            default:
                if (string.Equals(
                        message,
                        AbilityCombatPower.WayOfTheSlayerExecuteStatusPopupLabel,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    color = executePresentationColor;
                    return true;
                }

                return false;
        }
    }

    private void Awake()
    {
        if (!text) text = GetComponentInChildren<TMP_Text>(true);
        if (!group) group = GetComponent<CanvasGroup>();
        if (!rect) rect = GetComponent<RectTransform>();
        if (!group) group = gameObject.AddComponent<CanvasGroup>();
        if (text) text.raycastTarget = false;

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
        _hasCachedAnchorLocal = false;
    }

    internal void SetCachedAnchorLocal(Vector2 local)
    {
        _cachedAnchorLocal = local;
        _hasCachedAnchorLocal = true;
    }

    internal void PrepareForPool()
    {
        if (_run != null)
        {
            StopCoroutine(_run);
            _run = null;
        }

        if (text)
        {
            text.text = string.Empty;
            text.outlineWidth = 0f;
        }

        if (group)
            group.alpha = 0f;

        _worldCam = null;
        _parentRect = null;
        _overlayEventCam = null;
        _hasCachedAnchorLocal = false;
        _isNumericDamage = false;
        _isDotNumeric = false;
        _spawnedAt = 0f;
        if (rect)
            rect.localScale = Vector3.one;
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
        _isNumericDamage = true;
        _isDotNumeric = isDot;
        _spawnedAt = Time.unscaledTime;

        float totalVisible = visibleSeconds + fadeOutSeconds;
        // Crit numbers now dissipate at the same speed as normal hits.

        Vector2 dir = BuildDirection(worldDirection, isCrit);

        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Run(dir, totalVisible, isCrit));
    }

    public void InitCompactDamage(int amount, bool pulseCrit)
    {
        if (!text)
            return;

        if (_run != null)
            StopCoroutine(_run);

        _isNumericDamage = true;
        _isDotNumeric = false;
        _spawnedAt = Time.unscaledTime;

        text.text = Mathf.Max(0, amount).ToString();
        text.color = compactDamageColor;
        text.fontSize = _baseFontSize * normalHitSizeMultiplier;
        text.outlineWidth = 0f;
        group.alpha = 1f;
        _run = StartCoroutine(RunCompactPulse(pulseCrit));
    }

    public void InitBlocked(Vector3 worldDirection = default)
    {
        InitLingeringStatus("Blocked", blockColor);
    }

    public void InitEvaded(Vector3 worldDirection = default)
    {
        InitLingeringStatus("Evaded", evadeColor);
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
        _isNumericDamage = false;
        _isDotNumeric = false;
        _spawnedAt = Time.unscaledTime;

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
            PopupDamageKind.Evaded => evadeColor,
            PopupDamageKind.Immune => blockColor,
            PopupDamageKind.Healing => healingColor,
            _ => physicalColor
        };

        if (isCrit)
        {
            c = Brighten(c, critBrightnessMultiplier);
            c = Color.Lerp(c, compactCritHighlightColor, compactCritTintBlend);
        }

        return c;
    }

    /// <summary>Shared colour lookup for embedded overhead compact damage readouts.</summary>
    public static Color ResolveCompactColor(PopupDamageKind kind, bool isCrit = false)
    {
        FloatingDamageTextUI prefab = DamagePopupSystem.Instance != null ? DamagePopupSystem.Instance.PopupPrefab : null;
        if (prefab)
            return prefab.GetDisplayColor(kind, isCrit);
        return new Color32(220, 40, 40, 255);
    }

    public Color GetDisplayColorForKind(PopupDamageKind kind, bool isCrit) => GetDisplayColor(kind, isCrit);

    private float GetDisplayFontSize(PopupDamageKind kind, bool isCrit, bool isDot)
    {
        float size = _baseFontSize * normalHitSizeMultiplier;

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

    public bool IsNumericDamage => _isNumericDamage;
    public bool IsDotNumeric => _isDotNumeric;
    public float SpawnedAt => _spawnedAt;

    public void ExpireQuickly(float quickFadeSeconds = 0.12f)
    {
        if (_run != null)
            StopCoroutine(_run);
        _run = StartCoroutine(RunQuickExpire(Mathf.Max(0.04f, quickFadeSeconds)));
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

    private Vector2 BuildDirection(Vector3 worldDirection, bool isCrit)
    {
        float spread = maxAngleOffset + (isCrit ? critAngleOffsetBonus : 0f);

        float awayX = Mathf.Abs(worldDirection.x) > 0.02f
            ? Mathf.Sign(worldDirection.x)
            : 1f;
        Vector2 dir = new Vector2(
            awayX * horizontalDriftWeight,
            Mathf.Max(minUpwardDrift, Mathf.Abs(worldDirection.y) + minUpwardDrift * 0.35f));

        if (dir.sqrMagnitude < 0.0001f)
            dir = new Vector2(0.65f, 0.75f);

        dir.Normalize();

        float randomAngle = Random.Range(-spread, spread);
        if (isCrit)
            randomAngle += Random.value < 0.5f ? -2f : 2f;
        return Rotate(dir, randomAngle);
    }

    private IEnumerator Run(Vector2 direction, float lifeTime, bool isCrit)
    {
        float travelDistance = distance * (isCrit ? critDistanceMultiplier : 1f);
        Vector2 travel = direction * travelDistance;
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
            float moveElapsed = elapsed * Mathf.Max(0.01f, floatTravelSpeed);
            float p = Mathf.Clamp01(moveElapsed / Mathf.Max(0.0001f, lifeTime));

            if (HasWorldFollow)
            {
                Vector2 anim = Vector2.Lerp(Vector2.zero, travel, p);
                float arc = Mathf.Sin(p * Mathf.PI * 0.5f) * arcHeight;
                anim.y += arc;
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter + anim;
            }
            else
            {
                Vector2 pos = Vector2.Lerp(legacyStart, legacyEnd, p);
                float arc = Mathf.Sin(p * Mathf.PI * 0.5f) * arcHeight;
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

        ReleasePopup();
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

        ReleasePopup();
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

        ReleasePopup();
    }

    private IEnumerator RunCompactPulse(bool pulseCrit)
    {
        float pulseMul = pulseCrit ? compactCritPulseMultiplier : compactPulseMultiplier;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, compactPulseSeconds);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(pulseMul, 1f, t);
            if (rect)
                rect.localScale = new Vector3(scale, scale, 1f);
            if (HasWorldFollow)
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter;
            yield return null;
        }

        if (rect)
            rect.localScale = Vector3.one;
        group.alpha = 1f;
        _run = null;
    }

    private IEnumerator RunQuickExpire(float seconds)
    {
        float startAlpha = group ? group.alpha : 1f;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            if (group)
                group.alpha = Mathf.Lerp(startAlpha, 0f, t);
            if (HasWorldFollow)
                rect.anchoredPosition = GetAnchorLocal() + _spawnJitter;
            yield return null;
        }

        ReleasePopup();
    }

    private void ReleasePopup()
    {
        if (DamagePopupSystem.Instance != null)
            DamagePopupSystem.Instance.Release(this);
        else
            Destroy(gameObject);
    }

    private Vector2 GetAnchorLocal()
    {
        if (_hasCachedAnchorLocal)
            return _cachedAnchorLocal;

        if (!HasWorldFollow)
            return rect.anchoredPosition - _spawnJitter;

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