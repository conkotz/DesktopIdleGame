using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

public enum MapNodeType
{
    Combat,
    Gathering,
    Dungeon,
    Boss,
    Special,
    Town,
    /// <summary>Wave-based combat: use <see cref="enduranceWaves"/>; <see cref="spawnGroupPlans"/> is ignored.</summary>
    EnduranceTrial
}

/// <summary>
/// How enemies react to the player on a map (<see cref="MapNodeDefinition.enemyAggroMode"/>).
/// </summary>
public enum LevelEnemyAggroMode
{
    [Tooltip("Enemies chase and attack when the player enters each enemy's aggro range (default).")]
    Aggressive,
    [Tooltip("No proximity aggro; enemies ignore the player until they take damage, then retaliate.")]
    Calm,
    [Tooltip("Starts calm. Once the player attacks any enemy, all active enemies switch to normal proximity aggro for this level session.")]
    CalmUntilPlayerAggressive
}

/// <summary>High-level environment for music, lighting, skybox, ambient VFX, etc.</summary>
public enum LevelBiome
{
    None,
    Forest,
    Plains,
    Desert,
    Ocean,
    Mountain,
    Cave,
    Swamp,
    Urban,
    DungeonInterior,
    Tundra,
    Custom
}

/// <summary>Prefab + count for a spawn plan.</summary>
[Serializable]
public class SpawnPrefabCount
{
    [Tooltip("SpawnPointGroup.groupId in the scene. Leave empty to use the wave default (endurance) or the parent plan Group Id (combat spawn plans).")]
    public string spawnPointGroupId = "";

    [Tooltip(
        "Optional: exact name of a child transform under the SpawnPointGroup (e.g. SpawnPoint, SpawnPoint2). " +
        "When set, each instance for this row tries that point first (still respects overlap / occupancy). " +
        "Leave empty to use plan order: sequential when shuffle is off, or shuffled cursor order when shuffle is on.")]
    public string spawnPointName = "";

    [Header("Content (enemy, prefab, or world item)")]
    [Tooltip("Preferred reference. Runtime resolves prefab via this asset and calls EnemyBaseController.InitializeFromDefinition when spawning enemies.")]
    public EnemyDefinition enemyDefinition;

    [Tooltip("Legacy / fallback prefab. Used when Enemy Definition is empty, or when the definition has no prefab but this field is set.")]
    public GameObject prefab;

    [Tooltip(
        "When set, spawns a world pickup at the same spawn points as enemies/NPCs (no despawn timer). " +
        "Takes precedence over enemy/prefab for that row. When Item Respawn Timer is 0, fully picking it up uses the one-shot save key (once per save). " +
        "When Item Respawn Timer is greater than 0, the pickup respawns in place after that many seconds and does not use the one-shot claim.")]
    public ItemDefinition itemDefinition;

    [Min(1)]
    [Tooltip("Stack per pickup when Item Definition is assigned.")]
    public int itemAmount = 1;

    [Min(0f)]
    [Tooltip(
        "Seconds after a full pickup before this world item respawns at the same spot. 0 = no respawn (one-time per save once picked up, using Level One Shot Pickup Key). Values > 0 ignore the one-shot key for persistence.")]
    public float itemRespawnTimer;

    [Tooltip("When true, this enemy row can respawn even if Enemy Respawn Enabled is off, but only until Simple Combat Waves start on this map.")]
    public bool respawnUntilSimpleWavesStart;

    [Tooltip("Optional stable save key. If empty, a key is derived from map node id + plan/row index + instance + item id.")]
    public string levelOneShotPickupKey = "";

    [Min(1)]
    [Tooltip("How many pickups to place for this row (each uses spawn point selection like enemy count).")]
    public int count = 1;

    [Header("Per-spawn instance overrides (optional)")]
    [Tooltip("When set and this row spawns a prefab with MapNodePortalTeleporter, override that instance destination node id.")]
    public string portalTargetMapNodeId = "";

    [Tooltip("When set and this row spawns a prefab with InMapTeleporter, assigns this same-map link id.")]
    public string teleporterLinkId = "";

    /// <summary>
    /// Resolves which prefab to instantiate: <see cref="enemyDefinition"/> first, then <see cref="prefab"/>.
    /// When the definition has no prefab, falls back to <see cref="prefab"/> if set.
    /// </summary>
    /// <param name="usedDefinitionForInit">Non-null when <see cref="enemyDefinition"/> was assigned and should be passed to <see cref="EnemyBaseController.InitializeFromDefinition"/>.</param>
    public bool TryResolveSpawnPrefab(out GameObject resolvedPrefab, out EnemyDefinition usedDefinitionForInit, UnityEngine.Object logContext, bool logWarnings)
    {
        resolvedPrefab = null;
        usedDefinitionForInit = null;

        if (itemDefinition)
            return false;

        if (enemyDefinition != null)
        {
            usedDefinitionForInit = enemyDefinition;
            resolvedPrefab = enemyDefinition.ResolveSpawnPrefab();

            if (resolvedPrefab == null && prefab != null)
            {
                resolvedPrefab = prefab;
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[Spawn] EnemyDefinition '{enemyDefinition.name}' has no prefab assigned; using the spawn row's fallback prefab (Spawn Point Group Id: '{spawnPointGroupId}').",
                        enemyDefinition);
                }
            }
            else if (resolvedPrefab == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[Spawn] EnemyDefinition '{enemyDefinition.name}' has no prefab and this row has no fallback prefab — skipped.",
                        enemyDefinition);
                }

                usedDefinitionForInit = null;
                return false;
            }

            return true;
        }

        if (prefab != null)
        {
            resolvedPrefab = prefab;
            usedDefinitionForInit = null;
            return true;
        }

        if (logWarnings)
            Debug.LogWarning("[Spawn] Spawn row has no EnemyDefinition and no prefab — skipped.", logContext);

        return false;
    }
}

