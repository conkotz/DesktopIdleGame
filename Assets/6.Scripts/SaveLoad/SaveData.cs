using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnhancementScrollHistoryEntry
{
    public string scrollName;
    public bool success;
    public string effectSummary;
}

[Serializable]
public class SaveData
{
    [Header("Meta")]
    public int version = 4;
    public long savedAtUnix;

    [Header("World / Map")]
    [Tooltip("MapNodeDefinition.nodeId for the last played area (restored on load).")]
    public string activeMapNodeId = "";

    [Tooltip("Cached display label for save slot UI (denormalized).")]
    public string activeMapDisplayName = "";

    [Tooltip("World map node ids the player may select (includes starting node; merged on load).")]
    public List<string> worldMapUnlockedNodeIds = new();

    [Tooltip("World map node ids marked story-completed.")]
    public List<string> worldMapCompletedNodeIds = new();

    [Tooltip("MapNodeDefinition.nodeId values the player has loaded in GamePlay at least once (entered map).")]
    public List<string> worldMapEnteredNodeIds = new();

    [Tooltip("Parallel lists: MapNodeDefinition.nodeId -> cumulative enemy kills recorded on that map.")]
    public List<string> worldMapEnemyKillNodeIds = new();
    public List<int> worldMapEnemyKillTotals = new();

    [Header("Endurance trials")]
    [Tooltip("Parallel lists: MapNodeDefinition.nodeId → max selectable tier (1–5) for that trial.")]
    public List<string> enduranceTrialNodeIds = new();

    public List<int> enduranceTrialMaxSelectableTier = new();

    [Tooltip("Parallel lists: MapNodeDefinition.nodeId → selected combat map scaling slider value (0–7).")]
    public List<string> combatMapScalingNodeIds = new();

    public List<int> combatMapScalingSelectedTier = new();

    [Header("Map enhancements")]
    [Tooltip("Rolled map enhancement item instances (runtime item ids with modifiers).")]
    public List<MapEnhancementItemData> mapEnhancementItems = new();

    [Tooltip("Parallel lists: map node id with up to 3 equipped enhancement item ids.")]
    public List<string> mapEnhancementNodeIds = new();
    public List<string> mapEnhancementSlot0 = new();
    public List<string> mapEnhancementSlot1 = new();
    public List<string> mapEnhancementSlot2 = new();

    [Header("Player")]
    [Tooltip("Auto-retaliate when struck (PlayerCombatController).")]
    public bool retaliationEnabled;

    public string playerName = "Adventurer";
    public int playerLevel = 1;
    public int xp = 0;
    public float playerCurrentHP = -1f;
    [Tooltip("Guard pool; -1 = omit (legacy saves).")]
    public float playerCurrentGuard = -1f;
    [Tooltip("Legacy; not restored on load. Energy refills to max when loading.")]
    public float playerCurrentEnergy = -1f;
    [Tooltip("Legacy; not restored on load. Mana refills to max when loading.")]
    public float playerCurrentMana = -1f;
    [Tooltip("When true, playerWorldPosX/Y/Z restores standing location after choosing Continue / Load Game. Per-map exit positions are preferred when available.")]
    public bool hasSavedPlayerWorldPosition = false;
    public float playerWorldPosX = 0f;
    public float playerWorldPosY = 0f;
    public float playerWorldPosZ = 0f;

    [Tooltip("Parallel lists: last standing position when leaving each map (MapNodeDefinition.nodeId).")]
    public List<string> mapExitPositionNodeIds = new();
    public List<float> mapExitPositionX = new();
    public List<float> mapExitPositionY = new();
    public List<float> mapExitPositionZ = new();

    [Header("Currency")]
    public int gold = 0;

    [Header("Inventory")]
    [Tooltip("Serialized default for JSON only; new games align to Inventory.SlotCount before first save.")]
    public int inventorySlotCount = 32;
    public List<InventorySlotData> inventorySlots = new();
    public List<EnhancedItemData> enhancedItems = new();

    [Header("Town storage chest")]
    [Tooltip("Serialized default for JSON only; new games align to PlayerStorage.SlotCount before first save.")]
    public int storageSlotCount = 28;
    public List<InventorySlotData> storageSlots = new();
    public List<int> storageTabOrder = new();
    public List<bool> storageTabAffinity = new();
    public List<int> storageTabBonusSlots = new();

    [Serializable]
    public struct InventorySlotData
    {
        public string itemId;
        public int amount;
    }

