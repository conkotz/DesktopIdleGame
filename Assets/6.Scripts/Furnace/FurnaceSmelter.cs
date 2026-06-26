using System;
using UnityEngine;

/// <summary>
/// Per-furnace smelting state. Smelting pauses while logged out; progress and contents restore on load.
/// </summary>
[DisallowMultipleComponent]
public class FurnaceSmelter : MonoBehaviour, ISaveable
{
    public event Action StateChanged;

    [Header("Identity")]
    [SerializeField] private string furnaceId = "furnace";

    [Header("Refs (optional)")]
    [SerializeField] private Inventory inventory;

    private string _storedOreItemId = "";
    private int _storedOreAmount;
    private int _readyBarAmount;
    private string _readyBarItemId = "";
    private string _activeOreItemId = "";
    private float _smeltProgressSeconds;
    private bool _isSmelting;
    private bool _loadedFromSave;
    private float _nextPeriodicSaveUnscaled;
    private const float PeriodicSaveIntervalSeconds = 5f;

    public string FurnaceId => string.IsNullOrWhiteSpace(furnaceId) ? "furnace" : furnaceId.Trim();
    public string StoredOreItemId => _storedOreItemId ?? "";
    public int StoredOreAmount => Mathf.Max(0, _storedOreAmount);
    public int ReadyBarAmount => Mathf.Max(0, _readyBarAmount);
    public string ReadyBarItemId => _readyBarItemId ?? "";
    public bool IsSmelting => _isSmelting;
    public float SmeltProgressSeconds => Mathf.Max(0f, _smeltProgressSeconds);
    public bool HasPendingBars => _readyBarAmount > 0;

