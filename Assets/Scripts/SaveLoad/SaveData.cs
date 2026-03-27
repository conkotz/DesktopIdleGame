using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    [Header("Meta")]
    public int version = 2;
    public long savedAtUnix;

    [Header("Player")]
    public string playerName = "Adventurer";
    public int playerLevel = 1;
    public int xp = 0;
    public float playerCurrentHP = -1f;
    public float playerCurrentEnergy = -1f;
    public float playerCurrentMana = -1f;

    [Header("Currency")]
    public int gold = 0;

    [Header("Inventory")]
    public int inventorySlotCount = 32;
    public List<InventorySlotData> inventorySlots = new();

    [Serializable]
    public struct InventorySlotData
    {
        public string itemId;
        public int amount;
    }

    [Header("Equipment")]
    public string equippedMainHand1ItemId;
    public string equippedOffHand1ItemId;
    public int equippedOffHand1StackAmount;

    public string equippedMainHand2ItemId;
    public string equippedOffHand2ItemId;
    public int equippedOffHand2StackAmount;

    public int activeWeaponSetIndex;

    public string equippedHelmetItemId;
    public string equippedBodyItemId;
    public string equippedBootsItemId;

    public string equippedTrinketItemId;
    public string equippedPendantItemId;
    public string equippedRing1ItemId;
    public string equippedRing2ItemId;

    [Header("Toolbelt")]
    public List<string> toolbeltItemIds = new();

    [Header("Skills")]
    public List<SkillSave> skills = new();
    public SkillType lastXpSkill = SkillType.Mining;
    public string lastXpSource = "";

    [Serializable]
    public struct SkillSave
    {
        public SkillType type;
        public int level;
        public int xp;
    }

    [Header("Action Bar")]
    public List<int> actionBarSlotIndexes = new();
    public List<int> actionBarKinds = new();
    public List<string> actionBarIds = new();

    [Header("Merchant Stock")]
    public List<MerchantStockSave> merchantStocks = new();

    [Serializable]
    public class MerchantStockSave
    {
        public string merchantId;
        public List<int> quantities = new();
    }
}