/// <summary>
/// Spawn plan that targets a scene's <c>SpawnPointGroup.groupId</c> (e.g. TownMerchants, CombatEnemies, Resources).
/// </summary>
[Serializable]
public class LevelSpawnGroupPlan
{
    [Tooltip("Default SpawnPointGroup.groupId for rows that leave Spawn Point Group Id empty. Can be empty if every row sets its own.")]
    public string groupId = "Default";

    [Tooltip("What to spawn and how many.")]
    public List<SpawnPrefabCount> spawns = new();

    [Tooltip("If true, points are shuffled before spawning.")]
    public bool shuffleSpawnPoints = true;
}

/// <summary>
/// Spawns a same-map teleporter at a named scene spawn point. Teleporters with the same
/// <see cref="teleporterLinkId"/> form a two-way group.
/// </summary>
[Serializable]
public class InMapTeleporterPlan
{
    [Tooltip("Shared link id. Every teleporter on this map with the same id sends players to another member.")]
    public string teleporterLinkId = "";

    [Tooltip("Optional SpawnPointGroup.groupId when the spawn point lives outside the plan default group.")]
    public string spawnPointGroupId = "";

    [Tooltip("Exact spawn point transform name in the GamePlay scene.")]
    public string spawnPointName = "";

    [Tooltip("Teleporter prefab to spawn. Leave empty to use the director default.")]
    public GameObject teleporterPrefab;
}

/// <summary>One wave in an EnduranceTrial map: a flat list of spawns (no nested group plans).</summary>
[Serializable]
public class EnduranceWavePlan : ISerializationCallbackReceiver
{
    [Tooltip("SpawnPointGroup.groupId used when a spawn row leaves Spawn Point Group Id empty.")]
    public string defaultSpawnGroupId = "";

    [Tooltip("Enemies for this wave. Set Spawn Point Group Id on each row to pick a scene group, or leave empty to use Default Spawn Group Id.")]
    public List<SpawnPrefabCount> spawns = new();

    [Tooltip("If true, shuffles spawn points within each group before placing.")]
    public bool shuffleSpawnPoints = true;

    [Tooltip("If enabled, this wave repeats indefinitely instead of advancing to the next wave.")]
    public bool repeatThisWave;

    [FormerlySerializedAs("groupPlans")]
    [SerializeField, HideInInspector]
    private List<LevelSpawnGroupPlan> _legacyGroupPlans;

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        MigrateLegacyIfNeeded();
    }

    /// <summary>Call before spawning if the asset might not have gone through Unity deserialization yet.</summary>
    public void EnsureReady()
    {
        MigrateLegacyIfNeeded();
    }

    /// <summary>Builds the single plan <see cref="LevelSpawnDirector"/> expects for one wave.</summary>
    public LevelSpawnGroupPlan ToSyntheticGroupPlan()
    {
        EnsureReady();
        return new LevelSpawnGroupPlan
        {
            groupId = defaultSpawnGroupId ?? "",
            spawns = spawns ?? new List<SpawnPrefabCount>(),
            shuffleSpawnPoints = shuffleSpawnPoints
        };
    }

    private void MigrateLegacyIfNeeded()
    {
        if (_legacyGroupPlans == null || _legacyGroupPlans.Count == 0)
            return;

        if (spawns == null)
            spawns = new List<SpawnPrefabCount>();

        if (spawns.Count > 0)
        {
            _legacyGroupPlans = null;
            return;
        }

        bool setDefault = string.IsNullOrWhiteSpace(defaultSpawnGroupId);
        bool shuffleFromFirstPlan = true;

        for (int i = 0; i < _legacyGroupPlans.Count; i++)
        {
            LevelSpawnGroupPlan gp = _legacyGroupPlans[i];
            if (gp == null)
                continue;

            if (shuffleFromFirstPlan)
            {
                shuffleSpawnPoints = gp.shuffleSpawnPoints;
                shuffleFromFirstPlan = false;
            }

            string planGid = gp.groupId != null ? gp.groupId.Trim() : string.Empty;
            if (setDefault && !string.IsNullOrWhiteSpace(planGid))
            {
                defaultSpawnGroupId = planGid;
                setDefault = false;
            }

            if (gp.spawns == null)
                continue;

            for (int j = 0; j < gp.spawns.Count; j++)
            {
                SpawnPrefabCount s = gp.spawns[j];
                if (s == null || s.count <= 0)
                    continue;
                if (!s.prefab && s.enemyDefinition == null && !s.itemDefinition)
                    continue;

                string rowGid = !string.IsNullOrWhiteSpace(s.spawnPointGroupId)
                    ? s.spawnPointGroupId.Trim()
                    : planGid;

                spawns.Add(new SpawnPrefabCount
                {
                    spawnPointGroupId = rowGid,
                    spawnPointName = s.spawnPointName,
                    enemyDefinition = s.enemyDefinition,
                    prefab = s.prefab,
                    itemDefinition = s.itemDefinition,
                    itemAmount = Mathf.Max(1, s.itemAmount),
                    itemRespawnTimer = Mathf.Max(0f, s.itemRespawnTimer),
                    levelOneShotPickupKey = s.levelOneShotPickupKey,
                    count = s.count
                });
            }
        }

        _legacyGroupPlans = null;
    }
}

