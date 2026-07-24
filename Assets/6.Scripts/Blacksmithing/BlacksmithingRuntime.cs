using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent blacksmithing anvil processing — keeps ticking while the player is on other map nodes.
/// </summary>
[DisallowMultipleComponent]
public sealed class BlacksmithingRuntime : MonoBehaviour, ISaveable
{
    private static BlacksmithingRuntime _instance;

    private readonly Dictionary<string, BlacksmithingRow> _rows =
        new Dictionary<string, BlacksmithingRow>(StringComparer.OrdinalIgnoreCase);

    public static BlacksmithingRuntime Instance => _instance;

    public static BlacksmithingRuntime EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var existing = FindFirstObjectByType<BlacksmithingRuntime>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var host = new GameObject(nameof(BlacksmithingRuntime));
        _instance = host.AddComponent<BlacksmithingRuntime>();
        DontDestroyOnLoad(host);
        return _instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        bool structuralChange = false;

        foreach (KeyValuePair<string, BlacksmithingRow> kv in _rows)
        {
            BlacksmithingRow row = kv.Value;
            // Pending STOP refunds must keep ticking after IsCrafting is cleared, or leftovers
            // stay locked forever (and a later TryStartCrafting would discard them).
            if (row == null || (!row.IsCrafting && !row.HasPendingIngredientRefunds))
                continue;

            if (row.TickCrafting(Time.deltaTime))
                structuralChange = true;
        }

