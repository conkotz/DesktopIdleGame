using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillTreeNodeUI : MonoBehaviour, ITreeConnectorEndpoint, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    /// <summary>Minimum gap between node edge and the inner edge of the side labels (pixels).</summary>
    private const float SideLabelPadding = 2f;
    /// <summary>Extra gap proportional to node width so larger tiers stay visually clear of the square.</summary>
    private const float SideLabelPaddingWidthFactor = 0.02f;
    /// <summary>Width of the side label rects (kept narrow; long labels can be multi-line).</summary>
    private const float SideLabelWidth = 80f;
    private const float CapstoneLabelYOffset = 12f;

    private const float LockedFillRgbScale = 0.06f;
    private const float LockedFillAlphaScale = 0.25f;
    private const float LockedIconRgbScale = 0.1f;
    private const float LockedIconAlphaScale = 0.18f;
    private const float LockedOverlayAlpha = 0.97f;

    [Header("References")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image outerRingImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private GameObject lockedOverlay;
    [Tooltip("Optional ornate frame (e.g. SelectedBorder under Fill). Toggled from selection rules; prefab handles layout.")]
    [SerializeField] private GameObject selectedBorderOverlay;
    [SerializeField] private GameObject selectedGlow;
    [Tooltip("Shown while the linked ability is on cooldown (driven by SkillTreeViewUI).")]
    [SerializeField] private GameObject cooldownOverlay;
    [Tooltip("Optional radial/vertical fill; uses Image.Type.Filled at runtime when assigned.")]
    [SerializeField] private Image cooldownOverlayFillImage;
    [SerializeField] private TMP_Text cooldownOverlayTimeText;
    [Tooltip("Shown while the linked ability has an active timed buff / lingering effect (driven by SkillTreeViewUI).")]
    [SerializeField] private GameObject activeBuffOverlay;
    [SerializeField] private TMP_Text activeBuffOverlayTimeText;
    [SerializeField] private Button button;
    [Tooltip("Opens the enhancement choice branch for eligible nodes (wired by SkillTreeViewUI).")]
    [SerializeField] private Button enhanceButton;
    [Tooltip("Shown when the player meets the level gate but has not committed this row’s pick / enhancement (driven by SkillTreeViewUI).")]
    [SerializeField] private GameObject notSelectedRoot;
    [SerializeField] private Color selectedOutlineColor = new Color(1f, 0.84f, 0.2f, 1f);
    [SerializeField] private float selectedBorderThickness = 4f;
    [SerializeField] private float selectedGlowPaddingCompensation = 4f;
    [Header("Minor passive selection chrome")]
    [Tooltip("Thinner selection ring math for Minor Passive only so the ornate border does not read as a smaller inner tile.")]
    [SerializeField] private float minorPassiveSelectedBorderThickness = 3.5f;
    [SerializeField] private float minorPassiveSelectedGlowPaddingCompensation = 2f;
    [Tooltip(
        "When the selection border is visible on Minor Passive / Minor Unlock, scales fill + icon only (layout stays root×0.75). " +
        "Default ~1/0.75 closes the gap between the black tile and the ornate border.")]
    [SerializeField, Min(1f)] private float minorPassiveSelectionFillScaleWhenBorderShown = 1f / 0.75f;

    [Header("Colors")]
    [FormerlySerializedAs("minorColor")]
    [SerializeField] private Color minorPassiveColor = new Color(0.72f, 0.33f, 0.33f);
    [SerializeField] private Color minorUnlockColor = new Color(0.28f, 0.55f, 0.72f);
    [FormerlySerializedAs("passiveColor")]
    [SerializeField] private Color majorPassiveColor = new Color(0.78f, 0.62f, 0.26f);
    [SerializeField] private Color unlockColor = Color.black;
    [SerializeField] private Color abilityColor = new Color(0.82f, 0.42f, 0.18f);
    [FormerlySerializedAs("branchColor")]
    [SerializeField] private Color choiceColor = new Color(0.26f, 0.53f, 0.82f);
    [FormerlySerializedAs("capstoneColor")]
    [SerializeField] private Color capstonePassiveColor = new Color(0.58f, 0.32f, 0.76f);

    [Header("Sizes")]
    /// <summary>Multiplier for <see cref="GetVisualSize"/>; layout code should scale serialized gaps via the same value.</summary>
    public const float NodeVisualScale = 1.5f;

    /// <summary>Filler spine nodes — kept small so milestone nodes read clearly.</summary>
    private static readonly Vector2 MINOR_PASSIVE_SIZE = new Vector2(18f, 18f);
    /// <summary>Between <see cref="MINOR_PASSIVE_SIZE"/> and milestone nodes (55).</summary>
    private static readonly Vector2 MINOR_UNLOCK_SIZE = new Vector2(36f, 36f);
    private static readonly Vector2 MILESTONE_SIZE = new Vector2(55f, 55f);
    private static readonly Vector2 MAJOR_PASSIVE_SIZE = MILESTONE_SIZE;
    private static readonly Vector2 UNLOCK_SIZE = MILESTONE_SIZE;
    private static readonly Vector2 ABILITY_SIZE = MILESTONE_SIZE;
    private static readonly Vector2 CHOICE_SIZE = MILESTONE_SIZE;
    private static readonly Vector2 CAPSTONE_PASSIVE_SIZE = new Vector2(80f, 80f);

    private bool isLocked;
    private bool isSelected;
    private SkillTreeNodeVisualType appliedVisualType;
    private System.Action onClickAction;
    private System.Action onRightClickAction;
    private System.Action onHoverEnter;
    private System.Action onHoverExit;
    private Vector2 _baseOuterRingSize;
    private Outline _fillOutline;
    private Color _unlockedFillColor = Color.black;
    private Color _unlockedIconColor = Color.white;
    private Color _unlockedRingColor = Color.white;
    private Color _defaultLockedOverlayColor = new Color(0f, 0f, 0f, 0.92f);
    private Color _unlockedOutlineEffect = Color.black;
    private bool _cachedUnlockedOutline;
    /// <summary>When false, the inner <see cref="fillImage"/> is transparent (icon-only milestone nodes).</summary>
    private bool _useColoredFillBackground = true;
    private UIPulseGlowOverlay _unlockGlow;

    private const float NotSelectedFadeSeconds = 0.35f;
    private const float NotSelectedHoldOpaqueSeconds = 2f;
    private CanvasGroup _notSelectedCanvasGroup;
    private Image _notSelectedImage;
    private Color _notSelectedImageBaseColor = Color.white;
    private Coroutine _notSelectedFlashRoutine;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    public void ShowUnlockGlow()
    {
        RectTransform rt = RectTransform;
        if (!rt)
            return;
        _unlockGlow = UIPulseGlowOverlay.Show(rt);
    }

    public void ClearUnlockGlow()
    {
        if (_unlockGlow != null)
            _unlockGlow.Clear();
        _unlockGlow = null;
    }

    private void Awake()
    {
        if (lockedOverlay != null)
        {
            Image overlayImg = lockedOverlay.GetComponent<Image>();
            if (overlayImg != null)
                _defaultLockedOverlayColor = overlayImg.color;
        }

        if (outerRingImage != null)
            _unlockedRingColor = outerRingImage.color;

        CacheNotSelectedVisualDriver();
    }

    private void OnDisable()
    {
        SetNotSelectedPrompt(false);
    }

    private void CacheNotSelectedVisualDriver()
    {
        if (notSelectedRoot == null)
            return;

        _notSelectedCanvasGroup = notSelectedRoot.GetComponent<CanvasGroup>();
        _notSelectedImage = notSelectedRoot.GetComponent<Image>();
        if (_notSelectedImage != null)
        {
            _notSelectedImageBaseColor = _notSelectedImage.color;
            _notSelectedImage.raycastTarget = false;
        }
    }

    /// <summary>
    /// Skill tree: pulse the optional <see cref="notSelectedRoot"/> while <paramref name="show"/> is true (alpha 0 → 1, hold, fade out, repeat).
    /// </summary>
    public void SetNotSelectedPrompt(bool show)
    {
        if (!show)
        {
            if (_notSelectedFlashRoutine != null)
            {
                StopCoroutine(_notSelectedFlashRoutine);
                _notSelectedFlashRoutine = null;
            }

            ApplyNotSelectedAlpha(0f);
            if (notSelectedRoot != null)
                notSelectedRoot.SetActive(false);
            return;
        }

        if (notSelectedRoot == null)
            return;

        if (_notSelectedCanvasGroup == null && _notSelectedImage == null)
            CacheNotSelectedVisualDriver();
        if (_notSelectedCanvasGroup == null && _notSelectedImage == null)
            return;

        notSelectedRoot.SetActive(true);
        if (_notSelectedFlashRoutine == null)
            _notSelectedFlashRoutine = StartCoroutine(NotSelectedFlashLoop());
    }

    private void ApplyNotSelectedAlpha(float a)
    {
        a = Mathf.Clamp01(a);
        if (_notSelectedCanvasGroup != null)
            _notSelectedCanvasGroup.alpha = a;
        else if (_notSelectedImage != null)
        {
            Color c = _notSelectedImageBaseColor;
            c.a = a * _notSelectedImageBaseColor.a;
            _notSelectedImage.color = c;
        }
    }

    private IEnumerator NotSelectedFlashLoop()
    {
        ApplyNotSelectedAlpha(0f);

        while (true)
        {
            yield return FadeNotSelectedAlpha(0f, 1f, NotSelectedFadeSeconds);
            float hold = 0f;
            while (hold < NotSelectedHoldOpaqueSeconds)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return FadeNotSelectedAlpha(1f, 0f, NotSelectedFadeSeconds);
            yield return null;
        }
    }

    private IEnumerator FadeNotSelectedAlpha(float from, float to, float duration)
    {
        if (duration <= 0.0001f)
        {
            ApplyNotSelectedAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            ApplyNotSelectedAlpha(Mathf.Lerp(from, to, u));
            yield return null;
        }

        ApplyNotSelectedAlpha(to);
    }

    /// <summary>
    /// Shared source of truth for node visual sizes.
    /// Use this from layout code to keep spacing in sync with ApplyVisualType().
    /// </summary>
    public static Vector2 GetVisualSize(SkillTreeNodeVisualType type)
    {
        Vector2 baseSize = type switch
        {
            SkillTreeNodeVisualType.MinorPassive => MINOR_PASSIVE_SIZE,
            SkillTreeNodeVisualType.MinorUnlock => MINOR_UNLOCK_SIZE,
            SkillTreeNodeVisualType.MajorPassive => MAJOR_PASSIVE_SIZE,
            SkillTreeNodeVisualType.Unlock => UNLOCK_SIZE,
            SkillTreeNodeVisualType.Ability => ABILITY_SIZE,
            SkillTreeNodeVisualType.Choice => CHOICE_SIZE,
            SkillTreeNodeVisualType.CapstonePassive => CAPSTONE_PASSIVE_SIZE,
            _ => MINOR_PASSIVE_SIZE
        };
        return baseSize * NodeVisualScale;
    }

    /// <summary>
    /// Returns the visible node box size used for layout/connector math (currently the Fill).
    /// Side labels do not affect this.
    /// </summary>
    public Vector2 GetVisualBoxSize()
    {
        if (fillImage != null)
            return fillImage.rectTransform.rect.size;

        return RectTransform.rect.size;
    }

    public float GetVisualHalfWidth() => GetVisualBoxSize().x * 0.5f;
    public float GetVisualHalfHeight() => GetVisualBoxSize().y * 0.5f;

    /// <summary>
    /// Static equivalent for layout pre-pass (matches ApplyVisualType fill sizing: root × 0.75).
    /// </summary>
    public static Vector2 GetVisualBoxSize(SkillTreeNodeVisualType type)
    {
        return GetVisualSize(type) * 0.75f;
    }

    public void SetLevelText(string text)
    {
        // Level labels are now displayed in a separate left-side column (not on each node).
    }

    public void SetTypeText(string text)
    {
        // Labels disabled for now (tooltips later).
    }

    public void SetClick(System.Action onClick)
    {
        onClickAction = onClick;

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }
    }

    /// <summary>
    /// Skill tree only: show the + control and route clicks to the view. Hidden when <paramref name="visible"/> is false.
    /// </summary>
    public void SetEnhanceControl(bool visible, UnityEngine.Events.UnityAction onEnhanceClicked)
    {
        if (enhanceButton == null)
            return;

        enhanceButton.onClick.RemoveAllListeners();
        if (visible && onEnhanceClicked != null)
        {
            enhanceButton.gameObject.SetActive(true);
            enhanceButton.onClick.AddListener(onEnhanceClicked);
        }
        else
        {
            enhanceButton.gameObject.SetActive(false);
        }
    }

    public void SetRightClick(System.Action onRightClick)
    {
        onRightClickAction = onRightClick;
    }

    public void SetHover(System.Action enter, System.Action exit)
    {
        onHoverEnter = enter;
        onHoverExit = exit;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ClearUnlockGlow();
        onHoverEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onHoverExit?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (isLocked)
                return;
            onRightClickAction?.Invoke();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (isLocked)
            return;

        // Fallback click path when Button reference is missing or misconfigured.
        onClickAction?.Invoke();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshSelectionChrome();
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;

        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(locked);
        }

        if (button != null)
        {
            button.interactable = !locked;
        }

        if (enhanceButton != null)
            enhanceButton.interactable = !locked;

        if (locked && cooldownOverlay != null)
            cooldownOverlay.SetActive(false);

        if (locked && activeBuffOverlay != null)
            activeBuffOverlay.SetActive(false);

        RefreshSelectionChrome();
        RefreshLockedPresentation();
        SyncButtonHitAndRaycasts();
    }

    private void RefreshLockedPresentation()
    {
        if (_fillOutline == null && fillImage != null)
            _fillOutline = fillImage.GetComponent<Outline>();
        if (_fillOutline != null && !_cachedUnlockedOutline)
        {
            _unlockedOutlineEffect = _fillOutline.effectColor;
            _cachedUnlockedOutline = true;
        }

        if (fillImage != null)
        {
            if (_useColoredFillBackground)
                fillImage.color = isLocked ? LockedTintFill(_unlockedFillColor) : _unlockedFillColor;
            else
                fillImage.color = new Color(0f, 0f, 0f, 0f);
        }

        if (iconImage != null && iconImage.gameObject.activeSelf && iconImage.sprite != null)
            iconImage.color = isLocked ? LockedTintIcon(_unlockedIconColor) : _unlockedIconColor;

        if (_fillOutline != null)
        {
            if (_useColoredFillBackground)
                _fillOutline.effectColor = isLocked ? LockedTintIcon(_unlockedOutlineEffect) : _unlockedOutlineEffect;
            else
                _fillOutline.effectColor = new Color(_unlockedOutlineEffect.r, _unlockedOutlineEffect.g, _unlockedOutlineEffect.b, 0f);
        }

        if (lockedOverlay != null)
        {
            Image overlayImg = lockedOverlay.GetComponent<Image>();
            if (overlayImg != null)
            {
                Color c = overlayImg.color;
                overlayImg.color = isLocked
                    ? new Color(c.r, c.g, c.b, LockedOverlayAlpha)
                    : _defaultLockedOverlayColor;
            }
        }

        if (outerRingImage != null)
            outerRingImage.color = isLocked ? LockedTintIcon(_unlockedRingColor) : _unlockedRingColor;
    }

    private static Color LockedTintFill(Color c)
    {
        return new Color(
            c.r * LockedFillRgbScale,
            c.g * LockedFillRgbScale,
            c.b * LockedFillRgbScale,
            c.a * LockedFillAlphaScale);
    }

    private static Color LockedTintIcon(Color c)
    {
        return new Color(
            c.r * LockedIconRgbScale,
            c.g * LockedIconRgbScale,
            c.b * LockedIconRgbScale,
            c.a * LockedIconAlphaScale);
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public bool IsSelected()
    {
        return isSelected;
    }

    private bool _skillTreeCooldownVisible;
    private float _skillTreeCooldownFill = -1f;
    private int _skillTreeCooldownSecondsShown = -1;
    private bool _skillTreeBuffVisible;
    private int _skillTreeBuffSecondsShown = -1;

    /// <summary>Skill tree only: cooldown ring/text from <see cref="PlayerAbilityController"/>.</summary>
    public void SetSkillTreeCooldownPresentation(bool visible, float normalizedCooldown01, float remainingSeconds)
    {
        bool show = visible && !isLocked;
        int secondsShown = show && remainingSeconds >= 1f ? Mathf.CeilToInt(remainingSeconds) : -1;
        float fill = show ? Mathf.Clamp01(normalizedCooldown01) : 0f;

        // Only skip work while actively showing the same values. Never skip hiding — prefab defaults and
        // stale UI must still be cleared when show is false (cache can be false before first apply).
        if (show &&
            _skillTreeCooldownVisible &&
            Mathf.Approximately(_skillTreeCooldownFill, fill) &&
            _skillTreeCooldownSecondsShown == secondsShown)
            return;

        _skillTreeCooldownVisible = show;
        _skillTreeCooldownFill = fill;
        _skillTreeCooldownSecondsShown = secondsShown;

        if (cooldownOverlay != null)
            cooldownOverlay.SetActive(show);

        if (cooldownOverlayFillImage != null)
        {
            bool showFill = show && fill > 0f;
            cooldownOverlayFillImage.enabled = showFill;
            if (showFill)
            {
                if (cooldownOverlayFillImage.type != Image.Type.Filled)
                    cooldownOverlayFillImage.type = Image.Type.Filled;
                cooldownOverlayFillImage.fillAmount = fill;
            }
        }

        if (cooldownOverlayTimeText != null)
            cooldownOverlayTimeText.text = secondsShown >= 1 ? secondsShown.ToString() : string.Empty;
    }

    /// <summary>Skill tree only: active buff / lingering effect (cooldown overlay takes priority when both apply).</summary>
    public void SetSkillTreeActiveBuffPresentation(bool visible, float buffRemainingSeconds)
    {
        bool show = visible && !isLocked;
        int secondsShown = show && buffRemainingSeconds >= 1f ? Mathf.CeilToInt(buffRemainingSeconds) : -1;

        if (show &&
            _skillTreeBuffVisible &&
            _skillTreeBuffSecondsShown == secondsShown)
            return;

        _skillTreeBuffVisible = show;
        _skillTreeBuffSecondsShown = secondsShown;

        if (activeBuffOverlay != null)
            activeBuffOverlay.SetActive(show);

        if (activeBuffOverlayTimeText != null)
            activeBuffOverlayTimeText.text = secondsShown >= 1 ? secondsShown.ToString() : string.Empty;
    }

    /// <summary>Reset presentation caches when a node is spawned/reused so prefab defaults do not stick.</summary>
    public void ResetSkillTreePresentationCache()
    {
        _skillTreeCooldownVisible = false;
        _skillTreeCooldownFill = -1f;
        _skillTreeCooldownSecondsShown = -1;
        _skillTreeBuffVisible = false;
        _skillTreeBuffSecondsShown = -1;
    }

    public void SetIcon(Sprite sprite, bool visible)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.gameObject.SetActive(visible && sprite != null);
        if (visible && sprite != null)
            _unlockedIconColor = Color.white;

        RefreshLockedPresentation();
        FitIconToNode();
        SyncButtonHitAndRaycasts();
    }

    public void ApplyVisualType(SkillTreeNodeVisualType type)
    {
        appliedVisualType = type;
        ResetMinorPassiveSelectionVisualBoost();
        if (_fillOutline == null && fillImage != null)
            _fillOutline = fillImage.GetComponent<Outline>();

        Color color = minorPassiveColor;
        Vector2 rootSize = GetVisualSize(type);
        bool showIcon = false;
        bool forceColoredFillBackground = false;

        switch (type)
        {
            case SkillTreeNodeVisualType.MinorPassive:
                color = minorPassiveColor;
                break;

            case SkillTreeNodeVisualType.MinorUnlock:
                color = minorUnlockColor;
                break;

            case SkillTreeNodeVisualType.MajorPassive:
                color = majorPassiveColor;
                showIcon = true;
                break;

            case SkillTreeNodeVisualType.Unlock:
                color = unlockColor;
                showIcon = true;
                // Keep unlock milestones on the original black inner fill for readability.
                forceColoredFillBackground = true;
                break;

            case SkillTreeNodeVisualType.Ability:
                color = abilityColor;
                showIcon = true;
                break;

            case SkillTreeNodeVisualType.Choice:
                color = choiceColor;
                showIcon = true;
                break;

            case SkillTreeNodeVisualType.CapstonePassive:
                color = capstonePassiveColor;
                showIcon = true;
                break;
        }

        _useColoredFillBackground = forceColoredFillBackground || !showIcon;

        RectTransform.sizeDelta = rootSize;

        if (outerRingImage != null)
        {
            RectTransform rt = outerRingImage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = rootSize;
            _baseOuterRingSize = rt.sizeDelta;
            outerRingImage.color = _unlockedRingColor;
        }

        _unlockedFillColor = color;

        if (fillImage != null)
        {
            RectTransform rt = fillImage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = rootSize * 0.75f;

            if (_useColoredFillBackground)
            {
                fillImage.color = color;
            }
            else
            {
                fillImage.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        if (iconImage != null)
        {
            iconImage.color = Color.white;
            _unlockedIconColor = Color.white;
            FitIconToNode();
            iconImage.gameObject.SetActive(showIcon && iconImage.sprite != null);
        }

        if (lockedOverlay != null)
        {
            RectTransform rt = lockedOverlay.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = fillImage != null ? fillImage.rectTransform.sizeDelta : (rootSize * 0.75f);
            }
        }

        if (levelText != null)
        {
            levelText.gameObject.SetActive(false);
            levelText.text = string.Empty;
        }

        if (typeText != null)
        {
            typeText.gameObject.SetActive(false);
            typeText.text = string.Empty;
        }

        // Side labels disabled for now (tooltips later).
        RefreshLockedPresentation();
        SyncButtonHitAndRaycasts();
    }

    private void ResetMinorPassiveSelectionVisualBoost()
    {
        if (fillImage != null)
            fillImage.rectTransform.localScale = Vector3.one;
        if (iconImage != null)
            iconImage.rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Match the invisible <see cref="Button"/> rect to the inner <see cref="fillImage"/> box so hover/tooltips
    /// do not use the full root padding. Decorative images do not enlarge the hit area.
    /// </summary>
    private void SyncButtonHitAndRaycasts()
    {
        if (button == null)
            return;

        RectTransform btnRt = button.transform as RectTransform;
        if (btnRt == null || fillImage == null)
            return;

        RectTransform fillRt = fillImage.rectTransform;
        btnRt.anchorMin = fillRt.anchorMin;
        btnRt.anchorMax = fillRt.anchorMax;
        btnRt.pivot = fillRt.pivot;
        btnRt.anchoredPosition = fillRt.anchoredPosition;
        btnRt.sizeDelta = fillRt.sizeDelta;

        fillImage.raycastTarget = false;
        if (iconImage != null)
            iconImage.raycastTarget = false;
        if (outerRingImage != null)
            outerRingImage.raycastTarget = false;

        if (selectedBorderOverlay != null)
        {
            Image borderImg = selectedBorderOverlay.GetComponent<Image>();
            if (borderImg != null)
                borderImg.raycastTarget = false;
        }

        Image btnImg = button.GetComponent<Image>();
        if (btnImg != null)
        {
            Color c = btnImg.color;
            c.a = 0f;
            btnImg.color = c;
            btnImg.raycastTarget = true;
        }
    }

    private void LayoutSideLabels(Vector2 rootSize, bool showSideLabels)
    {
        if (!showSideLabels)
            return;

        float halfW = Mathf.Abs(RectTransform.rect.width) * 0.5f;
        if (halfW <= 0.01f)
            halfW = rootSize.x * 0.5f;

        float pad = SideLabelPadding + rootSize.x * SideLabelPaddingWidthFactor;
        float yOffset = appliedVisualType == SkillTreeNodeVisualType.CapstonePassive ? CapstoneLabelYOffset : 0f;

        if (levelText != null)
        {
            RectTransform rt = levelText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(SideLabelWidth, Mathf.Max(rt.sizeDelta.y, 32f));
            levelText.alignment = TextAlignmentOptions.MidlineRight;
            levelText.textWrappingMode = TextWrappingModes.Normal;
            levelText.overflowMode = TextOverflowModes.Overflow;
            rt.anchoredPosition = new Vector2(-halfW - pad, yOffset);
        }

        if (typeText != null)
        {
            RectTransform rt = typeText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(SideLabelWidth, Mathf.Max(rt.sizeDelta.y, 32f));
            typeText.alignment = TextAlignmentOptions.MidlineLeft;
            typeText.textWrappingMode = TextWrappingModes.Normal;
            typeText.overflowMode = TextOverflowModes.Overflow;
            rt.anchoredPosition = new Vector2(halfW + pad, yOffset);
        }
    }

    private void FitIconToNode()
    {
        if (iconImage == null)
            return;

        RectTransform iconRt = iconImage.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;

        Vector2 baseSize = fillImage != null ? fillImage.rectTransform.sizeDelta : RectTransform.sizeDelta;
        // Full inner fill size so prefab/scene serialized overrides cannot shrink ability icons.
        if (appliedVisualType == SkillTreeNodeVisualType.MinorUnlock)
        {
            const float minorUnlockIconScale = 0.78f;
            iconRt.sizeDelta = baseSize * minorUnlockIconScale;
            iconImage.preserveAspect = true;
        }
        else
        {
            iconRt.sizeDelta = baseSize;
            // Minor passive spine gems: fill the tile (non-square sprites stretch slightly instead of letterboxing).
            bool minorPassive = appliedVisualType == SkillTreeNodeVisualType.MinorPassive;
            iconImage.preserveAspect = !minorPassive;
        }
    }

    private void RefreshSelectionChrome()
    {
        bool show = ShouldShowSelectedGlow();

        if (selectedBorderOverlay != null)
            selectedBorderOverlay.SetActive(show);

        bool minorPassive = appliedVisualType == SkillTreeNodeVisualType.MinorPassive;
        bool minorSpine =
            appliedVisualType == SkillTreeNodeVisualType.MinorPassive ||
            appliedVisualType == SkillTreeNodeVisualType.MinorUnlock;
        if (minorSpine && fillImage != null)
        {
            float boost = minorPassiveSelectionFillScaleWhenBorderShown;
            // Prefabs that still store the old "no zoom" default (~1) get a sensible fill-in-border scale.
            if (show && boost <= 1.02f)
                boost = 1f / 0.75f;
            float fillBoost = show ? Mathf.Max(1f, boost) : 1f;
            fillImage.rectTransform.localScale = Vector3.one * fillBoost;
            if (iconImage != null)
                iconImage.rectTransform.localScale = Vector3.one * fillBoost;
        }

        float effBorderThickness = minorPassive ? minorPassiveSelectedBorderThickness : selectedBorderThickness;
        float effGlowPad = minorPassive ? minorPassiveSelectedGlowPaddingCompensation : selectedGlowPaddingCompensation;

        bool hasGlowObject = selectedGlow != null;
        if (hasGlowObject)
        {
            RectTransform glowRt = selectedGlow.GetComponent<RectTransform>();
            if (glowRt != null)
            {
                glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
                glowRt.pivot = new Vector2(0.5f, 0.5f);
                glowRt.anchoredPosition = Vector2.zero;

                Vector2 baseSize;
                if (outerRingImage != null)
                    baseSize = outerRingImage.rectTransform.sizeDelta;
                else if (fillImage != null)
                    baseSize = fillImage.rectTransform.sizeDelta;
                else
                    baseSize = RectTransform.sizeDelta;
                float perSide = Mathf.Max(0f, effBorderThickness) + Mathf.Max(0f, effGlowPad);
                float extra = perSide * 2f;
                glowRt.sizeDelta = baseSize + new Vector2(extra, extra);
            }

            Image glowImage = selectedGlow.GetComponent<Image>();
            if (glowImage != null)
            {
                glowImage.color = selectedOutlineColor;
                glowImage.raycastTarget = false;
                if (glowImage.type == Image.Type.Simple)
                    glowImage.type = Image.Type.Sliced;
            }

            selectedGlow.SetActive(show);
        }

        bool selectionOrnamentCoversFillOutline = show && (hasGlowObject || selectedBorderOverlay != null);
        if (_fillOutline != null)
            _fillOutline.enabled = _useColoredFillBackground && !selectionOrnamentCoversFillOutline;

        if (!hasGlowObject)
            ApplyOuterRingSelectionSizing(show, effBorderThickness);

        if (minorSpine)
            SyncButtonHitAndRaycasts();
    }

    private void ApplyOuterRingSelectionSizing(bool showSelection, float thicknessForRing)
    {
        if (outerRingImage == null)
            return;

        RectTransform rt = outerRingImage.rectTransform;
        if (showSelection)
        {
            float extra = Mathf.Max(0f, thicknessForRing * 2f);
            rt.sizeDelta = _baseOuterRingSize + new Vector2(extra, extra);
        }
        else
            rt.sizeDelta = _baseOuterRingSize;
    }

    /// <summary>
    /// Minor passive / minor unlock: selection border/glow only when unlocked and those objects exist on the prefab.
    /// Major passive / unlock / capstone: border whenever the node is unlocked at level. Ability / choice: committed or active pick.
    /// </summary>
    private bool ShouldShowSelectedGlow()
    {
        switch (appliedVisualType)
        {
            case SkillTreeNodeVisualType.MinorPassive:
            case SkillTreeNodeVisualType.MinorUnlock:
                // Selection chrome (border/glow) optional on prefab; avoid driving fill scale with no art — it breaks vertical spacing vs locked rows.
                return !isLocked && (selectedBorderOverlay != null || selectedGlow != null);

            case SkillTreeNodeVisualType.MajorPassive:
            case SkillTreeNodeVisualType.Unlock:
            case SkillTreeNodeVisualType.CapstonePassive:
                return !isLocked;

            case SkillTreeNodeVisualType.Choice:
            case SkillTreeNodeVisualType.Ability:
                return isSelected;

            default:
                return isSelected && !isLocked;
        }
    }
}