    public bool HasActiveRecipe => SmeltingRecipes.TryGetForOre(GetActiveOreItemId(), out _);

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
        return TryGetActiveRecipe(out SmeltingRecipe recipe) ? recipe.SecondsPerBar : 1f;
    }

    /// <summary>
    /// Estimated wall time to smelt all stored ore into bars (includes in-progress bar when smelting).
    /// </summary>
    public bool TryGetSmeltTimeEstimate(out float totalRemainingSeconds, out float secondsPerBar, out int barsRemaining)
    {
        totalRemainingSeconds = 0f;
        secondsPerBar = 0f;
        barsRemaining = 0;

        string oreId = _isSmelting ? GetActiveOreItemId() : _storedOreItemId;
        if (!SmeltingRecipes.TryGetForOre(oreId, out SmeltingRecipe recipe))
            return false;

        secondsPerBar = recipe.SecondsPerBar;
        int ore = StoredOreAmount;
        if (ore < recipe.OrePerBar)
            return false;

        barsRemaining = ore / recipe.OrePerBar;
        if (barsRemaining <= 0)
            return false;

        if (_isSmelting)
        {
            float currentBarRemaining = Mathf.Max(0f, recipe.SecondsPerBar - _smeltProgressSeconds);
            totalRemainingSeconds = currentBarRemaining + (barsRemaining - 1) * recipe.SecondsPerBar;
        }
        else
        {
            totalRemainingSeconds = barsRemaining * recipe.SecondsPerBar;
        }

        return true;
    }

    public int GetOrePerBar()
    {
        return TryGetActiveRecipe(out SmeltingRecipe recipe) ? recipe.OrePerBar : SmeltingRecipes.DefaultOrePerBar;
    }

    public bool CanStartSmelting()
    {
        if (_isSmelting)
            return false;
        if (_readyBarAmount > 0)
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
        _nextPeriodicSaveUnscaled = Time.unscaledTime + PeriodicSaveIntervalSeconds;
        NotifyChanged();
        RequestSaveImmediate();
        return true;
    }

    public void StopSmelting()
    {
        if (!_isSmelting)
            return;

        _isSmelting = false;
        NotifyChanged();
        RequestSaveImmediate();
    }

    /// <summary>Deposit ore from a specific inventory slot (drag-drop / double-click).</summary>
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

    /// <summary>Deposit ore from player inventory into the furnace (single ore type per furnace).</summary>
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

        Inventory inv = ResolveInventory();
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
        Inventory inv = ResolveInventory();
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

        Inventory inv = ResolveInventory();
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

        NotifyChanged();
        RequestSaveImmediate();
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

        Inventory inv = ResolveInventory();
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
        if (_readyBarAmount <= 0)
        {
            _readyBarAmount = 0;
            _readyBarItemId = "";
            if (!_isSmelting && _storedOreAmount <= 0)
            {
                _activeOreItemId = "";
                _smeltProgressSeconds = 0f;
            }
        }

        NotifyChanged();
        RequestSaveImmediate();
        return true;
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
        NotifyChanged();
        RequestSaveImmediate();
    }

    private void Awake()
    {
        if (!inventory)
            inventory = Inventory.ResolvePlayer();
    }

    private void Start()
    {
        if (_loadedFromSave)
            return;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.TryGetLastLoadedData(out SaveData data) &&
            data != null)
        {
            LoadFrom(data);
        }
    }

    private void Update()
    {
        if (!_isSmelting)
            return;

        TickSmelting(Time.deltaTime);
        MaybePeriodicSave();
    }

    private void OnApplicationQuit()
    {
        if (_isSmelting || _storedOreAmount > 0 || _readyBarAmount > 0)
            RequestSaveImmediate();
    }

    private void TickSmelting(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || !_isSmelting)
            return;

        if (!TryGetActiveRecipe(out SmeltingRecipe recipe))
        {
            _isSmelting = false;
            NotifyChanged();
            RequestSaveImmediate();
            return;
        }

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

            float needed = recipe.SecondsPerBar - _smeltProgressSeconds;
            if (remaining >= needed)
            {
                remaining -= needed;
                _smeltProgressSeconds = 0f;
                _storedOreAmount -= recipe.OrePerBar;
                _readyBarAmount++;
                _readyBarItemId = recipe.BarItemId;
                structuralChange = true;

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
            NotifyChanged();
            RequestSaveImmediate();
        }
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        data.furnaceSmelters ??= new System.Collections.Generic.List<SaveData.FurnaceSmelterSave>();
        SaveData.FurnaceSmelterSave row = FindOrCreateRow(data);
        row.furnaceId = FurnaceId;
        row.storedOreItemId = _storedOreItemId ?? "";
        row.storedOreAmount = Mathf.Max(0, _storedOreAmount);
        row.readyBarAmount = Mathf.Max(0, _readyBarAmount);
        row.readyBarItemId = GetReadyBarItemId();
        row.activeOreItemId = _activeOreItemId ?? "";
        row.smeltProgressSeconds = Mathf.Max(0f, _smeltProgressSeconds);
        row.isSmelting = _isSmelting;
    }

    public void LoadFrom(SaveData data)
    {
        _loadedFromSave = true;

        if (data == null)
            return;

        ResetRuntimeState();

        if (data.furnaceSmelters == null)
            return;

        for (int i = 0; i < data.furnaceSmelters.Count; i++)
        {
            SaveData.FurnaceSmelterSave row = data.furnaceSmelters[i];
            if (row == null || !string.Equals(row.furnaceId, FurnaceId, StringComparison.OrdinalIgnoreCase))
                continue;

            _storedOreItemId = row.storedOreItemId ?? "";
            _storedOreAmount = Mathf.Max(0, row.storedOreAmount);
            _readyBarAmount = Mathf.Max(0, row.readyBarAmount);
            _readyBarItemId = row.readyBarItemId ?? "";
            _activeOreItemId = row.activeOreItemId ?? "";
            _smeltProgressSeconds = Mathf.Max(0f, row.smeltProgressSeconds);
            _isSmelting = row.isSmelting;
            break;
        }

        NormalizeStateAfterLoad();
        _nextPeriodicSaveUnscaled = Time.unscaledTime + PeriodicSaveIntervalSeconds;
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

        if (!_isSmelting)
            return;

        if (!TryGetActiveRecipe(out SmeltingRecipe activeRecipe) || _storedOreAmount < activeRecipe.OrePerBar)
            _isSmelting = false;
    }

    private SaveData.FurnaceSmelterSave FindOrCreateRow(SaveData data)
    {
        for (int i = 0; i < data.furnaceSmelters.Count; i++)
        {
            SaveData.FurnaceSmelterSave row = data.furnaceSmelters[i];
            if (row != null && string.Equals(row.furnaceId, FurnaceId, StringComparison.OrdinalIgnoreCase))
                return row;
        }

        var created = new SaveData.FurnaceSmelterSave { furnaceId = FurnaceId };
        data.furnaceSmelters.Add(created);
        return created;
    }

    private void ResetRuntimeState()
    {
        _storedOreItemId = "";
        _storedOreAmount = 0;
        _readyBarAmount = 0;
        _readyBarItemId = "";
        _activeOreItemId = "";
        _smeltProgressSeconds = 0f;
        _isSmelting = false;
    }

    private Inventory ResolveInventory()
    {
        if (!inventory)
            inventory = Inventory.ResolvePlayer();
        return inventory;
    }

    private void NotifyChanged() => StateChanged?.Invoke();

    private void MaybePeriodicSave()
    {
        if (Time.unscaledTime < _nextPeriodicSaveUnscaled)
            return;

        _nextPeriodicSaveUnscaled = Time.unscaledTime + PeriodicSaveIntervalSeconds;
        RequestSaveImmediate();
    }

    private static void RequestSaveImmediate()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.RequestSave(SaveManager.SaveRequestKind.InventoryChanged);
    }
}
