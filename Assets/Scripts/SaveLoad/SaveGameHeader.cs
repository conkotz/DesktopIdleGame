using UnityEngine;

[System.Serializable]
public class SaveGameHeader
{
    public int slotIndex;
    public bool hasSave;
    public string characterName;
    public int playerLevel;
    public int combatPower;
    public int gold;
    public string sceneName;
    [Tooltip("Human-readable location (from MapNodeDefinition), for slot UI.")]
    public string activeMapDisplayName;
    public string activeMapNodeId;
    public string lastSavedUtc;
    public float playTimeSeconds;
}