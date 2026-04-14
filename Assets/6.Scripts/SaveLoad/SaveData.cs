using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    [Header("Meta")]
    public int version = 3;
    public long savedAtUnix;

    [Header("World / Map")]
    [Tooltip("MapNodeDefinition.nodeId for the last played area (restored on load).")]
    public string activeMapNodeId = "";

    [Tooltip("Cached display label for save slot UI (denormalized).")]
    public string activeMapDisplayName = "";

    [Header("Endurance trials")]
    [Tooltip("Parallel lists: MapNodeDefinition.nodeId → max selectable tier (1–5) for that trial.")]
    public List<string> enduranceTrialNodeIds = new();

    public List<int> enduranceTrialMaxSelectableTier = new();

    [Header("Player")]
    [Tooltip("Auto-retaliate when struck (PlayerCombatController).")]
    public bool retaliationEnabled;

    public string playerName = "Adventurer";
    public int playerLevel = 1;
    public int xp = 0;
    public float playerCurrentHP = -1f;
    [Tooltip("Legacy; not restored on load. Energy refills to max when loading.")]
    public float playerCurrentEnergy = -1f;
    [Tooltip("Legacy; not restored on load. Mana refills to max when loading.")]
    public float playerCurrentMana = -1f;

    [Header("Currency")]
    public int gold = 0;

    [Header("Inventory")]
    public int inventorySlotCount = 32;
    public List<InventorySlotData> inventorySlots = new();

    [Header("Town storage chest")]
    public int storageSlotCount = 28;
    public List<InventorySlotData> storageSlots = new();

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
    [Tooltip("Choice selection keys in format 'SkillType:SourceLevel' (e.g. 'Melee:5').")]
    public List<string> skillChoiceSelectionKeys = new();
    [Tooltip("Selected choice index per key; 0/1 for two-choice rows.")]
    public List<int> skillChoiceSelectionValues = new();

    [Tooltip("Keys like 'Melee:abilityRow:5' → which sibling ability (0..n-1) is committed for that level tier.")]
    public List<string> skillAbilityRowPickKeys = new();

    public List<int> skillAbilityRowPickValues = new();

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

    [Header("Quests")]
    [Tooltip("Parallel lists: QuestDefinition.questId → current objective amount.")]
    public List<string> questProgressIds = new();

    public List<int> questProgressAmounts = new();

    [Tooltip("QuestDefinition.questId values that have had rewards claimed (non-repeatable quests stay here).")]
    public List<string> questRewardClaimedIds = new();

    [Header("Merchant Stock")]
    public List<MerchantStockSave> merchantStocks = new();

    [Serializable]
    public class MerchantStockSave
    {
        public string merchantId;
        public List<int> quantities = new();
    }
}