using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Reusable timeline skill-tree node chrome. Visual-only — no save/unlock/skill-point wiring.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class SkillTimelineNodeUI : MonoBehaviour
{
    public enum SkillTimelineNodeType
    {
        Ability,
        MinorPassive,
        MajorPassive,
        Unlock,
        Capstone
    }

    public enum SkillTimelineNodeState
    {
        Locked,
        Available,
        Unlocked,
        Selected
    }

    public const float StandardNodeButtonSize = 56f;
    public static readonly float StandardNodeHalfHeight = StandardNodeButtonSize * 0.5f;
    public const float SpineDiamondSize = 17f;
    public const float MinorPassiveIconDisplaySize = 28f;

    private static readonly Vector2 StandardNodeSize = new(StandardNodeButtonSize, StandardNodeButtonSize);
    private static readonly Vector2 StandardIconSize = new(42f, 42f);
    private static readonly Vector2 MinorNodeSize = new(28f, 28f);
    private static readonly Vector2 MinorIconSize = new(MinorPassiveIconDisplaySize, MinorPassiveIconDisplaySize);
    private static readonly Vector2 StandardTypeDiamondSize = new(14f, 14f);
    private static readonly Vector2 MinorTypeDiamondSize = new(10f, 10f);

    [Header("References")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Button rootButton;
    [SerializeField] private Image background;
    [SerializeField] private Outline borderOutline;
    [SerializeField] private Image icon;
    [SerializeField] private Image minorIcon;
    [SerializeField] private Image typeDiamond;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private GameObject selectedGlow;
    [SerializeField] private GameObject checkmark;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text nameLabelUnlocks;

    [Header("Type Icon Sprites")]
    [SerializeField] private Sprite abilityIconSprite;
    [SerializeField] private Sprite minorPassiveIconSprite;
    [SerializeField] private Sprite majorPassiveIconSprite;
    [SerializeField] private Sprite unlockIconSprite;
    [SerializeField] private Sprite capstoneIconSprite;

    [Header("Type Colors")]
    [SerializeField] private Color abilityColor = new(0.22f, 0.62f, 0.38f, 1f);
    [SerializeField] private Color minorPassiveColor = new(0.26f, 0.53f, 0.82f, 1f);
    [SerializeField] private Color majorPassiveColor = new(0.58f, 0.32f, 0.76f, 1f);
    [SerializeField] private Color unlockColor = new(0.95f, 0.78f, 0.22f, 1f);
    [SerializeField] private Color capstoneColor = new(0.78f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color backgroundTint = new(0.12f, 0.1f, 0.08f, 0.92f);
    [Tooltip("When true, hides the dark node plate so icons/diamonds read clearly.")]
    [SerializeField] private bool hideHeavyBackground = false;

    [Header("State Colors")]
    [SerializeField] private Color lockedOverlayColor = new(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color lockedDimMultiplier = new(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color availableBorderColor = new(0.35f, 0.3f, 0.24f, 0.85f);
    [SerializeField] private Color selectedBorderColor = new(1f, 0.84f, 0.2f, 1f);
    [SerializeField] private Color selectedGlowColor = new(1f, 0.84f, 0.2f, 0.45f);

    [Header("Editor Preview")]
    [SerializeField] private bool applyPreviewInEditor = true;
    [SerializeField] private SkillTimelineNodeType previewNodeType = SkillTimelineNodeType.Ability;
    [SerializeField] private SkillTimelineNodeState previewState = SkillTimelineNodeState.Available;
    [SerializeField] private bool previewMinorPassiveLayout;
    [SerializeField] private string previewDisplayName = "Node Name";

    private SkillTimelineNodeType _appliedType;
    private SkillTimelineNodeState _appliedState;
    private bool _appliedMinorLayout;
    private Color _baseBackgroundColor = Color.white;
    private Color _baseIconColor = Color.white;

#if UNITY_EDITOR
    private bool _deferredPreviewQueued;
#endif

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;
    public Button RootButton => rootButton;

    private void Awake() => CacheBaseColors();

    private void OnEnable()
    {
        CacheBaseColors();
        if (!applyPreviewInEditor)
            return;

        if (Application.isPlaying)
            ApplyPresentation(previewNodeType, previewState, previewDisplayName, previewMinorPassiveLayout, spineDiamondOnly: false, hideNameLabel: false);
        else
            RequestEditorPreviewApply();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        CancelDeferredEditorPreview();
#endif
    }

    private void OnValidate()
    {
        if (!applyPreviewInEditor)
            return;
        RequestEditorPreviewApply();
    }

    [ContextMenu("Apply Editor Preview")]
    public void ApplyEditorPreviewFromInspector()
    {
        ApplyPreview(previewNodeType, previewState, previewDisplayName, previewMinorPassiveLayout);
    }

#if UNITY_EDITOR
    [ContextMenu("Assign Icons From NodeIconsSpritesheet")]
    private void AssignIconsFromNodeIconsSpritesheet()
    {
        const string sheetPath = "Assets/5.Art/Sprites/SkillsPageIcons/NodeIconsSpritesheet.png";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning($"[SkillTimelineNodeUI] No assets at {sheetPath}", this);
            return;
        }

        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
                continue;

            if (sprite.name.Contains("ability"))
                abilityIconSprite = sprite;
            else if (sprite.name.Contains("minor_passive"))
                minorPassiveIconSprite = sprite;
            else if (sprite.name.Contains("major_passive"))
                majorPassiveIconSprite = sprite;
            else if (sprite.name.Contains("unlock"))
                unlockIconSprite = sprite;
            else if (sprite.name.Contains("capstone"))
                capstoneIconSprite = sprite;
        }

        EditorUtility.SetDirty(this);
        ApplyEditorPreviewFromInspector();
    }
#endif

    /// <summary>Visual-only presentation for scaffold / future timeline builder.</summary>
    public void ApplyPreview(
        SkillTimelineNodeType nodeType,
        SkillTimelineNodeState state,
        string displayName,
        bool minorPassiveLayout,
        bool spineDiamondOnly = false,
        bool hideNameLabel = false)
    {
        previewNodeType = nodeType;
        previewState = state;
        previewDisplayName = displayName ?? string.Empty;
        previewMinorPassiveLayout = minorPassiveLayout;
        ApplyPresentation(nodeType, state, displayName, minorPassiveLayout, spineDiamondOnly, hideNameLabel);
    }

    /// <summary>Choice-group row node with per-node title under the icon.</summary>
    public void ApplyChoiceGroupNodePreview(
        SkillTimelineNodeType nodeType,
        string displayName,
        SkillTimelineNodeState state = SkillTimelineNodeState.Available)
    {
        ApplyPreview(nodeType, state, displayName, minorPassiveLayout: false, hideNameLabel: false);
        EnableTimelineNodeBackground(nodeType);
        ConfigureChoiceGroupNameLabel(displayName);
        SetChildActive(selectedGlow, false);
    }

    /// <summary>Unlock row node: label above icon, centered on milestone X.</summary>
    public void ApplyUnlockTimelinePreview(
        string displayName,
        SkillTimelineNodeState state = SkillTimelineNodeState.Available)
    {
        ApplyPreview(SkillTimelineNodeType.Unlock, state, displayName, minorPassiveLayout: false, hideNameLabel: false);
        EnableTimelineNodeBackground(SkillTimelineNodeType.Unlock);
        ConfigureUnlockNameLabel(displayName);
    }

    /// <summary>Small rotated diamond on the timeline spine (no card chrome).</summary>
    public void ApplySpineDiamondPreview(SkillTimelineNodeState state = SkillTimelineNodeState.Available)
    {
        ApplyPreview(SkillTimelineNodeType.MinorPassive, state, string.Empty, minorPassiveLayout: false, spineDiamondOnly: true);
    }

#if UNITY_EDITOR
    private void RequestEditorPreviewApply()
    {
        if (_deferredPreviewQueued)
            return;
        _deferredPreviewQueued = true;
        EditorApplication.delayCall += RunDeferredEditorPreview;
    }

    private void CancelDeferredEditorPreview()
    {
        EditorApplication.delayCall -= RunDeferredEditorPreview;
        _deferredPreviewQueued = false;
    }

    private void RunDeferredEditorPreview()
    {
        EditorApplication.delayCall -= RunDeferredEditorPreview;
        _deferredPreviewQueued = false;
        if (this == null || !applyPreviewInEditor)
            return;
        ApplyPresentation(previewNodeType, previewState, previewDisplayName, previewMinorPassiveLayout, spineDiamondOnly: false, hideNameLabel: false);
    }
#endif

    private void ApplyPresentation(
        SkillTimelineNodeType nodeType,
        SkillTimelineNodeState state,
        string displayName,
        bool minorPassiveLayout,
        bool spineDiamondOnly,
        bool hideNameLabel)
    {
        _appliedType = nodeType;
        _appliedState = state;
        _appliedMinorLayout = minorPassiveLayout;

        EnsureReferences();
        CacheBaseColors();

        if (spineDiamondOnly)
        {
            ApplySpineDiamondOnlyPresentation(nodeType, state);
            return;
        }

        ApplyLayout(minorPassiveLayout);
        ApplyTypeVisuals(nodeType, minorPassiveLayout);
        ApplyStateVisuals(state);
        ApplyDisplayName(displayName, minorPassiveLayout, hideNameLabel);
    }

    private void ApplySpineDiamondOnlyPresentation(SkillTimelineNodeType nodeType, SkillTimelineNodeState state)
    {
        Vector2 diamondSize = new(SpineDiamondSize, SpineDiamondSize);
        SetSizeDeltaIfChanged(rectTransform, diamondSize);

        if (rootButton != null)
        {
            RectTransform buttonRt = rootButton.transform as RectTransform;
            SetSizeDeltaIfChanged(buttonRt, diamondSize);
            rootButton.interactable = state != SkillTimelineNodeState.Locked;
        }

        SetChildActive(background, false);
        SetChildActive(borderOutline, false);
        SetChildActive(icon, false);
        SetChildActive(lockedOverlay, false);
        SetChildActive(selectedGlow, false);
        SetChildActive(checkmark, false);

        if (nameLabel != null)
            nameLabel.gameObject.SetActive(false);

        if (nameLabelUnlocks != null)
            nameLabelUnlocks.gameObject.SetActive(false);

        Color accent = GetTypeColor(nodeType);
        Sprite typeSprite = GetTypeIconSprite(nodeType);
        SetChildActive(typeDiamond, false);
        ApplyMinorIconVisual(minorIcon, typeSprite, accent, diamondSize, rotateFallbackDiamond: false);
    }

    private static void SetChildActive(Component component, bool active)
    {
        if (component == null)
            return;
        component.gameObject.SetActive(active);
    }

    private static void SetChildActive(GameObject go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }

    private void EnsureReferences()
    {
        if (rectTransform == null)
            rectTransform = RectTransform;

        if (rootButton == null)
            rootButton = GetComponentInChildren<Button>(true);

        if (background == null)
            background = transform.Find("RootButton/Background")?.GetComponent<Image>();

        if (borderOutline == null)
            borderOutline = transform.Find("RootButton/Border")?.GetComponent<Outline>();

        if (icon == null)
            icon = transform.Find("RootButton/Icon")?.GetComponent<Image>();

        if (minorIcon == null)
            minorIcon = transform.Find("RootButton/MinorIcon")?.GetComponent<Image>();

        if (typeDiamond == null)
            typeDiamond = transform.Find("RootButton/TypeDiamond")?.GetComponent<Image>();

        if (lockedOverlay == null)
        {
            Transform t = transform.Find("RootButton/LockedOverlay");
            if (t != null)
                lockedOverlay = t.gameObject;
        }

        if (selectedGlow == null)
        {
            Transform t = transform.Find("RootButton/SelectedGlow");
            if (t != null)
                selectedGlow = t.gameObject;
        }

        if (checkmark == null)
        {
            Transform t = transform.Find("RootButton/Checkmark");
            if (t != null)
                checkmark = t.gameObject;
        }

        if (nameLabel == null)
            nameLabel = transform.Find("NameLabel")?.GetComponent<TMP_Text>();

        if (nameLabelUnlocks == null)
            nameLabelUnlocks = transform.Find("NameLabelUnlocks")?.GetComponent<TMP_Text>();
    }

    private void CacheBaseColors()
    {
        if (background != null)
            _baseBackgroundColor = backgroundTint;

        if (icon != null && icon.color.a > 0.01f)
            _baseIconColor = Color.white;
    }

    private void ApplyLayout(bool minorPassiveLayout)
    {
        RectTransform buttonRt = rootButton != null ? rootButton.transform as RectTransform : null;
        Vector2 nodeSize = minorPassiveLayout ? MinorNodeSize : StandardNodeSize;
        Vector2 iconSize = minorPassiveLayout ? MinorIconSize : StandardIconSize;
        Vector2 diamondSize = minorPassiveLayout ? MinorTypeDiamondSize : StandardTypeDiamondSize;

        SetSizeDeltaIfChanged(buttonRt, nodeSize);

        if (icon != null)
        {
            SetSizeDeltaIfChanged(icon.rectTransform, iconSize);
            icon.gameObject.SetActive(!minorPassiveLayout || _appliedType != SkillTimelineNodeType.MinorPassive);
        }

        bool useMinorIcon = UsesMinorIconPresentation(_appliedType, minorPassiveLayout);
        if (typeDiamond != null)
        {
            typeDiamond.gameObject.SetActive(!useMinorIcon);
            if (!useMinorIcon)
            {
                RectTransform diamondRt = typeDiamond.rectTransform;
                SetSizeDeltaIfChanged(diamondRt, diamondSize);
                float diamondRotation = GetTypeIconSprite(_appliedType) != null ? 0f : 45f;
                SetLocalZRotationIfChanged(diamondRt, diamondRotation);
            }
        }

        if (minorIcon != null)
        {
            minorIcon.gameObject.SetActive(useMinorIcon);
            if (useMinorIcon)
                SetSizeDeltaIfChanged(minorIcon.rectTransform, iconSize);
        }

        if (selectedGlow != null && selectedGlow.TryGetComponent(out RectTransform glowRt))
            SetSizeDeltaIfChanged(glowRt, minorPassiveLayout ? new Vector2(36f, 36f) : new Vector2(66f, 66f));
    }

    private static void SetSizeDeltaIfChanged(RectTransform rt, Vector2 size)
    {
        if (rt == null)
            return;
        if ((rt.sizeDelta - size).sqrMagnitude < 0.01f)
            return;
        rt.sizeDelta = size;
    }

    private static void SetLocalZRotationIfChanged(RectTransform rt, float zDegrees)
    {
        if (rt == null)
            return;
        Vector3 euler = rt.localEulerAngles;
        if (Mathf.Abs(Mathf.DeltaAngle(euler.z, zDegrees)) < 0.1f)
            return;
        rt.localRotation = Quaternion.Euler(euler.x, euler.y, zDegrees);
    }

    private void ApplyTypeVisuals(SkillTimelineNodeType nodeType, bool minorPassiveLayout)
    {
        Color accent = GetTypeColor(nodeType);
        Sprite typeSprite = GetTypeIconSprite(nodeType);

        bool useMinorIcon = UsesMinorIconPresentation(nodeType, minorPassiveLayout);

        if (background != null)
        {
            bool showPlate = !useMinorIcon && (!hideHeavyBackground || minorPassiveLayout);
            background.gameObject.SetActive(showPlate);
            if (showPlate)
                background.color = Color.Lerp(backgroundTint, accent, minorPassiveLayout ? 0.35f : 0.18f);
        }
        if (typeDiamond != null)
        {
            typeDiamond.gameObject.SetActive(!useMinorIcon);
            if (!useMinorIcon)
            {
                ApplyTypeIconImage(typeDiamond, typeSprite, accent);
                float diamondRotation = typeSprite != null ? 0f : 45f;
                SetLocalZRotationIfChanged(typeDiamond.rectTransform, diamondRotation);
            }
        }

        ApplyMinorIconVisual(
            minorIcon,
            typeSprite,
            accent,
            useMinorIcon ? MinorIconSize : Vector2.zero,
            rotateFallbackDiamond: false);

        if (icon != null)
        {
            bool showIcon = !useMinorIcon && !minorPassiveLayout && nodeType != SkillTimelineNodeType.MinorPassive;
            icon.gameObject.SetActive(showIcon);
            if (showIcon)
                ApplyTypeIconImage(icon, typeSprite, accent);
        }
    }

    private static bool UsesMinorIconPresentation(SkillTimelineNodeType nodeType, bool minorPassiveLayout) =>
        nodeType == SkillTimelineNodeType.MinorPassive || minorPassiveLayout;

    private static void ApplyMinorIconVisual(
        Image minorIconImage,
        Sprite typeSprite,
        Color accent,
        Vector2 size,
        bool rotateFallbackDiamond)
    {
        if (minorIconImage == null)
            return;

        minorIconImage.gameObject.SetActive(size.sqrMagnitude > 0.01f);
        if (!minorIconImage.gameObject.activeSelf)
            return;

        ApplyTypeIconImage(minorIconImage, typeSprite, accent);
        SetSizeDeltaIfChanged(minorIconImage.rectTransform, size);
        float rotation = rotateFallbackDiamond && typeSprite == null ? 45f : 0f;
        SetLocalZRotationIfChanged(minorIconImage.rectTransform, rotation);
    }

    private static void ApplyTypeIconImage(Image image, Sprite sprite, Color fallbackTint)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = sprite != null ? Color.white : fallbackTint;
    }

    private Sprite GetTypeIconSprite(SkillTimelineNodeType nodeType)
    {
        return nodeType switch
        {
            SkillTimelineNodeType.Ability => abilityIconSprite,
            SkillTimelineNodeType.MinorPassive => minorPassiveIconSprite,
            SkillTimelineNodeType.MajorPassive => majorPassiveIconSprite,
            SkillTimelineNodeType.Unlock => unlockIconSprite,
            SkillTimelineNodeType.Capstone => capstoneIconSprite,
            _ => null
        };
    }

    private void ApplyStateVisuals(SkillTimelineNodeState state)
    {
        bool locked = state == SkillTimelineNodeState.Locked;
        bool selected = state == SkillTimelineNodeState.Selected;
        bool unlocked = state == SkillTimelineNodeState.Unlocked;

        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(locked);
            if (lockedOverlay.TryGetComponent(out Image overlayImg))
                overlayImg.color = lockedOverlayColor;
        }

        if (selectedGlow != null)
            selectedGlow.SetActive(selected);

        if (selectedGlow != null && selectedGlow.TryGetComponent(out Image glowImg))
            glowImg.color = selectedGlowColor;

        if (checkmark != null)
            checkmark.SetActive(unlocked);

        if (borderOutline != null)
            borderOutline.effectColor = selected ? selectedBorderColor : availableBorderColor;

        Color accent = GetTypeColor(_appliedType);
        if (background != null && background.gameObject.activeSelf && !UsesMinorIconPresentation(_appliedType, _appliedMinorLayout))
        {
            Color normalBg = Color.Lerp(backgroundTint, accent, _appliedMinorLayout ? 0.35f : 0.18f);
            background.color = locked ? MultiplyColor(normalBg, lockedDimMultiplier) : normalBg;
        }

        if (icon != null && icon.gameObject.activeSelf)
            icon.color = locked ? MultiplyColor(_baseIconColor, lockedDimMultiplier) : _baseIconColor;

        if (minorIcon != null && minorIcon.gameObject.activeSelf)
            minorIcon.color = locked ? MultiplyColor(Color.white, lockedDimMultiplier) : Color.white;

        if (rootButton != null)
            rootButton.interactable = !locked;
    }

    private void EnableTimelineNodeBackground(SkillTimelineNodeType nodeType)
    {
        if (background == null || nodeType == SkillTimelineNodeType.MinorPassive)
            return;

        Color accent = GetTypeColor(nodeType);
        background.gameObject.SetActive(true);
        background.color = Color.Lerp(backgroundTint, accent, 0.18f);
    }

    private void ConfigureChoiceGroupNameLabel(string displayName)
    {
        if (nameLabel == null)
            return;

        nameLabel.fontSize = 10f;
        nameLabel.fontStyle = FontStyles.Normal;
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.textWrappingMode = TextWrappingModes.Normal;
        nameLabel.overflowMode = TextOverflowModes.Ellipsis;

        float labelY = -(StandardNodeHalfHeight + 4f);
        RectTransform labelRt = nameLabel.rectTransform;
        labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0f, labelY);
        labelRt.sizeDelta = new Vector2(76f, 26f);

        ConfigureChoiceGroupLayoutElement();
    }

    private void ConfigureChoiceGroupLayoutElement()
    {
        if (!TryGetComponent(out LayoutElement layoutElement))
            layoutElement = gameObject.AddComponent<LayoutElement>();

        float height = StandardNodeButtonSize + 24f;
        layoutElement.minWidth = 72f;
        layoutElement.preferredWidth = 80f;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
    }

    private void ConfigureUnlockNameLabel(string displayName)
    {
        if (nameLabelUnlocks == null)
            return;

        nameLabelUnlocks.fontSize = 11f;
        nameLabelUnlocks.fontStyle = FontStyles.Bold;
        nameLabelUnlocks.alignment = TextAlignmentOptions.Center;
        nameLabelUnlocks.textWrappingMode = TextWrappingModes.Normal;
        nameLabelUnlocks.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform labelRt = nameLabelUnlocks.rectTransform;
        labelRt.sizeDelta = new Vector2(160f, 32f);
        labelRt.anchoredPosition = new Vector2(0f, 34f);
    }

    private void ApplyDisplayName(string displayName, bool minorPassiveLayout, bool hideNameLabel)
    {
        bool isUnlock = _appliedType == SkillTimelineNodeType.Unlock;
        bool showAnyName = !hideNameLabel && !minorPassiveLayout && _appliedType != SkillTimelineNodeType.MinorPassive;
        bool showUnlockLabel = showAnyName && isUnlock;
        bool showStandardLabel = showAnyName && !isUnlock;
        string text = string.IsNullOrWhiteSpace(displayName) ? "Node" : displayName;

        if (nameLabel != null)
        {
            nameLabel.gameObject.SetActive(showStandardLabel);
            if (showStandardLabel)
                nameLabel.text = text;
        }

        if (nameLabelUnlocks != null)
        {
            nameLabelUnlocks.gameObject.SetActive(showUnlockLabel);
            if (showUnlockLabel)
                nameLabelUnlocks.text = text;
        }
    }

    private Color GetTypeColor(SkillTimelineNodeType nodeType)
    {
        return nodeType switch
        {
            SkillTimelineNodeType.Ability => abilityColor,
            SkillTimelineNodeType.MinorPassive => minorPassiveColor,
            SkillTimelineNodeType.MajorPassive => majorPassiveColor,
            SkillTimelineNodeType.Unlock => unlockColor,
            SkillTimelineNodeType.Capstone => capstoneColor,
            _ => abilityColor
        };
    }

    private static Color MultiplyColor(Color c, Color multiplier)
    {
        return new Color(c.r * multiplier.r, c.g * multiplier.g, c.b * multiplier.b, c.a * multiplier.a);
    }
}
