using UnityEngine;

public class MainMenuWindowUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject mainMenuWindow;

    [Header("Pages")]
    [SerializeField] private GameObject characterPage;
    [SerializeField] private GameObject skillsPage;
    [SerializeField] private GameObject abilitiesPage;

    private GameObject currentPage;

    public bool IsOpen => mainMenuWindow != null && mainMenuWindow.activeSelf;
    public GameObject CurrentPage => currentPage;

    private void Awake()
    {
        if (mainMenuWindow)
            mainMenuWindow.SetActive(false);

        HideAllPages();
    }

    public void ToggleCharacter()
    {
        TogglePage(characterPage);
    }

    public void ToggleSkills()
    {
        TogglePage(skillsPage);
    }

    public void ToggleAbilities()
    {
        TogglePage(abilitiesPage);
    }

    public void OpenCharacter()
    {
        OpenPage(characterPage);
    }

    public void OpenSkills()
    {
        OpenPage(skillsPage);
    }

    public void OpenAbilities()
    {
        OpenPage(abilitiesPage);
    }

    public void Close()
    {
        if (mainMenuWindow)
            mainMenuWindow.SetActive(false);

        HideAllPages();
        currentPage = null;
    }

    private void TogglePage(GameObject targetPage)
    {
        if (!targetPage || !mainMenuWindow)
            return;

        if (IsOpen && currentPage == targetPage)
        {
            Close();
            return;
        }

        OpenPage(targetPage);
    }

    private void OpenPage(GameObject targetPage)
    {
        if (!targetPage || !mainMenuWindow)
            return;

        mainMenuWindow.SetActive(true);
        HideAllPages();

        targetPage.SetActive(true);
        currentPage = targetPage;
    }

    private void HideAllPages()
    {
        if (characterPage) characterPage.SetActive(false);
        if (skillsPage) skillsPage.SetActive(false);
        if (abilitiesPage) abilitiesPage.SetActive(false);
    }
}