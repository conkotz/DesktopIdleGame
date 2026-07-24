using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent cooking range processing — keeps ticking while the player is on other map nodes (scene cooking ranges unload).
/// </summary>
[DisallowMultipleComponent]
public sealed class CookingRuntime : MonoBehaviour, ISaveable
{
    private static CookingRuntime _instance;

    private readonly Dictionary<string, CookingRow> _rows =
        new Dictionary<string, CookingRow>(StringComparer.OrdinalIgnoreCase);

    public static CookingRuntime Instance => _instance;

    public static CookingRuntime EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var existing = FindFirstObjectByType<CookingRuntime>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var host = new GameObject(nameof(CookingRuntime));
        _instance = host.AddComponent<CookingRuntime>();
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

        foreach (KeyValuePair<string, CookingRow> kv in _rows)
        {
            CookingRow row = kv.Value;
            if (row == null || !row.IsCooking)
                continue;

            if (row.TickCooking(Time.deltaTime))
                structuralChange = true;
        }

        if (structuralChange)
            RequestSaveDebounced();
    }

    private void OnApplicationQuit()
    {
        foreach (KeyValuePair<string, CookingRow> kv in _rows)
        {
            CookingRow row = kv.Value;
            if (row != null && (row.IsCooking || row.StoredRawAmount > 0 || row.ReadyCookedAmount > 0))
            {
                if (SaveManager.Instance != null)
                    SaveManager.Instance.RequestSave(SaveManager.SaveRequestKind.AppQuit, immediate: true);
                return;
            }
        }
    }

    public CookingRow GetOrCreateRow(string stationId)
    {
        EnsureInstance();
        string key = NormalizeStationId(stationId);
        if (!_rows.TryGetValue(key, out CookingRow row) || row == null)
        {
            row = new CookingRow(key);
            _rows[key] = row;
        }

        return row;
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        data.cookingStations ??= new List<SaveData.CookingStationSave>();

        // Preserve snapshot-seeded cooking rows when this DDOL runtime has never been hydrated.
        if (_rows.Count == 0)
            return;

        data.cookingStations.Clear();

        foreach (KeyValuePair<string, CookingRow> kv in _rows)
            kv.Value?.WriteInto(data.cookingStations);
    }

    public void LoadFrom(SaveData data)
    {
        // Update existing row objects in place so scene stations that already
        // bound/subscribed in Awake keep valid references after late save apply.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (data?.cookingStations != null)
        {
            for (int i = 0; i < data.cookingStations.Count; i++)
            {
                SaveData.CookingStationSave save = data.cookingStations[i];
                if (save == null || string.IsNullOrWhiteSpace(save.stationId))
                    continue;

                string key = NormalizeStationId(save.stationId);
                if (!_rows.TryGetValue(key, out CookingRow row) || row == null)
                {
                    row = new CookingRow(key);
                    _rows[key] = row;
                }

                row.ReadFrom(save);
                seen.Add(key);
            }
        }

        foreach (KeyValuePair<string, CookingRow> kv in _rows)
        {
            if (kv.Value == null || seen.Contains(kv.Key))
                continue;

            kv.Value.ResetToEmpty();
        }
    }

    /// <summary>
    /// Advances active cooks by wall-clock time lost while the game was closed.
    /// Safe to call after <see cref="LoadFrom"/>; no-ops when nothing is cooking.
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

            foreach (KeyValuePair<string, CookingRow> kv in _rows)
            {
                CookingRow row = kv.Value;
                if (row == null || !row.IsCooking)
                    continue;

                anyActive = true;
                if (row.TickCooking(chunk))
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

    private static string NormalizeStationId(string stationId) =>
        string.IsNullOrWhiteSpace(stationId) ? "cooking_range" : stationId.Trim();

    public sealed class CookingRow
    {
        public event Action StateChanged;

        private readonly string _stationId;
        private readonly ProcessingFuelBank _fuelBank = new();
        private string _storedEnhancementItemId = "";
        private int _storedEnhancementAmount;
        private string _storedRawItemId = "";
        private int _storedRawAmount;
        private int _readyCookedAmount;
        private string _readyCookedItemId = "";
        private string _activeRawItemId = "";
        private float _cookProgressSeconds;
        private bool _isCooking;
        private float _lockedPortionDurationSeconds;
        private float _lockedPortionBurnChancePercent;
        private bool _hasLockedPortionModifiers;

        public CookingRow(string stationId) => _stationId = NormalizeStationId(stationId);

        public string StationId => _stationId;
        public string StoredEnhancementItemId => _storedEnhancementItemId ?? "";
        public int StoredEnhancementAmount => Mathf.Max(0, _storedEnhancementAmount);
        public string StoredFuelItemId => _fuelBank.StoredItemId;
        public int StoredFuelAmount => _fuelBank.StoredAmount;
        public bool HasFuel => _fuelBank.HasFuel;
        public float FuelSecondsRemaining => _fuelBank.GetSecondsRemaining();
        public string StoredRawItemId => _storedRawItemId ?? "";
        public int StoredRawAmount => Mathf.Max(0, _storedRawAmount);
        public int ReadyCookedAmount => Mathf.Max(0, _readyCookedAmount);
        public string ReadyCookedItemId => _readyCookedItemId ?? "";
        public bool IsCooking => _isCooking;
        public float CookProgressSeconds => Mathf.Max(0f, _cookProgressSeconds);

        public string GetActiveRawItemId()
        {
            if (!string.IsNullOrWhiteSpace(_activeRawItemId))
                return _activeRawItemId;
            return _storedRawItemId ?? "";
        }

        public bool TryGetActiveRecipe(out CookingRecipe recipe) =>
            CookingRecipes.TryGetForRaw(GetActiveRawItemId(), out recipe);

        public float GetActiveDurationSeconds()
        {
            if (_isCooking && _hasLockedPortionModifiers)
                return _lockedPortionDurationSeconds;
            return GetEffectiveDurationSeconds();
        }

        public float GetEffectiveDurationSeconds()
        {
            if (!TryGetActiveRecipe(out CookingRecipe recipe))
                return 1f;

            float speedBonus = ProcessingProficiencyRuntime.EnsureInstance().GetCookingBonuses().SpeedBonusPercent;
            float duration = recipe.SecondsPerCooked / (1f + speedBonus / 100f);
            duration -= GetEnhancementFlatSecondsReduction();
            return Mathf.Max(0.1f, duration);
        }

        public float GetEffectiveBurnChancePercent()
        {
            CookingProficiencyBonuses bonuses = ProcessingProficiencyRuntime.EnsureInstance().GetCookingBonuses();
            float burnChance = bonuses.EffectiveBurnChancePercent - GetEnhancementBurnChanceReductionPercent();
            return Mathf.Max(0f, burnChance);
        }

        public int GetRawPerCooked() =>
            TryGetActiveRecipe(out CookingRecipe recipe) ? recipe.RawPerCooked : CookingRecipes.DefaultRawPerCooked;

        public bool CanStartCooking()
        {
            if (_isCooking || _readyCookedAmount > 0)
                return false;
            if (!TryGetActiveRecipe(out CookingRecipe recipe))
                return false;
            if (!_fuelBank.HasFuel)
                return false;
            return _storedRawAmount >= recipe.RawPerCooked;
        }

        public bool TryStartCooking()
        {
            if (!CanStartCooking())
                return false;

            _activeRawItemId = _storedRawItemId;
            _isCooking = true;
            _cookProgressSeconds = 0f;
            LockPortionModifiers();
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public void StopCooking()
        {
            if (!_isCooking)
                return;

            _isCooking = false;
            ClearPortionModifiers();
            NotifyChanged();
            RequestSaveDebounced();
        }

        public bool TryGetCookTimeEstimate(out float totalRemainingSeconds, out float secondsPerCooked, out int portionsRemaining)
        {
            totalRemainingSeconds = 0f;
            secondsPerCooked = 0f;
            portionsRemaining = 0;

            string fishId = _isCooking ? GetActiveRawItemId() : _storedRawItemId;
            if (!CookingRecipes.TryGetForRaw(fishId, out CookingRecipe recipe))
                return false;

            secondsPerCooked = GetEffectiveDurationSeconds();
            int fish = StoredRawAmount;
            if (fish < recipe.RawPerCooked)
                return false;

            portionsRemaining = fish / recipe.RawPerCooked;
            if (portionsRemaining <= 0)
                return false;

            if (_isCooking)
            {
                float currentPortionRemaining = Mathf.Max(0f, secondsPerCooked - _cookProgressSeconds);
                totalRemainingSeconds = currentPortionRemaining + (portionsRemaining - 1) * secondsPerCooked;
            }
            else
            {
                totalRemainingSeconds = portionsRemaining * secondsPerCooked;
            }

            return true;
        }

        public bool TryActiveWork(out string failureReason) =>
            ProcessingProficiencyRuntime.EnsureInstance().TryApplyCookingActiveWork(this, out failureReason);

        public bool TryDepositRawFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason)
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

            if (!ValidateRawDeposit(slot.itemId, out failureReason))
                return false;

            int removed = inv.RemoveAmountAtSlot(slotIndex, deposit);
            if (removed <= 0)
            {
                failureReason = "Could not remove fish from inventory.";
                return false;
            }

            ApplyRawDeposit(slot.itemId, removed);
            return true;
        }

        public bool TryDepositRaw(string rawItemId, int amount, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrWhiteSpace(rawItemId) || amount <= 0)
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            if (!ValidateRawDeposit(rawItemId, out failureReason))
                return false;

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int available = inv.GetTotalAmount(rawItemId);
            if (available <= 0)
            {
                failureReason = "You do not have that fish.";
                return false;
            }

            int deposit = Mathf.Min(amount, available);
            if (!inv.Remove(rawItemId, deposit))
            {
                failureReason = "Could not remove fish from inventory.";
                return false;
            }

            ApplyRawDeposit(rawItemId, deposit);
            return true;
        }

        public bool TryDepositAllRawFromInventory(string rawItemId, out string failureReason)
        {
            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int available = inv.GetTotalAmount(rawItemId);
            return TryDepositRaw(rawItemId, available, out failureReason);
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

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Cooking", itemId, added);
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

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Cooking", itemId, added);
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public bool TryWithdrawAllRaw(out string failureReason)
        {
            failureReason = null;
            if (_storedRawAmount <= 0)
            {
                failureReason = "No fish stored.";
                return false;
            }

            if (_isCooking)
            {
                failureReason = "Stop cooking before removing fish.";
                return false;
            }

            if (_readyCookedAmount > 0)
            {
                failureReason = "Must collect cooked food first.";
                return false;
            }

            Inventory inv = Inventory.ResolvePlayer();
            if (!inv)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            string fishId = _storedRawItemId;
            int toReturn = _storedRawAmount;
            int before = inv.GetTotalAmount(fishId);
            inv.Add(fishId, toReturn, notifyItemGainPopup: false);
            int added = inv.GetTotalAmount(fishId) - before;
            if (added <= 0)
            {
                failureReason = "Inventory full.";
                return false;
            }

            _storedRawAmount -= added;
            if (_storedRawAmount <= 0)
            {
                _storedRawAmount = 0;
                _storedRawItemId = "";
                if (!_isCooking && _readyCookedAmount <= 0)
                {
                    _activeRawItemId = "";
                    _cookProgressSeconds = 0f;
                }
            }

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Cooking", fishId, added);
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public bool TryCollectCooked(int amount, out string failureReason)
        {
            failureReason = null;
            if (amount <= 0 || _readyCookedAmount <= 0)
            {
                failureReason = "Nothing ready to collect.";
                return false;
            }

            string cookedItemId = GetReadyCookedItemId();
            if (string.IsNullOrWhiteSpace(cookedItemId))
            {
                failureReason = "No cooked result configured.";
                return false;
            }

            Inventory inv = Inventory.ResolvePlayer();
            PlayerStorage storage = UnityEngine.Object.FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
            if (!inv && storage == null)
            {
                failureReason = "Inventory not found.";
                return false;
            }

            int collect = Mathf.Min(amount, _readyCookedAmount);
            int added = inv ? inv.AddPartial(cookedItemId, collect, notifyItemGainPopup: true) : 0;
            int left = collect - added;
            bool sentToStorage = false;
            if (left > 0 && storage != null)
            {
                int toStorage = storage.TryDepositAmountFromExternal(cookedItemId, left);
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

            _readyCookedAmount -= added;
            ClearStaleIdsWhenEmpty();

            if (sentToStorage)
            {
                GameLog.Add(
                    left > 0
                        ? "Inventory was full — sent some cooked food to storage (rest still on the range)."
                        : "Inventory was full — sent cooked food to storage.",
                    GameLog.CannotMessageColor);
            }

            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Cooking", cookedItemId, added);
            NotifyChanged();
            RequestSaveDebounced();
            return true;
        }

        public bool TickCooking(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || !_isCooking)
                return false;

            if (!TryGetActiveRecipe(out CookingRecipe recipe))
            {
                _isCooking = false;
                ClearPortionModifiers();
                NotifyChanged();
                return true;
            }

            if (!_fuelBank.HasFuel)
            {
                _isCooking = false;
                ClearPortionModifiers();
                NotifyChanged();
                return true;
            }

            bool wasCooking = _isCooking;
            bool structuralChange = false;
            float remaining = deltaSeconds;
            int burnsThisTick = 0;
            const int maxPortionsPerTick = 50;
            int portionsProcessed = 0;
            while (remaining > 0.0001f && _isCooking && portionsProcessed < maxPortionsPerTick)
            {
                if (_storedRawAmount < recipe.RawPerCooked)
                {
                    _isCooking = false;
                    ClearPortionModifiers();
                    structuralChange = true;
                    break;
                }

                float portionDuration = _hasLockedPortionModifiers
                    ? _lockedPortionDurationSeconds
                    : GetEffectiveDurationSeconds();
                if (portionDuration <= 0.0001f)
                    portionDuration = 0.0001f;

                float needed = portionDuration - _cookProgressSeconds;
                float step = remaining >= needed ? needed : remaining;

                // Burn fuel in lockstep with progress so partial fuel still advances the portion
                // and large deltas never burn more fuel than cook steps applied.
                float burned = _fuelBank.ConsumeUpTo(step);
                if (burned <= 0.0001f)
                {
                    _isCooking = false;
                    ClearPortionModifiers();
                    structuralChange = true;
                    break;
                }

                _cookProgressSeconds += burned;
                remaining -= burned;

                if (burned + 0.0001f < step)
                {
                    // Fuel ran out before the requested step finished.
                    _isCooking = false;
                    ClearPortionModifiers();
                    structuralChange = true;
                    break;
                }

                if (_cookProgressSeconds + 0.0001f < portionDuration)
                    break;

                _cookProgressSeconds = 0f;
                _storedRawAmount -= recipe.RawPerCooked;
                structuralChange = true;
                portionsProcessed++;
                ConsumeEnhancementForPortionAttempt();

                if (TryRollBurn(recipe))
                {
                    burnsThisTick++;
                    if (_storedRawAmount < recipe.RawPerCooked)
                    {
                        _isCooking = false;
                        ClearPortionModifiers();
                    }
                    else
                        LockPortionModifiers();

                    continue;
                }

                ProcessingProficiencyRuntime.EnsureInstance().AddCookingFishXp(recipe);
                _readyCookedAmount++;
                _readyCookedItemId = recipe.CookedItemId;

                if (_storedRawAmount < recipe.RawPerCooked)
                {
                    _isCooking = false;
                    ClearPortionModifiers();
                }
                else
                    LockPortionModifiers();
            }

            if (structuralChange)
            {
                if (burnsThisTick > 0)
                    LogBurnedFish(recipe, burnsThisTick);

                if (wasCooking && !_isCooking && _readyCookedAmount > 0)
                    LogCookingBatchComplete();

                ClearStaleIdsWhenEmpty();
                NotifyChanged();
            }

            return structuralChange;
        }

        private void LogCookingBatchComplete()
        {
            string cookedItemId = GetReadyCookedItemId();
            if (string.IsNullOrWhiteSpace(cookedItemId))
                return;

            int amount = _readyCookedAmount;
            string cookedLabel = ItemGainPopupNotifier.ResolveDisplayLabel(cookedItemId, amount);
            GameLog.Add(
                $"Cooking finished: {amount} {cookedLabel} ready to collect.",
                GameLog.QuestCompleteColor);
        }

        public void WriteInto(List<SaveData.CookingStationSave> target)
        {
            if (target == null)
                return;

            target.Add(new SaveData.CookingStationSave
            {
                stationId = _stationId,
                storedRawItemId = _storedRawItemId ?? "",
                storedRawAmount = Mathf.Max(0, _storedRawAmount),
                readyCookedAmount = Mathf.Max(0, _readyCookedAmount),
                readyCookedItemId = GetReadyCookedItemId(),
                storedEnhancementItemId = _storedEnhancementItemId ?? "",
                storedEnhancementAmount = Mathf.Max(0, _storedEnhancementAmount),
                storedFuelItemId = _fuelBank.StoredItemId,
                storedFuelAmount = _fuelBank.StoredAmount,
                fuelSecondsBurnedFromCurrentLog = _fuelBank.SecondsBurnedFromCurrentLog,
                activeRawItemId = _activeRawItemId ?? "",
                cookProgressSeconds = Mathf.Max(0f, _cookProgressSeconds),
                isCooking = _isCooking
            });
        }

        public void ReadFrom(SaveData.CookingStationSave row)
        {
            _storedRawItemId = row.storedRawItemId ?? "";
            _storedRawAmount = Mathf.Max(0, row.storedRawAmount);
            _readyCookedAmount = Mathf.Max(0, row.readyCookedAmount);
            _readyCookedItemId = row.readyCookedItemId ?? "";
            _storedEnhancementItemId = row.storedEnhancementItemId ?? "";
            _storedEnhancementAmount = Mathf.Max(0, row.storedEnhancementAmount);
            _fuelBank.Load(
                row.storedFuelItemId ?? "",
                row.storedFuelAmount,
                row.fuelSecondsBurnedFromCurrentLog);
            _activeRawItemId = row.activeRawItemId ?? "";
            _cookProgressSeconds = Mathf.Max(0f, row.cookProgressSeconds);
            _isCooking = row.isCooking;
            NormalizeStateAfterLoad();
            NotifyChanged();
        }

        public void ResetToEmpty()
        {
            ReadFrom(new SaveData.CookingStationSave { stationId = _stationId });
        }

        private void NormalizeStateAfterLoad()
        {
            if (_readyCookedAmount > 0 && string.IsNullOrWhiteSpace(_readyCookedItemId))
            {
                if (CookingRecipes.TryGetForRaw(_activeRawItemId, out CookingRecipe recipe) ||
                    CookingRecipes.TryGetForRaw(_storedRawItemId, out recipe))
                {
                    _readyCookedItemId = recipe.CookedItemId;
                }
                else
                {
                    // Unresolvable ready food softlocks withdraw/deposit; drop corrupt amount.
                    Debug.LogWarning(
                        $"[Cooking] Dropping corrupt ready food on '{_stationId}' with no resolvable item id.");
                    _readyCookedAmount = 0;
                    _readyCookedItemId = "";
                }
            }

            if (_isCooking &&
                (!_fuelBank.HasFuel ||
                 !TryGetActiveRecipe(out CookingRecipe activeRecipe) ||
                 _storedRawAmount < activeRecipe.RawPerCooked))
            {
                _isCooking = false;
                ClearPortionModifiers();
            }
            else if (_isCooking)
                LockPortionModifiers();

            ClearStaleIdsWhenEmpty();
        }

        private void ClearStaleIdsWhenEmpty()
        {
            if (_storedRawAmount <= 0)
            {
                _storedRawAmount = 0;
                _storedRawItemId = "";
            }

            if (_readyCookedAmount <= 0)
            {
                _readyCookedAmount = 0;
                _readyCookedItemId = "";
            }

            if (_storedEnhancementAmount <= 0)
            {
                _storedEnhancementAmount = 0;
                _storedEnhancementItemId = "";
            }

            if (!_isCooking && _storedRawAmount <= 0 && _readyCookedAmount <= 0)
            {
                _activeRawItemId = "";
                _cookProgressSeconds = 0f;
            }
        }

        private string GetReadyCookedItemId()
        {
            if (!string.IsNullOrWhiteSpace(_readyCookedItemId))
                return _readyCookedItemId;

            if (TryGetActiveRecipe(out CookingRecipe recipe))
                return recipe.CookedItemId;

            if (CookingRecipes.TryGetForRaw(_storedRawItemId, out recipe))
                return recipe.CookedItemId;

            return "";
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

            if (def.ProcessingSkillTarget != ProcessingSkillTarget.Cooking)
            {
                failureReason = "That enhancement is for smelting, not cooking.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_storedEnhancementItemId) &&
                !string.Equals(_storedEnhancementItemId, itemId, StringComparison.OrdinalIgnoreCase) &&
                _storedEnhancementAmount > 0)
            {
                failureReason = "This range already holds a different enhancement.";
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
            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Cooking", itemId.Trim().ToLowerInvariant(), -deposited);
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
                "Cooking",
                itemId.Trim().ToLowerInvariant(),
                -deposited);
            NotifyChanged();
            RequestSaveDebounced();
        }

        private void ConsumeEnhancementForPortionAttempt()
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

        private float GetEnhancementBurnChanceReductionPercent()
        {
            ItemDefinition def = ResolveStoredEnhancementDefinition();
            return def != null ? def.ProcessingBurnChanceReductionPercent : 0f;
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

        private bool ValidateRawDeposit(string rawItemId, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrWhiteSpace(rawItemId))
            {
                failureReason = "Invalid deposit.";
                return false;
            }

            if (!CookingRecipes.TryGetForRaw(rawItemId, out _))
            {
                failureReason = "That fish cannot be cooked here.";
                return false;
            }

            ItemDefinition def = ResolveItemDefinition(rawItemId);
            if (def != null && def.RequiredCookingLevel > 0)
            {
                int cookingLevel = ProcessingProficiencyRuntime.EnsureInstance().GetLevel(ProcessingSkillType.Cooking);
                if (cookingLevel < def.RequiredCookingLevel)
                {
                    failureReason = $"Requires Cooking level {def.RequiredCookingLevel}.";
                    return false;
                }
            }

            if (_readyCookedAmount > 0)
            {
                failureReason = "Collect cooked food before adding fish.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_storedRawItemId) &&
                !string.Equals(_storedRawItemId, rawItemId, StringComparison.OrdinalIgnoreCase) &&
                _storedRawAmount > 0)
            {
                failureReason = "This range already holds a different fish type.";
                return false;
            }

            return true;
        }

        private void ApplyRawDeposit(string rawItemId, int deposited)
        {
            if (deposited <= 0)
                return;

            if (string.IsNullOrWhiteSpace(_storedRawItemId))
                _storedRawItemId = rawItemId.Trim().ToLowerInvariant();

            _storedRawAmount += deposited;
            SessionTrackerData.EnsureInstance()?.RegisterLootChange("Cooking", rawItemId.Trim().ToLowerInvariant(), -deposited);
            NotifyChanged();
            RequestSaveDebounced();
        }

        private bool TryRollBurn(CookingRecipe recipe)
        {
            float burnChance = _hasLockedPortionModifiers
                ? _lockedPortionBurnChancePercent
                : GetEffectiveBurnChancePercent();
            if (burnChance <= 0f)
                return false;

            return UnityEngine.Random.value * 100f < burnChance;
        }

        private void LockPortionModifiers()
        {
            _lockedPortionDurationSeconds = GetEffectiveDurationSeconds();
            _lockedPortionBurnChancePercent = GetEffectiveBurnChancePercent();
            _hasLockedPortionModifiers = true;
        }

        private void ClearPortionModifiers() => _hasLockedPortionModifiers = false;

        private static void LogBurnedFish(CookingRecipe recipe, int amount)
        {
            if (amount <= 0 || !CookingUI.ShouldLogBurnMessages)
                return;

            string fishLabel = ItemGainPopupNotifier.ResolveDisplayLabel(recipe.RawItemId, amount);
            string message = amount == 1
                ? $"Burned {fishLabel}."
                : $"Burned {amount} {fishLabel}.";
            GameLog.Add(message, GameLog.ItemLostColor);
        }

        private void NotifyChanged() => StateChanged?.Invoke();
    }
}
