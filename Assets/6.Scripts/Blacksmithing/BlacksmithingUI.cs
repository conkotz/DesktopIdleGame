using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BlacksmithingUI : MonoBehaviour
{
    private static BlacksmithingUI _instance;

    private static readonly Color PanelBg = new Color32(28, 30, 34, 245);
    private static readonly Color SlotBg = new Color32(42, 44, 50, 255);
    private static readonly Color Accent = new Color32(120, 200, 110, 255);
    private static readonly Color TextLight = new Color32(220, 214, 198, 255);
    private static readonly Color ProgressBg = new Color32(100, 100, 100, 255);
    private static readonly Color ProgressFill = new Color32(90, 185, 80, 255);
    private static readonly Color PickerRowBg = new Color32(52, 54, 60, 255);
    private static readonly Color PickerRowSelectedBg = new Color32(62, 72, 58, 255);
    private static readonly Color StopAccent = new Color32(220, 80, 80, 255);

    private BlacksmithingStation _station;
    private BlacksmithingClick _clickSource;
    private Inventory _inventory;
    private ItemDatabase _itemDb;

    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _recipePickerRoot;
    private RectTransform _recipePickerScrollContent;
    private Image _recipeIcon;
    private Image _outputIcon;
    private TMP_Text _recipeText;
    private TMP_Text _outputAmountText;
    private Image _progressFill;
    private RectTransform _progressFillRt;
    private TMP_Text _progressText;
    private TMP_Text _ingredientsSummaryText;
    private TMP_Text _actionButtonText;
    private Button _actionButton;
    private Button _recipeButton;
    private Button _outputButton;
    private Button _helpButton;
    private Button _blacksmithingLevelButton;
    private TMP_Text _blacksmithingLevelButtonText;
    private Image _blacksmithingXpFill;
    private RectTransform _blacksmithingXpFillRt;
    private Button _activeWorkButton;
    private TMP_Text _activeWorkButtonText;

    private const float ScrollSensitivity = 8f;
    private const int UiLayoutVersion = 1;
    private int _builtUiLayoutVersion;

    private RectTransform _helpPanelRoot;
    private RectTransform _proficiencyPanelRoot;
    private RectTransform _proficiencyScrollContent;
    private TMP_Text _proficiencyFooterText;

    private float _nextBlockedLogTime;
    private bool _proficiencySubscribed;
    private string _selectedPickerOutputId = "";

    private const string HelpBodyText =
        "The blacksmithing anvil crafts weapons and armor from bars and materials.\n\n" +
        "Select a recipe, press Forge, and wait for the item to finish. Collect it before starting another craft.\n\n" +
        "Higher Blacksmithing level unlocks better gear and reduces resource costs.\n\n" +
        "Speed Up trims time from the current craft (3s cooldown).";

    private const int DefaultCanvasSortingOrder = 12000;
    private const float SidePanelGap = 10f;
    private const float BlockedLogCooldownSeconds = 2f;
    private static readonly Vector2 MainPanelSize = new Vector2(360f, 420f);
    private static readonly Vector2 RecipePickerPanelSize = new Vector2(300f, 380f);
    private static readonly Vector2 HelpPanelSize = new Vector2(280f, 260f);

    public static BlacksmithingUI Instance => _instance;
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

    public static BlacksmithingStation ActiveStation => _instance != null ? _instance._station : null;

    public static BlacksmithingUI EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject("BlacksmithingUI", typeof(BlacksmithingUI));
        DontDestroyOnLoad(host);
        _instance = host.GetComponent<BlacksmithingUI>();
        _instance.BuildUi();
        return _instance;
    }

    public static void ForceClose()
    {
        if (_instance != null && IsOpen)
            _instance.Close();
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

    public void Open(BlacksmithingStation station, BlacksmithingClick clickSource)
    {
        if (station == null)
            return;

        if (_root == null)
            BuildUi();

        if (_root == null || _progressText == null)
        {
            Debug.LogError("[BlacksmithingUI] Failed to build blacksmithing interface.");
            return;
        }

        FurnaceClick.ForceClose();
        CookingClick.ForceClose();

        if (_station != null)
            _station.StateChanged -= RefreshOnStationChange;

        _station = station;
        _clickSource = clickSource;
        _station.StateChanged += RefreshOnStationChange;

        CacheRefs();
        HideHelpPanel();
        HideProficiencyPanel();
        SubscribeProficiency();
        RebuildRecipePicker();
        ShowRecipePicker();
        ProcessingSkillsWindowLayout.ApplyLayoutToOpenPanel(_root, _canvas);
        _root.gameObject.SetActive(true);
        Refresh();

        if (_station.HasReadyOutput && !_station.IsCrafting)
            LogBlocked("Collect the finished item before forging again.");
    }

    public void Close()
    {
        HideRecipePicker();
        HideHelpPanel();
        HideProficiencyPanel();
        UnsubscribeProficiency();
        if (_station != null && SaveManager.Instance != null)
            SaveManager.Instance.NotifyInventoryChangedDebounced();

        ProcessingSkillsWindowLayout.RecordSessionFromPanel(_root, _canvas);
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
    }

    private void Refresh()
    {
        RefreshOnStationChange();
        RefreshBlacksmithingLevelButton();
        RefreshBlacksmithingXpBar();
    }

    private void RefreshOnStationChange()
    {
        if (_station == null)
            return;

        CacheRefs();
        RefreshRecipeSlotVisuals();
        RefreshOutputSlotVisuals();
        RefreshProgressOnly();
        RefreshIngredientsSummary();
        RefreshActionButton();
        RefreshActiveWorkButton();
        RefreshRecipePickerSelection();
    }

    private void RefreshRecipeSlotVisuals()
    {
        if (_recipeText == null || _recipeIcon == null)
            return;

        if (_station.TryGetSelectedRecipe(out BlacksmithingRecipe recipe))
        {
            _recipeText.text = BuildIngredientSummary(recipe);
            _recipeText.fontSize = 9f;
            _recipeText.color = TextLight;
            _recipeText.textWrappingMode = TextWrappingModes.Normal;

            ItemDefinition def = _itemDb != null ? _itemDb.Get(recipe.OutputItemId) : null;
            if (def != null && def.icon != null)
            {
                _recipeIcon.sprite = def.icon;
                _recipeIcon.color = Color.white;
                _recipeIcon.enabled = true;
            }
            else
            {
                _recipeIcon.sprite = null;
                _recipeIcon.enabled = false;
            }
        }
        else
        {
            _recipeText.text = "Recipe";
            _recipeText.fontSize = 13f;
            _recipeText.color = Accent;
            _recipeIcon.sprite = null;
            _recipeIcon.enabled = false;
        }
    }

    private void RefreshOutputSlotVisuals()
    {
        if (_outputIcon == null || _outputAmountText == null)
            return;

        string itemId = ResolveOutputSlotItemId();
        int amount = _station.HasReadyOutput ? 1 : 0;
        ApplySlot(_outputIcon, _outputAmountText, itemId, amount, "Output", _itemDb);
    }

    private string ResolveOutputSlotItemId()
    {
        if (_station == null)
            return "";

        if (_station.HasReadyOutput)
            return _station.ReadyOutputItemId;

        if (_station.IsCrafting && _station.TryGetActiveRecipe(out BlacksmithingRecipe active))
            return active.OutputItemId;

        if (_station.TryGetSelectedRecipe(out BlacksmithingRecipe selected))
            return selected.OutputItemId;

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
        float progress = _station.IsCrafting ? _station.CraftProgressSeconds : 0f;
        float t = duration > 0f ? Mathf.Clamp01(progress / duration) : 0f;
        if (_progressFillRt != null)
            _progressFillRt.anchorMax = new Vector2(t, 1f);
        else
            _progressFill.fillAmount = t;

        int shownCurrent = Mathf.FloorToInt(progress);
        int shownTotal = Mathf.CeilToInt(duration);
        _progressText.text = $"{shownCurrent} / {shownTotal} seconds";
    }

    private void RefreshIngredientsSummary()
    {
        if (_ingredientsSummaryText == null || _station == null)
            return;

        if (_station.HasReadyOutput)
        {
            _ingredientsSummaryText.text = "Ready to collect.";
            _ingredientsSummaryText.color = Accent;
            return;
        }

        if (_station.IsCrafting)
        {
            if (_station.TryGetActiveRecipe(out BlacksmithingRecipe active))
            {
                _ingredientsSummaryText.text = $"Forging {GetItemDisplayName(active.OutputItemId, "item")}...";
                _ingredientsSummaryText.color = TextLight;
            }
            else
            {
                _ingredientsSummaryText.text = "";
            }

            return;
        }

        if (!_station.TryGetSelectedRecipe(out BlacksmithingRecipe recipe))
        {
            _ingredientsSummaryText.text = "Select a recipe from the list.";
            _ingredientsSummaryText.color = TextLight;
            return;
        }

        string needs = BuildIngredientSummary(recipe);
        bool hasIngredients = _station.PlayerHasIngredientsForSelected(out string failureReason);
        int playerLevel = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Blacksmithing);
        bool levelOk = playerLevel >= recipe.RequiredLevel;

        if (!levelOk)
        {
            _ingredientsSummaryText.text = $"Requires Blacksmithing level {recipe.RequiredLevel}.";
            _ingredientsSummaryText.color = StopAccent;
            return;
        }

        if (hasIngredients)
        {
            _ingredientsSummaryText.text = $"Needs: {needs}";
            _ingredientsSummaryText.color = Accent;
        }
        else
        {
            _ingredientsSummaryText.text = string.IsNullOrWhiteSpace(failureReason) ? $"Needs: {needs}" : failureReason;
            _ingredientsSummaryText.color = StopAccent;
        }
    }

    private void RefreshActionButton()
    {
        if (_actionButton == null || _actionButtonText == null || _station == null)
            return;

        if (_station.IsCrafting)
        {
            _actionButtonText.text = "STOP";
            _actionButtonText.color = StopAccent;
            _actionButton.interactable = true;
        }
        else if (_station.HasReadyOutput)
        {
            _actionButtonText.text = "COLLECT";
            _actionButtonText.color = Accent;
            _actionButton.interactable = true;
        }
        else
        {
            _actionButtonText.text = "FORGE";
            bool canForge = _station.CanStartCrafting(out _);
            _actionButtonText.color = canForge ? Accent : StopAccent;
            _actionButton.interactable = true;
        }
    }

    private void OnActionClicked()
    {
        if (_station == null)
            return;

        if (_station.IsCrafting)
        {
            _station.StopCrafting();
            Refresh();
            return;
        }

        if (_station.HasReadyOutput)
        {
            if (!_station.TryCollectOutput(out string reason))
            {
                if (!string.IsNullOrWhiteSpace(reason))
                    LogBlocked(reason);
            }

            Refresh();
            return;
        }

        if (!_station.TryStartCrafting(out string failureReason))
        {
            if (!string.IsNullOrWhiteSpace(failureReason))
                LogBlocked(failureReason);
            Refresh();
            return;
        }

        Refresh();
    }

    private void OnRecipeSlotClicked()
    {
        ToggleRecipePicker();
    }

    private void OnOutputClicked()
    {
        if (_station == null || !_station.HasReadyOutput)
            return;

        OnActionClicked();
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

        HideRecipePicker();
        _helpPanelRoot.gameObject.SetActive(true);
    }

    private void OnBlacksmithingLevelClicked()
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
                LogBlocked(reason);
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

    private void ShowRecipePicker()
    {
        if (_recipePickerRoot != null)
            _recipePickerRoot.gameObject.SetActive(true);
    }

    private void HideRecipePicker()
    {
        if (_recipePickerRoot != null)
            _recipePickerRoot.gameObject.SetActive(false);
    }

    private void ToggleRecipePicker()
    {
        if (_recipePickerRoot == null)
            return;

        HideHelpPanel();
        if (_recipePickerRoot.gameObject.activeSelf)
            HideRecipePicker();
        else
            ShowRecipePicker();
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

        RefreshBlacksmithingLevelButton();
        RefreshBlacksmithingXpBar();
        RefreshActiveWorkButton();
        RefreshIngredientsSummary();
        RefreshRecipeSlotVisuals();
        if (_proficiencyPanelRoot != null && _proficiencyPanelRoot.gameObject.activeSelf)
            RebuildProficiencyPanel();
        if (_recipePickerRoot != null && _recipePickerRoot.gameObject.activeSelf)
            RebuildRecipePicker();
    }

    private void RefreshBlacksmithingLevelButton()
    {
        if (_blacksmithingLevelButtonText == null)
            return;

        int level = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Blacksmithing);
        _blacksmithingLevelButtonText.text = $"< Blacksmithing: Lv {level} >";
    }

    private void RefreshBlacksmithingXpBar()
    {
        if (_blacksmithingXpFillRt == null)
            return;

        ProcessingProficiencyRuntime runtime = ProcessingProficiencyRuntime.EnsureInstance();
        float t = runtime.GetProgress01(ProcessingSkillType.Blacksmithing);
        _blacksmithingXpFillRt.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
    }

    private void RefreshActiveWorkButton()
    {
        if (_activeWorkButton == null || _activeWorkButtonText == null)
            return;

        bool crafting = _station != null && _station.IsCrafting;
        float cooldown = ProcessingProficiencyRuntime.EnsureInstance().GetActiveWorkCooldownRemaining();
        bool onCooldown = cooldown > 0.01f;

        _activeWorkButton.interactable = crafting && !onCooldown;
        if (!crafting)
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
        int level = runtime.GetLevel(ProcessingSkillType.Blacksmithing);
        BlacksmithingProficiencyBonuses bonuses = runtime.GetBlacksmithingBonuses();

        CreateProficiencyLine($"Blacksmithing — Lv {level}", Accent, 16f, FontStyles.Bold);

        if (level < ProcessingSkillCurves.MaxLevel)
        {
            int xp = runtime.GetXp(ProcessingSkillType.Blacksmithing);
            int needed = runtime.GetXpToNextLevel(ProcessingSkillType.Blacksmithing);
            CreateProficiencyLine($"Next level: {xp}/{needed} XP", TextLight, 12f, FontStyles.Normal);
        }
        else
        {
            CreateProficiencyLine("Max level reached", TextLight, 12f, FontStyles.Normal);
        }

        CreateProficiencyLine("", TextLight, 6f, FontStyles.Normal);

        List<ProcessingProficiencyUnlockLines.Row> unlockLines = BlacksmithingProficiencyBonuses.BuildDisplayUnlockRows();
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
            $"{FormatPercent(bonuses.SpeedBonusPercent)} increased forging speed\n" +
            $"{FormatPercent(bonuses.ResourceCostReductionPercent)} reduced resource costs\n" +
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

    private void RebuildRecipePicker()
    {
        if (_recipePickerScrollContent == null)
            return;

        CacheRefs();
        for (int i = _recipePickerScrollContent.childCount - 1; i >= 0; i--)
            Destroy(_recipePickerScrollContent.GetChild(i).gameObject);

        int playerLevel = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Blacksmithing);
        ReadOnlySpan<string> categories = BlacksmithingRecipes.CategoryOrder;
        bool anyRecipe = false;

        for (int c = 0; c < categories.Length; c++)
        {
            string category = categories[c];
            IReadOnlyList<BlacksmithingRecipe> recipes = BlacksmithingRecipes.GetRecipesForCategory(category);
            if (recipes.Count == 0)
                continue;

            CreateRecipeCategoryHeader(category);
            for (int i = 0; i < recipes.Count; i++)
            {
                BlacksmithingRecipe recipe = recipes[i];
                bool levelTooLow = playerLevel < recipe.RequiredLevel;
                bool selected = string.Equals(_station != null ? _station.SelectedRecipeOutputId : "", recipe.OutputItemId, StringComparison.OrdinalIgnoreCase);
                CreateRecipePickerRow(recipe, levelTooLow, selected);
                anyRecipe = true;
            }
        }

        if (!anyRecipe)
            CreateRecipePickerMessage("No recipes available.");
    }

    private void RefreshRecipePickerSelection()
    {
        if (_recipePickerScrollContent == null || _station == null)
            return;

        string selectedId = _station.SelectedRecipeOutputId ?? "";
        if (string.Equals(_selectedPickerOutputId, selectedId, StringComparison.OrdinalIgnoreCase))
            return;

        _selectedPickerOutputId = selectedId;
        RebuildRecipePicker();
    }

    private void CreateRecipeCategoryHeader(string category)
    {
        var row = CreateUiObject("Category", _recipePickerScrollContent, typeof(RectTransform), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = 22f;
        var text = CreateTmpText("Label", row.transform, 13f, Accent, TextAlignmentOptions.MidlineLeft);
        StretchFull(text.rectTransform);
        text.fontStyle = FontStyles.Bold;
        text.text = category;
    }

    private void CreateRecipePickerMessage(string message)
    {
        var row = CreateUiObject("Msg", _recipePickerScrollContent, typeof(RectTransform), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = 28f;
        var text = CreateTmpText("Label", row.transform, 14f, TextLight, TextAlignmentOptions.MidlineLeft);
        StretchFull(text.rectTransform);
        text.text = message;
    }

    private void CreateRecipePickerRow(BlacksmithingRecipe recipe, bool levelTooLow, bool selected)
    {
        string outputId = recipe.OutputItemId;
        string outputName = GetItemDisplayName(outputId, outputId);
        string ingredients = BuildIngredientSummary(recipe);

        var row = CreateUiObject("RecipeRow", _recipePickerScrollContent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var rowLe = row.GetComponent<LayoutElement>();
        rowLe.preferredHeight = 54f;
        rowLe.minHeight = 54f;
        row.GetComponent<Image>().color = selected ? PickerRowSelectedBg : PickerRowBg;

        var button = row.GetComponent<Button>();
        button.onClick.AddListener(() => OnRecipeRowClicked(outputId));

        var text = CreateTmpText("Label", row.transform, 12f, levelTooLow ? StopAccent : TextLight, TextAlignmentOptions.TopLeft);
        var textRt = text.rectTransform;
        StretchFull(textRt);
        textRt.offsetMin = new Vector2(8f, 4f);
        textRt.offsetMax = new Vector2(-8f, -4f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.text = $"{outputName}\nLv {recipe.RequiredLevel} — {ingredients}";
    }

    private void OnRecipeRowClicked(string outputItemId)
    {
        if (_station == null)
            return;

        _station.SelectRecipe(outputItemId);
        Refresh();
    }

    private string BuildIngredientSummary(BlacksmithingRecipe recipe)
    {
        float costReduction = ProcessingProficiencyRuntime.EnsureInstance().GetBlacksmithingBonuses().ResourceCostReductionPercent;
        var parts = new List<string>(recipe.Ingredients.Length);
        for (int i = 0; i < recipe.Ingredients.Length; i++)
        {
            BlacksmithingIngredient ing = recipe.Ingredients[i];
            int needed = BlacksmithingRecipes.GetEffectiveIngredientAmount(ing.Amount, costReduction);
            parts.Add($"{needed} {GetItemDisplayName(ing.ItemId, ing.ItemId)}");
        }

        return string.Join(", ", parts);
    }

    private string GetItemDisplayName(string itemId, string fallback)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return fallback;

        CacheRefs();
        ItemDefinition def = _itemDb != null ? _itemDb.Get(itemId) : null;
        return def != null && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : itemId;
    }

    private void LogBlocked(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (Time.unscaledTime < _nextBlockedLogTime)
            return;

        _nextBlockedLogTime = Time.unscaledTime + BlockedLogCooldownSeconds;
        GameLog.Add(message, GameLog.CannotMessageColor);
    }

    private void BuildUi()
    {
        if (_root != null && _progressText != null && _ingredientsSummaryText != null &&
            _blacksmithingLevelButton != null && _helpButton != null && _activeWorkButton != null &&
            _blacksmithingXpFillRt != null && _recipePickerScrollContent != null &&
            _builtUiLayoutVersion == UiLayoutVersion)
            return;

        if (_root != null)
        {
            Destroy(_root.gameObject);
            _root = null;
            _recipePickerRoot = null;
            _recipePickerScrollContent = null;
            _helpPanelRoot = null;
            _proficiencyPanelRoot = null;
            _proficiencyScrollContent = null;
            _proficiencyFooterText = null;
            _progressText = null;
            _ingredientsSummaryText = null;
            _progressFill = null;
            _progressFillRt = null;
            _actionButton = null;
            _actionButtonText = null;
            _recipeButton = null;
            _outputButton = null;
            _helpButton = null;
            _blacksmithingLevelButton = null;
            _blacksmithingLevelButtonText = null;
            _blacksmithingXpFill = null;
            _blacksmithingXpFillRt = null;
            _activeWorkButton = null;
            _activeWorkButtonText = null;
            _recipeIcon = null;
            _outputIcon = null;
            _recipeText = null;
            _outputAmountText = null;
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

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        _root = CreatePanel("BlacksmithingPanel", transform, MainPanelSize);
        CreateHeader(_root, "Blacksmithing Anvil");

        _helpButton = CreateButton(_root, "HelpButton", "?", new Vector2(28f, 28f), new Vector2(0f, 1f));
        var helpRt = _helpButton.GetComponent<RectTransform>();
        helpRt.pivot = new Vector2(0f, 1f);
        helpRt.anchoredPosition = new Vector2(12f, -12f);
        _helpButton.onClick.AddListener(OnHelpClicked);

        _blacksmithingLevelButton = CreateButton(_root, "BlacksmithingLevelButton", "< Blacksmithing: Lv 1 >", new Vector2(240f, 24f), new Vector2(0.5f, 1f));
        var levelBtnRt = _blacksmithingLevelButton.GetComponent<RectTransform>();
        levelBtnRt.pivot = new Vector2(0.5f, 1f);
        levelBtnRt.anchoredPosition = new Vector2(0f, -42f);
        _blacksmithingLevelButtonText = _blacksmithingLevelButton.GetComponentInChildren<TMP_Text>();
        _blacksmithingLevelButtonText.fontSize = 13f;
        _blacksmithingLevelButton.onClick.AddListener(OnBlacksmithingLevelClicked);

        BuildBlacksmithingXpBar(_root);

        var processingBlock = CreateUiObject("ProcessingBlock", _root, typeof(RectTransform), typeof(VerticalLayoutGroup));
        var processingRt = processingBlock.GetComponent<RectTransform>();
        processingRt.anchorMin = new Vector2(0.5f, 0.58f);
        processingRt.anchorMax = new Vector2(0.5f, 0.58f);
        processingRt.pivot = new Vector2(0.5f, 0.5f);
        processingRt.sizeDelta = new Vector2(300f, 120f);
        var processingLayout = processingBlock.GetComponent<VerticalLayoutGroup>();
        processingLayout.spacing = 8f;
        processingLayout.childAlignment = TextAnchor.MiddleCenter;
        processingLayout.childControlWidth = false;
        processingLayout.childControlHeight = false;

        var slotsRow = CreateUiObject("SlotsRow", processingBlock.transform, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var slotsRt = slotsRow.GetComponent<RectTransform>();
        slotsRt.sizeDelta = new Vector2(300f, 88f);
        var hlg = slotsRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        _recipeButton = CreateSlotButton(slotsRow.transform, "Recipe", out _recipeIcon, out _recipeText, OnRecipeSlotClicked);
        var recipeIconRt = _recipeIcon.rectTransform;
        recipeIconRt.anchorMin = new Vector2(0.5f, 0.72f);
        recipeIconRt.anchorMax = new Vector2(0.5f, 0.72f);
        recipeIconRt.sizeDelta = new Vector2(28f, 28f);
        var recipeTextRt = _recipeText.rectTransform;
        recipeTextRt.anchorMin = new Vector2(0.5f, 0.18f);
        recipeTextRt.anchorMax = new Vector2(0.5f, 0.18f);
        recipeTextRt.sizeDelta = new Vector2(80f, 44f);
        _recipeText.fontSize = 9f;
        _recipeText.textWrappingMode = TextWrappingModes.Normal;

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

        _outputButton = CreateSlotButton(slotsRow.transform, "Output", out _outputIcon, out _outputAmountText, OnOutputClicked);

        var progressBg = CreateUiObject("ProgressBg", _root, typeof(RectTransform), typeof(Image));
        var progressBgRt = progressBg.GetComponent<RectTransform>();
        progressBgRt.anchorMin = new Vector2(0.5f, 0.34f);
        progressBgRt.anchorMax = new Vector2(0.5f, 0.34f);
        progressBgRt.pivot = new Vector2(0.5f, 0.5f);
        progressBgRt.sizeDelta = new Vector2(280f, 30f);
        progressBg.GetComponent<Image>().color = ProgressBg;
        progressBg.GetComponent<Image>().raycastTarget = false;

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

        _ingredientsSummaryText = CreateTmpText("IngredientsSummary", _root, 12f, TextLight, TextAlignmentOptions.Center);
        var summaryRt = _ingredientsSummaryText.rectTransform;
        summaryRt.anchorMin = new Vector2(0.5f, 0.24f);
        summaryRt.anchorMax = new Vector2(0.5f, 0.24f);
        summaryRt.pivot = new Vector2(0.5f, 0.5f);
        summaryRt.sizeDelta = new Vector2(320f, 48f);
        _ingredientsSummaryText.textWrappingMode = TextWrappingModes.Normal;
        _ingredientsSummaryText.text = "";

        _actionButton = CreateButton(_root, "ActionButton", "FORGE", new Vector2(200f, 40f), new Vector2(0.5f, 0.1f));
        _actionButtonText = _actionButton.GetComponentInChildren<TMP_Text>();
        _actionButton.onClick.AddListener(OnActionClicked);

        var closeBtn = CreateButton(_root, "CloseButton", "X", new Vector2(32f, 32f), new Vector2(1f, 1f));
        closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-12f, -12f);
        closeBtn.onClick.AddListener(Close);

        _recipePickerRoot = CreateSidePanel("RecipePicker", _root, RecipePickerPanelSize, leftSide: true);
        BuildRecipePickerPanelContent(_recipePickerRoot);
        _recipePickerRoot.gameObject.SetActive(false);

        _helpPanelRoot = CreateSidePanel("HelpPanel", _root, HelpPanelSize, leftSide: true);
        BuildHelpPanelContent(_helpPanelRoot);
        _helpPanelRoot.gameObject.SetActive(false);

        _proficiencyPanelRoot = CreateSidePanel("ProficiencyPanel", _root, new Vector2(300f, 320f), leftSide: false);
        BuildProficiencyPanelContent(_proficiencyPanelRoot);
        _proficiencyPanelRoot.gameObject.SetActive(false);

        RefreshBlacksmithingLevelButton();
        RefreshBlacksmithingXpBar();
        RefreshActiveWorkButton();
        ProcessingSkillsWindowLayout.EnsureProcessingDragBackdrop(_root, _canvas);
        _builtUiLayoutVersion = UiLayoutVersion;
    }

    private void BuildRecipePickerPanelContent(RectTransform panel)
    {
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var headerRow = CreateUiObject("Header", panel, typeof(RectTransform), typeof(LayoutElement));
        headerRow.GetComponent<LayoutElement>().preferredHeight = 24f;
        var headerText = CreateTmpText("Title", headerRow.transform, 16f, Accent, TextAlignmentOptions.MidlineLeft);
        StretchFull(headerText.rectTransform);
        headerText.fontStyle = FontStyles.Bold;
        headerText.text = "Recipes";

        _recipePickerScrollContent = BuildScrollListPanel(panel, preferredHeight: 320f);
    }

    private void BuildBlacksmithingXpBar(RectTransform parent)
    {
        var xpBg = CreateUiObject("BlacksmithingXpBg", parent, typeof(RectTransform), typeof(Image));
        var xpBgRt = xpBg.GetComponent<RectTransform>();
        xpBgRt.anchorMin = new Vector2(0.5f, 1f);
        xpBgRt.anchorMax = new Vector2(0.5f, 1f);
        xpBgRt.pivot = new Vector2(0.5f, 1f);
        xpBgRt.anchoredPosition = new Vector2(0f, -68f);
        xpBgRt.sizeDelta = new Vector2(240f, 8f);
        xpBg.GetComponent<Image>().color = ProgressBg;
        xpBg.GetComponent<Image>().raycastTarget = false;

        var xpFillGo = CreateUiObject("Fill", xpBg.transform, typeof(RectTransform), typeof(Image));
        _blacksmithingXpFillRt = xpFillGo.GetComponent<RectTransform>();
        _blacksmithingXpFillRt.anchorMin = Vector2.zero;
        _blacksmithingXpFillRt.anchorMax = Vector2.zero;
        _blacksmithingXpFillRt.offsetMin = Vector2.zero;
        _blacksmithingXpFillRt.offsetMax = Vector2.zero;
        _blacksmithingXpFill = xpFillGo.GetComponent<Image>();
        _blacksmithingXpFill.color = Accent;
        _blacksmithingXpFill.raycastTarget = false;
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
        headerText.text = "Blacksmithing";

        var scrollHost = CreateUiObject("ScrollHost", panel, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
        scrollHost.GetComponent<LayoutElement>().preferredHeight = 200f;
        scrollHost.GetComponent<LayoutElement>().flexibleHeight = 1f;
        scrollHost.GetComponent<Image>().color = new Color32(36, 38, 42, 255);

        var viewport = CreateUiObject("Viewport", scrollHost.transform, typeof(RectTransform), typeof(RectMask2D));
        StretchFull(viewport.GetComponent<RectTransform>());

        RectTransform content = CreatePanel("Content", viewport.transform, new Vector2(0f, 0f));
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

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        var go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        if (go.TryGetComponent(out RectTransform rt))
            rt.localScale = Vector3.one;
        return go;
    }
}
