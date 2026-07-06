using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SettingsButtonRowUI : MonoBehaviour
{
    const float ButtonPreferredWidth = 96f;
    const float ButtonPreferredHeight = 40f;
    [SerializeField] private SettingsButtonActionId actionId = SettingsButtonActionId.ReturnAllWindowsToAnchorPoints;
    [SerializeField] private TMP_Text settingNameText;
    [SerializeField] private TMP_Text buttonLabelText;
    [SerializeField] private Button button;

    private void Awake()
    {
        ResolveReferences();
        EnsureRowLayoutFitsLongLabel();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (button)
        {
            button.onClick.RemoveListener(RunAction);
            button.onClick.AddListener(RunAction);
        }

        GlobalUserSettings.RestoredDefaults += OnGlobalRestoredDefaults;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (button)
            button.onClick.RemoveListener(RunAction);

        GlobalUserSettings.RestoredDefaults -= OnGlobalRestoredDefaults;
    }

    private void OnGlobalRestoredDefaults() => RefreshDisplay();

    public void RefreshDisplay()
    {
        if (settingNameText)
            settingNameText.text = GetSettingName(actionId);
        if (buttonLabelText)
            buttonLabelText.text = GetButtonLabel(actionId);
    }

    private void RunAction()
    {
        switch (actionId)
        {
            case SettingsButtonActionId.ReturnAllWindowsToAnchorPoints:
                UIWindowPositionMemory.ResetAllWindowsToAnchors();
                break;

            case SettingsButtonActionId.SwapGameScreen:
                MonitorSwitcher switcher = FindFirstObjectByType<MonitorSwitcher>(FindObjectsInactive.Include);
                if (switcher)
                    switcher.SwapToNextMonitor();
                break;

            case SettingsButtonActionId.FactoryResetAllSettings:
                GlobalUserSettings.RestoreAllToDefaults();
                break;

            case SettingsButtonActionId.ResetTownObjectsToOriginalPositions:
                WorldObjectPositionStore.ResetTownObjectsToOriginalPositions();
                break;
        }
    }

    private void ResolveReferences()
    {
        if (!settingNameText)
        {
            Transform labelTf = transform.Find("SettingName") ?? transform.Find("ActionLabelText");
            settingNameText = labelTf ? labelTf.GetComponent<TMP_Text>() : null;
        }

        if (!button)
            button = GetComponentInChildren<Button>(true);
        if (!buttonLabelText && button)
            buttonLabelText = button.GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>
    /// Keeps the action button on-screen: label takes remaining width and wraps; row height grows with wrapped text.
    /// </summary>
    private void EnsureRowLayoutFitsLongLabel()
    {
        var hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
        }

        if (settingNameText)
        {
            GameObject labelGo = settingNameText.gameObject;
            var le = labelGo.GetComponent<LayoutElement>() ?? labelGo.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.preferredWidth = -1f;
            le.flexibleWidth = 1f;
            le.minHeight = 0f;
            le.preferredHeight = -1f;
            le.flexibleHeight = 0f;
            le.layoutPriority = 1;

            var fitter = labelGo.GetComponent<ContentSizeFitter>() ?? labelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform rt = settingNameText.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            settingNameText.textWrappingMode = TextWrappingModes.Normal;
            settingNameText.overflowMode = TextOverflowModes.Overflow;
        }

        if (button)
        {
            var ble = button.gameObject.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            ble.minWidth = ButtonPreferredWidth;
            ble.preferredWidth = ButtonPreferredWidth;
            ble.flexibleWidth = 0f;
            ble.minHeight = ButtonPreferredHeight;
            ble.preferredHeight = ButtonPreferredHeight;
            ble.flexibleHeight = 0f;
            ble.layoutPriority = 2;
        }

        var rowFitter = GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
        rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (TryGetComponent(out LayoutElement rowLe))
        {
            rowLe.preferredHeight = -1;
            rowLe.minHeight = 0f;
        }
    }

    private static string GetSettingName(SettingsButtonActionId id)
    {
        return id switch
        {
            SettingsButtonActionId.ReturnAllWindowsToAnchorPoints => "Resets all windows - size and position",
            SettingsButtonActionId.SwapGameScreen => "Change game screen",
            SettingsButtonActionId.FactoryResetAllSettings =>
                "Reset ALL settings to defaults (toggles, sliders, hotkeys, window positions)",
            SettingsButtonActionId.ResetTownObjectsToOriginalPositions =>
                "Reset town objects to original positions",
            _ => id.ToString()
        };
    }

    private static string GetButtonLabel(SettingsButtonActionId id)
    {
        return id switch
        {
            SettingsButtonActionId.ReturnAllWindowsToAnchorPoints => "Reset",
            SettingsButtonActionId.SwapGameScreen => "Swap",
            SettingsButtonActionId.FactoryResetAllSettings => "Reset",
            SettingsButtonActionId.ResetTownObjectsToOriginalPositions => "Reset",
            _ => "Run"
        };
    }

#if UNITY_EDITOR
    private bool _editorLayoutDelayScheduled;

    private void OnValidate()
    {
        ResolveReferences();
        RefreshDisplay();

        // Changing RectTransforms / layout during OnValidate triggers
        // "SendMessage cannot be called during ... OnValidate (ActionLabelText: OnRectTransformDimensionsChange)".
        // Defer layout to the next editor tick.
        if (_editorLayoutDelayScheduled)
            return;
        _editorLayoutDelayScheduled = true;
        UnityEditor.EditorApplication.delayCall += EditorDeferredEnsureRowLayout;
    }

    private void EditorDeferredEnsureRowLayout()
    {
        _editorLayoutDelayScheduled = false;
        if (this == null || gameObject == null)
            return;
        EnsureRowLayoutFitsLongLabel();
    }
#endif
}
