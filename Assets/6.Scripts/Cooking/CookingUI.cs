using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CookingUI : MonoBehaviour
{
    private static CookingUI _instance;

    private static readonly Color PanelBg = new Color32(28, 30, 34, 245);
    private static readonly Color SlotBg = new Color32(42, 44, 50, 255);
    private static readonly Color Accent = new Color32(120, 200, 110, 255);
    private static readonly Color TextLight = new Color32(220, 214, 198, 255);
    private static readonly Color ProgressBg = new Color32(100, 100, 100, 255);
    private static readonly Color ProgressFill = new Color32(90, 185, 80, 255);
    private static readonly Color PickerRowBg = new Color32(52, 54, 60, 255);
    private static readonly Color StopAccent = new Color32(220, 80, 80, 255);

    private CookingStation _station;
    private CookingClick _clickSource;
    private Inventory _inventory;
    private ItemDatabase _itemDb;

    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _fishPickerRoot;
    private RectTransform _fishPickerScrollContent;
    private Image _fishIcon;
    private Image _cookedIcon;
    private Image _enhancementIcon;
    private TMP_Text _fishAmountText;
    private TMP_Text _cookedAmountText;
    private TMP_Text _enhancementAmountText;
    private Button _enhancementButton;
    private Button _enhancementClearButton;
    private Image _progressFill;
    private RectTransform _progressFillRt;
    private TMP_Text _progressText;
    private TMP_Text _timeSummaryText;
    private TMP_Text _burnChanceText;
    private TMP_Text _actionButtonText;
    private Button _actionButton;
    private Button _fishButton;
    private Button _cookedButton;
    private Button _fishClearButton;
    private Button _helpButton;
    private Button _cookingLevelButton;
    private TMP_Text _cookingLevelButtonText;
    private Image _cookingXpFill;
    private RectTransform _cookingXpFillRt;
    private Button _activeWorkButton;
    private TMP_Text _activeWorkButtonText;
    private Toggle _showBurnLogsToggle;
    private SharedTooltipUI _sharedTooltip;

    private enum SidePickerMode
    {
        Fish,
        Enhancement
    }

    private SidePickerMode _pickerMode;
    private const int UiLayoutVersion = 4;
    private int _builtUiLayoutVersion;

    private RectTransform _helpPanelRoot;
    private RectTransform _proficiencyPanelRoot;
    private RectTransform _proficiencyScrollContent;
    private TMP_Text _proficiencyFooterText;

    private float _nextPendingCookedLogTime;
    private bool _proficiencySubscribed;

    private const string HelpBodyText =
        "The cooking range turns raw fish into cooked food over time.\n\n" +
        "Deposit fish, press Cook, and wait for food to finish. Collect cooked food before adding new fish or starting another batch.\n\n" +
        "Better fish take longer to cook but grant more Cooking proficiency.\n\n" +
        "Each portion can burn while cooking. Higher Cooking level reduces burn chance.\n\n" +
        "Speed Up trims time from the current portion while cooking (3s cooldown).";

    private const int DefaultCanvasSortingOrder = 12000;
    private const float SidePanelGap = 10f;
    private const float CookingBlockedLogCooldownSeconds = 2f;
    private const string ShowBurnLogsPrefsKey = "Settings.CookingShowBurnLogs";
    private static readonly Vector2 MainPanelSize = new Vector2(360f, 460f);
    private static readonly Vector2 SidePickerPanelSize = new Vector2(280f, 240f);
    private static readonly Vector2 HelpPanelSize = new Vector2(280f, 280f);

    public static bool ShouldLogBurnMessages =>
        PlayerPrefs.GetInt(ShowBurnLogsPrefsKey, 1) != 0;

    public static CookingUI Instance => _instance;
    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.gameObject.activeSelf;

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

    public static CookingStation ActiveStation => _instance != null ? _instance._station : null;

    /// <summary>Deposit fish from an inventory slot while the cooking UI is open (drag-drop / double-click).</summary>
    public static bool TryDepositFromInventorySlot(Inventory inv, int slotIndex, int amount = 0)
    {
        if (!IsOpen || _instance._station == null || inv == null || slotIndex < 0)
            return false;

        if (!_instance._station.TryDepositRawFromInventorySlot(inv, slotIndex, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Cooking] {reason}");
            return false;
        }

        _instance.Refresh();
        return true;
    }

    public static bool TryDepositEnhancementFromInventorySlot(Inventory inv, int slotIndex, int amount = 0)
    {
        if (!IsOpen || _instance._station == null || inv == null || slotIndex < 0)
            return false;

        if (!_instance._station.TryDepositEnhancementFromInventorySlot(inv, slotIndex, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Cooking] {reason}");
            return false;
        }

        _instance.Refresh();
        return true;
    }

    public static CookingUI EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("CookingUI", typeof(CookingUI));
        DontDestroyOnLoad(host);
        _instance = host.GetComponent<CookingUI>();
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
        if (_station != null)
            _station.StateChanged -= RefreshOnStationChange;

        UnsubscribeProficiency();

        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!IsOpen || _station == null)
            return;

        RefreshProgressOnly();
        RefreshActionButton();
        RefreshActiveWorkButton();
    }

    public void Open(CookingStation station, CookingClick clickSource)
    {
        if (station == null)
            return;

        if (_root == null)
            BuildUi();

        if (_root == null || _progressText == null)
        {
            Debug.LogError("[CookingUI] Failed to build cooking interface.");
            return;
        }

        if (_station != null)
            _station.StateChanged -= RefreshOnStationChange;

        _station = station;
        _clickSource = clickSource;
        _station.StateChanged += RefreshOnStationChange;

        CacheRefs();
        HideFishPicker();
        HideHelpPanel();
        HideProficiencyPanel();
        SubscribeProficiency();
        _root.gameObject.SetActive(true);
        Refresh();

        if (_station.ReadyCookedAmount > 0 && !_station.IsCooking)
            LogCookingBlocked("Collect cooked food before cooking again.");
    }

    public void Close()
    {
        HideFishPicker();
        HideHelpPanel();
        HideProficiencyPanel();
        HideEnhancementSlotTooltip();
        UnsubscribeProficiency();
        if (_station != null && SaveManager.Instance != null)
            SaveManager.Instance.RequestSave(SaveManager.SaveRequestKind.InventoryChanged);

        HideImmediate();
        _clickSource?.NotifyClosed();
        _clickSource = null;

        if (_station != null)
        {
            _station.StateChanged -= RefreshOnStationChange;
            _station = null;
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
        RefreshCookingLevelButton();
        RefreshCookingXpBar();
        RefreshBurnChanceText();
    }

    private void RefreshOnStationChange()
    {
        if (_station == null)
            return;

        CacheRefs();
        RefreshEnhancementSlotVisuals();
        RefreshSlotVisuals();
        RefreshProgressOnly();
        RefreshTimeSummary();
        RefreshActionButton();
        RefreshFishClearButton();
        RefreshEnhancementClearButton();
        RefreshActiveWorkButton();
    }

    private void RefreshEnhancementClearButton()
    {
        if (_enhancementClearButton == null)
            return;

        bool hasEnhancement = _station != null && _station.StoredEnhancementAmount > 0;
        _enhancementClearButton.gameObject.SetActive(hasEnhancement);
        _enhancementClearButton.interactable = hasEnhancement;
    }

    private void RefreshEnhancementSlotVisuals()
    {
        if (_station == null)
            return;

        int amt = _station.StoredEnhancementAmount;
        string itemId = amt > 0 ? _station.StoredEnhancementItemId : "";
        ApplySlot(_enhancementIcon, _enhancementAmountText, itemId, amt, "", _itemDb);
    }

    private void RefreshFishClearButton()
    {
        if (_fishClearButton == null)
            return;

        bool hasFish = _station != null && _station.StoredRawAmount > 0;
        _fishClearButton.gameObject.SetActive(hasFish);
        _fishClearButton.interactable = hasFish;
    }

    private void RefreshSlotVisuals()
    {
        int fishAmt = _station.StoredRawAmount;
        string fishId = fishAmt > 0 ? _station.StoredRawItemId : "";
        ApplySlot(_fishIcon, _fishAmountText, fishId, fishAmt, "Fish", _itemDb);

        int cookedAmt = _station.ReadyCookedAmount;
        string cookedId = ResolveCookedSlotItemId(fishAmt, cookedAmt);
        ApplySlot(_cookedIcon, _cookedAmountText, cookedId, cookedAmt, "Cooked", _itemDb);
    }

    private string ResolveCookedSlotItemId(int fishAmt, int cookedAmt)
    {
        if (cookedAmt > 0)
        {
            if (!string.IsNullOrWhiteSpace(_station.ReadyCookedItemId))
                return _station.ReadyCookedItemId;

            if (_station.TryGetActiveRecipe(out CookingRecipe recipe))
                return recipe.CookedItemId;

            if (fishAmt > 0 && CookingRecipes.TryGetForRaw(_station.StoredRawItemId, out recipe))
                return recipe.CookedItemId;

            return "";
        }

        if (fishAmt > 0 || _station.IsCooking)
        {
            if (fishAmt > 0 && CookingRecipes.TryGetForRaw(_station.StoredRawItemId, out CookingRecipe recipe))
                return recipe.CookedItemId;

            if (_station.TryGetActiveRecipe(out CookingRecipe activeRecipe))
                return activeRecipe.CookedItemId;
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
        if (_station == null || _progressFill == null || _progressText == null)
            return;

        float duration = _station.GetActiveDurationSeconds();
        float progress = _station.IsCooking ? _station.CookProgressSeconds : 0f;
        float t = duration > 0f ? Mathf.Clamp01(progress / duration) : 0f;
        if (_progressFillRt != null)
            _progressFillRt.anchorMax = new Vector2(t, 1f);
        else if (_progressFill != null)
            _progressFill.fillAmount = t;

        int shownCurrent = Mathf.FloorToInt(progress);
        if (_station.IsCooking && duration < 60f)
            _progressText.text =
                $"{FormatPortionDurationSeconds(progress, useFloor: true)} / {FormatPortionDurationSeconds(duration)} seconds";
        else
            _progressText.text = $"{shownCurrent} / {FormatPortionDurationSeconds(duration)} seconds";
    }

    private static string FormatPortionDurationSeconds(float seconds, bool useFloor = false)
    {
        seconds = Mathf.Max(0f, seconds);
        if (seconds >= 60f)
            return FormatCookDuration(seconds).TrimEnd('s');

        if (useFloor)
            return Mathf.FloorToInt(seconds).ToString();

        if (Mathf.Approximately(seconds, Mathf.Round(seconds)))
            return Mathf.RoundToInt(seconds).ToString();

        return $"{seconds:0.##}";
    }

    private void RefreshTimeSummary()
    {
        if (_timeSummaryText == null || _station == null)
            return;

        if (!_station.TryGetCookTimeEstimate(
                out float totalRemaining,
                out _,
                out int portionsRemaining))
        {
            _timeSummaryText.text = "";
            return;
        }

        int fish = _station.StoredRawAmount;
        string totalLabel = FormatCookDuration(totalRemaining);
        string portionWord = portionsRemaining == 1 ? "portion" : "portions";

        _timeSummaryText.text =
            $"Cooking time: {fish} fish ({portionsRemaining} {portionWord}) - {totalLabel}";
    }

    private static string FormatCookDuration(float seconds)
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
        if (_actionButton == null || _actionButtonText == null || _station == null)
            return;

        if (_station.IsCooking)
        {
            _actionButtonText.text = "STOP";
            _actionButtonText.color = StopAccent;
            _actionButton.interactable = true;
        }
        else if (_station.ReadyCookedAmount > 0)
        {
            _actionButtonText.text = "COLLECT";
            _actionButtonText.color = Accent;
            _actionButton.interactable = true;
        }
        else
        {
            _actionButtonText.text = "COOK";
            bool canCook = _station.CanStartCooking();
            _actionButtonText.color = canCook ? Accent : StopAccent;
            _actionButton.interactable = true;
        }
    }

    private void OnActionClicked()
    {
        if (_station == null)
            return;

        if (_station.IsCooking)
        {
            _station.StopCooking();
            Refresh();
            return;
        }

        if (_station.ReadyCookedAmount > 0)
        {
            CollectCooked(_station.ReadyCookedAmount);
            return;
        }

        if (!_station.CanStartCooking())
        {
            if (_station.StoredRawAmount < _station.GetRawPerCooked())
            {
                int rawPerCooked = _station.GetRawPerCooked();
                LogCookingBlocked($"Requires at least {rawPerCooked} fish to cook one portion.");
            }

            Refresh();
            return;
        }

        _station.TryStartCooking();
        Refresh();
    }

    private void OnFishClearClicked()
    {
        RemoveAllFish();
    }

    private void OnEnhancementClearClicked()
    {
        if (_station == null)
            return;

        if (!_station.TryWithdrawAllEnhancement(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogCookingBlocked(reason);
            return;
        }

        Refresh();
    }

    private void OnEnhancementClicked()
    {
        OpenSidePicker(SidePickerMode.Enhancement);
    }

    private void OnFishClicked()
    {
        OpenSidePicker(SidePickerMode.Fish);
    }

    private void OpenSidePicker(SidePickerMode mode)
    {
        if (_fishPickerRoot == null)
            return;

        HideHelpPanel();

        if (_fishPickerRoot.gameObject.activeSelf && _pickerMode == mode)
        {
            HideFishPicker();
            return;
        }

        _pickerMode = mode;
        if (mode == SidePickerMode.Fish)
            RebuildFishPicker();
        else
            RebuildEnhancementPicker();

        _fishPickerRoot.gameObject.SetActive(true);
    }

    private void ShowEnhancementSlotTooltip()
    {
        if (_station == null || _station.StoredEnhancementAmount <= 0)
            return;

        CacheRefs();
        if (_sharedTooltip == null)
            return;

        ItemDefinition def = _itemDb != null ? _itemDb.Get(_station.StoredEnhancementItemId) : null;
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

        HideFishPicker();
        _helpPanelRoot.gameObject.SetActive(true);
    }

    private void OnCookingLevelClicked()
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
        if (_station == null)
            return;

        if (!_station.TryActiveWork(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogCookingBlocked(reason);
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

        RefreshCookingLevelButton();
        RefreshCookingXpBar();
        RefreshActiveWorkButton();
        if (_proficiencyPanelRoot != null && _proficiencyPanelRoot.gameObject.activeSelf)
            RebuildProficiencyPanel();
    }

    private void RefreshCookingLevelButton()
    {
        if (_cookingLevelButtonText == null)
            return;

        int level = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Cooking);
        _cookingLevelButtonText.text = $"< Cooking: Lv {level} >";
    }

    private void RefreshCookingXpBar()
    {
        if (_cookingXpFillRt == null)
            return;

        ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
        int level = runtime.GetLevel(ProcessingSkillType.Cooking);
        float t = runtime.GetProgress01(ProcessingSkillType.Cooking);
        _cookingXpFillRt.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
    }

    private void RefreshActiveWorkButton()
    {
        if (_activeWorkButton == null || _activeWorkButtonText == null)
            return;

        bool cooking = _station != null && _station.IsCooking;
        float cooldown = ProcessingProficiencyRuntime.EnsureInstance().GetActiveWorkCooldownRemaining();
        bool onCooldown = cooldown > 0.01f;

        _activeWorkButton.interactable = cooking && !onCooldown;
        if (!cooking)
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
        int level = runtime.GetLevel(ProcessingSkillType.Cooking);
        CookingProficiencyBonuses bonuses = runtime.GetCookingBonuses();

        CreateProficiencyLine($"Cooking — Lv {level}", Accent, 16f, FontStyles.Bold);

        if (level < ProcessingSkillCurves.MaxLevel)
        {
            int xp = runtime.GetXp(ProcessingSkillType.Cooking);
            int needed = runtime.GetXpToNextLevel(ProcessingSkillType.Cooking);
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

        IReadOnlyList<string> unlockLines = CookingProficiencyBonuses.BuildUnlockLines();
        for (int i = 0; i < CookingProficiencyBonuses.UnlockRows.Length; i++)
        {
            CookingProficiencyBonuses.UnlockRow row = CookingProficiencyBonuses.UnlockRows[i];
            bool unlocked = level >= row.Level;
            Color color = unlocked ? Accent : new Color32(150, 150, 150, 255);
            CreateProficiencyLine(unlockLines[i], color, 13f, FontStyles.Normal);
        }

        float activeWorkSeconds = bonuses.ActiveWorkSecondsPerClick >= 1.99f ? 2 : 1;
        _proficiencyFooterText.text =
            "Total bonuses granted:\n" +
            $"{FormatPercent(bonuses.SpeedBonusPercent)} increased cooking speed\n" +
            $"{FormatPercent(bonuses.BurnRateReductionPercent)} reduced burn chance\n" +
            $"Burn chance: {FormatPercent(bonuses.EffectiveBurnChancePercent)}\n" +
            $"Speed up -> {activeWorkSeconds}s, 3s cooldown";
    }

    private void RefreshBurnChanceText()
    {
        if (_burnChanceText == null || _station == null)
            return;

        _burnChanceText.text = $"Burn chance: {FormatPercent(_station.GetEffectiveBurnChancePercent())}";
    }

    private void CreateProficiencyLine(string text, Color color, float fontSize, FontStyles style)
    {
        var row = CreateUiObject("Line", _proficiencyScrollContent, typeof(RectTransform), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = fontSize + 10f;
        var tmp = CreateTmpText("Text", row.transform, fontSize, color, TextAlignmentOptions.TopLeft);
        StretchFull(tmp.rectTransform);
        tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.text = text;
    }

    private static string FormatPercent(float value) => $"{value:0.#}%";

    private void HideFishPicker()
    {
        if (_fishPickerRoot != null)
            _fishPickerRoot.gameObject.SetActive(false);
    }

    private void RebuildEnhancementPicker()
    {
        if (_fishPickerScrollContent == null)
            return;

        CacheRefs();
        for (int i = _fishPickerScrollContent.childCount - 1; i >= 0; i--)
            Destroy(_fishPickerScrollContent.GetChild(i).gameObject);

        bool any = false;
        if (_itemDb != null)
        {
            IReadOnlyList<ItemDefinition> items = _itemDb.GetAll();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition def = items[i];
                if (def == null || !def.IsProcessingSkillEnhancement)
                    continue;
                if (def.ProcessingSkillTarget != ProcessingSkillTarget.Cooking)
                    continue;

                int count = _inventory != null ? _inventory.GetTotalAmount(def.itemId) : 0;
                if (count <= 0)
                    continue;

                any = true;
                CreateEnhancementPickerRow(def, count);
            }
        }

        if (!any)
            CreateFishPickerMessage("No cooking enhancements in inventory.");
    }

    private void CreateEnhancementPickerRow(ItemDefinition def, int playerCount)
    {
        string label = def != null ? def.displayName : "";
        string itemId = def != null ? def.itemId : "";

        var row = CreateUiObject("EnhancementRow", _fishPickerScrollContent, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
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
        if (_station == null)
            return;

        if (!_station.TryDepositEnhancement(itemId, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Cooking] {reason}");
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void DepositAllEnhancementFromPicker(string itemId)
    {
        if (_station == null)
            return;

        if (!_station.TryDepositAllEnhancementFromInventory(itemId, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Cooking] {reason}");
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void RebuildFishPicker()
    {
        if (_fishPickerScrollContent == null)
            return;

        CacheRefs();
        for (int i = _fishPickerScrollContent.childCount - 1; i >= 0; i--)
            Destroy(_fishPickerScrollContent.GetChild(i).gameObject);

        IReadOnlyList<CookingRecipe> recipes = CookingRecipes.All;
        bool any = false;
        for (int i = 0; i < recipes.Count; i++)
        {
            CookingRecipe recipe = recipes[i];
            int count = _inventory != null ? _inventory.GetTotalAmount(recipe.RawItemId) : 0;
            if (count <= 0)
                continue;

            any = true;
            CreateFishPickerRow(recipe, count);
        }

        if (!any)
            CreateFishPickerMessage("No cookable fish in inventory.");
    }

    private void CreateFishPickerMessage(string message)
    {
        var row = CreateUiObject("Msg", _fishPickerScrollContent, typeof(RectTransform), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = 28f;
        var text = CreateTmpText("Label", row.transform, 14f, TextLight, TextAlignmentOptions.MidlineLeft);
        StretchFull(text.rectTransform);
        text.text = message;
    }

    private void CreateFishPickerRow(CookingRecipe recipe, int playerCount)
    {
        ItemDefinition def = _itemDb != null ? _itemDb.Get(recipe.RawItemId) : null;
        string label = def != null ? def.displayName : recipe.RawItemId;
        string fishId = recipe.RawItemId;

        var row = CreateUiObject("FishRow", _fishPickerScrollContent, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
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

        CreateCompactPickerButton(row.transform, "x5", 42f, () => DepositFishFromPicker(fishId, 5));
        CreateCompactPickerButton(row.transform, "xAll", 48f, () => DepositAllFishFromPicker(fishId));
    }

    private void DepositFishFromPicker(string fishId, int amount)
    {
        if (_station == null)
            return;

        if (!_station.TryDepositRaw(fishId, amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Cooking] {reason}");
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void DepositAllFishFromPicker(string fishId)
    {
        if (_station == null)
            return;

        if (!_station.TryDepositAllRawFromInventory(fishId, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Cooking] {reason}");
            return;
        }

        RefreshPickerAfterDeposit();
    }

    private void RefreshPickerAfterDeposit()
    {
        Refresh();
        if (_fishPickerRoot != null && _fishPickerRoot.gameObject.activeSelf)
        {
            if (_pickerMode == SidePickerMode.Enhancement)
                RebuildEnhancementPicker();
            else
                RebuildFishPicker();
        }
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

    private void OnFishRightClicked(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointer && pointer.button != PointerEventData.InputButton.Right)
            return;
        if (_station == null)
            return;

        CacheRefs();
        string fishId = _station.StoredRawItemId;
        int stored = _station.StoredRawAmount;
        var entries = new List<ContextMenuEntry>();

        if (stored > 0)
        {
            string fishName = GetItemDisplayName(fishId, "Fish");
            int invCount = _inventory != null ? _inventory.GetTotalAmount(fishId) : 0;
            if (invCount > 0)
            {
                entries.Add(new ContextMenuEntry(
                    $"Add all {invCount} {fishName}",
                    () => AddAllFish(fishId)));
            }

            entries.Add(new ContextMenuEntry(
                $"Remove all {stored} {fishName}",
                () => RemoveAllFish()));
        }
        else
        {
            IReadOnlyList<CookingRecipe> recipes = CookingRecipes.All;
            for (int i = 0; i < recipes.Count; i++)
            {
                CookingRecipe recipe = recipes[i];
                int invCount = _inventory != null ? _inventory.GetTotalAmount(recipe.RawItemId) : 0;
                if (invCount <= 0)
                    continue;

                string fishName = GetItemDisplayName(recipe.RawItemId, "Fish");
                string capturedFishId = recipe.RawItemId;
                entries.Add(new ContextMenuEntry(
                    $"Add all {invCount} {fishName}",
                    () => AddAllFish(capturedFishId)));
            }
        }

        if (entries.Count == 0)
            return;

        string menuTitle = stored > 0 ? GetItemDisplayName(fishId, "Fish") : "Fish";
        ContextMenuUI.EnsureInstance().ShowAtScreen(entries, Input.mousePosition, menuTitle);
    }

    private void AddAllFish(string fishId)
    {
        if (_station == null)
            return;

        if (!_station.TryDepositAllRawFromInventory(fishId, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[Cooking] {reason}");
            return;
        }

        Refresh();
    }

    private void RemoveAllFish()
    {
        if (_station == null)
            return;

        if (!_station.TryWithdrawAllFish(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason) && reason != "No fish stored.")
                LogCookingBlocked(reason);
            return;
        }

        Refresh();
    }

    private void OnCookedRightClicked(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointer && pointer.button != PointerEventData.InputButton.Right)
            return;
        if (_station == null || _station.ReadyCookedAmount <= 0)
            return;

        CacheRefs();
        string cookedId = ResolveCookedItemId();
        string barName = GetItemDisplayName(cookedId, "Cooked");
        int ready = _station.ReadyCookedAmount;

        var entries = new List<ContextMenuEntry>
        {
            new($"Collect 1 {barName}", () => CollectCooked(1), ready < 1),
            new($"Collect all {ready} {barName}", () => CollectCooked(ready), ready <= 0)
        };

        ContextMenuUI.EnsureInstance().ShowAtScreen(entries, Input.mousePosition, barName);
    }

    private string ResolveCookedItemId()
    {
        if (_station == null)
            return "";

        if (!string.IsNullOrWhiteSpace(_station.ReadyCookedItemId))
            return _station.ReadyCookedItemId;

        if (_station.TryGetActiveRecipe(out CookingRecipe recipe))
            return recipe.CookedItemId;

        if (CookingRecipes.TryGetForRaw(_station.StoredRawItemId, out recipe))
            return recipe.CookedItemId;

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

    private void CollectCooked(int amount)
    {
        if (_station == null)
            return;

        if (!_station.TryCollectCooked(amount, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                LogCookingBlocked(reason);
            return;
        }

        Refresh();
    }

    private void LogCookingBlocked(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (Time.unscaledTime < _nextPendingCookedLogTime)
            return;

        _nextPendingCookedLogTime = Time.unscaledTime + CookingBlockedLogCooldownSeconds;
        GameLog.Add(message, GameLog.CannotMessageColor);
    }

    private void BuildUi()
    {
        if (_root != null && _progressText != null && _timeSummaryText != null && _fishClearButton != null &&
            _cookingLevelButton != null && _helpButton != null && _activeWorkButton != null &&
            _cookingXpFillRt != null && _fishPickerScrollContent != null && _showBurnLogsToggle != null &&
            _enhancementIcon != null && _builtUiLayoutVersion == UiLayoutVersion)
            return;

        if (_root != null)
        {
            Destroy(_root.gameObject);
            _root = null;
            _fishPickerRoot = null;
            _fishPickerScrollContent = null;
            _helpPanelRoot = null;
            _proficiencyPanelRoot = null;
            _proficiencyScrollContent = null;
            _proficiencyFooterText = null;
            _progressText = null;
            _timeSummaryText = null;
            _progressFill = null;
            _progressFillRt = null;
            _actionButton = null;
            _actionButtonText = null;
            _fishButton = null;
            _cookedButton = null;
            _fishClearButton = null;
            _helpButton = null;
            _cookingLevelButton = null;
            _cookingLevelButtonText = null;
            _cookingXpFill = null;
            _cookingXpFillRt = null;
            _activeWorkButton = null;
            _activeWorkButtonText = null;
            _showBurnLogsToggle = null;
            _enhancementButton = null;
            _enhancementClearButton = null;
            _enhancementIcon = null;
            _enhancementAmountText = null;
            _fishIcon = null;
            _cookedIcon = null;
            _fishAmountText = null;
            _cookedAmountText = null;
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

        _root = CreatePanel("CookingPanel", transform, MainPanelSize);
        CreateHeader(_root, "Cooking Range");

        _helpButton = CreateButton(_root, "HelpButton", "?", new Vector2(28f, 28f), new Vector2(0f, 1f));
        var helpRt = _helpButton.GetComponent<RectTransform>();
        helpRt.pivot = new Vector2(0f, 1f);
        helpRt.anchoredPosition = new Vector2(12f, -12f);
        _helpButton.onClick.AddListener(OnHelpClicked);

        _cookingLevelButton = CreateButton(_root, "CookingLevelButton", "< Cooking: Lv 1 >", new Vector2(220f, 24f), new Vector2(0.5f, 1f));
        var smeltBtnRt = _cookingLevelButton.GetComponent<RectTransform>();
        smeltBtnRt.pivot = new Vector2(0.5f, 1f);
        smeltBtnRt.anchoredPosition = new Vector2(0f, -42f);
        _cookingLevelButtonText = _cookingLevelButton.GetComponentInChildren<TMP_Text>();
        _cookingLevelButtonText.fontSize = 13f;
        _cookingLevelButton.onClick.AddListener(OnCookingLevelClicked);

        BuildCookingXpBar(_root);

        var processingBlock = CreateUiObject("ProcessingBlock", _root, typeof(RectTransform), typeof(VerticalLayoutGroup));
        var processingRt = processingBlock.GetComponent<RectTransform>();
        processingRt.anchorMin = new Vector2(0.5f, 0.57f);
        processingRt.anchorMax = new Vector2(0.5f, 0.57f);
        processingRt.pivot = new Vector2(0.5f, 0.5f);
        processingRt.sizeDelta = new Vector2(300f, 176f);
        var processingLayout = processingBlock.GetComponent<VerticalLayoutGroup>();
        processingLayout.spacing = 8f;
        processingLayout.childAlignment = TextAnchor.MiddleCenter;
        processingLayout.childControlWidth = false;
        processingLayout.childControlHeight = false;

        _enhancementButton = CreateSlotButton(processingBlock.transform, "", out _enhancementIcon, out _enhancementAmountText, OnEnhancementClicked);
        var enhancementRt = _enhancementButton.GetComponent<RectTransform>();
        enhancementRt.sizeDelta = new Vector2(64f, 64f);
        var enhancementIconRt = _enhancementIcon.rectTransform;
        enhancementIconRt.sizeDelta = new Vector2(40f, 40f);
        var enhancementTrigger = _enhancementButton.gameObject.AddComponent<CookingEnhancementSlotInteractions>();
        enhancementTrigger.Initialize(this);
        var enhancementHover = _enhancementButton.gameObject.AddComponent<CookingEnhancementSlotHover>();
        enhancementHover.Initialize(this);

        _enhancementClearButton = CreateButton(_enhancementButton.transform, "EnhancementClear", "×", new Vector2(22f, 22f), new Vector2(1f, 1f));
        var enhancementClearRt = _enhancementClearButton.GetComponent<RectTransform>();
        enhancementClearRt.anchoredPosition = new Vector2(-4f, -4f);
        var enhancementClearClick = _enhancementClearButton.gameObject.AddComponent<CookingSlotClearClick>();
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

        _fishButton = CreateSlotButton(slotsRow.transform, "Fish", out _fishIcon, out _fishAmountText, OnFishClicked);
        var oresTrigger = _fishButton.gameObject.AddComponent<CookingFishSlotInteractions>();
        oresTrigger.Initialize(this);

        _fishClearButton = CreateButton(_fishButton.transform, "OreClear", "×", new Vector2(22f, 22f), new Vector2(1f, 1f));
        var clearRt = _fishClearButton.GetComponent<RectTransform>();
        clearRt.anchoredPosition = new Vector2(-4f, -4f);
        _fishClearButton.onClick.AddListener(OnFishClearClicked);
        _fishClearButton.transform.SetAsLastSibling();
        _fishClearButton.gameObject.SetActive(true);

        TextMeshProUGUI arrowText = CreateTmpText("Arrow", slotsRow.transform, 28f, Accent, TextAlignmentOptions.Center);
        arrowText.text = "→";
        arrowText.rectTransform.sizeDelta = new Vector2(28f, 88f);

        _cookedButton = CreateSlotButton(slotsRow.transform, "Cooked", out _cookedIcon, out _cookedAmountText, null);
        var barsTrigger = _cookedButton.gameObject.AddComponent<CookingFoodContextTrigger>();
        barsTrigger.Initialize(this);

        _activeWorkButton = CreateButton(_cookedButton.transform, "ActiveWorkButton", "Speed Up", new Vector2(76f, 18f), new Vector2(0.5f, 1f));
        var activeRt = _activeWorkButton.GetComponent<RectTransform>();
        activeRt.pivot = new Vector2(0.5f, 0f);
        activeRt.anchoredPosition = new Vector2(0f, 6f);
        _activeWorkButtonText = _activeWorkButton.GetComponentInChildren<TMP_Text>();
        _activeWorkButtonText.fontSize = 10f;
        _activeWorkButton.onClick.AddListener(OnActiveWorkClicked);
        _activeWorkButton.transform.SetAsLastSibling();

        _burnChanceText = CreateTmpText("BurnChance", _root, 11f, StopAccent, TextAlignmentOptions.Center);
        var burnRt = _burnChanceText.rectTransform;
        burnRt.anchorMin = new Vector2(0.5f, 0.375f);
        burnRt.anchorMax = new Vector2(0.5f, 0.375f);
        burnRt.pivot = new Vector2(0.5f, 0.5f);
        burnRt.sizeDelta = new Vector2(320f, 18f);
        _burnChanceText.text = "Burn chance: 50%";

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

        _actionButton = CreateButton(_root, "ActionButton", "START", new Vector2(200f, 40f), new Vector2(0.5f, 0.1f));
        _actionButtonText = _actionButton.GetComponentInChildren<TMP_Text>();
        _actionButton.onClick.AddListener(OnActionClicked);

        BuildShowBurnLogsToggle(_root);

        var closeBtn = CreateButton(_root, "CloseButton", "X", new Vector2(32f, 32f), new Vector2(1f, 1f));
        closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-12f, -12f);
        closeBtn.onClick.AddListener(Close);

        _helpPanelRoot = CreateSidePanel("HelpPanel", _root, HelpPanelSize, leftSide: true);
        BuildHelpPanelContent(_helpPanelRoot);
        _helpPanelRoot.gameObject.SetActive(false);

        _fishPickerRoot = CreateSidePanel("FishPicker", _root, SidePickerPanelSize, leftSide: true);
        _fishPickerScrollContent = BuildScrollListPanel(_fishPickerRoot, preferredHeight: 200f);
        _fishPickerRoot.gameObject.SetActive(false);

        _proficiencyPanelRoot = CreateSidePanel("ProficiencyPanel", _root, new Vector2(300f, 320f), leftSide: false);
        BuildProficiencyPanelContent(_proficiencyPanelRoot);
        _proficiencyPanelRoot.gameObject.SetActive(false);

        RefreshCookingLevelButton();
        RefreshCookingXpBar();
        RefreshActiveWorkButton();
        _builtUiLayoutVersion = UiLayoutVersion;
    }

    private void BuildCookingXpBar(RectTransform parent)
    {
        var xpBg = CreateUiObject("CookingXpBg", parent, typeof(RectTransform), typeof(Image));
        var xpBgRt = xpBg.GetComponent<RectTransform>();
        xpBgRt.anchorMin = new Vector2(0.5f, 1f);
        xpBgRt.anchorMax = new Vector2(0.5f, 1f);
        xpBgRt.pivot = new Vector2(0.5f, 1f);
        xpBgRt.anchoredPosition = new Vector2(0f, -68f);
        xpBgRt.sizeDelta = new Vector2(240f, 8f);
        xpBg.GetComponent<Image>().color = ProgressBg;
        xpBg.GetComponent<Image>().raycastTarget = false;

        var xpFillGo = CreateUiObject("Fill", xpBg.transform, typeof(RectTransform), typeof(Image));
        _cookingXpFillRt = xpFillGo.GetComponent<RectTransform>();
        _cookingXpFillRt.anchorMin = Vector2.zero;
        _cookingXpFillRt.anchorMax = Vector2.zero;
        _cookingXpFillRt.offsetMin = Vector2.zero;
        _cookingXpFillRt.offsetMax = Vector2.zero;
        _cookingXpFill = xpFillGo.GetComponent<Image>();
        _cookingXpFill.color = Accent;
        _cookingXpFill.raycastTarget = false;
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
        headerText.text = "Cooking";

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
        scroll.scrollSensitivity = 20f;
    }

    private void BuildShowBurnLogsToggle(RectTransform parent)
    {
        var row = CreateUiObject("ShowLogsRow", parent, typeof(RectTransform));
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(1f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.pivot = new Vector2(1f, 0f);
        rowRt.anchoredPosition = new Vector2(-8f, 4f);
        rowRt.sizeDelta = new Vector2(108f, 18f);

        var label = CreateTmpText("Label", row.transform, 9f, TextLight, TextAlignmentOptions.MidlineRight);
        var labelRt = label.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = new Vector2(-20f, 0f);
        label.text = "Show logs:";

        var toggleGo = CreateUiObject("Toggle", row.transform, typeof(RectTransform), typeof(Toggle), typeof(Image));
        var toggleRt = toggleGo.GetComponent<RectTransform>();
        toggleRt.anchorMin = new Vector2(1f, 0.5f);
        toggleRt.anchorMax = new Vector2(1f, 0.5f);
        toggleRt.pivot = new Vector2(1f, 0.5f);
        toggleRt.anchoredPosition = Vector2.zero;
        toggleRt.sizeDelta = new Vector2(16f, 16f);
        toggleGo.GetComponent<Image>().color = SlotBg;

        var checkGo = CreateUiObject("Check", toggleGo.transform, typeof(RectTransform), typeof(Image));
        var checkRt = checkGo.GetComponent<RectTransform>();
        StretchFull(checkRt);
        checkRt.offsetMin = new Vector2(3f, 3f);
        checkRt.offsetMax = new Vector2(-3f, -3f);
        var checkImage = checkGo.GetComponent<Image>();
        checkImage.color = Accent;

        _showBurnLogsToggle = toggleGo.GetComponent<Toggle>();
        _showBurnLogsToggle.targetGraphic = toggleGo.GetComponent<Image>();
        _showBurnLogsToggle.graphic = checkImage;
        _showBurnLogsToggle.isOn = ShouldLogBurnMessages;
        _showBurnLogsToggle.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetInt(ShowBurnLogsPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        });
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

    private sealed class CookingSlotClearClick : MonoBehaviour, IPointerClickHandler
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

    private sealed class CookingEnhancementSlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CookingUI _owner;

        public void Initialize(CookingUI owner) => _owner = owner;

        public void OnPointerEnter(PointerEventData eventData) => _owner?.ShowEnhancementSlotTooltip();

        public void OnPointerExit(PointerEventData eventData) => _owner?.HideEnhancementSlotTooltip();
    }

    private sealed class CookingEnhancementSlotInteractions : MonoBehaviour, IDropHandler
    {
        private CookingUI _owner;

        public void Initialize(CookingUI owner) => _owner = owner;

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
            if (CookingUI.TryDepositEnhancementFromInventorySlot(inv, fromSlot, amount))
                InventoryDragState.EndDrag();
        }
    }

    private sealed class CookingFishSlotInteractions : MonoBehaviour, IPointerClickHandler, IDropHandler
    {
        private CookingUI _owner;

        public void Initialize(CookingUI owner) => _owner = owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner == null)
                return;

            if (eventData.button == PointerEventData.InputButton.Right)
                _owner.OnFishRightClicked(eventData);
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
            if (CookingUI.TryDepositFromInventorySlot(inv, fromSlot, amount))
                InventoryDragState.EndDrag();
        }
    }

    private sealed class CookingFoodContextTrigger : MonoBehaviour, IPointerClickHandler
    {
        private CookingUI _owner;

        public void Initialize(CookingUI owner) => _owner = owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner == null)
                return;

            if (eventData.button == PointerEventData.InputButton.Right)
                _owner.OnCookedRightClicked(eventData);
        }
    }
}