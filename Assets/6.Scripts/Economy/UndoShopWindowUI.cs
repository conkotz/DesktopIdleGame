using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Undo sales UI shown beside the shop while merchant mode is open.
/// When parented under the shop window root, it uses scene layout instead of pinning.
/// </summary>
public class UndoShopWindowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform slotsGrid;
    [SerializeField] private UndoShopSlotUI slotPrefab;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button returnToShopButton;
    [SerializeField] private TMP_Text undoShopLabel;
    [SerializeField] private TMP_Text undoCapacityLabel;
    [Header("Grid Layout")]
    [SerializeField] private ScrollRect undoScrollRect;
    [SerializeField] private int undoGridColumns = 4;
    [Tooltip("Max slot size in px. 0 = fill panel width with fixed column count.")]
    [SerializeField] private float undoMaxCellSize = 0f;
    [SerializeField] private float undoMinCellSize = 40f;
    [SerializeField] private Vector2 undoGridSpacing = new Vector2(8f, 8f);
    [SerializeField] private int undoGridLayoutRetryFrames = 2;
    [Header("Activation")]
    [Tooltip("If true, closing this window also disables its GameObject so it can stay off in hierarchy by default.")]
    [SerializeField] private bool disableGameObjectWhenClosed = true;
    [Tooltip("When parented under the shop window, keep the RectTransform authored in the scene instead of pinning beside the shop.")]
    [SerializeField] private bool useShopWindowLayout = true;

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
    private GridLayoutGroup _undoGrid;
    private Coroutine _undoGridLayoutRetry;

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

        ResolveUiRefs();

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

    private void ResolveUiRefs()
    {
        Transform root = panelRoot ? panelRoot.transform : transform;

        if (!undoShopLabel)
        {
            Transform t = root.Find("Header/UndoShopLabel");
            if (t)
                undoShopLabel = t.GetComponent<TMP_Text>();
        }

        if (!undoCapacityLabel)
        {
            Transform t = root.Find("Header/UndoCapacityLabel");
            if (t)
                undoCapacityLabel = t.GetComponent<TMP_Text>();
        }

        if (!slotsGrid)
        {
            Transform t = root.Find("UndoScrollView/Viewport/Content/SlotsGrid");
            if (!t)
                t = root.Find("Content/SlotsGrid");
            if (t)
                slotsGrid = t;
        }

        if (!undoScrollRect)
        {
            Transform t = root.Find("UndoScrollView");
            if (t)
                undoScrollRect = t.GetComponent<ScrollRect>();
        }
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

        if (_undoGridLayoutRetry != null)
        {
            StopCoroutine(_undoGridLayoutRetry);
            _undoGridLayoutRetry = null;
        }
    }

    private void OnEntriesChanged()
    {
        if (IsOpen && _merchant)
        {
            RefreshCapacityLabel();
            RebuildGrid();
        }

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

        if (undoShopLabel)
        {
            undoShopLabel.text = string.IsNullOrWhiteSpace(merchant.MerchantName)
                ? "Undo"
                : $"{merchant.MerchantName} — Undo";
        }

        if (panelRoot)
        {
            panelRoot.SetActive(true);
            if (!UsesEmbeddedShopLayout())
                panelRoot.transform.SetAsLastSibling();
        }

        if (!UsesEmbeddedShopLayout())
            PinNextToShopWindow();

        RefreshCapacityLabel();
        RebuildGrid();
        ScheduleUndoGridLayoutRetry();
        UpdateUndoButtonInteractable();

        if (UsesEmbeddedShopLayout() && shopUI)
        {
            shopUI.RefreshShopRaycastTargets();
            shopUI.EnsureShopDragHandleOnTop();
            StartCoroutine(ClampShopWindowAfterOpen());
        }
    }

    private IEnumerator ClampShopWindowAfterOpen()
    {
        for (int i = 0; i < 2; i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        shopUI?.ClampWindowToCanvas();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (IsOpen)
            ApplyUndoGridLayout();
    }

    private bool UsesEmbeddedShopLayout()
    {
        if (!useShopWindowLayout || !shopUI)
            return false;

        RectTransform shopWindow = shopUI.WindowRectTransform;
        return shopWindow != null && transform.IsChildOf(shopWindow);
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

        ApplyUndoGridLayout();
        UpdateUndoButtonInteractable();
        ForceGridLayout();
    }

    private void RefreshCapacityLabel()
    {
        if (!undoCapacityLabel)
            return;

        if (!_merchant || SaleUndoManager.Instance == null)
        {
            undoCapacityLabel.text = $"0/{SaleUndoManager.Instance?.MaxEntriesPerMerchant ?? 30}";
            return;
        }

        int count = SaleUndoManager.Instance.GetUndoCountForMerchant(_merchant.MerchantId);
        int max = SaleUndoManager.Instance.MaxEntriesPerMerchant;
        undoCapacityLabel.text = $"{count}/{max}";
    }

    private void ScheduleUndoGridLayoutRetry()
    {
        if (_undoGridLayoutRetry != null)
            StopCoroutine(_undoGridLayoutRetry);
        _undoGridLayoutRetry = StartCoroutine(UndoGridLayoutRetryRoutine());
    }

    private IEnumerator UndoGridLayoutRetryRoutine()
    {
        for (int i = 0; i < Mathf.Max(1, undoGridLayoutRetryFrames); i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            ApplyUndoGridLayout();
            if (UndoGridViewportIsReady())
                break;
        }

        _undoGridLayoutRetry = null;
    }

    private bool UndoGridViewportIsReady()
    {
        RectTransform viewport = GetUndoGridViewport();
        return viewport && viewport.rect.width > 1f;
    }

    private RectTransform GetUndoGridViewport()
    {
        if (undoScrollRect && undoScrollRect.viewport)
            return undoScrollRect.viewport;

        if (slotsGrid && slotsGrid.parent && slotsGrid.parent.parent)
            return slotsGrid.parent.parent as RectTransform;

        return null;
    }

    private void ApplyUndoGridLayout()
    {
        if (!slotsGrid)
            return;

        if (!_undoGrid)
            _undoGrid = slotsGrid.GetComponent<GridLayoutGroup>();
        if (!_undoGrid)
            return;

        int cols = Mathf.Max(1, undoGridColumns);
        _undoGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _undoGrid.constraintCount = cols;
        _undoGrid.spacing = undoGridSpacing;

        float layoutWidth = ResolveUndoGridLayoutWidth();
        if (layoutWidth <= 1f)
            return;

        RectOffset pad = _undoGrid.padding;
        float usableW = layoutWidth - pad.left - pad.right - undoGridSpacing.x * (cols - 1);
        float cell = Mathf.Floor(Mathf.Max(1f, usableW) / cols);
        if (undoMaxCellSize > 0f)
            cell = Mathf.Min(cell, undoMaxCellSize);
        cell = Mathf.Max(undoMinCellSize, cell);

        _undoGrid.cellSize = new Vector2(cell, cell);
    }

    private float ResolveUndoGridLayoutWidth()
    {
        RectTransform viewport = GetUndoGridViewport();
        if (viewport && viewport.rect.width > 1f)
            return viewport.rect.width;

        if (windowRect && windowRect.rect.width > 1f)
            return windowRect.rect.width;

        if (panelRoot && panelRoot.transform is RectTransform panelRect && panelRect.rect.width > 1f)
            return panelRect.rect.width;

        return 0f;
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
            RefreshCapacityLabel();
            RebuildGrid();
            if (shopUI)
                shopUI.RefreshUndoSaleButtonState();
        }
    }

    private void OnReturnToShopClicked()
    {
        CloseWindow();
    }
}
