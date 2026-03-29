using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
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
    private Image _windowRootImage;

    /// <summary>
    /// True when this script lives on the same object as <see cref="mainMenuWindow"/>.
    /// Then we must NOT SetActive(false) on that object — it would disable this component and break toolbar onClick targets.
    /// We hide with CanvasGroup instead.
    /// </summary>
    private bool _hideWindowWithCanvasGroup;

    public bool IsOpen
    {
        get
        {
            if (!mainMenuWindow) return false;
            if (_hideWindowWithCanvasGroup && mainMenuCanvasGroup)
                return mainMenuCanvasGroup.alpha > 0.001f;
            return mainMenuWindow.activeSelf;
        }
    }

    public GameObject CurrentPage => currentPage;

    private void Awake()
    {
        if (!mainMenuWindow)
            return;

        _hideWindowWithCanvasGroup = mainMenuWindow == gameObject;

        if (_hideWindowWithCanvasGroup)
        {
            if (!mainMenuCanvasGroup)
                mainMenuCanvasGroup = mainMenuWindow.GetComponent<CanvasGroup>();
            if (!mainMenuCanvasGroup)
                mainMenuCanvasGroup = mainMenuWindow.AddComponent<CanvasGroup>();

            _windowRootImage = mainMenuWindow.GetComponent<Image>();
            ApplyWindowHiddenVisuals();
        }
        else
        {
            mainMenuWindow.SetActive(false);
            if (!mainMenuCanvasGroup)
                mainMenuCanvasGroup = mainMenuWindow.GetComponent<CanvasGroup>();
        }

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

    public void ToggleLevelSelect()
    {
        TogglePage(levelSelectPage);
    }

    public void OpenLevelSelect()
    {
        OpenPage(levelSelectPage);
    }

    public void Close()
    {
        if (mainMenuWindow)
        {
            if (_hideWindowWithCanvasGroup)
                ApplyWindowHiddenVisuals();
            else
                mainMenuWindow.SetActive(false);
        }

        HideAllPages();
        currentPage = null;
    }

    private void TogglePage(GameObject targetPage)
    {
        if (!mainMenuWindow)
        {
            Debug.LogError("[MainMenuWindowUI] mainMenuWindow is not assigned — window buttons will do nothing.", this);
            return;
        }

        if (!targetPage)
        {
            Debug.LogError("[MainMenuWindowUI] Target page is not assigned — hook this tab to the correct page GameObject.", this);
            return;
        }

        if (IsOpen && currentPage == targetPage)
        {
            Close();
            return;
        }

        OpenPage(targetPage);
    }

    private void OpenPage(GameObject targetPage)
    {
        if (!mainMenuWindow)
        {
            Debug.LogError("[MainMenuWindowUI] mainMenuWindow is not assigned — cannot open pages.", this);
            return;
        }

        if (!targetPage)
        {
            Debug.LogError("[MainMenuWindowUI] Target page is not assigned — cannot open this tab.", this);
            return;
        }

        if (!_hideWindowWithCanvasGroup)
            mainMenuWindow.SetActive(true);

        EnsureWindowInteractable();
        HideAllPages();

        targetPage.SetActive(true);
        currentPage = targetPage;
        RefreshHeaderTitle();

        if (!_hideWindowWithCanvasGroup &&
            (!mainMenuWindow.activeSelf || !targetPage.activeSelf))
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

    private void EnsureWindowInteractable()
    {
        if (!mainMenuCanvasGroup) return;

        if (mainMenuCanvasGroup.alpha <= 0.001f)
            mainMenuCanvasGroup.alpha = 1f;

        if (!mainMenuCanvasGroup.blocksRaycasts)
            mainMenuCanvasGroup.blocksRaycasts = true;

        if (!mainMenuCanvasGroup.interactable)
            mainMenuCanvasGroup.interactable = true;

        if (_hideWindowWithCanvasGroup && _windowRootImage)
            _windowRootImage.raycastTarget = true;
    }

    /// <summary>
    /// When the window is visually hidden but the GameObject stays active, a root Image with
    /// Raycast Target can still win hit tests over the bottom bar. Turn it off while closed.
    /// </summary>
    private void ApplyWindowHiddenVisuals()
    {
        if (!mainMenuCanvasGroup) return;

        mainMenuCanvasGroup.alpha = 0f;
        mainMenuCanvasGroup.interactable = false;
        mainMenuCanvasGroup.blocksRaycasts = false;

        if (_windowRootImage)
            _windowRootImage.raycastTarget = false;
    }
}
