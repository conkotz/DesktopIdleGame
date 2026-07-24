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
            if (row != null && (row.IsSmelting || row.StoredOreAmount > 0 || row.ReadyBarAmount > 0 || row.StoredEnhancementAmount > 0))
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

        // Preserve snapshot-seeded furnace rows when this DDOL runtime has never been hydrated.
        if (_rows.Count == 0)
            return;

        data.furnaceSmelters.Clear();

        foreach (KeyValuePair<string, FurnaceRow> kv in _rows)
            kv.Value?.WriteInto(data.furnaceSmelters);
    }

    public void LoadFrom(SaveData data)
    {
        // Update existing row objects in place so scene furnaces that already
        // bound/subscribed in Awake keep valid references after late save apply.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (data?.furnaceSmelters != null)
        {
            for (int i = 0; i < data.furnaceSmelters.Count; i++)
            {
                SaveData.FurnaceSmelterSave save = data.furnaceSmelters[i];
                if (save == null || string.IsNullOrWhiteSpace(save.furnaceId))
                    continue;

                string key = NormalizeFurnaceId(save.furnaceId);
                if (!_rows.TryGetValue(key, out FurnaceRow row) || row == null)
                {
                    row = new FurnaceRow(key);
                    _rows[key] = row;
                }

                row.ReadFrom(save);
                seen.Add(key);
            }
        }

        foreach (KeyValuePair<string, FurnaceRow> kv in _rows)
        {
            if (kv.Value == null || seen.Contains(kv.Key))
                continue;

            kv.Value.ResetToEmpty();
        }
    }

    /// <summary>
    /// Advances active smelts by wall-clock time lost while the game was closed.
    /// Safe to call after <see cref="LoadFrom"/>; no-ops when nothing is smelting.
    /// </summary>
    public void ApplyOfflineSeconds(float offlineSeconds)
    {
        offlineSeconds = Mathf.Clamp(offlineSeconds, 0f, 8f * 60f * 60f);
        if (offlineSeconds < 1f || _rows.Count == 0)
            return;

        const float chunkSeconds = 30f;
        float remaining = offlineSeconds;
        bool structuralChange = false;

        while (remaining > 0.0001f)
        {
            float chunk = Mathf.Min(remaining, chunkSeconds);
            bool anyActive = false;

            foreach (KeyValuePair<string, FurnaceRow> kv in _rows)
            {
                FurnaceRow row = kv.Value;
                if (row == null || !row.IsSmelting)
                    continue;

                anyActive = true;
                if (row.TickSmelting(chunk))
                    structuralChange = true;
            }

            if (!anyActive)
                break;

            remaining -= chunk;
        }

        if (structuralChange)
            RequestSaveDebounced();
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
        private readonly ProcessingFuelBank _fuelBank = new();
        private string _storedEnhancementItemId = "";
        private int _storedEnhancementAmount;
        private string _storedOreItemId = "";
        private int _storedOreAmount;
        private int _readyBarAmount;
        private string _readyBarItemId = "";
        private string _activeOreItemId = "";
        private float _smeltProgressSeconds;
        private bool _isSmelting;
        private float _lockedBarDurationSeconds;
        private bool _hasLockedBarModifiers;

        public FurnaceRow(string furnaceId) => _furnaceId = NormalizeFurnaceId(furnaceId);

        public string FurnaceId => _furnaceId;
        public string StoredEnhancementItemId => _storedEnhancementItemId ?? "";
        public int StoredEnhancementAmount => Mathf.Max(0, _storedEnhancementAmount);
        public string StoredFuelItemId => _fuelBank.StoredItemId;
        public int StoredFuelAmount => _fuelBank.StoredAmount;
        public bool HasFuel => _fuelBank.HasFuel;
        public float FuelSecondsRemaining => _fuelBank.GetSecondsRemaining();
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

        public float GetActiveDurationSeconds()
        {
            if (_isSmelting && _hasLockedBarModifiers)
                return _lockedBarDurationSeconds;
            return GetEffectiveDurationSeconds();
        }

        public float GetEffectiveDurationSeconds()
        {
            if (!TryGetActiveRecipe(out SmeltingRecipe recipe))
                return 1f;

            float speedBonus = ProcessingProficiencyRuntime.EnsureInstance().GetSmeltingBonuses().SpeedBonusPercent;
            float duration = recipe.SecondsPerBar / (1f + speedBonus / 100f);
            duration -= GetEnhancementFlatSecondsReduction();
            return Mathf.Max(0.1f, duration);
        }

        public int GetOrePerBar() =>
            TryGetActiveRecipe(out SmeltingRecipe recipe) ? recipe.OrePerBar : SmeltingRecipes.DefaultOrePerBar;

        public bool CanStartSmelting()
        {
            if (_isSmelting || _readyBarAmount > 0)
                return false;
            if (!TryGetActiveRecipe(out SmeltingRecipe recipe))
                return false;
            if (!MeetsSmeltingLevelRequirement(recipe))
                return false;
            if (!_fuelBank.HasFuel)
                return false;
            return _storedOreAmount >= recipe.OrePerBar;
        }

        public bool TryStartSmelting()
        {
            if (!CanStartSmelting())
                return false;

            _activeOreItemId = _storedOreItemId;
            _isSmelting = true;
            _smeltProgressSeconds = 0f;
            LockBarModifiers();
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public void StopSmelting()
        {
            if (!_isSmelting)
                return;

            _isSmelting = false;
            ClearBarModifiers();
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

        public bool TryDepositEnhancementFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason)
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

            if (!ValidateEnhancementDeposit(slot.itemId, out failureReason))
                return false;

            int removed = inv.RemoveAmountAtSlot(slotIndex, deposit);
            if (removed <= 0)
            {
                failureReason = "Could not remove item from inventory.";
                return false;
            }

            ApplyEnhancementDeposit(slot.itemId, removed);
            return true;
        }

        public bool TryDepositEnhancement(string itemId, int amount, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            if (!ValidateEnhancementDeposit(itemId, out failureReason))
                return false;

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int available = inv.GetTotalAmount(itemId);
            if (available <= 0)
            {
                failureReason = "You do not have that enhancement.";
                return false;
            }

            int deposit = Mathf.Min(amount, available);
            if (!inv.Remove(itemId, deposit))
            {
                failureReason = "Could not remove enhancement from inventory.";
                return false;
            }

            ApplyEnhancementDeposit(itemId, deposit);
            return true;
        }

        public bool TryDepositAllEnhancementFromInventory(string itemId, out string failureReason)
        {
            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int available = inv.GetTotalAmount(itemId);
            return TryDepositEnhancement(itemId, available, out failureReason);
        }

        public bool TryDepositFuelFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason)
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

            if (!ValidateFuelDeposit(slot.itemId, deposit, out failureReason, out int allowed))
                return false;

            deposit = allowed;
            int removed = inv.RemoveAmountAtSlot(slotIndex, deposit);
            if (removed <= 0)
            {
                failureReason = "Could not remove fuel from inventory.";
                return false;
            }

            ApplyFuelDeposit(slot.itemId, removed);
            return true;
        }

        public bool TryDepositFuel(string itemId, int amount, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            if (!ValidateFuelDeposit(itemId, amount, out failureReason, out int allowed))
                return false;

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int available = inv.GetTotalAmount(itemId);
            if (available <= 0)
            {
                failureReason = "You do not have that log.";
                return false;
            }

            int deposit = Mathf.Min(Mathf.Min(amount, allowed), available);
            if (!inv.Remove(itemId, deposit))
            {
                failureReason = "Could not remove fuel from inventory.";
                return false;
            }

            ApplyFuelDeposit(itemId, deposit);
            return true;
        }

        public bool TryDepositAllFuelFromInventory(string itemId, out string failureReason)
        {
            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int available = inv.GetTotalAmount(itemId);
            return TryDepositFuel(itemId, available, out failureReason);
        }

        public bool TryWithdrawAllFuel(out string failureReason)
        {
            failureReason = null;
            if (!_fuelBank.HasFuel)
            {
                failureReason = "No fuel stored.";
                return false;
            }

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            string itemId = _fuelBank.StoredItemId;
            float burned = _fuelBank.SecondsBurnedFromCurrentLog;
            int keptPartial = burned > 0.0001f ? 1 : 0;
            int toReturn = _fuelBank.WithdrawFullLogsOnly();
            if (toReturn <= 0)
            {
                failureReason = "Current fuel log is partially burned and cannot be withdrawn.";
                return false;
            }

            int before = inv.GetTotalAmount(itemId);
            inv.Add(itemId, toReturn, notifyItemGainPopup: false);
            int added = inv.GetTotalAmount(itemId) - before;
            if (added <= 0)
            {
                _fuelBank.Load(itemId, toReturn + keptPartial, burned);
                failureReason = "Inventory full.";
                return false;
            }

            if (added < toReturn)
                _fuelBank.Load(itemId, (toReturn - added) + keptPartial, burned);

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Furnace", itemId, added);
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public bool TryWithdrawAllEnhancement(out string failureReason)
        {
            failureReason = null;
            if (_storedEnhancementAmount <= 0)
            {
                failureReason = "No enhancement loaded.";
                return false;
            }

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            string itemId = _storedEnhancementItemId;
            int toReturn = _storedEnhancementAmount;
            int before = inv.GetTotalAmount(itemId);
            inv.Add(itemId, toReturn, notifyItemGainPopup: false);
            int added = inv.GetTotalAmount(itemId) - before;
            if (added <= 0)
            {
                failureReason = "Inventory full.";
                return false;
            }

            _storedEnhancementAmount -= added;
            if (_storedEnhancementAmount <= 0)
            {
                _storedEnhancementAmount = 0;
                _storedEnhancementItemId = "";
            }

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Furnace", itemId, added);
            NotifyChanged();
            RequestSaveDebounced();
            return true;
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
            PlayerStorage storage = UnityEngine.Object.FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
            if (!inv && storage == null)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int collect = Mathf.Min(amount, _readyBarAmount);
            int added = inv ? inv.AddPartial(barItemId, collect, notifyItemGainPopup: true) : 0;
            int left = collect - added;
            bool sentToStorage = false;
            if (left > 0 && storage != null)
            {
                int toStorage = storage.TryDepositAmountFromExternal(barItemId, left);
                if (toStorage > 0)
                {
                    left -= toStorage;
                    added += toStorage;
                    sentToStorage = true;
                }
            }

            if (added <= 0)
            {
                failureReason = "Inventory and storage are full.";
                return false;
            }

            _readyBarAmount -= added;
            ClearStaleIdsWhenEmpty();

            if (sentToStorage)
            {
                GameLog.Add(
                    left > 0
                        ? "Inventory was full — sent some bars to storage (rest still in the furnace)."
                        : "Inventory was full — sent bars to storage.",
                    GameLog.CannotMessageColor);
            }

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
                ClearBarModifiers();
                NotifyChanged();
                return true;
            }

            if (!_fuelBank.HasFuel)
            {
                _isSmelting = false;
                ClearBarModifiers();
                NotifyChanged();
                return true;
            }

            bool wasSmelting = _isSmelting;
            bool structuralChange = false;
            float remaining = deltaSeconds;
            const int maxBarsPerTick = 50;
            int barsProcessed = 0;
            while (remaining > 0.0001f && _isSmelting && barsProcessed < maxBarsPerTick)
            {
                if (_storedOreAmount < recipe.OrePerBar)
                {
                    _isSmelting = false;
                    ClearBarModifiers();
                    structuralChange = true;
                    break;
                }

                float barDuration = _hasLockedBarModifiers
                    ? _lockedBarDurationSeconds
                    : GetEffectiveDurationSeconds();
                if (barDuration <= 0.0001f)
                    barDuration = 0.0001f;

                float needed = barDuration - _smeltProgressSeconds;
                float step = remaining >= needed ? needed : remaining;

                // Burn fuel in lockstep with progress so partial fuel still advances the bar
                // and large deltas never burn more fuel than craft steps applied.
                float burned = _fuelBank.ConsumeUpTo(step);
                if (burned <= 0.0001f)
                {
                    _isSmelting = false;
                    ClearBarModifiers();
                    structuralChange = true;
                    break;
                }

                _smeltProgressSeconds += burned;
                remaining -= burned;

                if (burned + 0.0001f < step)
                {
                    // Fuel ran out before the requested step finished.
                    _isSmelting = false;
                    ClearBarModifiers();
                    structuralChange = true;
                    break;
                }

                if (_smeltProgressSeconds + 0.0001f < barDuration)
                    break;

                _smeltProgressSeconds = 0f;
                _storedOreAmount -= recipe.OrePerBar;
                _readyBarAmount++;
                _readyBarItemId = recipe.BarItemId;
                structuralChange = true;
                barsProcessed++;
                ConsumeEnhancementForBarAttempt();

                ProcessingProficiencyRuntime.EnsureInstance().AddSmeltingBarXp(recipe);
                TryRollBonusBar(recipe);

                if (_storedOreAmount < recipe.OrePerBar)
                {
                    _isSmelting = false;
                    ClearBarModifiers();
                }
                else
                    LockBarModifiers();
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
                storedEnhancementItemId = _storedEnhancementItemId ?? "",
                storedEnhancementAmount = Mathf.Max(0, _storedEnhancementAmount),
                storedFuelItemId = _fuelBank.StoredItemId,
                storedFuelAmount = _fuelBank.StoredAmount,
                fuelSecondsBurnedFromCurrentLog = _fuelBank.SecondsBurnedFromCurrentLog,
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
            _storedEnhancementItemId = row.storedEnhancementItemId ?? "";
            _storedEnhancementAmount = Mathf.Max(0, row.storedEnhancementAmount);
            _fuelBank.Load(
                row.storedFuelItemId ?? "",
                row.storedFuelAmount,
                row.fuelSecondsBurnedFromCurrentLog);
            _activeOreItemId = row.activeOreItemId ?? "";
            _smeltProgressSeconds = Mathf.Max(0f, row.smeltProgressSeconds);
            _isSmelting = row.isSmelting;
            NormalizeStateAfterLoad();
            NotifyChanged();
        }

        public void ResetToEmpty()
        {
            ReadFrom(new SaveData.FurnaceSmelterSave { furnaceId = _furnaceId });
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
                else
                {
                    // Unresolvable ready bars softlock withdraw/deposit; drop corrupt amount.
                    Debug.LogWarning(
                        $"[Furnace] Dropping corrupt ready bars on '{_furnaceId}' with no resolvable item id.");
                    _readyBarAmount = 0;
                    _readyBarItemId = "";
                }
            }

            if (_isSmelting &&
                (!_fuelBank.HasFuel ||
                 !TryGetActiveRecipe(out SmeltingRecipe activeRecipe) ||
                 _storedOreAmount < activeRecipe.OrePerBar))
            {
                _isSmelting = false;
                ClearBarModifiers();
            }
            else if (_isSmelting)
                LockBarModifiers();

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

            if (_storedEnhancementAmount <= 0)
            {
                _storedEnhancementAmount = 0;
                _storedEnhancementItemId = "";
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

        private static int GetRequiredSmeltingLevel(SmeltingRecipe recipe) =>
            SmeltingRecipes.GetRequiredSmeltingLevel(recipe);

        private bool ValidateOreDeposit(string oreItemId, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrWhiteSpace(oreItemId))
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            if (!SmeltingRecipes.TryGetForOre(oreItemId, out SmeltingRecipe recipe))
            {
                failureReason = "That ore cannot be smelted here.";
                return false;
            }

            int smeltingLevel = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Smelting);
            int requiredLevel = GetRequiredSmeltingLevel(recipe);
            if (smeltingLevel < requiredLevel)
            {
                failureReason = $"Requires Smelting level {requiredLevel}.";
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

        private static bool MeetsSmeltingLevelRequirement(SmeltingRecipe recipe)
        {
            int smeltingLevel = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Smelting);
            return smeltingLevel >= GetRequiredSmeltingLevel(recipe);
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

        private bool ValidateEnhancementDeposit(string itemId, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            ItemDefinition def = ResolveItemDefinition(itemId);
            if (def == null || !def.IsProcessingSkillEnhancement)
            {
                failureReason = "That item is not a processing enhancement.";
                return false;
            }

            if (def.ProcessingSkillTarget != ProcessingSkillTarget.Smelting)
            {
                failureReason = "That enhancement is for cooking, not smelting.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_storedEnhancementItemId) &&
                !string.Equals(_storedEnhancementItemId, itemId, StringComparison.OrdinalIgnoreCase) &&
                _storedEnhancementAmount > 0)
            {
                failureReason = "This furnace already holds a different enhancement.";
                return false;
            }

            return true;
        }

        private bool ValidateFuelDeposit(string itemId, int amount, out string failureReason, out int allowedAmount)
        {
            allowedAmount = 0;
            if (!_fuelBank.CanAccept(itemId, amount, out failureReason))
                return false;

            allowedAmount = _fuelBank.GetDepositCapacity(amount);
            if (allowedAmount <= 0)
            {
                failureReason = $"Fuel slot is full (max {ProcessingFuelCatalog.MaxFuelLogs}).";
                return false;
            }

            return true;
        }

        private void ApplyFuelDeposit(string itemId, int deposited)
        {
            if (deposited <= 0)
                return;

            _fuelBank.AddLogs(itemId, deposited);
            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Furnace", itemId.Trim().ToLowerInvariant(), -deposited);
            NotifyChanged();
            RequestSaveDebounced();
        }

        private void ApplyEnhancementDeposit(string itemId, int deposited)
        {
            if (deposited <= 0)
                return;

            if (string.IsNullOrWhiteSpace(_storedEnhancementItemId))
                _storedEnhancementItemId = itemId.Trim().ToLowerInvariant();

            _storedEnhancementAmount += deposited;
            SessionTrackerData.EnsureInstance()?.RegisterLootChange(
                "Furnace",
                itemId.Trim().ToLowerInvariant(),
                -deposited);
            NotifyChanged();
            RequestSaveDebounced();
        }

        private void ConsumeEnhancementForBarAttempt()
        {
            if (_storedEnhancementAmount <= 0 || string.IsNullOrWhiteSpace(_storedEnhancementItemId))
                return;

            _storedEnhancementAmount--;
            if (_storedEnhancementAmount <= 0)
            {
                _storedEnhancementAmount = 0;
                _storedEnhancementItemId = "";
            }
        }

        private float GetEnhancementFlatSecondsReduction()
        {
            ItemDefinition def = ResolveStoredEnhancementDefinition();
            return def != null ? def.ProcessingFlatSecondsReduction : 0f;
        }

        private ItemDefinition ResolveStoredEnhancementDefinition()
        {
            if (_storedEnhancementAmount <= 0 || string.IsNullOrWhiteSpace(_storedEnhancementItemId))
                return null;

            return ResolveItemDefinition(_storedEnhancementItemId);
        }

        private static ItemDefinition ResolveItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            ItemDatabase db = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
            return db != null ? db.Get(itemId) : null;
        }

        private void LockBarModifiers()
        {
            _lockedBarDurationSeconds = GetEffectiveDurationSeconds();
            _hasLockedBarModifiers = true;
        }

        private void ClearBarModifiers() => _hasLockedBarModifiers = false;

        private void NotifyChanged() => StateChanged?.Invoke();
    }
}
