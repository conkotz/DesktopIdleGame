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
    Calm
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

/// <summary>Named batch of prefabs to spawn (enemies, props, harvestables).</summary>
[Serializable]
public class EncounterPrefabGroup
{
    [Tooltip("Logical id for spawners: e.g. Enemies, Props, OreVeins, FishSpots.")]
    public string groupId = "Default";

    [Tooltip("Prefabs this group can instantiate.")]
    public List<GameObject> prefabs = new();
}

/// <summary>Prefab + count for a spawn plan.</summary>
[Serializable]
public class SpawnPrefabCount
{
    [Tooltip("SpawnPointGroup.groupId in the scene. Leave empty to use the wave default (endurance) or the parent plan Group Id (combat spawn plans).")]
    public string spawnPointGroupId = "";

    [Header("Content (EnemyDefinition preferred)")]
    [Tooltip("Preferred reference. Runtime resolves prefab via this asset and calls EnemyBaseController.InitializeFromDefinition when spawning enemies.")]
    public EnemyDefinition enemyDefinition;

    [Tooltip("Legacy / fallback prefab. Used when Enemy Definition is empty, or when the definition has no prefab but this field is set.")]
    public GameObject prefab;

    [Min(1)]
    public int count = 1;

    /// <summary>
    /// Resolves which prefab to instantiate: <see cref="enemyDefinition"/> first, then <see cref="prefab"/>.
    /// When the definition has no prefab, falls back to <see cref="prefab"/> if set.
    /// </summary>
    /// <param name="usedDefinitionForInit">Non-null when <see cref="enemyDefinition"/> was assigned and should be passed to <see cref="EnemyBaseController.InitializeFromDefinition"/>.</param>
    public bool TryResolveSpawnPrefab(out GameObject resolvedPrefab, out EnemyDefinition usedDefinitionForInit, UnityEngine.Object logContext, bool logWarnings)
    {
        resolvedPrefab = null;
        usedDefinitionForInit = null;

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
                if (!s.prefab && s.enemyDefinition == null)
                    continue;

                string rowGid = !string.IsNullOrWhiteSpace(s.spawnPointGroupId)
                    ? s.spawnPointGroupId.Trim()
                    : planGid;

                spawns.Add(new SpawnPrefabCount
                {
                    spawnPointGroupId = rowGid,
                    enemyDefinition = s.enemyDefinition,
                    prefab = s.prefab,
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
    AnyOfMeleeRangedMagic
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

[CreateAssetMenu(menuName = "DesktopIdleGame/World Map/Map Node Definition", fileName = "MapNode_")]
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

    [Tooltip("Aggressive: proximity aggro as usual. Calm: enemies ignore the player until damaged (retaliation only).")]
    public LevelEnemyAggroMode enemyAggroMode = LevelEnemyAggroMode.Aggressive;

    [Tooltip("When true, spawned enemies ignore aggro range and always chase/attack the player (no distance gate). Use for endurance trials / waves so every enemy commits immediately.")]
    public bool ignoreAggroRange;

    [Header("Enemy respawn (spawn group plans)")]
    [Tooltip("When true, enemies spawned from spawn group plans can respawn after death. Delay is Enemy Respawn Delay Seconds below. Ignored for endurance waves.")]
    public bool enemyRespawnEnabled;

    [Min(0.01f)]
    [Tooltip("Seconds after death before a respawn attempt. LevelSpawnDirector subtracts equipped ItemDefinition → Misc → Enemy Respawn Time Reduction from this value.")]
    public float enemyRespawnDelaySeconds = 30f;

    [FormerlySerializedAs("recommendedCombatPower")]
    [Min(1)]
    [Tooltip("Manual fallback when RecommendedCombatPower cannot score spawn rows (e.g. missing Enemy Definition). Display CP is computed from spawns in code: endurance trials use wave stress; combat zones use spawn group plans only (no wave multiplier).")]
    public int fallbackRecommendedCombatPower = 1;

    [Tooltip("If false, node may be hidden or disabled after first clear (future use).")]
    public bool isRepeatable = true;

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

    [Tooltip("Grouped prefabs for spawners (enemies, gathering nodes, town props, etc.).")]
    public List<EncounterPrefabGroup> prefabGroups = new();

    [Tooltip("Concrete spawn plan: which prefabs to instantiate and how many, mapped to SpawnPointGroup ids in the scene.")]
    public List<LevelSpawnGroupPlan> spawnGroupPlans = new();

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
    /// True when the player can enter: map gate (if any) + skill gates.
    /// </summary>
    public bool CanEnter(WorldMapProgressManager progress, SkillsManager skills)
    {
        if (!IsMapProgressSatisfied(progress))
            return false;
        return MeetsSkillRequirements(skills);
    }

    /// <summary>
    /// Short label for list/detail: distinguishes map lock vs skill lock vs completed.
    /// </summary>
    public string GetUiStateLabel(WorldMapProgressManager progress, SkillsManager skills)
    {
        if (!IsMapProgressSatisfied(progress))
            return "Map locked";
        if (!MeetsSkillRequirements(skills))
            return "Skill locked";
        if (progress != null && progress.IsNodeCompleted(nodeId))
            return "Completed";
        return "Unlocked";
    }

    public string BuildRequirementsDisplayText()
    {
        var sb = new StringBuilder();

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
        }

        if (!string.IsNullOrWhiteSpace(unlockRequirementNotes))
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(unlockRequirementNotes.Trim());
        }

        return sb.ToString().Trim();
    }
}
