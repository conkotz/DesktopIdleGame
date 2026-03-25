using UnityEngine;

public class MainMenuWindowUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject mainMenuWindow;

    [Header("Pages")]
    [SerializeField] private GameObject characterPage;
    [SerializeField] private GameObject skillsAbilitiesPage;

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

    public void ToggleSkillsAbilities()
    {
        TogglePage(skillsAbilitiesPage);
    }

    public void OpenCharacter()
    {
        OpenPage(characterPage);
    }

    public void OpenSkillsAbilities()
    {
        OpenPage(skillsAbilitiesPage);
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
        if (skillsAbilitiesPage) skillsAbilitiesPage.SetActive(false);
    }
}