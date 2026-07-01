using System;
using System.IO;
using UnityEngine;

public static class SaveSlotManager
{
    public const int MaxSlots = 2;
    private const string LastPlayedSlotPrefKey = "SaveSlots.LastPlayedSlotIndex";

    public static int ActiveSlotIndex { get; private set; } = -1;

    public enum SlotStartMode
    {
        None = 0,
        NewGame = 1,
        LoadGame = 2
    }

    /// <summary>
    /// How <see cref="PlayerSpawnController"/> should place the player when the GamePlay scene loads.
    /// Resume-from-save uses world coords from <see cref="SaveData"/>; level transitions use <c>SpawnPoint_Player</c>.
    /// </summary>
    public enum GameplaySpawnDisposition
    {
        DefaultSpawnPoint = 0,
        /// <summary>Continue / load game — per-map exit position, then legacy global coords, then spawn.</summary>
        RestoreSavedWorldPositionIfAvailable = 1,
        /// <summary>Map UI teleport — per-map exit position for destination only, then spawn.</summary>
        RestoreMapExitPositionIfAvailable = 2,
        /// <summary>Signpost / portal travel — spawn at the portal on this map whose destination is the map we left.</summary>
        RestoreLinkedPortalSpawnIfAvailable = 3,
    }

    private static GameplaySpawnDisposition _pendingGameplaySpawnDisposition = GameplaySpawnDisposition.DefaultSpawnPoint;
    private static bool _skipApplySavedWorldPositionFromSaveOnce;

    public static void SetPendingGameplaySpawnDisposition(GameplaySpawnDisposition disposition)
    {
        _pendingGameplaySpawnDisposition = disposition;
    }

    /// <summary>Clears spawn disposition and one-shot skip flags (e.g. when entering Bootstrap).</summary>
    public static void ResetGameplaySpawnSessionFlags()
    {
        _pendingGameplaySpawnDisposition = GameplaySpawnDisposition.DefaultSpawnPoint;
        _skipApplySavedWorldPositionFromSaveOnce = false;
    }

    public static GameplaySpawnDisposition PeekPendingGameplaySpawnDisposition() =>
        _pendingGameplaySpawnDisposition;

    public static GameplaySpawnDisposition ConsumePendingGameplaySpawnDisposition()
    {
        GameplaySpawnDisposition v = _pendingGameplaySpawnDisposition;
        _pendingGameplaySpawnDisposition = GameplaySpawnDisposition.DefaultSpawnPoint;
        return v;
    }

    /// <summary>
    /// When true, the next <see cref="PlayerSave.LoadFrom"/> must not teleport from saved world coordinates
    /// (level transition already placed the player at <c>SpawnPoint_Player</c>).
    /// </summary>
    public static void MarkSkipApplySavedWorldPositionFromSaveOnce()
    {
        _skipApplySavedWorldPositionFromSaveOnce = true;
    }

    public static bool ConsumeSkipApplySavedWorldPositionFromSaveOnce()
    {
        if (!_skipApplySavedWorldPositionFromSaveOnce)
            return false;
        _skipApplySavedWorldPositionFromSaveOnce = false;
        return true;
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
        string mapLabel = "";
        string mapId = "";
        if (data != null)
        {
            if (!string.IsNullOrWhiteSpace(data.activeMapDisplayName))
                mapLabel = data.activeMapDisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(data.activeMapNodeId))
                mapId = data.activeMapNodeId.Trim();
        }

        return new SaveGameHeader
        {
            slotIndex = slotIndex,
            hasSave = true,
            characterName = (data != null && !string.IsNullOrWhiteSpace(data.playerName)) ? data.playerName : "Player",
            playerLevel = Mathf.Max(1, data != null ? data.playerLevel : 1),
            combatPower = Mathf.Max(0, combatPower),
            gold = Mathf.Max(0, data != null ? data.gold : 0),
            sceneName = string.IsNullOrWhiteSpace(sceneName) ? "" : sceneName,
            activeMapDisplayName = mapLabel,
            activeMapNodeId = mapId,
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

        TryDeleteFile(savePath);
        TryDeleteFile(metaPath);
        TryDeleteFile(savePath + ".bak");
    }