/// <summary>One possible reward when an endurance trial is completed. Rolled independently; drops spawn in list order. Same item can appear multiple times with different ranges/chances.</summary>
[Serializable]
public class EnduranceTrialLootEntry : ISerializationCallbackReceiver
{
    [Tooltip("Item to drop if the roll succeeds.")]
    public ItemDefinition item;

    [FormerlySerializedAs("amount")]
    [Min(1)]
    [Tooltip("Minimum stack size when this entry succeeds.")]
    public int amountMin = 1;

    [Min(1)]
    [Tooltip("Maximum stack size (inclusive). Must be >= Amount Min.")]
    public int amountMax = 1;

    [Range(0f, 1f)]
    [Tooltip("Independent chance this entry is rolled (0 = never, 1 = always). Each row is a separate roll.")]
    public float dropChance = 1f;

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        if (amountMax < amountMin)
            amountMax = amountMin;
    }
}

/// <summary>
/// Completion loot for one endurance trial difficulty tier (I–V). Matched by <see cref="tier"/>.
/// </summary>
[Serializable]
public class EnduranceTrialLootByTier
{
    [Tooltip("Difficulty tier (1 = Tier I … 5 = Tier V). Must match the tier the player selects.")]
    [Range(1, 5)]
    public int tier = 1;

    [Tooltip("If true, these rows are added after the base list and after every lower tier that also uses Append (Tier III gets base + Tier I append + Tier II append + Tier III append). If false, only this tier's rows are used for that tier (no base, no stacking).")]
    public bool appendToBaseLoot;

    [Tooltip("Loot rows for this tier (same rules as base completion loot).")]
    public List<EnduranceTrialLootEntry> entries = new();
}

/// <summary>
/// How combat skill levels gate entry for bosses/dungeons etc.
/// </summary>
public enum CombatSkillGateMode
{
    [Tooltip("No combat skill check.")]
    None,
    [Tooltip("Exactly one combat skill must meet the level (e.g. Magic 60 only).")]
    SingleCombatSkill,
    [Tooltip("At least one of Melee, Ranged, or Magic must meet the level (OR).")]
    AnyOfMeleeRangedMagic,
    [Tooltip("At least one of Melee, Ranged, Magic, or Endurance must meet the level (OR).")]
    AnyCombatSkill
}

/// <summary>
/// Single skill threshold (gathering + combat skills). Multiple entries are ANDed together.
/// </summary>
[Serializable]
public class SkillLevelRequirement
{
    [Tooltip("Skill that must be at least Required Level.")]
    public SkillType skill;

    [Min(1)]
    [Tooltip("Minimum skill level to enter.")]
    public int requiredLevel = 1;
}

/// <summary>
/// Optional prerequisite map progression gate for entering a node.
/// Supports requiring completion of another map, with optional kill-count progress on that map.
/// </summary>
[Serializable]
public class PreviousMapCompletionRequirement
{
    [Tooltip("Turn this on to enforce this prerequisite row.")]
    public bool enabled = true;

    [Tooltip("MapNodeDefinition.nodeId that must satisfy this prerequisite.")]
    public string requiredMapNodeId = "";

    [Tooltip("If enabled, the required map must be marked completed.")]
    public bool requireMapCompleted = true;

    [Tooltip("If enabled, also require at least Required Enemy Kills On Map kills recorded on Required Map Node Id.")]
    public bool requireEnemyKillsOnMap;

    [Min(1)]
    [Tooltip("Minimum cumulative enemy kills on Required Map Node Id when Require Enemy Kills On Map is enabled.")]
    public int requiredEnemyKillsOnMap = 1;
}

