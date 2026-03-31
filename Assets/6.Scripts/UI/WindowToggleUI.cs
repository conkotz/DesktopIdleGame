using UnityEngine;

public class WindowToggleUI : MonoBehaviour
{
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI;

    private void Awake()
    {
        if (!mainMenuWindowUI)
            Debug.LogWarning("[WindowToggleUI] MainMenuWindowUI not assigned.");
    }

    public bool IsOpen => mainMenuWindowUI != null && mainMenuWindowUI.IsOpen;

    public void Toggle()
    {
        if (!mainMenuWindowUI)
        {
            Debug.LogWarning("[WindowToggleUI] MainMenuWindowUI is not assigned.");
            return;
        }

        mainMenuWindowUI.ToggleCharacter();
    }

    public void Open()
    {
        if (!mainMenuWindowUI) return;
        mainMenuWindowUI.OpenCharacter();
    }

    public void Close()
    {
        if (!mainMenuWindowUI) return;
        if (!mainMenuWindowUI.IsOpen) return;

        mainMenuWindowUI.Close();
    }
}