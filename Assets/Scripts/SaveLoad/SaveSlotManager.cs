using System.IO;
using UnityEngine;

public static class SaveSlotManager
{
    public const int MaxSlots = 2;

    public static int ActiveSlotIndex { get; private set; } = -1;

    public static void SetActiveSlot(int slotIndex)
    {
        ActiveSlotIndex = Mathf.Clamp(slotIndex, 0, MaxSlots - 1);
        Debug.Log($"[SaveSlotManager] Active slot set to {ActiveSlotIndex}");
    }

    public static string GetSavePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"slot_{slotIndex}.json");
    }

    public static string GetMetaPath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"slot_{slotIndex}_meta.json");
    }

    public static bool HasSave(int slotIndex)
    {
        return File.Exists(GetSavePath(slotIndex));
    }

    public static void DeleteSlot(int slotIndex)
    {
        string savePath = GetSavePath(slotIndex);
        string metaPath = GetMetaPath(slotIndex);

        if (File.Exists(savePath))
            File.Delete(savePath);

        if (File.Exists(metaPath))
            File.Delete(metaPath);

        Debug.Log($"[SaveSlotManager] Deleted slot {slotIndex}");
    }
}