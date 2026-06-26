using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Storage category tabs — each tab is its own 88-slot page. Auto-wires to <c>TabsPanel</c> under <see cref="StorageUI"/>.
/// </summary>
public class StorageTabBarUI : MonoBehaviour
{
    [SerializeField] private PlayerStorage storage;
    [SerializeField] private StorageGridUI gridUi;
    [SerializeField] private RectTransform tabsRoot;

    private readonly List<StorageTabButtonUI> _tabs = new List<StorageTabButtonUI>(8);
    private StorageTabKind _activeTab = StorageTabKind.Main;

    public event Action<StorageTabKind> OnTabSelected;

    public StorageTabKind ActiveTab => _activeTab;

    private void Awake()
    {
        if (!tabsRoot)
            tabsRoot = transform as RectTransform;

        ResolveRefs();
        DiscoverTabs();
        ApplySavedTabOrder();
        SelectTab(_activeTab, notify: false);
    }

    private void OnEnable()
    {
        ResolveRefs();
        if (storage != null)
        {
            storage.OnTabOrderChanged += ApplySavedTabOrder;
            storage.OnTabAffinityChanged += RefreshAllAffinityBars;
        }

        RefreshAllAffinityBars();
    }

    private void OnDisable()
    {
        if (storage != null)
        {
            storage.OnTabOrderChanged -= ApplySavedTabOrder;
            storage.OnTabAffinityChanged -= RefreshAllAffinityBars;
        }
    }

    public void ResolveRefs()
    {
        if (!gridUi)
            gridUi = GetComponentInParent<StorageUI>(true)?.GetComponentInChildren<StorageGridUI>(true);

        if (gridUi != null && gridUi.PlayerStorage != null)
            storage = gridUi.PlayerStorage;

        if (!storage)
            storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
    }

    /// <summary>Called from <see cref="StorageGridUI"/> when tab bar was not placed in the scene.</summary>
    public static StorageTabBarUI EnsureOnStorageWindow(StorageGridUI grid)
    {
        if (grid == null)
            return null;

        StorageTabBarUI existing = grid.GetComponentInChildren<StorageTabBarUI>(true);
        if (existing != null)
            return existing;

        Transform tabsPanel = grid.transform.parent;
        while (tabsPanel != null)
        {
            if (string.Equals(tabsPanel.name, "TabsPanel", StringComparison.OrdinalIgnoreCase))
                break;
            tabsPanel = tabsPanel.parent;
        }

        if (tabsPanel == null)
        {
            StorageUI shell = grid.GetComponentInParent<StorageUI>(true);
            if (shell != null)
            {
                for (int i = 0; i < shell.transform.childCount; i++)
                {
                    Transform ch = shell.transform.GetChild(i);
                    if (ch != null && ch.name.IndexOf("Tab", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        tabsPanel = ch;
                        break;
                    }
                }
            }
        }

        if (tabsPanel == null)
            return null;

        var bar = tabsPanel.GetComponent<StorageTabBarUI>();
        if (bar == null)
            bar = tabsPanel.gameObject.AddComponent<StorageTabBarUI>();

        bar.gridUi = grid;
        bar.tabsRoot = tabsPanel as RectTransform;
        bar.ResolveRefs();
        bar.DiscoverTabs();
        bar.ApplySavedTabOrder();
        bar.SelectTab(bar._activeTab, notify: false);
        return bar;
    }

    private void DiscoverTabs()
    {
        _tabs.Clear();
        if (!tabsRoot)
            return;

        for (int i = 0; i < tabsRoot.childCount; i++)
        {
            Transform child = tabsRoot.GetChild(i);
            if (child == null)
                continue;

            StorageTabButtonUI tab = child.GetComponent<StorageTabButtonUI>();
            if (tab == null)
                tab = child.gameObject.AddComponent<StorageTabButtonUI>();

            tab.Initialize(this);
            _tabs.Add(tab);
        }

        RefreshAllAffinityBars();
    }

    private void RefreshAllAffinityBars()
    {
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].RefreshAffinityBar();
    }

    public void SelectTab(StorageTabKind tab, bool notify = true)
    {
        _activeTab = tab;
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].ApplySelectedVisual(_tabs[i].TabKind == tab);

        if (notify)
            OnTabSelected?.Invoke(tab);
    }

    /// <summary>Selects the leftmost tab after saved display order (first tab in the bar).</summary>
    public void SelectFirstDisplayedTab(bool notify = true)
    {
        if (_tabs.Count == 0)
            DiscoverTabs();

        ApplySavedTabOrder();

        if (_tabs.Count == 0)
            return;

        SelectTab(_tabs[0].TabKind, notify);
    }

    public void NotifyTabOrderChanged()
    {
        if (storage == null || !tabsRoot || tabsRoot.childCount != PlayerStorage.TabCount)
            return;

        int[] order = new int[PlayerStorage.TabCount];
        for (int i = 0; i < tabsRoot.childCount; i++)
        {
            StorageTabButtonUI tab = tabsRoot.GetChild(i).GetComponent<StorageTabButtonUI>();
            if (tab == null)
                return;

            order[i] = (int)tab.TabKind;
        }

        storage.SetTabDisplayOrder(order);
        _tabs.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
    }

    public void RepositionTabToFirst(StorageTabKind kind)
    {
        StorageTabButtonUI tab = FindTab(kind);
        if (tab == null)
            return;

        tab.transform.SetSiblingIndex(0);
        NotifyTabOrderChanged();
    }

    public void ApplySavedTabOrder()
    {
        if (!tabsRoot || storage == null || _tabs.Count != PlayerStorage.TabCount)
            return;

        IReadOnlyList<int> saved = storage.TabDisplayOrder;
        for (int displayIndex = 0; displayIndex < saved.Count; displayIndex++)
        {
            StorageTabKind kind = (StorageTabKind)saved[displayIndex];
            StorageTabButtonUI tab = FindTab(kind);
            if (tab != null)
                tab.transform.SetSiblingIndex(displayIndex);
        }

        _tabs.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
    }

    private StorageTabButtonUI FindTab(StorageTabKind kind)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i].TabKind == kind)
                return _tabs[i];
        }

        return null;
    }

    public void HandleItemDroppedOnTab(StorageTabKind tab)
    {
        gridUi?.RefreshNow();
    }
}
