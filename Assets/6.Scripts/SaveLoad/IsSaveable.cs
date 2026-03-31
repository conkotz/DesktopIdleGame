public interface ISaveable
{
    void SaveInto(SaveData data);
    void LoadFrom(SaveData data);
}