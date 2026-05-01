using UnityEngine;

/// <summary>
/// Session-scoped map aggro latch used by <see cref="LevelEnemyAggroMode.CalmUntilPlayerAggressive"/>.
/// Resets whenever the active level starts in <see cref="GameplayLevelBootstrapper"/>.
/// </summary>
public static class LevelAggroState
{
    private static string _activeNodeId = string.Empty;
    private static bool _waveAggroLatched;

    public static event System.Action<string> AggroPulseTriggered;

    public static void ResetForLevel(MapNodeDefinition def)
    {
        _activeNodeId = def != null && !string.IsNullOrWhiteSpace(def.nodeId)
            ? def.nodeId.Trim()
            : string.Empty;
        _waveAggroLatched = false;
    }

    public static bool IsWaveAggroLatched(MapNodeDefinition def)
    {
        string id = def != null && !string.IsNullOrWhiteSpace(def.nodeId)
            ? def.nodeId.Trim()
            : string.Empty;
        return !string.IsNullOrEmpty(id) &&
               string.Equals(_activeNodeId, id, System.StringComparison.Ordinal) &&
               _waveAggroLatched;
    }

    public static void TriggerPlayerAggression(MapNodeDefinition def)
    {
        string id = def != null && !string.IsNullOrWhiteSpace(def.nodeId)
            ? def.nodeId.Trim()
            : string.Empty;
        if (string.IsNullOrEmpty(id))
            return;

        if (!string.Equals(_activeNodeId, id, System.StringComparison.Ordinal))
            _activeNodeId = id;

        // Pulse active enemies right now (future spawns won't receive this past pulse).
        AggroPulseTriggered?.Invoke(id);

        // Persist aggression only when simple waves are currently running.
        if (SimpleCombatWaveDirector.IsSimpleWavesRunningFor(def))
            _waveAggroLatched = true;
    }
}