[CreateAssetMenu(menuName = "Desktop Idle Game/World Map/Map Node Definition", fileName = "MapNode_")]
public class MapNodeDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable id used by progress, saves, and navigation (e.g. green_fields).")]
    public string nodeId;

    [Tooltip("Shown on buttons and detail panel.")]
    public string displayName = "New Node";

    [TextArea(2, 6)]
    [Tooltip("Long description for the detail panel.")]
    public string description;

    [Header("Gameplay")]
    [Tooltip("Category for map UI and GamePlay. Use Endurance Trial when this node uses endurance waves (wave director + UI). Recommended CP can still be computed from waves even if this is wrong, but gameplay expects Endurance Trial.")]
    public MapNodeType nodeType = MapNodeType.Combat;

    [Tooltip("Aggressive: normal proximity aggro. Calm: enemies only retaliate when hit. CalmUntilPlayerAggressive: starts calm, then first player hit enables proximity aggro for all active enemies.")]
    public LevelEnemyAggroMode enemyAggroMode = LevelEnemyAggroMode.Aggressive;

    [Tooltip("When true, enemies skip horizontal aggro distance once they would aggro (Aggressive: commit from anywhere). Calm until provoked modes still require being hit or map-wide aggression before committing.")]
    public bool ignoreAggroRange;

    [Tooltip("When true, death respawns the player back onto this same map instead of the region town fallback.")]
    public bool respawnHereIfDied;

    [Header("Combat — map scaling")]
    [Tooltip("Combat maps only. When enabled, kills on this map unlock scaling tiers that boost enemy HP, XP, loot, and gold from base values.")]
    public bool mapCombatScalingEnabled;

    [Tooltip("Optional special loot per scaling level (2–7). Fixed drop chances — not scaled. Only the selected scaling tier's entries roll (not cumulative with lower tiers).")]
    public List<MapScalingLevelSpecialDrops> mapCombatScalingSpecialDropsByLevel = new();

    [Header("Enemy respawn (spawn group plans)")]
    [Tooltip("When true, enemies spawned from spawn group plans can respawn after death. Delay is Enemy Respawn Delay Seconds below. Ignored for endurance waves.")]
    public bool enemyRespawnEnabled;

    [Min(0.01f)]
    [Tooltip("Seconds after death before a respawn attempt. LevelSpawnDirector subtracts equipped ItemDefinition → Misc → Enemy Respawn Time Reduction from this value.")]
    public float enemyRespawnDelaySeconds = 30f;

    [Range(0f, 1f)]
    [Tooltip(
        "Chance (0–1) that an enemy spawned by respawn is Elite: +100% HP, +25% damage, 2× combat XP per damage; gold uses EnemyDefinition.eliteGoldMultiplier (default 2×); item loot uses EnemyDefinition elite loot handling. " +
        "Never rolled on the level's initial spawn — only when respawning after death.")]
    public float eliteSpawnChance = 0f;

    [FormerlySerializedAs("recommendedCombatPower")]
    [Min(1)]
    [Tooltip("Manual fallback when RecommendedCombatPower cannot score spawn rows (e.g. missing Enemy Definition). Display CP is computed from spawns in code: endurance trials use wave stress; combat zones use spawn group plans only (no wave multiplier).")]
    public int fallbackRecommendedCombatPower = 1;

    [Tooltip("If false, node may be hidden or disabled after first clear (future use).")]
    public bool isRepeatable = true;

    [Tooltip("When true, opening Level Select or Quests from the in-game main menu marks this node completed (for 'finish the level' quest gates).")]
    public bool markCompletedWhenReturningToMenu;

    [Header("Travel — in-world entrances")]
    [Tooltip(
        "When true, this map cannot be entered from the Locations menu Teleport button or the quest journal Enter map button. " +
        "Use an in-scene MapNodePortalTeleporter, NPC dialogue, or other scripted travel. " +
        "Quest reward teleports and save/load are unaffected.")]
    public bool entranceOnlyAccess;

    [Header("Map UI — completion label")]
    [Tooltip(
        "Optional MapNodeDefinition.nodeId. When set, level select shows \"Completed\" and one-shot retired styling only after " +
        "this node is saved as complete AND the player has entered that map at least once (Gameplay load). " +
        "Story/quest logic still uses raw completion. Example: tutorial_1 → tutorial_2.")]
    public string completedLabelRequiresEnteredNodeId = "";

    [Tooltip("When true, level select never shows “Completed” for this node — uses “Cleared” instead (e.g. Tutorial 2).")]
    public bool mapUiUseClearedInsteadOfCompleted;

    [Header("Unlock — Map progression")]
    [Tooltip("If true (default), WorldMapProgressManager must include this nodeId (starting node, story unlock, or additional list). If false, skill requirements only gate entry — use for areas that are open when skills are high enough.")]
    public bool requiresMapUnlock = true;

    [Header("Visuals")]
    [Tooltip("Icon for lists and map pins (future).")]
    public Sprite icon;

    [Header("Unlock — Skill levels")]
    [Tooltip("All listed skills must meet their levels (AND). Use for Fishing, Mining, Woodcutting, or any skill.")]
    public List<SkillLevelRequirement> requiredSkillLevels = new();

    [Tooltip("Optional combat gate in addition to Required Skill Levels. Use for dungeons/bosses.")]
    public CombatSkillGateMode combatSkillGateMode = CombatSkillGateMode.None;

    [Tooltip("When gate is Single Combat Skill, which skill must meet Combat Required Level.")]
    public SkillType combatSingleSkill = SkillType.Melee;

    [Min(1)]
    [Tooltip("Level threshold for the combat gate (single skill or any-of-three mode).")]
    public int combatRequiredLevel = 1;

    [Header("Unlock — Notes")]
    [TextArea(2, 6)]
    [Tooltip("Extra freeform notes (quests, flags) shown after skill lines.")]
    public string unlockRequirementNotes;

    [Header("Graph")]
    [Tooltip("Other nodes reachable from this one (for progression tools later).")]
    public List<string> connectedNodeIds = new();

    [Tooltip("Optional linear 'next' hints separate from bidirectional connections.")]
    public List<string> nextNodeIds = new();

    [Header("Playable content (GamePlay scene)")]
    [Tooltip("Environment key for lighting, music, skybox, ambient VFX.")]
    public LevelBiome biome = LevelBiome.None;

    [Header("Spawn plans (GamePlay scene)")]
    [Tooltip("Concrete spawn plan: which prefabs to instantiate and how many, mapped to SpawnPointGroup ids in the scene.")]
    public List<LevelSpawnGroupPlan> spawnGroupPlans = new();

    [Header("Side play areas")]
    [Tooltip(
        "SidePlayArea.areaId values active on this map. Enables side-region bounds for entities in those X ranges " +
        "and should be paired with spawnGroupPlans that reference the area's SpawnPointGroup id.")]
    public List<string> enabledSidePlayAreaIds = new();

    [Header("In-map teleporters")]
    [Tooltip(
        "Same-map teleporter pairs/groups. Each entry spawns one teleporter at Spawn Point Name. " +
        "Teleporters sharing Teleporter Link Id send players to each other.")]
    public List<InMapTeleporterPlan> inMapTeleporters = new();

    [Header("Unlock — Previous map completion")]
    [Tooltip("Optional additional progression prerequisites. All enabled rows are required (AND).")]
    public List<PreviousMapCompletionRequirement> requiredPreviousMapCompletions = new();

    [Header("Simple wave sequence (non-endurance combat)")]
    [Tooltip("Optional extra wave sequence for non-Endurance maps. Uses Endurance-style wave rows and spawns via LevelSpawnDirector.SpawnAdditionalGroupPlan.")]
    public List<EnduranceWavePlan> simpleCombatWaves = new();

    [Tooltip("If enabled, the simple wave sequence attempts to start automatically when this node loads.")]
    public bool simpleCombatWavesAutoStartOnLevelEnter = true;

    [Tooltip("If enabled, simple waves start only after Required Quest Id reward is claimed.")]
    public bool simpleCombatWavesRequireQuestRewardClaimed;

    [Tooltip("Quest id checked when Simple Combat Waves Require Quest Reward Claimed is enabled.")]
    public string simpleCombatWavesRequiredQuestId = "";

    [Tooltip("If enabled, simple waves start only after Quest Accepted Id is accepted in the quest log.")]
    public bool simpleCombatWavesRequireQuestAccepted;

    [Tooltip("Quest id checked when Simple Combat Waves Require Quest Accepted is enabled.")]
    public string simpleCombatWavesQuestAcceptedId = "";

    [Tooltip("If true, this sequence runs only once each time the gameplay scene is loaded.")]
    public bool simpleCombatWavesRunOncePerSceneSession = true;

    [Header("Endurance trial (wave mode)")]
    [Tooltip("When nodeType is EnduranceTrial, each entry is one wave: set Default Spawn Group Id (optional), then Spawns (prefab, count, Spawn Point Group Id per row). No nested Group Plans.")]
    public List<EnduranceWavePlan> enduranceWaves = new();

    [Header("Endurance trial — completion loot")]
    [Tooltip("Default loot when no per-tier row matches, or when a tier row is empty (unless that tier uses Append To Base). Each entry rolls Drop Chance independently.")]
    public List<EnduranceTrialLootEntry> enduranceCompletionLoot = new();

    [Tooltip("Per-tier loot. Append: stacks with base and lower tiers' append rows. Replace: only that tier's rows. First duplicate tier index wins.")]
    public List<EnduranceTrialLootByTier> enduranceCompletionLootByTier = new();

    [Min(0f)]
    [Tooltip("Seconds between each spawned drop (no delay before the first).")]
    public float enduranceCompletionLootInterval = 0.5f;

    [TextArea(2, 6)]
    [Tooltip("Designer notes for this level's spawn/setup.")]
    public string contentDesignerNotes;

    [Header("Loading (optional)")]
    [Tooltip("Additive or addressable scene name; optional if all content is spawned in GamePlay.")]
    public string sceneNamePlaceholder;

    [Tooltip("Optional external id for analytics/tools; if empty, ResolveEncounterId uses nodeId.")]
    public string encounterIdPlaceholder;

    /// <summary>Optional id for logging/saves — placeholder override, else <see cref="nodeId"/>.</summary>
    public string ResolveEncounterId()
    {
        if (!string.IsNullOrEmpty(encounterIdPlaceholder))
            return encounterIdPlaceholder;
        return nodeId ?? string.Empty;
    }

    /// <summary>
    /// True when any skill-based gate or notes should appear in the requirements UI.
    /// </summary>
    public bool HasRequirementsContent()
    {
        if (requiredPreviousMapCompletions != null)
        {
            foreach (var req in requiredPreviousMapCompletions)
            {
                if (req == null || !req.enabled || string.IsNullOrWhiteSpace(req.requiredMapNodeId))
                    continue;
                if (req.requireMapCompleted || (req.requireEnemyKillsOnMap && req.requiredEnemyKillsOnMap > 0))
                    return true;
            }
        }

        if (requiredSkillLevels != null)
        {
            foreach (var r in requiredSkillLevels)
            {
                if (r != null && r.requiredLevel > 0)
                    return true;
            }
        }

        if (combatSkillGateMode != CombatSkillGateMode.None && combatRequiredLevel > 0)
            return true;

        return !string.IsNullOrWhiteSpace(unlockRequirementNotes);
    }

    public bool MeetsPreviousMapCompletionRequirements(WorldMapProgressManager progress)
    {
        if (requiredPreviousMapCompletions == null || requiredPreviousMapCompletions.Count == 0)
            return true;
        if (progress == null)
            return false;

        for (int i = 0; i < requiredPreviousMapCompletions.Count; i++)
        {
            PreviousMapCompletionRequirement req = requiredPreviousMapCompletions[i];
            if (req == null || !req.enabled)
                continue;

            string requiredNodeId = req.requiredMapNodeId != null ? req.requiredMapNodeId.Trim() : "";
            if (string.IsNullOrEmpty(requiredNodeId))
                continue;

            if (req.requireMapCompleted && !progress.IsNodeCompleted(requiredNodeId))
                return false;

            if (req.requireEnemyKillsOnMap && req.requiredEnemyKillsOnMap > 0)
            {
                int kills = progress.GetEnemyKillsOnNode(requiredNodeId);
                if (kills < req.requiredEnemyKillsOnMap)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when skill levels must be checked for entry (ignores notes-only).
    /// </summary>
    public bool HasSkillGates()
    {
        if (requiredSkillLevels != null)
        {
            foreach (var r in requiredSkillLevels)
            {
                if (r != null && r.requiredLevel > 0)
                    return true;
            }
        }

        return combatSkillGateMode != CombatSkillGateMode.None && combatRequiredLevel > 0;
    }

    /// <summary>
    /// Returns true if the player meets all configured skill thresholds. If <paramref name="skills"/> is null, returns true (lenient).
    /// </summary>
    public bool MeetsSkillRequirements(SkillsManager skills)
    {
        if (!skills)
            return true;

        if (requiredSkillLevels != null)
        {
            foreach (var req in requiredSkillLevels)
            {
                if (req == null || req.requiredLevel <= 0)
                    continue;
                if (!skills.IsLevelUnlocked(req.skill, req.requiredLevel))
                    return false;
            }
        }

        switch (combatSkillGateMode)
        {
            case CombatSkillGateMode.None:
                break;
            case CombatSkillGateMode.SingleCombatSkill:
                if (combatRequiredLevel > 0 &&
                    !skills.IsLevelUnlocked(combatSingleSkill, combatRequiredLevel))
                    return false;
                break;
            case CombatSkillGateMode.AnyOfMeleeRangedMagic:
                if (combatRequiredLevel <= 0)
                    break;
                if (skills.IsLevelUnlocked(SkillType.Melee, combatRequiredLevel) ||
                    skills.IsLevelUnlocked(SkillType.Ranged, combatRequiredLevel) ||
                    skills.IsLevelUnlocked(SkillType.Magic, combatRequiredLevel))
                    break;
                return false;
            case CombatSkillGateMode.AnyCombatSkill:
                if (combatRequiredLevel <= 0)
                    break;
                if (skills.IsLevelUnlocked(SkillType.Melee, combatRequiredLevel) ||
                    skills.IsLevelUnlocked(SkillType.Ranged, combatRequiredLevel) ||
                    skills.IsLevelUnlocked(SkillType.Magic, combatRequiredLevel) ||
                    skills.IsLevelUnlocked(SkillType.Endurance, combatRequiredLevel))
                    break;
                return false;
        }

        return true;
    }

    /// <summary>
    /// Map/story unlock satisfied: either this node does not use map locks, or progress has unlocked this node id.
    /// </summary>
    public bool IsMapProgressSatisfied(WorldMapProgressManager progress)
    {
        if (!requiresMapUnlock)
            return true;
        return progress != null && progress.IsNodeUnlocked(nodeId);
    }

    /// <summary>
    /// Level-select Completed label / retired row: raw save completion plus optional <see cref="completedLabelRequiresEnteredNodeId"/> gate.
    /// Quests and region locks still use <see cref="WorldMapProgressManager.IsNodeCompleted"/> only.
    /// </summary>
    public bool IsCompletedShownInMapUi(WorldMapProgressManager progress)
    {
        if (progress == null || !progress.IsNodeCompleted(nodeId))
            return false;

        string need = completedLabelRequiresEnteredNodeId;
        if (string.IsNullOrWhiteSpace(need))
            return true;

        return progress.HasEnteredNode(need.Trim());
    }

    /// <summary>
    /// One-time nodes: shown as finished in level select (grey, no Enter) when <see cref="IsCompletedShownInMapUi"/> is true.
    /// </summary>
    public bool IsPermanentlyCompleted(WorldMapProgressManager progress)
    {
        return !isRepeatable && IsCompletedShownInMapUi(progress);
    }

    /// <summary>
    /// True when the player can enter: map gate (if any) + skill gates, and not a finished one-shot node (raw completion).
    /// </summary>
    public bool CanEnter(WorldMapProgressManager progress, SkillsManager skills)
    {
        if (!IsMapProgressSatisfied(progress))
            return false;
        if (!MeetsPreviousMapCompletionRequirements(progress))
            return false;
        if (!MeetsSkillRequirements(skills))
            return false;
        if (!isRepeatable && progress != null && progress.IsNodeCompleted(nodeId))
            return false;
        return true;
    }

    /// <summary>
    /// Locations list Teleport and quest journal Enter map — false when <see cref="entranceOnlyAccess"/> (use portals / scripted travel).
    /// </summary>
    public bool CanEnterFromLevelMenu(WorldMapProgressManager progress, SkillsManager skills)
    {
        return CanEnter(progress, skills) && !entranceOnlyAccess;
    }

    public bool IsMapCombatScalingEnabled() =>
        nodeType == MapNodeType.Combat && mapCombatScalingEnabled;

    public int GetCombatScalingLevel(WorldMapProgressManager progress) =>
        MapCombatScaling.ResolveActiveScalingLevel(this, progress);

    /// <summary>Map-specific special drops for the selected scaling tier (excludes automatic scroll defaults).</summary>
    public void CollectMapSpecificSpecialDrops(int sliderValue, List<MapScalingSpecialLootEntry> results)
    {
        if (results == null)
            return;

        int tier = Mathf.Clamp(sliderValue, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        if (tier <= 0 || mapCombatScalingSpecialDropsByLevel == null)
            return;

        for (int i = 0; i < mapCombatScalingSpecialDropsByLevel.Count; i++)
        {
            MapScalingLevelSpecialDrops tierEntry = mapCombatScalingSpecialDropsByLevel[i];
            if (tierEntry == null || tierEntry.scalingLevel != tier || tierEntry.drops == null)
                continue;

            for (int j = 0; j < tierEntry.drops.Count; j++)
            {
                MapScalingSpecialLootEntry entry = tierEntry.drops[j];
                if (entry?.item != null)
                    results.Add(entry);
            }
        }
    }

    /// <summary>Special drops for the selected map scaling slider tier only (not cumulative across lower tiers).</summary>
    [Obsolete("Use CollectMapSpecificSpecialDrops for map entries and MapCombatScalingSpecialDropDefaults.CollectGroupRollsForScalingLevel for scroll defaults.")]
    public void CollectCombatScalingSpecialDropsUpToSlider(int sliderValue, List<MapScalingSpecialLootEntry> results)
    {
        CollectMapSpecificSpecialDrops(sliderValue, results);
        if (IsMapCombatScalingEnabled())
            MapCombatScalingSpecialDropDefaults.CollectLegacyIndividualScrollEntries(sliderValue, results);
    }

    public const string EnterConditionTeleportAvailable = "Teleport to area available";
    public const string EnterConditionUnavailable = "Unavailable until requirements are met";
    public const string EnterConditionEntranceOnly =
        "Access to map available only from entrance (teleport unavailable)";

    /// <summary>Level select / world map details — how this node can be entered from the menu.</summary>
    public string GetEnterConditionDisplayText(WorldMapProgressManager progress, SkillsManager skills)
    {
        if (!CanEnter(progress, skills))
            return EnterConditionUnavailable;

        if (entranceOnlyAccess)
            return EnterConditionEntranceOnly;

        return EnterConditionTeleportAvailable;
    }

    /// <summary>
    /// Short label for list/detail: map/skill lock, Unlocked, Cleared (gated one-shot retired), or Completed.
    /// </summary>
    public string GetUiStateLabel(WorldMapProgressManager progress, SkillsManager skills)
    {
        if (!IsMapProgressSatisfied(progress))
            return "Map locked";
        if (!MeetsPreviousMapCompletionRequirements(progress))
            return "Progress locked";
        if (!MeetsSkillRequirements(skills))
            return "Skill locked";
        if (progress != null && progress.IsNodeCompleted(nodeId))
        {
            if (!IsCompletedShownInMapUi(progress))
            {
                if (!string.IsNullOrWhiteSpace(completedLabelRequiresEnteredNodeId))
                    return "Unlocked";
                return mapUiUseClearedInsteadOfCompleted ? "Cleared" : "Completed";
            }

            if (!string.IsNullOrWhiteSpace(completedLabelRequiresEnteredNodeId))
                return "Cleared";
            return mapUiUseClearedInsteadOfCompleted ? "Cleared" : "Completed";
        }

        return "Unlocked";
    }

    /// <summary>
    /// True when at least one enabled <see cref="PreviousMapCompletionRequirement"/> row
    /// gates entry on an enemy kill count on another map.
    /// </summary>
    public bool HasKillsProgressRequirements()
    {
        if (requiredPreviousMapCompletions == null)
            return false;

        for (int i = 0; i < requiredPreviousMapCompletions.Count; i++)
        {
            PreviousMapCompletionRequirement req = requiredPreviousMapCompletions[i];
            if (req == null || !req.enabled || !req.requireEnemyKillsOnMap)
                continue;
            if (req.requiredEnemyKillsOnMap <= 0)
                continue;
            if (string.IsNullOrWhiteSpace(req.requiredMapNodeId))
                continue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Build a single-line progress string suitable for inline messages (e.g. activity log):
    /// <c>2 / 150 enemies defeated in Hunting Grounds</c>. Multiple kill gates are joined with
    /// <c>", "</c>. Returns an empty string when there are no kill-count gates.
    /// </summary>
    public string BuildKillsProgressInlineText(WorldMapProgressManager progress, WorldMapDefinition worldMap = null)
    {
        if (requiredPreviousMapCompletions == null || requiredPreviousMapCompletions.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        for (int i = 0; i < requiredPreviousMapCompletions.Count; i++)
        {
            PreviousMapCompletionRequirement req = requiredPreviousMapCompletions[i];
            if (req == null || !req.enabled || !req.requireEnemyKillsOnMap)
                continue;

            int required = req.requiredEnemyKillsOnMap;
            if (required <= 0)
                continue;

            string requiredNodeId = req.requiredMapNodeId != null ? req.requiredMapNodeId.Trim() : "";
            if (string.IsNullOrEmpty(requiredNodeId))
                continue;

            string displayName = ResolveRequirementNodeDisplayName(requiredNodeId, worldMap);
            int kills = progress != null ? progress.GetEnemyKillsOnNode(requiredNodeId) : 0;
            int clamped = Mathf.Clamp(kills, 0, required);

            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append($"{clamped} / {required} enemies defeated in {displayName}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Build progress lines for any kill-count prerequisites on this node, e.g.
    /// <c>Battlegrounds: 47 / 200 enemies defeated</c>. One line per active requirement.
    /// Returns an empty string when there are no kill-count gates.
    /// </summary>
    public string BuildKillsProgressDisplayText(WorldMapProgressManager progress, WorldMapDefinition worldMap = null)
    {
        if (requiredPreviousMapCompletions == null || requiredPreviousMapCompletions.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        for (int i = 0; i < requiredPreviousMapCompletions.Count; i++)
        {
            PreviousMapCompletionRequirement req = requiredPreviousMapCompletions[i];
            if (req == null || !req.enabled || !req.requireEnemyKillsOnMap)
                continue;

            int required = req.requiredEnemyKillsOnMap;
            if (required <= 0)
                continue;

            string requiredNodeId = req.requiredMapNodeId != null ? req.requiredMapNodeId.Trim() : "";
            if (string.IsNullOrEmpty(requiredNodeId))
                continue;

            string displayName = ResolveRequirementNodeDisplayName(requiredNodeId, worldMap);
            int kills = progress != null ? progress.GetEnemyKillsOnNode(requiredNodeId) : 0;
            int clamped = Mathf.Clamp(kills, 0, required);

            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append($"{displayName}: {clamped} / {required} enemies defeated");
        }

        return sb.ToString();
    }

    public string BuildRequirementsDisplayText(WorldMapDefinition worldMap = null)
    {
        var sb = new StringBuilder();

        if (requiredPreviousMapCompletions != null)
        {
            for (int i = 0; i < requiredPreviousMapCompletions.Count; i++)
            {
                PreviousMapCompletionRequirement req = requiredPreviousMapCompletions[i];
                if (req == null || !req.enabled)
                    continue;

                string requiredNodeId = req.requiredMapNodeId != null ? req.requiredMapNodeId.Trim() : "";
                if (string.IsNullOrEmpty(requiredNodeId))
                    continue;

                string requiredDisplayName = ResolveRequirementNodeDisplayName(requiredNodeId, worldMap);

                if (req.requireMapCompleted)
                    sb.AppendLine($"Complete the {requiredDisplayName} map.");
                if (req.requireEnemyKillsOnMap && req.requiredEnemyKillsOnMap > 0)
                    sb.AppendLine($"Defeat {req.requiredEnemyKillsOnMap} enemies within the {requiredDisplayName}.");
            }
        }

        if (requiredSkillLevels != null)
        {
            foreach (var r in requiredSkillLevels)
            {
                if (r == null || r.requiredLevel <= 0)
                    continue;
                sb.AppendLine($"{r.skill}: level {r.requiredLevel}");
            }
        }

        switch (combatSkillGateMode)
        {
            case CombatSkillGateMode.SingleCombatSkill:
                if (combatRequiredLevel > 0)
                    sb.AppendLine($"{combatSingleSkill}: level {combatRequiredLevel}");
                break;
            case CombatSkillGateMode.AnyOfMeleeRangedMagic:
                if (combatRequiredLevel > 0)
                    sb.AppendLine($"Any of Melee, Ranged, or Magic: level {combatRequiredLevel}");
                break;
            case CombatSkillGateMode.AnyCombatSkill:
                if (combatRequiredLevel > 0)
                    sb.AppendLine($"Any combat skill: level {combatRequiredLevel}");
                break;
        }

        if (!string.IsNullOrWhiteSpace(unlockRequirementNotes))
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(unlockRequirementNotes.Trim());
        }

        return sb.ToString().Trim();
    }

    private static string ResolveRequirementNodeDisplayName(string requiredNodeId, WorldMapDefinition worldMap)
    {
        if (!string.IsNullOrWhiteSpace(requiredNodeId) && worldMap != null)
        {
            MapNodeDefinition requiredNode = worldMap.FindNodeById(requiredNodeId);
            if (requiredNode != null && !string.IsNullOrWhiteSpace(requiredNode.displayName))
                return requiredNode.displayName.Trim();
        }

        return requiredNodeId;
    }

    private void OnValidate()
    {
        NormalizeItemAmountsInPlans(spawnGroupPlans);
        NormalizeItemAmountsInWaves(simpleCombatWaves);
        NormalizeItemAmountsInWaves(enduranceWaves);
        NormalizePreviousMapCompletionRequirements(requiredPreviousMapCompletions);
    }

    private static void NormalizePreviousMapCompletionRequirements(List<PreviousMapCompletionRequirement> requirements)
    {
        if (requirements == null)
            return;

        for (int i = 0; i < requirements.Count; i++)
        {
            PreviousMapCompletionRequirement req = requirements[i];
            if (req == null)
                continue;

            if (!string.IsNullOrWhiteSpace(req.requiredMapNodeId))
                req.requiredMapNodeId = req.requiredMapNodeId.Trim();

            if (req.requireEnemyKillsOnMap)
                req.requiredEnemyKillsOnMap = Mathf.Max(1, req.requiredEnemyKillsOnMap);
        }
    }

    private static void NormalizeItemAmountsInPlans(List<LevelSpawnGroupPlan> plans)
    {
        if (plans == null)
            return;

        for (int i = 0; i < plans.Count; i++)
        {
            LevelSpawnGroupPlan plan = plans[i];
            if (plan == null || plan.spawns == null)
                continue;

            NormalizeItemAmountsInRows(plan.spawns);
        }
    }

    private static void NormalizeItemAmountsInWaves(List<EnduranceWavePlan> waves)
    {
        if (waves == null)
            return;

        for (int i = 0; i < waves.Count; i++)
        {
            EnduranceWavePlan wave = waves[i];
            if (wave == null || wave.spawns == null)
                continue;

            NormalizeItemAmountsInRows(wave.spawns);
        }
    }

    private static void NormalizeItemAmountsInRows(List<SpawnPrefabCount> rows)
    {
        if (rows == null)
            return;

        for (int i = 0; i < rows.Count; i++)
        {
            SpawnPrefabCount row = rows[i];
            if (row == null)
                continue;

            row.itemAmount = row.itemDefinition != null
                ? Mathf.Max(1, row.itemAmount)
                : 0;

            if (row.itemDefinition != null)
                row.itemRespawnTimer = Mathf.Max(0f, row.itemRespawnTimer);
        }
    }
}
