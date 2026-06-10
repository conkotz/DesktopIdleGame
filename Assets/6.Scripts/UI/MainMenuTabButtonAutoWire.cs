using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds <see cref="MainMenuTabButtonUI"/> to known title-bar and bottom-bar buttons by GameObject name.
/// </summary>
public static class MainMenuTabButtonAutoWire
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded += (_, _) => WireAllInLoadedScenes();
        WireAllInLoadedScenes();
    }

    private static void WireAllInLoadedScenes()
    {
        EnsureTabsControllerOnMenuWindows();

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;

            if (!t.gameObject.activeInHierarchy)
                continue;

            if (!t.GetComponentInParent<MainMenuWindowUI>(true))
                continue;

            if (MainMenuWindowUI.IsUnderOldUnused(t))
                continue;

            if (!TryResolveTabId(t.name, out MainMenuTabId tabId))
                continue;

            if (!t.GetComponent<Button>())
                continue;

            MainMenuTabButtonUI tab = t.GetComponent<MainMenuTabButtonUI>();
            if (!tab)
                tab = t.gameObject.AddComponent<MainMenuTabButtonUI>();
            tab.SetTabId(tabId);
        }

        RefreshAllMenuTabVisualLists();
    }

    private static void RefreshAllMenuTabVisualLists()
    {
        MainMenuWindowUI[] menus = Object.FindObjectsByType<MainMenuWindowUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            MainMenuWindowUI menu = menus[i];
            if (!menu)
                continue;

            MainMenuWindowTabsUI tabsUi = menu.GetComponent<MainMenuWindowTabsUI>();
            tabsUi?.RebuildTabButtonList();
            tabsUi?.RefreshTabVisuals(force: true);
        }
    }

    private static bool TryResolveTabId(string objectName, out MainMenuTabId tabId)
    {
        tabId = MainMenuTabId.Character;
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        if (IsName(objectName, "CharacterTabButton", "UIButton_Character"))
        {
            tabId = MainMenuTabId.Character;
            return true;
        }

        if (IsName(objectName, "SkillsTabButton", "UIButton_SkillsAbility", "UIButton_SkillsAbilities"))
        {
            tabId = MainMenuTabId.Skills;
            return true;
        }

        if (IsName(objectName, "QuestTabButton", "UIButton_Quest"))
        {
            tabId = MainMenuTabId.Quest;
            return true;
        }

        if (IsName(objectName, "MapTabButton", "UIButton_Map", "UIButton_WorldMap", "WorldMapTabButton", "UIButton_LevelSelect"))
        {
            tabId = MainMenuTabId.WorldMap;
            return true;
        }

        if (IsName(objectName, "UpgradeButton", "UpgradeTabButton", "UIButton_Upgrade"))
        {
            tabId = MainMenuTabId.Upgrade;
            return true;
        }

        if (IsName(objectName, "DatabaseButton", "DatabaseTabButton", "UIButton_Database"))
        {
            tabId = MainMenuTabId.Database;
            return true;
        }

        return false;
    }

    private static void EnsureTabsControllerOnMenuWindows()
    {
        MainMenuWindowUI[] menus = Object.FindObjectsByType<MainMenuWindowUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            MainMenuWindowUI menu = menus[i];
            if (!menu)
                continue;
            if (!menu.GetComponent<MainMenuWindowTabsUI>())
                menu.gameObject.AddComponent<MainMenuWindowTabsUI>();
        }
    }

    private static bool IsName(string actual, params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            if (string.Equals(actual, candidates[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
