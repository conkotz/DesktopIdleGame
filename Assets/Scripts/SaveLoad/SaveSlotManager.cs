using System;
using System.IO;
using UnityEngine;

public static class SaveSlotManager
{
    public const int MaxSlots = 2;

    public static int ActiveSlotIndex { get; private set; } = -1;

    public enum SlotStartMode
    {
        None = 0,
        NewGame = 1,
        LoadGame = 2
    }

    /// <summary>
    /// Runtime-only flag set by BootMenu to tell gameplay bootstrap whether to start fresh or load a save.
    /// This should be consumed (cleared) once the gameplay scene decides its path.
    /// </summary>
    public static SlotStartMode PendingStartMode { get; private set; } = SlotStartMode.None;

    /// <summary>
    /// Debug/runtime visibility of the most recent consumed start mode.
    /// Useful for scene-entry logic (e.g., spawn walk-in) that should differ for NewGame vs LoadGame.
    /// </summary>
    public static SlotStartMode LastConsumedStartMode { get; private set; } = SlotStartMode.None;
    public static string PendingNewGamePlayerName { get; private set; }

    public static void SetActiveSlot(int slotIndex)
    {
        ActiveSlotIndex = Mathf.Clamp(slotIndex, 0, MaxSlots - 1);
    }

    public static void SetPendingStartMode(SlotStartMode mode)
    {
        PendingStartMode = mode;
    }

    public static SlotStartMode ConsumePendingStartMode()
    {
        var mode = PendingStartMode;
        PendingStartMode = SlotStartMode.None;
        LastConsumedStartMode = mode;
        return mode;
    }

    public static void SetPendingNewGamePlayerName(string playerName)
    {
        PendingNewGamePlayerName = string.IsNullOrWhiteSpace(playerName) ? null : playerName.Trim();
    }

    public static string ConsumePendingNewGamePlayerName()
    {
        string value = PendingNewGamePlayerName;
        PendingNewGamePlayerName = null;
        return value;
    }

    public static string GetSavePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"slot_{slotIndex}.json");
    }

    public static string GetMetaPath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"slot_{slotIndex}_meta.json");
    }

    public static bool TryReadHeader(int slotIndex, out SaveGameHeader header)
    {
        header = null;

        try
        {
            string path = GetMetaPath(slotIndex);
            if (!File.Exists(path))
                return false;

            string json = File.ReadAllText(path);
            header = JsonUtility.FromJson<SaveGameHeader>(json);
            return header != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSlotManager] Failed to read header for slot {slotIndex}: {e.Message}");
            header = null;
            return false;
        }
    }

    public static void WriteHeader(SaveGameHeader header)
    {
        if (header == null) return;

        try
        {
            string json = JsonUtility.ToJson(header, true);
            File.WriteAllText(GetMetaPath(header.slotIndex), json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSlotManager] Failed to write header for slot {header.slotIndex}: {e.Message}");
        }
    }

    public static SaveGameHeader BuildHeaderFromSaveData(int slotIndex, SaveData data, string sceneName, DateTime utcNow, int combatPower)
    {
        return new SaveGameHeader
        {
            slotIndex = slotIndex,
            hasSave = true,
            characterName = (data != null && !string.IsNullOrWhiteSpace(data.playerName)) ? data.playerName : "Player",
            playerLevel = Mathf.Max(1, data != null ? data.playerLevel : 1),
            combatPower = Mathf.Max(0, combatPower),
            gold = Mathf.Max(0, data != null ? data.gold : 0),
            sceneName = string.IsNullOrWhiteSpace(sceneName) ? "" : sceneName,
            lastSavedUtc = utcNow.ToString("u"),
            playTimeSeconds = 0f
        };
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
    }
}