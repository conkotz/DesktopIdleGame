using UnityEngine;

public class PlayerSave : MonoBehaviour, ISaveable
{
    [SerializeField] private int level = 1;
    [SerializeField] private int xp = 0;

    public void SaveInto(SaveData data)
    {
        data.playerLevel = level;
        data.xp = xp;
    }

    public void LoadFrom(SaveData data)
    {
        level = data.playerLevel;
        xp = data.xp;
    }
}