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
        if (amount <= 0) return;

        long sum = (long)gold + amount;
        if (sum > GoldSoftCeiling)
            gold = GoldSoftCeiling;
        else
            gold = (int)sum;

        OnGoldChanged?.Invoke();
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
        return gold >= amount;
    }
}
