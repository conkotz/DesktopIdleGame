using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FurnaceUI : MonoBehaviour
{
    private static FurnaceUI _instance;

    private static readonly Color PanelBg = new Color32(28, 30, 34, 245);
    private static readonly Color SlotBg = new Color32(42, 44, 50, 255);
    private static readonly Color Accent = new Color32(120, 200, 110, 255);
    private static readonly Color TextLight = new Color32(220, 214, 198, 255);
    private static readonly Color ProgressBg = new Color32(100, 100, 100, 255);
    private static readonly Color ProgressFill = new Color32(90, 185, 80, 255);
    private static readonly Color PickerRowBg = new Color32(52, 54, 60, 255);
    private static readonly Color StopAccent = new Color32(220, 80, 80, 255);

    private FurnaceSmelter _smelter;
    private FurnaceClick _clickSource;
    private Inventory _inventory;
    private ItemDatabase _itemDb;

    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _orePickerRoot;
    private Image _oreIcon;
    private Image _barIcon;
    private TMP_Text _oreAmountText;
    private TMP_Text _barAmountText;
    private Image _progressFill;
    private RectTransform _progressFillRt;
    private TMP_Text _progressText;
    private TMP_Text _timeSummaryText;
    private TMP_Text _actionButtonText;
    private Button _actionButton;
    private Button _oresButton;
    private Button _barsButton;
    private Button _oreClearButton;

    public static FurnaceUI Instance => _instance;
    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

    public static FurnaceSmelter ActiveSmelter => _instance != null ? _instance._smelter : null;

    /// <summary>Deposit ore from an inventory slot while the furnace UI is open (drag-drop / double-click).</summary>
    public static bool TryDepositFromInventorySlot(Inventory inv, int slotIndex, int amount = 0)
    {
        if (!IsOpen || _instance._smelter == null || inv == null || slotIndex < 0)
            return false;

        if (!_instance._smelter.TryDepositOreFromInventorySlot(inv, slotIndex, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Furnace] {reason}");
            return false;
        }

        _instance.Refresh();
        return true;
    }

    public static FurnaceUI EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("FurnaceUI", typeof(FurnaceUI));
        DontDestroyOnLoad(host);
        _instance = host.GetComponent<FurnaceUI>();
        _instance.BuildUi();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        if (_root == null)
            BuildUi();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (_smelter != null)
            _smelter.StateChanged -= Refresh;

        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!IsOpen || _smelter == null)
            return;

        RefreshProgressOnly();
    }

    public void Open(FurnaceSmelter smelter, FurnaceClick clickSource)
    {
        if (smelter == null)
            return;

        if (_root == null)
            BuildUi();

        if (_root == null || _progressText == null)
        {
            Debug.LogError("[FurnaceUI] Failed to build furnace interface.");
            return;
        }

        if (_smelter != null)
            _smelter.StateChanged -= Refresh;

        _smelter = smelter;
        _clickSource = clickSource;
        _smelter.StateChanged += Refresh;

        CacheRefs();
        HideOrePicker();
        _root.gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        HideOrePicker();
        if (_smelter != null && SaveManager.Instance != null)
            SaveManager.Instance.RequestSave(SaveManager.SaveRequestKind.InventoryChanged);

        HideImmediate();
        _clickSource?.NotifyClosed();
        _clickSource = null;

        if (_smelter != null)
        {
            _smelter.StateChanged -= Refresh;
            _smelter = null;
        }
    }

    private void HideImmediate()
    {
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    private void CacheRefs()
    {
        if (!_inventory)
            _inventory = Inventory.ResolvePlayer();
        if (!_itemDb)
            _itemDb = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
    }

    private void Refresh()
    {
        if (_smelter == null)
            return;

        CacheRefs();
        RefreshSlotVisuals();
        RefreshProgressOnly();
        RefreshActionButton();
        RefreshOreClearButton();
    }

    private void RefreshOreClearButton()
    {
        if (_oreClearButton == null || _smelter == null)
            return;

        bool show = _smelter.StoredOreAmount > 0 && !_smelter.IsSmelting;
        _oreClearButton.gameObject.SetActive(show);
    }

    private void RefreshSlotVisuals()
    {
        string oreId = _smelter.StoredOreItemId;
        int oreAmt = _smelter.StoredOreAmount;
        ApplySlot(_oreIcon, _oreAmountText, oreId, oreAmt, "Ores", _itemDb);

        string barId = _smelter.ReadyBarItemId;
        if (string.IsNullOrWhiteSpace(barId))
        {
            if (SmeltingRecipes.TryGetForOre(oreId, out SmeltingRecipe recipe) ||
                _smelter.TryGetActiveRecipe(out recipe))
                barId = recipe.BarItemId;
        }

        ApplySlot(_barIcon, _barAmountText, barId, _smelter.ReadyBarAmount, "Bars", _itemDb);
    }

    private static void ApplySlot(Image icon, TMP_Text amountText, string itemId, int amount, string emptyLabel, ItemDatabase itemDb)
    {
        if (!icon || !amountText)
            return;

        ItemDefinition def = string.IsNullOrWhiteSpace(itemId)
            ? null
            : itemDb != null ? itemDb.Get(itemId) : null;

        if (def != null && def.icon != null)
        {
            icon.sprite = def.icon;
            icon.color = Color.white;
            icon.enabled = true;
            amountText.text = amount > 0 ? amount.ToString() : "";
        }
        else
        {
            icon.sprite = null;
            icon.enabled = false;
            amountText.text = amount > 0 ? amount.ToString() : emptyLabel;
        }
    }

    private void RefreshProgressOnly()
    {
        if (_smelter == null || _progressFill == null || _progressText == null)
            return;

        float duration = _smelter.GetActiveDurationSeconds();
        float progress = _smelter.IsSmelting ? _smelter.SmeltProgressSeconds : 0f;
        float t = duration > 0f ? Mathf.Clamp01(progress / duration) : 0f;
        if (_progressFillRt != null)
            _progressFillRt.anchorMax = new Vector2(t, 1f);
        else if (_progressFill != null)
            _progressFill.fillAmount = t;

        int shownCurrent = Mathf.FloorToInt(progress);
        int shownTotal = Mathf.CeilToInt(duration);
        _progressText.text = $"{shownCurrent} / {shownTotal} seconds";

        RefreshTimeSummary();
    }

    private void RefreshTimeSummary()
    {
        if (_timeSummaryText == null || _smelter == null)
            return;

        if (!_smelter.TryGetSmeltTimeEstimate(
                out float totalRemaining,
                out float secondsPerBar,
                out int barsRemaining))
        {
            _timeSummaryText.text = "";
            return;
        }

        int ore = _smelter.StoredOreAmount;
        int perBar = _smelter.GetOrePerBar();
        string totalLabel = FormatSmeltDuration(totalRemaining);
        string perBarLabel = FormatSmeltDuration(secondsPerBar);

        if (_smelter.IsSmelting)
        {
            _timeSummaryText.text =
                $"Total remaining: {totalLabel} ({barsRemaining} bar{(barsRemaining == 1 ? "" : "s")}, {ore} ore) · {perBarLabel} per bar ({perBar} ore)";
        }
        else
        {
            _timeSummaryText.text =
                $"Smelting {ore} ore will take {totalLabel} ({barsRemaining} bar{(barsRemaining == 1 ? "" : "s")}) · {perBarLabel} per bar ({perBar} ore)";
        }
    }

    private static string FormatSmeltDuration(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        if (total >= 3600)
        {
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;
            if (minutes == 0 && secs == 0)
                return $"{hours}h";
            if (secs == 0)
                return $"{hours}h {minutes}m";
            return $"{hours}h {minutes}m {secs}s";
        }

        if (total >= 60)
        {
            int minutes = total / 60;
            int secs = total % 60;
            return secs == 0 ? $"{minutes}m" : $"{minutes}m {secs}s";
        }

        return $"{total}s";
    }

    private void RefreshActionButton()
    {
        if (_actionButton == null || _actionButtonText == null || _smelter == null)
            return;

        if (_smelter.IsSmelting)
        {
            _actionButtonText.text = "STOP";
            _actionButtonText.color = StopAccent;
            _actionButton.interactable = true;
        }
        else
        {
            _actionButtonText.text = "SMELT";
            _actionButtonText.color = Accent;
            _actionButton.interactable = _smelter.CanStartSmelting();
        }
    }

    private void OnActionClicked()
    {
        if (_smelter == null)
            return;

        if (_smelter.IsSmelting)
            _smelter.StopSmelting();
        else
            _smelter.TryStartSmelting();

        Refresh();
    }

    private void OnOreClearClicked()
    {
        RemoveAllOre();
    }

    private void OnOresClicked()
    {
        if (_orePickerRoot == null)
            return;

        bool show = !_orePickerRoot.gameObject.activeSelf;
        if (!show)
        {
            HideOrePicker();
            return;
        }

        RebuildOrePicker();
        _orePickerRoot.gameObject.SetActive(true);
    }

    private void HideOrePicker()
    {
        if (_orePickerRoot != null)
            _orePickerRoot.gameObject.SetActive(false);
    }

    private void RebuildOrePicker()
    {
        if (_orePickerRoot == null)
            return;

        CacheRefs();
        for (int i = _orePickerRoot.childCount - 1; i >= 0; i--)
            Destroy(_orePickerRoot.GetChild(i).gameObject);

        IReadOnlyList<SmeltingRecipe> recipes = SmeltingRecipes.All;
        bool any = false;
        for (int i = 0; i < recipes.Count; i++)
        {
            SmeltingRecipe recipe = recipes[i];
            int count = _inventory != null ? _inventory.GetTotalAmount(recipe.OreItemId) : 0;
            if (count <= 0)
                continue;

            any = true;
            CreateOrePickerRow(recipe, count);
        }

        if (!any)
            CreateOrePickerMessage("No smeltable ore in inventory.");
    }

    private void CreateOrePickerMessage(string message)
    {
        var row = CreateUiObject("Msg", _orePickerRoot, typeof(RectTransform), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = 28f;
        var text = CreateTmpText("Label", row.transform, 14f, TextLight, TextAlignmentOptions.MidlineLeft);
        StretchFull(text.rectTransform);
        text.text = message;
    }

    private void CreateOrePickerRow(SmeltingRecipe recipe, int playerCount)
    {
        ItemDefinition def = _itemDb != null ? _itemDb.Get(recipe.OreItemId) : null;
        string label = def != null ? def.displayName : recipe.OreItemId;
        string oreId = recipe.OreItemId;

        var row = CreateUiObject("OreRow", _orePickerRoot, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        var rowLe = row.GetComponent<LayoutElement>();
        rowLe.preferredHeight = 40f;
        rowLe.minHeight = 40f;
        row.GetComponent<Image>().color = PickerRowBg;

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(6, 6, 6, 6);
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var iconGo = CreateUiObject("Icon", row.transform, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var iconLe = iconGo.GetComponent<LayoutElement>();
        iconLe.preferredWidth = 28f;
        iconLe.preferredHeight = 28f;
        var icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        if (def != null && def.icon != null)
            icon.sprite = def.icon;

        var labelGo = CreateUiObject("Label", row.transform, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        var labelLe = labelGo.GetComponent<LayoutElement>();
        labelLe.flexibleWidth = 1f;
        labelLe.minWidth = 60f;
        var labelText = labelGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            labelText.font = TMP_Settings.defaultFontAsset;
        labelText.fontSize = 14f;
        labelText.color = TextLight;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.text = $"{label}  x{playerCount}";

        CreateCompactPickerButton(row.transform, "x5", 42f, () => DepositOreFromPicker(oreId, 5));
        CreateCompactPickerButton(row.transform, "xAll", 48f, () => DepositAllOreFromPicker(oreId));
    }

    private void DepositOreFromPicker(string oreId, int amount)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryDepositOre(oreId, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Furnace] {reason}");
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void DepositAllOreFromPicker(string oreId)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryDepositAllOreFromInventory(oreId, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Furnace] {reason}");
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void RefreshPickerAfterDeposit()
    {
        Refresh();
        if (_orePickerRoot != null && _orePickerRoot.gameObject.activeSelf)
            RebuildOrePicker();
    }

    private Button CreateCompactPickerButton(Transform parent, string label, float width, UnityEngine.Events.UnityAction onClick)
    {
        var go = CreateUiObject(label + "Btn", parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 28f;
        le.minWidth = width;
        go.GetComponent<Image>().color = SlotBg;

        var tmp = CreateTmpText("Text", go.transform, 13f, Accent, TextAlignmentOptions.Center);
        StretchFull(tmp.rectTransform);
        tmp.text = label;

        var button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        return button;
    }

    private void OnOresRightClicked(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointer && pointer.button != PointerEventData.InputButton.Right)
            return;
        if (_smelter == null)
            return;

        CacheRefs();
        string oreId = _smelter.StoredOreItemId;
        int stored = _smelter.StoredOreAmount;
        var entries = new List<ContextMenuEntry>();

        if (stored > 0)
        {
            string oreName = GetItemDisplayName(oreId, "Ore");
            int invCount = _inventory != null ? _inventory.GetTotalAmount(oreId) : 0;
            if (invCount > 0)
            {
                entries.Add(new ContextMenuEntry(
                    $"Add all {invCount} {oreName}",
                    () => AddAllOre(oreId)));
            }

            entries.Add(new ContextMenuEntry(
                $"Remove all {stored} {oreName}",
                () => RemoveAllOre()));
        }
        else
        {
            IReadOnlyList<SmeltingRecipe> recipes = SmeltingRecipes.All;
            for (int i = 0; i < recipes.Count; i++)
            {
                SmeltingRecipe recipe = recipes[i];
                int invCount = _inventory != null ? _inventory.GetTotalAmount(recipe.OreItemId) : 0;
                if (invCount <= 0)
                    continue;

                string oreName = GetItemDisplayName(recipe.OreItemId, "Ore");
                string capturedOreId = recipe.OreItemId;
                entries.Add(new ContextMenuEntry(
                    $"Add all {invCount} {oreName}",
                    () => AddAllOre(capturedOreId)));
            }
        }

        if (entries.Count == 0)
            return;

        string menuTitle = stored > 0 ? GetItemDisplayName(oreId, "Ore") : "Ores";
        ContextMenuUI.EnsureInstance().ShowAtScreen(entries, Input.mousePosition, menuTitle);
    }

    private void AddAllOre(string oreId)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryDepositAllOreFromInventory(oreId, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Furnace] {reason}");
            return;
        }

        Refresh();
    }

    private void RemoveAllOre()
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryWithdrawAllOre(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Furnace] {reason}");
            return;
        }

        Refresh();
    }

    private void OnBarsRightClicked(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointer && pointer.button != PointerEventData.InputButton.Right)
            return;
        if (_smelter == null || _smelter.ReadyBarAmount <= 0)
            return;

        CacheRefs();
        string barId = ResolveBarItemId();
        string barName = GetItemDisplayName(barId, "Bar");
        int ready = _smelter.ReadyBarAmount;

        var entries = new List<ContextMenuEntry>
        {
            new($"Collect 1 {barName}", () => CollectBars(1), ready < 1),
            new($"Collect all {ready} {barName}", () => CollectBars(ready), ready <= 0)
        };

        ContextMenuUI.EnsureInstance().ShowAtScreen(entries, Input.mousePosition, barName);
    }

    private string ResolveBarItemId()
    {
        if (_smelter == null)
            return "";

        if (!string.IsNullOrWhiteSpace(_smelter.ReadyBarItemId))
            return _smelter.ReadyBarItemId;

        if (_smelter.TryGetActiveRecipe(out SmeltingRecipe recipe))
            return recipe.BarItemId;

        if (SmeltingRecipes.TryGetForOre(_smelter.StoredOreItemId, out recipe))
            return recipe.BarItemId;

        return "";
    }

    private string GetItemDisplayName(string itemId, string fallback)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return fallback;

        CacheRefs();
        ItemDefinition def = _itemDb != null ? _itemDb.Get(itemId) : null;
        return def != null && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : itemId;
    }

    private void CollectBars(int amount)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryCollectBars(amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Furnace] {reason}");
            return;
        }

        Refresh();
    }

    private void BuildUi()
    {
        if (_root != null && _progressText != null && _timeSummaryText != null && _oreClearButton != null)
            return;

        if (_root != null)
        {
            Destroy(_root.gameObject);
            _root = null;
            _orePickerRoot = null;
            _progressText = null;
            _timeSummaryText = null;
            _progressFill = null;
            _progressFillRt = null;
            _actionButton = null;
            _actionButtonText = null;
            _oresButton = null;
            _barsButton = null;
            _oreClearButton = null;
            _oreIcon = null;
            _barIcon = null;
            _oreAmountText = null;
            _barAmountText = null;
        }

        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null)
            _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 12000;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        _root = CreatePanel("FurnacePanel", transform, new Vector2(360f, 320f));
        CreateHeader(_root, "Furnace");

        var slotsRow = CreateUiObject("SlotsRow", _root, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var slotsRt = slotsRow.GetComponent<RectTransform>();
        slotsRt.anchorMin = new Vector2(0.5f, 0.55f);
        slotsRt.anchorMax = new Vector2(0.5f, 0.55f);
        slotsRt.pivot = new Vector2(0.5f, 0.5f);
        slotsRt.sizeDelta = new Vector2(300f, 88f);
        var hlg = slotsRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        _oresButton = CreateSlotButton(slotsRow.transform, "Ores", out _oreIcon, out _oreAmountText, OnOresClicked);
        var oresTrigger = _oresButton.gameObject.AddComponent<FurnaceOreSlotInteractions>();
        oresTrigger.Initialize(this);

        _oreClearButton = CreateButton(_oresButton.transform, "OreClear", "×", new Vector2(22f, 22f), new Vector2(1f, 1f));
        var clearRt = _oreClearButton.GetComponent<RectTransform>();
        clearRt.anchoredPosition = new Vector2(-4f, -4f);
        _oreClearButton.onClick.AddListener(OnOreClearClicked);
        _oreClearButton.transform.SetAsLastSibling();
        _oreClearButton.gameObject.SetActive(false);

        TextMeshProUGUI arrowText = CreateTmpText("Arrow", slotsRow.transform, 28f, Accent, TextAlignmentOptions.Center);
        arrowText.text = "→";
        arrowText.rectTransform.sizeDelta = new Vector2(28f, 88f);

        _barsButton = CreateSlotButton(slotsRow.transform, "Bars", out _barIcon, out _barAmountText, null);
        var barsTrigger = _barsButton.gameObject.AddComponent<FurnaceBarsContextTrigger>();
        barsTrigger.Initialize(this);

        var progressBg = CreateUiObject("ProgressBg", _root, typeof(RectTransform), typeof(Image));
        var progressBgRt = progressBg.GetComponent<RectTransform>();
        progressBgRt.anchorMin = new Vector2(0.5f, 0.34f);
        progressBgRt.anchorMax = new Vector2(0.5f, 0.34f);
        progressBgRt.pivot = new Vector2(0.5f, 0.5f);
        progressBgRt.sizeDelta = new Vector2(280f, 28f);
        var progressBgImage = progressBg.GetComponent<Image>();
        progressBgImage.color = ProgressBg;
        progressBgImage.raycastTarget = false;

        var progressFillGo = CreateUiObject("Fill", progressBg.transform, typeof(RectTransform), typeof(Image));
        _progressFillRt = progressFillGo.GetComponent<RectTransform>();
        _progressFillRt.anchorMin = Vector2.zero;
        _progressFillRt.anchorMax = Vector2.zero;
        _progressFillRt.offsetMin = Vector2.zero;
        _progressFillRt.offsetMax = Vector2.zero;
        _progressFill = progressFillGo.GetComponent<Image>();
        _progressFill.color = ProgressFill;
        _progressFill.raycastTarget = false;

        _progressText = CreateTmpText("ProgressText", progressBg.transform, 14f, TextLight, TextAlignmentOptions.Center);
        StretchFull(_progressText.rectTransform);
        _progressText.text = "0 / 0 seconds";

        _timeSummaryText = CreateTmpText("TimeSummary", _root, 12f, TextLight, TextAlignmentOptions.Center);
        var summaryRt = _timeSummaryText.rectTransform;
        summaryRt.anchorMin = new Vector2(0.5f, 0.24f);
        summaryRt.anchorMax = new Vector2(0.5f, 0.24f);
        summaryRt.pivot = new Vector2(0.5f, 0.5f);
        summaryRt.sizeDelta = new Vector2(320f, 36f);
        _timeSummaryText.enableWordWrapping = true;
        _timeSummaryText.text = "";

        _actionButton = CreateButton(_root, "ActionButton", "START", new Vector2(200f, 40f), new Vector2(0.5f, 0.12f));
        _actionButtonText = _actionButton.GetComponentInChildren<TMP_Text>();
        _actionButton.onClick.AddListener(OnActionClicked);

        var closeBtn = CreateButton(_root, "CloseButton", "X", new Vector2(32f, 32f), new Vector2(1f, 1f));
        closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-12f, -12f);
        closeBtn.onClick.AddListener(Close);

        _orePickerRoot = CreatePanel("OrePicker", _root, new Vector2(280f, 200f));
        _orePickerRoot.anchorMin = new Vector2(0f, 0.5f);
        _orePickerRoot.anchorMax = new Vector2(0f, 0.5f);
        _orePickerRoot.pivot = new Vector2(0f, 0.5f);
        _orePickerRoot.anchoredPosition = new Vector2(-290f, 0f);
        var pickerLayout = _orePickerRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        pickerLayout.padding = new RectOffset(8, 8, 8, 8);
        pickerLayout.spacing = 6f;
        pickerLayout.childControlHeight = true;
        pickerLayout.childForceExpandHeight = false;
        _orePickerRoot.gameObject.SetActive(false);
    }

    private Button CreateSlotButton(Transform parent, string label, out Image icon, out TMP_Text amountText, UnityEngine.Events.UnityAction onClick)
    {
        var go = CreateUiObject(label + "Slot", parent, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(88f, 88f);
        go.GetComponent<Image>().color = SlotBg;

        var iconGo = CreateUiObject("Icon", go.transform, typeof(RectTransform), typeof(Image));
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.55f);
        iconRt.anchorMax = new Vector2(0.5f, 0.55f);
        iconRt.sizeDelta = new Vector2(48f, 48f);
        icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;

        var labelText = CreateTmpText("Caption", go.transform, 13f, Accent, TextAlignmentOptions.Center);
        var labelRt = labelText.rectTransform;
        labelRt.anchorMin = new Vector2(0.5f, 0.12f);
        labelRt.anchorMax = new Vector2(0.5f, 0.12f);
        labelRt.sizeDelta = new Vector2(80f, 20f);
        amountText = labelText;
        amountText.text = label;

        var button = go.GetComponent<Button>();
        if (onClick != null)
            button.onClick.AddListener(onClick);
        return button;
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 size)
    {
        var panel = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = PanelBg;
        return rt;
    }

    private void CreateHeader(RectTransform parent, string title)
    {
        var header = CreateUiObject("Header", parent, typeof(RectTransform));
        var rt = header.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -10f);
        rt.sizeDelta = new Vector2(300f, 28f);
        var text = CreateTmpText("Title", header.transform, 20f, Accent, TextAlignmentOptions.Center);
        StretchFull(text.rectTransform);
        text.fontStyle = FontStyles.Bold;
        text.text = title;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchor)
    {
        var go = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = SlotBg;

        var tmp = CreateTmpText("Text", go.transform, 16f, Accent, TextAlignmentOptions.Center);
        StretchFull(tmp.rectTransform);
        tmp.text = label;

        return go.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateTmpText(
        string name,
        Transform parent,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        var go = CreateUiObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void StretchFull(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        if (go.TryGetComponent(out RectTransform rt))
            rt.localScale = Vector3.one;
        return go;
    }

    private sealed class FurnaceOreSlotInteractions : MonoBehaviour, IPointerClickHandler, IDropHandler
    {
        private FurnaceUI _owner;

        public void Initialize(FurnaceUI owner) => _owner = owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner == null)
                return;

            if (eventData.button == PointerEventData.InputButton.Right)
                _owner.OnOresRightClicked(eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_owner == null || !InventoryDragState.HasDrag)
                return;

            if (InventoryDragState.Source != InventoryDragState.SourceKind.Inventory)
                return;

            Inventory inv = Inventory.ResolvePlayer();
            if (inv == null)
                return;

            int fromSlot = InventoryDragState.FromSlotIndex;
            int amount = InventoryDragState.IsSplit ? InventoryDragState.CarriedAmount : 0;
            if (FurnaceUI.TryDepositFromInventorySlot(inv, fromSlot, amount))
                InventoryDragState.EndDrag();
        }
    }

    private sealed class FurnaceBarsContextTrigger : MonoBehaviour, IPointerClickHandler
    {
        private FurnaceUI _owner;

        public void Initialize(FurnaceUI owner) => _owner = owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner == null)
                return;

            if (eventData.button == PointerEventData.InputButton.Right)
                _owner.OnBarsRightClicked(eventData);
        }
    }
}