    [Serializable]
    public class MapEnhancementItemData
    {
        public string itemId;
        public string baseItemId;
        public string displayName;
        public string sourceMapNodeId;
        public int tier;
        public List<int> modTypes = new();
        public List<float> modValues = new();
        public List<string> modExtraSpawnEnemyIds = new();
    }

    [Serializable]
    public class EnhancedItemData
    {
        public string itemId;
        public string baseItemId;
        public string displayName;
        public int usedUpgradeSlots;
        public int successfulEnhancements;
        public List<EnhancementScrollHistoryEntry> enhancementScrollHistory = new();
        public WeaponStats weaponStats;
        public ArmorStats armorStats;
        public BonusStats bonusStats;
        public CombatSupportStats combatSupportStats;
        public ToolStats toolStats;
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

    public string equippedHelmet2ItemId;
    public string equippedBody2ItemId;
    public string equippedBoots2ItemId;

    public string equippedTrinket2ItemId;
    public string equippedPendant2ItemId;
    public string equippedRing12ItemId;
    public string equippedRing22ItemId;

    [Header("Toolbelt")]
    public List<string> toolbeltItemIds = new();

    [Header("Skills")]
    public List<SkillSave> skills = new();
    public SkillType lastXpSkill = SkillType.Mining;
    public string lastXpSource = "";
    [Tooltip("Choice selection keys in format 'SkillType:SourceLevel' (e.g. 'Melee:5').")]
    public List<string> skillChoiceSelectionKeys = new();
    [Tooltip("Selected choice index per key (0-based index into the unlock's choices list; supports up to 4 enhancements).")]
    public List<int> skillChoiceSelectionValues = new();

    [Tooltip("Keys like 'Melee:abilityRow:5' → which sibling ability (0..n-1) is committed for that level tier.")]
    public List<string> skillAbilityRowPickKeys = new();

    public List<int> skillAbilityRowPickValues = new();

    [Tooltip("Legacy global presets (v4); superseded by skillAbilityPresetGroups.")]
    public List<SkillAbilityPresetSave> skillAbilityPresets = new();

    [Tooltip("Per-skill Ability Preset slots (two presets each for Melee, Ranged, etc.).")]
    public List<SkillAbilityPresetGroupSave> skillAbilityPresetGroups = new();

    [Tooltip("Combat preset auto-applied when weapon set 1 or 2 is active (index 0 = set 1, 1 = set 2).")]
    public List<SkillAbilityPresetWeaponSetLinkSave> skillAbilityPresetWeaponSetLinks = new();

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
    public List<int> actionBarItemAmounts = new();
    public List<int> actionBarSecondarySlotIndexes = new();
    public List<int> actionBarSecondaryKinds = new();
    public List<string> actionBarSecondaryIds = new();
    public List<int> actionBarSecondaryItemAmounts = new();

    [Serializable]
    public class GatheringActionBarSaveBlock
    {
        public List<int> slotIndexes = new();
        public List<int> kinds = new();
        public List<string> ids = new();
        public List<int> itemAmounts = new();
    }

    [Tooltip("Ability hotkeys 1–5 when the Woodcutting gathering strip is shown; independent of combat loadouts.")]
    public GatheringActionBarSaveBlock actionBarGatherWoodcutting = new();

    [Tooltip("Ability hotkeys 1–5 when the Mining gathering strip is shown.")]
    public GatheringActionBarSaveBlock actionBarGatherMining = new();

    [Tooltip("Ability hotkeys 1–5 when the Fishing gathering strip is shown.")]
    public GatheringActionBarSaveBlock actionBarGatherFishing = new();

    [Header("Quests")]
    [Tooltip("Parallel lists: QuestDefinition.questId → current objective amount.")]
    public List<string> questProgressIds = new();

    public List<int> questProgressAmounts = new();

    [Tooltip("QuestDefinition.questId values that have had rewards claimed (non-repeatable quests stay here).")]
    public List<string> questRewardClaimedIds = new();

    [Tooltip("QuestDefinition.questId values accepted from quest givers.")]
    public List<string> acceptedQuestIds = new();

    [Tooltip(
        "Quest ids that already ran Respawn Enemy On Quest Accepted (one-time spawn per save, even if the player was off-map when accepting).")]
    public List<string> questAcceptedEnemyRespawnHandledIds = new();

    [Tooltip("Ordered QuestDefinition.questId entries currently tracked in the quest tracker window.")]
    public List<string> trackedQuestIds = new();

