using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillTreeNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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
    [SerializeField] private GameObject selectedGlow;
    [SerializeField] private Button button;
    [SerializeField] private Color selectedOutlineColor = new Color(1f, 0.84f, 0.2f, 1f);
    [SerializeField] private float selectedBorderThickness = 4f;
    [SerializeField] private float selectedGlowPaddingCompensation = 4f;

    [Header("Colors")]
    [FormerlySerializedAs("minorColor")]
    [SerializeField] private Color minorPassiveColor = new Color(0.72f, 0.33f, 0.33f);
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
    private UIPulseGlowOverlay _unlockGlow;

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
    /// Static equivalent for layout pre-pass (matches ApplyVisualType Fill sizing: root * 0.75).
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
        ApplySelectedOutlineFallback();
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

        ApplySelectedOutlineFallback();
        RefreshLockedPresentation();
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
            fillImage.color = isLocked ? LockedTintFill(_unlockedFillColor) : _unlockedFillColor;

        if (iconImage != null && iconImage.gameObject.activeSelf && iconImage.sprite != null)
            iconImage.color = isLocked ? LockedTintIcon(_unlockedIconColor) : _unlockedIconColor;

        if (_fillOutline != null)
            _fillOutline.effectColor = isLocked ? LockedTintIcon(_unlockedOutlineEffect) : _unlockedOutlineEffect;

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

    public void SetIcon(Sprite sprite, bool visible)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.gameObject.SetActive(visible && sprite != null);
        if (visible && sprite != null)
            _unlockedIconColor = Color.white;

        RefreshLockedPresentation();
    }

    public void ApplyVisualType(SkillTreeNodeVisualType type)
    {
        appliedVisualType = type;
        if (_fillOutline == null && fillImage != null)
            _fillOutline = fillImage.GetComponent<Outline>();

        Color color = minorPassiveColor;
        Vector2 rootSize = GetVisualSize(type);
        bool showIcon = false;

        switch (type)
        {
            case SkillTreeNodeVisualType.MinorPassive:
                color = minorPassiveColor;
                break;

            case SkillTreeNodeVisualType.MajorPassive:
                color = majorPassiveColor;
                showIcon = true;
                break;

            case SkillTreeNodeVisualType.Unlock:
                color = unlockColor;
                showIcon = true;
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
            fillImage.color = color;
            RectTransform rt = fillImage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = rootSize * 0.75f;
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

        UpdateSelectedGlowBorder();

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
        ApplySelectedOutlineFallback();
        RefreshLockedPresentation();
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
        // Force full inner fill size so prefab/scene serialized overrides cannot shrink ability icons.
        iconRt.sizeDelta = baseSize;
        iconImage.preserveAspect = true;
    }

    private void ApplySelectedOutlineFallback()
    {
        if (selectedGlow != null)
        {
            UpdateSelectedGlowBorder();
            return;
        }

        if (outerRingImage != null)
        {
            RectTransform rt = outerRingImage.rectTransform;
            if (isSelected)
            {
                float extra = Mathf.Max(0f, selectedBorderThickness * 2f);
                rt.sizeDelta = _baseOuterRingSize + new Vector2(extra, extra);
            }
            else
            {
                rt.sizeDelta = _baseOuterRingSize;
            }
        }
    }

    private void UpdateSelectedGlowBorder()
    {
        if (selectedGlow == null)
            return;

        bool showGlow = ShouldShowSelectedGlow();

        RectTransform glowRt = selectedGlow.GetComponent<RectTransform>();
        if (glowRt != null)
        {
            glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.pivot = new Vector2(0.5f, 0.5f);
            glowRt.anchoredPosition = Vector2.zero;

            // Match the *visible* node box:
            // - prefer outer ring when present
            // - otherwise use fill (common current prefab setup)
            Vector2 baseSize;
            if (outerRingImage != null)
                baseSize = outerRingImage.rectTransform.sizeDelta;
            else if (fillImage != null)
                baseSize = fillImage.rectTransform.sizeDelta;
            else
                baseSize = RectTransform.sizeDelta;
            // Some border sprites have internal inset/padding; compensate so border wraps outside the node.
            float perSide = Mathf.Max(0f, selectedBorderThickness) + Mathf.Max(0f, selectedGlowPaddingCompensation);
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

        selectedGlow.SetActive(showGlow);
        if (_fillOutline != null)
            _fillOutline.enabled = !showGlow;
    }

    /// <summary>
    /// Choice / ability: glow only when <see cref="isSelected"/> (committed pick or active choice). Other milestones: glow when unlocked and/or tree-selected.
    /// </summary>
    private bool ShouldShowSelectedGlow()
    {
        switch (appliedVisualType)
        {
            case SkillTreeNodeVisualType.Choice:
            case SkillTreeNodeVisualType.Ability:
                return isSelected;
            case SkillTreeNodeVisualType.MajorPassive:
            case SkillTreeNodeVisualType.Unlock:
            case SkillTreeNodeVisualType.CapstonePassive:
                return !isLocked || isSelected;
            default:
                return isSelected;
        }
    }
}
