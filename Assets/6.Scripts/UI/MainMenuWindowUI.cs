using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuWindowUI : MonoBehaviour
{
    private enum PersistedPage
    {
        None,
        Character,
        SkillsAbilities,
        WorldMap,
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
    /// Clears the cross-scene "reopen main menu" snapshot (used after death-respawn). Prevents stale restore intent when
    /// returning to Bootstrap from logout instead of reloading GamePlay.
    /// </summary>
    public static void CancelPersistedOpenRestore()
    {
        s_restoreOpen = false;
        s_restorePage = PersistedPage.None;
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

    [Header("Optional UI gating")]
    [Tooltip("If set, we force this CanvasGroup to be interactable when opening pages (prevents first-open issues).")]
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;

    [Header("Keyboard")]
    [Tooltip("e.g. Character toolbar Button. After the menu closes, keyboard Submit returns here so Enter/Space work on the bar again.")]
    [SerializeField] private Selectable keyboardToolbarFocusAfterClose;

    [Header("Pages")]
    [SerializeField] private GameObject characterPage;
    [SerializeField] private GameObject skillsAbilitiesPage;
    [SerializeField] private GameObject fullMapPage;
    [SerializeField] private GameObject questPage;
    [SerializeField] private GameObject settingsPage;

    private GameObject currentPage;
    private Image _windowRootImage;
    private Transform _menuTabsBar;

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

    public GameObject FullMapPage => fullMapPage;

    /// <summary>True when <paramref name="t"/> is under a hierarchy root named OLD_UNUSED.</summary>
    public static bool IsUnderOldUnused(Transform t)
    {
        while (t != null)
        {
            if (string.Equals(t.name, "OLD_UNUSED", StringComparison.OrdinalIgnoreCase))
                return true;
            t = t.parent;
        }

        return false;
    }

    /// <summary>Which main-menu tab is highlighted. <see cref="MainMenuTabId.None"/> when Settings (no tab) is open.</summary>
    public MainMenuTabId GetActiveTab()
    {
        if (!IsOpen || !currentPage)
            return MainMenuTabId.None;

        if (settingsPage && currentPage == settingsPage)
            return MainMenuTabId.None;

        if (currentPage == characterPage)
            return MainMenuTabId.Character;
        if (currentPage == skillsAbilitiesPage)
            return MainMenuTabId.Skills;
        if (currentPage == questPage)
            return MainMenuTabId.Quest;
        if (fullMapPage && currentPage == fullMapPage)
            return MainMenuTabId.WorldMap;

        return MainMenuTabId.None;
    }

    /// <summary>
    /// Opens the requested tab. Re-clicking the active tab does nothing (menu stays open).
    /// Title-bar tabs and bottom bag buttons should call this via <see cref="MainMenuWindowTabsUI"/>.
    /// </summary>
    public void SelectTab(MainMenuTabId tab)
    {
        if (IsOpen && GetActiveTab() == tab)
        {
            if (TryKeepOpenForHelperWhitelist(tab))
                EnsureWindowInteractable();
            return;
        }

        switch (tab)
        {
            case MainMenuTabId.Character:
                OpenCharacterShow();
                break;
            case MainMenuTabId.Skills:
                OpenSkillsAbilitiesShow();
                break;
            case MainMenuTabId.Quest:
                OpenQuestShow();
                break;
            case MainMenuTabId.WorldMap:
                OpenWorldMapShow();
                break;
        }
    }

    /// <summary>Outer menu shell used to pin merchant/storage windows beside the Character window.</summary>
    public RectTransform MenuWindowRect => mainMenuWindow != null ? mainMenuWindow.transform as RectTransform : null;

    public bool TryGetMenuWindowRect(out RectTransform rect)
    {
        rect = MenuWindowRect;
        return rect != null;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
            Debug.LogWarning("[MainMenuWindowUI] Multiple MainMenuWindowUI components in loaded scenes; the last Awake wins for Resolve().", this);

        s_instance = this;
        SanitizePageReferences();
        ResolveSkillsAbilitiesPageReference();

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
        ResolveFullMapPage();
    }

    private void Start()
    {
        RestorePersistedWindowState();
    }

    public void ToggleCharacter() => SelectTab(MainMenuTabId.Character);

    public void ToggleSkillsAbilities() => SelectTab(MainMenuTabId.Skills);

    public void OpenCharacter() => SelectTab(MainMenuTabId.Character);

    public void OpenSkillsAbilities() => SelectTab(MainMenuTabId.Skills);

    public void OpenWorldMap() => SelectTab(MainMenuTabId.WorldMap);

    public void OpenWorldMapShow()
    {
        ResolveFullMapPage();
        if (!fullMapPage)
            return;

        if (IsOpen && currentPage == fullMapPage)
            return;

        OpenPage(fullMapPage);
    }

    public void ToggleQuest() => SelectTab(MainMenuTabId.Quest);

    public void ToggleWorldMap() => SelectTab(MainMenuTabId.WorldMap);

    public void OpenQuest() => SelectTab(MainMenuTabId.Quest);

    public void OpenQuestShow()
    {
        if (!questPage)
            return;
        if (IsOpen && currentPage == questPage)
            return;
        OpenPage(questPage);
    }

    public void OpenCharacterShow()
    {
        if (!characterPage)
            return;
        if (IsOpen && currentPage == characterPage)
            return;
        OpenPage(characterPage);
    }

    public void OpenSkillsAbilitiesShow()
    {
        if (!skillsAbilitiesPage)
            return;
        if (IsOpen && currentPage == skillsAbilitiesPage)
            return;
        OpenPage(skillsAbilitiesPage);
    }

    public void ToggleSettings()
    {
        QuickMenuPanelToggleUI.HideIfOpen();
        TogglePage(settingsPage);
    }

    public void OpenSettings()
    {
        OpenPage(settingsPage);
    }

    public void Close()
    {
        if (mainMenuWindow && UIWindowCloseButton.BlocksClose(mainMenuWindow))
            return;

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
            EnsureWindowInteractable();
            return;
        }

        OpenPage(targetPage);
    }

    private bool TryKeepOpenForHelperWhitelist(MainMenuTabId tab)
    {
        string id = tab switch
        {
            MainMenuTabId.Character => HelperWhitelistUiInteractTarget.CharacterToolbarWhitelistId,
            MainMenuTabId.Skills => HelperWhitelistUiInteractTarget.SkillsAbilityToolbarWhitelistId,
            MainMenuTabId.Quest => HelperWhitelistUiInteractTarget.QuestToolbarWhitelistId,
            MainMenuTabId.WorldMap =>
                HelperWhitelistUiInteractTarget.LevelSelectToolbarWhitelistId,
            _ => null
        };

        return !string.IsNullOrWhiteSpace(id) &&
               HelperGameplayController.KeepMainMenuOpenWhenRepeatingToolbarTap(id);
    }

    private void ResolveFullMapPage()
    {
        if (fullMapPage)
            return;

        if (mainMenuWindow)
        {
            Transform content = mainMenuWindow.transform.Find("ContentRoot");
            if (content)
            {
                Transform t = content.Find("FullMapPage");
                if (t)
                    fullMapPage = t.gameObject;
            }
        }

        if (!fullMapPage)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                    continue;
                if (string.Equals(t.name, "FullMapPage", System.StringComparison.OrdinalIgnoreCase))
                {
                    fullMapPage = t.gameObject;
                    break;
                }
            }
        }
    }

    private void OpenPage(GameObject targetPage)
    {
        // Rebind flow can leave InputSystemUIInputModule disabled; bottom bar stops receiving keyboard Submit.
        HotkeySettingsRowUI.EnsureUiInputModulesEnabled();

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

        bool wasOpen = IsOpen;

        // Already on this page: avoid HideAllPages / SetActive churn so child UIs (e.g. inventory grid) don't
        // OnDisable/OnEnable and replay hide-until-layout — fixes flicker when opening a shop while Character is visible.
        if (IsOpen && currentPage == targetPage)
        {
            EnsureWindowInteractable();
            return;
        }

        // Ensure merchant mode never blocks opening pages.
        MerchantClick.ForceCloseMerchantMode();
        StorageClick.ForceCloseStorageMode();

        if (targetPage == questPage)
            MapNodeTravelProgress.TryMarkCurrentNodeIfConfigured();

        RememberOpenPage(targetPage);

        // Always activate the window root. UIWindowCloseButton (and similar) may SetActive(false) on this
        // GameObject; in canvas-group hide mode we previously skipped SetActive(true) and the menu could never reopen.
        mainMenuWindow.SetActive(true);
        if (!wasOpen && mainMenuWindow.transform.parent != null)
            mainMenuWindow.transform.SetAsLastSibling();

        EnsureWindowInteractable();
        HideAllPages();

        targetPage.SetActive(true);
        currentPage = targetPage;

        if (!_hideWindowWithCanvasGroup &&
            (!mainMenuWindow.activeSelf || !targetPage.activeSelf))
        {
            mainMenuWindow.SetActive(true);
            HideAllPages();
            targetPage.SetActive(true);
            currentPage = targetPage;
        }

        BringMenuTabsBarToFront();
    }

    private void HideAllPages()
    {
        if (characterPage) characterPage.SetActive(false);
        if (skillsAbilitiesPage) skillsAbilitiesPage.SetActive(false);
        if (fullMapPage) fullMapPage.SetActive(false);
        if (questPage) questPage.SetActive(false);
        if (settingsPage) settingsPage.SetActive(false);
        HideLegacyMenuPages();
    }

    /// <summary>Keep archived pages under OLD_UNUSED inactive even if something still references them.</summary>
    private void HideLegacyMenuPages()
    {
        if (!mainMenuWindow)
            return;

        Transform legacyRoot = FindNamedTransformInScene("OLD_UNUSED");
        if (legacyRoot != null)
        {
            legacyRoot.gameObject.SetActive(false);
            return;
        }

        Transform legacy = mainMenuWindow.transform.Find("SkillsAbilityPage");
        if (legacy != null && legacy.gameObject != skillsAbilitiesPage)
            legacy.gameObject.SetActive(false);
    }

    private static Transform FindNamedTransformInScene(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (string.Equals(t.name, objectName, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private void SanitizePageReferences()
    {
        if (skillsAbilitiesPage != null && IsUnderOldUnused(skillsAbilitiesPage.transform))
            skillsAbilitiesPage = null;
    }

    /// <summary>Later siblings draw on top — keep the tab bar above page content (e.g. SkillsAbilityPageNEW).</summary>
    private void BringMenuTabsBarToFront()
    {
        ResolveMenuTabsBar();
        if (_menuTabsBar != null)
            _menuTabsBar.SetAsLastSibling();
    }

    private void ResolveMenuTabsBar()
    {
        if (_menuTabsBar != null)
            return;

        if (mainMenuWindow != null)
            _menuTabsBar = mainMenuWindow.transform.Find("MenuTabsBar");
    }

    /// <summary>Uses SkillsAbilityPageNEW when present; does not move or resize any UI.</summary>
    private void ResolveSkillsAbilitiesPageReference()
    {
        if (skillsAbilitiesPage != null &&
            string.Equals(skillsAbilitiesPage.name, "SkillsAbilityPageNEW", System.StringComparison.Ordinal))
            return;

        SkillsAbilityPageNewUI newPageUi =
            FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);
        if (newPageUi != null && !IsUnderOldUnused(newPageUi.transform))
            skillsAbilitiesPage = newPageUi.gameObject;
        else if (mainMenuWindow != null)
        {
            Transform content = mainMenuWindow.transform.Find("ContentRoot");
            Transform page = content != null ? content.Find("SkillsAbilityPageNEW") : null;
            if (page != null)
                skillsAbilitiesPage = page.gameObject;
        }
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
        if (fullMapPage && page == fullMapPage)
            return PersistedPage.WorldMap;

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
            PersistedPage.WorldMap => fullMapPage,
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

