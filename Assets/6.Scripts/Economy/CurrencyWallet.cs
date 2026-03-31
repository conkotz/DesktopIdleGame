using System;
using UnityEngine;

public class CurrencyWallet : MonoBehaviour, ISaveable
{
    [SerializeField] private int gold;
    public int Gold => gold;

    public event Action OnGoldChanged;

    public void SetGold(int amount)
    {
        gold = Mathf.Max(0, amount);
        OnGoldChanged?.Invoke();
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
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
        gold = Mathf.Max(0, data.gold);
        OnGoldChanged?.Invoke();
    }

    public bool CanAfford(int amount)
    {
        return gold >= amount;
    }
}