        if (structuralChange)
            RequestSaveDebounced();
    }

    public BlacksmithingRow GetOrCreateRow(string stationId)
    {
        EnsureInstance();
        string key = NormalizeStationId(stationId);
        if (!_rows.TryGetValue(key, out BlacksmithingRow row) || row == null)
        {
            row = new BlacksmithingRow(key);
            _rows[key] = row;
        }

        return row;
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        data.blacksmithingStations ??= new List<SaveData.BlacksmithingStationSave>();

        // Preserve snapshot-seeded blacksmithing rows when this DDOL runtime has never been hydrated.
        if (_rows.Count == 0)
            return;

        data.blacksmithingStations.Clear();

        foreach (KeyValuePair<string, BlacksmithingRow> kv in _rows)
            kv.Value?.WriteInto(data.blacksmithingStations);
    }

    public void LoadFrom(SaveData data)
    {
        // Update existing row objects in place so scene stations that already
        // bound/subscribed in Awake keep valid references after late save apply.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (data?.blacksmithingStations != null)
        {
            for (int i = 0; i < data.blacksmithingStations.Count; i++)
            {
                SaveData.BlacksmithingStationSave save = data.blacksmithingStations[i];
                if (save == null || string.IsNullOrWhiteSpace(save.stationId))
                    continue;

                string key = NormalizeStationId(save.stationId);
                if (!_rows.TryGetValue(key, out BlacksmithingRow row) || row == null)
                {
                    row = new BlacksmithingRow(key);
                    _rows[key] = row;
                }

                row.ReadFrom(save);
                seen.Add(key);
            }
        }

        foreach (KeyValuePair<string, BlacksmithingRow> kv in _rows)
        {
            if (kv.Value == null || seen.Contains(kv.Key))
                continue;

            kv.Value.ResetToEmpty();
        }
    }

    private static void RequestSaveDebounced()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.NotifyInventoryChangedDebounced();
    }

    private static string NormalizeStationId(string stationId) =>
        string.IsNullOrWhiteSpace(stationId) ? "blacksmithing_anvil" : stationId.Trim();

    public sealed class BlacksmithingRow
    {
        public event Action StateChanged;

        private readonly string _stationId;
        private string _selectedRecipeOutputId = "";
        private string _activeRecipeOutputId = "";
        private string _readyOutputItemId = "";
        private float _craftProgressSeconds;
        private bool _isCrafting;
        private float _lockedCraftDurationSeconds;
        private readonly List<BlacksmithingIngredient> _lockedConsumedIngredients = new();

        public BlacksmithingRow(string stationId) => _stationId = NormalizeStationId(stationId);

        public string StationId => _stationId;
        public string SelectedRecipeOutputId => _selectedRecipeOutputId ?? "";
        public string ActiveRecipeOutputId => _activeRecipeOutputId ?? "";
        public string ReadyOutputItemId => _readyOutputItemId ?? "";
        public bool HasReadyOutput => !string.IsNullOrWhiteSpace(_readyOutputItemId);
        public bool IsCrafting => _isCrafting;
        /// <summary>True when STOP left unrecovered ingredients that still need inventory/storage space.</summary>
        public bool HasPendingIngredientRefunds =>
            !_isCrafting && _lockedConsumedIngredients.Count > 0;
        public float CraftProgressSeconds => Mathf.Max(0f, _craftProgressSeconds);

        public bool TryGetSelectedRecipe(out BlacksmithingRecipe recipe) =>
            BlacksmithingRecipes.TryGetForOutput(_selectedRecipeOutputId, out recipe);

        public bool TryGetActiveRecipe(out BlacksmithingRecipe recipe) =>
            BlacksmithingRecipes.TryGetForOutput(_activeRecipeOutputId, out recipe);

        public void SelectRecipe(string outputItemId)
        {
            if (_isCrafting || HasReadyOutput)
                return;

            if (string.IsNullOrWhiteSpace(outputItemId) ||
                !BlacksmithingRecipes.TryGetForOutput(outputItemId, out _))
            {
                _selectedRecipeOutputId = "";
            }
            else
            {
                _selectedRecipeOutputId = outputItemId.Trim();
            }

            NotifyChanged();
        }

        public float GetEffectiveDurationSeconds()
        {
            if (!TryGetActiveRecipe(out BlacksmithingRecipe recipe) &&
                !TryGetSelectedRecipe(out recipe))
                return BlacksmithingRecipes.DefaultCraftSeconds;

            float speedBonus = ProcessingProficiencyRuntime.EnsureInstance().GetBlacksmithingBonuses().SpeedBonusPercent;
            float duration = recipe.CraftSeconds / (1f + speedBonus / 100f);
            return Mathf.Max(0.1f, duration);
        }

        public float GetActiveDurationSeconds()
        {
            if (_isCrafting && _lockedCraftDurationSeconds > 0f)
                return _lockedCraftDurationSeconds;
            return GetEffectiveDurationSeconds();
        }

        public bool CanStartCrafting(out string failureReason)
        {
            failureReason = null;
            if (_isCrafting)
            {
                failureReason = "Already forging.";
                return false;
            }

            if (HasReadyOutput)
            {
                failureReason = "Collect the finished item first.";
                return false;
            }

            // STOP may leave locked leftovers when bag/storage are full. Never allow a new
            // forge to Clear() those leftovers — that permanently destroys the refund.
            if (HasPendingIngredientRefunds)
            {
                int beforeAmount = 0;
                for (int i = 0; i < _lockedConsumedIngredients.Count; i++)
                    beforeAmount += Mathf.Max(0, _lockedConsumedIngredients[i].Amount);

                TryFlushPendingIngredientRefunds(logIfBlocked: false);

                int afterAmount = 0;
                for (int i = 0; i < _lockedConsumedIngredients.Count; i++)
                    afterAmount += Mathf.Max(0, _lockedConsumedIngredients[i].Amount);

                if (afterAmount != beforeAmount)
                {
                    NotifyChanged();
                    RequestSaveDebounced();
                }

                if (HasPendingIngredientRefunds)
                {
                    failureReason = "Free space to recover forge materials first.";
                    return false;
                }
            }

            if (!TryGetSelectedRecipe(out BlacksmithingRecipe recipe))
            {
                failureReason = "Select a recipe.";
                return false;
            }

            int playerLevel = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Blacksmithing);
            if (playerLevel < recipe.RequiredLevel)
            {
                failureReason = $"Requires Blacksmithing level {recipe.RequiredLevel}.";
                return false;
            }

            if (!HasIngredientsInInventory(recipe, out failureReason))
                return false;

            return true;
        }

        public bool TryStartCrafting(out string failureReason)
        {
            if (!CanStartCrafting(out failureReason))
                return false;

            if (!TryGetSelectedRecipe(out BlacksmithingRecipe recipe))
            {
                failureReason = "Select a recipe.";
                return false;
            }

            // Belt-and-suspenders: never replace locked STOP leftovers with a new consume set.
            if (HasPendingIngredientRefunds)
            {
                failureReason = "Free space to recover forge materials first.";
                return false;
            }

            if (!TryConsumeIngredients(recipe, out failureReason, out List<BlacksmithingIngredient> consumed))
                return false;

            _lockedConsumedIngredients.Clear();
            _lockedConsumedIngredients.AddRange(consumed);
            _activeRecipeOutputId = recipe.OutputItemId;
            _isCrafting = true;
            _craftProgressSeconds = 0f;
            _lockedCraftDurationSeconds = GetEffectiveDurationSeconds();
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public void StopCrafting()
        {
            if (!_isCrafting)
                return;

            RefundLockedConsumedIngredients();
            _isCrafting = false;
            _activeRecipeOutputId = "";
            _craftProgressSeconds = 0f;
            _lockedCraftDurationSeconds = 0f;
            NotifyChanged();
            RequestSaveDebounced();
        }

        public bool TickCrafting(float deltaSeconds)
        {
            // Flush STOP leftovers when the player frees bag/storage space later.
            if (!_isCrafting && _lockedConsumedIngredients.Count > 0)
            {
                int beforeAmount = 0;
                for (int i = 0; i < _lockedConsumedIngredients.Count; i++)
                    beforeAmount += Mathf.Max(0, _lockedConsumedIngredients[i].Amount);

                TryFlushPendingIngredientRefunds(logIfBlocked: false);

                int afterAmount = 0;
                for (int i = 0; i < _lockedConsumedIngredients.Count; i++)
                    afterAmount += Mathf.Max(0, _lockedConsumedIngredients[i].Amount);

                if (afterAmount != beforeAmount)
                {
                    NotifyChanged();
                    RequestSaveDebounced();
                }
            }

            if (!_isCrafting || deltaSeconds <= 0f)
                return false;

            if (!TryGetActiveRecipe(out _))
            {
                StopCrafting();
                return true;
            }

            _craftProgressSeconds += deltaSeconds;
            float duration = GetActiveDurationSeconds();
            if (_craftProgressSeconds < duration)
            {
                NotifyChanged();
                return false;
            }

            CompleteCraft();
            return true;
        }

        public bool TryActiveWork(out string failureReason) =>
            ProcessingProficiencyRuntime.EnsureInstance().TryApplyBlacksmithingActiveWork(this, out failureReason);

        public bool CanCollectOutput(out string failureReason)
        {
            failureReason = null;
            if (!HasReadyOutput)
            {
                failureReason = "Nothing ready to collect.";
                return false;
            }

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            if (inv.GetReceivableAmount(_readyOutputItemId, 1) <= 0)
            {
                failureReason = "Inventory full.";
                return false;
            }

            return true;
        }

        public bool TryCollectOutput(out string failureReason)
        {
            if (!CanCollectOutput(out failureReason))
                return false;

            Inventory inv = Inventory.ResolvePlayer();
            string outputItemId = _readyOutputItemId;
            int added = inv.AddPartial(outputItemId, 1, notifyItemGainPopup: true);
            if (added <= 0)
            {
                failureReason = "Inventory full.";
                return false;
            }

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Blacksmithing", outputItemId, added);
            _readyOutputItemId = "";
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public bool PlayerHasIngredientsForSelected(out string failureReason) =>
            TryGetSelectedRecipe(out BlacksmithingRecipe recipe)
                ? HasIngredientsInInventory(recipe, out failureReason)
                : Fail(out failureReason, "Select a recipe.");

        public void WriteInto(List<SaveData.BlacksmithingStationSave> list)
        {
            if (list == null)
                return;

            var save = new SaveData.BlacksmithingStationSave
            {
                stationId = _stationId,
                selectedRecipeOutputId = _selectedRecipeOutputId ?? "",
                activeRecipeOutputId = _activeRecipeOutputId ?? "",
                readyOutputItemId = _readyOutputItemId ?? "",
                craftProgressSeconds = _craftProgressSeconds,
                isCrafting = _isCrafting,
                lockedCraftDurationSeconds = _lockedCraftDurationSeconds,
                lockedConsumedItemIds = new List<string>(_lockedConsumedIngredients.Count),
                lockedConsumedAmounts = new List<int>(_lockedConsumedIngredients.Count),
            };

            for (int i = 0; i < _lockedConsumedIngredients.Count; i++)
            {
                BlacksmithingIngredient ing = _lockedConsumedIngredients[i];
                save.lockedConsumedItemIds.Add(ing.ItemId ?? "");
                save.lockedConsumedAmounts.Add(ing.Amount);
            }

            list.Add(save);
        }

        public void ReadFrom(SaveData.BlacksmithingStationSave save)
        {
            if (save == null)
                return;

            _selectedRecipeOutputId = save.selectedRecipeOutputId ?? "";
            _activeRecipeOutputId = save.activeRecipeOutputId ?? "";
            _readyOutputItemId = save.readyOutputItemId ?? "";
            _craftProgressSeconds = Mathf.Max(0f, save.craftProgressSeconds);
            _isCrafting = save.isCrafting;
            _lockedCraftDurationSeconds = Mathf.Max(0f, save.lockedCraftDurationSeconds);
            ReadLockedConsumedIngredients(save);
            if (_isCrafting && _lockedConsumedIngredients.Count == 0)
                ReconstructLockedConsumedFromActiveRecipe();
            NotifyChanged();
        }

        public void ResetToEmpty()
        {
            ReadFrom(new SaveData.BlacksmithingStationSave { stationId = _stationId });
        }

        private void ReadLockedConsumedIngredients(SaveData.BlacksmithingStationSave save)
        {
            _lockedConsumedIngredients.Clear();
            if (save?.lockedConsumedItemIds == null || save.lockedConsumedAmounts == null)
                return;

            int count = Mathf.Min(save.lockedConsumedItemIds.Count, save.lockedConsumedAmounts.Count);
            for (int i = 0; i < count; i++)
            {
                string itemId = save.lockedConsumedItemIds[i];
                int amount = save.lockedConsumedAmounts[i];
                if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                    continue;

                _lockedConsumedIngredients.Add(new BlacksmithingIngredient(itemId.Trim(), amount));
            }
        }

        private void ReconstructLockedConsumedFromActiveRecipe()
        {
            if (!TryGetActiveRecipe(out BlacksmithingRecipe recipe))
                return;

            float costReduction = ProcessingProficiencyRuntime.EnsureInstance().GetBlacksmithingBonuses().ResourceCostReductionPercent;
            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                BlacksmithingIngredient ing = recipe.Ingredients[i];
                int needed = BlacksmithingRecipes.GetEffectiveIngredientAmount(ing.Amount, costReduction);
                _lockedConsumedIngredients.Add(new BlacksmithingIngredient(ing.ItemId, needed));
            }
        }

        private void CompleteCraft()
        {
            if (!TryGetActiveRecipe(out BlacksmithingRecipe recipe))
            {
                StopCrafting();
                return;
            }

            _isCrafting = false;
            _craftProgressSeconds = 0f;
            _lockedCraftDurationSeconds = 0f;
            _activeRecipeOutputId = "";
            _lockedConsumedIngredients.Clear();
            _readyOutputItemId = recipe.OutputItemId;
            ProcessingProficiencyRuntime.EnsureInstance().AddBlacksmithingCraftXp(recipe);
            GameLog.Add($"Forged {ItemGainPopupNotifier.ResolveDisplayLabel(recipe.OutputItemId, 1)}.", GameLog.LevelAvailableColor);
            NotifyChanged();
            RequestSaveDebounced();
        }

        private bool HasIngredientsInInventory(BlacksmithingRecipe recipe, out string failureReason)
        {
            failureReason = null;
            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            float costReduction = ProcessingProficiencyRuntime.EnsureInstance().GetBlacksmithingBonuses().ResourceCostReductionPercent;
            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                BlacksmithingIngredient ing = recipe.Ingredients[i];
                int needed = BlacksmithingRecipes.GetEffectiveIngredientAmount(ing.Amount, costReduction);
                if (inv.GetTotalAmount(ing.ItemId) < needed)
                {
                    failureReason = $"Need {needed} {ItemGainPopupNotifier.ResolveDisplayLabel(ing.ItemId, needed)}.";
                    return false;
                }
            }

            return true;
        }

        private bool TryConsumeIngredients(
            BlacksmithingRecipe recipe,
            out string failureReason,
            out List<BlacksmithingIngredient> consumed)
        {
            consumed = new List<BlacksmithingIngredient>(recipe.Ingredients.Length);
            if (!HasIngredientsInInventory(recipe, out failureReason))
                return false;

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            float costReduction = ProcessingProficiencyRuntime.EnsureInstance().GetBlacksmithingBonuses().ResourceCostReductionPercent;
            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                BlacksmithingIngredient ing = recipe.Ingredients[i];
                int needed = BlacksmithingRecipes.GetEffectiveIngredientAmount(ing.Amount, costReduction);
                if (!inv.Remove(ing.ItemId, needed))
                {
                    // Roll back anything already removed this attempt.
                    for (int r = 0; r < consumed.Count; r++)
                        inv.Add(consumed[r].ItemId, consumed[r].Amount, notifyItemGainPopup: false);
                    consumed.Clear();
                    failureReason = $"Could not remove {ItemGainPopupNotifier.ResolveDisplayLabel(ing.ItemId, needed)}.";
                    return false;
                }

                consumed.Add(new BlacksmithingIngredient(ing.ItemId, needed));
            }

            return true;
        }

        private void RefundLockedConsumedIngredients()
        {
            TryFlushPendingIngredientRefunds(logIfBlocked: true);
        }

        /// <summary>
        /// Returns as many locked STOP/refund ingredients as inventory (then storage) can hold.
        /// Keeps any remainder so materials are never discarded when bags are full.
        /// </summary>
        private void TryFlushPendingIngredientRefunds(bool logIfBlocked)
        {
            if (_lockedConsumedIngredients.Count == 0)
                return;

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
                return;

            PlayerStorage storage = UnityEngine.Object.FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
            var remaining = new List<BlacksmithingIngredient>(_lockedConsumedIngredients.Count);
            bool sentAnyToStorage = false;

            for (int i = 0; i < _lockedConsumedIngredients.Count; i++)
            {
                BlacksmithingIngredient ing = _lockedConsumedIngredients[i];
                if (string.IsNullOrWhiteSpace(ing.ItemId) || ing.Amount <= 0)
                    continue;

                int left = ing.Amount;
                int toInv = inv.AddPartial(ing.ItemId, left, notifyItemGainPopup: false);
                left -= toInv;

                if (left > 0 && storage != null)
                {
                    int toStorage = storage.TryDepositAmountFromExternal(ing.ItemId, left);
                    if (toStorage > 0)
                    {
                        left -= toStorage;
                        sentAnyToStorage = true;
                    }
                }

                if (left > 0)
                    remaining.Add(new BlacksmithingIngredient(ing.ItemId, left));
            }

            _lockedConsumedIngredients.Clear();
            _lockedConsumedIngredients.AddRange(remaining);

            if (!logIfBlocked)
                return;

            if (sentAnyToStorage)
                GameLog.Add("Inventory was full — returned forge materials to storage.", GameLog.CannotMessageColor);

            if (_lockedConsumedIngredients.Count > 0)
            {
                GameLog.Add(
                    "Inventory and storage are full — free space to recover the remaining forge materials.",
                    GameLog.CannotMessageColor);
            }
        }

        private void NotifyChanged() => StateChanged?.Invoke();

        private static bool Fail(out string failureReason, string message)
        {
            failureReason = message;
            return false;
        }
    }
}
