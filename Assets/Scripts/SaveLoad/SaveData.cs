using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int inventorySlotCount = 32;
    public List<InventorySlotData> inventorySlots = new List<InventorySlotData>();

    [Serializable]
    public struct InventorySlotData
    {
        public string itemId;
        public int amount;
    }

    public int version = 2;
    public long savedAtUnix;
    public int gold = 0;

    // Example player / meta
    public int playerLevel = 1;
    public int xp = 0;

    // Example “resources” (wood/stone/etc)
    public List<ResourceAmount> resources = new List<ResourceAmount>();

    // Example upgrades
    public List<string> unlockedUpgrades = new List<string>();

    // --- Equipment / Weapon Sets ---
    public string equippedMainHand1ItemId = null;
    public string equippedOffHand1ItemId = null;
    public int equippedOffHand1StackAmount = 0;

    public string equippedMainHand2ItemId = null;
    public string equippedOffHand2ItemId = null;
    public int equippedOffHand2StackAmount = 0;

    public int activeWeaponSetIndex = 0;

    // --- Armor / Accessories ---
    public string equippedHelmetItemId;
    public string equippedBodyItemId;
    public string equippedBootsItemId;

    public string equippedTrinketItemId;
    public string equippedPendantItemId;
    public string equippedRing1ItemId;
    public string equippedRing2ItemId;

    // --- Action Bar ---
    public List<int> actionBarSlotIndexes = new List<int>();
    public List<int> actionBarKinds = new List<int>();
    public List<string> actionBarIds = new List<string>();

    // --- Toolbelt ---
    public List<string> toolbeltItemIds = new List<string>() { null, null, null, null };

    // Helper types
    [Serializable]
    public struct ResourceAmount
    {
        public string id;
        public int amount;
    }

    [Serializable]
    public struct SerializableVector3
    {
        public float x, y, z;
        public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    // --- Skills ---
    public List<SkillSave> skills = new List<SkillSave>();
    public SkillType lastXpSkill = SkillType.Mining;
    public string lastXpSource = "";

    [Serializable]
    public struct SkillSave
    {
        public SkillType type;
        public int level;
        public int xp;
    }
}