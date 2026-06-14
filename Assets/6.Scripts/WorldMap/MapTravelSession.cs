using UnityEngine;

/// <summary>
/// Tracks how the player is entering the next GamePlay map so <see cref="PlayerSpawnController"/>
/// can restore a saved exit position (map UI) or spawn at the linked return signpost (portal travel).
/// </summary>
public static class MapTravelSession
{
    public enum EntryMethod
    {
        Unspecified = 0,
        /// <summary>Locations list, world map teleport, quest journal Enter map, town hotkey.</summary>
        MapTeleport = 1,
        /// <summary>In-scene portal, cave, signpost, NPC scripted travel.</summary>
        InWorldEntrance = 2
    }

    private static EntryMethod _pendingEntry = EntryMethod.Unspecified;
    private static string _pendingSourceMapNodeId;

    public static void BeginTravel(
        MapNodeDefinition destination,
        EntryMethod entry,
        bool logPendingLevel = true,
        string sourceMapNodeId = null)
    {
        if (entry == EntryMethod.MapTeleport)
            SaveManager.Instance?.StageLeavingMapExitPosition();

        _pendingEntry = entry;
        _pendingSourceMapNodeId = entry == EntryMethod.InWorldEntrance && !string.IsNullOrWhiteSpace(sourceMapNodeId)
            ? sourceMapNodeId.Trim()
            : null;
        ActiveLevelContext.SetPendingLevel(destination, logToConsole: logPendingLevel);
    }

  /// <summary>Call immediately before unloading GamePlay (after shrink, before save/load).</summary>
    public static void ApplyPendingSpawnDispositionBeforeSceneLoad()
    {
        EntryMethod entry = ConsumePendingEntryMethod();
        switch (entry)
        {
            case EntryMethod.MapTeleport:
                SaveSlotManager.SetPendingGameplaySpawnDisposition(
                    SaveSlotManager.GameplaySpawnDisposition.RestoreMapExitPositionIfAvailable);
                break;
            case EntryMethod.InWorldEntrance:
                if (!string.IsNullOrWhiteSpace(_pendingSourceMapNodeId))
                {
                    SaveSlotManager.SetPendingGameplaySpawnDisposition(
                        SaveSlotManager.GameplaySpawnDisposition.RestoreLinkedPortalSpawnIfAvailable);
                }
                else
                {
                    SaveSlotManager.SetPendingGameplaySpawnDisposition(
                        SaveSlotManager.GameplaySpawnDisposition.DefaultSpawnPoint);
                    SaveSlotManager.MarkSkipApplySavedWorldPositionFromSaveOnce();
                }
                break;
            default:
                break;
        }
    }

    public static EntryMethod ConsumePendingEntryMethod()
    {
        EntryMethod m = _pendingEntry;
        _pendingEntry = EntryMethod.Unspecified;
        return m;
    }

    /// <summary>Non-destructive read for shrink timing before <see cref="ApplyPendingSpawnDispositionBeforeSceneLoad"/> consumes the entry.</summary>
    public static EntryMethod PeekPendingEntryMethod() => _pendingEntry;

    public static void ClearPendingEntryMethod()
    {
        _pendingEntry = EntryMethod.Unspecified;
        _pendingSourceMapNodeId = null;
    }

    public static string ConsumePendingSourceMapNodeId()
    {
        string id = _pendingSourceMapNodeId;
        _pendingSourceMapNodeId = null;
        return id;
    }

    public static void ClearPendingSourceMapNodeId() => _pendingSourceMapNodeId = null;
}
