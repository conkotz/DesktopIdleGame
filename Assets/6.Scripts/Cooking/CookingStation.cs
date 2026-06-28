using System;
using UnityEngine;

/// <summary>
/// Scene cooking range binding. Cooking state and ticking live on <see cref="CookingRuntime"/> so progress continues on other maps.
/// </summary>
[DisallowMultipleComponent]
public class CookingStation : MonoBehaviour
{
    public event Action StateChanged;

    [Header("Identity")]
    [SerializeField] private string stationId = "cooking_range";

    private CookingRuntime.CookingRow _row;

    public string StationId => string.IsNullOrWhiteSpace(stationId) ? "cooking_range" : stationId.Trim();
    public string StoredRawItemId => _row != null ? _row.StoredRawItemId : "";
    public int StoredRawAmount => _row != null ? _row.StoredRawAmount : 0;
    public string StoredEnhancementItemId => _row != null ? _row.StoredEnhancementItemId : "";
    public int StoredEnhancementAmount => _row != null ? _row.StoredEnhancementAmount : 0;
    public string StoredFuelItemId => _row != null ? _row.StoredFuelItemId : "";
    public int StoredFuelAmount => _row != null ? _row.StoredFuelAmount : 0;
    public bool HasFuel => _row != null && _row.HasFuel;
    public float FuelSecondsRemaining => _row != null ? _row.FuelSecondsRemaining : 0f;
    public int ReadyCookedAmount => _row != null ? _row.ReadyCookedAmount : 0;
    public string ReadyCookedItemId => _row != null ? _row.ReadyCookedItemId : "";
    public bool IsCooking => _row != null && _row.IsCooking;
    public float CookProgressSeconds => _row != null ? _row.CookProgressSeconds : 0f;
    public bool HasPendingCooked => ReadyCookedAmount > 0;

    public bool HasActiveRecipe => _row != null && _row.TryGetActiveRecipe(out _);

    public string GetActiveRawItemId() => _row != null ? _row.GetActiveRawItemId() : "";

    public bool TryGetActiveRecipe(out CookingRecipe recipe)
    {
        recipe = default;
        return _row != null && _row.TryGetActiveRecipe(out recipe);
    }

    public float GetActiveDurationSeconds() => _row != null ? _row.GetActiveDurationSeconds() : 1f;

    public float GetEffectiveDurationSeconds() => _row != null ? _row.GetEffectiveDurationSeconds() : 1f;

    public float GetEffectiveBurnChancePercent() => _row != null ? _row.GetEffectiveBurnChancePercent() : CookingProficiencyBonuses.BaseBurnChancePercent;

    public bool TryGetCookTimeEstimate(out float totalRemainingSeconds, out float secondsPerCooked, out int portionsRemaining)
    {
        if (_row != null)
            return _row.TryGetCookTimeEstimate(out totalRemainingSeconds, out secondsPerCooked, out portionsRemaining);

        totalRemainingSeconds = 0f;
        secondsPerCooked = 0f;
        portionsRemaining = 0;
        return false;
    }

    public int GetRawPerCooked() => _row != null ? _row.GetRawPerCooked() : CookingRecipes.DefaultRawPerCooked;

    public bool CanStartCooking() => _row != null && _row.CanStartCooking();

    public bool TryStartCooking() => _row != null && _row.TryStartCooking();

    public void StopCooking() => _row?.StopCooking();

    public bool TryDepositRawFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositRawFromInventorySlot(inv, slotIndex, amount, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryDepositRaw(string rawItemId, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositRaw(rawItemId, amount, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryDepositAllRawFromInventory(string rawItemId, out string failureReason) =>
        _row != null
            ? _row.TryDepositAllRawFromInventory(rawItemId, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryDepositEnhancementFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositEnhancementFromInventorySlot(inv, slotIndex, amount, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryDepositEnhancement(string itemId, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositEnhancement(itemId, amount, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryDepositAllEnhancementFromInventory(string itemId, out string failureReason) =>
        _row != null
            ? _row.TryDepositAllEnhancementFromInventory(itemId, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryDepositFuelFromInventorySlot(Inventory inv, int slotIndex, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositFuelFromInventorySlot(inv, slotIndex, amount, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryDepositFuel(string itemId, int amount, out string failureReason) =>
        _row != null
            ? _row.TryDepositFuel(itemId, amount, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryDepositAllFuelFromInventory(string itemId, out string failureReason) =>
        _row != null
            ? _row.TryDepositAllFuelFromInventory(itemId, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryWithdrawAllFuel(out string failureReason) =>
        _row != null
            ? _row.TryWithdrawAllFuel(out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryWithdrawAllEnhancement(out string failureReason) =>
        _row != null
            ? _row.TryWithdrawAllEnhancement(out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryWithdrawAllFish(out string failureReason) =>
        _row != null
            ? _row.TryWithdrawAllRaw(out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryCollectCooked(int amount, out string failureReason) =>
        _row != null
            ? _row.TryCollectCooked(amount, out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

    public bool TryActiveWork(out string failureReason) =>
        _row != null
            ? _row.TryActiveWork(out failureReason)
            : Fail(out failureReason, "Cooking range not ready.");

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
        CookingRuntime.CookingRow row = CookingRuntime.EnsureInstance().GetOrCreateRow(StationId);
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
