using System;
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
    private RectTransform _orePickerScrollContent;
    private Image _oreIcon;
    private Image _barIcon;
    private TMP_Text _oreAmountText;
    private TMP_Text _barAmountText;
    private Image _progressFill;
    private RectTransform _progressFillRt;
    private TMP_Text _progressText;
    private TMP_Text _timeSummaryText;
    private TMP_Text _fuelSummaryText;
    private TMP_Text _actionButtonText;
    private Button _actionButton;
    private Button _oresButton;
    private Button _barsButton;
    private Button _oreClearButton;
    private Button _helpButton;
    private Button _smeltingLevelButton;
    private TMP_Text _smeltingLevelButtonText;
    private Image _smeltingXpFill;
    private RectTransform _smeltingXpFillRt;
    private Button _activeWorkButton;
    private TMP_Text _activeWorkButtonText;
    private Button _enhancementButton;
    private Button _enhancementClearButton;
    private Image _enhancementIcon;
    private TMP_Text _enhancementAmountText;
    private Button _fuelButton;
    private Button _fuelClearButton;
    private Image _fuelIcon;
    private TMP_Text _fuelAmountText;
    private SharedTooltipUI _sharedTooltip;

    private enum SidePickerMode
    {
        Ore,
        Fuel,
        Enhancement
    }

    private SidePickerMode _pickerMode;
    private const float ScrollSensitivity = 8f;
    private const int UiLayoutVersion = 5;
    private int _builtUiLayoutVersion;

    private RectTransform _helpPanelRoot;
    private RectTransform _proficiencyPanelRoot;
    private RectTransform _proficiencyScrollContent;
    private TMP_Text _proficiencyFooterText;

    private float _nextPendingBarsLogTime;
    private bool _proficiencySubscribed;

    private const string HelpBodyText =
        "The furnace converts ore into metal bars over time.\n\n" +
        "Deposit ore and logs, press Smelt, and wait for bars to finish. Collect bars before adding new ore or starting another batch.\n\n" +
        "Logs in the fuel slot keep the furnace burning (Splitwood 5s, Hardwood 15s, Wildwood 30s, Ember Oak 45s, Spiritwood 60s per log). Up to 999 logs.\n\n" +
        "Higher-tier ores take longer to smelt but grant more Smelting proficiency.\n\n" +
        "Load Coal in the enhancement slot to reduce smelt time by 2 seconds per bar (1 consumed per bar).\n\n" +
        "Speed Up trims time from the current bar while smelting (3s cooldown).";

    private const int DefaultCanvasSortingOrder = 12000;
    private const float SidePanelGap = 10f;
    private const float FurnaceBlockedLogCooldownSeconds = 2f;
    private static readonly Vector2 MainPanelSize = new Vector2(360f, 460f);
    private static readonly Vector2 SidePickerPanelSize = new Vector2(280f, 240f);
    private static readonly Vector2 HelpPanelSize = new Vector2(280f, 280f);

    public static FurnaceUI Instance => _instance;
    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

    public RectTransform GetLayoutPanel() => _root;
    public Canvas GetLayoutCanvas() => _canvas;

    public void ShowLayoutPreview()
    {
        if (_root == null)
            BuildUi();
        if (_root == null)
            return;

        ProcessingSkillsWindowLayout.ApplyLayoutToOpenPanel(_root, _canvas);
        _root.gameObject.SetActive(true);
    }

    public void HideLayoutPreview()
    {
        HideImmediate();
    }

    public static int CanvasSortingOrder =>
        _instance != null && _instance._canvas != null
            ? _instance._canvas.sortingOrder
            : DefaultCanvasSortingOrder;

    public static bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
    {
        if (!IsOpen || _instance._root == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(_instance._root, screenPoint, eventCamera);
    }

    public static FurnaceSmelter ActiveSmelter => _instance != null ? _instance._smelter : null;

    /// <summary>Deposit ore from an inventory slot while the furnace UI is open (drag-drop / double-click).</summary>
    public static bool TryDepositFromInventorySlot(Inventory inv, int slotIndex, int amount = 0)
    {
        if (!IsOpen || _instance._smelter == null || inv == null || slotIndex < 0)
            return false;

        if (!_instance._smelter.TryDepositOreFromInventorySlot(inv, slotIndex, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                _instance.LogFurnaceBlocked(reason);
            return false;
        }

        _instance.Refresh();
        return true;
    }

    public static bool TryDepositEnhancementFromInventorySlot(Inventory inv, int slotIndex, int amount = 0)
    {
        if (!IsOpen || _instance._smelter == null || inv == null || slotIndex < 0)
            return false;

        if (!_instance._smelter.TryDepositEnhancementFromInventorySlot(inv, slotIndex, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                _instance.LogFurnaceBlocked(reason);
            return false;
        }

        _instance.Refresh();
        return true;
    }

    public static bool TryDepositFuelFromInventorySlot(Inventory inv, int slotIndex, int amount = 0)
    {
        if (!IsOpen || _instance._smelter == null || inv == null || slotIndex < 0)
            return false;

        if (!_instance._smelter.TryDepositFuelFromInventorySlot(inv, slotIndex, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                _instance.LogFurnaceBlocked(reason);
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
            _smelter.StateChanged -= RefreshOnStationChange;

        UnsubscribeProficiency();

        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!IsOpen || _smelter == null)
            return;

        RefreshProgressOnly();
        if (_smelter.IsSmelting || _smelter.HasFuel)
            RefreshFuelSummary();
        RefreshActionButton();
        RefreshActiveWorkButton();
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
            _smelter.StateChanged -= RefreshOnStationChange;

        _smelter = smelter;
        _clickSource = clickSource;
        _smelter.StateChanged += RefreshOnStationChange;

        CacheRefs();
        HideOrePicker();
        HideHelpPanel();
        HideProficiencyPanel();
        SubscribeProficiency();
        ProcessingSkillsWindowLayout.ApplyLayoutToOpenPanel(_root, _canvas);
        _root.gameObject.SetActive(true);
        Refresh();

        if (_smelter.ReadyBarAmount > 0 && !_smelter.IsSmelting)
            LogFurnaceBlocked("Collect furnace bars before smelting.");
    }

    public void Close()
    {
        HideOrePicker();
        HideHelpPanel();
        HideProficiencyPanel();
        HideEnhancementSlotTooltip();
        UnsubscribeProficiency();
        if (_smelter != null && SaveManager.Instance != null)
            SaveManager.Instance.NotifyInventoryChangedDebounced();

        ProcessingSkillsWindowLayout.RecordSessionFromPanel(_root, _canvas);
        HideImmediate();
        _clickSource?.NotifyClosed();
        _clickSource = null;

        if (_smelter != null)
        {
            _smelter.StateChanged -= RefreshOnStationChange;
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
        if (_sharedTooltip == null)
            _sharedTooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
    }

    private void Refresh()
    {
        RefreshOnStationChange();
        RefreshSmeltingLevelButton();
        RefreshSmeltingXpBar();
    }

    private void RefreshOnStationChange()
    {
        if (_smelter == null)
            return;

        CacheRefs();
        RefreshEnhancementSlotVisuals();
        RefreshFuelSlotVisuals();
        RefreshSlotVisuals();
        RefreshProgressOnly();
        RefreshTimeSummary();
        RefreshFuelSummary();
        RefreshActionButton();
        RefreshOreClearButton();
        RefreshEnhancementClearButton();
        RefreshFuelClearButton();
        RefreshActiveWorkButton();
    }

    private void RefreshFuelClearButton()
    {
        if (_fuelClearButton == null)
            return;

        bool hasFuel = _smelter != null && _smelter.StoredFuelAmount > 0;
        _fuelClearButton.gameObject.SetActive(hasFuel);
        _fuelClearButton.interactable = hasFuel;
    }

    private void RefreshFuelSlotVisuals()
    {
        if (_smelter == null)
            return;

        int amt = _smelter.StoredFuelAmount;
        string itemId = amt > 0 ? _smelter.StoredFuelItemId : "";
        ApplySlot(_fuelIcon, _fuelAmountText, itemId, amt, "Fuel", _itemDb);
    }

    private void RefreshEnhancementClearButton()
    {
        if (_enhancementClearButton == null)
            return;

        bool hasEnhancement = _smelter != null && _smelter.StoredEnhancementAmount > 0;
        _enhancementClearButton.gameObject.SetActive(hasEnhancement);
        _enhancementClearButton.interactable = hasEnhancement;
    }

    private void RefreshEnhancementSlotVisuals()
    {
        if (_smelter == null)
            return;

        int amt = _smelter.StoredEnhancementAmount;
        string itemId = amt > 0 ? _smelter.StoredEnhancementItemId : "";
        ApplySlot(_enhancementIcon, _enhancementAmountText, itemId, amt, "", _itemDb);
    }

    private void RefreshOreClearButton()
    {
        if (_oreClearButton == null)
            return;

        bool hasOre = _smelter != null && _smelter.StoredOreAmount > 0;
        _oreClearButton.gameObject.SetActive(hasOre);
        _oreClearButton.interactable = hasOre;
    }

    private void RefreshSlotVisuals()
    {
        int oreAmt = _smelter.StoredOreAmount;
        string oreId = oreAmt > 0 ? _smelter.StoredOreItemId : "";
        ApplySlot(_oreIcon, _oreAmountText, oreId, oreAmt, "Ores", _itemDb);

        int barAmt = _smelter.ReadyBarAmount;
        string barId = ResolveBarSlotItemId(oreAmt, barAmt);
        ApplySlot(_barIcon, _barAmountText, barId, barAmt, "Bars", _itemDb);
    }

    private string ResolveBarSlotItemId(int oreAmt, int barAmt)
    {
        if (barAmt > 0)
        {
            if (!string.IsNullOrWhiteSpace(_smelter.ReadyBarItemId))
                return _smelter.ReadyBarItemId;

            if (_smelter.TryGetActiveRecipe(out SmeltingRecipe recipe))
                return recipe.BarItemId;

            if (oreAmt > 0 && SmeltingRecipes.TryGetForOre(_smelter.StoredOreItemId, out recipe))
                return recipe.BarItemId;

            return "";
        }

        if (oreAmt > 0 || _smelter.IsSmelting)
        {
            if (oreAmt > 0 && SmeltingRecipes.TryGetForOre(_smelter.StoredOreItemId, out SmeltingRecipe recipe))
                return recipe.BarItemId;

            if (_smelter.TryGetActiveRecipe(out SmeltingRecipe activeRecipe))
                return activeRecipe.BarItemId;
        }

        return "";
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
    }

    private void RefreshTimeSummary()
    {
        if (_timeSummaryText == null || _smelter == null)
            return;

        if (!_smelter.TryGetSmeltTimeEstimate(
                out float totalRemaining,
                out _,
                out int barsRemaining))
        {
            _timeSummaryText.text = "";
            return;
        }

        int ore = _smelter.StoredOreAmount;
        string totalLabel = FormatSmeltDuration(totalRemaining);
        string barWord = barsRemaining == 1 ? "bar" : "bars";

        _timeSummaryText.text =
            $"Smelting time: {ore} ore ({barsRemaining} {barWord}) - {totalLabel}";
    }

    private void RefreshFuelSummary()
    {
        if (_fuelSummaryText == null || _smelter == null)
            return;

        if (!_smelter.HasFuel)
        {
            _fuelSummaryText.text = "Fuel remaining: none";
            return;
        }

        _fuelSummaryText.text =
            $"Fuel remaining: {FormatSmeltDuration(_smelter.FuelSecondsRemaining)}";
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
        else if (_smelter.ReadyBarAmount > 0)
        {
            _actionButtonText.text = "COLLECT";
            _actionButtonText.color = Accent;
            _actionButton.interactable = true;
        }
        else
        {
            _actionButtonText.text = "SMELT";
            bool canSmelt = _smelter.CanStartSmelting();
            _actionButtonText.color = canSmelt ? Accent : StopAccent;
            _actionButton.interactable = true;
        }
    }

    private void OnActionClicked()
    {
        if (_smelter == null)
            return;

        if (_smelter.IsSmelting)
        {
            _smelter.StopSmelting();
            Refresh();
            return;
        }

        if (_smelter.ReadyBarAmount > 0)
        {
            CollectBars(_smelter.ReadyBarAmount);
            return;
        }

        if (!_smelter.CanStartSmelting())
        {
            if (!_smelter.HasFuel)
                LogFurnaceBlocked("Add logs to the fuel slot before smelting.");
            else if (_smelter.StoredOreAmount < _smelter.GetOrePerBar())
            {
                int orePerBar = _smelter.GetOrePerBar();
                LogFurnaceBlocked($"Requires at least {orePerBar} ores to smelt into a bar.");
            }

            Refresh();
            return;
        }

        _smelter.TryStartSmelting();
        Refresh();
    }

    private void OnOreClearClicked()
    {
        RemoveAllOre();
    }

    private void OnEnhancementClearClicked()
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryWithdrawAllEnhancement(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogFurnaceBlocked(reason);
            return;
        }

        Refresh();
    }

    private void OnEnhancementClicked()
    {
        OpenSidePicker(SidePickerMode.Enhancement);
    }

    private void OnFuelClicked()
    {
        OpenSidePicker(SidePickerMode.Fuel);
    }

    private void OnFuelClearClicked()
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryWithdrawAllFuel(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogFurnaceBlocked(reason);
            return;
        }

        Refresh();
    }

    private void OnOresClicked()
    {
        OpenSidePicker(SidePickerMode.Ore);
    }

    private void OpenSidePicker(SidePickerMode mode)
    {
        if (_orePickerRoot == null)
            return;

        HideHelpPanel();

        if (_orePickerRoot.gameObject.activeSelf && _pickerMode == mode)
        {
            HideOrePicker();
            return;
        }

        _pickerMode = mode;
        if (mode == SidePickerMode.Ore)
            RebuildOrePicker();
        else if (mode == SidePickerMode.Fuel)
            RebuildFuelPicker();
        else
            RebuildEnhancementPicker();

        _orePickerRoot.gameObject.SetActive(true);
    }

    private void ShowEnhancementSlotTooltip()
    {
        if (_smelter == null || _smelter.StoredEnhancementAmount <= 0)
            return;

        CacheRefs();
        if (_sharedTooltip == null)
            return;

        ItemDefinition def = _itemDb != null ? _itemDb.Get(_smelter.StoredEnhancementItemId) : null;
        if (def == null)
            return;

        string body = def.GetProcessingEnhancementEffectDescription();
        if (string.IsNullOrWhiteSpace(body))
            body = def.description;

        var enhancementRt = _enhancementButton.GetComponent<RectTransform>();
        if (_sharedTooltip.TryGetComponent(out FlipInsideBounds flipper))
        {
            flipper.SetPreferredSide(FlipInsideBounds.PreferredSide.Left);
            flipper.SetMeasureRect(enhancementRt);
            flipper.SetHeightRect(enhancementRt);
            if (_root != null)
                flipper.SetBoundsRect(_root);
        }

        _sharedTooltip.ShowTextAt(
            _enhancementButton.transform,
            def.displayName,
            body,
            measureRect: enhancementRt,
            heightRect: enhancementRt,
            preferredSide: FlipInsideBounds.PreferredSide.Left);
        // ShowText resets overlay sort to inventory default; re-apply above this station panel.
        _sharedTooltip.PushOverlaySortOrder(CanvasSortingOrder + 100);
    }

    private void HideEnhancementSlotTooltip()
    {
        _sharedTooltip?.Hide();
    }

    private void OnHelpClicked()
    {
        if (_helpPanelRoot == null)
            return;

        bool show = !_helpPanelRoot.gameObject.activeSelf;
        if (!show)
        {
            HideHelpPanel();
            return;
        }

        HideOrePicker();
        _helpPanelRoot.gameObject.SetActive(true);
    }

    private void OnSmeltingLevelClicked()
    {
        if (_proficiencyPanelRoot == null)
            return;

        bool show = !_proficiencyPanelRoot.gameObject.activeSelf;
        if (!show)
        {
            HideProficiencyPanel();
            return;
        }

        RebuildProficiencyPanel();
        _proficiencyPanelRoot.gameObject.SetActive(true);
    }

    private void OnActiveWorkClicked()
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryActiveWork(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogFurnaceBlocked(reason);
            RefreshActiveWorkButton();
            return;
        }

        Refresh();
    }

    private void HideHelpPanel()
    {
        if (_helpPanelRoot != null)
            _helpPanelRoot.gameObject.SetActive(false);
    }

    private void HideProficiencyPanel()
    {
        if (_proficiencyPanelRoot != null)
            _proficiencyPanelRoot.gameObject.SetActive(false);
    }

    private void SubscribeProficiency()
    {
        if (_proficiencySubscribed)
            return;

        ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
        runtime.Changed += OnProficiencyChanged;
        _proficiencySubscribed = true;
    }

    private void UnsubscribeProficiency()
    {
        if (!_proficiencySubscribed)
            return;

        if (ProcessingProficiencyRuntime.Instance != null)
            ProcessingProficiencyRuntime.Instance.Changed -= OnProficiencyChanged;
        _proficiencySubscribed = false;
    }

    private void OnProficiencyChanged()
    {
        if (!IsOpen)
            return;

        RefreshSmeltingLevelButton();
        RefreshSmeltingXpBar();
        RefreshActiveWorkButton();
        if (_proficiencyPanelRoot != null && _proficiencyPanelRoot.gameObject.activeSelf)
            RebuildProficiencyPanel();
    }

    private void RefreshSmeltingLevelButton()
    {
        if (_smeltingLevelButtonText == null)
            return;

        int level = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Smelting);
        _smeltingLevelButtonText.text = $"< Smelting: Lv {level} >";
    }

    private void RefreshSmeltingXpBar()
    {
        if (_smeltingXpFillRt == null)
            return;

        ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
        int level = runtime.GetLevel(ProcessingSkillType.Smelting);
        float t = runtime.GetProgress01(ProcessingSkillType.Smelting);
        _smeltingXpFillRt.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
    }

    private void RefreshActiveWorkButton()
    {
        if (_activeWorkButton == null || _activeWorkButtonText == null)
            return;

        bool smelting = _smelter != null && _smelter.IsSmelting;
        float cooldown = ProcessingProficiencyRuntime.EnsureInstance().GetActiveWorkCooldownRemaining();
        bool onCooldown = cooldown > 0.01f;

        _activeWorkButton.interactable = smelting && !onCooldown;
        if (!smelting)
            _activeWorkButtonText.text = "Speed Up";
        else if (onCooldown)
            _activeWorkButtonText.text = $"Speed Up ({Mathf.CeilToInt(cooldown)}s)";
        else
            _activeWorkButtonText.text = "Speed Up";

        _activeWorkButtonText.color = _activeWorkButton.interactable ? Accent : StopAccent;
    }

    private void RebuildProficiencyPanel()
    {
        if (_proficiencyScrollContent == null || _proficiencyFooterText == null)
            return;

        for (int i = _proficiencyScrollContent.childCount - 1; i >= 0; i--)
            Destroy(_proficiencyScrollContent.GetChild(i).gameObject);

        ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
        int level = runtime.GetLevel(ProcessingSkillType.Smelting);
        SmeltingProficiencyBonuses bonuses = runtime.GetSmeltingBonuses();

        CreateProficiencyLine($"Smelting — Lv {level}", Accent, 16f, FontStyles.Bold);

        if (level < ProcessingSkillCurves.MaxLevel)
        {
            int xp = runtime.GetXp(ProcessingSkillType.Smelting);
            int needed = runtime.GetXpToNextLevel(ProcessingSkillType.Smelting);
            CreateProficiencyLine(
                $"Next level: {xp}/{needed} XP",
                TextLight,
                12f,
                FontStyles.Normal);
        }
        else
        {
            CreateProficiencyLine("Max level reached", TextLight, 12f, FontStyles.Normal);
        }

        CreateProficiencyLine("", TextLight, 6f, FontStyles.Normal);

        List<ProcessingProficiencyUnlockLines.Row> unlockLines = SmeltingProficiencyBonuses.BuildDisplayUnlockRows();
        for (int i = 0; i < unlockLines.Count; i++)
        {
            ProcessingProficiencyUnlockLines.Row row = unlockLines[i];
            bool unlocked = level >= row.Level;
            Color color = unlocked ? Accent : new Color32(150, 150, 150, 255);
            bool compactWithNext = i + 1 < unlockLines.Count && unlockLines[i + 1].Level == row.Level;
            CreateProficiencyLine($"Lv {row.Level}: {row.Description}", color, 13f, FontStyles.Normal, compactWithNext);
        }

        float activeWorkSeconds = bonuses.ActiveWorkSecondsPerClick >= 1.99f ? 2 : 1;
        _proficiencyFooterText.text =
            "Total bonuses granted:\n" +
            $"{FormatPercent(bonuses.SpeedBonusPercent)} increased smelting speed\n" +
            $"{FormatPercent(bonuses.DoubleBarChancePercent)} chance to make 2 instead of 1 bar\n" +
            $"Speed up -> {activeWorkSeconds}s, 3s cooldown";
    }

    private void CreateProficiencyLine(string text, Color color, float fontSize, FontStyles style, bool compactWithNext = false)
    {
        var row = CreateUiObject("Line", _proficiencyScrollContent, typeof(RectTransform), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = compactWithNext ? fontSize + 2f : fontSize + 10f;
        var tmp = CreateTmpText("Text", row.transform, fontSize, color, TextAlignmentOptions.TopLeft);
        StretchFull(tmp.rectTransform);
        tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.text = text;
    }

    private static string FormatPercent(float value) => $"{value:0.#}%";

    private void HideOrePicker()
    {
        if (_orePickerRoot != null)
            _orePickerRoot.gameObject.SetActive(false);
    }

    private void RebuildOrePicker()
    {
        if (_orePickerScrollContent == null)
            return;

        CacheRefs();
        for (int i = _orePickerScrollContent.childCount - 1; i >= 0; i--)
            Destroy(_orePickerScrollContent.GetChild(i).gameObject);

        IReadOnlyList<SmeltingRecipe> recipes = SmeltingRecipes.All;
        int smeltingLevel = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Smelting);
        bool any = false;
        for (int i = 0; i < recipes.Count; i++)
        {
            SmeltingRecipe recipe = recipes[i];
            int count = _inventory != null ? _inventory.GetTotalAmount(recipe.OreItemId) : 0;
            if (count <= 0)
                continue;

            bool levelTooLow = smeltingLevel < SmeltingRecipes.GetRequiredSmeltingLevel(recipe);
            any = true;
            CreateOrePickerRow(recipe, count, levelTooLow);
        }

        if (!any)
            CreateOrePickerMessage("No smeltable ore in inventory.");
    }

    private void CreateOrePickerMessage(string message)
    {
        var row = CreateUiObject("Msg", _orePickerScrollContent, typeof(RectTransform), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = 28f;
        var text = CreateTmpText("Label", row.transform, 14f, TextLight, TextAlignmentOptions.MidlineLeft);
        StretchFull(text.rectTransform);
        text.text = message;
    }

    private void CreateOrePickerRow(SmeltingRecipe recipe, int playerCount, bool levelTooLow)
    {
        ItemDefinition def = _itemDb != null ? _itemDb.Get(recipe.OreItemId) : null;
        string label = def != null ? def.displayName : recipe.OreItemId;
        string oreId = recipe.OreItemId;

        var row = CreateUiObject("OreRow", _orePickerScrollContent, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
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
        labelText.color = levelTooLow ? StopAccent : TextLight;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.text = $"{label}  x{playerCount}";

        if (levelTooLow)
        {
            int requiredLevel = SmeltingRecipes.GetRequiredSmeltingLevel(recipe);
            CreateCompactPickerButton(
                row.transform,
                "Level too low",
                92f,
                () => LogFurnaceBlocked($"Requires Smelting level {requiredLevel}."),
                StopAccent);
        }
        else
        {
            CreateCompactPickerButton(row.transform, "x5", 42f, () => DepositOreFromPicker(oreId, 5));
            CreateCompactPickerButton(row.transform, "xAll", 48f, () => DepositAllOreFromPicker(oreId));
        }
    }

    private void DepositOreFromPicker(string oreId, int amount)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryDepositOre(oreId, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogFurnaceBlocked(reason);
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
                LogFurnaceBlocked(reason);
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void RefreshPickerAfterDeposit()
    {
        Refresh();
        if (_orePickerRoot != null && _orePickerRoot.gameObject.activeSelf)
        {
            if (_pickerMode == SidePickerMode.Enhancement)
                RebuildEnhancementPicker();
            else if (_pickerMode == SidePickerMode.Fuel)
                RebuildFuelPicker();
            else
                RebuildOrePicker();
        }
    }

    private void RebuildEnhancementPicker()
    {
        if (_orePickerScrollContent == null)
            return;

        CacheRefs();
        for (int i = _orePickerScrollContent.childCount - 1; i >= 0; i--)
            Destroy(_orePickerScrollContent.GetChild(i).gameObject);

        bool any = false;
        if (_itemDb != null)
        {
            IReadOnlyList<ItemDefinition> items = _itemDb.GetAll();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition def = items[i];
                if (def == null || !def.IsProcessingSkillEnhancement)
                    continue;
                if (def.ProcessingSkillTarget != ProcessingSkillTarget.Smelting)
                    continue;

                int count = _inventory != null ? _inventory.GetTotalAmount(def.itemId) : 0;
                if (count <= 0)
                    continue;

                any = true;
                CreateEnhancementPickerRow(def, count);
            }
        }

        if (!any)
            CreateOrePickerMessage("No smelting enhancements in inventory.");
    }

    private void RebuildFuelPicker()
    {
        if (_orePickerScrollContent == null)
            return;

        CacheRefs();
        for (int i = _orePickerScrollContent.childCount - 1; i >= 0; i--)
            Destroy(_orePickerScrollContent.GetChild(i).gameObject);

        bool any = false;
        ReadOnlySpan<string> logIds = ProcessingFuelCatalog.AllLogIds;
        for (int i = 0; i < logIds.Length; i++)
        {
            string logId = logIds[i];
            int count = _inventory != null ? _inventory.GetTotalAmount(logId) : 0;
            if (count <= 0)
                continue;

            any = true;
            CreateFuelPickerRow(logId, count);
        }

        if (!any)
            CreateOrePickerMessage("No logs in inventory.");
    }

    private void CreateFuelPickerRow(string logId, int playerCount)
    {
        ItemDefinition def = _itemDb != null ? _itemDb.Get(logId) : null;
        string label = def != null ? def.displayName : logId;
        ProcessingFuelCatalog.TryGetSecondsPerLog(logId, out float secondsPerLog);

        var row = CreateUiObject("FuelRow", _orePickerScrollContent, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
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
        labelText.text = $"{label} ({secondsPerLog:0}s)  x{playerCount}";

        CreateCompactPickerButton(row.transform, "x5", 42f, () => DepositFuelFromPicker(logId, 5));
        CreateCompactPickerButton(row.transform, "xAll", 48f, () => DepositAllFuelFromPicker(logId));
    }

    private void DepositFuelFromPicker(string logId, int amount)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryDepositFuel(logId, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogFurnaceBlocked(reason);
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void DepositAllFuelFromPicker(string logId)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryDepositAllFuelFromInventory(logId, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogFurnaceBlocked(reason);
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void CreateEnhancementPickerRow(ItemDefinition def, int playerCount)
    {
        string label = def != null ? def.displayName : "";
        string itemId = def != null ? def.itemId : "";

        var row = CreateUiObject("EnhancementRow", _orePickerScrollContent, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
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

        CreateCompactPickerButton(row.transform, "x5", 42f, () => DepositEnhancementFromPicker(itemId, 5));
        CreateCompactPickerButton(row.transform, "xAll", 48f, () => DepositAllEnhancementFromPicker(itemId));
    }

    private void DepositEnhancementFromPicker(string itemId, int amount)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryDepositEnhancement(itemId, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogFurnaceBlocked(reason);
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void DepositAllEnhancementFromPicker(string itemId)
    {
        if (_smelter == null)
            return;

        if (!_smelter.TryDepositAllEnhancementFromInventory(itemId, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogFurnaceBlocked(reason);
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private Button CreateCompactPickerButton(
        Transform parent,
        string label,
        float width,
        UnityEngine.Events.UnityAction onClick,
        Color? textColor = null)
    {
        var go = CreateUiObject(label + "Btn", parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 28f;
        le.minWidth = width;
        go.GetComponent<Image>().color = SlotBg;

        var tmp = CreateTmpText("Text", go.transform, 13f, textColor ?? Accent, TextAlignmentOptions.Center);
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
            int smeltingLevel = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Smelting);
            for (int i = 0; i < recipes.Count; i++)
            {
                SmeltingRecipe recipe = recipes[i];
                if (smeltingLevel < SmeltingRecipes.GetRequiredSmeltingLevel(recipe))
                    continue;

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
                LogFurnaceBlocked(reason);
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
            if (!string.IsNullOrWhiteSpace(reason) && reason != "No ore stored.")
                LogFurnaceBlocked(reason);
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
            new("Collect all", () => CollectBars(ready), ready <= 0)
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
                LogFurnaceBlocked(reason);
            return;
        }

        Refresh();
    }

    private void LogFurnaceBlocked(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (Time.unscaledTime < _nextPendingBarsLogTime)
            return;

        _nextPendingBarsLogTime = Time.unscaledTime + FurnaceBlockedLogCooldownSeconds;
        GameLog.Add(message, GameLog.CannotMessageColor);
    }

    private void BuildUi()
    {
        if (_root != null && _progressText != null && _timeSummaryText != null && _oreClearButton != null &&
            _smeltingLevelButton != null && _helpButton != null && _activeWorkButton != null &&
            _smeltingXpFillRt != null && _orePickerScrollContent != null && _enhancementIcon != null &&
            _fuelIcon != null && _fuelSummaryText != null && _builtUiLayoutVersion == UiLayoutVersion)
            return;

        if (_root != null)
        {
            Destroy(_root.gameObject);
            _root = null;
            _orePickerRoot = null;
            _orePickerScrollContent = null;
            _helpPanelRoot = null;
            _proficiencyPanelRoot = null;
            _proficiencyScrollContent = null;
            _proficiencyFooterText = null;
            _progressText = null;
            _timeSummaryText = null;
            _fuelSummaryText = null;
            _progressFill = null;
            _progressFillRt = null;
            _actionButton = null;
            _actionButtonText = null;
            _oresButton = null;
            _barsButton = null;
            _oreClearButton = null;
            _helpButton = null;
            _smeltingLevelButton = null;
            _smeltingLevelButtonText = null;
            _smeltingXpFill = null;
            _smeltingXpFillRt = null;
            _activeWorkButton = null;
            _activeWorkButtonText = null;
            _enhancementButton = null;
            _enhancementClearButton = null;
            _enhancementIcon = null;
            _enhancementAmountText = null;
            _fuelButton = null;
            _fuelClearButton = null;
            _fuelIcon = null;
            _fuelAmountText = null;
            _oreIcon = null;
            _barIcon = null;
            _oreAmountText = null;
            _barAmountText = null;
        }

        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null)
            _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = DefaultCanvasSortingOrder;

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

        _root = CreatePanel("FurnacePanel", transform, MainPanelSize);
        CreateHeader(_root, "Furnace");

        _helpButton = CreateButton(_root, "HelpButton", "?", new Vector2(28f, 28f), new Vector2(0f, 1f));
        var helpRt = _helpButton.GetComponent<RectTransform>();
        helpRt.pivot = new Vector2(0f, 1f);
        helpRt.anchoredPosition = new Vector2(12f, -12f);
        _helpButton.onClick.AddListener(OnHelpClicked);

        _smeltingLevelButton = CreateButton(_root, "SmeltingLevelButton", "< Smelting: Lv 1 >", new Vector2(220f, 24f), new Vector2(0.5f, 1f));
        var smeltBtnRt = _smeltingLevelButton.GetComponent<RectTransform>();
        smeltBtnRt.pivot = new Vector2(0.5f, 1f);
        smeltBtnRt.anchoredPosition = new Vector2(0f, -42f);
        _smeltingLevelButtonText = _smeltingLevelButton.GetComponentInChildren<TMP_Text>();
        _smeltingLevelButtonText.fontSize = 13f;
        _smeltingLevelButton.onClick.AddListener(OnSmeltingLevelClicked);

        BuildSmeltingXpBar(_root);

        var processingBlock = CreateUiObject("ProcessingBlock", _root, typeof(RectTransform), typeof(VerticalLayoutGroup));
        var processingRt = processingBlock.GetComponent<RectTransform>();
        processingRt.anchorMin = new Vector2(0.5f, 0.57f);
        processingRt.anchorMax = new Vector2(0.5f, 0.57f);
        processingRt.pivot = new Vector2(0.5f, 0.5f);
        processingRt.sizeDelta = new Vector2(300f, 200f);
        var processingLayout = processingBlock.GetComponent<VerticalLayoutGroup>();
        processingLayout.spacing = 8f;
        processingLayout.childAlignment = TextAnchor.MiddleCenter;
        processingLayout.childControlWidth = false;
        processingLayout.childControlHeight = false;

        var topSlotsRow = CreateUiObject("TopSlotsRow", processingBlock.transform, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var topSlotsRt = topSlotsRow.GetComponent<RectTransform>();
        topSlotsRt.sizeDelta = new Vector2(200f, 64f);
        var topHlg = topSlotsRow.GetComponent<HorizontalLayoutGroup>();
        topHlg.spacing = 32f;
        topHlg.childAlignment = TextAnchor.MiddleCenter;
        topHlg.childControlWidth = false;
        topHlg.childControlHeight = false;

        _fuelButton = CreateSlotButton(topSlotsRow.transform, "Fuel", out _fuelIcon, out _fuelAmountText, OnFuelClicked);
        var fuelRt = _fuelButton.GetComponent<RectTransform>();
        fuelRt.sizeDelta = new Vector2(64f, 64f);
        var fuelIconRt = _fuelIcon.rectTransform;
        fuelIconRt.sizeDelta = new Vector2(40f, 40f);
        var fuelTrigger = _fuelButton.gameObject.AddComponent<FurnaceFuelSlotInteractions>();
        fuelTrigger.Initialize(this);

        _fuelClearButton = CreateButton(_fuelButton.transform, "FuelClear", "×", new Vector2(22f, 22f), new Vector2(1f, 1f));
        var fuelClearRt = _fuelClearButton.GetComponent<RectTransform>();
        fuelClearRt.anchoredPosition = new Vector2(-4f, -4f);
        var fuelClearClick = _fuelClearButton.gameObject.AddComponent<FurnaceSlotClearClick>();
        fuelClearClick.Initialize(OnFuelClearClicked);
        _fuelClearButton.transform.SetAsLastSibling();
        _fuelClearButton.gameObject.SetActive(false);

        _enhancementButton = CreateSlotButton(topSlotsRow.transform, "", out _enhancementIcon, out _enhancementAmountText, OnEnhancementClicked);
        var enhancementRt = _enhancementButton.GetComponent<RectTransform>();
        enhancementRt.sizeDelta = new Vector2(64f, 64f);
        var enhancementIconRt = _enhancementIcon.rectTransform;
        enhancementIconRt.sizeDelta = new Vector2(40f, 40f);
        var enhancementTrigger = _enhancementButton.gameObject.AddComponent<FurnaceEnhancementSlotInteractions>();
        enhancementTrigger.Initialize(this);
        var enhancementHover = _enhancementButton.gameObject.AddComponent<FurnaceEnhancementSlotHover>();
        enhancementHover.Initialize(this);

        _enhancementClearButton = CreateButton(_enhancementButton.transform, "EnhancementClear", "×", new Vector2(22f, 22f), new Vector2(1f, 1f));
        var enhancementClearRt = _enhancementClearButton.GetComponent<RectTransform>();
        enhancementClearRt.anchoredPosition = new Vector2(-4f, -4f);
        var enhancementClearClick = _enhancementClearButton.gameObject.AddComponent<FurnaceSlotClearClick>();
        enhancementClearClick.Initialize(OnEnhancementClearClicked);
        _enhancementClearButton.transform.SetAsLastSibling();
        _enhancementClearButton.gameObject.SetActive(false);

        var slotsRow = CreateUiObject("SlotsRow", processingBlock.transform, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var slotsRt = slotsRow.GetComponent<RectTransform>();
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
        _oreClearButton.gameObject.SetActive(true);

        var centerColumn = CreateUiObject("CenterColumn", slotsRow.transform, typeof(RectTransform), typeof(VerticalLayoutGroup));
        var centerRt = centerColumn.GetComponent<RectTransform>();
        centerRt.sizeDelta = new Vector2(76f, 88f);
        var centerVlg = centerColumn.GetComponent<VerticalLayoutGroup>();
        centerVlg.spacing = 6f;
        centerVlg.childAlignment = TextAnchor.MiddleCenter;
        centerVlg.childControlWidth = false;
        centerVlg.childControlHeight = false;

        _activeWorkButton = CreateButton(centerColumn.transform, "ActiveWorkButton", "Speed Up", new Vector2(76f, 18f), new Vector2(0.5f, 0.5f));
        _activeWorkButtonText = _activeWorkButton.GetComponentInChildren<TMP_Text>();
        _activeWorkButtonText.fontSize = 10f;
        _activeWorkButton.onClick.AddListener(OnActiveWorkClicked);

        TextMeshProUGUI arrowText = CreateTmpText("Arrow", centerColumn.transform, 28f, Accent, TextAlignmentOptions.Center);
        arrowText.text = "→";
        arrowText.rectTransform.sizeDelta = new Vector2(28f, 28f);

        _barsButton = CreateSlotButton(slotsRow.transform, "Bars", out _barIcon, out _barAmountText, null);
        var barsTrigger = _barsButton.gameObject.AddComponent<FurnaceBarsContextTrigger>();
        barsTrigger.Initialize(this);

        var progressBg = CreateUiObject("ProgressBg", _root, typeof(RectTransform), typeof(Image));
        var progressBgRt = progressBg.GetComponent<RectTransform>();
        progressBgRt.anchorMin = new Vector2(0.5f, 0.305f);
        progressBgRt.anchorMax = new Vector2(0.5f, 0.305f);
        progressBgRt.pivot = new Vector2(0.5f, 0.5f);
        progressBgRt.sizeDelta = new Vector2(280f, 30f);
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
        summaryRt.anchorMin = new Vector2(0.5f, 0.225f);
        summaryRt.anchorMax = new Vector2(0.5f, 0.225f);
        summaryRt.pivot = new Vector2(0.5f, 0.5f);
        summaryRt.sizeDelta = new Vector2(320f, 40f);
        _timeSummaryText.textWrappingMode = TextWrappingModes.Normal;
        _timeSummaryText.text = "";

        _fuelSummaryText = CreateTmpText("FuelSummary", _root, 11f, TextLight, TextAlignmentOptions.Center);
        var fuelSummaryRt = _fuelSummaryText.rectTransform;
        fuelSummaryRt.anchorMin = new Vector2(0.5f, 0.195f);
        fuelSummaryRt.anchorMax = new Vector2(0.5f, 0.195f);
        fuelSummaryRt.pivot = new Vector2(0.5f, 0.5f);
        fuelSummaryRt.sizeDelta = new Vector2(320f, 18f);
        _fuelSummaryText.text = "";

        _actionButton = CreateButton(_root, "ActionButton", "START", new Vector2(200f, 40f), new Vector2(0.5f, 0.1f));
        _actionButtonText = _actionButton.GetComponentInChildren<TMP_Text>();
        _actionButton.onClick.AddListener(OnActionClicked);

        var closeBtn = CreateButton(_root, "CloseButton", "X", new Vector2(32f, 32f), new Vector2(1f, 1f));
        closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-12f, -12f);
        closeBtn.onClick.AddListener(Close);

        _helpPanelRoot = CreateSidePanel("HelpPanel", _root, HelpPanelSize, leftSide: true);
        BuildHelpPanelContent(_helpPanelRoot);
        _helpPanelRoot.gameObject.SetActive(false);

        _orePickerRoot = CreateSidePanel("OrePicker", _root, SidePickerPanelSize, leftSide: true);
        _orePickerScrollContent = BuildScrollListPanel(_orePickerRoot, preferredHeight: 200f);
        _orePickerRoot.gameObject.SetActive(false);

        _proficiencyPanelRoot = CreateSidePanel("ProficiencyPanel", _root, new Vector2(300f, 320f), leftSide: false);
        BuildProficiencyPanelContent(_proficiencyPanelRoot);
        _proficiencyPanelRoot.gameObject.SetActive(false);

        RefreshSmeltingLevelButton();
        RefreshSmeltingXpBar();
        RefreshActiveWorkButton();
        ProcessingSkillsWindowLayout.EnsureProcessingDragBackdrop(_root, _canvas);
        _builtUiLayoutVersion = UiLayoutVersion;
    }

    private void BuildSmeltingXpBar(RectTransform parent)
    {
        var xpBg = CreateUiObject("SmeltingXpBg", parent, typeof(RectTransform), typeof(Image));
        var xpBgRt = xpBg.GetComponent<RectTransform>();
        xpBgRt.anchorMin = new Vector2(0.5f, 1f);
        xpBgRt.anchorMax = new Vector2(0.5f, 1f);
        xpBgRt.pivot = new Vector2(0.5f, 1f);
        xpBgRt.anchoredPosition = new Vector2(0f, -68f);
        xpBgRt.sizeDelta = new Vector2(240f, 8f);
        xpBg.GetComponent<Image>().color = ProgressBg;
        xpBg.GetComponent<Image>().raycastTarget = false;

        var xpFillGo = CreateUiObject("Fill", xpBg.transform, typeof(RectTransform), typeof(Image));
        _smeltingXpFillRt = xpFillGo.GetComponent<RectTransform>();
        _smeltingXpFillRt.anchorMin = Vector2.zero;
        _smeltingXpFillRt.anchorMax = Vector2.zero;
        _smeltingXpFillRt.offsetMin = Vector2.zero;
        _smeltingXpFillRt.offsetMax = Vector2.zero;
        _smeltingXpFill = xpFillGo.GetComponent<Image>();
        _smeltingXpFill.color = Accent;
        _smeltingXpFill.raycastTarget = false;
    }

    private RectTransform CreateSidePanel(string name, Transform parent, Vector2 size, bool leftSide)
    {
        var panel = CreatePanel(name, parent, size);
        panel.anchorMin = new Vector2(leftSide ? 0f : 1f, 0.5f);
        panel.anchorMax = new Vector2(leftSide ? 0f : 1f, 0.5f);
        panel.pivot = new Vector2(leftSide ? 0f : 1f, 0.5f);
        float offset = size.x + SidePanelGap;
        panel.anchoredPosition = new Vector2(leftSide ? -offset : offset, 0f);
        return panel;
    }

    private void BuildHelpPanelContent(RectTransform panel)
    {
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var headerRow = CreateUiObject("Header", panel, typeof(RectTransform), typeof(LayoutElement));
        headerRow.GetComponent<LayoutElement>().preferredHeight = 24f;
        var headerText = CreateTmpText("Title", headerRow.transform, 16f, Accent, TextAlignmentOptions.MidlineLeft);
        StretchFull(headerText.rectTransform);
        headerText.fontStyle = FontStyles.Bold;
        headerText.text = "Smelting";

        var scrollHost = CreateUiObject("ScrollHost", panel, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
        scrollHost.GetComponent<LayoutElement>().preferredHeight = 220f;
        scrollHost.GetComponent<LayoutElement>().flexibleHeight = 1f;
        scrollHost.GetComponent<Image>().color = new Color32(36, 38, 42, 255);

        var viewport = CreateUiObject("Viewport", scrollHost.transform, typeof(RectTransform), typeof(RectMask2D));
        StretchFull(viewport.GetComponent<RectTransform>());

        var content = CreatePanel("Content", viewport.transform, new Vector2(0f, 0f));
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 0f);
        content.GetComponent<Image>().color = Color.clear;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var bodyText = CreateTmpText("Body", content.transform, 13f, TextLight, TextAlignmentOptions.TopLeft);
        var bodyRt = bodyText.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 1f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.sizeDelta = new Vector2(-12f, 0f);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.text = HelpBodyText;
        bodyText.ForceMeshUpdate();
        bodyRt.sizeDelta = new Vector2(-12f, bodyText.preferredHeight + 8f);

        ScrollRect scroll = scrollHost.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = ScrollSensitivity;
    }

    private RectTransform BuildScrollListPanel(RectTransform panel, float preferredHeight)
    {
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var scrollHost = CreateUiObject("ScrollHost", panel, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
        var scrollLe = scrollHost.GetComponent<LayoutElement>();
        scrollLe.preferredHeight = preferredHeight;
        scrollLe.flexibleHeight = 1f;
        scrollHost.GetComponent<Image>().color = new Color32(36, 38, 42, 255);

        var viewport = CreateUiObject("Viewport", scrollHost.transform, typeof(RectTransform), typeof(RectMask2D));
        StretchFull(viewport.GetComponent<RectTransform>());

        RectTransform content = CreatePanel("Content", viewport.transform, new Vector2(0f, 0f));
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 0f);
        content.GetComponent<Image>().color = Color.clear;
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.spacing = 6f;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollHost.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = ScrollSensitivity;

        return content;
    }

    private void BuildProficiencyPanelContent(RectTransform panel)
    {
        var rootLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(8, 8, 8, 8);
        rootLayout.spacing = 6f;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandHeight = false;

        var scrollHost = CreateUiObject("ScrollHost", panel, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
        scrollHost.GetComponent<LayoutElement>().preferredHeight = 220f;
        scrollHost.GetComponent<LayoutElement>().flexibleHeight = 1f;
        scrollHost.GetComponent<Image>().color = new Color32(36, 38, 42, 255);

        var viewport = CreateUiObject("Viewport", scrollHost.transform, typeof(RectTransform), typeof(RectMask2D));
        StretchFull(viewport.GetComponent<RectTransform>());

        _proficiencyScrollContent = CreatePanel("Content", viewport.transform, new Vector2(0f, 0f));
        _proficiencyScrollContent.anchorMin = new Vector2(0f, 1f);
        _proficiencyScrollContent.anchorMax = new Vector2(1f, 1f);
        _proficiencyScrollContent.pivot = new Vector2(0.5f, 1f);
        _proficiencyScrollContent.sizeDelta = new Vector2(0f, 0f);
        _proficiencyScrollContent.GetComponent<Image>().color = Color.clear;
        var contentLayout = _proficiencyScrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(6, 6, 6, 6);
        contentLayout.spacing = 4f;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandHeight = false;
        _proficiencyScrollContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollHost.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = _proficiencyScrollContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = ScrollSensitivity;

        var footerRow = CreateUiObject("Footer", panel, typeof(RectTransform), typeof(LayoutElement));
        footerRow.GetComponent<LayoutElement>().preferredHeight = 72f;
        _proficiencyFooterText = CreateTmpText("Footer", footerRow.transform, 12f, TextLight, TextAlignmentOptions.TopLeft);
        StretchFull(_proficiencyFooterText.rectTransform);
        _proficiencyFooterText.textWrappingMode = TextWrappingModes.Normal;
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

    private sealed class FurnaceSlotClearClick : MonoBehaviour, IPointerClickHandler
    {
        private System.Action _onClick;

        public void Initialize(System.Action onClick) => _onClick = onClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            var button = GetComponent<Button>();
            if (button != null && !button.interactable)
                return;

            _onClick?.Invoke();
            eventData.Use();
        }
    }

    private sealed class FurnaceEnhancementSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private FurnaceUI _owner;

        public void Initialize(FurnaceUI owner) => _owner = owner;

        public void OnPointerEnter(PointerEventData eventData) => _owner?.ShowEnhancementSlotTooltip();

        public void OnPointerExit(PointerEventData eventData) => _owner?.HideEnhancementSlotTooltip();
    }

    private sealed class FurnaceEnhancementSlotInteractions : MonoBehaviour, IDropHandler
    {
        private FurnaceUI _owner;

        public void Initialize(FurnaceUI owner) => _owner = owner;

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
            if (FurnaceUI.TryDepositEnhancementFromInventorySlot(inv, fromSlot, amount))
                InventoryDragState.EndDrag();
        }
    }

    private sealed class FurnaceFuelSlotInteractions : MonoBehaviour, IDropHandler
    {
        private FurnaceUI _owner;

        public void Initialize(FurnaceUI owner) => _owner = owner;

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
            if (FurnaceUI.TryDepositFuelFromInventorySlot(inv, fromSlot, amount))
                InventoryDragState.EndDrag();
        }
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