using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared tab selection for <see cref="MainMenuWindowUI"/> (title-bar tabs + bottom bag buttons).
/// Active tab uses <see cref="activeTabColor"/>; inactive tabs use <see cref="inactiveTabColor"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuWindowTabsUI : MonoBehaviour
{
    [Serializable]
    public class TabButtonVisual
    {
        public MainMenuTabId tabId = MainMenuTabId.Character;
        [Tooltip("Usually the tab Button's Image (target graphic).")]
        public Graphic graphic;
    }

    [SerializeField] private MainMenuWindowUI mainMenu;
    [SerializeField] private List<TabButtonVisual> tabButtons = new();

    [Header("Tab colours")]
    [SerializeField] private Color activeTabColor = new Color32(247, 225, 190, 255);
    [SerializeField] private Color inactiveTabColor = new Color32(168, 152, 118, 200);

    private MainMenuTabId _lastVisualTab = MainMenuTabId.None;

    private void Awake()
    {
        if (!mainMenu)
            mainMenu = GetComponent<MainMenuWindowUI>();
        if (!mainMenu)
            mainMenu = MainMenuWindowUI.Resolve();

        CollectTabButtonsFromChildren();
    }

    private void OnEnable()
    {
        if (!mainMenu)
            mainMenu = MainMenuWindowUI.Resolve();
        CollectTabButtonsFromChildren();
        RefreshTabVisuals(force: true);
    }

    private void LateUpdate()
    {
        if (!mainMenu || !mainMenu.IsOpen)
        {
            if (_lastVisualTab != MainMenuTabId.None)
            {
                _lastVisualTab = MainMenuTabId.None;
                ApplyDimmedToAll();
            }
            return;
        }

        MainMenuTabId active = mainMenu.GetActiveTab();
        if (active != _lastVisualTab)
            RefreshTabVisuals(active);
    }

    public void SelectTab(MainMenuTabId tab)
    {
        if (!mainMenu)
            mainMenu = MainMenuWindowUI.Resolve();
        if (!mainMenu)
            return;

        mainMenu.SelectTab(tab);
        RefreshTabVisuals(tab);
    }

    public void RefreshTabVisuals(bool force = false)
    {
        if (!mainMenu)
            return;
        RefreshTabVisuals(mainMenu.IsOpen ? mainMenu.GetActiveTab() : MainMenuTabId.None, force);
    }

    private void RefreshTabVisuals(MainMenuTabId active, bool force = false)
    {
        if (!force && active == _lastVisualTab)
            return;

        _lastVisualTab = active;

        for (int i = 0; i < tabButtons.Count; i++)
        {
            TabButtonVisual entry = tabButtons[i];
            if (entry == null || !entry.graphic)
                continue;

            bool isActive = entry.tabId == active;
            entry.graphic.color = isActive ? activeTabColor : inactiveTabColor;
        }
    }

    private void ApplyDimmedToAll()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            TabButtonVisual entry = tabButtons[i];
            if (entry != null && entry.graphic)
                entry.graphic.color = inactiveTabColor;
        }
    }

    private void CollectTabButtonsFromChildren()
    {
        MainMenuTabButtonUI[] found = GetComponentsInChildren<MainMenuTabButtonUI>(true);
        for (int i = 0; i < found.Length; i++)
        {
            MainMenuTabButtonUI tabBtn = found[i];
            if (!tabBtn)
                continue;

            Graphic g = tabBtn.GetComponent<Graphic>();
            if (!g)
            {
                Button b = tabBtn.GetComponent<Button>();
                if (b)
                    g = b.targetGraphic;
            }

            if (!g)
                continue;

            tabButtons.Add(new TabButtonVisual
            {
                tabId = tabBtn.TabId,
                graphic = g
            });
        }
    }

    private int FindEntry(MainMenuTabId id)
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] != null && tabButtons[i].tabId == id)
                return i;
        }

        return -1;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!mainMenu)
            mainMenu = GetComponent<MainMenuWindowUI>();
    }
#endif
}
