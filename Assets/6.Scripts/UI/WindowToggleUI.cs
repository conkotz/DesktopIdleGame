using UnityEngine;
using UnityEngine.UI;

public class WindowToggleUI : MonoBehaviour
{
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI;
    private Button _toolbarButton;

    private void Awake()
    {
        EnsureMenuRef();
        if (!GetMenu())
            Debug.LogWarning("[WindowToggleUI] MainMenuWindowUI not assigned and not found in scene.", this);
    }

    private MainMenuWindowUI GetMenu()
    {
        if (mainMenuWindowUI != null)
            return mainMenuWindowUI;

        return MainMenuWindowUI.Resolve();
    }

    private void EnsureMenuRef()
    {
        if (mainMenuWindowUI == null)
            mainMenuWindowUI = MainMenuWindowUI.Resolve();

        _toolbarButton = GetComponent<Button>();
    }

    public bool IsOpen
    {
        get
        {
            MainMenuWindowUI menu = GetMenu();
            return menu != null && menu.IsOpen;
        }
    }

    public void Toggle()
    {
        MainMenuWindowUI menu = GetMenu();
        if (menu == null)
        {
            Debug.LogWarning("[WindowToggleUI] MainMenuWindowUI not assigned and not found in scene.", this);
            return;
        }

        menu.SelectTab(MainMenuTabId.Character);
    }

    public void Open()
    {
        MainMenuWindowUI menu = GetMenu();
        if (menu == null) return;
        menu.SelectTab(MainMenuTabId.Character);
    }

    public void Close()
    {
        MainMenuWindowUI menu = GetMenu();
        if (menu == null) return;
        if (!menu.IsOpen) return;

        menu.Close();
    }
}