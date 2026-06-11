using UnityEngine;

/// <summary>
/// Persists a flag when the player dies in GamePlay so NPC conditional dialogue can run after respawn
/// (or on the next session if they quit before talking to the NPC). Cleared when that dialogue is shown.
/// Also stores which map node the death happened on so "After Death" lines can require that node, not the map you are on when the NPC speaks.
/// </summary>
public static class NpcPostDeathRespawnDialogueStore
{
    private static bool _pending;
    private static string _deathOccurredOnMapNodeId;

    public static bool IsPending => _pending;

    /// <summary>Non-empty when <see cref="IsPending"/> was set from a death with a known active level node id.</summary>
    public static string DeathOccurredOnMapNodeId => _deathOccurredOnMapNodeId ?? "";

    /// <summary>Active gameplay level node id at call time (same rules as NPC conditional "current map").</summary>
    public static string ResolveCurrentGameplayMapNodeId()
    {
        if (ActiveLevelContext.Current != null && !string.IsNullOrWhiteSpace(ActiveLevelContext.Current.nodeId))
            return ActiveLevelContext.Current.nodeId.Trim();

        GameplayLevelBootstrapper boot = GameplayLevelBootstrapper.Instance;
        if (boot != null && boot.ActiveDefinition != null && !string.IsNullOrWhiteSpace(boot.ActiveDefinition.nodeId))
            return boot.ActiveDefinition.nodeId.Trim();

        return "";
    }

    internal static void ApplyFromSaveData(SaveData data)
    {
        if (data == null)
        {
            _pending = false;
            _deathOccurredOnMapNodeId = "";
            return;
        }

        _pending = data.npcPostDeathRespawnDialoguePending;
        if (_pending)
            _deathOccurredOnMapNodeId = (data.npcPostDeathRespawnDialogueDeathNodeId ?? "").Trim();
        else
            _deathOccurredOnMapNodeId = "";
    }

    internal static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

        data.npcPostDeathRespawnDialoguePending = _pending;
        data.npcPostDeathRespawnDialogueDeathNodeId = _deathOccurredOnMapNodeId ?? "";
    }

    /// <summary>Call from player death before respawn save/transition.</summary>
    public static void MarkPendingAndSave(string diedOnMapNodeId = null)
    {
        _pending = true;
        _deathOccurredOnMapNodeId = string.IsNullOrWhiteSpace(diedOnMapNodeId) ? "" : diedOnMapNodeId.Trim();
        NpcOneWayDialogueQueueStore.PrepareOneWayQueuesForPlayerDeath(_deathOccurredOnMapNodeId);
        SaveManager.Instance?.Save();
        // Full Save() returns early while save data is being applied; still persist these flags to disk.
        SaveManager.Instance?.FlushNpcPostDeathDialogueToDisk();
    }

    /// <summary>After plain NPC dialogue that used the After Death And Respawn condition is shown.</summary>
    public static void ClearPendingAndSave()
    {
        if (!_pending)
            return;

        _pending = false;
        _deathOccurredOnMapNodeId = "";
        SaveManager.Instance?.Save();
        SaveManager.Instance?.FlushNpcPostDeathDialogueToDisk();
    }

    /// <summary>Used when rehydration applied a stale save that cleared in-memory post-death state mid-respawn.</summary>
    internal static void RestorePendingState(string deathNodeId)
    {
        _pending = true;
        _deathOccurredOnMapNodeId = string.IsNullOrWhiteSpace(deathNodeId) ? "" : deathNodeId.Trim();
    }
}
