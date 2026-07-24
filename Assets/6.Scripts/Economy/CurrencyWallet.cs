using System;
using UnityEngine;

public class CurrencyWallet : MonoBehaviour, ISaveable
{
    /// <summary>Matches <see cref="SaveDataIntegrity"/> soft ceiling so runtime gold cannot wrap int.</summary>
    public const int GoldSoftCeiling = 500_000_000;

    [SerializeField] private int gold;
    public int Gold => gold;

    public event Action OnGoldChanged;

    public void SetGold(int amount)
    {
        gold = Mathf.Clamp(amount, 0, GoldSoftCeiling);
        OnGoldChanged?.Invoke();
    }

    public void AddGold(int amount)
    {
        AddGoldReturningApplied(amount);
    }

    /// <summary>
    /// Adds gold and returns how much was actually applied after the soft ceiling.
    /// Sell/undo paths must record this value — not the requested amount — or undo can
    /// charge more than was granted and then discard the undo row on SpendGold failure.
    /// </summary>
    public int AddGoldReturningApplied(int amount)
    {
        if (amount <= 0) return 0;

        int before = gold;
        long sum = (long)gold + amount;
        if (sum > GoldSoftCeiling)
            gold = GoldSoftCeiling;
        else
            gold = (int)sum;

        if (gold != before)
            OnGoldChanged?.Invoke();

        return gold - before;
    }

    /// <summary>
    /// Multiplies unit value by stack size without int wrap. Result is clamped to
    /// <see cref="GoldSoftCeiling"/> so sell paths never feed a non-positive (wrapped) amount into <see cref="AddGold"/>.
    /// </summary>
    public static int ComputeClampedSaleGold(int valuePerItem, int amount)
    {
        if (valuePerItem <= 0 || amount <= 0)
            return 0;

        long product = (long)valuePerItem * amount;
        if (product >= GoldSoftCeiling)
            return GoldSoftCeiling;
        return (int)product;
    }

    /// <summary>Adds a sale gold product into an accumulator without int wrap.</summary>
    public static int AccumulateClampedSaleGold(int currentTotal, int valuePerItem, int amount)
    {
        if (currentTotal >= GoldSoftCeiling)
            return GoldSoftCeiling;

        long next = (long)Mathf.Max(0, currentTotal) + ComputeClampedSaleGold(valuePerItem, amount);
        if (next >= GoldSoftCeiling)
            return GoldSoftCeiling;
        return (int)next;
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (gold < amount) return false;
        gold -= amount;
        OnGoldChanged?.Invoke();
        return true;
    }

    public void SaveInto(SaveData data) => data.gold = gold;

    public void LoadFrom(SaveData data)
    {
        gold = Mathf.Clamp(Mathf.Max(0, data.gold), 0, GoldSoftCeiling);
        OnGoldChanged?.Invoke();
    }

    public bool CanAfford(int amount)
    {
        // Negative (wrapped) costs must never look affordable — SpendGold(<=0) returns true.
        if (amount <= 0) return amount == 0;
        return gold >= amount;
    }
}
