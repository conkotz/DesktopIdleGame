using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

public class MainMenuWindowUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject mainMenuWindow;

    [Header("Header")]
    [SerializeField] private TMP_Text headerTitleText;
    [SerializeField] private string characterTitle = "Character";
    [SerializeField] private string skillsTitle = "Skills & Abilities";
    [FormerlySerializedAs("worldMapTitle")]
    [SerializeField] private string levelSelectTitle = "Level select";

    [Header("Optional UI gating")]
    [Tooltip("If set, we force this CanvasGroup to be interactable when opening pages (prevents first-open issues).")]
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;

    [Header("Pages")]
    [SerializeField] private GameObject characterPage;
    [SerializeField] private GameObject skillsAbilitiesPage;
    [FormerlySerializedAs("worldMapPage")]
    [SerializeField] private GameObject levelSelectPage;

    private GameObject currentPage;
    private bool _loggedFirstCharacterOpen;
    private bool _loggedFirstSkillsOpen;
    private bool _loggedFirstLevelSelectOpen;

    public bool IsOpen => mainMenuWindow != null && mainMenuWindow.activeSelf;
    public GameObject CurrentPage => currentPage;

    private void Awake()
    {
        if (mainMenuWindow)
            mainMenuWindow.SetActive(false);

        if (!mainMenuCanvasGroup && mainMenuWindow)
            mainMenuCanvasGroup = mainMenuWindow.GetComponent<CanvasGroup>();

        HideAllPages();
    }

    public void ToggleCharacter()
    {
        LogFirstOpenAttempt("Character", ref _loggedFirstCharacterOpen);
        TogglePage(characterPage);
    }

    public void ToggleSkillsAbilities()
    {
        LogFirstOpenAttempt("Skills", ref _loggedFirstSkillsOpen);
        TogglePage(skillsAbilitiesPage);
    }

    public void OpenCharacter()
    {
        LogFirstOpenAttempt("Character", ref _loggedFirstCharacterOpen);
        OpenPage(characterPage);
    }

    public void OpenSkillsAbilities()
    {
        LogFirstOpenAttempt("Skills", ref _loggedFirstSkillsOpen);
        OpenPage(skillsAbilitiesPage);
    }

    public void ToggleLevelSelect()
    {
        LogFirstOpenAttempt("LevelSelect", ref _loggedFirstLevelSelectOpen);
        TogglePage(levelSelectPage);
    }

    public void OpenLevelSelect()
    {
        LogFirstOpenAttempt("LevelSelect", ref _loggedFirstLevelSelectOpen);
        OpenPage(levelSelectPage);
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
        EnsureWindowInteractable();
        HideAllPages();

        targetPage.SetActive(true);
        currentPage = targetPage;
        RefreshHeaderTitle();

        // Safeguard: if something else toggles visibility in the same frame,
        // force the desired state once more.
        if (!mainMenuWindow.activeSelf || !targetPage.activeSelf)
        {
            mainMenuWindow.SetActive(true);
            HideAllPages();
            targetPage.SetActive(true);
            currentPage = targetPage;
            RefreshHeaderTitle();
        }
    }

    private void HideAllPages()
    {
        if (characterPage) characterPage.SetActive(false);
        if (skillsAbilitiesPage) skillsAbilitiesPage.SetActive(false);
        if (levelSelectPage) levelSelectPage.SetActive(false);
    }

    private void RefreshHeaderTitle()
    {
        if (!headerTitleText) return;

        if (currentPage == characterPage)
            headerTitleText.text = characterTitle;
        else if (currentPage == skillsAbilitiesPage)
            headerTitleText.text = skillsTitle;
        else if (currentPage == levelSelectPage)
            headerTitleText.text = levelSelectTitle;
        else
            headerTitleText.text = "";
    }

    private void LogFirstOpenAttempt(string which, ref bool loggedFlag)
    {
        if (loggedFlag) return;
        loggedFlag = true;

        string windowName = mainMenuWindow ? mainMenuWindow.name : "<null>";
        string charName = characterPage ? characterPage.name : "<null>";
        string skillsName = skillsAbilitiesPage ? skillsAbilitiesPage.name : "<null>";
        string cg = mainMenuCanvasGroup
            ? $"CanvasGroup(alpha={mainMenuCanvasGroup.alpha:0.###}, blocks={mainMenuCanvasGroup.blocksRaycasts}, interact={mainMenuCanvasGroup.interactable})"
            : "CanvasGroup(<none>)";
    }

    private void EnsureWindowInteractable()
    {
        if (!mainMenuCanvasGroup) return;

        // If some other script (or a previous prewarm) left this in a non-interactable state,
        // the first click can appear to "do nothing" even though the page toggled.
        if (mainMenuCanvasGroup.alpha <= 0.001f)
            mainMenuCanvasGroup.alpha = 1f;

        if (!mainMenuCanvasGroup.blocksRaycasts)
            mainMenuCanvasGroup.blocksRaycasts = true;

        if (!mainMenuCanvasGroup.interactable)
            mainMenuCanvasGroup.interactable = true;
    }
}