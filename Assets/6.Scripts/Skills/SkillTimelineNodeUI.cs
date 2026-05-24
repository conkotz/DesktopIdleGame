using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
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

    /// <summary>Ability / major passive milestone rows use Select / Selected / Change chrome.</summary>
    public static bool UsesRowSelectionButtons(SkillTimelineNodeType nodeType) =>
        nodeType == SkillTimelineNodeType.Ability || nodeType == SkillTimelineNodeType.MajorPassive;

    /// <summary>Matches RootButton size on <c>SkillTimelineNodeUI</c> prefab; used by timeline layout math only.</summary>
    public const float StandardNodeButtonSize = 56f;
    public static readonly float StandardNodeHalfHeight = StandardNodeButtonSize * 0.5f;
    private const float DefaultNodeRootSize = 72f;
    private const float NodeRootHeightWithSelectionChrome = 100f;

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
    [SerializeField] private GameObject notSelectedRoot;
    [SerializeField] private GameObject checkmark;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text nameLabelUnlocks;
    [SerializeField] private Button selectSkillButton;
    [SerializeField] private GameObject selectedNodeRoot;
    [SerializeField] private Button changeNodeButton;

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
    [SerializeField] private Color backgroundTint = new(0.12f, 0.1f, 0.08f, 1f);
    [Tooltip("When true, hides the dark node plate so icons/diamonds read clearly.")]
    [SerializeField] private bool hideHeavyBackground = false;

    [Header("State Colors")]
    [SerializeField] private Color lockedOverlayColor = new(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color lockedDimMultiplier = new(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color availableBorderColor = new(0.35f, 0.3f, 0.24f, 0.85f);
    [SerializeField] private Color selectedBorderColor = new(1f, 0.84f, 0.2f, 1f);
    [SerializeField] private Color selectedGlowColor = new(1f, 0.84f, 0.2f, 1f);

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
    private Color _baseNameLabelColor = Color.white;
    private Color _baseNameLabelUnlocksColor = Color.white;

#if UNITY_EDITOR
    private bool _deferredPreviewQueued;
#endif

    public event Action<SkillTimelineNodeUI> Clicked;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;
    public Button RootButton => rootButton;
    public SkillTimelineNodeBinding Binding => _binding;

    private SkillTimelineNodeBinding _binding;
    private UnityAction _clickHandler;
    private UnityAction _selectSkillHandler;
    private UnityAction _changeNodeHandler;

    private const float NotSelectedFadeSeconds = 0.35f;
    private const float NotSelectedHoldOpaqueSeconds = 2f;
    private CanvasGroup _notSelectedCanvasGroup;
    private Image _notSelectedImage;
    private Color _notSelectedImageBaseColor = Color.white;
    private Coroutine _notSelectedFlashRoutine;

    private void Awake()
    {
        CacheBaseColors();
        EnsureClickHandler();
    }

    private void OnEnable()
    {
        CacheBaseColors();
        EnsureClickHandler();

        if (!applyPreviewInEditor)
            return;

        // Spawn code sets the real presentation per node type. Inspector preview here was
        // re-running as Ability on every OnEnable and re-showing Select on minors/unlocks.
        if (Application.isPlaying)
            return;

#if UNITY_EDITOR
        RequestEditorPreviewApply();
#endif
    }

    private void OnDisable()
    {
        RemoveClickHandler();
        RemoveSelectSkillHandler();
        RemoveChangeNodeHandler();
        SetNotSelectedPrompt(false);
#if UNITY_EDITOR
        CancelDeferredEditorPreview();
#endif
    }

    /// <summary>Row pick committed for this milestone (border highlight only).</summary>
    public void ApplyRowPickSelectionVisual(bool selected)
    {
        EnsureReferences();
        SkillTimelineNodeState state = selected
            ? SkillTimelineNodeState.Selected
            : _appliedState == SkillTimelineNodeState.Locked
                ? SkillTimelineNodeState.Locked
                : SkillTimelineNodeState.Available;
        ApplyStateVisuals(state);
    }

    /// <summary>Pulses <see cref="notSelectedRoot"/> (same timing as <see cref="SkillTreeNodeUI"/>).</summary>
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
        if (_notSelectedFlashRoutine == null && isActiveAndEnabled)
            _notSelectedFlashRoutine = StartCoroutine(NotSelectedFlashLoop());
    }

    /// <summary>
    /// <c>SelectNode</c> = pick this option when the row has no committed choice yet.
    /// <c>SelectedNode</c> = this option is the committed pick.
    /// <c>ChangeNode</c> = swap to this sibling while another option is already committed.
    /// </summary>
    public void ConfigureRowSelectionButtons(
        bool showSelectButton,
        bool showSelectedLabel,
        bool showChangeButton,
        Action<SkillTimelineNodeUI> onSelectNode,
        Action<SkillTimelineNodeUI> onChangeNode)
    {
        EnsureReferences();
        SetRowSelectionChromeVisible(showSelectButton, showSelectedLabel, showChangeButton);

        RemoveSelectSkillHandler();
        if (showSelectButton && onSelectNode != null && Application.isPlaying && selectSkillButton != null)
        {
            selectSkillButton.interactable = true;
            _selectSkillHandler = () => onSelectNode(this);
            selectSkillButton.onClick.AddListener(_selectSkillHandler);
        }

        RemoveChangeNodeHandler();
        if (showChangeButton && onChangeNode != null && Application.isPlaying && changeNodeButton != null)
        {
            changeNodeButton.interactable = true;
            _changeNodeHandler = () => onChangeNode(this);
            changeNodeButton.onClick.AddListener(_changeNodeHandler);
        }

        AdjustRootSizeForSelectionChrome(showSelectButton, showSelectedLabel, showChangeButton);
        if (showSelectButton || showSelectedLabel || showChangeButton)
            BringRowSelectionChromeToFront();
    }

    private void AdjustRootSizeForSelectionChrome(bool showSelect, bool showSelected, bool showChange)
    {
        RectTransform rt = RectTransform;
        if (rt == null)
            return;

        bool expanded = showSelect || showSelected || showChange;
        float height = expanded ? NodeRootHeightWithSelectionChrome : DefaultNodeRootSize;
        rt.sizeDelta = new Vector2(DefaultNodeRootSize, height);
    }

    private void BringRowSelectionChromeToFront()
    {
        if (selectSkillButton != null)
            selectSkillButton.transform.SetAsLastSibling();
        if (selectedNodeRoot != null)
            selectedNodeRoot.transform.SetAsLastSibling();
        if (changeNodeButton != null)
            changeNodeButton.transform.SetAsLastSibling();
    }

    private void SetRowSelectionChromeVisible(bool showSelect, bool showSelected, bool showChange)
    {
        GameObject selectGo = selectSkillButton != null
            ? selectSkillButton.gameObject
            : transform.Find("SelectNode")?.gameObject;
        if (selectGo != null)
            selectGo.SetActive(showSelect);

        if (selectedNodeRoot != null)
            selectedNodeRoot.SetActive(showSelected);
        else
        {
            Transform selected = transform.Find("SelectedNode");
            if (selected != null)
                selected.gameObject.SetActive(showSelected);
        }

        GameObject changeGo = changeNodeButton != null
            ? changeNodeButton.gameObject
            : transform.Find("ChangeNode")?.gameObject;
        if (changeGo != null)
            changeGo.SetActive(showChange);
    }

    /// <summary>Assigns unlock data used by the Current Selection panel (display only).</summary>
    public void Bind(SkillTimelineNodeBinding binding)
    {
        _binding = binding;
        ApplyAbilityContentIconFromBinding(binding);
    }

    private void EnsureClickHandler()
    {
        if (!Application.isPlaying)
            return;

        EnsureReferences();
        if (rootButton == null)
            return;

        if (_clickHandler != null)
            return;

        _clickHandler = HandleRootButtonClicked;
        rootButton.onClick.AddListener(_clickHandler);
    }

    private void RemoveClickHandler()
    {
        if (rootButton == null || _clickHandler == null)
            return;

        rootButton.onClick.RemoveListener(_clickHandler);
        _clickHandler = null;
    }

    private void RemoveSelectSkillHandler()
    {
        if (selectSkillButton == null || _selectSkillHandler == null)
            return;

        selectSkillButton.onClick.RemoveListener(_selectSkillHandler);
        _selectSkillHandler = null;
    }

    private void RemoveChangeNodeHandler()
    {
        if (changeNodeButton == null || _changeNodeHandler == null)
            return;

        changeNodeButton.onClick.RemoveListener(_changeNodeHandler);
        _changeNodeHandler = null;
    }

    private void HandleRootButtonClicked() => Clicked?.Invoke(this);

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!applyPreviewInEditor)
            return;
        RequestEditorPreviewApply();
    }
