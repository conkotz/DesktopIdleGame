using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

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
        public TMP_Text label;
    }

    [SerializeField] private MainMenuWindowUI mainMenu;
    private readonly List<TabButtonVisual> tabButtons = new();
    private readonly List<ExplicitButtonBinding> _explicitButtonBindings = new();

    [Header("Tab colours")]
    [SerializeField] private Color activeTabColor = new Color32(247, 225, 190, 255);
    [SerializeField] private Color inactiveTabColor = new Color32(168, 152, 118, 200);
    [SerializeField] private Color activeTabTextColor = new Color(0.17254902f, 0.14117648f, 0.11372549f, 1f);
    [SerializeField] private Color inactiveTabTextColor = new Color(0.6830188f, 0.63510895f, 0.5812103f, 1f);

    [Header("Optional MenuTabsBar Buttons")]
    [Tooltip("Assign the top Character tab button here to wire it explicitly.")]
    [SerializeField] private Button characterTabButton;
    [Tooltip("Assign the top Skills and Abilities tab button here to wire it explicitly.")]
    [SerializeField] private Button skillsTabButton;
    [Tooltip("Assign the top Quest tab button here to wire it explicitly.")]
    [SerializeField] private Button questTabButton;
    [Tooltip("Assign the top World Map tab button here to wire it explicitly.")]
    [SerializeField] private Button worldMapTabButton;
    [Tooltip("Assign the top Upgrade tab button here to wire it explicitly.")]
    [SerializeField] private Button upgradeTabButton;

    private MainMenuTabId _lastVisualTab = MainMenuTabId.None;

    private sealed class ExplicitButtonBinding
    {
        public Button button;
        public UnityAction handler;
    }

    private void Awake()
    {
        if (!mainMenu)
            mainMenu = GetComponent<MainMenuWindowUI>();
        if (!mainMenu)
            mainMenu = MainMenuWindowUI.Resolve();

        RebuildTabButtonList();
    }

    private void OnEnable()
    {
        if (!mainMenu)
            mainMenu = MainMenuWindowUI.Resolve();
        WireExplicitButtons();
        RebuildTabButtonList();
        RefreshTabVisuals(force: true);
    }

    private void OnDisable()
    {
        UnwireExplicitButtons();
    }

    /// <summary>Refreshes tab button graphics after <see cref="MainMenuTabButtonUI"/> is added at runtime.</summary>
    public void RebuildTabButtonList()
    {
        tabButtons.Clear();
        CollectExplicitAssignedButtons();
        CollectTabButtonsFromChildren();
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
            if (entry.label != null)
                entry.label.color = isActive ? activeTabTextColor : inactiveTabTextColor;
        }
    }

    private void ApplyDimmedToAll()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            TabButtonVisual entry = tabButtons[i];
            if (entry == null)
                continue;

            if (entry.graphic)
                entry.graphic.color = inactiveTabColor;
            if (entry.label != null)
                entry.label.color = inactiveTabTextColor;
        }
    }

    private void CollectTabButtonsFromChildren()
    {
        Transform searchRoot = ResolveMenuTabsSearchRoot();
        MainMenuTabButtonUI[] found = searchRoot != null
            ? searchRoot.GetComponentsInChildren<MainMenuTabButtonUI>(true)
            : GetComponentsInChildren<MainMenuTabButtonUI>(true);

        var seenTabIds = new System.Collections.Generic.HashSet<MainMenuTabId>();
        for (int i = 0; i < found.Length; i++)
        {
            MainMenuTabButtonUI tabBtn = found[i];
            if (!tabBtn || !tabBtn.isActiveAndEnabled || !tabBtn.gameObject.activeInHierarchy)
                continue;

            if (!seenTabIds.Add(tabBtn.TabId))
                continue;

            TryAddTabButtonVisual(tabBtn.TabId, tabBtn.GetComponent<Button>());
        }
    }

    private void CollectExplicitAssignedButtons()
    {
        TryAddTabButtonVisual(MainMenuTabId.Character, characterTabButton);
        TryAddTabButtonVisual(MainMenuTabId.Skills, skillsTabButton);
        TryAddTabButtonVisual(MainMenuTabId.Quest, questTabButton);
        TryAddTabButtonVisual(MainMenuTabId.WorldMap, worldMapTabButton);
        TryAddTabButtonVisual(MainMenuTabId.Upgrade, upgradeTabButton);
    }

    private void WireExplicitButtons()
    {
        UnwireExplicitButtons();
        WireExplicitButton(characterTabButton, MainMenuTabId.Character);
        WireExplicitButton(skillsTabButton, MainMenuTabId.Skills);
        WireExplicitButton(questTabButton, MainMenuTabId.Quest);
        WireExplicitButton(upgradeTabButton, MainMenuTabId.Upgrade);
        WireExplicitButton(worldMapTabButton, MainMenuTabId.WorldMap);
    }

    private void WireExplicitButton(Button button, MainMenuTabId tabId)
    {
        if (button == null)
            return;

        UnityAction handler = () => SelectTab(tabId);
        button.onClick.RemoveListener(handler);
        button.onClick.AddListener(handler);
        _explicitButtonBindings.Add(new ExplicitButtonBinding
        {
            button = button,
            handler = handler
        });
    }

    private void UnwireExplicitButtons()
    {
        for (int i = 0; i < _explicitButtonBindings.Count; i++)
        {
            ExplicitButtonBinding binding = _explicitButtonBindings[i];
            if (binding?.button == null || binding.handler == null)
                continue;

            binding.button.onClick.RemoveListener(binding.handler);
        }

        _explicitButtonBindings.Clear();
    }

    private void TryAddTabButtonVisual(MainMenuTabId tabId, Button button)
    {
        if (button == null || FindEntry(tabId) >= 0)
            return;

        Graphic g = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
        if (!g)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        tabButtons.Add(new TabButtonVisual
        {
            tabId = tabId,
            graphic = g,
            label = label
        });
    }

    private Transform ResolveMenuTabsSearchRoot()
    {
        Transform menuTabsBar = transform.Find("MenuTabsBar");
        if (menuTabsBar != null && menuTabsBar.gameObject.activeInHierarchy)
        {
            Transform row = menuTabsBar.Find("MenuTabsRow");
            return row != null && row.gameObject.activeInHierarchy ? row : menuTabsBar;
        }

        return null;
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

}
