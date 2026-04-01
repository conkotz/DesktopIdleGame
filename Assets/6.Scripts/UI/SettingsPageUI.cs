using UnityEngine;

/// <summary>
/// Optional root for the settings page. Hotkeys are handled by <see cref="HotkeySettingsPanelUI"/> /
/// <see cref="HotkeySettingsRowUI"/> under your layout. Use <see cref="CloseSettingsMenu"/> from a Close button.
/// </summary>
public class SettingsPageUI : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField] private MainMenuWindowUI mainMenuWindow;
    [SerializeField] private HotkeySettingsPanelUI hotkeyPanel;

    private void OnEnable()
    {
        if (hotkeyPanel)
            hotkeyPanel.RefreshAll();
    }

    public void CloseSettingsMenu()
    {
        MainMenuWindowUI menu = mainMenuWindow != null ? mainMenuWindow : MainMenuWindowUI.Resolve();
        if (menu != null)
            menu.Close();
    }
}