    [Header("Merchant Stock")]
    public List<MerchantStockSave> merchantStocks = new();

    [Serializable]
    public class MerchantStockSave
    {
        public string merchantId;
        /// <summary>
        /// Parallel to <see cref="MerchantStock.Items"/> indices. Must be <c>int[]</c> (not <c>List&lt;int&gt;</c>):
        /// Unity <see cref="UnityEngine.JsonUtility"/> does not round-trip nested lists inside list elements, so stock never persisted.
        /// </summary>
        public int[] quantities;
    }

    [Header("Desktop strip zoom")]
    [Tooltip("StripCamera orthographicSize ÷ prefab baseline (HUD \"Zoom %\"). 0 = legacy/unset; use scene default.")]
    public float stripCameraZoomMultiplier;

    [Header("UI window lock")]
    [Tooltip("Parallel keys: window id (usually GameObject name, see UIWindowCloseButton.persistenceWindowId).")]
    public List<string> uiWindowLockKeys = new();

    [Tooltip("1 = locked (cannot close via X / ESC / toggle), 0 = unlocked.")]
    public List<int> uiWindowLockLocked = new();

    [Header("Helpers / Tutorial popups")]
    [Tooltip("HelperPopupDefinition.helperId values dismissed for this character (do not replay until New Game clears the save).")]
    public List<string> dismissedHelperIds = new();

    [Tooltip(
        "Helper ids for which the flashing \"! new\" badge must not appear again (persists across respawn / scene load / app restart). Cleared on New Game.")]
    public List<string> helperNewBadgeSuppressedHelperIds = new();

    [Header("Level-placed item pickups")]
    [Tooltip("Keys for spawn-plan item drops that were fully picked up; those placements are not spawned again.")]
    public List<string> levelItemPickupOnceClaimedKeys = new();

    [Header("Permanent enemy deaths (map spawns)")]
    [Tooltip(
        "Spawn-slot keys for enemies with EnemyDefinition.cannotRespawn that have died; those slots stay empty across reload until New Game.")]
    public List<string> permanentDeadEnemySpawnKeys = new();

    [Header("NPC dialogue")]
    [Tooltip(
        "Set when the player dies in GamePlay; cleared after conditional dialogue with After Death And Respawn is shown.")]
    public bool npcPostDeathRespawnDialoguePending;

    [Tooltip(
        "MapNodeDefinition.nodeId where the player died when Pending was set (empty in legacy saves). " +
        "Used with After Death And Respawn + After Death Respawn Map Node Id on NPC dialogue.")]
    public string npcPostDeathRespawnDialogueDeathNodeId = "";

    [Tooltip(
        "NPCInteractionSettings one-way dialogue save ids that have shown a conditional line (base dialogue suppressed).")]
    public List<string> npcOneWayConditionalDialogueConsumedKeys = new();

    [Tooltip(
        "Per one-way save id: unified dialogue queue progress. Element 0 = base dialogue, 1+ = additional conditional rows.")]
    public List<NpcOneWayDialogueChainProgressRow> npcOneWayDialogueChainProgressRows = new();
}

/// <summary>Serialized row for <see cref="SaveData.npcOneWayDialogueChainProgressRows"/>.</summary>
[Serializable]
public class NpcOneWayDialogueChainProgressRow
{
    public string saveId;

    [Tooltip("Legacy conditional-only highest index (0-based). Superseded by queueIndex when present.")]
    public int highestIndex = -1;

    [Tooltip("Unified queue index: 0 = base dialogue shown, 1 = first conditional shown, etc. -1 = unset.")]
    public int queueIndex = -1;
}

/// <summary>Two preset slots for one <see cref="SkillType"/>.</summary>
[Serializable]
public class SkillAbilityPresetGroupSave
{
    public SkillType skillType;
    public List<SkillAbilityPresetSave> slots = new();
}

/// <summary>One Ability Preset slot (enhancement choices + ability row picks for a single skill).</summary>
[Serializable]
public class SkillAbilityPresetSave
{
    public bool hasSnapshot;
    public string displayName = "";
    public List<string> choiceKeys = new();
    public List<int> choiceValues = new();
    public List<string> abilityRowKeys = new();
    public List<int> abilityRowValues = new();
}

/// <summary>Links one combat ability preset to weapon set 1 or 2 for auto-apply on set swap.</summary>
[Serializable]
public class SkillAbilityPresetWeaponSetLinkSave
{
    public bool enabled;
    public SkillType skillType;
    public int presetSlotIndex;
}