    /// <summary>
    /// Deletes every slot save/meta/backup, activity log archive, and all <see cref="PlayerPrefs"/>.
    /// Resets runtime slot session flags so Bootstrap behaves like a first launch.
    /// </summary>
    public static void WipeAllPersistedSaveData()
    {
        for (int i = 0; i < MaxSlots; i++)
            DeleteSlot(i);

        string dataDir = Application.persistentDataPath;
        if (Directory.Exists(dataDir))
        {
            try
            {
                foreach (string path in Directory.GetFiles(dataDir, "slot_*.json"))
                    TryDeleteFile(path);
                foreach (string path in Directory.GetFiles(dataDir, "slot_*_meta.json"))
                    TryDeleteFile(path);
                foreach (string path in Directory.GetFiles(dataDir, "*.bak"))
                    TryDeleteFile(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSlotManager] Wipe scan failed: {e.Message}");
            }
        }

        TryDeleteFile(Path.Combine(dataDir, "activity_log_archive.txt"));

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        ActiveSlotIndex = -1;
        PendingStartMode = SlotStartMode.None;
        PendingNewGamePlayerName = null;
        LastConsumedStartMode = SlotStartMode.None;
        ResetGameplaySpawnSessionFlags();
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSlotManager] Failed to delete '{path}': {e.Message}");
        }
    }

    public static int GetLastPlayedSlotIndex()
    {
        int raw = PlayerPrefs.GetInt(LastPlayedSlotPrefKey, -1);
        return raw >= 0 && raw < MaxSlots ? raw : -1;
    }

    public static void MarkSlotAsLastPlayed(int slotIndex)
    {
        int clamped = Mathf.Clamp(slotIndex, 0, MaxSlots - 1);
        PlayerPrefs.SetInt(LastPlayedSlotPrefKey, clamped);
        PlayerPrefs.Save();
    }

    public static bool SwapSlots(int slotA, int slotB)
    {
        int a = Mathf.Clamp(slotA, 0, MaxSlots - 1);
        int b = Mathf.Clamp(slotB, 0, MaxSlots - 1);
        if (a == b)
            return true;

        try
        {
            SwapFilesSafe(GetSavePath(a), GetSavePath(b));
            SwapFilesSafe(GetMetaPath(a), GetMetaPath(b));

            RewriteHeaderSlotIndexIfPresent(a);
            RewriteHeaderSlotIndexIfPresent(b);

            int lastPlayed = GetLastPlayedSlotIndex();
            if (lastPlayed == a) MarkSlotAsLastPlayed(b);
            else if (lastPlayed == b) MarkSlotAsLastPlayed(a);

            if (ActiveSlotIndex == a) ActiveSlotIndex = b;
            else if (ActiveSlotIndex == b) ActiveSlotIndex = a;

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSlotManager] Failed to swap slot {a + 1} and slot {b + 1}: {e.Message}");
            return false;
        }
    }

    private static void SwapFilesSafe(string pathA, string pathB)
    {
        bool hasA = File.Exists(pathA);
        bool hasB = File.Exists(pathB);
        if (!hasA && !hasB)
            return;

        string temp = pathA + ".swap_tmp";
        if (File.Exists(temp))
            File.Delete(temp);

        if (hasA)
            File.Move(pathA, temp);
        if (hasB)
            File.Move(pathB, pathA);
        if (hasA)
            File.Move(temp, pathB);
    }

    private static void RewriteHeaderSlotIndexIfPresent(int slotIndex)
    {
        if (!TryReadHeader(slotIndex, out SaveGameHeader header) || header == null)
            return;

        header.slotIndex = slotIndex;
        WriteHeader(header);
    }
}