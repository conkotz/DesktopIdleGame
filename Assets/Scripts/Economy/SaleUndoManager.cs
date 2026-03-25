using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaleUndoManager : MonoBehaviour
{
    public static SaleUndoManager Instance { get; private set; }

    [Serializable]
    private class SaleEntry
    {
        public int id;
        public string itemId;
        public int amount;
        public int gold;
        public float expiresAt;
    }

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    [Header("UI (bound at runtime)")]
    [SerializeField] private GameObject undoPanel;          // UndoSellPanel
    [SerializeField] private RectTransform rowsRoot;        // RowRoot
    [SerializeField] private SaleUndoRowUI undoRowPrefab;   // inactive template

    [Header("Fixed Screen Position")]
    [Tooltip("Panel anchoredPosition in the canvas. Example: TopRight anchor with (-20,-20) for 20px inset.")]
    [SerializeField] private Vector2 fixedAnchoredPosition = new Vector2(-20f, -20f);

    [Tooltip("If true, panel stays active even when there are 0 entries (usually false).")]
    [SerializeField] private bool keepPanelVisibleWhenEmpty = false;

    [Header("Behaviour")]
    [SerializeField] private int visibleMax = 4;
    [SerializeField] private int maxEntriesKept = 30;
    [SerializeField] private float undoTimeoutSeconds = 10f;

    private readonly List<SaleEntry> _entries = new();
    private int _nextId = 1;

    private RectTransform _panelRect;

    private void Awake()
    {
        // ✅ singleton + persist
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Refs can be missing in Bootstrap; we rebind on scene load
        RebindRefs();

        // UI is now bound by SaleUndoUIBinder, not discovered here.
        ClearUIRefs();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // New scene may have a new Inventory (or none). Rebind.
        RebindRefs();

        // If UI is currently bound, refresh it (keeps panel correct after scene changes)
        if (undoPanel && rowsRoot && undoRowPrefab && _panelRect)
            RefreshUI();
    }

    private void RebindRefs()
    {
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!wallet) wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
    }

    private void ClearUIRefs()
    {
        undoPanel = null;
        rowsRoot = null;
        undoRowPrefab = null;
        _panelRect = null;
    }

    /// <summary>
    /// Called by the scene's UI binder when a selling UI exists in that scene.
    /// Safe to call multiple times (eg. different scenes).
    /// </summary>
    public void BindUI(GameObject panel, RectTransform root, SaleUndoRowUI rowPrefab)
    {
        undoPanel = panel;
        rowsRoot = root;
        undoRowPrefab = rowPrefab;

        _panelRect = undoPanel ? undoPanel.GetComponent<RectTransform>() : null;

        // If rowsRoot not provided, try find it
        if (!rowsRoot && _panelRect)
        {
            var rr = _panelRect.Find("RowRoot");
            rowsRoot = rr ? rr.GetComponent<RectTransform>() : _panelRect;
        }

        // If prefab not provided, try find inactive template
        if (!undoRowPrefab && rowsRoot)
        {
            var t = rowsRoot.Find("UndoRowPrefab");
            if (t) undoRowPrefab = t.GetComponent<SaleUndoRowUI>();
        }

        if (undoRowPrefab && undoRowPrefab.gameObject.activeSelf)
            undoRowPrefab.gameObject.SetActive(false);

        if (undoPanel) undoPanel.SetActive(keepPanelVisibleWhenEmpty && _entries.Count > 0);

        RefreshUI();
    }

    private void Update()
    {
        if (_entries.Count == 0) return;

        float now = Time.unscaledTime;
        bool changed = false;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (now >= _entries[i].expiresAt)
            {
                _entries.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            RefreshUI();
    }

    public void RecordSale(string itemId, int amount, int gold)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || gold <= 0) return;

        // Rebind in case Inventory/Wallet changed with scene
        if (!inventory || !wallet) RebindRefs();

        // UI may not exist in some scenes - that's OK.
        if (!inventory || !wallet || !_panelRect || rowsRoot == null || !undoRowPrefab) return;

        var entry = new SaleEntry
        {
            id = _nextId++,
            itemId = itemId,
            amount = amount,
            gold = gold,
            expiresAt = Time.unscaledTime + undoTimeoutSeconds
        };

        _entries.Insert(0, entry);

        if (_entries.Count > maxEntriesKept)
            _entries.RemoveRange(maxEntriesKept, _entries.Count - maxEntriesKept);

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (!undoPanel || rowsRoot == null || !undoRowPrefab || !_panelRect) return;

        // Clear clones (keep inactive template)
        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
        {
            var child = rowsRoot.GetChild(i);
            if (child.gameObject == undoRowPrefab.gameObject) continue;
            Destroy(child.gameObject);
        }

        if (_entries.Count == 0)
        {
            if (!keepPanelVisibleWhenEmpty)
                undoPanel.SetActive(false);
            return;
        }

        // Show panel + place it at fixed position
        undoPanel.SetActive(true);
        _panelRect.anchoredPosition = fixedAnchoredPosition;

        int showCount = Mathf.Min(visibleMax, _entries.Count);

        for (int i = 0; i < showCount; i++)
        {
            var e = _entries[i];

            var row = Instantiate(undoRowPrefab, rowsRoot);
            row.gameObject.SetActive(true);

            var def = inventory.GetItemDef(e.itemId);
            string niceName = def ? def.displayName : e.itemId;
            Sprite icon = def ? def.icon : null;

            row.Bind(e.id, $"Sold {e.amount}x {niceName} for {e.gold}g", icon, Undo);
        }

        int hidden = _entries.Count - showCount;
        if (hidden > 0)
        {
            var moreRow = Instantiate(undoRowPrefab, rowsRoot);
            moreRow.gameObject.SetActive(true);

            string txt = hidden == 1 ? "+1 more" : $"+{hidden} more";
            moreRow.BindSummary(txt);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowsRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
    }

    private void Undo(int entryId)
    {
        int idx = _entries.FindIndex(e => e.id == entryId);
        if (idx < 0) return;

        var e = _entries[idx];

        if (!wallet.SpendGold(e.gold))
        {
            _entries.RemoveAt(idx);
            RefreshUI();
            return;
        }

        int added = inventory.AddPartial(e.itemId, e.amount);
        if (added < e.amount)
        {
            if (added > 0) inventory.Remove(e.itemId, added);
            wallet.AddGold(e.gold);
            return;
        }

        _entries.RemoveAt(idx);
        RefreshUI();
    }
}