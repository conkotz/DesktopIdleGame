using System;
using UnityEngine;

/// <summary>
/// Scene furnace binding. Smelting state and ticking live on <see cref="FurnaceSmeltingRuntime"/> so progress continues on other maps.
/// </summary>
[DisallowMultipleComponent]
public class FurnaceSmelter : MonoBehaviour
{
    public event Action StateChanged;

    [Header("Identity")]
    [SerializeField] private string furnaceId = "furnace";

    private FurnaceSmeltingRuntime.FurnaceRow _row;

    public string FurnaceId => string.IsNullOrWhiteSpace(furnaceId) ? "furnace" : furnaceId.Trim();
    public string StoredOreItemId => _row != null ? _row.StoredOreItemId : "";
    public int StoredOreAmount => _row != null ? _row.StoredOreAmount : 0;
    public string StoredEnhancementItemId => _row != null ? _row.StoredEnhancementItemId : "";
    public int StoredEnhancementAmount => _row != null ? _row.StoredEnhancementAmount : 0;
    public string StoredFuelItemId => _row != null ? _row.StoredFuelItemId : "";
    public int StoredFuelAmount => _row != null ? _row.StoredFuelAmount : 0;
    public bool HasFuel => _row != null && _row.HasFuel;
    public float FuelSecondsRemaining => _row != null ? _row.FuelSecondsRemaining : 0f;
    public int ReadyBarAmount => _row != null ? _row.ReadyBarAmount : 0;
    public string ReadyBarItemId => _row != null ? _row.ReadyBarItemId : "";
    public bool IsSmelting => _row != null && _row.IsSmelting;
    public float SmeltProgressSeconds => _row != null ? _row.SmeltProgressSeconds : 0f;
    public bool HasPendingBars => ReadyBarAmount > 0;

    public bool HasActiveRecipe => _row != null && _row.TryGetActiveRecipe(out _);

    public string GetActiveOreItemId() => _row != null ? _row.GetActiveOreItemId() : "";

    public bool TryGetActiveRecipe(out SmeltingRecipe recipe)
    {
        recipe = default;
        return _row != null && _row.TryGetActiveRecipe(out recipe);
    }

    public float GetActiveDurationSeconds() => _row != null ? _row.GetActiveDurationSeconds() : 1f;

    public float GetEffectiveDurationSeconds() => _row != null ? _row.GetEffectiveDurationSeconds() : 1f;

    public bool TryGetSmeltTimeEstimate(out float totalRemainingSeconds, out float secondsPerBar, out int barsRemaining)
    {
        if (_row != null)
            return _row.TryGetSmeltTimeEstimate(out totalRemainingSeconds, out secondsPerBar, out barsRemaining);

        totalRemainingSeconds = 0f;
        secondsPerBar = 0f;
        barsRemaining = 0;
        return false;
    }

    public int GetOrePerBar() => _row != null ? _row.GetOrePerBar() : SmeltingRecipes.DefaultOrePerBar;

    public bool CanStartSmelting() => _row != null && _row.CanStartSmelting();

    public bool TryStartSmelting() => _row != null && _row.TryStartSmelting();

    public void StopSmelting() => _row?.StopSmelting();

    public bool TryDepositOreFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositOreFromInventorySlot(inv, slotIndex, amount, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryDepositOre(string oreItemId, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositOre(oreItemId, amount, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryDepositAllOreFromInventory(string oreItemId, out string failureReason) =>
        _row != null
            ? _row.TryDepositAllOreFromInventory(oreItemId, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryDepositEnhancementFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositEnhancementFromInventorySlot(inv, slotIndex, amount, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryDepositEnhancement(string itemId, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositEnhancement(itemId, amount, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryDepositAllEnhancementFromInventory(string itemId, out string failureReason) =>
        _row != null
            ? _row.TryDepositAllEnhancementFromInventory(itemId, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryDepositFuelFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositFuelFromInventorySlot(inv, slotIndex, amount, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryDepositFuel(string itemId, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositFuel(itemId, amount, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryDepositAllFuelFromInventory(string itemId, out string failureReason) =>
        _row != null
            ? _row.TryDepositAllFuelFromInventory(itemId, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryWithdrawAllFuel(out string failureReason) =>
        _row != null
            ? _row.TryWithdrawAllFuel(out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryWithdrawAllEnhancement(out string failureReason) =>
        _row != null
            ? _row.TryWithdrawAllEnhancement(out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryWithdrawAllOre(out string failureReason) =>
        _row != null
            ? _row.TryWithdrawAllOre(out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryCollectBars(int amount, out string failureReason) =>
        _row != null
            ? _row.TryCollectBars(amount, out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    public bool TryActiveWork(out string failureReason) =>
        _row != null
            ? _row.TryActiveWork(out failureReason)
            : Fail(out failureReason, "Furnace not ready.");

    private void Awake()
    {
        BindRow();
    }

    private void OnDestroy()
    {
        UnbindRow();
    }

    private void BindRow()
    {
        FurnaceSmeltingRuntime.FurnaceRow row = FurnaceSmeltingRuntime.EnsureInstance().GetOrCreateRow(FurnaceId);
        if (_row == row)
            return;

        UnbindRow();
        _row = row;
        _row.StateChanged += HandleRowStateChanged;
    }

    private void UnbindRow()
    {
        if (_row == null)
            return;

        _row.StateChanged -= HandleRowStateChanged;
        _row = null;
    }

    private void HandleRowStateChanged() => StateChanged?.Invoke();

    private static bool Fail(out string failureReason, string message)
    {
        failureReason = message;
        return false;
    }
}
