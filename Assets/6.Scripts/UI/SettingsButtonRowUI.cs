using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SettingsButtonRowUI : MonoBehaviour
{
    [SerializeField] private SettingsButtonActionId actionId = SettingsButtonActionId.ReturnAllWindowsToAnchorPoints;
    [SerializeField] private TMP_Text settingNameText;
    [SerializeField] private TMP_Text buttonLabelText;
    [SerializeField] private Button button;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (button)
        {
            button.onClick.RemoveListener(RunAction);
            button.onClick.AddListener(RunAction);
        }

        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (button)
            button.onClick.RemoveListener(RunAction);
    }

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
        }
    }

    private void ResolveReferences()
    {
        if (!settingNameText)
            settingNameText = transform.Find("SettingName")?.GetComponent<TMP_Text>();
        if (!button)
            button = GetComponentInChildren<Button>(true);
        if (!buttonLabelText && button)
            buttonLabelText = button.GetComponentInChildren<TMP_Text>(true);
    }

    private static string GetSettingName(SettingsButtonActionId id)
    {
        return id switch
        {
            SettingsButtonActionId.ReturnAllWindowsToAnchorPoints => "Resets all windows - size and position",
            SettingsButtonActionId.SwapGameScreen => "Change game screen",
            _ => id.ToString()
        };
    }

    private static string GetButtonLabel(SettingsButtonActionId id)
    {
        return id switch
        {
            SettingsButtonActionId.ReturnAllWindowsToAnchorPoints => "Reset",
            SettingsButtonActionId.SwapGameScreen => "Swap",
            _ => "Run"
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        RefreshDisplay();
    }
#endif
}
