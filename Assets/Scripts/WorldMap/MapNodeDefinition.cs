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
    Town
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
    public GameObject prefab;

    [Min(1)]
    public int count = 1;
}

/// <summary>
/// Spawn plan that targets a scene's <c>SpawnPointGroup.groupId</c> (e.g. TownMerchants, CombatEnemies, Resources).
/// </summary>
[Serializable]
public class LevelSpawnGroupPlan
{
    [Tooltip("Must match a SpawnPointGroup.groupId in the scene.")]
    public string groupId = "Default";

    [Tooltip("What to spawn and how many.")]
    public List<SpawnPrefabCount> spawns = new();

    [Tooltip("If true, points are shuffled before spawning.")]
    public bool shuffleSpawnPoints = true;
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
    [Tooltip("Category for map UI and GamePlay (combat, gathering, town, dungeon, etc.).")]
    public MapNodeType nodeType = MapNodeType.Combat;

    [FormerlySerializedAs("recommendedLevel")]
    [Min(1)]
    [Tooltip("Suggested combat power for this area (shown as Recommended CP in level select).")]
    public int recommendedCombatPower = 1;

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
