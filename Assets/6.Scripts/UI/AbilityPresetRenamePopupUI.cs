using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Edit popup for ability preset name and saved-build summary.</summary>
[DisallowMultipleComponent]
public sealed class AbilityPresetRenamePopupUI : MonoBehaviour
{
    private const int CanvasSortOrder = 10100;

    private static AbilityPresetRenamePopupUI _instance;

    private RectTransform _panelRoot;
    private RectTransform _weaponSetSectionRoot;
    private RectTransform _setPickRowRoot;
    private TMP_InputField _inputField;
    private TMP_Text _summaryText;
    private Toggle _applyToSetToggle;
    private Button _setOnePickButton;
    private Button _setTwoPickButton;
    private TMP_Text _overwriteWarningText;
    private Action<AbilityPresetEditResult> _onConfirm;

    private bool _showWeaponSetSection;
    private bool _applyToSetEnabled;
    private int _selectedWeaponSetIndex;
    private SkillType _editingSkillType;
    private int _editingPresetSlotIndex;

    public static AbilityPresetRenamePopupUI EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        _instance = FindFirstObjectByType<AbilityPresetRenamePopupUI>(FindObjectsInactive.Include);
        if (_instance != null)
            return _instance;

        var host = new GameObject(nameof(AbilityPresetRenamePopupUI), typeof(AbilityPresetRenamePopupUI));
        DontDestroyOnLoad(host);
        _instance = host.GetComponent<AbilityPresetRenamePopupUI>();
        _instance.BuildUi();
        return _instance;
    }

    public static void Show(
        string currentName,
        string summaryText,
        bool showWeaponSetSection,
        bool applyToWeaponSet,
        int weaponSetIndex,
        SkillType editingSkillType,
        int editingPresetSlotIndex,
        Action<AbilityPresetEditResult> onConfirm)
    {
        if (onConfirm == null)
            return;

        AbilityPresetRenamePopupUI popup = EnsureInstance();
        popup.Open(
            currentName,
            summaryText,
            showWeaponSetSection,
            applyToWeaponSet,
            weaponSetIndex,
            editingSkillType,
            editingPresetSlotIndex,
            onConfirm);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (_panelRoot == null)
            BuildUi();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (_panelRoot == null || !_panelRoot.gameObject.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            HideImmediate();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Confirm();
    }

    private void Open(
        string currentName,
        string summaryText,
        bool showWeaponSetSection,
        bool applyToWeaponSet,
        int weaponSetIndex,
        SkillType editingSkillType,
        int editingPresetSlotIndex,
        Action<AbilityPresetEditResult> onConfirm)
    {
        _onConfirm = onConfirm;
        _showWeaponSetSection = showWeaponSetSection;
        _applyToSetEnabled = applyToWeaponSet;
        _selectedWeaponSetIndex = Mathf.Clamp(weaponSetIndex, 0, SkillsManager.WeaponSetPresetLinkCount - 1);
        _editingSkillType = editingSkillType;
        _editingPresetSlotIndex = Mathf.Clamp(editingPresetSlotIndex, 0, SkillsManager.AbilityPresetSlotCount - 1);

        EnsureOverwriteWarningLabel();

        if (_inputField != null)
        {
            _inputField.characterLimit = SkillsManager.AbilityPresetMaxDisplayNameLength;
            _inputField.SetTextWithoutNotify(currentName ?? string.Empty);
            _inputField.ActivateInputField();
            _inputField.MoveTextEnd(false);
        }

        if (_summaryText != null)
            _summaryText.text = summaryText ?? string.Empty;

        if (_weaponSetSectionRoot != null)
            _weaponSetSectionRoot.gameObject.SetActive(showWeaponSetSection);

        if (_applyToSetToggle != null)
        {
            _applyToSetToggle.SetIsOnWithoutNotify(applyToWeaponSet);
            _applyToSetToggle.onValueChanged.RemoveListener(HandleApplyToSetToggled);
            _applyToSetToggle.onValueChanged.AddListener(HandleApplyToSetToggled);
        }

        RefreshWeaponSetPickVisuals();
        RefreshOverwriteWarning();

        if (_panelRoot != null)
            _panelRoot.gameObject.SetActive(true);
    }

    private void HandleApplyToSetToggled(bool enabled)
    {
        _applyToSetEnabled = enabled;
        RefreshWeaponSetPickVisuals();
        RefreshOverwriteWarning();
    }

    private void SelectWeaponSet(int setIndex)
    {
        _selectedWeaponSetIndex = Mathf.Clamp(setIndex, 0, SkillsManager.WeaponSetPresetLinkCount - 1);
        if (!_applyToSetEnabled && _applyToSetToggle != null)
            _applyToSetToggle.isOn = true;
        _applyToSetEnabled = true;
        RefreshWeaponSetPickVisuals();
        RefreshOverwriteWarning();
    }

    private void RefreshWeaponSetPickVisuals()
    {
        bool interactable = _applyToSetEnabled;
        if (_setPickRowRoot != null)
            _setPickRowRoot.gameObject.SetActive(_showWeaponSetSection);

        ApplySetPickButtonVisual(_setOnePickButton, interactable && _selectedWeaponSetIndex == 0);
        ApplySetPickButtonVisual(_setTwoPickButton, interactable && _selectedWeaponSetIndex == 1);
    }

    private void RefreshOverwriteWarning()
    {
        if (_overwriteWarningText == null)
            return;

        bool showWarning = false;
        string message = string.Empty;

        if (_showWeaponSetSection && _applyToSetEnabled)
        {
            SkillsManager skills = SkillsManager.Instance
                ?? FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
            if (skills != null
                && skills.WillAssigningPresetOverwriteWeaponSet(
                    _selectedWeaponSetIndex,
                    _editingSkillType,
                    _editingPresetSlotIndex,
                    out _))
            {
                message = "Setting this will overwrite the existing preset for this set.";
                showWarning = true;
            }
        }

        _overwriteWarningText.text = message;
        _overwriteWarningText.gameObject.SetActive(showWarning);
        if (_overwriteWarningText.transform.parent != null)
            _overwriteWarningText.transform.parent.gameObject.SetActive(showWarning);
    }

    private void EnsureOverwriteWarningLabel()
    {
        if (_overwriteWarningText != null || _weaponSetSectionRoot == null)
            return;

        RectTransform warnRow = CreateChild(_weaponSetSectionRoot, "OverwriteWarningRow");
        LayoutElement warnLe = warnRow.gameObject.AddComponent<LayoutElement>();
        warnLe.minHeight = 28f;

        var warnGo = new GameObject("Text", typeof(RectTransform));
        warnGo.transform.SetParent(warnRow, false);
        RectTransform warnRt = warnGo.GetComponent<RectTransform>();
        StretchFull(warnRt);

        _overwriteWarningText = warnGo.AddComponent<TextMeshProUGUI>();
        _overwriteWarningText.fontSize = 11f;
        _overwriteWarningText.color = new Color(0.92f, 0.32f, 0.28f, 1f);
        _overwriteWarningText.alignment = TextAlignmentOptions.TopLeft;
        _overwriteWarningText.textWrappingMode = TextWrappingModes.Normal;
        _overwriteWarningText.text = string.Empty;
        warnRow.gameObject.SetActive(false);
    }

    private static void ApplySetPickButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        button.interactable = true;
        if (button.targetGraphic is Image img)
            img.color = selected
                ? new Color(0.42f, 0.34f, 0.22f, 1f)
                : new Color(0.28f, 0.24f, 0.2f, 1f);
    }

    private void Confirm()
    {
        var result = new AbilityPresetEditResult
        {
            displayName = _inputField != null ? _inputField.text : string.Empty,
            applyToWeaponSet = _showWeaponSetSection && _applyToSetEnabled,
            weaponSetIndex = _selectedWeaponSetIndex
        };

        Action<AbilityPresetEditResult> cb = _onConfirm;
        HideImmediate();
        cb?.Invoke(result);
    }

    private void HideImmediate()
    {
        _onConfirm = null;
        if (_applyToSetToggle != null)
            _applyToSetToggle.onValueChanged.RemoveListener(HandleApplyToSetToggled);
        if (_panelRoot != null)
            _panelRoot.gameObject.SetActive(false);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("AbilityPresetEditCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = CanvasSortOrder;

        RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
        StretchFull(canvasRt);

        _panelRoot = CreateChild(canvasRt, "Panel");
        StretchFull(_panelRoot, 0.44f, 0.68f);

        Image panelBg = _panelRoot.gameObject.AddComponent<Image>();
        panelBg.color = new Color(0.24f, 0.2f, 0.16f, 0.98f);

        Outline panelOutline = _panelRoot.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.75f, 0.6f, 0.32f, 0.85f);
        panelOutline.effectDistance = new Vector2(2f, -2f);
        panelOutline.useGraphicAlpha = true;

        VerticalLayoutGroup vlg = _panelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        CreateLabel(_panelRoot, "Edit preset", 16, FontStyles.Bold);
        CreateLabel(_panelRoot, $"Name (max {SkillsManager.AbilityPresetMaxDisplayNameLength} characters)", 12, FontStyles.Normal);

        _inputField = CreateInputField(_panelRoot);

        BuildWeaponSetSection(_panelRoot);

        RectTransform buttonRow = CreateChild(_panelRoot, "Buttons");
        HorizontalLayoutGroup row = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = false;

        CreateButton(buttonRow, "Cancel", HideImmediate);
        CreateButton(buttonRow, "OK", Confirm);

        CreateLabel(_panelRoot, "Saved build", 13, FontStyles.Bold);
        _summaryText = CreateSummaryScrollArea(_panelRoot);
    }

    private void BuildWeaponSetSection(RectTransform parent)
    {
        _weaponSetSectionRoot = CreateChild(parent, "WeaponSetSection");
        LayoutElement sectionLe = _weaponSetSectionRoot.gameObject.AddComponent<LayoutElement>();
        sectionLe.minHeight = 72f;

        VerticalLayoutGroup sectionVlg = _weaponSetSectionRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        sectionVlg.spacing = 6;
        sectionVlg.childAlignment = TextAnchor.UpperLeft;
        sectionVlg.childControlWidth = true;
        sectionVlg.childControlHeight = true;
        sectionVlg.childForceExpandWidth = true;
        sectionVlg.childForceExpandHeight = false;

        CreateLabel(_weaponSetSectionRoot, "Apply preset to set:", 12, FontStyles.Bold);

        RectTransform toggleRow = CreateChild(_weaponSetSectionRoot, "ApplyToggleRow");
        LayoutElement toggleRowLe = toggleRow.gameObject.AddComponent<LayoutElement>();
        toggleRowLe.minHeight = 28f;

        HorizontalLayoutGroup toggleHlg = toggleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        toggleHlg.spacing = 8;
        toggleHlg.childAlignment = TextAnchor.MiddleLeft;
        toggleHlg.childControlWidth = false;
        toggleHlg.childControlHeight = true;
        toggleHlg.childForceExpandWidth = false;
        toggleHlg.childForceExpandHeight = false;

        RectTransform toggleRt = CreateChild(toggleRow, "Toggle");
        LayoutElement toggleLe = toggleRt.gameObject.AddComponent<LayoutElement>();
        toggleLe.minWidth = 28f;
        toggleLe.minHeight = 28f;

        Image toggleBg = toggleRt.gameObject.AddComponent<Image>();
        toggleBg.color = new Color(0.2f, 0.18f, 0.16f, 1f);

        RectTransform checkmarkRt = CreateChild(toggleRt, "Checkmark");
        StretchFull(checkmarkRt, 0.5f, 0.5f);
        Image checkmark = checkmarkRt.gameObject.AddComponent<Image>();
        checkmark.color = new Color(0.85f, 0.72f, 0.38f, 1f);

        _applyToSetToggle = toggleRt.gameObject.AddComponent<Toggle>();
        _applyToSetToggle.targetGraphic = toggleBg;
        _applyToSetToggle.graphic = checkmark;

        CreateInlineLabel(toggleRow, "When enabled, this preset loads on weapon set swap.");

        _setPickRowRoot = CreateChild(_weaponSetSectionRoot, "SetPickRow");
        LayoutElement setPickLe = _setPickRowRoot.gameObject.AddComponent<LayoutElement>();
        setPickLe.minHeight = 32f;

        HorizontalLayoutGroup setPickHlg = _setPickRowRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        setPickHlg.spacing = 8;
        setPickHlg.childAlignment = TextAnchor.MiddleLeft;
        setPickHlg.childControlWidth = true;
        setPickHlg.childControlHeight = true;
        setPickHlg.childForceExpandWidth = true;
        setPickHlg.childForceExpandHeight = false;

        _setOnePickButton = CreateSetPickButton(_setPickRowRoot, "Set 1", () => SelectWeaponSet(0));
        _setTwoPickButton = CreateSetPickButton(_setPickRowRoot, "Set 2", () => SelectWeaponSet(1));

        RectTransform warnRow = CreateChild(_weaponSetSectionRoot, "OverwriteWarningRow");
        LayoutElement warnLe = warnRow.gameObject.AddComponent<LayoutElement>();
        warnLe.minHeight = 28f;

        var warnGo = new GameObject("Text", typeof(RectTransform));
        warnGo.transform.SetParent(warnRow, false);
        RectTransform warnRt = warnGo.GetComponent<RectTransform>();
        StretchFull(warnRt);

        _overwriteWarningText = warnGo.AddComponent<TextMeshProUGUI>();
        _overwriteWarningText.fontSize = 11f;
        _overwriteWarningText.color = new Color(0.92f, 0.32f, 0.28f, 1f);
        _overwriteWarningText.alignment = TextAlignmentOptions.TopLeft;
        _overwriteWarningText.textWrappingMode = TextWrappingModes.Normal;
        _overwriteWarningText.text = string.Empty;
        warnRow.gameObject.SetActive(false);

        _weaponSetSectionRoot.gameObject.SetActive(false);
    }

    private Button CreateSetPickButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform btnRt = CreateChild(parent, label.Replace(" ", "") + "Pick");
        LayoutElement le = btnRt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 30f;

        Image img = btnRt.gameObject.AddComponent<Image>();
        img.color = new Color(0.28f, 0.24f, 0.2f, 1f);

        Button button = btnRt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        RectTransform textRt = CreateChild(btnRt, "Text");
        StretchFull(textRt);
        TMP_Text tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 13;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.93f, 0.9f, 0.84f, 1f);
        return button;
    }

    private static void CreateInlineLabel(RectTransform parent, string text)
    {
        RectTransform row = CreateChild(parent, "Hint");
        LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(row, false);
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        StretchFull(labelRt);

        TMP_Text tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 11f;
        tmp.color = new Color(0.7f, 0.66f, 0.6f, 1f);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.Normal;
    }

    private static TMP_Text CreateSummaryScrollArea(RectTransform parent)
    {
        RectTransform scrollRoot = CreateChild(parent, "SummaryScroll");
        LayoutElement scrollLe = scrollRoot.gameObject.AddComponent<LayoutElement>();
        scrollLe.minHeight = 140f;
        scrollLe.preferredHeight = 160f;
        scrollLe.flexibleHeight = 1f;

        Image scrollBg = scrollRoot.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(0.16f, 0.14f, 0.12f, 0.98f);

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f;

        RectTransform viewport = CreateChild(scrollRoot, "Viewport");
        StretchFull(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;

        RectTransform content = CreateChild(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup contentVlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentVlg.padding = new RectOffset(8, 8, 8, 8);
        contentVlg.childAlignment = TextAnchor.UpperLeft;
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = true;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;

        scroll.content = content;

        RectTransform textRow = CreateChild(content, "SummaryText");
        LayoutElement textLe = textRow.gameObject.AddComponent<LayoutElement>();
        textLe.flexibleWidth = 1f;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(textRow, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        StretchFull(textRt);

        TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13f;
        tmp.color = new Color(0.82f, 0.78f, 0.72f, 1f);
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.richText = false;
        tmp.lineSpacing = 6f;
        tmp.paragraphSpacing = 8f;
        tmp.text = string.Empty;
        return tmp;
    }

    private static void StretchFull(RectTransform rt, float widthFraction = 1f, float heightFraction = 1f)
    {
        rt.anchorMin = new Vector2(0.5f - widthFraction * 0.5f, 0.5f - heightFraction * 0.5f);
        rt.anchorMax = new Vector2(0.5f + widthFraction * 0.5f, 0.5f + heightFraction * 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static RectTransform CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static TMP_Text CreateLabel(RectTransform parent, string text, float fontSize, FontStyles style)
    {
        RectTransform row = CreateChild(parent, "Label");
        LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = fontSize + 8f;

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(row, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        StretchFull(labelRt);

        TMP_Text tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = new Color(0.93f, 0.9f, 0.84f, 1f);
        tmp.alignment = TextAlignmentOptions.Left;
        return tmp;
    }

    private static TMP_InputField CreateInputField(RectTransform parent)
    {
        RectTransform fieldRoot = CreateChild(parent, "InputField");
        LayoutElement fieldLe = fieldRoot.gameObject.AddComponent<LayoutElement>();
        fieldLe.minHeight = 36f;

        Image bg = fieldRoot.gameObject.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.18f, 0.16f, 1f);

        RectTransform textArea = CreateChild(fieldRoot, "Text Area");
        StretchFull(textArea);

        RectTransform placeholderRt = CreateChild(textArea, "Placeholder");
        StretchFull(placeholderRt);
        TMP_Text placeholder = placeholderRt.gameObject.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Preset name";
        placeholder.fontSize = 14;
        placeholder.color = new Color(0.55f, 0.52f, 0.48f, 0.8f);

        RectTransform textRt = CreateChild(textArea, "Text");
        StretchFull(textRt);
        TMP_Text text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 14;
        text.color = Color.white;

        TMP_InputField input = fieldRoot.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = SkillsManager.AbilityPresetMaxDisplayNameLength;
        return input;
    }

    private static void CreateButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform btnRt = CreateChild(parent, label + "Button");
        LayoutElement le = btnRt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 32f;

        Image img = btnRt.gameObject.AddComponent<Image>();
        img.color = new Color(0.28f, 0.24f, 0.2f, 1f);

        Button button = btnRt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        RectTransform textRt = CreateChild(btnRt, "Text");
        StretchFull(textRt);
        TMP_Text tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.93f, 0.9f, 0.84f, 1f);
    }
}
