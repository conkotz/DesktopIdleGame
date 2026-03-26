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
    public string lastSavedUtc;
    public float playTimeSeconds;
}