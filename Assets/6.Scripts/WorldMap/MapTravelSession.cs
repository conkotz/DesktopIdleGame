using UnityEngine;

/// <summary>
/// Tracks how the player is entering the next GamePlay map so <see cref="PlayerSpawnController"/>
/// can restore a saved exit position (map UI) or use <c>SpawnPoint_Player</c> (portals / caves / signposts).
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

    public static void BeginTravel(MapNodeDefinition destination, EntryMethod entry, bool logPendingLevel = true)
    {
        if (entry == EntryMethod.MapTeleport)
            SaveManager.Instance?.StageLeavingMapExitPosition();

        _pendingEntry = entry;
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
                SaveSlotManager.SetPendingGameplaySpawnDisposition(
                    SaveSlotManager.GameplaySpawnDisposition.DefaultSpawnPoint);
                SaveSlotManager.MarkSkipApplySavedWorldPositionFromSaveOnce();
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

    public static void ClearPendingEntryMethod() => _pendingEntry = EntryMethod.Unspecified;
}
