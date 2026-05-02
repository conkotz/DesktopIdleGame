using UnityEngine;

/// <summary>
/// Persists a flag when the player dies in GamePlay so NPC conditional dialogue can run after respawn
/// (or on the next session if they quit before talking to the NPC). Cleared when that dialogue is shown.
/// </summary>
public static class NpcPostDeathRespawnDialogueStore
{
    private static bool _pending;

    public static bool IsPending => _pending;

    internal static void ApplyFromSaveData(SaveData data)
    {
        _pending = data != null && data.npcPostDeathRespawnDialoguePending;
    }

    internal static void WriteInto(SaveData data)
    {
        if (data == null)
            return;
        data.npcPostDeathRespawnDialoguePending = _pending;
    }

    /// <summary>Call from player death before respawn save/transition.</summary>
    public static void MarkPendingAndSave()
    {
        _pending = true;
        SaveManager.Instance?.Save();
    }

    /// <summary>After plain NPC dialogue that used the After Death And Respawn condition is shown.</summary>
    public static void ClearPendingAndSave()
    {
        if (!_pending)
            return;
        _pending = false;
        SaveManager.Instance?.Save();
    }
}