#endif

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
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning($"[SkillTimelineNodeUI] No assets at {sheetPath}", this);
            return;
        }

        foreach (UnityEngine.Object asset in assets)
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
        SetChildActive(nameLabelUnlocks, false);
    }

    /// <summary>Single node below the spine (no milestone group shell).</summary>
    public void ApplyBelowSpineNodePreview(
        SkillTimelineNodeType nodeType,
        string displayName,
        SkillTimelineNodeState state = SkillTimelineNodeState.Available,
        bool capstoneScale = false)
    {
        ApplyPreview(nodeType, state, displayName, minorPassiveLayout: false, hideNameLabel: false);
        SetChildActive(nameLabelUnlocks, false);

        if (capstoneScale && rectTransform != null)
            rectTransform.localScale = Vector3.one * 1.12f;
    }

    /// <summary>Unlock row node: label above icon, centered on milestone X.</summary>
    public void ApplyUnlockTimelinePreview(
        string displayName,
        SkillTimelineNodeState state = SkillTimelineNodeState.Available)
    {
        ApplyPreview(SkillTimelineNodeType.Unlock, state, displayName, minorPassiveLayout: false, hideNameLabel: true);
    }

    /// <summary>Small minor icon on the timeline spine (no card chrome).</summary>
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

        ApplyTypeVisuals(nodeType, minorPassiveLayout);
        ApplyStateVisuals(state);
        ApplyDisplayName(displayName, minorPassiveLayout, hideNameLabel);

        if (!UsesRowSelectionButtons(nodeType))
            SetRowSelectionChromeVisible(false, false, false);
    }

    private void ApplySpineDiamondOnlyPresentation(SkillTimelineNodeType nodeType, SkillTimelineNodeState state)
    {
        bool locked = state == SkillTimelineNodeState.Locked;

        if (rootButton != null)
            rootButton.interactable = true;

        SetChildActive(background, false);
        SetChildActive(borderOutline, false);
        SetChildActive(icon, false);
        SetChildActive(typeDiamond, false);
        SetChildActive(lockedOverlay, false);
        SetChildActive(selectedGlow, false);
        SetChildActive(checkmark, false);

        if (nameLabel != null)
            nameLabel.gameObject.SetActive(false);

        if (nameLabelUnlocks != null)
            nameLabelUnlocks.gameObject.SetActive(false);

        Color accent = GetTypeColor(nodeType);
        Sprite typeSprite = GetTypeIconSprite(nodeType);
        ApplyMinorIconVisual(minorIcon, typeSprite, accent, rotateFallbackDiamond: false);

        if (minorIcon != null)
            minorIcon.color = locked ? MultiplyColor(Color.white, lockedDimMultiplier) : Color.white;

        SetRowSelectionChromeVisible(false, false, false);
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

        if (notSelectedRoot == null)
        {
            Transform t = transform.Find("RootButton/NotSelected");
            if (t != null)
                notSelectedRoot = t.gameObject;
        }

        ResolveSelectionChromeReferences();

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

    private void ResolveSelectionChromeReferences()
    {
        Transform select = transform.Find("SelectNode");
        if (select != null)
            selectSkillButton = select.GetComponent<Button>();

        if (selectSkillButton == null)
            selectSkillButton = transform.Find("SelectSkill")?.GetComponent<Button>();

        if (selectedNodeRoot == null)
        {
            Transform selected = transform.Find("SelectedNode");
            if (selected != null)
                selectedNodeRoot = selected.gameObject;
        }

        if (changeNodeButton == null)
            changeNodeButton = transform.Find("ChangeNode")?.GetComponent<Button>();
    }

    private void CacheBaseColors()
    {
        if (background != null)
            _baseBackgroundColor = backgroundTint;

        if (icon != null && icon.color.a > 0.01f)
            _baseIconColor = Color.white;

        if (nameLabel != null)
            _baseNameLabelColor = nameLabel.color;

        if (nameLabelUnlocks != null)
            _baseNameLabelUnlocksColor = nameLabelUnlocks.color;
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
            background.gameObject.SetActive(false);

        bool showTypeDiamond = !useMinorIcon && nodeType == SkillTimelineNodeType.Ability;
        if (typeDiamond != null)
        {
            typeDiamond.gameObject.SetActive(showTypeDiamond);
            if (showTypeDiamond)
            {
                ApplyTypeIconImage(typeDiamond, typeSprite, accent);
                float diamondRotation = typeSprite != null ? 0f : 45f;
                SetLocalZRotationIfChanged(typeDiamond.rectTransform, diamondRotation);
            }
        }

        if (useMinorIcon)
            ApplyMinorIconVisual(minorIcon, typeSprite, accent, rotateFallbackDiamond: false);
        else
            SetChildActive(minorIcon, false);

        if (icon != null)
        {
            bool showIcon = !useMinorIcon && !minorPassiveLayout && nodeType != SkillTimelineNodeType.MinorPassive;
            icon.gameObject.SetActive(showIcon);
            if (showIcon && nodeType != SkillTimelineNodeType.Ability)
                ApplyTypeIconImage(icon, typeSprite, accent);
        }
    }

    private void ApplyAbilityContentIconFromBinding(SkillTimelineNodeBinding binding)
    {
        if (_appliedType != SkillTimelineNodeType.Ability)
            return;

        EnsureReferences();

        Sprite typeSprite = GetTypeIconSprite(SkillTimelineNodeType.Ability);
        if (typeDiamond != null)
        {
            typeDiamond.gameObject.SetActive(true);
            ApplyTypeIconImage(typeDiamond, typeSprite, abilityColor);
            float diamondRotation = typeSprite != null ? 0f : 45f;
            SetLocalZRotationIfChanged(typeDiamond.rectTransform, diamondRotation);
        }

        Sprite contentIcon = ResolveAbilityContentIcon(binding);
        if (icon == null)
            return;

        icon.gameObject.SetActive(true);
        if (contentIcon != null)
        {
            ApplyTypeIconImage(icon, contentIcon, abilityColor);
            _baseIconColor = Color.white;
        }
        else
            ApplyTypeIconImage(icon, typeSprite, abilityColor);
    }

    private static Sprite ResolveAbilityContentIcon(SkillTimelineNodeBinding binding)
    {
        if (binding == null || binding.TimelineNodeType != SkillTimelineNodeType.Ability)
            return null;

        if (binding.Choice != null)
        {
            Sprite choiceIcon = SkillsAbilityPresentationResolver.ResolveChoiceIcon(binding.Choice);
            if (choiceIcon != null)
                return choiceIcon;
        }

        SkillUnlockDefinition unlock = binding.Unlock;
        if (unlock?.ability != null)
        {
            Sprite abilityIcon = SkillsAbilityPresentationResolver.ResolveAbilityIcon(unlock.ability);
            if (abilityIcon != null)
                return abilityIcon;
        }

        return unlock?.icon;
    }

    private static bool UsesMinorIconPresentation(SkillTimelineNodeType nodeType, bool minorPassiveLayout) =>
        nodeType == SkillTimelineNodeType.MinorPassive || minorPassiveLayout;

    private static void ApplyMinorIconVisual(
        Image minorIconImage,
        Sprite typeSprite,
        Color accent,
        bool rotateFallbackDiamond)
    {
        if (minorIconImage == null)
            return;

        minorIconImage.gameObject.SetActive(true);
        ApplyTypeIconImage(minorIconImage, typeSprite, accent);
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
            selectedGlow.SetActive(false);

        if (checkmark != null)
            checkmark.SetActive(unlocked);

        if (borderOutline != null)
            borderOutline.effectColor = selected ? selectedBorderColor : availableBorderColor;

        Color accent = GetTypeColor(_appliedType);
        if (icon != null && icon.gameObject.activeSelf)
            icon.color = locked ? MultiplyColor(_baseIconColor, lockedDimMultiplier) : _baseIconColor;

        if (minorIcon != null && minorIcon.gameObject.activeSelf)
            minorIcon.color = locked ? MultiplyColor(Color.white, lockedDimMultiplier) : Color.white;

        if (rootButton != null)
            rootButton.interactable = true;
    }

    private void ApplyDisplayName(string displayName, bool minorPassiveLayout, bool hideNameLabel)
    {
        bool isUnlock = _appliedType == SkillTimelineNodeType.Unlock;
        bool isMinorPassive = _appliedType == SkillTimelineNodeType.MinorPassive || minorPassiveLayout;
        bool showStandardLabel = !hideNameLabel && !isMinorPassive && !isUnlock;
        bool showUnlockLabel = isUnlock && !string.IsNullOrWhiteSpace(displayName);
        string text = string.IsNullOrWhiteSpace(displayName) ? "Node" : displayName;

        bool locked = _appliedState == SkillTimelineNodeState.Locked;

        if (nameLabel != null)
        {
            nameLabel.gameObject.SetActive(showStandardLabel);
            if (showStandardLabel)
            {
                nameLabel.text = text;
                nameLabel.color = locked
                    ? MultiplyColor(_baseNameLabelColor, lockedDimMultiplier)
                    : _baseNameLabelColor;
            }
        }

        if (nameLabelUnlocks != null)
        {
            nameLabelUnlocks.gameObject.SetActive(showUnlockLabel);
            if (showUnlockLabel)
            {
                nameLabelUnlocks.text = text;
                nameLabelUnlocks.color = locked
                    ? MultiplyColor(_baseNameLabelUnlocksColor, lockedDimMultiplier)
                    : _baseNameLabelUnlocksColor;
            }
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
