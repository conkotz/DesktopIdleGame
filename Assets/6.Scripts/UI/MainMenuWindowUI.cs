using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class MainMenuWindowUI : MonoBehaviour
{
    private static MainMenuWindowUI s_instance;

    /// <summary>
    /// Returns the in-scene menu (cached). Use from toolbar buttons when serialized references are missing or stale.
    /// </summary>
    public static MainMenuWindowUI Resolve()
    {
        if (s_instance != null)
            return s_instance;

        s_instance = FindFirstObjectByType<MainMenuWindowUI>(FindObjectsInactive.Include);
        return s_instance;
    }

    [Header("Root")]
    [SerializeField] private GameObject mainMenuWindow;

    [Header("Header")]
    [SerializeField] private TMP_Text headerTitleText;
    [SerializeField] private string characterTitle = "Character";
    [SerializeField] private string skillsTitle = "Skills & Abilities";
    [FormerlySerializedAs("worldMapTitle")]
    [SerializeField] private string levelSelectTitle = "Level select";
    [SerializeField] private string settingsTitle = "Settings";

    [Header("Optional UI gating")]
    [Tooltip("If set, we force this CanvasGroup to be interactable when opening pages (prevents first-open issues).")]
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;

    [Header("Keyboard")]
    [Tooltip("e.g. Character toolbar Button. After the menu closes, keyboard Submit returns here so Enter/Space work on the bar again.")]
    [SerializeField] private Selectable keyboardToolbarFocusAfterClose;

    [Header("Pages")]
    [SerializeField] private GameObject characterPage;
    [SerializeField] private GameObject skillsAbilitiesPage;
    [FormerlySerializedAs("worldMapPage")]
    [SerializeField] private GameObject levelSelectPage;
    [SerializeField] private GameObject settingsPage;

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
        if (s_instance != null && s_instance != this)
            Debug.LogWarning("[MainMenuWindowUI] Multiple MainMenuWindowUI components in loaded scenes; the last Awake wins for Resolve().", this);

        s_instance = this;

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
        if (IsOpen && currentPage == levelSelectPage)
        {
            Close();
            return;
        }

        OpenPage(levelSelectPage);
    }

    public void ToggleSettings()
    {
        TogglePage(settingsPage);
    }

    public void OpenSettings()
    {
        OpenPage(settingsPage);
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

        HotkeySettingsRowUI.EnsureUiInputModulesEnabled();
        RestoreToolbarKeyboardFocus();
    }

    private void LateUpdate()
    {
        if (IsOpen)
            return;

        SanitizeKeyboardSelectionWhenMenuClosed();
    }

    /// <summary>
    /// EventSystem can keep a selected control under the (now inactive) settings page; Submit then does nothing. Fix when menu is closed.
    /// </summary>
    private void SanitizeKeyboardSelectionWhenMenuClosed()
    {
        if (EventSystem.current == null)
            return;

        GameObject sel = EventSystem.current.currentSelectedGameObject;
        if (sel == null)
            return;

        if (!sel.activeInHierarchy)
        {
            RestoreToolbarKeyboardFocus();
            return;
        }

        if (sel.TryGetComponent(out Selectable s) && !s.IsInteractable())
            RestoreToolbarKeyboardFocus();
    }

    private void RestoreToolbarKeyboardFocus()
    {
        if (EventSystem.current == null)
            return;

        if (keyboardToolbarFocusAfterClose != null &&
            keyboardToolbarFocusAfterClose.gameObject.activeInHierarchy &&
            keyboardToolbarFocusAfterClose.IsInteractable())
        {
            keyboardToolbarFocusAfterClose.Select();
        }
        else
        {
            // Drop stale selection (e.g. hidden settings row) so Tab / navigation can recover.
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void TogglePage(GameObject targetPage)
    {
        // Merchant/shop mode can leave stale UI state that blocks normal menu interactions.
        // Always clear it when user explicitly toggles a main menu tab.
        MerchantClick.ForceCloseMerchantMode();

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
        // Rebind flow can leave InputSystemUIInputModule disabled; bottom bar stops receiving keyboard Submit.
        HotkeySettingsRowUI.EnsureUiInputModulesEnabled();

        // Ensure merchant mode never blocks opening pages.
        MerchantClick.ForceCloseMerchantMode();

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

        // Always activate the window root. UIWindowCloseButton (and similar) may SetActive(false) on this
        // GameObject; in canvas-group hide mode we previously skipped SetActive(true) and the menu could never reopen.
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
        if (settingsPage) settingsPage.SetActive(false);
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
        else if (currentPage == settingsPage)
            headerTitleText.text = settingsTitle;
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
        // Do not set interactable = false: it disables every Selectable under this root (including in-window tab rows
        // until the next open). Visibility/input blocking uses alpha + blocksRaycasts instead.
        mainMenuCanvasGroup.blocksRaycasts = false;

        if (_windowRootImage)
            _windowRootImage.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (s_instance == this)
            s_instance = null;
    }
}

