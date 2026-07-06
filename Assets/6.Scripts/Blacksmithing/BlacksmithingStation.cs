using System;
using UnityEngine;

/// <summary>
/// Scene blacksmithing anvil binding. State and ticking live on <see cref="BlacksmithingRuntime"/>.
/// </summary>
[DisallowMultipleComponent]
public class BlacksmithingStation : MonoBehaviour
{
    public event Action StateChanged;

    [Header("Identity")]
    [SerializeField] private string stationId = "blacksmithing_anvil";

    private BlacksmithingRuntime.BlacksmithingRow _row;

    public string StationId => string.IsNullOrWhiteSpace(stationId) ? "blacksmithing_anvil" : stationId.Trim();
    public string SelectedRecipeOutputId => _row != null ? _row.SelectedRecipeOutputId : "";
    public string ActiveRecipeOutputId => _row != null ? _row.ActiveRecipeOutputId : "";
    public string ReadyOutputItemId => _row != null ? _row.ReadyOutputItemId : "";
    public bool HasReadyOutput => _row != null && _row.HasReadyOutput;
    public bool IsCrafting => _row != null && _row.IsCrafting;
    public float CraftProgressSeconds => _row != null ? _row.CraftProgressSeconds : 0f;

    public bool TryGetSelectedRecipe(out BlacksmithingRecipe recipe)
    {
        if (_row != null)
            return _row.TryGetSelectedRecipe(out recipe);

        recipe = default;
        return false;
    }

    public bool TryGetActiveRecipe(out BlacksmithingRecipe recipe)
    {
        if (_row != null)
            return _row.TryGetActiveRecipe(out recipe);

        recipe = default;
        return false;
    }

    public float GetActiveDurationSeconds() => _row != null ? _row.GetActiveDurationSeconds() : 1f;

    public void SelectRecipe(string outputItemId) => _row?.SelectRecipe(outputItemId);

    public bool CanStartCrafting(out string failureReason) =>
        _row != null
            ? _row.CanStartCrafting(out failureReason)
            : Fail(out failureReason, "Anvil not ready.");

    public bool TryStartCrafting(out string failureReason) =>
        _row != null
            ? _row.TryStartCrafting(out failureReason)
            : Fail(out failureReason, "Anvil not ready.");

    public void StopCrafting() => _row?.StopCrafting();

    public bool CanCollectOutput(out string failureReason) =>
        _row != null
            ? _row.CanCollectOutput(out failureReason)
            : Fail(out failureReason, "Anvil not ready.");

    public bool TryCollectOutput(out string failureReason) =>
        _row != null
            ? _row.TryCollectOutput(out failureReason)
            : Fail(out failureReason, "Anvil not ready.");

    public bool TryActiveWork(out string failureReason) =>
        _row != null
            ? _row.TryActiveWork(out failureReason)
            : Fail(out failureReason, "Anvil not ready.");

    public bool PlayerHasIngredientsForSelected(out string failureReason) =>
        _row != null
            ? _row.PlayerHasIngredientsForSelected(out failureReason)
            : Fail(out failureReason, "Anvil not ready.");

    private void Awake() => BindRow();

    private void OnDestroy() => UnbindRow();

    private void BindRow()
    {
        BlacksmithingRuntime.BlacksmithingRow row = BlacksmithingRuntime.EnsureInstance().GetOrCreateRow(StationId);
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
