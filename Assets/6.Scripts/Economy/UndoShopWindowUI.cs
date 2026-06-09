using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-window undo UI: grid of recent sales for the current merchant; bar Undo restores selection.
/// Swaps with <see cref="ShopUI"/> when opened from the shop Undo control.
/// </summary>
public class UndoShopWindowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform slotsGrid;
    [SerializeField] private UndoShopSlotUI slotPrefab;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button returnToShopButton;
    [SerializeField] private TMP_Text titleText;
    [Header("Activation")]
    [Tooltip("If true, closing this window also disables its GameObject so it can stay off in hierarchy by default.")]
    [SerializeField] private bool disableGameObjectWhenClosed = true;

    [Header("Refs")]
    [SerializeField] private ShopUI shopUI;
    [SerializeField] private Inventory inventory;
    [SerializeField] private RectTransform windowRect;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI;
    [SerializeField] private float pinGap = 8f;
    [SerializeField] private float pinCanvasEdgeMargin = 4f;

    private readonly List<UndoShopSlotUI> _spawned = new();
    private static readonly List<SaleUndoSnapshot> _scratchSnapshots = new();

    private Merchant _merchant;
    private int _selectedEntryId = -1;

    public bool IsOpen => panelRoot && panelRoot.activeInHierarchy;

    /// <summary>Enable this window object for runtime use when it starts disabled in hierarchy.</summary>
    public void EnsureWindowEnabledForUse()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void Awake()
    {
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!shopUI)
            shopUI = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
        if (!mainMenuWindowUI)
            mainMenuWindowUI = MainMenuWindowUI.Resolve();

        if (!windowRect && panelRoot)
            windowRect = panelRoot.transform as RectTransform;

        if (!slotsGrid && panelRoot)
        {
            Transform t = panelRoot.transform.Find("Content/SlotsGrid");
            if (t) slotsGrid = t;
        }

        if (!canvasRect && windowRect)
        {
            Canvas c = windowRect.GetComponentInParent<Canvas>();
            if (c)
                canvasRect = c.transform as RectTransform;
        }

        if (undoButton)
        {
            undoButton.onClick.RemoveAllListeners();
            undoButton.onClick.AddListener(OnUndoClicked);
        }

        if (returnToShopButton)
        {
            returnToShopButton.onClick.RemoveAllListeners();
            returnToShopButton.onClick.AddListener(OnReturnToShopClicked);
        }

        if (panelRoot)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (SaleUndoManager.Instance != null)
            SaleUndoManager.Instance.EntriesChanged += OnEntriesChanged;
    }

    private void OnDisable()
    {
        if (SaleUndoManager.Instance != null)
            SaleUndoManager.Instance.EntriesChanged -= OnEntriesChanged;
    }

    private void OnEntriesChanged()
    {
        if (IsOpen && _merchant)
            RebuildGrid();
        if (shopUI && shopUI.IsOpen && _merchant)
            shopUI.RefreshUndoSaleButtonState();
    }

    /// <summary>Hide panel at startup; shop opens it explicitly.</summary>
    public void CloseWindow()
    {
        if (panelRoot)
            panelRoot.SetActive(false);
        _merchant = null;
        _selectedEntryId = -1;

        if (disableGameObjectWhenClosed)
            gameObject.SetActive(false);
    }

    public void OpenForMerchant(Merchant merchant)
    {
        EnsureWindowEnabledForUse();

        if (!merchant)
            return;

        _merchant = merchant;
        _selectedEntryId = -1;

        if (titleText)
            titleText.text = string.IsNullOrWhiteSpace(merchant.MerchantName) ? "Undo sales" : $"{merchant.MerchantName} — Undo";

        if (panelRoot)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        PinNextToShopWindow();
        RebuildGrid();
        UpdateUndoButtonInteractable();
    }

    private void PinNextToShopWindow()
    {
        if (!windowRect || !canvasRect)
            return;

        RectTransform shopAnchor = shopUI != null ? shopUI.WindowRectTransform : null;
        if (shopAnchor != null)
            UIPinNextToMenuWindow.PositionBeside(windowRect, shopAnchor, canvasRect, pinGap, pinCanvasEdgeMargin);
        else
            UIPinNextToMenuWindow.PositionNextToMainMenu(windowRect, canvasRect, mainMenuWindowUI, pinGap, pinCanvasEdgeMargin);
    }

    private void RebuildGrid()
    {
        foreach (UndoShopSlotUI s in _spawned)
        {
            if (s)
                Destroy(s.gameObject);
        }

        _spawned.Clear();

        if (!slotsGrid || !slotPrefab || !_merchant || SaleUndoManager.Instance == null)
            return;

        SaleUndoManager.Instance.CopyEntriesForMerchant(_merchant.MerchantId, _scratchSnapshots);

        for (int i = 0; i < _scratchSnapshots.Count; i++)
        {
            SaleUndoSnapshot snap = _scratchSnapshots[i];
            UndoShopSlotUI row = Instantiate(slotPrefab, slotsGrid);
            row.gameObject.SetActive(true);

            ItemDefinition def = inventory ? inventory.GetItemDef(snap.ItemId) : null;
            Sprite spr = def ? def.icon : null;

            int capturedId = snap.Id;
            row.Bind(capturedId, spr, snap.Amount, snap.Gold, OnSlotSelectRequested);
            row.SetSelected(capturedId == _selectedEntryId);
            _spawned.Add(row);
        }

        if (_spawned.Count == 0)
            _selectedEntryId = -1;

        UpdateUndoButtonInteractable();
        ForceGridLayout();
    }

    private void ForceGridLayout()
    {
        if (slotsGrid is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        Canvas.ForceUpdateCanvases();
    }

    private void OnSlotSelectRequested(int entryId)
    {
        _selectedEntryId = entryId;

        for (int i = 0; i < _spawned.Count; i++)
        {
            UndoShopSlotUI s = _spawned[i];
            if (!s) continue;
            s.SetSelected(s.BoundEntryId == _selectedEntryId);
        }

        UpdateUndoButtonInteractable();
    }

    private void UpdateUndoButtonInteractable()
    {
        if (!undoButton)
            return;

        bool has = _selectedEntryId > 0 && SaleUndoManager.Instance != null &&
                   SaleUndoManager.Instance.HasEntry(_selectedEntryId);
        undoButton.interactable = has;
    }

    private void OnUndoClicked()
    {
        if (SaleUndoManager.Instance == null || _selectedEntryId <= 0)
            return;

        if (SaleUndoManager.Instance.TryUndoEntry(_selectedEntryId))
        {
            _selectedEntryId = -1;
            RebuildGrid();
            if (shopUI)
                shopUI.RefreshUndoSaleButtonState();
        }
    }

    private void OnReturnToShopClicked()
    {
        Merchant m = _merchant;

        CloseWindow();

        if (shopUI && m)
        {
            shopUI.Open(m);
            var click = m.GetComponentInParent<MerchantClick>(true);
            if (!click)
                click = m.GetComponent<MerchantClick>();
            click?.RepositionShopNextToMenu();
        }
    }
}
