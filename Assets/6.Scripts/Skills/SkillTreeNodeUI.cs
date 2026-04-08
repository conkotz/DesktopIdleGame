using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class SkillTreeNodeUI : MonoBehaviour
{
    /// <summary>Minimum gap between node edge and the inner edge of the side labels (pixels).</summary>
    private const float SideLabelPadding = 2f;
    /// <summary>Extra gap proportional to node width so larger tiers stay visually clear of the square.</summary>
    private const float SideLabelPaddingWidthFactor = 0.02f;
    /// <summary>Width of the side label rects (kept narrow; long labels can be multi-line).</summary>
    private const float SideLabelWidth = 80f;
    private const float CapstoneLabelYOffset = 12f;

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

    [Header("Colors")]
    [FormerlySerializedAs("minorColor")]
    [SerializeField] private Color minorPassiveColor = new Color(0.72f, 0.33f, 0.33f);
    [FormerlySerializedAs("passiveColor")]
    [SerializeField] private Color majorPassiveColor = new Color(0.78f, 0.62f, 0.26f);
    [SerializeField] private Color unlockColor = new Color(0.28f, 0.55f, 0.62f);
    [SerializeField] private Color abilityColor = new Color(0.82f, 0.42f, 0.18f);
    [FormerlySerializedAs("branchColor")]
    [SerializeField] private Color choiceColor = new Color(0.26f, 0.53f, 0.82f);
    [FormerlySerializedAs("capstoneColor")]
    [SerializeField] private Color capstonePassiveColor = new Color(0.58f, 0.32f, 0.76f);

    [Header("Sizes")]
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

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    /// <summary>
    /// Shared source of truth for node visual sizes.
    /// Use this from layout code to keep spacing in sync with ApplyVisualType().
    /// </summary>
    public static Vector2 GetVisualSize(SkillTreeNodeVisualType type)
    {
        return type switch
        {
            SkillTreeNodeVisualType.MinorPassive => MINOR_PASSIVE_SIZE,
            SkillTreeNodeVisualType.MajorPassive => MAJOR_PASSIVE_SIZE,
            SkillTreeNodeVisualType.Unlock => UNLOCK_SIZE,
            SkillTreeNodeVisualType.Ability => ABILITY_SIZE,
            SkillTreeNodeVisualType.Choice => CHOICE_SIZE,
            SkillTreeNodeVisualType.CapstonePassive => CAPSTONE_PASSIVE_SIZE,
            _ => MINOR_PASSIVE_SIZE
        };
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
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selectedGlow != null)
        {
            selectedGlow.SetActive(selected);
        }
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
    }

    public void ApplyVisualType(SkillTreeNodeVisualType type)
    {
        appliedVisualType = type;

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
                break;

            case SkillTreeNodeVisualType.Ability:
                color = abilityColor;
                showIcon = true;
                break;

            case SkillTreeNodeVisualType.Choice:
                color = choiceColor;
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
        }

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
            iconImage.gameObject.SetActive(showIcon && iconImage.sprite != null);

        if (lockedOverlay != null)
            lockedOverlay.GetComponent<RectTransform>().sizeDelta = rootSize;

        if (selectedGlow != null)
            selectedGlow.GetComponent<RectTransform>().sizeDelta = rootSize + new Vector2(2f, 2f);

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
}
