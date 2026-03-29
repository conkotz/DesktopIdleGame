using System.Collections.Generic;
using UnityEngine;

public enum MapNodeType
{
    Combat,
    Gathering,
    Dungeon,
    Boss,
    Special,
    Town
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
    public MapNodeType nodeType = MapNodeType.Combat;

    [Min(1)]
    [Tooltip("Suggested character level for this area.")]
    public int recommendedLevel = 1;

    [Tooltip("If false, node may be hidden or disabled after first clear (future use).")]
    public bool isRepeatable = true;

    [Header("Visuals")]
    [Tooltip("Icon for lists and map pins (future).")]
    public Sprite icon;

    [Header("Unlock Requirements (Placeholder)")]
    [TextArea(1, 4)]
    [Tooltip("Designer notes: future quest ids, flags, items, etc.")]
    public string unlockRequirementNotes;

    [Min(0)]
    [Tooltip("0 = no level gate; future hook.")]
    public int requiredPlayerLevelPlaceholder;

    [Header("Graph")]
    [Tooltip("Other nodes reachable from this one (for progression tools later).")]
    public List<string> connectedNodeIds = new();

    [Tooltip("Optional linear 'next' hints separate from bidirectional connections.")]
    public List<string> nextNodeIds = new();

    [Header("Loading (Placeholder)")]
    [Tooltip("Future: scene to load when entering this node.")]
    public string sceneNamePlaceholder;

    [Tooltip("Future: encounter or content id resolved by a loader.")]
    public string encounterIdPlaceholder;
}
