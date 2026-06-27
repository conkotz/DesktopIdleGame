using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent furnace smelting — keeps ticking while the player is on other map nodes (scene furnaces unload).
/// </summary>
[DisallowMultipleComponent]
public sealed class FurnaceSmeltingRuntime : MonoBehaviour, ISaveable
{
    private static FurnaceSmeltingRuntime _instance;

    private readonly Dictionary<string, FurnaceRow> _rows =
        new Dictionary<string, FurnaceRow>(StringComparer.OrdinalIgnoreCase);

    public static FurnaceSmeltingRuntime Instance => _instance;

    public static FurnaceSmeltingRuntime EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var existing = FindFirstObjectByType<FurnaceSmeltingRuntime>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var host = new GameObject(nameof(FurnaceSmeltingRuntime));
        _instance = host.AddComponent<FurnaceSmeltingRuntime>();
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

        foreach (KeyValuePair<string, FurnaceRow> kv in _rows)
        {
            FurnaceRow row = kv.Value;
            if (row == null || !row.IsSmelting)
                continue;

            if (row.TickSmelting(Time.deltaTime))
                structuralChange = true;
        }

        if (structuralChange)
            RequestSaveDebounced();
    }

    private void OnApplicationQuit()
    {
        foreach (KeyValuePair<string, FurnaceRow> kv in _rows)
        {
            FurnaceRow row = kv.Value;
            if (row != null && (row.IsSmelting || row.StoredOreAmount > 0 || row.ReadyBarAmount > 0))
            {
                if (SaveManager.Instance != null)
                    SaveManager.Instance.RequestSave(SaveManager.SaveRequestKind.AppQuit, immediate: true);
                return;
            }
        }
    }

    public FurnaceRow GetOrCreateRow(string furnaceId)
    {
        EnsureInstance();
        string key = NormalizeFurnaceId(furnaceId);
        if (!_rows.TryGetValue(key, out FurnaceRow row) || row == null)
        {
            row = new FurnaceRow(key);
            _rows[key] = row;
        }

        return row;
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        data.furnaceSmelters ??= new List<SaveData.FurnaceSmelterSave>();
        data.furnaceSmelters.Clear();

        foreach (KeyValuePair<string, FurnaceRow> kv in _rows)
            kv.Value?.WriteInto(data.furnaceSmelters);
    }

    public void LoadFrom(SaveData data)
    {
        _rows.Clear();

        if (data?.furnaceSmelters == null)
            return;

        for (int i = 0; i < data.furnaceSmelters.Count; i++)
        {
            SaveData.FurnaceSmelterSave row = data.furnaceSmelters[i];
            if (row == null || string.IsNullOrWhiteSpace(row.furnaceId))
                continue;

            string key = NormalizeFurnaceId(row.furnaceId);
            var furnaceRow = new FurnaceRow(key);
            furnaceRow.ReadFrom(row);
            _rows[key] = furnaceRow;
        }
    }

    private static void RequestSaveDebounced()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.NotifyInventoryChangedDebounced();
    }

    private static string NormalizeFurnaceId(string furnaceId) =>
        string.IsNullOrWhiteSpace(furnaceId) ? "furnace" : furnaceId.Trim();

    public sealed class FurnaceRow
    {
        public event Action StateChanged;

        private readonly string _furnaceId;
        private string _storedOreItemId = "";
        private int _storedOreAmount;
        private int _readyBarAmount;
        private string _readyBarItemId = "";
        private string _activeOreItemId = "";
        private float _smeltProgressSeconds;
        private bool _isSmelting;

        public FurnaceRow(string furnaceId) => _furnaceId = NormalizeFurnaceId(furnaceId);

        public string FurnaceId => _furnaceId;
        public string StoredOreItemId => _storedOreItemId ?? "";
        public int StoredOreAmount => Mathf.Max(0, _storedOreAmount);
        public int ReadyBarAmount => Mathf.Max(0, _readyBarAmount);
        public string ReadyBarItemId => _readyBarItemId ?? "";
        public bool IsSmelting => _isSmelting;
        public float SmeltProgressSeconds => Mathf.Max(0f, _smeltProgressSeconds);

        public string GetActiveOreItemId()
        {
            if (!string.IsNullOrWhiteSpace(_activeOreItemId))
                return _activeOreItemId;
            return _storedOreItemId ?? "";
        }

        public bool TryGetActiveRecipe(out SmeltingRecipe recipe) =>
            SmeltingRecipes.TryGetForOre(GetActiveOreItemId(), out recipe);

        public float GetActiveDurationSeconds() => GetEffectiveDurationSeconds();

        public float GetEffectiveDurationSeconds()
        {
            if (!TryGetActiveRecipe(out SmeltingRecipe recipe))
                return 1f;

            float speedBonus = ProcessingProficiencyRuntime.EnsureInstance().GetSmeltingBonuses().SpeedBonusPercent;
            return recipe.SecondsPerBar / (1f + speedBonus / 100f);
        }

        public int GetOrePerBar() =>
            TryGetActiveRecipe(out SmeltingRecipe recipe) ? recipe.OrePerBar : SmeltingRecipes.DefaultOrePerBar;

        public bool CanStartSmelting()
        {
            if (_isSmelting || _readyBarAmount > 0)
                return false;
            if (!TryGetActiveRecipe(out SmeltingRecipe recipe))
                return false;
            return _storedOreAmount >= recipe.OrePerBar;
        }

        public bool TryStartSmelting()
        {
            if (!CanStartSmelting())
                return false;

            _activeOreItemId = _storedOreItemId;
            _isSmelting = true;
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public void StopSmelting()
        {
            if (!_isSmelting)
                return;

            _isSmelting = false;
            NotifyChanged();
            RequestSaveDebounced();
        }

        public bool TryGetSmeltTimeEstimate(out float totalRemainingSeconds, out float secondsPerBar, out int barsRemaining)
        {
            totalRemainingSeconds = 0f;
            secondsPerBar = 0f;
            barsRemaining = 0;

            string oreId = _isSmelting ? GetActiveOreItemId() : _storedOreItemId;
            if (!SmeltingRecipes.TryGetForOre(oreId, out SmeltingRecipe recipe))
                return false;

            secondsPerBar = GetEffectiveDurationSeconds();
            int ore = StoredOreAmount;
            if (ore < recipe.OrePerBar)
                return false;

            barsRemaining = ore / recipe.OrePerBar;
            if (barsRemaining <= 0)
                return false;

            if (_isSmelting)
            {
                float currentBarRemaining = Mathf.Max(0f, secondsPerBar - _smeltProgressSeconds);
                totalRemainingSeconds = currentBarRemaining + (barsRemaining - 1) * secondsPerBar;
            }
            else
            {
                totalRemainingSeconds = barsRemaining * secondsPerBar;
            }

            return true;
        }

        public bool TryActiveWork(out string failureReason) =>
            ProcessingProficiencyRuntime.EnsureInstance().TryApplyActiveWork(this, out failureReason);

        public bool TryDepositOreFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason)
        {
            failureReason = null;
            if (inv == null || slotIndex < 0)
            {
                failureReason = "Invalid slot.";
                return false;
            }

            var slot = inv.GetSlot(slotIndex);
            if (slot.IsEmpty)
            {
                failureReason = "Empty slot.";
                return false;
            }

            int deposit = amount <= 0 ? slot.amount : Mathf.Min(amount, slot.amount);
            if (deposit <= 0)
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            if (!ValidateOreDeposit(slot.itemId, out failureReason))
                return false;

            int removed = inv.RemoveAmountAtSlot(slotIndex, deposit);
            if (removed <= 0)
            {
                failureReason = "Could not remove ore from inventory.";
                return false;
            }

            ApplyOreDeposit(slot.itemId, removed);
            return true;
        }

        public bool TryDepositOre(string oreItemId, int amount, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrWhiteSpace(oreItemId) || amount <= 0)
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            if (!ValidateOreDeposit(oreItemId, out failureReason))
                return false;

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int available = inv.GetTotalAmount(oreItemId);
            if (available <= 0)
            {
                failureReason = "You do not have that ore.";
                return false;
            }

            int deposit = Mathf.Min(amount, available);
            if (!inv.Remove(oreItemId, deposit))
            {
                failureReason = "Could not remove ore from inventory.";
                return false;
            }

            ApplyOreDeposit(oreItemId, deposit);
            return true;
        }

        public bool TryDepositAllOreFromInventory(string oreItemId, out string failureReason)
        {
            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int available = inv.GetTotalAmount(oreItemId);
            return TryDepositOre(oreItemId, available, out failureReason);
        }

        public bool TryWithdrawAllOre(out string failureReason)
        {
            failureReason = null;
            if (_storedOreAmount <= 0)
            {
                failureReason = "No ore stored.";
                return false;
            }

            if (_isSmelting)
            {
                failureReason = "Stop smelting before removing ore.";
                return false;
            }

            if (_readyBarAmount > 0)
            {
                failureReason = "Must remove bars first.";
                return false;
            }

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            string oreId = _storedOreItemId;
            int toReturn = _storedOreAmount;
            int before = inv.GetTotalAmount(oreId);
            inv.Add(oreId, toReturn, notifyItemGainPopup: false);
            int added = inv.GetTotalAmount(oreId) - before;
            if (added <= 0)
            {
                failureReason = "Inventory full.";
                return false;
            }

            _storedOreAmount -= added;
            if (_storedOreAmount <= 0)
            {
                _storedOreAmount = 0;
                _storedOreItemId = "";
                if (!_isSmelting && _readyBarAmount <= 0)
                {
                    _activeOreItemId = "";
                    _smeltProgressSeconds = 0f;
                }
            }

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Furnace", oreId, added);
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public bool TryCollectBars(int amount, out string failureReason)
        {
            failureReason = null;
            if (amount <= 0 || _readyBarAmount <= 0)
            {
                failureReason = "No bars ready.";
                return false;
            }

            string barItemId = GetReadyBarItemId();
            if (string.IsNullOrWhiteSpace(barItemId))
            {
                failureReason = "No bar type configured.";
                return false;
            }

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int collect = Mathf.Min(amount, _readyBarAmount);
            int before = inv.GetTotalAmount(barItemId);
            inv.Add(barItemId, collect, notifyItemGainPopup: true);
            int added = inv.GetTotalAmount(barItemId) - before;
            if (added <= 0)
            {
                failureReason = "Inventory full.";
                return false;
            }

            _readyBarAmount -= added;
            ClearStaleIdsWhenEmpty();

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Furnace", barItemId, added);
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public bool TickSmelting(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || !_isSmelting)
                return false;

            if (!TryGetActiveRecipe(out SmeltingRecipe recipe))
            {
                _isSmelting = false;
                NotifyChanged();
                return true;
            }

            bool wasSmelting = _isSmelting;
            bool structuralChange = false;
            float remaining = deltaSeconds;
            while (remaining > 0f && _isSmelting)
            {
                if (_storedOreAmount < recipe.OrePerBar)
                {
                    _isSmelting = false;
                    structuralChange = true;
                    break;
                }

                float barDuration = GetEffectiveDurationSeconds();
                float needed = barDuration - _smeltProgressSeconds;
                if (remaining >= needed)
                {
                    remaining -= needed;
                    _smeltProgressSeconds = 0f;
                    _storedOreAmount -= recipe.OrePerBar;
                    _readyBarAmount++;
                    _readyBarItemId = recipe.BarItemId;
                    structuralChange = true;

                    ProcessingProficiencyRuntime.EnsureInstance().AddSmeltingBarXp(recipe);
                    TryRollBonusBar(recipe);

                    if (_storedOreAmount < recipe.OrePerBar)
                        _isSmelting = false;
                }
                else
                {
                    _smeltProgressSeconds += remaining;
                    remaining = 0f;
                }
            }

            if (structuralChange)
            {
                if (wasSmelting && !_isSmelting && _readyBarAmount > 0)
                    LogSmeltingBatchComplete();

                ClearStaleIdsWhenEmpty();
                NotifyChanged();
            }

            return structuralChange;
        }

        private void LogSmeltingBatchComplete()
        {
            string barItemId = GetReadyBarItemId();
            if (string.IsNullOrWhiteSpace(barItemId))
                return;

            int amount = _readyBarAmount;
            string barLabel = ItemGainPopupNotifier.ResolveDisplayLabel(barItemId, amount);
            GameLog.Add(
                $"Furnace finished smelting: {amount} {barLabel} ready to collect.",
                GameLog.QuestCompleteColor);
        }

        public void WriteInto(List<SaveData.FurnaceSmelterSave> target)
        {
            if (target == null)
                return;

            target.Add(new SaveData.FurnaceSmelterSave
            {
                furnaceId = _furnaceId,
                storedOreItemId = _storedOreItemId ?? "",
                storedOreAmount = Mathf.Max(0, _storedOreAmount),
                readyBarAmount = Mathf.Max(0, _readyBarAmount),
                readyBarItemId = GetReadyBarItemId(),
                activeOreItemId = _activeOreItemId ?? "",
                smeltProgressSeconds = Mathf.Max(0f, _smeltProgressSeconds),
                isSmelting = _isSmelting
            });
        }

        public void ReadFrom(SaveData.FurnaceSmelterSave row)
        {
            _storedOreItemId = row.storedOreItemId ?? "";
            _storedOreAmount = Mathf.Max(0, row.storedOreAmount);
            _readyBarAmount = Mathf.Max(0, row.readyBarAmount);
            _readyBarItemId = row.readyBarItemId ?? "";
            _activeOreItemId = row.activeOreItemId ?? "";
            _smeltProgressSeconds = Mathf.Max(0f, row.smeltProgressSeconds);
            _isSmelting = row.isSmelting;
            NormalizeStateAfterLoad();
            NotifyChanged();
        }

        private void NormalizeStateAfterLoad()
        {
            if (_readyBarAmount > 0 && string.IsNullOrWhiteSpace(_readyBarItemId))
            {
                if (SmeltingRecipes.TryGetForOre(_activeOreItemId, out SmeltingRecipe recipe) ||
                    SmeltingRecipes.TryGetForOre(_storedOreItemId, out recipe))
                {
                    _readyBarItemId = recipe.BarItemId;
                }
            }

            if (_isSmelting &&
                (!TryGetActiveRecipe(out SmeltingRecipe activeRecipe) || _storedOreAmount < activeRecipe.OrePerBar))
            {
                _isSmelting = false;
            }

            ClearStaleIdsWhenEmpty();
        }

        private void ClearStaleIdsWhenEmpty()
        {
            if (_storedOreAmount <= 0)
            {
                _storedOreAmount = 0;
                _storedOreItemId = "";
            }

            if (_readyBarAmount <= 0)
            {
                _readyBarAmount = 0;
                _readyBarItemId = "";
            }

            if (!_isSmelting && _storedOreAmount <= 0 && _readyBarAmount <= 0)
            {
                _activeOreItemId = "";
                _smeltProgressSeconds = 0f;
            }
        }

        private string GetReadyBarItemId()
        {
            if (!string.IsNullOrWhiteSpace(_readyBarItemId))
                return _readyBarItemId;

            if (TryGetActiveRecipe(out SmeltingRecipe recipe))
                return recipe.BarItemId;

            if (SmeltingRecipes.TryGetForOre(_storedOreItemId, out recipe))
                return recipe.BarItemId;

            return "";
        }

        private bool ValidateOreDeposit(string oreItemId, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrWhiteSpace(oreItemId))
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            if (!SmeltingRecipes.TryGetForOre(oreItemId, out _))
            {
                failureReason = "That ore cannot be smelted here.";
                return false;
            }

            if (_readyBarAmount > 0)
            {
                failureReason = "Collect furnace bars before adding ore.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_storedOreItemId) &&
                !string.Equals(_storedOreItemId, oreItemId, StringComparison.OrdinalIgnoreCase) &&
                _storedOreAmount > 0)
            {
                failureReason = "This furnace already holds a different ore type.";
                return false;
            }

            return true;
        }

        private void ApplyOreDeposit(string oreItemId, int deposited)
        {
            if (deposited <= 0)
                return;

            if (string.IsNullOrWhiteSpace(_storedOreItemId))
                _storedOreItemId = oreItemId.Trim().ToLowerInvariant();

            _storedOreAmount += deposited;
            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Furnace", oreItemId.Trim().ToLowerInvariant(), -deposited);
            NotifyChanged();
            RequestSaveDebounced();
        }

        private void TryRollBonusBar(SmeltingRecipe recipe)
        {
            SmeltingProficiencyBonuses bonuses = ProcessingProficiencyRuntime.EnsureInstance().GetSmeltingBonuses();
            if (bonuses.DoubleBarChancePercent <= 0f)
                return;

            if (UnityEngine.Random.value * 100f >= bonuses.DoubleBarChancePercent)
                return;

            _readyBarAmount++;
            _readyBarItemId = recipe.BarItemId;
        }

        private void NotifyChanged() => StateChanged?.Invoke();
    }
}
