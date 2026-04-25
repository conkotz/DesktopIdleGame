using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class MainMenuWindowUI : MonoBehaviour
{
    private enum PersistedPage
    {
        None,
        Character,
        SkillsAbilities,
        LevelSelect,
        Quest,
        Settings
    }

    private static MainMenuWindowUI s_instance;
    private static bool s_restoreOpen;
    private static PersistedPage s_restorePage = PersistedPage.None;

    private bool _restoredPersistedState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneLoadedRestore()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedRestoreMenu;
        SceneManager.sceneLoaded += OnSceneLoadedRestoreMenu;
    }

    private static void OnSceneLoadedRestoreMenu(Scene scene, LoadSceneMode mode)
    {
        RestoreAnyLoadedMenu();
    }

    public static void CaptureOpenStateForSceneChange()
    {
        MainMenuWindowUI menu = Resolve();
        if (menu == null || !menu.IsOpen)
        {
            s_restoreOpen = false;
            s_restorePage = PersistedPage.None;
            return;
        }

        s_restoreOpen = true;
        s_restorePage = menu.GetPersistedPage(menu.currentPage);
        if (s_restorePage == PersistedPage.None)
            s_restorePage = PersistedPage.Character;
    }

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
    [SerializeField] private string questTitle = "Quests";
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
    [SerializeField] private GameObject questPage;
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

    private void Start()
    {
        RestorePersistedWindowState();
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

    public void ToggleQuest()
    {
        TogglePage(questPage);
    }

    public void OpenQuest()
    {
        if (IsOpen && currentPage == questPage)
        {
            Close();
            return;
        }

        OpenPage(questPage);
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
        s_restoreOpen = false;
        s_restorePage = PersistedPage.None;

        if (mainMenuWindow)
        {
            if (_hideWindowWithCanvasGroup)
                ApplyWindowHiddenVisuals();
            else
                mainMenuWindow.SetActive(false);
        }

        HideAllPages();
        currentPage = null;

        MerchantClick.ForceCloseMerchantMode();
        StorageClick.ForceCloseStorageMode();

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
        StorageClick.ForceCloseStorageMode();

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
        StorageClick.ForceCloseStorageMode();

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

        if (targetPage == levelSelectPage || targetPage == questPage)
            MapNodeTravelProgress.TryMarkCurrentNodeIfConfigured();

        RememberOpenPage(targetPage);

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
        if (questPage) questPage.SetActive(false);
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
        else if (currentPage == questPage)
            headerTitleText.text = questTitle;
        else if (currentPage == settingsPage)
            headerTitleText.text = settingsTitle;
        else
            headerTitleText.text = "";
    }

    private void RestorePersistedWindowState()
    {
        if (_restoredPersistedState)
            return;
        _restoredPersistedState = true;

        if (!s_restoreOpen)
            return;

        GameObject page = ResolvePersistedPage(s_restorePage);
        if (!page)
            page = characterPage;
        if (page)
            OpenPage(page);
    }

    private void RememberOpenPage(GameObject page)
    {
        s_restoreOpen = true;
        s_restorePage = GetPersistedPage(page);
    }

    private PersistedPage GetPersistedPage(GameObject page)
    {
        if (page == characterPage)
            return PersistedPage.Character;
        if (page == skillsAbilitiesPage)
            return PersistedPage.SkillsAbilities;
        if (page == levelSelectPage)
            return PersistedPage.LevelSelect;
        if (page == questPage)
            return PersistedPage.Quest;
        if (page == settingsPage)
            return PersistedPage.Settings;
        return PersistedPage.None;
    }

    private GameObject ResolvePersistedPage(PersistedPage page)
    {
        return page switch
        {
            PersistedPage.Character => characterPage,
            PersistedPage.SkillsAbilities => skillsAbilitiesPage,
            PersistedPage.LevelSelect => levelSelectPage,
            PersistedPage.Quest => questPage,
            PersistedPage.Settings => settingsPage,
            _ => null
        };
    }

    private static void RestoreAnyLoadedMenu()
    {
        if (!s_restoreOpen)
            return;

        MainMenuWindowUI menu = Resolve();
        if (menu == null)
        {
            MainMenuWindowUI[] menus = FindObjectsByType<MainMenuWindowUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (menus != null && menus.Length > 0)
                menu = menus[0];
        }

        if (menu != null)
            menu.RestorePersistedWindowState();
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

